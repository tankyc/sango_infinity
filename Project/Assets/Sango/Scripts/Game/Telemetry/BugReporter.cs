/*
 * 文件名：BugReporter.cs
 * 描述：客户端错误日志采集（落盘 + 上报）。
 *
 * 【为什么需要它】
 *   Sango.Log 只是 UnityEngine.Debug 的薄封装（Framework/Log/Log.cs），
 *   不写文件、不带堆栈、不上报；线上包一旦报错，开发只能靠玩家截图。
 *   这里挂一个全局钩子，把 Error / Exception / Assert 留下来并送到后台。
 *
 * 【接入方式】
 *   不需要改任何业务代码：RuntimeInitializeOnLoadMethod 会在场景加载后自动初始化，
 *   订阅 Application.logMessageReceivedThreaded 即可收到所有线程的日志。
 *
 * 【几个关键取舍】
 *   1. 只收 Error / Exception / Assert —— Warning 量太大，收了会把队列冲爆；
 *   2. 用 logMessageReceivedThreaded 而不是同步版本 —— 子线程异常也要能抓到；
 *   3. 回调可能来自非主线程，所以入队必须加锁，网络请求统一交给主线程的
 *      TelemetryRunner 协程发送（UnityWebRequest 只能在主线程创建）；
 *   4. 相同错误在 DedupeWindow 秒内只记一条并累加 count，
 *      避免 Update 里每帧报同一个错把服务端刷爆。
 */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Sango.Core.Telemetry
{
    /// <summary>
    /// 启用的模组快照。字段名与 PlayerInsight/server/store.js 的 normalizeMods 一一对应。
    ///
    /// 【为什么只留这三个字段】模组描述（Description）动辄上千字，每条错误都带一份
    /// 会把上报体顶爆。排障真正需要的是"装了哪些模组的哪个版本"：
    ///   · id 是唯一可用于筛选的键（name 可以重名）；
    ///   · version 用于排除"某个模组某版本才有的问题"。
    /// </summary>
    [Serializable]
    public class ModEntry
    {
        /// <summary>模组 Id（英文唯一标识）</summary>
        public string id;

        /// <summary>模组名（可为中文）</summary>
        public string name;

        /// <summary>模组版本</summary>
        public string version;
    }

    /// <summary>
    /// 一条待上报的错误。字段名与 PlayerInsight 服务端的 normalize() 一一对应，
    /// 改字段时两边必须同步，否则后台会落到默认值。
    /// </summary>
    [Serializable]
    public class BugEntry
    {
        /// <summary>发生时间（Unix 毫秒）</summary>
        public long occurredAt;

        /// <summary>级别：Error / Exception / Assert</summary>
        public string level;

        /// <summary>分类（沿用 Sango.Log 的 LogType 名称）</summary>
        public string tag;

        /// <summary>错误正文</summary>
        public string message;

        /// <summary>调用堆栈</summary>
        public string stackTrace;

        /// <summary>
        /// 出现次数。客户端只做短窗口折叠（同一个错误连着刷屏时合并），
        /// 长期去重由服务端按"内容+堆栈"指纹合并，所以这个值传到后台会被累加。
        /// </summary>
        public int count = 1;

        /// <summary>错误指纹（内容+堆栈），仅本地去重用，不参与序列化上报</summary>
        [NonSerialized]
        public int fingerprint;

        public string appVersion;

        /// <summary>平台枚举名（RuntimePlatform，如 WindowsPlayer / Android），用于筛选与统计</summary>
        public string platform;

        /// <summary>平台可读名（Windows / Android / iOS / WebGL / Windows 编辑器），人看这个</summary>
        public string platformName;

        /// <summary>Unity 版本，能直接排除"某版本引擎的 bug"</summary>
        public string unityVersion;

        /// <summary>是否在编辑器里跑的（内部测试包与真机包要分得清）</summary>
        public bool isEditor = false;

        /// <summary>CPU 型号（区分 x86 / arm64）</summary>
        public string cpuType;

        /// <summary>内存（MB）</summary>
        public int memoryMB;

        /// <summary>显卡名（PC 端花屏、着色器报错时用得上）</summary>
        public string gpuName;

        /// <summary>分辨率（如 1920x1080），UI 错位类问题要看</summary>
        public string screenSize;

        /// <summary>系统语言（本地化相关的错误用得上）</summary>
        public string language;

        public string deviceId;
        public string deviceModel;
        public string osVersion;
        public string playerId;
        public string playerName;
        public string scenario;
        public string scene;

        /// <summary>
        /// 玩家启用的模组列表（按加载顺序）。
        /// 老客户端没有这个字段，后台按"未采集"展示 —— 不能用空数组推断"没装模组"。
        /// </summary>
        public List<ModEntry> mods = new List<ModEntry>();
    }

    /// <summary>批量上报外壳（服务端支持 items 数组）</summary>
    [Serializable]
    public class BugBatch
    {
        public List<BugEntry> items = new List<BugEntry>();
    }

    /// <summary>
    /// 错误采集器。对外只需要 ReportException（可选），其余自动运行。
    /// </summary>
    public static class BugReporter
    {
        /// <summary>富文本标签（Feature.Name 之类会带 &lt;color&gt;）</summary>
        private static readonly Regex RichTextRegex = new Regex("<[^>]+>", RegexOptions.Compiled);

        private static readonly object Gate = new object();

        /// <summary>待发送队列（按发生顺序）</summary>
        private static readonly List<BugEntry> Queue = new List<BugEntry>();

        /// <summary>去重表：错误指纹 → 队列里的那条记录</summary>
        private static readonly Dictionary<int, BugEntry> Recent = new Dictionary<int, BugEntry>();

        /// <summary>本地日志写入锁</summary>
        private static readonly object FileGate = new object();

        private static bool bootstrapped;

        /// <summary>当前待发送条数（调试用）</summary>
        public static int PendingCount
        {
            get
            {
                lock (Gate)
                {
                    return Queue.Count;
                }
            }
        }

        /// <summary>
        /// 自动初始化。AfterSceneLoad 而不是 BeforeSceneLoad：
        /// 此时 Sango.Log 之类的静态类已完成初始化，避免早期错误因依赖未就绪而丢失上下文。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (bootstrapped) return;
            bootstrapped = true;

            if (TelemetryConfig.EnableLocalLog)
            {
                CleanupLocalLogs();
            }

            Application.logMessageReceivedThreaded += OnLogMessage;

            // 协程宿主：隐藏物体，随场景切换常驻（TroopTelemetry 也复用它）
            TelemetryRunner.Ensure();

            Sango.Log.Info("[Telemetry] 错误采集已启动");
            Sango.Log.Info($"[Telemetry] 上报地址：{TelemetryConfig.IngestUrl}");
        }

        /// <summary>
        /// 全局日志回调。可能在子线程触发，这里只做"收集 + 落盘"，
        /// 真正的网络发送交给主线程的 TelemetryRunner。
        /// </summary>
        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!TelemetryConfig.EnableBugReport) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;

            try
            {
                BugEntry entry = BuildEntry(condition, stackTrace, type);
                if (entry == null) return;

                bool merged = TryMerge(entry);
                if (!merged)
                {
                    if (TelemetryConfig.EnableLocalLog) AppendToLocalFile(entry);
                }
            }
            catch (Exception)
            {
                // 采集本身出错绝不能再抛，否则会递归触发日志回调
            }
        }

        /// <summary>
        /// 手动上报一个异常（业务里 catch 到但不想走 Debug.LogError 时用）。
        /// </summary>
        public static void ReportException(Exception e, string tag = "Script")
        {
            if (e == null) return;

            BugEntry entry = BuildEntry(e.Message, e.StackTrace, LogType.Exception);
            if (entry == null) return;
            entry.tag = tag;

            if (!TryMerge(entry) && TelemetryConfig.EnableLocalLog)
            {
                AppendToLocalFile(entry);
            }
        }

        /// <summary>组装一条完整记录（补齐设备/玩家上下文）</summary>
        private static BugEntry BuildEntry(string condition, string stackTrace, LogType type)
        {
            string message = string.IsNullOrEmpty(condition) ? string.Empty : condition;
            string stack = stackTrace ?? string.Empty;

            if (message.Length == 0 && stack.Length == 0) return null;

            Scenario scenario = Scenario.Cur;

            BugEntry entry = new BugEntry
            {
                occurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                level = type.ToString(),
                tag = string.Empty,
                message = Truncate(message, 4000),
                stackTrace = Truncate(stack, 200 * 1024),
                count = 1,
                appVersion = Application.version,
                platform = Application.platform.ToString(),
                platformName = PlatformNameOf(Application.platform),
                unityVersion = Application.unityVersion,
                isEditor = Application.isEditor,
                cpuType = SystemInfo.processorType,
                memoryMB = SystemInfo.systemMemorySize,
                gpuName = SystemInfo.graphicsDeviceName,
                screenSize = $"{Screen.width}x{Screen.height}",
                language = Application.systemLanguage.ToString(),
                deviceId = SafeDeviceId(),
                deviceModel = SystemInfo.deviceModel,
                osVersion = SystemInfo.operatingSystem,
                playerName = PlayerNameOf(scenario),
                playerId = SafeDeviceId(),
                scenario = scenario != null && scenario.Name != null ? scenario.Name : string.Empty,
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                mods = EnabledMods(),
            };

            return entry;
        }

        /// <summary>
        /// 去重：窗口内出现过的相同错误只累加次数。
        /// @returns true 表示已合并（不需要再入队/落盘）
        /// </summary>
        private static bool TryMerge(BugEntry entry)
        {
            int fingerprint = Fingerprint(entry.message, entry.stackTrace);
            long now = entry.occurredAt;
            long window = (long)(TelemetryConfig.DedupeWindow * 1000);

            lock (Gate)
            {
                BugEntry existing;
                if (Recent.TryGetValue(fingerprint, out existing) && existing != null
                    && now - existing.occurredAt <= window)
                {
                    existing.count += 1;
                    existing.occurredAt = now;
                    return true;
                }

                // 新错误入队
                entry.fingerprint = fingerprint;
                Queue.Add(entry);
                Recent[fingerprint] = entry;

                // 队列超限：丢最旧的，避免断网期间内存无限增长
                while (Queue.Count > TelemetryConfig.MaxQueue)
                {
                    Queue.RemoveAt(0);
                }

                return false;
            }
        }

        /// <summary>取出一批待发送记录（从队列头部移除）</summary>
        internal static List<BugEntry> TakeBatch(int max)
        {
            List<BugEntry> batch = new List<BugEntry>();

            lock (Gate)
            {
                int take = Mathf.Min(max, Queue.Count);
                for (int i = 0; i < take; i++)
                {
                    BugEntry entry = Queue[i];
                    batch.Add(entry);

                    // 出队必须同时摘掉指纹：否则之后同样的错误会继续在这条"已发送"的
                    // 记录上累加 count，而它不会再发第二次，次数就丢了。
                    // 交给服务端按指纹合并，才能保证后台的次数是准的。
                    if (Recent.TryGetValue(entry.fingerprint, out BugEntry cached) && cached == entry)
                    {
                        Recent.Remove(entry.fingerprint);
                    }
                }

                if (take > 0) Queue.RemoveRange(0, take);

                // 清掉已经被取走、又超出窗口的指纹，防止 Recent 无限增长
                if (Recent.Count > TelemetryConfig.MaxQueue * 2)
                {
                    Recent.Clear();
                }
            }

            return batch;
        }

        /// <summary>发送失败时把整批塞回队头，等下一轮重试</summary>
        internal static void ReturnToQueue(List<BugEntry> batch)
        {
            if (batch == null || batch.Count == 0) return;

            lock (Gate)
            {
                // 回到队列意味着"还没发出去"，指纹要重新登记，
                // 否则窗口内同样的错误会再入队一条，后台就会重复计数
                for (int i = 0; i < batch.Count; i++)
                {
                    BugEntry entry = batch[i];
                    if (entry != null && entry.fingerprint != 0)
                    {
                        Recent[entry.fingerprint] = entry;
                    }
                }

                Queue.InsertRange(0, batch);

                while (Queue.Count > TelemetryConfig.MaxQueue)
                {
                    Queue.RemoveAt(Queue.Count - 1);
                }
            }
        }

        /// <summary>错误指纹（消息 + 堆栈），用于去重</summary>
        private static int Fingerprint(string message, string stack)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (message != null ? message.GetHashCode() : 0);
                hash = hash * 31 + (stack != null ? stack.GetHashCode() : 0);
                return hash;
            }
        }

        /// <summary>
        /// 当前启用的模组列表（按加载顺序）。组队上报复用。
        ///
        /// 【为什么要 try 包住】ModManager 是懒加载单例：编辑器预览、主菜单早期、
        /// Init 之前的任何一次调用，mEnabledModList 都可能是 null
        /// （ModManager.cs:636-639 对同样的场景做过判空）。采集代码绝不能因为
        /// 取模组列表失败把游戏搞崩，取不到就按空列表上报。
        ///
        /// 【为什么写全名】Mod 既是命名空间（Sango.Mod）又是类型（Sango.Mod.Mod），
        /// 直接 using Sango.Mod 会让 `Mod[]` 被当成命名空间引用而编译报错。
        /// </summary>
        /// <returns>启用的模组快照；取不到时返回空列表</returns>
        internal static List<ModEntry> EnabledMods()
        {
            List<ModEntry> list = new List<ModEntry>();

            try
            {
                Sango.Mod.ModManager manager = Sango.Mod.ModManager.Instance;
                if (manager == null) return list;

                Sango.Mod.Mod[] mods = manager.GetEnabledMods();
                if (mods == null) return list;

                for (int i = 0; i < mods.Length; i++)
                {
                    Sango.Mod.Mod mod = mods[i];
                    if (mod == null) continue;

                    list.Add(new ModEntry
                    {
                        id = mod.Id ?? string.Empty,
                        name = mod.Name ?? string.Empty,
                        version = mod.Version ?? string.Empty,
                    });
                }
            }
            catch (Exception)
            {
                // 模组信息缺失不能影响错误上报本身（采样本就是为了排障）
            }

            return list;
        }

        /// <summary>设备唯一标识（部分平台可能为空，兜底用机型 + 系统）。组队上报复用</summary>
        internal static string SafeDeviceId()
        {
            string id = SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(id) && id != "n/a") return id;
            return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}";
        }

        /// <summary>
        /// 取玩家势力。
        /// 用 <see cref="Force.IsPlayer"/>（由剧本 playerForceList 设定，Scenario.cs:893-906）
        /// 而不是 CurRunForce（当前行动势力，AI 回合拿到的是 AI），否则错误会被算到 AI 头上。
        /// </summary>
        /// <returns>玩家势力；剧本未加载时返回 null</returns>
        internal static Force PlayerForceOf(Scenario scenario)
        {
            if (scenario == null) return null;

            // SangoObjectSet.Find 内部按 id 扫整个数组并跳过空位（SangoObjectSet.cs:151-160），
            // 比按 Count 下标遍历安全：Count 返回的是最大已用 id，末位那条会被漏掉
            if (scenario.forceSet != null)
            {
                Force player = scenario.forceSet.Find(x => x.IsPlayer);
                if (player != null) return player;
            }

            // 兜底：主菜单/测试场景里没有 playerForceList，退回当前行动势力
            return scenario.CurRunForce;
        }

        /// <summary>玩家名：取玩家势力的君主名（单人游戏没有账号体系）。组队上报复用</summary>
        internal static string PlayerNameOf(Scenario scenario)
        {
            Force force = PlayerForceOf(scenario);
            if (force == null) return string.Empty;

            // Force 没有 Leader 属性，君主是 mGovernor（Force.cs:187，由 Governor 字段解析）
            if (force.mGovernor != null) return force.mGovernor.Name;

            return force.Name;
        }

        /// <summary>
        /// 平台枚举 → 可读名。RuntimePlatform 的枚举名（WindowsPlayer / IPhonePlayer）
        /// 对排障的人不直观，这里归一成玩家嘴里的"平台"。
        /// </summary>
        /// <param name="platform">Application.platform</param>
        /// <returns>可读平台名</returns>
        internal static string PlatformNameOf(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "Windows";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "macOS";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "Linux";
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.PS4:
                    return "PS4";
                case RuntimePlatform.PS5:
                    return "PS5";
                case RuntimePlatform.XboxOne:
                    return "Xbox One";
                case RuntimePlatform.Switch:
                    return "Switch";
                default:
                    return platform.ToString();
            }
        }

        /// <summary>裁掉富文本标签（特技名带 &lt;color&gt; 时有用）</summary>
        internal static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return RichTextRegex.Replace(text, string.Empty);
        }

        /// <summary>字符串截断（防止单条日志把请求体顶爆）</summary>
        private static string Truncate(string text, int limit)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= limit ? text : text.Substring(0, limit);
        }

        // ---------------------------------------------------------------- 本地落盘

        /// <summary>本地日志目录</summary>
        internal static string LocalLogDir
        {
            get { return System.IO.Path.Combine(Application.persistentDataPath, "telemetry"); }
        }

        /// <summary>追加写本地日志（网络不通时仍能在玩家手机上找到证据）</summary>
        private static void AppendToLocalFile(BugEntry entry)
        {
            try
            {
                string dir = LocalLogDir;
                // Sango.Framework 里另有 Directory/File/Path 三个同名静态类（Framework/IO），
                // 在本命名空间下会遮蔽 System.IO，必须写全名
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                string file = System.IO.Path.Combine(dir, $"bug-{DateTime.Now:yyyyMMdd}.jsonl");
                string line = JsonUtility.ToJson(entry);

                lock (FileGate)
                {
                    System.IO.File.AppendAllText(file, line + "\n");
                }
            }
            catch (Exception)
            {
                // 磁盘满/权限不足都忽略，采集不能影响游戏
            }
        }

        /// <summary>清理超过保留天数的本地日志</summary>
        private static void CleanupLocalLogs()
        {
            try
            {
                string dir = LocalLogDir;
                if (!System.IO.Directory.Exists(dir)) return;

                DateTime deadline = DateTime.Now.AddDays(-TelemetryConfig.LocalLogKeepDays);
                foreach (string file in System.IO.Directory.GetFiles(dir, "bug-*.jsonl", System.IO.SearchOption.TopDirectoryOnly))
                {
                    if (System.IO.File.GetLastWriteTime(file) < deadline)
                    {
                        System.IO.File.Delete(file);
                    }
                }
            }
            catch (Exception)
            {
                // 清理失败不影响主流程
            }
        }
    }
}
