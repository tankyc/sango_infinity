using TKNewtonsoft.Json;
using System;

namespace Sango.Core
{
    public class SangoObjectSetConverter<T> : JsonConverter<SangoObjectSet<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            SangoObjectSet<T> dest = value as SangoObjectSet<T>;
            //writer.WritePropertyName("0");
            //serializer.Serialize(writer, dest.Default);
            dest.ForEach(x =>
            {
                writer.WritePropertyName(x.Id.ToString());
                serializer.Serialize(writer, x);
            });
            writer.WriteEndObject();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            SangoObjectSet<T> dest = existingValue as SangoObjectSet<T>;
            string lastPropertyName = null;
            while (reader.Read())
            {
                if(reader.TokenType == JsonToken.PropertyName)
                {
                    lastPropertyName = reader.Value.ToString();
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    if(!string.IsNullOrEmpty(lastPropertyName))
                    {
                        int Id = int.Parse(lastPropertyName);
                        T exsist = dest.Get(Id);
                        if(exsist != null)
                        {
                            serializer.Populate(reader, exsist);
                            continue;
                        }
                    }
                    T v = serializer.Deserialize<T>(reader);
                    dest.Set(v);
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
