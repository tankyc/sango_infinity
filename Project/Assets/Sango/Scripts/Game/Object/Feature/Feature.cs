/*
 * 文件名：Feature.cs
 * 描述：特技数据（Features.json）与**特技培育规则**（对齐 Open311 force_learn_skill）。
 *
 * 为什么规则也放在这里：
 *   原版 `force_learn_skill`（s11_game_ai_force_cmd_learn.cpp:190-386）是一个按
 *   `Ability::get_skill_id()` 分支的巨大 switch，每个 case 自带"算分公式 + 前置条件"，
 *   特技 Id 硬编码在 AI 代码里。本项目把这份分支表搬进 Features.json：
 *   Feature 上的 learnXxx 字段决定"这条特技谁最适合学、在什么条件下才能学"，
 *   代码侧只按枚举分派算法（见文件末尾的 FeatureLearnRule）。
 *   ⇒ 新增 / 调整特技只需改数据，不用改 AI 代码。
 *
 * 对齐出处：
 *   · 打分分支总表 —— s11_game_ai_force_cmd_learn.cpp:190-386
 *   · 阶段一比较器 —— 同文件 :151-186（skill_set / 剩余名额 / rand_bool(50)）
 *   · 取适性       —— Person::get_tekisei()（Tekisei_C=0 / B=1 / A=2 / S=3，s11_enum_scen.h:559-566）
 *   · 取属性       —— Person::get_stat()（含伤病折减）
 *   · 设施前置     —— Game::ai_table_get_gov_facility_count + FacilityId_*
 *   · 驻在地前置   —— Building::get_city_id()（<0 = 关/港/阵，>=0 = 都市）
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sango.Core.Action;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 特技培育的打分口径（每个取值对应原版 switch 里的一类 score 公式）。
    /// </summary>
    public enum FeatureLearnScoreKind : int
    {
        /// <summary>
        /// 原版 default 分支：<c>max(枪/戟/弩/骑/兵器/水军 适性) × 武力</c>（不要求适性 ≥ B）。
        /// </summary>
        Default = 0,

        /// <summary>智力（<c>person->get_stat(PersonStatType_Intelligence)</c>）</summary>
        Intelligence = 1,

        /// <summary>政治</summary>
        Politics = 2,

        /// <summary>魅力</summary>
        Charisma = 3,

        /// <summary>智力 + 政治（原版 SkillId_Ronkyaku 论客）</summary>
        IntelligencePlusPolitics = 4,

        /// <summary>统率 + 武力 + 智力 + 政治（原版 SkillId_Shidou 指导）</summary>
        FourStatSum = 5,

        /// <summary>
        /// <c>MaxPersonStat * 5 - 五维之和</c>（原版给搬运 / 眼力 / 米道系 / 屯田的反向打分：
        /// 五维越低越优先，因为这些特技是给内政型武将用的）。
        /// </summary>
        ReverseFiveStatSum = 6,

        /// <summary>
        /// <c>兵种适性 × 武力</c>（原版各兵种神 / 将 / 长驱 / 疾驰 / 射程 / 工神）。
        /// 取哪个兵种由 <see cref="Feature.learnAptitudeKind"/> 决定，
        /// 是否要求适性 ≥ B 由 <see cref="Feature.learnAptitudeAtLeastB"/> 决定。
        /// </summary>
        AptitudeStrength = 7,
    }

    /// <summary>
    /// 打分取哪一个兵种适性（对齐原版 <c>person->get_tekisei(Heishu_X)</c> 的取法）。
    /// 凡取 <c>Max...</c> 的，等价于原版 <c>std::max({...})</c>。
    /// </summary>
    public enum FeatureLearnAptitudeKind : int
    {
        /// <summary>不取适性</summary>
        None = 0,
        /// <summary>枪（Heishu_Yari）</summary>
        Spear = 1,
        /// <summary>戟（Heishu_Geki）</summary>
        Halberd = 2,
        /// <summary>弩（Heishu_Bow）</summary>
        Bow = 3,
        /// <summary>骑（Heishu_Cavalry）</summary>
        Cavalry = 4,
        /// <summary>兵器（Heishu_Siege）</summary>
        Siege = 5,
        /// <summary>水军（Heishu_Navy）</summary>
        Navy = 6,
        /// <summary>枪/戟取高（原版 SkillId_Toushin 斗神）</summary>
        MaxSpearHalberd = 7,
        /// <summary>枪/戟/骑取高（原版 SkillId_Mosa 猛者）</summary>
        MaxSpearHalberdCavalry = 8,
        /// <summary>六兵种取高（原版 default 分支）</summary>
        MaxAll = 9,
    }

    /// <summary>
    /// 培育该特技时要求的驻在地（对齐原版 <c>Building::get_city_id()</c> 判据）。
    /// </summary>
    public enum FeatureLearnPlaceKind : int
    {
        /// <summary>不限（原版没有 get_city_id 判断的分支）</summary>
        Any = 0,
        /// <summary>必须驻在**都市**（原版 <c>service->get_city_id() &gt;= 0</c>）</summary>
        City = 1,
        /// <summary>必须驻在**关/港/阵**（原版 <c>service->get_city_id() &lt; 0</c>，屯田用）</summary>
        GatePort = 2,
    }

    /// <summary>
    /// 培育该特技时要求都市里拥有的设施（对齐原版 <c>FacilityId_*</c> 判据）。
    /// 判定走本项目的 <see cref="BuildingKindType"/>。
    /// </summary>
    public enum FeatureLearnFacilityKind : int
    {
        /// <summary>无要求</summary>
        None = 0,
        /// <summary>市场系（原版 Market / MarketLv2 / MarketLv3）</summary>
        Market = 1,
        /// <summary>锻冶系（原版 Smith / SmithLv2 / SmithLv3）</summary>
        Smith = 2,
        /// <summary>厩舍系（原版 Stable / StableLv2 / StableLv3）</summary>
        Stable = 3,
        /// <summary>工房（原版 Workshop）</summary>
        Workshop = 4,
        /// <summary>造船厂（原版 Shipyard）</summary>
        Shipyard = 5,
    }

    /// <summary>
    /// 特性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Feature : SangoObjectExtensionData
    {
        private string _name;
        public override string Name
        {
            get
            {
                if (actionEntities == null)
                    return $"<color=#6D6D6D>{_name}</color>";
                else
                    return _name;
            }

            set
            {
                _name = value;
            }
        }

        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty] public int kind;

        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty] public int level;

        /// <summary>
        /// 是否唯一
        /// </summary>
        [JsonProperty] public bool only;

        /// <summary>
        /// 效果实体集合
        /// </summary>
        [JsonProperty]
        public Newtonsoft.Json.Linq.JArray actionEntities;

        // ==================== 培育（force_learn_skill）规则 ====================
        //
        // 对齐出处：s11_game_ai_force_cmd_learn.cpp:190-386 —— 原版是一个按特技 Id 分支的
        // 巨大 switch，每个 case 自带"算分公式 + 前置条件"。本项目把这份分支表搬到数据里：
        // 下列字段决定"这条特技谁最适合学、在什么条件下才能学"，代码只按枚举分派算法。
        // 未填的字段取默认值 = 原版 default 分支（前线 + 六兵种适性取高 × 武力）。

        /// <summary>
        /// 培育打分口径（<see cref="FeatureLearnScoreKind"/>）。
        /// 默认 0 = 原版 default 分支：<c>max(六兵种适性) × 武力</c>。
        /// </summary>
        [JsonProperty] public int learnScoreKind;

        /// <summary>
        /// 打分取哪个兵种适性（<see cref="FeatureLearnAptitudeKind"/>）。
        /// 仅在 <see cref="learnScoreKind"/> = AptitudeStrength / Default 时有意义。
        /// </summary>
        [JsonProperty] public int learnAptitudeKind;

        /// <summary>
        /// 是否要求该兵种适性 ≥ B（<see cref="FeatureLearnRule.AptitudeB"/>）。
        /// 原版只有"射程 / 工神"两组不检查 B，其余兵种神都要求 ≥ B；default 分支也不检查。
        /// </summary>
        [JsonProperty] public bool learnAptitudeAtLeastB;

        /// <summary>
        /// 是否要求培育者所在据点为**前线**（原版 <c>kyoten_[index].state == KyotenState_Front</c>）。
        /// **默认 true** —— 原版 default 分支与绝大多数 case 都要求前线；
        /// 只有论客 / 指导 / 搬运 / 眼力 / 米道系 / 名声 / 能吏 / 繁殖 / 发明 / 造船 / 屯田
        /// 这些内政类特技显式填 false。
        /// </summary>
        [JsonProperty] public bool learnRequireFront = true;

        /// <summary>
        /// 培育者驻在地要求（<see cref="FeatureLearnPlaceKind"/>）：不限 / 必须都市 / 必须关港。
        /// </summary>
        [JsonProperty] public int learnPlaceKind;

        /// <summary>
        /// 培育者所在都市必须拥有的设施（<see cref="FeatureLearnFacilityKind"/>）。
        /// </summary>
        [JsonProperty] public int learnFacilityKind;

        /// <summary>
        /// 培育者所在据点的兵力下限（原版 SkillId_Tonden 屯田要求 ≥ 10000）。0 = 不限。
        /// </summary>
        [JsonProperty] public int learnMinTroops;

        public void InitActions(List<ActionBase> list, params SangoObject[] sangoObjects)
        {
            if (actionEntities == null) return;
            for (int i = 0; i < actionEntities.Count; i++)
            {
                JObject valus = actionEntities[i] as JObject;
                ActionBase action = ActionBase.Create(valus.Value<string>("class"));
                if (action != null)
                {
                    action.Init(valus, sangoObjects);
                    list.Add(action);
                }
            }
        }

    }

    /// <summary>
    /// 特技培育的规则判定与打分（对齐 Open311 force_learn_skill）。
    ///
    /// 两个阶段：
    /// ① <see cref="IsPreferredAbility"/>：先从"已研究且仍可学"的特技里选**一条**；
    /// ② <see cref="TryCalcScore"/>：再对这条特技给全势力武将打分取最高。
    /// 所有公式与前置条件都从 <see cref="Feature"/> 的 learnXxx 字段读取，本类不含特技 Id 判断。
    /// <para>
    /// 【只服务势力 AI 挑人】这些前置（驻在地 / 据点兵力 / 前线 / 都市设施 / 适性 B 档）
    /// 的出处全是 s11_game_ai_force_cmd_learn.cpp（:189-386 的 switch 分支 + :392-427 的
    /// get_learnable_person_list）。原版玩家侧链路
    /// can_learn_skill_command（s11_sys_person_order_kunshu.cpp:1913-1974）
    /// → PersonOrderManager::get_learnable_person_list（同文件 :1583-1602）
    /// → System::can_learn（s11_sys_scen_ability.cpp:60-64）
    /// 对特技只判"未拥有该特技"，**一条前置都没有**。
    /// 所以玩家侧不要调用本类，走 <see cref="AbilityStudySystem.GetLearnableAbilitiesFor"/> 的玩家分支。
    /// </para>
    /// </summary>
    public static class FeatureLearnRule
    {
        /// <summary>
        /// 武将能力上限 —— 对齐 Open311 `Game::get_max_person_stat()`。
        /// 用于"反向打分"公式 <c>MaxPersonStat * 5 - 五维之和</c>。
        /// </summary>
        public const int MaxPersonStat = 100;

        /// <summary>
        /// 兵种适性 B 档 —— 对齐 Open311 `Tekisei_B = 1`（s11_enum_scen.h:559-566：
        /// C=0 / B=1 / A=2 / S=3）。本项目 <see cref="PersonAbilityValue.value"/> 与之同序。
        /// </summary>
        public const int AptitudeB = 1;

        // ==================== 阶段一：选哪一条特技 ====================

        /// <summary>
        /// 候选特技是否比当前最优更优（对齐原版 force_learn_skill 的 comp 比较器，:162-172）。
        ///
        /// 三级比较键（**顺序不可颠倒**）：
        /// ① 势力内还没人拥有该特技的优先；② 剩余学习名额多者优先；③ 仍相同则 50% 掷骰。
        /// </summary>
        /// <param name="candidate">候选特技能力</param>
        /// <param name="current">当前最优（可为 null）</param>
        /// <param name="force">势力</param>
        /// <param name="ownedSkillIds">本势力已有人拥有的特技 Feature Id 集合</param>
        /// <returns>候选更优返回 true</returns>
        public static bool IsPreferredAbility(Ability candidate, Ability current, Force force, HashSet<int> ownedSkillIds)
        {
            if (candidate == null)
                return false;
            if (current == null)
                return true;
            if (force == null)
                return false;

            bool candidateOwned = ownedSkillIds != null && ownedSkillIds.Contains(candidate.skillId);
            bool currentOwned = ownedSkillIds != null && ownedSkillIds.Contains(current.skillId);
            if (candidateOwned != currentOwned)
                return !candidateOwned;                 // 还没人有 → 优先

            int candidateRemain = candidate.LearningLimitCount - force.GetAbilityLearnCount(candidate.Id);
            int currentRemain = current.LearningLimitCount - force.GetAbilityLearnCount(current.Id);
            if (candidateRemain != currentRemain)
                return candidateRemain > currentRemain;  // 剩余名额多 → 优先

            return GameRandom.Chance(50);
        }

        /// <summary>
        /// 收集"本势力已有武将拥有的特技 Id"（对齐原版 `skill_set`，:151-160）。
        ///
        /// 原版每名武将只有一个 `skill_id_`；本项目 `FeatureList` 可以有多个，
        /// 因此这里把整张列表都收进来（语义上仍是"势力内出现过该特技"）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>特技 Id 集合</returns>
        public static HashSet<int> CollectOwnedSkillIds(Force force, Scenario scenario)
        {
            HashSet<int> set = new HashSet<int>();
            if (force == null || scenario == null || scenario.personSet == null)
                return set;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force)
                    continue;
                if (person.FeatureList == null)
                    continue;
                for (int f = 0; f < person.FeatureList.Count; f++)
                {
                    Feature feature = person.FeatureList[f];
                    if (feature != null)
                        set.Add(feature.Id);
                }
            }
            return set;
        }

        // ==================== 阶段二：给指定特技选人 ====================

        /// <summary>
        /// 计算某武将培育某特技的分数，并判定全部前置条件。
        ///
        /// 对齐出处：s11_game_ai_force_cmd_learn.cpp:189-386 的 switch 分支 ——
        /// 原版每个 case 都是"先 continue 掉不合格的，再给 score 赋值"，
        /// 这里统一收敛成"前置不满足返回 false，否则输出 score"。
        /// </summary>
        /// <param name="person">候选武将</param>
        /// <param name="feature">目标特技</param>
        /// <param name="city">武将所在据点</param>
        /// <param name="score">输出：得分（越大越优先）</param>
        /// <returns>满足全部前置返回 true</returns>
        public static bool TryCalcScore(Person person, Feature feature, City city, out int score)
        {
            score = 0;
            if (person == null || feature == null || city == null)
                return false;

            bool atCity = city.IsCity();

            // ① 驻在地 —— 原版 `service->get_city_id()` 判据（>=0 都市 / <0 关港阵）
            switch ((FeatureLearnPlaceKind)feature.learnPlaceKind)
            {
                case FeatureLearnPlaceKind.City:
                    if (!atCity) return false;
                    break;
                case FeatureLearnPlaceKind.GatePort:
                    if (atCity) return false;
                    break;
            }

            // ② 据点兵力下限 —— 原版屯田 `building->get_troops() >= 10000`
            if (feature.learnMinTroops > 0 && city.troops < feature.learnMinTroops)
                return false;

            // ③ 前线 —— 原版 AI 挑人段 `kyoten_[index].state == KyotenState_Front`
            //     （s11_game_ai_force_cmd_learn.cpp:276-359）。
            //     本类已只服务势力 AI；玩家侧走 AbilityStudySystem 的玩家分支，不经过这里。
            if (feature.learnRequireFront && !IsFrontCity(city))
                return false;

            // ④ 都市设施 —— 原版 `ai_table_get_gov_facility_count` + FacilityId_* 判定
            if (feature.learnFacilityKind != (int)FeatureLearnFacilityKind.None
                && !HasFacility(city, feature.learnFacilityKind))
                return false;

            switch ((FeatureLearnScoreKind)feature.learnScoreKind)
            {
                case FeatureLearnScoreKind.Intelligence:
                    score = person.Intelligence;
                    return true;

                case FeatureLearnScoreKind.Politics:
                    score = person.Politics;
                    return true;

                case FeatureLearnScoreKind.Charisma:
                    score = person.Glamour;
                    return true;

                case FeatureLearnScoreKind.IntelligencePlusPolitics:   // 论客
                    score = person.Intelligence + person.Politics;
                    return true;

                case FeatureLearnScoreKind.FourStatSum:                // 指导
                    score = person.Command + person.Strength + person.Intelligence + person.Politics;
                    return true;

                case FeatureLearnScoreKind.ReverseFiveStatSum:         // 搬运 / 眼力 / 米道系 / 屯田
                    score = MaxPersonStat * 5
                        - (person.Command + person.Strength + person.Intelligence
                           + person.Politics + person.Glamour);
                    return true;

                case FeatureLearnScoreKind.AptitudeStrength:           // 各兵种神 / 长驱 / 疾驰 / 射程 / 工神
                case FeatureLearnScoreKind.Default:                    // 原版 default 分支
                default:
                    {
                        int kind = feature.learnAptitudeKind;
                        if (kind == (int)FeatureLearnAptitudeKind.None)
                            kind = (int)FeatureLearnAptitudeKind.MaxAll;
                        int aptitude = GetAptitude(person, kind);
                        // 原版只有"射程 / 工神"与 default 分支不检查 B 档
                        if (feature.learnAptitudeAtLeastB && aptitude < AptitudeB)
                            return false;
                        score = aptitude * person.Strength;
                        return true;
                    }
            }
        }

        /// <summary>
        /// 据点是否处于**前线**（对齐原版 `kyoten_[index].state == KyotenState_Front`）。
        /// 数据来自势力 AI 准备阶段采集的据点态势表。
        /// </summary>
        /// <param name="city">据点</param>
        /// <returns>前线返回 true</returns>
        public static bool IsFrontCity(City city)
        {
            if (city == null)
                return false;
            OutpostInfo info = OutpostRuntime.Find(city.Id);
            return info != null && info.state == OutpostState.Front;
        }

        /// <summary>
        /// 取武将某个口径的兵种适性（对齐原版 <c>person->get_tekisei(Heishu_X)</c>）。
        /// 用的是**当前适性**（含经验提升），对应本项目 <see cref="PersonAbilityValue.value"/>。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="kind">适性口径（<see cref="FeatureLearnAptitudeKind"/>）</param>
        /// <returns>适性等级下标（C=0 / B=1 / A=2 / S=3）</returns>
        public static int GetAptitude(Person person, int kind)
        {
            if (person == null)
                return 0;

            switch ((FeatureLearnAptitudeKind)kind)
            {
                case FeatureLearnAptitudeKind.Spear: return person.SpearLv;
                case FeatureLearnAptitudeKind.Halberd: return person.HalberdLv;
                case FeatureLearnAptitudeKind.Bow: return person.CrossbowLv;
                case FeatureLearnAptitudeKind.Cavalry: return person.RideLv;
                case FeatureLearnAptitudeKind.Siege: return person.MachineLv;
                case FeatureLearnAptitudeKind.Navy: return person.WaterLv;

                case FeatureLearnAptitudeKind.MaxSpearHalberd:
                    return System.Math.Max(person.SpearLv, person.HalberdLv);

                case FeatureLearnAptitudeKind.MaxSpearHalberdCavalry:
                    return System.Math.Max(person.SpearLv,
                        System.Math.Max(person.HalberdLv, person.RideLv));

                case FeatureLearnAptitudeKind.MaxAll:
                    {
                        int value = person.SpearLv;
                        value = System.Math.Max(value, person.HalberdLv);
                        value = System.Math.Max(value, person.CrossbowLv);
                        value = System.Math.Max(value, person.RideLv);
                        value = System.Math.Max(value, person.MachineLv);
                        value = System.Math.Max(value, person.WaterLv);
                        return value;
                    }

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 都市里是否有指定种类的设施（对齐原版 `ai_table_get_gov_facility_count`）。
        /// </summary>
        /// <param name="city">都市</param>
        /// <param name="kind">设施种类（<see cref="FeatureLearnFacilityKind"/>）</param>
        /// <returns>存在返回 true</returns>
        public static bool HasFacility(City city, int kind)
        {
            if (city == null || city.allBuildings == null)
                return false;

            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building building = city.allBuildings[i];
                // 未完工的设施不算 —— 原版 ai_table_get_gov_facility_count 统计的是已建成设施
                if (building == null || !building.IsAlive || building.BuildingType == null)
                    continue;
                if (!building.isComplate)
                    continue;
                if (MatchesFacility(building.BuildingType.kind, kind))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 单条建筑是否属于某个设施大类。
        /// 原版是三级设施 Id（Market / MarketLv2 / MarketLv3 …），本项目按 BuildingKindType 归并。
        /// </summary>
        /// <param name="buildingKind">建筑的 BuildingType.kind</param>
        /// <param name="kind">设施种类</param>
        /// <returns>属于返回 true</returns>
        static bool MatchesFacility(int buildingKind, int kind)
        {
            switch ((FeatureLearnFacilityKind)kind)
            {
                case FeatureLearnFacilityKind.Market:
                    return buildingKind == (int)BuildingKindType.Market
                        || buildingKind == (int)BuildingKindType.BigMarket
                        || buildingKind == (int)BuildingKindType.FishMarket;

                case FeatureLearnFacilityKind.Smith:
                    return buildingKind == (int)BuildingKindType.BlacksmithShop;

                case FeatureLearnFacilityKind.Stable:
                    return buildingKind == (int)BuildingKindType.Stable;

                case FeatureLearnFacilityKind.Workshop:
                    return buildingKind == (int)BuildingKindType.MechineFactory;

                case FeatureLearnFacilityKind.Shipyard:
                    return buildingKind == (int)BuildingKindType.BoatFactory;

                default:
                    return false;
            }
        }
    }
}
