using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 单挑结算触发的特技效果（统一 <see cref="CombatContext"/>）。
    ///
    /// <para>
    /// 【验收例】赢单挑后只混乱败军：
    /// <c>{ "class":"FeatureCombatDuelEffect", "resultType":0, "targetType":0,
    ///      "buffIds":[1], "buffProbability":10000, "buffTurns":2 }</c>
    /// 失败 / 平局 / 败方无部队 / 同势力都不会误触发。
    /// </para>
    /// <para>
    /// 参数：resultType（0 仅胜 / 1 仅负 / 2 平 / 3 任意）、targetType（0 败方 / 1 胜方 / 2 双方）、
    /// buffIds / buffProbability（万分比）/ buffTurns、morale、troopsPercent。
    /// </para>
    /// </summary>
    public class FeatureCombatDuelEffect : TroopActionBase
    {
        /// <summary>0 仅胜 / 1 仅负 / 2 平 / 3 任意</summary>
        int resultType;
        /// <summary>0 败方 / 1 胜方 / 2 双方</summary>
        int targetType;
        /// <summary>BUFF 候选池</summary>
        int[] buffIds;
        /// <summary>BUFF 概率（万分比）</summary>
        int buffProbability;
        /// <summary>BUFF 持续回合</summary>
        int buffTurns = 1;
        /// <summary>气力增减</summary>
        int morale;
        /// <summary>兵力百分比增减</summary>
        int troopsPercent;
        /// <summary>使目标主将负伤的概率（百分点，0 = 不启用）</summary>
        int injure;
        /// <summary>伤病等级（默认 1 = 轻伤）</summary>
        int injuryLevel = 1;

        /// <summary>
        /// 初始化并订阅单挑结算事件。
        /// </summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            resultType = p.Value<int>("resultType");
            targetType = p.Value<int>("targetType");
            buffIds = p.Value<JArray>("buffIds")?.ToObject<int[]>();
            buffProbability = p.Value<int>("buffProbability");
            buffTurns = p.Value<int>("buffTurns");
            if (buffTurns <= 0) buffTurns = 1;
            morale = p.Value<int>("morale");
            troopsPercent = p.Value<int>("troopsPercent");
            injure = p.Value<int>("injure");
            int level = p.Value<int>("injuryLevel");
            injuryLevel = level > 0 ? level : 1;

            GameEvent.OnCombatDuelFinished += OnCombatDuelFinished;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCombatDuelFinished -= OnCombatDuelFinished;
        }

        /// <summary>单挑结算回调：按胜负与目标类型筛选后结算</summary>
        void OnCombatDuelFinished(CombatContext ctx)
        {
            if (ctx == null || !ctx.isDuel || Troop == null)
                return;

            bool isWinner = ctx.caster == Troop;
            bool isLoser = ctx.targetTroop == Troop;
            if (!isWinner && !isLoser)
                return;

            // 同势力单挑（剧本事件）默认不触发，避免"友军切磋"误伤
            if (ctx.caster != null && ctx.targetTroop != null
                && ctx.caster.IsSameForce(ctx.targetTroop))
                return;

            if (resultType == 0 && !isWinner) return;
            if (resultType == 1 && !isLoser) return;
            if (resultType == 2) return; // 当前只派发"有胜负"的单挑

            if (targetType == 0)
                Apply(isWinner ? ctx.targetTroop : ctx.caster);
            else if (targetType == 1)
                Apply(isWinner ? ctx.caster : ctx.targetTroop);
            else
            {
                Apply(ctx.caster);
                Apply(ctx.targetTroop);
            }
        }

        /// <summary>对单个目标结算兵力 / 气力 / BUFF</summary>
        void Apply(Troop target)
        {
            if (target == null || !target.IsAlive)
                return;

            if (troopsPercent != 0)
            {
                int num = target.troops * troopsPercent / 100;
                if (num != 0)
                    target.ChangeTroops(num, Troop, 0);
            }

            if (morale != 0)
                target.ChangeMorale(morale);

            if (buffIds != null && buffIds.Length > 0 && buffProbability > 0
                && GameRandom.Chance(buffProbability, 10000))
            {
                int buffId = buffIds[GameRandom.Range(0, buffIds.Length)];
                target.AddBuff(buffId, buffTurns, Troop);
            }

            // 单挑结算后使目标主将负伤（锦骑 / 喝阵一类"单挑胜利令败军主将受伤"）
            // GameRandom.Chance 的参数是百分点，与其它 Action 的概率口径一致
            if (injure > 0 && target.Leader != null && GameRandom.Chance(injure))
            {
                Person hurt = target.Leader;
                if (PersonInjury.Injure(hurt, injuryLevel, Troop))
                {
                    Sango.Log.Info($"@特技@[{Troop?.Name}]单挑结算使[{target.Name}]的主将[{hurt.Name}]负伤，等级{ hurt.injury}");
                    GameEvent.OnPersonInjury?.Invoke(hurt, Troop);
                }
            }
        }
    }
}
