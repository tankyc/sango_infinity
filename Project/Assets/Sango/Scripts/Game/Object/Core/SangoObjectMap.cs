using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Sango.Core
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

        /// <summary>
        /// 遍历全部条目。
        ///
        /// 【跳过 Id &lt;= 0】<c>SangoObjectSet</c> 的 0 号位是"占位/默认值"（它的 ForEach 从下标 1 开始），
        /// Map 没有下标概念，但 <see cref="Default"/> 被访问时会塞入一个 Id = 0 的合成对象。
        /// 这里统一跳过，保证两种容器的遍历结果一致 —— 否则"读过一次 Default"
        /// 就会让 Id = 0 的空对象混进每次 ForEach（例如被当成一条特技去装配动作）。
        /// </summary>
        public override void ForEach(Action<T> action)
        {
            foreach (T obj in objects.Values)
            {
                if (obj != null && obj.Id > 0)
                    action(obj);
            }
        }

        /// <summary>遍历全部条目（跳过 Id &lt;= 0，原因见上）</summary>
        public override void ForEach(Action<SangoObject> action)
        {
            foreach (T obj in objects.Values)
            {
                if (obj != null && obj.Id > 0)
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

        /// <summary>
        /// 按 Id 移除（与 <c>SangoObjectSet&lt;T&gt;.Remove(int)</c> 对齐，
        /// 避免调用方因容器类型不同而编译失败）。
        /// </summary>
        /// <param name="id">对象 Id</param>
        public void Remove(int id)
        {
            objects.Remove(id);
        }

        /// <summary>
        /// 随机取一个对象（基类默认返回 null，Set / List 都自己实现了，这里补齐）。
        ///
        /// 【为什么必须重写】基类 <see cref="Database{T}.RandomGet"/> 是 virtual 且返回 null，
        /// 依赖它的逻辑（随机特技 / 随机兵种等）在换成 Map 后会**静默返回 null**。
        /// </summary>
        public override T RandomGet()
        {
            if (objects.Count == 0)
                return null;

            int index = GameRandom.Range(0, objects.Count);
            int i = 0;
            foreach (T obj in objects.Values)
            {
                if (i == index)
                    return obj;
                i++;
            }
            return null;
        }

        /// <summary>
        /// 导出 Id 数组。
        ///
        /// 【必须重写】基类实现是 <c>for (i = 0; i &lt; Count; i++) this[i]</c>，
        /// 而 Map 的 <c>this[i]</c> 是"按 Id 取"，Id 往往是稀疏的
        /// （特技表 Id 跨 1~32028，实际只有 1496 条）。照基类跑只会取到
        /// Id &lt; Count 的那几条，**其余对象静默丢失**。这里改为遍历字典。
        /// </summary>
        public override int[] ToArray()
        {
            int[] result = new int[objects.Count];
            int n = 0;
            foreach (KeyValuePair<int, T> kv in objects)
            {
                if (kv.Value != null && kv.Key > 0)
                    result[n++] = kv.Key;
            }
            if (n != result.Length)
                System.Array.Resize(ref result, n);
            return result;
        }

        /// <summary>Id 列表字符串（重写原因同 <see cref="ToArray"/>）</summary>
        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            foreach (KeyValuePair<int, T> kv in objects)
            {
                if (kv.Value != null && kv.Key > 0)
                {
                    sb.Append(kv.Key);
                    sb.Append(',');
                }
            }
            if (sb.Length > 0)
                sb.Length--;
            return sb.ToString();
        }

        /// <summary>
        /// 排序（形式兼容）。
        ///
        /// 【注意】Dictionary 不保证枚举顺序，因此本实现**不会**改变遍历顺序 ——
        /// 只是为了不抛 <see cref="NotImplementedException"/> 而做的接口对齐。
        /// 需要有序展示的调用方应自行 <c>FindAll(...)</c> 后用 List 排序。
        /// </summary>
        public override void Sort(IComparer<T> comparer)
        {
            // 无操作：见方法注释
        }

        /// <summary>排序（形式兼容，见 <see cref="Sort(IComparer{T})"/>）</summary>
        public override void Sort(Comparison<T> comparison)
        {
            // 无操作：见 Sort(IComparer<T>) 注释
        }

        public override bool Contains(int id)
        {
            return objects.ContainsKey(id);
        }
        public override bool Contains(T t)
        {
            return objects.ContainsKey(t.Id);
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
        public override IEnumerator GetEnumerator()
        {
            return objects.Values.GetEnumerator();
        }

        public int DataCount => objects.Count;

    }
}
