using System;
using static Sango.Game.City;
using static Sango.Game.TroopAIUtility;

namespace Sango.Game
{
    public class TrooprMovetoCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.MovetoCity; } }
        public override bool IsMissionComplete { get { return !TargetCity.IsSameForce(Troop); } }
        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
           
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                Troop.missionType = (int)MissionType.ReturnCity;
                Troop.missionTarget = Troop.BelongCity.Id;
                Troop.NeedPrepareMission();
            }
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return false;
            }

            if (troop.TryMoveToCity(TargetCity))
            {
                // 移动完成，进入城市
                if(troop.cell.building == TargetCity)
                {
                    troop.EnterCity(TargetCity);
                }
                return true;
            }

            return false;
        }
    }
}
