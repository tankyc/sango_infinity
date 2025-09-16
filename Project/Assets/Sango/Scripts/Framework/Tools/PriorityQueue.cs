using System.Collections.Generic;

namespace Sango.Tools
{
    public class PriorityQueue<T>
    {
        struct Node<T>
        {
            public int priority;
            public T value;
        }
        List<Node<T>> nodes = new List<Node<T>>();
        public bool reverse = false;

        public int Count { get { return nodes.Count; } }

        public void Push(T value, int priority)
        {

            for (int i = 0, count = nodes.Count; i < count; i++)
            {
                Node<T> node = nodes[i];
                if (priority > node.priority)
                {
                    nodes.Insert(i, new Node<T>() { value = value, priority = priority });
                    return;
                }
            }

            nodes.Add(new Node<T>() { value = value, priority = priority });
        }

        public void PushLower(T value, int priority)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                Node<T> node = nodes[i];
                if (priority > node.priority)
                {
                    nodes.Insert(i, new Node<T>() { value = value, priority = priority });
                    return;
                }
            }

            nodes.Add(new Node<T>() { value = value, priority = priority });
        }

        public T Lower(out int priority)
        {
            if (!reverse)
                reverse = true;
            if (Count == 0)
            {
                priority = 0;
                return default(T);
            }
            int pos = Count - 1;
            Node<T> node = nodes[pos];
            T rs = node.value;
            priority = node.priority;
            nodes.RemoveAt(pos);
            return rs;
        }
        public T Lower()
        {
            if (!reverse)
                reverse = true;
            if (Count == 0)
                return default(T);
            int pos = Count - 1;
            T rs = nodes[pos].value;
            nodes.RemoveAt(pos);
            return rs;
        }
        public T Higher(out int priority)
        {
            if (reverse)
                reverse = false;
            if (Count == 0)
            {
                priority = 0;
                return default(T);
            }
            Node<T> node = nodes[0];
            T rs = node.value;
            priority = node.priority;
            nodes.RemoveAt(0);
            return rs;
        }
        public T Higher()
        {
            if (reverse)
                reverse = false;
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
            int max = high.priority;
            result.Add(high.value);
            for (int i = 1; i < Count; ++i)
            {
                Node<T> node = nodes[i];
                if (node.priority != max)
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
            int low = lower.priority;
            result.Add(lower.value);
            for (int i = count - 2; i >= 0; --i)
            {
                Node<T> node = nodes[i];
                if (node.priority != low)
                    return;
                else
                    result.Add(node.value);
            }
        }

        public void Clear()
        {
            nodes.Clear();
        }

    }
}
