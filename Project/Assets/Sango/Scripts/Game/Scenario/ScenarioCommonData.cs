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
        /// <summary>
        /// 建筑类型表。
        ///
        /// 【为什么用 Map】Id 稀疏（1 城池 / 2 关隘 / 3 港 / 10+ 设施，中间有空洞），
        /// 且使用方式全是"按 Id 查"或"遍历全部"，没有"按序号取第 N 个"。
        /// Set 会按最大 Id 撑出一个大数组、所有遍历都带空洞；Map 只存真实条目。
        /// 注意：**不要**再写 <c>for (i &lt; Count) this[i]</c> 这种"下标即 Id"的循环，
        /// Map 的 Count 是实际条数，下标与 Id 无关。
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<BuildingType>))]
        [JsonProperty]
        public SangoObjectMap<BuildingType> BuildingTypes = new SangoObjectMap<BuildingType>();

        /// <summary>
        /// 特性（特技）
        ///
        /// 【为什么用 Map 而不是 Set】特技 Id 是**稀疏且跨度极大**的：
        /// 原版 1~100、模组 101~217、本命 30101~30162、宝物 32000~32028。
        /// <c>SangoObjectSet</c> 是按 Id 直接下标的数组，为了容纳 32028 会扩到 32768 槽
        /// （262 KB 引用数组，其中 31272 个空槽），且每次 <c>ForEach</c> 都要扫满 32768 次、
        /// <c>Count</c> 返回的是"最大 Id + 1"而非实际条数（容易被误用成循环上界）。
        /// 换成 <c>SangoObjectMap</c>（Dictionary）后：内存降到实际条数（1496 条），
        /// 遍历只跑真实数据，<c>Count</c> 语义变成"实际条数"，<c>Get(Id)</c> 仍是 O(1)
        /// （哈希查找，与数组下标同数量级），并且 Id 再往上加也不会撑大内存。
        ///
        /// 【JSON 结构不变】两种容器的转换器读的都是 <c>{ "Id": { ... } }</c> 形式，
        /// 现有 Features.json / Frontier*.json 无需改动，老存档也不受影响
        /// （存档里记的是 <c>FeatureListId</c> 这类 int[]，不是对象本身）。
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<Feature>))]
        [JsonProperty]
        public SangoObjectMap<Feature> Features = new SangoObjectMap<Feature>();

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
        /// 成对 / 组合特技配置（连舟寨、冀雄、车下士这类两人配合机制）。
        /// 由 Data/Common/FeaturePairs.json 填充，消费方是 <see cref="FeaturePairSystem"/>。
        /// </summary>
        /// <summary>
        /// 成对特技配置表（pairId → 配置）。使用方式只有 <c>Get(pairId)</c>，天然是 Map。
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<FeaturePairConfig>))]
        [JsonProperty]
        public SangoObjectMap<FeaturePairConfig> FeaturePairs = new SangoObjectMap<FeaturePairConfig>();

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
        /// <summary>
        /// 战法 / 计略表。
        ///
        /// 【为什么用 Map】使用方式只有 <c>Get(Id)</c>（战法 Id 直接写在 SkillInstance / 兵种配置里）
        /// 与"遍历全部"，没有按序号取；且 Id 稀疏（原版 1~100、模组扩展段）。
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<Skill>))]
        [JsonProperty]
        public SangoObjectMap<Skill> Skills = new SangoObjectMap<Skill>();

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
        /// <summary>
        /// 性格表。使用方式是 <c>Get(Id)</c> 与"遍历全部"，用 Map。
        ///
        /// 【注意】武将创建界面曾按"下标即 Id"遍历本表（`for (i &lt; Count) list[i]`），
        /// 换 Map 后该写法会漏项，已改为 <c>ForEach</c> 顺序遍历（见 UIPersonCreateDetail）。
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<Personality>))]
        [JsonProperty]
        public SangoObjectMap<Personality> Personalities = new SangoObjectMap<Personality>();

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
            FeaturePairs.ForEach(x => x.OnScenarioPrepare(scenario));

            // 道具类型：解析 ItemType.features_list → features（装备/宝物附加特技）。
            // 之前漏了这一行，ItemType.features 永远是空列表，装备特技整体不生效。
            // OnScenarioPrepare 内部有 features.Count == 0 的幂等判断，重复调用安全。
            ItemTypes.ForEach(x => x.OnScenarioPrepare(scenario));

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
