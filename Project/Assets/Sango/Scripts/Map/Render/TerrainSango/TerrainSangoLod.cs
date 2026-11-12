using System.Collections.Generic;
using UnityEngine;

namespace Sango.Render.TerrainSango
{
    /// <summary>
    /// Sango 地形的四叉树 LOD 选择。
    ///
    /// 【结构来源】
    /// 与 314 的 TerrainLodSelectionMath 同构：以"到节点的平面距离"决定是否继续分裂，
    /// 层级越高覆盖面积越大；叶子固定 64×64 格。与 314 的差异见下面两条注释。
    ///
    /// 【差异 1：树是按需遍历，不预建节点表】
    /// 314 的节点表来自原版资源（19 层共几十万节点）。本项目的地形是规则网格，
    /// 父子关系可以由 (Level, GridX, GridZ) 直接推导（父 = 坐标右移一位），
    /// 因此这里不预建节点数组，遍历时现算，内存占用为 0。
    ///
    /// 【差异 2：距离阈值直接与 patch 世界尺寸挂钩，供 Morph 复用】
    /// 分裂阈值 T = SplitFactor * Size。顶点着色器用同一个公式算出 Morph 系数，
    /// 于是"几何上收敛到父层"与"LOD 何时切换"严格同步，不会出现切换瞬间的跳变。
    /// </summary>
    public sealed class TerrainSangoLod
    {
        /// <summary>分裂距离系数：距离小于 SplitFactor * patchSize 时继续分裂。</summary>
        public float SplitFactor = 1f;

        /// <summary>
        /// 相机高度计入视距的权重（0 = 完全不计入，1 = 完整计入，默认 1）。
        ///
        /// 【为什么必须计入】
        ///   LOD 的距离原本只取"观察点到 patch 矩形的平面距离"（XZ）。相机俯视整张地图时，
        ///   观察点落在图中心，中心 patch 的平面距离恒为 0 —— 无论相机升到多高，
        ///   中心都会一路细分到叶层，"拉远就该变粗"根本不会发生（实测 SplitFactor 调到 0.5
        ///   与 1 得到的分级几乎一样，就是这个原因）。
        ///   真实的可见精度取决于**视距**：相机在 4000 米高空时，脚下那块地也离你 4000 米，
        ///   看不到 5 单位级别的细节。因此距离按 <c>sqrt(平面距离² + (相机高度×权重)²)</c> 取，
        ///   相机持续拉高 → 所有 patch 的视距同步变大 → 分级连续变粗（这正是需要的"拉高降精度"）。
        ///
        /// 【与 morph 的同步】着色器的 morph 阈值必须用同一个距离，
        ///   所以 TerrainSangoRenderer 会把 ViewHeight 作为 _ViewHeight 传给地表着色器。
        /// </summary>
        public float HeightWeight = 1f;

        /// <summary>迟滞系数：已经分裂的节点要退到更远才合并，避免相机微动时反复切换。</summary>
        public float Hysteresis = 1.15f;

        /// <summary>单帧最多选多少个 patch（对齐 314 的 SelectionCapacity）。</summary>
        public int Capacity = 1024;

        /// <summary>
        /// 强制使用的 LOD 级别（-1 = 按距离自动选择）。
        ///
        /// 【为什么需要它】
        ///   自动 LOD 的观察点取"相机 XZ 投影到地图内"（见 ResolveEye），只要观察点落在根节点矩形内，
        ///   根节点到该交点的平面距离就是 0，永远满足 <c>distance &lt; SplitFactor * size</c>，
        ///   因此根层（全局最粗）在自动模式下永远不会被单独选中 —— 想观察/验收最粗级别，
        ///   只能靠这个开关把整幅地图锁死在指定层。
        ///
        /// 【取值】
        ///   0 = 叶层（满精度，本图 256 个 patch，顶点间距 = QuadSize）；
        ///   LevelCount - 1 = 根层（本图 level 4，整图 1 个 patch，顶点间距 16 个源格）。
        ///
        /// 【原版无对应实现】Open311 的地形是分块静态 mesh（MapTerrain 按 64×64 顶点切块、
        ///   每层一个 MeshRenderer），没有 LOD 选择器，因此不存在"强制级别"的对应物。
        ///   本字段是为新管线的验收/调试新增的设计，不承担任何业务语义，
        ///   默认值 -1 保证运行时行为与没有该字段时完全一致。
        /// </summary>
        public int ForceLevel = -1;

        /// <summary>视锥剔除时 AABB 在 Y 方向的扩展量。</summary>
        public float CullExtentY = 400f;

        /// <summary>节点引用（遍历用的栈元素，避免每帧分配）。</summary>
        private struct NodeRef
        {
            public int Level;
            public int GridX;
            public int GridZ;
        }

        private readonly List<TerrainSangoPatch> patches = new();
        private readonly Dictionary<long, bool> splitState = new();
        private readonly List<NodeRef> stack = new();
        private readonly Plane[] planes = new Plane[6];
        /// <summary>最粗层级（= source.LevelCount - 1）。IsEdgeCoarse 向上查更粗层时用。</summary>
        private int maxLevel;

        /// <summary>最近一次选择的 patch 列表。</summary>
        public IReadOnlyList<TerrainSangoPatch> Patches => patches;

        /// <summary>最近一次被视锥剔除的数量。</summary>
        public int CulledCount { get; private set; }

        /// <summary>最近一次可见的 patch 数量。</summary>
        public int VisibleCount => patches.Count - CulledCount;

        /// <summary>
        /// 最近一次选择使用的观察点（世界坐标，Y 落在地形中值平面上）。
        ///
        /// 必须原样传给着色器作为 morph 的距离原点：LOD 分裂与顶点收敛必须共用同一个点，
        /// 否则"何时分裂"由 A 点决定、"顶点何时收敛"由 B 点决定，LOD 与几何不再同步。
        /// </summary>
        public Vector3 Eye { get; private set; }

        /// <summary>
        /// 最近一次选择使用的"相机高度"（相机位置到地形中值平面的高度差 × HeightWeight）。
        ///
        /// 参与视距计算：<c>距离 = sqrt(平面距离² + ViewHeight²)</c>，因此相机越高、整体越粗。
        /// 着色器必须拿到同一个值（uniform _ViewHeight）才能让 morph 与 LOD 判定同步。
        /// </summary>
        public float ViewHeight { get; private set; }

        /// <summary>
        /// 用给定相机计算一次 LOD 选择。
        /// </summary>
        /// <param name="camera">观察相机。</param>
        /// <param name="source">地形数据源。</param>
        /// <returns>选中的 patch 列表（内部复用，下一次调用会被清空）。</returns>
        public IReadOnlyList<TerrainSangoPatch> Build(Camera camera, TerrainSangoSource source)
        {
            patches.Clear();
            CulledCount = 0;
            if (camera == null || source == null) return patches;

            // 观察点对外暴露：着色器的 morph 必须用它，而不是相机自身的世界坐标
            Eye = ResolveEye(camera, source);
            Vector3 eye = Eye;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);

            float mapMaxX = source.WorldHeight;   // 世界 X 方向的边长
            float mapMaxZ = source.WorldWidth;    // 世界 Z 方向的边长

            maxLevel = source.LevelCount - 1;

            // 强制级别：不做距离分裂，直接把指定层的全部 patch 铺满整图
            if (ForceLevel >= 0)
            {
                BuildForced(source, Mathf.Clamp(ForceLevel, 0, source.LevelCount - 1));
                MarkEdges(patches);
                CullPatches(source);
                return patches;
            }

            stack.Clear();
            stack.Add(new NodeRef { Level = source.LevelCount - 1, GridX = 0, GridZ = 0 });

            while (stack.Count > 0)
            {
                NodeRef node = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);

                float size = PatchWorldSize(source, node.Level);
                float originX = node.GridX * size;
                float originZ = node.GridZ * size;

                // 完全落在地图外的节点直接剪掉（根节点是正方形，非方形地图会有大片空白）
                if (originX >= mapMaxX || originZ >= mapMaxZ) continue;

                float distance = DistanceToRect(originX, originZ, size, eye);
                float threshold = SplitFactor * size;
                long key = MakeKey(node);
                bool wasSplit = splitState.TryGetValue(key, out bool previous) && previous;
                // 已分裂的节点要退到 threshold * Hysteresis 之外才合并
                bool canSplit = node.Level > 0
                                && distance < threshold * (wasSplit ? Hysteresis : 1f)
                                && patches.Count + stack.Count < Capacity;

                if (canSplit)
                {
                    splitState[key] = true;
                    int childLevel = node.Level - 1;
                    int cx = node.GridX << 1;
                    int cz = node.GridZ << 1;
                    stack.Add(new NodeRef { Level = childLevel, GridX = cx, GridZ = cz });
                    stack.Add(new NodeRef { Level = childLevel, GridX = cx + 1, GridZ = cz });
                    stack.Add(new NodeRef { Level = childLevel, GridX = cx, GridZ = cz + 1 });
                    stack.Add(new NodeRef { Level = childLevel, GridX = cx + 1, GridZ = cz + 1 });
                    continue;
                }

                splitState[key] = false;
                patches.Add(new TerrainSangoPatch
                {
                    Level = node.Level,
                    GridX = node.GridX,
                    GridZ = node.GridZ,
                    OriginX = originX,
                    OriginZ = originZ,
                    Size = size,
                    Distance = distance,
                });
            }

            // 选择完成后做视锥剔除（对齐 314：先选后剔）
            // 边界标记必须在剔除之后、返回之前算：用完整选中集（含被剔除的），
            // 保证视锥边界的 patch 拿到的邻居关系稳定，不会逐帧翻转
            CullPatches(source);
            MarkEdges(patches);

            // 状态表长期只增不减会漂，超过阈值直接清空（丢掉迟滞一次，无正确性影响）
            if (splitState.Count > 200000) splitState.Clear();

            return patches;
        }

        /// <summary>
        /// 按指定级别铺满整图（不分裂、不合并）。
        ///
        /// 【用途】验收/调试：需要把整幅地图锁死在某一层观察几何密度时使用。
        ///   本图（5120×5120，QuadSize=5，PatchCells=64）的实际含义：
        ///     level 0 → 256 个 patch，顶点间距 5  世界单位（= 1 个源格，满精度）
        ///     level 1 →  64 个 patch，顶点间距 10
        ///     level 2 →  16 个 patch，顶点间距 20
        ///     level 3 →   4 个 patch，顶点间距 40
        ///     level 4 →   1 个 patch，顶点间距 80（= 16 个源格，全局最粗）
        ///
        /// 【口径】每层仍然是同一张 65×65 顶点的共享网格，只是实例矩阵的世界尺寸不同，
        ///   因此不需要为任何层准备额外 mesh；绘制路径与自动 LOD 完全一致。
        /// </summary>
        private void BuildForced(TerrainSangoSource source, int level)
        {
            float size = PatchWorldSize(source, level);
            if (size <= 0f) return;

            // 世界 X 方向受 WorldHeight 约束，世界 Z 方向受 WorldWidth 约束（与自动遍历同口径）
            int countX = Mathf.Max(Mathf.CeilToInt(source.WorldHeight / size), 1);
            int countZ = Mathf.Max(Mathf.CeilToInt(source.WorldWidth / size), 1);

            for (int gz = 0; gz < countZ; gz++)
            {
                float originZ = gz * size;
                if (originZ >= source.WorldWidth) continue;
                for (int gx = 0; gx < countX; gx++)
                {
                    float originX = gx * size;
                    if (originX >= source.WorldHeight) continue;
                    patches.Add(new TerrainSangoPatch
                    {
                        Level = level,
                        GridX = gx,
                        GridZ = gz,
                        OriginX = originX,
                        OriginZ = originZ,
                        Size = size,
                        Distance = 0f,
                    });
                }
            }
        }

        /// <summary>
        /// 视锥剔除：给完全不在视锥内的 patch 打上 Culled 标记。
        ///
        /// 抽成独立方法是为了让"自动 LOD"与"强制级别"两条路径共用同一套剔除口径
        /// （对齐 314 的"先选后剔"：选择阶段不看视锥，只在选完之后剔除）。
        /// </summary>
        private void CullPatches(TerrainSangoSource source)
        {
            float minY = source.MinimumHeight - CullExtentY;
            float maxY = source.MaximumHeight + CullExtentY;
            float centerY = (minY + maxY) * 0.5f;
            float sizeY = maxY - minY;

            for (int i = 0; i < patches.Count; i++)
            {
                TerrainSangoPatch patch = patches[i];
                var bounds = new Bounds(
                    new Vector3(patch.OriginX + patch.Size * 0.5f, centerY, patch.OriginZ + patch.Size * 0.5f),
                    new Vector3(patch.Size, sizeY, patch.Size));
                if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                {
                    // 标记写回列表：绘制阶段据此跳过，不需要再重算一遍
                    patch.Culled = true;
                    patches[i] = patch;
                    CulledCount++;
                }
            }
        }

        /// <summary>某一层 patch 的世界边长。</summary>
        public static float PatchWorldSize(TerrainSangoSource source, int level)
            => source.LeafWorldSize * (1 << level);

        /// <summary>把节点打包成字典键。</summary>
        private static long MakeKey(NodeRef node)
            => ((long)node.Level << 42) | ((long)node.GridX << 21) | (long)node.GridZ;

        /// <summary>把 (level, 格坐标) 打包成与 <see cref="MakeKey(NodeRef)"/> 同口径的键。</summary>
        private static long MakeKey(int level, int gridX, int gridZ)
            => ((long)level << 42) | ((long)gridX << 21) | (long)gridZ;

        /// <summary>四邻标记位：东（X+）/ 西（X-）/ 北（Z+）/ 南（Z-），对应 EdgeCoarse 的 bit0~3。</summary>
        private const int EdgeEast = 1, EdgeWest = 2, EdgeNorth = 4, EdgeSouth = 8;

        /// <summary>本帧全部选中节点的键集合（含被剔除的）。MarkEdges 用它判同层邻居。</summary>
        private readonly HashSet<long> selectedKeys = new HashSet<long>();

        /// <summary>
        /// 为每个选中 patch 计算四边缝合标记：
        /// 邻居更粗 → <see cref="TerrainSangoPatch.EdgeCoarse"/>（细侧锁 1），
        /// 邻居更细 → <see cref="TerrainSangoPatch.EdgeFine"/>（粗侧锁 0）。
        ///
        /// 【为什么必须双向】T 型接缝是共享网格实例化方案的固有问题：细层 patch 挨着更粗层时，
        /// 两侧边界顶点密度差 2 倍。单靠细侧锁 1（吸到父层格点 = 粗侧的原始格点位置）还不够 ——
        /// 粗层这一边此时正处于自己的 morph 带内（d ∈ [T粗, 2T粗]），它的边界顶点在
        /// "父层格点"与"自身格点"之间连续滑动；细侧锁死、粗侧在动，共享边界照样撕裂。
        /// 粗侧锁 0（保持原始 64 格位置）后，两侧边界顶点逐点重合（间距都是 S/32），缝才真正消失。
        ///
        /// 【判定规则】对 patch 的每条边，看邻居格位的层级：
        ///   · 同层 patch → 不锁。两侧 morph 公式相同（threshold 同、共享边界上
        ///     顶点到观察点的距离相同），顶点位置天然一致；
        ///   · 被更粗层覆盖 → 本块是细侧，EdgeCoarse 置位（锁 1）；
        ///   · 被更细层瓜分 → 本块是粗侧，EdgeFine 置位（锁 0，不许随自身 morph 滑动）；
        ///   · 地图外 → 沿用旧口径，EdgeCoarse 置位（那一侧没有对接对象，锁了无害）。
        ///
        /// 【为什么用选中集而不是可见集】视锥剔除逐帧抖动，按可见集算会让剔除边界上的
        /// 锁定位反复翻转、几何闪烁；选中集（剔除标记之前）是稳定的拓扑关系。
        /// </summary>
        private void MarkEdges(List<TerrainSangoPatch> patches)
        {
            selectedKeys.Clear();
            for (int i = 0; i < patches.Count; i++)
                selectedKeys.Add(MakeKey(patches[i].Level, patches[i].GridX, patches[i].GridZ));

            for (int i = 0; i < patches.Count; i++)
            {
                TerrainSangoPatch p = patches[i];
                int coarse = 0, fine = 0;
                AccumulateEdgeMark(p.Level, p.GridX + 1, p.GridZ, EdgeEast, ref coarse, ref fine);
                AccumulateEdgeMark(p.Level, p.GridX - 1, p.GridZ, EdgeWest, ref coarse, ref fine);
                AccumulateEdgeMark(p.Level, p.GridX, p.GridZ + 1, EdgeNorth, ref coarse, ref fine);
                AccumulateEdgeMark(p.Level, p.GridX, p.GridZ - 1, EdgeSouth, ref coarse, ref fine);
                p.EdgeCoarse = coarse;
                p.EdgeFine = fine;
                patches[i] = p;
            }
        }

        /// <summary>
        /// 归类某条边外侧的邻居层级关系并累计到对应标记。
        /// </summary>
        private void AccumulateEdgeMark(int level, int gridX, int gridZ, int flag, ref int coarse, ref int fine)
        {
            EdgeNeighbor neighbor = ClassifyEdgeNeighbor(level, gridX, gridZ);
            if (neighbor == EdgeNeighbor.Coarser)
                coarse |= flag;         // 我是细侧：锁 1，吸到父层格点（= 粗侧原始格点位置）
            else if (neighbor == EdgeNeighbor.Finer)
                fine |= flag;           // 我是粗侧：锁 0，不许随自身 morph 滑动
            else if (neighbor == EdgeNeighbor.Outside)
                coarse |= flag;         // 地图外：保持旧口径（锁 1），那一侧没有对接对象
            // Same：两侧 morph 公式相同，天然重合，不锁
        }

        /// <summary>某条边外侧的邻居层级关系。</summary>
        private enum EdgeNeighbor
        {
            /// <summary>同层 patch 就在：两侧 morph 公式相同，天然重合。</summary>
            Same,
            /// <summary>被更粗层覆盖：本块是细侧，边界顶点要吸到父层格点。</summary>
            Coarser,
            /// <summary>被更细层瓜分：本块是粗侧，边界顶点必须保持原始格点。</summary>
            Finer,
            /// <summary>地图外：没有对接对象。</summary>
            Outside,
        }

        /// <summary>
        /// 判断 (level, gridX, gridZ) 格位外侧被哪个层级的 patch 覆盖。
        /// 查找顺序：同层 → 更粗层 → 更细层 → 地图外。
        /// </summary>
        private EdgeNeighbor ClassifyEdgeNeighbor(int level, int gridX, int gridZ)
        {
            // 同层邻居就在：两侧 morph 公式相同、共享边界距离相同，天然重合
            if (selectedKeys.Contains(MakeKey(level, gridX, gridZ)))
                return EdgeNeighbor.Same;

            // 向上查：该格位被某个更粗层的 patch 覆盖 → 我是细侧，要锁 1
            // （level L 的格坐标右移 1 位就是 level L+1 的格坐标，见 MakeKey 的打包口径）
            for (int l = level + 1; l <= maxLevel; l++)
            {
                if (selectedKeys.Contains(MakeKey(l, gridX >> (l - level), gridZ >> (l - level))))
                    return EdgeNeighbor.Coarser;
            }

            // 向下查：该格位被更细层的 patch 瓜分 → 我是粗侧，要锁 0
            // （细侧查它的同层邻居时找不到、向上查会命中我，由它锁）
            if (level > 0)
            {
                int childX = gridX << 1;
                int childZ = gridZ << 1;
                if (selectedKeys.Contains(MakeKey(level - 1, childX, childZ)) ||
                    selectedKeys.Contains(MakeKey(level - 1, childX + 1, childZ)) ||
                    selectedKeys.Contains(MakeKey(level - 1, childX, childZ + 1)) ||
                    selectedKeys.Contains(MakeKey(level - 1, childX + 1, childZ + 1)))
                    return EdgeNeighbor.Finer;
            }

            // 剩下的只有地图外：没有对接对象
            return EdgeNeighbor.Outside;
        }


        /// <summary>
        /// 点到节点的视距：平面最近距离与相机高度的合成（点落在矩形内时只剩高度项）。
        ///
        /// 【为什么不是纯平面距离】见 HeightWeight 的注释：俯视全图时观察点在中心，
        /// 中心 patch 的平面距离恒为 0，相机升到多高都不会变粗。合成后：
        ///   相机贴近地面 → 约等于平面距离（近细远粗）；
        ///   相机持续拉高 → 所有 patch 的视距趋近于相机高度 → 整体逐级变粗。
        /// </summary>
        private float DistanceToRect(float originX, float originZ, float size, Vector3 point)
        {
            float dx = Mathf.Clamp(point.x, originX, originX + size) - point.x;
            float dz = Mathf.Clamp(point.z, originZ, originZ + size) - point.z;
            return Mathf.Sqrt(dx * dx + dz * dz + ViewHeight * ViewHeight);
        }

        /// <summary>
        /// 取用于距离判定的观察点。
        ///
        /// 【为什么不再取"视线与地面的交点"】
        ///   旧实现用视线与地形中值平面的交点，相机看哪儿就细分哪儿；但它有一个致命副作用：
        ///   morph 的距离原点也是同一个 Eye（shader 里的 _EyePos），而交点随**相机姿态**
        ///   （俯仰角在 22.5°~70° 内）非线性滑动 —— 俯仰变 1°，交点在地面上可能跳几百单位；
        ///   视线落出地图时又切到 fallback，再突跳一次。Eye 一跳，全图每个顶点的 morph
        ///   同时重排，地形整体跟着变形 —— 表现为"相机一移动高度剧烈抖动"。
        ///
        ///   现改为恒定返回"相机 XZ 投影到地图内"：相机平移 / 旋转时 Eye 连续平滑，
        ///   morph 只随相机位置连续变化。Scene 视图（相机被推远框住整图）由 clamp 兜底，
        ///   细分会集中在观察点附近而不是视野中心 —— 运行时主相机不受影响，
        ///   Scene 视图验收时知道这一点即可。
        /// </summary>
        private Vector3 ResolveEye(Camera camera, TerrainSangoSource source)
        {
            Vector3 eye = camera.transform.position;
            float planeY = (source.MinimumHeight + source.MaximumHeight) * 0.5f;

            // 相机高度计入视距：这是"相机持续拉高 → 精度持续下降"的唯一来源。
            // 纯平面距离与相机高度无关（俯视时中心恒为 0），必须在这里把高度记下来。
            ViewHeight = Mathf.Max(0f, eye.y - planeY) * Mathf.Max(HeightWeight, 0f);

            return new Vector3(
                Mathf.Clamp(eye.x, 0f, Mathf.Max(source.WorldHeight, 0f)),
                planeY,
                Mathf.Clamp(eye.z, 0f, Mathf.Max(source.WorldWidth, 0f)));
        }
    }
}
