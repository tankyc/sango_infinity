using System;
using System.Buffers.Binary;
using System.IO;
// 注意：项目在 Sango 命名空间下自带 Path / Directory / File 类型，
// 而本文件的命名空间 Sango.Render.Terrain314 位于其下，
// 直接写 Path.Combine 会被解析成 Sango.Path。别名优先级更高，可强制指向 System.IO。
using UnityEngine;

namespace Sango.Render.Terrain314
{
    // 项目在 Sango 命名空间下自带 Path / Directory / File 三个静态类，
    // 而本命名空间位于 Sango 之下，直接写 Path.Combine 会被解析成 Sango.Path（CS0117）。
    // 别名必须声明在命名空间内部才能压过外层命名空间成员。
    using Path = System.IO.Path;
    using Directory = System.IO.Directory;
    using File = System.IO.File;

    /// <summary>
    /// 314 式地形渲染体系的类型定义。
    ///
    /// 【独立体系说明】
    /// 本目录是一套与现有 MapTerrain / MapLayer 完全并列的地形渲染方案，
    /// 不注册进 MapRender，不修改任何现有类，仅通过 Terrain314Renderer
    /// 挂到一个独立 GameObject 上工作。
    ///
    /// 【对齐依据】
    /// 数据结构逐字段对齐 三国志14 复刻项目（D:\San14）：
    ///   src/San14.Data/Rendering/Terrain/TerrainPatchInputMath.cs:8-24（56 字节实例布局）
    ///   src/San14.Data/Rendering/Terrain/TerrainPatchInputMath.cs:29,39-50（patch 参数推导）
    ///   src/San14.Data/Formats/Terrain/SpatialTreeReader.cs:6-11（四叉树节点）
    ///
    /// 【世界坐标约定】
    /// 本体系最终使用的坐标系为：**+Z 指向东、+X 指向南、+Y 向上**（左手于南北-东西平面）。
    /// 314 导出的高度场与空间树是"列 = 东、行 = 南"的排列，
    /// 因此在 Terrain314Source.Load 加载阶段就把数据的 X/Z 分量整体互换（见 LoadR32 的
    /// transposeXZ 参数），使内存里的 Samples 直接满足：
    ///   列索引 ← 世界 X（南向）
    ///   行索引 ← 世界 Z（东向）
    /// 下游（patch 原点、LOD、网格、着色器）不得再自行交换一次。
    /// </summary>
    public static class Terrain314Constants
    {
        /// <summary>每个 patch 的顶点边长（65 = 64 格 + 1）。对齐 314 的 65x65 采样模式。</summary>
        public const int PatchVertices = 65;

        /// <summary>每个 patch 覆盖的地形格数。</summary>
        public const int PatchCells = PatchVertices - 1;

        /// <summary>
        /// 314 的层数除数表。
        /// 对应 314 TerrainPatchInputMath.cs:29 的 GridDivisors，
        /// 索引 0 占位，索引 N 表示"N 层树的根 patch 顶点边长除数"。
        /// </summary>
        public static readonly int[] GridDivisors = { 0, 1, 2, 4, 8, 16, 32 };
    }

    /// <summary>
    /// 空间四叉树节点，对齐 314 的 SpatialTreeNode。
    ///
    /// 314 的树是固定形状：6 层、1365 个节点（1 + 4 + 16 + 64 + 256 + 1024）。
    /// Minimum / Maximum 描述该节点自身 65x65 采样高度网格的极值，
    /// 不是整棵子树的包围范围。
    /// </summary>
    [Serializable]
    public struct Terrain314SpatialNode
    {
        /// <summary>节点在树中的序号（层内 Morton 序）。</summary>
        public int Index;
        /// <summary>层级，0 为最深的叶层。</summary>
        public int Level;
        /// <summary>该层内的格坐标 X。</summary>
        public int GridX;
        /// <summary>该层内的格坐标 Y。</summary>
        public int GridY;
        /// <summary>该节点的世界尺寸。</summary>
        public float Size;
        /// <summary>本节点采样高度最小值。</summary>
        public float SampleMinimum;
        /// <summary>本节点采样高度最大值。</summary>
        public float SampleMaximum;
        /// <summary>来源保留标识，语义未知，不得擅自解释为模型 Id。</summary>
        public uint Identifier20;
    }

    /// <summary>
    /// 单个地形 patch 的实例数据，共 56 字节。
    ///
    /// 布局与 314 完全一致（TerrainPatchInputMath.cs:11-23）：
    ///   0  Origin.X            4  Origin.Y            8  Origin.Z
    ///  12  Extent.X           16  Extent.Y           20  Extent.Z
    ///  24  UvBounds.X         28  UvBounds.Y         32  UvBounds.Z        36  UvBounds.W
    ///  40  EdgeFlags(uint)    44  GridSize(uint)
    ///  48  ControlFrequency   52  ControlCount
    ///
    /// 其中 GridSize 在 314 里是"顶点边长"（本作 65），不是格数。
    /// EdgeFlags 低 16 位是四条边的缝合位（每边 4 bit），高 16 位必须为 0。
    /// </summary>
    public struct Terrain314PatchInput
    {
        /// <summary>
        /// patch 世界原点（XZ 平面，Y 恒为 0；高度由高度图采样）。
        /// X 为南向、Z 为东向，由 Terrain314Source.BuildPatchInput 用已交换过 X/Z 的节点坐标推导。
        /// </summary>
        public Vector3 Origin;
        /// <summary>patch 世界范围（X / Z 为边长，Y 恒为 1）。</summary>
        public Vector3 Extent;
        /// <summary>在整张地形上的归一化 UV 范围（U0, V0, U1, V1）。</summary>
        public Vector4 UvBounds;
        /// <summary>四条边的 LOD 缝合标记，低 16 位有效。</summary>
        public uint EdgeFlags;
        /// <summary>顶点边长（65）。</summary>
        public uint GridSize;
        /// <summary>控制图频率：(controlWidth - 1) / side。</summary>
        public float ControlFrequency;
        /// <summary>控制图边长。</summary>
        public float ControlCount;

        /// <summary>编码成 56 字节实例流，小端，布局与 314 一致。</summary>
        public byte[] EncodeInstance()
        {
            byte[] bytes = new byte[56];
            float[] first =
            {
                Origin.x, Origin.y, Origin.z,
                Extent.x, Extent.y, Extent.z,
                UvBounds.x, UvBounds.y, UvBounds.z, UvBounds.w
            };
            for (int i = 0; i < first.Length; i++)
            {
                // Unity 2021.3 的 netstandard2.1 没有 BinaryPrimitives.Read/WriteSingleLittleEndian
                // （那是 .NET 5+ API），这里统一走 int 位模式转换。
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4),
                    BitConverter.SingleToInt32Bits(first[i]));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40, 4), EdgeFlags);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44, 4), GridSize);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(48, 4),
                BitConverter.SingleToInt32Bits(ControlFrequency));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52, 4),
                BitConverter.SingleToInt32Bits(ControlCount));
            return bytes;
        }
    }

    /// <summary>
    /// 一个绘制批次。对齐 314 的 batches.txt 行（group / start / count / coarse）。
    /// </summary>
    public struct Terrain314Batch
    {
        /// <summary>批次分组：0/1 为 near 程序，2 为 coarse 程序。</summary>
        public int Group;
        /// <summary>起始实例下标。</summary>
        public int StartInstance;
        /// <summary>实例数量。</summary>
        public int InstanceCount;
        /// <summary>是否使用 coarse（非叶层）程序。</summary>
        public bool Coarse;
    }

    /// <summary>
    /// 高度场。对齐 314 的 MHK 高度场（MhkHeightFieldReader.cs）。
    ///
    /// 314 的采样是 (CellWidth + 1) x (CellHeight + 1) 个点，
    /// 每点 24 位量化，按 Minimum + t * (Maximum - Minimum) 还原。
    /// </summary>
    public sealed class Terrain314HeightField
    {
        /// <summary>格宽。</summary>
        public int CellWidth { get; }
        /// <summary>格高。</summary>
        public int CellHeight { get; }
        /// <summary>采样间距（世界单位）。</summary>
        public float SampleSpacing { get; }
        /// <summary>高度最小值。</summary>
        public float MinimumHeight { get; }
        /// <summary>高度最大值。</summary>
        public float MaximumHeight { get; }

        /// <summary>采样宽度 = CellWidth + 1。</summary>
        public int SampleWidth => CellWidth + 1;
        /// <summary>采样高度 = CellHeight + 1。</summary>
        public int SampleHeight => CellHeight + 1;
        /// <summary>高度区间。</summary>
        public float Range => MaximumHeight - MinimumHeight;
        /// <summary>原始浮点采样（行优先）。</summary>
        public float[] Samples { get; }
        /// <summary>整张地形的世界边长。</summary>
        public float WorldSize => CellWidth * SampleSpacing;

        public Terrain314HeightField(int cellWidth, int cellHeight, float sampleSpacing,
            float minimumHeight, float maximumHeight, float[] samples)
        {
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            SampleSpacing = sampleSpacing;
            MinimumHeight = minimumHeight;
            MaximumHeight = maximumHeight;
            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        }

        /// <summary>取一个采样的高度。越界时钳到边界。</summary>
        public float GetHeight(int column, int row)
        {
            column = Mathf.Clamp(column, 0, SampleWidth - 1);
            row = Mathf.Clamp(row, 0, SampleHeight - 1);
            return Samples[row * SampleWidth + column];
        }

        /// <summary>
        /// 双线性采样任意世界坐标处的高度。
        ///
        /// 约定：worldX 沿南向（对应采样列），worldZ 沿东向（对应采样行），
        /// 与 Terrain314Source.Load 里对数据做的 X/Z 互换保持一致。
        ///
        /// 注意：314 的格心双线性会按采样低字节选择对角（见 314 UnitWorldGroundHeight），
        /// 这里先用标准双线性，后续与 314 对拍时再切到对角规则。
        /// </summary>
        public float SampleWorld(float worldX, float worldZ)
        {
            float fx = worldX / SampleSpacing;
            float fz = worldZ / SampleSpacing;
            int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0, tz = fz - z0;
            float h00 = GetHeight(x0, z0), h10 = GetHeight(x0 + 1, z0);
            float h01 = GetHeight(x0, z0 + 1), h11 = GetHeight(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }

        /// <summary>
        /// 从 314 导出的 height.r32（小端 float32，行优先）加载。
        ///
        /// transposeXZ = true 时在读取阶段就把数据行列互换：
        /// 导出文件的"列 = 东、行 = 南"会变成"列 = 世界 X（南）、行 = 世界 Z（东）"，
        /// 与项目的 +Z 东 / +X 南 约定对齐。这是唯一的分量交换点，
        /// 下游（patch 原点、网格、着色器）不再做第二次交换。
        /// </summary>
        /// <param name="path">height.r32 路径。</param>
        /// <param name="cellWidth">导出文件的格宽（列数 - 1）。</param>
        /// <param name="cellHeight">导出文件的格高（行数 - 1）。</param>
        /// <param name="sampleSpacing">采样间距（世界单位）。</param>
        /// <param name="minimumHeight">高度最小值。</param>
        /// <param name="maximumHeight">高度最大值。</param>
        /// <param name="transposeXZ">是否在加载时互换 X/Z 分量（行列转置）。</param>
        public static Terrain314HeightField LoadR32(string path, int cellWidth, int cellHeight,
            float sampleSpacing, float minimumHeight, float maximumHeight, bool transposeXZ = false)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int expected = (cellWidth + 1) * (cellHeight + 1) * sizeof(float);
            if (bytes.Length != expected)
            {
                throw new InvalidDataException(
                    $"Terrain314 高度场长度不符: 期望 {expected} 字节, 实际 {bytes.Length} 字节 ({path})");
            }

            int sampleWidth = cellWidth + 1;
            int sampleHeight = cellHeight + 1;
            var samples = new float[expected / sizeof(float)];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = BitConverter.Int32BitsToSingle(
                    BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i * 4, 4)));
            }

            if (!transposeXZ)
            {
                return new Terrain314HeightField(cellWidth, cellHeight, sampleSpacing,
                    minimumHeight, maximumHeight, samples);
            }

            // 转置后行列数互换；方形地图（2048x2048）两者相同，这里仍按通用写法处理
            var transposed = new float[samples.Length];
            TransposeBlocked(samples, transposed, sampleWidth, sampleHeight);
            return new Terrain314HeightField(cellHeight, cellWidth, sampleSpacing,
                minimumHeight, maximumHeight, transposed);
        }

        /// <summary>
        /// 分块转置一个行优先的二维浮点数组。
        ///
        /// 朴素转置逐点跨行写入，2049x2049 时几乎每次写都 cache miss（实测可达数百毫秒）；
        /// 按 32x32 分块后读写都落在同一批 cache line 内，加载耗时可压到十几毫秒。
        /// </summary>
        /// <param name="source">源数组，长度为 width * height。</param>
        /// <param name="destination">目标数组，长度为 width * height。</param>
        /// <param name="width">源数组列数（= 目标数组行数）。</param>
        /// <param name="height">源数组行数（= 目标数组列数）。</param>
        private static void TransposeBlocked(float[] source, float[] destination, int width, int height)
        {
            const int Block = 32;
            for (int rowBase = 0; rowBase < height; rowBase += Block)
            {
                int rowEnd = System.Math.Min(rowBase + Block, height);
                for (int columnBase = 0; columnBase < width; columnBase += Block)
                {
                    int columnEnd = System.Math.Min(columnBase + Block, width);
                    for (int row = rowBase; row < rowEnd; row++)
                    {
                        int sourceRow = row * width;
                        for (int column = columnBase; column < columnEnd; column++)
                        {
                            destination[column * height + row] = source[sourceRow + column];
                        }
                    }
                }
            }
        }
    }
}
