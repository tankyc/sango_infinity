using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango.Render.Terrain314
{
    /// <summary>
    /// 314 程序化植被（GroundDetail）渲染器。
    ///
    /// 【数据来源】StreamingAssets/Terrain314/vegetation/ 由 San14TerrainExport 导出：
    ///   vegetation.json  声明（field/cell/block 尺寸、biome→component→element 三层目录）
    ///   density.bin      每格 float32 密度
    ///   biome.bin        每格 uint8 biome 索引（255 = 无植被）
    ///
    /// 【算法对齐】逐行照抄 314 的 MapProceduralPlacementProducer.GenerateBlock
    ///   （D:/San14/src/San14.Data/Rendering/Terrain/MapProceduralPlacementProducer.cs:94-182）：
    ///     - 每块每 biome 一条 LCG 流，种子 = biome.RandomSeed * 0x35C + 0x1534FF
    ///       + blockX * 0x47 + ((blockZ * 0x6A836) >> 3)，再预推进 (blockZ*3 + blockX*2) % 13 步
    ///     - 逐格消耗一次随机数（无论是否命中该 biome），保证跨 biome 的随机流不错位
    ///     - 每格实例数 = density * cellSize² * Parameters[0] / 10000，小数部分按随机数进位
    ///     - 候选按 Fisher-Yates 打乱后再落位，避免"先到先得"的规律性
    ///     - 落位随机流 = cellX * 123 + cellZ * 456 + placementSalt + ordinal * 789，
    ///       再按 (cellX * 1.2 + cellZ + ordinal * 12) % 13 预推进
    ///     - 缩放 = lerp(Parameters[1], Parameters[2], random)，朝向 = 2π * random
    ///
    /// 【坐标交换】植被数据与高度场同样来自 314 的"列 = 东、行 = 南"排列，
    ///   而项目世界是 +Z 东 / +X 南，因此最终世界坐标要把 314 的 X/Z 结果互换，
    ///   随机数的消耗顺序保持不变（否则整张图的落位会全变）。
    ///
    /// 【与 314 的差异】
    ///   314 的 31 个模板模型（DisplaySet）与风场贴图未导出，这里用工程的
    ///   mesh_tree_*.obj 按 element 轮转映射；没有模型时退化为程序化交叉草片。
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class Terrain314VegetationRenderer : MonoBehaviour
    {
        /// <summary>单次 DrawMeshInstanced 的实例上限。</summary>
        private const int MaxInstancesPerBatch = 1023;

        /// <summary>工程现成的树木模型目录（编辑器下自动加载）。</summary>
        private const string TreeMeshFolder = "Assets/Mods/Content/Assets/Model/Mesh/tree";

        /// <summary>地形渲染器，留空则自动取本物体上的。</summary>
        [Header("数据源")]
        public Terrain314Renderer Terrain;

        /// <summary>植被模型池；留空时编辑器下自动加载 mesh_tree_1..10。</summary>
        [Header("模型")]
        public Mesh[] Meshes;

        /// <summary>植被可见距离。314 的 ViewDistance = 20000。</summary>
        [Header("范围")]
        public float ViewDistance = 20000f;

        /// <summary>每帧最多烘焙多少个新块，避免一次性生成卡顿。</summary>
        public int MaxBlocksPerFrame = 4;

        /// <summary>风速倍率。</summary>
        [Header("外观")]
        public float WindStrength = 22f;

        /// <summary>
        /// 每个模型对应的地表贴图，索引与 <see cref="Meshes"/> 一致。
        ///
        /// 314 的植被模板贴图导出后按同样顺序填这里；留空时 shader 的 _BaseMap 走默认 white，
        /// 采样恒为 1，渲染结果与纯色版本完全一致，不会因为加了贴图通道而变样。
        /// </summary>
        [Tooltip("按模型顺序的地表贴图（314 植被模板导出后填入）；留空则使用纯色")]
        public Texture2D[] BaseMaps;

        /// <summary>
        /// 替代模型的尺寸基准（世界单位）。
        ///
        /// 314 的 Parameters[1]/[2] 是在"原版模板原始尺寸"上的倍率，本项目换成工程模型后
        /// 必须先统一到同一基准高度，倍率才有意义。默认 200 与一格地格宽度同量级，
        /// 调大整体变大、调小整体变小。
        /// </summary>
        [Tooltip("模型尺寸基准：先把每个替代模型缩到该高度，再乘 314 的 Parameters 倍率（默认 200，与格宽同量级）")]
        public float ModelReferenceHeight = 200f;

        /// <summary>是否用 element 索引轮转颜色。</summary>
        public bool ColorByElement = true;

        private VegetationDocument document;
        private float[] density;
        private byte[] biomeMap;
        private int cellsX;
        private int cellsZ;
        private int blocksX;
        private int blocksZ;
        private int cellsPerBlockX;
        private int cellsPerBlockZ;
        private float cellSize;
        private float blockWorldSize;

        private Block[] blocks;
        private Material material;
        // MaterialPropertyBlock 不能在字段初始化器里 new：Unity 会在 MonoBehaviour 构造期
        // 调用 CreateImpl 并直接抛 UnityException，必须在 Awake/首次加载时创建
        private MaterialPropertyBlock propertyBlock;
        private Matrix4x4[] batchBuffer;
        // 按模型分组：长度 = 模型数，Load 成功后按实际模型数重建
        private List<Matrix4x4>[] drawLists = new List<Matrix4x4>[0];
        private Color[] elementColors = new Color[0];

        private bool failed;
        private bool loaded;
        private Mesh fallbackMesh;

        /// <summary>314 植被模板资源（贴图 + 模板真实包围盒），缺失时为 null。</summary>
        private Terrain314VegetationAssets vegetationAssets;
        /// <summary>element 数量，分组维度。</summary>
        private int elementCount;
        /// <summary>每个 element 使用的替代模型索引。</summary>
        private int[] elementMeshIndex;
        /// <summary>每个 element 对应 314 模板的真实高度，用作 Parameters 倍率的尺寸基准。</summary>
        private float[] elementReferenceHeight;
        /// <summary>每个 element 的 314 模板贴图。</summary>
        private Texture2D[] elementBaseMap;

        /// <summary>已烘焙的块数量，调试用。</summary>
        public int BakedBlockCount { get; private set; }

        /// <summary>上一帧提交的实例总数，调试用。</summary>
        public int VisibleInstanceCount { get; private set; }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ReleaseResources();
        }

        /// <summary>加载数据与资源；失败只报一次，避免每帧刷屏。</summary>
        private bool EnsureLoaded()
        {
            if (failed) return false;
            if (loaded) return true;

            if (Terrain == null) Terrain = GetComponent<Terrain314Renderer>();
            if (Terrain == null || Terrain.Source == null) return false;

            try
            {
                string directory = System.IO.Path.Combine(Terrain.ResolvedDirectory, "vegetation");
                string jsonPath = System.IO.Path.Combine(directory, "vegetation.json");
                if (!System.IO.File.Exists(jsonPath))
                {
                    failed = true;
                    Log.Warning($"Terrain314 缺少植被声明: {jsonPath}");
                    return false;
                }

                document = JsonUtility.FromJson<VegetationDocument>(System.IO.File.ReadAllText(jsonPath));
                density = ReadFloats(System.IO.Path.Combine(directory, "density.bin"));
                biomeMap = System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory, "biome.bin"));

                cellsX = document.cells[0];
                cellsZ = document.cells[1];
                blocksX = document.blocks[0];
                blocksZ = document.blocks[1];
                cellsPerBlockX = cellsX / blocksX;
                cellsPerBlockZ = cellsZ / blocksZ;
                cellSize = document.cellSize;
                blockWorldSize = cellsPerBlockX * cellSize;

                int cellCount = cellsX * cellsZ;
                if (density.Length != cellCount || biomeMap.Length != cellCount)
                {
                    failed = true;
                    Log.Error($"Terrain314 植被图尺寸不符: cells={cellsX}x{cellsZ}, " +
                              $"density={density.Length}, biome={biomeMap.Length}");
                    return false;
                }

                blocks = new Block[blocksX * blocksZ];
                batchBuffer = new Matrix4x4[MaxInstancesPerBatch];
                propertyBlock ??= new MaterialPropertyBlock();
                EnsureMeshes();
                // 314 模板资源：贴图与真实包围盒，缺目录时退化为纯色 + 统一基准
                vegetationAssets ??= Terrain314VegetationAssets.Load(directory);
                InitializeDrawLists();
                InitializeElementBindings();
                EnsureMaterial();
                loaded = true;

                Log.Info($"Terrain314 植被就绪: {cellsX}x{cellsZ} 格, {blocksX}x{blocksZ} 块, " +
                         $"element {elementCount} 个, 模型 {Meshes.Length} 个, " +
                         $"314 模板 {(vegetationAssets != null && vegetationAssets.Loaded ? vegetationAssets.Count : 0)} 个, " +
                         $"有植被格 {CountVegetatedCells()} (ViewDistance={ViewDistance:F0})");
                return true;
            }
            catch (System.Exception exception)
            {
                failed = true;
                Log.Error($"Terrain314 植被加载失败: {exception.Message}");
                return false;
            }
        }

        /// <summary>模型池：编辑器下按固定文件名加载 <c>mesh_tree_1..10.obj</c>。</summary>
        private void EnsureMeshes()
        {
            // Inspector 已绑模型就直接用；否则编辑器下自动加载工程里的树模型
            if (HasAnyMesh()) return;
            if (TryLoadEditorMeshes() && HasAnyMesh()) return;

            // 没有任何模型时退化：3 片交叉草叶，至少能验证分布是否正确
            fallbackMesh = BuildFallbackMesh();
            Meshes = new[] { fallbackMesh };
            Log.Warning("Terrain314 植被未找到模型，已退化为程序化交叉草片");
        }

        private bool HasAnyMesh()
        {
            if (Meshes == null) return false;
            for (int i = 0; i < Meshes.Length; i++)
            {
                if (Meshes[i] != null) return true;
            }
            return false;
        }

        private bool TryLoadEditorMeshes()
        {
#if UNITY_EDITOR
            var meshes = new List<Mesh>();
            for (int index = 1; index <= 10; index++)
            {
                string path = $"{TreeMeshFolder}/mesh_tree_{index}.obj";
                // .obj 的主资产是 GameObject，Mesh 是子资产，必须用 LoadAllAssetsAtPath 取
                foreach (UnityEngine.Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Mesh mesh)
                    {
                        meshes.Add(mesh);
                        break;
                    }
                }
            }
            if (meshes.Count == 0) return false;
            Meshes = meshes.ToArray();
            return true;
#else
            return false;
#endif
        }

        /// <summary>材质与 element 配色表。</summary>
        private void EnsureMaterial()
        {
            Shader shader = Shader.Find("Sango/Terrain314Vegetation");
            if (shader == null)
            {
                failed = true;
                Log.Error("Terrain314 植被着色器缺失: Sango/Terrain314Vegetation");
                return;
            }

            material = new Material(shader) { name = "Terrain314VegetationRuntime" };
            material.enableInstancing = true;
            material.SetFloat("_WindStrength", WindStrength);

            for (int i = 0; i < elementColors.Length; i++)
            {
                // 按 element 轮转色相：0.22~0.32 之间摆动，饱和度与明度保持植被感
                float hue = 0.20f + 0.04f * (i % 4);
                float value = 0.32f + 0.10f * (i % 3);
                elementColors[i] = Color.HSVToRGB(hue, 0.55f, value);
            }
        }

        /// <summary>
        /// 按 element 建立分组与配色表。
        ///
        /// 分组维度必须是 element 而不是模型：314 的每个 element 指向一个独立模板
        /// （有各自的贴图与真实尺寸），同一模型可能被多个 element 复用，
        /// 按模型分组会把不同贴图/不同尺寸的实例混到一次 DrawMeshInstanced 里。
        /// </summary>
        private void InitializeDrawLists()
        {
            elementCount = document?.elements != null && document.elements.Length > 0
                ? document.elements.Length : 1;
            drawLists = new List<Matrix4x4>[elementCount];
            elementColors = new Color[elementCount];
            elementMeshIndex = new int[elementCount];
            elementReferenceHeight = new float[elementCount];
            elementBaseMap = new Texture2D[elementCount];
            for (int i = 0; i < elementCount; i++) drawLists[i] = new List<Matrix4x4>(512);
        }

        /// <summary>
        /// 把每个 element 绑到"替代模型 + 314 模板贴图 + 模板真实高度"。
        ///
        /// element.displaySetNameHash 是 314 的 DisplaySet NameHash，
        /// 与 templates-index.json 里的 nameHash 一一对应（实测 27 个 element 全部命中 31 个模板）。
        /// 找不到模板时高度记 0（回落 ModelReferenceHeight）、贴图为 null（回落纯色），不会中断渲染。
        /// </summary>
        private void InitializeElementBindings()
        {
            if (document?.elements == null) return;
            int meshCount = Meshes != null ? Meshes.Length : 0;
            int matched = 0;

            for (int i = 0; i < elementCount; i++)
            {
                VegetationElementData element = document.elements[i];
                // 用数组下标而不是 element.index：分组也是按下标建的，两者必须同一套编号
                elementMeshIndex[i] = meshCount > 0 ? i % meshCount : -1;
                elementReferenceHeight[i] = 0f;
                elementBaseMap[i] = null;
                if (vegetationAssets == null || !vegetationAssets.Loaded) continue;

                uint nameHash = unchecked((uint)element.displaySetNameHash);
                if (!vegetationAssets.TryGetTemplate(nameHash, out Terrain314VegetationAssets.Template template))
                    continue;

                elementReferenceHeight[i] = template.Height;
                elementBaseMap[i] = vegetationAssets.GetBaseMap(nameHash);
                matched++;
            }

            if (vegetationAssets != null && vegetationAssets.Loaded)
            {
                Log.Info($"Terrain314 植被模板对齐: {matched}/{elementCount} 个 element 命中 314 模板，" +
                         $"尺寸基准取自模板包围盒高度");
            }
        }

        /// <summary>相机渲染前提交植被。</summary>
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!Application.isPlaying && !Terrain314PreviewEnabled()) return;
            if (!EnsureLoaded()) return;
            if (material == null || Terrain == null || Terrain.Source == null) return;
            if (camera != Terrain.CurrentCamera) return;

            // 用观察点而不是相机位置：Scene 视图相机可能离场景几十万单位，
            // 直接拿它的坐标算块索引会全部越界（blockX/blockZ 超出 blocks 范围），
            // 结果是整个视角一块都不烘焙，看起来像"植被不渲染"。
            Vector3 eye = Terrain.ObservationPoint;
            int centerBlockX = Mathf.FloorToInt(eye.z / blockWorldSize);
            int centerBlockZ = Mathf.FloorToInt(eye.x / blockWorldSize);
            int radius = Mathf.CeilToInt(ViewDistance / blockWorldSize);

            VisibleInstanceCount = 0;
            for (int i = 0; i < drawLists.Length; i++) drawLists[i].Clear();

            int budget = MaxBlocksPerFrame;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // 块索引与世界方向同样要交换：世界 X 对应 314 的块 Z
                    int blockX = centerBlockX + dx;
                    int blockZ = centerBlockZ + dz;
                    if (blockX < 0 || blockZ < 0 || blockX >= blocksX || blockZ >= blocksZ) continue;

                    float offsetX = ((blockX + 0.5f) * blockWorldSize) - eye.z;
                    float offsetZ = ((blockZ + 0.5f) * blockWorldSize) - eye.x;
                    if (offsetX * offsetX + offsetZ * offsetZ > ViewDistance * ViewDistance) continue;

                    Block block = blocks[blockZ * blocksX + blockX];
                    if (block == null)
                    {
                        if (budget-- <= 0) continue;
                        block = BuildBlock(blockX, blockZ);
                        blocks[blockZ * blocksX + blockX] = block;
                        BakedBlockCount++;
                    }

                    for (int meshIndex = 0; meshIndex < drawLists.Length; meshIndex++)
                    {
                        List<Matrix4x4> group = block.Groups[meshIndex];
                        if (group.Count > 0) drawLists[meshIndex].AddRange(group);
                    }
                    VisibleInstanceCount += block.InstanceCount;
                }
            }

            DrawVegetation(camera);
        }

        private bool Terrain314PreviewEnabled()
        {
            return Terrain != null && Terrain.PreviewInEditMode;
        }

        /// <summary>按 element 分组分批提交（每个 element 有自己的模型、贴图与尺寸基准）。</summary>
        private void DrawVegetation(Camera camera)
        {
            if (material == null || batchBuffer == null || propertyBlock == null) return;
            if (Meshes == null || Meshes.Length == 0) return;
            // 风强可以在运行时调，每帧同步一次，避免只在创建材质时生效
            material.SetFloat("_WindStrength", WindStrength);
            for (int elementIndex = 0; elementIndex < drawLists.Length; elementIndex++)
            {
                List<Matrix4x4> list = drawLists[elementIndex];
                int meshIndex = elementMeshIndex != null && elementIndex < elementMeshIndex.Length
                    ? elementMeshIndex[elementIndex] : -1;
                if (list.Count == 0 || meshIndex < 0 || meshIndex >= Meshes.Length || Meshes[meshIndex] == null)
                    continue;

                if (ColorByElement) propertyBlock.SetColor("_BaseColor", elementColors[elementIndex]);
                // 贴图按 element 绑定：优先用 314 模板导出的贴图，没有就显式回退 white，
                // 避免上一批的贴图残留到这一批。
                Texture2D baseMap = elementBaseMap != null && elementIndex < elementBaseMap.Length
                    ? elementBaseMap[elementIndex] : null;
                if (baseMap == null && BaseMaps != null && meshIndex < BaseMaps.Length)
                    baseMap = BaseMaps[meshIndex];
                propertyBlock.SetTexture("_BaseMap", baseMap != null ? baseMap : Texture2D.whiteTexture);
                for (int start = 0; start < list.Count; start += MaxInstancesPerBatch)
                {
                    int count = Mathf.Min(MaxInstancesPerBatch, list.Count - start);
                    list.CopyTo(start, batchBuffer, 0, count);
                    Graphics.DrawMeshInstanced(Meshes[meshIndex], 0, material, batchBuffer, count,
                        propertyBlock, ShadowCastingMode.Off, false, gameObject.layer, camera);
                }
            }
        }

        /// <summary>烘焙一个块的实例（照抄 314 的随机流顺序）。</summary>
        private Block BuildBlock(int blockX, int blockZ)
        {
            var block = new Block(elementCount);
            int startX = blockX * cellsPerBlockX;
            int startZ = blockZ * cellsPerBlockZ;
            var candidates = new List<long>(128);

            if (Meshes == null || Meshes.Length == 0) return block;

            // 锚点补偿与尺寸归一化按 element 预计算（每个 element 固定一个模型 + 一份 314 模板尺寸）。
            //
            // 锚点：314 的落位要减掉"缩放后的包围盒中心"，
            // 出处 San14.Data/Rendering/Terrain/MapProceduralPlacementProducer.cs:137-138,154-156。
            //
            // 尺寸：314 的 Parameters[1]/[2] 是"原版模板原始尺寸"上的倍率，
            // 模板真实高度由 templates-index.json 给出（导出实测 63 ~ 2124 单位，差异几十倍），
            // 所以必须逐个 element 用自己模板的高度做基准，不能用统一常数。
            var anchorXs = new float[elementCount];
            var anchorZs = new float[elementCount];
            var sizeNormalizes = new float[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                int meshIndexForElement = elementMeshIndex[i];
                Mesh mesh = meshIndexForElement >= 0 && meshIndexForElement < Meshes.Length
                    ? Meshes[meshIndexForElement] : null;
                if (mesh == null)
                {
                    sizeNormalizes[i] = 0f;
                    continue;
                }
                Bounds bounds = mesh.bounds;
                anchorXs[i] = bounds.center.x;
                anchorZs[i] = bounds.center.z;
                float referenceHeight = elementReferenceHeight[i];
                if (referenceHeight <= 0f) referenceHeight = ModelReferenceHeight;
                sizeNormalizes[i] = referenceHeight / Mathf.Max(bounds.size.y, 1e-4f);
            }

            foreach (VegetationBiomeData biome in document.biomes)
            {
                if (biome.components == null || biome.components.Length == 0) continue;

                // randomSeed 在 JSON 里是无符号大值，DTO 用 int 存位模式，这里还原
                uint random = unchecked((uint)biome.randomSeed * 0x35Cu + 0x1534FFu +
                                        (uint)blockX * 0x47u +
                                        (unchecked((uint)blockZ * 0x6A836u) >> 3));
                uint advance = unchecked((uint)blockZ * 3u + (uint)blockX * 2u) % 13u;
                for (uint step = 0; step < advance; step++) random = Step(random);
                uint first = Step(random);
                uint elementSalt = (uint)((first >> 16) * Unit(Step(first)));

                foreach (int componentIndex in biome.components)
                {
                    VegetationComponentData component = document.components[componentIndex];
                    if (component.elements == null) continue;

                    foreach (int elementIndex in component.elements)
                    {
                        VegetationElementData element = document.elements[elementIndex];
                        random = unchecked((uint)element.randomSeed + elementSalt);
                        candidates.Clear();

                        for (int z = startZ; z < startZ + cellsPerBlockZ; z++)
                        {
                            for (int x = startX; x < startX + cellsPerBlockX; x++)
                            {
                                random = Step(random);
                                int cell = z * cellsX + x;
                                if (biomeMap[cell] != biome.index) continue;

                                float amount = density[cell] * cellSize * cellSize *
                                               element.parameters[0] / 10000f;
                                uint count = (uint)amount;
                                if (Unit(random) < amount - count) count++;
                                for (int ordinal = 0; ordinal < count; ordinal++)
                                {
                                    candidates.Add(((long)cell << 32) | (uint)ordinal);
                                }
                            }
                        }

                        uint fullShuffleSeed = Step(random);
                        uint shuffle = fullShuffleSeed >> 16;
                        for (int i = 0; i < candidates.Count - 1; i++)
                        {
                            shuffle = Step(shuffle);
                            int j = (int)((shuffle >> 16) % (uint)(candidates.Count - i)) + i;
                            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                        }

                        uint placementSalt = Step(fullShuffleSeed) >> 16;

                        foreach (long candidate in candidates)
                        {
                            int cell = (int)(candidate >> 32);
                            int ordinal = (int)(candidate & 0xFFFFFFFFL);
                            int x = cell % cellsX;
                            int z = cell / cellsX;

                            // element 与模型一一固定：314 的 element 本身就指向一个模板，
                            // 同一 element 的所有实例共用同一个模型、同一份贴图、同一个尺寸基准。
                            List<Matrix4x4> group = block.Groups[elementIndex];
                            float anchorX = anchorXs[elementIndex];
                            float anchorZ = anchorZs[elementIndex];
                            float sizeNormalize = sizeNormalizes[elementIndex];
                            if (sizeNormalize <= 0f) continue;

                            random = unchecked((uint)x * 123u + (uint)z * 456u +
                                               placementSalt + (uint)ordinal * 789u);
                            advance = (uint)((x * 1.2f + z + ordinal * 12) % 13f);
                            for (uint step = 0; step < advance; step++) random = Step(random);
                            random = Step(random);

                            float scale = Mathf.Lerp(element.parameters[1], element.parameters[2],
                                Unit(random));
                            scale = Mathf.Max(scale, 1.1920929e-5f);
                            // 再乘模型基准归一化，把"原版模板原始尺寸"折算成统一基准高度
                            scale *= sizeNormalize;

                            random = Step(random);
                            float yawHalf = Unit(random) * 3.1415927f;
                            float yaw = yawHalf + yawHalf;

                            random = Step(random);
                            float positionX314 = (x + Unit(random)) * cellSize +
                                                 document.fieldOffset[0];
                            random = Step(random);
                            float positionZ314 = (Unit(random) + z) * cellSize +
                                                 document.fieldOffset[1];

                            // 锚点补偿 + 边界夹取，对齐原版 154-161 行：
                            // 先减掉缩放后的包围盒中心，再夹到「场地范围 ± cellSize*0.001」内，
                            // 避免边缘格子的实例被推出地形边界。
                            positionX314 -= scale * anchorX;
                            positionZ314 -= scale * anchorZ;
                            float margin = cellSize * 0.001f;
                            positionX314 = Mathf.Clamp(positionX314,
                                document.fieldOffset[0] + margin,
                                cellsX * cellSize + document.fieldOffset[0] - margin);
                            positionZ314 = Mathf.Clamp(positionZ314,
                                document.fieldOffset[1] + margin,
                                cellsZ * cellSize + document.fieldOffset[1] - margin);

                            // 314 的 X 是东、Z 是南；项目世界 X 是南、Z 是东，因此交换
                            float worldX = positionZ314;
                            float worldZ = positionX314;
                            float worldY = Terrain.Source.Height.SampleWorld(worldX, worldZ);

                            Vector3 position = new(worldX, worldY, worldZ);
                            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                            group.Add(Matrix4x4.TRS(position, rotation, Vector3.one * scale));
                        }
                    }
                }
            }

            return block;
        }

        /// <summary>314 的 LCG：state = state * 0x10DCD + 1。</summary>
        private static uint Step(uint state) => unchecked(state * 0x10DCDu + 1u);

        /// <summary>取高 16 位归一化到 [0,1]。</summary>
        private static float Unit(uint state) => (state >> 16) / 65535f;

        /// <summary>读 float32 小端数组。</summary>
        private static float[] ReadFloats(string path)
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            var values = new float[bytes.Length / sizeof(float)];
            System.Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private int CountVegetatedCells()
        {
            int count = 0;
            for (int i = 0; i < biomeMap.Length; i++)
            {
                if (biomeMap[i] != byte.MaxValue) count++;
            }
            return count;
        }

        /// <summary>兜底几何：三片 45° 交叉的窄草叶。</summary>
        private static Mesh BuildFallbackMesh()
        {
            var vertices = new Vector3[12];
            var triangles = new int[18];
            for (int blade = 0; blade < 3; blade++)
            {
                float angle = blade * Mathf.PI / 3f;
                Vector3 right = new(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);
                int index = blade * 4;
                vertices[index] = -right;
                vertices[index + 1] = right;
                vertices[index + 2] = right + Vector3.up;
                vertices[index + 3] = -right + Vector3.up;
                int tri = blade * 6;
                triangles[tri] = index;
                triangles[tri + 1] = index + 2;
                triangles[tri + 2] = index + 1;
                triangles[tri + 3] = index;
                triangles[tri + 4] = index + 3;
                triangles[tri + 5] = index + 2;
            }

            var mesh = new Mesh { name = "Terrain314VegetationFallback" };
            mesh.SetVertices(new List<Vector3>(vertices));
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
            return mesh;
        }

        /// <summary>释放运行时资源。</summary>
        private void ReleaseResources()
        {
            bool playing = Application.isPlaying;
            if (material != null)
            {
                if (playing) Destroy(material); else DestroyImmediate(material);
                material = null;
            }
            if (fallbackMesh != null)
            {
                if (playing) Destroy(fallbackMesh); else DestroyImmediate(fallbackMesh);
                fallbackMesh = null;
            }
            // 314 模板贴图是运行时创建的 Texture2D，必须显式释放，否则重载会持续涨显存
            vegetationAssets?.Release();
            vegetationAssets = null;
            elementBaseMap = null;
            elementMeshIndex = null;
            elementReferenceHeight = null;
            blocks = null;
            loaded = false;
            failed = false;
            BakedBlockCount = 0;
            VisibleInstanceCount = 0;
        }

        /// <summary>一个已烘焙的块：按 element 分组存放实例矩阵。</summary>
        private sealed class Block
        {
            public readonly List<Matrix4x4>[] Groups;
            public int InstanceCount;

            public Block(int groupCount)
            {
                Groups = new List<Matrix4x4>[groupCount];
                for (int i = 0; i < groupCount; i++) Groups[i] = new List<Matrix4x4>(16);
            }
        }

        // DTO 字段全部由 JsonUtility 反射赋值，编译器看不到写入点，屏蔽 CS0649
#pragma warning disable 0649
        /// <summary>vegetation.json 的顶层结构。</summary>
        [System.Serializable]
        private sealed class VegetationDocument
        {
            public int version;
            public float[] fieldSize;
            public float[] fieldOffset;
            public int[] cells;
            public int[] blocks;
            public float cellSize;
            public float updateDistance;
            public float freeDistance;
            public float viewDistance;
            public int terrainType;
            public int globalFlags;
            public float absolutelyRenderDistanceH;
            public float absolutelyRenderDistanceV;
            public VegetationBiomeData[] biomes;
            public VegetationComponentData[] components;
            public VegetationElementData[] elements;
        }

        /// <summary>一个 biome：引用若干 component。</summary>
        [System.Serializable]
        private sealed class VegetationBiomeData
        {
            public int index;
            // 索引与种子统一用 int：JSON 里的种子是无符号大值，int 只是位容器，
            // 参与 LCG 前用 unchecked((uint)value) 还原
            public int[] components;
            public int densityMapMode;
            public int randomSeed;
            public int tail;
        }

        /// <summary>一个 component：引用若干 element。</summary>
        [System.Serializable]
        private sealed class VegetationComponentData
        {
            public int index;
            public int[] elements;
            public int tail0;
            public int tail1;
        }

        /// <summary>一个 element：Parameters = [密度权重, 最小缩放, 最大缩放]。</summary>
        [System.Serializable]
        private sealed class VegetationElementData
        {
            public int index;
            public int type;
            public int flags;
            public int displaySetNameHash;
            public int randomSeed;
            public float[] parameters;
            public float[] lodDistances;
        }
#pragma warning restore 0649
    }
}
