using Newtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ForceRelation
    {
        [JsonProperty] public int relation;
        [JsonProperty] public byte state;

    }
}
