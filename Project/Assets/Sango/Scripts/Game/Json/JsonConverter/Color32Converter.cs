using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Sango.Game
{
    internal class Color32Converter : JsonConverter<Color32>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            base.WriteJson(writer, value, serializer);
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Color32 dest = (Color32)existingValue;
            dest.a = 255;
            int index = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return dest;
                }
                else if(reader.TokenType == JsonToken.Integer)
                {
                    byte v = serializer.Deserialize<byte>(reader);
                    dest[index] = v;
                    index++;
                }
            }
            return dest;

        }
    }
}
