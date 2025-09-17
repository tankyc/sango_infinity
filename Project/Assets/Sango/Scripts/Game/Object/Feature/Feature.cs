using Newtonsoft.Json;
namespace Sango.Game
{
    /// <summary>
    /// 州
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Feature : SangoObject
    {
        [JsonProperty] public string desc;
        [JsonProperty] public string kind;
        [JsonProperty] public int level;
        [JsonProperty] public int effect;
    }
}
