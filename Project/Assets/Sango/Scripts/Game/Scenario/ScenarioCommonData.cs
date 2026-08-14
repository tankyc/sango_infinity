using TKNewtonsoft.Json;
using System.Collections.Generic;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioCommonData
    {
        /// <summary>
        /// 地形类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<TerrainType>))]
        [JsonProperty]
        public SangoObjectSet<TerrainType> TerrainTypes = new SangoObjectSet<TerrainType>();

        /// <summary>
        /// 建筑类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<BuildingType>))]
        [JsonProperty]
        public SangoObjectSet<BuildingType> BuildingTypes = new SangoObjectSet<BuildingType>();

        /// <summary>
        /// 特性
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Feature>))]
        [JsonProperty]
        public SangoObjectSet<Feature> Features = new SangoObjectSet<Feature>();

        /// <summary>
        /// 兵种类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<TroopType>))]
        [JsonProperty]
        public SangoObjectMap<TroopType> TroopTypes = new SangoObjectMap<TroopType>();

        /// <summary>
        /// 道具类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<ItemType>))]
        [JsonProperty]
        public SangoObjectMap<ItemType> ItemTypes = new SangoObjectMap<ItemType>();

        /// <summary>
        /// 兵种动画
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<TroopAnimation>))]
        [JsonProperty]
        public SangoObjectSet<TroopAnimation> TroopAnimations = new SangoObjectSet<TroopAnimation>();

        /// <summary>
        /// 能力变化类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<AttributeChangeType>))]
        [JsonProperty]
        public SangoObjectSet<AttributeChangeType> AttributeChangeTypes = new SangoObjectSet<AttributeChangeType>();

        /// <summary>
        /// 属性类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<PersonAttributeType>))]
        [JsonProperty]
        public SangoObjectSet<PersonAttributeType> PersonAttributeTypes = new SangoObjectSet<PersonAttributeType>();

        /// <summary>
        /// 城市等级
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<CityLevelType>))]
        [JsonProperty]
        public SangoObjectSet<CityLevelType> CityLevelTypes = new SangoObjectSet<CityLevelType>();

        /// <summary>
        /// 旗帜
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Flag>))]
        [JsonProperty]
        public SangoObjectSet<Flag> Flags = new SangoObjectSet<Flag>();

        /// <summary>
        /// 州
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Province>))]
        [JsonProperty]
        public SangoObjectSet<Province> Provinces = new SangoObjectSet<Province>();

        /// <summary>
        /// 州
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Region>))]
        [JsonProperty]
        public SangoObjectSet<Region> Regions = new SangoObjectSet<Region>();

        /// <summary>
        /// 爵位称号
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Title>))]
        [JsonProperty]
        public SangoObjectSet<Title> Titles = new SangoObjectSet<Title>();

        /// <summary>
        /// 官职
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Official>))]
        [JsonProperty]
        public SangoObjectSet<Official> Officials = new SangoObjectSet<Official>();

        /// <summary>
        /// 技能
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Skill>))]
        [JsonProperty]
        public SangoObjectSet<Skill> Skills = new SangoObjectSet<Skill>();

        /// <summary>
        /// 武将等级
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<PersonLevel>))]
        [JsonProperty]
        public SangoObjectSet<PersonLevel> PersonLevels = new SangoObjectSet<PersonLevel>();

        /// <summary>
        /// 工作类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<JobType>))]
        [JsonProperty]
        public SangoObjectMap<JobType> JobTypes = new SangoObjectMap<JobType>();

        /// <summary>
        /// 工作类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<Buff>))]
        [JsonProperty]
        public SangoObjectMap<Buff> Buffs = new SangoObjectMap<Buff>();

        /// <summary>
        /// 科技
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<Technique>))]
        [JsonProperty]
        public SangoObjectMap<Technique> Techniques = new SangoObjectMap<Technique>();

        /// <summary>
        /// 性格
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Personality>))]
        [JsonProperty]
        public SangoObjectSet<Personality> Personalities = new SangoObjectSet<Personality>();

        /// <summary>
        /// 义理
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Argumentation>))]
        [JsonProperty]
        public SangoObjectSet<Argumentation> Argumentations = new SangoObjectSet<Argumentation>();

        /// <summary>
        /// 武将库
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<PersonLib>))]
        [JsonProperty] 
        public SangoObjectSet<PersonLib> PersonLibrary = new SangoObjectSet<PersonLib>();



        public List<ItemType> ItemTypeList { get; set; }

        public void Init()
        {
            ItemTypeList = new List<ItemType>();
            ItemTypes.ForEach(type =>
            {
                if (type.store && type.validTechId <= 0)
                {
                    ItemTypeList.Add(type);
                }
            });
            ItemTypeList.Sort((a, b) => a.Id.CompareTo(b.Id));
        }


        public void Load(string file)
        {
            if (File.Exists(file))
            {
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), this);
            }
        }

    }
}
