using Newtonsoft.Json;
using System;

namespace Sango.Game
{
    public class SangoObjectSetConverter<T> : JsonConverter<SangoObjectSet<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            SangoObjectSet<T> dest = value as SangoObjectSet<T>;
            serializer.Serialize(writer, dest.Default);
            dest.ForEach(x =>
            {
                serializer.Serialize(writer, x);
            });
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            SangoObjectSet<T> dest = existingValue as SangoObjectSet<T>;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    T v = serializer.Deserialize<T>(reader);
                    if (v.Id < dest.objects.Length)
                        dest.objects[v.Id] = v;
                    else
                    {
                        Sango.Log.Error($"数据ID超出区间[0 - {dest.objects.Length - 1}]范围 id:{v.Id}, ");
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
