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
        /// 学习次数上限（**实际次数**，不是档位索引）。
        /// 按"势力"计数：同一势力内最多几人学会本条能力。
        /// <c>Abilities.json</c> 里：基础能力 5 / 兵种适性 3 / 特技 1~2；0 表示不限。
        /// 口径说明见 <see cref="LearningLimitCount"/>。
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

        /// <summary>
        /// 界面显示种类（决定能力 Item 上 <c>type</c> 底图的颜色）。
        ///
        /// <para>
        /// 编码与 <see cref="AbilityStatKind"/> 一致：0 武力（红 <c>4846-34_8</c>）/ 1 统率（紫 <c>_6</c>）/
        /// 2 政治（绿 <c>_7</c>）/ 3 智力（黄 <c>_9</c>）；<c>-1</c> = 未配置（回退按 statKind 推导）。
        /// </para>
        /// <para>
        /// 与 <see cref="statKind"/> 拆开的原因：statKind 是原版"关联属性"（参与
        /// <c>force_research_ability</c> 的维排名打分，改它会牵连 AI 选择），
        /// 而 displayKind 只是配色，允许两者不同（隐藏槽位按槽位种类配色）。
        /// 1~98 的取值按原版研究界面的节点底色逐条录入。
        /// </para>
        /// </summary>
        [JsonProperty] public int displayKind = -1;

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

        // ==================== 六边形网格布局（纯 UI 字段，不参与任何数值计算） ====================
        //
        // 原版 Ability 表里没有位置数据（Open311 s11_scen.h:2527-2605 只有名称/描述/前置/耗时），
        // 研究界面的排布原本由游戏内硬编码的固定坐标决定。本项目把它显式放进数据：
        // 设计师改 Json 即可调版面，UI 代码不再做任何自动布局推算。

        /// <summary>
        /// 六边形网格轴向坐标 q（横轴，向右为正）。
        /// <para>
        /// 原点 (q, r) = (0, 0) 是研究界面中心格（原版「研究开始」所在的位置），
        /// 三轴坐标满足 q + r + s = 0，第三轴见 <see cref="hexS"/>。
        /// </para>
        /// </summary>
        [JsonProperty] public int q;

        /// <summary>
        /// 六边形网格轴向坐标 r（纵轴，向下为正）。
        /// <para>
        /// 相邻两排横向错开半个格宽，因此像素横坐标 = (q + r * 0.5) * 格宽，纵坐标只由 r 决定。
        /// </para>
        /// </summary>
        [JsonProperty] public int r;

        /// <summary>
        /// 方向显示数组：元素是方向编号 1~6，数组里有哪个方向就在节点边缘显示哪个箭头。
        /// <para>
        /// 语义是"本条能力的前置（<see cref="requiredAbilityIds"/>）位于哪些方向"。
        /// 方向编号与节点模板 <c>dir/1</c> ~ <c>dir/6</c> 的子物体名一一对应：
        /// 1 右上 / 2 右 / 3 右下 / 4 左下 / 5 左 / 6 左上。
        /// </para>
        /// <para>
        /// 取值完全由数据配置，UI 只按数组显示，不做推导 —— 节点挪到别的格子，箭头也不会跟着乱变。
        /// 写成数组而不是位掩码，是为了 Json 里一眼能看懂：<c>"dir": [1, 6]</c> = 左上与右上各一个箭头；
        /// <c>"dir": []</c> = 不显示箭头（没有前置 / 不想显示）。
        /// </para>
        /// </summary>
        [JsonProperty] public int[] dir;

        /// <summary>
        /// 学习次数上限的实际数值（按"势力"计数：同一势力内最多几人学会本条能力）。
        ///
        /// 【口径：<see cref="learningLimit"/> 直接写"次数"，不是 Open311 的档位索引】
        /// 原版 `Ability::get_learning_limit_count()`（s11_scen_obj_ability.cpp:90-104）
        /// 是"档位 → 次数"的展开表（0→255 / 1→1 / 2→3 / 3→5）。
        /// 本项目的 <c>Data/Common/Abilities.json</c> 已经**按次数写好了**：
        /// 基础能力 5、兵种适性 3、特技 1~2。
        /// 早前这里又套了一次档位映射，导致双重换算、全线错位：
        /// <list type="bullet">
        /// <item>5（基础能力）落到 default 返回 0 —— 一次都学不了，
        /// 研究完成后"培育"入口恒灰、节点全部标成"学习次数已用完"（玩家反馈的现象）；</item>
        /// <item>3（兵种适性）被当成档位 Five，放开到 5 次；</item>
        /// <item>2（特技）被当成档位 Three，放开到 3 次。</item>
        /// </list>
        /// 现在直接返回次数本身；0 / 未配置表示不限（对齐原版档位 0 → 255）。
        /// </summary>
        public int LearningLimitCount
        {
            get
            {
                if (learningLimit <= 0)
                    return 255;
                return learningLimit;
            }
        }

        /// <summary>
        /// 六边形三轴坐标的第三轴 s = -q - r（只用于算距离，不落库）。
        /// </summary>
        public int hexS => -q - r;

        /// <summary>
        /// 是否在指定方向（1~6）上配置了箭头。
        /// </summary>
        /// <param name="direction">方向编号，见 <see cref="AbilityHex"/></param>
        /// <returns>该方向置位返回 true</returns>
        public bool HasDirection(int direction)
        {
            return AbilityHex.HasDirection(dir, direction);
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

    /// <summary>
    /// 能力节点的六边形网格工具：方向编号、邻格偏移、方向数组判定。
    ///
    /// <para>
    /// 方向编号与节点模板 <c>node_template/dir/1~6</c> 的子物体名**严格一一对应**，
    /// 该对应关系由 prefab 里六个子节点的锚点位置确定（均已核对）：
    /// </para>
    /// <code>
    /// 编号  子物体位置(x, y)   方位   轴向增量(dq, dr)
    /// ──────────────────────────────────────────────
    ///  1    ( 22.5,  27.1)    右上       (+1, -1)
    ///  2    ( 42.3,   0.0)    右         (+1,  0)
    ///  3    ( 22.5, -27.2)    右下       ( 0, +1)
    ///  4    (-21.4, -27.2)    左下       (-1, +1)
    ///  5    (-41.2,   0.0)    左         (-1,  0)
    ///  6    (-22.6,  25.7)    左上       ( 0, -1)
    /// </code>
    /// <para>
    /// 数据字段 <see cref="Ability.dir"/> 直接用方向编号数组存这六个方向（如 <c>[1, 6]</c>）。
    /// </para>
    /// </summary>
    public static class AbilityHex
    {
        /// <summary>方向总数（六边形相邻格数）</summary>
        public const int MaxDirection = 6;

        /// <summary>方向 1~6 的 q 增量（下标 0 = 方向 1）</summary>
        static readonly int[] DirectionQ = { 1, 1, 0, -1, -1, 0 };

        /// <summary>方向 1~6 的 r 增量（下标 0 = 方向 1）</summary>
        static readonly int[] DirectionR = { -1, 0, 1, 1, 0, -1 };

        /// <summary>
        /// 方向数组里是否包含某个方向。
        /// </summary>
        /// <param name="directions">方向编号数组（<see cref="Ability.dir"/>，可为 null / 空）</param>
        /// <param name="direction">方向编号（1~6）</param>
        /// <returns>数组里含有该方向返回 true</returns>
        public static bool HasDirection(int[] directions, int direction)
        {
            if (directions == null || direction < 1 || direction > MaxDirection)
                return false;
            for (int i = 0; i < directions.Length; i++)
            {
                if (directions[i] == direction)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 取某方向的邻格轴向增量。
        /// </summary>
        /// <param name="direction">方向编号（1~6）</param>
        /// <param name="deltaQ">输出的 q 增量</param>
        /// <param name="deltaR">输出的 r 增量</param>
        public static void GetNeighborOffset(int direction, out int deltaQ, out int deltaR)
        {
            if (direction < 1 || direction > MaxDirection)
            {
                deltaQ = 0;
                deltaR = 0;
                return;
            }
            deltaQ = DirectionQ[direction - 1];
            deltaR = DirectionR[direction - 1];
        }

        /// <summary>
        /// 取从 (fromQ, fromR) 指向 (toQ, toR) 的方向编号。
        ///
        /// <para>
        /// 相邻格返回精确方向；跨格时（两个节点不在紧邻的六格上）按"屏幕方位角最接近"的六方向之一返回。
        /// 中心「研究开始」节点指向各个入口能力时用的就是这条路径。
        /// </para>
        /// </summary>
        /// <param name="fromQ">起点 q</param>
        /// <param name="fromR">起点 r</param>
        /// <param name="toQ">终点 q</param>
        /// <param name="toR">终点 r</param>
        /// <returns>方向编号 1~6；两点重合返回 0</returns>
        public static int GetDirection(int fromQ, int fromR, int toQ, int toR)
        {
            int deltaQ = toQ - fromQ;
            int deltaR = toR - fromR;
            if (deltaQ == 0 && deltaR == 0)
                return 0;

            // 轴向差换成屏幕方向：x 与 (q + r/2) 同向，y 与 -r 同向（r 越大越靠下）
            double dx = deltaQ + deltaR * 0.5;
            double dy = -deltaR;
            double length = System.Math.Sqrt(dx * dx + dy * dy);

            int best = 0;
            double bestCos = -2.0;
            for (int i = 0; i < MaxDirection; i++)
            {
                double vx = DirectionQ[i] + DirectionR[i] * 0.5;
                double vy = -DirectionR[i];
                double cos = length > 0.0
                    ? (dx * vx + dy * vy) / (length * System.Math.Sqrt(vx * vx + vy * vy))
                    : -1.0;
                if (cos > bestCos)
                {
                    bestCos = cos;
                    best = i + 1;
                }
            }
            return best;
        }

        /// <summary>
        /// 两个轴向坐标之间的六边形距离（走格数）。
        /// </summary>
        /// <param name="q0">起点 q</param>
        /// <param name="r0">起点 r</param>
        /// <param name="q1">终点 q</param>
        /// <param name="r1">终点 r</param>
        /// <returns>最少走几格</returns>
        public static int Distance(int q0, int r0, int q1, int r1)
        {
            int dq = q1 - q0;
            int dr = r1 - r0;
            int ds = (-q1 - r1) - (-q0 - r0);
            return (System.Math.Abs(dq) + System.Math.Abs(dr) + System.Math.Abs(ds)) / 2;
        }
    }
}
