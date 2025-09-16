using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Alliance : SangoObject
    {

        /// <summary>
        /// 部队列表
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Force>))]
        [JsonProperty]
        public SangoObjectList<Force> ForceList;

        [JsonProperty] public int leftCount;

        public bool Contains(int forceId)
        {
            return ForceList.Contains(forceId);
        }
    }
}
