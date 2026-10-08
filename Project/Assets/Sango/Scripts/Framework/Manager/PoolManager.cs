

using Sango.Loader;
using System.Collections.Generic;
using UnityEngine;

namespace Sango
{
    public interface IPoolNode
    {
        public object key { get; }
        public object customDesc { get; }
    }

    public interface IPoolObject<T> where T : class
    {
        T Create();
        void Destroy();
        void OnCreate(ref T node);
        void OnRecycle(ref T node);
        void OnDestroy(ref T node);
        IPoolNode headNode { get; set; }
    }

    public class PoolNode<T, T2> : IPoolNode where T : class where T2 : class, IPoolObject<T>
    {
        public static float POOLLIFE = 20;
        public static float NODELIFE = 3;
        public T2 srcObject { get; private set; }
        public int useCount { get; private set; }
        public object key { get; private set; }
        public object customDesc { get; private set; }
        public bool clearFlag { get; internal set; }
        public float life { get; private set; }
        private float maxlife = -1;
        public Queue<T> instance_list = new Queue<T>();
        public PoolNode(object key, object customDesc, T2 node, int initCount)
        {
            srcObject = node;
            srcObject.headNode = this;
            this.customDesc = customDesc;
            useCount = 0;
            this.key = key;
            RefreshLife();

            // 初始化池数量
            for (int i = 0; i < initCount; ++i)
                instance_list.Enqueue(node.Create());

        }
        public void RefreshLife()
        {
            if (maxlife < 0)
            {
                maxlife = POOLLIFE;
            }
            life = maxlife;
        }
        public T Get()
        {
            useCount++;
            while (instance_list.Count > 0)
            {
                T node = instance_list.Dequeue();
                if (node != null)
                {
                    srcObject.OnCreate(ref node);
                    return node;
                }
                else
                    useCount--;
            }
            RefreshLife();
            return srcObject.Create();
        }
        public bool IsValid()
        {
            return useCount > 0;
        }
        public void Recycle(T node)
        {
            if (node == null)
                return;
            useCount--;
            srcObject.OnRecycle(ref node);
            instance_list.Enqueue(node);
        }
        public void Clear()
        {
            while (instance_list.Count > 0)
            {
                T node = instance_list.Dequeue();
                srcObject.OnDestroy(ref node);
            }
            srcObject.Destroy();
            srcObject = null;
            instance_list = null;
        }
        /// <summary>
        /// 更新该资源池的生命周期
        /// 池中有对象正在使用时重置生命周期；空闲时间超过POOLLIFE后释放池中缓存的实例，
        /// 再空闲超过NODELIFE后标记该资源池需要被清理
        /// </summary>
        /// <param name="dtTime">距离上一帧的时间(秒)</param>
        /// <returns>是否需要清理该资源池</returns>
        public bool Update(float dtTime)
        {
            // 池中有对象正在被使用时，重置生命周期，不做任何清理
            if (useCount > 0)
            {
                RefreshLife();
                return false;
            }

            // 递减池的剩余存活时间
            life -= dtTime;
            if (life > 0)
                return false;

            // 池中还缓存着实例时，先释放这些实例，再等待NODELIFE时间回收池结构
            if (instance_list != null && instance_list.Count > 0)
            {
                while (instance_list.Count > 0)
                {
                    T node = instance_list.Dequeue();
                    srcObject.OnDestroy(ref node);
                }

                life = NODELIFE;
                return false;
            }

            // 池结构长时间未被使用，标记为需要清理
            clearFlag = true;
            return true;
        }

    }

    public class NodeInfo : MonoBehaviour
    {
        public object key;
    }

    public class PoolManager : System<PoolManager>
    {
        public class GameObjectPoolObject : IPoolObject<GameObject>
        {
            PoolManager manager;
            UnityEngine.Object srcObject;
            public GameObjectPoolObject(PoolManager manager, UnityEngine.Object obj)
            {
                this.manager = manager;
                srcObject = obj;
            }

            public IPoolNode headNode { get; set; }

            public GameObject Create()
            {
                GameObject obj = GameObject.Instantiate(srcObject) as GameObject;
                obj.name = srcObject.name;
                NodeInfo nodeInfo = obj.AddComponent<NodeInfo>();
                nodeInfo.key = headNode.key;
                OnCreate(ref obj);
                return obj;
            }

            public void Destroy()
            {
                srcObject = null;
            }

            public virtual void OnCreate(ref GameObject node)
            {
            }

            public virtual void OnDestroy(ref GameObject node)
            {
                GameObject.Destroy(node);
            }

            public virtual void OnRecycle(ref GameObject node)
            {
            }
        }
        Dictionary<object, PoolNode<GameObject, GameObjectPoolObject>> all_pools = new Dictionary<object, PoolNode<GameObject, GameObjectPoolObject>>();

        public delegate GameObjectPoolObject OnCreatePoolObject(PoolManager manager, UnityEngine.Object obj);
        public OnCreatePoolObject onCreatePoolObject;
        GameObject poolNode;

        /// <summary>
        /// 更新资源池时使用的临时列表，避免每次更新都产生新的GC
        /// </summary>
        private List<object> expiredKeys = new List<object>();

        public PoolManager()
        {
            poolNode = new GameObject("pool_node");
            poolNode.SetActive(false);
            GameObject.DontDestroyOnLoad(poolNode);
        }

        /// <summary>
        /// 检查并清理长时间未被使用的资源池，由 Game 的主循环每帧驱动
        /// </summary>
        /// <param name="dtTime">距离上一帧的时间(秒)</param>
        public void CheckExpired(float dtTime)
        {
            if (all_pools.Count == 0) return;

            expiredKeys.Clear();

            // 收集所有需要清理的资源池键
            foreach (KeyValuePair<object, PoolNode<GameObject, GameObjectPoolObject>> kv in all_pools)
            {
                if (kv.Value.Update(dtTime))
                    expiredKeys.Add(kv.Key);
            }

            // 清理过期的资源池
            for (int i = 0; i < expiredKeys.Count; i++)
            {
                PoolNode<GameObject, GameObjectPoolObject> info;
                if (all_pools.TryGetValue(expiredKeys[i], out info))
                {
                    Log.Info(string.Format("资源池[{0}]超过存活时间未被使用,已自动清理", expiredKeys[i]));
                    info.Clear();
                    all_pools.Remove(expiredKeys[i]);
                }
            }

            expiredKeys.Clear();
        }

        protected GameObject _Get(object key)
        {
            PoolNode<GameObject, GameObjectPoolObject> info;
            if (all_pools.TryGetValue(key, out info))
                return info.Get();
            return null;
        }
        public static GameObject Get(object key)
        {
            return Instance._Get(key);
        }
        protected bool _Add(object key, object customDesc, UnityEngine.Object obj)
        {
            if (obj == null) return false;

            PoolNode<GameObject, GameObjectPoolObject> info;
            if (all_pools.TryGetValue(key, out info))
                return false;

            GameObjectPoolObject goNode;
            if (onCreatePoolObject != null)
                goNode = onCreatePoolObject.Invoke(this, obj);
            else
                goNode = new GameObjectPoolObject(this, obj);
            info = new PoolNode<GameObject, GameObjectPoolObject>(key, customDesc, goNode, 1);
            all_pools.Add(key, info);
            return true;
        }
        public static bool Add(object key, UnityEngine.Object obj, object customDesc = null)
        {
            return Instance._Add(key, customDesc, obj);
        }
        protected bool _Recycle(GameObject obj)
        {
            if (obj == null) return false;

            NodeInfo nodeInfo = obj.GetComponent<NodeInfo>();
            if (nodeInfo == null) return false;

            PoolNode<GameObject, GameObjectPoolObject> info;
            if (all_pools.TryGetValue(nodeInfo.key, out info))
            {
                obj.transform.SetParent(poolNode.transform, false);
                info.Recycle(obj);
                return true;
            }

            return false;
        }
        public static bool Recycle(GameObject obj)
        {
            return Instance._Recycle(obj);
        }

        //public static bool AttachScript(LuaTable table, bool callawake = true)
        //{
        //    return Instance.AttachScript(table, callawake);
        //}

        public static GameObject Create(string assetsPath)
        {
            GameObject poolObj = Get(assetsPath);
            if (poolObj == null)
            {
                poolObj = ObjectLoader.LoadObject<GameObject>(assetsPath);
                if (poolObj != null)
                {
                    Add(assetsPath, poolObj);
                    poolObj = Get(assetsPath);
                }
            }
            return poolObj;
        }

        public static GameObject Create(string assetsPath, System.Action<GameObject> onCreate)
        {
            GameObject poolObj = Get(assetsPath);
            if (poolObj == null)
            {
                poolObj = ObjectLoader.LoadObject<GameObject>(assetsPath);
                if (poolObj != null)
                {
                    onCreate?.Invoke(poolObj);
                    Add(assetsPath, poolObj);
                    poolObj = Get(assetsPath);
                }
            }
            return poolObj;
        }
    }
}
