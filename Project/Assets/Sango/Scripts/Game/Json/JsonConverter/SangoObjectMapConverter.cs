using Newtonsoft.Json;
using System;

namespace Sango.Game
{
    public class SangoObjectMaptConverter<T> : JsonConverter<SangoObjectMap<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            SangoObjectMap<T> dest = value as SangoObjectMap<T>;
            dest.ForEach(x =>
            {
                serializer.Serialize(writer, x);
            });
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if(existingValue == null)
                existingValue = Create(objectType);
            SangoObjectMap<T> dest = existingValue as SangoObjectMap<T>;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    T v = serializer.Deserialize<T>(reader);
                    if(dest.Contains(v.Id))
                    {
                        Sango.Log.Error($"数据ID重复 id:{v.Id} ");
                    }
                    else
                    {
                        dest.Add(v);
                    }
                }
                else if (reader.TokenType == JsonToken.EndArray)
                {
                    return dest;
                }
            }
            return dest;
        }
    }
}
