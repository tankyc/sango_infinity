using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 每回合把一部分兵力转换为随军粮食（脯粮一类"以兵换粮"的效果）。
    ///
    /// <para>
    /// 【为什么挂在回合开始】转换会真实改动兵力，若挂在属性重算上，
    /// 一次界面刷新可能触发多次重算，等于按帧扣兵。
    /// <see cref="GameEvent.OnTroopTurnStart"/> 每支部队每回合只派发一次，
    /// 与"一年 36 回合、每回合一次结算"的节奏一致。
    /// </para>
    /// <para>
    /// 【安全护栏】转换量大于等于当前兵力时直接跳过：
    /// 该效果是"省粮/筹粮"，不应该把部队自己清零。
    /// </para>
    /// </summary>
    public class TroopConvertTroopsToFood : TroopTroopActionBase
    {
        /// <summary>每回合转换掉的兵力</summary>
        int cutPerTurn;

        /// <summary>每 1 名士兵转换出的粮食</summary>
        int foodPerTroop;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：cutPerTurn（缺省回落到 value）、foodPerTroop（缺省 1）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (Troop == null)
            {
                Sango.Log.Error("TroopConvertTroopsToFood 只能配在部队（特技）上，配在势力上不会生效");
                return;
            }

            cutPerTurn = p.Value<int?>("cutPerTurn") ?? value;
            foodPerTroop = p.Value<int?>("foodPerTroop") ?? 1;

            if (cutPerTurn <= 0)
            {
                Sango.Log.Warning($"TroopConvertTroopsToFood 的 cutPerTurn 未配置，特技 {Troop.Name} 不会生效");
                return;
            }

            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
        }

        /// <summary>
        /// 回合开始时执行一次兵力→粮食转换。
        /// </summary>
        /// <param name="troop">本回合开始的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (Troop == null || troop != Troop)
                return;

            // 护栏：不能把自己转没
            if (troop.troops <= cutPerTurn)
                return;

            // 抑制伤害修正：转换是效果结算，不应被"减伤/增伤"特技改写
            BeginSuppressDamageModifier();
            try
            {
                troop.ChangeTroops(-cutPerTurn, troop, 0);
                troop.food += cutPerTurn * foodPerTroop;
            }
            finally
            {
                EndSuppressDamageModifier();
            }
        }
    }
}
