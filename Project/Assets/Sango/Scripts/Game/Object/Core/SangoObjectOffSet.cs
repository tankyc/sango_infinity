using System;
using System.Collections;
using System.Collections.Generic;

namespace Sango.Core
{

    /// <summary>
    /// SangoObject限定数组数据集, 限定最大容量, 记住下标从1开始,0作为默认无效值,数据集用0号位来作为默认值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SangoObjectOffSet<T> : Database<T> where T : SangoObject, new()
    {
        public int offset = 0;

        public T[] objects;
        public override T Default
        {
            get
            {
                T t = objects[0];
                if (t != null) return t;
                t = new T();
                t.Id = 0;
                objects[0] = t;
                return t;
            }
        }

        public override int Count { get { return MaxCount; } }
        public override T this[int aIndex] { get { return objects[aIndex]; } set { objects[aIndex] = value; } }

        public SangoObjectOffSet()
        {
            Reset(256);
        }
        public SangoObjectOffSet(int length)
        {
            Reset(length);
        }

        public override void Reset(int length)
        {
            SearchingBeginIndex = 1;
            MaxCount = 0;
            objects = new T[length];
            warnedOffsetMismatch = false;
        }

        /// <summary>
        /// 是否已提示过"条目 Id 小于容器 offset"。
        /// 整库加载时可能几百条都命中，只在第一条打一次，避免刷屏。
        /// </summary>
        bool warnedOffsetMismatch;

        int MaxCount { get; set; }
        int SearchingBeginIndex { get; set; }
        void CheckLength(int max)
        {
            int len = objects.Length;
            MaxCount = Math.Max(max, MaxCount);
            if (len > max)
                return;
            while (len <= max)
                len *= 2;
            T[] old = objects;
            objects = new T[len];
            Array.Copy(old, objects, old.Length);
        }

        public override void Add(T obj)
        {
            if (obj.Id < 0)
            {
                obj.Id = objects.Length + offset;
                for (int i = SearchingBeginIndex; i < objects.Length; i++)
                {
                    if (objects[i] == null)
                    {
                        obj.Id = i + offset;
                        SearchingBeginIndex = i;
                        break;
                    }
                }
            }
            else
            {
                if (obj.Id < offset)
                {
                    obj.Id += offset;
                }
            }

            CheckLength(obj.Id - offset + 1);
            objects[obj.Id - offset] = obj;
        }

        public override void Set(T obj)
        {
            if (obj.Id < 0)
                return;

            // 【负下标防护】offset 是"数据里的 Id"与"容器下标"的差值。
            // 附加武将库（Data/CustomPerson.json）由 SangoObjectOffSetConverter 反序列化时，
            // 文件头可能带着 "offset": 10000 / "offset": 20000，而条目 Id 仍是 1 起编的相对下标
            // （手工编辑、或从别的编辑器导出的文件都是这种形态）。
            // 此时 obj.Id - offset 为负数，CheckLength(负数 + 1) 会因 "len > max" 恒成立而直接
            // return（不扩容），紧接着 objects[负数] 抛 IndexOutOfRangeException，
            // 一路打断 GameData.Init → GameCustomEdit.Init → ScenarioAddon.Load
            //（实测栈：SangoObjectOffSetConverter.ReadJson:77 → Set:96）。
            // 这里与 Add() 的口径保持一致：Id 小于 offset 时视为相对下标，补上 offset。
            if (obj.Id < offset)
            {
                int fixedId = obj.Id + offset;
                if (!warnedOffsetMismatch)
                {
                    warnedOffsetMismatch = true;
                    Sango.Log.Error(
                        $"附加数据条目 Id={obj.Id} 小于容器 offset={offset}（数据里用的是相对下标），" +
                        $"已按 offset 修正为 Id={fixedId}；同一次加载中的同类条目不再重复提示",
                        Sango.Log.LogType.Object);
                }
                obj.Id = fixedId;
            }

            CheckLength(obj.Id - offset + 1);
            objects[obj.Id - offset] = obj;
        }


        public override void Remove(T obj)
        {
            SearchingBeginIndex = Math.Min(SearchingBeginIndex, obj.Id - offset);
            objects[obj.Id - offset] = null;
        }

        public void Remove(int Id)
        {
            if (!Check(Id - offset))
                return;
            SearchingBeginIndex = Math.Min(SearchingBeginIndex, Id - offset);
            objects[Id - offset] = null;
        }

        public override T Get(int id)
        {
            if (!Check(id - offset))
                return null;
            return objects[id - offset];
        }

        public override T RandomGet()
        {
            List<T> list = new List<T>();
            ForEach(x =>
            {
                list.Add(x);
            });
            int index = GameRandom.Range(0, list.Count);
            return list[index];
        }

        public override bool Check(int id)
        {
            if (id < 0 || id >= objects.Length)
                return false;
            return true;
        }

        public override void ForEach(Action<T> action)
        {
            for (int i = 1; i < objects.Length; i++)
            {
                T obj = objects[i];
                if (obj != null)
                    action(obj);
            }
        }

        public override void ForEach(Action<SangoObject> action)
        {
            for (int i = 1; i < objects.Length; i++)
            {
                T obj = objects[i];
                if (obj != null)
                    action(obj);
            }
        }


        public override T Find(Predicate<T> match)
        {
            for (int i = 1; i < objects.Length; i++)
            {
                T obj = objects[i];
                if (obj != null && match(obj))
                    return obj;
            }
            return null;
        }

        public override List<T> FindAll(Predicate<T> match)
        {
            List<T> values = null;
            for (int i = 1; i < objects.Length; i++)
            {
                T obj = objects[i];
                if (obj != null && match(obj))
                {
                    if (values == null)
                        values = new List<T>();
                    values.Add(obj);
                }
            }
            return values;
        }

        public override void Clear()
        {
            Reset(objects.Length);
        }

        public override T Find(int id)
        {
            return Get(id);
        }

        public override bool Contains(int id)
        {
            if (!Check(id - offset)) return false;
            return Get(id) != null;
        }

        public override bool Contains(T t)
        {
            return Get(t.Id) != null;
        }

        public override void Sort(IComparer<T> comparer)
        {
            Array.Sort(objects, comparer);
        }

        public override void Sort(Comparison<T> comparison)
        {
            Array.Sort(objects, comparison);
        }

        public override void RemoveAll(Predicate<T> match)
        {
            for (int i = 1; i < objects.Length; i++)
            {
                T obj = objects[i];
                if (obj != null)
                {
                    if (match(obj))
                    {
                        objects[i] = null;
                    }
                }
            }
        }

        public int DataCount
        {
            get
            {
                int count = 0;
                for (int i = 1; i < Count; i++)
                {
                    T t = objects[i];
                    if (t != null)
                        count++;
                }
                return count;
            }
        }

        public override IEnumerator GetEnumerator()
        {
            return objects.GetEnumerator();
        }
    }
}
