
using TKNewtonsoft.Json;
using Sango.Core;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TKNewtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Diagnostics;

namespace Sango.Mod
{
    public class ModManager : Singleton<ModManager>
    {
        public string ModListInfoUrl = "https://gitcode.com/gametank/sango_infinity_mod_test/releases/download/mods/mod_list.txt";
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
                return $"{netModMarket.url}/{netModInfo.id}@{netModInfo.version}.zip";
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
                yield return GitDownloader.Get(item.url + "/mod_list.txt", (f) =>
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
                    action(targetFile);
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
                    action(mod, targetFile);
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
                    action(targetFile);
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

        public bool HasMod(Mod mod)
        {
            return mModMap.ContainsKey(mod.Id);
        }
    }
}
