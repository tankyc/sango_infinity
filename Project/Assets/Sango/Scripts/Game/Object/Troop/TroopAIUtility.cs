using Sango.Tools;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public static class TroopAIUtility
    {
        public delegate int SkillAttackPriorityCalculateMethod(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell);
        public delegate int SkillDefencePriorityCalculateMethod(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell);

        static List<Cell> spellRangeCells = new List<Cell>(256);
        static List<Cell> attackCells = new List<Cell>(256);
        static List<PriorityActionData> higherList = new List<PriorityActionData>(256);
        static WeightList<PriorityActionData> wightList = new WeightList<PriorityActionData>();
        static List<PriorityActionData> checkList = new List<PriorityActionData>();
        static List<Cell> tempMoveRange = new List<Cell>(256);
        static List<SangoObject> tempTargets = new List<SangoObject>(64);

        public class PriorityActionData
        {
            public int prioriry;
            public Skill skill;
            public Cell movetoCell;
            public Cell spellCell;
            public Cell[] atkCells;
            public SangoObject[] targets;

        }

        public static bool TargetEquals(List<SangoObject> objects, SangoObject[] targets)
        {
            if (objects.Count != targets.Length) return false;
            for (int i = 0; i < objects.Count; i++)
            {
                SangoObject sangoObject = objects[i];
                bool find = false;
                for (int j = 0; j < targets.Length; j++)
                {
                    if (sangoObject == targets[j])
                    {
                        find = true;
                        break;
                    }
                }
                if (!find)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取技能的收益权重行动
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static PriorityActionData PriorityAction(Troop troop, Scenario scenario, SkillAttackPriorityCalculateMethod prioritySkillAtkMethod = null, SkillDefencePriorityCalculateMethod prioritySkillDefMethod = null)
        {
            List<SkillInstance> skill_list = troop.skills;
            troop.MoveRange.Clear();
            prioritySkillAtkMethod = prioritySkillAtkMethod ?? SkillStatusPriority;
            prioritySkillDefMethod = prioritySkillDefMethod ?? SkillDefencePriority;
            bool needUpdateMoverange = true;
            wightList.Clear();
            checkList.Clear();
            for (int i = 0, count = skill_list.Count; i < count; i++)
            {
                Skill skill = skill_list[i].Skill;
                if (skill.CanBeSpell(troop))
                {
                    if (needUpdateMoverange)
                    {
                        scenario.Map.GetMoveRange(troop, troop.MoveRange);
                        //float time = UnityEngine.Time.realtimeSinceStartup;
                        //for (int i1 = 0; i1 < 100; i1++)
                        //{
                        //    troop.MoveRange.Clear();
                        //    scenario.Map.GetMoveRange(troop, troop.MoveRange);
                        //}

                        //UnityEngine.Debug.LogError("1-->" + (UnityEngine.Time.realtimeSinceStartup - time));
                        //time = UnityEngine.Time.realtimeSinceStartup;

                        //for (int i1 = 0; i1 < 100; i1++)
                        //{
                        //    troop.MoveRange.Clear();
                        //    scenario.Map.GetMoveRange2(troop, troop.MoveRange);
                        //}

                        //UnityEngine.Debug.LogError("2-->" + (UnityEngine.Time.realtimeSinceStartup - time));


                        needUpdateMoverange = false;
#if SANGO_DEBUG_AI
                        GameAIDebug.Instance.ShowMoveRange(troop.MoveRange, troop);
#endif
                    }

                    if (troop.MoveRange.Count > 0)
                    {
                        for (int j = 0; j < troop.MoveRange.Count; j++)
                        {
                            Cell cell = troop.MoveRange[j];
                            if ((cell == troop.cell && cell.building == null) || cell.IsEmpty())
                            {
                                spellRangeCells.Clear();
                                skill.GetSpellRange(troop, cell, spellRangeCells);
                                for (int k = 0; k < spellRangeCells.Count; k++)
                                {
                                    Cell spellCell = spellRangeCells[k];
                                    attackCells.Clear();
                                    tempTargets.Clear();
                                    int atk_priority = 0;
                                    skill.GetAttackCells(troop, spellCell, attackCells);
                                    for (int m = 0; m < attackCells.Count; m++)
                                    {
                                        Cell atkCell = attackCells[m];
                                        if (atkCell.troop != null)
                                            tempTargets.Add(atkCell.troop);
                                        if (atkCell.building != null)
                                            tempTargets.Add(atkCell.building);
                                    }

                                    if (tempTargets.Count > 0)
                                    {
                                        PriorityActionData priorityActionData = checkList.Find(x =>
                                        {
                                            return x.skill == skill && TargetEquals(tempTargets, x.targets);
                                        });

                                        if (priorityActionData != null)
                                        {
                                            int dis = troop.cell.Distance(cell);
                                            if (dis < priorityActionData.prioriry)
                                            {
                                                priorityActionData.movetoCell = cell;
                                                priorityActionData.spellCell = spellCell;
                                                priorityActionData.atkCells = attackCells.ToArray();
                                                priorityActionData.targets = tempTargets.ToArray();
                                            }
                                        }
                                        else
                                        {
                                            priorityActionData = new PriorityActionData()
                                            {
                                                prioriry = troop.cell.Distance(cell),
                                                skill = skill,
                                                movetoCell = cell,
                                                spellCell = spellCell,
                                                atkCells = attackCells.ToArray(),
                                                targets = tempTargets.ToArray(),
                                            };
                                            checkList.Add(priorityActionData);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < checkList.Count; i++)
            {
                PriorityActionData priorityActionData = checkList[i];
                int atk_priority = 0;
                for (int m = 0; m < priorityActionData.atkCells.Length; m++)
                {
                    Cell atkCell = priorityActionData.atkCells[m];
                    atk_priority += prioritySkillAtkMethod?.Invoke(troop, priorityActionData.skill, atkCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;
                    //if (target != null && !atkCell.IsEmpty() && (atkCell.building == target || atkCell.troop == target))
                    //    atk_priority += 10000;
                }
                int def_priority = prioritySkillDefMethod?.Invoke(troop, priorityActionData.skill, priorityActionData.spellCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;
                int s_p = atk_priority + def_priority;
                if (s_p > 0)
                {
                    priorityActionData.prioriry = s_p / 100;
                    wightList.Push(priorityActionData, priorityActionData.prioriry);
                }
            }

            if (wightList.Count == 0)
                return null;

            // 现在是给了一个随机优先级行动
            return wightList.RandomGet();
        }

        public static PriorityActionData PriorityAction(Troop troop, List<Cell> enemyCells, Scenario scenario, SkillAttackPriorityCalculateMethod prioritySkillAtkMethod = null, SkillDefencePriorityCalculateMethod prioritySkillDefMethod = null)
        {
            List<SkillInstance> skill_list = troop.skills;
            List<Cell> moveRange = null;
            prioritySkillAtkMethod = prioritySkillAtkMethod ?? SkillAttackPriority;
            prioritySkillDefMethod = prioritySkillDefMethod ?? SkillDefencePriority;
            wightList.Clear();
            for (int i = 0, count = skill_list.Count; i < count; i++)
            {
                Skill skill = skill_list[i].Skill;
                if (skill.CanBeSpell(troop))
                {
                    for (int j = 0; j < enemyCells.Count; j++)
                    {
                        Cell dest = enemyCells[j];
                        int atk_priority = 0;
                        skill.GetAttackCells(troop, dest, attackCells);
                        for (int m = 0; m < attackCells.Count; m++)
                        {
                            Cell atkCell = attackCells[m];
                            atk_priority += prioritySkillAtkMethod?.Invoke(troop, skill, atkCell, dest, dest) ?? 0;
                            //if (target != null && !atkCell.IsEmpty() && (atkCell.building == target || atkCell.troop == target))
                            //    atk_priority += 10000;
                        }
                        int def_priority = prioritySkillDefMethod?.Invoke(troop, skill, dest, dest, dest) ?? 0;
                        int s_p = atk_priority + def_priority;
                        if (s_p > 0)
                        {
                            PriorityActionData priorityActionData = new PriorityActionData()
                            {
                                prioriry = s_p,
                                skill = skill,
                                movetoCell = dest,
                                spellCell = dest,
                                atkCells = attackCells.ToArray()
                            };
                            wightList.Push(priorityActionData, s_p / 100);
                        }
                    }
                }
            }

            if (wightList.Count == 0)
                return null;

            // 现在是给了一个随机优先级行动
            return wightList.RandomGet();
        }


        public static void RangeEnemyCell(Troop troop, int range, List<Cell> cells, Scenario scenario)
        {
            scenario.Map.SpiralAction(troop.cell, range, (cell) =>
            {
                if ((cell.troop != null && cell.troop.IsEnemy(troop)) || (cell.building != null && cell.building.IsEnemy(troop)))
                    cells.Add(cell);
            });
        }

        /// <summary>
        /// 技能攻击评分
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="skill"></param>
        /// <param name="target"></param>
        /// <param name="movetoCell"></param>
        /// <param name="spellCell"></param>
        /// <returns></returns>
        public static int SkillAttackPriority(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            if (target.IsEmpty()) return 0;
            if (target.troop != null && skill.canDamageTroop)
            {
                if (troop.IsEnemy(target.troop))
                {
                    int damage = Troop.CalculateSkillDamage(troop, target.troop, skill);
                    float hitBack = target.troop.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(target, movetoCell));
                    if (hitBack > 0)
                    {
                        int hitBackDmg = (int)System.Math.Ceiling(hitBack * Troop.CalculateSkillDamage(target.troop, troop, null));
                        return damage - hitBackDmg;
                    }
                    else
                        return damage;
                }
                else if (skill.canDamageTeam)
                {
                    int damage = Troop.CalculateSkillDamage(troop, target.troop, skill);
                    return -damage;
                }
            }
            else if (target.building != null && skill.canDamageBuilding)
            {
                //TODO: 对建筑的攻击评分
                if (troop.IsEnemy(target.building))
                {
                    int damage = Troop.CalculateSkillDamage(troop, target.building, skill);
                    //float hitBack = target.building.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(target, movetoCell));
                    //if (hitBack > 0)
                    //{
                    //    int hitBackDmg = (int)math.ceil(hitBack * Troop.CalculateSkillDamage(target.building, troop, null));
                    //    return (damage - hitBackDmg) * 4;
                    //}
                    //else
                    return damage * 4;
                }
                else if (skill.canDamageTeam)
                {
                    int damage = Troop.CalculateSkillDamage(troop, target.building, skill);
                    return -damage * 4;
                }
            }
            return 0;
        }

        //攻击时被反击防守评分
        public static int SkillDefencePriority(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            //TODO: 攻击时被反击防守评分(上面减除了,暂时返回0)

            return 0;
        }

        /// <summary>
        /// 攻防属性评分
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="skill"></param>
        /// <param name="target"></param>
        /// <param name="movetoCell"></param>
        /// <param name="spellCell"></param>
        /// <returns></returns>
        public static int SkillStatusPriority(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            if (target.IsEmpty()) return 0;
            int rangeFactor = skill.isRange ? 150 : 100;
            if (target.troop != null && skill.canDamageTroop)
            {
                if (troop.IsEnemy(target.troop))
                {
                    return (skill.atk + 10) * (troop.Attack - target.troop.Defence + 200) * rangeFactor / 100;
                }
                else if (skill.canDamageTeam)
                {
                    return -(skill.atk + 10) * (troop.Attack - target.troop.Defence + 200) * rangeFactor / 100;
                }
            }
            else if (target.building != null && skill.canDamageBuilding)
            {
                //TODO: 对建筑的攻击评分
                if (troop.IsEnemy(target.building))
                {
                    return skill.atkDurability * 4 * rangeFactor;
                }
                else if (skill.canDamageTeam)
                {
                    return skill.atkDurability * -4 * rangeFactor;
                }
            }
            return 0;
        }

    }
}
