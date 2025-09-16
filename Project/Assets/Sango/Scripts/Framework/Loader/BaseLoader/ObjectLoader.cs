using LuaInterface;
using System.Collections.Generic;
using UnityEngine;
using Sango;
using System;
using System.CodeDom;

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
                Game.Game.Instance.AddTick(helper);
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
            public List<LuaFunction> onLoadedFuncs;
            public List<object> lua_customData;
            public List<OnObjectLoaded> onCSharpLoadedFuncs;
            public List<object> c_customData;
            public UnityEngine.Object rsObject;
            public void Call()
            {
                if (onLoadedFuncs != null)
                {
                    for (int i = 0; i < onLoadedFuncs.Count; ++i)
                    {
                        LuaFunction onLoadedFunc = onLoadedFuncs[i];
                        if (onLoadedFunc != null)
                        {
                            onLoadedFunc.BeginPCall();
                            onLoadedFunc.Push(rsObject);
                            object customData = lua_customData[i];
                            if (customData != null)
                                onLoadedFunc.Push(customData);
                            onLoadedFunc.PCall();
                            onLoadedFunc.EndPCall();
                        }
                    }

                    onLoadedFuncs.Clear();
                    lua_customData.Clear();
                }

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

            public void AddCall(LuaFunction func, object customData)
            {
                if (onLoadedFuncs == null)
                {
                    onLoadedFuncs = new List<LuaFunction>();
                    lua_customData = new List<object>();
                }

                onLoadedFuncs.Add(func);
                lua_customData.Add(customData);
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
            if (!string.IsNullOrEmpty(packageName))
            {
                string storeName = string.Format("obj_{0}_{1}", packageName, objName);
                UnityEngine.Object obj = AssetStore.Instance.GetAsset(storeName);
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
                }
                return obj;
            }
            else
            {
                if (objType == TextureType)
                {
                    return TextureLoader.LoadFromFileSync(objName, (bool)ps[0], (bool)ps[1]);
                }
                else if (objType == MaterialType)
                {
                    return MaterialLoader.LoadMaterial(objName, (bool)ps[0]);
                }
                else if (objType == GameObjectType)
                {
                    return ModelLoader.LoadFromFileSync(objName, (string)ps[0], (bool)ps[1], (string)ps[2], (bool)ps[3]);
                }
                else if (objType == SpriteType)
                {
                    return SpriteLoader.LoadSprite(objName);
                }
            }
            return null;
        }


        public static T LoadObject<T>(string assetName, params object[] ps) where T : UnityEngine.Object
        {
            Type type = typeof(T);
            return LoadObject(assetName, type, ps) as T;
        }

    }
}
