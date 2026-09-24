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
                    troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                }
                troop.NeedPrepareMission();
                return;

            }

            // 【修复】部队已经站在目标城池格上（例如态势撤退时"最近己方据点"就是本城）：
            // 此时不做通路检查，避免返城路径经过敌方建筑时把任务改写为"攻击该建筑"，
            // 导致部队永远停留在城池格上无法移动，表现为"卡在城池上"。
            if (troop.cell != null && troop.cell.building == TargetCity)
                return;

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

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return true;
            }

            // 【修复】已在目标城池格上：直接进城，优先于任何攻击行动。
            if (troop.cell != null && troop.cell.building == TargetCity)
            {
                troop.EnterCity(TargetCity);
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
