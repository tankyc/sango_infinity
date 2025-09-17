using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Sango.Game
{
    /// <summary>
    /// SangoObject映射数据集
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [JsonObject(MemberSerialization.OptIn)]
    public class SangoObjectMap<T> : Database<T> where T : SangoObject, new()
    {
        public Dictionary<int, T> objects = new Dictionary<int, T>();
        public override T Default
        {
            get
            {
                T t;
                if (!objects.TryGetValue(0, out t))
                {
                    t = new T();
                    t.Id = 0;
                    objects[0] = t;
                }
                return t;
            }
        }
        public override int Count { get { return objects.Count; } }

        public override T this[int aIndex] { get { return Get(aIndex); } set { Add(value); } }

        public SangoObjectMap()
        {

        }
        public override void Clear()
        {
            objects.Clear();
        }
        public override void Reset(int length)
        {
            objects.Clear();
        }
        public override void Add(T obj)
        {
            objects[obj.Id] = obj;
        }
        public override void Set(T obj)
        {
            objects[obj.Id] = obj;
        }
        public override void Remove(T obj)
        {
            objects.Remove(obj.Id);
        }
        public override T Get(int id)
        {
            T obj;
            if (objects.TryGetValue(id, out obj))
                return obj;
            return null;
        }

        public override bool Check(int id)
        {
            return objects.ContainsKey(id);
        }

        public override void ForEach(Action<T> action)
        {
            foreach (T obj in objects.Values)
            {
                if (obj != null)
                    action(obj);
            }
        }

        public override T Find(Predicate<T> match)
        {
            foreach (T obj in objects.Values)
            {
                if (obj != null && match.Invoke(obj))
                {
                    return obj;
                }
            }
            return null;
        }

        public override List<T> FindAll(Predicate<T> match)
        {
            List<T> list = new List<T>();
            foreach (T obj in objects.Values)
            {
                if (obj != null && match.Invoke(obj))
                {
                    list.Add(obj);
                }
            }
            return list;
        }

        public override T Find(int id)
        {
            return Get(id);
        }

        public override bool Contains(int id)
        {
            return objects.ContainsKey(id);
        }
        public override bool Contains(T t)
        {
            return objects.ContainsKey(t.Id);
        }
        public override void Sort(IComparer<T> comparer)
        {
            throw new NotImplementedException();
        }

        public override void Sort(Comparison<T> comparison)
        {
            throw new NotImplementedException();
        }

        public override void RemoveAll(Predicate<T> match)
        {
            List<T> values = FindAll(match);
            if (values != null)
            {
                foreach (T obj in values)
                {
                    objects.Remove(obj.Id);
                }
            }
        }

        public int DataCount => objects.Count;

    }
}
