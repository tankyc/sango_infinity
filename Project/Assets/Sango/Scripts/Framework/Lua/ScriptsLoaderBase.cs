/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;
using System;
using System.IO;
using UnityEngine;

namespace Sango
{
    /// <summary>
    /// 基础脚本读取类，可以扩展为自定义脚本结构。
    /// 默认结构为脚本打包成一个ab文件。
    /// </summary>
    [Serializable]
    public class ScriptsLoaderBase : LuaFileUtils
    {
        public const string LUA_LASTNAME = ".lua";
        public const string LUA_OUT_DIR = "Lua";
        public const string LUA_BUNDLE_NAME = "scripts.script";
        public const string LUA_BUNDLE_LASTNAME = ".script";

        protected bool canReadFileInMobile = false;
        protected AssetBundle scriptBundle;
        public ScriptsLoaderBase()
        {
            beZip = true;
        }
        public virtual void Init()
        {
            instance = this;
            canReadFileInMobile = Directory.Exists(string.Format("{0}/{1}", Path.ContentRootPath, LUA_OUT_DIR));
            scriptBundle = LoadScriptsBundle(LUA_BUNDLE_NAME);
        }
        /// <summary>
        /// 重载
        /// </summary>
        /// <returns></returns>
        public virtual bool Reload()
        {
            if (scriptBundle != null)
                scriptBundle.Unload(true);
            scriptBundle = LoadScriptsBundle(LUA_BUNDLE_NAME);
            return true;
        }
        /// <summary>
        /// 清理状态
        /// </summary>
        public virtual void Clear()
        {
            if (scriptBundle != null)
                scriptBundle.Unload(true);
            scriptBundle = null;
        }
        public override byte[] ReadFile(string fileName)
        {
            // fileName like aa/bb/cc/dd or aa/bb/cc/dd.lua
            // 优先使用外部整包资源
            if (Config.isDebug)
                Log.Print("加载lua文件: " + fileName, Log.LogType.Script);

            if (!fileName.EndsWith(LUA_LASTNAME))
                fileName += LUA_LASTNAME;

            byte[] rs = SearchingLuaInDisk(fileName);
            if (rs != null) return rs;

            return SearchingLuaInAssetBundle(fileName);
        }
        protected virtual byte[] SearchingLuaInDisk(string fileName)
        {
            // 关闭真机直接读取lua文件的权限(仅作用于编辑器)
#if !UNITY_EDITOR
            //if (canReadFileInMobile)
            {
#endif
            if(System.IO.Path.IsPathRooted(fileName)) {
                if (System.IO.File.Exists(fileName)) {
                    return System.IO.File.ReadAllBytes(fileName);
                }
            }

            //for (int i = 0; i < searchPaths.Count; i++)
            //{
            //    string fullPath = string.Format("{0}/{1}", searchPaths[i], fileName);
            //    if (System.IO.File.Exists(fullPath))
            //    {
            //        return System.IO.File.ReadAllBytes(fileName);
            //    }
            //}

            string root_path = string.Format("{0}/{1}", Path.ContentRootPath, fileName);
            if (System.IO.File.Exists(root_path)) {
                return System.IO.File.ReadAllBytes(root_path);
            }

            string path = string.Format("{0}/{1}/{2}", Path.ContentRootPath, LUA_OUT_DIR, fileName);
            if (System.IO.File.Exists(path)) {
                return System.IO.File.ReadAllBytes(path);
            }
            else {
                if (System.IO.File.Exists(fileName)) {
                    return System.IO.File.ReadAllBytes(fileName);
                }
            }
#if !UNITY_EDITOR
            }
#endif
            return null;
        }
        protected virtual byte[] SearchingLuaInAssetBundle(string fileName)
        {
            if (scriptBundle == null) return null;

            // 小写名字
            fileName = fileName.ToLower();

            //  文件名字会采用路径文件名字
            fileName = fileName.Replace('/', '_');

            byte[] buffer = null;
            fileName += ".bytes";

#if UNITY_5_6_OR_NEWER
            TextAsset luaCode = scriptBundle.LoadAsset<TextAsset>(fileName);
#else
            TextAsset luaCode = scriptBundle.Load(finalName, typeof(TextAsset)) as TextAsset;
#endif
            if (luaCode != null)
            {
                buffer = luaCode.bytes;
                Resources.UnloadAsset(luaCode);
            }

            return buffer;
        }
        /// <summary>
        /// 加载ab
        /// </summary>
        /// <param name="bundleName">ab基于mResourcePath下的相对路径</param>
        /// <returns></returns>
        protected static AssetBundle LoadScriptsBundle(string bundleName)
        {
            return Platform.isEditorMode ? null : AssetBundleManager.CreateFromFile(GetBundlePath(bundleName));
        }

        protected static string GetBundlePath(string bundleName)
        {
            return string.Format("{0}/{1}", LUA_OUT_DIR, bundleName);
        }

        /// <summary>
        /// 将文件基于LUA_OUT_DIR下的文件名重命名
        /// 打包代码的时候会传入aa_bb_cc_dd.lua
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public virtual string TransforomPackName(string fileName)
        {
            return fileName;
        }

    }
}