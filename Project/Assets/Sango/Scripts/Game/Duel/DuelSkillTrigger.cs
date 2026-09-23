/*
 * 文件名：DuelSkillTrigger.cs
 * 描述：战法释放完成后按概率挑起单挑
 *
 * 规则：
 *   · 只处理**战法**（Skill.kind = 2），普攻 / 计略不参与；
 *   · **战法自身的开关**：SkillInstance.canTriggerDuel = false 时一律不触发。
 *     该值在 SkillInstance.Init 里由战法定义 Skill.canTriggerDuel 复制而来，
 *     所以既能按战法在数据里勾选，也能在运行期被 Action（OnSkillCalculateAttribute）临时禁掉；
 *   · 必须**命中**：CheckSuccess 失败时会把 tempSuccessFactor 置 0，那种情况不触发；
 *   · 概率取自**发起部队的属性** Troop.duelChance（百分比，夹在 0~100）。
 *     该属性在 Troop.CalculateAttribute 里求值：基础值 = 剧本参数 Scenario.Cur.Variables.skillDuelChance，
 *     之后由修改型 Action（TroopChangeDuelChance 等）在 OnTroopCalculateAttribute 上二次修正；
 *   · 发起资格完全交给 DuelChallengeFlow → DuelManager.CanStartDuel（敌对 / 双方都是战斗部队 /
 *     都有主将 / 相邻），所以射程 2 以上的战法命中后不会挑起单挑，友军目标也不会；
 *   · 战法引发的单挑是**强制单挑**：不询问、不掷应战概率，直接进入（仍会播叫阵 / 应战台词与观战询问）。
 */

using UnityEngine;

namespace Sango.Core.Duel
{
    /// <summary>战法命中后按概率挑起单挑</summary>
    public static class DuelSkillTrigger
    {
        /// <summary>战法的 kind 值（1=普攻 2=战法 3=计略）</summary>
        public const int SkillKindTactic = 2;

        private static bool s_installed = false;

        /// <summary>同一次施法只掷一次骰子（时间轴战法可能多次回调 Action）</summary>
        private static SkillInstance s_lastRolled;

        /// <summary>把触发挂到技能释放完成事件上（由 DuelIntegration.Install 调用一次）</summary>
        public static void Install()
        {
            if (s_installed) return;
            s_installed = true;
            GameEvent.OnSkillActionEnd += OnSkillActionEnd;
        }

        /// <summary>
        /// 战法释放完成。此时效果已经结算完（命中与否、伤害都已落定），
        /// 正好适合用"这次打中了没"作为触发前提。
        /// </summary>
        private static void OnSkillActionEnd(SkillInstance skill, Cell spellCell, Troop targetTroop, BuildingBase targetBuilding)
        {
            if (skill == null || skill.master == null || targetTroop == null) return;
            if (skill.skill == null || skill.skill.kind != SkillKindTactic) return;
            if (!skill.canTriggerDuel) return;                      // 本战法声明不挑起单挑（定义或运行期改写）
            if (ReferenceEquals(s_lastRolled, skill)) return;       // 同一次施法只判定一次
            s_lastRolled = skill;

            if (!Roll(skill)) return;

            // 资格 / 观战 / 表现层统一由发起流程处理，这里不重复判定；
            // forceAccept = true → 战法引发的单挑是强制单挑，不掷应战概率
            DuelChallengeFlow.Request(skill.master, targetTroop, DuelChallengeFlow.Source.Skill, true);
        }

        /// <summary>掷一次骰子（供测试或其它触发方复用）</summary>
        public static bool Roll(SkillInstance skill)
        {
            return DuelRandom.Chance(CalcChance(skill));
        }

        /// <summary>
        /// 本次战法触发单挑的概率(百分比)。
        /// 直接取发起部队的属性 Troop.duelChance（基础来自剧本参数，可被 Action 修改）；
        /// 战法实例关掉（canTriggerDuel = false）、或没命中（tempSuccessFactor ≤ 0）恒为 0。
        /// </summary>
        public static int CalcChance(SkillInstance skill)
        {
            if (skill == null || skill.skill == null) return 0;
            if (!skill.canTriggerDuel) return 0;
            if (skill.tempSuccessFactor <= 0) return 0;
            if (skill.master == null) return 0;

            return Mathf.Clamp(skill.master.duelChance, 0, 100);
        }
    }
}
