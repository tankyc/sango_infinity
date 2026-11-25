using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按武将属性折算的攻击力加成（例：霸王 —— 攻击力 += 武力 × 10%，向下取整）。
    ///
    /// <para>
    /// 【为什么不能直接用 TroopAddAttack】那个类只接受固定值 <c>value</c>，
    /// 而按属性折算的效果量随主将五维变化，必须每次属性重算时现算。
    /// </para>
    /// <para>
    /// 【为什么累加是安全的】本类挂在 <see cref="GameEvent.OnTroopCalculateAttribute"/> 上，
    /// 该事件在 <c>Troop.CalculateAttribute</c> 内部触发，而 <c>landAttack</c> /
    /// <c>waterAttack</c> 在此之前已被整体重新赋值（见 Troop.cs 中的攻击力公式），
    /// 事件里只做 <c>+=</c> 不会跨次叠加 —— 与 <see cref="TroopAddAttack"/> 完全同构。
    /// </para>
    /// </summary>
    public class TroopAddAttackByAttribute : TroopActionBase
    {
        /// <summary>取哪一项属性（command / strength / intelligence / politics / glamour）</summary>
        string attType;

        /// <summary>百分比（10 表示属性值的 10%）</summary>
        int percent;

        /// <summary>作用面：0 陆水都加 / 1 只加陆地 / 2 只加水上</summary>
        int checkLand;

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：attType（缺省 strength）、percent（百分比，缺省时回落到通用 value）、
        /// checkLand（0/1/2，缺省 0）、kinds（兵种 kind 过滤，可空）。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            // 【默认 strength】攻击力公式（70% 武力 + 30% 统率）以武力为主，
            // 因此未显式配置时默认按武力折算，符合"猛将攻击更高"的直觉。
            attType = p.Value<string>("attType") ?? "strength";

            // 【兼容 value】基类已把 "value" 读进字段。
            // 优先用 percent；没填 percent 时用 value 兜底，避免键名写错后加成静默失效。
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
                case "command":
                    return troop.Command;
                case "intelligence":
                    return troop.Intelligence;
                case "politics":
                    return troop.Politics;
                case "glamour":
                    return troop.Glamour;
                default:
                    return troop.Strength;
            }
        }

        /// <summary>
        /// 部队属性重算时按属性折算攻击力。
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

            // 整数除法即向下取整
            int add = GetAttribute(troop) * percent / 100;
            if (add == 0)
                return;

            if (checkLand == 1)
            {
                if (kinds == null || (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind)))
                    troop.landAttack += add;
            }
            else if (checkLand == 2)
            {
                if (kinds == null || (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind)))
                    troop.waterAttack += add;
            }
            else
            {
                if (kinds == null)
                {
                    troop.landAttack += add;
                    troop.waterAttack += add;
                }
                else
                {
                    if (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind))
                        troop.landAttack += add;
                    if (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind))
                        troop.waterAttack += add;
                }
            }
        }
    }
}
