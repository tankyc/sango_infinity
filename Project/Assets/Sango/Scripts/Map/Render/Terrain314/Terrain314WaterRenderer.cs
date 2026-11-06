using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Sango.Render.Terrain314
{
    /// <summary>
    /// 314 式海面渲染器（Terrain314 独立体系）。
    ///
    /// 【对齐依据】
    ///   - 水面是世界常数高度，不是"高度场 ≤ 阈值即水"：
    ///     src/San14.Data/Rendering/Environment/State/OceanSurfaceSettings.cs:7（WaterHeight）
    ///     本项目高度场里 13.6% 的采样精确为 0，即海平面 = 0，默认值取 0。
    ///   - 可见域 = 视锥与水面平面求交后的 XZ 包围盒，按 tile 平铺：
    ///     native/.../Ocean/WaterGeometry.h:107-136
    ///   - LOD 阈值为"距离分段"，近处密、远处疏：
    ///     native/.../Ocean/OceanGrid.h:176,180-182
    ///   - 远处用 4 顶点远海四边形收尾，不参与波动：
    ///     native/.../Ocean/WaterGeometry.h:139-140
    ///
    /// 【与 314 的差异（明确记录，不假装已完全对齐）】
    ///   1) 314 是 deferred 不透明 + 写深度 + 6 MRT（OceanRenderer.h:192-224）；
    ///      本项目走 URP Forward + 透明混合，靠深度测试被陆地遮挡。
    ///   2) 314 的顶点位移来自 CPU 端 FFT（OceanFft.h:195-245）；
    ///      这里改用 4 层 Gerstner 在顶点着色器里解析求解，省掉每帧回写 VB。
    ///   3) 314 的 6 级 LOD + skirt 缝合降为 3 档（近/中/远），不做 skirt：
    ///      水面位移幅度远小于地形高差，远档接缝在实际视角下不可见。
    ///
    /// 【使用方式】
    ///   挂在 Terrain314Renderer 所在的同一个 GameObject 上（或手动指定 Terrain 字段），
    ///   运行时自动在地形之上铺水面；不需要修改 Terrain314Renderer 的任何参数。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Terrain314WaterRenderer : MonoBehaviour
    {
        /// <summary>单次 DrawMeshInstanced 的实例上限（Unity 硬限制 1023）。</summary>
        private const int MaxInstancesPerBatch = 1023;

        /// <summary>地形渲染器：复用它的高度场与相机，留空则在本物体上查找。</summary>
        [Header("数据源")]
        [Tooltip("地形渲染器，留空则自动取本物体上的 Terrain314Renderer")]
        public Terrain314Renderer Terrain;

        /// <summary>水面世界高度。</summary>
        [Header("水面")]
        [Tooltip("是否用导出的 water.json 覆盖水面高度（314 OceanSurfaceSettings.WaterHeight）")]
        public bool UseExportedParameters = true;

        [Tooltip("水面世界高度。314 导出值是 450，不是 0：高度场里 0 是海床，陆地在 450 以上")]
        public float WaterHeight = 450f;

        [Tooltip("水面绘制高度偏移：海面与地形共面会 z-fighting，抬高一点点即可")]
        public float SurfaceOffset = 2f;

        [Tooltip("可见距离：超出该半径不再铺 tile，改由远海四边形覆盖")]
        public float CutoffDistance = 60000f;

        [Tooltip("单块水面 tile 的世界边长。314 OceanGrid.Size = 10000")]
        public float TileSize = 10000f;

        /// <summary>近/中/远三档的格数（格数越大顶点越密）。</summary>
        [Header("LOD")]
        public int NearCells = 32;
        /// <summary>中档格数。</summary>
        public int MidCells = 16;
        /// <summary>远档格数。</summary>
        public int FarCells = 8;
        /// <summary>近档距离上限（世界单位）。314 的 LOD 间距 = MaximumDistance / DeclaredLod。</summary>
        public float NearDistance = 12000f;
        /// <summary>中档距离上限（世界单位），超出即用远档。</summary>
        public float MidDistance = 30000f;
        /// <summary>是否绘制覆盖地平线的远海四边形。</summary>
        public bool DrawFarOcean = true;

        /// <summary>浅水颜色。</summary>
        [Header("水体")]
        public Color ShallowColor = new(0.18f, 0.42f, 0.46f, 1f);
        /// <summary>深水颜色。</summary>
        public Color DeepColor = new(0.03f, 0.12f, 0.20f, 1f);
        /// <summary>菲涅尔反射色（没有反射探针时的近似）。</summary>
        public Color ReflectionColor = new(0.55f, 0.68f, 0.78f, 1f);
        /// <summary>水深到该值即完全变成深水色。</summary>
        public float DepthFade = 900f;
        /// <summary>每米水深增加的不透明度。</summary>
        public float OpacityPerMeter = 0.004f;
        /// <summary>深水区不透明度上限。</summary>
        public float MaxOpacity = 0.92f;
        /// <summary>菲涅尔指数。</summary>
        public float FresnelPower = 5f;
        /// <summary>高光锐度。</summary>
        public float SpecularPower = 240f;
        /// <summary>高光强度。</summary>
        public float SpecularIntensity = 1.2f;
        /// <summary>泡沫颜色。</summary>
        public Color FoamColor = new(0.86f, 0.92f, 0.94f, 1f);
        /// <summary>泡沫出现的浅水距离。</summary>
        public float FoamDistance = 160f;
        /// <summary>泡沫强度。</summary>
        public float FoamStrength = 1f;

        /// <summary>主导风向（xy 有效）。</summary>
        [Header("波浪")]
        public Vector4 WaveDirection = new(1f, 0.35f, 0f, 0f);
        /// <summary>四层波振幅。</summary>
        public Vector4 WaveAmplitude = new(26f, 14f, 7f, 3.5f);
        /// <summary>四层波波长。</summary>
        public Vector4 WaveLength = new(2400f, 1300f, 620f, 310f);
        /// <summary>四层波速度倍率。</summary>
        public Vector4 WaveSpeed = new(9f, 11f, 14f, 18f);
        /// <summary>四层波陡度（0 = 纯正弦，1 = 尖峰）。</summary>
        public Vector4 WaveSteepness = new(0.55f, 0.5f, 0.45f, 0.4f);

        /// <summary>手动指定的材质；留空则按 Sango/Terrain314Water 自动创建。</summary>
        [Header("材质")]
        public Material OverrideMaterial;

        private Mesh nearMesh;
        private Mesh midMesh;
        private Mesh farMesh;
        private Mesh farOceanMesh;
        private Material material;
        private MaterialPropertyBlock farOceanProperties;
        // 314 导出的海床高度遮罩（2048² R32），用于浅滩泡沫与水陆边缘淡出
        private Texture2D heightMask;
        // 314 导出的三张水体贴图：体积法线、法线噪声、三层气泡数组
        private Texture3D normalVolume;
        private Texture2D normalNoise;
        private Texture2DArray bubbleArray;
        private bool surfaceTexturesLoaded;
        // 314 导出值：NormalAnimationSpeed = 50、SurfaceBubblePeriod = 3
        private float exportedNormalAnimationSpeed = 50f;
        private float exportedSurfaceBubblePeriod = 3f;
        // 导出参数只读一次：water.json 是静态数据，重复读盘没有意义
        private bool parametersLoaded;
        private bool failed;

        // 三档 tile 的实例矩阵分开攒，一次 DrawMeshInstanced 画一档，
        // 避免每帧为分批而分配临时数组。
        private readonly List<Matrix4x4> nearMatrices = new(256);
        private readonly List<Matrix4x4> midMatrices = new(256);
        private readonly List<Matrix4x4> farMatrices = new(256);
        private readonly Matrix4x4[] batchBuffer = new Matrix4x4[MaxInstancesPerBatch];

        /// <summary>上一帧铺出的 tile 总数（三档合计），用于调试可见域覆盖是否正常。</summary>
        public int TileCount => nearMatrices.Count + midMatrices.Count + farMatrices.Count;

        private void OnEnable()
        {
            // 与地形渲染器一致：绘制必须挂在相机渲染回调上。
            // 用 EditorApplication.update 每帧提交会让渲染节点在 Scene 视图不重绘时持续堆积，
            // 最终触发 "RenderNodeQueue Page Allocator out of memory"。
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ReleaseResources();
        }

        /// <summary>
        /// 相机渲染前铺出水面。提交次数与渲染次数一一对应，不会堆积渲染节点。
        /// </summary>
        /// <param name="context">当前 SRP 渲染上下文（未使用）。</param>
        /// <param name="camera">正在渲染的相机。</param>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (failed) return;
            if (Terrain == null) Terrain = GetComponent<Terrain314Renderer>();
            if (Terrain == null) return;
            // 与地形渲染器共用同一个预览开关：没开预览就不加载、不提交绘制
            if (!Application.isPlaying && !Terrain.PreviewInEditMode) return;
            if (Terrain.Source == null) return;
            if (camera != Terrain.CurrentCamera) return;

            if (!EnsureResources()) return;

            ApplyMaterialParameters();
            BuildTileMatrices(camera);
            DrawTileBatch(nearMesh, nearMatrices, camera);
            DrawTileBatch(midMesh, midMatrices, camera);
            DrawTileBatch(farMesh, farMatrices, camera);
            DrawFarOceanQuad(camera);
        }

        /// <summary>
        /// 创建 mesh 与材质。任何一步失败都置 failed，避免每帧重试刷爆 Console。
        /// </summary>
        /// <returns>资源是否可用。</returns>
        private bool EnsureResources()
        {
            if (material == null)
            {
                if (OverrideMaterial != null)
                {
                    material = OverrideMaterial;
                }
                else
                {
                    Shader shader = Shader.Find("Sango/Terrain314Water");
                    if (shader == null)
                    {
                        failed = true;
                        Log.Error("Terrain314 水面着色器缺失: Sango/Terrain314Water");
                        return false;
                    }
                    material = new Material(shader) { name = "Terrain314WaterRuntime" };
                }
                // DrawMeshInstanced 要求材质显式打开实例化，否则每次调用都抛 InvalidOperationException
                material.enableInstancing = true;
                farOceanProperties = new MaterialPropertyBlock();
            }

            if (nearMesh == null) nearMesh = BuildTileMesh(NearCells, "Terrain314WaterNear");
            if (midMesh == null) midMesh = BuildTileMesh(MidCells, "Terrain314WaterMid");
            if (farMesh == null) farMesh = BuildTileMesh(FarCells, "Terrain314WaterFar");
            if (farOceanMesh == null) farOceanMesh = BuildTileMesh(1, "Terrain314WaterFarOcean");

            if (!parametersLoaded)
            {
                parametersLoaded = true;
                LoadExportedParameters();
                LoadHeightMask();
                LoadSurfaceTextures();
            }
            return true;
        }

        /// <summary>
        /// 加载 314 导出的三张水体贴图并上传成 Unity 运行时纹理。
        ///
        /// 导出的都是解码后的裸数据，尺寸从 water-textures.json 读，避免两端口径分叉：
        ///   normal3d.rg8   512x512x128 RG8 → Texture3D（体积法线）
        ///   normal-noise.r8 1024x1024 R8 → Texture2D
        ///   bubble.rg8     256x256x60 RG8 → Texture2DArray（三层气泡 × 20 帧）
        /// </summary>
        private void LoadSurfaceTextures()
        {
            if (surfaceTexturesLoaded || Terrain == null) return;
            surfaceTexturesLoaded = true;

            string directory = System.IO.Path.Combine(Terrain.ResolvedDirectory, "water");
            string manifestPath = System.IO.Path.Combine(directory, "water-textures.json");
            if (!System.IO.File.Exists(manifestPath))
            {
                Log.Warning($"Terrain314 缺少水体贴图清单: {manifestPath}");
                return;
            }

            try
            {
                WaterTextureDocument manifest = JsonUtility.FromJson<WaterTextureDocument>(
                    System.IO.File.ReadAllText(manifestPath));
                // 清单把四张贴图放在 textures 子对象下，缺节点要显式报一次，
                // 否则下面的 null 判断会静默跳过，表现为"没有贴图也没有任何提示"
                WaterTextureSet textures = manifest?.textures;
                if (textures == null)
                {
                    Log.Warning($"Terrain314 水体贴图清单缺少 textures 节点: {manifestPath}");
                    return;
                }

                if (textures.normal3d != null && textures.normal3d.width > 0)
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(
                        System.IO.Path.Combine(directory, textures.normal3d.file));
                    // Unity 2021.3 的 TextureFormat 没有 RG8（2022.2 才加入），
                    // 用等价的 GraphicsFormat.R8G8_UNorm 表达同样的 2 字节/像素布局：
                    // 数据零转换直传，显存与 314 原版一致，shader 采样 .rg 语义不变。
                    normalVolume = new Texture3D(textures.normal3d.width, textures.normal3d.height,
                        textures.normal3d.depth, GraphicsFormat.R8G8_UNorm, TextureCreationFlags.None)
                    {
                        name = "Terrain314WaterNormal3D",
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Bilinear
                    };
                    normalVolume.SetPixelData(bytes, 0);
                    normalVolume.Apply(false, true);
                }

                if (textures.normalNoise != null && textures.normalNoise.width > 0)
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(
                        System.IO.Path.Combine(directory, textures.normalNoise.file));
                    normalNoise = new Texture2D(textures.normalNoise.width, textures.normalNoise.height,
                        TextureFormat.R8, false, true)
                    {
                        name = "Terrain314WaterNormalNoise",
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Bilinear
                    };
                    normalNoise.LoadRawTextureData(bytes);
                    normalNoise.Apply(false, true);
                }

                if (textures.bubble != null && textures.bubble.layers > 0)
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(
                        System.IO.Path.Combine(directory, textures.bubble.file));
                    int width = textures.bubble.width;
                    int height = textures.bubble.height;
                    int layers = textures.bubble.layers;
                    // 同 normalVolume：2021.3 无 TextureFormat.RG8，改用 GraphicsFormat 等价表达。
                    // 每层仍是 width*height*2 字节，SetPixelData 的 layerBytes 无需调整。
                    bubbleArray = new Texture2DArray(width, height, layers,
                        GraphicsFormat.R8G8_UNorm, TextureCreationFlags.None)
                    {
                        name = "Terrain314WaterBubble",
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Bilinear
                    };
                    int layerBytes = width * height * 2;
                    for (int layer = 0; layer < layers; layer++)
                    {
                        bubbleArray.SetPixelData(bytes, 0, layer, layer * layerBytes);
                    }
                    bubbleArray.Apply(false, true);
                }

                Log.Info($"Terrain314 水体贴图就绪: 体积法线 {textures.normal3d?.width}x" +
                         $"{textures.normal3d?.height}x{textures.normal3d?.depth}, " +
                         $"气泡层 {textures.bubble?.layers}");
            }
            catch (System.Exception exception)
            {
                Log.Warning($"Terrain314 水体贴图加载失败: {exception.Message}");
            }
        }

        /// <summary>
        /// 读 314 导出的 water.json，取第一季第一个相机包的水面高度。
        ///
        /// 只取 WaterHeight：314 的 Wave.Amplitude 是 Phillips 谱的方差量纲，
        /// 与这里 Gerstner 的世界单位振幅不是同一套量纲，直接套用会得到 0 波幅，
        /// 所以波浪参数继续用 Inspector 上按视觉调好的值。
        /// </summary>
        private void LoadExportedParameters()
        {
            if (!UseExportedParameters || Terrain == null) return;

            string path = System.IO.Path.Combine(Terrain.ResolvedDirectory, "water", "water.json");
            if (!System.IO.File.Exists(path)) return;

            try
            {
                WaterDocument document = JsonUtility.FromJson<WaterDocument>(
                    System.IO.File.ReadAllText(path));
                if (document?.seasons == null || document.seasons.Length == 0) return;
                if (document.seasons[0].cameras == null || document.seasons[0].cameras.Length == 0) return;

                WaterSurfaceData surface = document.seasons[0].cameras[0].surface;
                if (surface == null) return;

                WaterHeight = surface.waterHeight;
                exportedNormalAnimationSpeed = surface.normalAnimationSpeed;
                exportedSurfaceBubblePeriod = surface.surfaceBubblePeriod;
                Log.Info($"Terrain314 水面参数来自 314 导出: WaterHeight={WaterHeight:F1}, " +
                         $"CutoffDistance={surface.cutoffDistance:F0}, " +
                         $"NormalSpeed={exportedNormalAnimationSpeed:F1}");
            }
            catch (System.Exception exception)
            {
                Log.Warning($"Terrain314 water.json 解析失败，改用 Inspector 参数: {exception.Message}");
            }
        }

        /// <summary>
        /// 加载 314 导出的海床高度遮罩（2048² R32，单位是世界高度）。
        /// 原始字节是小端 float，直接交给 TextureFormat.RFloat，不做任何解码。
        /// </summary>
        private void LoadHeightMask()
        {
            if (Terrain == null) return;

            string path = System.IO.Path.Combine(Terrain.ResolvedDirectory, "water", "height-mask.r32");
            if (!System.IO.File.Exists(path))
            {
                Log.Warning($"Terrain314 缺少海床高度遮罩: {path}，浅滩泡沫将退化为纯深度计算");
                return;
            }

            byte[] bytes = System.IO.File.ReadAllBytes(path);
            const int side = 2048;
            if (bytes.Length != side * side * sizeof(float))
            {
                Log.Warning($"Terrain314 海床高度遮罩尺寸不符: {bytes.Length} 字节，应为 {side * side * 4}");
                return;
            }

            heightMask = new Texture2D(side, side, TextureFormat.RFloat, false, true)
            {
                name = "Terrain314HeightMask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            heightMask.LoadRawTextureData(bytes);
            heightMask.Apply(false, true);
        }

        /// <summary>
        /// 把 Inspector 参数同步到材质。
        /// 每帧设置而不是靠 OnValidate：材质是运行时对象，编辑器里改完参数要立刻生效。
        /// </summary>
        private void ApplyMaterialParameters()
        {
            material.SetColor("_ShallowColor", ShallowColor);
            material.SetColor("_DeepColor", DeepColor);
            material.SetColor("_ReflectionColor", ReflectionColor);
            material.SetFloat("_DepthFade", DepthFade);
            material.SetFloat("_OpacityPerMeter", OpacityPerMeter);
            material.SetFloat("_MaxOpacity", MaxOpacity);
            material.SetFloat("_FresnelPower", FresnelPower);
            material.SetFloat("_SpecularPower", SpecularPower);
            material.SetFloat("_SpecularIntensity", SpecularIntensity);
            material.SetColor("_FoamColor", FoamColor);
            material.SetFloat("_FoamDistance", FoamDistance);
            material.SetFloat("_FoamStrength", FoamStrength);
            material.SetVector("_WaveDirection", WaveDirection);
            material.SetVector("_WaveAmplitude", WaveAmplitude);
            material.SetVector("_WaveLength", WaveLength);
            material.SetVector("_WaveSpeed", WaveSpeed);
            material.SetVector("_WaveSteepness", WaveSteepness);

            // 海床遮罩：uv = 世界坐标 * (1 / 地形世界边长)，与高度场共用 2048 格
            material.SetFloat("_WaterHeight", WaterHeight);
            float worldSize = Terrain != null && Terrain.Source != null
                ? Terrain.Source.Height.WorldSize
                : 204800f;
            material.SetFloat("_HeightMaskScale", 1f / Mathf.Max(worldSize, 1f));
            if (heightMask != null) material.SetTexture("_HeightMaskTex", heightMask);

            // 314 水体贴图：体积法线滚时间轴、噪声扰动、三层气泡数组
            if (normalVolume != null) material.SetTexture("_NormalVolume", normalVolume);
            if (normalNoise != null) material.SetTexture("_NormalNoise", normalNoise);
            if (bubbleArray != null) material.SetTexture("_BubbleArray", bubbleArray);
            material.SetFloat("_NormalAnimationSpeed", exportedNormalAnimationSpeed);
            material.SetFloat("_BubblePeriod", exportedSurfaceBubblePeriod);
        }

        /// <summary>
        /// 按相机位置平铺 tile。
        ///
        /// 314 的做法是把视锥 8 角与水面平面求交得到 XZ 包围盒（WaterGeometry.h:107-136）；
        /// 这里用"相机 XZ + 半径 CutoffDistance"的方形代替，两者在俯视相机下等价，
        /// 但方形平铺的 tile 数量固定，不会因为视锥朝向变化导致每帧增删实例。
        /// </summary>
        /// <param name="camera">当前相机。</param>
        private void BuildTileMatrices(Camera camera)
        {
            nearMatrices.Clear();
            midMatrices.Clear();
            farMatrices.Clear();

            // 与地形/植被一致：用观察点而不是相机位置。
            // Scene 视图相机可能离场景几十万单位，按它的坐标平铺会把整片水面铺到地图之外。
            Vector3 cameraPosition = Terrain != null ? Terrain.ObservationPoint : camera.transform.position;
            float radius = Mathf.Max(CutoffDistance, TileSize);
            int halfCount = Mathf.CeilToInt(radius / TileSize);

            // tile 网格吸附到全局格点，相机移动时不会出现整片水面抖动
            long centerX = (long)System.Math.Round(cameraPosition.x / TileSize);
            long centerZ = (long)System.Math.Round(cameraPosition.z / TileSize);

            for (int dz = -halfCount; dz <= halfCount; dz++)
            {
                for (int dx = -halfCount; dx <= halfCount; dx++)
                {
                    float tileX = (centerX + dx) * TileSize;
                    float tileZ = (centerZ + dz) * TileSize;
                    float offsetX = tileX - cameraPosition.x;
                    float offsetZ = tileZ - cameraPosition.z;
                    float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                    if (distance > radius) continue;

                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(tileX, WaterHeight + SurfaceOffset, tileZ),
                        Quaternion.identity,
                        new Vector3(TileSize, 1f, TileSize));

                    if (distance <= NearDistance) nearMatrices.Add(matrix);
                    else if (distance <= MidDistance) midMatrices.Add(matrix);
                    else farMatrices.Add(matrix);
                }
            }
        }

        /// <summary>按 1023 个一批提交某一档 tile。</summary>
        /// <param name="mesh">该档的 tile mesh。</param>
        /// <param name="matrices">实例矩阵列表。</param>
        /// <param name="camera">绘制相机。</param>
        private void DrawTileBatch(Mesh mesh, List<Matrix4x4> matrices, Camera camera)
        {
            if (mesh == null || matrices.Count == 0) return;
            for (int start = 0; start < matrices.Count; start += MaxInstancesPerBatch)
            {
                int count = Mathf.Min(MaxInstancesPerBatch, matrices.Count - start);
                matrices.CopyTo(start, batchBuffer, 0, count);
                Graphics.DrawMeshInstanced(mesh, 0, material, batchBuffer, count, null,
                    ShadowCastingMode.Off, false, gameObject.layer, camera);
            }
        }

        /// <summary>
        /// 远海四边形：314 用 4 顶点 far quad 覆盖 cutoff 之外的海面
        /// （WaterGeometry.h:139-140），并把波浪系数清零。
        /// 这里用同一份材质 + MaterialPropertyBlock 把 _WaveScale 置 0，避免额外材质。
        /// </summary>
        /// <param name="camera">绘制相机。</param>
        private void DrawFarOceanQuad(Camera camera)
        {
            if (!DrawFarOcean || farOceanMesh == null || farOceanProperties == null) return;

            // 同上：远海四边形跟随观察点，而不是可能远在场景之外的相机坐标
            Vector3 center = Terrain != null ? Terrain.ObservationPoint : camera.transform.position;
            center.y = WaterHeight + SurfaceOffset;
            // 足够大以覆盖到地平线：地形边长 204800，这里再留一倍余量
            float size = Mathf.Max(CutoffDistance * 8f, 400000f);
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size, 1f, size));

            farOceanProperties.SetFloat("_WaveScale", 0f);
            Graphics.DrawMesh(farOceanMesh, matrix, material, gameObject.layer, camera, 0,
                farOceanProperties, ShadowCastingMode.Off, false);
        }

        /// <summary>
        /// 生成一块 tile mesh：局部坐标固定为 -0.5..0.5 的 XZ 方格，
        /// 世界尺寸完全由实例矩阵的缩放决定，因此三档 LOD 可以共用同一套绘制代码。
        /// </summary>
        /// <param name="cells">格数（顶点数 = cells + 1 的平方）。</param>
        /// <param name="meshName">mesh 名称，便于 Profiler 识别。</param>
        /// <returns>新建的 mesh。</returns>
        private static Mesh BuildTileMesh(int cells, string meshName)
        {
            cells = Mathf.Max(cells, 1);
            int verticesPerSide = cells + 1;
            var vertices = new Vector3[verticesPerSide * verticesPerSide];
            var triangles = new int[cells * cells * 6];

            for (int row = 0; row < verticesPerSide; row++)
            {
                for (int column = 0; column < verticesPerSide; column++)
                {
                    vertices[row * verticesPerSide + column] = new Vector3(
                        (float)column / cells - 0.5f,
                        0f,
                        (float)row / cells - 0.5f);
                }
            }

            int write = 0;
            for (int row = 0; row < cells; row++)
            {
                for (int column = 0; column < cells; column++)
                {
                    int v00 = row * verticesPerSide + column;
                    int v10 = v00 + 1;
                    int v01 = v00 + verticesPerSide;
                    int v11 = v01 + 1;
                    // 与地形 patch 相同的绕序：从上方看为顺时针，Cull Back 下正面朝上
                    triangles[write++] = v00; triangles[write++] = v01; triangles[write++] = v11;
                    triangles[write++] = v00; triangles[write++] = v11; triangles[write++] = v10;
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            // 顶点在 VS 里被波位移，包围盒按波幅给足，避免被视锥误剔除
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1.2f, 400f, 1.2f));
            return mesh;
        }

        /// <summary>释放运行时创建的 mesh 与材质。</summary>
        private void ReleaseResources()
        {
            bool playing = Application.isPlaying;
            DestroyMesh(ref nearMesh, playing);
            DestroyMesh(ref midMesh, playing);
            DestroyMesh(ref farMesh, playing);
            DestroyMesh(ref farOceanMesh, playing);
            if (material != null && OverrideMaterial == null)
            {
                if (playing) Destroy(material); else DestroyImmediate(material);
            }
            material = null;
            farOceanProperties = null;
            if (heightMask != null)
            {
                if (playing) Destroy(heightMask); else DestroyImmediate(heightMask);
                heightMask = null;
            }
            if (normalVolume != null)
            {
                if (playing) Destroy(normalVolume); else DestroyImmediate(normalVolume);
                normalVolume = null;
            }
            if (normalNoise != null)
            {
                if (playing) Destroy(normalNoise); else DestroyImmediate(normalNoise);
                normalNoise = null;
            }
            if (bubbleArray != null)
            {
                if (playing) Destroy(bubbleArray); else DestroyImmediate(bubbleArray);
                bubbleArray = null;
            }
            surfaceTexturesLoaded = false;
            // 域重载后重新读一次导出参数，避免 Inspector 停在旧值
            parametersLoaded = false;
        }

        /// <summary>销毁一个 mesh 并把引用置空。</summary>
        /// <param name="mesh">目标 mesh 引用。</param>
        /// <param name="playing">是否处于运行模式（决定用 Destroy 还是 DestroyImmediate）。</param>
        private static void DestroyMesh(ref Mesh mesh, bool playing)
        {
            if (mesh == null) return;
            if (playing) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
            mesh = null;
        }

        /// <summary>
        /// water.json 的最小反序列化模型。
        /// JsonUtility 只认 public 字段且不处理字典，因此这里按导出文件的实际结构逐层声明；
        /// 波浪/光学/气泡的完整参数暂时不读，需要时再补字段即可，不必改导出器。
        /// </summary>
        [System.Serializable]
        private sealed class WaterDocument
        {
            public int version;
            public WaterSeasonData[] seasons;
        }

        /// <summary>一个季节的水面数据。</summary>
        [System.Serializable]
        private sealed class WaterSeasonData
        {
            public int month;
            public int seasonIndex;
            public WaterCameraData[] cameras;
        }

        /// <summary>一个相机包的水面数据。</summary>
        [System.Serializable]
        private sealed class WaterCameraData
        {
            public uint objectId;
            public WaterSurfaceData surface;
        }

        /// <summary>OceanSurfaceSettings 的导出子集。</summary>
        [System.Serializable]
        private sealed class WaterSurfaceData
        {
            public float waterHeight;
            public float cutoffDistance;
            public float textureWaveDisplacement;
            public float normalAnimationSpeed;
            public uint bubbleMaskMode;
            public float surfaceBubblePeriod;
            public float innerBubblePeriod;
            public float[] textureWaveDirection;
        }

        /// <summary>
        /// water-textures.json 的根对象。
        ///
        /// 导出端把四张贴图放在 "textures" 子对象下，这里必须保留同名包装字段：
        /// JsonUtility 不认路径也不报缺字段，平铺写会静默解析出全 null，
        /// 表现为"三张贴图都不加载且没有任何警告"。
        /// </summary>
        [System.Serializable]
        private sealed class WaterTextureDocument
        {
            public int version;
            public WaterTextureSet textures;
        }

        /// <summary>water-textures.json 的 textures 子对象：四张贴图的尺寸与文件名。</summary>
        [System.Serializable]
        private sealed class WaterTextureSet
        {
            public WaterTextureEntry normal3d;
            public WaterTextureEntry normalNoise;
            public WaterTextureEntry bubble;
            public WaterTextureEntry heightMask;
        }

        /// <summary>单张贴图的尺寸描述；depth / layers 只有对应维度才非 0。</summary>
        [System.Serializable]
        private sealed class WaterTextureEntry
        {
            public int width;
            public int height;
            public int depth;
            public int layers;
            public string format;
            public string file;
        }
    }
}
