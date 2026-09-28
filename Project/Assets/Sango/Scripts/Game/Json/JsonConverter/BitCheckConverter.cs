using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    internal class BitCheckConverter : JsonConverter<BitCheck>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            BitCheck dest = (BitCheck)value;
            writer.WriteStartArray();
            for (int i = 0; i < dest.state.Length; i++)
            {
                writer.WriteValue(dest.state[i]);
            }
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            BitCheck dest = (BitCheck)existingValue;
            List<uint> keys = new List<uint>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    dest.state = keys.ToArray();
                    return dest;
                }
                else if (reader.TokenType == JsonToken.Integer)
                {
                    uint v = serializer.Deserialize<uint>(reader);
                    keys.Add(v);
                }
            }
            return dest;

        }
    }
}
