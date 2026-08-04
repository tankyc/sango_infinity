using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopStay : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopStay; } }

        public override bool IsMissionComplete
        {
            get
            {
                return true;
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
           
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            return true;
        }
    }
}
