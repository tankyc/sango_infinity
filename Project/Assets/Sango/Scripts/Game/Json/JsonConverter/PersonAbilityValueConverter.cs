using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class PersonAbilityValueConverter : JsonConverter<PersonAbilityValue>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            PersonAbilityValue dest = value as PersonAbilityValue;
            writer.WriteStartArray();
            writer.WriteValue(dest.baseValue);
            writer.WriteValue(dest.valueExp);
            writer.WriteValue(dest.value);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            PersonAbilityValue dest = existingValue as PersonAbilityValue;

            // 兼容旧版武将库数据（未带 dataVersion 版本标记）：兵种适性存的是单个整数，
            // 这里直接当作基础值（也就是等级），经验置 0。
            if (reader.TokenType != JsonToken.StartArray)
            {
                dest.baseValue = serializer.Deserialize<int>(reader);
                dest.valueExp = 0;
                dest.value = dest.baseValue;
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
                    // 精简数组没有"等级"一项（如 [基础值] / [基础值, 经验]），按基础值兜底
                    if (ints.Count < 3) dest.value = dest.baseValue;
                    return dest;
                }
            }
            return dest;
        }
    }
}

