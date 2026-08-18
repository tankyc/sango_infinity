using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerWhenSkillAfterHitTroop : SkillTrigger
    {
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call);
            GameEvent.OnSkillDamageTroopAfter += OnSkillDamageTroopAfter;
        }

        public override void Clear()
        {
            GameEvent.OnSkillDamageTroopAfter -= OnSkillDamageTroopAfter;
        }

        public void OnSkillDamageTroopAfter(SkillInstance skill, Troop target, OverrideData<int> damage)
        {
            atk_cell = target.cell;
            this.skill = skill;
            this.targetTroop = target;
            this.targetBuilding = atk_cell.building;
            damageOverride = damage;
            triggerCall?.Invoke(this);
        }

    }
}
