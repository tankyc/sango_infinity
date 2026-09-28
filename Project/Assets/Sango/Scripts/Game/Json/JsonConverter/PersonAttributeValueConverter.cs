using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class PersonAttributeValueConverter : JsonConverter<PersonAttributeValue>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            PersonAttributeValue dest = value as PersonAttributeValue;
            writer.WriteStartArray();
            writer.WriteValue(dest.baseValue);
            writer.WriteValue(dest.changeType?.Id ?? 0);
            writer.WriteValue(dest.valueExp);
            writer.WriteValue(dest.valueFacter);
            writer.WriteValue(dest.Value);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            PersonAttributeValue dest = existingValue as PersonAttributeValue;

            // 兼容旧版武将库数据（未带 dataVersion 版本标记）：五维存的是单个整数，
            // 这里直接当作基础值，成长类型保持默认（游戏内按 5 普通型处理）。
            if (reader.TokenType != JsonToken.StartArray)
            {
                dest.baseValue = serializer.Deserialize<int>(reader);
                dest.UpdateNoAge();
                return dest;
            }

            List<int> ints = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.Integer)
                {
                    int id = serializer.Deserialize<int>(reader);
                    ints.Add(id);
                }
                else if (reader.TokenType == JsonToken.EndArray)
                {
                    dest.FromArray(ints.ToArray());
                    // 精简数组（如新版早期的 [基础值, 成长类型Id]）没有"最终值"一项，
                    // 这里按基础值兜底，避免 Value / Command 等取值口径拿到 0。
                    if (ints.Count < 5) dest.UpdateNoAge();
                    return dest;
                }
            }
            return dest;
        }
    }
}

