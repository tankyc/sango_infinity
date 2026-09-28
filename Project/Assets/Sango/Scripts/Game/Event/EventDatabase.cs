/*
 * 文件名：EventDatabase.cs
 * 描述：事件库——三源加载全部剧本事件，并建立按触发时机的索引
 * 创建日期：2026-09-26
 * 最后修改：2026-09-26
 */

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件库：负责"把事件从磁盘读进来"和"按触发时机快速取出候选"。
    ///
    /// 【三源加载】后加载的按 Id 覆盖先加载的：
    ///   1. 内置   &lt;ContentRootPath&gt;/Data/ScenarioEvent/**/*.json
    ///   2. 自建   &lt;CustomEditRootPath&gt;/Data/ScenarioEvent/**/*.json
    ///   3. Mod    &lt;ModRootPath&gt;/**/Data/ScenarioEvent/**/*.json
    ///
    /// 【为什么要有索引】项目最终会有数百个历史事件。
    /// 若每次 hook 都全量求值条件树，一回合几十次 hook × 数百事件 = 上万次求值，
    /// 帧率会被吃穿。因此按"触发时机"建一级索引，把候选缩到个位数，
    /// 再用 Trigger.EarliestYear / LatestYear 做二级裁剪（年份不符的直接跳过）。
    ///
    /// 【为什么不用 ModManager.EnumFiles】它按"相对文件路径"枚举，
    /// 适合"每个 Mod 一个 NameConfig.json"这种场景；而事件是按目录组织的一批文件，
    /// 直接递归扫物理目录更直接，也避免依赖 ModManager 的内部路径约定。
    /// </summary>
    public class EventDatabase
    {
        /// <summary>事件文件所在目录（相对于内容根 / 自建根 / Mod 根）</summary>
        public const string EventFolder = "Data/ScenarioEvent";

        /// <summary>全部事件（按 Id 升序，便于编辑器与调试面板稳定展示）</summary>
        public readonly List<EventDefinition> allEvents = new List<EventDefinition>();

        /// <summary>Id → 事件</summary>
        readonly Dictionary<int, EventDefinition> byId = new Dictionary<int, EventDefinition>();

        /// <summary>触发时机 → 候选事件</summary>
        readonly Dictionary<EventTriggerKind, List<EventDefinition>> byKind =
            new Dictionary<EventTriggerKind, List<EventDefinition>>();

        /// <summary>是否已加载</summary>
        public bool Loaded { get; private set; }

        /// <summary>加载报告（中文，含读到的文件数与跳过原因，供调试面板显示）</summary>
        public string LoadReport { get; private set; }

        /// <summary>事件总数</summary>
        public int Count { get { return allEvents.Count; } }

        #region 加载

        /// <summary>
        /// 从三个来源加载全部事件。重复调用会先清空，因此可安全用于"重载事件"。
        /// </summary>
        /// <returns>成功加载的事件数</returns>
        public int Load()
        {
            allEvents.Clear();
            byId.Clear();
            byKind.Clear();
            Loaded = false;

            int fileCount = 0;
            int errorCount = 0;

            // 顺序即优先级：内置 → 自建 → Mod（后者覆盖前者）
            // 注意必须写全限定名 Sango.Path：本文件有 using System.IO，
            // 而 C# 的名字查找在**当前命名空间声明**这一层就会命中 using 引入的 System.IO.Path，
            // 于是 Path.ContentRootPath 会报"Path 不含 ContentRootPath"。
            fileCount += LoadDirectory(Sango.Path.ContentRootPath + "/" + EventFolder, "内置", ref errorCount);
            fileCount += LoadDirectory(Sango.Path.CustomEditRootPath + "/" + EventFolder, "自建", ref errorCount);
            fileCount += LoadModDirectory(Sango.Path.ModRootPath, ref errorCount);

            allEvents.Sort((a, b) => a.Id.CompareTo(b.Id));
            RebuildIndex();

            Loaded = true;
            LoadReport = $"已加载 {allEvents.Count} 个事件（{fileCount} 个文件，{errorCount} 个文件解析失败）";
            Log.Info("剧本事件库：" + LoadReport);
            return allEvents.Count;
        }

        /// <summary>递归加载一个目录下的全部事件 JSON</summary>
        /// <param name="directory">物理目录</param>
        /// <param name="sourceTag">来源标记</param>
        /// <param name="errorCount">累计解析失败数</param>
        /// <returns>读到的文件数</returns>
        int LoadDirectory(string directory, string sourceTag, ref int errorCount)
        {
            if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
                return 0;

            int fileCount = 0;
            string[] files = System.IO.Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                fileCount++;
                if (!LoadFile(files[i], sourceTag)) errorCount++;
            }
            return fileCount;
        }

        /// <summary>
        /// 扫描 Mod 根目录下的所有事件目录。
        /// 只认路径里带 "/Data/ScenarioEvent/" 的文件，避免把 Mod 的其它 JSON 当成事件读。
        /// </summary>
        /// <param name="modRoot">Mod 根目录</param>
        /// <param name="errorCount">累计解析失败数</param>
        /// <returns>读到的文件数</returns>
        int LoadModDirectory(string modRoot, ref int errorCount)
        {
            if (string.IsNullOrEmpty(modRoot) || !System.IO.Directory.Exists(modRoot))
                return 0;

            string[] files = System.IO.Directory.GetFiles(modRoot, "*.json", SearchOption.AllDirectories);
            int fileCount = 0;
            for (int i = 0; i < files.Length; i++)
            {
                // 统一成正斜杠再判断，避免 Windows 反斜杠漏匹配
                string normalized = files[i].Replace('\\', '/');
                if (normalized.IndexOf("/" + EventFolder + "/", System.StringComparison.Ordinal) < 0)
                    continue;

                fileCount++;
                if (!LoadFile(files[i], "Mod")) errorCount++;
            }
            return fileCount;
        }

        /// <summary>
        /// 读取单个事件文件。文件是一个 JSON 数组（可含多个事件）。
        /// </summary>
        /// <param name="file">文件绝对路径</param>
        /// <param name="sourceTag">来源标记</param>
        /// <returns>是否成功</returns>
        bool LoadFile(string file, string sourceTag)
        {
            try
            {
                string text = System.IO.File.ReadAllText(file);
                List<EventDefinition> list = JsonConvert.DeserializeObject<List<EventDefinition>>(text);
                if (list == null)
                {
                    Log.Warning($"剧本事件文件内容为空：{file}");
                    return false;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    EventDefinition def = list[i];
                    if (def == null) continue;

                    if (def.Id <= 0)
                    {
                        Log.Warning($"剧本事件缺少合法 Id，已跳过：{file}");
                        continue;
                    }

                    def.SourceTag = sourceTag;
                    def.SourceFile = file;
                    AddOrReplace(def);
                }
                return true;
            }
            catch (System.Exception e)
            {
                // 单个文件坏掉不能让整个事件系统起不来——这是 Mod 场景的常态
                Log.Error($"剧本事件文件解析失败：{file}\n{e.Message}");
                return false;
            }
        }

        /// <summary>按 Id 加入或覆盖（后加载者胜）</summary>
        /// <param name="def">事件定义</param>
        void AddOrReplace(EventDefinition def)
        {
            if (byId.TryGetValue(def.Id, out EventDefinition exist))
            {
                Log.Warning($"剧本事件 Id 冲突：{def.Id} 被 {def.SourceTag} 覆盖（原来自 {exist.SourceTag}）");
                allEvents.Remove(exist);
            }

            allEvents.Add(def);
            byId[def.Id] = def;
        }

        /// <summary>按触发时机建立一级索引</summary>
        void RebuildIndex()
        {
            byKind.Clear();
            for (int i = 0; i < allEvents.Count; i++)
            {
                EventDefinition def = allEvents[i];
                if (def.Trigger == null) def.Trigger = new EventTriggerDef();

                if (!byKind.TryGetValue(def.Trigger.Kind, out List<EventDefinition> list))
                {
                    list = new List<EventDefinition>();
                    byKind[def.Trigger.Kind] = list;
                }
                list.Add(def);
            }
        }

        /// <summary>
        /// 重新加载（供调试面板 / 编辑器保存后热重载）。
        /// </summary>
        /// <returns>成功加载的事件数</returns>
        public int Reload()
        {
            return Load();
        }

        #endregion

        #region 查询

        /// <summary>按 Id 取事件</summary>
        /// <param name="id">事件 Id</param>
        /// <returns>事件定义；不存在返回 null</returns>
        public EventDefinition Get(int id)
        {
            return byId.TryGetValue(id, out EventDefinition def) ? def : null;
        }

        /// <summary>
        /// 取某个触发时机的候选事件（一级索引）。
        /// 返回的是内部列表，调用方**不要修改**。
        /// </summary>
        /// <param name="kind">触发时机</param>
        /// <returns>候选列表；无则返回空列表</returns>
        public List<EventDefinition> GetByKind(EventTriggerKind kind)
        {
            return byKind.TryGetValue(kind, out List<EventDefinition> list)
                ? list
                : EmptyList;
        }

        /// <summary>空列表常量（避免每次查询都 new）</summary>
        static readonly List<EventDefinition> EmptyList = new List<EventDefinition>();

        /// <summary>
        /// 按优先级排序候选：优先级高的先播，同优先级按 Id 升序（保证可复现）。
        /// 排序结果写入调用方给的临时列表，不触碰索引内部结构。
        /// </summary>
        /// <param name="candidates">候选事件</param>
        /// <param name="into">排序结果输出</param>
        public static void SortByPriority(List<EventDefinition> candidates, List<EventDefinition> into)
        {
            into.Clear();
            if (candidates == null) return;

            into.AddRange(candidates);
            into.Sort((a, b) =>
            {
                int byPriority = b.Priority.CompareTo(a.Priority);
                return byPriority != 0 ? byPriority : a.Id.CompareTo(b.Id);
            });
        }

        /// <summary>
        /// 判断事件的剧本绑定过滤是否命中当前剧本。
        ///
        /// 【为什么外部传 startYear 而不是自己读 Scenario.Info.year】
        /// ScenarioInfo.year 是"**当前**年份"，会随游戏推进逐年增长；
        /// 而剧本过滤要的是"这个剧本从**哪一年开局**"（官渡之战只应出现在 200 年开局的剧本里）。
        /// 项目没有存开局年份的字段，所以由 ScenarioEventManager 在剧本开始时捕获一次并缓存。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="startYear">剧本开局年份（0 = 未知，跳过 YearRange 过滤）</param>
        /// <param name="scenarioId">剧本 Id</param>
        /// <returns>true = 适用于当前剧本</returns>
        public static bool MatchScenarioFilter(EventDefinition def, int startYear, int scenarioId)
        {
            if (def == null || def.ScenarioFilter == null) return true;

            EventScenarioFilter filter = def.ScenarioFilter;
            if (string.IsNullOrEmpty(filter.Mode) || filter.Mode == "Any") return true;

            switch (filter.Mode)
            {
                case "YearRange":
                    // 开局年份未知时不做过滤，宁可多播也不要因数据不全而静默吞掉事件
                    if (startYear <= 0) return true;
                    if (filter.YearMin > 0 && startYear < filter.YearMin) return false;
                    if (filter.YearMax > 0 && startYear > filter.YearMax) return false;
                    return true;

                case "ExplicitIds":
                    return filter.ScenarioIds != null && filter.ScenarioIds.Contains(scenarioId);

                default:
                    return true;
            }
        }

        #endregion
    }
}
