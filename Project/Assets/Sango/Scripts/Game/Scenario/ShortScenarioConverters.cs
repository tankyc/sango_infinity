// =====================================================================
//  ShortScenarioConverters.cs
//  说明：ShortForce / ShortPerson / ShortCity 三个数据类的零反射反序列化转换器。
//  全部基于 JsonConverter<T>.ReadJson(JsonReader,Type,object,JsonSerializer)
//  手写 token 解析，不使用任何反射 / JObject / serializer.Deserialize，
//  符合 Newtonsoft.Json（TKNewtonsoft.Json）官方推荐的最快反序列化方案。
// =====================================================================

using TKNewtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sango.Core
{
    /// <summary>
    /// ShortForce 高性能反序列化转换器（无反射）。
    /// 字段：
    ///   Id, Name, Governor, Counsellor, Flag, desc
    /// </summary>
    public sealed class ShortForceConverter : JsonConverter<ShortForce>
    {
        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            // 1. 跳过 null
            if (reader.TokenType == JsonToken.Null)
                return null;

            // 2. 必须是 { 开头
            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(
                    "Unexpected token '" + reader.TokenType + "' when parsing ShortForce; expected StartObject.");

            // 3. 复用或新建对象
            ShortForce force = (existingValue as ShortForce) ?? new ShortForce();

            // 4. 手写 token 解析，零反射
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                string propName = (string)reader.Value;
                if (!reader.Read())
                    break;

                switch (propName)
                {
                    case "Id":
                        force.Id = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "Name":
                        force.Name = reader.Value as string;
                        break;
                    case "Governor":
                        force.Governor = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "Counsellor":
                        force.Counsellor = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "Flag":
                        force.Flag = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "desc":
                        force.desc = reader.Value as string;
                        break;
                    default:
                        // 跳过未知字段，防止 reader 偏移
                        reader.Skip();
                        break;
                }
            }

            return force;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // 该转换器仅用于反序列化，写出由默认序列化器处理
            throw new NotSupportedException("ShortForceConverter 只支持反序列化。");
        }
    }

    /// <summary>
    /// ShortPerson 高性能反序列化转换器（无反射）。
    /// 字段：
    ///   Id, Name, BelongForce, BelongCity, headIconID, imageID
    /// </summary>
    public sealed class ShortPersonConverter : JsonConverter<ShortPerson>
    {
        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(
                    "Unexpected token '" + reader.TokenType + "' when parsing ShortPerson; expected StartObject.");

            ShortPerson person = (existingValue as ShortPerson) ?? new ShortPerson();

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                string propName = (string)reader.Value;
                if (!reader.Read())
                    break;

                switch (propName)
                {
                    case "Id":
                        person.Id = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "Name":
                        person.Name = reader.Value as string;
                        break;
                    case "BelongForce":
                        person.BelongForce = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "BelongCity":
                        person.BelongCity = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "headIconID":
                        person.headIconID = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "imageID":
                        person.imageID = reader.Value?.ToString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return person;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("ShortPersonConverter 只支持反序列化。");
        }
    }

    /// <summary>
    /// ShortCity 高性能反序列化转换器（无反射）。
    /// 字段：
    ///   Id, Name, BelongForce, BuildingType, x, y, troops, gold, food
    /// </summary>
    public sealed class ShortCityConverter : JsonConverter<ShortCity>
    {
        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(
                    "Unexpected token '" + reader.TokenType + "' when parsing ShortCity; expected StartObject.");

            ShortCity city = (existingValue as ShortCity) ?? new ShortCity();

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                string propName = (string)reader.Value;
                if (!reader.Read())
                    break;

                switch (propName)
                {
                    case "Id":
                        city.Id = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "Name":
                        city.Name = reader.Value as string;
                        break;
                    case "BelongForce":
                        city.BelongForce = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "BuildingType":
                        city.BuildingType = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "x":
                        city.x = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "y":
                        city.y = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "troops":
                        city.troops = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "gold":
                        city.gold = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    case "food":
                        city.food = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return city;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("ShortCityConverter 只支持反序列化。");
        }
    }

    /// <summary>
    /// Dictionary&lt;int, ShortPerson&gt; 过滤反序列化转换器。
    /// 仅解析 _allowedIds 中存在的武将 ID；其余 entry 用 reader.Skip() 零开销跳过。
    /// 用于场景初始化时只加载"主公/军师"，大幅缩短加载时间。
    /// </summary>
    public sealed class ShortPersonSetConverter : JsonConverter<SangoObjectSet<ShortPerson>>
    {
        private readonly HashSet<int> _allowedIds;
        private readonly ShortPersonConverter _innerConverter;

        public ShortPersonSetConverter(HashSet<int> allowedIds)
        {
            _allowedIds = allowedIds;
            _innerConverter = new ShortPersonConverter();
        }

        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            // 1. 跳过 null
            if (reader.TokenType == JsonToken.Null)
                return null;

            // 2. 必须是 { 开头
            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(
                    "Unexpected token '" + reader.TokenType + "' when parsing Dictionary<int, ShortPerson>; expected StartObject.");

            // 3. 复用或新建字典
            SangoObjectSet<ShortPerson> dict = (existingValue as SangoObjectSet<ShortPerson>) ?? new SangoObjectSet<ShortPerson>();

            // 4. 手写 token 解析；只对白名单 ID 走完整 ShortPerson 解析，其他直接 skip
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                // 字典 key：武将 ID
                int id = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);

                // 推进到值（StartObject）
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    continue;

                if (_allowedIds != null && !_allowedIds.Contains(id))
                {
                    // 非主公/军师：直接跳过整个对象，零解析开销
                    reader.Skip();
                    continue;
                }

                // 主公/军师：复用 ShortPersonConverter 零反射解析
                ShortPerson person = _innerConverter.ReadJson(reader, typeof(ShortPerson), null, serializer) as ShortPerson;
                if (person != null)
                    dict.Add(person);
            }

            return dict;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("ShortPersonSetConverter 只支持反序列化。");
        }
    }

    /// <summary>
    /// Dictionary&lt;int, ShortPerson&gt; 过滤反序列化转换器。
    /// 仅解析 _allowedIds 中存在的武将 ID；其余 entry 用 reader.Skip() 零开销跳过。
    /// 用于场景初始化时只加载"主公/军师"，大幅缩短加载时间。
    /// </summary>
    public sealed class ShortCitySetConverter : JsonConverter<SangoObjectSet<ShortCity>>
    {
        private readonly ShortCityConverter _innerConverter;

        public ShortCitySetConverter()
        {
            _innerConverter = new ShortCityConverter();
        }

        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            // 1. 跳过 null
            if (reader.TokenType == JsonToken.Null)
                return null;

            // 2. 必须是 { 开头
            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(
                    "Unexpected token '" + reader.TokenType + "' when parsing Dictionary<int, ShortCity>; expected StartObject.");

            // 3. 复用或新建字典
            SangoObjectSet<ShortCity> dict = (existingValue as SangoObjectSet<ShortCity>) ?? new SangoObjectSet<ShortCity>();

            // 4. 手写 token 解析；只对白名单 ID 走完整 ShortCity 解析，其他直接 skip
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                // 字典 key：武将 ID
                int id = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);

                // 推进到值（StartObject）
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    continue;

                // 主公/军师：复用 ShortCityConverter 零反射解析
                ShortCity person = _innerConverter.ReadJson(reader, typeof(ShortCity), null, serializer) as ShortCity;
                if (person != null)
                    dict.Add(person);
            }

            return dict;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("ShortForceSetConverter 只支持反序列化。");
        }
    }

    /// <summary>
    /// Dictionary&lt;int, ShortForce&gt; 过滤反序列化转换器。
    /// 仅解析 _allowedIds 中存在的武将 ID；其余 entry 用 reader.Skip() 零开销跳过。
    /// 用于场景初始化时只加载"主公/军师"，大幅缩短加载时间。
    /// </summary>
    public sealed class ShortForceSetConverter : JsonConverter<SangoObjectSet<ShortForce>>
    {
        private readonly ShortForceConverter _innerConverter;

        public ShortForceSetConverter()
        {
            _innerConverter = new ShortForceConverter();
        }

        public override object ReadJson(
            JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            // 1. 跳过 null
            if (reader.TokenType == JsonToken.Null)
                return null;

            // 2. 必须是 { 开头
            if (reader.TokenType != JsonToken.StartObject)
                throw new JsonSerializationException(
                    "Unexpected token '" + reader.TokenType + "' when parsing Dictionary<int, ShortForce>; expected StartObject.");

            // 3. 复用或新建字典
            SangoObjectSet<ShortForce> dict = (existingValue as SangoObjectSet<ShortForce>) ?? new SangoObjectSet<ShortForce>();

            // 4. 手写 token 解析；只对白名单 ID 走完整 ShortForce 解析，其他直接 skip
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                // 字典 key：武将 ID
                int id = Convert.ToInt32(reader.Value, CultureInfo.InvariantCulture);

                // 推进到值（StartObject）
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    continue;

                // 主公/军师：复用 ShortForceConverter 零反射解析
                ShortForce person = _innerConverter.ReadJson(reader, typeof(ShortForce), null, serializer) as ShortForce;
                if (person != null)
                    dict.Add(person);
            }

            return dict;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException("ShortForceSetConverter 只支持反序列化。");
        }
    }
}
