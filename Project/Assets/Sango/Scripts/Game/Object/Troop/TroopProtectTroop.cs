using System;

namespace Sango.Game
{
    public class TroopProtectTroop : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.ProtectTroop; } }
        public override bool IsMissionComplete => throw new NotImplementedException();

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            return true;
        }
    }
}
