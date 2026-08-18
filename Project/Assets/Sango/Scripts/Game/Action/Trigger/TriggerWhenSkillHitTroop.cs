using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerWhenSkillHitTroop : SkillTrigger
    {
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call);
            GameEvent.OnSkillDamageTroop += OnSkillDamageTroop;
        }

        public override void Clear()
        {
            GameEvent.OnSkillDamageTroop -= OnSkillDamageTroop;
        }

        public void OnSkillDamageTroop(SkillInstance skill, Troop target, OverrideData<int> damage)
        {
            atk_cell = target.cell;
            this.skill = skill;
            this.targetTroop = atk_cell.troop;
            this.targetBuilding = atk_cell.building;
            damageOverride = damage;
            triggerCall?.Invoke(this);
        }

    }
}
