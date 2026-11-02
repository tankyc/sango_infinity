using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// <see cref="HashSet{T}"/>（T = int）的 JSON 转换器：读写时以 <see cref="List{T}"/> 中转。
    ///
    /// 【为什么必须有这个类】
    /// IL2CPP（AOT）下，Newtonsoft 反序列化集合时会按需创建
    /// <c>Newtonsoft.Json.Utilities.CollectionWrapper&lt;T&gt;</c> 来包裹"不是 List&lt;T&gt; 的集合"：
    /// <code>
    /// JsonArrayContract.CreateWrapper(list) → new CollectionWrapper&lt;T&gt;(list)
    /// </code>
    /// 这个实例化是**运行时反射构造**的（MakeGenericType + 委托缓存），IL2CPP 静态分析看不到，
    /// 因此不会为它生成 AOT 代码。T 是引用类型时还能靠泛型共享跑起来，
    /// 但 T 是**值类型**（int）时必须有一份专门生成的代码，否则运行时抛：
    /// <code>
    /// ExecutionEngineException: Attempting to call method
    /// 'CollectionWrapper`1[[System.Int32, mscorlib ...]]::.ctor'
    /// for which no ahead of time (AOT) code was generated.
    /// </code>
    ///
    /// 【触发条件】实测（Newtonsoft 13.0.1，即 com.unity.nuget.newtonsoft-json@3.2.1）：
    ///   · <c>int[]</c> / <c>int[][]</c> / <c>int[,]</c> / <c>List&lt;int&gt;</c> /
    ///     <c>IEnumerable&lt;int&gt;</c> / <c>Queue&lt;int&gt;</c> / <c>Stack&lt;int&gt;</c>
    ///     → ShouldCreateWrapper = false，**不触发**；
    ///   · <c>IList&lt;int&gt;</c> / <c>ICollection&lt;int&gt;</c> / <c>ISet&lt;int&gt;</c> / <c>HashSet&lt;int&gt;</c>
    ///     → ShouldCreateWrapper = true 且元素为值类型，**触发**。
    ///
    /// 本项目里落到第二条的是 4 个 <c>[JsonProperty]</c> 的 <c>HashSet&lt;int&gt;</c> 字段：
    /// <see cref="Person.learnedAbilityIds"/>、<see cref="Force.hostileForceIds"/>、
    /// <see cref="Force.friendlyForceIds"/>、<see cref="Force.learnedAbilityIds"/>。
    /// 读剧本（ScenarioEdit 的 PopulateObject）与读档都会经过它们，
    /// 缺 AOT 代码时整条加载链路直接抛异常。
    ///
    /// 【为什么用转换器而不是把字段改成 List&lt;int&gt;】
    /// 改成 List 会丢掉去重语义，所有调用点（Contains / Add / 遍历）都要跟着改；
    /// 用转换器只换读写通道 —— JSON 仍然是普通数组，字段仍然是 HashSet，
    /// 语义、存档格式、调用方全部不变。
    /// </summary>
    public class HashSetIntJsonConverter : JsonConverter
    {
        /// <summary>
        /// 只接管 <see cref="HashSet{T}"/>（T = int）；其它类型交回默认处理。
        /// </summary>
        /// <param name="objectType">待判定的类型</param>
        /// <returns>是本转换器负责的类型返回 true</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(HashSet<int>);
        }

        /// <summary>
        /// 读 JSON 数组为 <see cref="HashSet{T}"/>。
        ///
        /// 关键点：一律走 <c>serializer.Deserialize&lt;List&lt;int&gt;&gt;</c>。
        /// <c>List&lt;int&gt;</c> 的 ShouldCreateWrapper 是 false，
        /// 因此不会产生 <c>CollectionWrapper&lt;int&gt;</c> 的实例化。
        /// </summary>
        /// <param name="reader">JSON 读取器</param>
        /// <param name="objectType">目标类型</param>
        /// <param name="existingValue">已存在的值（PopulateObject 时非空）</param>
        /// <param name="serializer">序列化器</param>
        /// <returns>填充好的集合，永不为 null</returns>
        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            // JSON 里是 null 或字段缺失：给一个空集合，避免调用方到处判空
            if (reader.TokenType == JsonToken.Null)
                return new HashSet<int>();

            List<int> list = serializer.Deserialize<List<int>>(reader);

            // 复用已有集合：保持 PopulateObject 的"清空后重填"语义，
            // 与 Newtonsoft 对普通集合的默认处理一致，避免同一对象上残留旧数据
            HashSet<int> set = existingValue as HashSet<int>;
            if (set == null)
            {
                set = new HashSet<int>();
            }
            else
            {
                set.Clear();
            }

            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                    set.Add(list[i]);
            }
            return set;
        }

        /// <summary>
        /// 写 <see cref="HashSet{T}"/> 为 JSON 数组。
        ///
        /// 同样以 <c>List&lt;int&gt;</c> 序列化，输出格式与旧版完全一致（<c>[1,2,3]</c>），
        /// 老存档、老剧本都能直接读。
        /// </summary>
        /// <param name="writer">JSON 写入器</param>
        /// <param name="value">待写入的集合</param>
        /// <param name="serializer">序列化器</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            HashSet<int> set = value as HashSet<int>;
            serializer.Serialize(writer, set == null ? new List<int>() : new List<int>(set));
        }
    }
}
