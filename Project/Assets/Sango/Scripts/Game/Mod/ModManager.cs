
using Newtonsoft.Json;
using Sango.Core;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Diagnostics;

namespace Sango.Mod
{
    public class ModManager : Singleton<ModManager>
    {
        //public string ModListInfoUrl = "https://gitcode.com/gametank/sango_infinity_mod_test/releases/download/mods/mod_list.txt";
        public string ModListInfoUrl = "http://139.155.98.66/market/mod_list.txt";

        public static string EditModName { get; set; }
        public static string MOD_ROOT_DIR = "Mods";
        public static string[] DEFAULT_MODS = { };
        //public static string[] DEFAULT_MODS = { };

        public List<Mod> mEnabledModList;
        public Dictionary<string, Mod> mModMap;

        [JsonObject(MemberSerialization.OptOut)]
        public class NetModMarket
        {
            public string name;
            public string url;
            public NetModInfo[] mods;

            /// <summary>
            /// 返回xxxx@1.0.zip
            /// </summary>
            /// <param name="netModMarket"></param>
            /// <param name="netModInfo"></param>
            /// <returns></returns>
            public static string MakeUrl(NetModMarket netModMarket, NetModInfo netModInfo)
            {
                string urlBegin = netModMarket.url;
                if (urlBegin.EndsWith("/mod_list.txt"))
                    urlBegin = urlBegin.Substring(0, urlBegin.Length - 13);
                return $"{urlBegin}/{netModInfo.id}@{netModInfo.version}.zip";
            }

            /// <summary>
            /// 构建候选下载地址列表(按优先级排列)
            ///
            /// 市场下发的 urls 第一个是 GitCode 镜像,第二个是本站直链;
            /// 无论服务端给没给 urls,最后都会补一条按 MakeUrl 规则拼出的本站地址兜底,
            /// 保证"市场数据缺字段 / 字段写错"时仍能下载。
            /// </summary>
            /// <param name="netModMarket">市场信息</param>
            /// <param name="netModInfo">模组信息</param>
            /// <returns>去重后的地址列表(可能为空)</returns>
            public static List<string> MakeUrlList(NetModMarket netModMarket, NetModInfo netModInfo)
            {
                List<string> list = new List<string>();
                if (netModInfo != null && netModInfo.urls != null)
                {
                    for (int i = 0; i < netModInfo.urls.Length; i++)
                    {
                        string u = netModInfo.urls[i];
                        if (!string.IsNullOrEmpty(u) && !list.Contains(u))
                            list.Add(u);
                    }
                }

                string fallback = MakeUrl(netModMarket, netModInfo);
                if (!string.IsNullOrEmpty(fallback) && !list.Contains(fallback))
                    list.Add(fallback);
                return list;
            }
        }

        [JsonObject(MemberSerialization.OptOut)]
        public class NetModInfo
        {
            public string id;
            public string name;
            public string version;
            public long size;
            public string auther;
            public string description;
            public string poster;
            /// <summary>
            /// 前置依赖模组Id,多个用分号隔开,例如 "modA;modB"
            /// 市场列表带上依赖后,未安装的模组也能在下载时自动把依赖一起下下来
            /// </summary>
            public string depends;

            /// <summary>
            /// 下载地址列表(按优先级排列,第一个是 GitCode 镜像,第二个是本站直链)。
            /// 老版本市场数据没有该字段时为 null,此时回落到 url + MakeUrl 规则。
            /// </summary>
            public string[] urls;
        }

        public Mod[] GetEnabledMods()
        {
            if (mEnabledModList == null)
                return System.Array.Empty<Mod>();
            return mEnabledModList.ToArray();
        }

        /// <summary>
        /// 获取当前启用模组的 Id 列表(按加载顺序)
        /// 存档时写入 <see cref="Sango.Core.ScenarioInfo.mods"/>,读档时用于对比
        /// </summary>
        public string[] GetEnabledModIds()
        {
            if (mEnabledModList == null || mEnabledModList.Count == 0)
                return System.Array.Empty<string>();

            string[] ids = new string[mEnabledModList.Count];
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                ids[i] = mod != null ? mod.Id : null;
            }
            return ids;
        }

        /// <summary>
        /// 取"已启用模组"的物理目录列表（按加载顺序）。
        ///
        /// 【为什么不能直接用 <see cref="GetEnabledMods"/>】
        /// 事件库 / 剧本参数预设这类"应用级静态数据"需要的是**目录路径**而不是 Mod 对象；
        /// 更关键的是加载时机：
        /// <code>
        /// Game.Init()
        ///   ├─ ModManager.Init()             // 先建空表：mEnabledModList = new List&lt;Mod&gt;()
        ///   ├─ GameSystemManager.Init()      // → ScenarioEventManager.Init() → EventDatabase.Load()
        ///   └─ GameInit() 协程 → InitMods()  // 这里才真正把启用模组填进 mEnabledModList
        /// </code>
        /// 也就是说事件库加载时 mEnabledModList **还是空表**，直接读它会把所有模组事件过滤成 0 个。
        /// 因此这里在启用表为空时回落到 <see cref="LoadModList"/>（Mods/modList.txt）——
        /// 它与 InitMods 读的是同一份数据，是"玩家勾选结果"的唯一持久化真源。
        /// </summary>
        /// <returns>
        /// 已启用模组的目录列表；模组系统尚未初始化（<c>mModMap</c> 为 null，
        /// 例如编辑器预览 / 纯逻辑单测）时返回 <c>null</c>，
        /// 调用方应据此退回"扫描整个模组根目录"的旧行为，避免把模组数据整体丢掉。
        /// </returns>
        public List<string> GetEnabledModDirs()
        {
            // 未初始化：由调用方决定是否退回全目录扫描
            if (mModMap == null)
                return null;

            List<string> dirs = new List<string>();

            // 运行期启用表非空就优先用它（顺序与加载顺序一致）；
            // 为空说明还没走到 InitMods，回落到 modList.txt。
            string[] ids = (mEnabledModList != null && mEnabledModList.Count > 0)
                ? GetEnabledModIds()
                : LoadModList();
            if (ids == null || ids.Length == 0)
                return dirs;

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id))
                    continue;

                Mod mod;
                if (!mModMap.TryGetValue(id, out mod) || mod == null || string.IsNullOrEmpty(mod.ModDir))
                    continue;

                // 依赖列表里可能有重复 Id，目录去重后再交给调用方
                if (!dirs.Contains(mod.ModDir))
                    dirs.Add(mod.ModDir);
            }
            return dirs;
        }

        /// <summary>
        /// 对比"存档记录的模组列表"与"当前启用的模组列表"
        ///
        /// 判定口径(只关心"存档需要的模组是否都还在"):
        ///   · 存档记录的每一个模组都已启用 → 一致。当前多启用了别的模组、或启用顺序与存档不同,
        ///     都不再拦截读档(多出来的模组不会破坏存档里已有的数据,顺序差异也不影响读取);
        ///   · 存档记录的模组有任意一个没启用 → 不一致(存档可能缺表缺资源,需要提醒玩家);
        ///   · 存档没有记录(老存档,mods 为 null)→ 当前一个模组都没启用才算一致,否则按不一致处理,
        ///     因为无法保证存档内容与当前数据对得上。
        /// </summary>
        /// <param name="savedMods">存档里记录的模组 Id 列表,可为 null</param>
        /// <param name="message">输出:面向玩家的差异说明(中文,可能多行);一致时为 null</param>
        /// <returns>true=一致(可以正常读档)</returns>
        public bool MatchSavedMods(string[] savedMods, out string message)
        {
            message = null;
            string[] curMods = GetEnabledModIds();
            int curCount = curMods != null ? curMods.Length : 0;
            int savedCount = savedMods != null ? savedMods.Length : 0;

            // 老存档没有模组信息:当前也没启用任何模组时视为一致
            if (savedMods == null)
            {
                if (curCount == 0)
                    return true;

                message = "该存档是旧版本保存的,没有记录使用的模组。\r\n"
                        + $"当前启用的模组: {string.Join("、", curMods)}\r\n"
                        + "继续读取可能因数据不匹配而失败。";
                return false;
            }

            HashSet<string> curSet = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < curCount; i++)
            {
                if (!string.IsNullOrEmpty(curMods[i]))
                    curSet.Add(curMods[i]);
            }

            // 只统计"存档有、当前没有"的模组:当前多启用的模组不算不一致
            List<string> missing = new List<string>();
            HashSet<string> visited = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < savedCount; i++)
            {
                string id = savedMods[i];
                if (string.IsNullOrEmpty(id) || !visited.Add(id))
                    continue;
                if (!curSet.Contains(id))
                    missing.Add(id);
            }

            // 存档记录的模组全都在启用列表中:直接视为一致,不弹任何提示
            if (missing.Count == 0)
                return true;

            message = "存档使用的模组与当前不一致。\r\n"
                    + $"缺少模组: {string.Join("、", missing)}\r\n"
                    + "继续读取很可能导致数据错乱或加载失败。";
            return false;
        }

        /// <summary>
        /// 检查"存档记录的模组"在当前环境的满足情况。
        ///
        /// 用途:读档发现模组不一致时,把缺失原因分类展示给玩家 ——
        /// "本机根本没下载"和"下载了但没启用"对应完全不同的处理方式,
        /// 玩家需要据此判断值不值得冒险直接读档。
        /// </summary>
        /// <param name="savedMods">存档记录的模组 Id 列表(可为 null = 老存档没有记录)</param>
        /// <param name="notDownloaded">输出:本机没有下载的模组 Id</param>
        /// <param name="inactive">输出:已下载但当前未激活的模组 Id</param>
        /// <returns>
        /// true = 存档需要的模组在本机都能找到(只是没启用);
        /// false = 有模组没下载,或者存档没有记录模组信息
        /// </returns>
        public bool CheckSavedModsAvailable(string[] savedMods, out List<string> notDownloaded, out List<string> inactive)
        {
            notDownloaded = new List<string>();
            inactive = new List<string>();

            // 老存档没有模组记录(null):不知道当时用了什么,无法还原,
            // 交给"模组不一致"的普通警告处理。
            // 注意:空数组([])是"记录了当时没有启用任何模组",属于可还原的情况(等于全部停用)。
            if (savedMods == null)
                return false;

            HashSet<string> enabledSet = new HashSet<string>(GetEnabledModIds(), System.StringComparer.OrdinalIgnoreCase);
            HashSet<string> visited = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < savedMods.Length; i++)
            {
                string id = savedMods[i];
                if (string.IsNullOrEmpty(id) || !visited.Add(id))
                    continue;

                Mod mod;
                if (mModMap == null || !mModMap.TryGetValue(id, out mod) || mod == null)
                    notDownloaded.Add(id);              // 本机没有这个模组
                else if (!enabledSet.Contains(id))
                    inactive.Add(id);                   // 下载了但没激活
            }

            return notDownloaded.Count == 0;
        }

        string marketSaveFile;

        /// <summary>
        /// mod市场数据
        /// </summary>
        public Dictionary<string, NetModMarket> mMarketMap = new Dictionary<string, NetModMarket>();

        public void AddMarketFromUrl(string url, Action onComplete, Action<float> onProgress = null)
        {
            App.Instance.StartCoroutine(GitDownloader.Get(url,
               onProgress,
               (content) =>
               {
                   if (string.IsNullOrEmpty(content))
                   {
                       // 失败必须留下 Error 日志(Info/Warning 在正式包会被编译剔除),
                       // 否则线上玩家反馈"模组列表加载不出来"时完全查不到原因
                       Sango.Log.Error("市场数据拉取失败: " + url);
                   }
                   else
                   {
                       try
                       {
                           InitMarket(content);
                           SaveMarket();
                       }
                       catch (Exception e)
                       {
                           Sango.Log.Error("解析市场数据失败: " + e.Message + " URL: " + url);
                       }
                   }
                   // 无论成功失败都要回调,否则界面会永远停在"加载中"
                   onComplete?.Invoke();
               }
           ));
        }

        public void SaveMarket()
        {
            Sango.File.WriteAllText(marketSaveFile, JsonConvert.SerializeObject(mMarketMap));
        }

        public void LoadMarket()
        {
            mMarketMap.Clear();
            if (File.Exists(marketSaveFile))
            {
                JsonConvert.PopulateObject(Sango.File.ReadAllText(marketSaveFile), mMarketMap);
            }
        }

        public void LoadMarketAsync(Action complete, Action<float> progress)
        {
            mMarketMap.Clear();

            // 本地缓存读取失败(文件损坏/被改成空文件)时要有兜底:
            // 旧实现会在这里抛异常,协程根本不会启动,界面会一直停在"加载中 0%",
            // 玩家看到的就是"点了没反应"
            bool hasCache = false;
            if (File.Exists(marketSaveFile))
            {
                try
                {
                    string json = Sango.File.ReadAllText(marketSaveFile);
                    JsonConvert.PopulateObject(json, mMarketMap);
                    hasCache = mMarketMap.Count > 0;
                }
                catch (Exception e)
                {
                    Sango.Log.Error("读取市场缓存失败,准备从网络重新拉取: " + e.Message);
                    mMarketMap.Clear();
                }
            }

            if (hasCache)
                App.Instance.StartCoroutine(LoadMarketAsCoroutine(complete, progress));
            else
                ModManager.Instance.AddMarketFromUrl(ModListInfoUrl, complete, progress);
        }

        IEnumerator LoadMarketAsCoroutine(Action complete, Action<float> progress)
        {
            // 用 int 计数:原来用 float 比较 current == cout,遇到浮点误差时可能永远不相等,
            // complete 不回调,界面就一直卡在"加载中"
            int total = mMarketMap.Count;
            int current = 0;
            List<NetModMarket> list = new List<NetModMarket>();
            foreach (var item in mMarketMap)
            {
                list.Add(item.Value);
            }

            if (total <= 0)
            {
                complete?.Invoke();
                yield break;
            }

            foreach (var item in list)
            {
                string urlBegin = item.url;
                if (!string.IsNullOrEmpty(urlBegin) && urlBegin.EndsWith("/mod_list.txt"))
                    urlBegin = urlBegin.Substring(0, urlBegin.Length - 13);
                yield return GitDownloader.Get(urlBegin + "/mod_list.txt", (f) =>
                {
                    progress?.Invoke((current + f) / total);
                }
                , (content) =>
                {
                    if (!string.IsNullOrEmpty(content))
                    {
                        InitMarket(content);
                        SaveMarket();
                    }
                    else
                    {
                        Sango.Log.Error("市场列表拉取失败: " + urlBegin);
                    }
                    current++;
                    if (current >= total)
                        complete?.Invoke();
                });
            }
        }

        public void InitMarket(string marketContent)
        {
            NetModMarket netModMarket = JsonConvert.DeserializeObject<NetModMarket>(marketContent);
            if (netModMarket == null)
            {
                Sango.Log.Error("不是有效的市场数据结构!! marketContent = " + marketContent);
                return;
            }

            if (mMarketMap.TryGetValue(netModMarket.name, out NetModMarket exsist))
            {
                // 初始化mod
                exsist.mods = netModMarket.mods;
                InitMarketModInfo(netModMarket);
            }
            else
            {
                mMarketMap.Add(netModMarket.name, netModMarket);
                InitMarketModInfo(netModMarket);
            }
        }

        public void Init()
        {
            string path = $"{Path.ContentRootPath}/Package/{PlatformUtility.GetPlatformName()}";
            Directory.EnumFiles(path, "*.pkg", SearchOption.AllDirectories, (file) =>
            {
                Sango.Log.Info($"LoadPackage: {file}");
                string packageName = System.IO.Path.GetFileNameWithoutExtension(file).Split('_')[0];
                PackageManager.Instance.AddPackage(packageName, file, true);
            });

            MOD_ROOT_DIR = Path.ModRootPath;

            if (!Sango.Directory.Exists(MOD_ROOT_DIR))
                Sango.Directory.Create(MOD_ROOT_DIR);

            marketSaveFile = $"{MOD_ROOT_DIR}/mod_market.json";
            LoadMarket();

            mEnabledModList = new List<Mod>();
            mModMap = new Dictionary<string, Mod>();

            string[] dirs = Directory.GetDirectories(MOD_ROOT_DIR, "*", System.IO.SearchOption.TopDirectoryOnly);
            if (dirs != null)
            {
                for (int i = 0; i < dirs.Length; i++)
                {
                    string mod_dir = dirs[i];

                    // 跳过安装过程中残留的临时解压目录/旧版本备份目录(上次更新异常中断留下的),
                    // 它们不是正常模组,直接忽略并顺手清理
                    if (GitDownloader.IsTempModDirectory(mod_dir))
                    {
                        Sango.Log.Warning("跳过残留的临时模组目录: " + mod_dir);
                        try { Sango.Directory.Delete(mod_dir); }
                        catch (System.Exception e) { Sango.Log.Warning("清理临时模组目录失败: " + e.Message); }
                        continue;
                    }

                    Mod mod = LoadMod(mod_dir);
                    if (mod != null)
                    {
                        if (mModMap.TryAdd(mod.Id, mod))
                        {
                            mod.ModDir = mod_dir;
                        }
                    }
                }
            }
        }

        public bool HasError = false;
        void InitMarketModInfo(NetModMarket content)
        {
            if (content == null || content.mods == null)
                return;

            for (int i = 0; i < content.mods.Length; i++)
            {
                NetModInfo info = content.mods[i];
                if (info == null) continue;

                Mod mod;
                if (mModMap.TryGetValue(info.id, out mod))
                {
                    // 地址列表:第一个是 GitCode 镜像,最后是本站直链(见 MakeUrlList)
                    mod.UrlList = NetModMarket.MakeUrlList(content, info);
                    mod.Url = mod.UrlList.Count > 0 ? mod.UrlList[0] : NetModMarket.MakeUrl(content, info);
                    mod.UrlVersion = info.version;
                    mod.Size = info.size;
                    mod.MarketName = content.name;
                    // 本地已安装的模组以包内 mod.info 的依赖为准,
                    // 只有当包内没有写依赖时,才用市场列表里的依赖补上
                    if (string.IsNullOrEmpty(mod.Depends))
                        mod.Depends = info.depends;
                }
                else
                {
                    mod = new Mod();
                    mod.Id = info.id;
                    mod.Name = info.name;
                    mod.Description = info.description;
                    mod.Author = info.auther;
                    mod.Size = info.size;
                    mod.Poster = info.poster;
                    // 未安装的模组只有市场列表这一个依赖来源
                    mod.Depends = info.depends;
                    mod.UrlList = NetModMarket.MakeUrlList(content, info);
                    mod.Url = mod.UrlList.Count > 0 ? mod.UrlList[0] : NetModMarket.MakeUrl(content, info);
                    mod.MarketName = content.name;
                    mModMap.Add(info.id, mod);
                }
            }
        }

        public Mod LoadMod(string path)
        {
            string info_file = $"{path}/mod.info";
            if (File.Exists(info_file))
            {
                Mod mod = new Mod();
                mod.ModDirName = System.IO.Path.GetFileName(path);
                string[] lines = File.ReadAllLines(info_file);
                foreach (string s in lines)
                {
                    string[] c_v = s.Split('=');
                    if (c_v.Length > 1)
                    {
                        switch (c_v[0].Trim().ToLower())
                        {
                            case "id":
                                mod.Id = c_v[1].Trim();
                                break;
                            case "name":
                                mod.Name = c_v[1].Trim();
                                break;
                            case "description":
                                mod.Description = c_v[1].Trim();
                                break;
                            case "version":
                                mod.Version = c_v[1].Trim();
                                break;
                            case "depends":
                                mod.Depends = c_v[1].Trim();
                                break;
                            case "poster":
                                mod.Poster = c_v[1].Trim();
                                break;
                            case "assembly":
                                mod.EntryAssembly = c_v[1].Trim();
                                break;
                            case "author":
                                mod.Author = c_v[1].Trim();
                                break;
                            case "auther":
                                mod.Author = c_v[1].Trim();
                                break;
                            case "size":
                                long.TryParse(c_v[1].Trim(), out mod.Size);
                                break;
                        }
                    }
                }
                return mod;
            }
            return null;
        }

        /// <summary>
        /// 从网上下载下来后,更新mod信息
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mod"></param>
        public void UpdateMod(string path, Mod mod)
        {
            string info_file = $"{path}/mod.info";
            if (File.Exists(info_file))
            {
                mod.ModDir = path;
                mod.ModDirName = System.IO.Path.GetFileName(path);
                string[] lines = File.ReadAllLines(info_file);
                foreach (string s in lines)
                {
                    string[] c_v = s.Split('=');
                    if (c_v.Length > 1)
                    {
                        switch (c_v[0].Trim().ToLower())
                        {
                            case "id":
                                mod.Id = c_v[1].Trim();
                                break;
                            case "name":
                                mod.Name = c_v[1].Trim();
                                break;
                            case "description":
                                mod.Description = c_v[1].Trim();
                                break;
                            case "version":
                                mod.Version = c_v[1].Trim();
                                break;
                            case "depends":
                                mod.Depends = c_v[1].Trim();
                                break;
                            case "poster":
                                mod.Poster = c_v[1].Trim();
                                break;
                            case "assembly":
                                mod.EntryAssembly = c_v[1].Trim();
                                break;
                            case "author":
                                mod.Author = c_v[1].Trim();
                                break;
                        }
                    }
                }
            }
            GameEvent.OnModUpdate?.Invoke(mod);
        }

        public string[] GetAllPath(string dirName)
        {
            List<string> path = new List<string>();
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                path.Add(mod.GetFullPath(dirName));
            }
            return path.ToArray();
        }

        /// <summary>
        /// 获取某个模组私有目录下文件的完整路径(显式调用,不参与全局搜索)
        /// </summary>
        /// <param name="modId">模组唯一标识</param>
        /// <param name="relativePath">相对私有目录的路径,例如 Data/Common/AIConfig.json</param>
        /// <param name="fullPath">输出:存在的完整路径;不存在返回 false</param>
        /// <returns>true=文件存在</returns>
        public bool TryGetPrivateFile(string modId, string relativePath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(relativePath))
                return false;

            // 统一走协议解析(静默版):顺带完成"模组是否启用""路径是否越界"的校验
            return ModAssetPath.TryResolve($"{ModAssetPath.Scheme}{modId}/{relativePath}", out fullPath);
        }

        /// <summary>
        /// 加载某个模组私有目录下的配置文件(显式调用)
        /// 与全局合并规则无关:由调用方决定什么时候加载、是否覆盖,因此不会被其他模组影响
        /// </summary>
        /// <param name="modId">模组唯一标识</param>
        /// <param name="relativePath">相对私有目录的路径,例如 Data/Common/AIConfig.json</param>
        /// <param name="action">文件存在时的回调</param>
        /// <returns>true=命中并执行了回调</returns>
        public bool LoadPrivateFile(string modId, string relativePath, System.Action<string> action)
        {
            string fullPath;
            if (!TryGetPrivateFile(modId, relativePath, out fullPath))
                return false;

            action?.Invoke(fullPath);
            return true;
        }

        /// <summary>
        /// 按"整文件覆盖"的语义加载一个文件:命中即返回
        /// 查找顺序为 内容根目录 -> 各模组目录;
        /// 需要读取某个模组的私有目录时,请改用 <see cref="LoadPrivateFile"/> 显式指定模组Id
        /// </summary>
        public void LoadFile(string filename, System.Action<string> mergeAction)
        {
            string baseFile = Path.FindFile(filename);
            if (!string.IsNullOrEmpty(baseFile))
            {
                mergeAction(baseFile);
                return;
            }

            // Init() 之前 mEnabledModList 为 null（编辑器预览、纯逻辑单测都会走到这里）。
            // 早期调用方只想"有就加载"，此时没有模组列表属于正常情况，直接返回即可。
            if (mEnabledModList == null)
                return;

            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                string destFile = mod.GetFullPath(filename);
                if (File.Exists(destFile))
                {
                    mergeAction(destFile);
                    return;
                }
            }
        }


        public string[] LoadModList()
        {
            string list_path = $"{MOD_ROOT_DIR}/modList.txt";
            if (!File.Exists(list_path))
                return null;

            string[] mod_list = File.ReadAllLines(list_path);
            List<string> list = new List<string>(DEFAULT_MODS);
            list.AddRange(mod_list);
            return list.ToArray();
        }

        public void SaveModList(string[] mod_list)
        {
            string list_path = $"{MOD_ROOT_DIR}/modList.txt";
            if (File.Exists(list_path))
                File.Delete(list_path);
            List<string> list = new List<string>(mod_list);
            foreach (string s in DEFAULT_MODS)
                list.Remove(s);
            File.WriteAllText(list_path, string.Join("\n", list));
        }

        public void SaveModList(Mod[] mod_list)
        {
            string list_path = $"{MOD_ROOT_DIR}/modList.txt";
            if (File.Exists(list_path))
                File.Delete(list_path);
            List<string> list = new List<string>();
            for (int i = 0; i < mod_list.Length; i++)
                list.Add(mod_list[i].Id);
            foreach (string s in DEFAULT_MODS)
                list.Remove(s);
            File.WriteAllText(list_path, string.Join("\n", list));
        }

        public void InitMods()
        {
            InitMods(null);
        }

        public void InitMods(string[] modNames)
        {
            if (modNames == null)
                modNames = LoadModList();

            Scenario.OnModInitStart();

            mEnabledModList.Clear();

            // 私有资源目录只做"登记",不加入搜索路径、不参与模组间覆盖;
            // 只有显式引用(mod://模组Id/路径 或 模组Id:路径)时才会被访问
            ModAssetPath.ClearModRoots();

            if (modNames != null)
            {
                for (int i = 0; i < modNames.Length; i++)
                {
                    Mod mod;
                    if (mModMap.TryGetValue(modNames[i], out mod))
                    {
                        mEnabledModList.Add(mod);
                    }
                }
            }

            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];

                // 私有资源目录:只登记"模组Id -> 私有目录"的映射,供 mod://模组Id/路径 显式引用
                // 它不会进入搜索路径,因此不会覆盖任何模组的普通资源,也不会被其他模组覆盖
                if (mod.HasPrivateDir)
                    ModAssetPath.RegisterModRoot(mod.Id, mod.PrivateDir);

                Path.AddSearchPath($"{mod.ModDir}", true);
                mod.LoadLanguage();
                mod.LoadScenario();
                mod.LoadUI();
                mod.LoadPackage();
                mod.LoadData();
                mod.LoadAssembly();
            }

            Scenario.OnModInitEnd();
        }

        /// <summary>
        /// 遍历文件,找到Mod下所有这个路径的文件,一般用来合并json文件
        /// </summary>
        /// <param name="path">Assets/AA/BB/cc.dd</param>
        /// <param name="action"></param>
        public void EnumFiles(string path, System.Action<string> action)
        {
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                string targetFile = mod.GetFullPath(path);
                if (File.Exists(targetFile))
                {
                    try
                    {
                        action(targetFile);

                    }
                    catch (Exception e)
                    {
                        Sango.Log.Error(e.Message + e.StackTrace);
                    }
                }
            }
        }

        /// <summary>
        /// 遍历文件,找到Mod下所有这个路径的文件,一般用来合并json文件
        /// </summary>
        /// <param name="path">Assets/AA/BB/cc.dd</param>
        /// <param name="action"></param>
        public void EnumFiles(string path, System.Action<Mod, string> action)
        {
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                string targetFile = mod.GetFullPath(path);
                if (File.Exists(targetFile))
                {
                    try
                    {
                        action(mod, targetFile);

                    }
                    catch(Exception e)
                    {
                        Sango.Log.Error(e.Message + e.StackTrace);
                    }
                }
            }
        }

        /// <summary>
        /// 遍历文件夹,找到Mod下所有这个路径下的文件,一般用来合并指定类型的json文件
        /// </summary>
        /// <param name="path">Assets/AA/BB/cc.dd</param>
        /// <param name="action"></param>
        public void EnumFiles(string path, string searchPattern, SearchOption searchOption, System.Action<string> action)
        {
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                string targetDir = mod.GetFullPath(path);
                Directory.EnumFiles(targetDir, searchPattern, searchOption, action);
            }
        }

        /// <summary>
        /// 遍历文件,找到Mod下所有这个路径的文件,一般用来合并json文件
        /// </summary>
        /// <param name="path">Assets/AA/BB/cc.dd</param>
        /// <param name="action"></param>
        public void EnumDirectory(string path, System.Action<string> action)
        {
            for (int i = 0; i < mEnabledModList.Count; i++)
            {
                Mod mod = mEnabledModList[i];
                string targetFile = mod.GetFullPath(path);
                if (Directory.Exists(targetFile))
                {
                    try
                    {
                        action(targetFile);

                    }
                    catch (Exception e)
                    {
                        Sango.Log.Error(e.Message + e.StackTrace);
                    }
                }
            }
        }

        public void RemoveMod(Mod mod)
        {
            if (mEnabledModList.Contains(mod))
                mEnabledModList.Remove(mod);
            mModMap.Remove(mod.Id);
            // 注销私有资源目录,避免卸载后仍能通过 mod://模组Id/路径 访问到已删除的模组
            ModAssetPath.UnregisterModRoot(mod.Id);
            Sango.Directory.Delete(mod.ModDir);
            GameEvent.OnModUpdate?.Invoke(mod);
        }

        /// <summary>
        /// 清理市场保存的数据：清空内存中的市场表,并删除市场数据存档文件 mod_market.json
        /// </summary>
        public void ClearMarket()
        {
            mMarketMap.Clear();
            if (!string.IsNullOrEmpty(marketSaveFile))
                File.Delete(marketSaveFile);
            Sango.Log.Info("市场保存的数据已清理");
        }

        /// <summary>
        /// 清理市场保存的数据和所有的模组(重装/排障时使用)
        /// 清理内容:
        ///   1. 市场数据: 清空内存中的市场表 mMarketMap,并删除市场数据存档 mod_market.json;
        ///   2. 所有Mod: 删除Mod根目录下的所有Mod文件夹;
        ///   3. 内存缓存: 清空已启用的Mod列表 mEnabledModList 与Mod表 mModMap;
        ///   4. 已启用Mod存档 modList.txt: 其中记录的Mod已被删除,一并删除避免残留脏数据;
        /// 清理完成后逐个通知界面刷新(GameEvent.OnModUpdate)
        /// 注意: 本方法是纯粹的清理操作,不会询问玩家;玩家入口见 UIModManager.OnClearAllMods
        /// (先弹窗确认,确认后才清理,清理完成后重启游戏)
        /// </summary>
        public void ClearAll()
        {
            Sango.Log.Info("开始清理市场数据和所有模组...");

            // 1.清理市场数据(内存 + 存档文件)
            ClearMarket();

            // 2.删除Mod根目录下的所有Mod文件夹
            if (Sango.Directory.Exists(MOD_ROOT_DIR))
            {
                string[] dirs = Directory.GetDirectories(MOD_ROOT_DIR, "*", System.IO.SearchOption.TopDirectoryOnly);
                if (dirs != null)
                {
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        Sango.Directory.Delete(dirs[i]);
                        Sango.Log.Info("已删除模组文件夹: " + dirs[i]);
                    }
                }
            }

            // 3.记录被清理的Mod,用于通知界面刷新
            List<Mod> removedMods = new List<Mod>();
            if (mModMap != null)
            {
                foreach (Mod mod in mModMap.Values)
                    removedMods.Add(mod);
            }

            // 4.清空内存缓存
            if (mEnabledModList != null)
                mEnabledModList.Clear();
            if (mModMap != null)
                mModMap.Clear();
            // 私有资源登记表一并清空:模组目录都删了,再按 mod://模组Id/路径 访问只会读到不存在的文件
            ModAssetPath.ClearModRoots();

            // 5.已启用Mod列表里记录的Mod都已经被删除,一并删除避免残留脏数据
            File.Delete($"{MOD_ROOT_DIR}/modList.txt");

            // 6.通知界面移除对应的Mod条目
            for (int i = 0; i < removedMods.Count; i++)
                GameEvent.OnModUpdate?.Invoke(removedMods[i]);

            Sango.Log.Info("市场数据和所有模组清理完毕");
        }

        public bool HasMod(Mod mod)
        {
            return mModMap.ContainsKey(mod.Id);
        }

        /// <summary>
        /// 根据模组Id获取模组
        /// </summary>
        /// <param name="id">模组唯一标识</param>
        /// <returns>找到的模组,找不到返回null</returns>
        public Mod GetModById(string id)
        {
            if (string.IsNullOrEmpty(id) || mModMap == null)
                return null;
            mModMap.TryGetValue(id, out Mod mod);
            return mod;
        }

        /// <summary>
        /// 解析依赖字符串(mod.info 的 depends,或市场列表的 depends)
        /// 支持分号/逗号/竖线分隔,并自动去掉空白项,例如 "modA;modB" / "modA,modB"
        /// </summary>
        /// <param name="depends">依赖字符串</param>
        /// <returns>依赖模组Id数组,无依赖时返回空数组</returns>
        public static string[] ParseDepends(string depends)
        {
            if (string.IsNullOrEmpty(depends))
                return System.Array.Empty<string>();

            string[] arr = depends.Split(new char[] { ';', ',', '|' }, System.StringSplitOptions.RemoveEmptyEntries);
            List<string> result = new List<string>();
            for (int i = 0; i < arr.Length; i++)
            {
                string depId = arr[i].Trim();
                if (!string.IsNullOrEmpty(depId))
                    result.Add(depId);
            }
            return result.ToArray();
        }

        /// <summary>
        /// 收集某个模组的全部前置依赖(递归、去重)
        /// 返回顺序为"最底层依赖在最前,自身在最后",可直接当作下载顺序或加载顺序使用
        /// </summary>
        /// <param name="mod">目标模组</param>
        /// <returns>依赖在前、自身在后的模组列表</returns>
        public List<Mod> CollectDepends(Mod mod)
        {
            List<Mod> result = new List<Mod>();
            CollectDependsRecursive(mod, result, new HashSet<string>());
            return result;
        }

        /// <summary>
        /// 递归收集依赖(深度优先后序:先把最底层依赖放进去,最后放自己)
        /// </summary>
        /// <param name="mod">当前访问的模组</param>
        /// <param name="result">收集结果</param>
        /// <param name="visited">已访问过的模组Id,用于去重与避免循环依赖死递归</param>
        private void CollectDependsRecursive(Mod mod, List<Mod> result, HashSet<string> visited)
        {
            if (mod == null)
                return;

            // 已访问过说明存在重复引用或循环依赖,直接返回
            if (!string.IsNullOrEmpty(mod.Id))
            {
                if (visited.Contains(mod.Id))
                    return;
                visited.Add(mod.Id);
            }

            string[] depIds = ParseDepends(mod.Depends);
            for (int i = 0; i < depIds.Length; i++)
            {
                Mod dep = GetModById(depIds[i]);
                if (dep != null)
                    CollectDependsRecursive(dep, result, visited);
                else
                    Sango.Log.Warning($"模组 {mod.Id} 依赖的模组 {depIds[i]} 未找到(本地与市场均无)");
            }

            result.Add(mod);
        }

        /// <summary>
        /// 获取某个模组(含递归依赖)中"本地与市场都找不到"的依赖Id列表
        /// 用于在下载/勾选前提示玩家,避免依赖缺失导致模组无法正常运行
        /// </summary>
        /// <param name="mod">目标模组</param>
        /// <returns>缺失的依赖Id列表</returns>
        public List<string> GetMissingDepends(Mod mod)
        {
            List<string> missing = new List<string>();
            CollectMissingDepends(mod, missing, new HashSet<string>());
            return missing;
        }

        /// <summary>
        /// 递归查找缺失的依赖
        /// </summary>
        /// <param name="mod">当前访问的模组</param>
        /// <param name="missing">缺失依赖收集结果</param>
        /// <param name="visited">已访问过的模组Id</param>
        private void CollectMissingDepends(Mod mod, List<string> missing, HashSet<string> visited)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Id) || !visited.Add(mod.Id))
                return;

            string[] depIds = ParseDepends(mod.Depends);
            for (int i = 0; i < depIds.Length; i++)
            {
                Mod dep = GetModById(depIds[i]);
                if (dep == null)
                {
                    if (!missing.Contains(depIds[i]))
                        missing.Add(depIds[i]);
                }
                else
                {
                    CollectMissingDepends(dep, missing, visited);
                }
            }
        }
    }
}
