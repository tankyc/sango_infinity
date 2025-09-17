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

    }
}
