using System.Collections.Generic;
using UnityEngine;
using Sango;
using System;

namespace Sango.Loader
{
    /// <summary>
    /// 通过弱引用缓存资源
    /// </summary>
    public class AssetStore : Singletion<AssetStore>
    {
        private Dictionary<string, WeakReference<UnityEngine.Object>> assetsMap = new Dictionary<string, WeakReference<UnityEngine.Object>>();
        Transform root;
        public AssetStore()
        {
            GameObject go = new GameObject("AssetStore");
            root = go.transform;
        }

        public UnityEngine.Object StoreAsset(string key, UnityEngine.Object obj)
        {
            WeakReference<UnityEngine.Object> assetRef;
            if (assetsMap.TryGetValue(key, out assetRef))
            {
                UnityEngine.Object get_obj = null;
                if (assetRef.TryGetTarget(out get_obj))
                {
                    Log.Warning(string.Format("已经存在{0}, 舍弃", key));
                    UnityEngine.GameObject.Destroy(obj);
                    return get_obj;
                }
                else
                {
                    assetRef.SetTarget(obj);
                    return obj;
                }
            }
            else
            {
                assetsMap.Add(key, new WeakReference<UnityEngine.Object>(obj));
                //GameObject go = obj as GameObject;
                //if (go != null)
                //{
                //    go.transform.SetParent(root);
                //    go.SetActive(false);
                //}
                return obj;
            }
        }

        public T CheckAsset<T>(string key) where T : UnityEngine.Object
        {
            return GetAsset(key) as T;
        }
        public UnityEngine.Object GetAsset(string key)
        {
            WeakReference<UnityEngine.Object> assetRef;
            if (assetsMap.TryGetValue(key, out assetRef))
            {
                UnityEngine.Object get_obj = null;
                if (assetRef.TryGetTarget(out get_obj))
                {
                    return get_obj;
                }
                else
                {
                    assetsMap.Remove(key);
                    return null;
                }
            }
            return null;
        }
    }
}
