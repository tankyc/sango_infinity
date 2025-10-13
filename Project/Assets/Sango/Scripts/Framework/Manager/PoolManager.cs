
using LuaInterface;
using System.Collections.Generic;
using UnityEngine;
using Sango;
using Sango.Game;
using Sango.Loader;
using System.Runtime.Remoting.Messaging;

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
        public bool Update(float dtTime)
        {
            return false;
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

            public void OnCreate(ref GameObject node)
            {
                manager.CallMethod(manager.onObjectCreate, node, headNode.key, headNode.customDesc);
            }

            public void OnDestroy(ref GameObject node)
            {
                manager.CallMethod(manager.onObjectDestroy, node, headNode.key, headNode.customDesc);
                GameObject.Destroy(node);
            }

            public void OnRecycle(ref GameObject node)
            {
                manager.CallMethod(manager.onObjectRecycle, node, headNode.key, headNode.customDesc);
            }
        }
        Dictionary<object, PoolNode<GameObject, GameObjectPoolObject>> all_pools = new Dictionary<object, PoolNode<GameObject, GameObjectPoolObject>>();
        protected LuaFunction onObjectRecycle;
        protected LuaFunction onObjectCreate;
        protected LuaFunction onObjectDestroy;
        GameObject poolNode;

        public PoolManager()
        {
            poolNode = new GameObject("pool_node");
            poolNode.SetActive(false);
            GameObject.DontDestroyOnLoad(poolNode);
        }

        protected override void OnInitFunctions()
        {


            base.OnInitFunctions();
            onObjectRecycle = GetFunction("OnObjectRecycle");
            onObjectCreate = GetFunction("OnObjectCreate");
            onObjectDestroy = GetFunction("OnObjectDestroy");
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

            GameObjectPoolObject goNode = new GameObjectPoolObject(this, obj);
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
            GameObject headBar = Get(assetsPath);
            if (headBar == null)
            {
                GameObject headBarObj = ObjectLoader.LoadObject<GameObject>(assetsPath);
                if (headBarObj != null)
                {
                    Add(assetsPath, headBarObj);
                    headBar = Get(assetsPath);
                }
            }
            return headBar;
        }

    }
}
