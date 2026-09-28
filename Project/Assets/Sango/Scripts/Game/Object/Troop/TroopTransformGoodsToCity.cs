using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopTransformGoodsToCity : TroopMissionBehaviour
    {
        internal static List<Cell> tempCellList = new List<Cell>(256);

        public override MissionType MissionType { get { return MissionType.TroopTransformGoodsToCity; } }
        public override bool IsMissionComplete { get { return TargetCity == null || !TargetCity.IsSameForce(Troop); } }
        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
           
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 【说明】原先此处有 `if (troop.IsPlayerControl) ClearMission()` 的补丁。
                // 玩家第一军团现已改走 PlayerTroopTransformGoodsToCity，
                // 由该类型负责"交回玩家"，故此处只保留势力 AI 的处理。
                Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                Troop.NeedPrepareMission();
                return;
            }

            tempCellList.Clear();
            scenario.Map.GetDirectPath(Troop.cell, TargetCity.CenterCell, tempCellList);
            for (int i = 0; i < tempCellList.Count; ++i)
            {
                Cell road = tempCellList[i];
                if (road.building != null && !road.building.IsCity() && !road.building.IsSameForce(Troop))
                {
                    priorityActionData = TroopAIUtility.PriorityAction(Troop, road, scenario);
                    break;
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

            if (priorityActionData != null)
            {
                if (!priorityActionData.moveFinish && !troop.MoveTo(priorityActionData.movetoCell))
                    return false;
                if (!priorityActionData.moveFinish)
                    priorityActionData.moveFinish = true;
                if (!troop.SpellSkill(priorityActionData.skill, priorityActionData.spellCell))
                    return false;
                return true;
            }
            else
            {
                if (troop.TryMoveToCity(TargetCity))
                {
                    // 移动完成，进入城市
                    if (troop.cell.building == TargetCity)
                    {
                        troop.EnterCity(TargetCity);
                    }
                    return true;
                }
                return false;
            }
        }

    }
}
