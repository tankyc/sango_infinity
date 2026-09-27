using System;

namespace Sango.Core
{
    /// <summary>
    /// 二虎竞食：使者抵达第一个目标势力的治所后施法，挑拨两个第三方势力的交情。
    /// 成功则让 A、B 两个目标势力的关系按固定值恶化，失败则反噬发起方与两个目标方的关系。
    /// 目标必须是"第三方"：不能挑拨自己与自己、也不能把发起方算进被挑拨的一对里。
    /// 本计略不潜入敌方据点办事，因此使者没有被捕风险（基类 CanBeCaptured 默认 false）。
    /// </summary>
    public class CityStrategyActionTwoTigers : CityStrategyActionBase
    {
        /// <summary>
        /// 构造二虎竞食行为
        /// </summary>
        /// <param name="sender">发起方势力</param>
        /// <param name="fromCity">出发据点</param>
        /// <param name="diplomat">使者武将</param>
        /// <param name="targetCity">使者目的地，即 TargetForceA 的治所</param>
        /// <param name="targetForceA">被挑拨的势力A</param>
        /// <param name="targetForceB">被挑拨的势力B</param>
        public CityStrategyActionTwoTigers(Force sender, City fromCity, Person diplomat, City targetCity, Force targetForceA, Force targetForceB)
            : base(CityStrategyType.TwoTigers, CityJobType.TwoTigers, sender, fromCity, diplomat, targetCity, targetForceA, targetForceB)
        {
        }

        /// <summary>
        /// 任务参数3：目标势力对中 Id 较小的一方。
        /// 固定按 Id 大小顺序存放，这样"选A再选B"和"选B再选A"会还原成同一份参数，
        /// 在途去重也只需要做相等比较
        /// </summary>
        public override int MissionParam3
        {
            get
            {
                if (TargetForceA == null || TargetForceB == null)
                    return 0;
                return Math.Min(TargetForceA.Id, TargetForceB.Id);
            }
        }

        /// <summary>
        /// 任务参数4：目标势力对中 Id 较大的一方
        /// </summary>
        public override int MissionParam4
        {
            get
            {
                if (TargetForceA == null || TargetForceB == null)
                    return 0;
                return Math.Max(TargetForceA.Id, TargetForceB.Id);
            }
        }

        /// <summary>
        /// 检查是否具备施放条件：两个目标势力都存在、存活、互不相同，且都不是发起方；使者目的地有效。
        /// </summary>
        /// <returns>可以施放返回 true</returns>
        public override bool CanPerform()
        {
            if (Sender == null || TargetCity == null)
                return false;
            if (TargetForceA == null || TargetForceB == null)
                return false;
            if (TargetForceA == TargetForceB)
                return false;
            if (!TargetForceA.IsAlive || !TargetForceB.IsAlive)
                return false;
            // 发起方不能是自己挑拨自己的一环，否则等于用计略给自己刷关系
            if (TargetForceA == Sender || TargetForceB == Sender)
                return false;
            return true;
        }

        /// <summary>
        /// 计算成功率：抵抗值为两个目标势力各自抵抗者智力的算术平均。
        /// 抵抗者按原版口径取"军师 → 君主"（B 方没有对应据点，故不参与太守这一档；A 方用其治所取太守）。
        /// 不再把"未设军师"当成智力 0，避免抵抗值被凭空腰斩。
        /// </summary>
        /// <returns>成功率（0-100 百分比）</returns>
        public override int CalculateSuccessRate()
        {
            Person resisterA = GetResistance(TargetForceA, TargetForceA.CapitalCity);
            Person resisterB = GetResistance(TargetForceB, TargetForceB.CapitalCity);
            // 记下方 A 的抵抗者供日志复述；双方抵抗者都缺位时按 0 计，由 CalculateRate 的下限托住
            Resister = resisterA ?? resisterB;
            int resistance = ((resisterA?.Intelligence ?? 0) + (resisterB?.Intelligence ?? 0)) / 2;
            SuccessRate = CalculateRate(Diplomat?.Intelligence ?? 0, resistance);
            return SuccessRate;
        }

        /// <summary>
        /// 结算二虎竞食：成功（含露馅）则恶化两个目标势力之间的交情，失败则走反噬。
        /// 交情跌破 cityStrategyWarTriggerRelation 之后是否破盟、开战，由 AI 在自己的势力回合里判定
        /// （CityStrategyAI），不在计略结算帧内直接改同盟结构，避免打断回合循环。
        /// </summary>
        /// <param name="result">本次计略的四态结果</param>
        public override void Perform(CityStrategyResult result)
        {
            if (result != CityStrategyResult.SucceededUndetected && result != CityStrategyResult.SucceededDetected)
            {
                OnFailed();
                return;
            }

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            int decrease = Scenario.Cur.Variables.cityStrategyTwoTigersRelationDecrease;
            // 两个目标方之间的关系是对称的，只需扣一次
            diplomacyManager.AddRelation(TargetForceA, TargetForceB, -decrease);

            int relationNow = diplomacyManager.GetRelation(TargetForceA, TargetForceB);
            string headline = result == CityStrategyResult.SucceededDetected
                ? "奏效，但使者露了行迹"
                : "奏效";
            BroadcastMessage($"{Sender?.ColorName}派{Diplomat?.ColorName ?? "使者"}施放的{GetActionName()}{headline}，"
                + $"{TargetForceA?.ColorName}与{TargetForceB.ColorName}的关系下降 {decrease}（现值 {relationNow}）。");

            // 露馅只付关系代价，不影响已经落到两方之间的交情恶化
            if (result == CityStrategyResult.SucceededDetected)
                OnDetected();

            // 给被挑拨的双方各记一笔"挑拨凭据"（互记，因为双方都可能据此重新评估这段同盟）。
            // 破盟/宣战本身不在结算帧做：CityStrategyAI 会在双方各自的势力回合里读这条凭据，
            // 再配合 cityStrategyWarTriggerRelation 判定要不要真的翻脸
            TargetForceA.MarkStrategyWarSeed(TargetForceB);
            TargetForceB.MarkStrategyWarSeed(TargetForceA);

#if SANGO_DEBUG
            Sango.Log.Info($"@计略@{Sender?.Name} 的{GetActionName()}结果={result}，{TargetForceA?.Name} 与 {TargetForceB?.Name} 的关系 -{decrease} → {relationNow}，抵抗者=({resisterNameA}, {resisterNameB})");
#endif
        }

        /// <summary>
        /// 仅供调试日志输出两方抵抗者姓名，缺位时记"无"。
        /// </summary>
        private string resisterNameA => GetResistance(TargetForceA, TargetForceA?.CapitalCity)?.Name ?? "无";

        /// <summary>
        /// 仅供调试日志输出 B 方抵抗者姓名，缺位时记"无"。
        /// </summary>
        private string resisterNameB => GetResistance(TargetForceB, TargetForceB?.CapitalCity)?.Name ?? "无";
    }
}
