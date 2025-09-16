using Sango.Game.Battle.Core;

namespace Sango.Game.Battle.Trigger
{
    public abstract class Trigger
    {
        public delegate void TriggerCall(Trigger trigger);

        public TriggerCall triggerCall;
        public BattleObject owner { private set; get; }
        public int roundFlag = -int.MaxValue;

        public bool CheckRound()
        {
            int v = 1 << owner.battle.round_count;
            return (roundFlag & v) == v;
        }

        public virtual void SetOwner(BattleObject o)
        {
            owner = o;
        }

        public virtual Trigger Clone()
        {
            return null;
        }

        public virtual void Active(TriggerCall call)
        {
            triggerCall = call;
        }

        public virtual void Clear()
        {
        }
    }
}
