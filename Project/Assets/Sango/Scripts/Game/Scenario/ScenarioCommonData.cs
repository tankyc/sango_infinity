using Newtonsoft.Json;

namespace Sango.Game
{
    public class ScenarioCommonData
    {
        /// <summary>
        /// 地形类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<TerrainType>))]
        public SangoObjectSet<TerrainType> TerrainTypes = new SangoObjectSet<TerrainType>();
        /// <summary>
        /// 建筑类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<BuildingType>))]
        public SangoObjectSet<BuildingType> BuildingTypes = new SangoObjectSet<BuildingType>();
        /// <summary>
        /// 特性
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Feature>))]
        public SangoObjectSet<Feature> Features = new SangoObjectSet<Feature>();
        /// <summary>
        /// 兵种类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectMaptConverter<TroopType>))]
        public SangoObjectMap<TroopType> TroopTypes = new SangoObjectMap<TroopType>();

        /// <summary>
        /// 道具类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectMaptConverter<ItemType>))]
        public SangoObjectMap<ItemType> ItemTypes = new SangoObjectMap<ItemType>();

        /// <summary>
        /// 兵种动画
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<TroopAnimation>))] 
        public SangoObjectSet<TroopAnimation> TroopAnimations = new SangoObjectSet<TroopAnimation>();
        /// <summary>
        /// 能力变化类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<AttributeChangeType>))] 
        public SangoObjectSet<AttributeChangeType> AttributeChangeTypes = new SangoObjectSet<AttributeChangeType>();
        /// <summary>
        /// 能力类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<PersonAttributeType>))] 
        public SangoObjectSet<PersonAttributeType> PersonAttributeTypes = new SangoObjectSet<PersonAttributeType>();
        /// <summary>
        /// 能力类型
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<CityLevelType>))]
        public SangoObjectSet<CityLevelType> CityLevelTypes = new SangoObjectSet<CityLevelType>();
        /// <summary>
        /// 旗帜
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Flag>))] 
        public SangoObjectSet<Flag> Flags = new SangoObjectSet<Flag>();
        /// <summary>
        /// 州
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<State>))] 
        public SangoObjectSet<State> States = new SangoObjectSet<State>();
        /// <summary>
        /// 官职
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Official>))] 
        public SangoObjectSet<Official> Officials = new SangoObjectSet<Official>();
        /// <summary>
        /// 技能
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Skill>))] 
        public SangoObjectSet<Skill> Skills = new SangoObjectSet<Skill>();

        /// <summary>
        /// 技能
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<PersonLevel>))]
        public SangoObjectSet<PersonLevel> PersonLevels = new SangoObjectSet<PersonLevel>();

        
    }
}
