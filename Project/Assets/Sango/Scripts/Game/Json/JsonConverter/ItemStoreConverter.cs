using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class ItemStoreConverter : JsonConverter<ItemStore>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            ItemStore dest = value as ItemStore;
            int[] ints = dest.ToArray();
            foreach (int i in ints)
            {
                writer.WriteValue(i);
            }
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            ItemStore dest = existingValue as ItemStore;
            List<int> ints = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return dest.FromArray(ints.ToArray());
                }
                else if (reader.TokenType == JsonToken.Integer)
                {
                    int v = serializer.Deserialize<int>(reader);
                    ints.Add(v);
                }
            }
            return dest;
        }
    }
}
