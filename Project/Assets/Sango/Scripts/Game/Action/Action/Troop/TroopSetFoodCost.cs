using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 设置本部队的粮耗倍率（百分比）。
    ///
    /// <para>
    /// 【用途】辟谷奇兵（部队不消耗粮食，value = 0）、军屯（在港口/关口的部队不耗粮，
    /// 配 condition 判定据点类型）、节用（内政与部队建筑资金/粮耗减半）。
    /// </para>
    /// <para>
    /// 【挂载口径】写 <c>troop.foodCostFactor</c>。该字段在
    /// <c>Troop.CalculateAttribute</c> 开头被重置为 1（与移动力同口径），
    /// 所以这里用乘法累积不会跨次叠加，摘下特技后下次重算自动恢复。
    /// </para>
    /// <para>
    /// 【value 必须显式配置】缺省值会被读成 0，而 0 恰好等价于"完全不耗粮"，
    /// 属于危险的默认值 —— 因此这里对"没有 value 键"直接报错并放弃装配。
    /// </para>
    /// </summary>
    public class TroopSetFoodCost : TroopTroopActionBase
    {
        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（百分比，100 = 正常，0 = 不耗粮）、checkLand、kinds、condition。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (p == null || p["value"] == null)
            {
                Sango.Log.Error("TroopSetFoodCost 缺少 value 参数（0 表示完全不耗粮），已放弃装配以避免误配");
                return;
            }

            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        /// <summary>
        /// 部队属性重算时按配置改写粮耗倍率。
        /// </summary>
        /// <param name="troop">正在重算的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (troop == null)
                return;

            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            if (checkLand == 1 && troop.IsInWater) return;
            if (checkLand == 2 && !troop.IsInWater) return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            bool landOk = kinds == null
                || (troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind));
            bool waterOk = kinds == null
                || (troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind));
            if (troop.IsInWater ? !waterOk : !landOk)
                return;

            troop.foodCostFactor = troop.foodCostFactor * value / 100f;

            // 立即刷新一次粮耗，让"撤兵/运输"界面上的日数估算与本回合结算口径一致。
            // PrepeareFoodCost 每次都从基础值重算（含着火烧粮），幂等且不会累加。
            troop.PrepeareFoodCost();
        }
    }
}
