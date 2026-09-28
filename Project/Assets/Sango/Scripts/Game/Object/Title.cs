using Newtonsoft.Json;
namespace Sango.Core
{
    /// <summary>
    /// 州
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Title : SangoObject
    {
        /// <summary>
        /// 指挥
        /// </summary>
        [JsonProperty]
        public int troopsLimit;

        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty]
        public int level;
    }
}
