using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 沉默：**不能施放战法与计略**，普攻不受影响。
    ///
    /// 【为什么效果类里没有逻辑】沉默是纯"可用性"状态，不需要订阅回合事件：
    ///   · 状态位由 <see cref="Buff.GetStateFlag"/> 按类名 <c>Silence</c> 自动推导为
    ///     <see cref="TroopStateFlag.Silence"/>，Buff 增删时由 BuffManager 统一重算，
    ///     写入 / 移除天然成对；
    ///   · 拦截点在 <see cref="SkillInstance.CanBeSpell"/>（菜单 + AI 的统一入口）
    ///     与 <see cref="Troop.SpellSkill"/>（任务链执行兜底）。
    /// 保留这个类只是为了让数据里的 <c>"class": "Silence"</c> 能被
    /// <see cref="BuffEffect.Create"/> 解析（否则 BuffInstance 会把它当成未知效果丢弃，
    /// 状态位也就推不出来）。
    /// </summary>
    public class Silence : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
        }

        public override void Action(BuffInstance buffInstance, Troop troop, Cell spellCell, System.Collections.Generic.List<Cell> atkCellList)
        {
            // 无需每回合结算：拦截点在 CanBeSpell / SpellSkill
        }
    }
}
