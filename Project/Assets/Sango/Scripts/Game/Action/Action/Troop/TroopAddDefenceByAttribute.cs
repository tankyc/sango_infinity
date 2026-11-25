using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按武将属性折算的防御力加成（例：铁壁 —— 防御力 += 统率 × 10%，向下取整）。
    ///
    /// <para>
    /// 【与 TroopAddAttackByAttribute 的关系】两者同构，只是作用字段不同：
    /// 攻击走 <c>landAttack</c> / <c>waterAttack</c>，防御走 <c>landDefence</c> / <c>waterDefence</c>。
    /// </para>
    /// <para>
    /// 【为什么累加是安全的】<c>Troop.CalculateAttribute</c> 会先按公式整体重算
    /// <c>landDefence</c> / <c>waterDefence</c>，再触发 <see cref="GameEvent.OnTroopCalculateAttribute"/>，
    /// 事件里只追加一次，不会跨次叠加。
    /// </para>
    /// </summary>
    public class TroopAddDefenceByAttribute : TroopActionBase
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
        /// JSON 参数：attType（缺省 command）、percent（百分比，缺省时回落到通用 value）、
        /// checkLand（0/1/2，缺省 0）、kinds（兵种 kind 过滤，可空）。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            // 【默认 command】防御力公式（70% 统率 + 30% 智力）以统率为主，
            // 因此未显式配置时默认按统率折算。
            attType = p.Value<string>("attType") ?? "command";

            // 【兼容 value】优先 percent，没填时用基类的 value 兜底
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
        /// 部队属性重算时按属性折算防御力。
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
                    troop.landDefence += add;
            }
            else if (checkLand == 2)
            {
                if (kinds == null || (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind)))
                    troop.waterDefence += add;
            }
            else
            {
                if (kinds == null)
                {
                    troop.landDefence += add;
                    troop.waterDefence += add;
                }
                else
                {
                    if (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind))
                        troop.landDefence += add;
                    if (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind))
                        troop.waterDefence += add;
                }
            }
        }
    }
}
