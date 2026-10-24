using Sango.Core.Action;
using Sango.Core.Debate;
using Sango.Core.Player;
using Sango.Render;
using System;
using System.Collections.Generic;
#if SANGO_DEBUG
using System.Text;
#endif
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 城市类，继承自BuildingBase，用于管理城市的各种属性和行为
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class City : BuildingBase
    {
        public override Force BelongForce { get { return BelongCorps?.BelongForce ?? null; } set { } }

        /// <summary>
        /// AI是否完成行动
        /// </summary>
        public virtual bool AIFinished { get; set; }
        /// <summary>
        /// AI是否准备完成
        /// </summary>
        public virtual bool AIPrepared { get; set; }
        /// <summary>
        /// 对象类型，返回城市类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.City; } }
        /// <summary>
        /// 带颜色的城市名称
        /// </summary>
        public override string ColorName => $"<color=#93C86D>{Name}</color>";

        /// <summary>
        /// 粮食
        /// </summary>
        [JsonProperty] public int food;

        /// <summary>
        /// 金钱
        /// </summary>
        [JsonProperty] public int gold;

        /// <summary>
        /// 丰收剩余生效月数。
        /// 大于零时，本城粮食产量提高 50%；每个月初递减，归零后自动失效。
        /// </summary>
        [JsonProperty] public int bumperHarvestRemainingMonths;

        /// <summary>
        /// 人口
        /// </summary>
        [JsonProperty] public int population;

        /// <summary>
        /// 兵役人口
        /// </summary>
        [JsonProperty] public int troopPopulation;

        /// <summary>
        /// 工作委任类型
        /// </summary>
        [JsonProperty] public int workingAppointType;

        /// <summary>
        /// 库存
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(ItemStoreConverter))]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 本城上次产出宝物 / 装备的回合号（&lt;= 0 表示从未产出）。
        /// 与 <see cref="ScenarioVariables.itemDiscoverGain"/> 的城市冷却配合使用，见 <see cref="ForceItemDiscover"/>。
        /// </summary>
        [JsonProperty] public int lastItemDiscoverTurn = -1;

        /// <summary>
        /// 商业值
        /// </summary>
        [JsonProperty] public int commerce;

        /// <summary>
        /// 农业值
        /// </summary>
        [JsonProperty] public int agriculture;

        /// <summary>
        /// 民心
        /// </summary>
        [JsonProperty] public byte popularSupport;

        /// <summary>
        /// 治安
        /// </summary>
        [JsonProperty] public int security;

        /// <summary>
        /// 战意
        /// </summary>
        [JsonProperty] public int energy;

        /// <summary>
        /// 士气
        /// </summary>
        [JsonProperty] public int morale;

        /// <summary>
        /// 最大士气
        /// </summary>
        public int MaxMorale { get; set; }

        /// <summary>
        /// 是否有商人, 数字为兑换比例 0为没有商人
        /// </summary>
        [JsonProperty] public byte hasBusiness;

        /// <summary>
        /// 当前兵力
        /// </summary>
        [JsonProperty] public int troops;

        /// <summary>
        /// 当前伤兵
        /// </summary>
        [JsonProperty] public int woundedTroops;

        /// <summary>
        /// 可容纳兵力基础值
        /// </summary>
        [JsonProperty] public int troopsLimit;
        /// <summary>
        /// 可容纳兵力总值
        /// </summary>
        public int TroopsLimit => troopsLimit + CityLevelType.troopsLimitAdd + troopsLimitAdd;
        /// <summary>
        /// 可容纳兵力额外增加值
        /// </summary>
        int troopsLimitAdd;
        /// <summary>
        /// 仓库大小基础值
        /// </summary>
        [JsonProperty] public int storeLimit;
        /// <summary>
        /// 仓库大小总值
        /// </summary>
        public int StoreLimit => storeLimit + CityLevelType.storeLimitAdd + storeLimitAdd;
        /// <summary>
        /// 仓库大小额外增加值
        /// </summary>
        int storeLimitAdd;

        /// <summary>
        /// 金库大小基础值
        /// </summary>
        [JsonProperty] public int goldLimit;
        /// <summary>
        /// 金库大小总值
        /// </summary>
        public int GoldLimit => goldLimit + CityLevelType.goldLimitAdd + goldLimitAdd;
        /// <summary>
        /// 金库大小额外增加值
        /// </summary>
        int goldLimitAdd;

        /// <summary>
        /// 粮仓大小基础值
        /// </summary>
        [JsonProperty] public int foodLimit;
        /// <summary>
        /// 粮仓大小总值
        /// </summary>
        public int FoodLimit => foodLimit + CityLevelType.foodLimitAdd + foodLimitAdd;
        /// <summary>
        /// 粮仓大小额外增加值
        /// </summary>
        int foodLimitAdd;

        /// <summary>
        /// 城内建筑槽位
        /// </summary>
        //[JsonProperty] public int insideSlot;
        //public int InsideSlot => insideSlot + CityLevelType.insideSlotAdd;

        /// <summary>
        /// 城外建筑槽位
        /// </summary>
        //[JsonProperty] public int outsideSlot;
        //public int OutsideSlot => outsideSlot + CityLevelType.outsideSlotAdd;

        /// <summary>
        /// 村庄槽位
        /// </summary>
        //[JsonProperty] public int villageSlot;
        //public int VillageSlot => villageSlot + CityLevelType.villageSlotAdd;

        /// <summary>
        /// 基础金钱收入 基础收入 = 基础收入 * 当前商业值 / 最大商业值
        /// </summary>
        [JsonProperty] public int baseGainGold;
        public virtual int BaseGainGold => baseGainGold + CityLevelType.baseGainGoldAdd;

        /// <summary>
        /// 基础粮食收入 基础收入 = 基础粮食收入 * 当前农业值 / 最大农业值
        /// </summary>
        [JsonProperty] public int baseGainFood;
        public virtual int BaseGainFood => baseGainFood + CityLevelType.baseGainFoodAdd;

        /// <summary>
        /// 最大商业值
        /// </summary>
        [JsonProperty] public int commerceLimit;
        public int CommerceLimit => commerceLimit + CityLevelType.commerceLimitAdd;

        /// <summary>
        /// 最大农业值
        /// </summary>
        [JsonProperty] public int agricultureLimit;

        public int AgricultureLimit => agricultureLimit + CityLevelType.agricultureLimitAdd;
        /// <summary>
        /// 最大耐久基础值
        /// </summary>
        [JsonProperty] public int durabilityLimit;
        /// <summary>
        /// 最大耐久额外增加值
        /// </summary>
        int durabilityLimitAdd;

        /// <summary>
        /// 最大耐久总值
        /// </summary>
        public override int DurabilityLimit => durabilityLimit + CityLevelType.durabilityLimitAdd + durabilityLimitAdd;

        /// <summary>
        /// 是否满兵
        /// </summary>
        public bool TroopsIsFull => troops /*+ woundedTroops*/ >= TroopsLimit;

        /// <summary>
        /// 太守
        /// </summary>
        //[JsonConverter(typeof(Id2ObjConverter<Person>))]
        //[JsonProperty]
        public Person Leader;

        /// <summary>
        /// 所属州的 id（存档数值），读取 province 时按需解析
        /// </summary>
        [JsonProperty("province")]
        public int ProvinceId;

        Province mProvince;
        public Province province
        {
            get
            {
                if (mProvince == null && ProvinceId > 0) mProvince = IdRef.Resolve<Province>(ProvinceId);
                return mProvince;
            }
            set { mProvince = value; ProvinceId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 相邻城市
        /// </summary>
        // 序列化形态保持 int[]（键名不变，老存档可读）；解析/回写并入本类已有的
        // OnScenarioPrepare / OnScenarioSave（避免重复定义 CS0111）。
        [JsonProperty("NeighborList")]
        public int[] NeighborList_list;
        public SangoObjectList<City> NeighborList = new SangoObjectList<City>();

        /// <summary>
        /// 城市等级数据的 id（存档数值），读取 CityLevelType 时按需解析
        /// </summary>
        [JsonProperty("CityLevelType")]
        public int CityLevelTypeId;

        CityLevelType mCityLevelType;
        /// <summary>
        /// 城市等级数据；写入时自动同步 CityLevelTypeId
        /// </summary>
        public CityLevelType CityLevelType
        {
            get
            {
                if (mCityLevelType == null && CityLevelTypeId > 0) mCityLevelType = IdRef.Resolve<CityLevelType>(CityLevelTypeId);
                return mCityLevelType;
            }
            set { mCityLevelType = value; CityLevelTypeId = value != null ? value.Id : 0; }
        }



        /// <summary>
        /// 工作计数
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> jobCounter = new Dictionary<int, int>();

        /// <summary>
        /// 最近一次被流言命中并生效的回合（Scenario.TurnCount）。小于等于 0 表示从未被流言命中。
        /// 用于流言的同城冷却（ScenarioVariables.cityStrategyRumorImmunityTurns），随存档序列化。
        /// 判定方式沿用 Force 的计略记录：只比较绝对回合数，不逐回合递减，因此不怕读档顺序。
        /// </summary>
        [JsonProperty] public int lastRumorTurn;

        /// <summary>
        /// 本城当前是否处于流言免疫窗内：窗口内再被流言命中不产生忠诚与治安效果。
        /// </summary>
        /// <returns>处于免疫窗内返回 true；无冷却配置或从未被命中时返回 false</returns>
        public bool IsRumorImmune()
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario != null ? scenario.Variables : null;
            int window = variables != null ? variables.cityStrategyRumorImmunityTurns : 0;
            if (window <= 0 || lastRumorTurn <= 0 || scenario == null)
                return false;
            return (scenario.TurnCount - lastRumorTurn) < window;
        }

        /// <summary>
        /// 记下本城刚被流言命中，开启免疫窗。
        /// </summary>
        public void MarkRumorHit()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario != null)
                lastRumorTurn = scenario.TurnCount;
        }

        //public List<Building> villageList = new List<Building>();
        /// <summary>
        /// 港口列表
        /// </summary>
        public List<Port> portList = new List<Port>();
        /// <summary>
        /// 关卡列表
        /// </summary>
        public List<Gate> gateList = new List<Gate>();

        /// <summary>
        /// 总粮食收入
        /// </summary>
        public int totalGainFood = 0;
        /// <summary>
        /// 总金钱收入
        /// </summary>
        public int totalGainGold = 0;

        /// <summary>
        /// 额外的影响倍率(事件等)
        /// </summary>
        public float extraGainFoodFactor = 0;
        /// <summary>
        /// 额外的金钱收入倍率
        /// </summary>
        public float extraGainGoldFactor = 0;
        /// <summary>
        /// 额外的人口增长倍率
        /// </summary>
        public float extraPopulationFactor = 1;

        /// <summary>
        /// 人口增长因子
        /// </summary>
        public float population_increase_factor = 0;

        /// <summary>
        /// 人口上限
        /// </summary>
        public int PopulationLimit => Scenario.Cur.Variables.populationLimitBase + CityLevelType.Id * Scenario.Cur.Variables.populationLimitPerLevel;

        /// <summary>
        /// 最大兵役人口
        /// </summary>
        public int MaxTroopPopulation => (int)(population * Scenario.Cur.Variables.maxTroopPopulationRatio);

        /// <summary>
        /// 基础兵役人口
        /// </summary>
        public int BaseTroopPopulation => (int)(population * Scenario.Cur.Variables.baseTroopPopulationRatio);
        /// <summary>
        /// 边界线
        /// </summary>
        internal int borderLine;
        /// <summary>
        /// 是否为边界城市
        /// </summary>
        public bool IsBorderCity => borderLine == 0;
        /// <summary>
        /// 边界线值
        /// </summary>
        public int BorderLine => borderLine;

        /// <summary>
        /// 空闲人员列表
        /// </summary>
        public List<Person> freePersons = new List<Person>();

        /// <summary>
        /// 在野人员列表
        /// </summary>
        public List<Person> wildPersons = new List<Person>();

        /// <summary>
        /// 不可见人员列表
        /// </summary>
        public List<Person> invisiblePersons = new List<Person>();

        /// <summary>
        /// 其他经过人员
        /// </summary>
        public List<Person> otherPersons = new List<Person>();

        /// <summary>
        /// 所有武将
        /// </summary>
        public SangoObjectList<Person> allPersons = new SangoObjectList<Person>();

        /// <summary>
        /// 俘虏（运行期容器，不参与序列化）
        /// </summary>
        public SangoObjectList<Person> captiveList = new SangoObjectList<Person>();

        /// <summary>
        /// 空闲人员数量
        /// </summary>
        public int FreePersonCount => freePersons.Count;

        public Vector3 CityScale
        {
            get
            {
                if (BuildingType.kind == 1)
                    return Vector3.one;
                else
                    return Vector3.one * 0.75f;
            }
        }

        //public int eventId;
        //public int model_wall;
        //public int model_city;

        /// <summary>
        /// 城特产（0 枪 / 1 戟 / 2 弩 / 3 马 / 4 兵器 / 5 水军 / 6 大都市）。
        ///
        /// 对齐出处：Open311 `s11_enum_scen.h:105-112` 的 TokusanEnum 与 `City::get_tokusan()`。
        /// 数据来源：剧本 `Scenario.json` 里每座城的 "specialty" 字段（一直是有的，
        /// 此前 City 类没有同名字段，Newtonsoft 反序列化时被静默丢弃）。
        /// 用途：最低内政设施配置（兵器→工房、马→厩舍、水军→造船厂）、
        /// 内政建设 AI 的特产加权、兵装生产的适性权重。
        /// </summary>
        [JsonProperty("specialty")]
        public int specialty;

        /// <summary>
        /// 粮价指数（30~70，**越大粮越便宜**）。
        ///
        /// 对齐出处：Open311 `City::rate_`（s11_scen.h:483）与
        /// `System::city_update_rate`（s11_sys_scen_city.cpp:110-179）。
        /// 用途：商人 AI 的惜售判断（`get_rate() &gt; 50` 即粮便宜时不出售）、交易价格的分母。
        /// <para>
        /// 【方向】它是交易公式的分母：rate 越大，同样粮食换到的金越少（粮越便宜）。
        /// 原版季节表 7 月 60~70（秋收前反而便宜）、疫病/蝗虫 30~40（灾年贵）都符合这个方向。
        /// </para>
        /// <para>
        /// 注意与 <c>hasBusiness</c> 的区别：后者是"商人兑换比例"（剧本固定值，
        /// 越大越划算买），本字段是逐月波动的粮价，两者不是同一个量。
        /// </para>
        /// </summary>
        /// <summary>
        /// 灾害类型。对齐 Open311 `DisasterType_Enum`（s11_enum_scen.h:129-142）。
        /// </summary>
        public enum DisasterType : int
        {
            /// <summary>疫病</summary>
            Ekibyou = 0,
            /// <summary>蝗虫</summary>
            Inago = 1,
            /// <summary>丰收</summary>
            Housaku = 2,
            /// <summary>类型总数</summary>
            Max = 3,
        }

        /// <summary>
        /// 本城当前灾害位集（bit N = <see cref="DisasterType"/> 的 N 号灾害）。
        /// 对齐出处：Open311 `City::disaster_`（s11_scen.h:495）。
        /// <para>
        /// 【当前状态】位集与读写 API 已就位，粮价修正已接通；
        /// 但**灾害触发**（月度按概率置位 `nextDisaster`、月末转正）尚未实现 ——
        /// 那属于独立的灾害子系统（原版在 `s11_game_month.cpp` 里按民心 / 治安抽样）。
        /// 因此目前 `disasterBits` 恒为 0，粮价走"无灾害"分支。
        /// </para>
        /// </summary>
        [JsonProperty("disaster")]
        public int disasterBits;

        /// <summary>
        /// 下月灾害位集（月末转正为 <see cref="disasterBits"/>）。
        /// 对齐出处：Open311 `City::next_disaster_`（s11_scen.h:496）。
        /// </summary>
        [JsonProperty("nextDisaster")]
        public int nextDisasterBits;

        /// <summary>
        /// 是否处于某类灾害中。
        /// 对齐出处：s11_scen_obj_city.cpp 的 `has_disaster`。
        /// </summary>
        /// <param name="type">灾害类型</param>
        /// <returns>处于该灾害返回 true</returns>
        public bool HasDisaster(DisasterType type, bool next = false)
        {
            int bit = (int)type;
            if (bit < 0 || bit >= 32)
                return false;
            int bits = next ? nextDisasterBits : disasterBits;
            return (bits & (1 << bit)) != 0;
        }

        /// <summary>
        /// 设置 / 清除某类灾害。
        /// 对齐出处：s11_scen_obj_city.cpp 的 `set_disaster` / `set_next_disaster`。
        /// </summary>
        /// <param name="type">灾害类型</param>
        /// <param name="value">true = 置位，false = 清除</param>
        /// <param name="next">true 操作下月位集，false 操作当前位集</param>
        public void SetDisaster(DisasterType type, bool value, bool next = false)
        {
            int bit = (int)type;
            if (bit < 0 || bit >= 32)
                return;
            if (next)
            {
                if (value) nextDisasterBits |= 1 << bit;
                else nextDisasterBits &= ~(1 << bit);
            }
            else
            {
                if (value) disasterBits |= 1 << bit;
                else disasterBits &= ~(1 << bit);
            }
        }

        [JsonProperty("rate")]
        public int rate = 50;

        /// <summary>
        /// 粮价的可读文本（供玩家提示 / UI 绑定使用）。
        ///
        /// 【方向】`rate` 越大粮越**便宜**（原版交易公式的分母，见 <see cref="JobTradeFood"/> 注释）：
        /// rate=1 时 100 粮要 1000 金，rate=100 时只要 10 金。
        /// </summary>
        /// <returns>形如「粮价 55（平稳）」</returns>
        public string GetRateText()
        {
            string level;
            if (rate >= 60) level = "充裕";      // rate 大 → 粮便宜
            else if (rate <= 40) level = "紧缺";  // rate 小 → 粮贵
            else level = "平稳";
            return $"粮价 {rate}（{level}）";
        }

        /// <summary>
        /// 当前与下月灾害的可读文本（供玩家提示 / UI 绑定使用）。
        /// </summary>
        /// <returns>没有任何灾害时返回 null</returns>
        public string GetDisasterText()
        {
            List<string> current = new List<string>();
            List<string> next = new List<string>();
            for (int i = 0; i < (int)DisasterType.Max; i++)
            {
                DisasterType type = (DisasterType)i;
                if (HasDisaster(type))
                    current.Add(DisasterName(type));
                if (HasDisaster(type, true))
                    next.Add(DisasterName(type));
            }

            if (current.Count == 0 && next.Count == 0)
                return null;

            string text = null;
            if (current.Count > 0)
                text = "当前灾害：" + string.Join("、", current);
            if (next.Count > 0)
            {
                string nextText = "下月预报：" + string.Join("、", next);
                text = text != null ? text + "；" + nextText : nextText;
            }
            return text;
        }

        /// <summary>灾害类型的中文名</summary>
        /// <param name="type">灾害类型</param>
        /// <returns>中文名</returns>
        public static string DisasterName(DisasterType type)
        {
            switch (type)
            {
                case DisasterType.Ekibyou: return "疫病";
                case DisasterType.Inago: return "蝗灾";
                case DisasterType.Housaku: return "丰收";
                default: return "灾害";
            }
        }

        /// <summary>粮价下限 —— 对齐原版 clamp(rate, 30, 70)</summary>
        public const int MinRate = 30;

        /// <summary>粮价上限 —— 对齐原版 clamp(rate, 30, 70)</summary>
        public const int MaxRate = 70;

        /// <summary>
        /// 更新粮价。
        ///
        /// 对齐出处：s11_sys_scen_city.cpp:110-179 city_update_rate。
        /// 两条路径：
        ///   · <paramref name="usePreset"/> = true：按月份预设表取值（开局 / 剧本载入时用）；
        ///   · false：按灾害与随机漂移（每月结算时用）。
        /// <para>
        /// 灾害修正已接通（疫病 / 蝗虫 → 30~40，丰收 → 60~70），
        /// 但**灾害本身不会被触发** —— 触发属于独立的灾害子系统（原版在月度结算里抽样置位），
        /// 本项目尚未实现，因此 `disasterBits` 目前恒为 0，该分支暂不会命中。
        /// </para>
        /// </summary>
        /// <param name="month">月份（1~12）</param>
        /// <param name="usePreset">是否用预设表</param>
        public void UpdateRate(int month, bool usePreset)
        {
            if (usePreset)
            {
                if (month < 1 || month > 12)
                    return;

                // 表格结构 {平准, 步长, 档数}，取值 = 平准 + 步长 × rand(档数) —— :120-133
                int[,] table =
                {
                    { 50, 10, 2 },  // 1 月  50..60
                    { 40, 10, 3 },  // 2 月  40..60
                    { 40, 10, 3 },  // 3 月  40..60
                    { 40, 10, 2 },  // 4 月  40..50
                    { 40, 10, 2 },  // 5 月  40..50
                    { 30, 10, 3 },  // 6 月  30..50
                    { 60, 10, 2 },  // 7 月  60..70
                    { 50, 10, 3 },  // 8 月  50..70
                    { 50, 10, 3 },  // 9 月  50..70
                    { 50, 10, 3 },  // 10 月 50..70
                    { 50, 10, 2 },  // 11 月 50..60
                    { 50, 10, 2 },  // 12 月 50..60
                };
                int index = month - 1;
                rate = table[index, 0] + table[index, 1] * GameRandom.Range(0, table[index, 2]);
                return;
            }

            // 灾害修正 —— :140-147
            // 注意：原版把「丰收」也列进灾害枚举，且丰收时粮价反而取 60~70（偏高）。
            // 这段按原版**逐字**对齐，未按常识反向修正 —— 若实跑发现粮价走势怪，
            // 问题在原版数据语义，不在这里。
            if (HasDisaster(DisasterType.Ekibyou) || HasDisaster(DisasterType.Inago))
            {
                rate = 10 * (3 + GameRandom.Range(0, 2));           // 30..40
                return;
            }
            if (HasDisaster(DisasterType.Housaku))
            {
                rate = 10 * (6 + GameRandom.Range(0, 2));           // 60..70
                return;
            }

            int value = rate;
            if (value == MinRate)
            {
                value = GameRandom.Chance(50) ? 30 : 40;
            }
            else if (value == MaxRate)
            {
                value = GameRandom.Chance(50) ? 60 : 70;
            }
            else if (value > 40)
            {
                if (value < 60)
                    value += 10 * GameRandom.Range(0, 3) - 10;      // -10 / 0 / +10
                else if (GameRandom.Chance(40))
                    value -= 10;
                else if (GameRandom.Chance(40))
                    value += 10;
            }
            else if (GameRandom.Chance(40))
            {
                value += 10;
            }
            else if (GameRandom.Chance(40))
            {
                value -= 10;
            }

            rate = System.Math.Clamp(value, MinRate, MaxRate);
        }
        /// <summary>
        /// 虚拟战斗力
        /// </summary>
        internal int virtualFightPower;
        /// <summary>
        /// 战斗力是否已更新
        /// </summary>
        internal bool isUpdatedFightPower;
        /// <summary>
        /// 所有攻击部队
        /// </summary>
        public List<Troop> allAttackTroops = new List<Troop>();
        /// <summary>
        /// 所有部队
        /// </summary>
        public List<Troop> allTroops = new List<Troop>();

        /// <summary>
        /// 城池直辖范围cell
        /// </summary>
        public List<Cell> areaCellList = new List<Cell>();

        /// <summary>
        /// 添加城池直辖范围的cell
        /// </summary>
        /// <param name="cell">要添加的cell</param>
        public void AddAreaCell(Cell cell)
        {
            areaCellList.Add(cell);
            if (BelongCity != null)
            {
                BelongCity.AddAreaCell(cell);
            }
        }

        /// <summary>
        /// 所有内城设施
        /// </summary>
        //public int[] innerSlot;

        /// <summary>
        /// 已激活的部队类型
        /// </summary>
        public List<TroopType> activedTroopType = new List<TroopType>();
        /// <summary>
        /// 战斗力
        /// </summary>
        int fightPower = 0;

        /// <summary>
        /// 到相邻城市的道路缓存
        /// </summary>
        public Dictionary<City, List<Cell>> raodToNeighborCache = new Dictionary<City, List<Cell>>();

        /// <summary>
        /// 所有设施
        /// </summary>
        public SangoObjectList<Building> allBuildings = new SangoObjectList<Building>();
        /// <summary>
        /// 建筑数量映射
        /// </summary>
        public Dictionary<int, int> buildingCountMap = new Dictionary<int, int>();


        //public SangoObjectList<Building> allIntriorBuildings = new SangoObjectList<Building>();
        //public Dictionary<int, int> buildingCountMap = new Dictionary<int, int>();

        /// <summary>
        /// AI指令集
        /// </summary> 
        public List<System.Func<City, Scenario, bool>> AICommandList = new List<System.Func<City, Scenario, bool>>();

        /// <summary>
        /// AI 命令队列的读取游标。
        ///
        /// 旧实现每执行完一条命令就 <c>AICommandList.RemoveAt(0)</c>，整队跑完是 O(n²) 次数组搬移。
        /// 改成游标前进、跑完整队后一次性 <c>Clear</c>，摊还成 O(n)（与 RenderEvent 的事件队列同一取舍）。
        /// 语义不变：命令返回 false 时游标停在原地，下次进入从同一条命令继续。
        /// </summary>
        int aiCommandIndex = 0;

        /// <summary>
        /// 防御cell列表
        /// </summary>
        public List<Cell> defenceCellList = new List<Cell>();
        /// <summary>
        /// 内城cell列表
        /// </summary>
        public List<Cell> interiorCellList = new List<Cell>();

        /// <summary>
        /// 内城cell数量
        /// </summary>
        public int InteriorCellCount => interiorCellList.Count;

        bool needUpdateLeader = false;

        /// <summary>
        /// 只有武将特性类型为 7(收入), 8(灾害)才会在都市中生效
        /// </summary>
        public List<ActionBase> actionList = new List<ActionBase>();

        public List<City> subCities = new List<City>();// = new List<Cell>();

        /// <summary>
        /// 攻击部队数量
        /// </summary>
        public int AttackTroopsCount
        {
            get
            {
                //int troopsCount = 0;
                //SangoObjectSet<Troop> troopSet = Scenario.Cur.troopsSet;
                //for (int i = 0; i < troopSet.Count; i++)
                //{
                //    Troop troop = troopSet[i];
                //    if (troop != null && troop.IsAlive && troop.BelongCityId == this)
                //        troopsCount++;
                //}
                //return troopsCount;
                return allAttackTroops.Count;
            }
        }

        /// <summary>
        /// 本城正在执行**进攻任务**（TroopOccupyCity）的部队数量。
        ///
        /// 与 <see cref="AttackTroopsCount"/>（所有在外的战斗部队，含求援 / 撤离中的部队）不同，
        /// 该值用于判断"还需要补派多少支进攻部队"，避免去求援或撤离中的部队仍占用进攻名额，
        /// 导致城市误判"已经派够"而不再补派。
        /// </summary>
        public int AttackingTroopsCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < allAttackTroops.Count; i++)
                {
                    Troop t = allAttackTroops[i];
                    if (t != null && t.IsAlive && t.missionType == (int)MissionType.TroopOccupyCity)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 按部队池重建本城的 <see cref="allTroops"/> / <see cref="allAttackTroops"/>。
        ///
        /// 【为什么需要】这两个名单是**运行期缓存**，不参与存档（City 用 OptIn 序列化），
        /// 读档后一律为空；之后只有"新组建 / 入城 / 换旗"三条路径会往里加。
        /// 于是一直在外的部队会从城市面板与 AI 统计里消失，直到它回城一次 ——
        /// 表现就是 allTroops 的数量比实际少。
        ///
        /// 【原版对齐】原版没有这份缓存：System::get_service_unit_list(const Building*)
        /// 每次都是遍历 unit 池现算（见 s11_sys_scen_unit.cpp）。本项目为了性能保留缓存，
        /// 用"剧本准备完成 + 势力回合开始"两个时机重建，逼近原版的"永远现算"语义。
        ///
        /// 【归属口径】只认 <see cref="Troop.BelongCity"/>（主将的所属城，实时派生），
        /// **不看部队当前站在哪个格子** —— allTroops 是"本城派出的部队"，不是"停在城里的部队"。
        /// </summary>
        public void RebuildTroopLists()
        {
            allTroops.Clear();
            allAttackTroops.Clear();

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.troopsSet == null)
                return;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive)
                    continue;

                // 只按所属城归类：部队在外作战时 BelongCity 仍然是出发城
                if (troop.BelongCity != this)
                    continue;

                allTroops.Add(troop);
                if (troop.IsListedAsAttackTroop)
                    allAttackTroops.Add(troop);
            }
        }

        /// <summary>
        /// 增加工作计数
        /// </summary>
        /// <param name="jobId">工作ID</param>
        /// <returns>增加后的工作计数</returns>
        public int AddJobCounter(int jobId)
        {
            if (jobCounter.TryGetValue(jobId, out var job))
            {
                job++;
                jobCounter[jobId] = job;
                return job;
            }
            else
            {
                jobCounter.Add(jobId, 1);
                return 1;
            }
        }

        /// <summary>
        /// 获取工作计数
        /// </summary>
        /// <param name="jobId">工作ID</param>
        /// <returns>工作计数</returns>
        public int GetJobCounter(int jobId)
        {
            if (jobCounter.TryGetValue(jobId, out var job))
            {
                return job;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 增加金钱
        /// </summary>
        /// <param name="v">增加的金钱数量</param>
        /// <returns>增加后的金钱数量</returns>
        public int AddGold(int v)
        {
            if (v == 0) return gold;
            gold += v;
            if (gold > GoldLimit)
                gold = GoldLimit;
            else if (gold < 0)
                gold = 0;
            return gold;
        }

        /// <summary>
        /// 增加粮食
        /// </summary>
        /// <param name="v">增加的粮食数量</param>
        /// <returns>增加后的粮食数量</returns>
        public int AddFood(int v)
        {
            if (v == 0) return food;
            food += v;
            if (food > FoodLimit)
                food = FoodLimit;
            else if (food < 0)
                food = 0;
            return food;
        }

        /// <summary>
        /// 增加兵力
        /// </summary>
        /// <param name="v">增加的兵力数量</param>
        /// <returns>增加后的兵力数量</returns>
        public int AddTroops(int v)
        {
            if (v == 0) return troops;
            troops += v;
            if (troops > TroopsLimit)
                troops = TroopsLimit;
            else if (troops < 0)
                troops = 0;
            return troops;
        }

        /// <summary>
        /// 增加物品
        /// </summary>
        /// <param name="itemTypeId">物品类型ID</param>
        /// <param name="v">增加的数量</param>
        /// <returns>增加后的物品数量</returns>
        public int AddItem(int itemTypeId, int v)
        {
            return itemStore.Add(itemTypeId, v);
        }

        /// <summary>
        /// 增加治安
        /// </summary>
        /// <param name="v">增加的治安值</param>
        /// <returns>增加后的治安值</returns>
        public int AddSecurity(int v)
        {
            security += v;
            if (security > 100)
                security = 100;
            else if (security < 0)
                security = 0;
            return security;
        }

        /// <summary>
        /// 增加士气
        /// </summary>
        /// <param name="v">增加的士气值</param>
        /// <returns>增加后的士气值</returns>
        public int AddMorale(int v)
        {
            morale += v;
            if (morale > MaxMorale)
                morale = MaxMorale;
            else if (morale < 0)
                morale = 0;
            return morale;
        }

        /// <summary>
        /// 获取到相邻城市的道路
        /// </summary>
        /// <param name="city">目标城市</param>
        /// <returns>道路的cell列表</returns>
        public List<Cell> GetRoadToNeighbor(City city)
        {
            List<Cell> list;
            if (!raodToNeighborCache.TryGetValue(city, out list))
            {
                list = new List<Cell>();
                Scenario.Cur.Map.GetDirectPath(CenterCell, city.CenterCell, list);
                raodToNeighborCache.Add(city, list);
                city.raodToNeighborCache.Add(this, list);
            }
            return list;
        }

        /// <summary>
        /// 战斗力
        /// </summary>
        public int FightPower => fightPower;

        /// <summary>
        /// 添加一般武将
        /// </summary>
        /// <param name="person"></param>
        public void AddPerson(Person person)
        {
            if (person.BelongForce != BelongForce)
            {
                Sango.Log.Error($"调用栈: {new System.Diagnostics.StackTrace(true)}");
                Sango.Log.Error($"*{Name}{BelongForce?.Name} -> allPersons 添加 {person.Name}{person.BelongForce?.Name} 势力不一致!!");
                person.BelongForce = BelongForce;
                person.BelongCorps = BelongCorps;
                // 换势力时复位褒赏标记 —— 对齐原版 person_change_force 的 set_bonus_done(false)
                // （s11_sys_scen_person.cpp:748-753）
                person.SetBonusDone(false);
            }

            if (person.BelongCorps != BelongCorps)
            {
                Sango.Log.Error($"调用栈: {new System.Diagnostics.StackTrace(true)}");
                Sango.Log.Error($"*{Name}{BelongCorps?.Name} -> allPersons 添加 {person.Name}{person.BelongCorps?.Name} 军团不一致!!");
                person.BelongForce = BelongForce;
                person.BelongCorps = BelongCorps;
                person.SetBonusDone(false);
            }

            // 【归属兜底】上面的自愈只修正了武将身上的势力 / 军团，若他的"所属城"还挂在别处，
            // 旧城的 allPersons 会留下脏引用：面板人数、人才搜索、任务指派仍会挑到他，
            // 而且下次 RemovePerson 会在错误的城里做移除（真正该摘的那份永远摘不掉）。
            // 因此这里把户口一并迁到本城。
            if (person.BelongCity != this)
            {
                person.BelongCity?.RemovePerson(person);
                person.BelongCity = this;
            }

#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> allPersons 添加 {person.Name} ");
#endif
            // 去重：读档重登记 / 重复入城等路径可能对同一个人重复调用 AddPerson，
            // 名单里出现两份会让城市人数统计与遍历逻辑凭空翻倍。
            if (!allPersons.Contains(person))
                allPersons.Add(person);
        }

        /// <summary>
        /// 移除一般武将
        /// </summary>
        /// <param name="person"></param>
        public void RemovePerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> allPersons 删除 {person.Name} ");
#endif
            if (person.workingBuilding != null)
            {
                if (person.workingBuilding.Workers != null)
                    person.workingBuilding.Workers.Remove(person);
                person.workingBuilding = null;
            }

            allPersons.Remove(person);
            freePersons.Remove(person);
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person AddCaptive(Person person, bool breakCircal = false)
        {
            if (person == null)
                return null;

            // 规则：势力尚存的君主不可被俘虏(收押/登用)，只能被"释放"或"斩首"。
            // 用 IsActiveGovernor：势力已灭亡的君主下野后是普通在野武将，可被收押/招募。
            if (person.IsActiveGovernor)
            {
                Sango.Log.Error($"*{Name} -> 拒绝收押君主 {person.Name}（势力尚存，只能被释放或斩首）");
                return null;
            }

            // 【归属治理】俘虏处理统一走 PersonStateSystem.ToCaptive（对应 Open311 的 System::capture）。
            // 原先这里手写了一套"改 state + 摘名册 + 置空部队"的流程，与 Troop.AddCaptive、
            // City.OnFall 的俘虏分支各写一遍，三处口径不一致时就会出现
            // "人在 captiveList 但 state 还是 Normal""部队字段被直接置空留下反向脏引用"。
            PersonStateSystem.ToCaptive(person, this, PersonChangeReason.Captured);

#if SANGO_DEBUG
            Sango.Log.Info($"@人才@[{person.Name}]被<{BelongForce?.Name}>俘虏至{Name}");
#endif
            return person;
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person RemoveCaptive(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> captiveList 删除 {person.Name} ");
#endif
            captiveList.Remove(person);
            person.BelongForce?.BeCaptiveList.Remove(person);
            return person;
        }

        public City RandomNerghbor()
        {
            if (BelongCity != null)
                return BelongCity.RandomNerghbor();
            return NeighborList[GameRandom.Range(0, NeighborList.Count)];
        }

        /// <summary>
        /// 添加在野武将
        /// </summary>
        /// <param name="person"></param>
        public void AddWildPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> wildPersons 添加 {person.Name} ");
#endif
            // 去重：登用/登场/下野/读档重登记等路径可能对同一个人重复调用，
            // 名单里出现两份会让"在野人数"统计与人才搜索凭空翻倍
            if (!wildPersons.Contains(person))
                wildPersons.Add(person);
        }

        /// <summary>
        /// 移除在野武将
        /// </summary>
        /// <param name="person"></param>
        public void RemoveWildPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> wildPersons 删除 {person.Name} ");
#endif
            wildPersons.Remove(person);
        }

        /// <summary>
        /// 添加未发现武将
        /// </summary>
        /// <param name="person"></param>
        public void AddInvisiblePerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> invisiblePersons 添加 {person.Name} ");
#endif
            // 去重：读档重登记 / 年度登场重入都会走到这里
            if (!invisiblePersons.Contains(person))
                invisiblePersons.Add(person);
        }

        /// <summary>
        /// 移除未发现武将
        /// </summary>
        /// <param name="person"></param>
        public void RemoveInvisiblePerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> invisiblePersons 删除 {person.Name} ");
#endif
            invisiblePersons.Remove(person);
        }

        /// <summary>
        /// 添加其他过路武将
        /// </summary>
        /// <param name="person"></param>
        public void AddOtherPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> otherPersons 添加 {person.Name} ");
#endif
            otherPersons.Add(person);
        }

        /// <summary>
        /// 移除其他过路武将
        /// </summary>
        /// <param name="person"></param>
        public void RemoveOtherPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> otherPersons 删除 {person.Name} ");
#endif
            otherPersons.Remove(person);
        }

        /// <summary>
        /// 更新已激活的部队类型
        /// </summary>
        public void UpdateActiveTroopTypes()
        {
            activedTroopType.Clear();
            Scenario.Cur.CommonData.TroopTypes.ForEach(x =>
            {
                //if (x.activeCondition == null || (x.activeCondition != null && x.activeCondition.Check(null)))
                //    activedTroopType.Add(x);
            });
        }

        /// <summary>
        /// 场景准备时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            isComplate = true;

            // 粮价按剧本当前月份做一次预设取值（原版 city_update_rate(use_preset=true)
            // 在剧本载入时调用一次，之后每月刷新由 Scenario.UpdateCityRate 承担）。
            // 不初始化的话，开局第一个月之前 rate 会是字段默认值 50，商人 AI 判断会失真。
            int startMonth = scenario.Info != null ? scenario.Info.month : 1;
            UpdateRate(startMonth, true);

            if (durabilityLimit == 0)
            {
                Sango.Log.Error($"城池:{Name} 坐标:<{x},{y}>的最大耐久为0!! 默认修复为3000");
                durabilityLimit = 3000;
            }

            //innerSlot = new int[InsideSlot];
            if (durability <= 0)
                durability = DurabilityLimit;

            // 地格占用
            OccupyCellList = new List<Cell>();
            scenario.Map.GetSpiral(x, y, BuildingType.radius, OccupyCellList);
            foreach (Cell cell in OccupyCellList)
                cell.building = this;

            if (OccupyCellList.Count == 0)
            {
                Sango.Log.Error(Name);
            }
            CenterCell = OccupyCellList[0];

            // 效果范围
            effectCells = new System.Collections.Generic.List<Cell>();
            scenario.Map.GetDirectSpiral(CenterCell, BuildingType.radius + 1, BuildingType.radius + BuildingType.atkRange, effectCells);

            for (int i = 0; i < areaCellList.Count; i++)
            {
                Cell cell = areaCellList[i];
                if (cell.HasGridState(Sango.Render.MapGrid.GridState.Defence))
                    defenceCellList.Add(cell);

                if (cell.IsInterior)
                {
                    if (BelongCity != null)
                        BelongCity.interiorCellList.Add(cell);
                    else
                        interiorCellList.Add(cell);
                }
            }

            captiveList.RemoveAll(x => x.CurrentCity != this);
            foreach (Person person in captiveList)
            {
                // 去重：BeCaptiveList 是普通 List，重复添加不会报错但会让
                // 势力层的俘虏忠诚结算对同一人重复执行。
                if (person.BelongForce != null && !person.BelongForce.BeCaptiveList.Contains(person))
                    person.BelongForce.BeCaptiveList.Add(person);
            }

            if (BelongCity != null)
                BelongCity.subCities.Add(this);

            // 【int[] → 对象】本城序列化列表的解析（追加进已有方法）
            if (NeighborList_list != null && NeighborList_list.Length > 0 && NeighborList.Count == 0)
                NeighborList.FromArray(NeighborList_list);
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);

            // 存档前回写 int[]（追加进已有方法；否则会把读档时的旧 id 存回去）
            NeighborList_list = NeighborList != null ? NeighborList.ToArray() : null;
        }

        /// <summary>
        /// 准备渲染时的处理
        /// </summary>
        public override void OnPrepareRender()
        {
            Render = new CityRender(this);
        }

        /// <summary>
        /// 清理城市
        ///
        /// 【泄漏修复】城市按武将特技装配出来的 Action（CityImproveGoldHarvest /
        /// CityImproveFoodHarvest / CityGoldHarvestEveryTurn 等）会订阅
        /// OnCityCalculateGoldHarvest / OnCityCalculateFoodHarvest / OnCityCalculateFoodCost /
        /// OnCityGainGoldHarvest / OnCityTurnStart 这些全局事件。
        ///
        /// 拆剧本时若不逐个 Clear，它们会带着旧剧本的订阅活到下一次开局：
        /// 每次读档/重开都会多出一份，GameEventDiagnostics 的 shutdown 快照会看到
        /// 这些事件的订阅数逐轮递增（实测 +1 ~ +2 / 轮）。
        ///
        /// Building / Troop / Person / Force 各自在 Clear 里都做了这件事，城市之前漏了。
        /// 这里刻意不把 actionList 置 null：工程里可能还有别处直接读 city.actionList
        /// （InitPersonAction 也没有 null 判断），保留下实例是最安全的选择。
        /// </summary>
        public override void Clear()
        {
            base.Clear();

            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
            }
        }

        /// <summary>
        /// 初始化城市
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public override void Init(Scenario scenario)
        {
            base.Init(scenario);

            if (BelongCorps != null && BelongForce != null && BelongCorps.BelongForce != BelongForce)
            {
                Sango.Log.Error($"城池:{Name} 所属军团和所属势力不匹配:<{BelongForce.ColorName} => {BelongCorps.ForceNumberName}>");
                //BelongCorps = BelongForce.CapitalCorps;
            }

            InitPersonAction();

            // 空闲人员判断（对应 Open311 的 is_at_service + is_action_done）
            // 除了"本回合未行动、没有任务、不在部队、不是俘虏"之外，
            // 还必须真的站在本城：出征途中 / 往别城移动中的武将不能派活，
            // 否则会出现"派了人却不在城里干活"，且同一人可能被两座城同时派出。
            freePersons.Clear();
            allPersons.ForEach(person =>
            {
                if (person == null)
                    return;
                // 事件武将 / NPC 不进入空闲名单：它们不参与出征、内政、计略等常规命令
                // （对应 Open311 Unit::can_set_member 的白名单——事件与 NPC 不可编入部队，
                //   而本项目的空闲名单同时是出征候选池，故在此一并排除）。
                // 副作用：某城若只剩 NPC，该城会显示"无可用武将"，这是正确行为。
                if (!person.ActionOver && person.IsFree && !person.IsDead
                    && person.CurrentCity == this && person.CanBeTroopMember)
                    freePersons.Add(person);
            });

            if (IsPort())
                BelongCity.portList.Add((Port)this);
            else if (IsGate())
                BelongCity.gateList.Add((Gate)this);

            if (BuildingType.Id == 1)
            {
                UpdateActiveTroopTypes();
                UpdateFightPower();
            }

            for (int i = 0; i < interiorCellList.Count; i++)
            {
                Cell c = interiorCellList[i];
                c.CreateInteriorModel();
            }

            UpdateNewLeader();

            //计算最大士气
            CalculateMaxMorale();
            CalculateHarvest();
            CalculateLimit();
        }

        /// <summary>
        /// 刷新上限
        /// </summary>
        public void UpdateCalculate()
        {
            //计算最大士气
            CalculateMaxMorale();
            CalculateHarvest();
            CalculateLimit();
        }

        /// <summary>
        /// 变为野生状态
        /// </summary>
        public void LeaveToWild()
        {
            Leader = null;
            BelongCorps = null;
            BelongForce = null;
            Render?.UpdateRender();
        }

        /// <summary>
        /// 更新战斗力
        /// </summary>
        public void UpdateFightPower()
        {
            fightPower = 1000 + troops;
            fightPower += UnityEngine.Mathf.Min(fightPower, allPersons.Count * 5000);
            isUpdatedFightPower = true;
            virtualFightPower = FightPower;
            //ForeachNeighborCities(x =>
            //{
            //    if (x.IsSameForce(this))
            //    {
            //        if (!x.isUpdatedFightPower)
            //        {
            //            x.UpdateFightPower();
            //            x.isUpdatedFightPower = true;
            //        }
            //        virtualFightPower += x.FightPower / 3;
            //    }
            //});
        }

        /// <summary>
        /// 计算收获
        /// </summary>
        public void CalculateHarvest()
        {
            GameEvent.OnCityCalculateHarvest?.Invoke(this);
        }

        /// <summary>
        /// 遍历相邻城市
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForeachNeighborCities(Action<City> action)
        {
            for (int i = 0; i < NeighborList.Count; i++)
            {
                City c = NeighborList[i];
                if (c == null) continue;
                action(c);
            }
        }

        /// <summary>
        /// 季度粮食收入
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnSeasonStart(Scenario scenario)
        {
            //if (BelongCorpsId == null)
            //    return true;

            //            int harvest = GameRandom.Random(totalGainFood, 0.05f);
            //            Render?.ShowInfo(harvest, (int)InfoType.Food);
            //            food += harvest;
            //#if SANGO_DEBUG
            //            Sango.Log.Info($"城市：{Name}, 收获粮食：{harvest}, 现有粮食: {food}");
            //#endif

            GameEvent.OnCitySeasonStart?.Invoke(this, scenario);

            return base.OnSeasonStart(scenario);
        }


        /// <summary>
        /// 月度金钱收入
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnMonthStart(Scenario scenario)
        {
            int pop = 0;
            int troopPop = 0;
            if (scenario.Variables.populationEnable)
            {
                // 计算人口增长
                pop = GameRandom.Random((int)(population * population_increase_factor * extraPopulationFactor), 0.1f);
                // 确保人口不超过上限
                int newPopulation = population + pop;
                if (newPopulation > PopulationLimit)
                {
                    pop = PopulationLimit - population;
                    newPopulation = PopulationLimit;
                }
                population = newPopulation;

                // 计算兵役人口增长
                troopPop = (int)(pop * scenario.Variables.baseTroopPopulationRatio);
                int newTroopPopulation = troopPopulation + troopPop;
                // 确保兵役人口不超过最大限制
                if (newTroopPopulation > MaxTroopPopulation)
                {
                    newTroopPopulation = MaxTroopPopulation;
                }
                troopPopulation = newTroopPopulation;
            }

            if (BelongCorps == null)
                return true;

            if (Render != null)
                Render.UpdateRender();

            GameEvent.OnCityMonthStart?.Invoke(this, scenario);

            int cost = -GoldCost(scenario);
            if (cost < 0)
            {
                AddGold(cost);
                Render?.ShowInfo(cost, (int)InfoType.Gold);

#if SANGO_DEBUG
                Sango.Log.Info($"城市：{Name},  支出：{cost}, 现有资金: {gold}");
#endif
            }

            return base.OnMonthStart(scenario);
        }

        /// <summary>
        /// 月度金钱收入
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnYearStart(Scenario scenario)
        {
            // 每年1月清理黑市
            // 【必须打日志】这是规则性拆除，此前完全静默 —— 玩家会看到"城里凭空少了一座建筑"，
            // 且与 AI 拆除的日志对不上，难以排查。
            int blackMarketRemoved = 0;
            allBuildings.ForEach(building =>
            {
                if (building.BuildingType.kind == (int)BuildingKindType.BlackMarket)
                {
                    building.OnFall(null);
                    blackMarketRemoved++;
                }
            });
            if (blackMarketRemoved > 0)
                Sango.Log.Info($"*[城政]{Name} 新年清理黑市 {blackMarketRemoved} 处");

            return base.OnYearStart(scenario);
        }

        /// <summary>
        /// 每日开始时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnDayStart(Scenario scenario)
        {
            return base.OnDayStart(scenario);
        }

        /// <summary>
        /// 建筑完成时的处理
        /// </summary>
        /// <param name="building">完成的建筑</param>
        /// <param name="builder">建造者</param>
        public virtual void OnBuildingComplete(Building building, SangoObjectList<Person> builder)
        {
            this.CalculateHarvest();
        }

        /// <summary>
        /// 建筑升级完成时的处理
        /// </summary>
        /// <param name="building">升级的建筑</param>
        /// <param name="builder">升级者</param>
        public virtual void OnBuildingUpgradeComplete(Building building, SangoObjectList<Person> builder)
        {
            this.CalculateHarvest();
        }

        /// <summary>
        /// 建筑创建时的处理
        /// </summary>
        /// <param name="building">创建的建筑</param>
        public void OnBuildingCreate(Building building)
        {
            allBuildings.Add(building);
            int buildingKind = building.BuildingType.kind;
            int count;
            if (buildingCountMap.TryGetValue(buildingKind, out count))
                buildingCountMap[buildingKind] = count + 1;
            else
                buildingCountMap.Add(buildingKind, 1);
        }

        /// <summary>
        /// 建筑销毁时的处理
        /// </summary>
        /// <param name="building">销毁的建筑</param>
        public void OnBuildingDestroy(Building building)
        {
            allBuildings.Remove(building);
            this.CalculateHarvest();
            int buildingKind = building.BuildingType.kind;
            int count;
            if (buildingCountMap.TryGetValue(buildingKind, out count))
                buildingCountMap[buildingKind] = count - 1;
        }

        /// <summary>
        /// 获取一个未行动的建筑
        /// </summary>
        /// <param name="buildingKindType">建筑类型</param>
        /// <returns>未行动的建筑</returns>
        public Building GetCommandBuilding(BuildingKindType buildingKindType)
        {
            Building rs = null;
            int maxLv = 0;
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building building = allBuildings[i];
                if (!building.ActionOver && building.BuildingType.kind == (int)buildingKindType)
                {
                    if (building.BuildingType.level > maxLv)
                    {
                        rs = building;
                        maxLv = building.BuildingType.level;
                    }
                }
            }
            return rs;
        }
        List<Feature> temp_FeatureList = new List<Feature>();
        public void InitPersonAction()
        {
            // 重置
            for (int i = 0; i < actionList.Count; i++)
                actionList[i].Clear();
            actionList.Clear();
            temp_FeatureList.Clear();
            allPersons.ForEach(x =>
            {
                if (x.FeatureList != null)
                {
                    for (int i = 0; i < x.FeatureList.Count; i++)
                    {
                        Feature feature = x.FeatureList[i];
                        // 仅城市收入、灾害类特技可在城市装配；括号确保空特技不会继续访问 kind。
                        if (feature != null && (feature.kind == (int)FeatureKindType.CityHarvest || feature.kind == (int)FeatureKindType.CityDisaster))
                        {
                            if (!feature.only)
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(actionList, this, x);
                            }
                            else
                            {
                                if (!temp_FeatureList.Contains(feature))
                                {
                                    temp_FeatureList.Add(feature);
                                    feature.InitActions(actionList, this, x);
                                }
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 势力回合开始时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnForceTurnStart(Scenario scenario)
        {
            jobCounter.Clear();
            // 复位 AI 命令队列：上一回合若被中断（未跑完整队）会残留条目，这里统一清空并复位游标，
            // 保证本回合 AIPrepare 从空队列重新装填。
            AICommandList.Clear();
            aiCommandIndex = 0;
            AIPrepared = false;
            AIFinished = false;
            ActionOver = false;
            CalculateHarvest();
            UpdateFightPower();
            JobHealingTroop();

            // 空闲人员判断（对应 Open311 的 is_at_service + is_action_done）
            // 除了"本回合未行动、没有任务、不在部队、不是俘虏"之外，
            // 还必须真的站在本城：出征途中 / 往别城移动中的武将不能派活，
            // 否则会出现"派了人却不在城里干活"，且同一人可能被两座城同时派出。
            freePersons.Clear();
            allPersons.ForEach(person =>
            {
                if (person == null)
                    return;
                // 褒赏标记的复位已移到 Person.OnForceTurnStart（Force 逐武将调用，
                // 覆盖面是"全势力存活武将"，不限本城 allPersons）。
                // 那里对应原版 reset_command_flags()（s11_game_week.cpp:166-172）。
                // 事件武将 / NPC 不进入空闲名单：它们不参与出征、内政、计略等常规命令
                // （对应 Open311 Unit::can_set_member 的白名单——事件与 NPC 不可编入部队，
                //   而本项目的空闲名单同时是出征候选池，故在此一并排除）。
                // 副作用：某城若只剩 NPC，该城会显示"无可用武将"，这是正确行为。
                if (!person.ActionOver && person.IsFree && !person.IsDead
                    && person.CurrentCity == this && person.CanBeTroopMember)
                    freePersons.Add(person);
            });

            // 粮食消耗
            CostFood(scenario);

            // 耐久自修复
            if (durability < DurabilityLimit)
            {
                ChangeDurability(Leader?.BaseBuildAbility * 2 + 50 ?? 50, null);
            }

            if (Render != null)
                Render.UpdateRender();

            // 俘虏羁押天数累积
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                if (person != null)
                    person.missionCounter++;
            }

            GameEvent.OnCityTurnStart?.Invoke(this, scenario);

            return base.OnForceTurnStart(scenario);
        }

        /// <summary>
        /// 势力回合结束时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            CurActiveTroop = null;
            isUpdatedFightPower = false;

            // 计算俘虏越狱
            //
            // 【修复 · "不是囚犯,无法逃跑!" 日志无限重复】
            // 遍历前先清洗本城俘虏表的**残留引用**（代码里同类清理见 City.cs:928 的
            // captiveList.RemoveAll(x => x.CurrentCity != this)，只是这条路径漏了）：
            //   · 已不是俘虏：被登用 / 释放后 state 已变，但本城列表没摘；
            //   · CurrentCity 已不是本城：被转移 / 换城后旧城列表没摘。
            // 这两类残留若不清，就会被送进 person.Escape()；而 Escape 的非囚犯分支
            // 是用 person.CurrentCity 去 RemoveCaptive —— 摘的不是本城这条 → 永远摘不掉，
            // 于是每个势力回合结束都重复刷一片 Person.cs:1917 的 Sango.Log.Error。
            captiveList.RemoveAll(x => x == null || !x.IsPrisoner || x.CurrentCity != this);

            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                if (person == null) continue;
                if (GameRandom.Chance(GameFormula.Instance.PersonEscapeProbablility_InCity(person, this, scenario), 10000))
                {
                    person.Escape(EscapeType.Escape);
                    if (IsPlayer)
                    {
                        PersonEscapeEvent personEscapeEvent = new PersonEscapeEvent()
                        {
                            person = person
                        };
                        RenderEvent.Instance.Add(personEscapeEvent);
                    }
#if SANGO_DEBUG
                    Sango.Log.Info($"{person.Name}逃跑!");
#endif
                }
            }

            GameEvent.OnCityTurnEnd?.Invoke(this, scenario);

            // 太守不在此城,需要更新太守
            if (Leader == null || Leader.BelongCity != this || needUpdateLeader)
                UpdateNewLeader();

            InitPersonAction();

            return base.OnForceTurnEnd(scenario);
        }

        /// <summary>
        /// 计算粮食消耗
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>粮食消耗数量</returns>
        public int FoodCost(Scenario scenario)
        {
            int foodCost = 0;
            // 军队粮食消耗
            foodCost += (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInCity * (troops + woundedTroops));
            // 人口粮食消耗
            if (scenario.Variables.populationEnable)
            {
                foodCost += (int)System.Math.Ceiling(population * scenario.Variables.populationFoodCostFactor);
            }

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(foodCost);
            GameEvent.OnCityCalculateFoodCost?.Invoke(this, scenario, overrideData);
            foodCost = overrideData.ValueAndRecycle;
            return foodCost;
        }

        /// <summary>
        /// 消耗粮食
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public void CostFood(Scenario scenario)
        {
            if (food > 0)
            {
                int foodCost = FoodCost(scenario);
                int needFood = foodCost - food;
                if (needFood > 0)
                {
                    float runawayTroops = ((float)needFood / (float)foodCost) * scenario.Variables.runawayWhenCityFoodNotEnough;
                    troops = (int)System.Math.Ceiling(troops * (1.0f - runawayTroops));
                    if (woundedTroops > 100)
                    {
                        woundedTroops = (int)System.Math.Ceiling(woundedTroops * (1.0f - runawayTroops));
                    }
                    else
                    {
                        woundedTroops = 0;
                    }
                    food = 0;
                }
                else
                    food -= foodCost;

            }
            else
            {
                food = 0;
                float runawayTroops = scenario.Variables.runawayWhenCityFoodNotEnough;
                troops = (int)System.Math.Ceiling(troops * (1.0f - runawayTroops));
                if (woundedTroops > 100)
                {
                    woundedTroops = (int)System.Math.Ceiling(woundedTroops * (1.0f - runawayTroops));
                }
                else
                {
                    woundedTroops = 0;
                }
            }
        }

        /// <summary>
        /// 计算金钱消耗
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>金钱消耗数量</returns>
        public int GoldCost(Scenario scenario)
        {
            int goldCost = 0;
            allPersons.ForEach(person =>
            {
                if (person.Official != null)
                {
                    goldCost += person.Official.cost;
                }
            });

            // 计算俘虏的消耗
            for (int i = 0; i < captiveList.Count; i++)
            {
                if (captiveList[i] != null)
                    goldCost += 100;
            }

            return goldCost;
        }

        //public void CreateTroop(Troop troop)
        //{
        //    AddTroop(troop);

        //}

        //public void AddTroop(Troop troop)
        //{
        //    // 先加入剧本才能分配ID
        //    Add(troop);
        //    troop.Leader.BelongTroopId = troop;
        //    for (int i = 0; i < troop.MemberList.Count; i++)
        //        troop.MemberList[i].BelongTroopId = troop;

        //    troop.BelongCityId = this;
        //    troop.cell = CenterCell;
        //    troop.cell.troop = troop;
        //    troop.x = troop.cell.x;
        //    troop.y = troop.cell.y;
        //}

        //public Person Add(Person person)
        //{
        //    allPersons.Add(person);
        //    if (BelongCorpsId == null)
        //    {
        //        Sango.Log.Error($"why {Name}->BelongCorpsId is null");
        //    }
        //    BelongCorpsId.Add(person);
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(BelongCorpsId.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(BelongCorpsId.Add(building));
        //    return building;
        //}
        //public Troop Add(Troop troop)
        //{
        //    this.TroopList.Add(troop);
        //    return troop;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(person);
        //    BelongCorpsId.Remove(person);
        //    return person;
        //}
        //public Troop Remove(Troop troop)
        //{
        //    this.TroopList.Remove(troop);
        //    return troop;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(BelongCorpsId.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(BelongCorpsId.Remove(building));
        //    return building;
        //}

        /// <summary>
        /// 改变耐久度
        /// </summary>
        /// <param name="num">改变的耐久度值</param>
        /// <param name="atk">攻击者</param>
        /// <param name="showDamage">是否显示伤害</param>
        /// <returns>是否成功</returns>
        public override bool ChangeDurability(int num, SangoObject atk, bool showDamage = true)
        {
            bool rs = base.ChangeDurability(num, atk, showDamage);
            if (rs)
            {
                // 保留40%
                durability = DurabilityLimit * 40 / 100;
            }

            if (Render != null)
                Render.UpdateRender();

            return rs;
        }

        /// <summary>
        /// 获取最近的己方城市
        /// </summary>
        /// <returns>最近的己方城市</returns>
        public City GetNearnestForceCity()
        {
            City nearnest = null;
            int distance = 100000;
            BelongForce.ForEachCity(city =>
            {
                if (city != this)
                {
                    // 拓扑网距离最近的城市,非港关,找不到则属于最后一城了
                    int dis = city.Distance(this);
                    if (dis < distance)
                    {
                        distance = dis;
                        nearnest = city;
                    }
                }
            });

            return nearnest;
        }

        /// <summary>
        /// 改变所属军团
        /// </summary>
        /// <param name="other">新的军团</param>
        /// <returns>原来的军团</returns>
        public Corps ChangeCorps(Corps other)
        {
            Corps last = null;
            if (BelongCorps != other)
            {
                last = BelongCorps;
                BelongCorps = other;
                if (BelongForce != other.BelongForce)
                {
                    BelongForce = other.BelongForce;
                }
                Render?.UpdateRender();
            }
            return last;
        }

        /// <summary>
        /// 改变军团后,需要用此方法更新所属信息
        ///
        /// 【覆盖面】城池的"军团归属"散落在三份名单里：在籍武将(allPersons)、建筑(allBuildings)、
        /// 俘虏(captiveList)。只改城市自身的 BelongCorps 而不调用本方法，就会留下
        /// "城属A团、人属B团"的分裂 —— 之后任何一次 AddPerson（读档重登记 / 入城 / 释放 / 归返）
        /// 都会打出"军团不一致"的错误日志。故改军团一律用 ChangeCorps + UpdateCorps 成对调用。
        /// </summary>
        public void UpdateCorps()
        {
            allPersons.ForEach(person =>
            {
                person.ChangeCorps(BelongCorps);
            });
            allBuildings.ForEach(person =>
            {
                person.ChangeCorps(BelongCorps);
            });

            // 俘虏名单也要跟：同势力的"自家俘虏"（城陷 / 读档中途态）若军团停在旧值，
            // 之后释放 / 归返时同样会撞上 AddPerson 的校验。
            // 但敌方俘虏的势力与军团代表其原属国，登庸 / 释放 / 逃走的判定要读它，
            // 绝不能一并改，所以这里只同步"势力已经与自己相同"的那部分。
            captiveList.ForEach(person =>
            {
                if (person != null && person.BelongForce == BelongForce)
                    person.ChangeCorps(BelongCorps);
            });
        }

        /// <summary>
        /// 改变兵力
        /// </summary>
        /// <param name="num">改变的兵力值</param>
        /// <param name="atk">攻击者</param>
        /// <param name="showDamage">是否显示伤害</param>
        /// <returns>是否还有兵力</returns>
        public bool ChangeTroops(int num, SangoObject atk, bool showDamage = true)
        {
            // 白城直接占领
            if (this.BelongForce == null)
                return false;

            if (showDamage)
                Render?.ShowInfo(num, (int)InfoType.Troop);

            troops = troops + num;
            if (troops < 0)
                troops = 0;

            if (Render != null)
            {
                Render.UpdateRender();
            }
            return troops > 0;
        }

        /// <summary>
        /// 城市被攻占时的处理
        /// </summary>
        /// <param name="atker">攻击者</param>
        public override void OnFall(SangoObject atker)
        {

            Scenario scenario = Scenario.Cur;
            ScenarioVariables scenarioVariables = Scenario.Cur.Variables;

            Troop atk = atker as Troop;
            SkillInstance skillInstance = null;
            if (atker.ObjectType == SangoObjectType.SkillInstance)
            {
                skillInstance = (atker as SkillInstance);
                atk = skillInstance.master;
            }
            else
            {
                atk = atker as Troop;
            }

            if (atk == null) return;

            if (BelongForce == atk.BelongForce)
                return;

            // 【关系值：城池陷落惩罚】把双方友好度压到 15 地板
            // 对齐 s11_sys_event_kanraku.cpp:221-229：
            //   `if (like > 15) set_like(15); else add_like(-1);`
            // 原版这是"打完还能再谈"的设计：一场战争最多压到 15，反复冲突才继续跌。
            DiplomacyRules.ApplyHostilityPenalty(atk.BelongForce, BelongForce);

            Force lastBelongForce = BelongForce;
            Corps lastBelongCorps = BelongCorps;
            freePersons.Clear();

            // 清理火
            for (int i = 0; i < OccupyCellList.Count; ++i)
            {
                Cell cell = OccupyCellList[i];
                if (cell.fire != null)
                {
                    cell.fire.Clear();
                    cell.fire = null;
                }
            }

            // 白城
            if (BelongCorps == null)
            {
                Sango.Log.Info($"*[城陷]{Name}（无主）被 {atk.BelongForce?.Name ?? "未知"} 占领（攻方部队 {atk.Name}）");
                BelongCorps = atk.BelongCorps;
                BelongForce = atk.BelongForce;
                if (skillInstance != null && !skillInstance.IsRange())
                {
                    // 【顺序】必须先让攻方入城（改户口），再指定太守。
                    // 反序会出现"Leader 的 BelongCity 还不是本城、也不在 allPersons 里"，
                    // 年度巡检会判太守失效并清空，玩家看到"刚打下的城太守凭空消失"。
                    Person newLeader = atk.Leader;
                    atk.EnterCity(this);
                    if (newLeader != null && newLeader.IsAlive && newLeader.BelongCity == this && allPersons.Contains(newLeader))
                        Leader = newLeader;
                }
                Render?.UpdateRender();
                CalculateHarvest();
                GameEvent.OnCityFall?.Invoke(this, lastBelongForce, atk);
                return;
            }

            BelongForce.UpdateCityCount(scenario);
            BelongForce.CityBaseCount--;
            if (IsCity()) BelongForce.CityCount--;
            BelongForce.CityList.Remove(this);

            // 确认一个撤退城市
            City escapeCity = null;
            if (this == BelongForce.CapitalCity)
                escapeCity = GetNearnestForceCity();
            else
                escapeCity = BelongForce.CapitalCity;

            // 最后一城,港关不算城市数量,必须要有最后一城
            if (BelongForce.CityCount == 0)
            {
                escapeCity = null;
            }

#if SANGO_DEBUG
            if (IsPort() || IsGate())
            {
                if (escapeCity == null)
                {
                    Sango.Log.Error("为啥 escapeCity == null", Sango.Log.LogType.Game);
                }
            }
#endif

            // 【城陷总览】先打一条带上下文的日志，后面每个武将的处理结果都挂在它下面看
            Sango.Log.Info(escapeCity != null
                ? $"*[城陷]{Name}（{lastBelongForce.Name}）被 {atk.BelongForce.Name} 攻陷，残部撤往 {escapeCity.Name}"
                : $"*[城陷]{Name}（{lastBelongForce.Name}）被 {atk.BelongForce.Name} 攻陷，这是该势力最后一座城");

            // 基础抓捕率
            int cacaptureChangce = escapeCity != null ? scenarioVariables.captureChangceWhenCityFall : scenarioVariables.captureChangceWhenLastCityFall;

            // 处理俘虏
            List<Person> temp_captive_list = new List<Person>();

            // 必须优先处理队伍
            if (escapeCity == null)
            {
                BelongForce.IsAlive = false;
                BelongCorps.IsAlive = false;
                // 灭亡后,队伍要清除
                // 【修复】troop.BelongForce 由主将实时推导（Leader?.BelongForce）。
                // 主将已脱离部队（被俘流转 / 阵亡 / 旧存档中间态）的无主部队，
                // 其 BelongForce 为 null，旧条件会漏掉它们——表现为势力灭亡后
                // 部队仍残留在地图上且永远拿不到任务（冻结原地）。
                // 因此增加兜底口径：主将缺失时按副将归属判定（此刻武将尚未全量脱籍，
                // 副将的 BelongForce 仍指向被灭势力）。
                scenario.troopsSet.ForEach((troop) =>
                {
                    if (!troop.IsAlive)
                        return;

                    // 常规口径：主将推导的所属势力即被灭势力
                    if (troop.BelongForce == this.BelongForce)
                    {
                        troop.Clear();
                        return;
                    }

                    // 兜底口径：无主部队按副将归属判定
                    if (troop.Leader == null
                        && ((troop.Member1 != null && troop.Member1.BelongForce == this.BelongForce)
                            || (troop.Member2 != null && troop.Member2.BelongForce == this.BelongForce)))
                    {
                        Sango.Log.Warning($"势力[{BelongForce.Name}]灭亡时清理了无主部队[{troop.Name}]（主将已脱离，按副将归属判定）");
                        troop.Clear();
                    }
                });

                // 清理港口,关卡
                for (int i = 0; i < scenario.citySet.Count; ++i)
                {
                    var c = scenario.citySet[i];
                    if (c != null && c.BelongForce == BelongForce)
                    {
                        if (c.IsGate() || c.IsPort())
                        {
                            c.allAttackTroops.Clear();
                            c.allTroops.Clear();
                            c.freePersons.Clear();
                            c.allPersons.ForEach(p =>
                            {
                                p.LeaveToWild();
                            });
                            c.allPersons.Clear();
                            c.allBuildings.ForEach(building =>
                            {
                                building.OnFall(atk);
                            });
                            c.captiveList.ForEach(person =>
                            {
                                person.Escape(EscapeType.Escape);
                            });
                            c.LeaveToWild();
                        }
                    }
                }
            }

            // 处理缓存队伍信息
            allAttackTroops.Clear();
            allTroops.Clear();
            allPersons.ForEach(person =>
            {
                // 【原版对齐】城陷时只处理"物理在本城"的武将。
                // 在途武将（CurrentCity 停在中途某城）的户口虽然还挂在这座陷落城，
                // 但他的行程与任务要继续保留，等走到目的地后由 OnPersonReturnCity -> Enter 改户口。
                // 依据：Open311 的 城陷事件 用 BuildingDistrictChangeReason_0 跳过成员迁移，
                // 而 capture 只抓 location_id 等于本城的人 —— 在途者既不被撤退也不被俘。
                // 只有"势力灭亡"（escapeCity == null）时才需要对所有人收尾。
                if (escapeCity != null && person.CurrentCity != this)
                    return;

                person.workingBuilding = null;
                if (escapeCity != null)
                {
                    person.ClearMission();
                    person.OnWillChangeToCity(escapeCity);
                    person.ChangeBelongCity(escapeCity);
                    if (person.mBelongTroop == null && person.CurrentCity == this && person != person.BelongForce.mGovernor && GameRandom.Chance(cacaptureChangce))
                    {
                        Sango.Log.Info($"*[城陷]{person.Name} 留在 {Name}，进入待俘判定");
                        temp_captive_list.Add(person);
                    }
                    else
                    {
                        if (person.mBelongTroop == null)
                        {
                            Sango.Log.Info($"*[城陷]{person.Name} 撤往 {escapeCity.Name}");
                            person.SetMission(MissionType.PersonReturn, person.BelongCity);
                        }
                        else
                        {
                            // 随军出征的人不撤也不俘：跟着部队走，户口随后续入城流程改
                            Sango.Log.Info($"*[城陷]{person.Name} 随军在外（{person.mBelongTroop.Name}），不参与撤离");
                        }
                    }
                }
                else
                {
                    // 最后一城：势力灭亡，所有人（含在途者）转在野
                    Sango.Log.Info($"*[城陷]{person.Name} 随 {lastBelongForce.Name} 灭亡下野");
                    person.ClearMission();
                    person.LeaveToWild();
                    temp_captive_list.Add(person);
                }
            });
            allPersons.Clear();
            freePersons.Clear();

            //处理建筑
            allBuildings.ForEach(building =>
            {
                if (building.isComplate && !building.isUpgrading && GameRandom.Chance(30))
                {
                    building.ChangeCorps(atk.BelongCorps);
                    building.Builder?.Clear();
                    building.Workers?.Clear();
                }
                else
                {
                    building.OnFall(atk);
                }
            });

            Force destroyedForce = null;
            if (escapeCity == null)
            {
                destroyedForce = BelongForce;

                // 【原版对齐】势力灭亡改调独立的 Force.Ruin（对应 System::force_ruin）。
                // 之前这段内联在这里，与城陷、战斗结算、演出缠在一起：既无法复用于
                // 事件灭国 / 编辑器删势力，也容易在新增攻城来源时漏掉某一步。
                // 返回 true 表示达成天下一统 —— 此时主循环已暂停、终局演出已广播，
                // 必须立刻返回，不能再往下走改旗与太守重任命。
                if (BelongForce.Ruin(atk.BelongForce, this, atk))
                    return;
            }
            Leader = null;

            // 【归属治理】城本体改旗走统一入口（对应 Open311 的 building_set_district_id）。
            // 它负责：势力与军团同步、新旧势力的城市账本全量重算（幂等，不用再做增量加减）。
            // 原先这里是"裸赋两个字段 + 手工 CityBaseCount++/CityCount++/CityList.Add"，
            // 增量维护一旦在某个 return 分支被跳过就会永久漂移，无法自愈。
            if (atk.BelongCorps != null)
                CityStateSystem.SetCorps(this, atk.BelongCorps);

            if (escapeCity != null)
                lastBelongCorps.UpdateWhenCityChange();
            atk.BelongCorps.UpdateWhenCityChange();

            // 处理库存和钱粮,兵力
            food = food * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepFoodFactor) * 10 + 10) / 100;
            gold = gold * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepGoldFactor) * 10 + 10) / 100;
            troops = troops * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepTroopsFactor) * 10 + 10) / 100;
            itemStore.Split((100 - (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepItemFactor) * 10 + 10)) / 100);
            agriculture = agriculture * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepAgriculture) * 10 + 10) / 100;
            commerce = commerce * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepCommerce) * 10 + 10) / 100;

            // 解救俘虏
            // 【归属治理】同势力俘虏直接归顺本城，统一走 PersonStateSystem.Recruit
            //（对应 Open311 的 person_change_force）。
            // 原先这里手写"改 state -> 赋三字段 -> AddPerson"，顺序颠倒时
            // AddPerson 的"军团不一致"校验会刷错误日志，且漏掉部队字段与在途命令的清理。
            // 倒序遍历 + 逐个移除是安全的（ForEach 内部从后往前扫）。
            captiveList.ForEach(person =>
            {
                if (person != null && person.IsSameForce(atk))
                {
                    PersonStateSystem.Recruit(person, this, PersonChangeReason.Released);
                    if (!freePersons.Contains(person))
                        freePersons.Add(person);
                }
            });

            if (skillInstance != null && !skillInstance.IsRange())
            {
                Leader = atk.Leader;
                atk.EnterCity(this);
            }

            Render?.UpdateRender();

            CalculateHarvest();
            CalculateMaxMorale();
            CalculateLimit();

            // 再保证一次转换成功（幂等，仍走统一入口以维持"势力==军团势力"的不变式）
            if (atk.BelongCorps != null)
                CityStateSystem.SetCorps(this, atk.BelongCorps);

            // 【归属链】都市易主后，下属港关必须跟着改旗。
            // 港关自身独立存 BelongForce/BelongCorps，若不在此处同步，
            // 就会留下"都市属 A、港口/关隘仍属 B"的飞地，并在后续
            // 军团统计、AI 出兵、补给判定里持续制造错误数据。
            CityStateSystem.SyncSubCities(this);

            GameEvent.OnCityFall?.Invoke(this, lastBelongForce, atk);

            if (atk.BelongCorps.IsPlayer)
            {
                RenderEvent.Instance.Add(new Render.CityFallCompleteEvent()
                {
                    city = this
                });
            }

            CityRecruitPersonWhenCityFallEvent te = RenderEvent.Instance.Create<CityRecruitPersonWhenCityFallEvent>();
            te.Init(temp_captive_list, this, escapeCity, atk, escapeCity == null ? (int)PersonRecruitType.OnForceFall : (int)PersonRecruitType.OnCityFall);
            RenderEvent.Instance.Add(te);


            if (escapeCity == null && destroyedForce != null)
            {
                RenderEvent.Instance.Add(new Render.ForceFallCompleteEvent()
                {
                    force = destroyedForce
                });
            }


        }

        /// <summary>
        /// 获取城市之间的距离
        /// </summary>
        /// <param name="other">目标城市</param>
        /// <returns>城市之间的距离</returns>
        public int Distance(City other)
        {
            if (other == null) return 999;

            if (this == other)
                return 0;

            if (BelongCity != null)
            {
                // 隶属范围内,需要1回合
                if (BelongCity == other) return 1;
                return BelongCity.Distance(other);
            }

            if (other.BelongCity != null)
            {
                // 隶属范围内,需要1回合
                if (other.BelongCity == this) return 1;
                other = other.BelongCity;
            }

            return Scenario.Cur.GetCityDistance(this, other);
        }

        /// <summary>
        /// 武将返回城市时的处理
        /// </summary>
        /// <param name="person">返回的武将</param>
        public void OnPersonReturnCity(Person person)
        {
            if (person == null)
                return;

            City from = person.BelongCity;

            // 【原版对齐】到达 = 入城（对应 Open311 的 person_enter）：
            // 迁户口 -> 同步势力/军团 -> 维护在籍名册 -> 太守重算。
            // 原先这里只改所在地（CurrentCity），户口仍停在出发城，
            // 于是"人在本城、名册在别城"，太守任命与本城人才列表都会漏掉他。
            PersonStateSystem.Enter(person, this, PersonChangeReason.Moved);

#if SANGO_DEBUG
            Sango.Log.Info($"[{person.BelongForce?.Name}]{person.Name}回到[{BelongForce?.Name}]<{Name}>");
#endif
            GameEvent.OnPersonChangeBelongCity?.Invoke(person, from, this);
        }

        /// <summary>
        /// 武将转换结束时的处理
        /// </summary>
        /// <param name="person">转换的武将</param>
        public void OnPersonTransformEnd(Person person, City from)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"[{person.BelongForce.Name}]{person.Name}到达[{BelongForce.Name}]<{Name}>");
#endif
            GameEvent.OnPersonChangeBelongCity?.Invoke(person, from, this);
        }

        /// <summary>
        /// 检查工作成本
        /// </summary>
        /// <param name="cityJobType">城市工作类型</param>
        /// <returns>是否有足够的资金</returns>
        public bool CheckJobCost(CityJobType cityJobType)
        {
            // 走 GetJobCost 统一口径：那里已包含 `OnCityCheckJobCost` 的 override
            // 与「外交府 → 亲善减半」，避免门禁按原价、扣费按折后价的分叉。
            return gold >= GetJobCost(cityJobType);
        }

        /// <summary>
        /// 获取工作成本
        /// </summary>
        /// <param name="cityJobType">城市工作类型</param>
        /// <returns>工作成本</returns>
        public int GetJobCost(CityJobType cityJobType)
        {
            int jobId = (int)cityJobType;
            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, null, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            // 【外交府：亲善资金减半】
            // 原版 `exec_donate_command` 里 `donate_gold_cost` 在有外交府时减半
            // （`s11_sys_person_order_diplomacy.cpp` 的 AP / 资金减半段）。
            // 放在 override 之后：MOD 若显式改了金额，仍按改后的值打折。
            if (cityJobType == CityJobType.SendGift
                && CityFacilityRequirement.HasGovFacility(this, InteriorFacilityKind.DiplomaticOffice))
            {
                goldNeed = goldNeed / 2;
            }
            return goldNeed;
        }


        /// <summary>
        /// 建造建筑
        /// </summary>
        /// <param name="buildCenter">建造中心</param>
        /// <param name="builder">建造者部队</param>
        /// <param name="buildingType">建筑类型</param>
        /// <returns>建造的建筑</returns>
        public Building BuildBuilding(Cell buildCenter, Troop builder, BuildingType buildingType)
        {
            Building building = new Building();
            building.BelongForce = BelongForce;
            building.BelongCorps = BelongCorps;
            building.BelongCity = this;
            building.BuildingType = buildingType;
            building.x = buildCenter.x;
            building.y = buildCenter.y;
            building.rot = GameRandom.Range(0, 10) * 90 * Mathf.Deg2Rad;

            building.durability = 0;

            Scenario scenario = Scenario.Cur;

            // TODO: 获取高度
            // ------
            scenario.Add(building);
            building.Init(scenario);
            building.Workers = null;
            building.isComplate = false;
            building.durability = 1;
            building.ChangeDurability(GameUtility.Method_TroopBuildAbility(builder), null);

            // 【军事府：领地内建军事设施 8 折】
            // 对齐 get_facility_gold_cost（s11_sys_scen_unit.cpp:1607-1614）：
            // 部队在本城领地内建造时，造价 × 8/10。
            int troopBuildCost = buildingType.cost;
            if (CityFacilityRequirement.HasGovFacility(this, InteriorFacilityKind.MilitaryOffice))
                troopBuildCost = troopBuildCost * 8 / 10;
            builder.gold -= troopBuildCost;

#if SANGO_DEBUG
            Sango.Log.Info($"[{BelongForce.Name}]在<{Name}>由{builder.Name}开始修建: {building.Name}");
#endif
            building.Render.UpdateRender();
            return building;
        }

        /// <summary>
        /// 获取空的内城cell
        /// </summary>
        /// <returns>空的内城cell</returns>
        public Cell GetEmptyInteriorCell()
        {
            for (int i = 0; i < interiorCellList.Count; ++i)
            {
                Cell c = interiorCellList[i];
                if (c.building == null)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// 初始化工作特性
        /// </summary>
        /// <param name="people">人员数组</param>
        public void InitJobFeature(Person[] people)
        {
            GameUtility.InitJobFeature(people, this);
        }

        /// <summary>
        /// 初始化工作特性
        /// </summary>
        /// <param name="person">人员</param>
        public void InitJobFeature(Person person)
        {
            GameUtility.InitJobFeature(person, this);
        }

        /// <summary>
        /// 清除工作特性
        /// </summary>
        public void ClearJobFeature()
        {
            GameUtility.ClearJobFeature();
        }

        /// <summary>
        /// 建造内城建筑
        /// </summary>
        /// <param name="buildCenter">建造中心</param>
        /// <param name="builders">建造者</param>
        /// <param name="buildingType">建筑类型</param>
        /// <param name="buildCount">建造所需回合数</param>
        /// <returns>建造的建筑</returns>
        public Building JobBuildBuilding(Cell buildCenter, Person[] builders, BuildingType buildingType, int buildCount)
        {
            // 【资金复核 —— 必须放在最前】
            // 门禁只按"最便宜的可建项"放行，玩家可能先开菜单、再在列表里换成更贵的建筑。
            // 此处若拖到中途才判，建筑已 `scenario.Add`、武将已被摘出 freePersons，
            // 直接返回会留下悬空建筑 + 武将丢失。
            if (gold < buildingType.cost)
            {
                Sango.Log.Warning($"[内政]{Name} 资金不足，取消建造 {buildingType.Name}"
                    + $"（需 {buildingType.cost}，现有 {gold}）");
                return null;
            }

            Building building = new Building();
            building.BelongForce = BelongForce;
            building.BelongCorps = BelongCorps;
            building.BelongCity = this;
            building.BuildingType = buildingType;
            building.x = buildCenter.x;
            building.y = buildCenter.y;
            building.durability = 0;
            building.rot = GameRandom.Range(0, 10) * 90 * Mathf.Deg2Rad;

            Scenario scenario = Scenario.Cur;
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            // TODO: 获取高度
            // ------
            scenario.Add(building);
            building.Init(scenario);
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();
            foreach (Person person in builders)
            {
                if (person == null) continue;
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
                person.SetMission(MissionType.PersonBuild, building, 0);
                person.ActionOver = true;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
            }

            building.Builder = sangoObjectList;
            building.isComplate = false;
            building.durability = 1;
            building.LeftCounter = buildCount;
            gold -= buildingType.cost;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.Build));

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>由{stringBuilder}开始修建: {building.Name} 需耗时:{buildCount} 回合");
#endif
            building.Render.UpdateRender();
            return building;
        }

        /// <summary>
        /// 升级内城建筑
        /// </summary>
        /// <param name="building">要升级的建筑</param>
        /// <param name="builders">升级者</param>
        /// <param name="upgradeBuildingType">升级后的建筑类型</param>
        /// <param name="buildCount">升级所需回合数</param>
        /// <returns>升级后的建筑</returns>
        public Building JobUpgradeBuilding(Building building, Person[] builders, BuildingType upgradeBuildingType, int buildCount)
        {
            // 【资金复核 —— 必须放在最前】
            // 若拖到中途才判，`isUpgrading` 已置位、武将也已摘出 freePersons，
            // 返回 null 会留下一座"永远升级中"的建筑
            // （`AIDestroy` 与 `FindUnnecessaryBuilding` 都会跳过 isUpgrading，等于永久卡死）。
            if (gold < upgradeBuildingType.cost)
            {
                Sango.Log.Warning($"[内政]{Name} 资金不足，取消升级 {building.Name}"
                    + $"（需 {upgradeBuildingType.cost}，现有 {gold}）");
                return null;
            }

            building.isUpgrading = true;
            building.durability = 1;

            Scenario scenario = Scenario.Cur;
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            // TODO: 获取高度
            // ------
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();
            foreach (Person person in builders)
            {
                if (person == null) continue;
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
                person.SetMission(MissionType.PersonBuild, building, 0);
                person.ActionOver = true;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
            }

            building.Builder = sangoObjectList;
            building.LeftCounter = buildCount;
            gold -= upgradeBuildingType.cost;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.UpgradeBuilding));

            building.Render?.UpdateRender();

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>由{stringBuilder}开始升级建筑: {building.Name} 需耗时: {buildCount}回合");
#endif
            return building;
        }

        /// <summary>
        /// 生产船
        /// </summary>
        /// <param name="personList">生产人员</param>
        /// <param name="itemType">船的物品类型</param>
        /// <param name="building">建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>生产信息数组</returns>
        public int[] JobCreateBoat(Person[] personList, ItemType itemType, Building building, bool isTest = false)
        {
            if (personList == null || personList.Length == 0 || itemType == null) return null;

            if (itemType.kind != (int)ItemKindType.Boat) return null;

            int totalNum = itemStore.GetNumber((int)ItemStoreKindType.Boat);
            if (totalNum >= itemType.TransformLimit(StoreLimit))
                return null;

            Scenario scenario = Scenario.Cur;

            InitJobFeature(personList);
            int empty = itemType.TransformLimit(StoreLimit) - totalNum;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateBoat;

            int goldNeed = JobType.GetJobCost(jobId) + itemType.cost;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return null;
            }

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseCreativeAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseCreativeAbility;
                }
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            int subValue = 0;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    subValue += maxPerson.BaseCreativeAbility;
                }
            }

            int turnCount = GameUtility.Method_CreateBoatCounter(maxValue, subValue, building.BuildingType.level);
            overrideData = Tools.OverrideData<int>.Create(turnCount);
            GameEvent.OnCityJobCounterResult?.Invoke(this, jobId, personList, overrideData);
            turnCount = overrideData.ValueAndRecycle;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            maxValue *= 5;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    maxValue += maxPerson.BaseCreativeAbility * 10;
                }
            }


            if (isTest)
            {
                int totalValue = GameUtility.Method_CreateItems(maxValue, building.BuildingType.level) / itemType.p1 / 3;
                overrideData = Tools.OverrideData<int>.Create(totalValue);
                GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
                totalValue = overrideData.ValueAndRecycle;
                ClearJobFeature();
                return new int[] { turnCount, totalValue };
            }

            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            building.ActionOver = true;
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
                person.ActionOver = true;
                person.SetMission(MissionType.PersonCreateBoat, itemType, turnCount, building.Id, maxValue);
            }
            //building.Workers = sangoObjectList;
            building.LeftCounter = turnCount;
            building.isWorking = true;
#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了舰船生产!开始生产{itemType.Name}, 所需回合:{turnCount}, 建筑:{building.Name}");
#endif
            ClearJobFeature();
            return null;
        }

        /// <summary>
        /// 生产兵装
        /// </summary>
        /// <param name="itemType">物品类型</param>
        /// <param name="buildingId">建筑ID</param>
        /// <param name="totalValue">总价值</param>
        /// <returns>是否成功</returns>
        public bool DoJobCreateBoat(ItemType itemType, int buildingId, int totalValue)
        {
            List<Person> people = new List<Person>();
            allPersons.ForEach(person =>
            {
                if (person.missionType == (int)MissionType.PersonCreateBoat)
                {
                    people.Add(person);
                }
            });
            if (people.Count == 0) return false;

            Scenario scenario = Scenario.Cur;
            Building building = scenario.GetObject<Building>(buildingId);
            if (building == null)
            {
                for (int i = 0; i < people.Count; i++)
                {
                    Person person = people[i];
                    if (person == null) continue;
                    person.ClearMission();
                }
                return false;
            }
            building.isWorking = false;

            InitJobFeature(people.ToArray());
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateBoat;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);


#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < people.Count; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验
                person.ClearMission();
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            Person[] personList = people.ToArray();

            totalValue = GameUtility.Method_CreateItems(totalValue, building.BuildingType.level) / itemType.p1 / 3;
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.CreateBoat);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.Value;


            overrideData.Value = techniquePointGain;
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.Value;

            overrideData.Recycle();

            int totalNum = itemStore.GetNumber((int)ItemStoreKindType.Boat);

            int empty = itemType.TransformLimit(StoreLimit) - totalNum;
            totalValue = Math.Min(empty, totalValue);
            int exsistNumber = itemStore.Add(itemType.storeKind, totalValue);

            BelongForce.GainTechniquePoint(techniquePointGain);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了船只生产!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}, 建筑:{building.Name}");
#endif

            Render?.ShowInfo(totalValue, itemType.Id + 1);
            ClearJobFeature();
            return true;
        }



        /// <summary>
        /// 生产器械
        /// </summary>
        /// <param name="personList">生产人员</param>
        /// <param name="itemType">器械的物品类型</param>
        /// <param name="building">建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>生产信息数组</returns>
        public int[] JobCreateMachine(Person[] personList, ItemType itemType, Building building, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return null;
            if (itemType == null) return null;

            if (itemType.kind != (int)ItemKindType.Machine) return null;
            int totalNum = itemStore.GetNumber((int)itemType.storeKind);
            if (totalNum > itemType.TransformLimit(StoreLimit)) return null;


            Scenario scenario = Scenario.Cur;
            int empty = itemType.TransformLimit(StoreLimit) - totalNum;

            InitJobFeature(personList);

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateMachine;

            int goldNeed = JobType.GetJobCost(jobId) + itemType.cost;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();

                return null;
            }

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
            int lastTroops = troops;
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseCreativeAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseCreativeAbility;
                }

#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            int subValue = 0;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    subValue += maxPerson.BaseCreativeAbility;
                }
            }


            int turnCount = GameUtility.Method_CreateMachineCounter(maxValue, subValue, building.BuildingType.level);
            overrideData = Tools.OverrideData<int>.Create(turnCount);
            GameEvent.OnCityJobCounterResult?.Invoke(this, jobId, personList, overrideData);
            turnCount = overrideData.ValueAndRecycle;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            maxValue *= 5;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    maxValue += maxPerson.BaseCreativeAbility * 10;
                }
            }

            if (isTest)
            {
                int totalValue = GameUtility.Method_CreateItems(maxValue, building.BuildingType.level) / itemType.p1 / 3;
                overrideData = Tools.OverrideData<int>.Create(totalValue);
                GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
                totalValue = overrideData.ValueAndRecycle;
                ClearJobFeature();
                return new int[] { turnCount, totalValue };
            }

            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            building.ActionOver = true;
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();

            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
                person.ActionOver = true;
                person.SetMission(MissionType.PersonCreateMachine, itemType, turnCount, building.Id, maxValue);
            }
            //building.Workers = sangoObjectList;
            building.LeftCounter = turnCount;
            building.isWorking = true;

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了器械生产!开始生产{itemType.Name}, 所需回合:{turnCount}, 建筑:{building.Name}");
#endif
            Render?.UpdateRender();
            ClearJobFeature();
            return null;
        }

        /// <summary>
        /// 执行器械生产任务
        /// </summary>
        /// <param name="itemType">物品类型</param>
        /// <param name="buildingId">建筑ID</param>
        /// <param name="totalValue">总价值</param>
        /// <returns>是否成功</returns>
        public bool DoJobCreateMachine(ItemType itemType, int buildingId, int totalValue)
        {
            List<Person> people = new List<Person>();
            allPersons.ForEach(person =>
            {
                if (person.missionType == (int)MissionType.PersonCreateMachine)
                {
                    people.Add(person);
                }
            });
            if (people.Count == 0) return false;
            Scenario scenario = Scenario.Cur;
            Building building = scenario.GetObject<Building>(buildingId);
            if (building == null)
            {
                for (int i = 0; i < people.Count; i++)
                {
                    Person person = people[i];
                    if (person == null) continue;
                    person.ClearMission();
                }
                return false;
            }
            building.isWorking = false;


            InitJobFeature(people.ToArray());
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateMachine;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < people.Count; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验
                person.ClearMission();
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            Person[] personList = people.ToArray();

            totalValue = GameUtility.Method_CreateItems(totalValue, building.BuildingType.level) / itemType.p1 / 3;
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.CreateMachine);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.Value;

            overrideData.Value = techniquePointGain;
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.Value;

            overrideData.Recycle();

            int empty = itemType.TransformLimit(StoreLimit) - itemStore.GetNumber(itemType.storeKind);
            totalValue = Math.Min(empty, totalValue);
            int exsistNumber = itemStore.Add(itemType.storeKind, totalValue);

            BelongForce.GainTechniquePoint(techniquePointGain);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了器械生产!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}, 建筑:{building.Name}");
#endif

            Render?.ShowInfo(totalValue, itemType.Id + 1);
            ClearJobFeature();
            return true;
        }

        /// <summary>
        /// 农业
        /// </summary>
        /// <param name="personList">农业人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>农业值增加量</returns>
        public int JobFarming(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;
            if (agriculture >= AgricultureLimit) return 0;
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Farming;

            InitJobFeature(personList);

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                totalValue += person.BaseAgricultureAbility;
            }

            totalValue = GameUtility.Method_FarmingAbility(totalValue);
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.Farming);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验
                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            agriculture += totalValue;
            if (agriculture > AgricultureLimit)
                agriculture = AgricultureLimit;

            Render?.ShowInfo(totalValue, (int)InfoType.Food);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了开垦!农业值达到了:{agriculture}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 开发
        /// </summary>
        /// <param name="personList">开发人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>商业值增加量</returns>
        public int JobDevelop(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            if (commerce >= CommerceLimit) return 0;
            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Develop;

            InitJobFeature(personList);
            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                totalValue += person.BaseCommerceAbility;
            }

            totalValue = GameUtility.Method_DevelopAbility(totalValue);
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.Develop);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            commerce += totalValue;
            if (commerce > CommerceLimit)
                commerce = CommerceLimit;

            Render?.ShowInfo(totalValue, (int)InfoType.Gold);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了开发!商业值达到了:{commerce}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 按执行武将的**性格效率系数**折算内政产出。
        ///
        /// 多人执行时取所有执行武将"该项目效率倍率"的**平均值**；
        /// 无有效性格数据（或未启用）时返回原值，不做任何修正。
        /// </summary>
        /// <param name="value">原始产出值</param>
        /// <param name="personList">执行武将</param>
        /// <param name="jobType">内政项目</param>
        /// <returns>折算后的产出值</returns>
        public int ApplyPersonalityDomesticScale(int value, Person[] personList, CityJobType jobType)
        {
            if (value <= 0 || personList == null || personList.Length == 0)
                return value;

            int total = 0;
            int count = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null || person.mPersonality == null)
                    continue;

                int scale = person.mPersonality.GetDomesticScale(jobType);
                if (scale <= 0)
                    scale = 100;

                total += scale;
                count++;
            }

            if (count == 0)
                return value;

            // 平均性格效率系数（%）
            long avgScale = total / count;
            if (avgScale == 100)
                return value;

            long result = (long)value * avgScale / 100;
            if (result > int.MaxValue) result = int.MaxValue;
            return (int)result;
        }

        /// <summary>
        /// 取得执行武将的**性格内政效率平均值**（%，100 = 不修正）。
        /// </summary>
        /// <param name="personList">执行武将</param>
        /// <param name="jobType">内政项目</param>
        /// <returns>平均效率倍率；无有效数据时返回 100</returns>
        public int GetPersonalityDomesticScale(Person[] personList, CityJobType jobType)
        {
            if (personList == null || personList.Length == 0)
                return 100;

            int total = 0;
            int count = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null || person.mPersonality == null)
                    continue;

                int scale = person.mPersonality.GetDomesticScale(jobType);
                if (scale <= 0)
                    scale = 100;

                total += scale;
                count++;
            }

            if (count == 0)
                return 100;
            return total / count;
        }

        /// <summary>
        /// 按执行武将的性格建造效率缩短工期（效率越高工期越短）。
        /// </summary>
        /// <param name="buildCount">原始工期（回合）</param>
        /// <param name="personList">执行武将</param>
        /// <returns>折算后的工期（至少 1 回合）</returns>
        public int ApplyPersonalityBuildCounter(int buildCount, Person[] personList)
        {
            if (buildCount <= 0)
                return buildCount;

            int scale = GetPersonalityDomesticScale(personList, CityJobType.Build);
            if (scale <= 0 || scale == 100)
                return buildCount;

            int result = (int)((long)buildCount * 100 / scale);
            return result < 1 ? 1 : result;
        }

        /// <summary>
        /// 按执行武将的性格搜索效率折算搜索成功率（%）。
        /// </summary>
        /// <param name="chance">原始成功率（%）</param>
        /// <param name="person">执行武将</param>
        /// <returns>折算后的成功率（至少 1）</returns>
        public int ApplyPersonalitySearchChance(int chance, Person person)
        {
            if (chance <= 0 || person == null || person.mPersonality == null)
                return chance;

            int scale = person.mPersonality.domesticSearchScale;
            if (scale <= 0 || scale == 100)
                return chance;

            int result = (int)((long)chance * scale / 100);
            return result < 1 ? 1 : result;
        }

        /// <summary>
        /// 取得武将性格的"忠诚影响值"，由"忠诚维持"与"忠诚漂移"共同构成：
        /// 前者表示性格本身的归附倾向（降低掉忠的概率），后者表示忠诚值的稳定性。
        ///
        /// 语义：这是一个**概率量（百分点）**，不是直接加到忠诚上的数值。
        /// 褒奖时由 <see cref="CalcRewardLoyaltyGain"/> 把它当作"额外提升的触发概率"使用，且只取正值。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <returns>忠诚影响值；无性格数据时返回 0</returns>
        public static int GetLoyaltyKeepAdd(Person person)
        {
            if (person == null || person.mPersonality == null)
                return 0;
            return person.mPersonality.loyaltyKeepAdd + person.mPersonality.loyaltyDriftAdd;
        }

        #region 褒奖忠诚（概率偏移）

        /// <summary>
        /// 某武将当前是否可被褒赏。
        ///
        /// 对齐出处：Open311 `can_bonus`（`s11_sys_person_order_personnel.cpp:1187-1210`）：
        /// <list type="bullet">
        /// <item>存活、属于本势力；</item>
        /// <item>非事件武将 / NPC / 异族（原版 `is_event() or is_iminzoku() or is_bandit()`）；</item>
        /// <item>身份为 都督 / 太守 / 一般，<b>不含君主</b>（原版 `test_mibun({Totoku, Taishu, Ippan})`）；</item>
        /// <item><b>不在部队中</b>（`is_unitize()`）—— 出征 / 运输的武将必然编入部队，由本条挡住；</item>
        /// <item>不处于"任务计时器在跑、任务却为空"的异常态。</item>
        /// <item>本回合未被褒赏（`is_bonus_done()`）；</item>
        /// <item>忠诚 &lt; 100。</item>
        /// </list>
        /// <para>
        /// 玩家界面（<c>CityReward</c>）与执行（<c>JobRewardPerson</c> / <c>JobRewardPersons</c>）
        /// 共用本方法，避免"界面上能点、执行时被拒"或反过来的口径分叉。
        /// </para>
        /// </summary>
        /// <param name="person">待判定的武将</param>
        /// <returns>可褒赏返回 true</returns>
        public bool CanBeRewarded(Person person)
        {
            if (person == null || !person.IsAlive)
                return false;
            if (BelongForce == null || person.BelongForce != BelongForce)
                return false;
            // 事件 / NPC / 异族
            if (person.IsSpecialPerson)
                return false;
            // 身份：都督 / 太守 / 一般，不含君主
            if (person.state != (int)PersonStateType.Commander
                && person.state != (int)PersonStateType.Leader
                && person.state != (int)PersonStateType.Normal)
                return false;
            // 【出征 / 运输者由本条挡住】原版 `is_unitize()`（s11_scen_obj_person.cpp:697-705）
            // 判定"此人是某个部队的成员"；出征与运输都必须先编入部队，所以这里一刀切掉，
            // 不需要再按 missionType 单独判断。
            if (person.mBelongTroop != null)
                return false;
            // 原版：`is_absent() and get_order_id() < 0` → 不可褒赏。
            // is_absent() 的真义是 order_timer_ != 0（任务倒计时未归零，
            // 见 s11_scen_obj_person.cpp:1383-1386），不是"人不在城里"。
            // 因此这条挡的是"倒计时在跑、任务 Id 却为空"的脏数据组合。
            // 单独移动（不带兵，order_id >= 0）的武将不受此条限制 —— 与原版一致。
            if (person.missionCounter != 0 && person.missionType == (int)MissionType.None)
                return false;
            // 本回合已褒赏过
            if (person.IsBonusDone)
                return false;
            // 忠诚已满
            if (person.loyalty >= 100)
                return false;
            return true;
        }

        /// <summary>褒奖的忠诚提升基数 —— 对齐原版 `4 + rand(魅力系数) + (75-相性)/15`</summary>
        public const int RewardLoyaltyBase = 4;

        /// <summary>褒奖的忠诚提升上限 —— 对齐原版 `min(n, 13)`</summary>
        public const int RewardLoyaltyMax = 13;

        /// <summary>
        /// 褒奖（花钱）的忠诚提升量。
        ///
        /// 对齐出处：Open311 `exec_bonus_command`（`s11_sys_person_order_personnel.cpp:1145-1160`）：
        /// <code>
        /// charisma_coef = max(2, 君主魅力 / 17);          // 2..5
        /// n = 4 + rand_int(charisma_coef) + (75 − 相性) / 15;
        /// n = min(n, 13);
        /// </code>
        /// 其中"相性"是目标武将与**本势力君主**的相性距离。
        /// <para>
        /// 【注意】原版有两条容易混淆的命令：
        ///   · `PersonOrderId_Bonus`（0xd，포상）= **花钱褒奖** ← 本方法对齐的就是它；
        ///   · `PersonOrderId_Reward`（0xe，수여）= 授予道具，忠诚 += `item.value`，是另一套。
        /// </para>
        /// </summary>
        /// <param name="person">被褒奖的武将</param>
        /// <param name="kunshu">本势力君主（决定随机系数与相性）</param>
        /// <returns>本次忠诚提升量（4~13）</returns>
        public static int CalcRewardLoyaltyGain(Person person, Person kunshu)
        {
            if (person == null)
                return 0;

            int charismaCoef = 2;
            if (kunshu != null)
                charismaCoef = System.Math.Max(2, kunshu.Glamour / 17);

            int aishou = kunshu != null ? person.CompatibilityDistance(kunshu) : 25;
            int gain = RewardLoyaltyBase
                     + GameRandom.Range(0, charismaCoef)
                     + (75 - aishou) / 15;

            return System.Math.Min(gain, RewardLoyaltyMax);
        }

        #endregion

        /// <summary>
        /// 治安巡视
        /// </summary>
        /// <param name="personList">巡视人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>治安值增加量</returns>
        public int JobInspection(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            if (security >= 100) return 0;

            Scenario scenario = Scenario.Cur;

            InitJobFeature(personList);
            //int barracksLv = GetIntriorBuildingComplateMaxLevel((int)BuildingKindType.PatrolBureau);
            //if (barracksLv == 0) return 0;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Inspection;

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;


            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                totalValue += person.BaseSecurityAbility;
            }

            // 最终数值 —— 对齐原版 `Σ统率/28 + 2`（s11_sys_person_order_city.cpp:1365-1367）
            totalValue = GameUtility.Method_SecurityAbility(totalValue);
            // 兵临城下时巡查效果减半 —— 对齐 calc_patrol_public_order
            // （s11_sys_person_order_city.cpp:1366-1367：`if (building_has_enemy(self)) n /= 2;`）
            if (HasEnemyNearby())
                totalValue /= 2;
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.Inspection);

            // 
            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            // 技巧点 = 治安变化 × 5/7 + 5 —— 对齐原版 s11_sys_person_order_city.cpp:1323
            // （原实现取数据表的固定值 20）
            int techniquePointGain = totalValue * 5 / 7 + 5;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            security += totalValue;
            if (security > 100)
                security = 100;
            AddJobCounter(jobId);
            Render?.ShowInfo(totalValue, (int)InfoType.Security);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了巡视!治安提升到了:{security}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 训练
        /// </summary>
        /// <param name="personList">训练人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>士气增加量</returns>
        public int JobTrainTroops(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            // 没有兵就无从训练 —— 对齐原版 can_drill_command 的 `troops != 0`
            if (troops <= 0) return 0;
            if (morale >= MaxMorale) return 0;
            Scenario scenario = Scenario.Cur;

            InitJobFeature(personList);

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.TrainTroops;

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            // 原版口径：Σ武力 + max武力（s11_sys_person_order_military.cpp:131-156）
            // 旧实现把"最高者的值"重复累加了 n-1 次，与参与者的实际武力无关，属计算错误。
            int sumStrength = 0;
            int maxStrength = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                int ability = person.BaseTrainTroopAbility;
                sumStrength += ability;
                if (ability > maxStrength)
                    maxStrength = ability;
            }

            // 最终数值 —— 3 + (Σ武+max武)/min(20+troops/2000,100)，有训练所再 ×1.5
            bool hasTrainingGround = CityFacilityRequirement.HasGovFacility(
                this, InteriorFacilityKind.TrainingSchool);
            int totalValue = GameUtility.Method_TrainTroops(
                sumStrength, maxStrength, troops, hasTrainingGround);
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.TrainTroops);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            // 技巧点 = 气力变化 / 2 + 5 —— 对齐原版 s11_sys_person_order_military.cpp:95
            // （原实现取数据表的固定值 20）
            int techniquePointGain = totalValue / 2 + 5;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;
            BelongForce.GainTechniquePoint(techniquePointGain);

            gold -= goldNeed;
            morale += totalValue;
            // 走本城口径：军事府可让训练 AP 减半
            BelongCorps.ReduceActionPoint(GetJobCostAP((CityJobType)jobId));
            if (morale > MaxMorale)
                morale = MaxMorale;

            AddJobCounter(jobId);
            Render?.ShowInfo(totalValue, (int)InfoType.Morale);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了训练!士气提升到了:{morale}");
#endif
            ClearJobFeature();
            return totalValue;
        }


        /// <summary>
        /// 登庸武将
        /// </summary>
        /// <param name="person">执行登庸的武将</param>
        /// <param name="dest">目标武将</param>
        /// <returns>是否成功</returns>
        public bool JobRecruitPerson(Person person, Person dest)
        {
            // 【执行前防御】对应 Open311 的 validate_recruit_command：
            //   · 双方武将必须有效；
            //   · 执行者必须挂在本城的空闲名册里（否则会白白从别的城盗走一个人）；
            //   · 目标不能是本势力的人（登用只针对他势力 / 在野）。
            if (person == null || dest == null) return false;
            if (!person.IsAlive || !dest.IsAlive) return false;
            if (!freePersons.Contains(person)) return false;
            if (dest.BelongForce == BelongForce) return false;

            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.RecruitPerson;
            // 走本城口径：人才府可让登用 AP 减半
            int apCost = GetJobCostAP(CityJobType.RecruitPerson);

            freePersons.Remove(person);
            if (dest.CurrentCity == person.CurrentCity)
            {
                CityRecruitPersonEvent te = RenderEvent.Instance.Create<CityRecruitPersonEvent>();
                te.Init(person, dest);
                RenderEvent.Instance.Add(te);
                BelongCorps.ReduceActionPoint(apCost);
                return true;
            }
            else
            {
                person.SetMission(MissionType.PersonRecruitPerson, dest, 100, dest.CurrentCity.Id);
                person.ActionOver = true;
                BelongCorps.ReduceActionPoint(apCost);
                return false;
            }
        }

        /// <summary>
        /// 褒奖武将
        /// </summary>
        /// <param name="persons">要褒奖的武将数组</param>
        /// <returns>是否成功</returns>
        public bool JobRewardPersons(Person[] persons)
        {
            if (!GameUtility.IsValidPersonArray(persons)) return true;


            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Reward;
            int apCost = JobType.GetJobCostAP(jobId);
            int goldCost = JobType.GetJobCost(jobId);
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            // 【先算能负担几人】原实现是"循环里先加忠诚、循环后再统一扣金"，
            // 资金不足时会把 gold 扣成负数，而忠诚已经涨上去了。
            // 门禁（CityReward.IsValid）只能按"1 人份"判断（那时玩家还没选人），
            // 因此这里必须兜底：只褒赏付得起的那几个人。
            int affordable = goldCost > 0 ? gold / goldCost : int.MaxValue;
            int rewarded = 0;

            int totalApCost = 0;
            int totalGoldCost = 0;
            for (int i = 0; i < persons.Length; ++i)
            {
                Person person = persons[i];
                if (person == null) continue;
                // 执行前逐项校验 —— 与界面共用同一口径（对齐原版 can_bonus）
                if (!CanBeRewarded(person)) continue;

                if (rewarded >= affordable)
                {
                    Sango.Log.Info($"[内政]{Name} 资金不足（现有 {gold}，每人 {goldCost}），"
                        + $"本次只褒赏 {rewarded} 人，其余跳过");
                    break;
                }

                totalApCost += apCost;
                totalGoldCost += goldCost;
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                // 褒奖忠诚：4 + rand(魅力系数) + (75-相性)/15，封顶 13（见 CalcRewardLoyaltyGain）
                person.AddLoyalty(CalcRewardLoyaltyGain(person, BelongForce != null ? BelongForce.mGovernor : null));
                person.SetBonusDone(true);
                rewarded++;
            }
            gold -= totalGoldCost;
            BelongCorps.ReduceActionPoint(totalApCost);
            BelongCorps.AddJobCounter(jobId);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>使用资金对{stringBuilder}进行了褒赏!!");
#endif
            return true;
        }


        /// <summary>
        /// 褒奖单个武将
        /// </summary>
        /// <param name="person">要褒奖的武将</param>
        /// <returns>是否成功</returns>
        public bool JobRewardPerson(Person person)
        {
            if (person == null)
                return true;
            // 执行前逐项校验 —— 与界面共用同一口径（对齐原版 can_bonus）
            if (!CanBeRewarded(person))
                return false;

            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Reward;
            int apCost = JobType.GetJobCostAP(jobId);
            int goldCost = JobType.GetJobCost(jobId);
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif

#if SANGO_DEBUG
            stringBuilder.Append(person.Name);
            stringBuilder.Append(",");
            int lastLoyalty = person.loyalty;
#endif
            // 褒奖忠诚：与多人褒奖同一套口径（4 + rand(魅力系数) + (75-相性)/15，封顶 13）。
            // 必须走 AddLoyalty：裸 `loyalty +=` 会把 90 点直接抬到 101，越过 0~100 上限
            // （实测日志：忠诚从 90 提升到 -> 101，随后被换季结算当成"掉忠 1"修正回 100）。
            person.AddLoyalty(CalcRewardLoyaltyGain(person, BelongForce != null ? BelongForce.mGovernor : null));
            person.SetBonusDone(true);
            gold -= goldCost;
            BelongCorps.ReduceActionPoint(apCost);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>使用资金对{stringBuilder}进行了褒赏!! 忠诚从{lastLoyalty}提升到->{person.loyalty}");
#endif
            return true;
        }


        /// <summary>
        /// 授予宝物 / 装备（数据侧）。
        ///
        /// 定位（2026-10 改版）：装备管理操作，**不占行动力、不记回合工作次数、不改忠诚**。
        /// 唯一成本是技巧点，由 <see cref="ItemGrantRules.Grant"/> 内部按件扣除；
        /// 本方法只做校验与转发，因此可以在同一回合连续调用任意多次（够技巧点就行）。
        /// 校验不通过时**不扣技巧点**，避免玩家白付代价。
        /// </summary>
        /// <param name="person">目标武将（须为本城在职武将）</param>
        /// <param name="item">待授予的珍品（须在本势力国库中）</param>
        /// <returns>执行成功返回 true；校验不通过返回 false</returns>
        public bool JobGrantItem(Person person, ItemType item)
        {
            string reason;
            if (!ItemGrantRules.CanGrant(this, person, item, out reason))
            {
                Sango.Log.Warning($"[授予]<{Name}>无法授予{(person != null ? person.Name : "空武将")}" +
                                  $"{(item != null ? item.Name : "空道具")}：{reason}");
                return false;
            }

            string grantReason;
            ItemType replaced = ItemGrantRules.Grant(this, person, item, out grantReason);
            if (replaced == null && !string.IsNullOrEmpty(grantReason))
                return false;

            return true;
        }

        /// <summary>
        /// 没收宝物 / 装备（数据侧）。
        ///
        /// 与授予对称：不占行动力、不记回合工作次数、不改忠诚，
        /// 成本只有技巧点（由 <see cref="ItemGrantRules.Confiscate"/> 内部按件扣除）。
        /// </summary>
        /// <param name="person">目标武将（须为本城在职武将）</param>
        /// <param name="item">待没收的珍品（须在该武将身上或装备栏中）</param>
        /// <returns>执行成功返回 true；校验不通过返回 false</returns>
        public bool JobConfiscateItem(Person person, ItemType item)
        {
            string reason;
            if (!ItemGrantRules.CanConfiscate(this, person, item, out reason))
            {
                Sango.Log.Warning($"[没收]<{Name}>无法没收{(person != null ? person.Name : "空武将")}" +
                                  $"{(item != null ? item.Name : "空道具")}：{reason}");
                return false;
            }

            string confiscateReason;
            ItemType taken = ItemGrantRules.Confiscate(this, person, item, out confiscateReason);
            if (taken == null)
                return false;

            return true;
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="personList">搜索人员</param>
        /// <returns>是否成功</returns>
        public bool JobSearching(Person[] personList)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return false;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                CityPersonSearchingEvent te = RenderEvent.Instance.Create<CityPersonSearchingEvent>();
                te.Init(this, person);
                RenderEvent.Instance.Add(te);
            }
            return true;
        }

        /// <summary>
        /// 把"可被探索到"的人追加进目标列表。
        ///
        /// 对齐原版 can_be_searched（s11_sys_person_order_personnel.cpp:510-529）的排除链：
        /// 死亡 / 事件与 NPC / 异族（原版 `is_event() or is_iminzoku() or is_bandit()`），
        /// 以及"任务正在进行中"（原版 `is_absent() and order_id != -1`，
        /// 其中 `is_absent()` 的真义是 order_timer_ != 0 而非"不在城"）。
        /// 本项目 <see cref="Person.IsSpecialPerson"/> 正好覆盖前两类三选。
        /// </summary>
        /// <param name="source">来源列表（本据点 / 下属港关的未发现人才）</param>
        /// <param name="dest">输出列表</param>
        static void CollectSearchablePersons(List<Person> source, List<Person> dest)
        {
            if (source == null || dest == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                Person p = source[i];
                if (p == null || !p.IsAlive)
                    continue;
                if (p.IsSpecialPerson)
                    continue;
                // 原版 `is_absent() and get_order_id() != -1` —— is_absent() 是
                // order_timer_ != 0（任务倒计时未归零，s11_scen_obj_person.cpp:1383-1386），
                // 即"任务正在进行中"的人不可被搜索到。
                if (p.missionCounter != 0 && p.missionType != (int)MissionType.None)
                    continue;
                dest.Add(p);
            }
        }

        /// <summary>
        /// 执行搜索任务
        /// </summary>
        /// <param name="person">执行搜索的武将</param>
        /// <param name="target">找到的目标武将</param>
        /// <param name="foundItem">找到的宝物 / 装备（已进势力国库）</param>
        /// <returns>
        /// 搜索结果：0 = 找到人才，正数 = 找到的金钱数额，-2 = 找到宝物 / 装备，-1 = 什么也没找到。
        /// 道具结果码取负值是为了不与"金钱数额"混淆（金钱是任意正整数）。
        /// </returns>
        public int DoJobSearching(Person person, out Person target, out ItemType foundItem)
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Searching;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);
            // 走本城口径：人才府可让探索 AP 减半
            int apCost = GetJobCostAP(CityJobType.Searching);
            target = null;
            foundItem = null;
            freePersons.Remove(person);
            InitJobFeature(person);

            BelongCorps.ReduceActionPoint(apCost);

            // 支持发现港关人才，并按"可被搜索"过滤。
            // 对齐原版 can_be_searched（s11_sys_person_order_personnel.cpp:510-529）：
            //   排除事件 / NPC / 异族（原版 `is_event() or is_iminzoku() or is_bandit()` 一条链），
            //   排除已死亡、以及"出征且有任务"的人。
            // 本项目的 invisiblePersons 本身已是"本据点未发现"的子集，所以"据点匹配 + 未发现"天然满足。
            List<Person> invisible = new List<Person>();
            CollectSearchablePersons(invisiblePersons, invisible);
            for (int i = 0; i < subCities.Count; i++)
                CollectSearchablePersons(subCities[i].invisiblePersons, invisible);

            // 发现人才 —— 对齐原版 calc_search_chance（s11_sys_person_order_personnel.cpp:491-508）：
            //   province = 10；本城所属州与执行者出身州相同 → 11
            //   chance = (min(候选数, 5) × 3 + 7) × 政治 × province / 300
            //   "眼力"特技 → 100%（由 Features.json#86 的 CityChangeSearchingWild override 叠加）
            // 原实现是 `20 + 政治×3/5`，与候选人数无关，本次替换。
            int candidateCount = System.Math.Min(invisible.Count, 5);
            int searchProvince = (ProvinceId > 0 && ProvinceId == person.birthplace) ? 11 : 10;
            int probality = (candidateCount * 3 + 7) * person.Politics * searchProvince / 300;
            // 【性格】搜索效率：按执行武将性格折算发现成功率
            probality = ApplyPersonalitySearchChance(probality, person);
            if (invisible.Count > 0)
            {
                Tools.OverrideData<int> overrideData1 = Tools.OverrideData<int>.Create(probality);
                GameEvent.OnCityJobSearchingWild?.Invoke(this, jobId, person, overrideData1);
                probality = overrideData1.ValueAndRecycle;
                if (GameRandom.Chance(probality))
                {
                    target = invisible[GameRandom.Range(0, invisible.Count)];
                    target.state = (int)PersonStateType.Unemployed;

                    if (IsPlayer)
                    {
                        PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}发现人才{target.ColorName}。",
                            BelongForce, x, y);
                    }

#if SANGO_DEBUG
                    Sango.Log.Info($"@内政@[{BelongForce.Name}]<{Name}>的{person.Name}发现了人才->{target.Name}");
#endif
                    target.CurrentCity.RemoveInvisiblePerson(target);
                    target.CurrentCity.AddWildPerson(target);

                    // 搜索成功必得技巧点 —— 对齐原版 on_search_success 的 force_add_tech_point(force, 20)
                    BelongForce?.GainTechniquePoint(20);

                    person.merit += meritGain;
                    person.GainExp(meritGain);
                    person.GainJobAttributeExp(jobId);      // 搜索(找到人物) → 智力经验
                    person.ActionOver = true;
                    ClearJobFeature();
                    return 0;
                }
            }

            // 搜索宝物 / 装备：概率受执行武将政治 / 智力影响，另有开局保护、势力与城市冷却、
            // 累计上限、单件唯一等限流（详见 ForceItemDiscover）。命中后产出**直接进势力国库**。
            if (!person.ActionOver)
            {
                foundItem = ForceItemDiscover.TryDiscover(this, person);
                if (foundItem != null)
                {
                    if (IsPlayer)
                    {
                        // 道具名按品级上色（见 ItemType.ColorName）；下面的日志保持纯文本，不要带 <color> 标记
                        PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}发现{foundItem.ColorName}。",
                            BelongForce, x, y);
                    }

#if SANGO_DEBUG
                    Sango.Log.Info($"@内政@[{BelongForce.Name}]<{Name}>的{person.Name}发现了{foundItem.Name}");
#endif

                    person.merit += meritGain;
                    person.GainExp(meritGain);
                    person.GainJobAttributeExp(jobId);      // 搜索(找到宝物/装备) → 智力经验
                    person.ActionOver = true;
                    ClearJobFeature();
                    return -2;
                }
            }

            // 搜索钱财
            probality = person.Politics * 3 / 5;
            // 【性格】搜索效率：按执行武将性格折算发现成功率
            probality = ApplyPersonalitySearchChance(probality, person);
            if (!person.ActionOver && GameRandom.Chance(probality))
            {
                // 金额 —— 对齐原版：`min(rand(魅力), 80)`，低于 30 视为空手（什么都不给）
                int findGold = System.Math.Min(GameRandom.Range(0, System.Math.Max(person.Glamour, 1)), 80);
                if (findGold >= 30)
                {
                    if (IsPlayer)
                    {
                        PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}发现资金{findGold}。",
                            BelongForce, x, y);
                    }
                    AddGold(findGold);
                    Render?.ShowInfo(findGold, (int)InfoType.Gold);

                    person.merit += meritGain;
                    person.GainExp(meritGain);
                    person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验
                    person.ActionOver = true;
                    ClearJobFeature();
                    return findGold;
                }
            }

            //TODO: 触发事件

            // 什么也没找到
            if (!person.ActionOver)
            {
                if (IsPlayer)
                {
                    PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}什么也没发现。",
                        BelongForce, x, y);
                }

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验
                person.ActionOver = true;
            }

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, new Person[] { person }, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            ClearJobFeature();
            return -1;
        }


        /// <summary>
        /// 治疗伤兵
        /// </summary>
        /// <returns></returns>
        public bool JobHealingTroop()
        {
            // 城池满了不再招募

            if (woundedTroops <= 0) return false;
            int recruitNum = agriculture + commerce;
            int rs = Math.Min(woundedTroops, recruitNum);
            troops += rs;
            woundedTroops -= rs;
#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]<{Name}>进行了士兵治愈!共治愈到{rs}人, 当前士兵提升到了:{troops}");
#endif
            return true;
        }

        /// <summary>
        /// 招募士兵
        /// </summary>
        /// <param name="personList">招募人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>招募的士兵数量</returns>
        public int JobRecruitTroop(Person[] personList, bool isTest = false)
        {
            Building barracks = GetFreeBuilding((int)BuildingKindType.Barracks);
            if (barracks == null) return 0;
            return JobRecruitTroop(personList, barracks, isTest);
        }

        /// <summary>
        /// 招募士兵
        /// </summary>
        /// <param name="personList">招募人员</param>
        /// <param name="barracks">兵营建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>招募的士兵数量</returns>
        public int JobRecruitTroop(Person[] personList, Building barracks, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            // 城池满了不再招募
            if (TroopsIsFull) return 0;

            Scenario scenario = Scenario.Cur;
            InitJobFeature(personList);
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.RecruitTroops;

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                totalValue += person.BaseRecruitmentAbility;

            }

            // 对齐原版：基准含治安项 `(治安+20) × Σ魅力 × 5/100`（s11_sys_scen_city.cpp:582-607）
            // "名声"的 ×1.5 由 Features.json#80 的 CityImproveJobResult override 后续叠加
            totalValue = GameUtility.Method_RecruitTroops(
                totalValue, security, barracks.BuildingType.level);

            // 兵临城下时征兵量减半 —— 对齐 get_recruit_troops
            // （s11_sys_person_order_city.cpp:602-603：`if (building_has_enemy(self)) n /= 2;`）
            if (HasEnemyNearby())
                totalValue /= 2;

            // 技巧点 = min(1 + 征兵数/300, 10) —— 对齐原版（数据表里写死 20 的不适用）
            int techniquePointGain = System.Math.Min(1 + totalValue / 300, 10);
            // 【性格】按执行武将的性格效率折算（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.RecruitTroops);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            // 治安对征兵的影响
            totalValue = (int)(totalValue * (1f - Math.Max(0, (100 - security)) * variables.securityInfluenceRecruitTroops));

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            if (Scenario.Cur.Variables.populationEnable)
            {
                totalValue = Math.Min(totalValue, troopPopulation);
            }

            if (totalValue + troops > TroopsLimit)
                totalValue = TroopsLimit - troops;

            if (isTest)
            {

                ClearJobFeature();
                return totalValue;
            }

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
            int lastTroops = troops;
#endif

            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验
                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            if (Scenario.Cur.Variables.populationEnable)
            {
                troopPopulation -= totalValue;
                population -= totalValue;
            }

            //士气减少
            morale = (troops * morale + totalValue * 30) / (troops + totalValue);
            troops += totalValue;
            gold -= goldNeed;

            //治安减少
            security -= Math.Min(6, 4 * totalValue / 1000);
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            BelongForce.GainTechniquePoint(techniquePointGain);

            barracks.ActionOver = true;

            Render?.UpdateRender();
            Render?.ShowInfo(totalValue, (int)InfoType.Troop);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了招募!共招募到{troops - lastTroops}人, 当前士兵人数提升到了:{troops}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 生产兵装
        /// </summary>
        /// <param name="personList">生产人员</param>
        /// <param name="itemType">物品类型</param>
        /// <param name="building">生产建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>生产的兵装数量</returns>
        public int JobCreateItems(Person[] personList, ItemType itemType, Building building, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            Scenario scenario = Scenario.Cur;

            // 【顺序修正】原先 `isTest` 的空值兜底写在对 itemType 的解引用之后，
            // 一旦 itemType 为 null 会先 NRE，兜底永远走不到。
            if (isTest && itemType == null)
            {
                building = GetFreeBuilding((int)BuildingKindType.BlacksmithShop);
                itemType = scenario.GetObject<ItemType>((int)ItemKindType.Weapon);
            }
            // 执行前防御：没有目标道具类型就没有生产对象
            if (itemType == null) return 0;
            if (building == null) return 0;

            int totalNum = itemStore.GetNumber(itemType.storeKind);
            if (totalNum >= itemType.TransformLimit(StoreLimit)) return 0;

            if (itemType == null) return 0;
            if (building == null) return 0;

            InitJobFeature(personList);

            int empty = itemType.TransformLimit(StoreLimit) - totalNum;

            ScenarioVariables variables = scenario.Variables;
            int jobId = itemType.Id == 5 ? (int)CityJobType.CreateHorse : (int)CityJobType.CreateItems;

            int goldNeed = JobType.GetJobCost(jobId) + itemType.cost;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseCreativeAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseCreativeAbility;
                }
            }

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    totalValue += maxPerson.BaseCreativeAbility * 5;
                }
                else
                {
                    totalValue += maxPerson.BaseCreativeAbility * 10;
                }
            }

            totalValue = GameUtility.Method_CreateItems(totalValue, building.BuildingType.level);

            // 兵器 / 船只每次固定产出 1 件 —— 对齐 Open311 `s11_sys_scen_building.cpp:1092-1093`
            int storeKind = itemType.storeKind;
            if (storeKind == (int)ItemStoreKindType.Helepolis
                || storeKind == (int)ItemStoreKindType.Catapult
                || storeKind == (int)ItemStoreKindType.Boat)
            {
                if (totalValue > 0)
                    totalValue = 1;
            }

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;
            totalValue = Math.Min(empty, totalValue);

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 内政工作 → 对应属性经验

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            int exsistNumber = itemStore.Add(itemType.Id, totalValue);

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;
            building.ActionOver = true;
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            BelongForce.GainTechniquePoint(techniquePointGain);

            if (itemType.Id == 2)
                Render?.ShowInfo(totalValue, 1);
            else if (itemType.Id == 5)
                Render?.ShowInfo(totalValue, itemType.Id + 1);
            else
                Render?.ShowInfo(totalValue, itemType.Id);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了生产兵装!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}, 建筑:{building.Name}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 交易粮食
        /// </summary>
        /// <param name="personList">交易人员</param>
        /// <param name="goldNum">交易的金钱数量（正数为购买粮食，负数为出售粮食）</param>
        /// <returns>是否成功</returns>
        public bool JobTradeFood(Person[] personList, int goldNum)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return false;
            if (goldNum == 0) return false;
            if (hasBusiness == 0) return false;

            // 【资金复核】门禁 `CityTrade.IsValid` 查的是 `CheckJobCost(TradeFood)`，
            // 而 JobTypes.json 里该工作的 cost 是 0 —— 等于恒真；
            // 实际扣的是滑条金额 `goldNum`。买粮（正数）必须在此复核，否则能把资金扣成负数。
            if (goldNum > 0 && gold < goldNum)
            {
                Sango.Log.Info($"[内政]{Name} 资金不足，无法买粮（需 {goldNum}，现有 {gold}）");
                return false;
            }

            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.TradeFood;

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

            Person person = personList[0];
            if (person == null) return false;

            InitJobFeature(personList);

            person.merit += meritGain;
            person.GainExp(meritGain);
            person.GainJobAttributeExp(jobId);          // 交易粮食 → 政治经验
            freePersons.Remove(person);
            person.ActionOver = true;

            // 原版交易价格（s11_sys_person_order_city.cpp:1490-1529 的 calc_merchant）：
            //   pol_coef = 450 - 政治
            //   买：gold = food × pol_coef × 10 / (rate × 400)
            //   卖：gold = food × -3200 / (pol_coef × rate)
            // 其中 rate 是"粮价倒数"（越大粮越便宜）：
            //   原文注释的对照表为 rate=1 时 100 粮要 1000 金、rate=100 时只要 10 金。
            // 原版按"粮数"下单，本项目接口按"金"下单（买），卖出时 goldNum 已是粮数（负），
            // 因此买入方向用反算。
            int rate = System.Math.Max(this.rate, 1);
            int polCoef = System.Math.Max(450 - person.Politics, 1);

            int totalValue;
            if (goldNum > 0)
            {
                // 花 goldNum 金能买到的粮
                totalValue = goldNum * rate * 400 / (polCoef * 10);
            }
            else
            {
                // 卖出 -goldNum 粮能换到的金
                totalValue = (-goldNum) * 3200 / (polCoef * rate);
            }

            // 【性格】交易效率：按执行武将性格调整交易量（多人取平均）
            totalValue = ApplyPersonalityDomesticScale(totalValue, personList, CityJobType.TradeFood);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.Value;

            overrideData.Value = techniquePointGain;
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.Value;

            overrideData.Recycle();

            if (goldNum > 0)
            {
                if (totalValue + food > foodLimit)
                    totalValue = foodLimit - food;
                if (totalValue <= 0)
                    return false;

                AddGold(-goldNum);
                AddFood(totalValue);

                Render?.ShowInfo(-goldNum, (int)InfoType.Gold);
                Render?.ShowInfo(totalValue, (int)InfoType.Food);
#if SANGO_DEBUG
                Sango.Log.Info($"@内政@[{BelongForce.Name}]{person.Name}在<{Name}>花费{goldNum}交易到了{totalValue}粮食, 现有粮食:{food}");
#endif
            }
            else
            {
                if (totalValue + gold > goldLimit)
                    totalValue = goldLimit - gold;
                if (totalValue <= 0)
                    return false;

                AddGold(totalValue);
                AddFood(goldNum);

                Render?.ShowInfo(goldNum, (int)InfoType.Gold);
                Render?.ShowInfo(totalValue, (int)InfoType.Food);

#if SANGO_DEBUG
                Sango.Log.Info($"@内政@[{BelongForce.Name}]{person.Name}在<{Name}>花费{-goldNum}交易到了{totalValue}资金, 现有资金:{gold}");
#endif
            }

            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            AddJobCounter(jobId);

            BelongForce.GainTechniquePoint(techniquePointGain);

            ClearJobFeature();
            return true;
        }

        /// <summary>
        /// 获取城池的攻击力
        /// </summary>
        /// <returns></returns>
        public override int GetAttack()
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            // 根据太守数值来计算基础伤害
            int atk = Math.Max(BuildingType.atk, (Leader?.Strength ?? 50 * 5000 + Leader?.Command ?? 50 * 5000) / 10000);

            return atk;
        }

        /// <summary>
        /// 获取城池的反击力
        /// </summary>
        /// <returns>城池的反击力</returns>
        public override int GetAttackBack()
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            // 根据太守数值来计算基础伤害
            int atk = Math.Max(BuildingType.atkBack, (Leader?.Strength ?? 50 * 5000 + Leader?.Command ?? 50 * 5000) / 10000);

            return atk;
        }


        /// <summary>
        /// 获取城池的防御力
        /// </summary>
        /// <returns></returns>
        public override int GetDefence()
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;

            // 根据太守数值来计算基础防御
            int def = Math.Max(70, (Leader?.Intelligence ?? 70 * 3000 + Leader?.Command ?? 70 * 7000) / 10000);

            return def;
        }

        /// <summary>
        /// 获取可用于技能的兵力
        /// </summary>
        /// <returns>可用于技能的兵力</returns>
        public override int GetSkillMethodAvaliabledTroops()
        {
            return Math.Max(5000, durability * troops / DurabilityLimit);
        }

        public struct EnemyInfo
        {
            public Troop troop;
            public int distance;
        }

        protected const int SAVE_ROUND = 40;
        protected List<EnemyInfo> enemies = new List<EnemyInfo>();
        protected bool[] enemiesRound = new bool[SAVE_ROUND];

        /// <summary>
        /// 获取最近的敌人部队
        /// </summary>
        /// <param name="checkCell">检查的单元格</param>
        /// <returns>最近的敌人部队</returns>
        public Troop GetNearestEnemy(Cell checkCell)
        {
            Troop target = null;
            int dis = 999999;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInfo enemyInfo = enemies[i];
                int distance = Scenario.Cur.Map.Distance(enemyInfo.troop.cell, checkCell);
                if (distance < dis)
                {
                    target = enemyInfo.troop;
                }
            }
            return target;
        }

        /// <summary>
        /// 本据点周边是否还有**可站立部队**的空地块。
        ///
        /// 对齐出处：Open311 `has_empty_place`（s11_sys_person_order_military.cpp:358-363）：
        /// 以据点为中心扫一圈，**都市半径 2 格、关隘港口半径 1 格**。
        /// <para>
        /// 【比原版严格一处】原版只判 `hex.is_empty()`（无部队、无建筑），
        /// 但据点可能贴着山岭或海面 —— 那些格子在 <c>is_empty</c> 意义上也是"空"的，
        /// 部队却站不上去。因此这里额外要求地形 `moveable`（<see cref="Cell.CanMove"/>），
        /// 否则会出现"界面没置灰、点进去却建不出队"的情况。
        /// </para>
        /// </summary>
        /// <returns>存在可站立部队的空地返回 true</returns>
        public bool HasEmptyCellForTroop()
        {
            Cell center = CenterCell;
            if (center == null)
                return false;

            int radius = IsCity() ? 2 : 1;
            for (int i = 0; i < 6; i++)
            {
                Cell ring1 = center.GetNeighbor(i);
                if (ring1 == null)
                    continue;
                if (CanStand(ring1))
                    return true;
                if (radius < 2)
                    continue;

                // 半径 2：沿 1 环格再外扩一圈（六边形 6 方向）
                for (int j = 0; j < 6; j++)
                {
                    if (CanStand(ring1.GetNeighbor(j)))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 该格能否站下一支部队：自身为空（无部队、无建筑）**且**地形可通行。
        /// </summary>
        /// <param name="cell">待判定的格子</param>
        /// <returns>可站立返回 true</returns>
        static bool CanStand(Cell cell)
        {
            if (cell == null || !cell.IsEmpty())
                return false;
            // TerrainType.CanMoveBy 目前只返回 moveable 字段，与部队无关，传 null 安全。
            return cell.CanMove(null);
        }

        /// <summary>某格上是否有敌方部队。</summary>
        /// <param name="cell">待判定的格子</param>
        /// <returns>有敌方部队返回 true</returns>
        bool HasEnemyTroopOn(Cell cell)
        {
            return cell != null && cell.troop != null && cell.troop.IsEnemy(this);
        }

        /// <summary>
        /// 本城执行某工作时的**实际行动力消耗**（已计入建筑减半效果）。
        ///
        /// PK 版新增的四座幕僚建筑都有一项"某项工作 AP 减半"的效果，原版各自写在自己的命令里：
        /// <list type="bullet">
        /// <item>**军事府**：训练 / 出阵 —— `s11_sys_person_order_military.cpp:114-124`、`:365-375`</item>
        /// <item>**人才府**：探索 / 登用 —— `s11_sys_person_order_personnel.cpp:471-473`、`:899-901`</item>
        /// <item>**计略府**：破坏 / 协作 / 流言 —— `s11_sys_person_order_strategy.cpp`（各命令 AP 计算处）</item>
        /// <item>**外交府**：外交八项 —— `s11_sys_person_order_diplomacy.cpp:359,371,699,950,1224,1546,1899,2328`</item>
        /// </list>
        /// 本项目原先所有命令都直接调 `JobType.GetJobCostAP`，完全没有这层判断。
        /// 收敛到本方法后，各命令只需把 `JobType.GetJobCostAP((int)job)` 换成
        /// `GetJobCostAP(job)`，判定口径就统一了（也避免每处各写一遍设施名）。
        /// </summary>
        /// <param name="job">工作类型</param>
        /// <returns>扣除减半效果后的实际 AP 消耗</returns>
        public int GetJobCostAP(CityJobType job)
        {
            int cost = JobType.GetJobCostAP((int)job);
            if (cost <= 0)
                return cost;

            InteriorFacilityKind office;
            switch (job)
            {
                // 军事府：训练 / 出阵
                case CityJobType.TrainTroops:
                case CityJobType.MakeTroop:
                    office = InteriorFacilityKind.MilitaryOffice;
                    break;

                // 人才府：探索 / 登用
                case CityJobType.Searching:
                case CityJobType.RecruitPerson:
                    office = InteriorFacilityKind.PersonnelOffice;
                    break;

                // 计略府：三个计略
                case CityJobType.TwoTigers:
                case CityJobType.TigerDevour:
                case CityJobType.Rumor:
                    office = InteriorFacilityKind.StrategemOffice;
                    break;

                // 外交府：亲善 / 同盟 / 弃盟
                // （停战 / 劝降 / 交换 / 援军在本项目复用 Alliance 的成本，一并覆盖）
                case CityJobType.SendGift:
                case CityJobType.Alliance:
                case CityJobType.DiscardAlliance:
                    office = InteriorFacilityKind.DiplomaticOffice;
                    break;

                default:
                    return cost;
            }

            return CityFacilityRequirement.HasGovFacility(this, office) ? cost / 2 : cost;
        }

        /// <summary>
        /// **兵临城下**：敌军是否在地图半径内。
        ///
        /// 对齐出处：Open311 `building_has_enemy`（s11_sys_scen_building.cpp:1259-1269）：
        /// 以据点为中心**按地图格距**扫一圈，都市半径 3、关隘港口半径 2。
        /// 用途：被围时禁止开发 / 交易 / 研究 / 拆除 / 移动 / 召唤，征兵与巡查减半，并触发逃兵。
        /// <para>
        /// 【与 <see cref="IsEnemiesRound"/> 的区别 —— 两者不是一回事】
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <b>本方法（兵临城下）</b>：纯地图距离，只看格子坐标，与"城属于谁、区域怎么划"无关。
        /// 这是原版 `building_has_enemy` 的语义，所有命令前置都用它。
        /// </item>
        /// <item>
        /// <b><see cref="IsEnemiesRound"/></b>：数据源是 <see cref="PrepareEnemiesInfo"/> 遍历
        /// <c>areaCellList</c>（本城直辖区域格）得到的快照 —— 敌军站在半径内、但不在本城区域内时
        /// **不会**被它计入。这个语义更接近原版的 `map_has_enemy`（AI 态势用，半径 5/4）。
        /// </item>
        /// </list>
        /// <para>
        /// 早期实现直接拿 <c>IsEnemiesRound(3)</c> 当兵临城下，会漏判"敌军贴脸但不在本城区域内"的情况，
        /// 导致被围时命令仍可点、逃兵不触发。本次改为独立的地图半径扫描。
        /// </para>
        /// <para>
        /// 【注意】原版**没有**"据点被完全围死（所有邻格被占）"这个独立概念，
        /// `encircling` 是外交上的"包围网"事件，与据点走投无路无关。因此这里只有半径判定。
        /// </para>
        /// </summary>
        /// <returns>半径内有敌方部队返回 true</returns>
        public bool HasEnemyNearby()
        {
            Cell center = CenterCell;
            if (center == null)
                return false;

            int radius = IsCity() ? 3 : 2;

            // 【为什么不用 Map.GetDirectSpiral】它是 BFS，只在 `moveAble` 的格子上扩散，
            // 得到的深度是"**路径可走到的跳数**"而不是坐标距离：
            // 城池贴山、半岛、隔水时，几何上只差 1~2 格的敌军可能因为绕路而被算成 4~5 跳，
            // 从而**漏判兵临城下**（城被围着却还能开发 / 交易 / 召唤）。
            // 原版 `building_has_enemy` 用的是 `range_contains(pos, 1, 3)` —— 纯坐标距离，不看地形。
            // 这里改为按六边形邻格逐环展开，与坐标距离严格一致。
            for (int i = 0; i < 6; i++)
            {
                Cell ring1 = center.GetNeighbor(i);
                if (HasEnemyTroopOn(ring1))
                    return true;
                if (radius < 2 || ring1 == null)
                    continue;

                for (int j = 0; j < 6; j++)
                {
                    Cell ring2 = ring1.GetNeighbor(j);
                    if (HasEnemyTroopOn(ring2))
                        return true;
                    if (radius < 3 || ring2 == null)
                        continue;

                    for (int k = 0; k < 6; k++)
                    {
                        if (HasEnemyTroopOn(ring2.GetNeighbor(k)))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 检查指定回合是否有敌人
        /// </summary>
        /// <param name="round">回合数</param>
        /// <returns>是否有敌人</returns>
        public bool IsEnemiesRound(int round)
        {
            if (round < enemiesRound.Length)
                return enemiesRound[round];
            return false;
        }
        /// <summary>
        /// 检查是否有敌人
        /// </summary>
        /// <returns>是否有敌人</returns>
        public bool IsEnemiesRound()
        {
            for (int i = 0; i < enemiesRound.Length; i++)
            {
                if (enemiesRound[i]) return true;
            }
            return false;
        }

        /// <summary>
        /// 圈层：距国境的跳数（0 = 边境）。
        /// <para>
        /// 港关本身不参与圈层计算（<see cref="borderLine"/> 只在都市上刷新，见 Force 的圈层扩散），
        /// 因此港关一律按归属都市推导。
        /// </para>
        /// 说明：该方法原来在已删除的 CityEstablishment 里（人才部署体系），
        /// 现收归 City 自身 —— 它读的就是 <see cref="borderLine"/>，与原版
        /// outpost distance_from_border 是同一个概念。
        /// </summary>
        /// <returns>圈层</returns>
        public int ResolveRing()
        {
            if (IsPort() || IsGate())
            {
                City parent = BelongCity;
                if (parent != null && parent != this)
                    return parent.ResolveRing();
            }
            return borderLine;
        }

        /// <summary>
        /// 未发现武将数量（本城 + 所属港关）。
        /// 供城池 AI 的"搜索"命令评估探索价值。
        /// </summary>
        /// <returns>未发现武将数</returns>
        public int CountInvisiblePersons()
        {
            return CountPersonPool(true);
        }

        /// <summary>
        /// 在野武将数量（本城 + 所属港关）。
        /// 供城池 AI 的"登用"命令评估招募价值。
        /// </summary>
        /// <returns>在野武将数</returns>
        public int CountWildPersons()
        {
            return CountPersonPool(false);
        }

        /// <summary>
        /// 统计本城与所属港关的人才池数量。
        /// </summary>
        /// <param name="invisible">true 统计未发现武将，false 统计在野武将</param>
        /// <returns>数量</returns>
        int CountPersonPool(bool invisible)
        {
            int count = 0;
            if (invisible)
            {
                if (invisiblePersons != null) count += invisiblePersons.Count;
            }
            else
            {
                if (wildPersons != null) count += wildPersons.Count;
            }

            if (subCities != null)
            {
                for (int i = 0; i < subCities.Count; i++)
                {
                    City sub = subCities[i];
                    if (sub == null)
                        continue;
                    if (invisible)
                    {
                        if (sub.invisiblePersons != null) count += sub.invisiblePersons.Count;
                    }
                    else
                    {
                        if (sub.wildPersons != null) count += sub.wildPersons.Count;
                    }
                }
            }
            return count;
        }


        /// <summary>
        /// 检查敌人是否存活
        /// </summary>
        /// <param name="enemyInfo">存活的敌人信息</param>
        /// <returns>是否有存活的敌人</returns>
        public bool CheckEnemiesIfAlive(out EnemyInfo enemyInfo)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInfo check = enemies[i];
                if (check.troop.IsAlive)
                {
                    enemyInfo = check;
                    return true;
                }
            }
            enemyInfo = default;
            return false;
        }

        /// <summary>
        /// 检查是否有存活的敌人
        /// </summary>
        /// <returns>是否有存活的敌人</returns>
        public bool CheckEnemiesIfAlive()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInfo check = enemies[i];
                if (check.troop.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }

        /// 这几个属性提供给AI使用
        internal Troop CurActiveTroop = null;
        internal MissionType TroopMissionType = MissionType.None;
        internal int TroopMissionTargetId;

        /// <summary>
        /// 确保部队初始化并加入场景
        /// </summary>
        /// <param name="troop">要确保的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>确保后的部队</returns>
        public Troop EnsureTroop(Troop troop, Scenario scenario)
        {
            // 先加入剧本才能分配ID
            troop.cell = this.CenterCell;
            this.CenterCell.troop = troop;
            scenario.Add(troop);
            troop.Init(scenario);
            return troop;
        }

        /// <summary>
        /// 据点级 AI：被军团调度到时，按当前据点状态跑一遍命令队列。
        ///
        /// 对齐出处：Open311 s11_game_ai_kyoten.cpp:5-23 的 据点主流程。
        /// 与原 <see cref="DoAI"/> 的区别：
        ///   · DoAI 是"本城本回合的全部命令一次跑完"（旧模型的回合制命令链）；
        ///   · DoOutpostAI 是"军团分给我多少行动力，我就花多少"（原版的预算模型），
        ///     同回合内可能被调用一次；命令内部的 <c>OutpostRuntime.actionPoint</c> 决定能花多久。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public bool DoOutpostAI(Scenario scenario)
        {
            return CityAI.DoOutpostAI(this, scenario);
        }

        /// <summary>
        /// 执行AI逻辑
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (BelongForce == null)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                GameEvent.OnCityAIStart?.Invoke(this, scenario);
                AIPrepared = true;
            }

            if (CurActiveTroop != null)
            {
                if (CurActiveTroop.IsAlive)
                {
                    if (!CurActiveTroop.DoAI(scenario))
                        return false;
                }
                CurActiveTroop = null;
            }

            // 用游标顺序推进命令队列，避免 RemoveAt(0) 的 O(n) 搬移（整队跑完由 O(n²) 降为 O(n)）。
            while (aiCommandIndex < AICommandList.Count)
            {
                System.Func<City, Scenario, bool> CurrentCommand = AICommandList[aiCommandIndex];
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                aiCommandIndex++;
            }

            // 整队跑完：清空队列并复位游标，供下一回合 AIPrepare 重新装填
            AICommandList.Clear();
            aiCommandIndex = 0;

            GameEvent.OnCityAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            ActionOver = true;
            return true;
        }

        /// <summary>
        /// 准备敌人信息
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public virtual void PrepareEnemiesInfo(Scenario scenario)
        {
            // 准备敌人信息
            enemies.Clear();
            for (int i = 0; i < enemiesRound.Length; i++)
                enemiesRound[i] = false;

            for (int i = 0; i < areaCellList.Count; i++)
            {
                Cell cell = areaCellList[i];
                if (cell.troop != null && cell.troop.IsEnemy(this))
                {
                    int round = scenario.Map.Distance(CenterCell, cell);
                    if (round < SAVE_ROUND)
                    {
                        enemies.Add(new EnemyInfo { troop = cell.troop, distance = round });
                        for (int j = round; j < enemiesRound.Length; j++)
                            enemiesRound[j] = true;
                    }
                }
            }

            if (enemies.Count > 1)
            {
                enemies.Sort((a, b) =>
                {
                    return a.distance.CompareTo(b.distance);
                });
            }
        }

        /// <summary>
        /// 准备AI逻辑
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public virtual void AIPrepare(Scenario scenario)
        {
            // 准备敌人信息
            PrepareEnemiesInfo(scenario);

            UpdateActiveTroopTypes();
            UpdateFightPower();

            //if (IsBorderCity)
            //{

            //    AICommandList.Add(CityAI.AIAttack);
            //    AICommandList.Add(CityAI.AITradeFood);
            //    //AICommandList.Add(CityAI.AISecurity);

            //    //if (troops < 20000)
            //    //{
            //    //    AICommandList.Add(CityAI.AIRecruitTroop);
            //    //    AICommandList.Add(CityAI.AIIntrior);
            //    //}
            //    //else
            //    //{
            //    //    if (scenario.Info.day == 10)
            //    //    {
            //    //        AICommandList.Add(CityAI.AIRecruitTroop);
            //    //        AICommandList.Add(CityAI.AICreateItems);
            //    //        AICommandList.Add(CityAI.AIIntrior);
            //    //    }
            //    //    else if (scenario.Info.day == 20)
            //    //    {
            //    //        AICommandList.Add(CityAI.AIIntrior);
            //    //        AICommandList.Add(CityAI.AIRecruitTroop);
            //    //        AICommandList.Add(CityAI.AICreateItems);
            //    //    }
            //    //    else
            //    //    {
            //    //        AICommandList.Add(CityAI.AICreateItems);
            //    //        AICommandList.Add(CityAI.AIRecruitTroop);
            //    //        AICommandList.Add(CityAI.AIIntrior);
            //    //    }
            //    //}
            //    AICommandList.Add(CityAI.AIIntrior);
            //}
            //else
            //{
            //    //AICommandList.Add(CityAI.AISecurity);
            //    AICommandList.Add(CityAI.AITradeFood);
            //    // 物资输送
            //    AICommandList.Add(CityAI.AITransfrom);
            //    //if (troops < itemStore.TotalNumber)
            //    //    AICommandList.Add(CityAI.AIRecruitTroop);
            //    //else
            //    //    AICommandList.Add(CityAI.AICreateItems);
            //    AICommandList.Add(CityAI.AIIntrior);
            //}

            GameEvent.OnCityAIPrepare?.Invoke(this, scenario);
        }

        /// <summary>
        /// 检查是否太守需要重新设置
        /// </summary>
        /// <param name="person">原太守</param>
        public void CheckIfLoseLeader(Person person)
        {
            if (Leader != person) return;

            Person dest = null;
            Official higher = null;
            int commandHigher = 0;
            for (int i = 0; i < allPersons.Count; i++)
            {
                Person checker = allPersons[i];
                if (checker != null && checker != Leader && checker.IsAlive)
                {
                    if (dest == null)
                    {
                        dest = checker;
                        higher = dest.Official;
                        commandHigher = dest.Command;
                    }
                    else
                    {
                        if (checker.Official.level > higher.level)
                        {
                            dest = checker;
                            higher = dest.Official;
                            commandHigher = dest.Command;
                        }
                        else if (checker.Official.level == higher.level)
                        {
                            if (checker.Command > commandHigher)
                            {
                                dest = checker;
                                higher = dest.Official;
                                commandHigher = dest.Command;
                            }
                        }
                    }
                }
            }
            Leader = dest;
        }

        public void NeedUpdateLeader()
        {
            needUpdateLeader = true;
        }

        /// <summary>
        /// 更新太守
        /// </summary>
        public void UpdateNewLeader()
        {
            needUpdateLeader = false;
            Person dest = null;
            Official higher = null;
            int commandHigher = 0;
            for (int i = 0; i < allPersons.Count; i++)
            {
                Person checker = allPersons[i];
                if (CanBeLeader(checker))
                {
                    if (checker.IsGovernor)
                    {
                        dest = checker;
                        break;
                    }
                }
            }

            if (dest == null)
            {
                for (int i = 0; i < allPersons.Count; i++)
                {
                    Person checker = allPersons[i];
                    if (CanBeLeader(checker))
                    {
                        if (checker.IsCommander)
                        {
                            dest = checker;
                            break;
                        }
                    }
                }
            }

            if (dest == null)
            {
                for (int i = 0; i < allPersons.Count; i++)
                {
                    Person checker = allPersons[i];
                    if (CanBeLeader(checker))
                    {
                        if (dest == null)
                        {
                            dest = checker;
                            higher = dest.Official;
                            commandHigher = dest.Command;
                        }
                        else
                        {
                            if (checker.Official.level > higher.level)
                            {
                                dest = checker;
                                higher = dest.Official;
                                commandHigher = dest.Command;
                            }
                            else if (checker.Official.level == higher.level)
                            {
                                if (checker.Command > commandHigher)
                                {
                                    dest = checker;
                                    higher = dest.Official;
                                    commandHigher = dest.Command;
                                }
                            }
                        }
                    }
                }
            }

            // 旧太守不再担任本城太守,必须先降级为一般武将。
            // 规则2:状态必须与所属匹配。若只提升新太守而不降级旧太守,
            // 就会出现同一势力内存在多个"太守"状态的武将
            if (Leader != null && Leader != dest && Leader.IsLeader)
            {
                Leader.SetStateNormal();
            }

            Leader = dest;
            Leader?.SetStateLeader();
        }

        /// <summary>
        /// 是否具备担任本城太守的资格（对应 Open311 的 Person::can_be_taishu）。
        ///
        /// 必须同时满足：活着、不是俘虏、属于本城、且"人在本城待命"。
        /// 出征中的部队成员、正在城与城之间移动的武将、被关在别处的俘虏都不能当太守
        /// —— 否则会出现"太守在外打仗，城里没有太守"的空缺，或同一人被两座城认领。
        ///
        /// 注：CurrentCity 为 null 时视为"读档尚未解析"，放行，避免初始化阶段
        /// 因解析顺序问题导致所有城都没有太守。
        /// </summary>
        /// <param name="person">候选武将</param>
        /// <returns>是否有资格</returns>
        private bool CanBeLeader(Person person)
        {
            if (person == null || !person.IsAlive || person.IsDead || person.IsPrisoner)
                return false;
            if (person.BelongCity != this)
                return false;
            if (person.CurrentCity != null && person.CurrentCity != this)
                return false;
            return PersonStateSystem.IsInServiceState((PersonStateType)person.state);
        }

        /// <summary>
        /// 获取已建造完成的建筑类型的数量
        /// </summary>
        /// <param name="buildingKindId">建筑类型ID</param>
        /// <returns>已建造完成的建筑数量</returns>
        public int GetBuildingComplateNumber(int buildingKindId)
        {
            int complateNum = 0;
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building building = allBuildings[i];
                if (building.BuildingType.kind == buildingKindId && building.isComplate)
                {
                    complateNum++;
                }
            }
            return complateNum;
        }


        /// <summary>
        /// 获取空闲的建筑
        /// </summary>
        /// <param name="buildingKindId">建筑类型ID</param>
        /// <returns>空闲的建筑</returns>
        public Building GetFreeBuilding(int buildingKindId)
        {
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building building = allBuildings[i];
                if (building.BuildingType.kind == buildingKindId && building.isComplate && !building.ActionOver && !building.isWorking)
                {
                    return building;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取已建造的建筑类型的数量(包括未完成的)
        /// </summary>
        /// <param name="buildingKindId">建筑类型ID</param>
        /// <returns>建筑数量</returns>
        public int GetBuildingNumber(int buildingKindId)
        {
            int count;
            if (buildingCountMap.TryGetValue(buildingKindId, out count))
                return count;
            return 0;
        }

        /// <summary>
        /// 检查内城建筑是否已满
        /// </summary>
        /// <returns>内城建筑是否已满</returns>
        public bool IsInteriorBuildFull()
        {
            for (int i = 0; i < interiorCellList.Count; i++)
            {
                if (interiorCellList[i].building == null)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 本城内政设施的总等级。
        ///
        /// 【为什么需要这个】原版判断"内政是否建满"用的是**设施数量**（
        /// `city->get_facility_count() >= city->get_dev_count()`），
        /// 而原版有**吸收合并**（3 座同类低级合成 1 座高级）—— 合并会让数量**下降**，
        /// 于是"建满"之后仍能通过合并腾出空位、继续循环发展。
        /// <para>
        /// 本项目没有吸收合并，建筑只能单座升级（`BuildingType.nextId`），**数量永不下降**。
        /// 若照搬数量判据，内政地一建满就彻底饱和、再也无法优化 ——
        /// 因此改用**总等级**作为真正的饱和指标：每块内政地最高 3 级。
        /// </para>
        /// </summary>
        /// <returns>所有内政设施的等级之和</returns>
        public int InteriorBuildingLevelSum()
        {
            if (allBuildings == null)
                return 0;

            int sum = 0;
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building b = allBuildings[i];
                if (b == null || !b.IsAlive || b.BuildingType == null)
                    continue;
                if (!b.BuildingType.IsIntrior)
                    continue;
                sum += b.BuildingType.level;
            }
            return sum;
        }

        /// <summary>
        /// 沿 `BuildingType.nextId` 链推算某类建筑的**最高等级**。
        ///
        /// 各类建筑的上限并不统一：农场 / 市场能升到 Lv3，铸币厂 / 谷仓这类 Lv1 就是顶级。
        /// 因此不能用"内政地数 × 3"一刀切，必须顺着升级链走到头：
        /// `nextId &lt;= 0` 即表示已到顶。
        /// </summary>
        /// <param name="type">起始建筑类型</param>
        /// <returns>该类型可达到的最高等级；无法解析时返回自身等级</returns>
        public static int MaxLevelOf(BuildingType type)
        {
            if (type == null)
                return 0;

            Scenario scenario = Scenario.Cur;
            int level = type.level;
            int nextId = type.nextId;

            // 防御：数据里若出现环，最多走到 16 级就停
            for (int guard = 0; nextId > 0 && guard < 16; guard++)
            {
                BuildingType next = scenario != null ? scenario.GetObject<BuildingType>(nextId) : null;
                if (next == null)
                    break;
                level = next.level;
                nextId = next.nextId;
            }
            return level;
        }

        /// <summary>
        /// 本城内政设施的总等级上限 —— **按每座建筑各自的升级链推算**。
        ///
        /// 例：10 块地里有 6 座农场（能到 Lv3）+ 2 座铸币厂（Lv1 封顶）+ 2 座谷仓（Lv1 封顶），
        /// 上限 = 6×3 + 2×1 + 2×1 = 22，而不是简单粗暴的 10×3 = 30。
        /// <para>
        /// 之所以以"已建建筑的最终潜力"为上限（而不是"地数 × 最高等级"）：
        /// 那样会把 Lv1 封顶的建筑也算出 3 级余量，AI 会一直以为自己还能升级、
        /// 在无法升级的建筑上空转。用各自的 `nextId` 链推算，才是真正可达的目标。
        /// </para>
        /// </summary>
        public int InteriorBuildingLevelLimit
        {
            get
            {
                if (allBuildings == null)
                    return 0;

                int sum = 0;
                for (int i = 0; i < allBuildings.Count; i++)
                {
                    Building b = allBuildings[i];
                    if (b == null || !b.IsAlive || b.BuildingType == null)
                        continue;
                    if (!b.BuildingType.IsIntrior)
                        continue;
                    sum += MaxLevelOf(b.BuildingType);
                }
                return sum;
            }
        }

        /// <summary>
        /// 内政是否**真正**饱和（数量与总等级双双到顶）。
        ///
        /// 只要还有"数量空位"或"等级提升空间"，就仍有发展余地，
        /// AI 不该把现有设施当成冗余来拆。
        /// </summary>
        /// <returns>数量与总等级都到上限返回 true</returns>
        public bool IsInteriorFullyDeveloped()
        {
            if (!IsInteriorBuildFull())
                return false;
            return InteriorBuildingLevelSum() >= InteriorBuildingLevelLimit;
        }

        /// <summary>
        /// 获取内政地使用数量
        /// </summary>
        /// <returns>内政地使用数量</returns>
        public int GetInteriorCellUsedCount()
        {
            int complateNum = 0;
            for (int i = 0; i < interiorCellList.Count; i++)
            {
                if (interiorCellList[i].building != null)
                    complateNum++;
            }
            return complateNum;
        }

        ///// <summary>
        ///// 获取已建造完成的建筑类型的最大等级(不叠加)
        ///// </summary>
        ///// <param name="buildingTypeId"></param>
        ///// <returns></returns>
        //public int GetIntriorBuildingComplateMaxLevel(int buildingKindId)
        //{
        //    int complateNum = 0;
        //    for (int i = 0; i < allBuildings.Count; i++)
        //    {
        //        Building building = allBuildings[i];
        //        if (building.BuildingType.kind == buildingKindId && building.isComplte)
        //        {
        //            complateNum = Math.Max(complateNum, building.BuildingType.level);
        //        }
        //    }
        //    return complateNum;
        //}

        public int EnemyCount
        {
            get
            {
                return enemies.Count;
            }
        }

        /// <summary>
        /// 已预计算的敌方总兵力(供统一战场态势模型复用,避免重复扫描地图)。
        /// </summary>
        public int EnemyTroops
        {
            get
            {
                int total = 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].troop != null)
                        total += enemies[i].troop.troops;
                }
                return total;
            }
        }

        /// <summary>
        /// 最近敌人的距离(int.MaxValue 表示附近没有敌人)。
        /// </summary>
        public int NearestEnemyDistance
        {
            get
            {
                int min = int.MaxValue;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].distance < min)
                        min = enemies[i].distance;
                }
                return min;
            }
        }

        /// <summary>
        /// 计算最大士气
        /// </summary>
        public void CalculateMaxMorale()
        {
            int max = 100;
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(max);
            GameEvent.OnCityCalculateMaxMorale?.Invoke(this, overrideData);
            MaxMorale = overrideData.ValueAndRecycle;
        }

        public void CalculateLimit()
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(0);
            GameEvent.OnCityCalculateMaxGold?.Invoke(this, overrideData);
            goldLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxFood?.Invoke(this, overrideData);
            foodLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxItemStoreSize?.Invoke(this, overrideData);
            storeLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxTroops?.Invoke(this, overrideData);
            troopsLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxDurability?.Invoke(this, overrideData);
            durabilityLimitAdd = overrideData.Value;

            overrideData.Recycle();
        }
    }
}
