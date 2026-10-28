using System.Collections.Generic;
using Sango.Tools;

namespace Sango.Core
{
    /// <summary>
    /// 原版寻路搜索器 —— 逐行对齐 Open311 的 `UnitPath`
    /// （`s11_sys_unit_path.cpp:85-196`）。
    ///
    /// 【与原版一致的三点】
    ///   1. **多目标**：一次搜索覆盖整个目的地集合（进城 ring1、围城 ring2…），
    ///      不是"对每个目标各跑一次 A*"；
    ///   2. **逆向**：把目的地集合放进 open，**弹出 `src` 时结束** ——
    ///      这样"哪座目标城最近"由搜索本身回答，不需要外层循环；
    ///   3. **失败语义**：返回 <see cref="PathCostImpossible"/>（INT_MAX），路径为空。
    ///
    /// 【与项目的既有 A*（`GetMinCostDirectPath`）的分工】
    ///   · 既有 A*：正向、单目标、可采纳启发式，恒给出最小消耗路径 —— 供"点选目的地"用，保留不动；
    ///   · 本方法：逆向、多目标，用于"去哪个目标最优"这类判定（攻城目标选择、援军出击目标）。
    ///
    /// 【启发式】原版用 `|dx| + |dy|`（曼哈顿距离，在 odd-q 六角格上会高估、只能近似最优）。
    /// 这里改用 <see cref="Map.Distance(Cell, Cell)"/>（六角真实距离）——
    /// 它在六角格上恒 ≤ 曼哈顿距离，因此**仍是可采纳下界**，但更紧、展开更少。
    /// 若要严格复刻原版的"近似最优"，把 `h` 换成 `|x1-x2|+|y1-y2|` 即可。
    /// </summary>
    public partial class Map
    {
        /// <summary>走不通（原版 `UnitPath::get_cost()` 失败返回 INT_MAX）</summary>
        public const int PathCostImpossible = int.MaxValue;

        /// <summary>目的地上已有单位/建筑时的额外代价（原版 `:95-97` 的 `f += 64`）</summary>
        public const int PathCostTargetOccupied = 64;

        /// <summary>
        /// 移动范围搜索的**深度上限**（性能保底）。
        ///
        /// 原版 `get_range` 是 10（`s11_sys_unit_path.cpp:226`）。这里取 **20**：
        ///   · 项目的高机动部队（骑兵 + 加成）移动力常超过 10 格，
        ///     硬限 10 会出现"移动力还剩很多，范围却只有 10 圈"的割裂感；
        ///   · 20 跳足以覆盖正常作战范围，同时挡住移动力异常时（Buff / 改数据）
        ///     BFS 逐层展开导致的性能爆炸。
        /// </summary>
        public const int MaxMoveRangeDepth = 20;

        /// <summary>回溯深度保护（防止异常数据造成死循环）</summary>
        const int PathTraceLimit = 20000;

        /// <summary>
        /// 原版寻路：**多目标逆向 A\***。
        ///
        /// 流程（对齐 `s11_sys_unit_path.cpp:85-196`）：
        /// <code>
        /// 把 targets 全部入 open，g=0，f=h(到 src)
        /// while open 非空:
        ///     cur = open 弹出最小 f
        ///     若 cur 已展开 → 跳过（重复入队的惰性删除）
        ///     若 cur == src → 命中，回溯 cameFrom 得到 src→target 的路径
        ///     对 cur 的 6 邻格 next:
        ///         地形不可通行 → 跳过（原版 enabled_terrains_ 白名单）
        ///         newG = g(cur) + 进入 cur 的代价
        ///         若 newG 不优于已记录的 g(next) → 跳过
        ///         记 cameFrom，f = newG + h(next → src)，入队
        /// 未命中 → 返回 INT_MAX，path 清空
        /// </code>
        /// </summary>
        /// <param name="troop">部队（决定地形消耗、ZOC 与敌我附加代价）</param>
        /// <param name="targets">目的地集合（可含多个候选）</param>
        /// <param name="path">输出：src → 命中的目的地 的格子序列（含两端）</param>
        /// <returns>累计消耗；走不通返回 <see cref="PathCostImpossible"/></returns>
        public int GetPathReverseAStar(Troop troop, List<Cell> targets, List<Cell> path)
        {
            if (path == null)
                return PathCostImpossible;

            path.Clear();

            if (troop == null || troop.cell == null || targets == null || targets.Count == 0)
                return PathCostImpossible;

            Cell src = troop.cell;

            Dictionary<Cell, int> gScore = new Dictionary<Cell, int>();
            Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();
            HashSet<Cell> closed = new HashSet<Cell>();
            PriorityQueue<Cell> open = new PriorityQueue<Cell>();
            open.reverse = false;

            for (int i = 0; i < targets.Count; i++)
            {
                Cell t = targets[i];
                if (t == null) continue;

                // 目的地上已有单位/建筑 → 额外代价（原版 :95-97）。
                // 放在 f 里而不是 g 里：它只影响"优先探索哪块"，不改变路径本身的消耗。
                int f = Distance(t, src);
                if (t.troop != null || t.building != null)
                    f += PathCostTargetOccupied;

                gScore[t] = 0;
                open.Push(t, f);
            }

            if (open.Count == 0)
                return PathCostImpossible;

            int result = PathCostImpossible;

            while (open.Count > 0)
            {
                Cell cur = open.Lower();
                if (cur == null) continue;

                // 已展开过 → 说明这是同一格的旧队列入队（惰性删除）
                if (!closed.Add(cur)) continue;

                if (cur == src)
                {
                    result = gScore[src];
                    break;
                }

                int curG = gScore[cur];

                // 进入 cur 的代价只与 cur 有关，出队时算一次即可
                int step = PathAlignment.GetStepCost(troop, cur);

                for (int i = 0; i < 6; i++)
                {
                    Cell next = cur.GetNeighbor(i);
                    if (next == null || closed.Contains(next)) continue;

                    // 地形不可通行（原版 enabled_terrains_ 白名单）
                    if (!next.CanMove(troop)) continue;

                    int newG = curG + step;

                    int oldG;
                    if (gScore.TryGetValue(next, out oldG) && newG >= oldG)
                        continue;

                    gScore[next] = newG;
                    cameFrom[next] = cur;
                    open.Push(next, newG + Distance(next, src));
                }
            }

            if (result == PathCostImpossible)
                return PathCostImpossible;

            // 回溯：src → … → 命中的目的地
            Cell node = src;
            path.Add(node);

            while (cameFrom.TryGetValue(node, out Cell parent))
            {
                path.Add(parent);
                node = parent;

                if (path.Count > PathTraceLimit)
                {
                    // 数据异常（成环），按失败处理，避免把半截路径交给上层
                    path.Clear();
                    return PathCostImpossible;
                }
            }

            return result;
        }

        /// <summary>
        /// 单目标版本（`GetPathReverseAStar` 的常用包装）。
        /// </summary>
        public int GetPathReverseAStar(Troop troop, Cell target, List<Cell> path)
        {
            List<Cell> targets = new List<Cell>(1);
            targets.Add(target);
            return GetPathReverseAStar(troop, targets, path);
        }

        /// <summary>
        /// 按"环"求路径 —— 对齐原版 `get_path_to_ring0/1/2` 的多目标用法：
        /// 先生成环上的目标点集，再一次逆向 A* 求出最优的那一个。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="center">环中心（通常是目标据点格）</param>
        /// <param name="ring">0 = 中心格；1 = 六邻；2 = 二环</param>
        /// <param name="path">输出路径</param>
        public int GetPathToRing(Troop troop, Cell center, int ring, List<Cell> path)
        {
            if (path == null) return PathCostImpossible;
            path.Clear();

            if (center == null) return PathCostImpossible;

            List<Cell> targets = new List<Cell>();
            PathAlignment.CollectRing(center, ring, targets);

            // 环上被占用的格不作为落点（原版由 get_path_to_ringN 的调用方过滤）
            if (targets.Count > 1)
                targets.RemoveAll(c => c != null && (c.troop != null || c.building != null));

            if (targets.Count == 0)
                targets.Add(center);

            return GetPathReverseAStar(troop, targets, path);
        }
    }
}
