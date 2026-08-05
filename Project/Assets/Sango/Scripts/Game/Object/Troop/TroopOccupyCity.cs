using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopOccupyCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.TroopOccupyCity; } }

        public override bool IsMissionComplete
        {
            get
            {
                return (TargetCity == null || !TargetCity.IsEnemy(Troop));
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);
            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete || (!troop.IsPlayerControl && troop.IsWithOutFood() == 1 && GameRandom.Chance(30)))
            {
                if (TargetCity == null)
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                }
                else if (!TargetCity.IsSameForce(Troop))
                {
                    // 被友军拿取,保护友军城池,直到消灭敌人
                    Troop.SetMission(MissionType.TroopProtectCity, TargetCity.Id);
                }
                else
                {
                    Troop.SetMission(MissionType.TroopMovetoCity, TargetCity.Id);
                }
                Troop.NeedPrepareMission();
                return;

            }
            else
            {
                // 检查目标城市周围的敌人
                List<Cell> enemyCells = new List<Cell>();
                TroopAIUtility.RangeEnemyCell(troop, 4, enemyCells, scenario);
                
                // 根据情况选择不同的策略
                if (enemyCells.Count > 0)
                {
                    // 有敌人，优先攻击威胁大的敌人
                    priorityActionData = TroopAIUtility.PriorityAction(Troop, TargetCity.CenterCell, scenario, SkillAttackPriority);
                }
                else
                {
                    // 没有敌人，直接向城市前进
                    priorityActionData = null;
                }
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
                    if (target.building == TargetCity)
                    {
                        socer += 500000;
                        if (movetoCell == troop.cell)
                            socer += 1000000;
                    }
                    else
                    {
                        socer = 5;
                    }
                }
                else 
                {
                    // 优先攻击威胁大的敌方部队
                    if (!target.IsEmpty() && target.troop != null && target.troop.troops > troop.troops * 1.5f)
                        socer += 30000;
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
                return troop.TryCloseTo(TargetCity.CenterCell);
                // 向目标前进，优先选择安全路径
                //return TroopAIUtility.MoveToTargetSafely(troop, TargetCity.CenterCell, scenario);
            }
        }
    }
}
