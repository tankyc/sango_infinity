using Sango.Tools;
using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public class TroopDestroyTroop : TroopMissionBehaviour
    {
        static WeightList<PriorityActionData> wightList = new WeightList<PriorityActionData>();

        public override MissionType MissionType { get { return MissionType.TroopDestroyTroop; } }

        public override bool IsMissionComplete
        {
            get
            {
                return (TargetTroop == null || !TargetTroop.IsAlive || !TargetTroop.IsEnemy(Troop));
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetTroop == null || TargetTroop.Id != troop.missionTarget) TargetTroop = scenario.troopsSet.Get(Troop.missionTarget);

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
                // 获取目标城市周围的敌人
               TroopAIUtility.PriorityAction(wightList, Troop, TargetTroop.cell, scenario, SkillAttackPriority);
                priorityActionData = wightList.Find((x) =>
                {
                    for(int i = 0; i < x.targets.Length; i++)
                    {
                        if (x.targets[i] == TargetTroop)
                        {
                            return true;
                        }
                    }

                    return false;
                });

                if (priorityActionData == null)
                    priorityActionData = wightList.RandomGet();
            }
        }

        // 技能攻击评分
        public int SkillAttackPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int socer = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (socer > 0)
            {
                if (!target.IsEmpty() && (target.troop != null))
                {
                    if (target.troop == TargetTroop)
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
                // 向目标前进
                return troop.TryCloseTo(TargetTroop.cell);
            }
        }
    }
}
