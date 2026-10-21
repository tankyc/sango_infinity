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
        public static string settingSavePath = Application.dataPath + "/../FrameworkProjectDataDir";
#endif

        public static string ContentRootPath { get; private set; }
        public static string CustomEditRootPath { get; private set; }
        public static string ModRootPath { get; private set; }
        public static string SaveRootPath { get; private set; }
        public static string StreamingAssetsPath { get; private set; }
        public static string PersistentDataPathPath { get; private set; }

        /// <summary>
        /// 从入口脚本解析资源目录
        /// </summary>
        /// <param name="mainPath"></param>
        public static void Init()
        {
            StreamingAssetsPath = Application.streamingAssetsPath;
            PersistentDataPathPath = Application.persistentDataPath;
            SaveRootPath = PersistentDataPathPath.Replace("\\", "/");
            ContentRootPath = SaveRootPath + "/Content";
            CustomEditRootPath = SaveRootPath + "/CustomEdit";
            ModRootPath = SaveRootPath + "/Mods";
#if UNITY_STANDALONE_WIN

#if UNITY_EDITOR
            string destDir = System.IO.File.ReadAllText(settingSavePath);
#else
            string destDir;
            if (File.Exists("./contentDir"))
            {
                destDir = System.IO.Path.GetFullPath(File.ReadAllText("./contentDir"));
            }
            else
            {
                DirectoryInfo pathInfo = new DirectoryInfo(Application.dataPath);
                destDir = pathInfo.Parent.FullName;
            }
#endif
            SaveRootPath = destDir;
            ContentRootPath = destDir + "/Content";
            ModRootPath = destDir + "/Mods";
            CustomEditRootPath = destDir + "/CustomEdit";

#else

#if UNITY_EDITOR
            string destDir = System.IO.File.ReadAllText(settingSavePath);
            ContentRootPath = destDir + "/Content";
            CustomEditRootPath = destDir + "/CustomEdit";
            ModRootPath = destDir + "/Mods";
            SaveRootPath = destDir;
#endif

#endif
            ContentRootPath = ContentRootPath.Replace("\\", "/");
            CustomEditRootPath = CustomEditRootPath.Replace("\\", "/");
            ModRootPath = ModRootPath.Replace("\\", "/");
            Log.Info("游戏内容目录: " + ContentRootPath, Log.LogType.Game);
            Log.Info("游戏Mod目录: " + ModRootPath, Log.LogType.Game);
            Log.Info("游戏存档目录: " + SaveRootPath, Log.LogType.Game);
            Log.Info("游戏自定义修改目录: " + CustomEditRootPath, Log.LogType.Game);
        }

        static List<string> searchPaths = new List<string>();
        //格式: 路径/?.lua
        static public bool AddSearchPath(string path, bool front = true)
        {
            int index = searchPaths.IndexOf(path);
            if (index >= 0)
                return false;           // 已登记过：不重复加、也不刷日志（Mod 重复初始化会走到这里）
            if (front)
                searchPaths.Insert(0, path);
            else
                searchPaths.Add(path);
            Log.Info("AddSearchPath : " + path, Log.LogType.Game);
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
        /// 图片扩展名候选表。内容目录里的贴图会批量换格式(png -> jpg),
        /// 代码里写死的旧扩展名路径靠它兜底命中。
        /// </summary>
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// e.g. Assets/Texture/11.png -> ${ContentRootPath}/Assets/Texture/11.png 
        /// or ${ModName}/Assets/Texture/11.png 
        /// 先按原路径精确查找;找不到且是图片扩展名时,再换同名其它图片格式查找。
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        static public string FindFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            // 模组私有资源引用(mod://模组Id/路径 或 模组Id:路径):
            // 只查该模组的 Private 目录,命中即返回;解析失败直接返回 null,不参与普通搜索、也不回落
            if (ModAssetPath.IsModUri(fileName))
                return ModAssetPath.Resolve(fileName);

            // ① 精确匹配,保证原路径优先级最高
            string fullPath = FindFileExact(fileName);
            if (!string.IsNullOrEmpty(fullPath))
                return fullPath;

            // ② 扩展名回退: png 换成 jpg/jpeg 再找一遍
            //    (贴图批量转格式后,代码里写死的 .png 路径不至于失效)
            string ext = System.IO.Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext))
                return null;

            string baseName = fileName.Substring(0, fileName.Length - ext.Length);
            for (int i = 0; i < ImageExtensions.Length; i++)
            {
                if (string.Equals(ImageExtensions[i], ext, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                fullPath = FindFileExact(baseName + ImageExtensions[i]);
                if (!string.IsNullOrEmpty(fullPath))
                    return fullPath;
            }

            return null;
        }

        /// <summary>
        /// 精确查找文件,不做任何扩展名推测
        /// </summary>
        /// <param name="fileName">相对资源路径</param>
        /// <returns>存在的完整路径,不存在返回 null</returns>
        static public string FindFileExact(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            if (System.IO.Path.IsPathRooted(fileName))
                return fileName;

            // 模组私有资源引用:只查指定模组的 Private 目录,不参与普通搜索
            if (ModAssetPath.IsModUri(fileName))
                return ModAssetPath.Resolve(fileName);

            string fullPath;
            // ① 普通搜索路径(各模组目录,后加载的模组优先)
            for (int i = 0; i < searchPaths.Count; i++)
            {
                fullPath = string.Format("{0}/{1}", searchPaths[i], fileName);
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // ② 内容根目录兜底
            fullPath = $"{ContentRootPath}/{fileName}";
            if (System.IO.File.Exists(fullPath))
            {
                return fullPath;
            }

            return null;
        }

        static public string FindFile(string[] fileName)
        {
            if (fileName == null)
                return null;

            string fullPath;
            // ① 模组私有资源引用:逐个候选按私有协议解析,命中即返回
            for (int j = 0; j < fileName.Length; j++)
            {
                if (ModAssetPath.IsModUri(fileName[j]))
                {
                    fullPath = ModAssetPath.Resolve(fileName[j]);
                    if (!string.IsNullOrEmpty(fullPath))
                        return fullPath;
                }
            }

            // ② 普通搜索路径(跳过私有协议引用,它们只走上面的私有解析)
            for (int i = 0; i < searchPaths.Count; i++)
            {
                for (int j = 0; j < fileName.Length; j++)
                {
                    if (ModAssetPath.IsModUri(fileName[j]))
                        continue;
                    fullPath = string.Format("{0}/{1}", searchPaths[i], fileName[j]);
                    if (System.IO.File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            for (int j = 0; j < fileName.Length; j++)
            {
                if (ModAssetPath.IsModUri(fileName[j]))
                    continue;
                fullPath = $"{ContentRootPath}/{fileName[j]}";
                if (System.IO.File.Exists(fullPath))
                {
                    return fullPath;
                }
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

            // 模组私有资源引用:只查指定模组的 Private 目录,不参与普通搜索
            if (ModAssetPath.IsModUri(dirName))
                return ModAssetPath.ResolveDirectory(dirName);

            string fullPath;
            // ① 普通搜索路径(各模组目录,后加载的模组优先)
            for (int i = 0; i < searchPaths.Count; i++)
            {
                fullPath = string.Format("{0}/{1}", searchPaths[i], dirName);
                if (System.IO.Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            // ② 内容根目录兜底
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