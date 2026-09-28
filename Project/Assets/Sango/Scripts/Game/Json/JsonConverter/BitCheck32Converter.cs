using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    internal class BitCheck32Converter : JsonConverter<BitCheck32>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            BitCheck32 dest = (BitCheck32)value;
            writer.WriteStartArray();
            writer.WriteValue(dest.state);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            BitCheck32 dest = (BitCheck32)existingValue;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return dest;
                }
                else if (reader.TokenType == JsonToken.Integer)
                {
                    uint v = serializer.Deserialize<uint>(reader);
                    dest.state = v;
                }
            }
            return dest;

        }
    }
}
