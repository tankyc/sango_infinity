using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sango;

namespace Sango
{
    /// <summary>
    /// 该类可以在脚本根目录下建立根目录映射文件夹。可以将文件人为的划分管理
    /// </summary>
    public class LuaLoader : ScriptsLoaderBase
    {
        public List<string> searchingPath = new List<string>();
        public LuaLoader()
        {

        }

        public override void Init()
        {
            base.Init();

            for (int i = 0; i < searchingPath.Count; ++i)
            {
                addSearchingBundle(searchingPath[i].ToLower() + LUA_BUNDLE_LASTNAME);
            }
        }

        protected List<SearchingBundle> mSearchingBundles = new List<SearchingBundle>();
        public class SearchingBundle
        {
            public string bundle_name;
            public AssetBundle bundle;
            public SearchingBundle(string bundleName)
            {
                bundle_name = bundleName;
                Reload();
            }
            public void Unload()
            {
                if (bundle != null)
                    bundle.Unload(true);
            }
            public void Reload()
            {
                if (bundle != null)
                    bundle.Unload(true);
                if (!string.IsNullOrEmpty(bundle_name))
                {
                    bundle = LoadScriptsBundle(bundle_name);
                    if (bundle == null)
                    {
                        if (!Platform.isEditorMode)
                            Log.Error("无法加载AB" + bundle_name);
                    }
                }
            }
        }
        /// <summary>
        /// 重载
        /// </summary>
        /// <returns></returns>
        public override bool Reload()
        {
            foreach (SearchingBundle ab in mSearchingBundles)
            {
                ab.bundle.Unload(true);
                ab.bundle = LoadScriptsBundle(ab.bundle_name);
            }
            return true;
        }
        /// <summary>
        /// 清理状态
        /// </summary>
        public override void Clear()
        {
            foreach (SearchingBundle ab in mSearchingBundles)
            {
                if (ab.bundle != null)
                    ab.bundle.Unload(true);
            }
            mSearchingBundles.Clear();
        }

        protected override byte[] SearchingLuaInDisk(string fileName)
        {
            // 关闭真机直接读取lua文件的权限(仅作用于编辑器)
#if !UNITY_EDITOR
            //if (canReadFileInMobile)
            {
#endif
            string path;
            for (int i = searchingPath.Count - 1; i >= 0; --i)
            {
                // 这里多了一次string构建,可以考虑用replace来制作
                // 不过仅限于editor就无所谓了
                path = string.Format("{0}/{1}/{2}/{3}", Path.ContentRootPath, LUA_OUT_DIR, searchingPath[i], fileName);

                if (System.IO.File.Exists(path)) {
                    return System.IO.File.ReadAllBytes(path);
                }
            }

#if !UNITY_EDITOR
            }
#endif
            return base.SearchingLuaInDisk(fileName);
        }
        protected override byte[] SearchingLuaInAssetBundle(string fileName)
        {
            // 小写名字,文件名字会采用路径文件名字
            string searchingfileName = fileName.ToLower().Replace('/', '_');
            byte[] buffer = null;
            searchingfileName += ".bytes";

            for (int i = mSearchingBundles.Count - 1; i >= 0; --i)
            {
                SearchingBundle sb = mSearchingBundles[i];
                if (sb.bundle == null) continue;

                if (Config.isDebug)
                {
                    Log.Print("查找bundle ->" + sb.bundle_name + "  读取文件:  " + searchingfileName);
                }
#if UNITY_5_6_OR_NEWER
                TextAsset luaCode = sb.bundle.LoadAsset<TextAsset>(searchingfileName);
#else
                TextAsset luaCode = sb.bundle.Load(searchingfileName, typeof(TextAsset)) as TextAsset;
#endif
                if (luaCode != null)
                {
                    buffer = luaCode.bytes;
                    Resources.UnloadAsset(luaCode);
                    break;
                }
            }

            if (buffer == null) buffer = base.SearchingLuaInAssetBundle(fileName);
            return buffer;
        }
        /// <summary>
        /// 添加查找资源,后添加的资源优先查找(可用于覆盖以前的资源)
        /// </summary>
        /// <param name="bundleName">ab基于资源文件夹下面的相对路径</param>
        /// <returns></returns>
        public virtual bool addSearchingBundle(string bundleName, bool head = false)
        {
            SearchingBundle sb = new SearchingBundle(bundleName);
            if (!head)
                mSearchingBundles.Add(sb);
            else
                mSearchingBundles.Insert(0, sb);
            return true;
        }
        public virtual bool addSearchingPath(string path)
        {
            searchingPath.Add(path);
            addSearchingBundle(path.ToLower() + LUA_BUNDLE_LASTNAME);
            return true;
        }
    }
}