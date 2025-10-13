using Newtonsoft.Json;
namespace Sango.Game
{
    /// <summary>
    /// 特性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Feature : SangoObject
    {
        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;
        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty] public string kind;
        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty] public int level;
        /// <summary>
        /// 效果
        /// </summary>
        [JsonProperty] public int effect;
        /// <summary>
        /// 效果参数
        /// </summary>
        [JsonProperty] public int[] effectParamas;
    }
}
