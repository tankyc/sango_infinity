using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public class TroopDestroyBuilding : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.DestroyBuilding; } }
        public override bool IsMissionComplete => throw new NotImplementedException();

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            return true;
        }
    }
}
