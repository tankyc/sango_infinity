using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BuildingType : SangoObject
    {
        [JsonProperty] public string desc;
        [JsonProperty] public byte kind;
        [JsonProperty] public string icon;
        [JsonProperty] public int model;
        [JsonProperty] public int durabilityLimit;
        [JsonProperty] public int buildNumLimit;
        [JsonProperty] public int goldGain;
        [JsonProperty] public int foodGain;
        [JsonProperty] public float populationGain;
        [JsonProperty] public int cost;
        [JsonProperty] public byte radius;
        [JsonProperty] public bool isIntrior;

        /// <summary>
        /// 反击攻击力
        /// </summary>
        [JsonProperty] public int atk;

        /// <summary>
        /// 被伤害倍率
        /// </summary>
        [JsonProperty] public float damageBounds;
        
    }
}
