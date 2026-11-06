using System;
using System.IO;
using UnityEngine;

namespace Sango.Render.Terrain314
{
    /// <summary>
    /// 单个地格的合并视图（只读快照）。
    ///
    /// 【为什么单独做一层】
    ///   地形、植被、水面原先各算各的：地形只认高度场采样，植被只认 density.bin / biome.bin，
    ///   没有任何地方能回答"这一格是什么地表、多高、有没有植被"。
    ///   314 的这三类数据本来就落在同一套格网上，这里合并成一层，供渲染侧按格查询。
    ///
    /// 【坐标系】
    ///   世界 +X 为南、+Z 为东（与 Terrain314Source 加载时做的 X/Z 互换一致），
    ///   格 (x,z) 覆盖世界范围 [x*CellSize, (x+1)*CellSize) × [z*CellSize, (z+1)*CellSize)。
    /// </summary>
    public readonly struct Terrain314Cell
    {
        /// <summary>格列号（南向）。</summary>
        public readonly int X;
        /// <summary>格行号（东向）。</summary>
        public readonly int Z;
        /// <summary>格中心世界 X（南向）。</summary>
        public readonly float WorldX;
        /// <summary>格中心世界 Z（东向）。</summary>
        public readonly float WorldZ;
        /// <summary>格中心地形高度。</summary>
        public readonly float Height;
        /// <summary>地表类型：314 调色板参数索引（对应 Terrain314MaterialSet.PaletteTexIndex 的下标）。</summary>
        public readonly byte PaletteIndex;
        /// <summary>植被 biome 索引；<see cref="Terrain314CellData.NoBiome"/> 表示该格没有植被。</summary>
        public readonly byte BiomeIndex;
        /// <summary>植被密度（0~1 量纲，来自 314 的 DensityMap）。</summary>
        public readonly float Density;

        /// <summary>构造一个地格快照。</summary>
        public Terrain314Cell(int x, int z, float worldX, float worldZ, float height,
            byte paletteIndex, byte biomeIndex, float density)
        {
            X = x;
            Z = z;
            WorldX = worldX;
            WorldZ = worldZ;
            Height = height;
            PaletteIndex = paletteIndex;
            BiomeIndex = biomeIndex;
            Density = density;
        }

        /// <summary>该格是否有植被（biome 不是"无"哨兵值）。</summary>
        public bool HasVegetation => BiomeIndex != Terrain314CellData.NoBiome;

        /// <summary>调试输出用的简短描述。</summary>
        public override string ToString() =>
            $"格({X},{Z}) 世界({WorldX:F0},{WorldZ:F0}) 高度{Height:F1} 地表{PaletteIndex} " +
            $"biome{BiomeIndex} 密度{Density:F3}";
    }

    /// <summary>
    /// 314 地格级数据：地表类型、高度、植被密度 / biome 的统一读取层。
    ///
    /// 【数据来源与出处】
    ///   1) 地表类型：Control 组的 initial control（2049² RGBA8，见 Terrain314MaterialSet.ControlInitial）。
    ///      314 的 PS13 用 control 的 R 通道索引 vDtlTexIdx（San14 TerrainRendererPalette.cs:36-44），
    ///      所以 R 值就是"调色板参数索引"，不是纹理层号。
    ///      2049 = 1024*2+1，与高度场采样同粒度（间距 100），每格（200 单位）覆盖 2×2 个采样。
    ///      运行时纹理被标成不可读（Apply(false,true)），因此在编辑器里把每格中心采样
    ///      （2x+1, 2z+1）烘焙成 cells/palette.bin，运行时直接读 bin。
    ///   2) 植被密度 / biome：vegetation/density.bin（1024² float32）、vegetation/biome.bin（1024² uint8），
    ///      与地格一一对应，无需重采样。
    ///   3) 高度：不单独导出，直接由 Terrain314HeightField.SampleWorld 在格中心取值，
    ///      与地形 mesh 顶点使用同一入口，保证贴地一致。
    ///
    /// 【缺数据时的行为】
    ///   palette.bin 缺失只让"地表类型"退化（PaletteIndex = 0），不影响高度与植被字段；
    ///   density/biome 缺失时对应数组为 null，查询返回 0 与 NoBiome。不会抛异常打断渲染。
    /// </summary>
    public sealed class Terrain314CellData
    {
        /// <summary>没有植被的 biome 哨兵值（与 314 的 ResourceMap 一致）。</summary>
        public const byte NoBiome = 255;

        /// <summary>默认格列数（对应 RootSize 204800 / CellSize 200）。</summary>
        public const int DefaultCellsX = 1024;
        /// <summary>默认格行数。</summary>
        public const int DefaultCellsZ = 1024;
        /// <summary>默认格宽（世界单位），与 vegetation.json 的 cellSize 一致。</summary>
        public const float DefaultCellSize = 200f;

        /// <summary>格列数。</summary>
        public int CellsX { get; private set; }
        /// <summary>格行数。</summary>
        public int CellsZ { get; private set; }
        /// <summary>格宽（世界单位）。</summary>
        public float CellSize { get; private set; }
        /// <summary>每格地表类型（调色板参数索引）；缺失时为 null。</summary>
        public byte[] PaletteIndex { get; private set; }
        /// <summary>每格 biome；缺失时为 null。</summary>
        public byte[] BiomeIndex { get; private set; }
        /// <summary>每格植被密度；缺失时为 null。</summary>
        public float[] Density { get; private set; }
        /// <summary>每格中心高度缓存；缺失时为 null。</summary>
        public float[] Height { get; private set; }

        /// <summary>是否具备地表类型数据。</summary>
        public bool HasTerrainType => PaletteIndex != null;
        /// <summary>是否具备植被域数据。</summary>
        public bool HasVegetation => BiomeIndex != null && Density != null;

        private Terrain314CellData() { }

        /// <summary>
        /// 从 314 导出目录加载地格数据。
        /// </summary>
        /// <param name="terrainDirectory">Terrain314 根目录（含 cells/ 与 vegetation/ 子目录）。</param>
        /// <param name="height">高度场，用于填每格中心高度；可为 null。</param>
        /// <param name="cellsX">格列数，默认 1024。</param>
        /// <param name="cellsZ">格行数，默认 1024。</param>
        /// <param name="cellSize">格宽，默认 200。</param>
        /// <returns>加载结果；即使部分文件缺失也会返回可用对象，不会返回 null。</returns>
        public static Terrain314CellData Load(string terrainDirectory,
            Terrain314HeightField height,
            int cellsX = DefaultCellsX, int cellsZ = DefaultCellsZ, float cellSize = DefaultCellSize)
        {
            var data = new Terrain314CellData
            {
                CellsX = Mathf.Max(1, cellsX),
                CellsZ = Mathf.Max(1, cellsZ),
                CellSize = Mathf.Max(1f, cellSize)
            };
            if (string.IsNullOrEmpty(terrainDirectory)) return data;

            int count = data.CellsX * data.CellsZ;

            // 1) 地表类型（编辑器期烘焙产物）
            string palettePath = System.IO.Path.Combine(terrainDirectory, "cells", "palette.bin");
            if (System.IO.File.Exists(palettePath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(palettePath);
                if (bytes.Length == count)
                {
                    data.PaletteIndex = bytes;
                }
                else
                {
                    Log.Warning($"Terrain314 地格地表类型尺寸不符: {palettePath} 期望 {count} 字节，实际 {bytes.Length} 字节");
                }
            }

            // 2) 植被密度与 biome（与地格同网格，直接按索引对齐）
            string vegetationDirectory = System.IO.Path.Combine(terrainDirectory, "vegetation");
            string densityPath = System.IO.Path.Combine(vegetationDirectory, "density.bin");
            if (System.IO.File.Exists(densityPath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(densityPath);
                if (bytes.Length == count * sizeof(float))
                {
                    var values = new float[count];
                    Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                    data.Density = values;
                }
            }

            string biomePath = System.IO.Path.Combine(vegetationDirectory, "biome.bin");
            if (System.IO.File.Exists(biomePath))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(biomePath);
                if (bytes.Length == count) data.BiomeIndex = bytes;
            }

            // 3) 每格中心高度：与地形 mesh 共用 SampleWorld，避免两套插值口径
            if (height != null)
            {
                var heights = new float[count];
                float half = data.CellSize * 0.5f;
                for (int z = 0; z < data.CellsZ; z++)
                {
                    float worldZ = z * data.CellSize + half;
                    int row = z * data.CellsX;
                    for (int x = 0; x < data.CellsX; x++)
                    {
                        heights[row + x] = height.SampleWorld(x * data.CellSize + half, worldZ);
                    }
                }
                data.Height = heights;
            }

            return data;
        }

        /// <summary>
        /// 按格号取地格快照；坐标越界时夹到边缘，避免调用方到处写边界判断。
        /// </summary>
        public Terrain314Cell GetCell(int x, int z)
        {
            x = Mathf.Clamp(x, 0, CellsX - 1);
            z = Mathf.Clamp(z, 0, CellsZ - 1);
            int index = z * CellsX + x;
            float half = CellSize * 0.5f;
            byte palette = PaletteIndex != null ? PaletteIndex[index] : (byte)0;
            byte biome = BiomeIndex != null ? BiomeIndex[index] : NoBiome;
            float density = Density != null ? Density[index] : 0f;
            float heightValue = Height != null ? Height[index] : 0f;
            return new Terrain314Cell(x, z,
                x * CellSize + half, z * CellSize + half, heightValue, palette, biome, density);
        }

        /// <summary>按世界坐标取地格快照。</summary>
        public Terrain314Cell GetCellAtWorld(float worldX, float worldZ)
        {
            int x = Mathf.FloorToInt(worldX / CellSize);
            int z = Mathf.FloorToInt(worldZ / CellSize);
            return GetCell(x, z);
        }

        /// <summary>该格地表类型的调色板参数索引（越界夹边）。</summary>
        public int GetPaletteIndex(int x, int z)
        {
            if (PaletteIndex == null) return 0;
            x = Mathf.Clamp(x, 0, CellsX - 1);
            z = Mathf.Clamp(z, 0, CellsZ - 1);
            return PaletteIndex[z * CellsX + x];
        }

#if UNITY_EDITOR
        /// <summary>
        /// 把 Control 组的 initial control 烘焙成每格一份的地表类型表（cells/palette.bin）。
        ///
        /// 运行时纹理是 Apply(false, true) 之后不可读的，只能在编辑器里做：
        /// 从源 PNG 重新解码出可读纹理，再按"每格覆盖 2×2 采样"取格中心那个采样点
        /// （ControlWidth = CellsX * 2 + 1，中心恰为 2x+1 / 2z+1）。
        ///
        /// 314 的 control R 通道是"调色板参数索引"而不是纹理层号，
        /// 所以这里存原值，映射留给 Terrain314MaterialSet.PaletteTexIndex。
        /// </summary>
        /// <param name="controlPngPath">control_initial 的 PNG 绝对/工程相对路径。</param>
        /// <param name="outputPath">输出 bin 路径。</param>
        /// <param name="cellsX">格列数。</param>
        /// <param name="cellsZ">格行数。</param>
        /// <param name="message">结果说明（中文，含失败原因）。</param>
        public static bool BakePaletteIndex(string controlPngPath, string outputPath,
            int cellsX, int cellsZ, out string message)
        {
            message = string.Empty;
            if (!System.IO.File.Exists(controlPngPath))
            {
                message = $"找不到 control 贴图: {controlPngPath}";
                return false;
            }

            Texture2D source = new(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!source.LoadImage(System.IO.File.ReadAllBytes(controlPngPath)))
                {
                    message = $"control 贴图解码失败: {controlPngPath}";
                    return false;
                }

                int width = source.width, height = source.height;
                Color32[] pixels = source.GetPixels32();
                var output = new byte[cellsX * cellsZ];
                for (int z = 0; z < cellsZ; z++)
                {
                    int sampleZ = Mathf.Min(z * 2 + 1, height - 1);
                    int row = z * cellsX;
                    for (int x = 0; x < cellsX; x++)
                    {
                        int sampleX = Mathf.Min(x * 2 + 1, width - 1);
                        output[row + x] = pixels[sampleZ * width + sampleX].r;
                    }
                }

                string directory = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(outputPath, output);
                message = $"地格地表类型已烘焙: {cellsX}x{cellsZ} → {outputPath}（control {width}x{height}）";
                return true;
            }
            catch (Exception exception)
            {
                message = $"地格地表类型烘焙异常: {exception.Message}";
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
#endif
    }
}
