using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerTroopDestroyTroop : TroopTrigger
    {
        SkillInstance skillInstance;
        public override SkillInstance ActionSkill => skillInstance;
        public override SkillInstance TargetSkill => skillInstance;

        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnTroopDestroyed += OnTroopDestroyed;
        }

        public override void Clear()
        {
            GameEvent.OnTroopDestroyed -= OnTroopDestroyed;
        }

        public void OnTroopDestroyed(Troop troop, SangoObject atk, int atkBack,  Scenario scenario)
        {
            if (atk.ObjectType != SangoObjectType.SkillInstance)
                return;
            skillInstance = (SkillInstance)atk;
            if (!CheckForceTroop(skillInstance.master))
                return;
            DestTroop = troop;
            triggerCall?.Invoke(this);
        }
    }
}
