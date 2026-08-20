using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerTroopOnMoralChange : TroopTrigger
    {
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
                GameEvent.OnTroopChangeMorale += OnTroopChangeMorale;
        }

        public override void Clear()
        {
            if (Troop != null)
                GameEvent.OnTroopChangeMorale -= OnTroopChangeMorale;
        }

        public void OnTroopChangeMorale(Troop troop, int value, OverrideData<int> overrideData)
        {
            if (!CheckForceTroop(troop))
                return;

            valueOverride = overrideData;
            triggerCall?.Invoke(this);
        }
    }
}
