using System;
using System.Collections.Generic;
using System.Xml;

namespace Sango.Game
{
    public class LinkObjectList<T, T1> where T : ScenarioObject<T1>, new() where T1 : SangoObject, new()
    {
        private List<T> values = new List<T>();

        public LinkObjectList()
        {
           
        }

        void Init(int length)
        {
            values.Clear();
        }

        public bool Contains(int id)
        {
            return values.Find(x => x.Id == id) != null;
        }

        public T1 Get(int id)
        {
            T t = values.Find(x => x.Id == id);
            if (t != null)
                return t.Object;
            return null;
        }
        public void Add(T1 o)
        {

            values.Add(new T() { Object = o });
        }

        public int Count { get { return values.Count; } }
        public int Length { get { return values.Count; } }

        public void ForEach(Action<T> action)
        {
            for (int i = 0; i < values.Count; i++)
            {
                T obj = values[i];
                if (obj != null)
                    action(obj);
            }
        }

        public T Find(Predicate<T> match)
        {
            return values.Find(match);
        }

        public List<T> FindAll(Predicate<T> match)
        {
            return values.FindAll(match);
        }

        public void Sort(IComparer<T> comparer)
        {
            values.Sort(comparer);
        }

        public void Sort(Comparison<T> comparison)
        {
            values.Sort(comparison);
        }
        public void Remove(T1 obj)
        {
#if UNITY_EDITOR
            if (obj == null)
            {
                Sango.Log.Error("不能移除null元素!!");
                return;
            }
#endif
            for (int i = 0; i < values.Count; i++)
            {
                T o = values[i];
                if (o.Object == obj)
                {
                    values.RemoveAt(i);
                    return;
                }
            }
        }

        public T this[int aIndex] { get { return values[aIndex]; } set { } }
       
    }
}
