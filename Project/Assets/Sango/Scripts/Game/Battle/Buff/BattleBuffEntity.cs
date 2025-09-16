using Sango.Game.Battle.Skill;

namespace Sango.Game.Battle.Buff
{

    public class BattleBuffEntity
    {
        public BattleBuff owner;
        public Trigger.Trigger trigger;
        public Condition.Condition conditions;

        public BattleBuffEntity Clone()
        {
            BattleBuffEntity battleBuffEntity = new BattleBuffEntity();
            if (trigger != null)
                battleBuffEntity.trigger = trigger.Clone();
            if (conditions != null)
                battleBuffEntity.conditions = conditions.Clone();
            return battleBuffEntity;
        }
        public void SetOwner(BattleBuff buff)
        {
            owner = buff;
            if (trigger != null)
                trigger.SetOwner(buff);
            if (conditions != null)
                conditions.SetOwner(buff);
        }

        public void Active()
        {
            if (trigger != null)
            {
                trigger.Active(OnTrigger);
            }
            else
            {
                if (conditions != null)
                    conditions.Active();
            }
        }

        public void OnTrigger(Trigger.Trigger trigger)
        {
            if (conditions != null)
                conditions.Active(trigger);
        }

        public void Clear()
        {
            if (trigger != null)
                trigger.Clear();
            if (conditions != null)
                conditions.Clear();
        }
    }
}