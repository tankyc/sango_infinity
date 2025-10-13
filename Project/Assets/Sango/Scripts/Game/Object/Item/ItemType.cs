using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]

    public class ItemType : SangoObject
    {
        /// <summary>
        /// 主类型
        /// </summary>
        [JsonProperty] public byte kind;

        /// <summary>
        /// 次类型
        /// </summary>
        [JsonProperty] public byte subKind;

        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 图标
        /// </summary>
        [JsonProperty] public string icon;

        /// <summary>
        /// 额外费用
        /// </summary>
        [JsonProperty] public int cost;
    }
}
