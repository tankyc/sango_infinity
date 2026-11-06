using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Render.Terrain314
{
    /// <summary>一个被选中的 patch。</summary>
    public struct Terrain314SelectedPatch
    {
        /// <summary>节点下标（用于缓存 mesh）。</summary>
        public int NodeIndex;
        /// <summary>层级。</summary>
        public int Level;
        /// <summary>该节点世界尺寸。</summary>
        public float Size;
        /// <summary>到相机的水平距离。</summary>
        public float Distance;
        /// <summary>是否被视锥剔除（剔除项仍保留用于统计）。</summary>
        public bool Culled;
    }

    /// <summary>一次 LOD 选择的完整结果。</summary>
    public sealed class Terrain314LodPlan
    {
        /// <summary>选中的 patch 列表（已按 Level → Distance → Index 排序）。</summary>
        public List<Terrain314SelectedPatch> Patches { get; } = new();
        /// <summary>绘制批次。</summary>
        public List<Terrain314Batch> Batches { get; } = new();
        /// <summary>被剔除的数量。</summary>
        public int CulledCount { get; internal set; }
        /// <summary>选中但未被剔除的数量。</summary>
        public int VisibleCount => Patches.Count - CulledCount;

        internal void Clear()
        {
            Patches.Clear();
            Batches.Clear();
            CulledCount = 0;
        }
    }

    /// <summary>
    /// 314 式地形 LOD 选择与视锥剔除。
    ///
    /// 对齐依据（314 TerrainLodSelectionMath / TerrainLodDrawPlan）：
    ///   1. 四叉树按"水平距离"递归分裂，与视锥无关；
    ///      距离强制取 Y = 0，即只比平面距离。
    ///      分裂阈值 = distanceScale * sqrt(2) * (nodeSize / 2)。
    ///   2. 选择完成后再排序：Level → Distance → Index。
    ///   3. 视锥剔除发生在选择之后，AABB 的 Y 上下各扩 R1000。
    ///   4. 分三组批次：叶层前 4 个为 group0，其余叶层为 group1，非叶层为 group2（coarse）。
    ///
    /// 与 314 的差异：314 的 AABB 扩展到 ±1000 用的是它自己的世界尺度，
    /// 本体系世界尺度不同（RootSize 204800），因此 ExtentY 可配置。
    /// </summary>
    public sealed class Terrain314Lod
    {
        private readonly Terrain314Source source;
        private readonly Terrain314LodPlan plan = new();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private readonly List<int> visitStack = new();

        /// <summary>分裂距离系数，越大越晚分裂（越粗糙）。</summary>
        public float DistanceScale = 1.0f;

        /// <summary>视锥剔除时 AABB 在 Y 方向的扩展量。</summary>
        public float CullExtentY = 1000f;

        /// <summary>叶层前多少个 patch 归入 group0。</summary>
        public int NearGroupSize = 4;

        public Terrain314Lod(Terrain314Source source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// 用给定相机重新计算一次 LOD 计划。
        /// </summary>
        public Terrain314LodPlan Build(Camera camera)
        {
            plan.Clear();
            if (camera == null) return plan;

            Vector3 eye = ResolveEye(camera);
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);

            // 根节点是最高层（Level = LevelCount - 1）
            int rootIndex = FindRootIndex();
            if (rootIndex < 0) return plan;

            // 1) 四叉树距离分裂
            visitStack.Clear();
            visitStack.Add(rootIndex);
            while (visitStack.Count > 0)
            {
                int index = visitStack[^1];
                visitStack.RemoveAt(visitStack.Count - 1);

                Terrain314SpatialNode node = source.Nodes[index];
                // 对齐 314 TerrainLodSelectionMath.PlanarDistance：到矩形（AABB）的距离，
                // 相机落在矩形内时距离为 0，不是到中心的距离。
                float planarDistance = DistanceToRectangle(node, eye);

                // 对齐 314 SplitThreshold：distanceScale * sqrt(2) * (nodeSize / 2)
                float splitThreshold = DistanceScale * 1.41421356f * (node.Size * 0.5f);
                IReadOnlyList<int> children = source.ChildrenOf(index);

                // 对齐 314 SelectCore：叶层无条件终止；否则"距离 > 阈值"时停止细分并选中，
                // 距离越近才继续向下细分（此前方向写反，导致整图只选出一个粗 patch）。
                bool terminal = node.Level == 0;
                bool stop = terminal || planarDistance > splitThreshold ||
                            children.Count == 0 || plan.Patches.Count >= SelectionCapacity;

                if (!stop)
                {
                    for (int i = 0; i < children.Count; i++) visitStack.Add(children[i]);
                    continue;
                }

                plan.Patches.Add(new Terrain314SelectedPatch
                {
                    NodeIndex = index,
                    Level = node.Level,
                    Size = node.Size,
                    Distance = planarDistance,
                    Culled = false
                });
            }

            // 2) 排序：Level → Distance → Index
            plan.Patches.Sort(static (left, right) =>
            {
                int order = left.Level.CompareTo(right.Level);
                if (order != 0) return order;
                order = left.Distance.CompareTo(right.Distance);
                return order != 0 ? order : left.NodeIndex.CompareTo(right.NodeIndex);
            });

            // 3) 视锥剔除（选择之后）
            // 剔除包围盒的 Y 范围必须按高度场真实取值，不能照搬 314 的 ±1000：
            // 314 的 ±1000 是它自己的世界尺度，本体系高度场是 0~6966，
            // 只在 0 上下各扩 CullExtentY 会让低空视角的视锥与盒子完全不相交，
            // 大片本应可见的地形被整块剔除（表现为"什么都没渲染"）。
            float minY = -CullExtentY;
            float maxY = CullExtentY;
            if (source.Height != null)
            {
                minY = source.Height.MinimumHeight - CullExtentY;
                maxY = source.Height.MaximumHeight + CullExtentY;
            }
            float centerY = (minY + maxY) * 0.5f;
            float sizeY = maxY - minY;

            for (int i = 0; i < plan.Patches.Count; i++)
            {
                Terrain314SelectedPatch patch = plan.Patches[i];
                Terrain314SpatialNode node = source.Nodes[patch.NodeIndex];
                Bounds bounds = new(
                    new Vector3(node.GridX * node.Size + node.Size * 0.5f, centerY,
                        node.GridY * node.Size + node.Size * 0.5f),
                    new Vector3(node.Size, sizeY, node.Size));
                bool visible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
                if (!visible)
                {
                    patch.Culled = true;
                    plan.CulledCount++;
                    plan.Patches[i] = patch;
                }
            }

            // 4) 分批次：叶层 group0/group1，非叶层 group2（coarse）
            int leafGroupRemaining = NearGroupSize;
            for (int i = 0; i < plan.Patches.Count; i++)
            {
                Terrain314SelectedPatch patch = plan.Patches[i];
                if (patch.Culled) continue;

                int group;
                if (patch.Level == 0)
                {
                    group = leafGroupRemaining-- > 0 ? 0 : 1;
                }
                else
                {
                    group = 2;
                }

                if (plan.Batches.Count > 0)
                {
                    Terrain314Batch last = plan.Batches[^1];
                    if (last.Group == group && last.StartInstance + last.InstanceCount == i)
                    {
                        last.InstanceCount++;
                        plan.Batches[^1] = last;
                        continue;
                    }
                }
                plan.Batches.Add(new Terrain314Batch
                {
                    Group = group,
                    StartInstance = i,
                    InstanceCount = 1,
                    Coarse = group == 2
                });
            }

            return plan;
        }

        /// <summary>
        /// 选中 patch 的容量上限，对齐 314 TerrainLodSelectionMath.DefaultPrimaryCapacity。
        /// </summary>
        public const int SelectionCapacity = 1024;

        /// <summary>
        /// 取用于四叉树距离判定的观察点。
        ///
        /// 不能直接用 camera.transform.position：Scene 视图相机的距离由 size 推导，
        /// 框住整张图时它会被推到离场景几十万单位处（正交和透视都一样），
        /// 此时所有节点的平面距离都超过分裂阈值，整张图只选中一个最粗的 patch，
        /// 表现就是"Scene 视图里什么都不渲染"。
        ///
        /// 这里统一改用"视线与地形中值平面的交点"：相机看哪儿就细分哪儿，
        /// 距离远近不影响结论。交点飞出地图或视线朝上时退回相机位置的水平分量。
        /// </summary>
        public Vector3 ResolveEye(Camera camera)
        {
            Vector3 eye = camera.transform.position;
            float root = source.RootSize;
            float planeY = source.Height != null
                ? (source.Height.MinimumHeight + source.Height.MaximumHeight) * 0.5f
                : 0f;
            Vector3 fallback = new(Mathf.Clamp(eye.x, 0f, root), planeY, Mathf.Clamp(eye.z, 0f, root));

            Vector3 forward = camera.transform.forward;
            if (Mathf.Abs(forward.y) < 1e-4f) return fallback;

            float distance = (planeY - eye.y) / forward.y;
            if (distance <= 0f) return fallback;

            Vector3 hit = eye + forward * distance;
            if (hit.x < 0f || hit.z < 0f || hit.x > root || hit.z > root) return fallback;
            return hit;
        }

        /// <summary>
        /// 点到节点矩形的水平距离，对齐 314 PlanarDistance：
        /// 先把点钳到矩形范围内再求差，因此点在矩形内时距离为 0。
        /// Y 分量恒为 0，即只在平面上比较。
        /// </summary>
        private static float DistanceToRectangle(Terrain314SpatialNode node, Vector3 point)
        {
            float minX = node.GridX * node.Size;
            float minZ = node.GridY * node.Size;
            float dx = Mathf.Clamp(point.x, minX, minX + node.Size) - point.x;
            float dz = Mathf.Clamp(point.z, minZ, minZ + node.Size) - point.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private int FindRootIndex()
        {
            for (int i = 0; i < source.Nodes.Length; i++)
            {
                if (source.Nodes[i].Level == source.LevelCount - 1) return i;
            }
            return -1;
        }
    }
}
