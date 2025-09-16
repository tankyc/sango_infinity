using Sango.Game.Battle.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Trigger
{
    public class TriggerList : Trigger
    {
        public Trigger[] triggers;
        public override void SetOwner(BattleObject o)
        {
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; ++i)
                    triggers[i].SetOwner(o);
            }
        }

        public override Trigger Clone()
        {
            TriggerList triggerList = new TriggerList();
            if(triggers != null)
            {
                triggerList.triggers = new Trigger[triggers.Length];
                for (int i = 0; i < triggers.Length; ++i)
                    triggerList.triggers[i] = triggers[i].Clone();
            }
            return triggerList;
        }

        public override void Active(TriggerCall call)
        {
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; ++i)
                    triggers[i].Active(triggerCall);
            }
        }

        public override void Clear()
        {
            if (triggers != null)
            {
                for (int i = 0; i < triggers.Length; ++i)
                    triggers[i].Clear();
            }
        }
    }
}
