using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango.Render
{
    /// <summary>
    /// MapCell 地形共享烘焙资产（全图唯一一份，所有 <see cref="MapCell"/> 共用）。
    ///
    /// 【改造目标】旧 MapCell 管线"每层一个网格 + 每层一张全图 diffuse + 每层一个材质实例"，
    /// 36~49 个图层就是 36~49 次 draw call × 数千块，且材质互不相同、SRP Batcher 完全无法合批。
    /// 改造后：
    ///   · 图层贴图打包进一张 <see cref="Texture2DArray"/>（切片下标 = 顶点 textureIndex）；
    ///   · 图层归属烘进一张 R8 控制贴图（1 texel = 1 顶点格），着色器按控制贴图选切片；
    ///   · 全图只有一个地形材质实例 + 一个水材质实例 → SRP Batcher 把全部块的渲染合进极少的批次；
    ///   · 高度 / 水面高度各烘一张 R8 纹理，供远景 Instanced 渲染（<see cref="TerrainCellBatchRenderer"/>）
    ///     在顶点着色器做位移——远景不依赖 CPU 网格。
    ///
    /// 【烘焙口径与旧管线逐项对齐】
    ///   · 高度：VertexPosition 的 height * 0.5f → R8 原始字节不变，着色器乘 _HeightRange(127.5)；
    ///   · 水面：VertexWaterPosition 的 water * 0.5f → 同上；
    ///   · 图层：textureIndex 原始字节 → R8，着色器 floor(raw*255+0.5) 反解（与 TerrainSango 同口径）；
    ///   · 每层 tiling：LayerData.textureScale 原样进 _LayerParams。
    ///
    /// 【原版无对应实现】Open311 无 Texture2DArray / 控制贴图烘焙，此为渲染层自主设计。
    /// </summary>
    public sealed class MapCellTerrainAsset : IDisposable
    {
        /// <summary>数组切片统一边长（像素）。原图 64~512 混排，数组要求同尺寸，统一重采样到 256。</summary>
        public const int TextureSize = 256;

        /// <summary>高度 / 水面高度的世界缩放（byte 0~255 → 0~127.5 世界单位，与 height*0.5f 一致）。</summary>
        public const float HeightRange = 127.5f;

        // ───────────── 烘焙产物 ─────────────

        /// <summary>地表贴图数组（切片下标 = 顶点 textureIndex）。</summary>
        public Texture2DArray LayerArray { get; private set; }

        /// <summary>
        /// 每层参数纹理（宽 = 层数，高 = 1，RGBAFloat）：
        /// R = textureScale.x，G = textureScale.y，B = isLit，A = 1。
        /// 用纹理而不是 uniform 数组：URP 的 UnityPerMaterial 缓冲放不下变长数组。
        /// </summary>
        public Texture2D LayerParams { get; private set; }

        /// <summary>
        /// 预烘焙控制贴图（R8，1 texel = 1 顶点格）：R = textureIndex。
        /// 着色器按该贴图做"4 角双线性图层混合"，取代旧管线"每层单独网格 + 顶点色 alpha 叠加"。
        /// </summary>
        public Texture2D ControlTex { get; private set; }

        /// <summary>预烘焙高度贴图（R8，1 texel = 1 顶点格）：远景 Instanced 渲染的位移源。</summary>
        public Texture2D HeightTex { get; private set; }

        /// <summary>预烘焙水面高度贴图（R8）：远景水面 Instanced 渲染的位移源，<b>0 = 无水</b>（着色器 clip）。</summary>
        public Texture2D WaterHeightTex { get; private set; }

        // ───────────── 共享材质 ─────────────

        /// <summary>
        /// 地形材质（近景，MeshRenderer 正常矩阵渲染，顶点自带高度）。
        /// enableInstancing 必须为 false：着色器用 UNITY_INSTANCING_ENABLED 区分
        /// "CPU 顶点直出"与"平面模板 + 高度纹理位移"两种顶点路径，
        /// 近景材质若开了 instancing 会误走位移分支。
        /// </summary>
        public Material TerrainMaterial { get; private set; }

        /// <summary>地形材质（远景 Instanced 版，同一着色器、enableInstancing = true）。</summary>
        public Material TerrainInstanceMaterial { get; private set; }

        /// <summary>远景水面材质（平面模板 + 水面高度位移 + 非水区域 clip）。</summary>
        public Material WaterInstanceMaterial { get; private set; }

        // ───────────── 元数据 ─────────────

        /// <summary>图层数（= 数组切片数）。</summary>
        public int LayerCount { get; private set; }

        /// <summary>顶点间距（世界单位）。</summary>
        public float QuadSize { get; private set; }

        /// <summary>顶点格数量（vertex_width + 1 / vertex_height + 1）。</summary>
        public int VertexWidth { get; private set; }
        public int VertexHeight { get; private set; }

        /// <summary>水面层下标（layerDatas 最后一层）；-1 表示无水面层。</summary>
        public int WaterLayerIndex { get; private set; }

        /// <summary>贴图缺失的层数（烘焙日志用）。</summary>
        public int MissingCount { get; private set; }

        /// <summary>加载的全局底图 / 地格贴图（Dispose 时释放）。</summary>
        Texture2D globalBaseTex;
        Texture2D globalGridTex;

        /// <summary>是否已经烘焙过（重复 Build 直接返回已有实例）。</summary>
        public static MapCellTerrainAsset Current { get; private set; }

        /// <summary>纹理数组里当前装的是哪个季节的贴图（-1 = 尚未烘焙）。</summary>
        public int BakedSeason { get; private set; } = -1;

        /// <summary>
        /// 图层元数据引用（<see cref="MapRender.mapLayer"/> 的同一份数组）。
        /// 季节切换时靠它按新季节重新取贴图，与 Build 走同一入口。
        /// </summary>
        MapLayer.LayerData[] layers;

        /// <summary>
        /// 当前设备 / 图形 API 是否支持 2D 纹理数组 —— 新块实现（<see cref="MapCellBatch"/>）的前提条件。
        ///
        /// <see cref="MapTerrain.CreateCells"/> 用它在建块时二选一：支持则整图用新块，
        /// 不支持则整图回退老 <see cref="MapCell"/>。判定只在这里出现一次，
        /// 避免"建块处判一次、烘焙处再判一次"两处口径不一致导致半新半旧。
        /// </summary>
        public static bool IsSupported()
        {
            return SystemInfo.supports2DArrayTextures;
        }

        /// <summary>
        /// 构建（或复用已构建的）全图地形资产。
        /// 失败（设备不支持纹理数组等）返回 null，调用方回退旧行为。
        /// </summary>
        public static MapCellTerrainAsset Build(MapRender map)
        {
            if (Current != null)
                return Current;

            if (map == null || map.mapData == null || map.mapLayer == null)
                return null;

            // 【设备能力】低端移动 GPU 可能没有 2D 纹理数组，提前返回让调用方回退
            if (!IsSupported())
            {
                Log.Warning("当前设备不支持 2D 纹理数组（supports2DArrayTextures = false），MapCell 管线保持旧分层渲染");
                return null;
            }

            MapData mapData = map.mapData;
            MapLayer.LayerData[] layers = map.mapLayer.layerDatas;
            if (layers == null || layers.Length == 0)
                return null;

            // 【时序守卫】MapTerrain.Init（建块）发生在 MapLayer.OnLoad（读图层贴图名）之前，
            // 此时 LayerData.diffuse 全是构造默认的 whiteTexture、diffuseTexName 全空。
            // 在这里先烘只会得到一张全白的贴图数组，等 MapRender.LoadMap 尾部
            // RebuildTerrainAsset 再烘才是真正有效的一次；本次直接返回 null，
            // MapCell 会降级用旧管线材质显示，不会出现"地表一片白"。
            int seasonIndex = Mathf.Clamp(map.curSeason, 0, 3);
            bool layerNamesReady = false;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null && !string.IsNullOrEmpty(layers[i].GetDiffuseName(seasonIndex)))
                {
                    layerNamesReady = true;
                    break;
                }
            }
            if (!layerNamesReady)
            {
                Log.Info("MapCellTerrainAsset 图层贴图名尚未加载，延后到地图数据加载完成后再烘焙", Log.LogType.World);
                return null;
            }

            MapCellTerrainAsset asset = new MapCellTerrainAsset();
            asset.layers = layers;
            asset.LayerCount = layers.Length;
            asset.QuadSize = mapData.quadSize;
            asset.VertexWidth = mapData.vertex_width + 1;
            asset.VertexHeight = mapData.vertex_height + 1;
            asset.WaterLayerIndex = layers.Length - 1;

            int season = Mathf.Clamp(map.curSeason, 0, 3);

            // ---- ① 控制贴图 / 高度 / 水面高度（一次遍历三张 R8）----
            int texW = asset.VertexWidth;
            int texH = asset.VertexHeight;
            byte[] control = new byte[texW * texH];
            byte[] height = new byte[texW * texH];
            byte[] water = new byte[texW * texH];

            MapData.VertexData[][] vm = mapData.vertexDatas;
            if (vm == null || vm.Length == 0)
                return null;

            // 轴序：texel.x = vx（世界 Z 方向）、texel.y = vy（世界 X 方向），与 vertexDatas[x][y] 一致。
            // 【内存布局】Unity 纹理是行主序：texel(vx, vy) 位于 index = vy * texW + vx（vy 是行）。
            for (int x = 0; x < texW && x < vm.Length; x++)
            {
                MapData.VertexData[] col = vm[x];
                int copy = Mathf.Min(texH, col.Length);
                for (int y = 0; y < copy; y++)
                {
                    MapData.VertexData v = col[y];
                    int index = y * texW + x;
                    control[index] = v.textureIndex;
                    height[index] = v.height;
                    // water 字段本身就是"有没有水"的唯一真值：0 = 无水，非 0 = 水面高度（×0.5 得世界高度）。
                    // 远景水面的 clip 与岸线 alpha 全部从这一张图推出来，不再额外烘掩码。
                    water[index] = v.water;
                }
            }

            asset.ControlTex = CreateR8("MapCellTerrain_Control", texW, texH, control);
            asset.HeightTex = CreateR8("MapCellTerrain_Height", texW, texH, height);
            asset.WaterHeightTex = CreateR8("MapCellTerrain_Water", texW, texH, water);

            // ---- ② 图层贴图数组 + 每层参数 ----
            asset.BuildLayerArray(season);
            asset.BakedSeason = season;

            // ---- ③ 共享材质 ----
            asset.BuildMaterials(season);

            // ---- ④ 全局叠加纹理（底图 / 地格贴图）----
            // 旧管线（terrain_urp）通过 Shader.SetGlobalTexture 消费 _BaseTex / _GridTex；
            // 运行时 MapBaseColor.UpdateRender 只会把 _BaseTex 设为构造时的白色兜底
            // （MapBaseColor.cs:25-28），美术底图从未上线 —— 而编辑器 / TerrainSango
            // 都会加载 BaseTex/BaseMap{季节}.png 叠加。这里对齐 TerrainSangoLayerSet:163-165
            // 的加载口径，否则新管线会比 TerrainSango"亮一截"且没有大色块。
            asset.LoadGlobalOverlays(map, season);

            Current = asset;
            if (asset.MissingCount > 0)
                Log.Warning($"MapCellTerrainAsset 有 {asset.MissingCount} 层贴图缺失，已用白色兜底");
            Log.Info($"MapCellTerrainAsset 烘焙完成：{texW}x{texH} 控制贴图，{asset.LayerCount} 层贴图数组", Log.LogType.World);
            return asset;
        }

        /// <summary>
        /// 烘焙图层贴图数组与参数纹理。
        /// 数组 / 参数纹理只创建一次，季节切换时由 <see cref="FillLayerArray"/> 原地重灌，
        /// 不重建 GPU 资源（材质上的引用因此始终有效）。
        /// </summary>
        private void BuildLayerArray(int season)
        {
            LayerArray = new Texture2DArray(TextureSize, TextureSize, layers.Length, TextureFormat.RGBA32, true, false)
            {
                name = "MapCellTerrain_Layers",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave,
            };

            LayerParams = new Texture2D(layers.Length, 1, TextureFormat.RGBAFloat, false, true)
            {
                name = "MapCellTerrain_LayerParams",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };

            FillLayerArray(season);
        }

        /// <summary>
        /// 把指定季节的全部图层贴图灌进纹理数组，并同步每层 tiling 参数。
        ///
        /// 【首次烘焙与季节切换共用本方法】四季贴图各一套，切换季节时只有贴图内容与
        /// textureScale 会变；走同一条路径才能保证"开局季节"与"切季后的季节"取图口径一致。
        /// </summary>
        /// <param name="season">季节索引 0~3（Autumn / Spring / Summer / Winter）</param>
        private void FillLayerArray(int season)
        {
            // 白色兜底（缺图的层）：一次分配，全部缺图层共用
            Color32[] white = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < white.Length; i++)
                white[i] = new Color32(255, 255, 255, 255);

            MissingCount = 0;
            Vector4[] parameters = new Vector4[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                MapLayer.LayerData layer = layers[i];
                Color32[] pixels = null;

                // 【必须走 GetDiffuse】不能直接读 layer.diffuse[season]：
                // 它的构造默认值是 whiteTexture，真贴图只在 MapLayer.OnLoad / 材质刷新时才就位；
                // GetDiffuse 内部会走 EnsureSeasonTexture 主动加载（并顺带算好
                // layer.textureScale），是唯一可靠的取值入口。
                Texture source = null;
                if (layer != null)
                {
                    Texture raw = layer.GetDiffuse(season);
                    if (raw != null && raw != Texture2D.whiteTexture)
                        source = raw;
                }

                if (source != null)
                    pixels = ResizeToTextureSize(source);
                if (pixels == null)
                {
                    if (i != WaterLayerIndex)
                        MissingCount++;
                    pixels = white;
                }
                LayerArray.SetPixels32(pixels, i);

                if (layer != null)
                    parameters[i] = new Vector4(layer.textureScale.x, layer.textureScale.y,
                        layer.isLit ? 1f : 0f, layer.isWaterLayer ? 1f : 0f);
                else
                    parameters[i] = Vector4.one;
            }
            LayerArray.Apply(true, false);      // 生成 mip：远景压缩到几个像素时避免闪噪点

            LayerParams.SetPixelData(parameters, 0);
            LayerParams.Apply(false, false);
        }

        /// <summary>
        /// 季节切换：按新季节重灌地表贴图数组、每层 tiling 与远景水面贴图。
        ///
        /// 【为什么需要】旧管线每层一个材质，MapLayer.UpdateRender（MapLayer.cs:542-550）在季节
        /// 切换时会逐层换 _BaseMap；新管线把全部图层烘进一张 Texture2DArray，
        /// 不重灌就会永远停在开局季节的地表。
        /// 控制贴图 / 高度 / 水面高度与季节无关，几何也无需重建，这里只动贴图侧。
        /// </summary>
        /// <param name="season">季节索引 0~3</param>
        public void RefreshSeason(int season)
        {
            if (layers == null || LayerArray == null || LayerParams == null)
                return;

            season = Mathf.Clamp(season, 0, 3);
            if (season == BakedSeason)
                return;

            FillLayerArray(season);
            BakedSeason = season;
            RefreshWaterTexture(season);

            if (MissingCount > 0)
                Log.Warning($"MapCellTerrainAsset 切换季节后有 {MissingCount} 层贴图缺失，已用白色兜底");
            Log.Info($"MapCellTerrainAsset 地表已切换季节：{MapRender.SeasonNames[season]}（{LayerCount} 层）",
                Log.LogType.World);
        }

        /// <summary>
        /// 远景水材质的序列帧贴图切到指定季节（与近景水材质共用同一份资源，
        /// 近景由 MapLayer.UpdateRender 自己换，这里只管 Instanced 的那一份）。
        /// </summary>
        private void RefreshWaterTexture(int season)
        {
            if (WaterInstanceMaterial == null || layers == null || layers.Length == 0)
                return;

            MapLayer.LayerData water = layers[layers.Length - 1];
            if (water == null)
                return;

            Texture tex = water.GetDiffuse(season);
            if (tex != null)
            {
                WaterInstanceMaterial.SetTexture("_BaseMap", tex);

                // 【tiling 必须与近景水材质一致】近景水面材质由 MapLayer 维护，
                // 每次都跟着 SetTextureScale("_BaseMap", layer.textureScale)（MapLayer.cs:76/154/181/325）；
                // 远景水材质是独立的一份，不设就会退回默认 (1,1) —— 水面纹理被放大几十倍。
                // textureScale 依赖贴图尺寸，GetDiffuse 之后才是最新值，所以放在这里同步。
                WaterInstanceMaterial.SetTextureScale("_BaseMap", water.textureScale);
            }
        }

        /// <summary>把任意尺寸贴图重采样到 TextureSize²（RenderTexture blit 路径，失败返回 null）。</summary>
        private static Color32[] ResizeToTextureSize(Texture source)
        {
            RenderTexture target = null;
            Texture2D resized = null;
            try
            {
                target = RenderTexture.GetTemporary(TextureSize, TextureSize, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(source, target);

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
                Log.Warning($"MapCellTerrainAsset 重采样图层贴图失败: {source.name} ({exception.Message})");
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
            }
        }

        /// <summary>构建三份共享材质。</summary>
        /// <param name="season">当前季节（取水层该季节的序列帧贴图给远景水材质）</param>
        private void BuildMaterials(int season)
        {
            Shader terrainShader = Shader.Find("Sango/terrain_cell");
            // passCount <= 0 表示 shader 存在但没有有效 pass（导入中 / 编译失败的空壳）——
            // 用它建出来的材质不会出任何像素，必须当作不可用处理，让 MapCell 走降级链
            if (terrainShader == null || terrainShader.passCount <= 0)
            {
                Log.Error("Sango/terrain_cell 不可用（未找到或编译失败/导入中，passCount="
                    + (terrainShader != null ? terrainShader.passCount : 0)
                    + "），MapCell 地形将降级为旧管线材质");
                return;
            }

            // 近景材质：顶点高度直出，必须关 instancing（见 TerrainMaterial 注释）
            TerrainMaterial = new Material(terrainShader)
            {
                name = "MapCellTerrain_Shared",
                hideFlags = HideFlags.HideAndDontSave,
            };
            TerrainMaterial.enableInstancing = false;

            // 远景材质：平面模板 + 高度纹理位移，需要 instancing
            TerrainInstanceMaterial = new Material(terrainShader)
            {
                name = "MapCellTerrain_Instanced",
                hideFlags = HideFlags.HideAndDontSave,
            };
            TerrainInstanceMaterial.enableInstancing = true;

            Shader waterShader = Shader.Find("Sango/terrain_cell_water");
            if (waterShader != null)
            {
                WaterInstanceMaterial = new Material(waterShader)
                {
                    name = "MapCellTerrain_Water",
                    hideFlags = HideFlags.HideAndDontSave,
                };

                // 远景水面与地形共用同一批实例矩阵（TerrainCellBatchRenderer.Flush），
                // 不开 instancing 的话 UNITY_SETUP_INSTANCE_ID 是空宏，所有实例会叠在原点。
                WaterInstanceMaterial.enableInstancing = true;

                // 远景水材质的序列帧贴图：复用水层（最后一层）当前季节的贴图，
                // 与近景水材质（layerDatas[last].material）同一资源，动画口径一致
                RefreshWaterTexture(season);
            }
            else
            {
                Log.Warning("找不到着色器 Sango/terrain_cell_water，远景水面将不渲染");
            }

            // 材质参数（三份材质共用的全局口径在这里一次设好；
            // _MapWidth/_MapHeight/_GridSize/_FactionMap 等仍走 Shader.SetGlobalXXX，与旧管线一致）
            ApplyMaterialParams(TerrainMaterial);
            ApplyMaterialParams(TerrainInstanceMaterial);
            ApplyMaterialParams(WaterInstanceMaterial);
        }

        /// <summary>把烘焙产物写入一份材质。</summary>
        private void ApplyMaterialParams(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty("_ControlTex"))
                material.SetTexture("_ControlTex", ControlTex);
            if (material.HasProperty("_LayerTexArray"))
                material.SetTexture("_LayerTexArray", LayerArray);
            if (material.HasProperty("_LayerParams"))
                material.SetTexture("_LayerParams", LayerParams);
            if (material.HasProperty("_HeightTex"))
                material.SetTexture("_HeightTex", HeightTex);
            if (material.HasProperty("_WaterHeightTex"))
                material.SetTexture("_WaterHeightTex", WaterHeightTex);
            if (material.HasProperty("_LayerCount"))
                material.SetFloat("_LayerCount", LayerCount);
            if (material.HasProperty("_QuadSize"))
                material.SetFloat("_QuadSize", QuadSize);
            if (material.HasProperty("_HeightRange"))
                material.SetFloat("_HeightRange", HeightRange);
            if (material.HasProperty("_TexSize"))
                material.SetVector("_TexSize", new Vector4(VertexWidth, VertexHeight, 0f, 0f));
            if (material.HasProperty("_WaterLayerIndex"))
                material.SetFloat("_WaterLayerIndex", WaterLayerIndex);
        }

        /// <summary>
        /// 刷新一段顶点格区域的控制贴图 texel（编辑器笔刷改 textureIndex 后调用）。
        /// 旧管线笔刷改完 textureIndex 只重建网格就能看到效果（层归属在网格里）；
        /// 改造后层归属在控制贴图里，mesh 重建不再携带它，必须在这里同步刷新。
        /// </summary>
        /// <param name="mapData">地图数据（取最新 textureIndex）</param>
        /// <param name="vx0">区域起点 vx（含）</param>
        /// <param name="vy0">区域起点 vy（含）</param>
        /// <param name="w">区域宽度（顶点格数，含边界）</param>
        /// <param name="h">区域高度（顶点格数，含边界）</param>
        public void RefreshControlRegion(MapData mapData, int vx0, int vy0, int w, int h)
        {
            if (ControlTex == null || mapData == null || mapData.vertexDatas == null)
                return;

            MapData.VertexData[][] vm = mapData.vertexDatas;
            int texW = VertexWidth;
            int texH = VertexHeight;
            byte[] raw = ControlTex.GetRawTextureData();

            int x1 = Math.Min(vx0 + w, Math.Min(texW, vm.Length));
            int y1 = Math.Min(vy0 + h, texH);
            for (int x = Math.Max(0, vx0); x < x1; x++)
            {
                MapData.VertexData[] col = vm[x];
                int copy = Math.Min(y1, col.Length);
                for (int y = Math.Max(0, vy0); y < copy; y++)
                    raw[y * texW + x] = col[y].textureIndex;
            }
            ControlTex.Apply(false, false);
        }

        /// <summary>
        /// 加载并设置全局叠加纹理：底图（_BaseTex）与地格贴图（_GridTex）。
        /// 加载口径复刻 TerrainSangoLayerSet.LoadFull（TerrainSangoLayerSet.cs:163-165, 248-275）：
        /// BaseTex/BaseMap{季节}.png（整幅采样，Clamp）与 Grid/grid.png（平铺，Repeat）。
        /// 文件缺失时跳过并保持旧全局值（MapBaseColor 的白色兜底），不影响运行。
        /// </summary>
        private void LoadGlobalOverlays(MapRender map, int season)
        {
            string assetRoot = ResolveAssetRoot(map);
            if (string.IsNullOrEmpty(assetRoot))
            {
                Log.Warning("MapCellTerrainAsset 定位不到地图资源目录（Assets/Map/{地图名|Default}），底图/地格叠加已跳过");
                return;
            }

            Texture2D baseMap = LoadFullFromDisk(assetRoot, "BaseTex", "BaseMap" + season, TextureWrapMode.Clamp);
            if (baseMap != null)
            {
                Shader.SetGlobalTexture("_BaseTex", baseMap);
                globalBaseTex = baseMap;
            }
            else
                Log.Warning($"MapCellTerrainAsset 找不到底图 BaseTex/BaseMap{season}.png，底图叠加已跳过");

            Texture2D gridTex = LoadFullFromDisk(assetRoot, "Grid", "grid", TextureWrapMode.Repeat);
            if (gridTex != null)
            {
                Shader.SetGlobalTexture("_GridTex", gridTex);
                globalGridTex = gridTex;
            }
        }

        /// <summary>
        /// 地图资源根目录：{Content}/Assets/Map/{地图名}，不存在回落 Default
        /// （口径与 TerrainSangoSource.ResolveMapAssetDirectory:945-951 一致）。
        /// </summary>
        private static string ResolveAssetRoot(MapRender map)
        {
            string contentRoot = Sango.Path.ContentRootPath;
            if (string.IsNullOrEmpty(contentRoot))
                return null;

            string workContent = !string.IsNullOrEmpty(map.WorkContent) ? map.WorkContent : "Default";
            string baseDir = System.IO.Path.Combine(contentRoot, "Assets", "Map");
            string mine = System.IO.Path.Combine(baseDir, workContent);
            if (System.IO.Directory.Exists(mine))
                return mine;

            string fallback = System.IO.Path.Combine(baseDir, "Default");
            return System.IO.Directory.Exists(fallback) ? fallback : null;
        }

        /// <summary>从磁盘整幅加载 png（口径复刻 TerrainSangoLayerSet.LoadFull:248-275）。</summary>
        private static Texture2D LoadFullFromDisk(string assetRoot, string folder, string textureName, TextureWrapMode wrapMode)
        {
            string path = System.IO.Path.Combine(assetRoot, folder, textureName + ".png");
            if (!System.IO.File.Exists(path))
                return null;

            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                if (!texture.LoadImage(bytes, false))
                {
                    Log.Warning($"MapCellTerrainAsset 底图/地格贴图解码失败: {path}");
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }
                texture.name = $"MapCellTerrain_{folder}_{textureName}";
                texture.wrapMode = wrapMode;
                texture.filterMode = FilterMode.Bilinear;
                texture.anisoLevel = 4;
                texture.hideFlags = HideFlags.HideAndDontSave;
                return texture;
            }
            catch (Exception exception)
            {
                Log.Warning($"MapCellTerrainAsset 读取贴图失败: {path} ({exception.Message})");
                return null;
            }
        }

        /// <summary>创建 R8 单通道贴图（Point + Clamp：texel 与顶点格 1:1 对齐，不允许插值越界）。</summary>
        private static Texture2D CreateR8(string name, int width, int height, byte[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.R8, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>释放全部 GPU 资源（地图卸载 / 重载时调用）。</summary>
        public void Dispose()
        {
            DestroyObject(LayerArray);
            DestroyObject(LayerParams);
            DestroyObject(ControlTex);
            DestroyObject(HeightTex);
            DestroyObject(WaterHeightTex);
            DestroyObject(TerrainMaterial);
            DestroyObject(TerrainInstanceMaterial);
            DestroyObject(WaterInstanceMaterial);
            DestroyObject(globalBaseTex);
            DestroyObject(globalGridTex);
            layers = null;
            BakedSeason = -1;
            if (Current == this)
                Current = null;
        }

        /// <summary>按运行态销毁一个引擎对象并返回 null。</summary>
        private static UnityEngine.Object DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
            return null;
        }
    }
}
