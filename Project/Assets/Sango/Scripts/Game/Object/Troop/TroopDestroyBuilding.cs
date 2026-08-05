using System;

namespace Sango.Core
{
    public class TroopDestroyBuilding : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopDestroyBuilding; } }
        public override bool IsMissionComplete
        {
            get
            {
                return (TargetBuilding == null || !TargetBuilding.IsAlive || TargetBuilding.durability <= 0 || TargetBuilding.IsSameForce(Troop));
            }
        }


        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetBuilding == null || TargetBuilding.Id != troop.missionTarget) TargetBuilding = scenario.buildingSet.Get(Troop.missionTarget);

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
            else
            {
                // 获取目标城市周围的敌人
                priorityActionData = TroopAIUtility.PriorityAction(Troop, TargetBuilding.CenterCell, scenario, SkillAttackPriority);
            }
        }

        // 技能攻击评分
        public int SkillAttackPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int socer = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (socer > 0)
            {
                if (!target.IsEmpty() && (target.building != null))
                {
                    if (target.building == TargetBuilding)
                    {
                        socer += 50000;
                        if (movetoCell == troop.cell)
                            socer += 100000;
                    }
                    else
                    {
                        socer = 5;
                    }
                }
                else
                {
                    if (movetoCell == troop.cell && !troop.TroopType.isRange)
                        socer += 50000;
                }
            }
            return socer;
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return false;
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
                // 向目标前进
                return troop.TryCloseTo(TargetBuilding.CenterCell);
            }
        }
    }
}
