using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Sango.Core
{
    internal class Vector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector3 dest = (Vector3)value;
            writer.WriteStartArray();
            writer.WriteValue(dest.x);
            writer.WriteValue(dest.y);
            writer.WriteValue(dest.z);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Vector3 dest = (Vector3)existingValue;
            int index = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return dest;
                }
                else if(reader.TokenType == JsonToken.Float)
                {
                    float v = serializer.Deserialize<float>(reader);
                    dest[index] = v;
                    index++;
                }
            }
            return dest;

        }
    }
}
