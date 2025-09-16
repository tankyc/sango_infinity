using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Flag : SangoObject
    {
        [JsonConverter(typeof(Color32Converter))]
        [JsonProperty]public UnityEngine.Color32 color;

        UnityEngine.Color Color { get { return new UnityEngine.Color(color.r / 255.0f, color.g / 255.0f, color.b / 255.0f); } }
    }
}
