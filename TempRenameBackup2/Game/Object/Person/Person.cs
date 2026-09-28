using TKNewtonsoft.Json;
using Sango.Render;
using Sango.Core.Action;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 逃出方式枚举
    /// </summary>
    public enum EscapeType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 逃跑
        /// </summary>
        Escape,
        /// <summary>
        /// 被释放
        /// </summary>
        Released,
        /// <summary>
        /// 部队灭亡
        /// </summary>
        TroopDestroyed
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Person : SangoObjectExtensionData
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Person; } }

        public string ColorName => $"<color=#7CCADB>{Name}</color>";

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty("BelongForce")]
        public int BelongForceId;
        public Force BelongForce { get; set; }

        public bool IsPlayer => BelongForce?.IsPlayer ?? false;
        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => BelongCorps?.IsPlayerControl ?? false;
        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => BelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// 所属军团
        /// </summary>
        [JsonProperty("BelongCorps")]
        public int BelongCorpsId;

        public Corps BelongCorps { get; set; }

        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonProperty("BelongCity")]
        public int BelongCityId;

        public City BelongCity { get; set; }

        /// <summary>
        /// 所在城池
        /// </summary>
        [JsonProperty("CurrentCity")]
        public int CurrentCityId;

        public City CurrentCity { get; set; }

        /// <summary>
        /// 所属部队
        /// </summary>
        [JsonProperty("BelongTroop")]
        public int BelongTroopId;

        public Troop mBelongTroop { get; set; }

        /// <summary>
        /// 姓
        /// </summary>
        public int familyNameID;
        [JsonProperty] public string familyName;

        /// <summary>
        /// 名
        /// </summary>
        public int giveNameID;
        [JsonProperty] public string giveName;

        /// <summary>
        /// 字
        /// </summary>
        public int nickNameID;
        [JsonProperty] public string nickName;

        /// <summary>
        /// 身平
        /// </summary>
        [JsonProperty] public string description;

        /// <summary>
        /// 头像id
        /// </summary>
        [JsonProperty] public int headIconID;

        /// <summary>
        /// 立绘id(弃用)
        /// </summary>
        [JsonProperty] public string imageID;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public string image;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public string image_old;

        /// <summary>
        /// 性别 0男,1女
        /// </summary>
        [JsonProperty] public int sex;

        /// <summary>
        /// 登场年份
        /// </summary>
        [JsonProperty] public int appearance;

        /// <summary>
        /// 出生地
        /// </summary>
        [JsonProperty] public int birthplace;

        /// <summary>
        /// 语气
        /// </summary>
        [JsonProperty] public int tone;

        /// <summary>
        /// 声音
        /// </summary>
        [JsonProperty] public int voice;

        /// <summary>
        /// 是否被发现
        /// </summary>
        public bool beFinded => !Invisible;

        /// <summary>
        /// 出生年
        /// </summary>
        [JsonProperty] public int yearBorn;

        /// <summary>
        /// 死亡年
        /// </summary>
        [JsonProperty] public int yearDead;

        /// <summary>
        /// 相性
        /// </summary>
        [JsonProperty] public int compatibility;

        /// <summary>
        /// 身分
        /// </summary>
        [JsonProperty]
#if SANGO_DEBUG

        public int state
        {
            get { return _state; }
            set
            {
                _state = value;
                //Sango.Log.Info($"{Name}改变状态=> {PersonSortFunction.SortByState.GetValueStr(this)}");
            }
        }
        private int _state;
#else
        public int state;
#endif

        /// <summary>
        /// 性格
        /// </summary>
        [JsonProperty]
        public int personality;

        public Personality mPersonality;

        /// <summary>
        /// 义理
        /// </summary>
        [JsonProperty]
        public int argumentation;

        public Argumentation mArgumentation;

        /// <summary>
        /// 官职（存档里只存 OfficialId 这个数值，加载时由 ResolveReferenceObjects 解析成对象引用）
        /// </summary>
        [JsonProperty("Official")]
        public int OfficialId;

        /// <summary>
        /// 官职对象（不参与序列化，见 OfficialId）
        /// </summary>
        public Official Official { get; set; }

        public bool CanUpgradeOfficial
        {
            get
            {
                if (Official == null)
                    return false;
                return Official.meritNeeds > 0 && merit >= Official.meritNeeds;
            }
        }

        /// <summary>
        /// 忠诚
        /// </summary>
        [JsonProperty] public int loyalty;

        /// <summary>
        /// 功绩
        /// </summary>
        [JsonProperty] public int merit;

        /// <summary>
        /// 体力
        /// </summary>
        [JsonProperty] public int stamina;

        /// <summary>
        /// 经验
        /// </summary>
        [JsonProperty] public int Exp { get; private set; }

        /// <summary>
        /// 等级（存档里只存 LevelId 这个数值，加载时由 ResolveReferenceObjects 解析成对象引用）
        /// </summary>
        [JsonProperty("Level")]
        public int LevelId;

        /// <summary>
        /// 等级对象（不参与序列化，见 LevelId）
        /// </summary>
        public PersonLevel Level { get; set; }

        /// <summary>
        /// 统御
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue command = new PersonAttributeValue();

        /// <summary>
        /// 武力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue strength = new PersonAttributeValue();

        /// <summary>
        /// 智力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue intelligence = new PersonAttributeValue();

        /// <summary>
        /// 政治
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue politics = new PersonAttributeValue();

        /// <summary>
        /// 魅力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue glamour = new PersonAttributeValue();

        /// <summary>
        /// 血缘
        /// </summary>
        [JsonProperty] public int consanguinity;

        /// <summary>
        /// 伤病
        /// </summary>
        [JsonProperty] public int injury;

        /// <summary>
        /// 汉室态度
        /// </summary>
        [JsonProperty]
        public int kanshitsu;

        /// <summary>
        /// 理想
        /// </summary>
        [JsonProperty]
        public int ideal;

        /// <summary>
        /// 才幹
        /// </summary>
        [JsonProperty]
        public int talent;

        /// <summary>
        /// 父亲
        /// </summary>
        [JsonProperty]
        public int Father;
        public Person mFather { get; set; }

        /// <summary>
        /// 母亲
        /// </summary>
        [JsonProperty]
        public int Mother;
        public Person mMother { get; set; }

        /// <summary>
        /// 配偶
        /// </summary>
        [JsonProperty]
        public int[] SpouseList;

        public SangoObjectList<Person> mSpouseList { get; private set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        [JsonProperty]
        public int Brother;

        public Person mBrother { get; set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        public List<Person> BrotherList;

        /// <summary>
        /// 喜欢武将
        /// </summary>
        [JsonProperty]
        public int[] LikePersonList;

        public SangoObjectList<Person> mLikePersonList;

        /// <summary>
        /// 厌恶武将
        /// </summary>
        [JsonProperty]
        public int[] HatePersonList;
        public SangoObjectList<Person> mHatePersonList;

        /// <summary>
        /// 儿子们, 由father属性添加至父亲的属性里
        /// </summary>
        public SangoObjectList<Person> sonList = new SangoObjectList<Person>();

        /// <summary>
        /// 矛
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue spearLv = new PersonAbilityValue();

        /// <summary>
        /// 戟
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue halberdLv = new PersonAbilityValue();

        /// <summary>
        /// 弓弩
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue crossbowLv = new PersonAbilityValue();

        /// <summary>
        /// 骑
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue rideLv = new PersonAbilityValue();

        /// <summary>
        /// 水军
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue waterLv = new PersonAbilityValue();

        /// <summary>
        /// 器械
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue machineLv = new PersonAbilityValue();

        /// <summary>
        /// 行动标记
        /// </summary>
        [JsonProperty] public BitCheck32 actionFlag = new BitCheck32();

        /// <summary>
        /// 武将特性
        /// </summary>
        [JsonProperty]
        public int[] FeatureList;

        public SangoObjectList<Feature> mFeatureList;

        /// <summary>
        /// 武将个人关系类特技装配的动作列表。
        /// </summary>
        public List<ActionBase> actionList;

        /// <summary>
        /// 库存
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(ItemStoreConverter))]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 装备的武器（存档里只存 EquippedWeaponId 这个数值，加载时由 ResolveReferenceObjects 解析）
        /// </summary>
        [JsonProperty("EquippedWeapon")]
        public int EquippedWeaponId;

        /// <summary>装备的武器对象（不参与序列化，见 EquippedWeaponId）</summary>
        public Equipment EquippedWeapon { get; set; }

        /// <summary>
        /// 装备的马（存档里只存 EquippedHorseId）
        /// </summary>
        [JsonProperty("EquippedHorse")]
        public int EquippedHorseId;

        /// <summary>装备的马对象（不参与序列化，见 EquippedHorseId）</summary>
        public Equipment EquippedHorse { get; set; }

        /// <summary>
        /// 装备的铠甲（存档里只存 EquippedArmorId）
        /// </summary>
        [JsonProperty("EquippedArmor")]
        public int EquippedArmorId;

        /// <summary>装备的铠甲对象（不参与序列化，见 EquippedArmorId）</summary>
        public Equipment EquippedArmor { get; set; }

        [JsonProperty]
        public int bannedForceId;

        /// <summary>
        /// 正在工作的建筑（存档里只存 workingBuildingId 这个数值）
        /// </summary>
        [JsonProperty("workingBuilding")]
        public int workingBuildingId;

        /// <summary>正在工作的建筑对象（不参与序列化，见 workingBuildingId）</summary>
        public Building workingBuilding;


        /// <summary>
        /// 舌战话术
        /// </summary>
        [JsonProperty]
        public int[] wordTac;

        /// <summary>
        /// 舌战得意话题
        /// </summary>
        [JsonProperty]
        public int wadai;

        // 个人逃跑概率
        public int escapeFactorWhenTroopDestroy = 0;

        public bool HasItem(int itemTypeId)
        {
            return itemStore.GetNumber(itemTypeId) > 0;
        }

        public bool IsLeader => state == (int)PersonStateType.Leader;
        public bool IsCommander => state == (int)PersonStateType.Commander;
        public bool IsGovernor => state == (int)PersonStateType.Governor;

        public void SetStateNormal() { state = (int)PersonStateType.Normal; }
        public void SetStateLeader()
        {
            if (IsGovernor) return;
            if (IsCommander) return;
            state = (int)PersonStateType.Leader;
        }
        public void SetStateCommander()
        {
            if (IsGovernor) return;
            state = (int)PersonStateType.Commander;
        }

        public override bool ActionOver
        {
            get => base.ActionOver;

            set
            {
                if (value == true)
                {
                    if (base.ActionOver != value)
                    {
                        GameEvent.OnPersonActionOver?.Invoke(this);
                    }
                }

                base.ActionOver = value;
            }
        }

        /// <summary>
        /// 枪兵适应
        /// </summary>
        public int SpearLv => spearLv.value;

        /// <summary>
        /// 盾兵适应
        /// </summary>
        public int HalberdLv => halberdLv.value;

        /// <summary>
        /// 弓兵适应
        /// </summary>
        public int CrossbowLv => crossbowLv.value;

        /// <summary>
        /// 骑兵适应
        /// </summary>
        public int RideLv => rideLv.value;

        /// <summary>
        /// 水军适应
        /// </summary>
        public int WaterLv => waterLv.value;

        /// <summary>
        /// 兵器适应
        /// </summary>
        public int MachineLv => machineLv.value;

        /// <summary>伤病等级上限（0=健康 / 1=轻伤 / 2=中伤 / 3=重伤，与 InjuryLevel.NearDeath 一致）</summary>
        public const int InjuryMaxLevel = 3;

        /// <summary>
        /// 各等级伤病的能力系数（百分点，下标 = 伤病等级）。
        ///
        /// 采用**三国志11 原版数值**：健康 100% / 轻伤 80% / 重伤 50% / 濒危 30%
        /// （即 轻伤 -20%、重伤 -50%、濒危 -70%）。
        /// 本项目四档的命名是 健康 / 轻伤 / 中伤 / 重伤，按**档位序号**与 11 的四档一一对应
        /// （第 3 档就是 InjuryLevel.NearDeath，即"濒死/濒危"）。
        /// </summary>
        public static readonly int[] InjuryFactorPercent = { 100, 80, 50, 30 };

        /// <summary>
        /// 按伤病折算能力值。
        ///
        /// 五维（统率 / 武力 / 智力 / 政治 / 魅力）的 getter 都过这一道，
        /// 所以**外部取到的就是带伤之后的最终值**——部队攻防、单挑、舌战、内政判定自动口径一致；
        /// 原始数据（baseValue / _value）不动，武将编辑界面看到的仍是底子。
        ///
        /// 【下限 1】折减是整除截断（例如智力 3 在濒危时 3*30/100 = 0），而能力值会被当作除数/乘数
        /// 参与各种公式（如战法成功率 V2 的分母 A智²+B智²），0 会直接除零、也会让其它公式失真，
        /// 所以这里兜住下限：折减后的能力永远 ≥ 1。
        /// </summary>
        public static int ApplyInjuryDecay(int value, int injury)
        {
            int level;
            if (injury <= 0) level = 0;
            else if (injury > InjuryMaxLevel) level = InjuryMaxLevel;
            else level = injury;

            int decayed = value * InjuryFactorPercent[level] / 100;
            return decayed < 1 ? 1 : decayed;
        }

        /// <summary>统率（带伤时按伤病衰减）</summary>
        public int Command => ApplyInjuryDecay(RawCommand, injury);

        /// <summary>武力（带伤时按伤病衰减）</summary>
        public int Strength => ApplyInjuryDecay(RawStrength, injury);

        /// <summary>智力（带伤时按伤病衰减）</summary>
        public int Intelligence => ApplyInjuryDecay(RawIntelligence, injury);

        /// <summary>政治（带伤时按伤病衰减）</summary>
        public int Politics => ApplyInjuryDecay(RawPolitics, injury);

        /// <summary>魅力（带伤时按伤病衰减）</summary>
        public int Glamour => ApplyInjuryDecay(RawGlamour, injury);

        /// <summary>统率（不含伤病折减；单挑等需要自己按"当事人当时的伤病"折算的地方用）</summary>
        public int RawCommand => command.Value + GetEquipmentBonus(x => x.commandBonus);

        /// <summary>武力（不含伤病折减）</summary>
        public int RawStrength => strength.Value + GetEquipmentBonus(x => x.strengthBonus);

        /// <summary>智力（不含伤病折减）</summary>
        public int RawIntelligence => intelligence.Value + GetEquipmentBonus(x => x.intelligenceBonus);

        /// <summary>政治（不含伤病折减）</summary>
        public int RawPolitics => politics.Value + GetEquipmentBonus(x => x.politicsBonus);

        /// <summary>魅力（不含伤病折减）</summary>
        public int RawGlamour => glamour.Value + GetEquipmentBonus(x => x.glamourBonus);

        /// <summary>
        /// 是否可登场
        /// </summary>
        public virtual bool IsValid => state > 0 && state != (int)PersonStateType.Invalid && state != (int)PersonStateType.Dead;

        /// <summary>
        /// 兵力上限其他更改值(道具等加持)
        /// </summary>
        public int troopsLimitExtra = 0;

        /// <summary>
        /// 带兵上限,根据官职和国家科技决定ui 
        /// </summary>
        public int TroopsLimit
        {
            //TODO: 增加国家科技加持
            get { return Math.Max(IsGovernor ? 15000 : 0, Official.troopsLimit) + Level.troops + troopsLimitExtra; }
        }

        /// <summary>
        /// 军事能力
        /// </summary>
        public int MilitaryAbility
        {
            get { return Command * 2 + Math.Max(Strength, Intelligence) * 3; }
        }

        /// <summary>
        /// 商业能力
        /// </summary>
        public int BaseCommerceAbility => Intelligence;

        /// <summary>
        /// 巡视能力
        /// </summary>
        public int BaseSecurityAbility => Command;

        /// <summary>
        /// 训练能力
        /// </summary>
        public int BaseTrainTroopAbility => Strength;

        /// <summary>
        /// 农业能力
        /// </summary>
        public int BaseAgricultureAbility => Politics;

        /// <summary>
        /// 建设能力
        /// </summary>
        public int BaseBuildAbility => Politics;

        /// <summary>
        /// 生产能力
        /// </summary>
        public int BaseCreativeAbility => Intelligence;

        /// <summary>
        /// 搜寻能力
        /// </summary>
        public int BaseSearchingAbility
        {
            get
            {
                return (Politics + Glamour) / 2;
            }
        }

        /// <summary>
        /// 招募能力
        /// </summary>
        public int BaseRecruitmentAbility => Glamour;


        public void OnPersonAgeUpdate(Scenario scenario)
        {
            Age = scenario.Info.year - yearBorn;
            if (scenario.Variables.AgeEnabled && scenario.Variables.EnableAgeAbilityFactor)
            {
                command.Update(Age, scenario); strength.Update(Age, scenario); intelligence.Update(Age, scenario); politics.Update(Age, scenario); glamour.Update(Age, scenario);
                //spearLv.Update(); halberdLv.Update(); crossbowLv.Update(); horseLv.Update(); waterLv.Update(); machineLv.Update();
            }
        }

        public ushort skill;

        [JsonProperty] public int missionType;
        [JsonProperty] public int missionTarget;
        [JsonProperty] public int missionCounter;
        [JsonProperty] public int missionParams1;
        [JsonProperty] public int missionParams2;
        [JsonProperty] public int missionParams3;
        [JsonProperty] public int missionParams4;

        /// <summary>
        /// 在当前城市的停留回合数
        /// </summary>
        [JsonProperty] public int stayTurnCount;

        /// <summary>
        /// 在野回合数
        /// </summary>
        [JsonProperty] public int wildTurnCount;

        public bool rewardOver;

        public int Age { get; private set; }

        /// <summary>
        /// 是否空闲
        /// </summary>
        public bool IsFree { get { return mBelongTroop == null && missionType == (int)MissionType.None && !IsPrisoner && !IsDead; } }

        /// <summary>
        /// 是否在野
        /// </summary>
        public bool IsWild { get { return state == (int)PersonStateType.Unemployed; } }

        /// <summary>
        /// 是否为俘虏
        /// </summary>
        public bool IsPrisoner { get { return state == (int)PersonStateType.Prisoner; } }

        /// <summary>
        /// 是否未发现
        /// </summary>
        public bool Invisible { get { return state == (int)PersonStateType.Invisible; } }

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead { get { return state == (int)PersonStateType.Dead; } }

        public bool IsAlliance(BuildingBase other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Person other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        /// <summary>
        /// 所有的武将情况归属,全由武将决定,城池不再记录任何武将归属情况
        /// </summary>
        /// <param name="scenario"></param>
        /// <summary>
        /// 解析本武将身上"只存 id"的对象引用：官职 / 等级 / 三件装备 / 工作建筑。
        ///
        /// 这些字段以前是靠 Id2ObjConverter 在全局 OnScenarioPrepare 事件里延迟回填的。
        /// 现在改成显式的 int 字段 + 这里解析：存档里就是纯数值，加载顺序也不再依赖事件时序。
        /// 剧本加载（<see cref="OnScenarioPrepare"/>）与基础武将编辑器（ScenarioMaker）都要调用本方法。
        /// </summary>
        public void ResolveReferenceObjects(Scenario scenario)
        {
            if (scenario == null) return;       // 没有剧本上下文时保持现有引用不动

            Official = IdToObject<Official>(scenario, OfficialId);
            Level = IdToObject<PersonLevel>(scenario, LevelId);
            workingBuilding = IdToObject<Building>(scenario, workingBuildingId);

            // 装备存放在道具库(ItemTypes)里、Equipment 是 ItemType 的子类，所以按 ItemType 查再转回 Equipment。
            // 旧写法把 typeof(Equipment) 交给 Scenario.GetObject，而分派表里只有 ItemType 分支，
            // 结果装备每次加载都是 null —— 这里一并修正。
            EquippedWeapon = IdToObject<ItemType>(scenario, EquippedWeaponId) as Equipment;
            EquippedHorse = IdToObject<ItemType>(scenario, EquippedHorseId) as Equipment;
            EquippedArmor = IdToObject<ItemType>(scenario, EquippedArmorId) as Equipment;
        }

        /// <summary>id → 对象；id &lt;= 0 表示没有，返回 null（与 Id2ObjConverter.Id2Object 的口径一致）</summary>
        static T IdToObject<T>(Scenario scenario, int id) where T : SangoObject, new()
        {
            if (scenario == null || id <= 0) return null;
            return scenario.GetObject<T>(id);
        }

        public override void OnScenarioPrepare(Scenario scenario)
        {
            ResolveReferenceObjects(scenario);      // 官职/等级/装备/工作建筑：只存 id，在这里解析
            BelongForce = scenario.Id2Object(scenario.forceSet, BelongForceId);
            BelongCorps = scenario.Id2Object(scenario.corpsSet, BelongCorpsId);
            BelongCity = scenario.Id2Object(scenario.citySet, BelongCityId);
            CurrentCity = scenario.Id2Object(scenario.citySet, CurrentCityId);
            if (CurrentCity == null && BelongCity != null)
                CurrentCity = BelongCity;
            mBelongTroop = scenario.Id2Object(scenario.troopsSet, BelongTroopId);

            if (personality <= 0)
                personality = 1;
            mPersonality = scenario.Id2Object(scenario.CommonData.Personalities, personality);

            if (argumentation <= 0)
                argumentation = 2;
            mArgumentation = scenario.Id2Object(scenario.CommonData.Argumentations, argumentation);

            //if (attributeChangeType <= 0)
            //    attributeChangeType = 5;
            //mAttributeChangeType = scenario.Id2Object(scenario.CommonData.AttributeChangeTypes, attributeChangeType);

            mFather = scenario.Id2Object(scenario.personSet, Father);
            mMother = scenario.Id2Object(scenario.personSet, Mother);
            mBrother = scenario.Id2Object(scenario.personSet, Brother);

            mSpouseList = scenario.Array2ObjectList(scenario.personSet, SpouseList);
            mLikePersonList = scenario.Array2ObjectList(scenario.personSet, LikePersonList);
            mHatePersonList = scenario.Array2ObjectList(scenario.personSet, HatePersonList);
            mFeatureList = scenario.Array2ObjectList(scenario.CommonData.Features, FeatureList);
            // 特技对象已完成解析后再装配个人关系 Action，确保事件订阅只依赖有效配置。
            InitPersonActions();

            if (!scenario.Variables.AgeEnabled || !scenario.Variables.EnableAgeAbilityFactor)
            {
                command.UpdateNoAge();
                strength.UpdateNoAge();
                intelligence.UpdateNoAge();
                politics.UpdateNoAge();
                glamour.UpdateNoAge();
            }

            // 处理义兄弟
            if (mBrother != null)
            {
                if (mBrother.BrotherList == null)
                    mBrother.BrotherList = new List<Person>();

                mBrother.BrotherList.Add(this);
            }

            if (IsAlive)
            {
                switch ((PersonStateType)state)
                {
                    // 处理主公
                    case PersonStateType.Governor:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            BelongCity.NeedUpdateLeader();
                            CheckBelongConsistency();
                        }
                        break;
                    // 军团长
                    case PersonStateType.Commander:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            BelongCity.NeedUpdateLeader();
                            CheckBelongConsistency();
                        }
                        break;
                    // 太守
                    case PersonStateType.Leader:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            BelongCity.NeedUpdateLeader();
                            CheckBelongConsistency();
                        }
                        break;
                    // 一般武将
                    case PersonStateType.Normal:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            CheckBelongConsistency();
                        }
                        break;
                    // 在野
                    case PersonStateType.Unemployed:
                        CurrentCity.wildPersons.Add(this);
                        break;
                    // 囚犯
                    case PersonStateType.Prisoner:
                        if (mBelongTroop != null)
                        {
                            if (BelongForce != null)
                                BelongForce.BeCaptiveList.Add(this);
                            mBelongTroop.captiveList.Add(this);
                        }
                        else
                        {
                            // 修复一下
                            if (CurrentCity.IsSameForce(this))
                            {
                                BelongCity = CurrentCity;
                                if (BelongForce != null)
                                {
                                    state = (int)PersonStateType.Normal;
                                    BelongCity.allPersons.Add(this);
                                    BelongCity.freePersons.Add(this);
                                }
                                else
                                {
                                    state = (int)PersonStateType.Unemployed;
                                    BelongCity.wildPersons.Add(this);
                                }
                            }
                            else
                            {
                                if (BelongForce != null)
                                    BelongForce.BeCaptiveList.Add(this);
                                CurrentCity.captiveList.Add(this);
                            }
                        }
                        break;
                    // 未登场
                    case PersonStateType.Invalid:
                        break;
                    // 未发现
                    case PersonStateType.Invisible:
                        if (CurrentCity != null)
                            CurrentCity.invisiblePersons.Add(this);
                        else if (BelongCity != null)
                        {
                            CurrentCity = BelongCity;
                            BelongCity.invisiblePersons.Add(this);
                        }
                        break;
                    // 死亡
                    case PersonStateType.Dead:
                        break;
                }
            }

            // 处理父亲
            if (mFather != null)
                mFather.sonList.Add(this);

            if (mMother != null)
                mMother.sonList.Add(this);

            OnPersonAgeUpdate(scenario);

            spearLv.Update();
            halberdLv.Update();
            crossbowLv.Update();
            rideLv.Update();
            waterLv.Update();
            machineLv.Update();

            if (Official == null)
                Official = scenario.CommonData.Officials[0];

            Official.OnPersonAdd(this);

            if (Level == null)
                Level = scenario.CommonData.PersonLevels[0];
        }

        /// <summary>
        /// 校验并修正武将的势力/军团与所属城市之间的归属一致性。
        /// 规则2:隶属势力的武将(主公/都督/太守/一般),其所属军团必须是所属势力下的军团,
        /// 并且必须同时拥有隶属城市与所在城市。
        /// 存档中一旦出现"武将归属 A 势力,却待在 B 势力的城池、或顶着 B 势力的军团"的情况,
        /// 这里统一以所属城市的归属为准纠正,并输出错误日志便于定位数据错乱的来源。
        /// </summary>
        private void CheckBelongConsistency()
        {
            // 没有隶属城市时无从比对,直接跳过
            if (BelongCity == null)
                return;

            // 势力与军团都与所属城市一致,属于正常情况
            if (BelongForce == BelongCity.BelongForce && BelongCorps == BelongCity.BelongCorps)
                return;

            Sango.Log.Error($"[{Id}]{Name}归属force:{BelongForce?.Name} corps:{BelongCorps?.Name}, 但在city[{BelongCity?.Name}] force:{BelongCity.BelongForce?.Name} corps:{BelongCity.BelongCorps?.Name}");
            BelongForce = BelongCity.BelongForce;
            BelongCorps = BelongCity.BelongCorps;
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            BelongForceId = BelongForce?.Id ?? 0;
            BelongCorpsId = BelongCorps?.Id ?? 0;
            BelongCityId = BelongCity?.Id ?? 0;
            CurrentCityId = CurrentCity?.Id ?? 0;
            BelongTroopId = mBelongTroop?.Id ?? 0;
            personality = mPersonality?.Id ?? 0;
            argumentation = mArgumentation?.Id ?? 0;

            // 只存 id 的对象引用：把当前对象回写成数值（原来是 Id2ObjConverter 在写 JSON 时自动完成）
            OfficialId = Official?.Id ?? 0;
            LevelId = Level?.Id ?? 0;
            EquippedWeaponId = EquippedWeapon?.Id ?? 0;
            EquippedHorseId = EquippedHorse?.Id ?? 0;
            EquippedArmorId = EquippedArmor?.Id ?? 0;
            workingBuildingId = workingBuilding?.Id ?? 0;

            Father = mFather?.Id ?? 0;
            Mother = mMother?.Id ?? 0;
            Brother = mBrother?.Id ?? 0;

            SpouseList = mSpouseList?.ToArray() ?? null;
            LikePersonList = mLikePersonList?.ToArray() ?? null;
            HatePersonList = mHatePersonList?.ToArray() ?? null;
            FeatureList = mFeatureList?.ToArray() ?? null;
        }

        public override void Init(Scenario scenario)
        {
            base.Init(scenario);

            if (mBrother != null)
            {
                if (mBrother == this)
                {
                    BrotherList.Sort(SangoObject.Compare);

                }
                else
                {
                    BrotherList = mBrother.BrotherList;
                }
            }

            //if (IsPrisoner && mBelongTroop == null)
            //{
            //    if (CurrentCity.IsSameForce(this))
            //    {
            //        if (BelongForce != null)
            //        {
            //            BelongForce.BeCaptiveList.Remove(this);
            //            state = (int)PersonStateType.Normal;
            //            CurrentCity.allPersons.Remove(this);
            //            CurrentCity.allPersons.Add(this);
            //            CurrentCity.freePersons.Remove(this);
            //            CurrentCity.freePersons.Add(this);
            //        }
            //        else
            //        {
            //            state = (int)PersonStateType.Unemployed;
            //            CurrentCity.wildPersons.Remove(this);
            //            CurrentCity.wildPersons.Add(this);
            //        }
            //        CurrentCity.captiveList.Remove(this);
            //    }
            //}
        }

        public override bool OnYearStart(Scenario scenario)
        {
            if (IsDead) return true;

            OnPersonAgeUpdate(scenario);

            if (state == (int)PersonStateType.Invalid)
            {
                if (scenario.Variables.allowInvalidPersonValidWhenYearPass)
                {
                    //出场年
                    if (appearance > 0 && appearance <= scenario.Info.year)
                    {
                        state = (int)PersonStateType.Invisible;

                        City city = null;
                        if (birthplace > 0)
                        {
                            Province prov = scenario.CommonData.Provinces[birthplace];
                            city = prov.RandomBelongCity(scenario);
                        }

                        if (city == null)
                            city = scenario.citySet.RandomGet();

                        // 这里要处理登场城池
                        city.invisiblePersons.Add(this);
                        CurrentCity = city;
                    }
                }
            }
            else
            {
                if (IsWild && sonList != null)
                {
                    sonList.ForEach(x =>
                    {
                        if (x.state == (int)PersonStateType.Invalid)
                        {
                            if (x.Age >= 16)
                            {
                                x.CurrentCity = CurrentCity;
                                x.state = (int)PersonStateType.Invisible;
                                CurrentCity.invisiblePersons.Add(this);
                            }
                        }
                    });
                }
            }

            return base.OnYearStart(scenario);
        }

        public bool DoMove(City dest, Scenario scenario)
        {
            City target = dest.BelongCity == null ? dest : dest.BelongCity;
            City currentCity = CurrentCity.BelongCity == null ? CurrentCity : CurrentCity.BelongCity;

            if (target == currentCity)
            {
                return true;
            }

            // 找到最短移动路径
            List<City> path = scenario.FindShortestPath(currentCity, target);
            if (path == null || path.Count <= 1)
            {
                return true;
            }

            City next = path[1];
            ChangeCurrentCity(next);
            if (next == dest)
            {
                return true;
            }
            return false;
        }

        public void UpdateMission(Scenario scenario)
        {
            if (missionType == 0) return;

            switch (missionType)
            {
                case (int)MissionType.PersonReturn:
                    {
                        City dest = scenario.citySet.Get(missionTarget);
                        if (!this.IsSameForce(dest))
                        {
                            if (BelongForce != null)
                            {
                                SetMission(MissionType.PersonReturn, BelongCity);
                            }
                            else
                            {
                                ClearMission();
                            }
                            return;
                        }

                        if (DoMove(dest, scenario))
                        {
                            ClearMission();
                            dest.OnPersonReturnCity(this);
                        }
                    }
                    break;
                case (int)MissionType.PersonRecruitPerson:
                    {
                        Person dest_person = scenario.personSet.Get(missionTarget);
                        City dest = scenario.citySet.Get(missionParams1);
                        if (BelongCorps != null && this.IsSameForce(dest_person))
                        {
                            // 已经有人招募成功
                            SetMission(MissionType.PersonReturn, BelongCity);
                            return;
                        }

                        if (DoMove(dest, scenario))
                        {
                            ClearMission();
                            CityRecruitPersonEvent te = RenderEvent.Instance.Create<CityRecruitPersonEvent>();
                            te.Init(this, dest_person);
                            RenderEvent.Instance.Add(te);
                            SetMission(MissionType.PersonReturn, BelongCity);
                        }
                    }
                    break;
                case (int)MissionType.PersonCreateBoat:
                    {
                        missionCounter--;
                        if (missionCounter <= 0)
                        {
                            int buildingId = missionParams1;
                            int totalValue = missionParams2;
                            ItemType itemType = scenario.GetObject<ItemType>(missionTarget);
                            BelongCity.DoJobCreateBoat(itemType, buildingId, totalValue);
                        }
                    }
                    break;
                case (int)MissionType.PersonCreateMachine:
                    {
                        missionCounter--;
                        if (missionCounter <= 0)
                        {
                            int buildingId = missionParams1;
                            int totalValue = missionParams2;
                            ItemType itemType = scenario.GetObject<ItemType>(missionTarget);
                            BelongCity.DoJobCreateMachine(itemType, buildingId, totalValue);
                        }
                    }
                    break;
                case (int)MissionType.PersonResearch:
                    {
                        missionCounter--;
                        if (missionCounter <= 0)
                        {
                            ClearMission();
                        }
                    }
                    break;
                case (int)MissionType.PersonBuild:
                    {
                        Building target = scenario.GetObject<Building>(missionTarget);
                        if (target == null || !target.IsAlive || !target.IsSameForce(this) || (target.isComplate && !target.isUpgrading))
                        {
                            ClearMission();
                        }
                    }
                    break;
                case (int)MissionType.PersonDiplomacy:
                    {
                        City targetCity = scenario.citySet.Get(missionTarget);
                        if (DoMove(targetCity, scenario))
                        {
                            // 执行外交行动
                            Force receiverForce = scenario.forceSet.Get(missionParams1);
                            if (receiverForce == null || !receiverForce.IsAlive || receiverForce.CapitalCity != targetCity)
                            {
                                // 完成任务，返回原城市
                                SetMission(MissionType.PersonReturn, BelongCity);
                                return;
                            }
                            DiplomacyActionType actionType = (DiplomacyActionType)missionParams2;
                            if (this.IsPlayer || receiverForce.IsPlayer)
                            {
                                Sango.Render.DiplomacyEvent diplomacyEvent = RenderEvent.Instance.Create<Sango.Render.DiplomacyEvent>();
                                diplomacyEvent.Init(this, actionType, receiverForce, targetCity, missionParams3, missionParams4);
                                RenderEvent.Instance.Add(diplomacyEvent);
                            }
                            else
                            {
                                GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(this, actionType, receiverForce, missionParams3);
                            }

                            // 完成任务，返回原城市
                            SetMission(MissionType.PersonReturn, BelongCity);
                        }
                    }
                    break;
            }
        }
        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3, int p4)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = p3;
            this.missionParams4 = p4;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = p3;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = 0;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void ClearMission()
        {
            this.missionType = 0;
            this.missionTarget = 0;
            this.missionCounter = 0;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            // 伤病恢复：每回合（旬）结算一次，据点内休养才会好转（规则见 PersonInjury）
            PersonInjury.TryRecover(this);

            if (state == (int)PersonStateType.Invalid)
            {
                if (scenario.Variables.allowInvalidPersonValidWhenYearPass)
                {
                    //出场年
                    if (appearance > 0 && appearance <= scenario.Info.year)
                    {
                        Person belongP = null;
                        if (mFather != null)
                            belongP = mFather;
                        else if (mMother != null)
                            belongP = mMother;

                        if (belongP != null)
                        {
                            if (belongP.BelongForce != null)
                            {
                                BelongForce = belongP.BelongForce;
                                BelongCorps = BelongForce.CapitalCorps;
                                BelongCity = BelongForce.CapitalCity;
                                CurrentCity = BelongCity;
                                BelongCity.allPersons.Add(this);
                                BelongCity.freePersons.Add(this);
                                state = (int)PersonStateType.Normal;
                                loyalty = 100;
                                if (IsPlayer)
                                {
                                    RenderEvent.Instance.Add(new PersonGrowupEvent()
                                    {
                                        father = belongP,
                                        person = this
                                    });
                                }
                                return base.OnTurnStart(scenario);
                            }
                        }

                        if (GameRandom.Chance(10))
                        {

                            state = (int)PersonStateType.Invisible;

                            City city = null;
                            if (birthplace > 0)
                            {
                                Province prov = scenario.CommonData.Provinces[birthplace];
                                city = prov.RandomBelongCity(scenario);
                            }

                            if (city == null)
                                city = scenario.citySet.RandomGet();

                            // 这里要处理登场城池
                            city.invisiblePersons.Add(this);
                            CurrentCity = city;

                            RenderEvent.Instance.Add(new PersonValidEvent()
                            {
                                province = city.province,
                                person = this
                            });
                        }
                    }
                }
            }
            return base.OnTurnStart(scenario);
        }
        public override bool OnForceTurnStart(Scenario scenario)
        {
            if (BelongForce != null && IsAlive)
            {
                BelongForce.GainHegemonyPoint(1);
            }

            // 这里肯定有势力
            if (sonList != null)
            {
                sonList.ForEach(x =>
                {
                    if (x.state == (int)PersonStateType.Invalid)
                    {
                        if (x.Age >= 16)
                        {
                            x.BelongForce = BelongForce;
                            x.BelongCorps = BelongCorps;

                            City becameCity = BelongCity;
                            if (IsPrisoner)
                            {
                                becameCity = BelongForce.CapitalCity;
                            }
                            x.BelongCity = becameCity;
                            x.CurrentCity = becameCity;
                            becameCity.allPersons.Add(x);
                            becameCity.freePersons.Add(x);
                            x.state = (int)PersonStateType.Normal;
                            x.loyalty = 100;

                            if (IsPlayer)
                            {
                                RenderEvent.Instance.Add(new PersonGrowupEvent()
                                {
                                    father = this,
                                    person = x
                                });
                            }
                        }
                    }
                });
            }

            ActionOver = !IsFree;
            return base.OnForceTurnStart(scenario);
        }

        public override bool OnForceTurnEnd(Scenario scenario)
        {
            return base.OnForceTurnEnd(scenario);
        }

        public override bool OnTurnEnd(Scenario scenario)
        {
            // 在野武将移动逻辑
            if (IsWild || state == (int)PersonStateType.Invisible)
            {
                wildTurnCount++;
                stayTurnCount++;
                if (stayTurnCount > 5 && GameRandom.Chance(5)) // 10%概率
                {
                    if (IsWild)
                        CurrentCity.wildPersons.Remove(this);
                    else
                        CurrentCity.invisiblePersons.Remove(this);

                    //如果在港关,移动到所属城市
                    if (!CurrentCity.IsCity())
                    {
                        City targetCity = CurrentCity.BelongCity;
                        CurrentCity.RemoveWildPerson(this);
                        // 移动到新城市
                        ChangeCurrentCity(targetCity);
                        BelongCity = targetCity;

                        // 重置停留时间
                        stayTurnCount = 0;
                        Sango.Log.Info($"@人才@在野武将{Name}从{BelongCity.Name}移动到{targetCity.Name}");
                    }
                    else
                    {
                        // 随机选择一个邻接城市
                        SangoObjectList<City> neighborCities = BelongCity.NeighborList;
                        if (neighborCities.Count > 0)
                        {
                            int randomIndex = GameRandom.Range(neighborCities.Count);
                            City targetCity = neighborCities[randomIndex];
                            if (targetCity != null)
                            {
                                // 移动到新城市
                                ChangeCurrentCity(targetCity);
                                BelongCity = targetCity;

                                // 重置停留时间
                                stayTurnCount = 0;
                        Sango.Log.Info($"@人才@在野武将{Name}从{BelongCity.Name}移动到{targetCity.Name}");
                            }
                        }
                    }

                    if (IsWild)
                        CurrentCity.wildPersons.Add(this);
                    else
                        CurrentCity.invisiblePersons.Add(this);
                }
            }
            else
            {
                wildTurnCount = 0;
            }

            UpdateMission(scenario);
            return base.OnTurnEnd(scenario);
        }

        public void OnWillBeCaptive()
        {
            // 军师被捕
            if (BelongForce != null)
            {
                if (BelongForce.mCounsellor == this)
                {
                    BelongForce.mCounsellor = null;
                }
            }

            if (IsGovernor)
            {

            }
            else if (IsCommander)
            {
                // 都督被俘:让出都督职位并重新推举
                if (BelongCorps != null)
                {
                    BelongCorps.mComander = null;
                    BelongCorps.NeedUpdateCommander();
                }
                // 都督不一定兼任太守,只有本人确实是该城太守时才需要让位
                if (BelongCity != null && BelongCity.Leader == this)
                {
                    BelongCity.Leader = null;
                    BelongCity.NeedUpdateLeader();
                }
            }
            else if (IsLeader)
            {
                // 太守被俘:让出太守职位并重新推举
                if (BelongCity != null)
                {
                    BelongCity.Leader = null;
                    BelongCity.NeedUpdateLeader();
                }
            }

        }

        public void OnWillChangeToCity(City dest)
        {
            // 如果转移主公到其他军团城市,需要解散目标军团
            if (IsGovernor && dest.BelongCorps != BelongCorps)
            {
                Corps corps = dest.BelongCorps;
                dest.ChangeCorps(BelongCorps);
                dest.UpdateCorps();
                dest.Render?.UpdateRender();
                corps.RemoveCity(dest);
                BelongCity.NeedUpdateLeader();
                dest.NeedUpdateLeader();
            }
            else if (IsCommander)
            {
                if (dest.BelongCorps != BelongCorps)
                {
                    SetStateNormal();
                    BelongCorps.NeedUpdateCommander();
                }
                else
                {
                    dest.NeedUpdateLeader();
                }
                BelongCity.NeedUpdateLeader();
            }
            else if (IsLeader)
            {
                SetStateNormal();
                BelongCity.NeedUpdateLeader();
            }
        }

        public void TransformToCity(City dest)
        {
            OnWillChangeToCity(dest);

            City lastCity = BelongCity;
            ChangeBelongCity(dest);
            //dest.AddPerson(this);
            SetMission(MissionType.PersonReturn, dest);

            ActionOver = true;
            Sango.Log.Info($"*{BelongForce?.Name}的{Name}从{BelongCity.Name}向{dest.Name}转移*");
        }

        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;
            if (BelongCorps != corps)
            {
                last = BelongCorps;
                BelongCorps = corps;
                // 目标军团可能为空(例如城市处于无归属状态),此时只清空军团,势力交由调用方处理,
                // 直接取 corps.BelongForce 会触发空引用
                if (corps != null && BelongForce != corps.BelongForce)
                {
                    BelongForce = corps.BelongForce;
                }
            }
            return last;
        }

        /// <summary>
        /// 改变所在城市
        /// </summary>
        /// <param name="city"></param>
        /// <returns></returns>
        public City ChangeCurrentCity(City city)
        {
            City last = CurrentCity;
            CurrentCity = city;
            Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所在城市 {last.Name} -> {city.Name}");
            GameEvent.OnPersonChangCurrentCity?.Invoke(this, city, last);
            return last;
        }

        /// <summary>
        /// 改变所属城市
        /// </summary>
        /// <param name="city"></param>
        /// <returns></returns>
        public City ChangeBelongCity(City city)
        {
            City last = null;
            if (BelongCity != city)
            {
                last = BelongCity;
                Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所属城市 {BelongCity?.Name} => {city.Name}");
                if (!IsWild)
                {
                    BelongCity?.RemovePerson(this);
                    city.AddPerson(this);
                    BelongCity = city;
                    if (BelongCorps != city.BelongCorps)
                        BelongCorps = city.BelongCorps;
                    if (BelongForce != city.BelongForce)
                        BelongForce = city.BelongForce;
                }
                else
                {
                    // 在野武将只登记在野名单,不继承目标城市的势力与军团。
                    // 规则1:在野武将必须是在野状态、有所在城市、且不能有部队,
                    // 因此这里显式清空势力/军团,避免"在野却仍隶属某势力"的残留
                    BelongCity?.RemoveWildPerson(this);
                    city.AddWildPerson(this);
                    BelongCity = city;
                    BelongCorps = null;
                    BelongForce = null;
                }

                mBelongTroop?.OnPersonChangeCity(this, last, city);
            }
            return last;
        }


        public bool JobRecruitPerson(Person person, City targetCity, int type)
        {
            int probability = GameFormula.Instance.RecruitPersonProbability(this, person, type);

            // 【性格】招募武将效率：按执行武将的性格折算成功率
            if (mPersonality != null && mPersonality.domesticRecruitPersonScale > 0
                && mPersonality.domesticRecruitPersonScale != 100)
            {
                long scaled = (long)probability * mPersonality.domesticRecruitPersonScale / 100;
                probability = scaled > 100 ? 100 : (int)scaled;
            }
            Sango.Log.Info($"[{BelongForce.Name}]<{Name}>登庸 -> {person.Name} 成功率:{probability}");
            //TODO: 招募成功概率计算
            bool success = GameRandom.Chance(probability);
            if (success)
            {
                person.BeRecruit(this, targetCity);
            }
            else
            {
                // 登用失败：按概率强制进入舌战（概率见剧本参数 debateChanceWhenRecruitFail）。
                // 只有目标是在野武将 / 没有势力的俘虏才会触发；辩胜时由 DebateConsequence 改判为登用成功，
                // 所以要把"登用成功后目标加入的城"一并传过去。
                Sango.Core.Debate.DebateTrigger.OnRecruitFailed(this, person, targetCity);
            }
            ScenarioVariables variables = Scenario.Cur.Variables;
            int jobId = (int)CityJobType.RecruitPerson;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);
            merit += meritGain;
            GainJobAttributeExp(jobId);                     // 登用 → 魅力经验
            BelongForce?.GainTechniquePoint(techniquePointGain);
            ActionOver = true;
            return success;
        }

        public bool JobRecruitPerson(Person person, int type)
        {
            return JobRecruitPerson(person, BelongCity, type);
        }

        public void BeRecruit(Person person, City targetCity)
        {
            Sango.Log.Info($"[{person.BelongForce.Name}]<{person.Name}>登庸成功, {Name}加入了势力{person.BelongForce.Name}");
            loyalty = 80;
            if (IsPrisoner)
            {
                BelongForce?.BeCaptiveList.Remove(this);
                // 囚犯从监牢中移除
                if (mBelongTroop != null)
                    mBelongTroop.RemoveCaptive(this);
                else
                    CurrentCity.RemoveCaptive(this);
                state = (int)PersonStateType.Normal;
                JoinToForce(targetCity);
                SetMission(MissionType.PersonReturn, targetCity);
            }
            else
            {
                if (IsWild)
                    CurrentCity.RemoveWildPerson(this);
                else if (Invisible)
                    CurrentCity.RemoveInvisiblePerson(this);
                else
                    BelongCity?.RemovePerson(this);

                // 部队中
                if (mBelongTroop != null)
                {
                    Troop troop = mBelongTroop;
                    // 部队主将
                    if (this == mBelongTroop.Leader)
                    {
                        mBelongTroop.JoinToForce(targetCity);
                        mBelongTroop.ActionOver = true;
                    }
                    else
                    {
                        mBelongTroop.RemovePerson(this);
                        ChangeCurrentCity(troop.CurrentCity);
                        JoinToForce(targetCity);
                        SetMission(MissionType.PersonReturn, targetCity);
                        troop.ResetActionAndStatus();
                    }
                    troop.Render?.UpdateRender();
                }
                else
                {
                    // 有归属
                    JoinToForce(targetCity);
                    SetMission(MissionType.PersonReturn, targetCity);
                }
            }
            ActionOver = true;
        }

        /// <summary>
        /// 加入某个势力,需要指定一个城市
        /// </summary>
        /// <param name="city"></param>
        public bool JoinToForce(City city)
        {
            bool isSameCity = CurrentCity == city;
            BelongCity = city;
            BelongCorps = city.BelongCorps;
            BelongForce = city.BelongForce;
            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;
            // 主公不能被降级为一般武将,否则会出现"势力主公却是普通状态"的不一致
            if (!IsGovernor)
            {
                state = (int)PersonStateType.Normal;
            }
            // 避免重复入城:调用方可能已经把武将加进过该城的人员名单
            if (!BelongCity.allPersons.Contains(this))
            {
                BelongCity.AddPerson(this);
            }
            return isSameCity;
        }

        /// <summary>
        /// 下野
        /// </summary>
        public void LeaveToWild()
        {
            if (IsCommander)
            {
                BelongCorps.mComander = null;
                BelongCorps.NeedUpdateCommander();
            }

            if (IsLeader)
            {
                BelongCity.Leader = null;
                BelongCity.NeedUpdateLeader();
            }
            ClearMission();
            workingBuilding = null;
            loyalty = 0;
            BelongCity?.RemovePerson(this);
            CurrentCity?.RemovePerson(this);

            // 下野武将不允许再留在部队中(规则1),必须从部队里正常摘除。
            // 直接把 mBelongTroop 置空会让部队的主将/成员引用残留,造成双向不一致
            if (mBelongTroop != null)
            {
                mBelongTroop.RemovePerson(this);
            }

            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;
            // 在港关下野时归属到其隶属的主城;CurrentCity 可能为空,需要做兜底
            BelongCity = CurrentCity == null ? BelongCity : (CurrentCity.BelongCity == null ? CurrentCity : CurrentCity.BelongCity);
            if (IsPrisoner)
            {
                BelongForce?.BeCaptiveList.Remove(this);
                CurrentCity?.captiveList.Remove(this);
                Sango.Log.Info($"@人才@<{Name}>失去势力,进入囚犯下野状态");
            }
            else
            {
                Sango.Log.Info($"@人才@[{BelongForce?.Name}]的<{Name}>下野至{BelongCity?.Name}");
            }
            state = (int)PersonStateType.Unemployed;
            CurrentCity = BelongCity;
            // 在野武将必须挂靠到某个城市的在野名单上(规则1:必须有所在城市)
            if (BelongCity != null)
            {
                BelongCity.wildPersons.Add(this);
            }
            else
            {
                Sango.Log.Error($"@人才@<{Name}>下野失败:没有可挂靠的城市");
            }

            BelongCorps = null;
            BelongForce = null;
            mBelongTroop = null;
        }

        public Person Escape(EscapeType escapeType = EscapeType.None, SangoObject sangoObject = null)
        {
            if (!IsPrisoner)
            {
                Sango.Log.Error($"不是囚犯,无法逃跑!");
                // 兜底清理:状态不是俘虏却仍被挂在俘虏列表里时,要把残留引用摘掉
                CurrentCity?.RemoveCaptive(this);
                if (mBelongTroop != null)
                    mBelongTroop.RemoveCaptive(this);
                return this;
            }

            // 在部队中
            if (mBelongTroop != null)
            {
                City currentCity = mBelongTroop.CurrentCity;
                mBelongTroop.RemoveCaptive(this);
                ChangeCurrentCity(currentCity);
                mBelongTroop = null;
            }
            else
            {
                CurrentCity.RemoveCaptive(this);
            }

            if (BelongForce != null && BelongForce.IsAlive)
            {
                // 原势力尚存:脱逃后回归原势力都城,状态复位为一般武将
                state = (int)PersonStateType.Normal;
                ChangeBelongCity(BelongForce.CapitalCity);
                SetMission(MissionType.PersonReturn, BelongCity);
            }
            else
            {
                // 原势力已灭亡或本就无势力:转为在野。
                // 在野武将不允许再持有势力/军团/部队(规则1),这里必须显式清空,
                // 否则会出现"state 是在野,却仍隶属某个已灭亡势力与其军团"的不一致
                BelongForce?.BeCaptiveList.Remove(this);
                state = (int)PersonStateType.Unemployed;
                ChangeBelongCity(CurrentCity);
                BelongCorps = null;
                BelongForce = null;
                mBelongTroop = null;
            }

            // 根据逃出方式触发对应的事件
            if (escapeType == EscapeType.Escape)
            {
                Sango.Log.Info($"@人才@[{Name}]逃亡!");
                GameEvent.OnPersonEscape?.Invoke(this, BelongCity);
            }
            else if (escapeType == EscapeType.Released)
            {
                Sango.Log.Info($"@人才@[{Name}]被释放!");
                // 被释放的逻辑已经在PersonRecruit.ReleaseTarget中处理
                GameEvent.OnPersonRelease?.Invoke(this, sangoObject as Force);
            }
            else if (escapeType == EscapeType.TroopDestroyed)
            {
                Sango.Log.Info($"@人才@[{Name}]逃亡!");
                // 部队灭亡的情况可以在这里处理
                GameEvent.OnPersonEscape?.Invoke(this, BelongCity);
            }

            return this;
        }


        /// <summary>
        /// 获取经验
        /// </summary>
        /// <param name="add"></param>
        public void GainExp(int add)
        {
            // 【性格】学习速度 × 成长速度共同影响经验获取量
            //（冷静型学得更快，莽撞型偏慢）
            if (mPersonality != null)
            {
                int scale = 100;
                if (mPersonality.learnSpeedScale > 0)
                    scale = scale * mPersonality.learnSpeedScale / 100;
                if (mPersonality.growthScale > 0)
                    scale = scale * mPersonality.growthScale / 100;

                if (scale != 100 && scale > 0)
                {
                    long scaled = (long)add * scale / 100;
                    if (scaled > int.MaxValue) scaled = int.MaxValue;
                    add = (int)scaled;
                }
            }

            Exp += add;
            if (Level.Next == null)
                return;
            while (Level.exp > 0)
            {
                if (Exp > Level.exp)
                {
                    if (Level.Next != null)
                    {
                        Exp = Level.exp - Exp;
                        Level = Level.Next;
                    }
                    else
                        break;
                    Sango.Log.Info($"@个人@{Name}升级到{Level.Id}级");
                    GameEvent.OnPersonLevelUp?.Invoke(this);
                }
                else
                    break;
            }
        }

        public void GainMerit(int m)
        {
            merit += m;
        }

        /// <summary>
        /// 执行内政工作时，按"工作类型 → 对应属性"给能力经验
        /// （例：训练给武力、农业给政治，见 <see cref="GetJobAttribute"/>）。
        /// 每次的获取量是配置里的区间 <see cref="GainPlace.AttributeExpFromJob"/>（随机 min~max）。
        /// 上限仍由 ScenarioVariables.MaxAttributeGet 统一控制（在 PersonAttributeValue.SetExp 里封顶）。
        /// </summary>
        public void GainJobAttributeExp(int jobId)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return;

            PersonAttributeValue attr = GetJobAttribute(jobId);
            if (attr == null) return;

            int exp = GainValueConfig.Roll(GainPlace.AttributeExpFromJob);
            if (exp <= 0) return;

            attr.SetExp(attr.valueExp + exp, scenario);
        }

        /// <summary>工作类型 → 对应属性（没有对应关系时返回 null，表示这项工作不给能力经验）</summary>
        PersonAttributeValue GetJobAttribute(int jobId)
        {
            switch ((CityJobType)jobId)
            {
                // 武力：训练部队
                case CityJobType.TrainTroops:
                    return strength;

                // 统率：征兵 / 组建部队 / 生产马
                case CityJobType.RecruitTroops:
                case CityJobType.MakeTroop:
                case CityJobType.MakeTansport:
                case CityJobType.CreateHorse:
                    return command;

                // 智力：搜索 / 生产兵器 / 造船 / 生产兵装 / 研究
                case CityJobType.Searching:
                case CityJobType.CreateMachine:
                case CityJobType.CreateBoat:
                case CityJobType.CreateItems:
                case CityJobType.Research:
                    return intelligence;

                // 政治：农业 / 商业 / 交易 / 建造 / 升级建筑 / 派遣
                case CityJobType.Farming:
                case CityJobType.Develop:
                case CityJobType.TradeFood:
                case CityJobType.Build:
                case CityJobType.UpgradeBuilding:
                case CityJobType.TransformPerson:
                    return politics;

                // 魅力：巡查 / 登用
                case CityJobType.Inspection:
                case CityJobType.RecruitPerson:
                    return glamour;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 给某一项兵种适性加经验。
        /// <paramref name="influenceAbility"/> 直接用 TroopType.influenceAbility（= AbilityType），
        /// 所以"部队用的什么兵种就练哪一项适性"，不需要另写映射。
        /// 成长量是配置项（GainPlace.AbilityExpAttack / AbilityExpDestroyTroop / AbilityExpDestroyCity）。
        /// </summary>
        public void GainAbilityExp(int influenceAbility, int exp)
        {
            if (exp <= 0) return;
            if (Scenario.Cur == null) return;

            PersonAbilityValue ability = GetAbilityValue(influenceAbility);
            if (ability == null) return;

            long next = (long)ability.valueExp + exp;
            if (next > ushort.MaxValue) next = ushort.MaxValue;

            ability.SetExp((ushort)next);
        }

        /// <summary>兵种适性类型（AbilityType）→ 对应的适性数据（无对应返回 null）</summary>
        PersonAbilityValue GetAbilityValue(int influenceAbility)
        {
            switch ((AbilityType)influenceAbility)
            {
                case AbilityType.Spear: return spearLv;
                case AbilityType.Halberd: return halberdLv;
                case AbilityType.Crossbow: return crossbowLv;
                case AbilityType.Ride: return rideLv;
                case AbilityType.Water: return waterLv;
                case AbilityType.Machine: return machineLv;
                default: return null;
            }
        }

        public bool HasFeatrue(int id)
        {
            if (mFeatureList == null || mFeatureList.Count == 0) return false;
            return mFeatureList.Contains(id);
        }

        /// <summary>
        /// 初始化武将个人关系类特技的 Action。
        /// 同一武将重复准备剧本时先解除旧订阅，避免事件处理器重复注册。
        /// </summary>
        private void InitPersonActions()
        {
            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                {
                    actionList[i].Clear();
                }
                actionList.Clear();
            }
            else
            {
                actionList = new List<ActionBase>();
            }

            if (mFeatureList == null)
            {
                return;
            }

            for (int i = 0; i < mFeatureList.Count; i++)
            {
                Feature feature = mFeatureList[i];
                if (feature != null && feature.kind == (int)FeatureKindType.PersonRelationship)
                {
                    feature.InitActions(actionList, this);
                }
            }
        }

        /// <summary>
        /// 清理武将个人特技的事件订阅，防止剧本卸载后保留失效引用。
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            if (actionList == null)
            {
                return;
            }

            for (int i = 0; i < actionList.Count; i++)
            {
                actionList[i].Clear();
            }
            actionList.Clear();
            actionList = null;
        }

        public bool HasFeatrue(int[] ids)
        {
            if (mFeatureList == null || mFeatureList.Count == 0) return false;
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (mFeatureList.Contains(ids[i])) return true;
            }
            return false;
        }

        public int Distance(Person other)
        {
            if (other == null) return 0;
            Cell cell = mBelongTroop != null ? mBelongTroop.cell : BelongCity.CenterCell;
            Cell otherCell = other.mBelongTroop != null ? other.mBelongTroop.cell : other.BelongCity.CenterCell;
            return cell.Distance(otherCell);
        }

        public int DistanceDays(Person other)
        {
            if (other == null) return 0;
            City otherCity = other.mBelongTroop != null ? other.mBelongTroop.cell.BelongCityId : other.BelongCity;
            City thisCity = mBelongTroop != null ? mBelongTroop.cell.BelongCityId : BelongCity;
            return otherCity.Distance(thisCity);
        }

        public int DistanceDays(City otherCity)
        {
            if (otherCity == null) return 0;
            City thisCity = mBelongTroop != null ? mBelongTroop.cell.BelongCityId : (BelongCity == null ? CurrentCity : BelongCity);
            return otherCity.Distance(thisCity);
        }

        public int CompatibilityDistance(Person other)
        {
            if (other == null) return 0;
            return System.Math.Abs(compatibility - (other.compatibility));
        }

        public bool IsLike(Person other)
        {
            if (other == null || mLikePersonList == null) return false;
            return mLikePersonList.Contains(other);
        }

        public bool IsHate(Person other)
        {
            if (other == null || mHatePersonList == null) return false;
            return mHatePersonList.Contains(other);
        }

        /// <summary>
        /// other是否在自己记录的兄弟列表里
        /// 注意: 本方法不保证对称! BrotherList 是 PostInit 里每个人只把自己
        /// 塞进 mBrother.BrotherList 逐步拼出来的, 幼弟那份列表里不一定含组头,
        /// 所以 "弟.IsBrother(兄)" 会返回false。判断"两人是否同组"请一律用 IsBrotherGroupmate
        /// </summary>
        public bool IsBrother(Person other)
        {
            if (other == null || BrotherList == null) return false;
            return BrotherList.Contains(other);
        }

        /// <summary>
        /// 两人是否同属一个兄弟组(仲介结义与剧本原设通用, 保证对称)
        /// </summary>
        public bool IsBrotherGroupmate(Person other)
        {
            if (other == null || other == this) return false;

            // 主判据: 同组的人 Brother 全部指向同一个组头Id
            // (Person.SwornBrothers 与 Scenario 的配表建组都是这个约定, 天生对称)
            if (Brother > 0 && Brother == other.Brother)
                return true;

            // 兜底一: 两人解析到的组头是同一个对象
            if (mBrother != null && mBrother == other.mBrother)
                return true;

            // 兜底二: 共享同一个兄弟列表实例(List未重载==, 即引用比较)
            if (BrotherList != null && BrotherList == other.BrotherList)
                return true;

            return false;
        }

        /// <summary>
        /// 两人之间是否存在厌恶关系(单向也算)
        /// IsHate 只表示"自己厌恶对方", 所以必须双向判断
        /// </summary>
        public static bool IsHatedByEither(Person a, Person b)
        {
            if (a == null || b == null) return false;
            return a.IsHate(b) || b.IsHate(a);
        }

        /// <summary>
        /// 血亲追溯的最大代数(自己往上几代算作同一家族)
        /// </summary>
        const int MaxBloodGeneration = 3;

        /// <summary>
        /// 两人是否有血缘关系:
        /// 1. 父母子女(含过继)
        /// 2. 同父或同母的兄弟姐妹(含同父异母, 同母异父)
        /// 3. 三代以内有共同祖先(祖孙, 叔侄, 堂表兄姊)
        /// 同胞与义兄弟共用 Brother 字段无法区分, 所以兄弟姐妹一律靠父母字段判定
        /// </summary>
        public bool IsBloodRelative(Person other)
        {
            if (other == null || other == this) return false;
            if (IsParentchild(other)) return true;

            // 同一位父亲或同一位母亲即为兄弟姐妹
            if (mFather != null && mFather == other.mFather) return true;
            if (mMother != null && mMother == other.mMother) return true;

            return HasCommonAncestor(other);
        }

        /// <summary>
        /// 两人往上追 MaxBloodGeneration 代, 是否存在同一个祖先
        /// 集合里含自己, 所以"自己就是对方的祖辈"这种隔代直系也能命中
        /// </summary>
        bool HasCommonAncestor(Person other)
        {
            HashSet<Person> myAncestors = CollectAncestors(this);
            HashSet<Person> otherAncestors = CollectAncestors(other);
            foreach (Person person in myAncestors)
            {
                if (otherAncestors.Contains(person))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 逐层往上收集祖先(含自己), result.Add 兼做去重, 数据错乱成环时也能终止
        /// </summary>
        static HashSet<Person> CollectAncestors(Person person)
        {
            HashSet<Person> result = new HashSet<Person>();
            if (person == null)
                return result;

            result.Add(person);
            List<Person> current = new List<Person>() { person };
            for (int i = 0; i < MaxBloodGeneration && current.Count > 0; i++)
            {
                List<Person> next = new List<Person>();
                for (int j = 0; j < current.Count; j++)
                {
                    AddAncestor(current[j].mFather, result, next);
                    AddAncestor(current[j].mMother, result, next);
                }
                current = next;
            }
            return result;
        }

        static void AddAncestor(Person parent, HashSet<Person> result, List<Person> next)
        {
            if (parent == null || !result.Add(parent))
                return;
            next.Add(parent);
        }

        #region 仲介-关系读写
        /// <summary>
        /// 是否已有配偶(含剧本原配, 和亲, 仲介结婚)
        /// </summary>
        public bool HasSpouse => mSpouseList != null && mSpouseList.Count > 0;

        /// <summary>
        /// 是否已属于某个义兄弟组(含剧本原设兄弟, 仲介结义)
        /// </summary>
        public bool HasSwornBrother => Brother > 0 || mBrother != null
            || (BrotherList != null && BrotherList.Count > 0);

        /// <summary>
        /// other是否为自己的配偶
        /// </summary>
        public bool IsSpouse(Person other)
        {
            if (other == null || mSpouseList == null) return false;
            return mSpouseList.Contains(other);
        }

        /// <summary>
        /// 把other追加为自己的配偶, 并重建mSpouseList
        /// </summary>
        public void AddSpouse(Person other)
        {
            if (other == null || other == this) return;

            Scenario scenario = Scenario.Cur;
            List<int> ids = new List<int>();
            if (SpouseList != null)
                ids.AddRange(SpouseList);
            if (ids.Contains(other.Id))
                return;

            ids.Add(other.Id);
            SpouseList = ids.ToArray();
            mSpouseList = scenario.Array2ObjectList(scenario.personSet, SpouseList);
        }

        /// <summary>
        /// 结义: members全部指向组头, 并共享同一个BrotherList实例
        /// 组头的Brother指向自己, 与PostInit/Init的既有语义保持一致
        /// </summary>
        public static void SwornBrothers(List<Person> members)
        {
            if (members == null || members.Count < 2) return;

            List<Person> group = new List<Person>(members);
            group.Sort((a, b) => a.Id.CompareTo(b.Id));
            Person head = group[0];

            List<Person> brotherList = new List<Person>();
            head.Brother = head.Id;
            head.mBrother = head;

            for (int i = 0; i < group.Count; i++)
            {
                Person person = group[i];
                if (person == null) continue;

                person.Brother = head.Id;
                person.mBrother = head;
                if (!brotherList.Contains(person))
                    brotherList.Add(person);
                // 所有成员共享同一个列表引用
                person.BrotherList = brotherList;
            }

            brotherList.Sort(SangoObject.Compare);
        }
        #endregion

        public bool IsParentchild(Person other)
        {
            if (other == null) return false;
            if (other.mFather == this) return true;
            if (other.mMother == this) return true;
            if (mFather == other) return true;
            if (mMother == other) return true;
            return false;
        }

        public void Dead()
        {
            // 必须先缓存俘虏身份:下面会立刻把 state 改写为 Dead,
            // 改写之后 IsPrisoner 恒为 false,俘虏相关的清理逻辑将永远执行不到
            bool wasPrisoner = IsPrisoner;

            state = (int)PersonStateType.Dead;
            if (BelongCity != null)
            {
                BelongCity.allPersons.Remove(this);
                BelongCity.freePersons.Remove(this);
                BelongCity.wildPersons.Remove(this);
            }

            if (wasPrisoner)
            {
                // 俘虏死亡:必须从关押方的俘虏名单与势力的被俘名单中一并移除,
                // 否则会出现"已经死亡的武将仍然挂在俘虏列表里"的残留。
                // 注意:俘虏入狱时 BelongCity 已被置空,所在城市应取 CurrentCity
                BelongForce?.BeCaptiveList.Remove(this);
                if (mBelongTroop != null)
                {
                    mBelongTroop.captiveList.Remove(this);
                }
                else
                {
                    CurrentCity?.captiveList.Remove(this);
                }
            }
            else if (mBelongTroop != null)
            {
                // 非俘虏必须从部队中正常摘除,保证部队主将/成员引用与武将的 mBelongTroop 双向一致
                mBelongTroop.RemovePerson(this);
            }

            // 死亡武将不再参与任何部队与建造
            mBelongTroop = null;
            workingBuilding = null;
        }

        public int GetAttribute(int attrType)
        {
            switch (attrType)
            {
                case 0:// (int)AttributeType.Command:
                    return Command;
                case 1:// (int)AttributeType.Strength:
                    return Strength;
                case 2:// (int)AttributeType.Intelligence:
                    return Intelligence;
                case 3:// (int)AttributeType.Politics:
                    return Politics;
                case 4:// (int)AttributeType.Glamour:
                    return Glamour;
            }
            return 0;
        }

        /// <summary>
        /// 获取装备的属性加成
        /// </summary>
        /// <param name="getBonus">获取单个装备加成的委托</param>
        /// <returns>总加成值</returns>
        private int GetEquipmentBonus(System.Func<Equipment, int> getBonus)
        {
            int bonus = 0;

            if (EquippedWeapon != null)
            {
                bonus += getBonus(EquippedWeapon);
            }

            if (EquippedHorse != null)
            {
                bonus += getBonus(EquippedHorse);
            }

            if (EquippedArmor != null)
            {
                bonus += getBonus(EquippedArmor);
            }

            return bonus;
        }

        /// <summary>
        /// 装备武器
        /// </summary>
        /// <param name="weapon">武器</param>
        public void EquipWeapon(Equipment weapon)
        {
            if (weapon != null && weapon.kind == (int)ItemKindType.Equipment_Weapon)
            {
                EquippedWeapon = weapon;
            }
        }

        /// <summary>
        /// 装备马
        /// </summary>
        /// <param name="horse">马</param>
        public void EquipHorse(Equipment horse)
        {
            if (horse != null && horse.kind == (int)ItemKindType.Equipment_Horse)
            {
                EquippedHorse = horse;
            }
        }

        /// <summary>
        /// 装备铠甲
        /// </summary>
        /// <param name="armor">铠甲</param>
        public void EquipArmor(Equipment armor)
        {
            if (armor != null && armor.kind == (int)ItemKindType.Equipment_Armor)
            {
                EquippedArmor = armor;
            }
        }

        /// <summary>
        /// 卸下武器
        /// </summary>
        public void UnequipWeapon()
        {
            EquippedWeapon = null;
        }

        /// <summary>
        /// 卸下马
        /// </summary>
        public void UnequipHorse()
        {
            EquippedHorse = null;
        }

        /// <summary>
        /// 卸下铠甲
        /// </summary>
        public void UnequipArmor()
        {
            EquippedArmor = null;
        }

        public void UpgradeOfficial(Official official)
        {
            if (official == null) return;

            Official last = Official;
            last.OnPersonRemove(this);
            int need = Official.meritNeeds;
            Official = official;
            Official.OnPersonAdd(this);
            merit -= need;
            Sango.Log.Info($"@个人@{Name}官职升到[{Official.Name}]!!");
            GameEvent.OnPersonUpgradeOfficial?.Invoke(this, last);
        }

        public bool IsHighStength()
        {
            return strength.baseValue > command.baseValue && strength.baseValue > intelligence.baseValue
                && strength.baseValue > politics.baseValue;
        }

        public string GetDescription()
        {
            if (!string.IsNullOrEmpty(description) && description != "0")
                return description;
            return GameLanguage.GetString(Id);
        }

        public static Person FormLib(PersonLib personLib)
        {
            Person person = new Person();
            person.Id = personLib.Id;
            person.image = personLib.image;
            person.image_old = personLib.image_old;
            return person;
        }
        private static int[] CloneArray(int[] source)
        {
            if (source == null) return new int[0];
            return (int[])source.Clone();
        }
        public static Person FormLib2(PersonLib personLib)
        {
            Person person = new Person();
            // 深拷贝:字段名字与类型都一致的直接拷贝,不一致的字段放弃
            //person.Id = personLib.Id;
            person.Name = personLib.familyName + personLib.giveName;
            person.familyNameID = personLib.familyNameID;
            person.familyName = personLib.familyName;
            person.description = personLib.description;
            person.giveNameID = personLib.giveNameID;
            person.giveName = personLib.giveName;
            person.nickNameID = personLib.nickNameID;
            person.nickName = personLib.nickName;
            person.headIconID = personLib.headIconID;
            person.imageID = personLib.imageID;
            person.image = personLib.image;
            person.image_old = personLib.image_old;
            person.sex = personLib.sex;
            person.yearBorn = personLib.yearBorn;
            person.yearDead = personLib.yearDead;
            person.compatibility = personLib.compatibility & 0xFF;
            person.state = personLib.state;
            person.voice = personLib.voice;
            person.tone = personLib.tone;
            person.kanshitsu = personLib.kanshitsu;
            person.ideal = personLib.ideal;
            person.talent = personLib.talent;
            person.merit = personLib.merit;
            person.stamina = personLib.stamina;
            person.Exp = personLib.Exp;
            person.consanguinity = personLib.consanguinity;
            person.command.baseValue = personLib.command;
            person.command.changeId = personLib.attributeChangeType;
            person.strength.baseValue = personLib.strength;
            person.strength.changeId = personLib.attributeChangeType;
            person.intelligence.baseValue = personLib.intelligence;
            person.intelligence.changeId = personLib.attributeChangeType;
            person.politics.baseValue = personLib.politics;
            person.politics.changeId = personLib.attributeChangeType;
            person.glamour.baseValue = personLib.glamour;
            person.glamour.changeId = personLib.attributeChangeType;
            person.spearLv.baseValue = personLib.spearLv;
            person.halberdLv.baseValue = personLib.halberdLv;
            person.crossbowLv.baseValue = personLib.crossbowLv;
            person.rideLv.baseValue = personLib.rideLv;
            person.waterLv.baseValue = personLib.waterLv;
            person.machineLv.baseValue = personLib.machineLv;
            person.personality = personLib.personality;
            person.argumentation = personLib.argumentation;

            person.Father = personLib.Father;
            person.Mother = personLib.Mother;
            person.Brother = personLib.Brother;

            person.SpouseList = CloneArray(personLib.SpouseList);
            person.LikePersonList = CloneArray(personLib.LikePersonList);
            person.HatePersonList = CloneArray(personLib.HatePersonList);
            person.FeatureList = CloneArray(personLib.FeatureList);


            return person;
        }
    }
}
