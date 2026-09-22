/*
 * 文件名：DuelPersonBehaviour.cs
 * 描述：单个武将的"单挑行为覆盖"基类。
 *
 * 设计：
 *   · 代码里只保留这一套钩子（基类 + 注册表 + 数据驱动实现），**不写任何具体武将的分支**；
 *   · 每个武将的行为要么不配置（= 这里全部默认实现，等价于"没有任何加成"），
 *     要么由数据文件 Data/Common/duelPersonBehaviour.json 驱动（见 DuelPersonBehaviours）；
 *   · 以后要加新武将 / 新规则，优先改数据；只有当某条规则数据表达不了时，才在基类上加钩子。
 *
 * 钩子分四组：战力 / 一击必杀 / 伤害 / 概率与行动。
 * 带【预留】标记的是当前版本还没有接线、留给后续扩展的空实现。
 */

namespace Sango.Core.Duel
{
    /// <summary>武将单挑行为。默认实现 = 不加任何修正。</summary>
    public class DuelPersonBehaviour
    {
        /// <summary>武将真实 Id（Person.Id；-1 = 未指定。调试 / 日志用）</summary>
        public int personId = -1;

        /// <summary>配置名（调试 / 日志用）</summary>
        public string name;

        #region 战力

        /// <summary>
        /// 武力修正。revised = true 表示这是"已按伤病折算"的那次取值（GetDuelStrength）。
        /// 例：吕布 +10、张飞 / 关羽 +5。
        /// </summary>
        public virtual int ModifyDuelStrength(Person self, Person opponent, int strength, bool revised) { return strength; }

        /// <summary>年龄衰减修正（黄忠：虚拟模式给固定值，否则按年龄段）</summary>
        public virtual int ModifyAgeDecay(Person self, int bonus, LifeMode lifeMode) { return bonus; }

        #endregion

        #region 一击必杀

        /// <summary>会心一击(暴击)概率修正。attacker 表示自己是不是攻方。</summary>
        public virtual int ModifyCriticalChance(Person self, Person opponent, int chance, bool attacker) { return chance; }

        /// <summary>一击必杀概率修正。</summary>
        public virtual int ModifyFtkChance(Person self, Person opponent, int chance, bool attacker) { return chance; }

        /// <summary>是否免疫一击必杀（例：吕布 / 关羽 / 张飞 / 许褚 / 赵云 / 马超）。</summary>
        public virtual bool IsFtkImmune(Person self, Person opponent) { return false; }

        #endregion

        #region 伤害

        /// <summary>必杀威力修正（special = DuelSpecial）。</summary>
        public virtual int ModifySpecialDamage(Person self, Person opponent, int special, int damage) { return damage; }

        /// <summary>必杀命中伤害修正（对特定对手的额外加成）。</summary>
        public virtual int ModifySpecialHitDamage(Person self, Person opponent, int special, int damage) { return damage; }

        /// <summary>普通攻击伤害修正。</summary>
        public virtual int ModifyAttackDamage(Person self, Person opponent, int damage) { return damage; }

        /// <summary>【预留】受到伤害时的减免修正。</summary>
        public virtual int ModifyDamageTaken(Person self, Person opponent, int damage) { return damage; }

        #endregion

        #region 概率 / 行动 / AI

        /// <summary>退却概率修正（例：关羽 +2，对手是关羽则 −2）。</summary>
        public virtual int ModifyRetreatChance(Person self, Person opponent, int chance) { return chance; }

        /// <summary>【先攻】自己这一侧的先攻概率修正（例：关羽 +2）。</summary>
        public virtual int ModifyFirstChance(Person self, Person opponent, int chance) { return chance; }

        /// <summary>【先攻·对手侧】当我作为"对手"时，对对方先攻概率的修正（例：对面是关羽则 −2）。</summary>
        public virtual int ModifyOpponentFirstChance(Person self, Person opponent, int chance) { return chance; }

        /// <summary>【预留】致伤（负伤）概率修正。</summary>
        public virtual int ModifyWoundChance(Person self, Person opponent, int chance) { return chance; }

        /// <summary>【预留】斗志获得量修正（百分比，100 = 不变）。</summary>
        public virtual int ModifySpiritGain(Person self, bool attacker, int value) { return value; }

        /// <summary>【预留】单挑抓捕率修正（在"胜方部队抓捕率 − 败方逃跑系数"之后再修正）。</summary>
        public virtual int ModifyCaptureChance(Person self, Person opponent, int chance) { return chance; }

        /// <summary>【预留】单挑触发概率修正（战法命中后挑起单挑）。</summary>
        public virtual int ModifyDuelTriggerChance(Person self, Troop selfTroop, int chance) { return chance; }

        /// <summary>必定格挡（例：吕布 / 张飞 / 关羽 双方同为攻守时 50% 格挡）。</summary>
        public virtual bool IsAlwaysBlock(Duel duel, Person self, Person opponent) { return false; }

        /// <summary>AI 行为表 id；-1 = 走默认"按性格选表"。</summary>
        public virtual int GetAITableId(Person self) { return -1; }

        /// <summary>【预留】行动方针系数修正。</summary>
        public virtual int ModifyStanceCoef(Person self, int stance, int coef) { return coef; }

        /// <summary>【预留】必杀斗志消耗修正。</summary>
        public virtual int ModifySpecialCost(Person self, int special, int cost) { return cost; }

        /// <summary>【预留】是否允许使用某个必杀。</summary>
        public virtual bool CanUseSpecial(Person self, int special) { return true; }

        /// <summary>【预留】宝物提供的单挑战力修正。</summary>
        public virtual int ModifyItemPower(Person self, int power) { return power; }

        #endregion
    }
}
