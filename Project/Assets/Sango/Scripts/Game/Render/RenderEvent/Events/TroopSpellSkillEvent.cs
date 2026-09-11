using Sango.Core;
using UnityEngine;

namespace Sango.Render
{
    public class TroopSpellSkillEvent : RenderEventBase
    {
        public Troop troop;
        public Troop targetTroop;
        public BuildingBase targetBuilding;
        public SkillInstance skill;
        public Cell spellCell;
        /// <summary>
        /// 这场施法是不是"援助补刀", 由 Troop.DoAssistAttack 排入队列后标上。
        /// 援助是排队演出的, 轮到它时前面几发补刀可能已经把敌军打灭,
        /// 那时整场取消(不播表现也不出手), 否则就是对着空格子放空炮
        /// </summary>
        public bool isAssistAttack;
        private bool isAction = false;
        private float time = 0;

        public void Init(SkillInstance skill, Cell spellCell)
        {
            this.troop = skill.master;
            this.skill = skill;
            this.spellCell = spellCell;
            this.targetTroop = spellCell.troop;
            this.targetBuilding = spellCell.building;
            this.isAction = false;
            this.isAssistAttack = false;
            this.time = 0;
            IsDone = false;
        }

        /// <summary>
        /// 援助是否已经失去意义: 援助方或敌军已不在场, 或敌军已不在当初那个格子上
        /// </summary>
        bool IsAssistCancelled()
        {
            if (!isAssistAttack) return false;
            if (troop == null || !troop.IsAlive) return true;
            if (targetTroop == null || !targetTroop.IsAlive) return true;
            // 敌军被消灭后该格换了别支部队, 也不能再拿它当援助目标
            return spellCell.troop != targetTroop;
        }

        public override void Enter(Scenario scenario)
        {
            isAction = false;
            time = 0;
            // 已经没意义的援助连呼喝、烟尘都不该演, Update 里会直接把这帧收尾
            if (IsAssistCancelled()) return;
            if (IsVisible())
            {
                if (!skill.IsNormal())
                {
                    GameMedia.Instance.PlaySfx(69);
                    GameMedia.Instance.PlayPersonSay(troop.Leader, skill.GerPersonSay());
                }
                else if (skill.IsStrategy())
                {
                    GameMedia.Instance.PlaySfx(85);
                }
                else
                {
                    if (!skill.IsRange() && spellCell.building != null)
                    {
                        GameMedia.Instance.PlayPersonSay(troop.Leader, GameRandom.Chance(50) ? 2684 : 2698);
                    }
                    else
                    {
                        GameMedia.Instance.PlayPersonSay(troop.Leader, skill.GerPersonSay());
                        GameMedia.Instance.PlaySfx(82);
                    }

                }

                troop.Render.SetSmokeShow(true);
            }
            if (!skill.IsNormal())
                troop.Render.ShowSkill(skill, false, false);
        }

        public override void Exit(Scenario scenario)
        {
            if (troop != null && troop.Render != null && IsVisible())
            {
                troop.Render.SetSmokeShow(false);
            }
        }

        public override bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            // 放在最前面: 目标已消失时不能再走下面的分支, 否则会当场 Action() 把伤害打到空格里
            if (IsAssistCancelled())
            {
                Troop.LogAssist($"[{(troop == null ? "null" : troop.Name)}] 的援助取消: 目标[{(targetTroop == null ? "null" : targetTroop.Name)}]已不在场, 不播动画也不出手");
                IsDone = true;
                return IsDone;
            }
            if (!IsVisible() || Input.GetMouseButtonDown(0))
            {
                Action();
                troop?.Render?.SetAniShow(0);
                IsDone = true;
                return IsDone;
            }
            IsDone = skill.UpdateRender(spellCell, scenario, time, Action);
            time += deltaTime;
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;
            skill.Action(spellCell, 100);
            GameEvent.OnSkillActionEnd?.Invoke(skill, spellCell, targetTroop, targetBuilding);
            isAction = true;
        }

    }
}
