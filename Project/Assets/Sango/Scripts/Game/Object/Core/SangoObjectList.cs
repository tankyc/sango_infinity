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
        List<T> mObjects = new List<T>();

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

            if (!mObjects.Contains(obj))
            {
                Sango.Log.Warning("不能移除不存在的!!!");
            }
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
            return mObjects.Find(x => x != null && x.Id == id) != null;
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
