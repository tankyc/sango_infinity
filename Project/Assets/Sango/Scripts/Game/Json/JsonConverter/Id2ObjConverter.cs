using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class Id2ObjConverter<T> : JsonConverter<T> where T : SangoObject, new()
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
                GameEvent.OnScenarioPrepare -= OnScenarioPrepare;
            }
            public static void Add(DelaySetValue setValue)
            {
                if (delaySetValues_List.Count == 0)
                    GameEvent.OnScenarioPrepare += OnScenarioPrepare;
                delaySetValues_List.Add(setValue);
            }
        }

        public override T Create(Type objectType)
        {
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            SangoObject dest = value as SangoObject;
            writer.WriteValue(dest.Id);
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

        public static T Id2Object<T>(Scenario scenario, int id) where T : SangoObject, new()
        {
            if (id == 0)
                return null;

            return scenario.GetObject<T>(id);
        }

        public static T Id2Object0<T>(Scenario scenario, int id) where T : SangoObject, new()
        {
            return scenario.GetObject<T>(id);
        }
    }
}
