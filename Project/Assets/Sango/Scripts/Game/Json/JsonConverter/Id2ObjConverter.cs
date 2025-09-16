using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public struct DelaySetValue
    {
        public object target;
        public JsonProperty property;
        public int id;
        public Type objectType;

        public static List<DelaySetValue> delaySetValues_List = new List<DelaySetValue>();
        public static void OnScenarioPrepare(Scenario scenario)
        {
            for (int i = 0; i < delaySetValues_List.Count; i++)
            {
                DelaySetValue setValue = delaySetValues_List[i];
                var value = scenario.GetObject(setValue.id, setValue.objectType);
                if (value != null && setValue.property != null)
                    setValue.property.ValueProvider.SetValue(setValue.target, value);
            }
            delaySetValues_List.Clear();
            scenario.Event.OnPrepare -= OnScenarioPrepare;
        }
        public static void Add(DelaySetValue setValue)
        {
            if (delaySetValues_List.Count == 0)
                Scenario.Cur.Event.OnPrepare += OnScenarioPrepare;
            delaySetValues_List.Add(setValue);
        }
    }

    public class Id2ObjConverter<T> : JsonConverter<T> where T : SangoObject, new()
    {
        public override T Create(Type objectType)
        {
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            SangoObject dest = value as SangoObject;
            writer.WriteValue(dest.Id);
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer, JsonProperty property, object target)
        {
            //while (reader.Read())
            {
                int id = serializer.Deserialize<int>(reader);
                DelaySetValue.Add(new DelaySetValue
                {
                    id = id,
                    target = target,
                    property = property,
                    objectType = objectType
                });
                return null;
            }
            //return null;
        }
    }
}
