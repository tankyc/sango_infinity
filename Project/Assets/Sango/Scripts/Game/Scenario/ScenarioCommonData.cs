using Newtonsoft.Json;
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
        /// 能力等级
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<AbilityLevelType>))]
        [JsonProperty]
        public SangoObjectSet<AbilityLevelType> AbilityLevelTypes = new SangoObjectSet<AbilityLevelType>();

        /// <summary>
        /// 武将库
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<PersonLib>))]
        [JsonProperty] 
        public SangoObjectSet<PersonLib> PersonLibrary = new SangoObjectSet<PersonLib>();

        
        public List<ItemType> ItemTypeList { get; set; }

        public void Init(Scenario scenario)
        {
            Provinces.ForEach(x =>
            {
                x.Init(scenario);
            });

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

        /// <summary>
        /// 开局准备：对**本类全部数据库**逐项调用 OnScenarioPrepare，
        /// 由各对象自己把 `int[]` 反序列化字段解析成运行期对象列表。
        /// 这是列表解析的唯一入口（容器级懒解析机制已删除）。
        /// </summary>
        public void Prepare(Scenario scenario)
        {
            Provinces.ForEach(x => x.OnScenarioPrepare(scenario));
            TerrainTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            BuildingTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            Features.ForEach(x => x.OnScenarioPrepare(scenario));
            TroopAnimations.ForEach(x => x.OnScenarioPrepare(scenario));
            AttributeChangeTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            PersonAttributeTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            CityLevelTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            Flags.ForEach(x => x.OnScenarioPrepare(scenario));
            Regions.ForEach(x => x.OnScenarioPrepare(scenario));
            Titles.ForEach(x => x.OnScenarioPrepare(scenario));
            Officials.ForEach(x => x.OnScenarioPrepare(scenario));
            Skills.ForEach(x => x.OnScenarioPrepare(scenario));
            PersonLevels.ForEach(x => x.OnScenarioPrepare(scenario));
            Personalities.ForEach(x => x.OnScenarioPrepare(scenario));
            Argumentations.ForEach(x => x.OnScenarioPrepare(scenario));
            AbilityLevelTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            PersonLibrary.ForEach(x => x.OnScenarioPrepare(scenario));
        }

        public void Load(string file)
        {
            if (File.Exists(file))
            {
                Newtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), this);
            }
        }

    }
}
