using static Sango.Game.City;
using static Sango.Game.TroopAIUtility;

namespace Sango.Game
{
    public class TroopProtectCity : TroopMissionBehaviour
    {
        public override MissionType MissionType { get { return MissionType.ProtectCity; } }
        PriorityActionData priorityActionData;
        Troop nearestEnemy;
        bool isNoEnemyAlive = false;
        public override bool IsMissionComplete
        {
            get
            {
                return isNoEnemyAlive;
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetCity == null || TargetCity.Id != troop.missionTarget) TargetCity = scenario.citySet.Get(Troop.missionTarget);

            EnemyInfo enemyInfo;
            isNoEnemyAlive = !TargetCity.CheckEnemiesIfAlive(out enemyInfo);

            if (IsMissionComplete)
            {
                if (TargetCity.IsEnemy(troop))
                {
                    // 如果城池失守,不返回,直接死战,避免过长的寻路导致性能问题
                    Troop.missionType = (int)MissionType.OccupyCity;
                    Troop.missionTarget = Troop.BelongCity.Id;
                    Troop.NeedPrepareMission();
                }
                else
                {
                    Troop.missionType = (int)MissionType.ReturnCity;
                    Troop.missionTarget = Troop.BelongCity.Id;
                    Troop.NeedPrepareMission();
                }
            }
            else
            {
                // 获取目标城市周围的敌人
                priorityActionData = TroopAIUtility.PriorityAction(Troop, scenario, SkillAttackPriority);
                if (priorityActionData == null)
                {
                    nearestEnemy = TargetCity.GetNearestEnemy(troop.cell);
                    if (nearestEnemy == null)
                    {
                        Troop.missionType = (int)MissionType.ReturnCity;
                        Troop.missionTarget = Troop.BelongCity.Id;
                        isNoEnemyAlive = true;
                        Troop.NeedPrepareMission();
                    }
                }
            }
        }

        public int SkillAttackPriority(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int socer = TroopAIUtility.SkillStatusPriority(troop, skill, target, movetoCell, spellCell);
            if (socer > 0)
            {
                if (!target.IsEmpty() && (target.troop != null))
                {
                    int distance = Scenario.Cur.Map.Distance(TargetCity.CenterCell, target.troop.cell);
                    distance = UnityEngine.Mathf.Max(0, 5 - distance);
                    socer += distance * GameRandom.Random(500, 1000);
                    if (movetoCell == troop.cell)
                        socer += 100000;
                }

                if (movetoCell == troop.cell && !troop.TroopType.isRange)
                    socer += 50000;
            }
            return socer;
        }


        public override bool DoAI(Troop troop, Scenario scenario)
        {

            // 任务完成后,回到创建城池
            if (IsMissionComplete)
            {
                Troop.NeedPrepareMission();
                return false;
            }

            // 获取目标城市周围的敌人
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
                if (nearestEnemy != null)
                    return troop.TryCloseTo(nearestEnemy.cell);
            }

            return true;
        }
    }
}
