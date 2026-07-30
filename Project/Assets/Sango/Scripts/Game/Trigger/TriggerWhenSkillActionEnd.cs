namespace Sango.Core
{
    public class TriggerWhenSkillActionEnd : SkillTrigger
    {
        public override void Init(TriggerCall call)
        {
            base.Init(call);
            GameEvent.OnSkillActionEnd += OnSkillRenderEnd;
        }

        public override void Clear()
        {
            GameEvent.OnSkillActionEnd -= OnSkillRenderEnd;
        }

        public void OnSkillRenderEnd(SkillInstance skill, Cell spellCell, Troop targetTroop, BuildingBase targetBuilding)
        {
            atk_cell = spellCell;
            this.skill = skill;
            this.targetTroop = targetTroop;
            this.targetBuilding = targetBuilding;
            triggerCall?.Invoke(this);
        }

    }
}
