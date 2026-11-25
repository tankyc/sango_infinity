using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 条件满足时修改部队属性的通用 Action（加算或乘算）。
    ///
    /// <para>
    /// 【为什么需要这个类】大量特技的效果是"满足某前提时攻/防/移动力提高或翻倍"，
    /// 例如威震华夏（兵力 &gt; 5000 时防御 +10%）、咆哮（兵力 &lt; 3000 时攻击翻倍）、
    /// 执锐（攻击 +20%）、披坚（防御 +20%）、龙胆（兵力越少攻防越高）、七进七出。
    /// 已有的 <see cref="TroopAddAttack"/> / <see cref="TroopAddDefence"/> 只能给固定值，
    /// <see cref="TroopAddAttackByAttribute"/> 只能按武将属性折算，都表达不了"按兵力百分比"。
    /// </para>
    /// <para>
    /// 【累加安全】挂 <see cref="GameEvent.OnTroopCalculateAttribute"/>，
    /// 攻击 / 防御 / 移动力在 <c>Troop.CalculateAttribute</c> 里都会先整体重算再触发事件，
    /// 因此每次事件里做一次乘算或加算都不会跨次叠加。
    /// </para>
    /// <para>
    /// 【条件取谁】<c>condition</c> 的 <see cref="IConditionDatabase.ActionTroop"/>
    /// 就是被重算的这支部队自己（<see cref="TroopConditionDatabase"/>），
    /// 所以"兵力低于 3000""气力高于 50"这类判断直接配 condition 即可。
    /// </para>
    /// </summary>
    public class TroopModifyAttributeByCondition : TroopTroopActionBase
    {
        /// <summary>
        /// 目标属性：
        /// attack（攻击力）/ defence（防御力）/ moveAbility（移动力）/ maxTroops（兵力上限）。
        /// </summary>
        string attr;

        /// <summary>计算方式：0 = 加算固定值（默认），1 = 按百分比乘算</summary>
        int mode;

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：attr、mode、value、checkLand、kinds、condition。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            attr = p.Value<string>("attr") ?? "attack";
            mode = p.Value<int>("mode");
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        /// <summary>
        /// 按配置的加算 / 乘算方式修改一项属性。
        /// </summary>
        /// <param name="cur">当前值</param>
        /// <returns>修改后的值</returns>
        int Modify(int cur)
        {
            // 乘算：value 是百分点（20 表示 +20%）；加算：value 是绝对值
            return mode == 1 ? cur * (100 + value) / 100 : cur + value;
        }

        /// <summary>
        /// 部队属性重算时套用条件化加成。
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

            // 地形面过滤：1 只算陆地 / 2 只算水上
            if (checkLand == 1 && troop.IsInWater) return;
            if (checkLand == 2 && !troop.IsInWater) return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            // 兵种过滤：kinds 为 null 表示两种地形都吃满
            bool landOk = kinds == null
                || (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind));
            bool waterOk = kinds == null
                || (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind));

            switch (attr)
            {
                case "defence":
                    // 陆地部队改 landDefence、水上部队改 waterDefence；
                    // 不能两个都改，否则一次重算会把两套数值同时放大，切地形时结果翻倍。
                    if (troop.IsInWater)
                    {
                        if (waterOk) troop.waterDefence = Modify(troop.waterDefence);
                    }
                    else
                    {
                        if (landOk) troop.landDefence = Modify(troop.landDefence);
                    }
                    break;
                case "moveAbility":
                    if (landOk) troop.landMoveAbility = Modify(troop.landMoveAbility);
                    if (waterOk) troop.waterMoveAbility = Modify(troop.waterMoveAbility);
                    break;
                case "maxTroops":
                    troop.MaxTroops = Modify(troop.MaxTroops);
                    break;
                default:
                    // 同防御分支：只改当前地形对应的那一套，避免两套数值同时放大
                    if (troop.IsInWater)
                    {
                        if (waterOk) troop.waterAttack = Modify(troop.waterAttack);
                    }
                    else
                    {
                        if (landOk) troop.landAttack = Modify(troop.landAttack);
                    }
                    break;
            }
        }
    }
}
