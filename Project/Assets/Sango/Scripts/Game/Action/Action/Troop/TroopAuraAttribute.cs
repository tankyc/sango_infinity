using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技范围属性加成（以我方部队为圆心，按格数影响范围内部队的属性）。
    ///
    /// <para>
    /// 【与 AuraActionBase 的分工】<see cref="AuraActionBase"/> 走 AuraManager，
    /// 只支持攻击 / 防御两项，且没有 condition、没有兵种过滤；
    /// 本类直接挂 <see cref="GameEvent.OnTroopCalculateAttribute"/>，
    /// 在每次属性重算时用"圆心距离"实时判断，因此支持任意属性、条件与兵种过滤，
    /// 也不需要维护成员集合（比进出格事件更不容易漏算）。
    /// </para>
    /// <para>
    /// 【支持属性】attack / defence / moveAbility / foodCost。
    /// 前三个与 <see cref="TroopModifyAttributeByCondition"/> 同口径；
    /// foodCost 是"粮耗倍率"（100 = 不变，70 = 降低 30%）。
    /// </para>
    /// <para>
    /// 【势力级装配的限制】光环必须有圆心，所以本类只能配在部队（特技）上；
    /// 配在势力（技巧）上时会打 Error 并完全不生效 —— 这是刻意为之，
    /// 避免"技巧写了却不生效又查不出原因"。
    /// </para>
    /// </summary>
    public class TroopAuraAttribute : TroopTroopActionBase
    {
        /// <summary>
        /// 目标属性：attack / defence / moveAbility / foodCost。
        /// </summary>
        string attr;

        /// <summary>计算方式：0 = 加算固定值（默认），1 = 按百分比乘算；foodCost 恒按"设值"处理</summary>
        int mode;

        /// <summary>光环半径（格）</summary>
        int bound;

        /// <summary>作用对象：0 己方（含自己，默认）/ 1 敌方 / 2 全部</summary>
        int targetType;

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：bound、targetType、attr、mode、value、kinds、condition。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            bound = p.Value<int>("bound");
            targetType = p.Value<int>("targetType");
            attr = p.Value<string>("attr") ?? "attack";
            mode = p.Value<int>("mode");

            // 【装配校验】光环圆心 = 拥有者部队所在格，势力级装配没有圆心
            if (Troop == null)
            {
                Sango.Log.Error("TroopAuraAttribute 只能配在部队（特技）上，配在势力上不会生效");
                return;
            }
            if (bound <= 0)
            {
                Sango.Log.Warning($"TroopAuraAttribute 的 bound 未配置（或 <= 0），特技 {Troop.Name} 的范围加成不会生效");
                return;
            }

            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        /// <summary>目标是否在作用对象的允许范围内</summary>
        /// <param name="target">被重算的部队</param>
        /// <returns>允许返回 true</returns>
        bool MatchSide(Troop target)
        {
            if (targetType == 2)
                return true;
            bool same = target.BelongForce != null && target.BelongForce == Troop.BelongForce;
            return targetType == 1 ? !same : same;
        }

        /// <summary>按配置方式修改一项数值（foodCost 走"设值"语义）</summary>
        int Modify(int cur)
        {
            return mode == 1 ? cur * (100 + value) / 100 : cur + value;
        }

        /// <summary>
        /// 部队属性重算时判断是否落在光环内并套用加成。
        /// </summary>
        /// <param name="troop">正在重算的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (troop == null || Troop == null)
                return;

            // 【性能】按"最便宜的先判"排序：阵营是引用 / 布尔比较，距离是三次取绝对值取最大，
            // 条件检查最贵（可能遍历邻格）。先阵营后距离，能把敌方部队的重算直接挡掉。
            if (!MatchSide(troop))
                return;

            Cell ownerCell = Troop.cell;
            Cell targetCell = troop.cell;
            if (ownerCell == null || targetCell == null)
                return;
            if (ownerCell.Distance(targetCell) > bound)
                return;

            if (checkLand == 1 && troop.IsInWater) return;
            if (checkLand == 2 && !troop.IsInWater) return;
            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            bool landOk = kinds == null
                || (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind));
            bool waterOk = kinds == null
                || (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind));

            switch (attr)
            {
                case "defence":
                    if (landOk) troop.landDefence = Modify(troop.landDefence);
                    if (waterOk) troop.waterDefence = Modify(troop.waterDefence);
                    break;
                case "moveAbility":
                    if (landOk) troop.landMoveAbility = Modify(troop.landMoveAbility);
                    if (waterOk) troop.waterMoveAbility = Modify(troop.waterMoveAbility);
                    break;
                case "foodCost":
                    // 粮耗倍率按乘算累积：多条光环（营屯省用 + 节用）自然叠乘。
                    // foodCostFactor 每次属性重算前由 Troop.CalculateAttribute 重置为 1，
                    // 因此这里做乘法不会跨次累积，离开光环后也能自动恢复。
                    troop.foodCostFactor = troop.foodCostFactor * value / 100f;
                    break;
                default:
                    if (landOk) troop.landAttack = Modify(troop.landAttack);
                    if (waterOk) troop.waterAttack = Modify(troop.waterAttack);
                    break;
            }
        }
    }
}
