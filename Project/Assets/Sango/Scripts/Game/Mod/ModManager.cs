using LuaInterface;
using Sango.Game;
using System.Collections.Generic;
using System.IO;

namespace Sango.Mod
{
    public class ModManager : Singletion<ModManager>
    {
        public static string EditModName { get; set; }
        public static string MOD_ROOT_DIR = "Mods";
        public static string[] DEFAULT_MODS = { "Core" };
        //public static string[] DEFAULT_MODS = { };

        public List<Mod> mModList;
        public Dictionary<string, Mod> mModMap;

        public Mod[] GetEnabledMods()
        {
            return mModList.ToArray();
        }

        public void Init()
        {
            string path = Path.ContentRootPath + "/Package";
            Directory.EnumFiles(path, "*.pkg", SearchOption.AllDirectories, (file) =>
            {
                Debugger.Log($"LoadPackage: {file}");
                string packageName = System.IO.Path.GetFileNameWithoutExtension(file).Split('_')[0];
                PackageManager.Instance.AddPackage(packageName, file, true);
            });

            MOD_ROOT_DIR = Path.ModRootPath;

            mModList = new List<Mod>();
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
                        switch (c_v[0])
                        {
                            case "id":
                                mod.Id = c_v[1];
                                break;
                            case "name":
                                mod.Name = c_v[1];
                                break;
                            case "description":
                                mod.Description = c_v[1];
                                break;
                            case "version":
                                mod.Version = c_v[1];
                                break;
                            case "depends":
                                mod.Depends = c_v[1];
                                break;
                            case "poster":
                                mod.Poster = c_v[1];
                                break;
                        }
                    }
                }
                return mod;
            }
            return null;
        }

        public string[] GetAllPath(string dirName)
        {
            List<string> path = new List<string>();
            for (int i = 0; i < mModList.Count; i++)
            {
                Mod mod = mModList[i];
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
            for (int i = 0; i < mModList.Count; i++)
            {
                Mod mod = mModList[i];
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

        public void InitMods()
        {
            InitMods(null);
        }

        public void InitMods(string[] modNames)
        {
            if (modNames == null)
                modNames = LoadModList();


           
            Scenario.OnModInitStart();

            mModList.Clear();

            for (int i = 0; i < modNames.Length; i++)
            {
                Mod mod;
                if (mModMap.TryGetValue(modNames[i], out mod))
                {
                    mModList.Add(mod);
                }
            }

            //最终可以通过MOD/Lua/名字去查代码
            LuaFileUtils.Instance.AddSearchPath(MOD_ROOT_DIR);
            for (int i = 0; i < mModList.Count; i++)
                mModList[i].LoadScenario();
            for (int i = 0; i < mModList.Count; i++)
                mModList[i].LoadUI();
            for (int i = 0; i < mModList.Count; i++)
                mModList[i].LoadPackage();
            for (int i = 0; i < mModList.Count; i++)
                mModList[i].LoadData();
            for (int i = 0; i < mModList.Count; i++)
                Path.AddSearchPath($"{mModList[i].ModDir}/Assets", true);

            for (int i = 0; i < mModList.Count; i++)
            {
                Mod mod = mModList[i];
                LuaFileUtils.Instance.AddSearchPath($"{mod.ModDir}/Lua", true);
                mod.LoadLua();
            }

            for (int i = 0; i < mModList.Count; i++)
            {
                Mod mod = mModList[i];
                LuaTable modTable = LuaClient.GetTable(mod.ModDirName);
                if (modTable == null)
                {
                    modTable = LuaClient.GetTable(mod.ModDirName.ToLower());
                }

                if (modTable != null)
                {
                    LuaFunction luaFunction = modTable.GetLuaFunction("Init");
                    if (luaFunction != null)
                    {
                        luaFunction.Call(modTable);
                    }
                }
            }

            Scenario.OnModInitEnd();
        }
    }
}
