using Sango.Tools;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public static class TroopAIUtility
    {
        public delegate int SkillAttackPriorityCalculateMethod(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell);
        public delegate int SkillDefencePriorityCalculateMethod(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell);

        /// <summary>火计技能Id(目标格已有火焰时不重复释放)</summary>
        const int SKILL_ID_FIRE = 22;
        /// <summary>灭火技能Id(目标格无火焰时不释放)</summary>
        const int SKILL_ID_PUT_OUT_FIRE = 23;

        static List<Cell> spellRangeCells = new List<Cell>(256);
        static List<Cell> attackCells = new List<Cell>(256);
        static List<PriorityActionData> higherList = new List<PriorityActionData>(256);
        static WeightList<PriorityActionData> wightList = new WeightList<PriorityActionData>();
        /// <summary>技能 + 目标集合去重表(键为技能Id与目标无序哈希的组合),替代原先的线性查找去重</summary>
        static Dictionary<long, PriorityActionData> checkMap = new Dictionary<long, PriorityActionData>(256);
        static List<SangoObject> tempTargets = new List<SangoObject>(64);
        /// <summary>城市防御评估用的敌人格缓存,避免每次调用分配内存</summary>
        static List<Cell> enemyCellsCache = new List<Cell>(64);

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
            // 【分级策略】态势档位 × 部队角色 × 领队性格 → 评分倍率与行动择优池
            AIPolicy policy = troop != null
                ? AIPolicy.Combine(troop.GetTierWeights(scenario), troop.GetRoleWeights(), troop.LeaderPersonality)
                : AIPolicy.Default;

            PriorityAction(wightList, troop, targetCell, scenario, prioritySkillAtkMethod, prioritySkillDefMethod, policy);

            if (wightList.Count == 0)
                return null;

            // 【按档位动态择优】态势越明确，候选池越小（趋近直接取最优）；
            // 均势 / 胶着时保留更大候选池，避免 AI 行为僵化。
            return wightList.RandomGetTop(policy.bestN);
        }

        /// <summary>
        /// 获取技能的收益权重行动
        /// </summary>
        /// <param name="wightList">输出用的权重列表</param>
        /// <param name="troop">行动部队</param>
        /// <param name="targetCell">参考目标格</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="prioritySkillAtkMethod">攻击评分委托</param>
        /// <param name="prioritySkillDefMethod">防御（反击）评分委托</param>
        /// <param name="policy">分级策略快照；为 null 时不做档位 / 角色修正</param>
        public static void PriorityAction(WeightList<PriorityActionData> wightList, Troop troop, Cell targetCell, Scenario scenario, SkillAttackPriorityCalculateMethod prioritySkillAtkMethod = null, SkillDefencePriorityCalculateMethod prioritySkillDefMethod = null, AIPolicy? policy = null)
        {
            AIPolicy effective = policy ?? AIPolicy.Default;
            skill_list_temp.Clear();
            skill_list_temp.AddRange(troop.skills);
            skill_list_temp.AddRange(troop.StrategySkills);
            List<SkillInstance> skill_list = skill_list_temp;
            troop.MoveRange.Clear();
            prioritySkillAtkMethod = prioritySkillAtkMethod ?? SkillStatusPriority;
            prioritySkillDefMethod = prioritySkillDefMethod ?? SkillDefencePriority;
            wightList.Clear();
            checkMap.Clear();

            Cell baseCheckCell = targetCell ?? troop.cell;

            for (int i = 0, count = skill_list.Count; i < count; i++)
            {
                SkillInstance skill = skill_list[i];
                if (!skill.CanBeSpell(troop))
                    continue;

                if (troop.MoveRange.Count == 0)
                {
                    scenario.Map.GetMoveRange(troop, troop.MoveRange);
#if SANGO_DEBUG_AI
                    GameAIDebug.Instance.ShowMoveRange(troop.MoveRange, troop);
#endif
                }

                int moveRangeCount = troop.MoveRange.Count;
                if (moveRangeCount == 0)
                    continue;

                for (int j = 0; j < moveRangeCount; j++)
                {
                    Cell cell = troop.MoveRange[j];
                    // 只考虑可以停留的落点:当前位置(无建筑)或空地
                    if (!((cell == troop.cell && cell.building == null) || cell.IsEmpty()))
                        continue;

                    spellRangeCells.Clear();
                    skill.GetSpellRange(troop, cell, spellRangeCells);
                    for (int k = 0, spellCount = spellRangeCells.Count; k < spellCount; k++)
                    {
                        Cell spellCell = spellRangeCells[k];
                        if (!skill.CanSpellToHere(troop, spellCell))
                            continue;

                        attackCells.Clear();
                        tempTargets.Clear();
                        int targetHash = 0;
                        skill.GetAttackCells(troop, spellCell, attackCells);
                        for (int m = 0, atkCount = attackCells.Count; m < atkCount; m++)
                        {
                            Cell atkCell = attackCells[m];
                            if (atkCell.troop != null)
                            {
                                tempTargets.Add(atkCell.troop);
                                targetHash ^= (int)(atkCell.troop.Id * 2654435761u);
                            }
                            if (atkCell.building != null)
                            {
                                tempTargets.Add(atkCell.building);
                                targetHash ^= atkCell.building.Id * 40503;
                            }
                        }

                        if (tempTargets.Count == 0)
                            continue;

                        // 【性能优化】用「技能Id + 目标集合无序哈希」作为唯一键做 O(1) 去重,
                        // 替代原先 checkList.Find 的 O(n²) 线性查找(O(n²) → O(n))
                        long key = ((long)skill.Id << 32) | (uint)targetHash;
                        int dis = baseCheckCell.Distance(cell);

                        if (checkMap.TryGetValue(key, out PriorityActionData exist))
                        {
                            if (dis < exist.prioriry)
                            {
                                exist.movetoCell = cell;
                                exist.spellCell = spellCell;
                                exist.atkCells = attackCells.ToArray();
                                exist.targets = tempTargets.ToArray();
                            }
                        }
                        else
                        {
                            checkMap[key] = new PriorityActionData()
                            {
                                prioriry = dis,
                                skill = skill,
                                movetoCell = cell,
                                spellCell = spellCell,
                                atkCells = attackCells.ToArray(),
                                targets = tempTargets.ToArray(),
                            };
                        }
                    }
                }
            }

            foreach (KeyValuePair<long, PriorityActionData> pair in checkMap)
            {
                PriorityActionData priorityActionData = pair.Value;
                int atk_priority = 0;
                int targetCount = 0;
                for (int m = 0; m < priorityActionData.atkCells.Length; m++)
                {
                    Cell atkCell = priorityActionData.atkCells[m];
                    int p = prioritySkillAtkMethod?.Invoke(troop, priorityActionData.skill, atkCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;
                    if (p > 0)
                        targetCount++;
                    atk_priority += p;
                }

                if (!priorityActionData.skill.IsSingleSkill() && targetCount < 2)
                    atk_priority /= 2;

                int def_priority = prioritySkillDefMethod?.Invoke(troop, priorityActionData.skill, priorityActionData.spellCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;

                // 【分级策略】按态势档位与部队角色调整"进攻倾向"与"反击容忍度"：
                //   碾压 / 攻坚 → 放大攻击收益、压低反击惩罚（敢打）
                //   危局 / 护卫 → 压低攻击收益、放大反击惩罚（保守）
                if (effective.attackScale != 100)
                    atk_priority = (int)((long)atk_priority * effective.attackScale / 100);
                if (effective.counterPenaltyScale != 100)
                    def_priority = (int)((long)def_priority * effective.counterPenaltyScale / 100);

                int s_p = atk_priority + def_priority;
                // 【保护】被反击惩罚最多削弱一定比例的攻击收益,避免 AI 过度保守而在关键时刻"完全不行动"
                int floorPercent = AIConfig.Instance.counterAttackPenaltyFloorPercent;
                if (atk_priority > 0 && s_p < atk_priority * floorPercent / 100)
                    s_p = atk_priority * floorPercent / 100;
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
            // 【性能优化】复用静态缓存,避免每次调用分配 List
            enemyCellsCache.Clear();
            RangeEnemyCell(troop, 3, enemyCellsCache, scenario);
            for (int i = 0; i < enemyCellsCache.Count; i++)
            {
                Cell cell = enemyCellsCache[i];
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
            // 【修复】目标格即当前所在格：无需移动。
            // 否则下面的"选距离目标最近的落脚点"会挑到相邻格，造成原地来回抖动
            // （补给队刚出城时，后撤目标可能就是它所在的城市中心格）。
            if (targetCell == null || targetCell == troop.cell)
                return true;

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

        /// <summary>
        /// 攻击时被反击的风险评分(返回非正数,用于降低该行动的综合优先级)。
        /// 远程 / 策略技能不承受反击,返回 0;近战技能按被反击的期望伤害给出惩罚。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">使用的技能</param>
        /// <param name="target">命中格</param>
        /// <param name="movetoCell">移动落点</param>
        /// <param name="spellCell">施法格</param>
        /// <returns>反击惩罚分(非正数)</returns>
        public static int SkillDefencePriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            if (target == null || target.IsEmpty())
                return 0;
            if (target.troop == null || !skill.canDamageTroop)
                return 0;
            if (!troop.IsEnemy(target.troop))
                return 0;
            if (movetoCell == null)
                return 0;

            // 远程与策略攻击不会遭受反击
            if (skill.IsRange() || skill.IsStrategy())
                return 0;

            // 计算被反击的期望伤害
            float hitBack = target.troop.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(target, movetoCell));
            if (hitBack <= 0)
                return 0;

            int backDamage = (int)System.Math.Ceiling(hitBack * Troop.CalculateSkillDamage(target.troop, troop, null));
            // 反击伤害越高,惩罚越大,从而让 AI 规避"贴脸打高反击单位";
            // 惩罚幅度由 PriorityAction 中的下限保护约束,不会导致 AI 完全不行动
            return -backDamage * AIConfig.Instance.counterAttackPenaltyFactor;
        }

        /// <summary>
        /// 攻防属性评分（技能基础收益层）。
        ///
        /// 【分层设计】本方法只负责"技能 × 目标格"的**基础收益**，
        /// 不感知任务目标与战场态势；任务倍率由 <see cref="ApplyTaskBonus"/> 处理，
        /// 态势 / 角色倍率由 <see cref="PriorityAction(WeightList{PriorityActionData}, Troop, Cell, Scenario, SkillAttackPriorityCalculateMethod, SkillDefencePriorityCalculateMethod)"/> 统一应用。
        ///
        /// 全部系数取自 <see cref="AIConfig"/>，不再有硬编码字面量。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">使用的技能</param>
        /// <param name="target">命中格</param>
        /// <param name="movetoCell">移动落点</param>
        /// <param name="spellCell">施法格</param>
        /// <returns>基础收益分（负数表示误伤友军）</returns>
        public static int SkillStatusPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            int score = CalcSkillBasePriority(troop, skill, target, movetoCell, spellCell);
            if (score <= 0)
                return score;

            // 【领队性格】计略释放偏好：领队擅长的计略获得收益加成，不擅长的被降权。
            // 仅对计略类技能生效，普攻与战法不受影响。
            if (skill.IsStrategy())
                score = ApplyLeaderSkillPreference(score, troop, skill);

            return score;
        }

        /// <summary>
        /// 计略释放偏好：依据领队性格的"计略专精"与"计略倾向"调整计略技能的收益。
        ///
        /// <code>
        ///   mastery    = 性格对当前计略的专精加成（按技能 Id 映射）
        ///   rawScale   = troopSkillScale × (100 + mastery × leaderSkillTendencyScalePerPoint) / 100
        ///   scale      = clamp(rawScale, leaderSkillScaleMin, leaderSkillScaleMax)
        ///   score'     = score × scale / 100
        /// </code>
        /// </summary>
        /// <param name="score">基础收益分（正数）</param>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">使用的技能</param>
        /// <returns>调整后的收益分</returns>
        static int ApplyLeaderSkillPreference(int score, Troop troop, SkillInstance skill)
        {
            AIConfig cfg = AIConfig.Instance;
            if (!cfg.useLeaderPersonality || !cfg.useLeaderSkillPreference)
                return score;

            Personality PersonalityId = troop != null ? troop.LeaderPersonality : null;
            if (PersonalityId == null)
                return score;

            // 技能 Id → 性格计略类型；不属于性格表的计略不修正
            PersonalitySkillType skillType = PersonalitySkillMap.ToSkillType(skill.Id);
            if (skillType == PersonalitySkillType.None)
                return score;

            int mastery = PersonalityId.GetSkillMasteryAdd(skillType);
            int masteryScale = 100 + mastery * cfg.leaderSkillTendencyScalePerPoint;

            // 计略总倾向 × 专精系数（long 中转防溢出）
            long scale = (long)ClampPositiveScale(PersonalityId.troopSkillScale) * ClampPositiveScale(masteryScale) / 100;

            if (scale < cfg.leaderSkillScaleMin) scale = cfg.leaderSkillScaleMin;
            if (scale > cfg.leaderSkillScaleMax) scale = cfg.leaderSkillScaleMax;
            if (scale == 100)
                return score;

            long result = (long)score * scale / 100;
            if (result > int.MaxValue) result = int.MaxValue;
            return (int)result;
        }

        /// <summary>
        /// 把倍率钳制到 ≥ 1，避免配置为 0 或负数时把收益彻底抹平。
        /// </summary>
        /// <param name="scale">原始倍率</param>
        /// <returns>钳制后的倍率</returns>
        static int ClampPositiveScale(int scale)
        {
            return scale > 0 ? scale : 1;
        }

        /// <summary>
        /// 技能基础收益计算（不含任务倍率、态势与性格修正）。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">使用的技能</param>
        /// <param name="target">命中格</param>
        /// <param name="movetoCell">移动落点</param>
        /// <param name="spellCell">施法格</param>
        /// <returns>基础收益分</returns>
        static int CalcSkillBasePriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            if (target.IsEmpty()) return 0;
            AIConfig cfg = AIConfig.Instance;

            if (target.troop != null && skill.canDamageTroop)
            {
                if (troop.IsEnemy(target.troop))
                {
                    int strategyFactor = 0;
                    if (skill.IsStrategy())
                    {
                        if (skill.onlySpellToTeam)
                        {
                            if (!target.troop.IsSameForce(troop))
                                return 0;
                            else if (target.troop.HasControlBuff())
                                strategyFactor = cfg.scoreControlBonus;
                        }
                        else
                        {
                            // 不对着火的地方释放火计
                            if (skill.Id == SKILL_ID_FIRE && target.fire != null)
                                return 0;
                            else if (skill.Id == SKILL_ID_PUT_OUT_FIRE && target.fire == null)
                                return 0;
                            else if (skill.skillSuccessMethod == null)
                                return 0;
                            else if (target.troop.HasControlBuff())
                                return 0;

                            int succ = skill.skillSuccessMethod.Calculate(skill, troop, target);
                            if (succ < cfg.scoreStrategySuccessThreshold)
                                return 0;
                        }
                    }
                    else
                    {
                        if (target.troop.HasControlBuff())
                            strategyFactor += cfg.scoreControlBonus;
                    }

                    int bonus = strategyFactor
                              + (skill.IsRange() ? cfg.scoreRangeBonus : 0)
                              + (skill.IsSingleSkill() ? cfg.scoreSingleBonus : 0)
                              + (skill.HasEffect() ? cfg.scoreEffectBonus : 0);

                    return (skill.atk + bonus) * cfg.scoreSkillBaseFactor
                         / Math.Max(cfg.scoreMinCostEnergy, skill.costEnergy)
                         * (troop.Attack - target.troop.Defence + cfg.scoreAttackDefenceBase);
                }
                else if (skill.canDamageTeam && spellCell != target)
                {
                    int bonus = (skill.IsRange() ? cfg.scoreRangeBonus : 0)
                              + (skill.IsSingleSkill() ? cfg.scoreSingleBonus : 0)
                              + (skill.HasEffect() ? cfg.scoreEffectBonus : 0);

                    return -(skill.atk + bonus) * cfg.scoreSkillBaseFactor
                         / Math.Max(cfg.scoreMinCostEnergy, skill.costEnergy)
                         * (troop.Attack - target.troop.Defence + cfg.scoreAttackDefenceBase);
                }
            }
            else if (target.building != null && skill.canDamageBuilding)
            {
                int rangeFactor = skill.IsRange() ? cfg.scoreBuildingRangeFactor : cfg.scoreBuildingMeleeFactor;
                if (troop.IsEnemy(target.building))
                {
                    if (skill.IsStrategy())
                    {
                        if (skill.onlySpellToTeam)
                            return 0;

                        // 【修复】原先未判空即调用 Calculate，策略技能缺成功率算法时会抛异常
                        if (skill.skillSuccessMethod == null)
                            return 0;

                        int succ = skill.skillSuccessMethod.Calculate(skill, troop, target);
                        if (succ > cfg.scoreStrategySuccessThreshold)
                            rangeFactor += cfg.scoreStrategySuccessBonus * (succ - cfg.scoreStrategySuccessThreshold);
                    }

                    int bonus = (skill.IsSingleSkill() ? cfg.scoreSingleBonus : 0)
                              + (skill.HasEffect() ? cfg.scoreEffectBonus : 0);

                    return (skill.atkDurability + bonus) * cfg.scoreBuildingDamageFactor * rangeFactor
                         * (skill.IsSingleSkill() ? cfg.scoreBuildingSingleFactor : cfg.scoreBuildingMultiFactor);
                }
                else if (skill.canDamageTeam && spellCell != target)
                {
                    int bonus = (skill.IsSingleSkill() ? cfg.scoreSingleBonus : 0)
                              + (skill.HasEffect() ? cfg.scoreEffectBonus : 0);

                    return (skill.atkDurability + bonus) * cfg.scoreFriendlyBuildingFactor * rangeFactor
                         * (skill.IsSingleSkill() ? cfg.scoreBuildingSingleFactor : cfg.scoreBuildingMultiFactor);
                }
            }
            return 0;
        }

        /// <summary>
        /// 任务加成：把"是否命中任务主目标 / 是否原地施法 / 是否近战贴脸"折算为收益倍率。
        ///
        /// 【设计目的】统一原先分散在 TroopOccupyCity / TroopDestroyTroop / TroopProtectCity
        /// 三处的评分逻辑，并消除 500000 / 1000000 / 30000 / 50000 等硬编码魔法数字 ——
        /// 现在全部改为可配置的**倍率**（见 AIConfig 的 task* 字段）。
        /// </summary>
        /// <param name="score">基础收益分（非正数原样返回）</param>
        /// <param name="isPrimaryTarget">是否命中任务主目标</param>
        /// <param name="isStay">是否原地施法（未移动）</param>
        /// <param name="isMeleeClose">是否近战贴脸（非远程且原地）</param>
        /// <param name="roleWeights">角色权重（可为 null，表示不做角色修正）</param>
        /// <returns>调整后的收益分</returns>
        public static int ApplyTaskBonus(int score, bool isPrimaryTarget, bool isStay, bool isMeleeClose, TroopRoleWeights roleWeights)
        {
            if (score <= 0)
                return score;

            AIConfig cfg = AIConfig.Instance;
            int focusScale = roleWeights != null && roleWeights.missionFocusScale > 0
                ? roleWeights.missionFocusScale
                : 100;

            // 主目标大幅加权；非目标显著降权，使部队专注既定任务
            int weight = isPrimaryTarget ? cfg.taskPrimaryTargetWeight : cfg.taskOtherTargetWeight;

            // 用 long 中转：score 可达百万级、weight 可达数千，直接相乘会溢出 int
            long adjusted = (long)score * weight / 100;
            adjusted = adjusted * focusScale / 100;
            if (adjusted > int.MaxValue)
                adjusted = int.MaxValue;
            score = (int)adjusted;

            // 站位加成：原地施法优先于近战贴脸
            if (isStay)
                score = score + score * cfg.taskStayBonus / 100;
            else if (isMeleeClose)
                score = score + score * cfg.taskMeleeCloseBonus / 100;

            return score;
        }
    }
}
