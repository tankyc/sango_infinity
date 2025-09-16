namespace Sango.Render
{

    public class MapCache<T>
    {
        private T[] values;
        private int count;
        private int size;
        public MapCache(int size)
        {
            values = new T[size];
            count = 0;
            this.size = size;
        }

        public int Count { get { return count; } }
        public T[] Values { get { return values; } }
        public int Size { get { return size; } }
        public int Add(T t)
        {
            values[count++] = t;
            return count;
        }
        public void Change(int index, T t)
        {
            if (index < count)
                values[index] = t;
        }

        public void Reset()
        {
            count = 0;
        }
        public void Clear()
        {
            values = null;
        }
    }

}