/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using DG.Tweening.Plugins.Core.PathCore;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Sango
{
    /// <summary>
    /// 游戏的所有资源路径获取接口,提供准确的游戏资源路径
    /// </summary>
    public static class Path
    {
#if UNITY_EDITOR
        /// <summary>
        /// 配置保存文件地址,仅支持编辑器
        /// </summary>
        [LuaInterface.NoToLua]
        public static string settingSavePath = Application.dataPath + "/../FrameworkProjectDataDir";
#endif

        public static string ContentRootPath { get; private set; }
        public static string ModRootPath { get; private set; }
        public static string SaveRootPath { get; private set; }

        /// <summary>
        /// 从入口脚本解析资源目录
        /// </summary>
        /// <param name="mainPath"></param>
        public static void Init()
        {
            SaveRootPath = Application.persistentDataPath.Replace("\\", "/");
            ContentRootPath = SaveRootPath + "/Content";
            ModRootPath = SaveRootPath + "/Mods";
#if UNITY_STANDALONE_WIN

#if UNITY_EDITOR
            string destDir = System.IO.File.ReadAllText(settingSavePath);
#else
            string destDir;
            if (File.Exists("./contentDir"))
            {
                destDir = System.IO.Path.GetFullPath(File.ReadAllText("./assetDir"));
            }
            else
            {
                DirectoryInfo pathInfo = new DirectoryInfo(Application.dataPath);
                destDir = pathInfo.Parent.FullName;
            }
#endif
            ContentRootPath = destDir + "/Content";
            ModRootPath = destDir + "/Mods";
#endif
            ContentRootPath.Replace("\\", "/");
            ModRootPath.Replace("\\", "/");
            Log.Print("游戏内容目录: " + ContentRootPath, Log.LogType.Game);
            Log.Print("游戏Mod目录: " + ModRootPath, Log.LogType.Game);
            Log.Print("游戏存档目录: " + SaveRootPath, Log.LogType.Game);
        }

        static List<string> searchPaths = new List<string>();
        //格式: 路径/?.lua
        static public bool AddSearchPath(string path, bool front = true)
        {
            Debug.Log("AddSearchPath : " + path);
            int index = searchPaths.IndexOf(path);
            if (index >= 0)
                return false;
            if (front)
                searchPaths.Insert(0, path);
            else
                searchPaths.Add(path);
            return true;
        }

        static public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            List<string> files = new List<string>();
            for (int i = 0; i < searchPaths.Count; i++)
            {
                string fullPath = string.Format("{0}/{1}", searchPaths[i], path);
                if (System.IO.Directory.Exists(fullPath))
                {
                    string[] dest = Directory.GetFiles(fullPath, searchPattern, searchOption);
                    files.AddRange(dest);
                }
            }
            return files.ToArray();
        }

        static public bool RemoveSearchPath(string path)
        {
            int index = searchPaths.IndexOf(path);
            if (index >= 0)
            {
                searchPaths.RemoveAt(index);
                return true;
            }
            return false;
        }
        /// <summary>
        /// e.g. Assets/Texture/11.png -> ${ContentRootPath}/Assets/Texture/11.png 
        /// or ${ModName}/Assets/Texture/11.png 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        static public string FindFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            if (System.IO.Path.IsPathRooted(fileName))
                return fileName;

            string fullPath;
            for (int i = 0; i < searchPaths.Count; i++)
            {
                fullPath = string.Format("{0}/{1}", searchPaths[i], fileName);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            fullPath = $"{ContentRootPath}/{fileName}";
            if (System.IO.File.Exists(fullPath))
            {
                return fullPath;
            }

            return null;
        }

        /// <summary>
        /// e.g. Assets/Texture/11.png -> ${ContentRootPath}/Assets/Texture/11.png 
        /// or ${ModName}/Assets/Texture/11.png 
        /// </summary>
        /// <param name="dirName"></param>
        /// <returns></returns>
        static public string FindDirectory(string dirName)
        {
            if (string.IsNullOrEmpty(dirName))
                return null;

            if (System.IO.Path.IsPathRooted(dirName))
                return dirName;

            string fullPath;
            for (int i = 0; i < searchPaths.Count; i++)
            {
                fullPath = string.Format("{0}/{1}", searchPaths[i], dirName);
                if (System.IO.Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            fullPath = $"{ContentRootPath}/{dirName}";
            if (System.IO.Directory.Exists(fullPath))
            {
                return fullPath;
            }

            return null;
        }

        static public bool IsPathRooted(string fileName)
        {
            return System.IO.Path.IsPathRooted(fileName);
        }
    }
}