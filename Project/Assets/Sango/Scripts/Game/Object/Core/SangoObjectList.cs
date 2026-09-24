using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SangoObjectList<T> : Database<T> where T : SangoObject, new()
    {
        List<T> mObjects = new List<T>();

        /// <summary>
        /// 列表内容。
        ///
        /// 存档里读到的是 id 数组（<see cref="Database{T}.arrayDataCache"/>），
        /// 这里在第一次访问时按需解析成对象；不再订阅
        /// <c>GameEvent.OnScenarioPrepare</c> 做"容器级延迟解析"，
        /// 因此挂在嵌套对象 / 数据库项上的列表同样有效。
        /// </summary>
        public List<T> objects
        {
            get { EnsureResolved(); return mObjects; }
            set
            {
                mObjects = value != null ? value : new List<T>();
                arrayDataCache = null;
            }
        }

        /// <summary>
        /// 把挂起的 id 数组解析成对象。幂等：解析完 arrayDataCache 即为 null，
        /// 后续调用只剩一次空判断。
        /// </summary>
        void EnsureResolved()
        {
            if (arrayDataCache == null) return;

            // 拿不到剧本就先保持挂起（等剧本可用时再解析），不能把 id 丢掉
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return;

            int[] ids = arrayDataCache;
            arrayDataCache = null;
            if (ids.Length == 0) return;

            Database<T> database = scenario.GetDatabase<T>();
            for (int i = 0; i < ids.Length; i++)
            {
                T obj = database != null ? database.Get(ids[i]) : scenario.GetObject<T>(ids[i]);
                if (obj != null)
                    mObjects.Add(obj);
            }
        }

        public override int Count { get { EnsureResolved(); return mObjects.Count; } }

        public override T Default => throw new NotImplementedException();

        public override void Clear() { arrayDataCache = null; mObjects.Clear(); }
        public override bool Check(int index)
        {
            EnsureResolved();
            if (index < 0 || index >= mObjects.Count)
                return false;
            return true;
        }
        public override void Reset(int length) { arrayDataCache = null; mObjects.Clear(); }
        public override void Add(T obj)
        {
            EnsureResolved();
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
            EnsureResolved();
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
            EnsureResolved();
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
            EnsureResolved();
            mObjects.RemoveAll(match);
        }

        public override T Get(int index)
        {
            EnsureResolved();
            return mObjects[index];
        }
        public override void ForEach(Action<T> action)
        {
            EnsureResolved();
            for (int i = mObjects.Count - 1; i >= 0; i--)
            {
                T obj = mObjects[i];
                if (obj != null)
                    action(obj);
            }
        }

        public override void ForEach(Action<SangoObject> action)
        {
            EnsureResolved();
            for (int i = mObjects.Count - 1; i >= 0; i--)
            {
                T obj = mObjects[i];
                if (obj != null)
                    action(obj);
            }
        }

        public override T Find(Predicate<T> match)
        {
            EnsureResolved();
            return mObjects.Find(match);
        }
        public override List<T> FindAll(Predicate<T> match)
        {
            EnsureResolved();
            return mObjects.FindAll(match);
        }
        public override void Sort(IComparer<T> comparer)
        {
            EnsureResolved();
            mObjects.Sort(comparer);
        }
        public override void Sort(Comparison<T> comparison)
        {
            EnsureResolved();
            mObjects.Sort(comparison);
        }
        public override T this[int aIndex] { get { EnsureResolved(); return mObjects[aIndex]; } set { } }

        public override T Find(int id)
        {
            EnsureResolved();
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
            EnsureResolved();
            return mObjects.Find(x => x != null && x.Id == id) != null;
        }

        public override bool Contains(T t)
        {
            EnsureResolved();
            return mObjects.Contains(t);
        }
        public override IEnumerator GetEnumerator()
        {
            EnsureResolved();
            return mObjects.GetEnumerator();
        }
    }
}
