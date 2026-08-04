using Sango.Tools;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public static class TroopAIUtility
    {
        public delegate int SkillAttackPriorityCalculateMethod(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell);
        public delegate int SkillDefencePriorityCalculateMethod(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell);

        static List<Cell> spellRangeCells = new List<Cell>(256);
        static List<Cell> attackCells = new List<Cell>(256);
        static List<PriorityActionData> higherList = new List<PriorityActionData>(256);
        static WeightList<PriorityActionData> wightList = new WeightList<PriorityActionData>();
        static List<PriorityActionData> checkList = new List<PriorityActionData>();
        static List<SangoObject> tempTargets = new List<SangoObject>(64);

        public class PriorityActionData
        {
            public int prioriry;
            public SkillInstance skill;
            public Cell movetoCell;
            public Cell spellCell;
            public Cell[] atkCells;
            public SangoObject[] targets;
            public bool moveFinish = false;
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
        static List<SkillInstance> skill_list_temp = new List<SkillInstance>();
        /// <summary>
        /// 获取技能的收益权重行动
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static PriorityActionData PriorityAction(Troop troop, Cell targetCell, Scenario scenario, SkillAttackPriorityCalculateMethod prioritySkillAtkMethod = null, SkillDefencePriorityCalculateMethod prioritySkillDefMethod = null)
        {
            PriorityAction(wightList, troop, targetCell, scenario, prioritySkillAtkMethod, prioritySkillDefMethod);

            if (wightList.Count == 0)
                return null;

            // 现在是给了一个随机优先级行动
            return wightList.RandomGet();
        }

        /// <summary>
        /// 获取技能的收益权重行动
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static void PriorityAction(WeightList<PriorityActionData> wightList, Troop troop, Cell targetCell, Scenario scenario, SkillAttackPriorityCalculateMethod prioritySkillAtkMethod = null, SkillDefencePriorityCalculateMethod prioritySkillDefMethod = null)
        {
            skill_list_temp.Clear();
            skill_list_temp.AddRange(troop.skills);
            skill_list_temp.AddRange(troop.StrategySkills);
            List<SkillInstance> skill_list = skill_list_temp;
            troop.MoveRange.Clear();
            prioritySkillAtkMethod = prioritySkillAtkMethod ?? SkillStatusPriority;
            prioritySkillDefMethod = prioritySkillDefMethod ?? SkillDefencePriority;
            wightList.Clear();
            checkList.Clear();
            for (int i = 0, count = skill_list.Count; i < count; i++)
            {
                SkillInstance skill = skill_list[i];
                if (skill.CanBeSpell(troop))
                {
                    if (troop.MoveRange.Count == 0)
                    {
                        scenario.Map.GetMoveRange(troop, troop.MoveRange);
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
                                    if (!skill.CanSpellToHere(troop, spellCell))
                                        continue;

                                    attackCells.Clear();
                                    tempTargets.Clear();
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
                                            Cell checkCell = targetCell;
                                            if (checkCell == null)
                                                checkCell = troop.cell;

                                            int dis = checkCell.Distance(cell);
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
                                            Cell checkCell = targetCell;
                                            if (checkCell == null)
                                                checkCell = troop.cell;

                                            priorityActionData = new PriorityActionData()
                                            {
                                                prioriry = checkCell.Distance(cell),
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
                int targetCount = 0;
                for (int m = 0; m < priorityActionData.atkCells.Length; m++)
                {
                    Cell atkCell = priorityActionData.atkCells[m];
                    int p = prioritySkillAtkMethod?.Invoke(troop, priorityActionData.skill, atkCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;
                    if (p > 0)
                        targetCount++;
                    atk_priority += p;
                    //if (target != null && !atkCell.IsEmpty() && (atkCell.building == target || atkCell.troop == target))
                    //    atk_priority += 10000;
                }

                if (!priorityActionData.skill.IsSingleSkill() && targetCount < 2)
                    atk_priority /= 2;

                int def_priority = prioritySkillDefMethod?.Invoke(troop, priorityActionData.skill, priorityActionData.spellCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;
                int s_p = atk_priority + def_priority;
                if (s_p > 0)
                {
                    priorityActionData.prioriry = s_p / 100;
                    wightList.Push(priorityActionData, priorityActionData.prioriry);
                }
            }
        }


        /// <summary>
        /// 评估格子的安全性
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="cell"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static int EvaluateCellSafety(Troop troop, Cell cell, Scenario scenario)
        {
            int safetyScore = 0;

            // 检查周围敌人
            scenario.Map.SpiralAction(cell, 2, (checkCell) =>
            {
                if (checkCell.troop != null && troop.IsEnemy(checkCell.troop))
                {
                    int distance = cell.Distance(checkCell);
                    int threat = (100 - distance * 30) * (checkCell.troop.troops / 1000);
                    safetyScore -= threat;
                }
            });

            // 检查地形加成
            if (troop.TroopType.terrainDefenceBonus != null && cell.TerrainType != null)
            {
                int terrainId = cell.TerrainType.Id;
                if (terrainId < troop.TroopType.terrainDefenceBonus.Length)
                {
                    safetyScore += (int)(troop.TroopType.terrainDefenceBonus[terrainId] * 5);
                }
            }

            return safetyScore;
        }

        /// <summary>
        /// 评估城市防御强度
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="city"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static int EvaluateCityDefenceStrength(Troop troop, City city, Scenario scenario)
        {
            int defenceStrength = 0;

            // 城市耐久度（降低权重）
            defenceStrength += city.durability * 2;

            // 城市驻军（保持合理权重）
            defenceStrength += city.troops;

            // 城市建筑防御加成（降低权重）
            foreach (Building building in city.allBuildings)
            {
                if (!building.IsIntorBuilding())
                    defenceStrength += 100;
            }

            // 周围敌方部队（保持合理权重）
            List<Cell> enemyCells = new List<Cell>();
            RangeEnemyCell(troop, 3, enemyCells, scenario);
            foreach (Cell cell in enemyCells)
            {
                if (cell.troop != null)
                    defenceStrength += cell.troop.troops;
            }

            return defenceStrength;
        }

        /// <summary>
        /// 安全地移动到目标
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="targetCell"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static bool MoveToTargetSafely(Troop troop, Cell targetCell, Scenario scenario)
        {
            // 确保tempMoveRange已填充
            if (troop.MoveRange.Count == 0)
                scenario.Map.GetMoveRange(troop, troop.MoveRange);

            // 评估移动范围内的格子安全性
            Cell bestNextCell = null;
            int bestSafetyScore = int.MinValue;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < troop.MoveRange.Count; i++)
            {
                Cell moveCell = troop.MoveRange[i];
                // 跳过当前格子和不可进入的格子
                if (moveCell == troop.cell || !moveCell.IsEmpty())
                    continue;

                // 计算到目标的距离
                int distance = moveCell.Distance(targetCell);

                // 评估安全性
                int safetyScore = EvaluateCellSafety(troop, moveCell, scenario);

                // 优先选择距离目标近且安全的格子
                if (safetyScore > bestSafetyScore || (safetyScore == bestSafetyScore && distance < bestDistance))
                {
                    bestSafetyScore = safetyScore;
                    bestDistance = distance;
                    bestNextCell = moveCell;
                }
            }

            if (bestNextCell != null)
            {
                return troop.MoveTo(bestNextCell);
            }

            // 如果找不到安全路径，使用默认移动
            return troop.TryCloseTo(targetCell);
        }

        //public static PriorityActionData PriorityAction(Troop troop, List<Cell> enemyCells, Scenario scenario, SkillAttackPriorityCalculateMethod prioritySkillAtkMethod = null, SkillDefencePriorityCalculateMethod prioritySkillDefMethod = null)
        //{
        //    List<SkillInstance> skill_list = troop.skills;
        //    List<Cell> moveRange = null;
        //    prioritySkillAtkMethod = prioritySkillAtkMethod ?? SkillAttackPriority;
        //    prioritySkillDefMethod = prioritySkillDefMethod ?? SkillDefencePriority;
        //    wightList.Clear();
        //    for (int i = 0, count = skill_list.Count; i < count; i++)
        //    {
        //        Skill skill = skill_list[i].Skill;
        //        if (skill.CanBeSpell(troop))
        //        {
        //            for (int j = 0; j < enemyCells.Count; j++)
        //            {
        //                Cell dest = enemyCells[j];
        //                int atk_priority = 0;
        //                skill.GetAttackCells(troop, dest, attackCells);
        //                for (int m = 0; m < attackCells.Count; m++)
        //                {
        //                    Cell atkCell = attackCells[m];
        //                    atk_priority += prioritySkillAtkMethod?.Invoke(troop, skill, atkCell, dest, dest) ?? 0;
        //                    //if (target != null && !atkCell.IsEmpty() && (atkCell.building == target || atkCell.troop == target))
        //                    //    atk_priority += 10000;
        //                }
        //                int def_priority = prioritySkillDefMethod?.Invoke(troop, skill, dest, dest, dest) ?? 0;
        //                int s_p = atk_priority + def_priority;
        //                if (s_p > 0)
        //                {
        //                    PriorityActionData priorityActionData = new PriorityActionData()
        //                    {
        //                        prioriry = s_p,
        //                        skill = skill,
        //                        movetoCell = dest,
        //                        spellCell = dest,
        //                        atkCells = attackCells.ToArray()
        //                    };
        //                    wightList.Push(priorityActionData, s_p / 100);
        //                }
        //            }
        //        }
        //    }

        //    if (wightList.Count == 0)
        //        return null;

        //    // 现在是给了一个随机优先级行动
        //    return wightList.RandomGet();
        //}


        public static void RangeEnemyCell(Troop troop, int range, List<Cell> cells, Scenario scenario)
        {
            scenario.Map.SpiralAction(troop.cell, range, (cell) =>
            {
                if ((cell.troop != null && cell.troop.IsEnemy(troop)) || (cell.building != null && cell.building.IsEnemy(troop)))
                    cells.Add(cell);
            });
        }

        ///// <summary>
        ///// 技能攻击评分
        ///// </summary>
        ///// <param name="troop"></param>
        ///// <param name="skill"></param>
        ///// <param name="target"></param>
        ///// <param name="movetoCell"></param>
        ///// <param name="spellCell"></param>
        ///// <returns></returns>
        //public static int SkillAttackPriority(Troop troop, Skill skill, Cell target, Cell movetoCell, Cell spellCell)
        //{
        //    if (target.IsEmpty()) return 0;
        //    if (target.troop != null && skill.canDamageTroop)
        //    {
        //        if (troop.IsEnemy(target.troop))
        //        {
        //            int damage = Troop.CalculateSkillDamage(troop, target.troop, skill);
        //            float hitBack = target.troop.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(target, movetoCell));
        //            if (hitBack > 0)
        //            {
        //                int hitBackDmg = (int)System.Math.Ceiling(hitBack * Troop.CalculateSkillDamage(target.troop, troop, null));
        //                return damage - hitBackDmg;
        //            }
        //            else
        //                return damage;
        //        }
        //        else if (skill.canDamageTeam)
        //        {
        //            int damage = Troop.CalculateSkillDamage(troop, target.troop, skill);
        //            return -damage;
        //        }
        //    }
        //    else if (target.building != null && skill.canDamageBuilding)
        //    {
        //        //TODO: 对建筑的攻击评分
        //        if (troop.IsEnemy(target.building))
        //        {
        //            int damage = Troop.CalculateSkillDamage(troop, target.building, skill);
        //            //float hitBack = target.building.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(target, movetoCell));
        //            //if (hitBack > 0)
        //            //{
        //            //    int hitBackDmg = (int)math.ceil(hitBack * Troop.CalculateSkillDamage(target.building, troop, null));
        //            //    return (damage - hitBackDmg) * 4;
        //            //}
        //            //else
        //            return damage * 4;
        //        }
        //        else if (skill.canDamageTeam)
        //        {
        //            int damage = Troop.CalculateSkillDamage(troop, target.building, skill);
        //            return -damage * 4;
        //        }
        //    }
        //    return 0;
        //}

        //攻击时被反击防守评分
        public static int SkillDefencePriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
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
        public static int SkillStatusPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            if (target.IsEmpty()) return 0;
            if (target.troop != null && skill.canDamageTroop)
            {
                if (troop.IsEnemy(target.troop))
                {
                    int strategy_factor = 0;
                    if (skill.IsStrategy())
                    {
                        if (skill.onlySpellToTeam)
                        {
                            if (!target.troop.IsSameForce(troop))
                                return 0;
                            else if (target.troop.HasControlBuff())
                                strategy_factor = 30;
                        }
                        else
                        {
                            // 不对着火的地方释放火计
                            if (skill.Id == 22 && target.fire != null)
                                return 0;
                            else if (skill.Id == 23 && target.fire == null)
                                return 0;
                            else if (skill.skillSuccessMethod == null)
                                return 0;
                            else if (target.troop.HasControlBuff())
                                return 0;

                            int succ = skill.skillSuccessMethod.Calculate(skill, troop, target);
                            if (succ < 75)
                            {
                                return 0;
                            }
                        }
                    }
                    else
                    {
                        if (target.troop.HasControlBuff())
                            strategy_factor += 30;
                    }

                    return (skill.atk + strategy_factor + (skill.IsRange() ? 15 : 0) + (skill.IsSingleSkill() ? 5 : 0) + (skill.HasEffect() ? 5 : 0)) * 150 / Math.Max(10, skill.costEnergy) * (troop.Attack - target.troop.Defence + 200);
                }
                else if (skill.canDamageTeam && spellCell != target)
                {
                    return -(skill.atk + (skill.IsRange() ? 15 : 0) + (skill.IsSingleSkill() ? 5 : 0) + (skill.HasEffect() ? 5 : 0)) * 150 / Math.Max(10, skill.costEnergy) * (troop.Attack - target.troop.Defence + 200);
                }
            }
            else if (target.building != null && skill.canDamageBuilding)
            {
                int rangeFactor = skill.IsRange() ? 150 : 100;
                //TODO: 对建筑的攻击评分
                if (troop.IsEnemy(target.building))
                {
                    if (skill.IsStrategy())
                    {
                        if (skill.onlySpellToTeam)
                        {
                            return 0;
                        }
                        else
                        {
                            int succ = skill.skillSuccessMethod.Calculate(skill, troop, target);
                            if (succ > 75)
                            {
                                rangeFactor += 2 * (succ - 75);
                            }
                        }
                    }
                    return (skill.atkDurability + (skill.IsSingleSkill() ? 5 : 0) + (skill.HasEffect() ? 5 : 0)) * 4 * rangeFactor * (skill.IsSingleSkill() ? 15 : 10);
                }
                else if (skill.canDamageTeam && spellCell != target)
                {
                    return (skill.atkDurability + (skill.IsSingleSkill() ? 5 : 0) + (skill.HasEffect() ? 5 : 0)) * -8 * rangeFactor * (skill.IsSingleSkill() ? 15 : 10);
                }
            }
            return 0;
        }
    }
}
