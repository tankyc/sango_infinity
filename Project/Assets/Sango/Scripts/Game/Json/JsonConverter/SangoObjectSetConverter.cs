using Newtonsoft.Json;
using System;

namespace Sango.Core
{
    public class SangoObjectSetConverter<T> : JsonConverter<SangoObjectSet<T>> where T : SangoObject, new()
    {
        /// <summary>
        /// 当前容器是否需要读写结构版本标记（dataVersion）。
        /// 目前只有武将库（PersonLib）定义了容器级版本标记，其它容器保持原样。
        /// </summary>
        private static readonly bool SupportDataVersion = typeof(T) == typeof(PersonLib);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            SangoObjectSet<T> dest = value as SangoObjectSet<T>;
            // 武将库写出时带上结构版本标记，便于读取方判断按新结构还是旧结构解析
            if (SupportDataVersion)
            {
                writer.WritePropertyName(PersonLibraryDataFormat.VersionKey);
                serializer.Serialize(writer, PersonLibraryDataFormat.CurrentVersion);
            }
            dest.ForEach(x =>
            {
                writer.WritePropertyName(x.Id.ToString());
                serializer.Serialize(writer, x);
            });
            writer.WriteEndObject();
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue == null)
                existingValue = Create(objectType);
            SangoObjectSet<T> dest = existingValue as SangoObjectSet<T>;
            // 每次读取都重新判定版本：文件未带标记即按旧结构处理
            if (SupportDataVersion) PersonLibraryDataFormat.BeginLoad();
            string lastPropertyName = null;
            while (reader.Read())
            {
                if(reader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = reader.Value.ToString();
                    if (SupportDataVersion && propertyName == PersonLibraryDataFormat.VersionKey)
                    {
                        // 容器级版本标记：记录后跳过，不作为武将数据解析
                        reader.Read();
                        PersonLibraryDataFormat.MarkLoadedVersion(serializer.Deserialize<int>(reader));
                        lastPropertyName = null;
                    }
                    else
                    {
                        lastPropertyName = propertyName;
                    }
                }
                else if (reader.TokenType == JsonToken.StartObject)
                {
                    if(!string.IsNullOrEmpty(lastPropertyName))
                    {
                        int Id = int.Parse(lastPropertyName);
                        T exsist = dest.Get(Id);
                        if(exsist != null)
                        {
                            serializer.Populate(reader, exsist);
                            continue;
                        }
                    }
                    T v = serializer.Deserialize<T>(reader);
                    dest.Set(v);
                }
                else if (reader.TokenType == JsonToken.EndObject)
                {
                    return dest;
                }
            }
            return dest;
        }
    }
}
