using TKNewtonsoft.Json;
using System;

namespace Sango.Core
{
    public class SangoObjectOffSetConverter<T> : JsonConverter<SangoObjectOffSet<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            SangoObjectOffSet<T> dest = value as SangoObjectOffSet<T>;
            writer.WritePropertyName("offset");
            serializer.Serialize(writer, dest.offset);
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
            SangoObjectOffSet<T> dest = existingValue as SangoObjectOffSet<T>;
            string lastPropertyName = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value.ToString();
                    if (propertyName == "offset")
                    {
                        reader.Read();
                        int v = serializer.Deserialize<int>(reader);
                        dest.offset = v;
                    }
                    else
                    {
                        lastPropertyName = reader.Value.ToString();
                    }
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
