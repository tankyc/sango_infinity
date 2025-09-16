using System;

namespace Sango.Game
{
    public class TroopBanishTroop : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.BanishTroop; } }
        public override bool IsMissionComplete => throw new NotImplementedException();

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            return true;
        }
    }
}
