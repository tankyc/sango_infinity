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
        /// <summary>出城空位检查用的格子缓存(对齐原版 has_empty_place 的固定圈检查)</summary>
        static List<Cell> leaveBuildingCheckCells = new List<Cell>(64);

        /// <summary>
        /// 「技能 + 施法格」的命中信息缓存。
        ///
        /// 【为什么可以缓存】<see cref="SkillInstance.CanSpellToHere"/> 与
        /// <see cref="SkillInstance.GetAttackCells"/> 的输入只有 (技能实例, 施法格)，
        /// 与"施放者从哪个落点走过来"无关。而原实现是「每个落点 × 每个施法格」各算一遍，
        /// 相邻落点的施法格高度重叠，等于把同一份结果重复算了 N 遍。
        /// </summary>
        class SpellAttackCache
        {
            /// <summary>该施法格是否满足技能释放条件</summary>
            public bool canSpell;
            /// <summary>命中目标集合的无序哈希（用于候选去重）</summary>
            public int targetHash;
            /// <summary>命中格快照</summary>
            public Cell[] atkCells;
            /// <summary>命中目标快照</summary>
            public SangoObject[] targets;
        }

        /// <summary>按技能复用的施法格缓存（每个技能开始时 Clear，避免跨技能串味）</summary>
        static Dictionary<Cell, SpellAttackCache> spellAttackCache = new Dictionary<Cell, SpellAttackCache>(128);

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

            // 【严格对齐原版】原版 action_list_remove_by_chance（s11_sys_unit_action.cpp:1355-1386）
            // 只从**第 2 名**开始按动作类型概率移除，第一名永远保留 —— 等价于"总是取评分最高的动作"。
            // 择优池大小由 AIConfig.o311BestN 控制：1 = 取最优（默认），N = 前 N 名加权随机。
            if (AIConfig.Instance.useOpen311ScoreFormula)
                return wightList.RandomGetTop(AIConfig.Instance.o311BestN);

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

            bool moveRangeReady = false;
            for (int i = 0, count = skill_list.Count; i < count; i++)
            {
                SkillInstance skill = skill_list[i];
                if (!skill.CanBeSpell(troop))
                    continue;

                // 【性能】移动范围整次评估只算一次。
                // 原先的写法是"MoveRange.Count == 0 就再算一遍"，一旦本回合确实无处可走
                // （被围死 / 城头全是敌军 / 地形不通），每个技能都会重跑一次全量 BFS
                // （实测单次 GetMoveRange ≈ 2.4ms，5 个技能就白烧 12ms）。
                if (!moveRangeReady)
                {
                    scenario.Map.GetMoveRange(troop, troop.MoveRange);
                    moveRangeReady = true;
#if SANGO_DEBUG_AI
                    GameAIDebug.Instance.ShowMoveRange(troop.MoveRange, troop);
#endif
                }

                int moveRangeCount = troop.MoveRange.Count;
                if (moveRangeCount == 0)
                    continue;

                // 【性能】同一个施法格会被多个落点重复命中，而 CanSpellToHere / GetAttackCells
                // 都只取决于 (技能, 施法格)，与"从哪个落点放"无关。按技能缓存一次，
                // 把原来的「落点数 × 施法格数」次重算压成「施法格数」次。
                spellAttackCache.Clear();

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

                        // 【性能】同一施法格在相邻落点上会反复命中，这里按 (技能, 施法格) 只算一次：
                        // 可施放判定 + 命中格 + 命中目标 + 目标哈希，全部与落点无关。
                        if (!spellAttackCache.TryGetValue(spellCell, out SpellAttackCache cache))
                        {
                            cache = new SpellAttackCache();
                            cache.canSpell = skill.CanSpellToHere(troop, spellCell);
                            if (cache.canSpell)
                            {
                                attackCells.Clear();
                                tempTargets.Clear();
                                int hash = 0;
                                bool hasObject = false;
                                skill.GetAttackCells(troop, spellCell, attackCells);
                                for (int m = 0, atkCount = attackCells.Count; m < atkCount; m++)
                                {
                                    Cell atkCell = attackCells[m];
                                    if (atkCell.troop != null)
                                    {
                                        tempTargets.Add(atkCell.troop);
                                        hash ^= (int)(atkCell.troop.Id * 2654435761u);
                                        hasObject = true;
                                    }
                                    if (atkCell.building != null)
                                    {
                                        tempTargets.Add(atkCell.building);
                                        hash ^= atkCell.building.Id * 40503;
                                        hasObject = true;
                                    }
                                }
                                // 【空目标】空火格消火时命中格内没有任何对象，按原样会得到 hash = 0，
                                // 所有空火格就会撞同一个去重键（checkMap 只留最近的一格）。
                                // 改用命中格坐标参与哈希，保证每个空格是独立候选。
                                cache.targetHash = hasObject
                                    ? hash
                                    : (attackCells.Count > 0
                                        ? (attackCells[0].x * 73856093) ^ (attackCells[0].y * 19349663)
                                        : 0);
                                cache.atkCells = attackCells.Count > 0 ? attackCells.ToArray() : System.Array.Empty<Cell>();
                                cache.targets = tempTargets.Count > 0 ? tempTargets.ToArray() : System.Array.Empty<SangoObject>();
                            }
                            spellAttackCache[spellCell] = cache;
                        }

                        if (!cache.canSpell)
                            continue;

                        // 【对齐原版】空格子也有合法动作，但条件各不相同：
                        //   · 消火 —— get_after_move_action:340-344
                        //       `add_strategy_action(..., self_shouka = hex.get_fire_timer() > 1)`
                        //   · 火计 —— get_enabled_strategies 空对象分支（s11_sys_unit_target.cpp:374-378）
                        //       `if (is_firable_terrain(get_terrain_id())) flags[StrategyId_Fire] = true`
                        // 本项目原先用 `targets.Length == 0` 一刀切，两类空格动作都进不了候选。
                        if (cache.targets.Length == 0
                            && !IsEmptyFireCellShoukaCandidate(troop, skill, spellCell)
                            && !IsEmptyCellFireCandidate(troop, skill, spellCell))
                            continue;

                        // 【性能优化】用「技能Id + 目标集合无序哈希」作为唯一键做 O(1) 去重,
                        // 替代原先 checkList.Find 的 O(n²) 线性查找(O(n²) → O(n))
                        long key = ((long)skill.Id << 32) | (uint)cache.targetHash;
                        int dis = baseCheckCell.Distance(cell);

                        if (checkMap.TryGetValue(key, out PriorityActionData exist))
                        {
                            if (dis < exist.prioriry)
                            {
                                exist.movetoCell = cell;
                                exist.spellCell = spellCell;
                                exist.atkCells = cache.atkCells;
                                exist.targets = cache.targets;
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
                                atkCells = cache.atkCells,
                                targets = cache.targets,
                            };
                        }
                    }
                }
            }

            AIConfig cfgAgg = AIConfig.Instance;
            bool open311 = cfgAgg.useOpen311ScoreFormula;

            foreach (KeyValuePair<long, PriorityActionData> pair in checkMap)
            {
                PriorityActionData priorityActionData = pair.Value;

                // 【陷阱引爆闸门 —— 候选级】对齐原版 add_attack_action:408 / add_tactic_action:669
                // （命中对象是"已建成陷阱"且非任务主目标时，原版不生成该候选）。
                //
                // 为什么必须放在候选级，而不是逐格扣分：
                // 本项目把一次技能的多个命中格聚合成**一个**候选，逐格负分还会被下面的
                // `if (p > 0)` 过滤掉。实测分值是「单个敌人目标 ≈ +9200~10200」对
                // 「单个陷阱罚分 ≈ -8800」，穿线上只要有 1 个敌人就能把陷阱的负分完全抵消，
                // 于是"瞄准陷阱、穿过它打后面敌人"成了高分候选 —— 这正是
                // 「诸葛诞 贯箭 =>(火球格)」那条日志的成因。
                //
                // 判定口径：陷阱自己算一次引爆收益（get_trap_score）
                //   · 收益 > 0 → 爆炸范围内确实有敌人可炸，保留候选（正常逐格累加）；
                //   · 收益 ≤ 0（炸不到人 / 只炸到友军 / 引爆者也在爆心）→ 整条候选作废。
                if (HasUselessTrapInAttackCells(troop, priorityActionData))
                    continue;

                int atk_priority = 0;
                int targetCount = 0;
                for (int m = 0; m < priorityActionData.atkCells.Length; m++)
                {
                    Cell atkCell = priorityActionData.atkCells[m];
                    int p = prioritySkillAtkMethod?.Invoke(troop, priorityActionData.skill, atkCell, priorityActionData.movetoCell, priorityActionData.spellCell) ?? 0;
                    if (p > 0)
                        targetCount++;

                    if (open311)
                    {
                        // 【本项目保留】多目标累加：一次技能命中的多个目标合成为**一个**候选，
                        // 分值为各目标之和（原版是逐目标独立候选，这里按项目需要保留聚合）。
                        // 【累加规则】正分照常累加；负分分两种，必须区别对待：
                        //   · 「无效目标」标记（原版 get_target_score 的 -1 一档，
                        //     p 落在 o311InvalidTargetMark 附近）→ 跳过。
                        //     一行空地就能扫出十几个无效目标，全累加会把整条候选拖垮；
                        //   · 真正的误伤扣分（AIConfig.o311SelfDamagePenalty，默认 -12000）
                        //     → **必须照常累加**。这是"AI 放妖术砸自家内政设施"的唯一拦阻：
                        //     canDamageTeam 技能在执行层对友军照打（SkillInstance.cs:862），
                        //     AI 侧只能靠分值把这种落点压到负分、进而被 s_p > 0 淘汰。
                        if (p > 0)
                            atk_priority += p;
                        else if (p <= cfgAgg.o311InvalidTargetMark)
                            atk_priority += p;
                    }
                    else
                    {
                        atk_priority += p;
                    }
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

        /// <summary>
        /// 【出城兜底】判断部队是否正停在城池 / 关隘 / 港口等建筑格（城头）上。
        ///
        /// 游戏硬性规则：部队不允许停留在建筑格上（见 <see cref="Cell.CanStay"/>）。
        /// 部队因出征创建（EnsureTroop 落在城头）、任务改写、占领异常等原因
        /// 滞留城头时，调用方应优先安排"离开城头"。
        /// </summary>
        /// <param name="troop">待检查的部队</param>
        /// <returns>是否正停在建筑格上</returns>
        public static bool IsOnBuildingCell(Troop troop)
        {
            return troop != null && troop.cell != null && troop.cell.building != null;
        }

        /// <summary>
        /// 【出城兜底】在部队移动范围内寻找离脚下建筑格最近的、可停留的空地。
        ///
        /// 落脚点由 <see cref="Cell.CanStay"/> 把关（无部队、无建筑、地形可通行），
        /// 确保离开城头后一定站在合法地块上，不会再出现"停在城头"的情况。
        /// </summary>
        /// <param name="troop">滞留在建筑格上的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>推荐落脚格；移动范围内没有可停留格（被围死）时返回 null</returns>
        public static Cell FindLeaveBuildingCell(Troop troop, Scenario scenario)
        {
            if (troop == null || troop.cell == null || troop.cell.building == null)
                return null;

            Map map = scenario.Map;
            // 与其它移动逻辑保持同一口径：移动范围只在为空时计算一次。
            // 【不能豁免友方建筑代价】本方法同时服务 TryLeaveBuildingCell 的"实际出城移动"，
            // 若豁免，移动范围(无 +16)会与 MoveTo → GetMovePath 的实际寻路(友方建筑仍 +16)口径不一致，
            // 找到"走不到"的落脚点，部队就会卡在城头。友方建筑 +16 对齐原版 get_path。
            if (troop.MoveRange.Count == 0)
                map.GetMoveRange(troop, troop.MoveRange);

            Cell best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < troop.MoveRange.Count; i++)
            {
                Cell candidate = troop.MoveRange[i];
                // 跳过自己所在格（建筑格不可停留）与不可停留的地块
                if (candidate == null || candidate == troop.cell)
                    continue;
                if (!candidate.CanStay(troop))
                    continue;

                // 选最近的一格：出城这一步尽量少消耗移动力
                int distance = map.Distance(troop.cell, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// 【出城兜底】把停在城头的部队送到移动范围内最近的、可停留的空地。
        ///
        /// 该逻辑推广自补给队的"出城优先"（TroopSupplyTroop），适用于所有部队：
        /// 停在建筑格上时，本回合只做一件事——离开城头。
        /// </summary>
        /// <param name="troop">滞留在建筑格上的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>
        /// true  —— 已经站在可停留地块上，或本回合移动范围内确实没有落脚点（被围死）；
        /// false —— 出城移动动画未播完，调用方需 return false 等待下一帧。
        /// </returns>
        public static bool TryLeaveBuildingCell(Troop troop, Scenario scenario)
        {
            if (troop == null || troop.cell == null || troop.cell.building == null)
                return true;

            Cell best = FindLeaveBuildingCell(troop, scenario);

            // 被敌军 / 建筑彻底围死：本回合无处可去，保持临时状态，下次行动再试
            if (best == null)
                return true;

            return troop.MoveTo(best);
        }

        /// <summary>
        /// 【前置判断】停靠在建筑格（城头）上的部队是否还能走出城：移动范围内
        /// 是否存在至少一个可停留的空地（由 <see cref="Cell.CanStay"/> 把关）。
        ///
        /// 供 AI 出兵的"前置判断"使用：城池被完全围困（无可停留格）时，
        /// 出征的部队无法离开城头，此时不应再组建 / 派出该部队。
        /// </summary>
        /// <param name="troop">已停在建筑格上的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>移动范围内存在可停留格时返回 true</returns>
        public static bool CanLeaveBuildingCell(Troop troop, Scenario scenario)
        {
            if (troop == null || troop.cell == null || troop.cell.building == null)
                return false;

            // 对齐原版 s11_sys_person_order_military.cpp:358-361 has_empty_place：
            //   部署前检查"城周围 2 圈（关/港 1 圈）内有无空位（is_empty）"，无空位就不部署。
            // 原版用「固定 hex 距离」判断，不受移动力 / 移动代价 / ZOC 影响；本项目此前用
            // 「移动范围」(GetMoveRange) 判断，而移动范围会被敌军 CanPassThrough 挡死，把
            // 「城外 2 圈仍有空位」误判成「被完全围困」，导致部队组建后卡在城头出不去。
            int checkRadius = troop.cell.building is City ? 2 : 1;
            leaveBuildingCheckCells.Clear();
            scenario.Map.GetSpiral(troop.cell, checkRadius, leaveBuildingCheckCells);
            for (int i = 0; i < leaveBuildingCheckCells.Count; i++)
            {
                if (leaveBuildingCheckCells[i].IsEmpty())
                    return true;
            }
            return false;
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

            // hitBack 是百分比（90 = 0.9 倍），必须 /100 后再乘伤害。
            // 原来漏了这一步，导致 AI 把反击惩罚放大 100 倍（几乎不敢近战）。
            int backDamage = (int)(hitBack * Troop.CalculateSkillDamage(target.troop, troop, null) / 100);
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

            // 【严格对齐原版】Open311 评分体系下不叠加下面这两个乘性偏好：
            //   · 领队性格的"计略专精倍率"（原版只有 param.strategy_score 的加分，
            //     已在 CalcOpen311ParamScore 里实现）
            //   · 剑兵的"计略倾向加成"（原版无）
            if (AIConfig.Instance.useOpen311ScoreFormula)
                return score;

            // 【领队性格】计略释放偏好：领队擅长的计略获得收益加成，不擅长的被降权。
            // 仅对计略类技能生效，普攻与战法不受影响。
            if (skill.IsStrategy())
            {
                score = ApplyLeaderSkillPreference(score, troop, skill);

                // 【兵种倾向】剑兵只带"近战普攻"，物理输出孱弱，对计略额外加成，
                // 使其更倾向于靠计略弥补战力短板。
                score = ApplyTroopTypeSkillPreference(score, troop, skill);
            }

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
        /// 兵种计略倾向：剑兵（<see cref="Troop.IsSword"/>）只有"近战普攻"这一种物理手段，
        /// 硬拼必亏，因此对计略技能的收益额外加成，让其更倾向于释放计略。
        ///
        /// 只对计略类技能生效，普攻 / 战法不受影响；倍率由
        /// <see cref="AIConfig.swordStrategyScoreScale"/> 配置（100 = 与其它兵种无差别）。
        /// </summary>
        /// <param name="score">经过领队性格修正后的收益分（正数）</param>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">使用的技能</param>
        /// <returns>调整后的收益分</returns>
        static int ApplyTroopTypeSkillPreference(int score, Troop troop, SkillInstance skill)
        {
            AIConfig cfg = AIConfig.Instance;
            if (!cfg.useSwordStrategyPreference)
                return score;

            if (troop == null || troop.LandTroopType == null || !troop.IsSword)
                return score;

            int scale = cfg.swordStrategyScoreScale;
            if (scale <= 0 || scale == 100)
                return score;

            long result = (long)score * scale / 100;
            if (result > int.MaxValue) result = int.MaxValue;
            return (int)result;
        }

        /// <summary>
        /// 计略成功率门槛：默认取 <see cref="AIConfig.scoreStrategySuccessThreshold"/>；
        /// 剑兵物理孱弱，门槛放宽为 <see cref="AIConfig.swordStrategySuccessThreshold"/>，
        /// 否则智力平平的剑兵队计略评分恒为 0，"倾向计略"的加权也无从生效。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <returns>成功率门槛（%）</returns>
        static int StrategySuccessThreshold(Troop troop)
        {
            AIConfig cfg = AIConfig.Instance;
            if (cfg.useSwordStrategyPreference
                && troop != null && troop.LandTroopType != null && troop.IsSword)
                return cfg.swordStrategySuccessThreshold;

            return cfg.scoreStrategySuccessThreshold;
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

            // 【严格对齐原版】启用原版评分体系时，走离散加项制（get_target_score +
            // get_attack_damage_score + 动作常量），与下面的连续分制完全互斥。
            if (cfg.useOpen311ScoreFormula)
                return CalcOpen311BasePriority(troop, skill, target, movetoCell, spellCell);

            if (target.troop != null && skill.canDamageTroop)
            {
                if (troop.IsEnemy(target.troop))
                {
                    // 【原版闸门】get_attack_damage_score（s11_sys_unit_action.cpp:1790-1799）：
                    // 真实伤害若连"敌兵力的一半"都打不掉，这一击对原版 AI 毫无价值 → 分值 0。
                    // 这是原版与旧实现最大的行为差：旧实现用 Attack-Defence 近似，从不放弃。
                    if (cfg.useRealDamageScore && !PassTroopDamageGate(troop, target.troop, skill))
                        return 0;

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
                            // 成功率门槛按兵种取：剑兵门槛更低，允许其赌一赌计略
                            //（见 AIConfig 的"剑兵计略倾向"参数）
                            if (succ < StrategySuccessThreshold(troop))
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

                    int score = (skill.atk + bonus) * cfg.scoreSkillBaseFactor
                              / Math.Max(cfg.scoreMinCostEnergy, skill.costEnergy)
                              * (troop.Attack - target.troop.Defence + cfg.scoreAttackDefenceBase);

                    // 【原版 get_target_score:1739-1741】目标处于**伪报**状态时，
                    // score = score * 0.1f —— 伪报部队会自己往回跑，打它等于浪费一次行动。
                    // 注意只对伪报降权：混乱（Stun）没有这条，反而因"不能反击"而更值得打，
                    // 所以上面的 scoreControlBonus 加成分支保留。
                    if (target.troop.IsEscapeState())
                        score = score * 10 / 100;

                    // 【原版 get_target_score:1742-1754】目标周围 1~2 格内有本方建筑 → +1375。
                    //
                    // 【必须加在最终分上】原版这一项是"动作总分"的加数
                    // （s11_sys_unit_action.cpp:275 的 score + target_score + param.attack_score），
                    // 不是"技能攻击力"的加数。本项目如果把 1375 加进 bonus（= skill.atk 的加项），
                    // 会被后面的 ×150/energy×(攻防差+200) 放大上百倍，属于量纲错误。
                    if (HasFriendlyBuildingNear(troop, target.troop.cell, 2))
                        score += cfg.scoreNearFriendlyBuildingBonus;

                    return score;
                }
                else if (skill.canDamageTeam && spellCell != target)
                {
                    int bonus = (skill.IsRange() ? cfg.scoreRangeBonus : 0)
                              + (skill.IsSingleSkill() ? cfg.scoreSingleBonus : 0)
                              + (skill.HasEffect() ? cfg.scoreEffectBonus : 0);

                    // 【修复】误伤友军的评分必须恒为负分（越伤越负）。
                    // 原先直接乘 (troop.Attack - target.troop.Defence + scoreAttackDefenceBase)，
                    // 当友军防御比我方攻击高出 scoreAttackDefenceBase(默认200) 以上时该乘数为负，
                    // 与前面的负号相乘后整体变成**正收益**，AI 反而会主动轰击高防御的友军
                    // （落雷 / 妖术这类 canDamageTeam 的范围计略尤其容易触发）。
                    // 这里把攻防差钳制到最小 0，保证惩罚项恒为正、整体恒为非正分。
                    int atkDefDiff = troop.Attack - target.troop.Defence;
                    if (atkDefDiff < 0)
                        atkDefDiff = 0;

                    return -(skill.atk + bonus) * cfg.scoreSkillBaseFactor
                         / Math.Max(cfg.scoreMinCostEnergy, skill.costEnergy)
                         * (atkDefDiff + cfg.scoreAttackDefenceBase);
                }
            }
            else if (target.building != null && skill.canDamageBuilding)
            {
                // 【原版闸门 :404-409】已建成的陷阱默认不生成攻击候选，只有陷阱本身是任务主目标时才保留。
                // 连续分制与 Open311 分制必须同口径，否则关掉 useOpen311ScoreFormula 后
                // "部队路过顺手打陷阱 / 工程队引爆自己刚建的陷阱"会立刻复发。
                bool isTrap = IsCompletedTrap(target.building);
                bool trapMainTarget = isTrap && IsTrapMainTarget(troop, target.building);
                if (isTrap && !trapMainTarget)
                    return 0;

                int rangeFactor = skill.IsRange() ? cfg.scoreBuildingRangeFactor : cfg.scoreBuildingMeleeFactor;
                if (troop.IsEnemy(target.building))
                {
                    // 【原版 is_base_in_friend_area（s11_sys_unit_utils.cpp:624-637）】
                    // 友方领土上的异族根城不拆；任务主目标除外。连续分制与 Open311 分制必须同口径，
                    // 否则关掉 useOpen311ScoreFormula 后 AI 行为会立刻分叉。
                    if (target.building.Id != troop.missionTarget && IsBaseInFriendArea(troop, target.building))
                        return 0;

                    // 【原版闸门】get_attack_damage_score 的建筑分支（s11_sys_unit_action.cpp:1800-1832）：
                    //   据点：不能一击破，且兵力伤害与耐久伤害都远不够 → 0；
                    //   陷阱 / 障碍 → 0；普通设施一击打不掉耐久 → 0。
                    // 例外：陷阱是任务主目标时不套用（原版 :408 允许生成候选，只是拿不到伤害加分），
                    // 否则"专门去拆对手陷阱"的任务会永远打不动、部队卡死。
                    if (cfg.useRealDamageScore && !trapMainTarget
                        && !PassBuildingDamageGate(troop, target.building, skill))
                        return 0;

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
        /// 目标周围 range 格内是否有**本方**建筑。
        ///
        /// 【原版对应】get_target_score（s11_sys_unit_action.cpp:1742-1754）里的
        /// <c>MapRangeIterator(&amp;map_, target->get_pos(), 1, 2)</c> + "建筑属于自己势力" → +1375。
        /// 原版遍历的是距离 1~2 格（不含自身格），这里用 SpiralAction(range) 含自身格，
        /// 差异仅在"目标脚下正好压着一座己方建筑"的极端情况，可忽略。
        /// </summary>
        static bool HasFriendlyBuildingNear(Troop troop, Cell center, int range)
        {
            if (troop == null || center == null || troop.BelongForce == null)
                return false;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null)
                return false;

            bool found = false;
            scenario.Map.SpiralAction(center, range, c =>
            {
                if (found || c == null || c.building == null)
                    return;
                if (c.building.BelongForce == troop.BelongForce)
                    found = true;
            });
            return found;
        }

        /// <summary>
        /// 【原版直译】对部队目标的"值不值得打"闸门 —— get_attack_damage_score 部队分支
        /// （D:\open311\s11\s11_sys_unit_action.cpp:1790-1799）。
        ///
        /// 原版判定：
        ///     伤害 &gt;= 敌兵力       → 2000（能击破）
        ///     伤害 * 2 &gt;= 敌兵力   → 1000（打掉一半以上，值得）
        ///     否则                    → 0（放弃该目标）
        /// 这里只回答"放不放弃"，具体分值仍由本项目的连续评分体系给出。
        /// </summary>
        static bool PassTroopDamageGate(Troop troop, Troop target, SkillInstance skill)
        {
            if (troop == null || target == null) return true;
            int damage = Troop.CalculateSkillDamage(troop, target, skill);
            if (damage >= target.troops) return true;
            return damage * 2 >= target.troops;
        }

        /// <summary>
        /// 【原版直译】对建筑目标的闸门 —— get_attack_damage_score 建筑分支
        /// （D:\open311\s11\s11_sys_unit_action.cpp:1800-1832）。
        ///
        /// 据点（城 / 关 / 港）：
        ///     兵力伤害 &gt;= 守军 或 耐久伤害 &gt;= 剩余耐久 → 一击破，值得；
        ///     兵力伤害 * 3 &lt; 守军                        → 再看耐久：耐久 * 3 &gt; 剩余耐久 才值得；
        ///     其余                                        → 值得。
        /// 陷阱 / 障碍 → 永远 0（原版明确返回 0，哪怕打得掉）。
        /// 普通设施：耐久伤害 &lt; 剩余耐久 → 0。
        /// </summary>
        static bool PassBuildingDamageGate(Troop troop, BuildingBase target, SkillInstance skill)
        {
            if (troop == null || target == null) return true;

            BuildingType type = target.BuildingType;
            if (type != null && (type.IsExplosive || type.IsObstacle))
                return false;

            // 【根城必须放行】
            // 原版 get_attack_damage_score 对 FacilityId_Base / Den 走的是最后的
            // "普通设施"分支（s11_sys_unit_action.cpp:1826-1831）：一击打不掉耐久时
            // **只有 damage_score 这一项返回 0**，动作总分里仍然保留
            // get_target_score 的 hostile +5000（:1694-1695）与 Base/Den +200（:1770-1773），
            // 所以原版部队会持续磨掉拆不掉的根城。
            // 本项目的 gate 语义是"整条动作否决"，直接套用会让根城在耐久打不完时恒为 0 分
            // —— 表现就是"部队清掉出城的贼兵后，再也不去动根据地"。
            // 因此这里对根城不做一次性否决，把取舍交回评分层。
            if (BanditCampPlacer.IsRaiderCamp(target as Building))
                return true;

            City city = target as City;
            if (city != null && (city.IsCity() || city.IsGate() || city.IsPort()))
            {
                int dmgTroops = Troop.CalculateSkillDamageTroopOnCity(troop, city, skill);
                int dmgDurability = Troop.CalculateSkillDamage(troop, target, skill);

                if (dmgTroops >= city.troops || dmgDurability >= target.durability)
                    return true;

                if (dmgTroops * 3 < city.troops)
                    return dmgDurability * 3 > target.durability;

                return true;
            }

            int hp = Troop.CalculateSkillDamage(troop, target, skill);
            return hp >= target.durability;
        }

        #region Open311 原版评分体系（离散加项制）

        /*
         * 原版一次动作的总分 = 目标分 + 属性分 + 动作常量 + 伤害分
         * ────────────────────────────────────────────────────────────────
         *   target_score            get_target_score      s11_sys_unit_action.cpp:1687-1778
         *   param.xxx_score         :18-40（统率/武力/智力 + 性格）
         *   action.score 累加        add_attack_action     :404-425
         *   attack/defence 惩罚      :411-412（-150）
         *   attack_damage_score      get_attack_damage_score :1780-1834
         *
         * 全部常量见 AIConfig.o311*，默认值与源码一一对应。
         */

        /// <summary>
        /// 【异族专用】强制走原版离散评分（get_target_score + get_attack_damage_score + 动作常量），
        /// 不受全局 <see cref="AIConfig.useOpen311ScoreFormula"/> 开关影响。
        ///
        /// 为什么需要：异族 / 贼部队（Subjugate）在原版里 main_target 被清空
        /// （s11_sys_unit_action.cpp:65-68），评分完全由 get_target_score 的原生分值决定。
        /// 若跟随全局开关走连续分制，异族会失去"能一击摧毁的据点 / 敌部队 / 建筑设施"
        /// 这组离散优先级，行为与原版分叉。
        /// </summary>
        public static int Open311SkillPriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            return CalcOpen311BasePriority(troop, skill, target, movetoCell, spellCell);
        }

        /// <summary>
        /// 【原版直译】技能基础收益（离散加项制）。
        /// 三种动作分别对应原版的 add_attack_action / add_tactic_action / add_strategy_action。
        /// </summary>
        static int CalcOpen311BasePriority(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell)
        {
            AIConfig cfg = AIConfig.Instance;
            if (troop == null || skill == null || target == null) return 0;

            int targetScore = CalcOpen311TargetScore(troop, skill, target);
            if (targetScore < 0) return -1;              // 原版 -1 = 该动作从候选里丢弃

            int paramScore = CalcOpen311ParamScore(troop, skill);

            // 计略（add_strategy_action）
            // 原版 :894 `base_score += get_pos_score(self, src_pos) + 500;`
            // 之后按计略类型分别加减，见 CalcOpen311StrategyScore。
            if (skill.IsStrategy())
            {
                int baseScore = paramScore + targetScore
                              + cfg.o311StrategyBaseBonus
                              + CalcOpen311PosScore(troop, spellCell ?? movetoCell);
                return CalcOpen311StrategyScore(troop, skill, target, movetoCell, spellCell, baseScore);
            }

            // 普攻（add_attack_action:404-425）
            if (skill.IsNormal())
            {
                // 【原版闸门 :404-409】`if (not is_completed_trap(target) or target == main_target)`
                // —— 已建成的陷阱默认**不生成普攻候选**，只有陷阱本身是任务主目标时才保留。
                // 原版不靠扣分压制，而是根本不入列，所以这里直接返回 -1（本项目 -1 = 丢弃该候选）。
                // 少了这一句，陷阱会以 paramScore + 动作常量 1000 + 位置分的小正分混进队列。
                if (target.building != null
                    && IsCompletedTrap(target.building)
                    && !IsTrapMainTarget(troop, target.building))
                    return -1;

                int score = paramScore + targetScore + cfg.o311AttackActionBonus;
                if (CanBeCounterattacked(troop, skill, target, movetoCell))
                    score -= cfg.o311CounterPenalty;
                int ads = CalcOpen311DamageScore(troop, skill, target, movetoCell);
                if (ads > 0)
                    score += ads + cfg.o311DamageHitBonus;

                // :416-420 对部队目标追加兵装评分（get_equipment_score）
                if (target.troop != null)
                    score += CalcOpen311EquipmentScore(troop, target.troop);

                // :421 位置分（原版 get_pos_score(self, src_pos)，src_pos = 发起格）
                score += CalcOpen311PosScore(troop, movetoCell ?? troop.cell);

                return score;
            }

            // 战法（add_tactic_action）：base + 成功率 + 每个战法各自的常量 + 伤害分
            // 原版把常量按 TacticId 硬编码（949/1049/899…），本项目由 Skill.aiActionBonus 配置，
            // 未配置（0）时回落到 o311TacticActionBonus。
            int tacticBonus = skill.aiActionBonus > 0 ? skill.aiActionBonus : cfg.o311TacticActionBonus;
            int tacticChance2 = CalcOpen311SuccessChance(troop, skill, spellCell);
            int tacticScore = paramScore + targetScore + tacticChance2 + tacticBonus
                            + CalcOpen311DamageScore(troop, skill, target, spellCell ?? movetoCell);

            // add_tactic_action：- get_pos_score(target) + get_pos_score(self)
            if (target.troop != null)
                tacticScore -= CalcOpen311PosScore(target.troop, target);
            tacticScore += CalcOpen311PosScore(troop, spellCell ?? movetoCell);

            // 【原版 :630-637 / :842-849】战法打陷阱不能靠常规伤害分判断（陷阱伤害分恒 0），
            // 必须改走 get_trap_score：
            //     action.score += get_trap_score(...);
            //     if (trap_score <= 0) action.score -= 10000;
            // 即"能炸到敌人就加分，炸不到就重罚"，避免用战法白烧一座陷阱。
            if (target.building != null && IsCompletedTrap(target.building))
            {
                // 【引爆者站位必须用落点，不能用 spellCell】
                // 原版 :632 传的是 `src_pos`（= action.src_pos，发起位置 = 移动后站位），
                // 因为 get_trap_score 用 `map_.calc_dir(unit_pos, trap_pos)` 决定火球滚动方向。
                // 本项目对"打陷阱"这类单格战法，spellCell 就等于陷阱格本身 →
                // DirectionTo 退化成固定方向，与执行层（BuildingTrapEffect.Trigger 用引爆者
                // 的 cell 算方向）不一致，会出现"AI 以为能炸到人、实际一座陷阱白烧"。
                int trapScore = CalcOpen311TrapScore(troop, target.building, movetoCell ?? troop.cell);
                if (trapScore > 0)
                    tacticScore += trapScore;
                else
                    tacticScore -= cfg.o311TrapScorePenalty;   // 原版 :633-636（>0 才加，否则只罚）
            }

            return tacticScore;
        }

        /// <summary>
        /// 【原版直译】目标分 —— get_target_score（s11_sys_unit_action.cpp:1687-1778）。
        ///
        /// 逐项：
        ///   敌对 +5000 / 友方 +1500                      :1694-1697
        ///   攻城兵器（冲车~木兽）打建筑 +2000；冲车打部队 → 0    :1699-1721
        ///   普通兵装打部队 +2000                          :1715-1720
        ///   主目标 +10000；非主目标且是友方领地的障碍 → -1    :1723-1733
        ///   部队目标：伪报 ×0.1；周围 1~2 格有本方建筑 +1375  :1735-1755
        ///   建筑目标：箭楼 / 阵 +200                       :1756-1775
        /// </summary>
        static int CalcOpen311TargetScore(Troop troop, SkillInstance skill, Cell target)
        {
            AIConfig cfg = AIConfig.Instance;
            Troop targetTroop = target.troop;
            BuildingBase targetBuilding = target.building;
            if (targetTroop == null && targetBuilding == null)
                return 0;

            int score = 0;

            // :1694-1697 敌对 / 友方
            if (targetTroop != null)
            {
                if (troop.IsEnemy(targetTroop)) score += cfg.o311TargetHostile;
                else if (targetTroop.BelongForce == troop.BelongForce) score += cfg.o311TargetFriendly;
            }
            else
            {
                // 【对齐原版 get_target_score（s11_sys_unit_action.cpp:1694-1697）】
                // 无主建筑（force_id < 0）：原版 is_enemy(-1)=false 且 is_same_force=false → +0。
                // 本项目 IsEnemy(null)=true 会把无主建筑误判为敌对 +5000，抬高"点火陷阱"等
                // 无主建筑的火计/攻击评分，故显式排除无主建筑，严格对齐原版。
                if (targetBuilding.BelongForce == null)
                {
                    // 无主建筑：不加分（原版 +0）
                }
                else if (troop.IsEnemy(targetBuilding))
                    score += cfg.o311TargetHostile;
                else if (targetBuilding.BelongForce == troop.BelongForce)
                    score += cfg.o311TargetFriendly;
            }

            // :1699-1721 兵器分支。原版 in_range(Shousha, Mokujuu) 正好是四种攻城兵器，
            // 本项目用 siegeRole != 0（Ram + Artillery）等价表达。
            TroopType tt = troop.TroopType;
            bool isSiege = tt != null && tt.siegeRole != 0;
            if (isSiege)
            {
                if (targetBuilding != null)
                    score += cfg.o311SiegeOnBuilding;
                else if (tt.siegeRole == (int)SiegeRoleType.Ram)
                    return 0;                          // :1711-1712 冲车打部队 → 直接 0
            }
            else
            {
                if (targetTroop != null)
                    score += cfg.o311NormalOnUnit;
            }

            // :1723-1733 主目标。
            // 原版的 main_target 是 MilitaryUnitObject*，既可能是部队也可能是建筑；
            // 本项目按 Id 比对（不匹配即为非主目标，安全）。
            bool isMainTarget = false;
            if (targetBuilding != null)
                isMainTarget = targetBuilding.Id == troop.missionTarget;
            else if (targetTroop != null)
                isMainTarget = targetTroop.Id == troop.missionTarget;

            // 【原版对齐 · 异族分支】异族 / 贼（非正常势力）Subjugate 时，原版在
            // run_target_action 开头执行 `if (is_city(main_target)) main_target = nullptr;`
            // （s11_sys_unit_action.cpp:65-68），把"目标城"清出主目标 —— 异族攻城时
            // **没有 +10000 的任务目标加成**，只按 get_target_score 的原生分值与其他目标竞争：
            //   能一击摧毁的据点 → get_attack_damage_score +10000/+2000
            //   敌部队          → hostile +5000 + 普通武器打部队 +2000
            //   建筑设施        → hostile +5000（Base/Den 再 +200）
            // 注意原版只清"城"（is_city），港关仍保留主目标加成，这里同样只对 IsCity() 生效。
            City mainCity = targetBuilding as City;
            if (isMainTarget && mainCity != null && mainCity.IsCity()
                && troop.BelongForce != null && !troop.BelongForce.IsNormal)
            {
                isMainTarget = false;
            }

            // 【无主据点不给主目标加成】
            // 原版的 main_target 是"作战目标"，白城（force_id < 0）已经不属于任何人的作战范畴；
            // 而本项目 CityAI 会把无主据点当高优先级目标下发（CityAI.cs:668 权重 9999），
            // 一旦部队的 missionTarget 指向某座白城 / 空港，+o311TargetMain 会让它**永远**
            // 被评为最优攻击格。但 City.ChangeTroops 对无主城是直接 return false
            // （City.cs:2365-2367「白城直接占领」），既不扣兵力也不触发 OnFall ——
            // 部队就会原地反复攻击同一格，玩家看到的就是"对着空港口鞭尸"。
            // 去掉这份加成后，白城回落成"基础分 + 0"，AI 会自然转向还有主的目标。
            if (isMainTarget && targetBuilding != null && targetBuilding.BelongForce == null)
            {
                isMainTarget = false;
            }

            if (isMainTarget)
            {
                score += cfg.o311TargetMain;
            }
            else if (targetBuilding != null && targetBuilding.BuildingType != null
                     && targetBuilding.BuildingType.IsObstacle
                     && IsInFriendArea(troop, targetBuilding))
            {
                // 原版 get_target_score:1731-1732：`is_completed_obstacle && is_in_friend_area → -1`
                return -1;
            }

            // 【原版 s11_sys_unit_action.cpp:261-267】非主目标再次过滤：
            //   `if (is_base_in_friend_area(self, target)) continue;`
            // 语义见 IsBaseInFriendArea 的注释：自家领土上的**异族根城**不拆（贼根城照常）。
            if (!isMainTarget && IsBaseInFriendArea(troop, targetBuilding))
                return -1;

            // :1735-1755 部队目标
            if (targetTroop != null)
            {
                // :1739-1741 伪报目标 → ×0.1
                if (targetTroop.IsEscapeState())
                    score = score * 10 / 100;

                // :1742-1754 周围 1~2 格有本方建筑 → +1375
                if (HasFriendlyBuildingNear(troop, targetTroop.cell, 2))
                    score += cfg.o311NearFriendlyBuilding;
            }
            else
            {
                // :1756-1775 建筑目标：原版**只有** FacilityId_Base / Den（异族 / 贼根城）额外 +200，
                // 箭楼 / 阵 / 投石台等其它设施没有这一项（它们只拿上面的 hostile +5000）。
                // 本项目 kind=15/16 就是这两种根城（BuildingKindType.Base / Base2）。
                BuildingType bt = targetBuilding.BuildingType;
                if (bt != null && (bt.kind == (byte)BuildingKindType.Base || bt.kind == (byte)BuildingKindType.Base2))
                    score += cfg.o311BaseDenBonus;
            }

            return score;
        }

        /// <summary>
        /// 原版 <c>is_in_friend_area</c>（s11_sys_unit_utils.cpp:612-622）：
        /// 目标所在**领地**的核心据点（<c>get_core_building</c>）与 self 是友方。
        ///
        /// 原版逐行：
        /// <code>
        /// const Building* core = get_core_building(target);    // 目标格所属领地核心据点（area_building_id）
        /// return is_friend(self->get_force_id(), core->get_force_id());
        /// </code>
        ///
        /// 其中 <c>is_friend</c>（s11_sys_scen_force.cpp:904-914）= 同势力 / 同盟（is_ally）/ 停战（ceasefire_timer）。
        ///
        /// 【与 is_base_in_friend_area 的区别】is_base_in_friend_area 用 <c>not is_enemy(core)</c>（只排除敌对），
        /// 本函数用 <c>is_friend</c>（正向友方，含停战），二者对"中立 / 无主"领地的判定不同。
        ///
        /// 调用点是 <c>get_target_score</c> 的"非主目标障碍物 → -1"分支（s11_sys_unit_action.cpp:1731-1732）。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="target">目标建筑（可为 null）</param>
        /// <returns>目标所在领地核心据点是友方返回 true</returns>
        static bool IsInFriendArea(Troop troop, BuildingBase target)
        {
            if (troop == null || target == null)
                return false;

            Building building = target as Building;
            if (building == null)
                return false;

            // 领地核心据点：原版 get_core_building → get_area_building_id(false)
            City area = BanditCampPlacer.GetAreaCity(building);
            if (area == null || area.BelongForce == null)
                return false;       // 无主领地不算友方

            Force selfForce = troop.BelongForce;
            if (selfForce == null)
                return false;

            // is_friend（s11_sys_scen_force.cpp:904-914）：同势力 / 同盟 / 停战
            if (selfForce == area.BelongForce)
                return true;
            if (selfForce.IsAlliance(area.BelongForce))
                return true;
            if (selfForce.IsTruce(area.BelongForce))
                return true;
            return false;
        }

        /// <summary>
        /// 原版 <c>is_base_in_friend_area</c>（s11_sys_unit_utils.cpp:624-637）：
        /// **异族根城**落在友方 / 己方领土内时，部队不把它当攻击目标。
        ///
        /// 原版逐行：
        /// <code>
        /// if (building->get_facility_id() != FacilityId_Base) return false;   // 只认异族根城
        /// Building* core = get_building(building->get_area_building_id());     // 领地核心据点
        /// return not unit->is_enemy(core);
        /// </code>
        ///
        /// 【设计意图】异族老巢长在你自己的领地里，**自家部队不去清剿**（它就是个持续骚扰源）；
        /// 只有别的势力打进来时才会顺手拆掉。贼根城（FacilityId_Den，本项目 kind=16）没有这条豁免。
        ///
        /// 调用点是 <c>get_target_action</c> 的非主目标分支（s11_sys_unit_action.cpp:266）——
        /// 任务指定主目标时仍然照打，不受此限。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="target">目标建筑（可为 null）</param>
        /// <returns>是"友方领土上的异族根城"返回 true</returns>
        static bool IsBaseInFriendArea(Troop troop, BuildingBase target)
        {
            if (troop == null || target == null)
                return false;

            BuildingType type = target.BuildingType;
            if (type == null || type.kind != (byte)BuildingKindType.Base)
                return false;       // 只认 kind=15（异族根城）；16（贼根城）不豁免

            Building building = target as Building;
            if (building == null)
                return false;

            // 领地核心据点：原版 building->get_area_building_id()
            City area = BanditCampPlacer.GetAreaCity(building);
            if (area == null || area.BelongForce == null)
                return false;       // 无主领地不算友方

            Force selfForce = troop.BelongForce;
            if (selfForce == null)
                return false;

            return !selfForce.IsEnemy(area.BelongForce);
        }

        /// <summary>
        /// 【原版直译】属性分 —— param.attack_score / tactic_score / strategy_score
        /// （s11_sys_unit_action.cpp:18-40）。
        ///
        ///   普攻 / 战法：统率 + 武力
        ///   计略：智力
        ///   性格修正：**不查原版 Seikaku 枚举**，改为读性格上的权重字段
        ///   （strategyIntelligencePercent / attackExtraCommandPercent / attackExtraStrengthPercent），
        ///   这样新增任何性格只需要填数据，不必映射到原版的四档。
        ///
        /// 【原版等价】对应 s11_sys_unit_action.cpp:27-40：
        ///   小心 → 计略再 +智力        ⇔ strategyIntelligencePercent = 200
        ///   猪突 → 普攻/战法再 +(统率+武力) ⇔ extraCommand = 100, extraStrength = 100
        ///   强胆 → 普攻/战法再 +武力    ⇔ extraStrength = 100
        ///   冷静 → 无额外加成           ⇔ 三项都是默认值
        /// </summary>
        static int CalcOpen311ParamScore(Troop troop, SkillInstance skill)
        {
            int leadership = troop.Command;            // 统率
            int strength = troop.Strength;
            int intelligence = troop.Intelligence;

            Personality personality = troop.LeaderPersonality;

            if (skill.IsStrategy())
            {
                // :18-20 基础是 1 次智力，性格按百分比追加
                int percent = personality != null ? personality.strategyIntelligencePercent : 100;
                if (percent < 0) percent = 0;
                return intelligence * percent / 100;
            }

            // :18-19 基础是 统率 + 武力
            int commandPercent = 100 + (personality != null ? personality.attackExtraCommandPercent : 0);
            int strengthPercent = 100 + (personality != null ? personality.attackExtraStrengthPercent : 0);
            if (commandPercent < 0) commandPercent = 0;
            if (strengthPercent < 0) strengthPercent = 0;

            return leadership * commandPercent / 100 + strength * strengthPercent / 100;
        }

        /// <summary>
        /// 【原版直译】伤害分 —— get_attack_damage_score（s11_sys_unit_action.cpp:1780-1834）。
        ///
        /// 部队：击破 → 2000；打掉一半 → 1000；否则 0
        /// 据点：一击破 → 近战 10000 / 远程 2000；兵力伤害不足 1/3 且耐久不足 1/3 → 0
        /// 陷阱 / 障碍 → 0；普通设施一击不破 → 0
        /// </summary>
        static int CalcOpen311DamageScore(Troop troop, SkillInstance skill, Cell target, Cell fromCell)
        {
            AIConfig cfg = AIConfig.Instance;

            if (target.troop != null)
            {
                int dmg = Troop.CalculateSkillDamage(troop, target.troop, skill);
                if (dmg >= target.troop.troops) return cfg.o311DamageDefault;
                if (dmg * 2 >= target.troop.troops) return cfg.o311DamagePartial;
                return 0;
            }

            BuildingBase building = target.building;
            if (building == null) return 0;

            City city = building as City;
            if (city != null && (city.IsCity() || city.IsGate() || city.IsPort()))
            {
                int dmgTroops = Troop.CalculateSkillDamageTroopOnCity(troop, city, skill);
                int dmgHp = Troop.CalculateSkillDamage(troop, building, skill);

                if (dmgTroops >= city.troops || dmgHp >= building.durability)
                {
                    // 原版 is_neighbor(src_pos, target_pos)：src_pos 是发起格（移动后的落点）
                    bool neighbor = fromCell != null && Scenario.Cur.Map.Distance(fromCell, target) <= 1;
                    return neighbor ? cfg.o311DamageKillMelee : cfg.o311DamageKillRanged;
                }

                if (dmgTroops * 3 < city.troops)
                    return dmgHp * 3 > building.durability ? cfg.o311DamageDefault : 0;

                return cfg.o311DamageDefault;
            }

            BuildingType bt = building.BuildingType;
            if (bt != null && (bt.IsExplosive || bt.IsObstacle))
                return 0;

            int hpDmg = Troop.CalculateSkillDamage(troop, building, skill);
            return hpDmg >= building.durability ? cfg.o311DamageDefault : 0;
        }

        /// <summary>
        /// 【原版直译】兵装评分 —— get_equipment_score（s11_sys_unit_action.cpp:1626-1685）。
        ///
        ///   ① 自身兵种适性：S 档 +100 / A 档 +50 / B 档 +25
        ///   ② 兵种相克：枪克骑 +100、枪遇戟 -100；戟克枪 +100、戟遇骑 -100；骑克戟 +100、骑遇枪 -100
        ///   ③ 剑兵 / 走舸打"非剑兵、非攻城"目标 → -150
        ///   ④ 目标若是剑兵 / 攻城兵器 → +100
        ///   ⑤ 自己不是攻城兵器、目标却是攻城兵器 → +1000（优先殴打攻城器械）
        ///
        /// 【映射】原版 EquipmentId → 本项目 TroopType.kind：
        ///   剑兵=1、枪兵=2、戟兵=3、弩兵=4、骑兵=5、水军=6、运输=7、冲车/木兽=8、井阑/投石=9
        ///   其中 8/9 视为"攻城兵器"（原版 Shousha~Mokujuu），6 视为"走舸/楼船"。
        /// 【适性】原版 Tekisei 五档，本项目 LandTroopTypeLv 0~4；按 4=S、3=A、2=B 折算。
        /// </summary>
        static int CalcOpen311EquipmentScore(Troop troop, Troop target)
        {
            if (troop == null || target == null || troop.TroopType == null || target.TroopType == null)
                return 0;

            int score = 0;

            // ① 适性
            int lv = troop.LandTroopTypeLv;
            if (lv >= 4) score = 100;
            else if (lv == 3) score = 50;
            else if (lv == 2) score = 25;

            int selfKind = troop.TroopType.kind;
            int targetKind = target.TroopType.kind;

            // ② 兵种相克
            if (selfKind == 2)                 // 枪
            {
                if (targetKind == 5) score += 100;
                else if (targetKind == 3) score -= 100;
            }
            else if (selfKind == 3)            // 戟
            {
                if (targetKind == 2) score += 100;
                else if (targetKind == 5) score -= 100;
            }
            else if (selfKind == 5)            // 骑
            {
                if (targetKind == 3) score += 100;
                else if (targetKind == 2) score -= 100;
            }
            else if (selfKind == 1 || selfKind == 6)   // 剑兵 / 走舸
            {
                bool targetIsSwordOrSiege = targetKind == 1 || targetKind == 6 || targetKind >= 8;
                if (!targetIsSwordOrSiege) score -= 150;
            }

            // ④ 目标为剑兵 / 攻城兵器
            bool targetSwordOrSiege2 = targetKind == 1 || targetKind == 6 || targetKind >= 8;
            if (targetSwordOrSiege2) score += 100;

            // ⑤ 我方普通部队 打 敌方攻城兵器
            bool selfSiege = selfKind >= 8;
            bool targetSiege = targetKind >= 8;
            if (!selfSiege && targetSiege) score += 1000;

            return score;
        }

        /// <summary>
        /// 【原版直译】位置分 —— get_pos_score（s11_sys_unit_action.cpp:1452-1471）。
        ///
        ///   score = 设施分 + 地形分 - 相邻友军数 × 100 - (着火 ? 750 : 0)
        ///
        /// 组成（全部已实现）：
        ///   · 设施分 —— <see cref="CalcOpen311AroundFacilitiesScore"/>（读 BuildingType.posScoreKind）
        ///   · 地形分 —— <see cref="CalcOpen311TerrainScore"/>（读 TerrainType.posKind）
        ///   · 相邻友军惩罚、所在格着火惩罚
        /// </summary>
        static int CalcOpen311PosScore(Troop troop, Cell pos)
        {
            if (troop == null || pos == null) return 0;
            AIConfig cfg = AIConfig.Instance;

            // :1454 设施分 + :1455 地形分
            int score = CalcOpen311AroundFacilitiesScore(troop, pos) + CalcOpen311TerrainScore(troop, pos);

            // :1456-1465 相邻友军部队，每个 -100（原版 MapRingIterator pos 1，只算紧邻一圈）
            int ownNeighbor = 0;
            for (int i = 0; i < 6; i++)
            {
                Cell neighbor = pos.GetNeighbor(i);
                if (neighbor == null || neighbor.troop == null) continue;
                if (neighbor.troop.BelongForce == troop.BelongForce) ownNeighbor++;
            }
            score -= ownNeighbor * cfg.o311PosNeighborOwnUnitPenalty;

            // :1467-1469 所在格着火
            if (pos.fire != null)
                score -= cfg.o311PosFirePenalty;

            return score;
        }

        /// <summary>
        /// 【原版直译】可燃的**空格**能否作为火计目标 —— get_enabled_strategies 火计分支
        /// （s11_sys_unit_target.cpp:364-380）。
        ///
        /// 原版：
        ///   · 格子上有对象 → 需 <c>can_attack</c> 且不是未完工的堤防；
        ///   · 格子上没有对象 → 只看地形 <c>is_firable_terrain(get_terrain_id())</c>。
        ///
        /// 【为什么需要这个函数】技能配的 <c>canSpellToCell = true</c> 只回答"能不能对该格施法"
        /// （<see cref="SkillInstance.CanSpellToHere"/> 的判定层），**不代表 AI 会把它生成候选**：
        /// 候选生成另有 `cache.targets.Length == 0 → continue` 这道门槛（命中格内必须有部队或建筑），
        /// 所以火计对空地此前在 AI 侧永远不成立，只有玩家手动能点。本函数把这道门槛按原版补开。
        ///
        /// 【地形等价】<see cref="TerrainType.fireRate"/> &gt; 0 且非水域，与 <see cref="Fire.CanIgnite"/>
        /// （火焰能否蔓延到该格）同口径，保证"AI 认为能烧的地"与"火真的能烧起来的地"一致。
        /// 与 <see cref="Fire.CanIgnite"/> 不同的是**不排除已着火的格子**：原版对已着火空地照样开启
        /// 火计（:1028-1030 只做 -250 降权），本项目 SetFire 对已有火焰会重置 counter（续火），有意义。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">待评估技能</param>
        /// <param name="spellCell">施法格（单格计略里即目标格）</param>
        /// <returns>true = 允许作为候选</returns>
        static bool IsEmptyCellFireCandidate(Troop troop, SkillInstance skill, Cell spellCell)
        {
            if (troop == null || skill == null || spellCell == null)
                return false;

            if ((StrategyType)skill.strategyType != StrategyType.Fire)
                return false;

            // 有部队 / 建筑时走正常的目标分支（那里有归属与陷阱判定）
            if (!spellCell.IsEmpty())
                return false;

            TerrainType terrain = spellCell.TerrainType;
            if (terrain == null || terrain.isWater)
                return false;

            return terrain.fireRate > 0;
        }

        /// <summary>
        /// 【原版直译】空着的着火格能否作为消火候选 —— get_after_move_action（s11_sys_unit_action.cpp:340-344）
        /// 与 add_strategy_action 的空坐标分支（:956-990）。
        ///
        /// 原版条件（全部满足才入列）：
        ///   ① 该格是空格（无部队 / 建筑）—— 否则走正常的目标分支；
        ///   ② 有一团**还会继续烧**的火：<c>hex.get_fire_timer() &gt; 1</c>（本项目 <see cref="Fire.counter"/> 同义）；
        ///   ③ 能量够双倍消耗：<c>get_energy() &gt;= cost * 2</c>，
        ///      运输队 / 撤退 / 建设 / 修理 / 移动到建筑的部队豁免（:961-971）；
        ///   ④ 目标格周围 1 环内至少有一个非空对象（:977-988），避免 AI 在荒野凭空灭火。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="skill">待评估技能</param>
        /// <param name="spellCell">施法格（单格计略里即目标格）</param>
        /// <returns>true = 允许作为候选</returns>
        static bool IsEmptyFireCellShoukaCandidate(Troop troop, SkillInstance skill, Cell spellCell)
        {
            if (troop == null || skill == null || spellCell == null)
                return false;

            // 原版的 self_shouka 只喂给 add_strategy_action 的消火分支，其它计略不适用
            if ((StrategyType)skill.strategyType != StrategyType.Shouka)
                return false;

            // ① 空格：有部队 / 建筑时由正常的目标分支处理
            if (!spellCell.IsEmpty())
                return false;

            // ② 火还在烧（原版 fire_timer > 1）
            Fire fire = spellCell.fire;
            if (fire == null || fire.counter <= 1)
                return false;

            // ④ 周围 1 环内至少有一个非空对象
            bool hasNeighborObject = false;
            for (int i = 0; i < 6; i++)
            {
                Cell neighbor = spellCell.GetNeighbor(i);
                if (neighbor != null && !neighbor.IsEmpty())
                {
                    hasNeighborObject = true;
                    break;
                }
            }
            if (!hasNeighborObject)
                return false;

            // ③ 双倍气力门槛（豁免见 IsShoukaCostFree）。
            // 【变量映射】原版 get_energy() 是"气力"，本项目对应 **morale**：
            // 施法消耗同样走 morale（SkillInstance.cs:999 `troop.ChangeMorale(-costEnergy)`），
            // 而 Troop.energy 是"行动力"，不是计略资源。
            if (!IsShoukaCostFree(troop) && troop.morale < skill.costEnergy * 2)
                return false;

            return true;
        }

        /// <summary>
        /// 【原版直译】消火的"免气力门槛"命令集合 —— add_strategy_action（s11_sys_unit_action.cpp:961-971）。
        ///
        /// 原版把以下状态的 cost 归零，即不受"气力 &gt;= 双倍消耗"限制：
        /// <c>UnitType_Transport</c> / <c>UnitOrderId_Retreat</c> / <c>UnitOrderId_Build</c> /
        /// <c>UnitOrderId_Repair</c> / <c>UnitOrderId_Move + order_target_type == Building</c>。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <returns>true = 不受双倍能量门槛限制</returns>
        static bool IsShoukaCostFree(Troop troop)
        {
            if (troop == null) return false;
            if (troop.IsTransport) return true;                 // UnitType_Transport

            switch ((MissionType)troop.missionType)
            {
                case MissionType.TroopReturnCity:                   // UnitOrderId_Retreat
                case MissionType.TroopBuildBuilding:                // UnitOrderId_Build
                case MissionType.TroopFixBuilding:                  // UnitOrderId_Repair
                case MissionType.TroopMovetoCity:                   // UnitOrderId_Move（目标为建筑）
                case MissionType.TroopMovetoBuild:                  // 同上（本项目拆出的专用任务）
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 【原版直译】计略效果分 —— add_strategy_action（s11_sys_unit_action.cpp:883-1225）。
        ///
        /// 原版按 StrategyId 逐块判断，每块结构一致：
        ///     ① chance = calc_strategy_chance(...)
        ///     ② score = base_score + chance（或 + 常量）
        ///     ③ 若 chance == 0 或 chance &gt;= 35 才继续做目标相关的加减
        ///     ④ score &gt; 0 才入列
        /// 本项目用 Skill.strategyType 代替 StrategyId 做分支。
        /// </summary>
        static int CalcOpen311StrategyScore(Troop troop, SkillInstance skill, Cell target, Cell movetoCell, Cell spellCell, int baseScore)
        {
            AIConfig cfg = AIConfig.Instance;
            int chance = CalcOpen311SuccessChance(troop, skill, spellCell);

            Troop targetTroop = target != null ? target.troop : null;
            BuildingBase targetBuilding = target != null ? target.building : null;

            // 原版 :1004-1095 / :1098-... 的通用门槛：成功率 0 或 >= 35 才评估
            bool chancePass = chance == 0 || chance >= cfg.o311StrategyChanceThreshold;

            switch ((StrategyType)skill.strategyType)
            {
                // ---------- 消火（:902-991） ----------
                case StrategyType.Shouka:
                    {
                        // 【对齐原版 get_enabled_strategies 消火命令（s11_sys_unit_target.cpp:382-389）】
                        //   `if (not is_alive(target_object) or not self->hostile_target(target_object))
                        //        flags[StrategyId_Shouka] = true;`
                        // 原版对**敌对目标**根本不开启消火命令，所以绝不会替敌人灭火。
                        //
                        // 本项目这条闸门缺在施法判定层：技能「灭火」配置为 canSpellToCell = true
                        // （Skills.json:946），而 SkillInstance.CanSpellToHere 在 canSpellToCell 为真时
                        // 直接 return true（SkillInstance.cs:449-452），把 onlySpellToTeam 一起短路掉；
                        // 唯一的前置 CheckFire 又只判 `where.fire != null`（SkillSpellConditionMethod.cs:62-70），
                        // 不看归属。于是"敌方着火建筑 / 敌方着火部队"全都成了合法候选格。
                        // 故必须在评分阶段补上归属闸门。
                        if (targetTroop != null && troop.IsEnemy(targetTroop))
                            return 0;
                        // 无主建筑 BelongForce == null，原版 hostile_target 为假 → 允许（不拦）
                        if (targetBuilding != null
                            && targetBuilding.BelongForce != null
                            && troop.IsEnemy(targetBuilding))
                            return 0;

                        // 【对齐原版 s11_sys_unit_action.cpp:909-913】
                        //   `if (src_pos == node->pos and not self->has_skill(SkillId_Kashin))
                        //        action.score = 20000;`
                        // 语义是"**施法者自己站在着火格上**"，先灭自己脚底的火。
                        //
                        // 【变量映射】本项目 src_pos ↔ movetoCell（移动落点 = 施法者站位），
                        // node->pos ↔ target（单格计略里命中格就是施法格）。
                        // 原写法 `target == spellCell` 是错的：灭火没有 atkOffsetPoint，
                        // GetAttackCells 直接把 spellCell 当命中格返回（Skill.cs:237-242），
                        // 所以 `target == spellCell` **恒成立** → 任何着火格一律 20000 分，
                        // 稳压普攻 / 攻城（数千~一万），AI 才会丢下城池跑去灭火。
                        //
                        // 【候选来源】灭火的 spellRanges 已配为 [ 0, 1, 2 ]（Skills.json:929-933）：
                        // GetRing(0) 补上落点自身格（Map.cs:408-414 只有 radius == 0 才加自身），
                        // GetRing(1)/GetRing(2) 给出相邻两环 —— 基础射程 2 格对齐原版
                        // `distance <= 2 + kibou`（s11_sys_unit_target.cpp:384；kibou 特技加成由
                        // TroopChangeSkillSpellRange 追加，见 Features.json 鬼谋 #66）。
                        // 而 GetMoveRange 首行即把当前格加入移动范围（Map.cs:561），
                        // 所以 `movetoCell == troop.cell` 的原地分支是可达的。
                        // 注意 0 必须排在数组最前：TroopAddSkillSpellRange 等都取
                        // spellRanges[Length - 1] 作为最大值往后追加（TroopAddSkillSpellRange.cs:34）。
                        if (target != null && target.fire != null
                            && movetoCell != null && movetoCell == target
                            && !HasTroopFeature(troop, FeatureId_Kashin))
                            return cfg.o311StrategyShoukaSelf;

                        // 友方建筑
                        if (targetBuilding != null && targetBuilding.BelongForce == troop.BelongForce)
                        {
                            int bonus;
                            if (IsKyoten(targetBuilding)) bonus = cfg.o311StrategyShoukaKyoten;
                            else if (IsCampType(targetBuilding)) bonus = cfg.o311StrategyShoukaCamp;
                            else bonus = cfg.o311StrategyShoukaOther;
                            return baseScore + bonus;
                        }

                        // 友方部队（只对已行动完的部队消火，原版 :948-953）
                        if (targetTroop != null && targetTroop.BelongForce == troop.BelongForce)
                            return targetTroop.ActionOver ? baseScore + cfg.o311StrategyShoukaKyoten : 0;

                        // 空坐标：只有该格**确实着火**才消（原版 hex.get_fire_timer()）
                        if (target != null && target.IsEmpty())
                            return target.fire != null ? baseScore : 0;
                        return 0;
                    }

                // ---------- 火计（:1004-1095） ----------
                case StrategyType.Fire:
                    {
                        if (!chancePass) return 0;

                        int score = baseScore + chance;

                        // :1016-1025 目标本身是陷阱 → 调 get_trap_score（:1019）
                        // 判定口径与战法分支保持一致：IsCompletedTrap（= 原版 is_completed_trap，
                        // 排除堤防、要求已建成），未建成的陷阱由 CalcOpen311TrapScore 返回 0 后同样重罚。
                        if (targetBuilding != null && IsCompletedTrap(targetBuilding))
                        {
                            int trapScore = CalcOpen311TrapScore(troop, targetBuilding, movetoCell ?? troop.cell);
                            score += trapScore;
                            // :1021-1022 陷阱评分 <= 0 时重罚（烧了不划算，甚至有害）
                            if (trapScore <= 0) score -= cfg.o311TrapScorePenalty;
                            return score > 0 ? score : 0;
                        }

                        // :1028-1030 目标格已经着火
                        if (target != null && target.fire != null) score -= 250;

                        if (targetTroop != null)
                        {
                            // 【对齐原版 get_enabled_strategies 火计命令（s11_sys_unit_target.cpp:364-380）】
                            // 火计只对 can_attack 的目标开启；can_attack 对部队 = is_enemy
                            // （s11_scen_obj_unit.cpp:342-352）。友军 / 同盟部队不可作为火计目标，
                            // 原版根本不会为该格开启火计命令，这里直接丢弃，否则 AI 会对自己人放火。
                            if (!troop.IsEnemy(targetTroop))
                                return 0;

                            // :1035-1039 混乱 +250；伪报 → 0
                            if (targetTroop.IsEscapeState()) return 0;
                            if (targetTroop.HasControlBuff()) score += 250;
                        }
                        else if (targetBuilding != null)
                        {
                            // 【对齐原版 can_attack / hostile_target（s11_scen_obj_unit.cpp:329-354）】
                            // can_attack 建筑 = force_id < 0 || is_enemy；hostile_target 建筑 = is_enemy。
                            // 只有 is_enemy（敌对）才加分；无主建筑（can_attack true 但非敌）不额外加分；
                            // 友军 / 同盟建筑（can_attack false）命令根本不开，绝不烧。
                            if (targetBuilding.BelongForce != null && troop.IsEnemy(targetBuilding))
                            {
                                // :1048-1049 high_hp = 耐久 >= 400 且 耐久 >= 上限 × 2/3
                                int durLimit = targetBuilding.DurabilityLimit;
                                bool highHp = targetBuilding.durability >= 400
                                           && durLimit > 0 && targetBuilding.durability >= durLimit * 2 / 3;

                                if (IsKyoten(targetBuilding))
                                {
                                    // :1056-1061 城关港：火计更有效才加分，否则倒扣
                                    if (CalcOpen311IsFireRecommended(troop, targetBuilding))
                                        score += highHp ? 375 : 250;
                                    else
                                        score -= 125 + chance;
                                }
                                else if (IsCampType(targetBuilding))
                                    score += highHp ? 250 : 174;      // :1062-1066
                                else
                                    score += highHp ? 75 : 50;        // :1067-1069
                            }
                            else if (targetBuilding.BelongForce == null)
                            {
                                // 无主建筑（原版 can_attack = force_id < 0，但 hostile_target 非敌）
                                if (IsKyoten(targetBuilding))
                                    return 0;   // :1079-1080 中立据点不烧
                                // 其他无主建筑：原版不加分（score 保持 base + chance）
                            }
                            else
                            {
                                // 友军 / 同盟建筑：原版 can_attack = false，命令不开 → 绝不烧
                                return 0;
                            }
                        }
                        return score > 0 ? score : 0;
                    }

                // ---------- 伪报（:1098-1128） ----------
                case StrategyType.Gihou:
                    {
                        if (!chancePass) return 0;

                        int score = baseScore + chance;
                        MissionType mt = (MissionType)troop.missionType;

                        // 防御 / 建设中的部队，对兵力 1000 以上的目标伪报更划算
                        if ((mt == MissionType.TroopProtectCity || mt == MissionType.TroopBuildBuilding)
                            && targetTroop != null && targetTroop.troops > 1000)
                            score += 125;

                        // 自己在进攻中则伪报意义不大
                        if (IsOnAttack(troop)) score -= 5000;

                        // 目标已非正常状态、或是运输队 → 放弃
                        if (targetTroop != null && (targetTroop.HasControlBuff() || targetTroop.IsTransport))
                            return 0;

                        return score > 0 ? score : 0;
                    }

                // ---------- 混乱（:1131-1143） ----------
                case StrategyType.Kakuran:
                    if (!chancePass) return 0;
                    if (targetTroop != null && !targetTroop.HasControlBuff()) return baseScore;
                    return 0;

                // ---------- 镇静（:1146-1152） ----------
                case StrategyType.Chinsei:
                    return baseScore + chance;

                // ---------- 伏兵（:1155-1168） ----------
                case StrategyType.Fukuhei:
                    if (!chancePass) return 0;
                    return baseScore + chance + cfg.o311StrategyFukuheiBonus;

                // ---------- 同讨（:1171-1187） ----------
                case StrategyType.Doushiuchi:
                    {
                        if (!chancePass) return 0;
                        int score = baseScore + chance + cfg.o311StrategyDoushiuchiBonus;
                        // 施法部队有「神算」或「深谋」
                        if (HasTroopFeature(troop, FeatureId_Shinsan) || HasTroopFeature(troop, FeatureId_Shinbou))
                            score += cfg.o311StrategyDoushiuchiSkillBonus;
                        return score;
                    }

                // ---------- 妖术（:1190-1203） ----------
                case StrategyType.Yojutsu:
                    {
                        if (!chancePass) return 0;

                        // 【命中己方 → 大负分】
                        // 妖术是 Spiral 半径 1 的 AOE（Skills.json Id=30 atkOffsetPoint[0]=5），
                        // 执行层对 canDamageTeam 的友军目标是照打的：
                        //     SkillInstance.cs:760  `troop.IsEnemy(beAtkTroop) || canDamageTeam`
                        //     SkillInstance.cs:862  `troop.IsEnemy(beAtkBuildingBase) || canDamageTeam`
                        //     SkillInstance.cs:1269/:1277 计略效果落地同款判断
                        // 原版 can_attack 对友军建筑 / 部队一律 false（s11_scen_obj_unit.cpp:329-354），
                        // AI 根本拿不到这种落点；本项目把"误伤"做成了玩法，就必须在评分里把代价算清楚 ——
                        // 否则 AI 会挑"以自家市场 / 农田为中心"的落点，一次扣 5 点耐久。
                        //
                        // 每个命中的己方格各扣一次（上层按格聚合累加），
                        // 所以"AOE 扫到一片自家设施"会得到数倍惩罚，足以压过 +o311TargetMain(10000)。
                        // 注意 IsEnemy 对无主返回 true（SangoObject.IsEnemy），中立目标不算误伤。
                        if (targetTroop != null && !troop.IsEnemy(targetTroop))
                            return -cfg.o311SelfDamagePenalty;
                        if (targetBuilding != null && !troop.IsEnemy(targetBuilding))
                            return -cfg.o311SelfDamagePenalty;

                        return baseScore + chance;
                    }

                // ---------- 落雷（:1206-1224） ----------
                case StrategyType.Rakurai:
                    {
                        int posScore = CalcOpen311RakuraiPosScore(troop, target);
                        if (posScore <= 0) return 0;
                        if (!chancePass) return 0;

                        int score = baseScore + chance + posScore;
                        // 【原版 :1218-1219】`int cost = get_strategy_energy_cost(Rakurai) * 2;
                        // if (self->get_energy() > cost) ...`
                        // 原版 get_energy() 是"气力"，本项目对应 **morale**（施法扣的是
                        // troop.ChangeMorale(-costEnergy)，见 SkillInstance.cs:999）；
                        // Troop.energy 是"行动力"，不是计略资源。
                        // 比较符保持原版的严格大于（>），不是 >=。
                        int cost = skill.costEnergy * 2;
                        if (troop.morale > cost) score += posScore * 2;
                        return score;
                    }
            }

            // 未配置计略类型：退回通用式（成功率 + 常量）
            return baseScore + chance + cfg.o311StrategyActionBonus;
        }

        /// <summary>
        /// 【原版直译】落雷目标位置分 —— calc_rakurai_target_pos_score（:1590-1624）。
        ///   目标格：敌对 +1 / 友方 -2；周围 6 格：敌对 +1 / 友方 -1
        ///   若施法者"莽"（智力低 / 强胆猪突 / 武力≥智力）且总分只有 1 → 放弃
        ///   正分平方后 × 500 / 4
        /// </summary>
        static int CalcOpen311RakuraiPosScore(Troop troop, Cell pos)
        {
            if (pos == null) return 0;

            int score = 0;

            // 目标格自身：敌对 +1 / 友方 -2（原版 :1596-1599，比周围格更重）
            // 【必须用 hostile_target / friendly_target】原版这两个判定对无主对象
            // （force_id < 0）一律 false，而本项目 IsEnemy 对 null 势力返回 true ——
            // 直接套用会让 AI 把"砸中立陷阱 / 中立设施"算成 +1 收益，白放一次落雷。
            if (pos.troop != null)
            {
                if (IsHostileTarget(troop, pos.troop)) score += 1;
                else if (IsFriendlyTarget(troop, pos.troop)) score -= 2;
            }
            else if (pos.building != null)
            {
                if (IsHostileTarget(troop, pos.building)) score += 1;
                else if (IsFriendlyTarget(troop, pos.building)) score -= 2;
            }

            for (int i = 0; i < 6; i++)
            {
                Cell n = pos.GetNeighbor(i);
                if (n == null) continue;
                if (n.troop != null)
                {
                    if (IsHostileTarget(troop, n.troop)) score += 1;
                    else if (IsFriendlyTarget(troop, n.troop)) score -= 1;
                }
                else if (n.building != null)
                {
                    if (IsHostileTarget(troop, n.building)) score += 1;
                    else if (IsFriendlyTarget(troop, n.building)) score -= 1;
                }
            }

            int intelligence = troop.Intelligence;
            int strength = troop.Strength;
            bool reckless = intelligence < 70 || IsAggressiveLeader(troop)
                            || (!(intelligence >= 90 && troop.troops <= 1000) && strength >= intelligence);
            if (reckless && score == 1) return 0;

            if (score > 0) score = score * score;
            return 500 * score / 4;
        }

        /// <summary>领队是否为"莽撞型"（原版强胆 / 猪突，本项目按性格三分档或好战度近似）</summary>
        static bool IsAggressiveLeader(Troop troop)
        {
            Personality personality = troop.LeaderPersonality;
            if (personality == null) return false;
            // 原版按 Seikaku_Goutan ~ Seikaku_Chototsu 判定，本项目读性格配置的标记位，
            // 不再依赖性格枚举取值 —— 新增性格只要勾上 isReckless 即可。
            return personality.isReckless;
        }

        /// <summary>
        /// 取建筑的火势系数 —— 对应原版 get_fire_facility_coef（s11_sys_anim_unit_strategy.cpp:493-562）。
        ///
        /// 优先读 <see cref="BuildingType.fireCoef"/>（数据驱动，可覆盖）；
        /// 未配置（0）时按 <see cref="BuildingKindType"/> 回落到原版分组：
        ///   10 城/关/小城/石兵 · 20 土垒/堤防 · 25 砦 · 30 港/连弩楼/投石台/太鼓台/军乐台/生产设施
        ///   35 阵 · 40 箭楼/市场系/兵舍系/各府 · 100 火种类
        ///
        /// 【为什么需要 JSON 覆盖】阵/砦/城塞三者 kind 都是 4，但原版系数分别是 35/25/10；
        /// 土垒/石墙 kind 都是 9，原版是 20/10 —— 光看 kind 无法区分。
        /// </summary>
        public static int GetFireFacilityCoef(BuildingType type)
        {
            if (type == null) return 0;
            if (type.fireCoef != 0) return type.fireCoef;

            switch ((BuildingKindType)type.kind)
            {
                case BuildingKindType.City:
                case BuildingKindType.Gate:
                case BuildingKindType.LittleCity:
                case BuildingKindType.Golems:
                    return 10;

                case BuildingKindType.Port:
                case BuildingKindType.CatapultTower:
                case BuildingKindType.DrumTower:
                case BuildingKindType.BandTower:
                    return 30;

                case BuildingKindType.Camp:
                    return 35;

                case BuildingKindType.ArrowTower:
                    return 40;

                case BuildingKindType.Wall:
                case BuildingKindType.Levee:
                    return 20;

                case BuildingKindType.Fire:
                case BuildingKindType.HellFire:
                case BuildingKindType.FireBall:
                case BuildingKindType.HellFireBall:
                case BuildingKindType.FireShip:
                case BuildingKindType.InfernoBall:
                case BuildingKindType.Inferno:
                    return 100;

                case BuildingKindType.Market:
                case BuildingKindType.Barracks:
                case BuildingKindType.BlacksmithShop:
                case BuildingKindType.Stable:
                case BuildingKindType.SpBuilding1:
                case BuildingKindType.MilitaryOffice:
                case BuildingKindType.RecruitBuilding:
                case BuildingKindType.SpBuilding2:
                case BuildingKindType.SpBuilding3:
                case BuildingKindType.TrainTroopBuilding:
                case BuildingKindType.BigMarket:
                case BuildingKindType.FishMarket:
                case BuildingKindType.BlackMarket:
                    return 40;

                case BuildingKindType.Farm:
                case BuildingKindType.MechineFactory:
                case BuildingKindType.BoatFactory:
                case BuildingKindType.Mint:
                case BuildingKindType.Barn:
                case BuildingKindType.MilitaryGarrison:
                    return 30;
            }
            return 0;
        }

        /// <summary>
        /// 【原版直译】火计是否比强攻更有效 —— is_fire_recommended（s11_sys_unit_action.cpp:1387-1396）。
        ///
        ///   剑兵：兵力 &lt; 5000 就用火
        ///   兵力 &lt; 1000
        ///   兵力 &lt; 5000 且 少于守军兵力
        /// </summary>
        static bool CalcOpen311IsFireRecommended(Troop troop, BuildingBase target)
        {
            if (troop == null) return false;

            if (troop.IsSword) return troop.troops < 5000;
            if (troop.troops < 1000) return true;

            City city = target as City;
            int garrison = city != null ? city.troops : 0;
            if (troop.troops < 5000 && troop.troops < garrison) return true;

            return false;
        }

        /// <summary>
        /// 【原版直译】是否为**陷阱类**设施 —— is_trap_type（s11_scen_obj_building.cpp:305-310）。
        ///
        /// 原版：<c>facility_ == FacilityId_Levee → false</c>，其余看
        /// <c>get_facility_type() == FacilityType_Trap</c>。
        /// 本项目 `BuildingType.IsExplosive` 对应 FacilityType_Trap，但**包含堤防**，
        /// 所以这里补上原版对 Levee 的显式排除（与 <c>Building.OnCompleted</c> 中立化逻辑同口径）。
        /// </summary>
        /// <param name="type">建筑类型</param>
        /// <returns>是否陷阱类（不含堤防）</returns>
        public static bool IsTrapType(BuildingType type)
        {
            if (type == null) return false;
            if (type.kind == (int)BuildingKindType.Levee) return false;   // :307-308
            // 浅滩（Sandbar）在数据里也归在 majorType = Explosive，但它没有爆发效果，
            // 与 BuildingTrapEffect.CanTrigger:67-69 的判定保持同口径，否则 AI 会误判浅滩为陷阱而不去破坏。
            if (type.kind == (int)BuildingKindType.Sandbar) return false;
            return type.IsExplosive;
        }

        /// <summary>
        /// 【原版直译】是否为**已建成**的陷阱 —— is_completed_trap（s11_sys_unit_utils.cpp:73-85）。
        ///
        /// 原版四连判：
        ///   · <c>utils::is_alive(object)</c>      → 耐久 &gt; 0
        ///   · <c>object->is_instance(ScenObjType_Building)</c> → 是建筑（入参已保证）
        ///   · <c>building->is_trap_type()</c>      → <see cref="IsTrapType"/>
        ///   · <c>building->is_completed()</c>      → <see cref="BuildingBase.isComplate"/>
        ///
        /// 原版用它做"攻击动作候选"的闸门（普攻 / 包围 :389、:408）：
        /// **已建成的陷阱默认不生成攻击候选**，除非它是任务主目标。
        /// 缺失这道闸门时，中立陷阱会以「统率+武力 + 动作常量 1000 + 位置分」≈ 1100 的小正分
        /// 混进候选，在附近没有更值钱目标时被选中 —— 表现为"路过陷阱顺手打一下"、
        /// "工程队刚修好就把自己修的陷阱引爆"。
        /// </summary>
        /// <param name="building">建筑对象</param>
        /// <returns>是否为已建成的陷阱</returns>
        public static bool IsCompletedTrap(BuildingBase building)
        {
            if (building == null) return false;
            if (building.durability <= 0) return false;                   // :75-76 is_alive
            if (!IsTrapType(building.BuildingType)) return false;          // :80-81
            return building.isComplate;                                   // :82-83
        }

        /// <summary>
        /// 【原版直译】是否为**敌对**目标 —— Unit::hostile_target（s11_scen_obj_unit.cpp:329-340）。
        ///
        /// 原版只认三件事：对象存活、是建筑/部队、<c>is_enemy()</c>。
        /// 而 <c>is_enemy(a, b)</c> 在任一势力不存在时直接 false
        /// （s11_sys_scen_force.cpp:881-892 的 885-888 两行），即**无主对象（force_id &lt; 0）永远不是敌人**。
        ///
        /// 【为什么不能直接调 IsEnemy】本项目 <see cref="SangoObject.IsEnemy(Force, Force)"/>
        /// 在 forceB == null 时返回 **true**（SangoObject.cs:306-307）。陷阱建成后是中立建筑
        /// （BelongForce == null，见 Building.OnCompleted 与 GovernanceValidator:85-108），
        /// 直接套用会把中立陷阱 / 土垒 / 箭楼算成"敌人"，于是 get_trap_score 认为
        /// "炸得到敌人"、返回正分 —— AI 就会去引爆一座实际上炸不到任何敌军的陷阱。
        /// </summary>
        static bool IsHostileTarget(Troop troop, Troop target)
        {
            if (troop == null || target == null) return false;
            if (!target.IsAlive) return false;
            if (troop.BelongForce == null || target.BelongForce == null) return false;
            return troop.IsEnemy(target);
        }

        /// <summary>
        /// 【原版直译】是否为**敌对**建筑 —— Unit::hostile_target（s11_scen_obj_unit.cpp:329-340）。
        /// 见 <see cref="IsHostileTarget(Troop, Troop)"/> 的说明：无主建筑不算敌人。
        /// </summary>
        static bool IsHostileTarget(Troop troop, BuildingBase target)
        {
            if (troop == null || target == null) return false;
            if (!target.IsAlive) return false;
            if (troop.BelongForce == null || target.BelongForce == null) return false;
            return troop.IsEnemy(target);
        }

        /// <summary>
        /// 【原版直译】是否为**同势力**目标 —— Unit::friendly_target（s11_scen_obj_unit.cpp:316-327）。
        /// 原版 <c>is_same_force</c> 比较的是 force_id（s11_scen_obj_unit.cpp:304-307）；
        /// 本项目两边的 BelongForce 都为 null（无主）时按"不是友军"处理，与原版
        /// "-1 == -1" 的字面差异只影响"无主部队 vs 中立建筑"，AI 部队（必有势力）不受影响。
        /// </summary>
        static bool IsFriendlyTarget(Troop troop, Troop target)
        {
            if (troop == null || target == null) return false;
            if (!target.IsAlive) return false;
            if (troop.BelongForce == null || target.BelongForce == null) return false;
            return target.BelongForce == troop.BelongForce;
        }

        /// <summary>
        /// 【原版直译】是否为**同势力**建筑 —— Unit::friendly_target（s11_scen_obj_unit.cpp:316-327）。
        /// </summary>
        static bool IsFriendlyTarget(Troop troop, BuildingBase target)
        {
            if (troop == null || target == null) return false;
            if (!target.IsAlive) return false;
            if (troop.BelongForce == null || target.BelongForce == null) return false;
            return target.BelongForce == troop.BelongForce;
        }

        /// <summary>
        /// 陷阱是否就是本次行动的"任务主目标" —— 原版 <c>add_attack_action:408</c> 的
        /// <c>target == main_target</c>（主目标时允许攻击已建成陷阱）。
        ///
        /// 原版用对象指针比较。本项目 <c>troop.missionTarget</c> 是 int，且**语义随任务类型变化**：
        /// 拆建筑任务是建筑对象 Id，建造任务是 <c>BuildingType.Id</c>（类型 Id），返城 / 占领是城池 Id。
        /// 若直接比 Id，工程队（missionTarget = 陷阱类型 Id）会与某座陷阱的对象 Id 撞号，
        /// 把"正在修的那类陷阱"误判成主目标、重新放行攻击。因此这里限定
        /// "拆建筑任务 + 对象 Id 命中"，还原原版指针比较的语义。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="building">建筑对象</param>
        /// <returns>该陷阱是否为当前拆建筑任务的目标</returns>
        public static bool IsTrapMainTarget(Troop troop, BuildingBase building)
        {
            if (troop == null || building == null) return false;
            if (troop.missionType != (int)MissionType.TroopDestroyBuilding) return false;
            return building.Id == troop.missionTarget;
        }

        /// <summary>
        /// 候选的命中集合里是否含有"白烧"的陷阱（已完成陷阱 + 非主目标 + 引爆收益 ≤ 0）。
        ///
        /// 【为什么要独立算一次引爆收益】多目标候选聚合后，逐格负分会被
        /// <c>PriorityAction</c> 的 <c>if (p &gt; 0)</c> 过滤掉，单靠罚分压不住：
        /// 单个敌人目标的逐格分约 +9200~10200，而单座陷阱的罚分约 -8800，
        /// 穿线上只要有 1 个敌人就足以抵消。所以要把"这座陷阱值不值得引爆"提到候选级判定。
        ///
        /// 【判定口径】直接复用原版 get_trap_score（<see cref="CalcOpen311TrapScore"/>）：
        ///   · &gt; 0 —— 爆炸范围内确实有敌人（每个敌人 +1、每个友军 -2、引爆者自己格 -2），
        ///     引爆有收益，候选保留；
        ///   · ≤ 0 —— 炸不到人 / 只炸到友军 / 引爆者也在爆心 / 陷阱还没建成，整条候选作废。
        ///
        /// 任务主目标（<see cref="IsTrapMainTarget"/>）不受此限，对齐原版
        /// <c>add_attack_action:408</c> 的 <c>target == main_target</c> 例外。
        /// </summary>
        /// <param name="troop">行动部队</param>
        /// <param name="action">候选动作</param>
        /// <returns>true = 该候选应被丢弃</returns>
        static bool HasUselessTrapInAttackCells(Troop troop, PriorityActionData action)
        {
            if (troop == null || action == null || action.atkCells == null)
                return false;

            for (int i = 0; i < action.atkCells.Length; i++)
            {
                Cell cell = action.atkCells[i];
                if (cell == null || cell.building == null)
                    continue;

                BuildingBase building = cell.building;

                // 只拦"已建成的陷阱"：未建成的陷阱不会引爆（BuildingTrapEffect.Trigger:56 要求 isComplate），
                // 打它等同于打普通在建工地，不在本规则的范畴内。
                if (!IsCompletedTrap(building))
                    continue;

                // 任务主目标例外（原版 target == main_target）
                if (IsTrapMainTarget(troop, building))
                    continue;

                // 引爆收益 ≤ 0 → 这座陷阱白烧，整条候选作废
                int trapScore = CalcOpen311TrapScore(troop, building, action.movetoCell ?? troop.cell);
                if (trapScore <= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 【原版直译】陷阱评分 —— get_trap_score（s11_sys_unit_action.cpp:2154-2191）。
        ///
        ///   火种 / 火船      × 2500（半径 1）
        ///   火焰种           × 3000（半径 2）
        ///   业火种           × 3500（半径 2）
        ///   火球             × 2500（直线 3）
        ///   火焰球           × 3000（直线 5）
        ///   业火球           × 3500（直线 4，可穿建筑）
        ///   正分时再 +10000
        ///
        /// :2187-2188 原版入口先判 <c>is_completed_trap</c>，未建成的陷阱一律 0 分
        /// （调用方的 "≤0 则 -10000" 因此会把半成品陷阱也重罚掉）。
        /// </summary>
        static int CalcOpen311TrapScore(Troop troop, BuildingBase trap, Cell unitCell)
        {
            if (!IsCompletedTrap(trap)) return 0;                          // :2187-2188

            Cell trapCell = trap.CenterCell;
            if (trapCell == null) return 0;

            return CalcOpen311TrapScoreByKind(troop, (BuildingKindType)trap.BuildingType.kind, trapCell, unitCell);
        }

        /// <summary>
        /// 【原版直译】按陷阱类型评分 —— get_trap_score（s11_sys_unit_action.cpp:2154-2191）。
        /// 与 <see cref="CalcOpen311TrapScore"/> 的差别：不依赖建筑对象，直接给陷阱 kind + 位置，
        /// 供「假设在 targetPos 造陷阱、引爆者站 unitCell」的建址反推（get_trap_build_pos_score）使用。
        /// </summary>
        public static int CalcOpen311TrapScoreByKind(Troop troop, BuildingKindType kind, Cell trapCell, Cell unitCell)
        {
            if (troop == null || trapCell == null) return 0;

            int score = 0;
            switch (kind)
            {
                case BuildingKindType.Fire:
                case BuildingKindType.FireShip:
                    score = CalcOpen311FireTrapWeight(troop, unitCell, trapCell, 1) * 2500;
                    break;

                case BuildingKindType.HellFire:
                    score = CalcOpen311FireTrapWeight(troop, unitCell, trapCell, 2) * 3000;
                    break;

                case BuildingKindType.Inferno:
                    score = CalcOpen311FireTrapWeight(troop, unitCell, trapCell, 2) * 3500;
                    break;

                case BuildingKindType.FireBall:
                    score = CalcOpen311FireballTrapWeight(troop, trapCell, unitCell, 3, false) * 2500;
                    break;

                case BuildingKindType.HellFireBall:
                    score = CalcOpen311FireballTrapWeight(troop, trapCell, unitCell, 5, false) * 3000;
                    break;

                case BuildingKindType.InfernoBall:
                    score = CalcOpen311FireballTrapWeight(troop, trapCell, unitCell, 4, true) * 3500;
                    break;
            }

            if (score > 0) score += 10000;
            return score;
        }

        /// <summary>
        /// 【原版直译】火计可连携的友军数 —— get_kakei_assistable_unit_count（s11_sys_unit_utils.cpp:702-710）。
        /// 统计 targetPos 周围 maxDistance 内、能对陷阱放火计引爆的友军部队数。
        /// self 传 null 表示「不排除任何部队」（城池 AI 派工程队时没有具体工程队对象）。
        /// </summary>
        public static int GetKakeiAssistableUnitCount(Troop self, int maxDistance, Cell targetPos)
        {
            if (targetPos == null || Scenario.Cur == null || Scenario.Cur.Map == null)
                return 0;

            int count = 0;
            if (IsKakeiAssistable(self, targetPos.troop))
                count++;

            Scenario.Cur.Map.SpiralAction(targetPos, maxDistance, c =>
            {
                if (c != null && c != targetPos && IsKakeiAssistable(self, c.troop))
                    count++;
            });
            return count;
        }

        /// <summary>
        /// 【原版直译】判断某支部队能否「点火引爆陷阱」—— is_kakei_assistable_pred（s11_sys_unit_utils.cpp:712-739）：
        ///   ① 不是自己 ② 同势力 ③ 未行动完 ④ 非运输队
        ///   ⑤ 命令是讨伐(Subjugate→TroopOccupyCity)或驱逐(Expel→TroopProtectCity)
        ///   ⑥ 气力够放火计 ⑦ 建造力 ≥ 70（calc_build_power，见 <see cref="CalcBuildPower"/>）
        /// self 为 null 时跳过①②（由调用方按 force 自行保证同势力）。
        /// </summary>
        public static bool IsKakeiAssistable(Troop self, Troop unit)
        {
            if (unit == null || unit == self)
                return false;
            if (self != null && unit.BelongForce != self.BelongForce)
                return false;
            if (unit.ActionOver)
                return false;
            if (unit.IsTransport)
                return false;

            MissionType mt = (MissionType)unit.missionType;
            if (mt != MissionType.TroopOccupyCity && mt != MissionType.TroopProtectCity)
                return false;

            if (unit.energy < GetFireCostEnergy())
                return false;
            if (CalcBuildPower(unit) < 70)
                return false;
            return true;
        }

        /// <summary>
        /// 火计消耗气力（对齐原版 get_strategy_energy_cost(StrategyId_Fire)）。
        /// 从 CommonData.Skills 的火计(Id 22)配置读取，缺省给 10。
        /// </summary>
        public static int GetFireCostEnergy()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Skills == null)
                return 10;

            Skill skill = scenario.CommonData.Skills.Get(SKILL_ID_FIRE);
            return skill != null && skill.costEnergy > 0 ? skill.costEnergy : 10;
        }

        /// <summary>
        /// 【原版直译】部队建造力 —— calc_build_power（s11_sys_scen_unit.cpp:1616-1631）。
        ///   n = sqrt(troops/5) * BuildPower / 8；n &gt; 405 时钳到 405 + rand_int(10)（405~414），下限 1。
        /// 与原版的差异：
        ///   · 原版阈值是 405 + rand_int(10)（405~414 随机），此处简化为固定 405（&gt;405 才钳），
        ///     且 rand_int(10) 只调用一次（原版比较/赋值各一次）；对 &lt;70 / &lt;200 门槛判断零影响。
        ///   · 原版用 get_seed()/set_seed() 包裹使抖动不推进全局随机序列；sango 的 GameRandom 无种子恢复接口，
        ///     此处 Range(0,10) 会推进序列。
        ///   · 筑城特技(SkillId_Chikujou)的 ×2 由 TroopImproveBuildPower（特技 #78）在加耐久时处理，此处不重复。
        /// </summary>
        public static int CalcBuildPower(Troop troop)
        {
            if (troop == null) return 0;
            int n = (int)(System.Math.Sqrt(troop.troops / 5.0) * troop.BuildPower / 8.0);
            if (n > 405)
                n = 405 + GameRandom.Range(0, 10);
            if (n <= 1) n = 1;
            return n;
        }

        /// <summary>
        /// 【原版直译】造陷阱位置评分 —— get_trap_build_pos_score（s11_sys_unit_action.cpp:2084-2152）。
        /// 遍历 targetPos 周围一圈，模拟「友军站在某格引爆」的得分，遇**首个**满足条件的环格即返回：
        ///   · 拥有神火计（<see cref="ForceAbilityFlags.DivineFire"/>）时直接采用首个正分；
        ///   · 否则（:2106-2149）需确认引爆者能接近陷阱，见 <see cref="CanApproachTrap"/>。
        /// 注意：环格遍历顺序用 Map.GetRing（Hex.Ring 默认 startDir），与原版 MapRingIterator 的起始方向
        /// 可能有差异，导致「首个」选取不同；但「首个满足条件即返回」的语义已严格对齐。
        /// </summary>
        public static int GetTrapBuildPosScore(BuildingKindType trapKind, Troop unit, Cell unitPos, Cell targetPos)
        {
            if (unit == null || targetPos == null || Scenario.Cur == null || Scenario.Cur.Map == null)
                return 0;

            // 神火计科技（:2090-2093）。原版查科技表里的神火计，本项目查势力能力标记位
            // （由 ForceAddAbilityFlag 这个 action 在技巧注册时置位）。
            // 无势力（无主部队）按"没有"处理，走"无神火计"分支。
            bool divineFire = unit.BelongForce != null && unit.BelongForce.HasAbilityFlag(ForceAbilityFlags.DivineFire);

            // 用 GetRing 取环格列表，自己 for 遍历，遇首个满足条件即 return（对齐原版 :2094-2149）。
            List<Cell> ring = new List<Cell>();
            Scenario.Cur.Map.GetRing(targetPos, 1, ring);

            for (int i = 0; i < ring.Count; i++)
            {
                Cell r = ring[i];
                if (r == null || r == targetPos || r == unitPos)
                    continue;                            // 跳过建造者自己所在格（:2096-2097）
                if (r.troop != null || r.building != null)
                    continue;                            // 引爆者位置必须为空（:2099 is_empty）
                if (r.fire != null)
                    continue;                            // 已着火跳过（:2099 fire_timer）

                int score = CalcOpen311TrapScoreByKind(unit, trapKind, targetPos, r);
                if (score <= 10000)
                    continue;                            // 正分才 +10000（:2102-2103）

                if (divineFire)
                    return score;                        // :2104-2105 直接返回首个正分

                if (CanApproachTrap(unit, r))
                    return score;                        // :2148-2149 有可接近路径
            }
            return 0;
        }

        /// <summary>
        /// 【原版直译】判断引爆者（站在 detonatorCell）能否接近陷阱 —— get_trap_build_pos_score 非神火计分支
        /// （s11_sys_unit_action.cpp:2106-2149）。
        /// 遍历引爆者周围一圈：route = 圈格数 - 1，着火 -1、非存活对象 -1、敌方(zoc) -2、中立 -1、同势力不减；
        /// 若紧邻「能火计联动的友军」直接返回 true（:2138-2144），否则 route &gt; 0 视为可接近（:2147-2149）。
        /// </summary>
        static bool CanApproachTrap(Troop unit, Cell detonatorCell)
        {
            if (unit == null || detonatorCell == null || Scenario.Cur == null || Scenario.Cur.Map == null)
                return false;

            Scenario scenario = Scenario.Cur;

            // route 初始 = 引爆者周围一圈的格数 - 1（"내가 위치할 칸 제외"，:2109）
            int subRingCount = 0;
            scenario.Map.SpiralAction(detonatorCell, 1, c =>
            {
                if (c != null && c != detonatorCell)
                    subRingCount++;
            });
            int route = subRingCount - 1;

            bool nearAssistable = false;
            scenario.Map.SpiralAction(detonatorCell, 1, c =>
            {
                if (nearAssistable || c == null || c == detonatorCell)
                    return;

                if (c.fire != null)
                    route -= 1;                          // :2113-2114 着火 -1（不 continue，继续下判）

                if (c.troop == null && c.building == null)
                    return;                              // :2115-2116 空格 continue

                Troop t = c.troop;
                BuildingBase b = c.building;

                // :2117-2125 非存活对象 -1
                if (t != null && !t.IsAlive) { route -= 1; return; }
                if (b != null && !b.IsAlive) { route -= 1; return; }

                bool hostile;
                bool friendly;
                if (t != null)
                {
                    hostile = unit.IsEnemy(t);
                    friendly = t.BelongForce == unit.BelongForce;
                }
                else
                {
                    hostile = unit.IsEnemy(b);
                    friendly = b.BelongForce == unit.BelongForce;
                }

                if (hostile) { route -= 2; return; }     // :2126-2130 敌方 zoc -2
                if (!friendly) { route -= 1; return; }   // :2131-2136 中立 -1
                // 同势力：不减；若是部队，检查能否火计联动（:2136-2144）
                if (t != null && IsKakeiAssistable(unit, t))
                    nearAssistable = true;
            });

            if (nearAssistable) return true;             // :2138-2144 紧邻联动友军
            return route > 0;                            // :2147-2149 有可接近路径
        }

        /// <summary>
        /// 【原版直译】火种类陷阱的连锁权重 —— get_fire_trap_weight（:2232-2259）。
        ///   遍历半径 1~maxDistance：自己 -2、敌方 +1、友方 -2、中立 +1（仅当总分 &gt; 0 时并入）
        /// </summary>
        static int CalcOpen311FireTrapWeight(Troop troop, Cell unitCell, Cell trapCell, int maxDistance)
        {
            if (troop == null || trapCell == null) return 0;
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null) return 0;

            int weight = 0;
            int alliesCount = 0;

            scenario.Map.SpiralAction(trapCell, maxDistance, c =>
            {
                if (c == null || c == trapCell) return;          // 原版是 1~max，不含自身

                if (unitCell != null && c == unitCell)
                {
                    weight -= 2;
                    return;
                }

                if (c.troop != null)
                {
                    // 敌 +1 / 友 -2 / 其它（含无主）计入 allies_count（原版 :2247-2252）
                    if (IsHostileTarget(troop, c.troop)) weight += 1;
                    else if (IsFriendlyTarget(troop, c.troop)) weight -= 2;
                    else alliesCount += 1;
                }
                else if (c.building != null)
                {
                    if (IsHostileTarget(troop, c.building)) weight += 1;
                    else if (IsFriendlyTarget(troop, c.building)) weight -= 2;
                    else alliesCount += 1;
                }
            });

            if (weight > 0) weight += alliesCount;
            return weight;
        }

        /// <summary>
        /// 【原版直译】火球类陷阱的直线权重 —— get_fireball_trap_weight（:2261-2289）。
        ///   从陷阱格出发，沿「点火者 → 陷阱」的方向继续推进 maxDistance 格：
        ///   敌 +1、友 -1、中立另计（最终 weight &gt; 0 时才并入）；
        ///   遇不可通行地形停止；遇建筑时按 throughBuilding 决定是否继续。
        ///
        /// 【方向必须由点火者决定，不能六方向取最大】
        /// 原版 <c>get_trap_score</c> 里 <c>dir = map_.calc_dir(unit_pos, trap_pos)</c>
        /// （s11_sys_unit_action.cpp:2156）—— 火球从陷阱出发，**背离点火者**滚动。
        /// 也就是说"这次点火会烧到谁"完全取决于点火者站在哪一侧。
        /// 若改成"六个方向各算一遍取最大"，会得到两个错误结果：
        ///   ① 建址评分虚高：日志里 12500（= 2500×1 + 10000）其实是六方向里的最优方向，
        ///      真实点火站位未必能烧到人；
        ///   ② 点火评估恒为正：即使实际站位会把自己的友军一起烧掉（原版权重为负、判 0），
        ///      取最大值也能找到一条"只烧敌人"的方向，于是 AI 以为有利、真打出去却未必。
        /// </summary>
        /// <param name="troop">点火部队</param>
        /// <param name="trapCell">陷阱所在格</param>
        /// <param name="unitCell">点火者所在格（决定火球滚动方向）</param>
        /// <param name="maxDistance">直线长度（火球 3 / 火焰球 5 / 业火球 4）</param>
        /// <param name="throughBuilding">是否穿透建筑（业火球 true）</param>
        /// <returns>权重（&gt;0 才视为有利）</returns>
        static int CalcOpen311FireballTrapWeight(Troop troop, Cell trapCell, Cell unitCell, int maxDistance, bool throughBuilding)
        {
            if (troop == null || trapCell == null || unitCell == null) return 0;

            // 原版 map_.calc_dir(unit_pos, trap_pos)：从点火者指向陷阱的方向
            int dir = unitCell.DirectionTo(trapCell);

            int weight = 0;
            int alliesCount = 0;
            Cell cur = trapCell;

            for (int i = 0; i < maxDistance; i++)
            {
                if (cur == null) break;
                cur = cur.GetNeighbor(dir);
                if (cur == null) break;
                if (cur.TerrainType == null || !cur.TerrainType.moveable) break;

                if (cur.troop != null)
                {
                    // 敌 +1 / 友 -1 / 其它（含无主）计入 allies_count（原版 :2276-2281）
                    if (IsHostileTarget(troop, cur.troop)) weight += 1;
                    else if (IsFriendlyTarget(troop, cur.troop)) weight -= 1;
                    else alliesCount += 1;
                }
                else if (cur.building != null)
                {
                    if (IsHostileTarget(troop, cur.building)) weight += 1;
                    else if (IsFriendlyTarget(troop, cur.building)) weight -= 1;
                    else alliesCount += 1;

                    // 原版 :2282-2283：撞到建筑且不穿透 → 停止
                    if (!throughBuilding) break;
                }
            }

            if (weight > 0) weight += alliesCount;
            return weight;
        }

        /// <summary>是否据点（城 / 关 / 港 / 小城）</summary>
        static bool IsKyoten(BuildingBase building)
        {
            City city = building as City;
            return city != null && (city.IsCity() || city.IsGate() || city.IsPort());
        }

        /// <summary>是否阵类（阵 / 砦 / 城塞）</summary>
        static bool IsCampType(BuildingBase building)
        {
            return building != null && building.BuildingType != null
                && building.BuildingType.kind == (byte)BuildingKindType.Camp;
        }

        /// <summary>是否正在执行进攻类任务</summary>
        static bool IsOnAttack(Troop troop)
        {
            MissionType mt = (MissionType)troop.missionType;
            return mt == MissionType.TroopDestroyTroop
                || mt == MissionType.TroopDestroyBuilding
                || mt == MissionType.TroopOccupyCity
                || mt == MissionType.TroopHarassCity
                || mt == MissionType.TroopBanishTroop;
        }

        /// <summary>「火神」（Features.json Id 63）</summary>
        const int FeatureId_Kashin = 63;
        /// <summary>「神算」（Features.json Id 64）</summary>
        const int FeatureId_Shinsan = 64;
        /// <summary>「深谋」（Features.json Id 68）</summary>
        const int FeatureId_Shinbou = 68;

        /// <summary>
        /// <see cref="CalcOpen311AroundFacilitiesScore"/> 的计数缓冲（下标 = BuildingType.posScoreKind）。
        ///
        /// 提成字段是为了避免每个候选落点都 new int[8]：该函数在
        /// TroopAIUtility.PriorityAction 的目标扫描里每次评分都会调用一次。
        /// 单线程回合制，不存在并发访问；每次使用前 Array.Clear 归零。
        /// </summary>
        static readonly int[] OwnScoreBuf = new int[8];

        /// <inheritdoc cref="OwnScoreBuf"/>
        static readonly int[] EnemyScoreBuf = new int[8];

        /// <summary>
        /// <see cref="CalcOpen311AroundFacilitiesScore"/> 的螺旋取格缓冲。
        /// 复用内部数组容量（Clear 不释放），避免每落点都新建 List&lt;Cell&gt;。
        /// </summary>
        static readonly List<Cell> SpiralCellBuf = new List<Cell>(64);

        /// <summary>
        /// 【原版直译】周围设施分 —— get_pos_around_facilities_score
        /// （s11_sys_unit_action.cpp:1473-1565）。
        ///
        /// 遍历周围 1~3 格的建筑，按 <see cref="BuildingType.posScoreKind"/> 分类计数：
        ///   友方：据点 +(550 + n×20)、太鼓台/军乐台 +100×(鼓×3+乐)、
        ///         阵/箭楼 +350×(阵×4+箭)、投石台 +450×n
        ///   敌方：上述全部取负，且据点额外 +(4×(275 + n×5))（原版 :1558-1560）
        ///
        /// 距离过滤：原版迭代 1~3 格后按 <c>facility.get_min_range()/get_max_range()</c> 再过滤，
        /// 本项目读 <see cref="BuildingType.posScoreKind"/> 同族的
        /// <see cref="BuildingType.minRange"/> / <see cref="BuildingType.maxRange"/>
        /// （默认 1~3，与原版迭代区间一致）。
        /// 【差异 2】原版"敌方 = k+7"，本项目按建筑当前所属势力实时判断（等价，且能跟随易主）。
        /// 【缺失】原版 k=6 石兵、k=14/15 火种火球类：前者本项目暂无该设施，
        ///          后者在原版评分公式里也不参与计算（只计数），故都不影响结果。
        /// </summary>
        static int CalcOpen311AroundFacilitiesScore(Troop troop, Cell pos)
        {
            if (troop == null || pos == null) return 0;
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null) return 0;

            AIConfig cfg = AIConfig.Instance;

            // 下标 = BuildingType.posScoreKind（1~7），0 不计分
            //
            // 【性能】原写法每次调用要分配四样东西：own / enemy 两个 int[8]（各 56 B）、
            // SpiralAction 的闭包对象（128 B）、Action<Cell> 委托（64 B），合计 312 B。
            // 本函数由 CalcOpen311PosScore 对每个候选落点调用一次，
            // TroopAIUtility.PriorityAction 单帧 283.2 KB GC 里绝大部分出自这里。
            // 改法：计数数组改静态复用（每次 Array.Clear），
            // 遍历改用「起点 + Ring(1..3)」展开 —— 与 Map.SpiralAction 的调用顺序
            // （先 action(start)，再 RingAction(1)(2)(3)）完全一致，结果不变。
            int[] own = OwnScoreBuf;
            int[] enemy = EnemyScoreBuf;
            System.Array.Clear(own, 0, own.Length);
            System.Array.Clear(enemy, 0, enemy.Length);

            SpiralCellBuf.Clear();
            SpiralCellBuf.Add(pos);
            for (int r = 1; r <= 3; r++)
                scenario.Map.GetRing(pos, r, SpiralCellBuf);

            for (int ci = 0; ci < SpiralCellBuf.Count; ci++)
            {
                Cell cell = SpiralCellBuf[ci];
                if (cell == null || cell.building == null) continue;
                BuildingBase building = cell.building;
                if (building.BelongForce == null) continue;              // 中立设施不计分
                BuildingType type = building.BuildingType;
                if (type == null) continue;

                int kind = type.posScoreKind;
                if (kind <= 0 || kind >= own.Length) continue;

                // 原版 :1485 按设施自身的生效范围过滤。原版迭代从距离 1 开始（不含自身格），
                // 这里用 minRange 的默认值 1 表达同一语义。
                int distance = scenario.Map.Distance(pos, cell);
                int minRange = type.minRange;
                int maxRange = type.maxRange;
                if (maxRange < minRange) maxRange = minRange;
                if (distance < minRange || distance > maxRange) continue;

                if (building.BelongForce == troop.BelongForce) own[kind]++;
                else enemy[kind]++;
            }

            int score = 0;

            // 友方
            if (own[1] > 0) score += cfg.o311PosOwnKyotenBase + own[1] * cfg.o311PosOwnKyotenPerCount;
            score += cfg.o311PosDrumBandFactor * (own[5] * 3 + own[6]);
            score += cfg.o311PosCampTowerFactor * (own[2] * 4 + own[3]);
            score += cfg.o311PosCatapultFactor * own[4];

            // 敌方
            if (enemy[1] > 0) score -= cfg.o311PosOwnKyotenBase + enemy[1] * cfg.o311PosOwnKyotenPerCount;
            score -= cfg.o311PosDrumBandFactor * (enemy[5] * 3 + enemy[6]);
            score -= cfg.o311PosCampTowerFactor * (enemy[2] * 4 + enemy[3]);
            score -= cfg.o311PosCatapultFactor * enemy[4];

            // 敌方据点额外（原版 :1558-1560）
            if (enemy[1] > 0)
                score += 4 * (cfg.o311PosEnemyKyotenExtraBase + enemy[1] * cfg.o311PosEnemyKyotenExtraPerCount);

            return score;
        }

        /// <summary>
        /// 【原版直译】地形分 —— get_pos_terrain_score（s11_sys_unit_action.cpp:1567-1588）。
        ///
        /// 地形归属由 <see cref="TerrainType.posKind"/> 配置，代码不认地形 Id：
        ///   1 砂地 +40 / 2 湿地 +30 / 3 毒泉 -10000（解毒 +150）/
        ///   4 森林 +80 / 5 栈道 -250（难所行军或藤甲 +70）
        /// </summary>
        static int CalcOpen311TerrainScore(Troop troop, Cell pos)
        {
            TerrainType terrain = pos.TerrainType;
            if (terrain == null) return 0;
            AIConfig cfg = AIConfig.Instance;

            switch (terrain.posKind)
            {
                case 1:
                    return cfg.o311PosTerrainSand;

                case 2:
                    return cfg.o311PosTerrainWetland;

                case 3:
                    // 毒泉：有「解毒」特技就反过来变成好位置
                    return HasTroopFeature(troop, FeatureId_Gedoku)
                        ? cfg.o311PosTerrainPoisonWithFeat
                        : cfg.o311PosTerrainPoison;

                case 4:
                    return cfg.o311PosTerrainForest;

                case 5:
                    // 栈道：有「难所行军」科技或「藤甲」特技才安全
                    return (HasForceTechnique(troop, TechId_Nanshokougun) || HasTroopFeature(troop, FeatureId_Toukou))
                        ? cfg.o311PosTerrainCliffroadWithFeat
                        : cfg.o311PosTerrainCliffroad;
            }
            return 0;
        }

        /// <summary>队内任一武将是否具备指定特技（原版 has_skill 是 Unit 级聚合）</summary>
        static bool HasTroopFeature(Troop troop, int featureId)
        {
            if (troop == null) return false;
            Person leader = troop.Leader;
            if (leader != null && leader.HasFeatrue(featureId)) return true;
            Person member1 = troop.Member1;
            if (member1 != null && member1.HasFeatrue(featureId)) return true;
            Person member2 = troop.Member2;
            if (member2 != null && member2.HasFeatrue(featureId)) return true;
            return false;
        }

        /// <summary>势力是否已掌握指定技巧</summary>
        static bool HasForceTechnique(Troop troop, int techniqueId)
        {
            Force force = troop != null ? troop.BelongForce : null;
            if (force == null || force.Techniques == null) return false;
            return force.Techniques.Contains(techniqueId);
        }

        /// <summary>「解毒」（Features.json Id 9）</summary>
        const int FeatureId_Gedoku = 9;
        /// <summary>「藤甲」（Features.json Id 32）</summary>
        const int FeatureId_Toukou = 32;
        /// <summary>「难所行军」（Techniques.json Id 18）</summary>
        const int TechId_Nanshokougun = 18;

        /// <summary>原版动作的"成功率"项：取技能自身的成功率算法（百分比）</summary>
        static int CalcOpen311SuccessChance(Troop troop, SkillInstance skill, Cell spellCell)
        {
            if (skill.skillSuccessMethod == null)
                return 0;
            return skill.skillSuccessMethod.Calculate(skill, troop, spellCell);
        }

        /// <summary>
        /// 原版 can_be_counterattacked（s11_sys_anim_unit.cpp:750-760）：
        /// 远程 / 计略不承受反击；近战且目标有反击系数才扣分。
        /// </summary>
        static bool CanBeCounterattacked(Troop troop, SkillInstance skill, Cell target, Cell movetoCell)
        {
            if (target.troop == null || movetoCell == null) return false;
            if (skill.IsRange() || skill.IsStrategy()) return false;

            float factor = target.troop.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(target, movetoCell));
            return factor > 0;
        }

        #endregion

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

            // 【严格对齐原版】Open311 评分体系下不再叠加任何任务倍率。
            //
            // 原版把"是否命中任务主目标"直接做成了 get_target_score 的 +10000 加项
            // （s11_sys_unit_action.cpp:1723-1727），并且**没有**下面这些乘性修正：
            //   · 主目标乘性加权 / 非主目标降权（本项目的 taskPrimaryTargetWeight / taskOtherTargetWeight）
            //   · 原地施法加成（taskStayBonus）
            //   · 近战贴脸加成（taskMeleeCloseBonus）
            //   · 角色任务聚焦倍率（missionFocusScale）
            // 若在 Open311 模式下再乘一遍，主目标会被重复加权，与原版结果不一致。
            if (cfg.useOpen311ScoreFormula)
                return score;

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
