using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 提高本部队的带兵上限（附众：部队带兵上限 +3000）。
    ///
    /// <para>
    /// 【与 ForceTroopMaxTroop 的区别】那个是势力级技巧，走
    /// <c>OnTroopCalculateMaxTroops</c> 给全势力部队加成；
    /// 本类挂在部队上（特技），只影响持技的这一支部队。
    /// </para>
    /// <para>
    /// 【累加安全】<c>Troop.CalculateMaxTroops</c> 在 <c>CalculateAttribute</c> 里
    /// 先整体重算 <c>MaxTroops</c>，之后才派发 <see cref="GameEvent.OnTroopCalculateAttribute"/>，
    /// 所以这里 <c>+=</c> 不会跨次叠加。挂在属性事件而不是 MaxTroops 事件上，
    /// 是为了复用已验证的挂载点，且两处都只有一个触发者。
    /// </para>
    /// </summary>
    public class TroopAddMaxTroops : TroopTroopActionBase
    {
        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（追加的带兵上限）、condition（可选）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (value <= 0)
            {
                Sango.Log.Warning("TroopAddMaxTroops 的 value 未配置（或 <= 0），不会生效");
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
        /// 部队属性重算时追加带兵上限。
        /// </summary>
        /// <param name="troop">正在重算的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (troop == null)
                return;

            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            troop.MaxTroops += value;
        }
    }
}
