using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 统一战斗结算上下文（单挑 / 命中 / 伤害后 / 击破）。
    ///
    /// <para>
    /// 【为什么要统一】旧事件 payload 各不相同（单挑是 Duel、战法是 SkillInstance+Troop、
    /// 击破是 Troop+SangoObject），特技侧要在四套异构参数上各写一遍过滤，且缺"持有人 /
    /// 主副将 / 胜败 / 被击破坐标"这类聚合字段。
    /// </para>
    /// <para>
    /// 【性能】用对象池复用，单挑/击破都是低频事件；无订阅者时调用方直接跳过，不产生分配。
    /// </para>
    /// </summary>
    public sealed class CombatContext
    {
        /// <summary>是否单挑结算</summary>
        public bool isDuel;
        /// <summary>单挑结果（相对 caster）：0 平 / 1 胜 / 2 负</summary>
        public int duelResult;
        /// <summary>实际施术者 / 胜方部队（单挑时）</summary>
        public Troop caster;
        /// <summary>目标部队 / 败方部队（单挑时）</summary>
        public Troop targetTroop;
        /// <summary>施术者主将</summary>
        public Person casterPerson;
        /// <summary>目标主将</summary>
        public Person targetPerson;
        /// <summary>当前战法（可为 null）</summary>
        public SkillInstance skill;
        /// <summary>是否计策</summary>
        public bool isStrategy;
        /// <summary>是否普攻</summary>
        public bool isNormal;
        /// <summary>伤害前兵力</summary>
        public int damageBefore;
        /// <summary>伤害后兵力</summary>
        public int damageAfter;
        /// <summary>被击破 / 受击格</summary>
        public Cell targetCell;
        /// <summary>原始来源（SkillInstance / Troop / Fire / null）</summary>
        public SangoObject source;

        static readonly Stack<CombatContext> pool = new Stack<CombatContext>(8);

        /// <summary>从池中取一个上下文（调用方负责 Return）</summary>
        public static CombatContext Rent()
        {
            return pool.Count > 0 ? pool.Pop() : new CombatContext();
        }

        /// <summary>归还上下文并清空引用，避免池持有对象</summary>
        public static void Return(CombatContext ctx)
        {
            if (ctx == null)
                return;

            ctx.isDuel = false;
            ctx.duelResult = 0;
            ctx.caster = null;
            ctx.targetTroop = null;
            ctx.casterPerson = null;
            ctx.targetPerson = null;
            ctx.skill = null;
            ctx.isStrategy = false;
            ctx.isNormal = false;
            ctx.damageBefore = 0;
            ctx.damageAfter = 0;
            ctx.targetCell = null;
            ctx.source = null;

            pool.Push(ctx);
        }
    }
}
