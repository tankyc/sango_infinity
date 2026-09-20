using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public class TroopReturnCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopReturnCity; } }
        public override bool IsMissionComplete
        {
            get
            {
                return TargetCity != Troop.mBelongCity || !TargetCity.IsSameForce(Troop);
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
            priorityActionData = null;

            // 这种情况发生在玩家把部队放在外面没有任何任务,然后将此座城池设置成军团了
            if (TargetCity == null)
            {
                TargetCity = troop.mBelongCity;
                troop.SetMission(MissionType.TroopReturnCity, TargetCity.Id);
            }

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                if (troop.IsPlayerControl)
                {
                    troop.ClearMission();
                }
                else
                {
                    if (TargetCity.IsEnemy(troop))
                    {
                        Troop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
                    }
                    else if (troop.mBelongCity != null)
                    {
                        // 【修复】目标既不是敌方也不是归属城（例如被改成了其它友城）：
                        // 纠正为"返回归属城"，而不是切到无意义的 TroopStay 并打印错误日志。
                        Troop.SetMission(MissionType.TroopReturnCity, troop.mBelongCity.Id);
                    }
                }
                troop.NeedPrepareMission();
                return;
            }

            // 【修复】部队已经站在目标城池格上（例如刚出城就切到返城任务）：
            // 此时不做任何通路检查。否则返城路径若经过正在被攻击的敌方建筑，
            // 会把任务改写为"攻击该建筑"，导致部队永远停留在城池格上反复尝试，表现为"卡在城池上"。
            // 直接留空 priorityActionData，由 DoAI 走进城分支。
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

            // 【修复】已在目标城池格上：直接进城解散，优先于任何攻击行动。
            // 原先该判断被放在 TryMoveToCity 分支内部，一旦 priorityActionData 非空
            // 就完全绕过进城逻辑，部队会卡在城池格上无法移动。
            if (troop.cell != null && troop.cell.building == TargetCity)
            {
                troop.EnterCity(TargetCity);
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
            else if (troop.TryMoveToCity(TargetCity))
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
