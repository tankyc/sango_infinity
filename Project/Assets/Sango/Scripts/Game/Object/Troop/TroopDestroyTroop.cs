using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public class TroopDestroyTroop : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.DestroyTroop; } }

        public override bool IsMissionComplete
        {
            get
            {
                return (TargetTroop == null || !TargetTroop.IsEnemy(Troop));
            }
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetTroop == null || TargetTroop.Id != troop.missionTarget) scenario.troopsSet.Get(Troop.missionTarget);



            return true;
        }
    }
}
