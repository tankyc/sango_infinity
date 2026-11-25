using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 光环作用的效果类型。
    ///
    /// 【为什么单独定义枚举】旧 <c>TroopAura</c> 只有 0 攻击 / 1 防御两种"属性"，
    /// 本系统要覆盖属性光环（攻防 / 会心 / 暴伤 / 计策 / 助攻 / 气力）共 8 项，
    /// 且每项的落点不同（写字段 / 判定时查询 / 回合结算），必须用类型区分。
    /// </summary>
    public enum AuraEffectType : int
    {
        /// <summary>攻击力（落点由 Scope 决定，通常范围覆盖，写 Troop.auraExtraAttack）</summary>
        Attack = 1,
        /// <summary>防御力（落点由 Scope 决定，通常范围覆盖，写 Troop.auraExtraDefence）</summary>
        Defence = 2,
        /// <summary>物理战法会心率（百分点，落点由 Scope 决定）</summary>
        SkillCriticalRate = 3,
        /// <summary>暴击倍率（百分点，落点由 Scope 决定）</summary>
        SkillCriticalDamage = 4,
        /// <summary>计策成功率（百分点，落点由 Scope 决定）</summary>
        SkillSuccessRate = 5,
        /// <summary>计策识破率（百分点，降低对方成功率，落点由 Scope 决定）</summary>
        SkillResistRate = 6,
        /// <summary>
        /// 辅助攻击概率（范围结算，百分点）。
        ///
        /// 挂在"支援方"身上：光环半径内的友军作为援助方去帮别人攻击时，
        /// 按该值判定是否跟着补一刀（由 Troop.CalcAssistChance 读取 helper.auraAssistChance）。
        /// </summary>
        AssistRate = 7,
        /// <summary>每回合气力回复（本队结算，点）</summary>
        MoraleRecover = 8,
    }

    /// <summary>
    /// 单条光环效果的数值公式定义。
    ///
    /// 公式：<c>clamp(Base + floor((属性 - Threshold) / Step) * PerStep, Min, Max)</c>，
    /// 封顶后再按军魂系数放大（见 <see cref="AuraGlobals.MoraleSoulFactor"/>）。
    /// </summary>
    public class AuraEffectDef
    {
        /// <summary>效果类型（见 <see cref="AuraEffectType"/>）</summary>
        [JsonProperty] public int Type;

        /// <summary>基础值（属性正好等于阈值时的数值）</summary>
        [JsonProperty] public int Base;

        /// <summary>每多少点属性提升一档（0 或负数按 1 处理，避免除零）</summary>
        [JsonProperty] public int Step;

        /// <summary>每档提升的数值</summary>
        [JsonProperty] public int PerStep;

        /// <summary>下限</summary>
        [JsonProperty] public int Min;

        /// <summary>上限</summary>
        [JsonProperty] public int Max;

        /// <summary>
        /// 作用范围：0 = 光环半径内同势力部队（所有效果都可选）；1 = 仅来源本队。
        ///
        /// 【逐项生效】每个 effect 独立判定，同一光环里可以"攻防范围生效、会心只本队"。
        /// </summary>
        [JsonProperty] public int Scope;

        /// <summary>效果类型枚举视图</summary>
        [JsonIgnore] public AuraEffectType EffectType => (AuraEffectType)Type;
    }

    /// <summary>
    /// 光环半径分档（按属性从高到低匹配，取满足条件里最高的一档）。
    /// </summary>
    public class AuraRangeTier
    {
        /// <summary>属性下限（达到该值即使用本档半径）</summary>
        [JsonProperty] public int Min;

        /// <summary>光环半径（格）</summary>
        [JsonProperty] public int Range;
    }

    /// <summary>
    /// 一条属性光环的配置（统率 / 武力 / 智力 / 政治 / 魅力各一条）。
    ///
    /// 【与旧 TroopAura 的关系】旧类只能由特技 actionEntities 装配、数值与半径都是常量；
    /// 本配置是"全局规则"：任何出战武将只要属性达标就自动开启，不需要特技挂载。
    /// </summary>
    public class AuraConfig : SangoObject
    {
        /// <summary>判定属性：0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力</summary>
        [JsonProperty] public int StatType;

        /// <summary>开启阈值（属性达到该值才开启本光环）</summary>
        [JsonProperty] public int Threshold;

        /// <summary>
        /// 本光环是否为"统率总开关"：为 true 时，它不要求别的光环先开启；
        /// 为 false 时额外要求本人统率达到 <see cref="CommandRequire"/>（附加光环仍要求统率 90）。
        /// </summary>
        [JsonProperty] public bool RequireCommandForOthers;

        /// <summary>附加光环要求的本人统率下限（默认 90）</summary>
        [JsonProperty] public int CommandRequire = 90;

        /// <summary>
        /// 可强行开启本光环的特技 Id（武干）。统率不足阈值时：
        /// 按阈值档计算数值、半径固定 1 格、且不触发 <see cref="RangeFeatureAdd"/>。
        /// </summary>
        [JsonProperty] public int ForceEnableFeatureId;

        /// <summary>半径加成特技 Id（武干）：正常开启时半径 +<see cref="RangeFeatureAdd"/></summary>
        [JsonProperty] public int RangeFeatureId;

        /// <summary>半径加成值（武干 +1 格）</summary>
        [JsonProperty] public int RangeFeatureAdd = 1;

        /// <summary>
        /// 半径分档表。
        ///
        /// 【只有统率光环的这一份会被使用】AuraSystem.Init 会把它复制成全局表，
        /// 所有光环（武/智/政/魅）的半径都按提供者统率查这张表；
        /// 各光环自己的 RangeTiers 仅在全局表缺失时作为兜底。效果数值仍按各自属性计算。
        /// </summary>
        [JsonProperty] public List<AuraRangeTier> RangeTiers = new List<AuraRangeTier>();

        /// <summary>本光环提供的效果列表（统率光环含攻防 + 会心两项）</summary>
        [JsonProperty] public List<AuraEffectDef> Effects = new List<AuraEffectDef>();
    }

    /// <summary>
    /// 光环全局参数（军魂系数等）。
    /// </summary>
    public class AuraGlobals
    {
        /// <summary>军魂特技 Id：持有者提供的光环数值在封顶后再乘 <see cref="MoraleSoulFactor"/>%</summary>
        [JsonProperty] public int MoraleSoulFeatureId;

        /// <summary>军魂系数（百分比，150 = ×1.5，向下取整）</summary>
        [JsonProperty] public int MoraleSoulFactor = 150;
    }
}
