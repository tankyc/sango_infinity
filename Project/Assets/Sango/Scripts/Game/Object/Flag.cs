using Newtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Flag : SangoObject
    {
        //[JsonConverter(typeof(Color32Converter))]
        //[JsonProperty]public UnityEngine.Color32 color;

        [JsonConverter(typeof(ColorConverter))]
        [JsonProperty] public UnityEngine.Color color;

        UnityEngine.Color Color => color;
    }
}
