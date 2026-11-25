using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技范围减伤（以拥有者部队为圆心，半径内符合条件的部队受到的伤害按百分比降低）。
    ///
    /// <para>
    /// 【用途】干城（三格内己方/联盟部队减伤 7%）、义勇军（周围部队减伤）、
    /// 列阵（相邻友军减伤）等"范围内友军更耐打"的设计。
    /// 这类效果改的是"结算时的伤害"而不是"面板防御"，因此必须挂在
    /// <see cref="GameEvent.OnTroopChangeTroops"/>，不能挂在属性重算上。
    /// </para>
    /// <para>
    /// 【只减不增】只对 <c>overrideData.Value &gt; 0</c>（掉兵）生效，
    /// 回血 / 补给这类负值不参与，避免"减伤"变成"减治疗"。
    /// </para>
    /// </summary>
    public class TroopAuraDamageReduce : TroopTroopActionBase
    {
        /// <summary>光环半径（格）</summary>
        int bound;

        /// <summary>作用对象：0 己方（含自己，默认）/ 1 敌方 / 2 全部</summary>
        int targetType;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：bound、targetType、value（减伤百分点）、condition。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            bound = p.Value<int>("bound");
            targetType = p.Value<int>("targetType");

            if (Troop == null)
            {
                Sango.Log.Error("TroopAuraDamageReduce 只能配在部队（特技）上，配在势力上不会生效");
                return;
            }
            if (bound <= 0)
            {
                Sango.Log.Warning($"TroopAuraDamageReduce 的 bound 未配置（或 <= 0），特技 {Troop.Name} 的范围减伤不会生效");
                return;
            }

            // 走独立事件：多个设施/光环只取最大值，不会连乘
            GameEvent.OnTroopCalculateAuraDamageReduce += OnTroopCalculateAuraDamageReduce;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateAuraDamageReduce -= OnTroopCalculateAuraDamageReduce;
        }

        /// <summary>受伤部队是否属于本光环的作用对象</summary>
        /// <param name="target">受伤部队</param>
        /// <returns>属于返回 true</returns>
        bool MatchSide(Troop target)
        {
            if (targetType == 2)
                return true;
            bool same = target.BelongForce != null && target.BelongForce == Troop.BelongForce;
            return targetType == 1 ? !same : same;
        }

        /// <summary>
        /// 兵力变化结算时按光环距离减伤。
        /// </summary>
        /// <param name="defencer">受伤部队</param>
        /// <param name="atker">攻击来源</param>
        /// <param name="atkBack">反击强度因子，0 = 非反击（见 TroopActionBase.IsAttackBack）</param>
        /// <param name="overrideData">兵力变化量</param>
        void OnTroopCalculateAuraDamageReduce(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (defencer == null || Troop == null || overrideData == null)
                return;

            // 只收集正减伤；增伤请用其他 Action，避免这里的 max 语义被负数干扰
            if (value <= 0)
                return;

            if (!MatchSide(defencer))
                return;

            Cell ownerCell = Troop.cell;
            Cell targetCell = defencer.cell;
            if (ownerCell == null || targetCell == null)
                return;
            if (ownerCell.Distance(targetCell) > bound)
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(defencer)))
                return;

            // 多个设施/光环取最高减伤，不连乘
            if (value > overrideData.Value)
                overrideData.Value = value;
        }
    }
}
