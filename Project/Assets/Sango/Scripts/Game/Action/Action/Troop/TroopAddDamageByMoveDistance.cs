using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按本回合行军距离增加伤害（冲锋 / 突击一类"跑得越远打得越狠"的效果）。
    ///
    /// <para>
    /// 【距离从哪来】<see cref="Troop.moveDistanceThisTurn"/>：由 <c>Troop.UpdateCell</c>
    /// 每换一格 +1、<c>OnForceTurnStart</c> 清零，口径是"本回合到目前为止走过的格数"。
    /// </para>
    /// <para>
    /// 【只对攻击方生效】必须由持有者自己打出去才算，被敌人打进来不加成；
    /// 因此不用 <c>isDefender</c> 参数，直接在结算里比较 <c>atk.AsTroop() == Troop</c>。
    /// </para>
    /// <para>
    /// 【上限】<c>maxDistance</c> 用于封顶：长途奔袭不该无限放大伤害，
    /// 配 0 表示不封顶（不建议，容易被绕圈刷满）。
    /// </para>
    /// </summary>
    public class TroopAddDamageByMoveDistance : TroopTroopActionBase
    {
        /// <summary>每移动一格增加的伤害百分点</summary>
        int percentPerCell;

        /// <summary>计入的格数上限（0 = 不封顶）</summary>
        int maxDistance;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：percentPerCell（缺省回落到 value）、maxDistance（缺省 0）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            percentPerCell = p.Value<int?>("percentPerCell") ?? value;
            maxDistance = p.Value<int>("maxDistance");

            if (percentPerCell <= 0)
            {
                Sango.Log.Warning("TroopAddDamageByMoveDistance 的 percentPerCell 未配置（或 <= 0），不会生效");
                return;
            }

            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        /// <summary>
        /// 兵力变化结算时按行军距离增伤。
        /// </summary>
        /// <param name="defencer">受伤部队</param>
        /// <param name="atk">攻击来源</param>
        /// <param name="atkBack">反击标记</param>
        /// <param name="overrideData">兵力变化量</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atk, int atkBack, OverrideData<int> overrideData)
        {
            if (Troop == null || overrideData == null)
                return;

            // 只修正掉兵；回血不参与
            if (overrideData.Value <= 0)
                return;

            // 必须是自己打出去的伤害（火焰 / 建筑反击取不到部队，自然被挡掉）
            Troop atkTroop = atk?.AsTroop();
            if (atkTroop != Troop)
                return;

            int dist = Troop.moveDistanceThisTurn;
            if (dist <= 0)
                return;
            if (maxDistance > 0 && dist > maxDistance)
                dist = maxDistance;

            if (condition != null && !condition.Check(new TroopConditionDatabase(Troop)))
                return;

            overrideData.Value = overrideData.Value * (100 + dist * percentPerCell) / 100;
        }
    }
}
