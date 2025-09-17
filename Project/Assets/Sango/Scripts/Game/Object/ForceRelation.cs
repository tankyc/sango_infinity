using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ForceRelation
    {
        [JsonProperty] public int relation;
        [JsonProperty] public byte state;

    }
}
