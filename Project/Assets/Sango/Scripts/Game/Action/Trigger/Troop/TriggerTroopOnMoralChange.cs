using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerTroopOnMoralChange : TroopTrigger
    {
      
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            if (Troop != null)
                Troop.Event.OnChangeMorale += OnChangeMorale;
        }

        public override void Clear()
        {
            if (Troop != null)
                Troop.Event.OnChangeMorale -= OnChangeMorale;
        }

        public void OnChangeMorale(Troop troop, int value, OverrideData<int> overrideData)
        {
            damageOverride = overrideData;
            triggerCall?.Invoke(this, troop, value, overrideData);
        }
    }
}
