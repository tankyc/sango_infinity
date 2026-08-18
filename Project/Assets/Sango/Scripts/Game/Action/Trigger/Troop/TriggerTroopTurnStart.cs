using Sango.Core.Tools;

namespace Sango.Core
{
    public class TriggerTroopTurnStart : TroopTrigger
    {
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            if (Troop != null)
                Troop.Event.OnTurnStart += OnTurnStart;
        }

        public override void Clear()
        {
            if (Troop != null)
                Troop.Event.OnTurnStart -= OnTurnStart;
        }

        public void OnTurnStart(Troop troop, Scenario scenario)
        {
            triggerCall?.Invoke(this, troop, scenario);
        }
    }
}
