using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 寻路对齐层 —— 把 Open311 `s11_sys_unit_path.cpp` 里**项目缺失的那部分**补上。
    ///
    /// 【为什么不重写搜索本体】
    ///   · 项目的 `Map.GetMinCostDirectPath` 用**可采纳启发式**（每步消耗下界 × 格距），
    ///     恒给出最小消耗路径；原版用曼哈顿距离，在 odd-q 六角格上会高估、只能得到近似最优。
    ///     两者谁更严格很清楚，没必要为了"对齐"而降级。
    ///   · 原版真正独有、而项目缺的是两样：
    ///       ① 每格的**附加代价**（敌我部队/建筑、ZOC、着火）；
    ///       ② 按"目标类型"选 ring0/1/2 目标集（进城 / 围城 / 邻接）。
    ///
    /// 因此本类只做这两件事，搜索仍然交给 <see cref="Map"/>，属纯新增、不改变现有寻路行为。
    /// </summary>
    /// <summary>
    /// 寻路标记位（按部队生效，存在 <see cref="Troop.pathFlags"/>）。
    ///
    /// 对应原版 `UnitPath` 里那批"有某技能/科技就无视某惩罚"的分支：
    ///   · 解毒特技 → 无需为毒泉付代价；
    ///   · 难所行军科技 / 藤甲特技 → 无需为栈道付代价；
    ///   · 飞将类 → 无视 ZOC。
    /// 【置位时机】由技能/科技获得与失去时写入，本类只读。
    /// </summary>
    [System.Flags]
    public enum PathFlag
    {
        None = 0,

        /// <summary>无视毒泉代价（原版：有「解毒」特技）</summary>
        IgnorePoison = 1 << 0,

        /// <summary>无视栈道代价（原版：有「难所行军」科技或「藤甲」特技）</summary>
        IgnoreCliffroad = 1 << 1,

        /// <summary>
        /// 无视**陆地** ZOC。原版 ZOC 分水陆两套（`UnitPath::ignore_land_zoc_`），
        /// 因为水上部队与陆上部队的 ZOC 来源不同，不能合并成一个标记。
        /// </summary>
        IgnoreLandZoc = 1 << 2,

        /// <summary>无视**水上** ZOC（对应原版 `ignore_water_zoc_`）</summary>
        IgnoreWaterZoc = 1 << 3,

        /// <summary>免疫着火格的 ×4 消耗（对应原版 `ignore_fire_`）</summary>
        IgnoreFire = 1 << 4,
    }

    public static class PathAlignment
    {
        /// <summary>走不通时的代价（原版 `UnitPath::get_cost()` 失败返回 INT_MAX）</summary>
        public const int ImpossibleCost = int.MaxValue;

        // ===== 附加代价常量（原版 s11_sys_unit_path.cpp:132-150）=====

        /// <summary>格上有敌军部队</summary>
        public const int CostHostileTroop = 32;

        /// <summary>格上有敌方据点（城/港/关）</summary>
        public const int CostHostileBase = 256;

        /// <summary>格上有敌方普通设施</summary>
        public const int CostHostileBuilding = 128;

        /// <summary>格上有友方/中立建筑</summary>
        public const int CostFriendlyBuilding = 16;

        /// <summary>毒泉 / 栈道（无对应特技或科技时 +64，原版 :149-150 两处同值）</summary>
        public const int CostPoisonOrCliffroad = 64;

        // ===== ZOC 代价（原版 :32-58，只看 6 邻格、取首个命中）=====

        public const int CostZocTroop = 16;
        public const int CostZocBase = 64;
        public const int CostZocBuilding = 32;

        /// <summary>
        /// 把一条路径换算成原版 A* 的 `g` 值（累计消耗）。
        ///
        /// 起点不计代价（与原版一致：起点 `acc_cost = 0`）；
        /// 路径为空表示走不通，返回 <see cref="ImpossibleCost"/>。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="path">起点 → 目标 的格子序列（含两端）</param>
        public static int GetPathCost(Troop troop, List<Cell> path)
        {
            if (troop == null || path == null || path.Count == 0)
                return ImpossibleCost;

            int cost = 0;
            for (int i = 1; i < path.Count; i++)
                cost += GetStepCost(troop, path[i]);

            return cost;
        }

        /// <summary>
        /// 单格进入代价 = 地形消耗 + 着火倍率 + 格上敌我附加 + ZOC。
        /// 对应原版 `:127-162` 的整段计算。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="cell">目标格</param>
        public static int GetStepCost(Troop troop, Cell cell)
        {
            if (troop == null || cell == null) return 0;

            // ① 地形消耗：水/陆分表由 Troop.MoveCost 决定（对应 movement_cost_[equipment][terrain]）
            int cost = troop.MoveCost(cell);

            // ② 着火格 ×4（原版 :134）；免疫火焰（原版 ignore_fire_）则照常
            if (cell.fire != null && (troop.pathFlags & (int)PathFlag.IgnoreFire) == 0)
                cost *= 4;

            // ③ 格上单位 / 建筑（原版 :140-148）
            if (cell.troop != null && cell.troop != troop)
            {
                if (IsHostile(troop, cell.troop.BelongForce))
                    cost += CostHostileTroop;
            }
            else if (cell.building != null)
            {
                bool hostile = IsHostile(troop, cell.building.BelongForce);
                if (hostile)
                    cost += cell.building is City ? CostHostileBase : CostHostileBuilding;
                else
                    cost += CostFriendlyBuilding;
            }
            else
            {
                // ④ ZOC（原版 :151-152）：只在"空格"上才结算
                cost += GetZocCost(troop, cell);
            }

            // ⑤ 毒泉 / 栈道（原版 :149-150）：无对应能力时 +64
            //    项目用地形档位标记：`posKind == 3` 毒泉、`posKind == 5` 栈道
            //    （见 TerrainType.posKind 注释，AI 位置评分用的也是这两档）。
            //
            //    【特技豁免暂未接】原版此处会检查"解毒"特技 / "难所行军"科技，
            //    项目没有 `Troop.HasSkill(int)` 这样的入口，为避免瞎猜特技 Id，
            //    当前一律要求 +64（偏保守：代价只高不低，不会规划出更危险的路线）。
            if (cell.TerrainType != null)
            {
                int pk = cell.TerrainType.posKind;
                int flags = troop.pathFlags;

                // 毒泉：有「解毒」（bit0）则免
                if (pk == 3 && (flags & (int)PathFlag.IgnorePoison) == 0)
                    cost += CostPoisonOrCliffroad;

                // 栈道：有「难所行军」/「藤甲」（bit1）则免
                if (pk == 5 && (flags & (int)PathFlag.IgnoreCliffroad) == 0)
                    cost += CostPoisonOrCliffroad;
            }

            return cost;
        }

        /// <summary>
        /// ZOC 代价（原版 `get_zoc_cost`，`:32-58`）：只看 6 邻格，取首个命中的敌对目标。
        /// 敌部队 16 / 敌据点 64 / 敌普通设施 32。
        /// </summary>
        public static int GetZocCost(Troop troop, Cell cell)
        {
            if (troop == null || cell == null) return 0;

            // 无视 ZOC 分**水陆两套**（原版 `ignore_land_zoc_` / `ignore_water_zoc_`）：
            // 水上部队与陆上部队的 ZOC 来源不同，不能用一个标记互相顶替。
            if (cell.IsWater)
            {
                if ((troop.pathFlags & (int)PathFlag.IgnoreWaterZoc) != 0) return 0;
            }
            else
            {
                if ((troop.pathFlags & (int)PathFlag.IgnoreLandZoc) != 0) return 0;
            }

            for (int i = 0; i < 6; i++)
            {
                Cell n = cell.GetNeighbor(i);
                if (n == null) continue;

                if (n.troop != null && n.troop != troop)
                {
                    if (IsHostile(troop, n.troop.BelongForce))
                        return CostZocTroop;
                }
                else if (n.building != null)
                {
                    if (IsHostile(troop, n.building.BelongForce))
                        return n.building is City ? CostZocBase : CostZocBuilding;
                }
            }

            return 0;
        }

        /// <summary>
        /// 求到目标 ring 的最优路径 —— 对应原版 `get_path_to_ring0 / ring1 / ring2`
        /// （`s11_sys_unit_order.cpp:1199-1222`）：先生成目标点集，再逐个跑 A* 取最小代价。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="target">目标格</param>
        /// <param name="ring">0 = 该格本身；1 = 六邻；2 = 二环</param>
        /// <param name="cellList">输出：起点 → 目标 的路径；走不通时为空</param>
        /// <returns>累计代价；走不通返回 <see cref="ImpossibleCost"/></returns>
        public static int GetPath(Troop troop, Cell target, int ring, List<Cell> cellList)
        {
            if (cellList == null) return ImpossibleCost;
            cellList.Clear();

            if (troop == null || troop.cell == null || target == null)
                return ImpossibleCost;

            Map map = Scenario.Cur != null ? Scenario.Cur.Map : null;
            if (map == null) return ImpossibleCost;

            List<Cell> candidates = new List<Cell>();
            CollectRing(target, ring, candidates);
            if (candidates.Count == 0)
                candidates.Add(target);

            // 【改为一次搜索】原版 `UnitPath` 是**多目标逆向 A\***：
            // 把整个环放进 open，一次搜索就回答"环上哪一格最优"。
            // 旧实现是"对环上每个目标各跑一次 A\*"，k 个目标 = k 次搜索（ring2 是 19 次）；
            // 现在统一走 `Map.GetPathReverseAStar`，与原版同构，复杂度从 O(k·n log n) 降到 O(n log n)。
            return map.GetPathReverseAStar(troop, candidates, cellList);
        }

        /// <summary>
        /// 求到某座据点的最优路径 —— 对应原版 `get_path_to_building`（`:1173-1192`）：
        ///   · 同势力都市 → ring1（进城）；
        ///   · 敌方都市   → ring2（围城）；
        ///   · 同势力关隘/港口 → ring0（直接走到格上）；
        ///   · 其余       → ring1。
        /// </summary>
        public static int GetPathToBuilding(Troop troop, City city, List<Cell> cellList)
        {
            if (city == null || troop == null) return ImpossibleCost;

            bool sameForce = troop.BelongForce != null && troop.BelongForce == city.BelongForce;
            bool isCity = city.IsCity();

            int ring;
            if (sameForce) ring = isCity ? 1 : 0;
            else ring = isCity ? 2 : 1;

            // City 没有直接的格子字段，用它在地图上的坐标取格（Map.GetCell 越界会返回 null）
            Map map = Scenario.Cur != null ? Scenario.Cur.Map : null;
            if (map == null) return ImpossibleCost;

            Cell target = map.GetCell(city.x, city.y);
            if (target == null) return ImpossibleCost;

            return GetPath(troop, target, ring, cellList);
        }

        /// <summary>原版 `get_range` 的递归深度硬上限（`s11_sys_unit_path.cpp:226`）</summary>
        public const int MaxMoveRangeDepth = 10;

        /// <summary>
        /// 移动范围 —— 对应原版 `get_range`（`s11_sys_unit_path.cpp:224-325`）。
        ///
        /// 【当前状态：未接入】实际移动范围仍由 `Map.GetMoveRange` 承担 ——
        /// 它本来就实现了原版的关键结构（ZOC 格吃掉全部移动力、水陆移动力分开、`CanPassThrough` 硬阻挡），
        /// 本轮把它的**地形消耗**换成了 `GetStepCost`（含敌我/ZOC/毒泉/着火），代价口径已对齐。
        /// 本方法保留作参考实现：若将来要严格复刻"DFS + 深度上限 10"，
        /// 需要先补上水陆移动力（`landMoveAbility` / `waterMoveAbility`）与 `CanPassThrough`，
        /// 否则直接替换会**丢失**这两条，比现状更远离原版。
        ///
        /// <code>
        /// dfs(cell, remaining, depth):
        ///     for 6 个邻格:
        ///         敌对部队/建筑   → 硬阻挡（不进入，也不向外扩）
        ///         地形不可通行     → 跳过
        ///         cost > remaining → 剪枝
        ///         收入范围
        ///         进入 ZOC         → **不再向外扩展**
        ///         dfs(next, remaining - cost, depth + 1)
        /// </code>
        ///
        /// 【与 Map.GetMoveRange 的关系】后者是 BFS 螺旋（不区分 ZOC、没有深度上限），
        /// 语义与原版不同。本方法是原版语义的**独立实现**，不替换现有移动范围，
        /// 供"需要严格对齐"的 AI 判定调用。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="movePoint">本次可用移动力</param>
        /// <param name="result">输出：可达格（含起点，起点在 [0]）</param>
        public static void GetMoveRange(Troop troop, int movePoint, List<Cell> result)
        {
            if (result == null) return;
            result.Clear();

            if (troop == null || troop.cell == null || movePoint <= 0)
                return;

            HashSet<Cell> visited = new HashSet<Cell>();
            visited.Add(troop.cell);
            result.Add(troop.cell);

            DfsMoveRange(troop, troop.cell, movePoint, 0, visited, result);
        }

        static void DfsMoveRange(Troop troop, Cell cell, int remaining, int depth,
                                 HashSet<Cell> visited, List<Cell> result)
        {
            if (depth >= MaxMoveRangeDepth) return;

            for (int i = 0; i < 6; i++)
            {
                Cell next = cell.GetNeighbor(i);
                if (next == null || visited.Contains(next)) continue;

                // 硬阻挡：敌对部队 / 敌对建筑
                if (IsBlockingCell(troop, next)) continue;

                // 地形不可通行（水陆分表由 TerrainType.CanMoveBy 决定）
                if (!next.CanMove(troop)) continue;

                int cost = GetStepCost(troop, next);
                if (cost > remaining) continue;

                visited.Add(next);
                result.Add(next);

                // 进入 ZOC 后不再向外扩展
                if (GetZocCost(troop, next) > 0) continue;

                DfsMoveRange(troop, next, remaining - cost, depth + 1, visited, result);
            }
        }

        /// <summary>该格是否硬阻挡（敌对部队 / 敌对建筑）</summary>
        static bool IsBlockingCell(Troop troop, Cell cell)
        {
            if (cell == null) return false;

            if (cell.troop != null && cell.troop != troop)
                return IsHostile(troop, cell.troop.BelongForce);

            if (cell.building != null)
                return IsHostile(troop, cell.building.BelongForce);

            return false;
        }

        /// <summary>
        /// 收集环上的目标点（原版 `Map::get_ring`，`s11_scen_map.cpp:200-210`）。
        /// ring0 = 该格本身；ring1 = 六邻；ring2 = 邻居的邻居（去重，排除环内已有点）。
        /// </summary>
        public static void CollectRing(Cell center, int ring, List<Cell> result)
        {
            if (result == null) return;
            result.Clear();

            if (center == null) return;

            if (ring <= 0)
            {
                result.Add(center);
                return;
            }

            List<Cell> first = new List<Cell>();
            for (int i = 0; i < 6; i++)
            {
                Cell n = center.GetNeighbor(i);
                if (n == null) continue;

                first.Add(n);
                if (!result.Contains(n)) result.Add(n);
            }

            if (ring < 2) return;

            for (int i = 0; i < first.Count; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Cell n = first[i].GetNeighbor(j);
                    if (n == null || n == center) continue;
                    if (first.Contains(n) || result.Contains(n)) continue;
                    result.Add(n);
                }
            }
        }

        /// <summary>是否敌对（同势力 / 同盟 都不算）</summary>
        static bool IsHostile(Troop troop, Force other)
        {
            if (troop == null || other == null) return false;

            Force self = troop.BelongForce;
            if (self == null) return false;
            if (self == other) return false;

            return !self.IsAlliance(other);
        }
    }
}
