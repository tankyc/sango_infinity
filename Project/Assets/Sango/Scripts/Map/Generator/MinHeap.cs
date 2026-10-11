namespace Sango.MapGen
{
    /// <summary>
    /// 二叉最小堆，供 Dijkstra 等最短路算法使用。
    /// 容量按需自动扩容(每次翻倍)，避免因松弛次数超过节点数而丢弃元素。
    /// </summary>
    public class MinHeap
    {
        private int[] _cost;
        private int[] _item;
        private int _count;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="capacity">初始容量</param>
        public MinHeap(int capacity = 64)
        {
            int cap = capacity > 0 ? capacity : 64;
            _cost = new int[cap];
            _item = new int[cap];
            _count = 0;
        }

        /// <summary>堆内元素数量</summary>
        public int Count { get { return _count; } }

        /// <summary>清空堆</summary>
        public void Clear()
        {
            _count = 0;
        }

        /// <summary>
        /// 压入一个元素
        /// </summary>
        /// <param name="cost">代价(用于比较)</param>
        /// <param name="item">元素值(通常为地格下标)</param>
        public void Push(int cost, int item)
        {
            if (_count >= _cost.Length)
                Grow();

            int i = _count++;
            _cost[i] = cost;
            _item[i] = item;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_cost[parent] <= _cost[i])
                    break;
                Swap(parent, i);
                i = parent;
            }
        }

        /// <summary>
        /// 弹出代价最小的元素
        /// </summary>
        /// <param name="cost">输出的代价</param>
        /// <returns>元素值</returns>
        public int Pop(out int cost)
        {
            int topItem = _item[0];
            cost = _cost[0];

            _count--;
            if (_count > 0)
            {
                _cost[0] = _cost[_count];
                _item[0] = _item[_count];

                int i = 0;
                while (true)
                {
                    int left = (i << 1) + 1;
                    int right = left + 1;
                    int smallest = i;

                    if (left < _count && _cost[left] < _cost[smallest])
                        smallest = left;
                    if (right < _count && _cost[right] < _cost[smallest])
                        smallest = right;
                    if (smallest == i)
                        break;

                    Swap(smallest, i);
                    i = smallest;
                }
            }

            return topItem;
        }

        /// <summary>容量翻倍</summary>
        private void Grow()
        {
            int newCap = _cost.Length * 2;
            int[] newCost = new int[newCap];
            int[] newItem = new int[newCap];
            System.Array.Copy(_cost, newCost, _count);
            System.Array.Copy(_item, newItem, _count);
            _cost = newCost;
            _item = newItem;
        }

        /// <summary>交换两个位置</summary>
        private void Swap(int a, int b)
        {
            int tmp = _cost[a]; _cost[a] = _cost[b]; _cost[b] = tmp;
            tmp = _item[a]; _item[a] = _item[b]; _item[b] = tmp;
        }
    }
}
