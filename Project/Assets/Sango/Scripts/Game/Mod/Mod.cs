using LuaInterface;
using Sango.Game;
using System.IO;

namespace Sango.Mod
{
    public class Mod
    {
        /// <summary>
        /// 唯一标识符,只能用英文
        /// </summary>
        public string Id { internal set; get; }
        /// <summary>
        /// Mod名字,可以中文
        /// </summary>
        public string Name { internal set; get; }
        /// <summary>
        /// Mod标签
        /// </summary>
        public string Tag { internal set; get; }
        /// <summary>
        /// 海报图片相对路径
        /// </summary>
        public string Version { internal set; get; }
        /// <summary>
        /// 说明文字
        /// </summary>
        public string Description { internal set; get; }
        /// <summary>
        /// 依赖模组,以分号隔开 mod1;mod2
        /// </summary>
        public string Depends { internal set; get; }
        /// <summary>
        /// 海报图片相对路径
        /// </summary>
        public string Poster { internal set; get; }
        /// <summary>
        /// Mod路径
        /// </summary>
        public string ModDir { internal set; get; }
        /// <summary>
        /// Mod文件夹名字
        /// </summary>
        public string ModDirName { internal set; get; }

        public void LoadData()
        {
            string path = GetFullPath("Data");
            Directory.EnumFiles(path, "*.json", SearchOption.AllDirectories, (file) =>
            {
                Debugger.Log($"LoadData: {file}");
            });
            //Directory.EnumFiles(path, "*.txt", SearchOption.AllDirectories, (file) =>
            //{
            //    Debugger.Log($"LoadData: {file}");
            //    Game.GameData.Load(file);
            //});

            //Game.GameData.LoadBin(path);
        }
        public void LoadLua()
        {
            string path = GetFullPath("Lua");
            ToLua.push_script_evn(path);
            Directory.EnumFiles(path, "*.lua", SearchOption.AllDirectories, (file) =>
            {
                Debugger.Log($"LoadLua: {file}");
                SangoLuaClient.Require(file);
            });
            ToLua.remove_script_evn();
        }
        public void LoadUI()
        {
            string path = GetFullPath("UI");
            Directory.EnumFiles(path, "*.bytes", SearchOption.AllDirectories, (file) =>
            {
                Debugger.Log($"LoadUI: {file}");
                string packageName = System.IO.Path.GetFileNameWithoutExtension(file).Split('_')[0];
                Window.Instance.AddPackage(file, packageName);
            });
        }
        public void LoadPackage()
        {
            string path = GetFullPath("Package");
            Directory.EnumFiles(path, "*.pkg", SearchOption.AllDirectories, (file) =>
            {
                Debugger.Log($"LoadPackage: {file}");
                string packageName = System.IO.Path.GetFileNameWithoutExtension(file).Split('_')[0];
                PackageManager.Instance.AddPackage(packageName, file, true);
            });
        }

        public void LoadScenario()
        {
            string path = GetFullPath("Scenario");
            Directory.EnumFiles(path, "*.json", SearchOption.AllDirectories, (file) =>
            {
                Debugger.Log($"Find Scenario: {file}");
                Scenario.Add(file);
            });
        }
        public string GetFullPath(string path)
        {
            return $"{ModDir}/{path}";
        }
    }
}
