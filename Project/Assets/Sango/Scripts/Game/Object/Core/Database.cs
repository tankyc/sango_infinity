using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Sango.Core
{
    public interface IDatabase
    {
        abstract void ForEach(Action<SangoObject> action);
    }

    public abstract class Database<T> : IDatabase, IStringDataObject, IAarryDataObject where T : SangoObject, new()
    {
        public abstract T Default { get; }
        public abstract int Count { get; }
        public abstract void Clear();
        public abstract void Reset(int length);
        public abstract void Add(T obj);
        public abstract void Remove(T obj);
        public abstract void RemoveAll(Predicate<T> match);
        public abstract T Get(int index);
        public virtual T RandomGet()
        {
            return null;
        }
        public abstract T Find(int id);
        public abstract bool Check(int id);
        public abstract void Set(T obj);
        public abstract bool Contains(int id);
        public virtual bool Contains(int[] ids)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                int destId = ids[i];
                if (Contains(destId))
                    return true;
            }
            return false;
        }
        public abstract bool Contains(T t);
        public virtual bool Contains(T [] t)
        {
            if (t == null) return false;
            for (int i = 0; i < t.Length; i++)
            {
                T destId = t[i];
                if (Contains(destId))
                    return true;
            }
            return false;
        }
        public abstract void ForEach(Action<T> action);
        public abstract T Find(Predicate<T> match);
        public abstract List<T> FindAll(Predicate<T> match);
        public abstract void Sort(IComparer<T> comparer);
        public abstract void Sort(Comparison<T> comparison);
        public abstract T this[int aIndex] { get; set; }
        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            for (int i = 0; i < Count; i++)
            {
                T obj = this[i];
                if (obj != null && obj.Id > 0)
                {
                    sb.Append(obj.Id.ToString());
                    sb.Append(',');
                }
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
        public virtual IStringDataObject FromString(string content)
        {
            if (content == null) return this;
            string[] strings = content.Split(',');
            for (int i = 0; i < strings.Length; ++i)
            {
                int id;
                if (int.TryParse(strings[i], out id))
                {
                    Add(Scenario.Cur.GetObject<T>(id));
                }
            }
            return this;
        }
        /// <summary>
        /// 提供一个指定获取T的地方，性能优于楼上
        /// </summary>
        /// <param name="content"></param>
        /// <param name="database"></param>
        public virtual IStringDataObject FromString(string content, Database<T> database)
        {
            if (content == null) return this;
            string[] strings = content.Split(',');
            for (int i = 0; i < strings.Length; ++i)
            {
                int id;
                if (int.TryParse(strings[i], out id))
                {
                    Add(database.Get(id));
                }
            }
            return this;
        }

        public virtual int[] ToArray()
        {
            List<int> list = new List<int>();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            for (int i = 0; i < Count; i++)
            {
                T obj = this[i];
                if (obj != null && obj.Id > 0)
                {
                    list.Add(obj.Id);
                }
            }
            return list.ToArray();
        }

        public virtual IAarryDataObject FromArray(int[] values)
        {
            if (values == null) return this;
            for (int i = 0; i < values.Length; ++i)
            {
                Add(Scenario.Cur.GetObject<T>(values[i]));
            }
            return this;
        }

        public virtual IAarryDataObject FromArray(int[] values, Database<T> database)
        {
            if (values == null) return this;
            for (int i = 0; i < values.Length; ++i)
            {
                Add(database.Get(values[i]));
            }
            return this;
        }

        public virtual IEnumerator GetEnumerator()
        {
            yield return null;
        }

        public virtual void ForEach(Action<SangoObject> action)
        {
           
        }

    }
}
