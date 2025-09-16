using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class PersonAttributeValueConverter : JsonConverter<PersonAttributeValue>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            PersonAttributeValue dest = value as PersonAttributeValue;
            writer.WriteStartArray();
            writer.WriteValue(dest.baseValue);
            writer.WriteValue(dest.changeType.Id);
            writer.WriteValue(dest.valueExp);
            writer.WriteValue(dest.valueFacter);
            writer.WriteValue(dest.value);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer, JsonProperty property, object target)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            PersonAttributeValue dest = existingValue as PersonAttributeValue;
            dest.master = target as Person;
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
                    dest.Update();
                    return dest;
                }
            }
            return dest;
        }
    }
}

