using System.Collections.Generic;

namespace Sango.Tools
{
    public class PriorityQueue<T> where T : class
    {
        struct Node<T>
        {
            public int priority;
            public T value;
        }
        List<Node<T>> nodes = new List<Node<T>>();
        public bool reverse = false;

        public int Count { get { return nodes.Count; } }

        /// <summary>
        /// 获取当前所有候选的 priority 合计。
        ///
        /// 【用途】按权重随机挑选候选时，需要先知道总权重才能做一次区间取随机
        /// （例如 AI 攻城目标：<c>Roll(GetTotalPriority())</c> 再累减落到某个城）。
        /// 逐个候选 <c>Chance(p, 10000)</c> 的做法会随候选数增加而整体命中率下降，
        /// 且高权重候选不一定先被抽到。
        ///
        /// 【为什么每次遍历而不缓存】候选数很少（AI 攻城的邻城通常个位数），
        /// 维护缓存值要保证 Push / PushLower / Higher / Lower / Clear 全部同步，
        /// 漏一处就是"总权重不对、随机偏斜"的隐性 bug。这里 O(n) 更稳。
        /// </summary>
        /// <returns>所有节点的 priority 之和；空队列返回 0</returns>
        public int GetTotalPriority()
        {
            int total = 0;
            for (int i = 0; i < nodes.Count; i++)
                total += nodes[i].priority;
            return total;
        }

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
                return null;
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
                return null;
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
                return null;
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
                return null;
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
