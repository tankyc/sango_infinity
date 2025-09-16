using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public class TroopProtectBuilding : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.ProtectBuilding; } }
        public override bool IsMissionComplete => throw new NotImplementedException();

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            return true;
        }
    }
}
