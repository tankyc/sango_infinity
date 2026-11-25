using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 回合开始时对范围内部队转移兵力（攻心 / 轮回一类效果的通用实现）。
    ///
    /// <para>
    /// 【两种模式】
    /// · mode = 0 抽取（攻心）：把半径内敌军的兵力按配置比例转给自己；
    /// · mode = 1 恢复（轮回）：给半径内友军补充兵力，数量与主将魅力相关。
    /// </para>
    /// <para>
    /// 【为什么与魅力挂钩】设计稿写明"数值和魅力相关""与君主和主将的魅力有关系"，
    /// 因此基数取 <c>主将魅力 × percent%</c>，再由 <c>divByTargetCount</c> 决定
    /// "目标越多、单体越少"（对应"范围内部队数量越多，单回合恢复兵力数量越少"）。
    /// </para>
    /// <para>
    /// 【为什么抑制伤害修正】这里改兵力是"效果结算"而不是"战斗伤害"，
    /// 若不抑制，敌方的减伤特技会把吸取量打折、我方的增伤特技反而会放大吸取量。
    /// 用 <c>BeginSuppressDamageModifier</c> 包住整个结算区间，与陷阱结算同一口径。
    /// </para>
    /// </summary>
    public class TroopTurnDrainTroops : TroopTroopActionBase
    {
        /// <summary>作用半径（格），配置 0 时按 1 处理</summary>
        int bound;

        /// <summary>0 抽取敌军 / 1 恢复友军</summary>
        int mode;

        /// <summary>按主将魅力的百分比折算基数</summary>
        int percent;

        /// <summary>单回合单个目标的兵力上限（0 表示不限制）</summary>
        int maxPerTurn;

        /// <summary>1 = 目标越多单体越少（默认）/ 0 = 每个目标都用全额</summary>
        int divByTargetCount;

        /// <summary>目标复用表（避免每次回合都新分配 List）</summary>
        readonly List<Troop> targetBuffer = new List<Troop>(16);

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：bound、mode、percent（缺省回落到 value）、maxPerTurn、
        /// divByTargetCount（缺省 1）。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (Troop == null)
            {
                Sango.Log.Error("TroopTurnDrainTroops 只能配在部队（特技）上，配在势力上不会生效");
                return;
            }

            bound = p.Value<int>("bound");
            if (bound <= 0)
                bound = 1;

            mode = p.Value<int>("mode");
            percent = p.Value<int?>("percent") ?? value;
            maxPerTurn = p.Value<int>("maxPerTurn");
            divByTargetCount = p.Value<int?>("divByTargetCount") ?? 1;

            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
        }

        /// <summary>
        /// 回合开始时结算一次兵力转移。
        /// </summary>
        /// <param name="troop">本回合开始的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            // 只在拥有者自己的回合触发：每支部队回合开始都会派发，不做此判断会结算 N 次
            if (Troop == null || troop != Troop)
                return;
            if (troop.cell == null)
                return;

            int baseAmount = troop.Glamour * percent / 100;
            if (baseAmount <= 0)
                return;

            targetBuffer.Clear();
            Troop owner = troop;
            int modeLocal = mode;
            troop.cell.Spiral(bound, c =>
            {
                Troop t = c.troop;
                if (t == null || t == owner)
                    return;
                bool same = t.IsSameForce(owner);
                if (modeLocal == 0 ? same : !same)
                    return;
                targetBuffer.Add(t);
            });

            if (targetBuffer.Count == 0)
                return;

            int amount = divByTargetCount == 1 ? baseAmount / targetBuffer.Count : baseAmount;
            if (amount < 1)
                amount = 1;
            if (maxPerTurn > 0 && amount > maxPerTurn)
                amount = maxPerTurn;

            BeginSuppressDamageModifier();
            try
            {
                for (int i = 0; i < targetBuffer.Count; i++)
                {
                    Troop t = targetBuffer[i];
                    if (t == null || !t.IsAlive)
                        continue;

                    if (mode == 0)
                    {
                        // 抽取：敌军减、自己加，形成"吸血"而非凭空增兵
                        t.ChangeTroops(-amount, troop, 0);
                        troop.ChangeTroops(amount, troop, 0);
                    }
                    else
                    {
                        t.ChangeTroops(amount, troop, 0);
                    }
                }
            }
            finally
            {
                EndSuppressDamageModifier();
            }
        }
    }
}
