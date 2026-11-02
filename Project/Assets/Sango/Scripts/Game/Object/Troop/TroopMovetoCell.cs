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
                    // 【对齐原版 hostile_target（s11_scen_obj_unit.cpp:329-354）+ 寻路代价（s11_sys_unit_path.cpp:138-147）】
                    // 原版对路径上的建筑只按"敌对 → 提高通行代价"处理，中立建筑（陷阱 / 障碍 / 无主设施）
                    // hostile_target = false，部队**不会因此停下攻击**。
                    // 原写法 !IsSameForce 把中立陷阱也算成路障 → 触发一次全量 PriorityAction →
                    // 顺手把陷阱引爆（"部队经过陷阱也会攻击陷阱"）。这里改成只对敌对方建筑触发。
                    if (road.building != null && !road.building.IsCity()
                        && road.building.BelongForce != null && Troop.IsEnemy(road.building))
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
