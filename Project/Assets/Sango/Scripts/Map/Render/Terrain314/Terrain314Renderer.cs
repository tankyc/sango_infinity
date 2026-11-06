using System.Collections.Generic;
using System.IO;
// 见 Terrain314Types.cs：屏蔽 Sango.Path / Sango.Directory / Sango.File 的遮蔽
using UnityEngine;
// RenderPipelineManager / ShadowCastingMode：绘制必须挂在相机渲染回调上，不能靠 Update
using UnityEngine.Rendering;

namespace Sango.Render.Terrain314
{
    using Path = System.IO.Path;
    using Directory = System.IO.Directory;
    using File = System.IO.File;

    /// <summary>
    /// 314 式地形渲染器（独立体系，不影响现有地图）。
    ///
    /// 用法：
    ///   1. 新建一个空 GameObject，挂上本组件；
    ///   2. 把 ResourceDirectory 指向 314 导出的资源目录
    ///      （默认 StreamingAssets/Terrain314，内含 height.r32 / height.json /
    ///        spatial.json / patches.json）；
    ///   3. 运行即可看到地形。
    ///
    /// 设计约束：
    ///   - 不注册进 MapRender，不读写 MapData / MapTerrain / MapLayer；
    ///   - 不与现有地图共用相机与 DontDestroy 生命周期；
    ///   - 所有 mesh 按需构建并缓存，退出时释放。
    ///
    /// 对齐依据：314 的 TerrainPass + TerrainLodDrawPlanner
    ///   - 每个被选中的节点绘制一个 65x65 顶点的 patch；
    ///   - LOD 通过"选择哪个层级的节点"实现，而不是缩减顶点数；
    ///   - 索引对角按 (row + col) &amp; 1 交替，与 314 TerrainPatchInputMath.TriangleIndices 一致。
    /// </summary>
    // ExecuteAlways：地形是用 Graphics.DrawMesh 每帧提交的，不走 MeshRenderer，
    // 编辑器不跑 Update 就什么都画不出来，Scene 视图只能看到空白。
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Terrain314Renderer : MonoBehaviour
    {
        /// <summary>
        /// 非运行模式下是否也在 Scene 视图预览地形。
        /// 默认关闭：勾选后会立即加载 16.8MB 高度场与 3 个 19 层贴图数组（约 250MB 显存），
        /// 只想看 Game 视图时没必要付这个代价。
        /// </summary>
        [Header("编辑器预览")]
        [Tooltip("勾选后无需进入 Play 即可在 Scene 视图看到地形（会占用较多显存）")]
        public bool PreviewInEditMode;

        /// <summary>资源目录（相对 StreamingAssets 或绝对路径）。</summary>
        [Header("资源")]
        [Tooltip("314 导出资源目录，留空则使用 StreamingAssets/Terrain314")]
        public string ResourceDirectory = "Terrain314";

        /// <summary>用于 LOD 与视锥的相机，留空则取 Camera.main。</summary>
        [Header("相机与 LOD")]
        public Camera TargetCamera;

        [Tooltip("分裂距离系数，越大越粗糙")]
        [Range(0.25f, 8f)]
        public float DistanceScale = 1.0f;

        [Tooltip("视锥剔除时 AABB 的 Y 方向扩展量（世界单位）")]
        public float CullExtentY = 1000f;

        [Tooltip("叶层前多少个 patch 归入近处分组")]
        public int NearGroupSize = 4;

        /// <summary>材质，留空则自动创建。</summary>
        [Header("材质")]
        [Tooltip("优先级最高：手动指定的材质")]
        public Material OverrideMaterial;

        [Tooltip("314 材质组：指定后自动组装 Texture2DArray 与高度纹理并创建 Sango/Terrain314 材质")]
        public Terrain314MaterialSet MaterialSet;

        /// <summary>地形整体颜色。</summary>
        public Color Tint = new(0.55f, 0.62f, 0.45f, 1f);

        private Terrain314Source source;
        private Terrain314Lod lod;
        /// <summary>地格级数据（地表类型 / 高度 / 植被密度），由 Load 时一并建立。</summary>
        private Terrain314CellData cellData;
        private readonly Dictionary<int, Mesh> meshCache = new();
        private Material material;
        /// <summary>非叶层专用材质（_CoarseMode = 1），与 near 材质共用贴图。</summary>
        private Material coarseMaterial;
        private Texture2D heightTexture;
        private Texture2DArray rootArray;
        private Texture2DArray normalArray;
        private Texture2DArray reflectanceArray;
        private bool failed;
        private int lastVisibleCount;
        private int lastBatchCount;

        private void OnEnable()
        {
            // 只在"相机真正要渲染"时提交绘制。
            // 之前用 EditorApplication.update 兜底，每帧都会提交一次 Graphics.DrawMesh，
            // 但 Scene 视图不重绘时这些渲染节点只会进不会出，累积到 256MB 就会触发
            // "RenderNodeQueue Page Allocator out of memory" 致命错误（编辑器直接崩）。
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void Start()
        {
            // ExecuteAlways 下 Start 在编辑器也会触发；这里只让运行时立刻加载，
            // 编辑器预览由 Update 按 PreviewInEditMode 懒加载，避免一打开场景就吃掉显存
            if (Application.isPlaying) Load();
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshCache.Values)
            {
                if (mesh != null) DestroyImmediate(mesh);
            }
            meshCache.Clear();

            // 运行时创建的贴图与材质必须显式释放，否则切场景后会残留显存
            if (heightTexture != null) DestroyImmediate(heightTexture);
            if (rootArray != null) DestroyImmediate(rootArray);
            if (normalArray != null) DestroyImmediate(normalArray);
            if (reflectanceArray != null) DestroyImmediate(reflectanceArray);
            if (material != null && OverrideMaterial == null) DestroyImmediate(material);
            heightTexture = null;
            rootArray = null;
            normalArray = null;
            reflectanceArray = null;
            material = null;
        }

        /// <summary>加载资源并建立 LOD 规划器；失败时标记并停止后续更新。</summary>
        public bool Load()
        {
            if (source != null) return true;
            try
            {
                string directory = ResolveDirectory();
                Sango.Log.Info($"Terrain314 加载资源: {directory}", Log.LogType.World);
                source = Terrain314Source.Load(directory);
                // 地格级数据：地表类型（cells/palette.bin）+ 植被密度/biome + 每格中心高度。
                // 缺 palette.bin 只让地表类型退化，不影响地形与植被本体。
                cellData = Terrain314CellData.Load(directory, source.Height);
                lod = new Terrain314Lod(source)
                {
                    DistanceScale = DistanceScale,
                    CullExtentY = CullExtentY,
                    NearGroupSize = NearGroupSize
                };
                Sango.Log.Info(
                    $"Terrain314 就绪（世界坐标 +Z 东 / +X 南，高度场与空间树已在加载时互换 X/Z 分量）: " +
                    $"格 {source.Height.CellWidth}x{source.Height.CellHeight}, " +
                    $"间距 {source.Height.SampleSpacing}, 高度 [{source.Height.MinimumHeight:F1}, " +
                    $"{source.Height.MaximumHeight:F1}], 四叉树 {source.LevelCount} 层 {source.Nodes.Length} 节点",
                    Log.LogType.World);
                return true;
            }
            catch (System.Exception exception)
            {
                failed = true;
                Sango.Log.Error($"Terrain314 加载失败: {exception.Message}");
                return false;
            }
        }

        private string ResolveDirectory()
        {
            if (string.IsNullOrEmpty(ResourceDirectory)) ResourceDirectory = "Terrain314";
            if (System.IO.Path.IsPathRooted(ResourceDirectory)) return ResourceDirectory;
            return System.IO.Path.Combine(Application.streamingAssetsPath, ResourceDirectory);
        }

        /// <summary>
        /// Update 只负责加载与参数同步，不提交任何绘制。
        /// 绘制放到 <see cref="OnBeginCameraRendering"/>，保证"提交次数 == 渲染次数"。
        /// </summary>
        private void Update()
        {
            // 编辑器里没勾预览就直接退出，不加载
            if (!Application.isPlaying && !PreviewInEditMode) return;
            if (failed) return;
            if (source == null && !Load()) return;

            if (lod.DistanceScale != DistanceScale) lod.DistanceScale = DistanceScale;
            if (lod.CullExtentY != CullExtentY) lod.CullExtentY = CullExtentY;
            if (lod.NearGroupSize != NearGroupSize) lod.NearGroupSize = NearGroupSize;
        }

        /// <summary>
        /// 相机渲染前提交地形 patch。
        ///
        /// 用 beginCameraRendering 而不是 EditorApplication.update：
        ///   1) 提交与渲染严格一一对应，Scene 视图不重绘时不会堆积渲染节点；
        ///   2) camera 参数把绘制限定到当前相机，多相机场景下不会重复提交。
        /// </summary>
        /// <param name="context">当前 SRP 渲染上下文（未使用）。</param>
        /// <param name="camera">正在渲染的相机。</param>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (failed) return;
            if (!Application.isPlaying && !PreviewInEditMode) return;
            if (source == null || lod == null) return;
            // 只处理 LOD 规划用的那台相机；反射探针、预览相机等不参与
            if (camera != ResolveCamera()) return;

            Terrain314LodPlan plan;
            Material drawMaterial;
            // 材质构建若抛异常，material 会一直是 null 并每次渲染都重试刷爆 Console，
            // 这里只报一次并停止后续绘制。
            try
            {
                plan = lod.Build(camera);
                drawMaterial = EnsureMaterial();
            }
            catch (System.Exception exception)
            {
                failed = true;
                Log.Error($"Terrain314 绘制准备失败，已停止重试: {exception.Message}");
                return;
            }

            for (int i = 0; i < plan.Patches.Count; i++)
            {
                Terrain314SelectedPatch patch = plan.Patches[i];
                if (patch.Culled) continue;

                Mesh mesh = GetOrBuildMesh(patch.NodeIndex);
                if (mesh == null) continue;
                // 叶层用完整材质，非叶层用 coarse（对齐 314 的 near / coarse 两套程序）
                Material patchMaterial = patch.Level == 0 || coarseMaterial == null
                    ? drawMaterial
                    : coarseMaterial;
                // 地形不投影：阴影交给模型/植被，这里关掉可以少一轮 shadow pass 提交
                Graphics.DrawMesh(mesh, Matrix4x4.identity, patchMaterial, gameObject.layer,
                    camera, 0, null, ShadowCastingMode.Off, false);
            }

            lastVisibleCount = plan.VisibleCount;
            lastBatchCount = plan.Batches.Count;
        }

        /// <summary>
        /// 丢弃已构建的材质与贴图数组，下次 EnsureMaterial 会按当前数据重建。
        /// 编辑器里重绑贴图或改调色板后必须调用，否则一直用着旧材质。
        /// </summary>
        public void InvalidateMaterial()
        {
            bool playing = Application.isPlaying;
            if (material != null && OverrideMaterial == null)
            {
                if (playing) Destroy(material); else DestroyImmediate(material);
            }
            if (coarseMaterial != null)
            {
                if (playing) Destroy(coarseMaterial); else DestroyImmediate(coarseMaterial);
            }
            if (heightTexture != null) { if (playing) Destroy(heightTexture); else DestroyImmediate(heightTexture); }
            if (rootArray != null) { if (playing) Destroy(rootArray); else DestroyImmediate(rootArray); }
            if (normalArray != null) { if (playing) Destroy(normalArray); else DestroyImmediate(normalArray); }
            if (reflectanceArray != null) { if (playing) Destroy(reflectanceArray); else DestroyImmediate(reflectanceArray); }
            material = null;
            coarseMaterial = null;
            heightTexture = null;
            rootArray = null;
            normalArray = null;
            reflectanceArray = null;
        }

        /// <summary>
        /// 取用于 LOD 与视锥剔除的相机。
        ///
        /// 编辑器非运行模式下优先用 Scene 视图相机：LOD 是按相机位置挑选节点的，
        /// 若在 Scene 视图里飞到别处却仍按 Game 相机算，会看到"有些区块根本没生成"。
        /// </summary>
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

        private Material EnsureMaterial()
        {
            if (OverrideMaterial != null) return OverrideMaterial;
            if (material != null) return material;

            if (MaterialSet != null)
            {
                if (heightTexture == null)
                {
                    // 高度场在资源目录里（StreamingAssets/Terrain314/height.r32），
                    // 传 StreamingAssets 根目录会 File.Exists 失败、_HeightTex 永远是空
                    heightTexture = MaterialSet.BuildHeightTexture(ResolveDirectory(),
                        source.Height.SampleWidth, source.Height.SampleHeight);
                }
                if (rootArray == null)
                {
                    rootArray = MaterialSet.BuildRootArray();
                }
                if (normalArray == null)
                {
                    normalArray = MaterialSet.BuildNormalArray();
                }
                if (reflectanceArray == null)
                {
                    reflectanceArray = MaterialSet.BuildReflectanceArray();
                }
                material = MaterialSet.CreateMaterial(heightTexture, rootArray,
                    normalArray, reflectanceArray);
                if (material != null)
                {
                    ConfigureMaterial(material, 0f);

                    // 非叶层走 coarse 程序：原版的 coarse-vs/coarse-ps 是两套独立的
                    // 预编译着色器（San14 TerrainPrograms.h:50-58），按 batch 标志切换，
                    // 不是同一程序内分支。这里共用贴图，只靠 _CoarseMode 区分路径。
                    coarseMaterial = MaterialSet.CreateMaterial(heightTexture, rootArray,
                        normalArray, reflectanceArray);
                    if (coarseMaterial != null)
                    {
                        coarseMaterial.name = "Terrain314RuntimeCoarse";
                        ConfigureMaterial(coarseMaterial, 1f);
                    }

                    Log.Info($"Terrain314 材质就绪: 高度纹理 {(heightTexture != null ? "有" : "无")}, " +
                             $"albedo {(rootArray != null ? rootArray.depth + " 层" : "无")}, " +
                             $"normal {(normalArray != null ? normalArray.depth + " 层" : "无")}, " +
                             $"reflectance {(reflectanceArray != null ? reflectanceArray.depth + " 层" : "无")}, " +
                             $"coarse {(coarseMaterial != null ? "有" : "无")}",
                        Sango.Log.LogType.World);
                    return material;
                }
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader)
            {
                name = "Terrain314RuntimeMaterial",
                enableInstancing = false
            };
            material.SetColor("_BaseColor", Tint);
            material.SetColor("_Color", Tint);
            material.SetFloat("_Smoothness", 0.1f);
            return material;
        }

        /// <summary>
        /// 把地形级参数写到材质上。near 与 coarse 共用同一份贴图，只有 _CoarseMode 不同。
        /// </summary>
        /// <param name="target">目标材质。</param>
        /// <param name="coarseMode">0 = 叶层完整路径，1 = 非叶层简化路径。</param>
        private void ConfigureMaterial(Material target, float coarseMode)
        {
            // 314 的 control 图采样点数是 CellWidth + 1（2049），与高度场采样一致
            target.SetFloat("_TerrainSize", source.Height.WorldSize);
            target.SetFloat("_ControlCells", source.Height.CellWidth);
            target.SetFloat("_HeightRange", source.Height.Range);
            // 着色器的染色属性名是 _Tint（不是 URP Lit 的 _BaseColor）
            target.SetColor("_Tint", Tint);
            target.SetFloat("_CoarseMode", coarseMode);
        }

        /// <summary>
        /// 按世界坐标取样地形高度。
        ///
        /// 世界坐标约定为 +Z 东 / +X 南（数据加载时已互换 X/Z 分量），
        /// 这里不需要再做任何分量交换；顶点高度与法线差分都经过这一个入口，
        /// 保证地形起伏与地表贴图严格对齐。
        /// </summary>
        /// <param name="worldX">世界坐标 X（南向）。</param>
        /// <param name="worldZ">世界坐标 Z（东向）。</param>
        /// <returns>该点的地形高度。</returns>
        private float SampleHeight(float worldX, float worldZ)
        {
            return source.Height.SampleWorld(worldX, worldZ);
        }

        /// <summary>
        /// 构建一个节点的 patch mesh（65x65 顶点）。
        ///
        /// 顶点在 XZ 平面按 node 尺寸等分，Y 直接取高度场世界采样值；
        /// 索引对角按 (row + col) &amp; 1 交替，与 314 的走向一致。
        /// </summary>
        private Mesh GetOrBuildMesh(int nodeIndex)
        {
            if (meshCache.TryGetValue(nodeIndex, out Mesh cached) && cached != null) return cached;

            Terrain314SpatialNode node = source.Nodes[nodeIndex];
            Terrain314PatchInput patch = source.BuildPatchInput(node);
            int n = (int)patch.GridSize;
            int vertexCount = n * n;

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            float step = node.Size / (n - 1);

            for (int row = 0; row < n; row++)
            {
                for (int column = 0; column < n; column++)
                {
                    int index = row * n + column;
                    float worldX = patch.Origin.x + column * step;
                    float worldZ = patch.Origin.z + row * step;

                    // 只改"高度场取样"坐标，顶点朝向必须保持原样：
                    // 连顶点一起翻转等于改用左手坐标系（det &lt; 0），会反转三角形绕序，
                    // 背面剔除后地形会碎成条带。
                    vertices[index] = new Vector3(worldX,
                        SampleHeight(worldX, worldZ), worldZ);
                    // 网格 UV 只用于调试取点，着色器不采样它：地表 uv 全部由
                    // Terrain314.shader 用世界坐标（+Z 东 / +X 南）重算，
                    // 避免网格 uv 与数据行列约定再分叉一次。
                    uvs[index] = new Vector2((float)column / (n - 1), (float)row / (n - 1));
                }
            }

            // 法线：用相邻采样差分，避免依赖 Mesh.RecalculateNormals 的重算开销
            float sampleStep = Mathf.Max(1f, step);
            for (int row = 0; row < n; row++)
            {
                for (int column = 0; column < n; column++)
                {
                    int index = row * n + column;
                    Vector3 position = vertices[index];
                    // 差分取样与顶点走同一个入口，保证坡向与高度场一致
                    float left = SampleHeight(position.x - sampleStep, position.z);
                    float right = SampleHeight(position.x + sampleStep, position.z);
                    float down = SampleHeight(position.x, position.z - sampleStep);
                    float up = SampleHeight(position.x, position.z + sampleStep);
                    normals[index] = new Vector3(left - right, sampleStep * 2f, down - up).normalized;
                }
            }

            int quadCount = (n - 1) * (n - 1);
            var triangles = new int[quadCount * 6];
            int write = 0;
            for (int row = 0; row < n - 1; row++)
            {
                for (int column = 0; column < n - 1; column++)
                {
                    int v00 = row * n + column;
                    int v10 = v00 + 1;
                    int v01 = v00 + n;
                    int v11 = v01 + 1;

                    // 对角交替，保持与 314 TriangleIndices 相同的走向
                    if (((row + column) & 1) == 0)
                    {
                        triangles[write++] = v00; triangles[write++] = v01; triangles[write++] = v11;
                        triangles[write++] = v00; triangles[write++] = v11; triangles[write++] = v10;
                    }
                    else
                    {
                        triangles[write++] = v00; triangles[write++] = v01; triangles[write++] = v10;
                        triangles[write++] = v10; triangles[write++] = v01; triangles[write++] = v11;
                    }
                }
            }

            var mesh = new Mesh
            {
                name = $"Terrain314Patch_{nodeIndex}",
                indexFormat = vertexCount > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            meshCache[nodeIndex] = mesh;
            return mesh;
        }

        /// <summary>当前选中且未被剔除的 patch 数（供外部调试显示）。</summary>
        /// <summary>已加载的数据源；未加载（或加载失败）时为 null。</summary>
        public Terrain314Source Source => source;

        /// <summary>解析后的资源绝对目录，供水面/植被等 314 子渲染器共用同一份数据根目录。</summary>
        public string ResolvedDirectory => ResolveDirectory();

        /// <summary>
        /// 取当前用于 LOD 与剔除的相机。
        /// 水面等其他 314 子渲染器要跟随同一台相机，避免两边算出不同的可见域。
        /// </summary>
        public Camera CurrentCamera => ResolveCamera();

        /// <summary>地格级数据（地表类型 / 高度 / 植被密度）；未加载完成时为 null。</summary>
        public Terrain314CellData CellData => cellData;

        /// <summary>
        /// 把 Control 组的 initial control 烘焙成每格一份的地表类型表（cells/palette.bin）。
        ///
        /// 运行时纹理不可读，只能在编辑器里从源 PNG 重新解码后按格取中心采样。
        /// 产物放在 Terrain314/cells/palette.bin，与 vegetation/ 下的 density.bin 同网格。
        /// </summary>
        [ContextMenu("烘焙地格地表类型")]
        public void BakeCellPalette()
        {
#if UNITY_EDITOR
            string textureDirectory = MaterialSet != null && !string.IsNullOrEmpty(MaterialSet.TextureDirectory)
                ? MaterialSet.TextureDirectory
                : "Assets/Sango/Terrain314/Textures";
            if (!System.IO.Path.IsPathRooted(textureDirectory))
                textureDirectory = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", textureDirectory));

            string controlPath = null;
            if (System.IO.Directory.Exists(textureDirectory))
            {
                foreach (string file in System.IO.Directory.GetFiles(textureDirectory, "control_initial*.png"))
                {
                    controlPath = file;
                    break;
                }
            }

            string outputPath = System.IO.Path.Combine(ResolveDirectory(), "cells", "palette.bin");
            int cellsX = cellData != null ? cellData.CellsX : Terrain314CellData.DefaultCellsX;
            int cellsZ = cellData != null ? cellData.CellsZ : Terrain314CellData.DefaultCellsZ;
            bool ok = Terrain314CellData.BakePaletteIndex(controlPath, outputPath, cellsX, cellsZ,
                out string message);
            if (ok)
            {
                Sango.Log.Info(message, Log.LogType.World);
                // 产物变了，重新加载一次地格数据，Inspector 里立刻能看到新类型
                if (source != null) cellData = Terrain314CellData.Load(ResolveDirectory(), source.Height);
            }
            else
            {
                Sango.Log.Error(message);
            }
#endif
        }

        /// <summary>
        /// 当前观察点，供植被/水面等子渲染器做分块与距离判定。
        ///
        /// 不能直接用 <see cref="CurrentCamera"/> 的 position：Scene 视图相机在框住整张图时
        /// 会被推到离场景几十万单位外，拿它算块索引会全部越界，表现就是"子渲染器什么都不画"。
        /// 这里返回的是"视线与地形中值平面的交点"，即真正在看的那块地。
        /// </summary>
        public Vector3 ObservationPoint
        {
            get
            {
                Camera camera = ResolveCamera();
                if (camera == null) return Vector3.zero;
                return lod != null ? lod.ResolveEye(camera) : camera.transform.position;
            }
        }

        /// <summary>
        /// 把 Scene 视图相机框到地形整体范围。
        ///
        /// 本体系的网格顶点就是世界坐标、绘制用 identity 矩阵，因此挂载对象通常停在原点，
        /// 而地图实际占 [0, RootSize]²、高度 0~7000。在 Scene 视图里按 F 聚焦本对象会
        /// 飞到地图角落外侧（z &lt; 0），此时 LOD 只选中极少 patch，看起来像"地形不渲染"。
        /// 用这个菜单可以把视野直接框回地形。
        /// </summary>
        [ContextMenu("聚焦地形（Scene 视图）")]
        private void FocusTerrainInSceneView()
        {
#if UNITY_EDITOR
            UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
            if (view == null) return;
            if (source == null) Load();
            if (source == null) return;

            float root = source.RootSize;
            float top = source.Height != null ? source.Height.MaximumHeight : 0f;
            Vector3 center = new(root * 0.5f, top * 0.5f, root * 0.5f);
            view.LookAt(center, view.rotation, root);
            view.Repaint();
#endif
        }

        /// <summary>
        /// 取某个六边形地格的地面数据（高度 / 地表类型 / 植被密度）。
        ///
        /// 六边形网格与正方地格数据不是同一套粒度，这里用格中心的世界坐标去查询
        /// <see cref="Terrain314CellData"/>，得到离该六边形中心最近的正方格取值。
        /// </summary>
        public bool TryGetHexCell(int hexId, out Terrain314Cell cell)
        {
            cell = default;
            if (!Terrain314HexGrid.IsValid(hexId)) return false;
            if (cellData == null) return false;

            Vector3 center = Terrain314HexGrid.Project(hexId);
            cell = cellData.GetCellAtWorld(center.x, center.z);
            return true;
        }

#if UNITY_EDITOR
        /// <summary>是否在 Scene 视图画出 314 六边形地格（选中对象时生效）。</summary>
        [Tooltip("在 Scene 视图画出 314 的六边形地格，便于核对与地形/植被的对齐")]
        public bool ShowHexGrid = false;

        /// <summary>六边形地格的绘制半径（列/行数），过大会拖慢编辑器。</summary>
        [Tooltip("六边形地格绘制半径（列/行数），建议 4~12")]
        public int HexGridDrawRadius = 6;

        /// <summary>
        /// 选中时画地形真实覆盖范围与（可选的）六边形地格线框。
        /// 对象本身停在原点，没有这个线框很难在 Scene 视图里判断"相机到底在不在地形上方"。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (source == null) return;
            float root = source.RootSize;
            float top = source.Height != null ? source.Height.MaximumHeight : 0f;
            Gizmos.color = new Color(0.35f, 0.9f, 0.55f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(root * 0.5f, top * 0.5f, root * 0.5f),
                new Vector3(root, Mathf.Max(top, 1f), root));

            if (ShowHexGrid) DrawHexGridGizmos();
        }

        /// <summary>
        /// 画出观察点周围的 314 六边形地格。
        ///
        /// 每个格的高度取格中心的采样高度后整格抬平（flat-top 六边形的 6 个角点共用同一个 y），
        /// 这样线框不会因为逐角采样而锯齿抖动，便于直接和地形网格比对位置。
        /// </summary>
        private void DrawHexGridGizmos()
        {
            if (source == null || source.Height == null) return;

            Vector3 eye = ObservationPoint;
            if (!Terrain314HexGrid.TryPick(eye.x, eye.z, out int centerHex)) return;

            int centerColumn = Terrain314HexGrid.GetColumn(centerHex);
            int centerRow = Terrain314HexGrid.GetRow(centerHex);
            int radius = Mathf.Clamp(HexGridDrawRadius, 0, 16);
            var corners = new Vector3[6];
            int drawn = 0;

            Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.55f);
            for (int row = centerRow - radius; row <= centerRow + radius && drawn < 1500; row++)
            {
                if (row < 0 || row >= Terrain314HexGrid.Height) continue;
                for (int column = centerColumn - radius;
                     column <= centerColumn + radius && drawn < 1500; column++)
                {
                    if (column < 0 || column >= Terrain314HexGrid.Width) continue;

                    Vector3 center = Terrain314HexGrid.Project(Terrain314HexGrid.GetHexId(column, row));
                    center.y = source.Height.SampleWorld(center.x, center.z) + 2f;
                    Terrain314HexGrid.GetCorners(center, corners);
                    for (int i = 0; i < 6; i++)
                        Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
                    drawn++;
                }
            }
        }
#endif

        public int VisiblePatchCount => lastVisibleCount;

        /// <summary>当前批次数量。</summary>
        public int BatchCount => lastBatchCount;
    }
}
