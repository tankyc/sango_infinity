using Sango.Core;
using UnityEngine;


namespace Sango.Render
{
    public class TroopSpellSkillFailEvent : RenderEventBase
    {
        public Troop troop;
        public SkillInstance skill;
        public Cell spellCell;
        private bool isAction = false;
        private SkillInstance replaceSkill;
        private float time = 0;

        public void Init(Troop troop, SkillInstance skill, Cell spellCell)
        {
            this.troop = troop;
            this.skill = skill;
            this.spellCell = spellCell;
            this.isAction = false;
            this.replaceSkill = null;
            this.time = 0;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            isAction = false;
            time = 0;
            if (IsVisible())
            {
                GameMedia.Instance.PlaySfx(74);
                GameMedia.Instance.PlaySfx(82);
                troop.Render.SetSmokeShow(true);
            }

            if (!skill.IsStrategy())
            {
                replaceSkill = skill.IsRange() ? troop.NormalRangeSkill : troop.NormalSkill;
            }

            if (!skill.IsNormal())
                troop.Render.ShowSkill(skill, true, false);
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
                IsDone = true;
                return IsDone;
            }

            if (replaceSkill == null)
            {
                IsDone = true;
                troop?.Render?.SetAniShow(0);
                return IsDone;
            }

            IsDone = replaceSkill.UpdateRender(spellCell, scenario, time, Action);
            time += deltaTime;
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;
            if (replaceSkill != null)
                replaceSkill.Action(spellCell, 100);
            isAction = true;
        }

    }
}
