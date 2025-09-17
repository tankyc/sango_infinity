using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.UIElements;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SangoObjectList<T> : Database<T> where T : SangoObject, new()
    {
        public List<T> objects = new List<T>();

        public override int Count { get { return objects.Count; } }

        public override T Default => throw new NotImplementedException();

        public override void Clear() { objects.Clear(); }
        public override bool Check(int index)
        {
            if (index < 0 || index >= objects.Count)
                return false;
            return true;
        }
        public override void Reset(int length) { objects.Clear(); }
        public override void Add(T obj)
        {
#if UNITY_EDITOR || SANGO_DEBUG
            if (obj == null)
            {
                Sango.Log.Error("不能添加null元素!!");
                return;
            }

            if (objects.Contains(obj))
            {
                Sango.Log.Error("不能重复添加");
            }
#endif
            objects.Add(obj);
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
            objects[obj.Id] = obj;
        }
        public override void Remove(T obj)
        {
#if UNITY_EDITOR || SANGO_DEBUG
            if (obj == null)
            {
                Sango.Log.Error("不能移除null元素!!");
                return;
            }

            if (!objects.Contains(obj))
            {
                Sango.Log.Error("不能移除不存在的!!!");
            }
#endif


            objects.Remove(obj);
        }

        public override void RemoveAll(Predicate<T> match)
        {
            objects.RemoveAll(match);
        }

        public override T Get(int index)
        {
            return objects[index];
        }
        public override void ForEach(Action<T> action)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                T obj = objects[i];
                if (obj != null)
                    action(obj);
            }
        }
        public override T Find(Predicate<T> match)
        {
            return objects.Find(match);
        }
        public override List<T> FindAll(Predicate<T> match)
        {
            return objects.FindAll(match);
        }
        public override void Sort(IComparer<T> comparer)
        {
            objects.Sort(comparer);
        }
        public override void Sort(Comparison<T> comparison)
        {
            objects.Sort(comparison);
        }
        public override T this[int aIndex] { get { return objects[aIndex]; } set { } }


        public override T Find(int id)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                T obj = objects[i];
                if (obj != null && obj.Id == id)
                    return obj;
            }
            return null;
        }

        public override bool Contains(int id)
        {
            return objects.Find(x => x.Id == id) != null;
        }
        public override bool Contains(T t)
        {
            return objects.Contains(t);
        }
        public override IEnumerator GetEnumerator()
        {
            return objects.GetEnumerator();
        }
    }
}
