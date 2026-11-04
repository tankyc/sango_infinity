using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SangoObjectList<T> : Database<T> where T : SangoObject, new()
    {
        List<T> mObjects;

        /// <summary>
        /// 默认构造（反序列化路径必须有无参构造）。
        /// </summary>
        public SangoObjectList()
        {
            mObjects = new List<T>();
        }

        /// <summary>
        /// 按预估容量构造。
        ///
        /// 【为什么需要】<c>List&lt;T&gt;</c> 的容量是 4→8→16→… 的翻倍增长，
        /// 首次装填（例如 City.OnForceTurnStart 里重建 allPersons）时每翻一次
        /// 都要新分配一个内部数组并 Array.Copy，Profiler 里表现为连续的 GC.Alloc。
        /// 对确定会装到几十上百个元素的容器（城池武将这些）一次性给足容量，
        /// 之后整个对局都不再扩容。
        /// </summary>
        /// <param name="capacity">预估最大元素数</param>
        public SangoObjectList(int capacity)
        {
            mObjects = new List<T>(capacity > 0 ? capacity : 0);
        }

        /// <summary>
        /// 运行期对象列表。
        ///
        /// 存档形态已改为宿主对象上的 <c>int[] Xxx_list</c> 字段，
        /// 由宿主对象在 <c>OnScenarioPrepare</c> 里调用 <see cref="Database{T}.FromArray(int[])"/> 解析成本列表；
        /// 保存时再由 <c>OnScenarioSave</c> 回写成 int[]。
        /// 因此本容器自身不再参与序列化，也不再做任何延迟解析。
        /// </summary>
        public List<T> objects
        {
            get { return mObjects; }
            set { mObjects = value != null ? value : new List<T>(); }
        }

        public override int Count { get { return mObjects.Count; } }

        public override T Default => throw new NotImplementedException();

        public override void Clear() { mObjects.Clear(); }
        public override bool Check(int index)
        {
            if (index < 0 || index >= mObjects.Count)
                return false;
            return true;
        }
        public override void Reset(int length) { mObjects.Clear(); }
        public override void Add(T obj)
        {
#if UNITY_EDITOR || SANGO_DEBUG
            if (obj == null)
            {
                Sango.Log.Error("不能添加null元素!!");
                return;
            }

            if (mObjects.Contains(obj))
            {
                Sango.Log.Error("不能重复添加");
            }
#endif
            mObjects.Add(obj);
        }
        public override void Set(T obj)
        {
#if UNITY_EDITOR || SANGO_DEBUG
            if (obj == null)
            {
                Sango.Log.Error("不能设置null元素!!");
                return;
            }
#endif
            bool find = false;
            for (int i = mObjects.Count - 1; i >= 0; i--)
            {
                T o = mObjects[i];
                if (o != null && o.Id == obj.Id)
                {
                    mObjects[i] = obj;
                    find = true;
                    break;
                }
            }
            if(find)
            {
                mObjects.Add(obj);
            }
        }
        public override void Remove(T obj)
        {
#if UNITY_EDITOR || SANGO_DEBUG
            if (obj == null)
            {
                Sango.Log.Error("不能移除null元素!!");
                return;
            }

            //if (!mObjects.Contains(obj))
            //{
            //    Sango.Log.Warning("不能移除不存在的!!!");
            //}
#endif
            mObjects.Remove(obj);
        }

        public override void RemoveAll(Predicate<T> match)
        {
            mObjects.RemoveAll(match);
        }

        public override T Get(int index)
        {
            return mObjects[index];
        }
        public override void ForEach(Action<T> action)
        {
            for (int i = mObjects.Count - 1; i >= 0; i--)
            {
                T obj = mObjects[i];
                if (obj != null)
                    action(obj);
            }
        }

        public override void ForEach(Action<SangoObject> action)
        {
            for (int i = mObjects.Count - 1; i >= 0; i--)
            {
                T obj = mObjects[i];
                if (obj != null)
                    action(obj);
            }
        }

        public override T Find(Predicate<T> match)
        {
            return mObjects.Find(match);
        }
        public override List<T> FindAll(Predicate<T> match)
        {
            return mObjects.FindAll(match);
        }
        public override void Sort(IComparer<T> comparer)
        {
            mObjects.Sort(comparer);
        }
        public override void Sort(Comparison<T> comparison)
        {
            mObjects.Sort(comparison);
        }
        public override T this[int aIndex] { get { return mObjects[aIndex]; } set { } }

        public override T Find(int id)
        {
            for (int i = 0; i < mObjects.Count; i++)
            {
                T obj = mObjects[i];
                if (obj != null && obj.Id == id)
                    return obj;
            }
            return null;
        }

        public override bool Contains(int id)
        {
            // 【性能】原实现是 mObjects.Find(x => x != null && x.Id == id)。
            // List<T>.Find 吃 Predicate<T>，而这个 lambda 捕获了参数 id，
            // 每次调用都要现分配一个闭包对象 + 一个委托（≈56 字节）。
            // Contains(int) 是 HasTechnique / HasFeatrue 一类「高频判重」的底层依赖，
            // 例如 Force.prepareTechniqueList 每势力回合都会对每个技法调两次，
            // 单帧能堆出 20 KB 以上的 GC（见 Profiler: Force.UpdateTurnInfo）。
            // 手写循环零分配，且省掉一次委托调用，语义与原实现完全一致。
            for (int i = 0; i < mObjects.Count; i++)
            {
                T obj = mObjects[i];
                if (obj != null && obj.Id == id)
                    return true;
            }
            return false;
        }

        public override bool Contains(T t)
        {
            return mObjects.Contains(t);
        }
        public override IEnumerator GetEnumerator()
        {
            return mObjects.GetEnumerator();
        }
    }
}
