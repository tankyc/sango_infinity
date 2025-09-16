/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango
{
    /// <summary>
    /// 所有会返回ab索引的地方，都会增加资源引用，必须保存ab索引并手动释放
    /// </summary>
    public class AssetBundleManager : Singletion<AssetBundleManager>
    {
        public static readonly int InvalidId = -1;

        public static float ASSETBUNDLELIFE = 20;
        [NoToLua]
        public delegate void AsyncCreateFinishCall(int index);
        [NoToLua]
        public delegate void AsyncLoadAssetFinishCall(UnityEngine.Object obj, int abIndex);
        [NoToLua]
        public delegate void AsyncLoadAllAssetFinishCall(UnityEngine.Object[] obj, int abIndex);

        /// <summary>
        /// 资源信息
        /// </summary>
        public class AssetBundleInfo
        {
            public string fileName;
            public int index;
            private int _refCount = 0;
            public float life = 0;
#if UNITY_EDITOR
            [System.NonSerialized]
            [NoToLua]
            public int _direct_refCount = 0;
            [System.NonSerialized]
            [NoToLua]
            public int _depend_refCount = 0;
#endif
            public int refCount
            {
                get { return _refCount; }
                set
                {
                    _refCount = value;
#if UNITY_EDITOR
                    ////XLog.Log(fileName + " ref: " + _refCount);
#endif
                }
            }
            public AssetBundleCreateRequest request;
            public AssetBundle ab;
            public List<AsyncCreateFinishCall> asyncCreateCalls = new List<AsyncCreateFinishCall>();
            public List<LuaFunction> asyncCreateCallsForLua = new List<LuaFunction>();
            public List<AssetBundleLoadAssetInfo> asyncLoadTask = new List<AssetBundleLoadAssetInfo>();
            public List<AssetBundleInfo> depends = null;
            public void Run()
            {
                if (request != null)
                {
                    request.completed += OnAssetBundleCreateRequestCompleted;
                    asyncLoadList.Add(this);
                }
            }
            public void Clear()
            {
                if (ab != null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("卸载AssetBundle : " + fileName);
#endif
                    depends = null;
                    ab.Unload(true);
                    ab = null;
                }
            }
            protected static List<AssetBundleInfo> asyncLoadList = new List<AssetBundleInfo>();
            /// <summary>
            /// 异步加载回调
            /// </summary>
            /// <param name="data"></param>
            public void OnAssetBundleCreateRequestCompleted(AsyncOperation data)
            {
                AssetBundleCreateRequest abcr = (AssetBundleCreateRequest)data;
                // 先设置AB信息
                ab = abcr.assetBundle;

                // 按顺序处理回调，一直保证是依赖资源先加载，最后进行需求资源的操作
                while (asyncLoadList.Count > 0)
                {
                    AssetBundleInfo info = asyncLoadList[0];
                    if (info.request != null && !info.request.isDone)
                        break;

                    asyncLoadList.RemoveAt(0);

                    // 这个情况说明该资源已被手动卸载
                    if (info.refCount <= 0)
                    {
                        info.asyncCreateCalls.Clear();
                        info.asyncCreateCallsForLua.Clear();
                        info.asyncLoadTask.Clear();
                        continue;
                    }

                    if (info.request.isDone)
                    {
                        info.request.completed -= OnAssetBundleCreateRequestCompleted;
                        info.request = null;

                        foreach (AsyncCreateFinishCall call in info.asyncCreateCalls)
                        {
                            try
                            {
                                call(info.index);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError(e.Message + e.StackTrace);
                            }
                        }
                        info.asyncCreateCalls.Clear();

                        foreach (LuaFunction call in info.asyncCreateCallsForLua)
                        {
                            try
                            {
                                call.BeginPCall();
                                call.Push(info.index);
                                call.PCall();
                                call.EndPCall();
                            }
                            catch (Exception e)
                            {
                                Debug.LogError(e.Message + e.StackTrace);
                            }
                        }
                        info.asyncCreateCallsForLua.Clear();

                        // 继续读取任务
                        foreach (AssetBundleLoadAssetInfo task in info.asyncLoadTask)
                        {
                            task.Run();
                        }
                    }
                }
            }
            public void Release()
            {
                refCount--;
                if (refCount == 0)
                {
                    life = ASSETBUNDLELIFE;
#if UNITY_EDITOR
                    //XLog.Log("--释放资源信息：" + fileName);
#endif
                    if (depends != null)
                    {
                        foreach (AssetBundleInfo d in depends)
                        {

                            d.Release();
#if UNITY_EDITOR
                            //XLog.Log("释放依赖信息：" + d.fileName);
                            d._depend_refCount--;
#endif
                        }
                    }
                }
                else if (refCount < 0)
                {
                    refCount = 0;
#if UNITY_EDITOR
                    Log.Error("释放一个已经没有引用的资源信息" + fileName);
#endif
                }
            }
            public bool IsValid()
            {
                return ab != null;
            }
            public void AddReference()
            {
                refCount++;
                if (refCount == 1)
                {
#if UNITY_EDITOR
                    //XLog.Log("**创建资源信息：" + fileName);
#endif
                    if (depends != null)
                    {
                        foreach (AssetBundleInfo d in depends)
                        {
                            d.AddReference();
#if UNITY_EDITOR
                            d._depend_refCount++;
#endif
                        }
                    }
                }

            }
            public void Unload(bool unloadRes)
            {
                if (ab != null)
                    ab.Unload(unloadRes);
            }

            public bool ContainsAsset(string resName)
            {
                if (ab != null) return ab.Contains(resName);
                return false;
            }

            public UnityEngine.Object LoadAsset(string resName)
            {
                if (ab != null) return ab.LoadAsset(resName);
                return null;
            }
            public UnityEngine.Object LoadAsset(string resName, Type t)
            {
                if (ab != null) return ab.LoadAsset(resName, t);
                return null;
            }
            public T LoadAsset<T>(string resName) where T : UnityEngine.Object
            {
                if (ab != null) return ab.LoadAsset<T>(resName);
                return null;
            }
            public AssetBundleRequest LoadAssetAsync(string resName)
            {
                if (ab != null) return ab.LoadAssetAsync(resName);
                return null;
            }
            public AssetBundleRequest LoadAssetAsync(string resName, Type t)
            {
                if (ab != null) return ab.LoadAssetAsync(resName, t);
                return null;
            }
            public AssetBundleRequest LoadAssetAsync<T>(string resName) where T : UnityEngine.Object
            {
                if (ab != null) return ab.LoadAssetAsync<T>(resName);
                return null;
            }
            public UnityEngine.Object[] LoadAllAssets()
            {
                if (ab != null) return ab.LoadAllAssets();
                return null;
            }
            public UnityEngine.Object[] LoadAllAssets(Type t)
            {
                if (ab != null) return ab.LoadAllAssets(t);
                return null;
            }
            public AssetBundleRequest LoadAllAssetsAsync()
            {
                if (ab != null) return ab.LoadAllAssetsAsync();
                return null;
            }
            public AssetBundleRequest LoadAllAssetsAsync(Type t)
            {
                if (ab != null) return ab.LoadAllAssetsAsync(t);
                return null;
            }
        }

#if USE_RUNTIME_ASSETS && UNITY_EDITOR
        private class EditorLoadAssetInfo
        {
            public UnityEngine.Object obj;
            public UnityEngine.Object[] objs;
            public AsyncLoadAssetFinishCall asyncCall;
            public AsyncLoadAllAssetFinishCall asyncAllCall;
            public LuaFunction asyncCallForLua;
            public LuaFunction asyncAllCallForLua;
            public void Run()
            {
                // 这个情况说明该资源已被手动卸载
                if (asyncCallForLua != null)
                {
                    try
                    {
                        asyncCallForLua.BeginPCall();
                        asyncCallForLua.Push(obj);
                        asyncCallForLua.Push(XAssetBundleManager.InvalidId);
                        asyncCallForLua.PCall();
                        asyncCallForLua.EndPCall();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
                else if (asyncAllCallForLua != null)
                {
                    try
                    {
                        asyncAllCallForLua.BeginPCall();
                        asyncAllCallForLua.Push(objs);
                        asyncAllCallForLua.Push(XAssetBundleManager.InvalidId);
                        asyncAllCallForLua.PCall();
                        asyncAllCallForLua.EndPCall();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
                else if (asyncAllCall != null)
                {
                    try
                    {
                        asyncAllCall(objs, XAssetBundleManager.InvalidId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
                else if (asyncCall != null)
                {
                    try
                    {
                        asyncCall(obj, XAssetBundleManager.InvalidId);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
            }
        }
        private List<EditorLoadAssetInfo> editorAsyncLoad = new List<EditorLoadAssetInfo>();
#endif

        /// <summary>
        /// 加载AB中的Asset信息
        /// </summary>
        public class AssetBundleLoadAssetInfo
        {
            public AssetBundleInfo abInfo;
            public string resName;
            public Type loadType;
            public AsyncLoadAssetFinishCall asyncCall;
            public AsyncLoadAllAssetFinishCall asyncAllCall;
            public LuaFunction asyncCallForLua;
            public LuaFunction asyncAllCallForLua;
            public AssetBundleRequest request;
            public AssetBundleLoadAssetInfo(AssetBundleInfo abInfo, string resName, AsyncLoadAssetFinishCall asyncCall, Type loadType = null)
            {
                this.abInfo = abInfo;
                this.resName = resName;
                this.asyncCall = asyncCall;
                this.loadType = loadType;
            }
            public AssetBundleLoadAssetInfo(AssetBundleInfo abInfo, string resName, LuaFunction asyncCall, Type loadType = null)
            {
                this.abInfo = abInfo;
                this.resName = resName;
                this.asyncCallForLua = asyncCall;
                this.loadType = loadType;
            }
            public AssetBundleLoadAssetInfo(AssetBundleInfo abInfo, AsyncLoadAllAssetFinishCall asyncAllCall, Type loadType = null)
            {
                this.abInfo = abInfo;
                resName = null;
                this.asyncAllCall = asyncAllCall;
                this.loadType = loadType;
            }
            public AssetBundleLoadAssetInfo(AssetBundleInfo abInfo, LuaFunction asyncAllCall, Type loadType = null)
            {
                this.abInfo = abInfo;
                resName = null;
                this.asyncAllCallForLua = asyncAllCall;
                this.loadType = loadType;

            }
            void Clear()
            {
                abInfo = null;
                asyncCall = null;
                asyncAllCall = null;
                resName = null;
                request = null;
                asyncCallForLua = null;
                asyncAllCallForLua = null;
            }
            public void Run()
            {
                if (abInfo == null || !abInfo.IsValid())
                {
                    Clear();
                    return;
                }
                AssetBundle ab = abInfo.ab;

                // 这里判断该用那种加载函数
                if (asyncAllCall != null || asyncAllCallForLua != null)
                {
                    if (loadType != null)
                        request = ab.LoadAllAssetsAsync(loadType);
                    else
                        request = ab.LoadAllAssetsAsync();
                }
                else
                {
                    if (loadType != null)
                        request = ab.LoadAssetAsync(resName, loadType);
                    else
                        request = ab.LoadAssetAsync(resName);
                }

                // 保存信息，等待完成时候处理
                if (request != null)
                    request.completed += OnAssetBundleLoadAssetRequestCompleted;
                else
                {
                    // 包内没有这个资源
                    OnAssetBundleLoadAssetRequestCompleted(null);
                }
            }
            public void OnAssetBundleLoadAssetRequestCompleted(AsyncOperation data)
            {
                AssetBundleRequest abr = null;
                if (data != null)
                {
                    abr = (AssetBundleRequest)data;
                    request.completed -= OnAssetBundleLoadAssetRequestCompleted;
                }

                abInfo.asyncLoadTask.Remove(this);

                // 这个情况说明该资源已被手动卸载
                if (asyncCallForLua != null)
                {
                    try
                    {
                        asyncCallForLua.BeginPCall();
                        asyncCallForLua.Push(abr != null ? abr.asset : (object)null);
                        asyncCallForLua.Push(abInfo.index);
                        asyncCallForLua.PCall();
                        asyncCallForLua.EndPCall();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
                else if (asyncAllCallForLua != null)
                {
                    try
                    {
                        asyncAllCallForLua.BeginPCall();
                        asyncAllCallForLua.Push(abr != null ? abr.allAssets : (object)null);
                        asyncAllCallForLua.Push(abInfo.index);
                        asyncAllCallForLua.PCall();
                        asyncAllCallForLua.EndPCall();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
                else if (asyncAllCall != null)
                {
                    try
                    {
                        asyncAllCall(abr != null ? abr.allAssets : null, abInfo.index);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
                else if (asyncCall != null)
                {
                    try
                    {
                        asyncCall(abr != null ? abr.asset : null, abInfo.index);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message + e.StackTrace);
                    }
                }
            }
        }

        protected const int ARRAY_INCREASE_LENGTH = 128;
        public const string MANIFEST_NAME = "assetsinfo";


        [NoToLua]
        public static AssetBundleInfo[] GetInfos()
        {
            return Instance.loadedAssetBundles;
        }
        [NoToLua]
        public static AssetBundleInfo GetInfo(int index)
        {
            return Instance._GetAssetBundleInfo(index);
        }

        protected AssetBundleInfo[] loadedAssetBundles = new AssetBundleInfo[ARRAY_INCREASE_LENGTH];
        protected Dictionary<string, AssetBundleInfo> loadedAssetBundlesMap = new Dictionary<string, AssetBundleInfo>();

        protected int _AssetBundleIndex = 0;
        [NoToLua]
        protected UnityEngine.AssetBundleManifest abManifest;
        private List<string> fileNames = new List<string>();
#if UNITY_EDITOR
        protected UnityEngine.AssetBundleManifest abManifest_add;
#endif
        public AssetBundleManager()
        {
            _Init();
            //Register();
        }

        private void _Init()
        {
#if RES_HISTORY
            XAssetRecorder.Instance.LoadConfig();
#endif

            //#if UNITY_EDITOR
            //            if (!File.Exists(FileManager.GetFile(MANIFEST_NAME)))
            //            {
            //#if !USE_RUNTIME_ASSETS
            //                Debug.LogError("资源信息文件加载失败，你需要导出一份资源信息文件，否则无法正确加载资源。");
            //#endif
            //                manifest = AssetBundleManifest.CreateFormString("");
            //                //return;
            //            }
            //#endif
            //            // 加载主信息
            //            AssetBundle ab = _CreateFromFile(MANIFEST_NAME);
            //            if (ab != null)
            //            {
            //                TextAsset str = ab.LoadAsset<TextAsset>("assetsinfo");
            //                manifest = AssetBundleManifest.CreateFormString(str.text);
            //                Resources.UnloadAsset(str);
            //                ab.Unload(true);
            //            }

            //#if UNITY_EDITOR
            //            // 加载额外信息
            //            if (File.Exists(FileManager.GetFileOutPackage(MANIFEST_NAME + "_add")))
            //            {
            //                ab = _CreateFromFile(MANIFEST_NAME + "_add");
            //                if (ab != null)
            //                {
            //                    TextAsset str = ab.LoadAsset<TextAsset>("assetsinfo");
            //                    manifest.Upgrade(AssetBundleManifest.CreateFormString(str.text));
            //                    Resources.UnloadAsset(str);
            //                    ab.Unload(true);
            //                }
            //            }
            //#endif

            //            // 继续加载额外的资源信息,直到找不到信息为止
            //            int i = 1;
            //            while (File.Exists(FileManager.GetFileOutPackage(MANIFEST_NAME + i)))
            //            {
            //                AssetBundle abInfo = _CreateFromFile(MANIFEST_NAME + i);
            //                TextAsset str = abInfo.LoadAsset<TextAsset>("assetsinfo");
            //                manifest.Upgrade(AssetBundleManifest.CreateFormString(str.text));
            //                Resources.UnloadAsset(str);
            //                abInfo.Unload(true);
            //                i++;
            //            }

            // 加载AB信息
            AssetBundle ab_Manifest = _CreateFromFile(PlatformUtility.GetPlatformName());
            if (ab_Manifest != null)
            {
                abManifest = ab_Manifest.LoadAsset<UnityEngine.AssetBundleManifest>("AssetBundleManifest");
                ab_Manifest.Unload(false);
#if UNITY_EDITOR
                // 为AB生成固定索引信息
                if (abManifest != null)
                {
                    string[] abNames = abManifest.GetAllAssetBundles();
                    foreach (string n in abNames)
                    {
                        string loopName = CheckLoop(n);
                        if (loopName != null)
                        {
                            Debug.LogError(string.Format("资源循环引用：{0} - {1} ", n, loopName));
                        }
                    }
                }
#endif
            }
            //#if UNITY_EDITOR
            //            if (File.Exists(FileManager.GetFileOutPackage(PlatformUtility.GetPlatformName() + "_add")))
            //            {
            //                AssetBundle ab_abManifest_add = _CreateFromFile(PlatformUtility.GetPlatformName() + "_add");
            //                if (ab_abManifest_add != null)
            //                {
            //                    abManifest_add = ab_abManifest_add.LoadAsset<UnityEngine.AssetBundleManifest>("AssetBundleManifest");
            //                    ab_abManifest_add.Unload(false);
            //                }
            //            }
            //#endif
        }

        void _Clear(bool unload)
        {
            for (int i = 0; i < loadedAssetBundles.Length; ++i)
            {
                AssetBundleInfo abInfo = loadedAssetBundles[i];
                if (abInfo != null)
                {
                    abInfo.Unload(unload);
                }
                loadedAssetBundles[i] = null;
            }
            loadedAssetBundlesMap.Clear();
        }

        string[] _GetAllDependencies(string fileName)
        {
#if UNITY_EDITOR
            if (abManifest_add != null)
            {
                string[] rs = abManifest_add.GetAllDependencies(fileName);
                if (rs != null && rs.Length > 0)
                    return rs;
            }
#endif
            if (abManifest != null)
                return abManifest.GetAllDependencies(fileName);
            return null;
        }

        string[] _GetDirectDependencies(string fileName)
        {
#if UNITY_EDITOR
            if (abManifest_add != null)
            {
                string[] rs = abManifest_add.GetDirectDependencies(fileName);
                if (rs != null && rs.Length > 0)
                    return rs;
            }
#endif
            if (abManifest != null)
                return abManifest.GetDirectDependencies(fileName);
            return null;
        }

        protected AssetBundle _CreateFromFile(string fileName)
        {
            AssetBundle bundle = null;

            if (System.IO.File.Exists(fileName))
            {
                try
                {
                    bundle = AssetBundle.LoadFromFile(fileName);
                    if (bundle != null)
                        return bundle;
                }
                catch (Exception e)
                {
                    Debug.LogError("资源文件无法加载 error:" + e.Message);
                }
            }

            return bundle;
        }
        protected AssetBundleCreateRequest _CreateFromFileAsync(string fileName)
        {
            AssetBundleCreateRequest bundle = null;

            if (System.IO.File.Exists(fileName))
            {
                try
                {
                    bundle = AssetBundle.LoadFromFileAsync(fileName);
                    return bundle;
                }
                catch (Exception e)
                {
                    Debug.LogError("资源文件无法加载 error:" + e.Message);
                }
            }
            return bundle;
        }


        protected AssetBundleInfo _CreateAssetBundleInfo(string fileName)
        {
            AssetBundleInfo bundleInfo = null;
            if (!loadedAssetBundlesMap.TryGetValue(fileName, out bundleInfo))
            {
                bundleInfo = new AssetBundleInfo();
                bundleInfo.fileName = fileName;
                bundleInfo.index = _AssetBundleIndex++;
                if (_AssetBundleIndex == loadedAssetBundles.Length)
                {
                    Array.Resize<AssetBundleInfo>(ref loadedAssetBundles, loadedAssetBundles.Length + ARRAY_INCREASE_LENGTH);
                }
                loadedAssetBundlesMap.Add(fileName, bundleInfo);
                loadedAssetBundles[bundleInfo.index] = bundleInfo;
            }
            return bundleInfo;
        }


        protected AssetBundleInfo _SetAssetInfo(AssetBundleInfo info, AssetBundle ab)
        {
            if (info != null && ab != null)
            {
                info.ab = ab;
                loadedAssetBundles[info.index] = info;
            }
            return info;
        }

#if UNITY_EDITOR
        [NoToLua]
        public string CheckLoop(string fileName)
        {
            string[] loops = abManifest.GetAllDependencies(fileName);
            if (loops != null && loops.Length > 0)
            {
                foreach (string s in loops)
                {
                    string[] loops_2 = abManifest.GetAllDependencies(s);
                    foreach (string s2 in loops_2)
                    {
                        if (s2.Equals(fileName))
                        {
                            return s;
                        }
                    }
                }
            }
            return null;
        }
#endif

        /// <summary>
        /// *核心*同步创建资源信息
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        protected AssetBundleInfo _CreateInfoFromFile(string fileName, bool isDependCreate = false)
        {

#if UNITY_EDITOR
            //XLog.Log("==加载资源信息：" + fileName);
#endif
            AssetBundleInfo bundleInfo = _CreateAssetBundleInfo(fileName);
            if (bundleInfo.depends == null)
            {
                bundleInfo.depends = new List<AssetBundleInfo>();
                // 建立依赖信息
                string[] depends = _GetDirectDependencies(fileName);
                if (depends != null)
                {
                    foreach (string d in depends)
                    {
#if UNITY_EDITOR
                        //XLog.Log("==--加载依赖资源信息：" + fileName + "  ---->  " + d);
#endif
                        bundleInfo.depends.Add(_CreateInfoFromFile(d, true));
                    }
                }
            }

            if (!isDependCreate)
            {
                bundleInfo.AddReference();
#if UNITY_EDITOR
                //XLog.LogError(XLua.stackMsg( "==加载资源信息：" + fileName));
                bundleInfo._direct_refCount++;
#endif
            }

            if (bundleInfo.IsValid())
            {
                return bundleInfo;
            }

            //处理异步
            if (bundleInfo.request == null)
            {
                bundleInfo.ab = _CreateFromFile(fileName);
            }
            else
            {
                bundleInfo.ab = bundleInfo.request.assetBundle;
            }
            return bundleInfo;
        }
        /// <summary>
        /// *核心*异步创建资源信息
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        protected AssetBundleInfo _CreateInfoFromFileAsync(string fileName, bool isDependCreate = false)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

#if UNITY_EDITOR
            if (Config.isDebug)
                Log.Print("==异步加载资源信息：" + fileName);
#endif

            AssetBundleInfo bundleInfo = _CreateAssetBundleInfo(fileName);

            if (bundleInfo.depends == null)
            {
                bundleInfo.depends = new List<AssetBundleInfo>();
                // 建立依赖信息
                string[] depends = _GetDirectDependencies(fileName);
                if (depends != null)
                {
                    foreach (string d in depends)
                    {
#if UNITY_EDITOR
                        //XLog.Log("==--异步加载依赖资源信息：" + fileName + "  ---->  " + d);
#endif
                        bundleInfo.depends.Add(_CreateInfoFromFileAsync(d, true));
                    }
                }

            }

            if (!isDependCreate)
            {
                bundleInfo.AddReference();
#if UNITY_EDITOR
                bundleInfo._direct_refCount++;
                //XLog.LogError(XLua.stackMsg( "==加载资源信息：" + fileName));
#endif
            }

            if (bundleInfo.IsValid())
            {
                return bundleInfo;
            }

            if (bundleInfo.request == null)
            {
                bundleInfo.request = _CreateFromFileAsync(fileName);
                bundleInfo.Run();
            }
            return bundleInfo;
        }

        protected AssetBundle _GetAssetBundle(int index)
        {
            AssetBundleInfo info = _GetAssetBundleInfo(index);
            if (info != null)
                return info.ab;
            return null;
        }
        protected AssetBundleInfo _GetAssetBundleInfo(int index)
        {
            if (index < 0 || index >= loadedAssetBundles.Length)
                return null;
            return loadedAssetBundles[index];
        }
        int _GetAssetBundleIndex(string fileName)
        {
            AssetBundleInfo bundleInfo = null;
            if (loadedAssetBundlesMap.TryGetValue(fileName, out bundleInfo))
            {
                return bundleInfo.index;
            }
            return AssetBundleManager.InvalidId;
        }
        int _Load(string fileName)
        {
#if USE_RUNTIME_ASSETS && UNITY_EDITOR
            return XAssetBundleManager.InvalidId;
#else
            return _CreateInfoFromFile(fileName).index;
#endif
        }

        /// <summary>
        /// AB创建的异步存档，解决栈内返回的时序问题，异步永远是在栈外回调
        /// </summary>
        private class AsyncCreateCall
        {
            AsyncCreateFinishCall call;
            LuaFunction luacall;
            AssetBundleInfo info;
            public AsyncCreateCall(AssetBundleInfo info, AsyncCreateFinishCall call, LuaFunction luacall)
            {
                this.call = call;
                this.luacall = luacall;
                this.info = info;
            }
            public void Call()
            {
                int abIndex = info != null ? info.index : AssetBundleManager.InvalidId;
                if (call != null)
                    call(abIndex);

                if (luacall != null)
                {
                    luacall.BeginPCall();
                    luacall.Push(abIndex);
                    luacall.PCall();
                    luacall.EndPCall();
                }
            }
        }
        List<AsyncCreateCall> asyncCreateCall = new List<AsyncCreateCall>();

        int _LoadAsync(string fileName, AsyncCreateFinishCall call, LuaFunction luacall)
        {

#if USE_RUNTIME_ASSETS && UNITY_EDITOR
            asyncCreateCall.Add(new AsyncCreateCall(null, call, luacall));
            return XAssetBundleManager.InvalidId;
#else
            AssetBundleInfo info = _CreateInfoFromFileAsync(fileName);
            if (info != null && info.request != null)
            {
                if (call != null)
                    info.asyncCreateCalls.Add(call);
                if (luacall != null)
                    info.asyncCreateCallsForLua.Add(luacall);
            }
            else
            {
                asyncCreateCall.Add(new AsyncCreateCall(info, call, luacall));
            }
            return info != null ? info.index : AssetBundleManager.InvalidId;
#endif
        }
        int _LoadAsync(string fileName, AsyncCreateFinishCall call)
        {
            return _LoadAsync(fileName, call, null);
        }
        int _LoadAsync(string fileName, LuaFunction call)
        {
            return _LoadAsync(fileName, null, call);

        }
        void _Load(string fileName, AsyncCreateFinishCall call, bool async = true)
        {
            AssetBundleInfo info = async ? _CreateInfoFromFile(fileName) : _CreateInfoFromFileAsync(fileName);
            if (info.request != null)
                info.asyncCreateCalls.Add(call);
            else
            {
                call(info.index);
            }
        }
        void _Unload(int index)
        {
            AssetBundleInfo abInfo = index >= 0 && index < loadedAssetBundles.Length ? loadedAssetBundles[index] : null;
            if (abInfo != null)
            {
                abInfo.Release();
#if UNITY_EDITOR
                abInfo._direct_refCount--;
#endif
            }
        }
        bool _ContainsAsset(int index, string name)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return false;
            return ab.ContainsAsset(name);
        }
        UnityEngine.Object _LoadAsset(int index, string name)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAsset(name);
        }
        UnityEngine.Object _LoadAsset(int index, string name, Type t)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAsset(name, t);
        }
        AssetBundleRequest _LoadAssetAsync(int index, string name)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAssetAsync(name);
        }
        AssetBundleRequest _LoadAssetAsync(int index, string name, Type t)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAssetAsync(name, t);
        }

        UnityEngine.Object[] _LoadAllAssets(int index)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAllAssets();
        }
        UnityEngine.Object[] _LoadAllAssets(int index, Type t)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAllAssets(t);
        }
        AssetBundleRequest _LoadAllAssetsAsync(int index)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAllAssetsAsync();
        }
        AssetBundleRequest _LoadAllAssetsAsync(int index, Type t)
        {
            AssetBundleInfo ab = _GetAssetBundleInfo(index);
            if (ab == null) return null;
            return ab.LoadAllAssetsAsync(t);
        }

        void _LoadInfo(string fileName, out int index, out AssetBundle ab)
        {
            AssetBundleInfo info = _CreateInfoFromFile(fileName);
            index = info.index;
            ab = info.ab;
        }


        /// <summary>
        /// *警告*用此方法创建的AB不受管理器管理，需要管理请调用Load方法
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static AssetBundle CreateFromFile(string fileName)
        {
            return Instance._CreateFromFile(fileName);
        }
        public static AssetBundle GetAssetBundle(int index)
        {
            return Instance._GetAssetBundle(index);
        }

        public static int Load(string fileName)
        {
            return Instance._Load(fileName);
        }
        [NoToLua]
        public static int LoadAsync(string fileName, AsyncCreateFinishCall completed)
        {
            return Instance._LoadAsync(fileName, completed);
        }
        public static int LoadAsync(string fileName, LuaFunction completed)
        {
            return Instance._LoadAsync(fileName, completed);
        }
        public static void Unload(int index)
        {
            Instance._Unload(index);
        }
        public static void Unload(string fileName)
        {
            Instance._Unload(NameToIndex(fileName));
        }
        public static bool ContainsAsset(int index, string resName)
        {
#if RES_HISTORY
                XAssetRecorder.Instance.OnResLoad(resName);
#endif
            return Instance._ContainsAsset(index, resName);
        }
        public static UnityEngine.Object LoadAsset(int index, string resName)
        {
#if RES_HISTORY
                XAssetRecorder.Instance.OnResLoad(resName);
#endif
            return Instance._LoadAsset(index, resName);
        }
        public static UnityEngine.Object LoadAsset(int index, string resName, Type t)
        {
#if RES_HISTORY
                XAssetRecorder.Instance.OnResLoad(resName);
#endif
            return Instance._LoadAsset(index, resName, t);
        }
        public static AssetBundleRequest LoadAssetAsync(int index, string resName)
        {
#if RES_HISTORY
                XAssetRecorder.Instance.OnResLoad(resName);
#endif
            return Instance._LoadAssetAsync(index, resName);
        }
        public static AssetBundleRequest LoadAssetAsync(int index, string resName, Type t)
        {
#if RES_HISTORY
                XAssetRecorder.Instance.OnResLoad(resName);
#endif
            return Instance._LoadAssetAsync(index, resName, t);
        }
        public static UnityEngine.Object[] LoadAllAssets(int index)
        {
            return Instance._LoadAllAssets(index);
        }
        public static UnityEngine.Object[] LoadAllAssets(int index, Type t)
        {
            return Instance._LoadAllAssets(index, t);
        }
        public static AssetBundleRequest LoadAllAssetsAsync(int index)
        {
            return Instance._LoadAllAssetsAsync(index);
        }
        public static AssetBundleRequest LoadAllAssetsAsync(int index, Type t)
        {
            return Instance._LoadAllAssetsAsync(index, t);
        }

        public static void LoadInfo(string fileName, out int index, out AssetBundle ab)
        {
            Instance._LoadInfo(fileName, out index, out ab);
        }

        //---------------------------------------------


        public static void LoadAssetAsync(int abIndex, string resName, AsyncLoadAssetFinishCall completed, Type t)
        {
#if USE_RUNTIME_ASSETS && UNITY_EDITOR
            EditorLoadAssetInfo elai = new EditorLoadAssetInfo();
            elai.asyncCall = completed;
            string assetPath = XRuntimeManifest.Instance.GetAssetPath(resName);
            // sprite特殊处理
            if (t == typeof(Sprite))
            {
                UnityEngine.Object[] sprites = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                elai.obj = Array.Find(sprites, x => x.name.Equals(resName));
            }
            else if (t == null)
                elai.obj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
            else
                elai.obj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, t);
            Instance.editorAsyncLoad.Add(elai);
#else
            AssetBundleInfo info = Instance._GetAssetBundleInfo(abIndex);
            if (info.IsValid())
            {
                AssetBundleLoadAssetInfo ablai = new AssetBundleLoadAssetInfo(info, resName, completed, t);
                info.asyncLoadTask.Add(ablai);
                ablai.Run();
            }
            else if (info.request != null)
            {
                info.asyncLoadTask.Add(new AssetBundleLoadAssetInfo(info, resName, completed, t));
            }
            else
                completed(null, abIndex);
#endif
        }
        [NoToLua]
        public static void LoadAssetAsync(int abIndex, string resName, AsyncLoadAssetFinishCall completed)
        {
            LoadAssetAsync(abIndex, resName, completed, null);
        }
        [NoToLua]
        public static int LoadAllAssetsAsync(string fileName, AsyncLoadAllAssetFinishCall completed, Type t)
        {
            AssetBundleInfo info = Instance._CreateInfoFromFileAsync(fileName);
            if (info.IsValid())
            {
                new AssetBundleLoadAssetInfo(info, completed, t).Run();
            }
            else if (info.request != null)
            {
                info.asyncLoadTask.Add(new AssetBundleLoadAssetInfo(info, completed, t));
            }
            else
                completed(null, info.index);
            return info.index;
        }
        [NoToLua]
        public static int LoadAllAssetsAsync(string fileName, AsyncLoadAllAssetFinishCall completed)
        {
            return LoadAllAssetsAsync(fileName, completed, null);
        }
        [NoToLua]
        public static void LoadAllAssetsAsync(int abIndex, AsyncLoadAllAssetFinishCall completed, Type t)
        {
            AssetBundleInfo info = Instance._GetAssetBundleInfo(abIndex);
            if (info.IsValid())
            {
                new AssetBundleLoadAssetInfo(info, completed, t).Run();
            }
            else if (info.request != null)
            {
                info.asyncLoadTask.Add(new AssetBundleLoadAssetInfo(info, completed, t));
            }
            else
                completed(null, info.index);
        }
        [NoToLua]
        public static void LoadAllAssetsAsync(int abIndex, AsyncLoadAllAssetFinishCall completed)
        {
            LoadAllAssetsAsync(abIndex, completed, null);
        }

        //----------------------------------------------
        public static void LoadAssetAsync(int abIndex, string resName, LuaFunction completed, Type t)
        {
#if USE_RUNTIME_ASSETS && UNITY_EDITOR
            EditorLoadAssetInfo elai = new EditorLoadAssetInfo();
            elai.asyncCallForLua = completed;
            string assetPath = XRuntimeManifest.Instance.GetAssetPath(resName);
            // sprite特殊处理
            if (t == typeof(Sprite))
            {
                UnityEngine.Object[] sprites = UnityEditor.AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                elai.obj = Array.Find(sprites, x => x.name.Equals(resName));
            }
            else if (t == null)
                elai.obj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
            else
                elai.obj = UnityEditor.AssetDatabase.LoadAssetAtPath(assetPath, t);
            Instance.editorAsyncLoad.Add(elai);
#else
            AssetBundleInfo info = Instance._GetAssetBundleInfo(abIndex);
            if (info.IsValid())
            {
                AssetBundleLoadAssetInfo ablai = new AssetBundleLoadAssetInfo(info, resName, completed, t);
                info.asyncLoadTask.Add(ablai);
                ablai.Run();
            }
            else if (info.request != null)
            {
                info.asyncLoadTask.Add(new AssetBundleLoadAssetInfo(info, resName, completed, t));
            }
            else
            {
                completed.BeginPCall();
                completed.Push((object)null);
                completed.Push(info.index);
                completed.PCall();
                completed.EndPCall();
            }
#endif

        }
        public static void LoadAssetAsync(int abIndex, string resName, LuaFunction completed)
        {
            LoadAssetAsync(abIndex, resName, completed, null);
        }

        public static int LoadAllAssetsAsync(string fileName, LuaFunction completed, Type t)
        {
            AssetBundleInfo info = Instance._CreateInfoFromFileAsync(fileName);
            if (info.IsValid())
            {
                new AssetBundleLoadAssetInfo(info, completed, t).Run();
            }
            else if (info.request != null)
            {
                info.asyncLoadTask.Add(new AssetBundleLoadAssetInfo(info, completed, t));
            }
            else
            {
                completed.BeginPCall();
                completed.Push((object)null);
                completed.Push(info.index);
                completed.PCall();
                completed.EndPCall();
            }
            return info.index;
        }
        public static int LoadAllAssetsAsync(string fileName, LuaFunction completed)
        {
            return LoadAllAssetsAsync(fileName, completed, null);
        }
        public static void LoadAllAssetsAsync(int abIndex, LuaFunction completed, Type t)
        {
            AssetBundleInfo info = Instance._GetAssetBundleInfo(abIndex);
            if (info.IsValid())
            {
                new AssetBundleLoadAssetInfo(info, completed, t).Run();
            }
            else if (info.request != null)
            {
                info.asyncLoadTask.Add(new AssetBundleLoadAssetInfo(info, completed, t));
            }
            else
            {
                completed.BeginPCall();
                completed.Push((object)null);
                completed.Push(info.index);
                completed.PCall();
                completed.EndPCall();
            }
        }
        public static void LoadAllAssetsAsync(int abIndex, LuaFunction completed)
        {
            LoadAllAssetsAsync(abIndex, completed, null);
        }

        //--- ------------------------ --- //

        public static string[] GetAllDependencies(string fileName)
        {
            return Instance._GetAllDependencies(fileName);
        }
        public static string[] GetDirectDependencies(string fileName)
        {
            return Instance._GetDirectDependencies(fileName);
        }
        public static int NameToIndex(string fileName)
        {
            return Instance._CreateAssetBundleInfo(fileName).index;
        }
        public static string IndexToName(int abIndex)
        {
            AssetBundleInfo info = Instance._GetAssetBundleInfo(abIndex);
            if (info != null) return info.fileName;
            return null;
        }
        public static float AsyncProgress(int abIndex, List<int> deps = null)
        {
            AssetBundleInfo info = Instance._GetAssetBundleInfo(abIndex);
            if (info != null)
            {
                float total = 0;
                float div = 0;
                if (info.depends != null && info.depends.Count > 0)
                {
                    if (deps == null)
                        deps = new List<int>();
                    deps.Add(abIndex);

                    foreach (AssetBundleInfo d in info.depends)
                    {
                        if (!deps.Contains(d.index))
                        {
                            //避免循环依赖死循环
                            total += AsyncProgress(d.index, deps);
                        }
#if UNITY_EDITOR
                        //else
                        //{
                        //    Debug.LogErrorFormat("跳过循环依赖：{0} -> {1}", info.fileName, d.fileName);

                        //}
#endif
                    }
                    deps.Remove(abIndex);
                    div += info.depends.Count;
                }

                if (info.request != null)
                {
                    total += info.request.progress;
                }
                else
                {
                    total += 1;
                }

                foreach (AssetBundleLoadAssetInfo task in info.asyncLoadTask)
                {
                    if (task.request != null)
                        total += task.request.progress;
                    else
                        total += 1;
                }
                div = div + 1 + info.asyncLoadTask.Count;
                return total / div;

            }
            return 1;
        }
        public void OnUpdate(float dtTime, float unscaleDtTime)
        {
            //base.OnUpdate(dtTime, unscaleDtTime);
            int count = asyncCreateCall.Count;
            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                    asyncCreateCall[i].Call();

                // 回调里面可能还会有添加处理，所以这里有可能会使数组长度变化。
                asyncCreateCall.RemoveRange(0, count);
            }

#if USE_RUNTIME_ASSETS && UNITY_EDITOR
            count = editorAsyncLoad.Count;
            if (count > 0)
            {
                for (int i = 0; i < count; ++i)
                    editorAsyncLoad[i].Run();

                editorAsyncLoad.RemoveRange(0, count);
            }
#endif

            // 资源删除检测
            for (int i = 0; i < loadedAssetBundles.Length; ++i)
            {
                AssetBundleInfo info = loadedAssetBundles[i];
                if (info != null && info.refCount <= 0 && info.life >= 0)
                {
                    info.life -= dtTime;
                    if (info.life < 0)
                    {
                        info.Clear();
                    }
                }
            }
        }
        /// <summary>
        /// 重新初始化
        /// </summary>
        public static void Reset()
        {
            Instance._Clear(false);
            Instance._Init();
        }
        public static bool CheckEditorLoad()
        {
#if USE_RUNTIME_ASSETS && UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
        public static void UnloadUnusedBundles()
        {
            // 资源删除检测
            for (int i = 0; i < Instance.loadedAssetBundles.Length; ++i)
            {
                AssetBundleInfo info = Instance.loadedAssetBundles[i];
                if (info != null && info.refCount <= 0)
                {
                    info.life = 0;
                    info.Clear();
                }
            }
        }
    }
}
