using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango.Render.TerrainSango
{
    /// <summary>
    /// Sango 地形渲染器（阶段 1：几何与 LOD，独立于旧管线）。
    ///
    /// 【与旧 MapTerrain 的根本区别】
    ///   旧：地形切成固定 64×64 顶点的块 → 每块 CPU 建 mesh → 每层一个 MeshRenderer
    ///       → 拉远时 draw call 与块数×层数成正比，拉近时精度封顶。
    ///   新：全部 patch 共用一张 65×65 的"平面网格"，高度在顶点着色器里从 R8 高度纹理采样位移，
    ///       一个 patch 只提交一个实例矩阵 → draw call 恒定（≤2），且缩放时不建任何 mesh。
    ///
    /// 【无缝（无极）缩放怎么做到的】
    ///   CDLOD：每个 patch 都在"距离逼近自己的分裂阈值"时，把顶点位置插值到父层的 32 段网格上
    ///   （见 TerrainSango.shader 的 morph 段）。当它真的被子节点替换时，子节点此刻正好也
    ///   收敛到它的网格 → 几何连续，没有跳变，也没有 LOD 接缝。
    ///
    /// 【不修改任何旧管线】
    ///   本组件不注册进 MapRender、不碰 MapData/MapTerrain/MapCell，只读 MapData 生成高度纹理，
    ///   因此旧渲染可随时回退（阶段 3 才做后端开关）。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class TerrainSangoRenderer : MonoBehaviour
    {
        /// <summary>数据源模式。</summary>
        public enum SourceMode
        {
            /// <summary>程序化演示地形（不依赖任何资源，用于阶段 1 验证）。</summary>
            Procedural = 0,

            /// <summary>从已加载的游戏地图（MapRender.mapData）构建。</summary>
            GameMap = 1,

            /// <summary>直接从地图 .bin 文件构建（不需要进游戏、不需要 MapRender）。</summary>
            MapBin = 2,
        }

        [Header("数据源")]
        [Tooltip("程序化 = 自带一份起伏地形；游戏地图 = 读 MapRender.mapData（需地图已加载）；地图文件 = 直接读 .bin")]
        public SourceMode Mode = SourceMode.Procedural;

        [Tooltip("地图 .bin 路径。可填绝对路径，或相对 Content 目录（如 Map/DefaultMap.bin）；Mode = 地图文件 时使用")]
        public string MapBinPath = "Map/DefaultMap.bin";

        [Tooltip("程序化地形顶点数（世界 Z 方向）")]
        public int ProceduralWidth = 1024;

        [Tooltip("程序化地形顶点数（世界 X 方向）")]
        public int ProceduralHeight = 1024;

        [Tooltip("顶点间距（世界单位，与 MapData.quadSize 同义）")]
        public float QuadSize = 5f;

        [Tooltip("程序化噪声种子，保证每次生成结果一致")]
        public int NoiseSeed = 1337;

        [Header("LOD")]
        [Tooltip("分裂距离系数：越大越粗糙（距离小于 系数×patch尺寸 才分裂）")]
        [Range(0.25f, 4f)]
        public float SplitFactor = 1f;

        [Tooltip("相机高度计入视距的权重：1 = 完整计入（默认，相机拉高即整体变粗）；" +
                 "0 = 只看平面距离（相机拉高不变粗，等于旧行为）；0~1 之间可让高度影响更柔和。")]
        [Range(0f, 2f)]
        public float HeightLodWeight = 1f;

        [Tooltip("单帧 patch 上限（对齐 314 SelectionCapacity）")]
        [Range(64, 4096)]
        public int Capacity = 1024;

        [Tooltip("迟滞系数：已分裂的节点要退到更远才合并，防抖")]
        [Range(1f, 2f)]
        public float Hysteresis = 1.15f;

        [Tooltip("强制 LOD 级别（调试用）：-1 = 按距离自动；0 = 叶层最精细；取 LevelCount-1（本图 4）= 全局最粗，" +
                 "整幅地图只有 1 个 patch、65×65 顶点、顶点间距 80 世界单位。\n" +
                 "注意：自动模式下根层永远不会被单独选中 —— 观察点恒在地图内，根节点距离恒为 0，必然分裂。")]
        public int ForceLodLevel = -1;

        [Tooltip("视锥剔除时 AABB 的 Y 扩展量")]
        public float CullExtentY = 400f;

        [Header("外观")]
        [Tooltip("基础色：仅在没读到地表图层时生效（地图文件模式读到图层后会被贴图取代）")]
        public Color BaseColor = new(0.46f, 0.52f, 0.34f, 1f);

        [Tooltip("是否使用地图自带的 48 层地表贴图（关闭则回落到单一基础色）")]
        public bool UseLayerTexture = true;

        [Tooltip("是否叠加底图 BaseTex/BaseMap{季节}.png（旧管线 _BaseTex 口径）")]
        public bool UseBaseMap = true;

        [Range(0f, 1f)]
        [Tooltip("底图叠加强度（旧管线 _BaseColorIntensity，默认 0.85）")]
        public float BaseColorIntensity = 0.85f;

        [Tooltip("是否显示六边形地格线（旧管线 _GridTex 口径）")]
        public bool ShowGrid = true;

        [Tooltip("是否绘制水面（高度取顶点 water 数据 × 0.5，与旧管线 MapTerrain 同口径）")]
        public bool RenderWater = true;

        [Range(0f, 1f)]
        [Tooltip("水面整体不透明度（旧管线 water.shader 的 _Alpha 默认 0.9）")]
        public float WaterAlpha = 0.9f;

        [Range(0, 2)]
        [Tooltip("水面 LOD 粗化档位：0 = 与地表同档（最精细）；1 = 水面分裂半径减半（默认）；2 = 再减半。\n" +
                 "档位越大水面 patch 越少、顶点/三角形越省，岸线取样粒度同步变粗" +
                 "（叶层 patch 320 单位时，水面顶点间距 = 320/32 = 10 世界单位）。\n" +
                 "水面没有顶点动画，只需表达海平面高度与岸线渐变，所以允许比地表粗。")]
        public int WaterLodBias = 1;

        [Tooltip("裙边深度比例（相对 patch 尺寸）；0 = 关闭。Morph 正确时不需要，异常接缝可临时开启")]
        [Range(0f, 0.05f)]
        public float SkirtDepth = 0f;

        [Header("调试")]
        [Tooltip("调试视图：层级着色 / Morph 热力图")]
        public TerrainSangoDebugView DebugView = TerrainSangoDebugView.None;

        [Tooltip("把最近一次绘制统计打到 Console")]
        public bool LogStats = false;

        [Tooltip("编辑器非运行模式下也用 Scene 视图预览")]
        public bool PreviewInEditMode = true;

        /// <summary>地形数据源（可被外部读取，用于贴地查询）。</summary>
        public TerrainSangoSource Source { get; private set; }

        /// <summary>最近一次选中的 patch 数（含被剔除的）。</summary>
        public int LastPatchCount { get; private set; }

        /// <summary>最近一次可见的 patch 数。</summary>
        public int LastVisibleCount { get; private set; }

        /// <summary>最近一次的实例化绘制调用次数。</summary>
        public int LastDrawCalls { get; private set; }

        /// <summary>最近一次提交的水面 patch 数（合并后，与地表 patch 数不同）。</summary>
        public int LastWaterPatchCount { get; private set; }

        /// <summary>相机。</summary>
        [Header("相机")]
        [Tooltip("LOD 与剔除使用的相机，留空取 Camera.main")]
        public Camera TargetCamera;

        private TerrainSangoLod lod;
        private TerrainSangoLayerSet layerSet;
        private Mesh patchMesh;
        private Mesh waterMesh;
        private Material material;
        private Material waterMaterial;
        private readonly Matrix4x4[] instanceMatrices = new Matrix4x4[TerrainSangoConstants.MaxInstancesPerDraw];
        /// <summary>水面专用的 LOD 选择器（独立迟滞状态，避免与地表的 LOD 互相干扰）。</summary>
        private TerrainSangoLod waterLod;
        private bool failed;

        /// <summary>上次成功构建时使用的 .bin 路径；用于在 Inspector 改路径后自动重建。</summary>
        private string builtMapBinPath;

        private void OnEnable()
        {
            // 绘制挂在相机渲染回调上：提交次数严格等于渲染次数，Scene 视图不重绘时不会堆积渲染节点
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        /// <summary>
        /// 重建数据源与网格。编辑器里改完参数或换了地图后手动调用。
        /// </summary>
        [ContextMenu("重建地形数据源")]
        public void Rebuild()
        {
            failed = false;
            ReleaseSource();
            ReleasePatchMesh();

            try
            {
                Source = Mode switch
                {
                    SourceMode.GameMap => BuildFromGameMap(),
                    SourceMode.MapBin => BuildFromMapBin(),
                    _ => BuildProcedural(),
                };
                builtMapBinPath = Mode == SourceMode.MapBin ? MapBinPath : null;
                if (Source == null)
                {
                    failed = true;
                    return;
                }
                lod = new TerrainSangoLod
                {
                    SplitFactor = SplitFactor,
                    HeightWeight = HeightLodWeight,
                    Capacity = Capacity,
                    Hysteresis = Hysteresis,
                    CullExtentY = CullExtentY,
                    ForceLevel = ForceLodLevel,
                };
                layerSet = UseLayerTexture ? TerrainSangoLayerSet.Build(Source) : null;
                Log.Info($"TerrainSango 就绪: 顶点 {Source.VertexWidth}x{Source.VertexHeight}, " +
                         $"间距 {Source.QuadSize}, 世界 {Source.WorldWidth:F0}x{Source.WorldHeight:F0}, " +
                         $"四叉树 {Source.LevelCount} 层 (叶 patch {Source.LeafWorldSize:F0} 世界单位), " +
                         $"地表图层 {(layerSet != null ? layerSet.Count.ToString() : "无")}",
                    Sango.Log.LogType.World);
            }
            catch (System.Exception exception)
            {
                failed = true;
                Log.Error($"TerrainSango 初始化失败: {exception.Message}");
            }
        }

        private TerrainSangoSource BuildProcedural()
            => TerrainSangoSource.CreateProcedural(ProceduralWidth, ProceduralHeight, QuadSize, NoiseSeed);

        private TerrainSangoSource BuildFromGameMap()
        {
            MapRender render = MapRender.Instance;
            if (render == null || render.mapData == null || render.mapData.vertexDatas == null)
            {
                Log.Warning("TerrainSango 取不到 MapRender.mapData，无法使用游戏地图模式");
                return null;
            }
            TerrainSangoSource source = TerrainSangoSource.FromMapData(render.mapData, render.mapData.quadSize);
            // FromMapData 只有高度：图层索引、水面标记、每层贴图名与 tiling、贴图目录全部要在这里补齐，
            // 否则 TerrainSangoLayerSet.Build 会因 Layers 为空直接返回 null，地表退化成单色、且没有水面
            source.FillFromGameMap(render);
            InjectCellMoveable(render, source);
            return source;
        }

        /// <summary>
        /// 切换季节：只重建地表贴图集合（Texture2DArray / 底图 / 地格），不动高度与 LOD。
        ///
        /// 【为什么单开一条路径】Rebuild 会重新遍历整张高度场并重建 1025×1025 的索引纹理，
        /// 大地图上要上百毫秒；换季只是"同一批图层换一套贴图"，没必要付这个代价。
        ///
        /// 【谁调用】TerrainSangoBootstrap 订阅 MapRender.onSeasonChange 后转发过来，
        /// 与旧管线 MapProperty.OnSeasonChange → MapLayer.UpdateRender 的语义一致。
        /// </summary>
        /// <param name="season">季节索引（0 = Autumn，与 MapRender.SeasonNames 同序）</param>
        public void ApplySeason(int season)
        {
            if (Source == null) return;

            Source.SetSeason(season);

            if (layerSet != null)
            {
                layerSet.Dispose();
                layerSet = null;
            }
            layerSet = UseLayerTexture ? TerrainSangoLayerSet.Build(Source) : null;

            Log.Info($"TerrainSango 切换季节: {Source.SeasonName}（地表图层 {(layerSet != null ? layerSet.Count.ToString() : "无")}）",
                Sango.Log.LogType.World);
        }

        /// <summary>
        /// 把旧管线已经算好的"每格可通行"搬到新管线（MapGrid.GridMaskTexture 的 alpha）。
        ///
        /// 【为什么读旧纹理而不是重查地形表】
        ///   MapGrid.cs:444-450 在加载地图时已按 <c>TerrainType.moveable</c> 写好 alpha：
        ///   可通行的格子 alpha = 255，不可通行为 0。直接复用这一份结果，
        ///   既不必重复解析 TerrainTypes，也保证新旧管线"哪里画格线"逐格一致
        ///   —— 水里的「河」「海」moveable = true，因此照样会画格子。
        ///
        /// 【坐标约定】纹理 x 轴 = 格 Z，y 轴 = 格 X 且垂直翻转存储（<c>py = height - 1 - 格X</c>），
        ///   与 CreateGridMaskTexture 的写入约定相同，这里反向读回即可。
        /// </summary>
        private static void InjectCellMoveable(MapRender render, TerrainSangoSource source)
        {
            MapGrid grid = render.mapGrid;
            if (grid == null || source == null) return;

            // 格边长必须以地图真实值为准：FromMapData 只有顶点数据，默认值可能与实际不符，
            // 那样格线会整体错位（着色器的 _GridSize 也取自这里）
            source.SetGridSize(grid.gridSize);

            Texture2D mask = grid.GridMaskTexture;
            if (mask == null || source.GridSize <= 0) return;

            int cellsZ = mask.width;    // 纹理 x 轴 = 格 Z
            int cellsX = mask.height;   // 纹理 y 轴 = 格 X（已垂直翻转存）
            int expectZ = Mathf.Max(Mathf.RoundToInt(source.WorldWidth / source.GridSize), 1);
            int expectX = Mathf.Max(Mathf.RoundToInt(source.WorldHeight / source.GridSize), 1);
            // 尺寸口径对不上时宁可不注入（回退到 water 代理），也不要按错位数据画格线
            if (cellsZ != expectZ || cellsX != expectX) return;

            Color32[] pixels = mask.GetPixels32();
            if (pixels == null || pixels.Length != cellsZ * cellsX) return;

            bool[] moveable = new bool[cellsZ * cellsX];
            for (int cx = 0; cx < cellsX; cx++)
            {
                int py = cellsX - 1 - cx;      // 与 MapGrid.cs:448 的垂直翻转存法对应
                int row = py * cellsZ;
                for (int cz = 0; cz < cellsZ; cz++)
                    moveable[cz * cellsX + cx] = pixels[row + cz].a >= 128;
            }
            source.SetCellMoveable(moveable, cellsZ, cellsX);
        }

        /// <summary>
        /// 从地图 .bin 文件构建。不依赖 MapRender，编辑器里也能直接看真实地图的高度场。
        /// </summary>
        private TerrainSangoSource BuildFromMapBin()
        {
            TerrainSangoSource source = TerrainSangoSource.FromMapBin(MapBinPath);
            Log.Info($"TerrainSango 读取地图文件成功: {MapBinPath} " +
                     $"({source.VertexWidth}x{source.VertexHeight} 顶点, 高度 " +
                     $"{source.MinimumHeight:F1}~{source.MaximumHeight:F1})",
                Sango.Log.LogType.World);
            return source;
        }

        /// <summary>相机渲染前提交地形 patch。</summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (failed) return;
            if (!Application.isPlaying && !PreviewInEditMode) return;
            if (camera != ResolveCamera()) return;

            // Inspector 里换了 .bin 路径就自动重建，不用每次手动点"重建地形数据源"
            bool binPathChanged = Mode == SourceMode.MapBin && builtMapBinPath != MapBinPath;
            if (Source == null || lod == null || binPathChanged) Rebuild();
            if (Source == null || lod == null) return;

            // 参数实时同步（编辑器里拖滑杆立刻能看到效果）
            lod.SplitFactor = SplitFactor;
            lod.HeightWeight = HeightLodWeight;
            lod.Capacity = Capacity;
            lod.Hysteresis = Hysteresis;
            lod.CullExtentY = CullExtentY;
            // 强制级别：-1 = 自动。改成其他值时整图锁死在该层，改回 -1 立即恢复按距离选择
            lod.ForceLevel = ForceLodLevel;

            Material drawMaterial = EnsureMaterial();
            if (drawMaterial == null) return;
            EnsurePatchMesh();
            if (patchMesh == null) return;

            IReadOnlyList<TerrainSangoPatch> patches = lod.Build(camera, Source);
            LastPatchCount = patches.Count;
            LastVisibleCount = lod.VisibleCount;

            // 材质参数必须在 Build 之后设置：morph 用的观察点要与 LOD 选择用的是同一个（lod.Eye）。
            // 若着色器直接用 _WorldSpaceCameraPos，Scene 视图把相机推到很远时两者会差一整个图幅，
            // 表现为"红色阈值带（morph=1）出现在相机脚下而不是细分中心"，切换时机与几何收敛错位。
            Vector2 texSize = Source.HeightTextureSize;
            drawMaterial.SetTexture("_HeightTex", Source.HeightTexture);
            drawMaterial.SetVector("_HeightTexSize", new Vector4(texSize.x, texSize.y, 0f, 0f));
            drawMaterial.SetFloat("_QuadSize", Source.QuadSize);
            drawMaterial.SetFloat("_HeightRange", 255f * Source.HeightScale);
            drawMaterial.SetFloat("_SplitFactor", Mathf.Max(SplitFactor, 0.01f));
            // 与 LOD 判定用的是同一个视距口径：morph 必须按"平面距离² + 相机高度²"算，
            // 否则相机拉高后 LOD 已经合并、顶点却还没收敛，切换瞬间会看到几何跳变
            drawMaterial.SetFloat("_ViewHeight", lod.ViewHeight);
            drawMaterial.SetFloat("_LeafSize", Source.LeafWorldSize);
            drawMaterial.SetFloat("_SkirtDepth", SkirtDepth);
            drawMaterial.SetFloat("_DebugView", (float)DebugView);
            drawMaterial.SetColor("_BaseColor", BaseColor);

            Vector3 eye = lod.Eye;
            drawMaterial.SetVector("_EyePos", new Vector4(eye.x, eye.y, eye.z, 0f));

            // 地表图层：索引纹理 + 数组 + 每层参数。任何一环缺失都回落到单色，不会画出黑地形。
            bool useLayers = layerSet != null && layerSet.Albedo != null && layerSet.Params != null
                             && Source.LayerIndexTexture != null;
            drawMaterial.SetFloat("_UseLayers", useLayers ? 1f : 0f);
            if (useLayers)
            {
                drawMaterial.SetTexture("_LayerIndexTex", Source.LayerIndexTexture);
                drawMaterial.SetTexture("_LayerAlbedo", layerSet.Albedo);
                drawMaterial.SetTexture("_LayerParams", layerSet.Params);
                drawMaterial.SetFloat("_LayerCount", layerSet.Count);
                drawMaterial.SetFloat("_WaterLayerIndex", Source.WaterLayerIndex);
                drawMaterial.SetVector("_WorldSize",
                    new Vector4(Source.WorldWidth, Source.WorldHeight, 0f, 0f));

                bool useBaseMap = UseBaseMap && layerSet.BaseMap != null;
                drawMaterial.SetFloat("_UseBaseMap", useBaseMap ? 1f : 0f);
                drawMaterial.SetFloat("_BaseColorIntensity", Mathf.Clamp01(BaseColorIntensity));
                if (useBaseMap)
                    drawMaterial.SetTexture("_BaseTex", layerSet.BaseMap);

                bool useGrid = ShowGrid && layerSet.GridTexture != null
                               && layerSet.GridMask != null && layerSet.RangeMask != null && layerSet.DarkMask != null;
                drawMaterial.SetFloat("_UseGrid", useGrid ? 1f : 0f);
                if (useGrid)
                {
                    drawMaterial.SetTexture("_GridTex", layerSet.GridTexture);
                    drawMaterial.SetTexture("_GridMask", layerSet.GridMask);
                    drawMaterial.SetTexture("_RangeMask", layerSet.RangeMask);
                    drawMaterial.SetTexture("_DarkMask", layerSet.DarkMask);
                    drawMaterial.SetFloat("_GridSize", Mathf.Max(Source.GridSize, 1f));
                    drawMaterial.SetFloat("_GridFlag", 1f);
                    drawMaterial.SetFloat("_DarkFlag", 0f);
                }
            }

            int drawCalls = 0;
            DrawPatches(camera, patches, drawMaterial, patchMesh, ref drawCalls);

            // 水面：独立一张面，高度取顶点 water 数据，画在地表之后（透明、ZWrite Off）。
            // 水面低于地表的地方会被深度测试挡住，形成"陆地露出水面"的正确遮挡关系。
            if (useLayers && RenderWater && Source.WaterLayerIndex >= 0)
            {
                Material water = EnsureWaterMaterial();
                if (water != null)
                {
                    water.SetTexture("_LayerIndexTex", Source.LayerIndexTexture);
                    water.SetTexture("_LayerAlbedo", layerSet.Albedo);
                    water.SetTexture("_LayerParams", layerSet.Params);
                    water.SetFloat("_LayerCount", layerSet.Count);
                    water.SetFloat("_WaterLayerIndex", Source.WaterLayerIndex);
                    water.SetFloat("_QuadSize", Source.QuadSize);
                    water.SetVector("_HeightTexSize", new Vector4(texSize.x, texSize.y, 0f, 0f));
                    water.SetVector("_WorldSize", new Vector4(Source.WorldWidth, Source.WorldHeight, 0f, 0f));
                    water.SetFloat("_WaterAlpha", Mathf.Clamp01(WaterAlpha));

                    // 水面用"更粗的网格 + 合并后的 patch"：
                    //   ① 网格 33×33（地表 65×65 的一半密度）—— 水面没有顶点动画，不需要 64 格；
                    //   ② patch 按 WaterLodBias 合并，4 个地表 patch 只提交 1 个水面实例。
                    // 两项合计把水面这一遍的提交数据压到地表的 1/16 左右。
                    Mesh waterDrawMesh = EnsureWaterMesh();
                    if (waterDrawMesh != null)
                    {
                        IReadOnlyList<TerrainSangoPatch> waterPatches = BuildWaterPatches(camera);
                        DrawPatches(camera, waterPatches, water, waterDrawMesh, ref drawCalls);
                        LastWaterPatchCount = waterPatches.Count;
                    }
                }
            }
            else
            {
                LastWaterPatchCount = 0;
            }

            LastDrawCalls = drawCalls;

            if (LogStats)
                Log.Info($"TerrainSango: patch {LastPatchCount}, 可见 {LastVisibleCount}, " +
                         $"水面 patch {LastWaterPatchCount}, draw {drawCalls}, " +
                         $"相机视距 {lod.ViewHeight:F0}",
                    Sango.Log.LogType.World);
        }

        /// <summary>
        /// 按可见 patch 提交实例。
        ///
        /// 实例矩阵只编码"世界矩形"：平移 = 原点，缩放 = 边长；
        /// patch 的世界尺寸在着色器里由矩阵的 X 轴长度反推，用于算 morph 与调试色。
        /// 地表与水面共用同一套矩阵编码，只是水面传自己的粗网格（33×33）。
        ///
        /// 【按 EdgeCoarse 分桶】CDLOD 边界缝合要求"挨着更粗邻居的边"把边界顶点 morph 锁 1，
        /// 这个标记是 per-patch 的；而 DrawMeshInstanced 的材质参数只能 per-draw（MaterialPropertyBlock），
        /// 所以按 16 种边界形态分桶，同桶共享一份 _EdgeCoarse。桶数上限 16，
        /// 每桶内部仍按 MaxInstancesPerDraw 切批，draw call 最多多十几次，可忽略。
        /// </summary>
        /// <param name="mesh">要提交的 patch 网格（地表 65×65 / 水面 33×33）</param>
        private void DrawPatches(Camera camera, IReadOnlyList<TerrainSangoPatch> patches, Material drawMaterial,
            Mesh mesh, ref int drawCalls)
        {
            if (mesh == null) return;

            EnsureStitchBuffers();
            int stride = stitchBucketStride;
            System.Array.Clear(stitchBucketCounts, 0, stitchBucketCounts.Length);

            for (int i = 0; i < patches.Count; i++)
            {
                TerrainSangoPatch patch = patches[i];
                if (patch.Culled) continue;

                int bucket = patch.EdgeCoarse & 15;
                int slot = stitchBucketCounts[bucket];
                if (slot >= stride) continue;   // 容量护栏：单桶上限 = Capacity，正常到不了
                stitchBucketCounts[bucket] = slot + 1;
                stitchBuckets[bucket * stride + slot] = Matrix4x4.TRS(
                    new Vector3(patch.OriginX, 0f, patch.OriginZ),
                    Quaternion.identity,
                    new Vector3(patch.Size, 1f, patch.Size));
            }

            int maxPerBatch = instanceMatrices.Length;
            for (int b = 0; b < 16; b++)
            {
                int count = stitchBucketCounts[b];
                if (count == 0) continue;

                stitchProps.SetVector("_EdgeCoarse", EdgeCoarseVectors[b]);
                for (int offset = 0; offset < count; offset += maxPerBatch)
                {
                    int n = System.Math.Min(maxPerBatch, count - offset);
                    // DrawMeshInstanced 只认 matrices[0..n)，把桶内矩阵拷到提交缓冲开头
                    System.Array.Copy(stitchBuckets, b * stride + offset, instanceMatrices, 0, n);
                    SubmitBatch(camera, n, drawMaterial, mesh, stitchProps);
                    drawCalls++;
                }
            }
        }

        /// <summary>提交一批实例。props 为空 = 不带 per-draw 材质参数（旧行为）。</summary>
        private void SubmitBatch(Camera camera, int count, Material drawMaterial, Mesh mesh,
            MaterialPropertyBlock props = null)
        {
            Graphics.DrawMeshInstanced(mesh, 0, drawMaterial, instanceMatrices, count,
                props, ShadowCastingMode.Off, false, gameObject.layer, camera);
        }

        /// <summary>按 EdgeCoarse 分桶的实例矩阵缓存（16 种边界形态，懒分配、Capacity 变大时重建）。</summary>
        private Matrix4x4[] stitchBuckets;
        private int[] stitchBucketCounts = new int[16];
        private int stitchBucketStride;
        /// <summary>
        /// per-draw 材质参数。UnityEngine.Object 派生类**不能**在 MonoBehaviour 的字段初始化器里 new
        /// （CreateImpl 限制），必须懒创建，否则 TerrainSangoRenderer 一实例化就抛异常、新地形整个起不来。
        /// </summary>
        private MaterialPropertyBlock stitchProps;

        /// <summary>16 种边界形态对应的 _EdgeCoarse 向量（x=东 y=西 z=北 w=南，1 = 该边邻居更粗）。</summary>
        private static readonly Vector4[] EdgeCoarseVectors = BuildEdgeCoarseVectors();

        private static Vector4[] BuildEdgeCoarseVectors()
        {
            var vectors = new Vector4[16];
            for (int i = 0; i < 16; i++)
                vectors[i] = new Vector4(
                    (i & 1) != 0 ? 1f : 0f,
                    (i & 2) != 0 ? 1f : 0f,
                    (i & 4) != 0 ? 1f : 0f,
                    (i & 8) != 0 ? 1f : 0f);
            return vectors;
        }

        /// <summary>分桶缓冲懒分配：stride 取 Capacity（单桶最多装全部 patch），Capacity 调大时重建。</summary>
        private void EnsureStitchBuffers()
        {
            if (stitchProps == null)
                stitchProps = new MaterialPropertyBlock();
            if (stitchBuckets != null && stitchBucketStride >= Capacity)
                return;
            stitchBucketStride = System.Math.Max(Capacity, instanceMatrices.Length);
            stitchBuckets = new Matrix4x4[16 * stitchBucketStride];
        }

        /// <summary>
        /// 选出一份"只给水面用"的 patch 集合。
        ///
        /// 【为什么不直接复用地表 patch 列表，也不做父层合并】
        ///   水面是平的：高度只来自 _LayerIndexTex 的 G 通道（本图 88.8% 的水面顶点都是同一个
        ///   高度字节），相邻 patch 之间没有任何需要保留的几何细节，用与地表同样密的划分纯属浪费。
        ///   但"把每个地表 patch 提升一层"是错的：混合 LOD 下地表同时存在细叶与粗节点，
        ///   某个远处的粗节点合并后会把它覆盖范围内的全部细分节点吞掉（去重时被判为被包含），
        ///   结果是近处水面也退化成最粗的那一档，岸线明显变钝。
        ///
        /// 【正确做法】把分裂阈值放大 2^bias 倍，用独立的 LOD 实例重新选一次：
        ///   得到的仍然是一套合法的四叉树叶子集合（互不重叠、无遗漏、近细远粗），
        ///   只是整体比地表粗 bias 档，patch 数约为地表的 1/4^bias。
        ///   独立实例意味着独立的迟滞状态，不会与地表的 LOD 互相干扰。
        /// </summary>
        /// <param name="camera">观察相机（与地表共用同一个观察点口径）</param>
        /// <returns>水面 patch 列表（内部复用，下一次调用会被清空）</returns>
        private IReadOnlyList<TerrainSangoPatch> BuildWaterPatches(Camera camera)
        {
            waterLod ??= new TerrainSangoLod();

            // 阈值除以 2^bias：LOD 的分裂判据是 distance < SplitFactor * size，
            // 阈值变小 = 分裂半径缩小 = 更粗（patch 面积 ×4^bias、数量 ÷4^bias），
            // 但仍保持"近细远粗"的形状 —— 不会像父层合并那样把近处的细分一起吞掉。
            waterLod.SplitFactor = Mathf.Max(SplitFactor, 0.01f) / (1 << Mathf.Clamp(WaterLodBias, 0, 2));
            // 相机高度同样计入水面视距：水面与地表同步变粗，否则远景下水会比地形细
            waterLod.HeightWeight = HeightLodWeight;
            waterLod.Hysteresis = Hysteresis;
            waterLod.Capacity = Capacity;
            waterLod.CullExtentY = CullExtentY;
            waterLod.ForceLevel = -1;   // 水面不走"强制级别"调试开关，保持自适应

            return waterLod.Build(camera, Source);
        }

        private Camera ResolveCamera()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null) return sceneView.camera;
            }
#endif
            return TargetCamera != null ? TargetCamera : Camera.main;
        }

        /// <summary>创建材质。</summary>
        private Material EnsureMaterial()
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Sango/TerrainSango");
            if (shader == null)
            {
                Log.Error("找不到着色器 Sango/TerrainSango");
                failed = true;
                return null;
            }
            material = new Material(shader)
            {
                name = "TerrainSangoRuntime",
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return material;
        }

        /// <summary>创建水面材质（Sango/TerrainSangoWater）。</summary>
        private Material EnsureWaterMaterial()
        {
            if (waterMaterial != null) return waterMaterial;
            Shader shader = Shader.Find("Sango/TerrainSangoWater");
            if (shader == null)
            {
                Log.Error("找不到着色器 Sango/TerrainSangoWater，水面不绘制");
                RenderWater = false;
                return null;
            }
            waterMaterial = new Material(shader)
            {
                name = "TerrainSangoWaterRuntime",
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return waterMaterial;
        }

        /// <summary>
        /// 构建 patch 共用网格：65×65 个 [0,1] 区间的平面顶点。
        ///
        /// 顶点 Y 恒为 0，真正高度由顶点着色器采样高度纹理得到。
        /// 索引对角按 (x + z) 的奇偶交替，避免规则网格在低精度下出现明显的条纹走向。
        /// </summary>
        private Mesh EnsurePatchMesh()
        {
            if (patchMesh != null) return patchMesh;
            int n = TerrainSangoConstants.PatchVertices;
            int vertexCount = n * n;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (int z = 0; z < n; z++)
            {
                float v = z / (float)(n - 1);
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1);
                    int i = z * n + x;
                    vertices[i] = new Vector3(u, 0f, v);
                    normals[i] = Vector3.up;
                    uvs[i] = new Vector2(u, v);
                }
            }

            var triangles = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i0 = z * n + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + n;
                    int i3 = i2 + 1;
                    if (((x + z) & 1) == 0)
                    {
                        triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                        triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
                    }
                    else
                    {
                        triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i3;
                        triangles[t++] = i0; triangles[t++] = i3; triangles[t++] = i1;
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "TerrainSangoPatch",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            // 包围盒必须覆盖"着色器位移后"的几何：本地 XZ 是 [0,1]，世界尺寸由实例矩阵缩放；
            // 高度虽然只在着色器里产生，但 Unity 仍会用这份本地包围盒做绘制级剔除，
            // 若 Y 只给 1 个单位，地形抬高后整批实例可能被误判为不可见。
            // 世界高度上限 = 255 * 0.5 = 127.5（见 TerrainSangoSource.HeightScale），
            // 因此 Y 取 [-64, 192] 覆盖 0..127.5 并留出余量。
            mesh.bounds = new Bounds(new Vector3(0.5f, 64f, 0.5f), new Vector3(1f, 256f, 1f));
            patchMesh = mesh;
            return patchMesh;
        }

        /// <summary>
        /// 构建水面 patch 共用的粗网格：33×33 个 [0,1] 区间的平面顶点。
        ///
        /// 【为什么只有地表的一半密度】
        ///   地表 65 顶点是为了采样高度场表达起伏（每格 5 世界单位）；
        ///   水面没有顶点动画，顶点只做两件事：
        ///     ① 抬到海平面高度（G 通道，本图 88.8% 的水面字节都是同一个值，插值不出差异）；
        ///     ② 把水面标记（B 通道）插值成岸线渐变，渐变带宽度 = 顶点间距。
        ///   32 格时间距 = patchSize/32（叶层 10 世界单位 = 半格地格），岸线仍然贴得住，
        ///   而三角形数只有地表的 1/4。
        ///
        /// 索引对角同样按 (x + z) 奇偶交替，避免规则网格在低精度下出现明显的条纹走向。
        /// </summary>
        private Mesh EnsureWaterMesh()
        {
            if (waterMesh != null) return waterMesh;

            int n = TerrainSangoConstants.WaterPatchVertices;
            int vertexCount = n * n;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (int z = 0; z < n; z++)
            {
                float v = z / (float)(n - 1);
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1);
                    int i = z * n + x;
                    vertices[i] = new Vector3(u, 0f, v);
                    normals[i] = Vector3.up;
                    uvs[i] = new Vector2(u, v);
                }
            }

            var triangles = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int z = 0; z < n - 1; z++)
            {
                for (int x = 0; x < n - 1; x++)
                {
                    int i0 = z * n + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + n;
                    int i3 = i2 + 1;
                    if (((x + z) & 1) == 0)
                    {
                        triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                        triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
                    }
                    else
                    {
                        triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i3;
                        triangles[t++] = i0; triangles[t++] = i3; triangles[t++] = i1;
                    }
                }
            }

            var mesh = new Mesh
            {
                name = "TerrainSangoWaterPatch",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            // 包围盒与地表一致：水面同样在顶点着色器里被抬到 0~127.5 的高度
            mesh.bounds = new Bounds(new Vector3(0.5f, 64f, 0.5f), new Vector3(1f, 256f, 1f));
            waterMesh = mesh;
            return waterMesh;
        }

        /// <summary>释放数据源。</summary>
        private void ReleaseSource()
        {
            if (layerSet != null)
            {
                layerSet.Dispose();
                layerSet = null;
            }
            if (Source != null)
            {
                Source.Dispose();
                Source = null;
            }
            lod = null;
            waterLod = null;
        }

        /// <summary>释放 patch 网格（地表与水面两张）。</summary>
        private void ReleasePatchMesh()
        {
            if (patchMesh != null)
            {
                if (Application.isPlaying) Destroy(patchMesh); else DestroyImmediate(patchMesh);
                patchMesh = null;
            }
            if (waterMesh != null)
            {
                if (Application.isPlaying) Destroy(waterMesh); else DestroyImmediate(waterMesh);
                waterMesh = null;
            }
        }

        /// <summary>释放全部运行时资源。</summary>
        private void ReleaseAll()
        {
            ReleaseSource();
            ReleasePatchMesh();
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                material = null;
            }
            if (waterMaterial != null)
            {
                if (Application.isPlaying) Destroy(waterMaterial); else DestroyImmediate(waterMaterial);
                waterMaterial = null;
            }
            failed = false;
        }
    }
}
