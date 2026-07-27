using Sango.Core;
using UnityEngine;

namespace Sango.Render
{
    public class TroopSpellSkillEvent : RenderEventBase
    {
        public Troop troop;
        public SkillInstance skill;
        public Cell spellCell;
        private bool isAction = false;
        private float time = 0;

        public void Init(SkillInstance skill, Cell spellCell)
        {
            this.troop = skill.master;
            this.skill = skill;
            this.spellCell = spellCell;
            this.isAction = false;
            this.time = 0;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            isAction = false;
            time = 0;
            if (IsVisible())
            {
                if (!skill.IsNormal())
                {
                    GameMedia.Instance.PlaySfx(69);
                    GameMedia.Instance.PlayPersonSay(troop.Leader, skill.GerPersonSay());
                }
                else if(skill.IsStrategy())
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
            if (IsVisible())
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
            if (!IsVisible() || Input.GetMouseButtonDown(0))
            {
                Action();
                troop?.Render?.SetAniShow(0);
                GameEvent.OnSkillRenderEnd?.Invoke(skill, spellCell);
                IsDone = true;
                return IsDone;
            }
            IsDone = skill.UpdateRender(spellCell, scenario, time, Action);
            time += deltaTime;
            if (IsDone)
            {
                GameEvent.OnSkillRenderEnd?.Invoke(skill, spellCell);
            }
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;
            skill.Action(spellCell, 100);
            isAction = true;
        }

    }
}
