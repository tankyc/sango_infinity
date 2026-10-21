
using System.Collections.Generic;
using UnityEngine;
using Sango.Core;
using System;

namespace Sango.Loader
{
    public class ObjectLoader
    {
        public delegate void OnObjectLoaded(UnityEngine.Object obj, object customData);
        protected static Queue<LoadData> rsQueue = new Queue<LoadData>();
        protected static Queue<LoadData> reusedQueue = new Queue<LoadData>();
        protected static List<LoadData> usingList = new List<LoadData>();
        class LoaderHelper : IUpdate
        {
            public bool Update(float dtTime, float unScaleTime)
            {
                if (rsQueue.Count > 0)
                {
                    LoadData data = rsQueue.Dequeue();
                    data.Call();
                }

                return true;
            }
        }
        private static LoaderHelper helper;
        public static void CheckHelper()
        {
            if (helper == null)
            {
                helper = new LoaderHelper();
                App.Instance.AddTick(helper);
            }
        }
        protected static LoadData CheckExistLoader(string fileName)
        {
            return usingList.Find(x => x.filePath.Equals(fileName));
        }
        protected class LoadData
        {
            public string filePath;
            public string texturePath;
            public string matName;

            public bool textureNeedCompress;
            public bool textureNeedMipmap;
            public bool shareMaterial;
            public List<OnObjectLoaded> onCSharpLoadedFuncs;
            public List<object> c_customData;
            public UnityEngine.Object rsObject;
            public void Call()
            {
                if (onCSharpLoadedFuncs != null)
                {
                    for (int i = 0; i < onCSharpLoadedFuncs.Count; ++i)
                    {
                        OnObjectLoaded onCSharpLoadedFunc = onCSharpLoadedFuncs[i];
                        if (onCSharpLoadedFunc != null)
                        {
                            onCSharpLoadedFunc(rsObject, c_customData[i]);
                        }
                    }


                    onCSharpLoadedFuncs.Clear();
                    c_customData.Clear();
                }


                reusedQueue.Enqueue(this);
                usingList.Remove(this);
            }

            public void AddCall(OnObjectLoaded func, object customData)
            {
                if (onCSharpLoadedFuncs == null)
                {
                    onCSharpLoadedFuncs = new List<OnObjectLoaded>();
                    c_customData = new List<object>();
                }

                onCSharpLoadedFuncs.Add(func);
                c_customData.Add(customData);
            }
        }

        public static UnityEngine.Object LoadObject(string assetName)
        {
            return LoadObject(null, assetName, null);
        }

        public static UnityEngine.Object LoadObject(string packageName, string assetName)
        {
            return LoadObject(packageName, assetName, null);
        }

        public static UnityEngine.Object LoadObject(string packageName, string assetName, System.Type objType)
        {
            bool isResources = string.IsNullOrEmpty(packageName);
            string storeName = string.Format("{0}_{1}", assetName, isResources ? "Resources" : packageName);
            UnityEngine.Object obj = AssetStore.Instance.GetAsset(storeName);
            if (obj == null)
            {
                if (isResources)
                    obj = Resources.Load(assetName, objType);
                else
                    obj = PackageManager.Instance.LoadAssets(packageName, assetName, objType);

                if (obj != null)
                {
                    AssetStore.Instance.StoreAsset(storeName, obj);
                }
            }

            if (obj)
            {
                if (obj is GameObject)
                {
                    return GameObject.Instantiate(obj);
                }
                else
                    return obj;
            }
            return obj;

        }

        private static Type TextureType = typeof(UnityEngine.Texture);
        private static Type GameObjectType = typeof(UnityEngine.GameObject);
        private static Type MaterialType = typeof(UnityEngine.Material);
        private static Type SpriteType = typeof(UnityEngine.Sprite);
        private static Type AudioClipType = typeof(UnityEngine.AudioClip);

        /// <summary>
        /// assetName = "PackageName:AssetPath" PackageName = "Resources"时在Resources文件夹中读取
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="objType"></param>
        /// <param name="ps"></param>
        /// <returns></returns>
        public static UnityEngine.Object LoadObject(string assetName, System.Type objType, params object[] ps)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            // 模组私有资源协议(mod://模组Id/路径 或 模组Id:路径)必须在冒号分包之前处理,
            // 否则会被误拆成 "PackageName:AssetPath"
            if (ModAssetPath.IsModUri(assetName))
                return LoadObject(assetName, null, objType, ps);

            string[] split_str = assetName.Split(new char[] { ':' });
            string packageName = null;
            string objName = assetName;
            if (split_str.Length > 1)
            {
                packageName = split_str[0];
                objName = split_str[1];
            }
            return LoadObject(objName, packageName, objType, ps);
        }

        public static UnityEngine.Object LoadObject(string assetName, string packageName, System.Type objType, params object[] ps)
        {
            string objName = assetName;

            // 模组私有资源协议:先解析成该模组 Private 目录下的绝对路径,再按普通"裸文件"流程加载
            // 解析失败直接返回 null,不回落包或内容目录 —— 引用带模组Id就必须命中该模组,否则宁可报错
            bool isPrivateAsset = false;
            if (ModAssetPath.IsModUri(assetName))
            {
                string privateFile = ModAssetPath.Resolve(assetName);
                if (string.IsNullOrEmpty(privateFile))
                    return null;
                objName = privateFile;
                packageName = null;
                isPrivateAsset = true;
            }

            // 优先外面读取
            UnityEngine.Object obj = null;
            if (objType == TextureType)
            {
                bool textureNeedCompress = true;
                bool needMipmap = false;
                if (ps.Length > 0)
                    textureNeedCompress = (bool)ps[0];
                if (ps.Length > 1)
                    needMipmap = (bool)ps[1];
                obj = TextureLoader.LoadFromFileSync(objName, textureNeedCompress, needMipmap);
            }
            else if (objType == MaterialType && ps.Length > 0)
            {
                obj = MaterialLoader.LoadMaterial(objName, (bool)ps[0]);
            }
            else if (objType == GameObjectType && ps.Length > 3)
            {
                obj = ModelLoader.LoadFromFileSync(objName, (string)ps[0], (bool)ps[1], (string)ps[2], (bool)ps[3]);
            }
            else if (objType == SpriteType)
            {
                obj = SpriteLoader.LoadSprite(objName);
            }
            else if (objType == AudioClipType)
            {
                bool is3D = false;
                if (ps.Length > 0)
                    is3D = (bool)ps[0];
                obj = AudioLoader.LoadFromFileSync(objName, is3D);
            }

            if (obj == null && !isPrivateAsset)
            {
                obj = LoadFromPackage(assetName, packageName, objType, ps);
                if (obj == null)
                    obj = LoadFromPackage(assetName, "Content", objType, ps);
            }
            return obj;
        }

        static UnityEngine.Object LoadFromPackage(string assetName, string packageName, System.Type objType, params object[] ps)
        {
            if (string.IsNullOrEmpty(packageName)) return null;

            string objName = assetName;

            // 优先外面读取
            UnityEngine.Object obj = null;

            string storeName = string.Format("obj_{0}_{1}", packageName, objName);
            obj = AssetStore.Instance.GetAsset(storeName);
            if (obj == null)
            {
                if (packageName.Equals("Resources"))
                {
                    obj = Resources.Load(objName, objType);
                }
                else
                {
                    obj = PackageManager.Instance.LoadAssets(packageName, objName, objType);
                }

                if (obj != null)
                {
                    AssetStore.Instance.StoreAsset(storeName, obj);
                }
                else
                {
                    if (objType == TextureType)
                    {
                        bool textureNeedCompress = true;
                        bool needMipmap = false;
                        if (ps.Length > 0)
                            textureNeedCompress = (bool)ps[0];
                        if (ps.Length > 1)
                            needMipmap = (bool)ps[1];
                        obj = TextureLoader.LoadFromFileSync(objName, textureNeedCompress, needMipmap);
                    }
                    else if (objType == MaterialType && ps.Length > 0)
                    {
                        obj = MaterialLoader.LoadMaterial(objName, (bool)ps[0]);
                    }
                    else if (objType == GameObjectType && ps.Length > 3)
                    {
                        obj = ModelLoader.LoadFromFileSync(objName, (string)ps[0], (bool)ps[1], (string)ps[2], (bool)ps[3]);
                    }
                    else if (objType == SpriteType)
                    {
                        obj = SpriteLoader.LoadSprite(objName);
                    }
                    else if (objType == AudioClipType)
                    {
                        bool is3D = false;
                        if (ps.Length > 0)
                            is3D = (bool)ps[0];
                        obj = AudioLoader.LoadFromFileSync(objName, is3D);
                    }
                }
            }

            return obj;
        }

        public static T LoadObject<T>(string assetName, params object[] ps) where T : UnityEngine.Object
        {
            Type type = typeof(T);
            return LoadObject(assetName, type, ps) as T;
        }
        public static T LoadObject<T>(string assetName, string packageName, params object[] ps) where T : UnityEngine.Object
        {
            Type type = typeof(T);
            return LoadObject(assetName, packageName, type, ps) as T;
        }
    }
}
