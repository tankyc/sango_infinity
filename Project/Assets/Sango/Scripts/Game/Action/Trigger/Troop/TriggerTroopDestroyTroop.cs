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
            // atk 可为 null：地形伤害（毒泉/栈道，ApplyTerrainMovementDamage 传 null）、
            // 着火、饿死等无攻击者的死亡，此时不可能命中"被战法击破"触发器。
            if (atk == null || atk.ObjectType != SangoObjectType.SkillInstance)
                return;
            skillInstance = (SkillInstance)atk;
            if (!CheckForceTroop(skillInstance.master))
                return;
            DestTroop = troop;
            triggerCall?.Invoke(this);
        }
    }
}
