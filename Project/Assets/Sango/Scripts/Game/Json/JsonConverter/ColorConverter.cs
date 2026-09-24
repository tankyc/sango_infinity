using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Sango.Core
{
    internal class ColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Color dest = (Color)value;
            writer.WriteValue(UnityEngine.ColorUtility.ToHtmlStringRGB(dest));
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string hexStr = serializer.Deserialize<string>(reader);
            Color dest = (Color)existingValue;
            dest.a = 1;
            GameUtility.HexToColor(hexStr, out dest);
            return dest;

        }
    }
}
