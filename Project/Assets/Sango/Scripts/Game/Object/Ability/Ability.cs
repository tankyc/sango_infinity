/*
 * 文件名：Ability.cs
 * 描述：能力研究 / 能力培育的**数据表**，逐字段对齐 Open311 的 Ability 类。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 类定义与字段   —— s11_scen.h:2527-2600（Ability : ScenObject，KeyEnum 即字段表）
 *   · 学习次数换算   —— s11_scen_obj_ability.cpp:90-104（0→255 / 1→1 / 2→3 / 3→5）
 *   · 基础能力推导表 —— 同文件 :8-24 的 StatTable（15 条 = 5 属性 × 3 档，上限 70/80/95）
 *   · 兵种适性推导表 —— 同文件 :27-40（12 条 = 6 兵种 × B/A）
 *   · 消费者         —— s11_game_ai_force_cmd.cpp:754-817 force_research_ability
 *                       s11_game_ai_force_cmd_learn.cpp:37-200 force_learn 三段
 *
 * 【与原版的两点差异（都是"更明确"而非"更少"）】
 *   1. 原版的"取目标属性 / 取档位上限 / 取兵种 / 取目标适性"四个派生方法，
 *      是拿 `Id - 起始Id` 去查两张**硬编码数组**；
 *      本项目把查表结果直接内联成字段（`personStatType` / `maxStat` / `troopKind` / `targetAptitude`），
 *      数据可以在 Json 里直接改，不依赖 Id 的连续性与起始值。
 *   2. 原版枚举名 `AbilityType` 在本项目已被"兵种适性"占用（PersonAblilityType.cs），
 *      故能力大类改用 `AbilityCategory`，避免命名冲突。
 *
 * 【未搬】`hiddenSlot`（隐藏槽位）只保留字段，本项目暂无对应的隐藏能力展示逻辑。
 */

using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 能力大类。对齐 Open311 `AbilityTypeEnum`（s11_enum_scen.h:887-893）。
    /// <para>注意：本项目已有 <see cref="AbilityType"/>（兵种适性），故此处不复用该名。</para>
    /// </summary>
    public enum AbilityCategory : int
    {
        /// <summary>基础能力：把某属性从低档提到高档（原版 BaseStat 类）</summary>
        BaseStat = 0,

        /// <summary>兵种适性：把某兵种的适性提一级（原版兵种适性类）</summary>
        TroopAptitude = 1,

        /// <summary>特技：学会某个特技（原版 Skill 类）</summary>
        Skill = 2,

        /// <summary>类型总数</summary>
        Max = 3,
    }

    /// <summary>
    /// 能力对应的属性。对齐 Open311 `AbilityStatTypeEnum`（s11_enum_scen.h:907-914）。
    /// </summary>
    public enum AbilityStatKind : int
    {
        /// <summary>武力</summary>
        Strength = 0,
        /// <summary>统率</summary>
        Leadership = 1,
        /// <summary>政治</summary>
        Politics = 2,
        /// <summary>智力</summary>
        Intelligence = 3,
        /// <summary>魅力（原版有此维度，StatTable 里占了 3 条）</summary>
        Charisma = 4,
        /// <summary>总数</summary>
        Max = 5,
    }

    /// <summary>
    /// 学习次数上限档位。对齐 Open311 `AbilityLearningLimitEnum`（s11_enum_scen.h:897-904）。
    /// </summary>
    public enum AbilityLearningLimit : int
    {
        /// <summary>不限次数（原版返回 255）</summary>
        Unlimited = 0,
        /// <summary>1 次</summary>
        Once = 1,
        /// <summary>3 次</summary>
        Three = 2,
        /// <summary>5 次</summary>
        Five = 3,
        /// <summary>总数</summary>
        Max = 4,
    }

    /// <summary>
    /// 一条能力（研究项目 / 培育项目）。
    ///
    /// 数据来源：<c>Data/Common/Abilities.json</c>，由 <c>ScenarioCommonData.Abilities</c> 承载。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Ability : SangoObjectExtensionData
    {
        /// <summary>名称（由数据表的 Name 字段填充，SangoObjectSetConverter 走 Name 属性）</summary>
        private string _name;

        /// <inheritdoc/>
        public override string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        /// <summary>描述</summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 学习次数上限档位（<see cref="AbilityLearningLimit"/>）。
        /// 按"势力"计数：同一势力内最多几人学会本条能力。
        /// </summary>
        [JsonProperty] public int learningLimit;

        /// <summary>能力大类（<see cref="AbilityCategory"/>）</summary>
        [JsonProperty] public int category;

        /// <summary>
        /// 前置能力 Id 列表（最多 <see cref="MaxRequiredAbilityCount"/> 个）。
        /// 原版 `MaxRequiredAbilityCount = 3`（s11_scen.h:2530）。
        /// </summary>
        [JsonProperty] public int[] requiredAbilityIds = new int[MaxRequiredAbilityCount];

        /// <summary>前置能力最多几个 —— 对齐 Open311 Ability::MaxRequiredAbilityCount</summary>
        public const int MaxRequiredAbilityCount = 3;

        /// <summary>
        /// 对应特技 Id（<see cref="AbilityCategory.Skill"/> 时有效，其余为 0）。
        /// 对齐 Open311 `Ability::get_skill_id()`。
        /// </summary>
        [JsonProperty] public int skillId;

        /// <summary>
        /// 对应属性（<see cref="AbilityStatKind"/>）。
        /// 对齐 Open311 `Ability::get_stat_type()`；用于 force_research_ability 的维排名打分。
        /// </summary>
        [JsonProperty] public int statKind;

        /// <summary>效果值 —— 对齐 Open311 `Ability::get_value()`</summary>
        [JsonProperty] public int value;

        /// <summary>学习耗时（月）—— 对齐 Open311 `Ability::get_time_cost()`</summary>
        [JsonProperty] public int timeCost;

        /// <summary>隐藏槽位 —— 对齐 Open311 `Ability::get_hidden_slot()`（本项目暂无消费逻辑）</summary>
        [JsonProperty] public int hiddenSlot;

        // ==================== 基础能力专用（原版 StatTable 内联） ====================

        /// <summary>
        /// 目标属性。取值与 <see cref="Person"/> 的四个属性字段对应：
        /// 0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力。
        /// 对齐 Open311 `Ability::get_person_stat_type()`（非 BaseStat 类返回 -1）。
        /// </summary>
        [JsonProperty] public int personStatType = -1;

        /// <summary>
        /// 本条能力能把属性提到多少（70 / 80 / 95）。
        /// 对齐 Open311 `Ability::get_max_stat()`（非 BaseStat 类返回 Person::MinStat）。
        /// </summary>
        [JsonProperty] public int maxStat;

        // ==================== 兵种适性专用（原版推导表内联） ====================

        /// <summary>
        /// 目标兵种（<see cref="AbilityType"/>：1 枪 / 2 戟 / 3 弩 / 4 骑兵 / 5 器械 / 6 水军）。
        /// 对齐原版"取兵种"派生（非兵种适性类返回 -1）。
        /// </summary>
        [JsonProperty] public int troopKind = -1;

        /// <summary>
        /// 目标适性等级（<c>PersonAbilityValue.baseValue</c> 的下标，越大越好）。
        /// 对齐原版"取目标适性"派生（B / A 两档）。
        /// </summary>
        [JsonProperty] public int targetAptitude = -1;

        /// <summary>
        /// 学习次数上限的实际数值。
        /// 对齐 Open311 `Ability::get_learning_limit_count()`（s11_scen_obj_ability.cpp:90-104）：
        /// 0 → 255（实质无限）/ 1 → 1 / 2 → 3 / 3 → 5。
        /// </summary>
        public int LearningLimitCount
        {
            get
            {
                switch (learningLimit)
                {
                    case (int)AbilityLearningLimit.Unlimited: return 255;
                    case (int)AbilityLearningLimit.Once: return 1;
                    case (int)AbilityLearningLimit.Three: return 3;
                    case (int)AbilityLearningLimit.Five: return 5;
                    default: return 0;
                }
            }
        }

        /// <summary>是否为"基础能力"类</summary>
        public bool IsBaseStat => category == (int)AbilityCategory.BaseStat;

        /// <summary>是否为"兵种适性"类</summary>
        public bool IsTroopAptitude => category == (int)AbilityCategory.TroopAptitude;

        /// <summary>是否为"特技"类</summary>
        public bool IsSkill => category == (int)AbilityCategory.Skill;

        /// <summary>
        /// 前置能力是否全部满足。
        /// 对齐 Open311 force_research_ability 里 `get_researchable_ability_list` 的前置过滤。
        /// </summary>
        /// <param name="ownedAbilityIds">势力已掌握的能力 Id 集合</param>
        /// <returns>全部满足（或没有前置）返回 true</returns>
        public bool IsPrerequisiteMet(System.Collections.Generic.HashSet<int> ownedAbilityIds)
        {
            if (requiredAbilityIds == null)
                return true;
            for (int i = 0; i < requiredAbilityIds.Length; i++)
            {
                int required = requiredAbilityIds[i];
                if (required <= 0)
                    continue;
                if (ownedAbilityIds == null || !ownedAbilityIds.Contains(required))
                    return false;
            }
            return true;
        }
    }
}
