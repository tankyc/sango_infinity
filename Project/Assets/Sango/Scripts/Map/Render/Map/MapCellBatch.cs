using Sango.Core;
using Sango.Tools;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango.Render
{
    /// <summary>
    /// 地形块（共享资产改造版，简称"新块"）。
    ///
    /// 【为什么和 MapCell 并存】
    ///   本类依赖 Texture2DArray（图层贴图数组），部分低端设备 / 图形 API 不支持
    ///   （<see cref="SystemInfo.supports2DArrayTextures"/> = false）。为保住这些设备的地形显示，
    ///   老实现 <see cref="MapCell"/> 完整保留，两者共同实现 <see cref="IMapCell"/>，
    ///   由 <see cref="MapTerrain"/> 在建块时按设备能力二选一。
    ///
    /// 【与旧实现的差异】
    ///   ① 不再分层：旧版一个块按顶点 textureIndex 拆成 36~49 个子节点（每层 Mesh/Renderer/
    ///      Collider/材质实例）；现在只有两个子节点 —— Terrain（地表）与 Water（水面独立网格），
    ///      层归属交给预烘焙控制贴图（<see cref="MapCellTerrainAsset.ControlTex"/>）在着色器里混合。
    ///   ② 共享材质：全图只有一份地形材质 + 一份水材质（来自 <see cref="MapCellTerrainAsset"/>），
    ///      SRP Batcher 可把全部块的渲染合批；旧版每块每层一个材质实例，完全无法合批。
    ///   ③ 网格重建快速化：构建是纯几何流程（步长抽稀 + 直接填数组），不再做逐层判断与
    ///      LayerMeshData 对象池周转，LOD 档位扩到 0~6（步长 1&lt;&lt;6 = 整块 1 个 quad），
    ///      支撑远景/顶视图全览的极低精度档。
    ///   ④ LOD 缺陷修复：块不可见时 <see cref="SetLod"/> 只记 lodPending 标记，
    ///      重新可见后由 <see cref="Update"/> 补建 —— "拉近相机地形卡低精度"的旧缺陷已修。
    ///   ⑤ 碰撞体独立：collider 用固定 LOD1 精度烘焙一次并缓存，之后 LOD 往返不再重烘
    ///      （MeshCollider 重烘焙是旧版单块重建耗时的大头）。
    ///
    /// 【远景】相机拉远后渲染被 <see cref="TerrainCellBatchRenderer"/> 接管（Instanced），
    /// 本类只保留 collider 供点击拾取，MeshRenderer 关闭。
    ///
    /// 【原版无对应实现】Open311 无对应的块级地形渲染，此为渲染层自主设计。
    /// </summary>
    public class MapCellBatch : MapProperty, IMapManageObject, IMapCell
    {
        public Vector2Int startCoords;
        public Vector2Int quadBounds;

        // ───────────── 子节点（Terrain + Water）─────────────
        GameObject terrainNode;
        GameObject waterNode;
        Mesh terrainMesh;
        MeshRenderer terrainRenderer;
        MeshCollider terrainCollider;
        Mesh waterMesh;
        MeshRenderer waterRenderer;
        MeshCollider waterCollider;

        /// <summary>
        /// 碰撞体专用网格（与渲染网格分离）：首次构建时按 <see cref="CollideLod"/> 精度烘焙一次，
        /// 之后 LOD 往返不再重烘（对同一 Mesh 改内容会触发 MeshCollider 重烘焙，是旧版耗时大头）。
        /// </summary>
        Mesh terrainColliderMesh;
        Mesh waterColliderMesh;
        bool colliderBaked;

        public GameObject gameObject { get { return terrainNode; } }
        public bool isAdded { get; set; }
        public Transform transform { get { return terrainNode != null ? terrainNode.transform : null; } }

        /// <summary>LOD 档位上限（1&lt;&lt;6 = 64 = 整块一格，顶视图全览的极低档）。</summary>
        public const int MaxLod = 6;

        /// <summary>碰撞体烘焙精度（LOD1 = 步长 2 格，点击拾取误差约 1 格，烘焙一次后不再重烘）。</summary>
        public const int CollideLod = 1;

#if UNITY_ANDROID || UNITY_IPHONE
        public int lod = 1;
#else
        public int lod = 0;
#endif

        /// <summary>
        /// LOD 待重建标记（旧缺陷①的修复）。
        /// 块不可见 / 未建网格 / 正在构建时 <see cref="SetLod"/> 只改字段 + 置位本标记；
        /// <see cref="Update"/> 在"可见且网格已建"时消费它补建 —— 块重新可见后精度一定跟上。
        /// </summary>
        bool lodPending;

        /// <summary>是否已切到"不带描边"的层（远距离 LOD 用，URP 描边 Feature 按 LayerMask 过滤）。</summary>
        bool noOutLineLayer;

        /// <summary>当前生效的物理层（Map / MapNoOutLine）</summary>
        int CurrentLayer { get { return noOutLineLayer ? MapLayers.MapNoOutLine : MapLayers.Map; } }

        /// <summary>离开视野后的累计时长（秒），由 MapTerrain 驱动，用于空闲卸载。</summary>
        public float idleTime;

        // ───────────── 后台构建状态 ─────────────

        /// <summary>一次后台构建的产物（地形 + 水两套网格数据），线程内填充、主线程应用。</summary>
        class MeshBuildData
        {
            public readonly List<Vector3> vertices = new List<Vector3>(4225);
            public readonly List<Vector3> normals = new List<Vector3>(4225);
            public readonly List<Vector2> uvs = new List<Vector2>(4225);
            public readonly List<Color> colors = new List<Color>(4225);
            public readonly List<int> triangles = new List<int>(24576);

            public void Clear()
            {
                vertices.Clear();
                normals.Clear();
                uvs.Clear();
                colors.Clear();
                triangles.Clear();
            }
        }

        /// <summary>地形构建产物（线程填充 → 主线程应用）。</summary>
        MeshBuildData terrainBuild;
        /// <summary>水面构建产物（线程填充 → 主线程应用）。</summary>
        MeshBuildData waterBuild;
        /// <summary>碰撞体构建产物（首次构建时生成，主线程烘焙后置空）。</summary>
        MeshBuildData terrainCollideBuild;
        MeshBuildData waterCollideBuild;

        /// <summary>后台构建是否完成待应用（主线程 Update 里消费）。</summary>
        volatile bool threadBuildDone;
        volatile bool threadCancelRequest;
        bool threadLoadStart;

        /// <summary>
        /// Mesh 对象池。块离开视野后归还、再次进入视野直接复用，
        /// 省掉"销毁 → 新建"的托管 / 原生内存抖动。只在主线程读写，无需加锁。
        /// </summary>
        static readonly Stack<Mesh> meshPool = new Stack<Mesh>();
        const int MeshPoolLimit = 512;

        /// <summary>取一个可用网格：优先复用池中的，池空才新建。</summary>
        static Mesh CreateMesh()
        {
            if (meshPool.Count > 0)
                return meshPool.Pop();

            Mesh m = new Mesh();
            m.MarkDynamic();
            return m;
        }

        /// <summary>归还网格：清空几何数据后放回池，超上限则直接销毁。</summary>
        static void RecycleMesh(Mesh m)
        {
            if (m == null) return;

            m.Clear(false);
            if (meshPool.Count < MeshPoolLimit)
                meshPool.Push(m);
            else
                GameObject.Destroy(m);
        }

        /// <summary>
        /// 是否已登记进 <see cref="MapTerrain"/> 的逐帧处理表。
        /// 地形不必每帧扫描全图块：只有"刚进入视野 / 等待空闲卸载"的块才有事可做。
        /// </summary>
        internal bool inFrameUpdateList = false;

        private bool _visible = false;
        private bool _visibleChanged = false;

        /// <summary>是否可保存至地图</summary>
        public bool canSave { get; set; } = false;

        public MapCellBatch(MapRender map, int x, int y, int w, int h) : base(map)
        {
            startCoords = new Vector2Int(x, y);
            quadBounds = new Vector2Int(w, h);
            position = new Vector3(startCoords.y * map.mapData.quadSize, 0, startCoords.x * map.mapData.quadSize);
            this.bounds = new Sango.Tools.Rect(0, 0, quadBounds.x * map.mapData.quadSize, quadBounds.y * map.mapData.quadSize);
        }

        public MapCellBatch(MapRender map, Vector2Int start, Vector2Int bounds) : base(map)
        {
            startCoords = start;
            quadBounds = bounds;
            position = new Vector3(startCoords.y * map.mapData.quadSize, 0, startCoords.x * map.mapData.quadSize);
            this.bounds = new Sango.Tools.Rect(0, 0, quadBounds.x * map.mapData.quadSize, quadBounds.y * map.mapData.quadSize);
        }

        // ───────────── 节点创建 ─────────────

        public override void Init()
        {
            // 远处才进入视野的块直接按当前目标精度构建（远景下新块不再是全精度）
            if (map != null && map.mapTerrain != null)
                lod = Math.Min(map.mapTerrain.TargetLod, MaxLod);

            if (terrainNode != null)
            {
                // 已经建过：可能被 ReleaseMesh 释放了 Mesh 实体，这里只补齐网格与层档位
                EnsureMeshes();
                ApplyLayer(CurrentLayer);
                return;
            }

            // 层索引走 MapLayers 缓存：地形块会在 Map / MapNoOutLine 之间切换（远距离 LOD）
            terrainNode = new GameObject(string.Format("Map区域_{0}_{1}", startCoords.x, startCoords.y));
            terrainNode.layer = CurrentLayer;

            // 地形块直接挂在 Map 根节点下
            if (MapRender.mapRoot != null)
                terrainNode.transform.SetParent(MapRender.mapRoot.transform);

            terrainNode.transform.position = position;

            // ---- Terrain 节点：地表网格 + 碰撞体 ----
            // 【顺序要求】先挂 MeshCollider 再挂 MeshFilter：AddComponent<MeshCollider> 时
            // Unity 会立即从同节点的 MeshFilter 提取几何（MeshCollider::Reset → ExtractMeshGeometry），
            // 此刻网格还是空的会刷 "This Mesh Collider ... doesn't have Read/Write enabled" 错误日志。
            // 先挂 Collider（没有 MeshFilter 可提取）再把 MeshFilter 接上，彻底避开这条噪音。
            terrainCollider = terrainNode.AddComponent<MeshCollider>();
            terrainCollider.sharedMesh = null;
            terrainColliderMesh = CreateMesh();

            terrainMesh = CreateMesh();
            terrainNode.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            terrainRenderer = terrainNode.AddComponent<MeshRenderer>();
            terrainRenderer.enabled = false;

            // ---- Water 节点：独立水面网格（共享水材质，岸线 alpha 渐变由顶点色提供）----
            waterNode = new GameObject("Water");
            waterNode.transform.SetParent(terrainNode.transform, false);
            waterNode.layer = CurrentLayer;

            waterCollider = waterNode.AddComponent<MeshCollider>();
            waterCollider.sharedMesh = null;
            waterColliderMesh = CreateMesh();

            waterMesh = CreateMesh();
            waterNode.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            waterRenderer = waterNode.AddComponent<MeshRenderer>();
            waterRenderer.enabled = false;

            ApplyMaterials();
        }

        /// <summary>材质无效时的降级警告是否已输出过（避免每块刷一条）。</summary>
        static bool warnedMaterialFallback;

        /// <summary>
        /// 重新赋共享材质（共享资产重建后由 <see cref="MapTerrain.RebuildTerrainAsset"/> 调用）。
        /// 资产重建时旧材质已随 Dispose 销毁，块必须重新取一次，否则引用悬空表现为地形消失。
        /// </summary>
        internal void ReapplyMaterials()
        {
            ApplyMaterials();
        }

        /// <summary>
        /// 给两个子节点赋共享材质。
        /// 【降级链】共享资产材质（有效）→ 旧管线首层材质 → null。
        /// 关键：Shader.Find 在 shader 导入中 / 编译失败时可能返回"没有有效 pass 的空壳"，
        /// 此时材质虽然非空但完全不出像素（表现为"地形不显示"），必须识别出来降级，
        /// 否则整张地图一片空白且没有任何线索。
        /// </summary>
        void ApplyMaterials()
        {
            MapCellTerrainAsset asset = MapCellTerrainAsset.Current;
            MapLayer.LayerData[] layers = map.mapLayer != null ? map.mapLayer.layerDatas : null;

            Material terrainMat = null;
            Material waterMat = null;

            // 共享材质是否真的可用（非 null 且至少一个 pass）
            bool assetMaterialValid = asset != null && asset.TerrainMaterial != null
                && asset.TerrainMaterial.passCount > 0;

            if (assetMaterialValid)
            {
                terrainMat = asset.TerrainMaterial;
            }
            else if (layers != null && layers.Length > 0 && layers[0] != null)
            {
                // 降级为"单层旧材质"，保住地形可见（着色器不可用时这是唯一的可渲染路径）
                terrainMat = layers[0].material;
                if (!warnedMaterialFallback)
                {
                    warnedMaterialFallback = true;
                    Log.Warning("Sango/terrain_cell 共享材质不可用（shader 未导入/编译失败或设备不支持纹理数组），"
                        + "MapCell 地形降级为旧管线首层材质显示。请检查 Console 中的 shader 报错。");
                }
            }

            if (layers != null && layers.Length > 0 && layers[layers.Length - 1] != null)
                waterMat = layers[layers.Length - 1].material;

            if (terrainRenderer != null)
                terrainRenderer.sharedMaterial = terrainMat;
            if (waterRenderer != null && waterMat != null)
                waterRenderer.sharedMaterial = waterMat;
        }

        /// <summary>补齐被 ReleaseMesh 释放掉的 Mesh 实体（节点与 Renderer 都还留着）。</summary>
        void EnsureMeshes()
        {
            if (terrainMesh == null && terrainRenderer != null)
            {
                terrainMesh = CreateMesh();
                MeshFilter mf = terrainRenderer.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = terrainMesh;
            }
            if (waterMesh == null && waterRenderer != null)
            {
                waterMesh = CreateMesh();
                MeshFilter mf = waterRenderer.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = waterMesh;
            }
            if (terrainColliderMesh == null)
                terrainColliderMesh = CreateMesh();
            if (waterColliderMesh == null)
                waterColliderMesh = CreateMesh();
        }

        /// <summary>
        /// 本块当前是否持有"节点 + 可渲染的地表网格"。
        ///
        /// 【为什么必须两个都判】节点可能在、网格已被 <see cref="ReleaseMesh"/>（空闲卸载）
        /// 归还进对象池 —— 那种状态下块是画不出任何东西的。只判节点会让
        /// <see cref="Update"/> 的"补建"兜底失效，表现为"拉远久一点再拉近，这些地块整片空白"。
        /// </summary>
        public bool HasMesh()
        {
            return terrainNode != null && terrainMesh != null;
        }

        /// <summary>
        /// 释放本块的网格实体（顶点数据仍在地图数据里），下次进入视野会自动重建。
        /// 只销毁 Mesh 本体、保留 GameObject：MapObject.position / worldBounds 等依赖依然可用。
        /// </summary>
        public void ReleaseMesh()
        {
            // 后台构建中不要拆，否则回调会踩到半成品
            if (IsInThreadLoad())
                return;
            if (terrainNode == null)
                return;

            if (terrainRenderer != null)
                terrainRenderer.enabled = false;
            if (waterRenderer != null)
                waterRenderer.enabled = false;

            if (terrainMesh != null) { RecycleMesh(terrainMesh); terrainMesh = null; }
            if (waterMesh != null) { RecycleMesh(waterMesh); waterMesh = null; }
            if (terrainColliderMesh != null) { RecycleMesh(terrainColliderMesh); terrainColliderMesh = null; }
            if (waterColliderMesh != null) { RecycleMesh(waterColliderMesh); waterColliderMesh = null; }
            if (terrainCollider != null) terrainCollider.sharedMesh = null;
            if (waterCollider != null) waterCollider.sharedMesh = null;
            colliderBaked = false;

            terrainBuild = null;
            waterBuild = null;
            terrainCollideBuild = null;
            waterCollideBuild = null;
            threadBuildDone = false;
            threadLoadStart = false;
            idleTime = 0f;

            // 解登记：本块已无网格，逐帧循环不必再访问它；下次进入视野会由 visible setter 重新登记。
            inFrameUpdateList = false;
        }

        public override void Clear()
        {
            // 请求后台构建协作式退出：清空后不应再把构建结果回写到已销毁的块上
            threadCancelRequest = true;

            if (terrainNode != null)
                GameObject.Destroy(terrainNode);
            terrainNode = null;
            waterNode = null;
            terrainMesh = null;
            waterMesh = null;
            terrainRenderer = null;
            waterRenderer = null;
            terrainCollider = null;
            waterCollider = null;
            terrainColliderMesh = null;
            waterColliderMesh = null;
            colliderBaked = false;
            terrainBuild = null;
            waterBuild = null;
            terrainCollideBuild = null;
            waterCollideBuild = null;

            // 解登记：本块已整体清理，逐帧循环里彻底移除
            inFrameUpdateList = false;
            lodPending = false;
        }

        // ───────────── 构建入口 ─────────────

        /// <summary>
        /// 同步构建（编辑器刷新 / 强制构建路径）。needInit = false 时不重建节点、只刷新网格。
        /// </summary>
        public void PrepareDatas(bool needInit = true)
        {
            if (needInit)
                Init();

            // 网格实体可能已被空闲卸载回收（ReleaseMesh），构建前先补建，否则 ApplyBuildResult
            // 会因 terrainMesh == null 丢弃整份结果。Mesh 只能在主线程创建，所以放在这里。
            EnsureMeshes();

            // 编辑器：笔刷可能改过 textureIndex（旧管线的层归属在网格里，改完重建即可见；
            // 改造后层归属在控制贴图里，必须在这里同步刷新本块区域）
            if (Tools.MapEditor.IsEditOn && map.mapData != null)
            {
                MapCellTerrainAsset asset = MapCellTerrainAsset.Current;
                if (asset != null)
                    asset.RefreshControlRegion(map.mapData,
                        startCoords.x, startCoords.y, quadBounds.x + 1, quadBounds.y + 1);
            }

            threadCancelRequest = false;
            BuildAll();
            ApplyBuildResult();
        }

        /// <summary>异步构建（进入视野的常规路径）：后台线程填数据，主线程 Update 里应用。</summary>
        public void AsyncPrepareDatas()
        {
            // 同 PrepareDatas：网格可能被空闲卸载回收过，构建线程只填顶点/三角形数据，
            // Mesh 实体必须在主线程先补回来（否则后台构建的成果会被 ApplyBuildResult 丢弃）
            EnsureMeshes();

            threadCancelRequest = false;    // 新一轮构建：复位上一次可能残留的取消请求
            threadLoadStart = true;
            threadBuildDone = false;
            ThreadPool.QueueUserWorkItem(new WaitCallback(_PrepareDatas));
        }

        void _PrepareDatas(object stateInfo)
        {
            // 协作式取消：Clear 会置位，构建线程在此自行退出，主线程不再读取其结果。
            if (threadCancelRequest)
                return;

            BuildAll();

            if (threadCancelRequest)
                return;

            threadBuildDone = true;
        }

        /// <summary>构建地形 + 水面 + 首次碰撞体数据（线程安全：只读 MapData）。</summary>
        void BuildAll()
        {
            if (terrainBuild == null) terrainBuild = new MeshBuildData();
            if (waterBuild == null) waterBuild = new MeshBuildData();
            terrainBuild.Clear();
            waterBuild.Clear();

            BuildTerrainMesh(terrainBuild, lod);
            BuildWaterMesh(waterBuild, Math.Min(lod, 2));

            // 碰撞体只烘一次（CollideLod 精度），后续 LOD 往返不再重烘；
            // 编辑器态顶点随时可改，每次都重烘保证笔刷结果立即可点。
            if (!colliderBaked || Tools.MapEditor.IsEditOn)
            {
                if (terrainCollideBuild == null) terrainCollideBuild = new MeshBuildData();
                if (waterCollideBuild == null) waterCollideBuild = new MeshBuildData();
                terrainCollideBuild.Clear();
                waterCollideBuild.Clear();
                BuildTerrainMesh(terrainCollideBuild, CollideLod);
                BuildWaterMesh(waterCollideBuild, Math.Min(CollideLod + 2, MaxLod));
            }
        }

        // ───────────── 网格构建（纯几何，无分层判断）─────────────

        /// <summary>
        /// 构建地表网格：步长抽稀的全格网格，高度 / 法线 / UV 全部按旧管线口径取自 MapData。
        /// 图层混合不再依赖网格（控制贴图在着色器里完成），因此无逐层判断。
        /// </summary>
        void BuildTerrainMesh(MeshBuildData data, int lodLevel)
        {
            MapData mapData = map.mapData;
            if (mapData == null || mapData.vertexDatas == null)
                return;

            int step = 1 << Math.Max(0, Math.Min(lodLevel, MaxLod));
            int xStart = startCoords.x;
            int yStart = startCoords.y;
            int xEnd = startCoords.x + quadBounds.x;
            int yEnd = startCoords.y + quadBounds.y;
            int cols = (xEnd - xStart) / step + 1;
            int rows = (yEnd - yStart) / step + 1;

            MapData.VertexData[][] vm = mapData.vertexDatas;
            int vxMax = vm.Length;
            int vyMax = vm[0].Length;

            // 顶点 = (vx, vy) 格点，沿 vx 为列、vy 为行；索引 index(vx, vy) = j * cols + i
            for (int j = 0; j < rows; j++)
            {
                int vy = Math.Min(yStart + j * step, vyMax - 1);
                for (int i = 0; i < cols; i++)
                {
                    int vx = Math.Min(xStart + i * step, vxMax - 1);
                    MapData.VertexData v = vm[vx][vy];
                    data.vertices.Add(mapData.VertexPosition(v.height, vx, vy) - position);
                    data.normals.Add(mapData.VertexNormal(v, vx, vy));
                    data.uvs.Add(mapData.VertexUV(vx, vy));
                    data.colors.Add(Color.white);
                }
            }

            // 三角形顺序照抄旧 CreateLayers：(i0,i2,i3) + (i1,i2,i0)
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < cols - 1; i++)
                {
                    int i0 = j * cols + i;
                    int i1 = j * cols + i + 1;
                    int i2 = (j + 1) * cols + i + 1;
                    int i3 = (j + 1) * cols + i;

                    data.triangles.Add(i0);
                    data.triangles.Add(i2);
                    data.triangles.Add(i3);
                    data.triangles.Add(i1);
                    data.triangles.Add(i2);
                    data.triangles.Add(i0);
                }
            }
        }

        /// <summary>
        /// 构建水面网格（独立 mesh）：旧口径"四角任一 water &gt; 0 就建面"，
        /// 顶点色 alpha 提供岸线渐变（water &gt; 0 白 / 否则黑，water.shader 用它做透明淡出）。
        /// 每个 quad 独立 4 顶点（不去重）：水面对顶点焊接无需求，构建路径最简。
        /// </summary>
        void BuildWaterMesh(MeshBuildData data, int lodLevel)
        {
            MapData mapData = map.mapData;
            if (mapData == null || mapData.vertexDatas == null)
                return;

            int step = 1 << Math.Max(0, Math.Min(lodLevel, MaxLod));
            int xEnd = startCoords.x + quadBounds.x;
            int yEnd = startCoords.y + quadBounds.y;

            MapData.VertexData[][] vm = mapData.vertexDatas;
            int vxMax = vm.Length;
            int vyMax = vm[0].Length;

            for (int x = startCoords.x; x < xEnd; x += step)
            {
                int xL = Math.Min(x + step, vxMax - 1);
                for (int y = startCoords.y; y < yEnd; y += step)
                {
                    int yL = Math.Min(y + step, vyMax - 1);

                    MapData.VertexData v0 = vm[x][y];
                    MapData.VertexData v1 = vm[xL][y];
                    MapData.VertexData v2 = vm[xL][yL];
                    MapData.VertexData v3 = vm[x][yL];

                    // 旧口径：四角全无水才跳过
                    if (v0.water == 0 && v1.water == 0 && v2.water == 0 && v3.water == 0)
                        continue;

                    int baseIndex = data.vertices.Count;
                    AddWaterVertex(data, mapData, vm, v0, x, y);
                    AddWaterVertex(data, mapData, vm, v1, xL, y);
                    AddWaterVertex(data, mapData, vm, v2, xL, yL);
                    AddWaterVertex(data, mapData, vm, v3, x, yL);

                    // 与旧 CreateLayers 相同的三角形顺序
                    data.triangles.Add(baseIndex);
                    data.triangles.Add(baseIndex + 2);
                    data.triangles.Add(baseIndex + 3);
                    data.triangles.Add(baseIndex + 1);
                    data.triangles.Add(baseIndex + 2);
                    data.triangles.Add(baseIndex);
                }
            }
        }

        /// <summary>水面顶点：水位（含邻域回退）+ 法线朝上 + 岸线 alpha（water&gt;0 白 / 否则黑）。</summary>
        void AddWaterVertex(MeshBuildData data, MapData mapData, MapData.VertexData[][] vm,
            MapData.VertexData v, int x, int y)
        {
            int vxMax = vm.Length;
            int vyMax = vm[0].Length;
            int cx = Math.Min(Math.Max(x, 0), vxMax - 1);
            int cy = Math.Min(Math.Max(y, 0), vyMax - 1);

            data.vertices.Add(mapData.VertexWaterPosition(vm[cx][cy], cx, cy) - position);
            data.normals.Add(Vector3.up);
            data.uvs.Add(mapData.VertexUV(cx, cy));
            data.colors.Add(vm[cx][cy].water > 0 ? Color.white : new Color(0f, 0f, 0f, 0f));
        }

        // ───────────── 构建结果应用（主线程）─────────────

        /// <summary>把构建产物写入 Mesh / Collider / Renderer。</summary>
        void ApplyBuildResult()
        {
            // 渲染开关不在这里处理：统一由 Update 每帧对齐（见 Update 里的"渲染开关每帧对齐"段）
            bool isEditor = Tools.MapEditor.IsEditOn;

            // ---- 地表 ----
            if (terrainMesh != null && terrainBuild != null)
                FillMesh(terrainMesh, terrainBuild);

            // ---- 碰撞体（首次构建时烘焙一次，之后 LOD 往返不再重烘）----
            if (!colliderBaked || isEditor)
            {
                if (terrainCollideBuild != null && terrainColliderMesh != null && terrainCollider != null)
                {
                    FillMesh(terrainColliderMesh, terrainCollideBuild);
                    terrainCollider.sharedMesh = terrainColliderMesh;
                }
                if (waterCollideBuild != null && waterColliderMesh != null && waterCollider != null)
                {
                    if (waterCollideBuild.triangles.Count > 0)
                    {
                        FillMesh(waterColliderMesh, waterCollideBuild);
                        waterCollider.sharedMesh = waterColliderMesh;
                    }
                    else
                    {
                        waterCollider.sharedMesh = null;
                    }
                }
                colliderBaked = true;
            }

            // ---- 水面（本块无水时保留节点、清空几何；渲染开关同样交给 Update 对齐）----
            if (waterMesh != null && waterBuild != null)
            {
                if (waterBuild.triangles.Count > 0)
                    FillMesh(waterMesh, waterBuild);
                else
                    waterMesh.Clear(false);
            }

            // 碰撞体数据应用完即可丢弃
            terrainCollideBuild = null;
            waterCollideBuild = null;
            threadBuildDone = false;
            threadLoadStart = false;
            lodPending = false;
        }

        /// <summary>把一套网格数据写入 Mesh（顶点 &lt; 65535 用 16 位索引省内存）。</summary>
        static void FillMesh(Mesh mesh, MeshBuildData data)
        {
            mesh.Clear(false);
            if (data.vertices.Count < 65535)
                mesh.indexFormat = IndexFormat.UInt16;
            else
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(data.vertices);
            mesh.SetNormals(data.normals);
            mesh.SetUVs(0, data.uvs);
            mesh.SetColors(data.colors);
            mesh.SetTriangles(data.triangles, 0, true);     // calculateBounds = true
        }

        // ───────────── 可见性 / LOD ─────────────

        /// <summary>
        /// 地形块渲染开关（默认 true）。远景 Instanced 渲染（<see cref="TerrainCellBatchRenderer"/>）
        /// 接管时由 <see cref="MapTerrain"/> 置 false：只关 MeshRenderer，collider 保留供点击拾取。
        ///
        /// 【为什么转发到 MapCell】两套块实现并存后，调用方（MapTerrain / TerrainSangoBootstrap）
        /// 不应该关心当前跑的是哪一套。统一由老类 <see cref="MapCell"/> 持有唯一一份开关，
        /// 本类只做转发，这样任何一处赋值对两种块同时生效，不会出现"新块关了、老块还开着"。
        /// </summary>
        public static bool TerrainRenderEnabled
        {
            get { return MapCell.TerrainRenderEnabled; }
            set { MapCell.TerrainRenderEnabled = value; }
        }

        public bool visible
        {
            get { return _visible; }
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    _visibleChanged = true;

                    // 刚变可见的块需要立即纳入地形的逐帧处理表：
                    // 本帧 / 下一帧要完成渲染器开关，并在必要时启动网格的异步构建。
                    if (value && map != null && map.mapTerrain != null)
                        map.mapTerrain.AddFrameUpdateCell(this);
                }
            }
        }

        // ───────────── IMapCell 显式实现 ─────────────
        // lod / idleTime / inFrameUpdateList 在本类是公共字段（与老 MapCell 保持一致），
        // 字段无法实现接口属性，这里用显式实现做一次桥接，不污染公共 API。

        int IMapCell.lod { get { return lod; } set { lod = value; } }
        float IMapCell.idleTime { get { return idleTime; } set { idleTime = value; } }
        bool IMapCell.inFrameUpdateList { get { return inFrameUpdateList; } set { inFrameUpdateList = value; } }

        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 scale { get; set; }
        public Vector2Int coords { get; set; }
        public MapRender manager { get { return map; } set { map = value; } }
        public bool remainInView { get; set; }

        public Tools.Rect bounds { get; set; }
        public Sango.Tools.Rect worldBounds
        {
            get
            {
                Vector3 pos = position;
                return new Tools.Rect(pos.z, pos.x, bounds.width, bounds.height);
            }
        }
        public int objId { get; set; }
        public int objType { get; set; }
        public int modelId { get; set; }
        public string modelAsset { get; set; }
        public int bindId { get; set; }
        public bool selectable { get; set; }
        public bool isStatic { get; set; }

        public void EditorShow(bool b) { }

        /// <summary>
        /// 切换地形描边材质（material 为 null 表示恢复单材质、关闭描边）。
        /// 只影响地表节点；描边本体由 URP Renderer 的 OutlineOpaque Feature 按 LayerMask 绘制。
        /// </summary>
        /// <param name="material">描边材质；null 表示还原为单材质</param>
        public void SetOutlineShow(Material material)
        {
            if (terrainRenderer == null)
                return;

            if (material == null)
            {
                // 单材质（旧实现里 i < maxLayer - 1 时才这样做，水面从不描边）
                terrainRenderer.sharedMaterials = new Material[1] { terrainRenderer.sharedMaterial };
            }
            else
            {
                terrainRenderer.sharedMaterials = new Material[2]
                {
                    terrainRenderer.sharedMaterial,
                    material
                };
            }
        }

        /// <summary>
        /// 切换网格精度档位（0 = 原始，l = 步长 1&lt;&lt;l 抽稀，上限 <see cref="MaxLod"/>）并异步重建。
        ///
        /// 【什么时候真的重建】只在"已经建好网格且当前可见"时重建：
        ///   · 还没建过网格的块，下次 Init 自然按新 lod 构建；
        ///   · 不可见的块只记 <see cref="lodPending"/>，重新可见后由 Update 补建（旧缺陷①修复点）；
        ///   · 正在后台构建的块不打断，构建完成后 Update 会检查 pending 再补一轮。
        /// </summary>
        /// <param name="l">精度档位，0~<see cref="MaxLod"/></param>
        public void SetLod(int l)
        {
            l = Math.Max(0, Math.Min(l, MaxLod));
            if (lod == l && !lodPending)
                return;

            lod = l;

            if (map == null || terrainNode == null || !IsValid() || threadLoadStart || !_visible)
            {
                lodPending = true;
                return;
            }

            AsyncPrepareDatas();
        }

        /// <summary>
        /// 切换"是否描边"的层档位（远距离 LOD 用）。由 MapRenderLod 在相机距离翻转时调用。
        /// 还没创建节点的块只记状态，等 Init 时按新档位建层。
        /// </summary>
        /// <param name="noOutline">true 切到 NoOutLine 层</param>
        public void SetNoOutLineLayer(bool noOutline)
        {
            if (noOutLineLayer == noOutline)
                return;

            noOutLineLayer = noOutline;
            ApplyLayer(CurrentLayer);
        }

        /// <summary>把当前层档位写到根节点与两个子节点（Collider 在子节点上）</summary>
        void ApplyLayer(int layer)
        {
            if (terrainNode != null && terrainNode.layer != layer)
                terrainNode.layer = layer;
            if (waterNode != null && waterNode.layer != layer)
                waterNode.layer = layer;
        }

        public bool IsInThreadLoad()
        {
            return threadLoadStart;
        }

        public bool IsValid()
        {
            return terrainNode != null;
        }

        public override void Update()
        {
            if (Tools.MapEditor.IsEditOn)
            {
                _visible = true;
                _visibleChanged = true;
            }

            if (_visibleChanged)
                _visibleChanged = false;

            // 渲染开关【每帧对齐】，不依赖"可见性翻转 / 构建完成"等任何时序：
            //   · 可见 且 地形渲染启用（远景 Instanced 接管时 TerrainRenderEnabled=false），
            //     且网格里确实有顶点数据 → 打开；否则关闭。
            //   · 旧实现只在 _visibleChanged 与 ApplyBuildResult 两个时刻打开 renderer，
            //     任一条路径卡住（构建线程异常、材质空壳等）都会表现为"整块地形凭空消失"，
            //     且没有任何日志线索。逐帧对齐把这类问题压缩到"最多一帧"。
            //   注：远景接管时只关 MeshRenderer，collider 保留供点击拾取
            //   （GameController.CheckMouseIsOnMapCell 靠 Map 层 Physics.Raycast 命中）。
            bool shouldRender = _visible && TerrainRenderEnabled;
            if (terrainRenderer != null)
            {
                bool want = shouldRender && terrainMesh != null && terrainMesh.vertexCount > 0;
                if (terrainRenderer.enabled != want)
                    terrainRenderer.enabled = want;
            }
            if (waterRenderer != null)
            {
                bool want = shouldRender && waterMesh != null && waterMesh.vertexCount > 0;
                if (waterRenderer.enabled != want)
                    waterRenderer.enabled = want;
            }

            // 后台构建完成 → 主线程应用
            if (threadLoadStart && threadBuildDone)
            {
                if (!threadCancelRequest)
                    ApplyBuildResult();
                else
                {
                    threadBuildDone = false;
                    threadLoadStart = false;
                }
            }

            // LOD 待重建补建（旧缺陷①修复点：不可见时记 pending，重新可见后在这里补建）
            if (lodPending && _visible && IsValid() && !IsInThreadLoad())
                SetLod(lod);

            // 构建兜底：可见但没网格时补建。放在 _visibleChanged 之外 —— 远景接管期间
            // （TerrainRenderEnabled=false）新可见的块跳过构建（渲染不依赖 CPU 网格），
            // 退出远景后由这里补建；否则这些块会永远停留在"可见但没网格"的状态。
            // 【两个分支缺一不可】
            //   · 节点还没建过 → Init（建节点 + 网格）+ 异步构建；
            //   · 节点在、但网格被空闲卸载回收了（ReleaseMesh 把 Mesh 归还进池）→ 只补网格。
            // 旧实现只判 !IsValid()（= 节点不存在），第二种情况会永远停在"可见但画不出来"，
            // 表现为：拉远一会儿、移动镜头、再拉近，那些被回收过的地块整片空白且不再恢复。
            if (_visible && TerrainRenderEnabled && !IsInThreadLoad())
            {
                if (!IsValid())
                {
                    Init();
                    AsyncPrepareDatas();
                }
                else if (!HasMesh())
                {
                    AsyncPrepareDatas();
                }
            }
        }

        public bool Overlaps(Sango.Tools.Rect rect)
        {
            return worldBounds.Overlaps(rect);
        }
        public void OnClick()
        {
            throw new NotImplementedException();
        }

        public void OnPointerEnter()
        {
            throw new NotImplementedException();
        }

        public void OnPointerExit()
        {
            throw new NotImplementedException();
        }

        public void SetParent(Transform parent) { terrainNode.transform.SetParent(parent); }
        public void SetParent(Transform parent, bool worldPositionStays) { terrainNode.transform.SetParent(parent, worldPositionStays); }
        public GameObject GetGameObject()
        {
            return terrainNode;
        }

        public void Destroy()
        {
            Clear();
        }

        public void CreateModel(string meshFile, string textureFile, string shaderName, bool isShareMat = true)
        {
            throw new NotImplementedException();
        }

        public void CreateModel(string packagePath, string assetName)
        {
            throw new NotImplementedException();
        }

        public void CreateModel(string assetName)
        {
            throw new NotImplementedException();
        }

        public void CreateModel(UnityEngine.Object modelObj)
        {
            throw new NotImplementedException();
        }

        public void ChangeModel(string newAsset)
        {
            throw new NotImplementedException();
        }

        public void ReLoadModels(bool checkAsset = true)
        {
            throw new NotImplementedException();
        }

        public void ClearModels()
        {
            throw new NotImplementedException();
        }

        public void ReCheckVisible()
        {
            throw new NotImplementedException();
        }
    }
}
