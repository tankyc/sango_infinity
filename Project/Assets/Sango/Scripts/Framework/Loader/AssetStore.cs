using System.Collections.Generic;
using UnityEngine;
using Sango;
using System;

namespace Sango.Loader
{
    /// <summary>
    /// 通过弱引用缓存资源
    /// 同时带有生命周期管理：存储或获取资源时会刷新访问时间，
    /// 超过存活时间仍未被访问的资源会被自动释放
    /// </summary>
    public class AssetStore : Singleton<AssetStore>
    {
        /// <summary>
        /// 缓存资源项，记录资源的弱引用与最后一次被访问的时间
        /// </summary>
        private class AssetEntry
        {
            /// <summary>
            /// 资源弱引用，允许垃圾回收在外部无引用时回收该资源
            /// </summary>
            public WeakReference<UnityEngine.Object> assetRef;

            /// <summary>
            /// 最后一次被访问的时间(Time.realtimeSinceStartup)
            /// </summary>
            public float lastAccessTime;
        }

        /// <summary>
        /// 资源缓存表：键为资源路径，值为缓存资源项
        /// </summary>
        private Dictionary<string, AssetEntry> assetsMap = new Dictionary<string, AssetEntry>();

        /// <summary>
        /// 资源存活时间(秒)，超过该时间未被访问的资源会被释放，小于等于0表示不进行生命周期管理
        /// </summary>
        public float lifeTime = 300f;

        /// <summary>
        /// 扫描过期资源的间隔(秒)，避免每帧都遍历整个缓存表
        /// </summary>
        public float checkInterval = 10f;

        /// <summary>
        /// 上一次扫描过期资源的时间
        /// </summary>
        private float lastCheckTime;

        /// <summary>
        /// 扫描过期资源时使用的临时列表，避免每次扫描都产生新的GC
        /// </summary>
        private List<string> expiredKeys = new List<string>();

        Transform root;

        public AssetStore()
        {
            GameObject go = new GameObject("AssetStore");
            // 仅在运行时才需要跨场景常驻，编辑模式下不设置
            if (Application.isPlaying)
                UnityTools.DontDestroyOnLoad(go);
            root = go.transform;
        }

        public UnityEngine.Object StoreAsset(string key, UnityEngine.Object obj)
        {
            float now = Time.realtimeSinceStartup;
            AssetEntry entry;
            if (assetsMap.TryGetValue(key, out entry))
            {
                UnityEngine.Object get_obj = null;
                if (entry.assetRef.TryGetTarget(out get_obj) && get_obj != null)
                {
                    Log.Warning(string.Format("已经存在{0}, 舍弃", key));
                    // 缓存的都是资源对象，使用Destroy会报"不允许销毁资源"的错误
                    // 必须使用DestroyImmediate并显式允许销毁资源
                    UnityEngine.Object.DestroyImmediate(obj, false);
                    // 刷新访问时间
                    entry.lastAccessTime = now;
                    return get_obj;
                }
                else
                {
                    // 旧的弱引用已失效，重新指向新的资源
                    entry.assetRef.SetTarget(obj);
                    // 刷新访问时间
                    entry.lastAccessTime = now;
                    return obj;
                }
            }
            else
            {
                entry = new AssetEntry();
                entry.assetRef = new WeakReference<UnityEngine.Object>(obj);
                entry.lastAccessTime = now;
                assetsMap.Add(key, entry);
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
            AssetEntry entry;
            if (assetsMap.TryGetValue(key, out entry))
            {
                UnityEngine.Object get_obj = null;
                if (entry.assetRef.TryGetTarget(out get_obj) && get_obj != null)
                {
                    // 刷新访问时间，延长资源的存活时间
                    entry.lastAccessTime = Time.realtimeSinceStartup;
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

        /// <summary>
        /// 检查并释放超过存活时间未被访问的资源
        /// 由 Game 的主循环每帧驱动调用，内部按 checkInterval 控制实际扫描频率
        /// </summary>
        public void CheckExpired()
        {
            // 未开启生命周期管理时直接返回
            if (lifeTime <= 0f) return;

            float now = Time.realtimeSinceStartup;
            // 未到达扫描间隔，跳过本次检查
            if (now - lastCheckTime < checkInterval) return;
            lastCheckTime = now;

            expiredKeys.Clear();

            // 收集已过期或弱引用已被回收的资源键
            foreach (KeyValuePair<string, AssetEntry> kv in assetsMap)
            {
                UnityEngine.Object target = null;
                // 弱引用已被垃圾回收或资源已被销毁，需要清理缓存记录
                if (!kv.Value.assetRef.TryGetTarget(out target) || target == null)
                {
                    expiredKeys.Add(kv.Key);
                    continue;
                }

                // 超过存活时间未被访问，需要释放
                if (now - kv.Value.lastAccessTime >= lifeTime)
                    expiredKeys.Add(kv.Key);
            }

            // 释放收集到的过期资源
            for (int i = 0; i < expiredKeys.Count; i++)
            {
                string key = expiredKeys[i];
                AssetEntry entry;
                if (assetsMap.TryGetValue(key, out entry))
                {
                    UnityEngine.Object target = null;
                    if (entry.assetRef.TryGetTarget(out target) && target != null)
                    {
                        Log.Info(string.Format("资源[{0}]超过存活时间{1}秒未被访问,自动释放", key, lifeTime));
                        // 缓存中存放的都是资源(Resources/AssetBundle中加载的对象)
                        // 对资源使用Destroy会报"Destroying assets is not permitted to avoid data loss"
                        // 必须使用DestroyImmediate并显式允许销毁资源
                        UnityEngine.Object.DestroyImmediate(target, false);
                    }
                    assetsMap.Remove(key);
                    entry.assetRef.SetTarget(null);
                }
            }

            expiredKeys.Clear();
        }
    }
}
