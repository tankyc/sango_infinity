using System;
using System.Collections.Generic;
using System.IO;
// 见 Terrain314Types.cs：屏蔽 Sango.Path / Sango.Directory / Sango.File 的遮蔽
using UnityEngine;

namespace Sango.Render.Terrain314
{
    using Path = System.IO.Path;
    using Directory = System.IO.Directory;
    using File = System.IO.File;

    /// <summary>spatial.json 的顶层结构（对齐导出器 SpatialFileMeta）。</summary>
    [Serializable]
    public class Terrain314SpatialFile
    {
        public float RootSize;
        public int LevelCount;
        public Terrain314SpatialNodeJson[] Nodes;
    }

    /// <summary>spatial.json 的节点结构。</summary>
    [Serializable]
    public class Terrain314SpatialNodeJson
    {
        public int Index;
        public int Level;
        public int GridX;
        public int GridY;
        public float Size;
        public float SampleMinimum;
        public float SampleMaximum;
        public uint Identifier20;
    }

    /// <summary>patches.json 的顶层结构（对齐导出器 PatchFileMeta）。</summary>
    [Serializable]
    public class Terrain314PatchFile
    {
        public int PatchVertices;
        public int LeafLevel;
        public int Count;
        public Terrain314PatchMetaJson[] Patches;
    }

    /// <summary>patches.json 的叶 patch 结构。</summary>
    [Serializable]
    public class Terrain314PatchMetaJson
    {
        public int NodeIndex;
        public int GridX;
        public int GridY;
        public float OriginX;
        public float OriginZ;
        public float Size;
        public int GridCells;
        public int GridVertices;
        public float SampleMinimum;
        public float SampleMaximum;
    }

    /// <summary>height.json 的元数据。</summary>
    [Serializable]
    public class Terrain314HeightFile
    {
        public int CellWidth;
        public int CellHeight;
        public int SampleWidth;
        public int SampleHeight;
        public float SampleSpacing;
        public float MinimumHeight;
        public float MaximumHeight;
        public float Range;
        public string Format;
        public uint FileId;
    }

    /// <summary>
    /// 314 地形资源加载与 patch 参数推导。
    ///
    /// 资源来源：由 D:\San14TerrainExport 从本机 314 原版安装的
    /// FieldEditor4.rdb 导出（MHK 高度场 + 空间四叉树）。
    ///
    /// patch 参数推导逐条对齐 314 的 TerrainPatchInputMath.Ordinary：
    ///   size   = heightWidth / GridDivisors[LevelCount] + 1
    ///   side   = 1 &lt;&lt; (LevelCount - node.Level - 1)
    ///   Origin = (GridX * size_world, 0, GridY * size_world)
    ///   Extent = (size_world, 1, size_world)
    ///   Uv     = (GridX/side, GridY/side, (GridX+1)/side, (GridY+1)/side)
    ///   GridSize = size（顶点边长，非格数）
    ///   ControlFrequency = (controlWidth - 1) / side
    ///
    /// 【坐标约定】加载阶段已把高度场与空间树的 X/Z 分量互换，因此此处的
    /// GridX 表示南向索引（世界 X）、GridY 表示东向索引（世界 Z）。
    /// 公式本身不变，只是两个分量的物理含义换过了。
    /// </summary>
    public sealed class Terrain314Source
    {
        /// <summary>高度场。</summary>
        public Terrain314HeightField Height { get; private set; }
        /// <summary>四叉树全部节点。</summary>
        public Terrain314SpatialNode[] Nodes { get; private set; }
        /// <summary>根尺寸。</summary>
        public float RootSize { get; private set; }
        /// <summary>层数。</summary>
        public int LevelCount { get; private set; }
        /// <summary>控制图边长（暂用高度场格宽，314 原版为独立控制图资源）。</summary>
        public int ControlWidth { get; private set; }

        private readonly Dictionary<int, List<int>> childrenOf = new();

        /// <summary>
        /// 从导出目录加载。
        ///
        /// 目录需包含 height.r32 / height.json / spatial.json / patches.json。
        /// </summary>
        public static Terrain314Source Load(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Terrain314 资源目录不存在: {directory}");
            }

            var source = new Terrain314Source();

            var heightMeta = JsonUtility.FromJson<Terrain314HeightFile>(
                File.ReadAllText(System.IO.Path.Combine(directory, "height.json")));
            // transposeXZ: 导出数据是"列 = 东、行 = 南"，项目最终要求 +Z 东 / +X 南，
            // 因此在加载阶段就互换 X/Z 分量，下游不再做第二次交换。
            source.Height = Terrain314HeightField.LoadR32(
                System.IO.Path.Combine(directory, "height.r32"),
                heightMeta.CellWidth, heightMeta.CellHeight, heightMeta.SampleSpacing,
                heightMeta.MinimumHeight, heightMeta.MaximumHeight, transposeXZ: true);

            var spatial = JsonUtility.FromJson<Terrain314SpatialFile>(
                File.ReadAllText(System.IO.Path.Combine(directory, "spatial.json")));
            source.RootSize = spatial.RootSize;
            source.LevelCount = spatial.LevelCount;
            // 控制图与高度场共用行列索引，转置后宽度取旧行数（方形地图与原值相同）
            source.ControlWidth = source.Height.CellWidth;

            source.Nodes = new Terrain314SpatialNode[spatial.Nodes.Length];
            for (int i = 0; i < spatial.Nodes.Length; i++)
            {
                Terrain314SpatialNodeJson node = spatial.Nodes[i];
                source.Nodes[i] = new Terrain314SpatialNode
                {
                    Index = node.Index,
                    Level = node.Level,
                    // 空间树与高度场必须同步转置，否则 patch 位置与高度错位
                    GridX = node.GridY,
                    GridY = node.GridX,
                    Size = node.Size,
                    SampleMinimum = node.SampleMinimum,
                    SampleMaximum = node.SampleMaximum,
                    Identifier20 = node.Identifier20
                };
            }

            source.BuildChildIndex();
            return source;
        }

        /// <summary>
        /// 建立"父节点 → 四个子节点"的索引。
        ///
        /// 314 的树按层顺序存储：第 0 层 1 个、第 1 层 4 个……
        /// 父节点在 (level+1, gx/2, gy/2)，子节点按 (2gx..2gx+1, 2gy..2gy+1) 排列。
        /// </summary>
        private void BuildChildIndex()
        {
            childrenOf.Clear();
            var byLevelGrid = new Dictionary<(int Level, int X, int Y), int>(Nodes.Length);
            for (int i = 0; i < Nodes.Length; i++)
            {
                byLevelGrid[(Nodes[i].Level, Nodes[i].GridX, Nodes[i].GridY)] = i;
            }

            foreach (Terrain314SpatialNode node in Nodes)
            {
                if (node.Level + 1 >= LevelCount) continue;
                if (!byLevelGrid.TryGetValue((node.Level + 1, node.GridX >> 1, node.GridY >> 1),
                        out int parent)) continue;
                if (!childrenOf.TryGetValue(parent, out List<int> list))
                {
                    list = new List<int>(4);
                    childrenOf[parent] = list;
                }
                list.Add(node.Index);
            }
        }

        /// <summary>取某节点的子节点下标；叶节点返回空。</summary>
        public IReadOnlyList<int> ChildrenOf(int nodeIndex) =>
            childrenOf.TryGetValue(nodeIndex, out List<int> list)
                ? list
                : (IReadOnlyList<int>)Array.Empty<int>();

        /// <summary>
        /// 按 314 的 TerrainPatchInputMath.Ordinary 推导一个节点的 patch 实例参数。
        /// </summary>
        public Terrain314PatchInput BuildPatchInput(Terrain314SpatialNode node, uint edgeFlags = 0)
        {
            if (LevelCount < 1 || LevelCount >= Terrain314Constants.GridDivisors.Length)
            {
                throw new InvalidOperationException($"Terrain314 层数非法: {LevelCount}");
            }

            int size = Height.CellWidth / Terrain314Constants.GridDivisors[LevelCount] + 1;
            int side = 1 << (LevelCount - node.Level - 1);

            if (node.GridX < 0 || node.GridX >= side || node.GridY < 0 || node.GridY >= side)
            {
                throw new InvalidOperationException(
                    $"Terrain314 节点 {node.Index} 的格坐标 ({node.GridX},{node.GridY}) 超出该层范围 {side}");
            }

            return new Terrain314PatchInput
            {
                Origin = new Vector3(node.GridX * node.Size, 0f, node.GridY * node.Size),
                Extent = new Vector3(node.Size, 1f, node.Size),
                UvBounds = new Vector4(
                    (float)node.GridX / side,
                    (float)node.GridY / side,
                    (float)(node.GridX + 1) / side,
                    (float)(node.GridY + 1) / side),
                EdgeFlags = edgeFlags,
                GridSize = (uint)size,
                ControlFrequency = (ControlWidth - 1) / (float)side,
                ControlCount = ControlWidth
            };
        }
    }
}
