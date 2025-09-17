using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Sango.Game
{

    /// <summary>
    /// SangoObject限定数组数据集, 限定最大容量, 记住下标从1开始,0作为默认无效值,数据集用0号位来作为默认值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [JsonObject(MemberSerialization.OptIn)]
    public class SangoObjectSet<T> : Database<T> where T : SangoObject, new()
    {
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

        public override int Count { get { return objects.Length; } }
        public override T this[int aIndex] { get { return objects[aIndex]; } set { objects[aIndex] = value; } }

        public SangoObjectSet()
        {
            Reset(256);
        }
        public SangoObjectSet(int length)
        {
            Reset(length);
        }

        public override void Reset(int length)
        {
            objects = new T[length];
        }
        public override void Add(T obj)
        {
            if (obj.Id > 0)
            {
                objects[obj.Id] = obj;
                return;
            }
            for (int i = 1; i < objects.Length; i++)
            {
                if (objects[i] == null)
                {
                    obj.Id = i;
                    objects[i] = obj;
                    return;
                }
            }
        }

        public override void Set(T obj)
        {
            objects[obj.Id] = obj;
        }


        public override void Remove(T obj)
        {
            objects[obj.Id] = null;
        }

        public override T Get(int id)
        {
            if (!Check(id))
                return null;
            return objects[id];
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
            objects = null;
        }

        public override T Find(int id)
        {
            if (!Check(id)) return null;
            return Get(id);
        }

        public override bool Contains(int id)
        {
            if (!Check(id)) return false;
            return Get(id) != null;
        }

        public override bool Contains(T t)
        {
            if (!Check(t.Id)) return false;
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
            throw new NotImplementedException();
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
    }
}
