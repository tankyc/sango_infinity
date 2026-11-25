using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 伤害已结算触发的特技效果（统一 <see cref="CombatContext"/>）。
    ///
    /// <para>
    /// 与 <see cref="FeatureCombatDuelEffect"/> 的分工：单挑用单挑事件，
    /// 这个是普攻 / 战法 / 计策命中并完成伤害结算后的通用入口。
    /// </para>
    /// <para>
    /// 参数：skillType（0 普攻 / 1 战法 / 2 计策 / 3 任意）、targetType（0 目标 / 1 施术者 / 2 双方）、
    /// selfHealPercent（按本次伤害给施术者回兵）、morale、buffIds / buffProbability / buffTurns。
    /// </para>
    /// </summary>
    public class FeatureCombatDamageEffect : TroopActionBase
    {
        /// <summary>0 普攻 / 1 战法 / 2 计策 / 3 任意</summary>
        int skillType;
        /// <summary>0 目标 / 1 施术者 / 2 双方</summary>
        int targetType;
        /// <summary>按本次伤害给施术者回兵的百分比</summary>
        int selfHealPercent;
        /// <summary>气力增减</summary>
        int morale;
        /// <summary>BUFF 候选池</summary>
        int[] buffIds;
        /// <summary>BUFF 概率（万分比）</summary>
        int buffProbability;
        /// <summary>BUFF 持续回合</summary>
        int buffTurns = 1;

        /// <summary>防递归：本 Action 自己触发的 ChangeTroops 不再进入本 Action</summary>
        static bool applying;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            skillType = p.Value<int>("skillType");
            if (skillType < 0 || skillType > 3)
            {
                Sango.Log.Error($"[战斗特技] FeatureCombatDamageEffect 的 skillType={skillType} 非法（0~3），已按任意处理");
                skillType = 3;
            }
            targetType = p.Value<int>("targetType");
            selfHealPercent = p.Value<int>("selfHealPercent");
            morale = p.Value<int>("morale");
            buffIds = p.Value<JArray>("buffIds")?.ToObject<int[]>();
            buffProbability = p.Value<int>("buffProbability");
            buffTurns = p.Value<int>("buffTurns");
            if (buffTurns <= 0) buffTurns = 1;

            GameEvent.OnCombatDamageCalculated += OnCombatDamageCalculated;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCombatDamageCalculated -= OnCombatDamageCalculated;
        }

        /// <summary>伤害已结算回调</summary>
        void OnCombatDamageCalculated(CombatContext ctx)
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
                    Apply(ctx, isCaster ? ctx.targetTroop : ctx.caster);
                else if (targetType == 1)
                    Apply(ctx, isCaster ? ctx.caster : ctx.targetTroop);
                else
                {
                    Apply(ctx, ctx.caster);
                    Apply(ctx, ctx.targetTroop);
                }
            }
            finally
            {
                applying = false;
            }
        }

        /// <summary>对单个目标结算回兵 / 气力 / BUFF</summary>
        void Apply(CombatContext ctx, Troop target)
        {
            if (target == null || !target.IsAlive)
                return;

            if (selfHealPercent > 0 && ctx.caster == target)
            {
                int damage = ctx.damageBefore - ctx.damageAfter;
                if (damage > 0)
                {
                    int heal = damage * selfHealPercent / 100;
                    if (heal > 0)
                        target.ChangeTroops(heal, target, 0);
                }
            }

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
