using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerTroopTurnStart : TroopTrigger
    {
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
        }

        public override void Clear()
        {
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
        }

        public void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (!CheckForceTroop(troop))
                return;
            triggerCall?.Invoke(this);
        }
    }
}
