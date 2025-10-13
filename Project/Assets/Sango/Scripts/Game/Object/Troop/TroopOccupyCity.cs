using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Sango.Game.TroopAIUtility;

namespace Sango.Game
{
    public class TroopOccupyCity : TroopMissionBehaviour
    {
        List<Cell> tempCells = new List<Cell>(256);
        PriorityActionData priorityActionData;

        public override MissionType MissionType { get { return MissionType.OccupyCity; } }

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
            if (IsMissionComplete)
            {
                if (TargetCity == null )
                {
                    Troop.missionType = (int)MissionType.ReturnCity;
                    Troop.missionTarget = Troop.BelongCity.Id;
                }
                else if(!TargetCity.IsSameForce(Troop))
                {
                    // 被友军拿取,保护友军城池,直到消灭敌人
                    Troop.missionType = (int)MissionType.ProtectCity;
                    Troop.missionTarget = Troop.BelongCity.Id;
                }
                else
                {
                    Troop.missionType = (int)MissionType.MovetoCity;
                }
                Troop.NeedPrepareMission();
            }
            else
            {
                //List<Cell> road = troop.BelongCity.GetRoadToNeighbor(TargetCity);



                //TroopAIUtility.RangeEnemyCell(troop, 3, tempCells, scenario);
                //if(tempCells.Count > 0)
                //{
                //    // 获取目标城市周围的敌人
                //    priorityActionData = TroopAIUtility.PriorityAction(Troop, tempCells, scenario, SkillAttackPriority);
                //}
                //else
                //{
                //    priorityActionData = null;
                //}

                // 获取目标城市周围的敌人
                priorityActionData = TroopAIUtility.PriorityAction(Troop, scenario, SkillAttackPriority);

            }
        }

        // 技能攻击评分
        public int SkillAttackPriority(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int socer = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (socer > 0)
            {
                if (!target.IsEmpty() && (target.building != null))
                {
                    if (target.building == TargetCity)
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
                if (!troop.MoveTo(priorityActionData.movetoCell))
                    return false;

                if (!troop.SpellSkill(priorityActionData.skill, priorityActionData.spellCell))
                    return false;
                return true;
            }
            else
            {
                // 向目标前进
                return troop.TryCloseTo(TargetCity.CenterCell);
            }
        }
    }
}
