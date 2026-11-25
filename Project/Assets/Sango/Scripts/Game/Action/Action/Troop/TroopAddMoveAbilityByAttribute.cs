using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按武将属性折算的移动力加成（兵贵神速：移动力 += 统率 × 5%，向下取整）。
    ///
    /// <para>
    /// 【为什么不能直接用 TroopAddMoveAbility】那个类只接受固定值 <c>value</c>，
    /// 而这类设计的效果量随武将属性变化，必须每次属性重算时现算。
    /// </para>
    /// <para>
    /// 【为什么累加是安全的】本类挂在 <see cref="GameEvent.OnTroopCalculateAttribute"/> 上，
    /// 该事件在 <c>Troop.CalculateAttribute</c> 内部触发，而调用方每次都会先把
    /// <c>landMoveAbility</c> / <c>waterMoveAbility</c> 重置回基础值再加 —— 与
    /// <see cref="TroopAddMoveAbility"/> 的 <c>+=</c> 完全同构，不会跨次累加。
    /// </para>
    /// </summary>
    public class TroopAddMoveAbilityByAttribute : TroopActionBase
    {
        /// <summary>取哪一项属性（command / strength / intelligence / politics / glamour）</summary>
        string attType;

        /// <summary>百分比（5 表示属性值的 5%）</summary>
        int percent;

        /// <summary>作用面：0 陆水都加 / 1 只加陆地 / 2 只加水上</summary>
        int checkLand;

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：attType（缺省 command）、percent（百分比，缺省时回落到通用 value）、
        /// checkLand（0/1/2，缺省 0）、kinds（兵种 kind 过滤，可空）。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            attType = p.Value<string>("attType") ?? "command";

            // 【兼容 value】TroopActionBase 已经把 "value" 读进基类字段。
            // 这里优先用 percent；没填 percent 时用 value 兜底，避免配置者写错键名后加成静默失效。
            percent = p.Value<int?>("percent") ?? value;

            checkLand = p.Value<int>("checkLand");

            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        /// <summary>退订事件（Action 被清理时必须调用，否则换剧本后旧实例仍会改新部队的属性）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        /// <summary>
        /// 取折算用的属性值。
        /// </summary>
        /// <param name="troop">部队（属性已由主将汇总，含伤病折减）</param>
        /// <returns>属性值</returns>
        int GetAttribute(Troop troop)
        {
            switch (attType)
            {
                case "strength":
                    return troop.Strength;
                case "intelligence":
                    return troop.Intelligence;
                case "politics":
                    return troop.Politics;
                case "glamour":
                    return troop.Glamour;
                default:
                    return troop.Command;
            }
        }

        /// <summary>
        /// 部队属性重算时按属性折算移动力。
        /// </summary>
        /// <param name="troop">正在重算的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (troop == null)
                return;

            // 装配范围过滤：势力级（技巧）或部队级（特技）
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            // 整数除法即向下取整，与设计稿"增加统率的百分之五（向下取整）"一致
            int add = GetAttribute(troop) * percent / 100;
            if (add == 0)
                return;

            if (checkLand == 1)
            {
                if (kinds == null || (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind)))
                    troop.landMoveAbility += add;
            }
            else if (checkLand == 2)
            {
                if (kinds == null || (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind)))
                    troop.waterMoveAbility += add;
            }
            else
            {
                if (kinds == null)
                {
                    troop.landMoveAbility += add;
                    troop.waterMoveAbility += add;
                }
                else
                {
                    if (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind))
                        troop.landMoveAbility += add;
                    if (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind))
                        troop.waterMoveAbility += add;
                }
            }
        }
    }
}
