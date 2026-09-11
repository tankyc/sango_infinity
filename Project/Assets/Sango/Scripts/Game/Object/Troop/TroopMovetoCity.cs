using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public class TroopMovetoCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopMovetoCity; } }
        public override bool IsMissionComplete { get { return !TargetCity.IsSameForce(Troop); } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
            priorityActionData = null;

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                if (troop.IsPlayerControl)
                {
                    troop.ClearMission();
                }
                else
                {
                    troop.SetMission(MissionType.TroopReturnCity, troop.mBelongCity.Id);
                }
                troop.NeedPrepareMission();
                return;

            }
            else
            {
                // 检查通路
                Troop.tempCellList.Clear();
                scenario.Map.GetDirectPath(Troop.cell, TargetCity.CenterCell, Troop.tempCellList);
                for (int i = 0; i < Troop.tempCellList.Count; ++i)
                {
                    Cell road = Troop.tempCellList[i];
                    if (road.building != null && !road.building.IsCity() && !road.building.IsSameForce(Troop))
                    {
                        priorityActionData = TroopAIUtility.PriorityAction(Troop, (Cell)null, scenario, SkillStatusPriority);
                        return;
                    }
                }
            }
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return true;
            }
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("2,");
            if (priorityActionData != null)
            {
                if (!priorityActionData.moveFinish && !troop.MoveTo(priorityActionData.movetoCell))
                {
                    if (GameSystemManager.debug)
                        GameSystemManager.debug_StringBuilder.Append("3,");
                    return false;
                }
                if (!priorityActionData.moveFinish)
                    priorityActionData.moveFinish = true;
                if (!troop.SpellSkill(priorityActionData.skill, priorityActionData.spellCell))
                {
                    if (GameSystemManager.debug)
                        GameSystemManager.debug_StringBuilder.Append("4,");
                    return false;
                }
                return true;
            }
            else if (troop.TryMoveToCity(TargetCity))
            {
                // 移动完成，进入城市
                if (troop.cell.building == TargetCity)
                {
                    troop.EnterCity(TargetCity);
                }
                return true;
            }
            if (GameSystemManager.debug)
                GameSystemManager.debug_StringBuilder.Append("10,");
            return false;
        }
    }
}
