using TKNewtonsoft.Json;
using System;

namespace Sango.Core
{
    public class SangoObjectMapConverter<T> : JsonConverter<SangoObjectMap<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            SangoObjectMap<T> dest = value as SangoObjectMap<T>;
            dest.ForEach(x =>
            {
                writer.WritePropertyName(x.Id.ToString());
                serializer.Serialize(writer, x);
            });
            writer.WriteEndObject();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(existingValue == null)
                existingValue = Create(objectType);
            SangoObjectMap<T> dest = existingValue as SangoObjectMap<T>;
            string lastPropertyName = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    lastPropertyName = reader.Value.ToString();
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    if (!string.IsNullOrEmpty(lastPropertyName))
                    {
                        int Id = int.Parse(lastPropertyName);
                        T exsist = dest.Get(Id);
                        if (exsist != null)
                        {
                            serializer.Populate(reader, exsist);
                            continue;
                        }
                    }
                    T v = serializer.Deserialize<T>(reader);
                    dest.Add(v);
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    return dest;
                }
            }
            return dest;
        }
    }
}
