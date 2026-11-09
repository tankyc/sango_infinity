using System;
using System.IO;
using UnityEngine;

namespace Sango.Render.TerrainSango
{
    /// <summary>
    /// 地表图层集合：把旧管线"每层一个网格 + 每层一张贴图"的数据，装成一张 Texture2DArray + 一张参数纹理。
    ///
    /// 【为什么要换成数组】
    /// 旧管线按顶点的 textureIndex 把三角形分桶到 36~49 个图层，每层一个网格、一次 draw call。
    /// 新管线所有 patch 共用一张网格、一次 DrawMeshInstanced 画完，
    /// 因此"层"只能靠着色器自己选：顶点图层索引 → 索引纹理 → 数组切片下标。
    /// 结果仍然是 1 次 draw call，但外观口径（每层独立贴图 + 每层独立 tiling）与旧管线一致。
    ///
    /// 【为什么统一重采样到 256】
    /// 原贴图尺寸是 64/128/256/512 混排，Texture2DArray 要求所有切片同尺寸。
    /// UV 缩放（tiling）来自层元数据 textureScale，与实际分辨率无关，
    /// 因此重采样只影响清晰度、不改变贴图在世界里的平铺大小，外观口径不漂移。
    /// </summary>
    public sealed class TerrainSangoLayerSet : IDisposable
    {
        /// <summary>数组切片统一边长（像素）。</summary>
        public const int TextureSize = 256;

        /// <summary>地表贴图数组（切片下标 = 顶点 textureIndex）。</summary>
        public Texture2DArray Albedo { get; private set; }

        /// <summary>
        /// 每层参数纹理（宽 = 层数，高 = 1，RGBAFloat）：
        /// R = textureScale.x，G = textureScale.y，B = isLit，A = isWater。
        /// 用纹理而不是 uniform 数组：URP 的 UnityPerMaterial 常量缓冲不方便放变长数组，
        /// 而一张 49×1 的浮点纹理采样几乎零成本，也不破坏 SRP 合批之外的任何东西。
        /// </summary>
        public Texture2D Params { get; private set; }

        /// <summary>
        /// 底图（旧管线 _BaseTex = BaseTex/BaseMap{季节}.png）。
        /// 与地表贴图相乘叠加，给整张地图压上美术手绘的大色块（森林/沙漠/雪线）。
        /// 与地表贴图不同，它按整幅地图 0~1 采样，不参与平铺。
        /// </summary>
        public Texture2D BaseMap { get; private set; }

        /// <summary>地格贴图（旧管线 _GridTex = Grid/grid.png，上下两行交错构成六边形格）。</summary>
        public Texture2D GridTexture { get; private set; }

        /// <summary>
        /// 地格遮罩。旧管线由 MapGrid 逐格写入（领土/可达范围/迷雾），
        /// 不加载 GameData 时没有这些数据，用 1×1 常量替代：
        /// (0,0,0,1) = alpha 有值但 RGB 为 0 → 网格线能画出来，且不会染上绿/蓝/红势力色。
        /// </summary>
        public Texture2D GridMask { get; private set; }

        /// <summary>范围遮罩。旧管线里悬停兵力可动范围用，未加载时为 (0,0,0,0) 不做变暗。</summary>
        public Texture2D RangeMask { get; private set; }

        /// <summary>迷雾遮罩。(0,0,0,1) 表示"未探索"权重为 0，即整图可见。</summary>
        public Texture2D DarkMask { get; private set; }

        /// <summary>层数（= 数组切片数）。</summary>
        public int Count { get; private set; }

        /// <summary>缺失贴图数量（用于日志告警，缺的层会用白色兜底）。</summary>
        public int MissingCount { get; private set; }

        /// <summary>
        /// 从数据源构建图层集合。
        /// </summary>
        /// <param name="source">已解析出图层元数据的数据源</param>
        /// <returns>图层集合；无图层信息或贴图目录不存在时返回 null（地表退化为单色）</returns>
        public static TerrainSangoLayerSet Build(TerrainSangoSource source)
        {
            if (source == null || source.Layers == null || source.Layers.Length == 0)
                return null;

            // 【设备能力】部分低端移动 GPU（GLES3.0 / 老驱动）没有 2D 纹理数组：
            // new Texture2DArray 会失败，着色器里的 TEXTURE2D_ARRAY 也编译不出来。
            // 此时提前退回"单色地表"（TerrainSangoRenderer 会置 _UseLayers = 0），
            // 至少保住地形几何，而不是让整块地形消失。
            if (!SystemInfo.supports2DArrayTextures)
            {
                Log.Warning("当前设备不支持 2D 纹理数组（supports2DArrayTextures = false），地表图层退化为单色");
                return null;
            }

            string directory = source.ResolveTerrainTextureDirectory();
            if (string.IsNullOrEmpty(directory))
            {
                Log.Warning($"TerrainSango 找不到地表贴图目录（{source.WorkContent}/{source.SeasonName}），地表使用单色");
                return null;
            }

            int count = source.Layers.Length;
            byte[] indexes = source.RawLayerIndexes;

            // 只解码真正被用到的层：一张大图通常只用到二三十层，其余是空壳（如 layer_47 没有贴图）
            bool[] used = new bool[count];
            if (indexes != null)
            {
                for (int i = 0; i < indexes.Length; i++)
                {
                    int layer = indexes[i];
                    if (layer < count) used[layer] = true;
                }
            }

            // mipChain = true：远景时贴图会被压缩到几个像素，没有 mip 会闪成一片噪点
            var array = new Texture2DArray(TextureSize, TextureSize, count, TextureFormat.RGBA32, true, false)
            {
                name = "TerrainSangoLayers",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var set = new TerrainSangoLayerSet { Count = count };
            Color32[] white = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);

            for (int i = 0; i < count; i++)
            {
                string name = source.Layers[i].DiffuseName;
                Color32[] pixels = null;
                if (used[i] && !string.IsNullOrEmpty(name))
                    pixels = LoadResized(directory, name);

                if (pixels == null)
                {
                    if (used[i]) set.MissingCount++;
                    pixels = white;
                }
                array.SetPixels32(pixels, i);
            }
            array.Apply(true, false);      // 生成 mip
            set.Albedo = array;

            var parameters = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                TerrainSangoLayerInfo info = source.Layers[i];
                parameters[i] = new Vector4(info.ScaleX, info.ScaleY, info.IsLit ? 1f : 0f, info.IsWater ? 1f : 0f);
            }
            var paramTexture = new Texture2D(count, 1, TextureFormat.RGBAFloat, false, true)
            {
                name = "TerrainSangoLayerParams",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            paramTexture.SetPixelData(parameters, 0);
            paramTexture.Apply(false, false);
            set.Params = paramTexture;

            // ---- 底图 / 地格：旧管线的 MapBaseColor 与 MapGrid 视觉部分 ----
            string assetRoot = source.ResolveMapAssetDirectory();
            if (!string.IsNullOrEmpty(assetRoot))
            {
                // 底图按整幅地图 0~1 采样 → Clamp；地格贴图按 gridSize 平铺（UV 跑到几十上百）→ Repeat
                // 底图按季节换：文件名是 BaseMap{季节索引}.png（旧管线 MapBaseColor 同一套资源），
                // 用 source.Season 而不是常量 CurrentSeason，否则换季后底图永远停在 Autumn
                set.BaseMap = LoadFull(assetRoot, "BaseTex", "BaseMap" + source.Season,
                    TextureWrapMode.Clamp);
                set.GridTexture = LoadFull(assetRoot, "Grid", "grid", TextureWrapMode.Repeat);
                if (set.BaseMap == null)
                    Log.Warning($"TerrainSango 找不到底图 BaseTex/BaseMap{TerrainSangoSource.CurrentSeason}.png，底图叠加已跳过");
                if (set.GridTexture == null)
                    Log.Warning("TerrainSango 找不到地格贴图 Grid/grid.png，地格显示已跳过");
            }

            // 地格遮罩按"每格可通行"逐格生成（对齐 MapGrid.cs:444-450 的 TerrainType.moveable）：
            // 河/海这类可通行水面照样画格线，川/远海/深海与山地等不可通行格不画；
            // 拿不到逐格数据时回退到顶点 water 代理；再生成不出来才退回全可见常量。
            set.GridMask = source.CreateGridMaskTexture();
            if (set.GridMask == null)
                set.GridMask = CreateConstant("TerrainSangoGridMask", new Color32(0, 0, 0, 255));
            set.RangeMask = CreateConstant("TerrainSangoRangeMask", new Color32(0, 0, 0, 0));
            set.DarkMask = CreateConstant("TerrainSangoDarkMask", new Color32(0, 0, 0, 255));

            if (set.MissingCount > 0)
                Log.Warning($"TerrainSango 有 {set.MissingCount} 层的贴图缺失，已用白色兜底（目录 {directory}）");

            return set;
        }

        /// <summary>
        /// 读取一张贴图并重采样到 TextureSize²，返回像素数组；失败返回 null。
        ///
        /// 用 RenderTexture + ReadPixels 而不是 Graphics.CopyTexture：
        /// CopyTexture 要求源与目标尺寸格式完全一致，且失败时是静默的（表现为整层黑掉）；
        /// ReadPixels 路径是纯 CPU 可校验的，出错立刻能在编辑器里读回来对账。
        /// </summary>
        private static Color32[] LoadResized(string directory, string textureName)
        {
            string path = System.IO.Path.Combine(directory, textureName + ".png");
            if (!File.Exists(path))
                return null;

            Texture2D loaded = null;
            RenderTexture target = null;
            Texture2D resized = null;
            try
            {
                loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!loaded.LoadImage(File.ReadAllBytes(path)))
                    return null;
                loaded.wrapMode = TextureWrapMode.Repeat;

                target = RenderTexture.GetTemporary(TextureSize, TextureSize, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(loaded, target);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                resized = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
                resized.ReadPixels(new Rect(0, 0, TextureSize, TextureSize), 0, 0);
                resized.Apply(false, false);
                RenderTexture.active = previous;

                return resized.GetPixels32();
            }
            catch (Exception exception)
            {
                Log.Warning($"TerrainSango 读取地表贴图失败: {textureName} ({exception.Message})");
                return null;
            }
            finally
            {
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (resized != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(resized);
                    else UnityEngine.Object.DestroyImmediate(resized);
                }
                if (loaded != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(loaded);
                    else UnityEngine.Object.DestroyImmediate(loaded);
                }
            }
        }

        /// <summary>
        /// 按原始分辨率读取一张贴图（底图、地格图用），失败返回 null。
        /// 这两张图不参与平铺，不需要重采样，也不能缩——底图缩到 256 会糊成一片色块。
        /// </summary>
        private static Texture2D LoadFull(string assetRoot, string folder, string textureName, TextureWrapMode wrapMode)
        {
            string path = System.IO.Path.Combine(assetRoot, folder, textureName + ".png");
            if (!File.Exists(path)) return null;

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "TerrainSango_" + textureName,
                    wrapMode = wrapMode,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }
                return texture;
            }
            catch (Exception exception)
            {
                Log.Warning($"TerrainSango 读取贴图失败: {folder}/{textureName} ({exception.Message})");
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }
        }

        /// <summary>创建 1×1 常量纹理（替代 MapGrid 的逐格遮罩）。</summary>
        private static Texture2D CreateConstant(string name, Color32 color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(new[] { color });
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>释放 GPU 资源。</summary>
        public void Dispose()
        {
            // 属性不能按 ref 传递（CS0206），这里逐个释放
            if (Albedo != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Albedo);
                else UnityEngine.Object.DestroyImmediate(Albedo);
                Albedo = null;
            }
            if (Params != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Params);
                else UnityEngine.Object.DestroyImmediate(Params);
                Params = null;
            }
            // 属性不能按 ref 传递（CS0206），用一个"销毁并返回 null"的辅助方法收回
            BaseMap = DestroyTexture(BaseMap);
            GridTexture = DestroyTexture(GridTexture);
            GridMask = DestroyTexture(GridMask);
            RangeMask = DestroyTexture(RangeMask);
            DarkMask = DestroyTexture(DarkMask);
        }

        /// <summary>销毁一张贴图并返回 null，便于 `字段 = DestroyTexture(字段);` 写法。</summary>
        private static Texture2D DestroyTexture(Texture2D texture)
        {
            if (texture == null) return null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }
    }
}
