using Newtonsoft.Json;
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
        [JsonProperty("personality")]
        public int PersonalityId;

        public Personality mPersonality;

        /// <summary>
        /// 义理
        /// </summary>
        [JsonProperty("argumentation")]
        public int ArgumentationId;

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
        /// 武将种类（对应 Open311 的 person_type_t type_）。
        ///
        /// 与 <see cref="state"/> 正交：state 是"当前身份/生存状态"（会变），
        /// 本字段是"数据来源分类"（运行期不变）。默认 -1 = 未分类，
        /// 老存档读入后就是 -1，不会误判成历史武将。
        /// </summary>
        [JsonProperty] public int type = (int)PersonType.None;

        /// <summary>
        /// 异族类型（0 = 非异族；1~4 = 羌 / 山越 / 乌丸 / 南蛮，取值见 <see cref="BarbarianConfig.Tribe"/>）。
        ///
        /// 【固化时机】剧本加载 / 读档 / 编辑器加载都会走 <see cref="OnScenarioPrepare"/>，
        /// 在那里由 <see cref="FixupBarbarianTribe"/> 按「Id + 名字」双重校验写入一次。
        /// 之后运行期只读本字段（O(1)），不再每次都遍历配置表做字符串比较。
        ///
        /// 【随存档保存】字段参与序列化，所以老存档读入后也会在第一次加载时补写，
        /// 后续进游戏不再重复查表。
        ///
        /// 【不覆盖已有值】固化只在字段为 0 时写入：编辑器 / Mod 手工把某人标成异族
        /// （或把某人清成非异族）后，后续加载不会把它改回去。
        /// </summary>
        [JsonProperty] public int barbarianTribe = 0;

        /// <summary>
        /// 是否已被判定寿终（对应 Open311 的 PersonStatus_EstimatedDeath）。
        ///
        /// 原版寿终是两段式：先按月抽样"标记"，下一次结算才真正死亡。
        /// 这样做的目的是让死亡分批发生（不会同一个月里所有人一起没），
        /// 也给玩家留出一回合的缓冲去应对（任命新太守、调整军队）。
        /// </summary>
        [JsonProperty] public bool estimatedDeath;

        /// <summary>
        /// 死亡类型（对应 Open311 的 DeathType_Natural / DeathType_NonNatural）。
        ///
        /// 影响寿终概率：非自然死亡（战死、处决未遂等）的武将寿命按能力值再扣，
        /// 且抽样系数从 4 降到 1 ——  equivalente 于"重伤在身、更容易早逝"。
        /// </summary>
        [JsonProperty] public int deathType = (int)PersonDeathType.Natural;

        /// <summary>
        /// 禁止仕官的势力君主 Id（对应 Open311 的 banned_kunshu_id）。
        ///
        /// 语义：换势力 / 被俘后的一段时间内，不能重新投效这个君主。
        /// -1 表示无限制。老存档没有该字段，反序列化后为 0（= 无限制），
        /// 但为了与"Id 从 1 开始"的约定一致，判断时统一用 <see cref="BanTimer"/> > 0。
        /// </summary>
        [JsonProperty] public int BannedKunshuId = -1;

        /// <summary>
        /// 禁止仕官剩余回合数（对应 Open311 的 ban_timer，单位为月）。
        /// 本项目一个月 = 一个回合，故 6 即半年。
        /// </summary>
        [JsonProperty] public int BanTimer;

        /// <summary>
        /// 最近一次被流言命中并动摇忠诚的回合（Scenario.TurnCount）。小于等于 0 表示近期未受流言影响。
        /// 供 CityAI 的褒奖响应识别"被 AI 互喷打低"的武将（见 ScenarioVariables.cityStrategyRumorVictimRewardTurns）。
        /// </summary>
        [JsonProperty] public int lastRumorTurn;

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
        [JsonProperty("Father")]
        public int FatherId;
        public Person Father { get; set; }

        /// <summary>
        /// 母亲
        /// </summary>
        [JsonProperty("Mother")]
        public int MotherId;
        public Person Mother { get; set; }

        /// <summary>
        /// 配偶
        /// </summary>
        [JsonProperty("SpouseList")]
        public int[] SpouseListId;

        public SangoObjectList<Person> SpouseList { get; private set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        [JsonProperty("Brother")]
        public int BrotherId;

        public Person Brother { get; set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        public List<Person> BrotherList;

        /// <summary>
        /// 喜欢武将
        /// </summary>
        [JsonProperty("LikePersonList")]
        public int[] LikePersonListId;

        public SangoObjectList<Person> LikePersonList;

        /// <summary>
        /// 厌恶武将
        /// </summary>
        [JsonProperty("HatePersonList")]
        public int[] HatePersonListId;
        public SangoObjectList<Person> HatePersonList;

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
        [JsonProperty("FeatureList")]
        public int[] FeatureListId;

        public SangoObjectList<Feature> FeatureList;

        /// <summary>
        /// 武将个人关系类特技装配的动作列表。
        /// </summary>
        public List<ActionBase> actionList;

        /// <summary>
        /// 装备与宝物提供的特效动作列表。
        ///
        /// 与 <see cref="actionList"/>（武将特技）**分开两张表**：换装备 / 授予 / 没收时只重建本表，
        /// 不会把武将自身的特技订阅一起清掉（两边都订阅同一批 GameEvent，混在一张表里必然互相清除）。
        /// 重建时机见 <see cref="BuildEquipActions"/>。
        /// </summary>
        public List<ActionBase> equipActionList;

        /// <summary>
        /// 库存
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(ItemStoreConverter))]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 装备的武器（存档里只存 EquippedWeaponId 这个数值，加载时由 ResolveReferenceObjects 解析）
        ///
        /// 【为什么是 ItemType 而不是 Equipment】
        ///   道具库由 <c>SangoObjectMap&lt;ItemType&gt;</c> 承载，反序列化统一产出 ItemType 实例，
        ///   写成 Equipment 会永远解析成 null（装备系统此前一直空转的原因）。
        /// </summary>
        [JsonProperty("EquippedWeapon")]
        public int EquippedWeaponId;

        /// <summary>装备的武器对象（不参与序列化，见 EquippedWeaponId）</summary>
        public ItemType EquippedWeapon { get; set; }

        /// <summary>
        /// 装备的马（存档里只存 EquippedHorseId）
        /// </summary>
        [JsonProperty("EquippedHorse")]
        public int EquippedHorseId;

        /// <summary>装备的马对象（不参与序列化，见 EquippedHorseId）</summary>
        public ItemType EquippedHorse { get; set; }

        /// <summary>
        /// 装备的铠甲（存档里只存 EquippedArmorId）
        /// </summary>
        [JsonProperty("EquippedArmor")]
        public int EquippedArmorId;

        /// <summary>装备的铠甲对象（不参与序列化，见 EquippedArmorId）</summary>
        public ItemType EquippedArmor { get; set; }

        /// <summary>
        /// 装备的宝物（栏位名「宝物」；存档里只存 EquippedTreasureId）。
        /// 非装备类宝物（书籍 / 玉器 / 珍玩 / 兵符等）不占装备栏，授予后立即生效，**每人限 1 件**。
        /// </summary>
        [JsonProperty("EquippedTreasure")]
        public int EquippedTreasureId;

        /// <summary>宝物对象（不参与序列化，见 EquippedTreasureId）</summary>
        public ItemType EquippedTreasure { get; set; }

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

        /// <summary>
        /// 是否"在世势力"的君主 —— 收押 / 招降的门禁判定。
        ///
        /// 与 <see cref="IsGovernor"/> 的区别：势力灭亡后君主会下野，
        /// state 变成 Unemployed，此时 IsGovernor 自然为 false；
        /// 但如果灭亡流程还没跑到"下野"那一步（比如单挑结算早于势力清理），
        /// 他的 state 仍停在 Governor，用 IsGovernor 拦截就会误伤。
        ///
        /// 这里同时要求"势力还在"，所以：
        ///   · 势力存活 → 君主不可被收押 / 招降（只能释放或斩杀）；
        ///   · 势力已灭亡 → 放行（可被收押、可被招募）。
        /// 语义与"势力灭亡后君主只是一介在野武将"一致。
        /// </summary>
        public bool IsActiveGovernor => IsGovernor && BelongForce != null && BelongForce.IsAlive;

        // ==================== 武将种类（对应 Open311 Person::is_xxx） ====================
        // 这些判定的语义与原版一一对应，注意 is_event 覆盖 NPC 两种取值。

        /// <summary>是否历史武将</summary>
        public bool IsHistorical => type == (int)PersonType.Historical;

        /// <summary>是否古代武将</summary>
        public bool IsAncient => type == (int)PersonType.Ancient;

        /// <summary>是否自建武将</summary>
        public bool IsCustom => type == (int)PersonType.Custom;

        /// <summary>是否临时武将（贼军/战场生成）</summary>
        public bool IsTemporary => type == (int)PersonType.Temporary;

        /// <summary>是否 NPC</summary>
        public bool IsNpc => type == (int)PersonType.Npc;

        /// <summary>
        /// 是否事件类武将（事件 + NPC）。
        /// 原版 Person::is_event 就是这个双值判定，用于把这类人排除在
        /// 寿命结算、自然死亡、婚姻等常规流程之外。
        /// </summary>
        public bool IsEventPerson => type == (int)PersonType.Event || type == (int)PersonType.Npc;

        /// <summary>
        /// 是否异族武将（乌丸 / 羌族 / 山越 / 南蛮）。
        ///
        /// 读 <see cref="barbarianTribe"/> 这个**已固化**的字段，不再每次比对配置表。
        /// 字段由 <see cref="OnScenarioPrepare"/> 在剧本加载时写入，见 <see cref="FixupBarbarianTribe"/>。
        /// 注意：在 <see cref="OnScenarioPrepare"/> 之前（例如刚 new 出来还没准备的编辑器对象）
        /// 该字段为 0，本属性返回 false —— 属于保守方向，不会误排除正常武将。
        /// </summary>
        public bool IsBarbarian => barbarianTribe != 0;

        /// <summary>
        /// 异族类型的强类型视图（<see cref="BarbarianConfig.Tribe"/>）。
        /// 读写都直接落到 <see cref="barbarianTribe"/>，便于后续按族刷怪 / 分组。
        /// </summary>
        public BarbarianConfig.Tribe BarbarianTribe
        {
            get { return (BarbarianConfig.Tribe)barbarianTribe; }
            set { barbarianTribe = (int)value; }
        }

        /// <summary>
        /// 按 <see cref="BarbarianConfig"/> 固化异族类型（**只在当前为 0 时写入**）。
        ///
        /// 调用时机：<see cref="OnScenarioPrepare"/> —— 剧本加载、读档、基础武将编辑器
        /// 三条加载路径都会经过这里，所以只需挂一处。
        /// 已有标记时不覆盖，保留编辑器 / Mod 手工指定的结果。
        /// </summary>
        public void FixupBarbarianTribe()
        {
            if (barbarianTribe != 0)
                return;

            barbarianTribe = (int)BarbarianConfig.GetTribe(Id, Name);
        }

        /// <summary>
        /// 是否"特殊武将"（事件 / NPC / 异族）。
        ///
        /// 原版对应 `is_event() or is_iminzoku() or is_bandit()` 这条三连表达式：
        /// 凡是"只该由常规武将参与"的流程（归属巡检、寿命结算、登用、军团编成、流言…）
        /// 都要先排除这批人。本项目没有 Force.ForceType，异族用 Id+名字配置兜底。
        /// </summary>
        public bool IsSpecialPerson => IsEventPerson || IsBarbarian;

        /// <summary>
        /// 是否可参与常规人生周期（寿命/死亡/婚姻/血缘维护）。
        /// 对应原版各处 `not is_event() and not is_temporary()` 的组合条件；
        /// 异族与事件同类，同样不参与——它们由异族系统按需生成/回收，
        /// 不该被自然死亡淘汰（原版异族武将是 PersonType_Temporary）。
        /// </summary>
        public bool HasLifeCycle => !IsSpecialPerson && !IsTemporary;

        /// <summary>
        /// 推算寿命终止年（对应 Open311 的 Person::get_estimated_death）。
        ///
        /// 原版 = 基础死亡年（death_）+ 长寿特技加成，再钳到数据库最大年。
        /// 本项目没有"长寿"特技，直接用 yearDead；
        /// yearDead <= 0 视为"无限寿命"（自建武将常见），返回 int.MaxValue 让寿终结算跳过。
        /// </summary>
        /// <returns>寿命终止年</returns>
        public int GetEstimatedDeathYear()
        {
            return yearDead > 0 ? yearDead : int.MaxValue;
        }

        /// <summary>
        /// 是否因寿终而被标记（下一次月度结算会真正死亡）。
        /// </summary>
        /// <returns>是否已标记寿终</returns>
        public bool IsEstimatedDeath => estimatedDeath;

        /// <summary>
        /// 是否可编入部队。
        ///
        /// 原版 Unit::can_set_member 是白名单（历史/古代/自建/临时），
        /// 这里改成"排除事件与 NPC"的黑名单：老存档与剧本内嵌武将的 type 是 -1（None），
        /// 用白名单会把整批未分类武将挡在编队之外，属于功能性倒退。
        /// </summary>
        public bool CanBeTroopMember => !IsEventPerson;

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

            // 装备存放在道具库(ItemTypes)里，运行期一律是 ItemType 实例（见 EquippedWeapon 字段注释）。
            // 解析完之后立刻重建装备特效，保证读档后效果与存档前一致（见 BuildEquipActions）。
            EquippedWeapon = IdToObject<ItemType>(scenario, EquippedWeaponId);
            EquippedHorse = IdToObject<ItemType>(scenario, EquippedHorseId);
            EquippedArmor = IdToObject<ItemType>(scenario, EquippedArmorId);
            EquippedTreasure = IdToObject<ItemType>(scenario, EquippedTreasureId);
            BuildEquipActions();
        }

        /// <summary>id → 对象；id &lt;= 0 表示没有，返回 null（与 Id2ObjConverter.Id2Object 的口径一致）</summary>
        static T IdToObject<T>(Scenario scenario, int id) where T : SangoObject, new()
        {
            if (scenario == null || id <= 0) return null;
            return scenario.GetObject<T>(id);
        }

        public override void OnScenarioPrepare(Scenario scenario)
        {
            // 异族标记固化：配置表（Id + 名字双重校验）→ 运行时字段，只写一次。
            // 放在最前面：后续任何流程（含归属巡检、寿命、编成）都依赖 IsBarbarian。
            FixupBarbarianTribe();

            ResolveReferenceObjects(scenario);      // 官职/等级/装备/工作建筑：只存 id，在这里解析
            BelongForce = scenario.Id2Object(scenario.forceSet, BelongForceId);
            BelongCorps = scenario.Id2Object(scenario.corpsSet, BelongCorpsId);
            BelongCity = scenario.Id2Object(scenario.citySet, BelongCityId);
            CurrentCity = scenario.Id2Object(scenario.citySet, CurrentCityId);
            if (CurrentCity == null && BelongCity != null)
                CurrentCity = BelongCity;
            mBelongTroop = scenario.Id2Object(scenario.troopsSet, BelongTroopId);

            if (PersonalityId <= 0)
                PersonalityId = 1;
            mPersonality = scenario.Id2Object(scenario.CommonData.Personalities, PersonalityId);

            if (ArgumentationId <= 0)
                ArgumentationId = 2;
            mArgumentation = scenario.Id2Object(scenario.CommonData.Argumentations, ArgumentationId);

            //if (attributeChangeType <= 0)
            //    attributeChangeType = 5;
            //mAttributeChangeType = scenario.Id2Object(scenario.CommonData.AttributeChangeTypes, attributeChangeType);

            Father = scenario.Id2Object(scenario.personSet, FatherId);
            Mother = scenario.Id2Object(scenario.personSet, MotherId);
            Brother = scenario.Id2Object(scenario.personSet, BrotherId);

            SpouseList = scenario.Array2ObjectList(scenario.personSet, SpouseListId);
            LikePersonList = scenario.Array2ObjectList(scenario.personSet, LikePersonListId);
            HatePersonList = scenario.Array2ObjectList(scenario.personSet, HatePersonListId);
            FeatureList = scenario.Array2ObjectList(scenario.CommonData.Features, FeatureListId);
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
            if (Brother != null)
            {
                if (Brother.BrotherList == null)
                    Brother.BrotherList = new List<Person>();

                Brother.BrotherList.Add(this);
            }

            if (BelongCity != null && BelongForce != null && BelongCorps != null)
            {
                if (state > 4)
                {
                    state = 4;
                }
            }

            // 修正一下所属城市
            if (state <= 4)
            {
                if (BelongCity == null && CurrentCity != null)
                    BelongCity = CurrentCity;
            }

            if (IsAlive)
            {
                switch ((PersonStateType)state)
                {
                    // 处理主公
                    case PersonStateType.Governor:


                        if (BelongCity != null)
                        {
                            BelongCorpsId = BelongCity.BelongCorpsId;
                            BelongCorps = scenario.Id2Object(scenario.corpsSet, BelongCorpsId);
                            BelongForceId = BelongCorps.BelongForceId;
                            BelongForce = scenario.Id2Object(scenario.forceSet, BelongForceId);
                            BelongCity.AddPerson(this);
                            BelongCity.NeedUpdateLeader();
                            if (BelongForce.Governor != Id)
                                state = (int)PersonStateType.Normal;

                            CheckBelongConsistency();
                        }
                        break;
                    // 军团长
                    case PersonStateType.Commander:
                        if (BelongCity != null)
                        {
                            BelongCorps = BelongCity.BelongCorps;
                            BelongForce = BelongCity.BelongForce;
                            BelongCity.AddPerson(this);

                            if (BelongCorps.Comander != Id)
                                state = (int)PersonStateType.Normal;

                            BelongCity.NeedUpdateLeader();
                            CheckBelongConsistency();
                        }
                        break;
                    // 太守
                    case PersonStateType.Leader:
                        if (BelongCity != null)
                        {
                            BelongCorps = BelongCity.BelongCorps;
                            BelongForce = BelongCity.BelongForce;
                            BelongCity.AddPerson(this);
                            BelongCity.NeedUpdateLeader();
                            if (BelongCity.Leader == null)
                                BelongCity.Leader = this;
                            else if (BelongCity.Leader != null)
                                state = (int)PersonStateType.Normal;

                            CheckBelongConsistency();
                        }
                        break;
                    // 一般武将
                    case PersonStateType.Normal:
                        if (BelongCity != null)
                        {
                            BelongCorps = BelongCity.BelongCorps;
                            BelongForce = BelongCity.BelongForce;
                            BelongCity.AddPerson(this);
                            CheckBelongConsistency();
                        }
                        break;
                    // 在野
                    case PersonStateType.Unemployed:
                        BelongCorps = null;
                        BelongForce = null;
                        if (BelongCity != null)
                        {
                            CurrentCity = BelongCity;
                            BelongCity = null;
                        }
                        if (CurrentCity != null)
                            CurrentCity.AddWildPerson(this);
                        else
                        {
                            state = (int)PersonStateType.Invalid;
                            CurrentCity = scenario.citySet.RandomGet();
                        }
                        break;
                    // 囚犯
                    case PersonStateType.Prisoner:
                        // 【去重】读档时 Troop/City 的 OnScenarioPrepare 可能已经把本人登记进
                        // 对应名册，这里再无条件 Add 会触发 SangoObjectList 的"不能重复添加"
                        // 错误日志（并且旧实现会把重复元素真的插进去）。
                        if (mBelongTroop != null)
                        {
                            if (BelongForce != null && !BelongForce.BeCaptiveList.Contains(this))
                                BelongForce.BeCaptiveList.Add(this);
                            if (!mBelongTroop.captiveList.Contains(this))
                                mBelongTroop.captiveList.Add(this);
                        }
                        else
                        {
                            // 修复一下
                            if (CurrentCity.IsSameForce(this))
                            {
                                BelongCity = CurrentCity;
                                BelongCorps = CurrentCity.BelongCorps;
                                if (BelongForce != null)
                                {
                                    state = (int)PersonStateType.Normal;
                                    BelongCity.AddPerson(this);
                                    BelongCity.freePersons.Add(this);
                                }
                                else
                                {
                                    state = (int)PersonStateType.Unemployed;
                                    BelongCity.AddWildPerson(this);
                                }
                            }
                            else
                            {
                                if (BelongForce != null && !BelongForce.BeCaptiveList.Contains(this))
                                    BelongForce.BeCaptiveList.Add(this);
                                if (!CurrentCity.captiveList.Contains(this))
                                    CurrentCity.captiveList.Add(this);
                            }
                        }
                        break;
                    // 未登场
                    case PersonStateType.Invalid:
                        BelongCorps = null;
                        BelongForce = null;
                        break;
                    // 未发现
                    case PersonStateType.Invisible:
                        BelongCorps = null;
                        BelongForce = null;
                        if (CurrentCity != null)
                            CurrentCity.AddInvisiblePerson(this);
                        else if (BelongCity != null)
                        {
                            CurrentCity = BelongCity;
                            BelongCity = null;
                            CurrentCity.AddInvisiblePerson(this);
                        }
                        else
                        {
                            CurrentCity = scenario.citySet.RandomGet();
                            CurrentCity.AddInvisiblePerson(this);
                        }
                        break;
                    // 死亡
                    case PersonStateType.Dead:
                        BelongCorps = null;
                        BelongForce = null;
                        break;
                }
            }

            // 处理父亲
            if (Father != null)
                Father.sonList.Add(this);

            if (Mother != null)
                Mother.sonList.Add(this);

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

            // 【在途武将表】读档后按任务重建（对应 Open311 actors_ 的派生重建）。
            // 这张表不参与序列化 —— 它是"从每个人的 missionType 派生"的运行期索引，
            // 存盘再读回来只需重扫一遍即可，避免存档里多一份需要维护的冗余数据。
            if (missionType != 0 && IsAlive && !IsDead)
                scenario.RegisterActor(this);
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
            // 【修复】放开自动修正：读档 / 运行期发现归属与所属城不一致时以城为准校正，
            // 避免脏数据随随机移动 / 登庸 / 内政链路继续传播（原先只报错不修）。
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
            PersonalityId = mPersonality?.Id ?? 0;
            ArgumentationId = mArgumentation?.Id ?? 0;

            // 只存 id 的对象引用：把当前对象回写成数值（原来是 Id2ObjConverter 在写 JSON 时自动完成）
            OfficialId = Official?.Id ?? 0;
            LevelId = Level?.Id ?? 0;
            EquippedWeaponId = EquippedWeapon?.Id ?? 0;
            EquippedHorseId = EquippedHorse?.Id ?? 0;
            EquippedArmorId = EquippedArmor?.Id ?? 0;
            EquippedTreasureId = EquippedTreasure?.Id ?? 0;
            workingBuildingId = workingBuilding?.Id ?? 0;

            FatherId = Father?.Id ?? 0;
            MotherId = Mother?.Id ?? 0;
            BrotherId = Brother?.Id ?? 0;

            SpouseListId = SpouseList?.ToArray() ?? null;
            LikePersonListId = LikePersonList?.ToArray() ?? null;
            HatePersonListId = HatePersonList?.ToArray() ?? null;
            FeatureListId = FeatureList?.ToArray() ?? null;
        }

        public override void Init(Scenario scenario)
        {
            base.Init(scenario);

            if (Brother != null)
            {
                if (Brother == this)
                {
                    BrotherList.Sort(SangoObject.Compare);

                }
                else
                {
                    BrotherList = Brother.BrotherList;
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
                        city.AddInvisiblePerson(this);
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
                                // 【修复】父亲没有所在城市时依次回退到所属城/随机城，
                                // 避免儿子登场后 CurrentCity 为空（会在回合结束的随机移动中空引用）；
                                // 登记名单应加入儿子 x，原代码误加了父亲 this。
                                City sonCity = CurrentCity;
                                if (sonCity == null)
                                {
                                    if (BelongCity != null)
                                        sonCity = BelongCity;
                                    else
                                        sonCity = scenario.citySet.RandomGet();
                                }
                                x.CurrentCity = sonCity;
                                x.state = (int)PersonStateType.Invisible;
                                sonCity.AddInvisiblePerson(x);
                            }
                        }
                    });
                }
            }

            return base.OnYearStart(scenario);
        }

        /// <summary>
        /// 沿城市邻接图走一站（对应 Open311 的 get_next_city_id + person_set_location_building_id）。
        ///
        /// 每回合只推进一个领接城，抵达终点才返回 true。移动耗时因此等于城间邻接步数，
        /// 与原版"一站 = 一回合"的口径一致。
        /// </summary>
        /// <param name="dest">最终目的地（可以是港口/关隘，内部归一到所属都市）</param>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否已抵达终点</returns>
        public bool DoMove(City dest, Scenario scenario)
        {
            if (dest == null)
                return true;

            City target = dest.BelongCity == null ? dest : dest.BelongCity;
            if (CurrentCity == null)
                return true;
            City currentCity = CurrentCity.BelongCity == null ? CurrentCity : CurrentCity.BelongCity;

            // 已经在目的地
            if (target == currentCity)
                return true;

            // 找到最短移动路径
            List<City> path = scenario.FindShortestPath(currentCity, target);
            if (path == null || path.Count <= 1)
            {
                // 路网不连通（数据异常）：视为抵达，由调用方迁户口兜底，
                // 避免任务永久悬挂把武将锁死在"移动中"
                Sango.Log.Error($"*{Name} 找不到 {currentCity.Name} -> {target.Name} 的路径，已强制结束移动");
                return true;
            }

            City next = path[1];
            ChangeCurrentCity(next);

            // 注意比较的是 target 而不是 dest：
            // 目的地若是港口/关隘，dest 与都市永不相等，会导致永远无法抵达
            if (next == target)
                return true;

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

                        // 目的地失效（被攻陷 / Id 越界 / 无势力）：改回出发城，没有出发城则取消任务。
                        // 原版对应 get_next_city_id 返回 -1 时的 person_reset_order。
                        if (dest == null || !this.IsSameForce(dest))
                        {
                            if (BelongCity != null && BelongCity != dest)
                            {
                                SetMission(MissionType.PersonReturn, BelongCity);
                            }
                            else
                            {
                                // 出发城也没了（城陷/势力灭亡）：不在这里"以城市为准"改势力
                                // —— 出发城可能已属敌方，用城校正会把己方武将瞬间改成敌方。
                                // 交给 ReturnHome 改去本势力都城，找不到则清任务由巡检安置。
                                //
                                // 【级别】这是**正常改道**，不是异常：城陷后返程改去都城属于预期流程
                                // （与 PersonStateSystem.ReturnHome 的同款日志保持一致，那边也是 Info）。
                                // 用 Warning 会让"城陷"这种常态事件在控制台标黄，掩盖真正的问题。
                                Sango.Log.Info($"*{Name} 的返程目的失效且无出发城，改去本势力都城");
                                PersonStateSystem.ReturnHome(this);
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
                case (int)MissionType.PersonCityStrategy:
                    {
                        // 任务参数约定（与 CityStrategyManager.Dispatch 一一对应）：
                        // missionTarget = 目标据点城市 Id；p1 = 发起势力 Id；p2 = CityStrategyType；
                        // p3/p4 = 二虎竞食的两个目标势力 Id；流言只用 p3（目标势力 Id），p4 恒为 0
                        City targetCity = scenario.citySet.Get(missionTarget);
                        Force senderForce = scenario.forceSet.Get(missionParams1);

                        // 目标据点或发起势力已经消失时不再移动，直接起程归家。
                        // 这里必须先判空再 DoMove：DoMove 会直接解引用 dest.BelongCity
                        if (targetCity == null || senderForce == null || !senderForce.IsAlive)
                        {
                            SetReturnHomeMission();
                            return;
                        }

                        if (DoMove(targetCity, scenario))
                        {
                            CityStrategyResult result = GameSystem.GetSystem<CityStrategyManager>()
                                .ExecuteMission(this, (CityStrategyType)missionParams2, targetCity, senderForce, missionParams3, missionParams4);

                            // 唯独"失败被捕"不返程：City.AddCaptive 已把本将挂成 targetCity 的囚犯并把
                            // BelongCity 置空，此时再挂返程既会在 missionTarget.Id 上空引用，
                            // 俘虏也不该自己走回己营——留在敌城俘虏名单里等处置。
                            // 其余三态（含"君主豁免导致降级为普通失败"）一律起程返回出发城。
                            if (result != CityStrategyResult.FailedCaptured)
                                SetReturnHomeMission();
                        }
                    }
                    break;
            }
        }
        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3, int p4)
        {
            SetMissionInternal(missionType, missionTarget, missionCounter, p1, p2, p3, p4);
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3)
        {
            SetMissionInternal(missionType, missionTarget, missionCounter, p1, p2, p3, 0);
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2)
        {
            SetMissionInternal(missionType, missionTarget, missionCounter, p1, p2, 0, 0);
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1)
        {
            SetMissionInternal(missionType, missionTarget, missionCounter, p1, 0, 0, 0);
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter)
        {
            SetMissionInternal(missionType, missionTarget, missionCounter, 0, 0, 0, 0);
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget)
        {
            SetMissionInternal(missionType, missionTarget, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// 任务下发的唯一实现：写字段 -> 同步"在途武将表"。
        ///
        /// 对应 Open311 的 person_change_order（末尾 set_actor）。
        /// 收口到一处是为了保证"有任务 = 在在途表里"这条不变式不会被某个重载漏掉。
        /// </summary>
        private void SetMissionInternal(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3, int p4)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget != null ? missionTarget.Id : 0;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = p3;
            this.missionParams4 = p4;
            NotifyMissionChanged();
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
            NotifyMissionChanged();
        }

        /// <summary>
        /// 任务变更后同步"在途武将表"（对应 Open311 的 set_actor / 摘除）。
        /// 有任务就登记、无任务就移除；Scenario 未就绪时静默跳过
        /// （编辑器/单测里会直接改任务，没有全局剧本上下文）。
        /// </summary>
        private void NotifyMissionChanged()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            if (missionType != 0)
                scenario.RegisterActor(this);
            else
                scenario.UnregisterActor(this);
        }

        /// <summary>
        /// 城市计略（流言 / 二虎竞食）事毕归家的收尾：挂上返程行军任务，按来路逐回合走回归属城。
        /// 耗时口径：城间行程按单程计（<see cref="City.Distance"/> 的邻接步数，面板折算成 步数×10 日），
        /// 去程与返程各占一份，所以单程 d 座城的一趟出使共占用 2d 个回合：到家发生在派遣后的第 2d-1 回合末，
        /// 第 2d 回合本势力回合开始时就能再派活。待命名单不需要在这里手工补 —— 0.2.6 起
        /// <see cref="Force.OnForceTurnStart"/> 把 <c>UpdateTurnInfo</c>（内含各城 <see cref="City.OnForceTurnStart"/>
        /// 重建 freePersons）排在武将循环之后，而 <c>ActionOver</c> 正是在武将循环里复位的，
        /// 所以"上一回合末刚恢复空闲"的武将会在同一回合被收进名单（作者修复"武将要 2 回合后才能再动"的改法）。
        /// 只给新增的计略分支用：外交仍沿用作者原本写在 <c>UpdateMission</c> 里的
        /// <c>SetMission(PersonReturn, BelongCity)</c>，不在这里改写既有指令。
        /// 归属城缺失时只清任务，绝不 <c>SetMission(PersonReturn, null)</c>：
        /// 该重载直接解引用 missionTarget，而使者被俘时 City.AddCaptive 已把 BelongCity 置空。
        /// 判空退化为清任务的写法沿用 PersonReturn 自身的失效保护（见 UpdateMission 的 PersonReturn 分支）。
        /// </summary>
        public void SetReturnHomeMission()
        {
            City homeCity = BelongCity;
            if (homeCity == null)
            {
                ClearMission();
                return;
            }

            SetMission(MissionType.PersonReturn, homeCity);
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
                        if (Father != null)
                            belongP = Father;
                        else if (Mother != null)
                            belongP = Mother;

                        if (belongP != null)
                        {
                            if (belongP.BelongForce != null)
                            {
                                BelongForce = belongP.BelongForce;
                                BelongCorps = BelongForce.CapitalCorps;
                                BelongCity = BelongForce.CapitalCity;
                                CurrentCity = BelongCity;
                                BelongCity.AddPerson(this);
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
                            city.AddInvisiblePerson(this);
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
            // 【禁止仕官】每回合递减（对应 Open311 的 ban_timer 月度衰减）。
            // 归零时清掉君主 Id，避免残留值永久挡住重新投效。
            if (BanTimer > 0)
            {
                BanTimer--;
                if (BanTimer <= 0)
                    BannedKunshuId = -1;
            }

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
                            // 【归属治理】成年出仕 = 入城（对应 Open311 的 person_debut -> person_enter）。
                            // 原先这里手写"赋三字段 + 裸 AddPerson + 裸 freePersons.Add + 手改 state"，
                            // 漏掉了在野/未发现名册的摘除，且把刚登场的人直接塞进空闲名单
                            // （他本回合还没行动过，本该等回合刷新后再入名单）。
                            City becameCity = IsPrisoner ? BelongForce?.CapitalCity : BelongCity;
                            if (becameCity == null)
                            {
                                Sango.Log.Error($"*{Name} 的儿子 {x.Name} 成年，但找不到可出仕的城池");
                                return;
                            }

                            PersonStateSystem.Enter(x, becameCity, PersonChangeReason.Appeared);
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
                    // 【修复】隐身/在野武将可能没有所在城市（登场数据异常、登场时继承的所在城为空等）。
                    // 这里自愈：补分配一座随机城并登记到对应名单，避免每回合空引用刷屏。
                    if (CurrentCity == null)
                    {
                        CurrentCity = scenario.citySet.RandomGet();
                        if (IsWild)
                            CurrentCity.AddWildPerson(this);
                        else
                            CurrentCity.AddInvisiblePerson(this);
                        Sango.Log.Error($"@人才@隐身/在野武将{Name}(Id:{Id})没有所在城市,已自动分配到{CurrentCity.Name}");
                    }

                    if (IsWild)
                        CurrentCity.wildPersons.Remove(this);
                    else
                        CurrentCity.invisiblePersons.Remove(this);

                    // 【修复】改归属前先从旧所属城的在野名单摘除。
                    // 旧所属城可能不同于所在城（例如城池陷落后人员未迁移），
                    // 直接 BelongCity = 新城 会给旧城 wildPersons 留下脏引用（双城挂名），
                    // 之后旧城的搜索 / 登庸仍可能挑到此人。
                    // （不可见武将不在 wildPersons 名单里，RemoveWildPerson 对其是无害的空移除。）
                    BelongCity?.RemoveWildPerson(this);

                    //如果在港关,移动到所属城市
                    if (!CurrentCity.IsCity())
                    {
                        City lastCity = CurrentCity;
                        City targetCity = CurrentCity.BelongCity;
                        if (targetCity == null)
                        {
                            // 港关没有隶属主城(异常数据):留在原地,只重置停留计数避免反复触发
                            Sango.Log.Error($"@人才@在野武将{Name}所在的港关{lastCity.Name}没有隶属主城,无法移动");
                            stayTurnCount = 0;
                            if (IsWild)
                                CurrentCity.AddWildPerson(this);
                            else
                                CurrentCity.AddInvisiblePerson(this);
                            return base.OnTurnEnd(scenario);
                        }
                        // 移动到新城市
                        ChangeCurrentCity(targetCity);
                        BelongCity = targetCity;

                        // 重置停留时间
                        stayTurnCount = 0;
                        Sango.Log.Info($"@人才@在野武将{Name}从{lastCity.Name}移动到{targetCity.Name}");
                    }
                    else
                    {
                        City last = CurrentCity;
                        // 随机选择一个邻接城市
                        SangoObjectList<City> neighborCities = CurrentCity.NeighborList;
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
                                Sango.Log.Info($"@人才@在野武将{Name}从{last.Name}移动到{targetCity.Name}");
                            }
                        }
                    }

                    if (IsWild)
                        CurrentCity.wildPersons.Add(this);
                    else
                        CurrentCity.AddInvisiblePerson(this);
                }
            }
            else
            {
                wildTurnCount = 0;
            }

            // 【性能】任务推进（UpdateMission）不在这里做：
            // 改由 Scenario.TurnEnd 遍历"在途武将表"驱动（对应 Open311 的 actors_）。
            // 本方法保留的是"在野/未发现武将随机游荡"这类与任务无关、必须全池扫描的逻辑。
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

        /// <summary>
        /// 迁城前的准备：处理君主/都督/太守的职务让渡。
        /// 返回 false 表示这次迁移不合法（跨势力），调用方应改走归顺流程。
        /// </summary>
        /// <param name="dest">目标城</param>
        /// <returns>是否可以继续迁移</returns>
        public bool OnWillChangeToCity(City dest)
        {
            if (dest == null)
            {
                Sango.Log.Error($"{Name} 迁城失败：目标城为空");
                return false;
            }

            if (BelongForce != dest.BelongForce && dest.BelongForce != null)
            {
                // 跨势力迁城不是"移动"，而是"归顺"，必须走 PersonStateSystem.Recruit。
                // 原先这里把 BelongForce 置 null（并紧接着解引用它）——既会空引用崩溃，
                // 又会留下"无势力的在籍武将"这种非法状态。
                Sango.Log.Error($"{BelongForce?.Name}的{Name} 尝试转到不同势力的城池 <{dest.BelongForce.Name}>，已拒绝");
                return false;
            }

            // 【原版对齐】出发只做"职务让渡"，**不改目标城的军团**。
            //
            // 依据：Open311 person_leave 里那段"君主 → 目标城改挂君主军团"，
            // 前置条件是 move == true，而所有调用点（Move / Summon / 外交 / 计略 / 录用 /
            // 部队解散回城）传入的都是 false —— 该分支实际是死代码。
            // 目标城改挂只发生在**到达时**的 person_enter（见 PersonStateSystem.Enter）。
            //
            // 早期版本在这里就把目标城改挂到君主军团，会出现"人还在路上，
            // 城已经换军团"的中间态：那座城的太守被提前重算、军团账本提前变动，
            // 途中若任务被取消（目的城易主 / 势力灭亡），城就永久留在了错的军团。
            if (IsCommander)
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
                BelongCity?.NeedUpdateLeader();
            }
            else if (IsLeader)
            {
                SetStateNormal();
                BelongCity?.NeedUpdateLeader();
            }
            else if (IsGovernor)
            {
                // 君主离城：本身不降级，但旧城若由他兼着太守要重算
                BelongCity?.NeedUpdateLeader();
            }

            return true;
        }

        public void TransformToCity(City dest)
        {
            if (dest == null || dest == BelongCity) return;

            // 跨势力迁城不是"移动"，是"归顺"，由调用方改走 PersonStateSystem.Recruit
            if (!OnWillChangeToCity(dest)) return;

            City lastCity = BelongCity;

            // 【原版对齐】出发只记"离城"，不改所属城（对应 Open311 的 person_leave）。
            // 户口（BelongCity）与在籍名册要等真正走到目的地后，
            // 由 City.OnPersonReturnCity -> PersonStateSystem.Enter 统一迁移。
            // 原先出发即改户口，会导致武将人还在路上却已经算目标城的在籍成员，
            // 被太守任命 / 人才调度 / 兵役统计提前纳入，"移动中"这个状态也失去意义。
            SetMission(MissionType.PersonReturn, dest);

            ActionOver = true;
            Sango.Log.Info($"*{BelongForce?.Name}的{Name}从{lastCity?.Name ?? "无"}向{dest.Name}转移*");
        }

        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;
            if (BelongCorps != corps)
            {
                last = BelongCorps;
                BelongCorps = corps;

                // 【锚点】势力由军团派生（Open311: Person::get_force_id = district_->force_）。
                // 原先这里只改军团不同步势力，于是 City.UpdateCorps 批量改军团之后，
                // 会出现"人在新军团、势力还是旧势力"的分裂 —— 这类数据只能靠年度巡检兜，
                // 期间 AI 出兵判断、俸禄、外交全都在读错的势力。
                Force newForce = corps != null ? corps.BelongForce : null;
                if (BelongForce != newForce)
                    BelongForce = newForce;
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
            if (last == city)
                return last;

            CurrentCity = city;
            // 日志与事件都要容忍空城：俘虏释放、城陷撤离、登用归顺等路径
            // 都会出现"从无城到有城"或"从有城到无城"的迁移
            Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所在城市 {last?.Name ?? "无"} -> {city?.Name ?? "无"}");
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

            // 目标城为空只允许"清空户口"这一种语义，且必须经 LeaveToWild 处理，
            // 否则直接解引用 city.BelongForce 会空引用（城陷撤离 / 军团解散的中间态会走到这里）
            if (city == null)
            {
                Sango.Log.Error($"*{Name} 改变所属城市失败：目标城为空");
                return BelongCity;
            }

            if (BelongCity != city || BelongForce != city.BelongForce || BelongCorps != city.BelongCorps)
            {
                last = BelongCity;
                Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所属城市 {BelongCity?.Name} => {city.Name}");
                if (!IsWild)
                {
                    BelongCity?.RemovePerson(this);
                    BelongCity = city;
                    if (BelongCorps != city.BelongCorps)
                        BelongCorps = city.BelongCorps;
                    // 【锚点】势力由军团派生（Open311: Person::get_force_id = district_->force_），
                    // 不直接读城的势力字段 —— 城若留着与军团不一致的旧势力，会把武将一起带偏。
                    Force newForce = BelongCorps != null ? BelongCorps.BelongForce : city.BelongForce;
                    if (BelongForce != newForce)
                        BelongForce = newForce;
                    city.AddPerson(this);
                }
                else
                {
                    // 在野武将只登记在野名单,不继承目标城市的势力与军团。
                    // 规则1:在野武将必须是在野状态、有所在城市、且不能有部队,
                    // 因此这里显式清空势力/军团,避免"在野却仍隶属某势力"的残留
                    BelongCity?.RemoveWildPerson(this);
                    BelongCity = city;
                    BelongCorps = null;
                    BelongForce = null;
                    city.AddWildPerson(this);
                }

                mBelongTroop?.OnPersonChangeCity(this, last, city);

                // 【太守失效的根源之一】户口迁走后，旧城的 Leader 会变成悬空引用：
                // 旧城的太守可能是本人，而"他已经不属于本城"这件事只有这里知道。
                // 原先靠每个调用方自己补 NeedUpdateLeader，漏一处就会留下
                // "*[归属巡检] xx 太守 xx 已失效（已调离）" 这类日志，且要等年度巡检才修。
                //
                // 【为什么这里要立即重算，而不是只打标记】
                //   NeedUpdateLeader 只在 City.OnForceTurnEnd 被消费，而年末巡检
                //   （Scenario.OnYearStart → GovernanceValidator）跑在"迁移"与"城市回合结束"之间，
                //   只打标记的话，先被巡检抓到一条"太守已失效"，白报一次。
                //   原版也是迁移后立刻 building_appoint_taishu(old_service, false)
                //   （s11_sys_scen_person.cpp:1025-1026），这里对齐。
                if (last != null && last != city)
                {
                    if (last.Leader == this)
                    {
                        // 本人就是旧城太守：旧城名册里已经摘掉他，立刻重选
                        last.Leader = null;
                        last.UpdateNewLeader();
                    }
                    else
                    {
                        // 其他人当太守：只需记账，交给城市回合结束统一重算
                        last.NeedUpdateLeader();
                    }
                }
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

            // 挖角代价：对"在职于其它势力"的武将下手，成败都会拉低我方与其所属势力的关系。
            // 放在分歧之前结算，保证舌战接管导致提前 return 时这笔代价也不会漏掉。
            ApplyForeignPoachPenalty(person, targetCity, type, success);

            if (success)
            {
                person.BeRecruit(this, targetCity);
            }
            else
            {
                // 登用失败：按概率强制进入舌战（概率见剧本参数 debateChanceWhenRecruitFail）。
                // 只有目标是在野武将 / 没有势力的俘虏才会触发；辩胜时由 DebateConsequence 改判为登用成功，
                // 所以要把"登用成功后目标加入的城"一并传过去。
                //
                // 【入口限定】只有普通登庸(PersonRecruitType.Normal)才接舌战入口；
                // 破城招降(OnCityFall)与灭亡势力招降(OnForceFall)不触发舌战：
                // 这条链在破城演出的 RenderEvent 里同步执行，舌战窗口叠在演出上后 AI 不出牌会卡死整场演出。
                //
                // 舌战一旦接管，这次登用的后续（功绩 / 经验 / 技术点 / 行动结束）就全交给舌战了：
                // 结算在 Debate.ParamSetWinner，行动结束由 DebateConsequence 在舌战结束时补上，
                // 所以这里立刻返回，不再往下执行；只有舌战没接管（概率没中 / 身份不符 / 已在舌战中）才继续。
                if (type == (int)PersonRecruitType.Normal
                    && Sango.Core.Debate.DebateTrigger.OnRecruitFailed(this, person, targetCity))
                    return false;
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

        /// <summary>
        /// 挖角外交代价：登庸"在职于其它势力"的武将时，拉低我方与其所属势力的关系。
        /// 目的：让"看戏等 AI 互喷把忠诚打低再去登庸"付出真实代价，而不是零成本白捡。
        /// 判定范围严格限定在普通登庸（PersonRecruitType.Normal）且目标确有存活势力：
        /// 在野武将、无势力俘虏、破城招降（OnCityFall）与灭亡势力招降（OnForceFall）都不受影响，
        /// 保持原有的招降手感。成功按 recruitForeignRelationPenalty 扣，失败被察觉按 recruitForeignRelationPenaltyFailed 扣。
        /// </summary>
        /// <param name="target">被登庸的武将，可为空</param>
        /// <param name="targetCity">登庸成功后目标加入的城，用于消息坐标兜底，可为空</param>
        /// <param name="type">登庸类型，见 PersonRecruitType</param>
        /// <param name="success">本次登庸是否成功</param>
        private void ApplyForeignPoachPenalty(Person target, City targetCity, int type, bool success)
        {
            if (target == null || target == this)
                return;
            // 只有普通登庸（直接对敌方在职武将下手）才算挖角
            if (type != (int)PersonRecruitType.Normal)
                return;

            Force myForce = BelongForce;
            Force targetForce = target.BelongForce;
            if (myForce == null || targetForce == null || targetForce == myForce)
                return;
            // 在野与俘虏不属于"别家编制"，只有仍在敌方势力内的在职武将才付代价
            if (target.IsWild || target.IsPrisoner || !targetForce.IsAlive)
                return;

            ScenarioVariables variables = Scenario.Cur != null ? Scenario.Cur.Variables : null;
            if (variables == null)
                return;
            int penalty = success ? variables.recruitForeignRelationPenalty : variables.recruitForeignRelationPenaltyFailed;
            if (penalty <= 0)
                return;

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            if (diplomacyManager == null)
                return;

            int before = diplomacyManager.GetRelation(myForce, targetForce);
            diplomacyManager.ReduceRelation(myForce, targetForce, penalty);
            int after = diplomacyManager.GetRelation(myForce, targetForce);

            Sango.Log.Info($"@登庸@{myForce.Name} 挖角 {targetForce.Name} 的 {target.Name}，{(success ? "得手" : "未果且已暴露")}，双方关系由 {before} 降至 {after}（实扣 {before - after}）");

            // 玩家自己挖角时给一条左下角消息，避免玩家看不到关系变化；AI 之间挖角不打扰玩家
            if (myForce.IsPlayer)
            {
                City anchor = BelongCity ?? targetCity;
                Sango.Core.Player.PlayerMessage.AddTextMessage(
                    $"挖角{targetForce.ColorName}的{target.ColorName}{(success ? "得手" : "未果且已暴露")}，双方关系由 {before} 降至 {after}。",
                    myForce, anchor != null ? anchor.x : 0, anchor != null ? anchor.y : 0);
            }
        }

        public void BeRecruit(Person person, City targetCity)
        {
            // 【禁止仕官】最后一道闸：候选列表已经过滤过，这里防的是
            // 剧本事件 / AI 直调 / 旧存档残留等在途绕过 UI 的接入点。
            if (IsBannedFrom(targetCity?.BelongForce))
            {
                Sango.Log.Warning($"*[登用]{Name} 半年内被 {targetCity.BelongForce.Name} 禁止仕官，本次登用无效");
                return;
            }

            Sango.Log.Info($"[{person.BelongForce.Name}]<{person.Name}>登庸成功, {Name}加入了势力{person.BelongForce.Name}");
            loyalty = 80;

            if (IsPrisoner)
            {
                BelongForce?.BeCaptiveList.Remove(this);
                // 囚犯从监牢中移除。身份转换与名册迁移交给 JoinToForce -> Recruit 统一处理，
                // 这里手改 state 会绕过名册重建，留下"身份是一般武将却仍在 captiveList 里"的脏数据
                if (mBelongTroop != null)
                    mBelongTroop.RemoveCaptive(this);
                else
                    CurrentCity?.RemoveCaptive(this);
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

            // 【归属治理】加入势力统一走 PersonStateSystem.Recruit（对应 Open311 的 person_change_force）。
            // 原先这里直接给 BelongCity/BelongCorps/BelongForce 三连赋 + 手改 state，
            // 会漏掉四件事：旧城 allPersons 的摘除、在野/未发现/俘虏名册的摘除、
            // 部队字段的清理、在途命令的取消，从而留下
            // "人在新城的名单里、旧城也还挂着一份"的双份脏数据。
            PersonStateSystem.Recruit(this, city, PersonChangeReason.Employed);

            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;

            // 主公不能被降级为一般武将,否则会出现"势力主公却是普通状态"的不一致
            if (!IsGovernor && state != (int)PersonStateType.Normal)
            {
                PersonStateSystem.SetState(this, PersonStateType.Normal, PersonChangeReason.Employed);
            }

            return isSameCity;
        }

        /// <summary>
        /// 武将登场：把未登场（Invalid）的武将落到指定城市，以在野状态出现在世界上。
        ///
        /// 与 <see cref="LeaveToWild"/> 的收尾约定保持一致：
        ///   在野武将**必须**挂靠到某个城市的在野名单（City.wildPersons）上，否则玩家搜索不到、
        ///   城池的在野人数统计也会漏掉他。先落城再改状态，因为状态的登记逻辑依赖 CurrentCity。
        ///
        /// 已登场 / 已死亡 / 已成为俘虏的武将不会被本文改变，只打一条警告。
        /// </summary>
        /// <param name="city">登场所在城市，不可为 null（在野武将必须有所在城市）</param>
        /// <returns>是否成功登场</returns>
        public bool Appear(City city)
        {
            if (city == null)
            {
                Log.Error($"武将 {Name} 登场失败：未指定所在城市");
                return false;
            }

            if (IsValid)
            {
                Log.Warning($"武将 {Name} 登场失败：已经登场");
                return false;
            }

            if (IsDead)
            {
                Log.Warning($"武将 {Name} 登场失败：武将已阵亡");
                return false;
            }

            // 【归属治理】登场 = 落到指定城并转为在野，
            // 统一走 PersonStateSystem.LeaveToWild（对应 Open311 的 person_appear -> person_set_zaiya）。
            // 内部负责：摘除残留势力/军团/部队、清命令、改身份、登记 wildPersons；
            // 原先这里手写"先落城再改 state"的顺序，容易被后续调用者改坏。
            PersonStateSystem.LeaveToWild(this, city, PersonChangeReason.Appeared);

            Log.Info($"武将 {Name} 已在 {city.Name} 登场（在野）");
            return true;
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
            // 【归属治理】身份切换走统一入口（对应 Open311 person_set_zaiya 里的 set_mibun），
            // 内部会按新旧身份重建城市侧名册（从 allPersons 摘出、登记到 wildPersons），
            // 避免"state 是野武将、人却还留在在籍名单里"的分裂
            PersonStateSystem.SetState(this, PersonStateType.Unemployed, PersonChangeReason.LeftToWild);

            CurrentCity = BelongCity;
            // 在野武将必须挂靠到某个城市的在野名单上(规则1:必须有所在城市)
            if (BelongCity != null)
            {
                BelongCity.AddWildPerson(this);
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
                // 原势力尚存:脱逃后回归原势力都城,状态复位为一般武将。
                // 【归属治理】顺序必须是"先改身份、再迁城"：
                // 此时 BelongCity 还是 null，名册重建不会误登记到关押城；
                // 随后 Enter 走正常的入城流程，把户口落到都城并在籍名单登记。
                City capital = BelongForce.CapitalCity;
                BelongForce.BeCaptiveList.Remove(this);

                if (capital != null)
                {
                    PersonStateSystem.SetState(this, PersonStateType.Normal, PersonChangeReason.Released);
                    PersonStateSystem.Enter(this, capital, PersonChangeReason.Released);
                    SetMission(MissionType.PersonReturn, BelongCity ?? capital);
                }
                else
                {
                    PersonStateSystem.SetState(this, PersonStateType.Normal, PersonChangeReason.Released);
                }
            }
            else
            {
                // 原势力已灭亡或本就无势力:转为在野。
                // 【归属治理】统一下野入口负责清势力/军团/部队并登记 wildPersons，
                // 避免"state 是在野,却仍隶属某个已灭亡势力与其军团"的不一致
                PersonStateSystem.LeaveToWild(this, CurrentCity, PersonChangeReason.Released);
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
            if (FeatureList == null || FeatureList.Count == 0) return false;
            return FeatureList.Contains(id);
        }

        /// <summary>
        /// 增减忠诚并把结果钳制在 0..100，返回钳制后的新值。
        /// 工程内既有写入点多为裸加减（如 Force.cs 的 person.loyalty -= v 会掉成负数），
        /// 而显示层只用 Math.Min(100, loyalty) 兜底上限、不兜底下限，脏数据会静默留存并影响
        /// 褒赏候选与登庸判定；因此计略类需要批量改忠诚时必须走本接口。
        /// 本期只新增该接口，不回头替换既有裸写入点，避免把技术债修复混进本需求。
        /// </summary>
        /// <param name="v">忠诚变化量，可为负</param>
        /// <returns>钳制后的最新忠诚值</returns>
        public int AddLoyalty(int v)
        {
            loyalty += v;
            if (loyalty < 0)
                loyalty = 0;
            else if (loyalty > 100)
                loyalty = 100;
            return loyalty;
        }

        /// <summary>
        /// 当前任务的目标城。
        ///
        /// 不同任务把目的地存在不同字段里（见 UpdateMission 的各个分支），
        /// 这里集中解析，供 UI 与"剩余天数"复用。
        /// </summary>
        /// <returns>目标城；没有目标或目标不是城时返回 null</returns>
        public City GetMissionTargetCity()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || missionType == 0)
                return null;

            switch ((MissionType)missionType)
            {
                // 这三个的目的地直接是城 Id
                case MissionType.PersonReturn:
                case MissionType.PersonDiplomacy:
                case MissionType.PersonCityStrategy:
                    return scenario.citySet.Get(missionTarget);

                // 登用任务：missionTarget 是被登用的武将，目的地城在 missionParams1
                case MissionType.PersonRecruitPerson:
                    return scenario.citySet.Get(missionParams1);

                default:
                    return null;
            }
        }

        /// <summary>
        /// 任务剩余天数估算（城间邻接步数 × 10 日，与项目里 DistanceDays 的口径一致）。
        ///
        /// 没有任务、没有目标城、或已经站在目标城时返回 0。
        /// 注意这是"按当前图距估算"，不是精确倒计时 —— 逐回合推进时它会自然变小。
        /// </summary>
        /// <returns>剩余天数</returns>
        public int RemainMissionDays()
        {
            if (missionType == 0)
                return 0;

            City dest = GetMissionTargetCity();
            if (dest == null)
                return 0;

            City from = CurrentCity;
            if (from == null || from == dest)
                return 0;

            int distance = from.Distance(dest);
            return distance > 0 ? distance * 10 : 0;
        }

        /// <summary>
        /// 是否处于"在路上"状态（有任务、且人不在任务的终点）。
        ///
        /// 与 IsFree 的区别：IsFree 判"能不能派活"，本项判"是不是正在赶路"。
        /// 用途是让人物面板把 PersonReturn 显示成"移动中"而不是"返回"。
        /// </summary>
        /// <returns>是否在途</returns>
        public bool IsOnTheWay()
        {
            if (missionType == 0 || mBelongTroop != null || IsPrisoner || IsDead)
                return false;

            City dest = GetMissionTargetCity();
            if (dest == null)
                return false;

            return CurrentCity != null && CurrentCity != dest;
        }

        /// <summary>
        /// 是否被指定势力"禁止仕官"（对应 Open311 的 banned_kunshu_id 读侧）。
        ///
        /// 语义：刚从该势力离开的人，半年内不能被重新登用回去。
        /// 判定以 <see cref="BanTimer"/> 为准：归零后君主 Id 会被清成 -1，
        /// 因此老存档里残留的 BannedKunshuId 不会永久生效。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <returns>是否禁止仕官</returns>
        public bool IsBannedFrom(Force force)
        {
            if (force == null || BanTimer <= 0)
                return false;
            return BannedKunshuId > 0 && BannedKunshuId == force.Governor;
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

            if (FeatureList == null)
            {
                return;
            }

            for (int i = 0; i < FeatureList.Count; i++)
            {
                Feature feature = FeatureList[i];
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

            // 装备 / 宝物的特效订阅同样要清掉，否则剧本卸载后会保留失效引用
            if (equipActionList != null)
            {
                for (int i = 0; i < equipActionList.Count; i++)
                {
                    equipActionList[i].Clear();
                }
                equipActionList.Clear();
                equipActionList = null;
            }
        }

        public bool HasFeatrue(int[] ids)
        {
            if (FeatureList == null || FeatureList.Count == 0) return false;
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (FeatureList.Contains(ids[i])) return true;
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
            City otherCity = other.mBelongTroop != null ? other.mBelongTroop.cell.BelongCity : other.BelongCity;
            City thisCity = mBelongTroop != null ? mBelongTroop.cell.BelongCity : BelongCity;
            return otherCity.Distance(thisCity);
        }

        public int DistanceDays(City otherCity)
        {
            if (otherCity == null) return 0;
            City thisCity = mBelongTroop != null ? mBelongTroop.cell.BelongCity : (BelongCity == null ? CurrentCity : BelongCity);
            return otherCity.Distance(thisCity);
        }

        public int CompatibilityDistance(Person other)
        {
            if (other == null) return 0;
            return System.Math.Abs(compatibility - (other.compatibility));
        }

        public bool IsLike(Person other)
        {
            if (other == null || LikePersonList == null) return false;
            return LikePersonList.Contains(other);
        }

        public bool IsHate(Person other)
        {
            if (other == null || HatePersonList == null) return false;
            return HatePersonList.Contains(other);
        }

        /// <summary>
        /// other是否在自己记录的兄弟列表里
        /// 注意: 本方法不保证对称! BrotherList 是 PostInit 里每个人只把自己
        /// 塞进 Brother.BrotherList 逐步拼出来的, 幼弟那份列表里不一定含组头,
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

            // 主判据: 同组的人 BrotherId 全部指向同一个组头Id
            // (Person.SwornBrothers 与 Scenario 的配表建组都是这个约定, 天生对称)
            if (BrotherId > 0 && BrotherId == other.BrotherId)
                return true;

            // 兜底一: 两人解析到的组头是同一个对象
            if (Brother != null && Brother == other.Brother)
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
        /// 同胞与义兄弟共用 BrotherId 字段无法区分, 所以兄弟姐妹一律靠父母字段判定
        /// </summary>
        public bool IsBloodRelative(Person other)
        {
            if (other == null || other == this) return false;
            if (IsParentchild(other)) return true;

            // 同一位父亲或同一位母亲即为兄弟姐妹
            if (Father != null && Father == other.Father) return true;
            if (Mother != null && Mother == other.Mother) return true;

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
                    AddAncestor(current[j].Father, result, next);
                    AddAncestor(current[j].Mother, result, next);
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
        public bool HasSpouse => SpouseList != null && SpouseList.Count > 0;

        /// <summary>
        /// 是否已属于某个义兄弟组(含剧本原设兄弟, 仲介结义)
        /// </summary>
        public bool HasSwornBrother => BrotherId > 0 || Brother != null
            || (BrotherList != null && BrotherList.Count > 0);

        /// <summary>
        /// other是否为自己的配偶
        /// </summary>
        public bool IsSpouse(Person other)
        {
            if (other == null || SpouseList == null) return false;
            return SpouseList.Contains(other);
        }

        /// <summary>
        /// 把other追加为自己的配偶, 并重建mSpouseList
        /// </summary>
        public void AddSpouse(Person other)
        {
            if (other == null || other == this) return;

            Scenario scenario = Scenario.Cur;
            List<int> ids = new List<int>();
            if (SpouseListId != null)
                ids.AddRange(SpouseListId);
            if (ids.Contains(other.Id))
                return;

            ids.Add(other.Id);
            SpouseListId = ids.ToArray();
            SpouseList = scenario.Array2ObjectList(scenario.personSet, SpouseListId);
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
            head.BrotherId = head.Id;
            head.Brother = head;

            for (int i = 0; i < group.Count; i++)
            {
                Person person = group[i];
                if (person == null) continue;

                person.BrotherId = head.Id;
                person.Brother = head;
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
            if (other.Father == this) return true;
            if (other.Mother == this) return true;
            if (Father == other) return true;
            if (Mother == other) return true;
            return false;
        }

        public void Dead()
        {
            // 必须先缓存俘虏身份:下面会立刻把 state 改写为 Dead,
            // 改写之后 IsPrisoner 恒为 false,俘虏相关的清理逻辑将永远执行不到
            bool wasPrisoner = IsPrisoner;

            // 【君主继承】必须在 SetState(Dead) 之前缓存势力与军团：
            // 改 state 后 IsGovernor 立刻为 false，末尾还会把 BelongForce / BelongCorps 都置空。
            // 军团必须一起缓存：Succeed 要把"旧第一军团"并入新君主所在军团，
            // 而那时 oldGovernor.BelongCorps 已经是 null，拿不到接收方。
            Force dyingForce = IsGovernor ? BelongForce : null;
            Corps dyingCorps = IsGovernor ? BelongCorps : null;

            // 【归属治理】身份切换走统一入口（对应 Open311 person_die 的 set_mibun(Shibou)）。
            // 内部会按"在籍 -> 死亡"自动把本人从所属城的在籍名册摘掉；
            // 下面再手工兜一遍所属城与所在城，覆盖两者不同的情况。
            PersonStateSystem.SetState(this, PersonStateType.Dead, PersonChangeReason.Died);

            if (BelongCity != null)
            {
                // 走 RemovePerson 而不是裸集合删除：它会同步摘除工作建筑的工人引用
                BelongCity.RemovePerson(this);
                BelongCity.RemoveWildPerson(this);
                BelongCity.RemoveInvisiblePerson(this);
                BelongCity.RemoveCaptive(this);

                // 死者若是该城太守，必须立即标记重算，否则 city.Leader 会指向一个死人，
                // 直到年度巡检才被发现（期间该城没有可用太守，AI 调度会跳过它）。
                BelongCity.NeedUpdateLeader();
            }

            if (CurrentCity != null)
            {
                CurrentCity.RemovePerson(this);
                CurrentCity.RemoveWildPerson(this);
                CurrentCity.RemoveInvisiblePerson(this);
                CurrentCity.RemoveCaptive(this);
            }

            if (mBelongTroop != null)
            {
                // 非俘虏必须从部队中正常摘除,保证部队主将/成员引用与武将的 mBelongTroop 双向一致
                mBelongTroop.captiveList.Remove(this);
                mBelongTroop.RemovePerson(this);

            }

            BelongForce?.BeCaptiveList.Remove(this);

            // 死亡武将不再参与任何部队与建造
            mBelongTroop = null;
            workingBuilding = null;
            BelongForce = null;
            BelongCorps = null;

            // 【修复】任务也要一并清掉。回合推进只对 IsAlive 的武将跑 UpdateMission
            // （Scenario.TurnEnd 里有 IsAlive 过滤），所以在途出使/施计的使者一旦阵亡，
            // missionType 就永远停在非 0 上：IsFree 恒为 false 还在其次，
            // 真正的问题是 CityStrategyManager.IsCityStrategyInProgress 只按 missionType 判重、
            // 不查存活 —— 死掉的使者会永久挡住同一目标城的流言/二虎竞食，外交任务同样悬挂。
            ClearMission();

            // 【君主继承】收尾之后再处理：此时本人已彻底脱离势力，
            // SelectSuccessor 不会把他算进候选。对应 Open311 person_die -> force_succeed。
            //
            // 没有这一步会出现：君主一死 mGovernor 悬空 → Force.IsAlive 返回 false
            // → 势力从 UI 与 AI 循环里消失，但城、军团、武将都还在，无法自愈。
            if (dyingForce != null)
            {
                Person successor = dyingForce.SelectSuccessor(this);
                if (successor != null)
                {
                    // 传入"死前所属军团"：Succeed 会把它并入新君主所在军团
                    dyingForce.Succeed(successor, dyingCorps);
                }
                else
                {
                    // 原版 force_succeed 在无继任者时直接 force_ruin
                    Sango.Log.Warning($"*[势力]{dyingForce.Name} 主公 {Name} 死亡且无继任者，势力灭亡");
                    dyingForce.Ruin(null, dyingForce.FirstCity(), null);
                }
            }
        }

        /// <summary>
        /// 战死接口：战场上的死亡（单挑落败身死等），是"战死"的统一入口。
        ///
        /// 免死效果（特技 #53 护卫）在派发的 <see cref="GameEvent.OnPersonCheckBattleDeath"/> 上拦截，
        /// 判定发生在**任何死亡处理之前**（与 <see cref="PersonInjury.Injure"/> 的同一套顺序约定）：
        /// 监听者把 canDie 置 false 即免于本次战死，本方法直接返回 false、武将状态一点都不动，
        /// 因此"谁先订阅"不影响结果，也不需要事后回滚。
        ///
        /// 注意区分：处决（斩首）、病逝、剧本事件的死亡都走 <see cref="Dead"/>，**不走**本接口，
        /// 所以护卫不会让同部队武将免于被斩首或剧情杀。
        /// </summary>
        /// <param name="source">致死来源（部队 / 战法 / 决斗对手等，可空；供监听者判断上下文）</param>
        /// <returns>是否真的战死（被免死 / 已死亡时返回 false）</returns>
        public bool DeadInBattle(SangoObject source = null)
        {
            // 判"是否已死"用 state 而不是 SangoObject.IsAlive：武将死亡时只把 state 置为 Dead，
            // IsAlive 这个序列化标记不会被改写（Utils.IsAlive 对武将同样是读 state）。
            if (state == (int)PersonStateType.Dead) return false;

            if (GameEvent.OnPersonCheckBattleDeath != null)
            {
                Tools.OverrideData<bool> canDie = Tools.OverrideData<bool>.Create(true);
                GameEvent.OnPersonCheckBattleDeath(this, source, canDie);
                if (!canDie.ValueAndRecycle) return false;
            }

            Dead();
            return true;
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
        /// 获取珍品（装备栏 + 宝物）的属性加成。
        ///
        /// 口径：三个装备栏与宝物的加成**全额叠加**。
        /// 这是"道具固定五维加成"的唯一出口 —— 外部通过 Command / Strength / Intelligence /
        /// Politics / Glamour 取值时已自动包含，无需各处改动（见各 RawXxx 属性）。
        /// </summary>
        /// <param name="getBonus">取单个道具某项加成的委托</param>
        /// <returns>总加成值</returns>
        private int GetEquipmentBonus(System.Func<ItemType, int> getBonus)
        {
            int bonus = 0;

            if (EquippedWeapon != null)
            {
                bonus += getBonus(EquippedWeapon);
            }

            if (EquippedArmor != null)
            {
                bonus += getBonus(EquippedArmor);
            }

            if (EquippedHorse != null)
            {
                bonus += getBonus(EquippedHorse);
            }

            if (EquippedTreasure != null)
            {
                bonus += getBonus(EquippedTreasure);
            }

            return bonus;
        }

        /// <summary>
        /// 该道具能否装进指定装备栏。
        /// 判定统一走 <see cref="ItemType.GetEquipSlot"/>：装备类按 subKind 匹配，
        /// 宝物只有武器（宝剑 / 长柄 / 暗器 / 宝弓）与名马可以上栏。
        /// </summary>
        /// <param name="item">道具</param>
        /// <param name="slot">目标栏位</param>
        /// <returns>可装备返回 true</returns>
        public bool CanEquip(ItemType item, EquipmentKind slot)
        {
            return item != null && item.GetEquipSlot() == (int)slot;
        }

        /// <summary>
        /// 装备武器（武器类装备，或宝物中的宝剑 / 长柄武器 / 暗器 / 宝弓）
        /// </summary>
        /// <param name="weapon">武器道具</param>
        /// <returns>装备成功返回 true；栏位不符返回 false</returns>
        public bool EquipWeapon(ItemType weapon)
        {
            if (!CanEquip(weapon, EquipmentKind.Weapon))
                return false;

            EquippedWeapon = weapon;
            BuildEquipActions();
            return true;
        }

        /// <summary>
        /// 装备铠甲（铠甲类装备，或日后预留的宝物铠甲分类）
        /// </summary>
        /// <param name="armor">铠甲道具</param>
        /// <returns>装备成功返回 true</returns>
        public bool EquipArmor(ItemType armor)
        {
            if (!CanEquip(armor, EquipmentKind.Armor))
                return false;

            EquippedArmor = armor;
            BuildEquipActions();
            return true;
        }

        /// <summary>
        /// 装备马匹（马匹类装备，或宝物中的名马：赤兔马、的卢等）
        /// </summary>
        /// <param name="horse">马匹道具</param>
        /// <returns>装备成功返回 true</returns>
        public bool EquipHorse(ItemType horse)
        {
            if (!CanEquip(horse, EquipmentKind.Horse))
                return false;

            EquippedHorse = horse;
            BuildEquipActions();
            return true;
        }

        /// <summary>
        /// 装备宝物（不占装备栏的那类：书籍 / 玉器 / 珍玩 / 兵符等）。
        ///
        /// 每人限 1 件；替换时旧宝物由调用方退回国库（见 ItemGrantRules），
        /// 本方法只负责换装与重建特效。
        /// </summary>
        /// <param name="treasure">宝物道具</param>
        /// <returns>装备成功返回 true；装备类或武器 / 名马类宝物应走对应装备栏，返回 false</returns>
        public bool EquipTreasure(ItemType treasure)
        {
            if (treasure == null || !treasure.IsTreasure() || treasure.NeedEquipSlot())
                return false;

            EquippedTreasure = treasure;
            BuildEquipActions();
            return true;
        }

        /// <summary>
        /// 按当前装备栏与宝物重建特效动作表。
        ///
        /// 与武将特技（<see cref="actionList"/>）**分开维护**：本方法只清 <see cref="equipActionList"/>，
        /// 不会误伤武将自身的特技订阅。
        ///
        /// 必须调用的时机（漏一处就会出现"装了装备没效果"或"卸了效果还在"）：
        ///   ① 读档完成（ResolveReferenceObjects）
        ///   ② 装备 / 卸下武器、铠甲、马匹、宝物
        ///   ③ 授予 / 没收 执行完成
        ///   ④ 该道具的初始化时机与武将特技一致（见 OnScenarioPrepare）
        /// </summary>
        public void BuildEquipActions()
        {
            if (equipActionList == null)
            {
                equipActionList = new List<ActionBase>();
            }
            else
            {
                for (int i = 0; i < equipActionList.Count; i++)
                {
                    equipActionList[i].Clear();
                }
                equipActionList.Clear();
            }

            EquippedWeapon?.InitActions(equipActionList, this);
            EquippedArmor?.InitActions(equipActionList, this);
            EquippedHorse?.InitActions(equipActionList, this);
            EquippedTreasure?.InitActions(equipActionList, this);
        }

        /// <summary>
        /// 卸下武器
        /// </summary>
        /// <returns>被卸下的武器；本来就没装备返回 null</returns>
        public ItemType UnequipWeapon()
        {
            ItemType last = EquippedWeapon;
            if (last == null)
                return null;

            EquippedWeapon = null;
            BuildEquipActions();
            return last;
        }

        /// <summary>
        /// 卸下铠甲
        /// </summary>
        /// <returns>被卸下的铠甲；本来就没装备返回 null</returns>
        public ItemType UnequipArmor()
        {
            ItemType last = EquippedArmor;
            if (last == null)
                return null;

            EquippedArmor = null;
            BuildEquipActions();
            return last;
        }

        /// <summary>
        /// 卸下马匹
        /// </summary>
        /// <returns>被卸下的马匹；本来就没装备返回 null</returns>
        public ItemType UnequipHorse()
        {
            ItemType last = EquippedHorse;
            if (last == null)
                return null;

            EquippedHorse = null;
            BuildEquipActions();
            return last;
        }

        /// <summary>
        /// 卸下宝物
        /// </summary>
        /// <returns>被卸下的宝物；本来就没有返回 null</returns>
        public ItemType UnequipTreasure()
        {
            ItemType last = EquippedTreasure;
            if (last == null)
                return null;

            EquippedTreasure = null;
            BuildEquipActions();
            return last;
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
            person.appearance = personLib.appearance;
            person.yearBorn = personLib.yearBorn;
            person.yearDead = personLib.yearDead;
            person.compatibility = personLib.compatibility & 0xFF;
            person.state = personLib.state;
            // 武将种类：PersonLib.type 存的就是 PersonTypeEnum 的取值（见 PersonLib 字段注释），
            // 不复制会让所有从武将库生成的武将都停留在 None，寿命/编成/事件过滤全部失效
            person.type = personLib.type;
            person.voice = personLib.voice;
            person.tone = personLib.tone;
            person.kanshitsu = personLib.kanshitsu;
            person.ideal = personLib.ideal;
            person.talent = personLib.talent;
            person.merit = personLib.merit;
            person.stamina = personLib.stamina;
            person.Exp = personLib.Exp;
            person.consanguinity = personLib.consanguinity;
            // 五维:PersonLib 与 Person 同为 PersonAttributeValue,直接深拷贝基础值与成长类型
            person.command.baseValue = personLib.command.baseValue;
            person.command.changeId = personLib.command.changeId;
            person.strength.baseValue = personLib.strength.baseValue;
            person.strength.changeId = personLib.strength.changeId;
            person.intelligence.baseValue = personLib.intelligence.baseValue;
            person.intelligence.changeId = personLib.intelligence.changeId;
            person.politics.baseValue = personLib.politics.baseValue;
            person.politics.changeId = personLib.politics.changeId;
            person.glamour.baseValue = personLib.glamour.baseValue;
            person.glamour.changeId = personLib.glamour.changeId;
            // 兵种适性:同为 PersonAbilityValue
            person.spearLv.baseValue = personLib.spearLv.baseValue;
            person.halberdLv.baseValue = personLib.halberdLv.baseValue;
            person.crossbowLv.baseValue = personLib.crossbowLv.baseValue;
            person.rideLv.baseValue = personLib.rideLv.baseValue;
            person.waterLv.baseValue = personLib.waterLv.baseValue;
            person.machineLv.baseValue = personLib.machineLv.baseValue;
            // 复制的是"原始适应力"，当前等级必须重算，否则没走 OnScenarioPrepare 的路径会拿到 value = 0
            person.spearLv.Update();
            person.halberdLv.Update();
            person.crossbowLv.Update();
            person.rideLv.Update();
            person.waterLv.Update();
            person.machineLv.Update();
            person.PersonalityId = personLib.PersonalityId;
            person.ArgumentationId = personLib.ArgumentationId;

            person.FatherId = personLib.FatherId;
            person.MotherId = personLib.MotherId;
            person.BrotherId = personLib.BrotherId;

            person.SpouseListId = CloneArray(personLib.SpouseListId);
            person.LikePersonListId = CloneArray(personLib.LikePersonListId);
            person.HatePersonListId = CloneArray(personLib.HatePersonListId);
            person.FeatureListId = CloneArray(personLib.FeatureListId);


            return person;
        }
    }
}
