//using Newtonsoft.Json;
//using System;

//namespace Sango.Game
//{
//    public class LinkObjectListConverter<T, T1> : JsonConverter<LinkObjectList<T, T1>> where T : ScenarioObject<T1>, new() where T1 : SangoObject, new()
//    {
//        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//        {
//            base.WriteJson(writer, value, serializer);
//        }
//        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//        {
//            LinkObjectList<T, T1> dest = existingValue as LinkObjectList<T, T1>;
//            while (reader.Read())
//            {
//                if (reader.TokenType == JsonToken.StartObject)
//                {
//                    T v = serializer.Deserialize<T>(reader);
//                    if (v.Id < dest.objects.Length)
//                        dest.objects[v.Id] = v;
//                    else
//                    {
//                        Sango.Log.Error($"数据ID超出区间[0 - {dest.objects.Length - 1}]范围 id:{v.Id}, ");
//                    }
//                }
//                else if (reader.TokenType == JsonToken.EndArray)
//                {
//                    return dest;
//                }
//            }
//            return dest;
//        }
//    }
//}
