using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class SangoObjectListIDConverter<T> : JsonConverter<SangoObjectList<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            SangoObjectList<T> dest = value as SangoObjectList<T>;
            dest.ForEach(x =>
            {
                writer.WriteValue(x.Id);
            });
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            SangoObjectList<T> dest = existingValue as SangoObjectList<T>;
            List<int> list = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    dest.arrayDataCache = list.ToArray();
                    dest.MarkToPrepareOnScenario();
                    return dest;
                }
                else if (reader.TokenType == JsonToken.Integer)
                {
                    list.Add(serializer.Deserialize<int>(reader));
                }
            }
            return dest;
        }
    }
}
