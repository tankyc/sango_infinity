using Sango.Tools;
using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Sango.Render
{
    public class MapTerrain : MapProperty
    {
        public int cellSize = 64;
        public MapCell[] terrainCells;

        public MapTerrain(MapRender map) : base(map)
        {

        }

        public override void Init()
        {
            base.Init();

            // 【运行时不再创建旧地形块】
            // 地形渲染已由 TerrainSango 接管（高度纹理 + 顶点着色器位移），地图点击也改走
            // TerrainSangoBootstrap.TryGetMapHit 的高度场求交，旧地形块在运行时已无任何消费者：
            //   · 它唯一的剩余价值是 meshCollider 充当点击靶子，而点击已不依赖它；
            //   · 全图数千块的 GameObject/Mesh 构建反而会白烧内存与首帧时间。
            // 编辑器（MapEditor.IsEditOn）必须保留：地形笔刷要靠 Map 层 Physics.Raycast 命中
            // MapCell 的 meshCollider 定位格子（MapEditor.QueryHeight、ModelBrush、MapLabelBrush
            // 都是这么取点的），改动后的可视化刷新也依赖 terrainCells（TerrainBrush、TerrainEditCommand）。
            // 另外把 TerrainSangoBootstrap.Enabled 关掉（回退）时也要建，否则回退后没有地形可看可直接点。
            if (!MapEditor.IsEditOn && TerrainSango.TerrainSangoBootstrap.Enabled)
            {
                terrainCells = Array.Empty<MapCell>();
                return;
            }

            CreateLegacyCells();
        }

        /// <summary>
        /// 创建旧地形块。三个调用场景：
        ///   ① 编辑器态（MapEditor.IsEditOn）—— 地形笔刷要用 collider 定位格子；
        ///   ② 回退态（TerrainSangoBootstrap.Enabled 关闭）—— 回到旧管线；
        ///   ③ **运行时兜底** —— 新地形接管失败（着色器被剥离、设备不支持纹理数组等）时，
        ///      由 TerrainSangoBootstrap 调回来补建，保证玩家至少能看到地形。
        ///
        /// 重复调用安全：已经有块时直接返回。
        /// </summary>
        public void CreateLegacyCells()
        {
            if (map == null || map.mapData == null) return;
            if (terrainCells != null && terrainCells.Length > 0) return;

            int wCount = map.mapData.vertex_width / cellSize;
            int hCount = map.mapData.vertex_height / cellSize;
            if (wCount <= 0 || hCount <= 0) return;

            terrainCells = new MapCell[wCount * hCount];

            for (int y = 0; y < hCount; y++)
            {
                for (int x = 0; x < wCount; x++)
                {
                    MapCell cell = new MapCell(map, x * cellSize, y * cellSize, cellSize, cellSize);
                    // 重载地图 / 回退重建时，LOD 层档位可能仍是远处的设置，新块要直接沿用
                    cell.SetNoOutLineLayer(noOutLineState);
                    terrainCells[y * wCount + x] = cell;
                    map.AddStatic(cell);
                }
            }
        }

        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(cellSize);
        }
        // 必须在layer后面
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            cellSize = reader.ReadInt32();
            Rebuild();
        }

        public override void Clear()
        {
            base.Clear();

            // 队列里可能还持有即将销毁的块，先清掉；目标档位复位为平台默认值
            lodRebuildQueue.Clear();
            targetLod = PlatformDefaultLod;

            if (terrainCells == null) return;
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                map.Remove(cell);
                cell.Clear();
            }
            terrainCells = null;
            frameUpdateCells.Clear();
        }

        public override void UpdateRender()
        {

        }

        public void Rebuild()
        {
            // 运行时 terrainCells 为空数组（旧地形不创建），Clear 之后还可能为 null，两种都要挡住
            if (terrainCells == null) return;

            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;

                //if (!MapEditor.IsEditOn)
                //{
                //    cell.Init();
                //    //cell.PrepareDatas();
                //    //duration = Time.realtimeSinceStartup - start;
                //    //Debug.LogError($"cell.Init, PrepareDatas耗时：{duration:F4}秒");
                //    //start = Time.realtimeSinceStartup;

                //    Vector2Int startCoords = cell.startCoords;
                //    Vector2Int quadBounds = cell.quadBounds;
                //    Vector3 position = cell.position;
                //    Vector2 MapUVPiece = map.mapData.MapUVPiece;
                //    int vertex_x_max = map.mapData.vertex_x_max;
                //    int vertex_y_max = map.mapData.vertex_y_max;
                //    int maxLayer = map.mapLayer.layerDatas.Length;
                //    int lodL = 1 << lod;
                //    int quadSize = map.mapData.quadSize;

                //    int cell_vertex_x_max = quadBounds.x + 1;
                //    int cell_vertex_y_max = quadBounds.y + 1;
                //    int cellmaxCount = cell_vertex_x_max * cell_vertex_y_max;

                //    for (int j = 0; j < maxLayer; j++)
                //    {
                //        int layer = j;
                //        bool layerIsWater = (layer == (maxLayer - 1));

                //        NativeArray<Vector3> vertexCache = new NativeArray<Vector3>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<Color> colorCache = new NativeArray<Color>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<int> triangleCache = new NativeArray<int>(cellmaxCount * 6, Allocator.Persistent);
                //        NativeArray<Vector2> uvCache = new NativeArray<Vector2>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<Vector3> normalCache = new NativeArray<Vector3>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<int> xIndexCache = new NativeArray<int>(2, Allocator.Persistent);
                //        NativeArray<int> yIndexCache = new NativeArray<int>(cell_vertex_y_max, Allocator.Persistent);
                //        NativeArray<bool> diffyIndexCache = new NativeArray<bool>(cell_vertex_y_max, Allocator.Persistent);
                //        NativeArray<int> cacheIndexs = new NativeArray<int>(5, Allocator.Persistent);

                //        var job = new LayerJob
                //        {
                //            vertexCache = vertexCache,
                //            colorCache = colorCache,
                //            triangleCache = triangleCache,
                //            uvCache = uvCache,
                //            normalCache = normalCache,
                //            xIndexCache = xIndexCache,
                //            yIndexCache = yIndexCache,
                //            diffyIndexCache = diffyIndexCache,
                //            layer = layer,
                //            lodL = lodL,
                //            maxLayer = maxLayer,
                //            layerIsWater = layerIsWater,
                //            vertexMap = map.mapData.vertexMap,
                //            startCoords = startCoords,
                //            quadBounds = quadBounds,
                //            position = position,
                //            MapUVPiece = MapUVPiece,
                //            vertex_x_max = vertex_x_max,
                //            vertex_y_max = vertex_y_max,
                //            quadSize = quadSize,
                //            cacheIndexs = cacheIndexs,
                //        };
                //        job.Schedule().Complete();

                //        Mesh mesh = cell.mesh[layer];

                //        mesh.SetVertices(vertexCache.ToArray(), 0, cacheIndexs[1], LayerMeshData.Flags);
                //        mesh.SetUVs(0, uvCache.ToArray(), 0, cacheIndexs[3], LayerMeshData.Flags);
                //        mesh.SetColors(colorCache.ToArray(), 0, cacheIndexs[4], LayerMeshData.Flags);
                //        mesh.SetNormals(normalCache.ToArray(), 0, cacheIndexs[2], LayerMeshData.Flags);
                //        mesh.SetTriangles(triangleCache.ToArray(), 0, cacheIndexs[0], 0, true);
                //        mesh.RecalculateBounds();

                //        if (cell.meshCollider[layer] == null)
                //        {
                //            MeshCollider co = cell.meshRenderer[layer].gameObject.AddComponent<MeshCollider>();
                //            co.sharedMesh = cell.mesh[layer];
                //            cell.meshCollider[layer] = co;
                //        }
                //        else
                //        {
                //            cell.meshCollider[layer].sharedMesh = cell.mesh[layer];
                //        }


                //        if (!cell.meshRenderer[layer].enabled)
                //            cell.meshRenderer[layer].enabled = cell.visible;

                //        vertexCache.Dispose();
                //        colorCache.Dispose();
                //        triangleCache.Dispose();
                //        uvCache.Dispose();
                //        normalCache.Dispose();
                //        xIndexCache.Dispose();
                //        yIndexCache.Dispose();
                //        diffyIndexCache.Dispose();

                //    }

                //    cell.threadBeginLayer = cell.mesh.Length;

                //}
                //else
                //{
                    if (cell.IsValid())
                    {
                        cell.PrepareDatas();
                    }
                //}
            }

            //duration = Time.realtimeSinceStartup - start;
            //Debug.LogError($"Schedule耗时：{duration:F4}秒");
        }



        /// <summary>
        /// 离开视野后网格再保留多久才释放（秒）。
        /// 留一段迟滞，避免镜头来回平移时反复重建（单块重建实测约 32ms）。
        /// 设为 &lt;= 0 可关闭空闲卸载。
        /// </summary>
        public float meshUnloadDelay = 6f;

        /// <summary>
        /// 逐帧需要处理的块的**工作集**：刚进入视野 / 正在后台分层构建 / 已离开视野但等待空闲卸载的块。
        ///
        /// 旧实现每帧遍历全部 <see cref="terrainCells"/>（大地图可达数千块），而其中绝大多数
        /// 既不可见也没有网格、逐帧什么都不做。改成只遍历工作集后，每帧遍历量从"全图块数"
        /// 降到"当前视野 + 待卸载块数"，且不改变任何语义：
        ///   · 变可见由 <see cref="MapCell.visible"/> setter 登记（见 <see cref="AddFrameUpdateCell"/>）；
        ///   · 网格被回收或块被清理时解登记（<see cref="RemoveFrameUpdateCell"/>）。
        /// </summary>
        readonly System.Collections.Generic.List<MapCell> frameUpdateCells = new System.Collections.Generic.List<MapCell>();

        /// <summary>把一个块登记进逐帧工作集（重复登记会被忽略）。</summary>
        internal void AddFrameUpdateCell(MapCell cell)
        {
            if (cell == null || cell.inFrameUpdateList)
                return;

            cell.inFrameUpdateList = true;
            frameUpdateCells.Add(cell);
        }

        /// <summary>把一个块移出逐帧工作集（惰性移除：仅解标记，实际条目在下次遍历时顺带剔除）。</summary>
        internal void RemoveFrameUpdateCell(MapCell cell)
        {
            if (cell == null)
                return;

            cell.inFrameUpdateList = false;
        }

        // ───────────────────────────── 远距离 LOD ─────────────────────────────

        /// <summary>
        /// 平台默认精度档位，必须与 <see cref="MapCell.lod"/> 的初始值一致。
        /// 移动端本来就是隔点采样（1），近景回档时要回到 1 而不是 0，否则等于凭空提高精度、掉帧。
        /// </summary>
        public static int PlatformDefaultLod
        {
            get
            {
#if UNITY_ANDROID || UNITY_IPHONE
                return 1;
#else
                return 0;
#endif
            }
        }

        /// <summary>
        /// 当前目标精度档位（0 = 原始，1 = 隔点，2 = 隔三点）。
        /// 由 <see cref="MapRenderLod"/> 在相机距离翻转时设置，块级精度按此重建。
        /// </summary>
        int targetLod = PlatformDefaultLod;

        /// <summary>
        /// 等待按新精度重建的块队列。
        /// 重建一块要重跑该块所有图层的顶点构建（实测单块数十毫秒级），
        /// 相机拉远时可见块可能有上百个，一次性全重建会直接卡住主线程，所以排队分摊到多帧。
        /// </summary>
        readonly System.Collections.Generic.Queue<MapCell> lodRebuildQueue
            = new System.Collections.Generic.Queue<MapCell>();

        /// <summary>每帧最多重建多少块。取小值换平滑，取大值换降级完成速度。</summary>
        public int lodRebuildPerFrame = 4;

        /// <summary>
        /// 当前目标精度档位（只读）。
        /// <see cref="MapCell.Init"/> 建块时直接采用它，否则"降级之后才进入视野的块"仍会是全精度，
        /// 远景下的顶点削减会随时间慢慢失效。
        /// </summary>
        public int TargetLod { get { return targetLod; } }

        /// <summary>
        /// 设置地形精度档位（远处降精度）。只影响"已经建过网格的块"的重建节奏；
        /// 还没建过网格的块会在下次 Init 时直接按新档位构建，不额外花代价。
        /// </summary>
        /// <param name="lod">0 = 原始，1 = 隔点，2 = 隔三点</param>
        public void SetCellLod(int lod)
        {
            if (targetLod == lod)
                return;

            targetLod = lod;

            if (terrainCells == null)
                return;

            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null)
                    continue;

                // 没网格的块 SetLod 只会记一个字段，入队也无妨；但全图数千块入队纯属浪费，直接跳过
                if (!cell.HasMesh())
                    continue;

                lodRebuildQueue.Enqueue(cell);
            }
        }

        /// <summary>当前描边层档位。重载地图 / 回退重建时用它初始化新建的块。</summary>
        bool noOutLineState;

        /// <summary>
        /// 统一切换地形块的描边层档位（远距离时让 URP 的描边 Feature 直接跳过地形）。
        /// 只在 LOD 翻转时调用一次，全图遍历的开销可以接受。
        /// </summary>
        /// <param name="noOutline">true 切到 MapNoOutLine 层</param>
        public void SetNoOutLineLayer(bool noOutline)
        {
            noOutLineState = noOutline;

            if (terrainCells == null)
                return;

            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null)
                    continue;

                cell.SetNoOutLineLayer(noOutline);
            }
        }

        /// <summary>消费 LOD 重建队列（每帧限量）</summary>
        void ProcessLodRebuild()
        {
            if (lodRebuildQueue.Count == 0)
                return;

            int budget = lodRebuildPerFrame > 0 ? lodRebuildPerFrame : 1;
            while (budget > 0 && lodRebuildQueue.Count > 0)
            {
                budget--;

                MapCell cell = lodRebuildQueue.Dequeue();
                if (cell == null)
                    continue;

                // 不可见的块在 SetLod 里只会更新字段（不重建），等它再次进入视野时按新档位构建
                cell.SetLod(targetLod);
            }
        }

        public override void Update()
        {
            if (terrainCells == null) return;

            // 远距离降精度：按帧预算把已建好网格的块重建为低精度
            ProcessLodRebuild();

            float dt = Time.deltaTime;
            bool allowUnload = meshUnloadDelay > 0f;

            // 编辑态下所有块都要强制可见并保持重建，回退到全量扫描（编辑器性能不敏感，行为保持不变）
            if (MapEditor.IsEditOn)
            {
                for (int i = 0; i < terrainCells.Length; i++)
                {
                    MapCell cell = terrainCells[i];
                    if (cell == null) continue;
                    cell.Update();
                }
                return;
            }

            // 从后往前遍历：解登记（含被清理）的块就地剔除，尾部删除接近 O(1)
            for (int i = frameUpdateCells.Count - 1; i >= 0; i--)
            {
                MapCell cell = frameUpdateCells[i];
                if (cell == null || !cell.inFrameUpdateList)
                {
                    frameUpdateCells.RemoveAt(i);
                    continue;
                }

                cell.Update();

                // 既不可见、又没网格、也没在加载：本块已无事可做，解登记避免长期滞留在工作集里
                // （相机快速平移时，块可能在"登记变可见"与"真正 Init"之间又被置回不可见）。
                if (!cell.visible && !cell.IsInThreadLoad() && !cell.HasMesh())
                {
                    cell.inFrameUpdateList = false;
                    continue;
                }

                if (!allowUnload) continue;

                // 可见 / 正在后台分层构建的块不动
                if (cell.visible || cell.IsInThreadLoad())
                {
                    cell.idleTime = 0f;
                    continue;
                }
                if (!cell.HasMesh()) continue;

                cell.idleTime += dt;
                if (cell.idleTime >= meshUnloadDelay)
                    cell.ReleaseMesh();
            }
        }

        public override void UpdateImmediate()
        {
            // 运行时没有旧地形块，这里直接返回（编辑器态才有块需要强制可见并构建）
            if (terrainCells == null) return;

            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                cell.visible = true;
                cell.PrepareDatas();
            }
        }
    }


    //[BurstCompile]
    //public struct LayerJob : IJob
    //{
    //    public NativeArray<MapData.VertexData> vertexMap;

    //    public NativeArray<Vector3> vertexCache;
    //    public NativeArray<Color> colorCache;
    //    public NativeArray<int> triangleCache;
    //    public NativeArray<Vector2> uvCache;
    //    public NativeArray<Vector3> normalCache;

    //    public NativeArray<int> xIndexCache;
    //    public NativeArray<int> yIndexCache;
    //    public NativeArray<bool> diffyIndexCache;

    //    public NativeArray<int> cacheIndexs;

    //    public readonly static Vector2Int[] NeighborVertex = new Vector2Int[] {
    //        new Vector2Int(1, 0),
    //        new Vector2Int(1, 1),
    //        new Vector2Int(0, 1),
    //        new Vector2Int(-1, 0),
    //        new Vector2Int(-1, -1),
    //        new Vector2Int(0, -1),
    //    };

    //    public int layer;
    //    public int lodL;
    //    public int maxLayer;
    //    public bool layerIsWater;
    //    public Vector2Int startCoords;
    //    public Vector2Int quadBounds;
    //    public Vector3 position;
    //    public Vector2 MapUVPiece;
    //    public int vertex_x_max;
    //    public int vertex_y_max;
    //    public int quadSize;

    //    static readonly Color color_white = Color.white;
    //    static readonly Color color_black = new Color(0, 0, 0, 0);
    //    public int triangleCacheIndex;
    //    public int vertexCacheIndex;
    //    public int normalCacheIndex;
    //    public int uvCacheIndex;
    //    public int colorCacheIndex;

    //    public Vector2 VertexUV(MapData.VertexData data, int x, int y)
    //    {
    //        return new Vector2(x * MapUVPiece.x, y * MapUVPiece.y);
    //    }
    //    public Vector3 VertexPosition(MapData.VertexData data, int x, int y)
    //    {
    //        return new Vector3(y * quadSize, data.height * 0.5f, x * quadSize);
    //    }
    //    public Vector3 VertexWaterPosition(MapData.VertexData data, int x, int y)
    //    {
    //        if (data.water == 0)
    //        {
    //            int lx = x - 1;
    //            if (lx > 0)
    //            {
    //                MapData.VertexData d = vertexMap[lx + vertex_x_max * y];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }
    //            }
    //            int uy = y - 1;
    //            if (uy > 0)
    //            {
    //                MapData.VertexData d = vertexMap[x + vertex_x_max * uy];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }

    //                if (lx > 0)
    //                {
    //                    d = vertexMap[lx + vertex_x_max * uy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }

    //            }
    //            int rx = x + 1;
    //            if (rx < vertex_x_max)
    //            {
    //                MapData.VertexData d = vertexMap[rx + vertex_x_max * y];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }

    //                if (uy > 0)
    //                {
    //                    d = vertexMap[rx + vertex_x_max * uy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }
    //            }
    //            int dy = y + 1;
    //            if (dy < vertex_y_max)
    //            {
    //                MapData.VertexData d = vertexMap[x + vertex_x_max * dy];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }

    //                if (rx < vertex_x_max)
    //                {
    //                    d = vertexMap[rx + vertex_x_max * dy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }

    //                if (lx > 0)
    //                {
    //                    d = vertexMap[lx + vertex_x_max * dy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }
    //            }
    //        }
    //        return new Vector3(y * quadSize, data.water * 0.5f, x * quadSize);
    //    }
    //    public Vector3 VertexNormal(MapData.VertexData data, int x, int y)
    //    {
    //        Vector3 vdPos = VertexPosition(data, x, y);
    //        Vector3 normal = Vector3.zero;

    //        for (int z = 0; z < 6; z++)
    //        {
    //            int next = z + 1;
    //            if (next == 6)
    //                next = 0;

    //            Vector2Int neighbor_z = NeighborVertex[z];
    //            int neighbor_z_x = neighbor_z.x + x;
    //            int neighbor_z_y = neighbor_z.y + y;
    //            Vector2Int neighbor_next = NeighborVertex[next];
    //            int neighbor_next_x = neighbor_next.x + x;
    //            int neighbor_next_y = neighbor_next.y + y;

    //            if (neighbor_z_x >= 0 && neighbor_z_x < vertex_x_max && neighbor_z_y >= 0 && neighbor_z_y < vertex_y_max &&
    //                neighbor_next_x >= 0 && neighbor_next_x < vertex_x_max && neighbor_next_y >= 0 && neighbor_next_y < vertex_y_max)
    //            {
    //                MapData.VertexData n_z = vertexMap[neighbor_z_x + vertex_x_max * neighbor_z_y];
    //                Vector3 pos_z = VertexPosition(n_z, neighbor_z_x, neighbor_z_y);

    //                MapData.VertexData n_next = vertexMap[neighbor_next_x + vertex_x_max * neighbor_next_y];
    //                Vector3 pos_n_next = VertexPosition(n_next, neighbor_next_x, neighbor_next_y);

    //                normal += Vector3.Cross(pos_z - vdPos, pos_n_next - vdPos);
    //            }
    //        }
    //        normal.Normalize();
    //        return normal;
    //    }

    //    void AddVertex(ref MapData.VertexData vtex, int x, int y, int layer, int lowestLayer, bool layerIsWater)
    //    {
    //        // color
    //        if (vtex.textureIndex == layer || layer == lowestLayer || (layerIsWater && vtex.water > 0))
    //        {
    //            colorCache[cacheIndexs[colorCacheIndex]++] = (color_white);
    //        }
    //        else
    //        {
    //            colorCache[cacheIndexs[colorCacheIndex]++] = (color_black);
    //        }
    //        uvCache[cacheIndexs[uvCacheIndex]++] = (VertexUV(vtex, x, y));

    //        if (layerIsWater)
    //        {
    //            vertexCache[cacheIndexs[vertexCacheIndex]++] = (VertexWaterPosition(vtex, x, y) - position);
    //            normalCache[cacheIndexs[normalCacheIndex]++] = (Vector3.up);

    //        }
    //        else
    //        {
    //            vertexCache[cacheIndexs[vertexCacheIndex]++] = (VertexPosition(vtex, x, y) - position);
    //            normalCache[cacheIndexs[normalCacheIndex]++] = (VertexNormal(vtex, x, y));
    //        }
    //    }

    //    public void Execute()
    //    {
    //        triangleCacheIndex = 0;
    //        vertexCacheIndex = 1;
    //        normalCacheIndex = 2;
    //        uvCacheIndex = 3;
    //        colorCacheIndex = 4;

    //        int _vertex_x_max = quadBounds.x + 1;
    //        int _vertex_y_max = quadBounds.y + 1;
    //        int maxCount = _vertex_x_max * _vertex_y_max;

    //        byte bLayer = (byte)layer;
    //        int x_end = startCoords.x + quadBounds.x;
    //        int y_end = startCoords.y + quadBounds.y;
    //        int maxIndex = 0;
    //        for (int x = startCoords.x; x < x_end; x += lodL)
    //        {
    //            xIndexCache[0] = yIndexCache[0];
    //            xIndexCache[1] = 0;

    //            for (int k = 0; k < diffyIndexCache.Length; k++)
    //                diffyIndexCache[k] = false;

    //            for (int y = startCoords.y; y < y_end; y += lodL)
    //            {

    //                int xL = x + lodL;
    //                int yL = y + lodL;
    //                MapData.VertexData start0 = vertexMap[x + vertex_x_max * y];
    //                MapData.VertexData start1 = vertexMap[xL + vertex_x_max * y];
    //                MapData.VertexData start2 = vertexMap[xL + vertex_x_max * yL];
    //                MapData.VertexData start3 = vertexMap[x + vertex_x_max * yL];

    //                int normalY = y - startCoords.y;

    //                if ((!layerIsWater && start0.textureIndex != bLayer && start1.textureIndex != bLayer && start2.textureIndex != bLayer && start3.textureIndex != bLayer) ||
    //                    (layerIsWater && start0.water == 0 && start1.water == 0 && start2.water == 0 && start3.water == 0))
    //                {
    //                    xIndexCache[0] = 0;
    //                    xIndexCache[1] = 0;
    //                    continue;
    //                }

    //                int lowestLayer = layerIsWater ? 0 : Math.Min(Math.Min(Math.Min(start0.textureIndex, start1.textureIndex), start2.textureIndex), start3.textureIndex);

    //                int index0 = xIndexCache[0];
    //                if (index0 == 0)
    //                {
    //                    index0 = yIndexCache[normalY];
    //                    if (index0 == 0)
    //                    {
    //                        AddVertex(ref start0, x, y, layer, lowestLayer, layerIsWater);
    //                        index0 = maxIndex++;
    //                    }
    //                    else
    //                    {
    //                        if (lowestLayer == layer)
    //                        {
    //                            colorCache[index0] = color_white;
    //                        }
    //                    }
    //                }
    //                else
    //                {
    //                    if (lowestLayer == layer)
    //                    {
    //                        colorCache[index0] = color_white;
    //                    }
    //                }


    //                int index1 = xIndexCache[1];
    //                if (index1 == 0)
    //                {
    //                    AddVertex(ref start1, xL, y, layer, lowestLayer, layerIsWater);
    //                    index1 = maxIndex++;
    //                }
    //                else
    //                {
    //                    if (lowestLayer == layer)
    //                    {
    //                        colorCache[index1] = color_white;
    //                    }
    //                }

    //                yIndexCache[normalY] = index1;
    //                diffyIndexCache[normalY] = true;
    //                AddVertex(ref start2, xL, yL, layer, lowestLayer, layerIsWater);
    //                int index2 = maxIndex++;
    //                xIndexCache[1] = index2;


    //                int index3 = yIndexCache[normalY + lodL];
    //                if (index3 == 0)
    //                {
    //                    AddVertex(ref start3, x, yL, layer, lowestLayer, layerIsWater);
    //                    index3 = maxIndex++;
    //                }
    //                else
    //                {
    //                    if (lowestLayer == layer)
    //                    {
    //                        colorCache[index3] = color_white;
    //                    }
    //                }

    //                xIndexCache[0] = index3;
    //                yIndexCache[normalY + lodL] = index2;
    //                diffyIndexCache[normalY + lodL] = true;

    //                triangleCache[triangleCacheIndex++] = (index0);
    //                triangleCache[triangleCacheIndex++] = (index2);
    //                triangleCache[triangleCacheIndex++] = (index3);
    //                triangleCache[triangleCacheIndex++] = (index1);
    //                triangleCache[triangleCacheIndex++] = (index2);
    //                triangleCache[triangleCacheIndex++] = (index0);
    //            }

    //            for (int k = 0; k < diffyIndexCache.Length; ++k)
    //            {
    //                if (!diffyIndexCache[k])
    //                    yIndexCache[k] = 0;
    //            }
    //        }
    //    }
    //}

}
