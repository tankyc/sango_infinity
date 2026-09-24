using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class SangoObjectListIDConverter<T> : JsonConverter<SangoObjectList<T>> where T : SangoObject, new()
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if(value != null )
            {
                SangoObjectList<T> dest = value as SangoObjectList<T>;
                dest.ForEach(x =>
                {
                    writer.WriteValue(x.Id);
                });
            }
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
                    // 只把 id 数组缓存下来，等真正被访问时由 SangoObjectList 自己解析
                    // （不再订阅 GameEvent.OnScenarioPrepare 做"容器级延迟解析"）
                    dest.arrayDataCache = list.ToArray();
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
