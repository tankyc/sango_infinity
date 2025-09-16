//using Newtonsoft.Json;
//using Newtonsoft.Json.Converters;
//using System;

//namespace Sango.Game
//{
//    public class SangoObjectSetConverter<T> : CustomCreationConverter<SangoObjectSet<T>> where T : SangoObject, new()
//    {
//        public override SangoObjectSet<T> Create(Type objectType)
//        {
//            return new SangoObjectSet<T>();
//        }
//        public override bool CanRead { get { return true; } }
//        public override bool CanWrite { get { return true; } }
//        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//        {
//            base.WriteJson(writer, value, serializer);
//        }
//        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//        {
//            Sango.Log.Error($"Start Converter SangoObjectSet<{typeof(T).ToString()}>");
//            SangoObjectSet<T> dest = existingValue as SangoObjectSet<T>;
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
