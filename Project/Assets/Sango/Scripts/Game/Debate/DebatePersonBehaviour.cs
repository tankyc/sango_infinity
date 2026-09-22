/*
 * 文件名：DebatePersonBehaviour.cs
 * 描述：单个武将的"舌战行为覆盖"基类（与单挑的 DuelPersonBehaviour 同款式）。
 *
 * 设计：
 *   · 代码里只保留这一套钩子（基类 + 注册表 + 数据驱动实现），**不写任何具体武将的分支**；
 *   · 每个武将的行为要么不配置（= 这里全部默认实现，等价于"没有任何加成"），
 *     要么由数据文件 Data/Common/debatePersonBehaviour.json 驱动（见 DebatePersonBehaviours）；
 *   · 以后要加新武将 / 新规则，优先改数据；只有当某条规则数据表达不了时，才在基类上加钩子。
 *
 * 与"剧本参数"的关系（按既定决策）：
 *   公式 / 参数先算出基础值，**behaviour 在其后覆盖** —— 每个钩子拿到的入参都是"已经算好的基础值"，
 *   返回什么就是最终值。话术那一项把 Person.wordTac 当基础值，behaviour 既可以追加也能覆盖。
 *
 * 命中方式：只按武将**真实 Id**（Person.Id，与 PersonLibrary.json 里的 Id 一致）找，
 *           不做"按姓名 / 按特技 / 按势力"的匹配。
 * 版本 1 接了 4 个钩子（话术 / 攻击力 / 体力伤害 / 会心倾向），其余是留待扩展的空实现。
 */

namespace Sango.Core.Debate
{
    /// <summary>武将舌战行为。默认实现 = 不加任何修正。</summary>
    public class DebatePersonBehaviour
    {
        /// <summary>该行为对应的武将真实 Id（-1 = 未指定，调试 / 日志用）</summary>
        public int personId = -1;

        /// <summary>配置名（调试 / 日志用）</summary>
        public string name;

        #region 话术

        /// <summary>
        /// 是否持有某个话术。rhetoric 与 Rhetoric 枚举一致（0=大喝 / 1=诡辩 / 2=无视 / 3=镇静 / 4=激昂）。
        ///
        /// has 是基础值：Person.wordTac 里有没有这一条（持有"书籍"时恒为 true，见 DebateGameSystem.HasAllRhetoric）。
        /// 返回 true 就把这张话术牌放进卡组。数据驱动实现支持"追加"与"覆盖"两种写法。
        /// </summary>
        public virtual bool ModifyRhetoric(Person self, int rhetoric, bool has) { return has; }

        #endregion

        #region 数值

        /// <summary>
        /// 攻击力修正。attack 是 CharacterInit 里按双方智力差算出的 70~227，
        /// 它参与 CalcHpDamage 的 n = attack + RandInt(5)，所以是"每一次伤害的底数"。
        /// </summary>
        public virtual int ModifyAttack(Person self, Person opponent, int attack) { return attack; }

        /// <summary>
        /// 体力伤害修正。damage 是 CalcHpDamage 按 性格/愤怒/话题/牌型等级/攻击力 算完的结果。
        /// </summary>
        public virtual int ModifyHpDamage(Person self, Person opponent, int card, int damage) { return damage; }

        #endregion

        #region 会心

        /// <summary>
        /// 会心倾向修正（AI 在"追击 / 留情"之间选）。
        ///
        /// critical 是基础结果；related = true 表示它来自"怨恨 / 敬爱"的关系判定（不是随机来的）。
        /// 默认原样返回；数据驱动实现可以强制指定，也可以只把随机那一支往"追击"偏。
        /// 玩家手动选的会心不走这里（那是玩家自己的决定）。
        /// </summary>
        public virtual int ModifyCritical(Person self, Person opponent, int critical, bool related) { return critical; }

        #endregion

        #region 预留

        /// <summary>【预留】愤怒(Stress)获得量修正。</summary>
        public virtual int ModifyStressGain(Person self, int value) { return value; }

        /// <summary>【预留】兴奋(愤怒)状态持续回合修正。</summary>
        public virtual int ModifyAngerTimer(Person self, int timer) { return timer; }

        /// <summary>【预留】开场体力(HP)修正。</summary>
        public virtual int ModifyMaxHp(Person self, int hp) { return hp; }

        /// <summary>【预留】最大手牌数修正。</summary>
        public virtual int ModifyMaxCardCount(Person self, int count) { return count; }

        /// <summary>【预留】AI 行为倾向系数修正。</summary>
        public virtual int ModifyAICoef(Person self, int coef) { return coef; }

        #endregion
    }
}
