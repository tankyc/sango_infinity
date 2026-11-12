using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango.Render
{
    /// <summary>
    /// 远景地形 Instanced 批渲染器（MapCell 改造版，原版无对应实现）。
    ///
    /// 相机拉远（MapRenderLod 触发）后接管全部地形渲染：所有 MapCell 的 MeshRenderer 关闭，
    /// 改由本类以 DrawMeshInstanced 提交 —— 全体块共用同一张平面模板网格与同一个材质实例
    /// （MapCellTerrainAsset.TerrainInstanceMaterial），高度在顶点着色器里按 R8 高度纹理
    /// （MapCellTerrainAsset.HeightTex）位移，全图地形只要几个 draw call，供
    /// "无极缩放 / 顶视图全览"使用。模板按相机距离分档（33/17/9/5 顶点每边），
    /// 同帧全图同档 → 相邻块拓扑一致，无 CDLOD 式 T 型接缝。
    /// </summary>
    public sealed class TerrainCellBatchRenderer : IDisposable
    {
        /// <summary>远景地形模板网格（每个档位一份，顶点在 [0,1] 平面、Y 恒 0）。</summary>
        private Mesh[] terrainTemplates;

        /// <summary>各档位的相机距离阈值（≥ 阈值即切到更低档位）。</summary>
        private static readonly float[] LevelDistances = { 900f, 1800f, 3600f };

        /// <summary>
        /// 远景水面材质是否可用（材质由 <see cref="MapCellTerrainAsset.WaterInstanceMaterial"/> 提供）。
        ///
        /// 【为什么水面必须和地形一样按块实例化】旧实现是"全图一张 129×129 网格 + 顶点着色器
        /// 采样水面高度"：全图 1800×1000 个顶点格被压进 129×129 个顶点，每顶点跨十几到几十格。
        /// 相邻顶点一旦一个落在水域（高度 100）、一个落在陆地（高度 0），三角形就斜插进地形，
        /// 被深度测试剔掉 —— 表现正是"水体萎缩 + 高度突变"。改成按块实例化后，水面与地形
        /// 用同一份模板网格、同一批实例矩阵，精度完全一致，插值不会再跨过水陆边界。
        /// </summary>
        private bool waterReady;

        /// <summary>实例矩阵缓存（块位置固定，构建一次，逐帧只按可见性拷贝）。</summary>
        private Matrix4x4[] cellMatrices;

        /// <summary>逐帧提交缓冲（DrawMeshInstanced 上限 1023 实例/批）。</summary>
        private Matrix4x4[] submitMatrices = new Matrix4x4[1023];

        /// <summary>水面实例提交缓冲（与地形分开：只提交块内确实有水的实例）。</summary>
        private Matrix4x4[] submitWaterMatrices = new Matrix4x4[1023];

        /// <summary>每块是否有水域（Setup 时扫一次 vertexDatas，逐帧水面提交用它过滤）。</summary>
        private bool[] cellHasWater;

        /// <summary>视锥剔除平面缓冲（CalculateFrustumPlanes 复用，避免每帧分配）。</summary>
        private readonly Plane[] frustumPlanes = new Plane[6];

        /// <summary>块的可见性测试包围盒（构建一次复用）。</summary>
        private Bounds[] cellBounds;

        private MapRender map;
        private MapCellTerrainAsset asset;
        private bool enabled;

        /// <summary>远景批渲染是否已激活（由 MapTerrain.SetBatchRender 控制）。</summary>
        public bool Enabled { get { return enabled; } }

        /// <summary>初始化：构建模板网格与实例矩阵缓存。</summary>
        public void Setup(MapRender mapRender, MapCellTerrainAsset terrainAsset)
        {
            Release();
            map = mapRender;
            asset = terrainAsset;
            if (map == null || asset == null)
                return;

            terrainTemplates = new Mesh[] {
                BuildPlaneTemplate("TerrainCellBatch_L33", 33),
                BuildPlaneTemplate("TerrainCellBatch_L17", 17),
                BuildPlaneTemplate("TerrainCellBatch_L9", 9),
                BuildPlaneTemplate("TerrainCellBatch_L5", 5),
            };
            // 水面与地形共用同一套模板网格（[0,1] 平面 + 每块实例矩阵），不再单独建全图网格
            waterReady = asset.WaterInstanceMaterial != null;

            IMapCell[] cells = map.mapTerrain != null ? map.mapTerrain.terrainCells : null;
            if (cells != null && cells.Length > 0)
            {
                cellMatrices = new Matrix4x4[cells.Length];
                cellBounds = new Bounds[cells.Length];
                cellHasWater = new bool[cells.Length];
                float cellWorldSize = 64f * asset.QuadSize;
                for (int i = 0; i < cells.Length; i++)
                {
                    IMapCell cell = cells[i];
                    if (cell == null)
                        continue;

                    // 平移 = 块原点，缩放 = 块世界边长（顶点着色器据此反推世界 XZ）
                    cellMatrices[i] = Matrix4x4.TRS(cell.position, Quaternion.identity,
                        new UnityEngine.Vector3(cellWorldSize, 1f, cellWorldSize));

                    // 视锥剔除用的包围盒：地形最高约 _HeightRange(127.5)，放宽到 140 覆盖
                    cellBounds[i] = new Bounds(
                        new UnityEngine.Vector3(cell.position.x + cellWorldSize * 0.5f, 70f,
                            cell.position.z + cellWorldSize * 0.5f),
                        new UnityEngine.Vector3(cellWorldSize, 140f, cellWorldSize));

                    cellHasWater[i] = HasWaterInCell(cell);
                }
            }
        }

        /// <summary>
        /// 扫描一块地形内是否含水域（vertexDatas[x][y].water &gt; 0）。
        ///
        /// 【用途】远景水面与地形共用模板网格后，每块都要提交一遍 33×33 平面；
        /// 内陆地图绝大多数块是纯陆地，全量提交等于让水面三角形凭空翻倍。
        /// 这里在 Setup 时扫一次（块数固定、只跑一次），逐帧只提交有水块。
        ///
        /// 【取不到数据时返回 true】宁可多画，也不能因为索引异常让整片水面消失。
        /// </summary>
        /// <param name="cell">地形块</param>
        /// <returns>块内是否含水</returns>
        private bool HasWaterInCell(IMapCell cell)
        {
            if (map == null || map.mapData == null)
                return true;

            MapData mapData = map.mapData;
            MapData.VertexData[][] vm = mapData.vertexDatas;
            if (vm == null || vm.Length == 0 || vm[0] == null)
                return true;

            float quad = mapData.quadSize > 0f ? mapData.quadSize : 5f;
            // 块原点 = (vy * quadSize, vx * quadSize) → 反推顶点索引（见 MapCell 构造）
            int vx0 = Mathf.RoundToInt(cell.position.z / quad);
            int vy0 = Mathf.RoundToInt(cell.position.x / quad);
            int vxMax = vm.Length;
            int vyMax = vm[0].Length;
            int vx1 = Mathf.Min(vx0 + 64, vxMax - 1);
            int vy1 = Mathf.Min(vy0 + 64, vyMax - 1);

            for (int x = Mathf.Max(0, vx0); x <= vx1; x++)
            {
                MapData.VertexData[] col = vm[x];
                int yEnd = Mathf.Min(vy1, col.Length - 1);
                for (int y = Mathf.Max(0, vy0); y <= yEnd; y++)
                {
                    if (col[y].water > 0)
                        return true;
                }
            }
            return false;
        }

        /// <summary>构建一张 n×n 顶点的 [0,1] 平面模板。</summary>
        private static Mesh BuildPlaneTemplate(string name, int vertexPerEdge)
        {
            Mesh mesh = new Mesh
            {
                name = name,
                indexFormat = IndexFormat.UInt16,
                hideFlags = HideFlags.HideAndDontSave,
            };

            int vertexCount = vertexPerEdge * vertexPerEdge;
            UnityEngine.Vector3[] vertices = new UnityEngine.Vector3[vertexCount];
            for (int x = 0; x < vertexPerEdge; x++)
            {
                for (int y = 0; y < vertexPerEdge; y++)
                {
                    vertices[y * vertexPerEdge + x] = new UnityEngine.Vector3(
                        x / (float)(vertexPerEdge - 1), 0f, y / (float)(vertexPerEdge - 1));
                }
            }

            int quadCount = (vertexPerEdge - 1) * (vertexPerEdge - 1);
            int[] triangles = new int[quadCount * 6];
            int triIndex = 0;
            for (int x = 0; x < vertexPerEdge - 1; x++)
            {
                for (int y = 0; y < vertexPerEdge - 1; y++)
                {
                    int v00 = y * vertexPerEdge + x;
                    int v10 = v00 + 1;
                    int v01 = v00 + vertexPerEdge;
                    int v11 = v01 + 1;

                    // 【绕序必须与模板顶点布局匹配，不能照抄 CPU 网格的索引顺序】
                    // CPU 网格：i 沿 vx（世界 Z）、j 沿 vy（世界 X），索引 (i0,i2,i3) 落到世界
                    // (0,0)→(1,1)→(1,0)，屏俯视为顺时针 = Unity 正面。
                    // 本模板：顶点 x 沿世界 X、z 沿世界 Z（与 CPU 的 i/j 轴序相反），
                    // 若照抄 (v00,v11,v01) 会得到逆时针 = 背面，远景整片被 Cull Back 剔除。
                    // 因此这里取镜像顺序：(v00,v01,v11) 与 (v00,v11,v10)。
                    triangles[triIndex++] = v00;
                    triangles[triIndex++] = v01;
                    triangles[triIndex++] = v11;
                    triangles[triIndex++] = v00;
                    triangles[triIndex++] = v11;
                    triangles[triIndex++] = v10;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>开关远景批渲染（远景翻转时由 MapTerrain.SetBatchRender 调用）。</summary>
        public void SetEnabled(bool value)
        {
            // 远景期间初次激活时（重载地图后矩阵缓存可能为空）补建
            if (value && cellMatrices == null && map != null && map.mapTerrain != null)
                Setup(map, asset);

            enabled = value && asset != null && terrainTemplates != null;
        }

        /// <summary>
        /// 逐帧提交（远景激活时由 MapTerrain.Update 调用）。
        /// </summary>
        /// <param name="camera">渲染相机（主地图相机）</param>
        /// <param name="distance">相机距离口径（与 MapRenderLod / MapCamera.cur_distance 一致）</param>
        /// <param name="layer">实例所在物理层（远景 = MapNoOutLine，描边 Feature 天然跳过）</param>
        public void Render(Camera camera, float distance, int layer)
        {
            if (!enabled || camera == null || asset == null)
                return;

            // ① 按距离选模板档位
            int level = 0;
            while (level < LevelDistances.Length && distance >= LevelDistances[level])
                level++;
            Mesh template = terrainTemplates[Mathf.Clamp(level, 0, terrainTemplates.Length - 1)];
            Material material = asset.TerrainInstanceMaterial;
            if (material == null)
                return;

            // ② 收集视锥内块矩阵并分批提交。
            // 【为什么不用 cell.visible 过滤】顶视图下 MapModels.renderEnabled=false，
            // MapModels.Update 整帧跳过（MapModels.cs:774-775），cell.visible 冻结在
            // 进入顶视图前的旧视野状态 —— 若按它过滤，全览时只有旧视野内的一小片有地形。
            // 因此这里直接按相机视锥做剔除（GeometryUtility），与 MapModels 的可见性解耦。
            IMapCell[] cells = map.mapTerrain != null ? map.mapTerrain.terrainCells : null;
            if (cells == null)
                return;

            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);




            int submitCount = 0;
            int waterCount = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                IMapCell cell = cells[i];
                if (cell == null)
                    continue;

                if (cellBounds != null && i < cellBounds.Length)
                {
                    if (!TestBoundsSidePlanes(cellBounds[i]))
                        continue;
                }

                Matrix4x4 cellMatrix = cellMatrices != null && i < cellMatrices.Length
                    ? cellMatrices[i]
                    : Matrix4x4.TRS(cell.position, Quaternion.identity,
                        new UnityEngine.Vector3(64f * asset.QuadSize, 1f, 64f * asset.QuadSize));

                submitMatrices[submitCount++] = cellMatrix;

                // 水面只提交"块内确实有水"的实例：内陆地图绝大多数块是纯陆地，
                // 全量提交会让水面三角形数白白翻一倍（每块 33×33 全铺一遍）。
                if (waterReady && cellHasWater != null && i < cellHasWater.Length && cellHasWater[i])
                {
                    submitWaterMatrices[waterCount++] = cellMatrix;
                    if (waterCount == submitWaterMatrices.Length)
                    {
                        FlushWater(camera, template, layer, waterCount);
                        waterCount = 0;
                    }
                }

                if (submitCount == submitMatrices.Length)
                {
                    FlushTerrain(camera, template, material, layer, submitCount);
                    submitCount = 0;
                }
            }
            if (submitCount > 0)
                FlushTerrain(camera, template, material, layer, submitCount);
            if (waterCount > 0)
                FlushWater(camera, template, layer, waterCount);
        }

        /// <summary>
        /// 块包围盒的视锥剔除：<b>只测 4 个侧面平面</b>（Left/Right/Bottom/Top），
        /// 跳过 [4] Near 与 [5] Far。
        ///
        /// 【为什么跳过 Far】MapCamera.far_clip 只有 950（MapCamera.cs:20/89），
        /// 相机拉到远景 630 时地形与相机的距离普遍超过 950 —— 用 Far 平面剔除会把远景
        /// 地形整体剔掉（表现为"拉远后地形消失"）。地形是覆盖型大几何，超出 far_clip
        /// 的部分本来就交给 GPU 裁剪，CPU 只需保证"视野左右上下之内"的块都提交。
        /// </summary>
        private bool TestBoundsSidePlanes(Bounds bounds)
        {
            for (int p = 0; p < 4; p++)
            {
                Plane plane = frustumPlanes[p];
                UnityEngine.Vector3 normal = plane.normal;
                UnityEngine.Vector3 center = bounds.center;
                UnityEngine.Vector3 extents = bounds.extents;

                // AABB 沿法线方向"最远角"到平面的有符号距离（标准 AABB-平面测试）
                float distance = UnityEngine.Vector3.Dot(normal, center) + plane.distance
                    + Mathf.Abs(normal.x) * extents.x
                    + Mathf.Abs(normal.y) * extents.y
                    + Mathf.Abs(normal.z) * extents.z;

                if (distance < 0f)
                    return false;   // 最远角都在平面外侧 → 整个 AABB 被剔除
            }
            return true;
        }

        /// <summary>
        /// 提交一批实例：地形 + 水面共用同一份模板网格与实例矩阵。
        /// 水面用同一套矩阵意味着它的几何精度、块划分与地形完全一致，
        /// 水面高度只在着色器里按 <see cref="MapCellTerrainAsset.WaterHeightTex"/> 抬升，
        /// 不会再出现"全图粗网格跨水陆插值导致水体萎缩"的问题。
        /// </summary>
        private void FlushTerrain(Camera camera, Mesh template, Material material, int layer, int count)
        {
            Graphics.DrawMeshInstanced(template, 0, material, submitMatrices, count,
                null, ShadowCastingMode.Off, false, layer, camera);
        }

        /// <summary>
        /// 提交一批水面实例：与地形同一模板网格、同一实例矩阵，只是换材质。
        /// 顶点高度由着色器按 WaterHeightTex 抬升，因此几何与地形严格同形，
        /// 不会再出现"全图粗网格跨水陆插值 → 三角形斜插进地形 → 水体萎缩"。
        /// </summary>
        private void FlushWater(Camera camera, Mesh template, int layer, int count)
        {
            if (!waterReady || asset == null || asset.WaterInstanceMaterial == null)
                return;

            Graphics.DrawMeshInstanced(template, 0, asset.WaterInstanceMaterial, submitWaterMatrices, count,
                null, ShadowCastingMode.Off, false, layer, camera);
        }

        /// <summary>释放模板网格与缓存。</summary>
        public void Release()
        {
            if (terrainTemplates != null)
            {
                for (int i = 0; i < terrainTemplates.Length; i++)
                {
                    if (terrainTemplates[i] != null)
                        UnityEngine.Object.Destroy(terrainTemplates[i]);
                }
                terrainTemplates = null;
            }
            waterReady = false;
            cellMatrices = null;
            cellHasWater = null;
            asset = null;
            map = null;
            enabled = false;
        }

        /// <summary>IDisposable 兼容（地图卸载时释放）。</summary>
        public void Dispose()
        {
            Release();
        }
    }
}
