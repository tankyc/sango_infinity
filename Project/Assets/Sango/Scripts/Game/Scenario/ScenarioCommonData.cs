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
        /// 能力（研究 / 培育项目）。
        ///
        /// 对齐 Open311 的 Ability 表（s11_scen.h:2527-2600）；
        /// 消费者：force_research_ability（势力级能力研究）与 force_learn（三段培育）。
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Ability>))]
        [JsonProperty]
        public SangoObjectSet<Ability> Abilities = new SangoObjectSet<Ability>();

        /// <summary>
        /// 武将属性光环配置（统率 / 武力 / 智力 / 政治 / 魅力各一条）。
        ///
        /// 由 Data/Common/AuraConfigs.json 填充，消费方是 <see cref="AuraSystem"/>。
        /// 原版无对应系统，属本项目自主设计（见 AuraConfig.cs 头部说明）。
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<AuraConfig>))]
        [JsonProperty]
        public SangoObjectSet<AuraConfig> AuraConfigs = new SangoObjectSet<AuraConfig>();

        /// <summary>
        /// 光环全局参数（军魂系数等），与 AuraConfigs 同文件、同属顶层保留键。
        /// </summary>
        [JsonProperty]
        public AuraGlobals AuraGlobals = new AuraGlobals();

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
        /// 国号（对齐 Open311 `nation_t`，84 条：史实国号 / 异民族 / 贼 / 儐系列占位）
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Nation>))]
        [JsonProperty]
        public SangoObjectSet<Nation> Nations = new SangoObjectSet<Nation>();

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
        /// 按等级下标取兵种适应等级配置。
        ///
        /// 【边界规则】与原 <c>ScenarioVariables.troops_adaptation_level_boost</c> 的钳制口径一致：
        /// <list type="bullet">
        /// <item>下标 &lt; 0：按 0 处理（最低等级）；</item>
        /// <item>下标 &gt;= 配置条数：<b>回退到最后一个等级</b>，而不是返回 null 或 0；</item>
        /// <item>配置存在空洞（该下标为空）时向前回退到最近一条有效配置，最多回退到 0 号位。</item>
        /// </list>
        /// 数据集为空（未进剧本 / 未加载通用数据）时返回 null，调用方需判空并给出默认加成。
        /// </summary>
        /// <param name="level">适应等级下标</param>
        /// <returns>等级配置对象；数据集为空时返回 null</returns>
        public AbilityLevelType GetAbilityLevelType(int level)
        {
            if (AbilityLevelTypes == null)
                return null;

            int count = AbilityLevelTypes.Count;
            if (count <= 0)
                return null;

            if (level < 0) level = 0;
            if (level >= count) level = count - 1;

            AbilityLevelType type = AbilityLevelTypes.Get(level);
            // 配置有洞时向前回退，避免加成意外退化成 0
            for (int i = level - 1; i >= 0 && type == null; i--)
                type = AbilityLevelTypes.Get(i);
            return type;
        }

        // 【武将库已迁出本类】原字段：
        //   [JsonConverter(typeof(SangoObjectSetConverter<PersonLib>))][JsonProperty]
        //   public SangoObjectSet<PersonLib> PersonLibrary = new SangoObjectSet<PersonLib>();
        //
        // 【为什么移除】本类挂在 Scenario.CommonData 上且带 [JsonProperty]，会把整份
        // 832 条武将库写进每一份存档；同时它又是"Data/Common 全目录 PopulateObject"的产物，
        // 与 GameCustomEdit.CoreScenarioAddon（读 Data/PersonLibrary.json）构成两套互不同步的
        // 数据源（实测两文件字段数 45 vs 42、description 逐条不同）。
        //
        // 【现在从哪取】统一走 <see cref="GameCustomEdit.CoreScenarioAddon"/>：
        //   GameCustomEdit.Instance.CoreScenarioAddon.PersonLibrary
        // 该对象在 GameData.Init() 里一次性加载，生命周期长于任一剧本，offset = 0，
        // 因此 Get(id) / ForEach 的口径与原来的 SangoObjectSet 完全一致。

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
            Nations.ForEach(x => x.OnScenarioPrepare(scenario));
            Skills.ForEach(x => x.OnScenarioPrepare(scenario));
            PersonLevels.ForEach(x => x.OnScenarioPrepare(scenario));
            Personalities.ForEach(x => x.OnScenarioPrepare(scenario));
            Argumentations.ForEach(x => x.OnScenarioPrepare(scenario));
            AbilityLevelTypes.ForEach(x => x.OnScenarioPrepare(scenario));
            // 光环配置目前没有需要解析的 int[] 列表字段，这里保持与其它表一致的调用口径，
            // 以后给 AuraConfig 增加运行期引用字段时不会漏掉 prepare。
            AuraConfigs.ForEach(x => x.OnScenarioPrepare(scenario));
            // PersonLibrary 已迁出本类（见字段处的说明）。PersonLib.OnScenarioPrepare 本身是
            // 空实现（PersonLib.cs:191-193，库条目只是数据模板，不做运行期解析），
            // 所以这里去掉该行不改变任何行为。
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
