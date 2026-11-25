using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 命中触发（伤害结算前）的特技效果，统一 <see cref="CombatContext"/>。
    ///
    /// <para>
    /// 与 <see cref="FeatureCombatDamageEffect"/> 的分工：命中只看“打中了没有”，
    /// 不依赖伤害数值，适合“命中后概率施加 BUFF / 减气力”这类设计。
    /// </para>
    /// <para>
    /// 参数：skillType（0 普攻 / 1 战法 / 2 计策 / 3 任意）、targetType（0 目标 / 1 施术者 / 2 双方）、
    /// morale、buffIds / buffProbability / buffTurns。
    /// </para>
    /// </summary>
    public class FeatureCombatHitEffect : TroopActionBase
    {
        /// <summary>0 普攻 / 1 战法 / 2 计策 / 3 任意</summary>
        int skillType;
        /// <summary>0 目标 / 1 施术者 / 2 双方</summary>
        int targetType;
        /// <summary>气力增减</summary>
        int morale;
        /// <summary>BUFF 候选池</summary>
        int[] buffIds;
        /// <summary>BUFF 概率（万分比）</summary>
        int buffProbability;
        /// <summary>BUFF 持续回合</summary>
        int buffTurns = 1;

        /// <summary>防递归：效果触发的 ChangeMorale / AddBuff 不再进入本 Action</summary>
        static bool applying;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            skillType = p.Value<int>("skillType");
            if (skillType < 0 || skillType > 3)
            {
                Sango.Log.Error($"[战斗特技] FeatureCombatHitEffect 的 skillType={skillType} 非法（0~3），已按任意处理");
                skillType = 3;
            }
            targetType = p.Value<int>("targetType");
            morale = p.Value<int>("morale");
            buffIds = p.Value<JArray>("buffIds")?.ToObject<int[]>();
            buffProbability = p.Value<int>("buffProbability");
            buffTurns = p.Value<int>("buffTurns");
            if (buffTurns <= 0) buffTurns = 1;

            GameEvent.OnCombatHit += OnCombatHit;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCombatHit -= OnCombatHit;
        }

        /// <summary>命中回调：按技能分类与目标类型筛选</summary>
        void OnCombatHit(CombatContext ctx)
        {
            if (applying || ctx == null || Troop == null)
                return;

            bool isCaster = ctx.caster == Troop;
            bool isTarget = ctx.targetTroop == Troop;
            if (!isCaster && !isTarget)
                return;

            if (skillType == 0 && !ctx.isNormal) return;
            if (skillType == 1 && (ctx.isStrategy || ctx.isNormal)) return;
            if (skillType == 2 && !ctx.isStrategy) return;

            applying = true;
            try
            {
                if (targetType == 0)
                    Apply(isCaster ? ctx.targetTroop : ctx.caster);
                else if (targetType == 1)
                    Apply(isCaster ? ctx.caster : ctx.targetTroop);
                else
                {
                    Apply(ctx.caster);
                    Apply(ctx.targetTroop);
                }
            }
            finally
            {
                applying = false;
            }
        }

        /// <summary>对单个目标结算气力 / BUFF</summary>
        void Apply(Troop target)
        {
            if (target == null || !target.IsAlive)
                return;

            if (morale != 0)
                target.ChangeMorale(morale);

            if (buffIds != null && buffIds.Length > 0 && buffProbability > 0
                && GameRandom.Chance(buffProbability, 10000))
            {
                int buffId = buffIds[GameRandom.Range(0, buffIds.Length)];
                target.AddBuff(buffId, buffTurns, Troop);
            }
        }
    }
}
