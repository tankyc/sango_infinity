using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public class TroopMovetoCell : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopMovetoCell; } }
        public override bool IsMissionComplete { get { return TargetCell != null && TargetCell == Troop.cell; } }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            TargetCell = scenario.Map.GetCell(troop.missionParams1, troop.missionParams2);
            priorityActionData = null;

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                // 【说明】原先此处有 `if (troop.IsPlayerControl) ClearMission()` 的补丁。
                // 玩家第一军团现已改走 PlayerTroopMovetoCell，
                // 由该类型负责"交回玩家"，故此处只保留势力 AI 的处理。
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
                return;
            }
            else
            {
                // 检查通路
                Troop.tempCellList.Clear();
                scenario.Map.GetDirectPath(Troop.cell, TargetCell, Troop.tempCellList);
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
                // 【说明】同 Prepare：玩家第一军团已改走 PlayerTroopMovetoCell
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
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
            else if (troop.TryMoveToCell(TargetCell))
            {
                return true;
            }

            return false;
        }
    }
}
