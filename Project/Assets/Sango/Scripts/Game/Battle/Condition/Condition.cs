using Sango.Game.Battle.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Condition
{
    public class Condition
    {
        public BattleObject owner { private set; get; }
        public ushort conditionType;
        public BattleEffect[] effects;
        public BattleEffect[] false_effects;

        public virtual void SetOwner(BattleObject o)
        {
            owner = o;
        }

        public virtual bool Check(BattleObject owner, BattlePerson target, params object[] data)
        {
            return false;
        }
        public virtual Condition Clone()
        {
            return null;
        }

        public virtual Condition Clone(ref Condition condition)
        {
            condition.conditionType = conditionType;
            return null;
        }
        public virtual void Active()
        {
        }
        public virtual void Active(Trigger.Trigger trigger)
        {
        }
        public virtual void Clear()
        {
        }


    }
}
