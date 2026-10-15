
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
        }

        public Mod[] GetEnabledMods()
        {
            return mEnabledModList.ToArray();
        }

        string marketSaveFile;

        /// <summary>
        /// mod市场数据
        /// </summary>
        public Dictionary<string, NetModMarket> mMarketMap = new Dictionary<string, NetModMarket>();

        public void AddMarketFromUrl(string url, Action onComplete, Action<float> onProgress = null)
        {
            bool hasError = false;
            App.Instance.StartCoroutine(GitDownloader.Get(url,
               onProgress,
               (content) =>
               {
                   if (string.IsNullOrEmpty(content))
                   {
                       hasError = true;
                   }
                   else
                   {
                       InitMarket(content);
                       SaveMarket();
                   }
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
            if (File.Exists(marketSaveFile))
            {
                JsonConvert.PopulateObject(Sango.File.ReadAllText(marketSaveFile), mMarketMap);
                App.Instance.StartCoroutine(LoadMarketAsCoroutine(complete, progress));
            }
            else
                ModManager.Instance.AddMarketFromUrl(ModListInfoUrl, complete, progress);
        }

        IEnumerator LoadMarketAsCoroutine(Action complete, Action<float> progress)
        {
            float cout = mMarketMap.Count;
            float current = 0;
            List<NetModMarket> list = new List<NetModMarket>();
            foreach (var item in mMarketMap)
            {
                list.Add(item.Value);
            }

            foreach (var item in list)
            {
                string urlBegin = item.url;
                if (urlBegin.EndsWith("/mod_list.txt"))
                    urlBegin = urlBegin.Substring(0, urlBegin.Length - 13);
                yield return GitDownloader.Get(urlBegin + "/mod_list.txt", (f) =>
                {
                    progress?.Invoke(current / cout + f / cout);
                }
                , (content) =>
                {
                    if (!string.IsNullOrEmpty(content))
                    {
                        InitMarket(content);
                        SaveMarket();
                    }
                    current++;
                    if (current == cout)
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
                    mod.Url = NetModMarket.MakeUrl(content, info);
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
                    mod.Url = NetModMarket.MakeUrl(content, info);
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

        public void LoadFile(string filename, System.Action<string> mergeAction)
        {
            string baseFile = Path.FindFile(filename);
            if (!string.IsNullOrEmpty(baseFile))
            {
                mergeAction(baseFile);
                return;
            }
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
