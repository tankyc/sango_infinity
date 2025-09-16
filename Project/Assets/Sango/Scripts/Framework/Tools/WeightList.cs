using Sango.Game;
using System;
using System.Collections.Generic;

namespace Sango.Tools
{
    public class WeightList<T>
    {
        struct Node<T>
        {
            public int wight;
            public T value;
        }
        List<Node<T>> nodes = new List<Node<T>>();
        public int TotaleWeight { get; private set; }
        public int Count { get { return nodes.Count; } }

        public void Push(T value, int wight)
        {
            TotaleWeight += wight;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node<T> node = nodes[i];
                if (wight > node.wight)
                {
                    nodes.Insert(i, new Node<T>() { value = value, wight = wight });
                    return;
                }
            }
            nodes.Add(new Node<T>() { value = value, wight = wight });
        }

        public T RandomGet()
        {
            if (Count == 0) return default(T);
            int ran = GameRandom.Range(TotaleWeight);
            for (int i = 0; i < nodes.Count; i++)
            {
                Node<T> node = nodes[i];
                if (ran <= node.wight)
                {
                    return node.value;
                }
                else
                    ran -= node.wight;
            }
            return nodes[nodes.Count - 1].value;
        }
        public T FastRandomGet(int downSamlpe)
        {
            if (Count == 0) return default(T);

            int total = 0;
            downSamlpe = Math.Min(downSamlpe, 2);
            int halfCount = nodes.Count / downSamlpe;
            for (int i = 0; i < halfCount; i++)
            {
                Node<T> node = nodes[i];
                total += node.wight;
            }
            int ran = GameRandom.Range(total);
            halfCount = Math.Max(halfCount, 1);
            for (int i = 0; i < halfCount; i++)
            {
                Node<T> node = nodes[i];
                if (ran <= node.wight)
                {
                    return node.value;
                }
                else
                    ran -= node.wight;
            }
            return nodes[halfCount - 1].value;
        }

        public T Lower()
        {
            if (Count == 0)
                return default(T);
            int pos = Count - 1;
            T rs = nodes[pos].value;
            nodes.RemoveAt(pos);
            return rs;
        }

        public T Higher()
        {
            if (Count == 0)
                return default(T);
            T rs = nodes[0].value;
            nodes.RemoveAt(0);
            return rs;
        }

        public void AllHigher(List<T> result)
        {
            if (Count == 0) return;

            Node<T> high = nodes[0];
            int max = high.wight;
            result.Add(high.value);
            for (int i = 1; i < Count; ++i)
            {
                Node<T> node = nodes[i];
                if (node.wight != max)
                    return;
                else
                    result.Add(node.value);
            }
        }

        public void AllLower(List<T> result)
        {
            if (Count == 0) return;

            int count = Count;
            Node<T> lower = nodes[count - 1];
            int low = lower.wight;
            result.Add(lower.value);
            for (int i = count - 2; i >= 0; --i)
            {
                Node<T> node = nodes[i];
                if (node.wight != low)
                    return;
                else
                    result.Add(node.value);
            }
        }

        public void Clear()
        {
            TotaleWeight = 0;
            nodes.Clear();
        }

    }
}
