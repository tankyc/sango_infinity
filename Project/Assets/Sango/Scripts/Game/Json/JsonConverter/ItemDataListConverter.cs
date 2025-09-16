using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using static UnityEngine.Rendering.DebugUI;

namespace Sango.Game
{
    public class ItemDataListConverter : JsonConverter<List<ItemData>>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            List<ItemData> dest = value as List<ItemData>;
            dest.ForEach(x =>
            {
                writer.WriteValue(x.itemType.Id);
                writer.WriteValue(x.number);
            });
            writer.WriteEndArray();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            List<ItemData> dest = existingValue as List<ItemData>;
            int itemTypeId = 0;
            int index = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    return dest;
                }
                else if (reader.TokenType == JsonToken.Integer)
                {
                    int v = serializer.Deserialize<int>(reader);
                    if(index % 2 == 0)
                    {
                        itemTypeId = v;
                    }
                    else
                    {
                        ItemData itemData = new ItemData()
                        {
                            itemType = Scenario.Cur.CommonData.ItemTypes.Get(itemTypeId),
                            number = v
                        };
                        dest.Add(itemData);
                    }
                    index++;
                }
            }
            return dest;
        }
    }
}
