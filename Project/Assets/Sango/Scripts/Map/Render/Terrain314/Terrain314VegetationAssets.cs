using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Sango.Render.Terrain314
{
    /// <summary>
    /// 314 植被模板资源（贴图 + 真实包围盒）。
    ///
    /// 【数据来源】
    ///   由 San14 侧工具 `San14.TerrainExport` 从原版 CE 版导出，产出在
    ///   StreamingAssets/Terrain314/vegetation/gpu-templates/ 下：
    ///     templates-index.json                      index ↔ NameHash ↔ 模板真实包围盒
    ///     template-{index}-{hash}/draw-0/textures-native.txt   register 格式 相对 .tex 路径
    ///     textures/{资源哈希}-{层}-{格式}.tex        贴图编码块（原始 BC/未压缩 mip）
    ///
    /// 【为什么要这份数据】
    ///   1) 314 的 Parameters[1]/[2] 是"原版模板原始尺寸"上的倍率。模板真实高度从
    ///      63 到 2124 单位不等，用统一的 200 基准对不上，必须按 element 的模板各自换算。
    ///   2) 原版植被是有贴图的（Tree2 着色器采样 t0），换成纯色会丢掉全部形态细节。
    ///
    /// 【.tex 格式】
    ///   D3D11ModelResourceWriter.EncodeTexture 的原样输出：
    ///     uint width, height, layerCount, mipCount, dxgiFormat
    ///     每级 mip：uint pitch, uint size, uint size2, byte[size]
    ///   这里只取 mip0，按 DXGI 映射到 Unity 的 TextureFormat 后 LoadRawTextureData，
    ///   不做 CPU 解码，BC 压缩块直接上传。
    /// </summary>
    public sealed class Terrain314VegetationAssets
    {
        /// <summary>单个植被模板的索引信息。</summary>
        public sealed class Template
        {
            /// <summary>物理槽位序号。</summary>
            public int Index;
            /// <summary>314 的 DisplaySet NameHash（对应 vegetation.json 里 element.displaySetNameHash）。</summary>
            public uint NameHash;
            /// <summary>模板目录名。</summary>
            public string Directory;
            /// <summary>模板真实包围盒尺寸（x/y/z）。</summary>
            public Vector3 Size;
            /// <summary>模板真实包围盒高度，用于把 Parameters 倍率换算成世界尺寸。</summary>
            public float Height => Size.y;
        }

        [Serializable]
        private sealed class IndexDocument
        {
            public int templateCount;
            public IndexEntry[] templates;
        }

        [Serializable]
        private sealed class IndexEntry
        {
            public int index;
            public string nameHashHex;
            public string directory;
            public float[] size;
        }

        private readonly Dictionary<uint, Template> byHash = new();
        private readonly Dictionary<uint, Texture2D> baseMaps = new();
        private readonly string rootDirectory;

        /// <summary>是否成功加载了模板索引。</summary>
        public bool Loaded { get; private set; }

        /// <summary>模板总数。</summary>
        public int Count => byHash.Count;

        private Terrain314VegetationAssets(string directory)
        {
            rootDirectory = directory;
        }

        /// <summary>
        /// 从 vegetation 目录（含 gpu-templates 子目录）加载模板索引。目录不存在时返回空对象，不抛异常。
        /// </summary>
        public static Terrain314VegetationAssets Load(string vegetationDirectory)
        {
            var assets = new Terrain314VegetationAssets(vegetationDirectory);
            if (string.IsNullOrEmpty(vegetationDirectory)) return assets;

            string indexPath = System.IO.Path.Combine(vegetationDirectory, "gpu-templates", "templates-index.json");
            if (!System.IO.File.Exists(indexPath)) return assets;

            try
            {
                IndexDocument document = JsonUtility.FromJson<IndexDocument>(File.ReadAllText(indexPath));
                if (document?.templates == null) return assets;

                foreach (IndexEntry entry in document.templates)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.nameHashHex)) continue;
                    if (!uint.TryParse(entry.nameHashHex, NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out uint hash)) continue;

                    Vector3 size = entry.size != null && entry.size.Length >= 3
                        ? new Vector3(entry.size[0], entry.size[1], entry.size[2])
                        : Vector3.zero;

                    assets.byHash[hash] = new Template
                    {
                        Index = entry.index,
                        NameHash = hash,
                        Directory = entry.directory,
                        Size = size
                    };
                }
                assets.Loaded = assets.byHash.Count > 0;
            }
            catch (Exception exception)
            {
                Log.Warning($"Terrain314 植被模板索引解析失败: {exception.Message}");
            }
            return assets;
        }

        /// <summary>按 NameHash 取模板信息。</summary>
        public bool TryGetTemplate(uint nameHash, out Template template)
            => byHash.TryGetValue(nameHash, out template);

        /// <summary>
        /// 取模板的 albedo 贴图（draw-0 里 register 最小的那张）。
        ///
        /// 首次访问才读盘；失败时缓存 null 并只提示一次，避免每帧重复 IO。
        /// </summary>
        public Texture2D GetBaseMap(uint nameHash)
        {
            if (baseMaps.TryGetValue(nameHash, out Texture2D cached)) return cached;
            baseMaps[nameHash] = null;
            if (!byHash.TryGetValue(nameHash, out Template template)) return null;

            string drawDirectory = System.IO.Path.Combine(rootDirectory, "gpu-templates",
                template.Directory, "draw-0");
            string manifest = System.IO.Path.Combine(drawDirectory, "textures-native.txt");
            if (!System.IO.File.Exists(manifest)) return null;

            string bestFile = null;
            int bestRegister = int.MaxValue;
            foreach (string line in System.IO.File.ReadAllLines(manifest))
            {
                // 行格式: {register} {resourceFormat} {shaderViewFormat} {相对 .tex 路径}
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0], out int register)) continue;
                if (register >= bestRegister) continue;
                bestRegister = register;
                bestFile = parts[3];
            }
            if (bestFile == null) return null;

            string texturePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(drawDirectory, bestFile.Replace('/', System.IO.Path.DirectorySeparatorChar)));
            Texture2D texture = LoadTexture(texturePath);
            baseMaps[nameHash] = texture;
            return texture;
        }

        /// <summary>
        /// 解析 .tex（5 个 uint 头 + 逐 mip 的 pitch/size/size2/数据），只取 mip0 上传。
        /// </summary>
        private static Texture2D LoadTexture(string path)
        {
            if (!System.IO.File.Exists(path)) return null;
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                if (bytes.Length < 28) return null;

                int width = BitConverter.ToInt32(bytes, 0);
                int height = BitConverter.ToInt32(bytes, 4);
                int mipCount = BitConverter.ToInt32(bytes, 12);
                uint dxgi = BitConverter.ToUInt32(bytes, 16);
                if (width <= 0 || height <= 0 || mipCount <= 0) return null;

                int offset = 20;
                int mip0Size = 0;
                int mip0Offset = 0;
                for (int mip = 0; mip < mipCount; mip++)
                {
                    if (offset + 12 > bytes.Length) return null;
                    int size = BitConverter.ToInt32(bytes, offset + 4);
                    int dataOffset = offset + 12;
                    if (size < 0 || dataOffset + size > bytes.Length) return null;
                    if (mip == 0)
                    {
                        mip0Size = size;
                        mip0Offset = dataOffset;
                    }
                    offset = dataOffset + size;
                }
                if (mip0Size <= 0) return null;

                if (!TryMapFormat(dxgi, out TextureFormat format, out bool linear))
                {
                    Log.Warning($"Terrain314 植被贴图格式未支持: DXGI {dxgi}（{System.IO.Path.GetFileName(path)}）");
                    return null;
                }

                var texture = new Texture2D(width, height, format, false, linear)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 4
                };
                byte[] mip0 = new byte[mip0Size];
                Buffer.BlockCopy(bytes, mip0Offset, mip0, 0, mip0Size);
                texture.LoadRawTextureData(mip0);
                texture.Apply(false, true);
                return texture;
            }
            catch (Exception exception)
            {
                Log.Warning($"Terrain314 植被贴图加载失败: {path} — {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// DXGI 格式 → Unity TextureFormat。
        ///
        /// 只覆盖植被实际会遇到的：BC1/BC3/BC4/BC5/BC6H/BC7 与未压缩 RGBA/BGRA。
        /// linear 由 sRGB 变体（奇数结尾的 _SRGB）决定，不能一律按 gamma 处理，
        /// 否则 BC1_SRGB 会被当成线性数据、颜色偏暗。
        /// </summary>
        private static bool TryMapFormat(uint dxgi, out TextureFormat format, out bool linear)
        {
            linear = true;
            switch (dxgi)
            {
                case 71: format = TextureFormat.DXT1; return true;
                case 72: format = TextureFormat.DXT1; linear = false; return true;
                case 77: format = TextureFormat.DXT5; return true;
                case 78: format = TextureFormat.DXT5; linear = false; return true;
                case 80: format = TextureFormat.BC4; return true;
                case 83: case 84: format = TextureFormat.BC5; return true;
                case 95: case 96: format = TextureFormat.BC6H; return true;
                case 98: format = TextureFormat.BC7; return true;
                case 99: format = TextureFormat.BC7; linear = false; return true;
                case 28: format = TextureFormat.RGBA32; return true;
                case 29: format = TextureFormat.RGBA32; linear = false; return true;
                case 87: format = TextureFormat.BGRA32; return true;
                case 91: format = TextureFormat.BGRA32; linear = false; return true;
                case 10: format = TextureFormat.RGBAHalf; return true;
                case 2: format = TextureFormat.RGBAFloat; return true;
                case 16: format = TextureFormat.RFloat; return true;
                case 41: format = TextureFormat.RFloat; return true;
                case 34: format = TextureFormat.RGHalf; return true;
                case 14: format = TextureFormat.RHalf; return true;
                default: format = TextureFormat.RGBA32; return false;
            }
        }

        /// <summary>释放所有已加载贴图。</summary>
        public void Release()
        {
            foreach (Texture2D texture in baseMaps.Values)
            {
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
            baseMaps.Clear();
        }
    }
}
