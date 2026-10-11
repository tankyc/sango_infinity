using Sango.Core;
using UnityEngine;
using System.Collections.Generic;

namespace Sango.MapGen
{
    /// <summary>
    /// 由分位数换算出的判定阈值。
    /// </summary>
    public struct MapGenThreshold
    {
        /// <summary>海平面：低于该值判定为水域</summary>
        public float seaLevel;
        /// <summary>浅滩下界：水域中高于该值判定为浅滩</summary>
        public float shallowLevel;
        /// <summary>山脉阈值：高于该值判定为崖(山地)</summary>
        public float mountainLevel;
    }

    /// <summary>
    /// 程序化地图生成的"上下文"，缓存一次生成所需的全部中间数据。
    ///
    /// 各生成阶段(高度/地形/河流/道路/城市/领地)都基于同一个上下文工作，
    /// 从而保证多次调用(例如先地形、再河流)时数据自洽。
    /// </summary>
    public class MapGenContext
    {
        /// <summary>最近一次构建的上下文，供 API 层复用(避免重复计算高度场)</summary>
        public static MapGenContext Current;

        /// <summary>目标地图</summary>
        public Render.MapRender map;
        /// <summary>生成参数</summary>
        public MapGenConfig config;

        /// <summary>地格宽度(格)</summary>
        public int cellsX;
        /// <summary>地格高度(格)</summary>
        public int cellsY;
        /// <summary>顶点宽度(顶点数)</summary>
        public int vertexXMax;
        /// <summary>顶点高度(顶点数)</summary>
        public int vertexYMax;

        /// <summary>顶点高度场(归一化 0~1)，下标 = vx * vertexYMax + vy</summary>
        public float[] heightField;
        /// <summary>地格中心高度(0~1)，下标 = cx * cellsY + cy</summary>
        public float[] cellHeight;
        /// <summary>地格湿度(0~1)</summary>
        public float[] cellHumidity;
        /// <summary>地格土壤噪声(0~1)，用于草地/土的分割</summary>
        public float[] cellSoil;

        /// <summary>地格地形类型(可被各个阶段修改)</summary>
        public byte[,] cellTerrain;

        /// <summary>地格归属城市ID(0 表示无归属)，由领地划分阶段填充</summary>
        public int[,] cellArea;

        /// <summary>地格状态位(内政/防御/贼)，由领地划分阶段填充</summary>
        public int[,] cellState;

        /// <summary>已生成的城市据点列表</summary>
        public List<CitySite> cities;

        /// <summary>当前阈值</summary>
        public MapGenThreshold thresholds;

        /// <summary>地形类型 → 顶点材质图层</summary>
        public int[] material;

        /// <summary>可用的最大地表图层下标（已排除最后一层水面层）</summary>
        public int maxUsableLayer;

        /// <summary>崖贴图分布所用的噪声偏移</summary>
        private float cliffNoiseX;
        private float cliffNoiseY;

        /// <summary>崖的坡度分档阈值（自适应，由崖格坡度分位数得到）</summary>
        private float cliffSteepThreshold;
        private float cliffGentleThreshold;

        /// <summary>各地形类型数量统计</summary>
        public int[] terrainCounts = new int[TerrainIds.Count];

        /// <summary>原版地图骨架(真实中国形貌与高度基准)；未启用时为 null</summary>
        public MapSkeleton skeleton;

        /// <summary>每格在骨架中的地形类型(重采样后)；无骨架时为 null</summary>
        public byte[,] cellSkeleton;

        // ------------------------------------------------------------------
        // 索引工具
        // ------------------------------------------------------------------

        /// <summary>顶点线性下标</summary>
        public int VertexIndex(int vx, int vy) { return vx * vertexYMax + vy; }

        /// <summary>地格线性下标</summary>
        public int CellIndex(int cx, int cy) { return cx * cellsY + cy; }

        /// <summary>由地格中心取对应顶点坐标</summary>
        public void CellCenterVertex(int cx, int cy, out int vx, out int vy)
        {
            vx = cx * 4 + 2;
            vy = cy * 4 + (cx & 1) * 2 + 2;
        }

        /// <summary>取地格中心高度</summary>
        public float CellHeightAt(int cx, int cy)
        {
            return cellHeight[cx * cellsY + cy];
        }

        /// <summary>
        /// 把本地地格坐标映射到骨架的源地格坐标（把骨架有效区域等比铺满本图）。
        /// </summary>
        /// <param name="cx">本地地格 X</param>
        /// <param name="cy">本地地格 Y</param>
        /// <param name="sx">输出的源地格 X</param>
        /// <param name="sy">输出的源地格 Y</param>
        public void MapToSkeletonCell(int cx, int cy, out int sx, out int sy)
        {
            if (skeleton == null)
            {
                sx = cx;
                sy = cy;
                return;
            }

            sx = skeleton.offX + (int)((cx + 0.5f) * skeleton.spanX / Mathf.Max(1, cellsX));
            sy = skeleton.offY + (int)((cy + 0.5f) * skeleton.spanY / Mathf.Max(1, cellsY));
            sx = Mathf.Clamp(sx, skeleton.offX, skeleton.offX + skeleton.spanX - 1);
            sy = Mathf.Clamp(sy, skeleton.offY, skeleton.offY + skeleton.spanY - 1);
        }

        /// <summary>
        /// 把本地顶点坐标映射到骨架的源顶点坐标（等比铺满，保留原版的逐顶点细节）。
        /// </summary>
        /// <param name="vx">本地顶点 X</param>
        /// <param name="vy">本地顶点 Y</param>
        /// <param name="svx">输出的源顶点 X</param>
        /// <param name="svy">输出的源顶点 Y</param>
        public void MapToSkeletonVertex(int vx, int vy, out int svx, out int svy)
        {
            if (skeleton == null)
            {
                svx = vx;
                svy = vy;
                return;
            }

            svx = skeleton.offX * 4
                + Mathf.RoundToInt(vx * (skeleton.spanX * 4f) / Mathf.Max(1, vertexXMax));
            svy = skeleton.offY * 4
                + Mathf.RoundToInt(vy * (skeleton.spanY * 4f) / Mathf.Max(1, vertexYMax));
            svx = Mathf.Clamp(svx, 0, skeleton.srcVertX - 1);
            svy = Mathf.Clamp(svy, 0, skeleton.srcVertY - 1);
        }

        /// <summary>
        /// 把"来源地图(原版中国图所在的世界网格)"的格坐标换算到本图坐标，
        /// 与 <see cref="MapToSkeletonCell"/> 互逆。
        /// 未启用骨架时按 <see cref="ChinaCityTable"/> 的偏移与格数做同样的等比换算。
        /// </summary>
        /// <param name="sx">源地格 X</param>
        /// <param name="sy">源地格 Y</param>
        /// <param name="cx">输出的本图地格 X</param>
        /// <param name="cy">输出的本图地格 Y</param>
        public void CellFromSource(int sx, int sy, out int cx, out int cy)
        {
            int offX, offY, spanX, spanY;
            if (skeleton != null)
            {
                offX = skeleton.offX;
                offY = skeleton.offY;
                spanX = skeleton.spanX;
                spanY = skeleton.spanY;
            }
            else
            {
                offX = ChinaCityTable.SourceOffset;
                offY = ChinaCityTable.SourceOffset;
                spanX = ChinaCityTable.SourceSpan;
                spanY = ChinaCityTable.SourceSpan;
            }

            // 与 MapToSkeletonCell 互逆： cx = (sx - offX + 0.5) * cellsX / spanX - 0.5
            cx = Mathf.RoundToInt((sx - offX + 0.5f) * cellsX / Mathf.Max(1, spanX) - 0.5f);
            cy = Mathf.RoundToInt((sy - offY + 0.5f) * cellsY / Mathf.Max(1, spanY) - 0.5f);
            cx = Mathf.Clamp(cx, 0, cellsX - 1);
            cy = Mathf.Clamp(cy, 0, cellsY - 1);
        }

        /// <summary>
        /// 把原版中国图格坐标(lo 向东、hi 向南)换算到本图坐标。
        /// </summary>
        /// <param name="lo">原版中国图格 X(向东)</param>
        /// <param name="hi">原版中国图格 Y(向南)</param>
        /// <param name="cx">输出的本图地格 X</param>
        /// <param name="cy">输出的本图地格 Y</param>
        public void CellFromChina(int lo, int hi, out int cx, out int cy)
        {
            CellFromSource(lo + ChinaCityTable.SourceOffset, hi + ChinaCityTable.SourceOffset, out cx, out cy);
        }

        /// <summary>
        /// 取骨架中某顶点的归一化高度(高度字节 / 255)。
        /// </summary>
        /// <param name="vx">本地顶点 X</param>
        /// <param name="vy">本地顶点 Y</param>
        /// <returns>归一化高度 0~1</returns>
        public float SkeletonHeightAt(int vx, int vy)
        {
            MapToSkeletonVertex(vx, vy, out int svx, out int svy);
            return skeleton.height[svx * skeleton.srcVertY + svy] / 255f;
        }

        /// <summary>判断地格是否为水域地形</summary>
        public static bool IsWaterTerrain(int terrainType)
        {
            return terrainType == TerrainIds.Sea || terrainType == TerrainIds.Shallow
                || terrainType == TerrainIds.Stream || terrainType == TerrainIds.River;
        }

        // ------------------------------------------------------------------
        // 构建
        // ------------------------------------------------------------------

        /// <summary>
        /// 构建上下文：学习材质、生成高度场、换算阈值、采样地格场、完成地形分类。
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="cfg">生成参数</param>
        /// <returns>构建好的上下文；失败时返回 null</returns>
        public static MapGenContext Build(Render.MapRender map, MapGenConfig cfg)
        {
            if (map == null || map.mapData == null || map.mapGrid == null)
                return null;
            if (GameData.Instance == null || GameData.Instance.ScenarioCommonData == null
                || GameData.Instance.ScenarioCommonData.TerrainTypes == null)
                return null;

            MapGenContext ctx = new MapGenContext();
            ctx.map = map;
            ctx.config = cfg != null ? cfg.Clone() : new MapGenConfig();
            ctx.cellsX = map.mapGrid.bounds.x;
            ctx.cellsY = map.mapGrid.bounds.y;
            ctx.vertexXMax = map.mapData.vertex_x_max;
            ctx.vertexYMax = map.mapData.vertex_y_max;

            if (ctx.cellsX <= 0 || ctx.cellsY <= 0 || ctx.vertexXMax <= 1 || ctx.vertexYMax <= 1)
                return null;

            // 可用地表图层数（排除最后一层水面层），并预生成崖贴图分布用的噪声偏移
            int usable = GetUsableLayerCount(map);
            ctx.maxUsableLayer = usable > 0 ? usable - 1 : 0;
            MapNoise.MakeSeedOffset(ctx.config.seed + 4242, out ctx.cliffNoiseX, out ctx.cliffNoiseY);

            ctx.material = BuildMaterialTable(map, ctx.cellsX, ctx.cellsY, usable);

            // 原版骨架：提供真实的海陆轮廓、山系、河道与高度基准。
            // 失败时只记录错误并回退为纯程序化生成，不阻断流程。
            if (ctx.config.useSkeleton)
            {
                MapSkeleton sk = MapSkeleton.Load(ctx.config.skeletonPath,
                    ctx.config.skeletonOffsetX, ctx.config.skeletonOffsetY);
                if (sk.valid)
                {
                    ctx.skeleton = sk;
                    Sango.Log.Info(sk.message);
                }
                else
                {
                    Sango.Log.Error(sk.message + "（本次回退为纯程序化生成）");
                }
            }

            ctx.heightField = ctx.BuildHeightField();
            ctx.thresholds = ctx.ComputeThresholds();
            ctx.SampleCellFields();
            ctx.ClassifyAll();

            Current = ctx;
            return ctx;
        }

        /// <summary>
        /// 重算地形数量统计。
        /// </summary>
        public void RebuildTerrainCounts()
        {
            terrainCounts = new int[TerrainIds.Count];
            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    int t = cellTerrain[cx, cy];
                    if (t >= 0 && t < TerrainIds.Count)
                        terrainCounts[t]++;
                }
            }
        }

        // ------------------------------------------------------------------
        // 材质映射
        // ------------------------------------------------------------------

        /// <summary>
        /// 获取某地形类型默认使用的贴图图层(取自原版实测表 TerrainLayerMap)。
        /// </summary>
        /// <param name="terrainId">地形类型ID</param>
        /// <returns>材质图层索引</returns>
        public static int MaterialForTerrain(int terrainId)
        {
            return TerrainLayerMap.Get(terrainId);
        }

        /// <summary>
        /// 取目标地图可用的地表图层数（排除最后一层"水面层"）。
        /// 引擎在 MapCell 里用 textureIndex == 层号 决定该顶点归哪一层，
        /// 若 textureIndex 超出 [0, layerDatas.Length-2] 会导致该处地表破洞。
        /// </summary>
        private static int GetUsableLayerCount(Render.MapRender map)
        {
            if (map == null || map.mapLayer == null || map.mapLayer.layerDatas == null)
                return 0;

            int count = map.mapLayer.layerDatas.Length - 1;
            return count > 0 ? count : 0;
        }

        /// <summary>
        /// 构建"地形类型 → 贴图图层"映射：
        ///   1. 先统计当前地图各地形的众数图层（这些值天然合法，且能贴合已有美术）；
        ///   2. 统计不到的地形退回原版实测表 TerrainLayerMap；
        ///   3. 所有取值一律钳制到 [0, 可用图层数-1]，避免超出 layerDatas.Length 造成地表破洞。
        /// </summary>
        private static int[] BuildMaterialTable(Render.MapRender map, int cellsX, int cellsY, int usable)
        {
            int maxLayer = usable > 0 ? usable - 1 : 0;

            int[] material = new int[TerrainIds.Count];
            for (int t = 0; t < TerrainIds.Count; t++)
                material[t] = Mathf.Clamp(TerrainLayerMap.Get(t), 0, maxLayer);

            if (usable <= 0)
            {
                Sango.Log.Warning("未找到地表图层(mapLayer)，贴图图层将全部回退为 0");
                return material;
            }

            // 注意：统计数组必须按"实际可用图层数"分配，
            // 原版地图有 49 层（索引可达 47），用固定 36 会数组越界。
            int[][] counts = new int[TerrainIds.Count][];
            for (int i = 0; i < TerrainIds.Count; i++)
                counts[i] = new int[usable];

            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    Render.MapGrid.GridData gridData = map.mapGrid.GetGridData(cx, cy);
                    if (gridData == null)
                        continue;

                    int terrainType = gridData.terrainType;
                    if (terrainType < 0 || terrainType >= TerrainIds.Count)
                        continue;

                    int vx = cx * 4 + 2;
                    int vy = cy * 4 + (cx & 1) * 2 + 2;
                    int layer = map.mapData.GetVertexData(vx, vy).textureIndex;
                    if (layer >= 0 && layer < usable)
                        counts[terrainType][layer]++;
                }
            }

            for (int t = 0; t < TerrainIds.Count; t++)
            {
                int bestLayer = -1;
                int bestCount = 0;
                for (int layer = 0; layer < usable; layer++)
                {
                    if (counts[t][layer] > bestCount)
                    {
                        bestCount = counts[t][layer];
                        bestLayer = layer;
                    }
                }
                if (bestLayer >= 0 && bestCount > 8)
                    material[t] = bestLayer;
            }

            return material;
        }

        // ------------------------------------------------------------------
        // 高度场
        // ------------------------------------------------------------------

        /// <summary>
        /// 生成顶点高度场：大陆层 + 细节层，叠加边缘衰减后平滑并归一化。
        /// </summary>
        private float[] BuildHeightField()
        {
            float[] field = new float[vertexXMax * vertexYMax];

            MapNoise.MakeSeedOffset(config.seed, out float offsetX, out float offsetY);

            const float vertexPerCell = 0.25f;
            float invFeature = vertexPerCell / Mathf.Max(1f, config.terrainFeatureSize);
            float invFeatureContinent = invFeature * Mathf.Max(0.01f, config.continentScale);

            for (int vx = 0; vx < vertexXMax; vx++)
            {
                for (int vy = 0; vy < vertexYMax; vy++)
                {
                    float continent = MapNoise.FBM(vx * invFeatureContinent, vy * invFeatureContinent,
                        config.octaves, config.persistence, config.lacunarity, offsetX, offsetY);
                    float detail = MapNoise.FBM(vx * invFeature, vy * invFeature,
                        config.octaves, config.persistence, config.lacunarity, offsetX + 137.5f, offsetY + 71.3f);

                    float h = continent * config.continentWeight + detail * (1f - config.continentWeight);

                    if (skeleton != null)
                    {
                        // 骨架模式：以原版真实高度为基准，分形噪声只做细节扰动。
                        // 不做边缘衰减，否则会把原版的真实海岸线压进海里。
                        float w = Mathf.Clamp01(config.skeletonWeight);
                        h = h * (1f - w) + SkeletonHeightAt(vx, vy) * w;
                    }
                    else
                    {
                        h *= EdgeFalloff(vx, vy, vertexXMax, vertexYMax, config.edgeFalloffCells);
                    }

                    field[vx * vertexYMax + vy] = h;
                }
            }

            for (int i = 0; i < config.smoothPasses; i++)
                field = BoxBlur(field, vertexXMax, vertexYMax);

            Normalize(field);
            return field;
        }

        /// <summary>计算边缘衰减系数(0~1)</summary>
        private static float EdgeFalloff(int vx, int vy, int vertexXMax, int vertexYMax, float edgeCells)
        {
            if (edgeCells <= 0f)
                return 1f;

            float fade = edgeCells * 4f;
            float dx = Mathf.Min(vx, vertexXMax - 1 - vx);
            float dy = Mathf.Min(vy, vertexYMax - 1 - vy);
            float d = Mathf.Min(dx, dy);
            float t = Mathf.Clamp01(d / fade);
            return t * t * (3f - 2f * t);
        }

        /// <summary>3x3 均值模糊</summary>
        private static float[] BoxBlur(float[] src, int w, int h)
        {
            float[] dst = new float[src.Length];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float sum = 0f;
                    int count = 0;
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox;
                        if (nx < 0 || nx >= w)
                            continue;
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            int ny = y + oy;
                            if (ny < 0 || ny >= h)
                                continue;
                            sum += src[nx * h + ny];
                            count++;
                        }
                    }
                    dst[x * h + y] = count > 0 ? sum / count : src[x * h + y];
                }
            }
            return dst;
        }

        /// <summary>线性归一化到 0~1</summary>
        private static void Normalize(float[] values)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] < min) min = values[i];
                if (values[i] > max) max = values[i];
            }
            float range = max - min;
            if (range < 1e-6f)
                return;
            float inv = 1f / range;
            for (int i = 0; i < values.Length; i++)
                values[i] = (values[i] - min) * inv;
        }

        // ------------------------------------------------------------------
        // 阈值
        // ------------------------------------------------------------------

        /// <summary>按配置比例取分位数，换算海平面/山脉/浅滩阈值</summary>
        private MapGenThreshold ComputeThresholds()
        {
            MapGenThreshold th = new MapGenThreshold();

            List<float> samples = new List<float>();
            const int stride = 2;
            for (int i = 0; i < heightField.Length; i += stride)
                samples.Add(heightField[i]);
            float[] sampleArray = samples.ToArray();

            float landRatio = Mathf.Clamp(config.landRatio, 0.05f, 0.95f);

            // 骨架模式下改用"骨架真实的陆地占比"来定海平面，
            // 这样高度标定的海陆分界与原版海岸线一致（配置里的 landRatio 仅作回退）。
            if (skeleton != null)
            {
                int land = 0;
                int total = 0;
                for (int cx = 0; cx < cellsX; cx++)
                {
                    for (int cy = 0; cy < cellsY; cy++)
                    {
                        MapToSkeletonCell(cx, cy, out int sx, out int sy);
                        int sk = skeleton.TerrainAt(sx, sy);
                        total++;
                        if (!IsWaterOrOutside(sk))
                            land++;
                    }
                }

                if (total > 0 && land > 0)
                    landRatio = Mathf.Clamp(land / (float)total, 0.05f, 0.95f);
            }

            th.seaLevel = MapNoise.Quantile(sampleArray, 1f - landRatio);

            List<float> landSamples = new List<float>();
            for (int i = 0; i < sampleArray.Length; i++)
                if (sampleArray[i] >= th.seaLevel)
                    landSamples.Add(sampleArray[i]);
            float[] landArray = landSamples.ToArray();
            th.mountainLevel = landArray.Length > 0
                ? MapNoise.Quantile(landArray, 1f - Mathf.Clamp01(config.mountainRatio))
                : th.seaLevel;

            List<float> waterSamples = new List<float>();
            for (int i = 0; i < sampleArray.Length; i++)
                if (sampleArray[i] < th.seaLevel)
                    waterSamples.Add(sampleArray[i]);
            float[] waterArray = waterSamples.ToArray();
            th.shallowLevel = waterArray.Length > 0
                ? MapNoise.Quantile(waterArray, 1f - Mathf.Clamp01(config.shallowRatio))
                : th.seaLevel;

            return th;
        }

        // ------------------------------------------------------------------
        // 地格采样与分类
        // ------------------------------------------------------------------

        /// <summary>采样地格中心的高度、湿度与土壤噪声</summary>
        private void SampleCellFields()
        {
            int n = cellsX * cellsY;
            cellHeight = new float[n];
            cellHumidity = new float[n];
            cellSoil = new float[n];

            // 骨架形貌：把原版地形重采样到本地地格
            if (skeleton != null)
                cellSkeleton = new byte[cellsX, cellsY];

            MapNoise.MakeSeedOffset(config.seed, out float offsetX, out float offsetY);
            float invClimate = 1f / Mathf.Max(1f, config.climateFeatureSize);
            float invSoil = 1f / Mathf.Max(1f, config.terrainFeatureSize * 0.35f);

            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    CellCenterVertex(cx, cy, out int vx, out int vy);
                    if (vx >= vertexXMax) vx = vertexXMax - 1;
                    if (vy >= vertexYMax) vy = vertexYMax - 1;

                    int index = cx * cellsY + cy;
                    cellHeight[index] = heightField[vx * vertexYMax + vy];
                    cellHumidity[index] = MapNoise.FBM(cx * invClimate, cy * invClimate,
                        config.octaves, config.persistence, config.lacunarity, offsetX + 331.7f, offsetY + 913.1f);
                    cellSoil[index] = MapNoise.FBM(cx * invSoil, cy * invSoil,
                        config.octaves, config.persistence, config.lacunarity, offsetX + 555.5f, offsetY + 222.2f);

                    if (cellSkeleton != null)
                    {
                        MapToSkeletonCell(cx, cy, out int sx, out int sy);
                        cellSkeleton[cx, cy] = (byte)Mathf.Clamp(skeleton.TerrainAt(sx, sy), 0, 255);
                    }
                }
            }
        }

        /// <summary>对所有地格分类地形</summary>
        private void ClassifyAll()
        {
            cellTerrain = new byte[cellsX, cellsY];
            cellArea = new int[cellsX, cellsY];
            cellState = new int[cellsX, cellsY];
            System.Array.Clear(terrainCounts, 0, terrainCounts.Length);

            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    int index = cx * cellsY + cy;
                    int type = ClassifyOne(cx, cy, cellHeight[index], cellHumidity[index], cellSoil[index]);
                    cellTerrain[cx, cy] = (byte)type;
                    if (type >= 0 && type < TerrainIds.Count)
                        terrainCounts[type]++;
                }
            }
        }

        /// <summary>单个地格的地形判定</summary>
        /// <param name="cx">地格 X</param>
        /// <param name="cy">地格 Y</param>
        /// <param name="height">归一化高度</param>
        /// <param name="humidity">归一化湿度</param>
        /// <param name="soilNoise">土壤噪声</param>
        /// <returns>地形类型ID</returns>
        private int ClassifyOne(int cx, int cy, float height, float humidity, float soilNoise)
        {
            if (cellSkeleton != null)
                return ClassifyWithSkeleton(cx, cy, height, humidity, soilNoise);

            if (height < thresholds.seaLevel)
                return height >= thresholds.shallowLevel ? TerrainIds.Shallow : TerrainIds.Sea;

            if (height > thresholds.mountainLevel)
                return TerrainIds.Cliff;

            return ClassifyLandCover(height, humidity, soilNoise);
        }

        /// <summary>
        /// 骨架地形是否属于水域（含"域外"哨兵值与"无地形"）。
        /// 用于统计骨架的真实陆地占比。
        /// </summary>
        /// <param name="skeletonTerrain">骨架中的地形类型ID</param>
        /// <returns>是水域返回 true</returns>
        private static bool IsWaterOrOutside(int skeletonTerrain)
        {
            return skeletonTerrain == MapSkeleton.OutsideTerrain
                || skeletonTerrain == TerrainIds.None
                || skeletonTerrain == TerrainIds.Sea
                || skeletonTerrain == TerrainIds.Shallow
                || skeletonTerrain == TerrainIds.Stream
                || skeletonTerrain == TerrainIds.River;
        }

        /// <summary>
        /// 领地/道路的"可连通"判定：只要非水域即视为连通。
        /// 不可通行的陆地(山地/岸)虽然不能驻军，但可以高代价穿行，
        /// 因此连通性判定必须与领地扩散的口径一致，否则会把同一陆块误判成多块。
        /// </summary>
        /// <param name="terrainTypes">地形类型表</param>
        /// <param name="type">地形类型ID</param>
        /// <returns>可连通返回 true</returns>
        private static bool IsConnectableTerrain(SangoObjectSet<TerrainType> terrainTypes, int type)
        {
            TerrainType tt = terrainTypes != null ? terrainTypes.Get(type) : null;
            return tt != null && !tt.isWater;
        }

        /// <summary>
        /// 标记"主大陆"：对非水域地格做六邻域连通分量分析，
        /// 只有规模达到最大分量一定比例的分量才被接受。
        /// 用于把城市限制在连成一片的大陆上，避免落在被水隔断的孤岛。
        /// 注意：连通性口径与领地扩散一致——山地可高代价穿行，因此不构成阻断。
        /// </summary>
        /// <param name="minComponentRatio">分量相对最大分量的最小占比(0~1)</param>
        /// <returns>与地格同尺寸的标记数组；true 表示属于可接受的大陆块</returns>
        public bool[] ComputeMainLandMask(float minComponentRatio)
        {
            int total = cellsX * cellsY;
            bool[] mask = new bool[total];
            if (cellTerrain == null || total <= 0)
                return mask;

            SangoObjectSet<TerrainType> terrainTypes =
                GameData.Instance != null && GameData.Instance.ScenarioCommonData != null
                    ? GameData.Instance.ScenarioCommonData.TerrainTypes : null;

            int[] comp = new int[total];
            for (int i = 0; i < total; i++)
                comp[i] = -1;

            List<int> sizes = new List<int>();
            Queue<int> queue = new Queue<int>();
            int id = 0;

            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    int start = cx * cellsY + cy;
                    if (comp[start] >= 0 || !IsConnectableTerrain(terrainTypes, cellTerrain[cx, cy]))
                        continue;

                    int size = 0;
                    comp[start] = id;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int cur = queue.Dequeue();
                        size++;
                        int px = cur / cellsY;
                        int py = cur % cellsY;
                        for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                        {
                            int ni = HexNeighbors.GetNeighbor(px, py, d, cellsX, cellsY, out int nx, out int ny);
                            if (ni < 0 || comp[ni] >= 0)
                                continue;
                            if (!IsConnectableTerrain(terrainTypes, cellTerrain[nx, ny]))
                                continue;
                            comp[ni] = id;
                            queue.Enqueue(ni);
                        }
                    }

                    sizes.Add(size);
                    id++;
                }
            }

            if (sizes.Count == 0)
                return mask;

            int maxSize = 0;
            for (int i = 0; i < sizes.Count; i++)
                if (sizes[i] > maxSize)
                    maxSize = sizes[i];

            int threshold = Mathf.Max(1, Mathf.RoundToInt(maxSize * Mathf.Clamp01(minComponentRatio)));
            for (int i = 0; i < total; i++)
                mask[i] = comp[i] >= 0 && sizes[comp[i]] >= threshold;

            return mask;
        }

        /// <summary>
        /// 骨架模式的地形判定：海陆轮廓、山系、河道沿用原版骨架（真实中国形貌），
        /// 地表类型(森林/荒地/草地/砂地/湿地)仍由程序化的气候与土壤噪声重绘。
        /// </summary>
        private int ClassifyWithSkeleton(int cx, int cy, float height, float humidity, float soilNoise)
        {
            int sk = cellSkeleton[cx, cy];

            // 骨架的"域外"哨兵值：本图要铺满整张地图，按海处理
            if (sk == MapSkeleton.OutsideTerrain || sk == TerrainIds.None)
                return TerrainIds.Sea;

            // 海陆轮廓沿用骨架；浅滩与海按本图的高度阈值细分
            if (sk == TerrainIds.Sea || sk == TerrainIds.Shallow)
                return height >= thresholds.shallowLevel ? TerrainIds.Shallow : TerrainIds.Sea;

            // 河道：原版已给出完整的河网
            if (config.skeletonUseRivers && (sk == TerrainIds.Stream || sk == TerrainIds.River))
                return sk;

            // 山系
            if (config.skeletonUseMountains && sk == TerrainIds.Cliff)
                return TerrainIds.Cliff;

            // 骨架里的据点/道路类地形不沿用，统一交给城市与道路阶段重绘
            return ClassifyLandCover(height, humidity, soilNoise);
        }

        /// <summary>按相对海拔与气候、土壤噪声判定陆地地表类型</summary>
        private int ClassifyLandCover(float height, float humidity, float soilNoise)
        {
            float altitude = Mathf.InverseLerp(thresholds.seaLevel, 1f, height);

            if (humidity > config.swampThreshold && altitude < config.swampMaxAltitude)
                return TerrainIds.Marsh;
            if (humidity > config.wetThreshold)
                return TerrainIds.Forest;
            if (humidity < config.sandThreshold)
                return TerrainIds.Sand;
            if (humidity < config.dryThreshold)
                return TerrainIds.Waste;

            return soilNoise > config.soilThreshold ? TerrainIds.Soil : TerrainIds.Grass;
        }

        // ------------------------------------------------------------------
        // 写回引擎
        // ------------------------------------------------------------------

        /// <summary>
        /// 把地格地形、状态位与领地归属写回引擎地格数据。
        /// </summary>
        public void ApplyCellTerrain()
        {
            bool hasState = cellState != null;
            bool hasArea = cellArea != null;

            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    map.mapGrid.SetTerrainType(cx, cy, cellTerrain[cx, cy]);

                    Render.MapGrid.GridData gridData = map.mapGrid.GetGridData(cx, cy);
                    if (gridData == null)
                        continue;

                    gridData.terrainState = hasState ? cellState[cx, cy] : 0;
                    int area = hasArea ? cellArea[cx, cy] : 0;
                    gridData.areaId = (ushort)(area < 0 ? 0 : (area > 65535 ? 0 : area));
                }
            }
        }

        /// <summary>河床允许的最低高度字节(保证不会出现负高度)</summary>
        private const int RiverBedMinByte = 0;

        /// <summary>把顶点高度、材质、水面与法线写回引擎顶点数据</summary>
        public void ApplyVertexData()
        {
            PrepareCliffThresholds();

            int quadSize = Mathf.Max(1, map.mapData.quadSize);

            for (int vx = 0; vx < vertexXMax; vx++)
            {
                Render.MapData.VertexData[] row = map.mapData.vertexDatas[vx];
                if (row == null)
                    continue;

                int cx = Mathf.Clamp(vx / 4, 0, cellsX - 1);

                for (int vy = 0; vy < vertexYMax; vy++)
                {
                    byte heightByte = ComputeHeightByte(vx, vy);
                    // VertexData 是 struct：必须直接写数组元素，不能改临时副本
                    row[vy].height = heightByte;

                    int cy = Mathf.Clamp((vy - (cx & 1) * 2) / 4, 0, cellsY - 1);
                    int type = cellTerrain[cx, cy];
                    if (type >= 0 && type < TerrainIds.Count)
                    {
                        // 崖(山地)按"坡度分档 + 噪声"在多张岩石贴图间分布，其余地形用统一映射
                        if (type == TerrainIds.Cliff && config.cliffLayerSpread)
                            row[vy].textureIndex = (byte)PickCliffLayer(vx, vy);
                        else
                            row[vy].textureIndex = (byte)material[type];
                    }

                    // 水面：海/浅滩共用海平面，川/河随地形抬升；非水域为 0 不绘制。
                    // 引擎以 water > height 判定是否绘制水面，ComputeWaterByte 内部已强制该约束。
                    if (config.renderWater)
                        row[vy].water = (byte)Mathf.Clamp(ComputeWaterByte(vx, vy, type), 0, 255);
                    else
                        row[vy].water = 0;
                }
            }

            // 水面顶点位置必须与水面高度字节同步：引擎把水面画在 waterPosition 上，
            // 且以 water > height 判定是否绘制。若不同步，水面会与地形重叠或沉入地下。
            ApplyWaterPositions();

            if (config.recomputeNormals)
                RecomputeNormals(quadSize);
        }

        /// <summary>
        /// 同步水面顶点位置(waterPosition.y = water * 0.5f)，使水面与地形保持落差、不重叠。
        ///
        /// 规则与引擎 MapData.VertexWaterPosition 完全一致：
        ///   - water > 0 的顶点：水面高度 = water * 0.5f；
        ///   - water == 0 的顶点：若邻域存在水域顶点，则借用其水面高度，
        ///     使水面网格在岸线处保持平整，而不会塌到 0。
        ///
        /// 说明：waterPosition 原本只在 MapData.OnLoad 中由 MapDataJob 重算，
        /// 编辑器内程序化生成时不会触发，因此必须在此自行同步。
        /// </summary>
        private void ApplyWaterPositions()
        {
            // waterPosition 已改为按需计算（MapData.VertexWaterPosition 会自行借用邻域水面高度），
            // 这里不再需要预先写回顶点。
        }

        /// <summary>
        /// 取邻域首个非零水面高度字节(搜索顺序与引擎 MapData.VertexWaterPosition 一致)。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>邻居的水面高度字节；找不到返回 0</returns>
        private int NeighborWaterByte(int vx, int vy)
        {
            int lx = vx - 1;
            int rx = vx + 1;
            int uy = vy - 1;
            int dy = vy + 1;

            if (lx > 0)
            {
                int w = WaterByteAt(lx, vy);
                if (w > 0)
                    return w;
            }

            if (uy > 0)
            {
                int w = WaterByteAt(vx, uy);
                if (w > 0)
                    return w;

                if (lx > 0)
                {
                    w = WaterByteAt(lx, uy);
                    if (w > 0)
                        return w;
                }
            }

            if (rx < vertexXMax)
            {
                int w = WaterByteAt(rx, vy);
                if (w > 0)
                    return w;

                if (uy > 0)
                {
                    w = WaterByteAt(rx, uy);
                    if (w > 0)
                        return w;
                }
            }

            if (dy < vertexYMax)
            {
                int w = WaterByteAt(vx, dy);
                if (w > 0)
                    return w;

                if (rx < vertexXMax)
                {
                    w = WaterByteAt(rx, dy);
                    if (w > 0)
                        return w;
                }

                if (lx > 0)
                {
                    w = WaterByteAt(lx, dy);
                    if (w > 0)
                        return w;
                }
            }

            return 0;
        }

        /// <summary>
        /// 读取指定顶点的水面高度字节(越界或空数据返回 0)。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>水面高度字节</returns>
        private int WaterByteAt(int vx, int vy)
        {
            if (vx < 0 || vy < 0 || vx >= vertexXMax || vy >= vertexYMax)
                return 0;

            Render.MapData.VertexData[] row = map.mapData.vertexDatas[vx];
            if (row == null)
                return 0;

            return row[vy].water;
        }

        /// <summary>
        /// 计算顶点若为陆地时的"自然地面高度字节"。
        /// 陆地 = 海平面 + t^curve * (最高高度 - 海平面)，t 为该点的归一化陆地高度。
        /// 河床与河面高度都以此基准推导，从而随海拔抬升。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>自然地面高度字节(0~255)</returns>
        public int LandGroundByte(int vx, int vy)
        {
            vx = Mathf.Clamp(vx, 0, vertexXMax - 1);
            vy = Mathf.Clamp(vy, 0, vertexYMax - 1);

            int seaByte = Mathf.Clamp(config.seaLevelByte, 0, 255);
            int maxByte = Mathf.Clamp(config.maxLandByte, seaByte, 255);

            float h = heightField[vx * vertexYMax + vy];
            float sea = thresholds.seaLevel;
            float t = sea < 1f ? Mathf.InverseLerp(sea, 1f, h) : 1f;
            float curved = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(1f, config.landHeightCurve));

            int value = seaByte + Mathf.RoundToInt(curved * (maxByte - seaByte));
            return Mathf.Clamp(value, 0, 255);
        }

        /// <summary>海平面水面高度字节(海与浅滩共用同一水平面)</summary>
        public int SeaWaterByte
        {
            get { return Mathf.Clamp(config.waterByte, 0, 255); }
        }

        /// <summary>
        /// 河川(川/河)的水面高度字节：以自然地面为基准下沉 riverBankDrop，
        /// 且不低于海平面。因此上游水位随地形抬升、下游汇入同一水平面。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>水面高度字节</returns>
        public int RiverWaterByte(int vx, int vy)
        {
            int level = LandGroundByte(vx, vy) - Mathf.Max(0, config.riverBankDrop);
            return Mathf.Max(SeaWaterByte, level);
        }

        /// <summary>
        /// 河川(川/河)的河床高度字节：水面下沉 riverDrop，保证水面严格高于河床。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>河床高度字节</returns>
        public int RiverBedByte(int vx, int vy)
        {
            int drop = Mathf.Max(1, config.riverDrop);
            return Mathf.Max(RiverBedMinByte, RiverWaterByte(vx, vy) - drop);
        }

        /// <summary>
        /// 计算顶点水面高度字节。非水域返回 0(不绘制水面)。
        ///   海 / 浅滩 = 海平面(保持海面是同一个平面)；
        ///   川 / 河   = 随地形抬升的水位；
        /// 并强制高于地面(引擎以 water > height 判定是否绘制水面，必须严格大于)。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <param name="terrainType">该顶点所属地格的地形类型ID</param>
        /// <returns>水面高度字节；非水域为 0</returns>
        public int ComputeWaterByte(int vx, int vy, int terrainType)
        {
            int water;
            if (terrainType == TerrainIds.Stream || terrainType == TerrainIds.River)
                water = RiverWaterByte(vx, vy);
            else if (terrainType == TerrainIds.Sea || terrainType == TerrainIds.Shallow)
                water = SeaWaterByte;
            else
                return 0;

            // 保证严格高于地面，否则引擎不会绘制水面(会出现水面与地形重叠)
            return Mathf.Max(water, ComputeHeightByte(vx, vy) + 1);
        }

        /// <summary>
        /// 计算顶点的高度字节。标定规则(与原版地图一致)：
        ///   海 = 0(海底)；浅滩 = 海平面；川/河 = 河床(河面下沉 riverDrop)；
        ///   陆地 = 自然地面高度(见 LandGroundByte)。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>高度字节(0~255)</returns>
        public byte ComputeHeightByte(int vx, int vy)
        {
            vx = Mathf.Clamp(vx, 0, vertexXMax - 1);
            vy = Mathf.Clamp(vy, 0, vertexYMax - 1);

            int cx = Mathf.Clamp(vx / 4, 0, cellsX - 1);
            int cy = Mathf.Clamp((vy - (cx & 1) * 2) / 4, 0, cellsY - 1);
            int type = cellTerrain[cx, cy];

            int seaByte = Mathf.Clamp(config.seaLevelByte, 0, 255);

            if (type == TerrainIds.Sea)
                return 0;
            if (type == TerrainIds.Stream || type == TerrainIds.River)
                return (byte)Mathf.Clamp(RiverBedByte(vx, vy), 0, 255);
            if (type == TerrainIds.Shallow)
                return (byte)Mathf.Clamp(seaByte, 0, 255);

            return (byte)Mathf.Clamp(LandGroundByte(vx, vy), 0, 255);
        }

        /// <summary>
        /// 判断某地形是否需要渲染水面(海/浅滩/川/河)。
        /// </summary>
        /// <param name="terrainType">地形类型ID</param>
        /// <returns>是否需要渲染水面</returns>
        public static bool IsRenderWaterTerrain(int terrainType)
        {
            return terrainType == TerrainIds.Sea || terrainType == TerrainIds.Shallow
                || terrainType == TerrainIds.Stream || terrainType == TerrainIds.River;
        }

        /// <summary>
        /// 估算某顶点的坡度：相邻顶点在 X/Z 两个方向的高度字节差之平均。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>坡度(高度字节差)</returns>
        private float RawSlope(int vx, int vy)
        {
            int hL = HeightAtByte(vx, vy - 1);
            int hR = HeightAtByte(vx, vy + 1);
            int hU = HeightAtByte(vx - 1, vy);
            int hD = HeightAtByte(vx + 1, vy);
            return (Mathf.Abs(hR - hL) + Mathf.Abs(hD - hU)) * 0.5f;
        }

        /// <summary>
        /// 预先计算崖的坡度分档阈值。
        ///
        /// 做法：统计所有崖格中心顶点的坡度，取 1/3 与 2/3 分位数作为"缓坡/陡坡"阈值，
        /// 使三档（陡坡岩壁 / 中坡碎石 / 缓坡土石）各约占 1/3，分布与原版的均匀混杂一致。
        /// 崖格过少或配置关闭分布时，退回配置中的固定阈值。
        /// </summary>
        private void PrepareCliffThresholds()
        {
            cliffSteepThreshold = config.cliffSteepSlope;
            cliffGentleThreshold = config.cliffGentleSlope;

            if (!config.cliffLayerSpread)
                return;

            List<float> slopes = new List<float>();
            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    if (cellTerrain[cx, cy] != TerrainIds.Cliff)
                        continue;

                    CellCenterVertex(cx, cy, out int vx, out int vy);
                    slopes.Add(RawSlope(vx, vy));
                }
            }

            if (slopes.Count < 12)
                return;

            float[] samples = slopes.ToArray();
            cliffSteepThreshold = MapNoise.Quantile(samples, 2f / 3f);
            cliffGentleThreshold = MapNoise.Quantile(samples, 1f / 3f);
        }

        /// <summary>
        /// 为崖(山地)顶点挑选地表图层。
        ///
        /// 做法：
        ///   1. 用相邻顶点的高度字节差估算坡度；
        ///   2. 坡度叠加一层低频噪声后分成三档（陡坡岩壁 / 中坡碎石 / 缓坡土石），
        ///      噪声用于打散分档边界，避免出现整齐的条带；
        ///   3. 档内再用一层高频噪声二选一，得到最终图层；
        ///   4. 结果按目标地图的可用图层数上限过滤（越界时顺延到最近的合法候选）。
        /// </summary>
        /// <param name="vx">顶点 X</param>
        /// <param name="vy">顶点 Y</param>
        /// <returns>图层下标</returns>
        private int PickCliffLayer(int vx, int vy)
        {
            int[] palette = TerrainLayerMap.GetCliffPalette();
            int maxLayer = Mathf.Max(0, maxUsableLayer);

            // 1. 坡度：相邻顶点的平均高度字节差
            float slope = RawSlope(vx, vy);

            // 2. 低频噪声扰动坡度(-8 ~ +8)，打散分档边界
            float noise = MapNoise.FBM(vx * 0.14f, vy * 0.14f, 2, 0.5f, 2f, cliffNoiseX, cliffNoiseY) * 16f - 8f;
            float value = slope + noise;

            int band;
            if (value >= cliffSteepThreshold)
                band = 0;   // 陡坡岩壁
            else if (value <= cliffGentleThreshold)
                band = 2;   // 缓坡土石
            else
                band = 1;   // 中坡碎石

            // 3. 档内高频噪声二选一
            float pick = MapNoise.FBM(vx * 0.42f, vy * 0.42f, 1, 0.5f, 2f, cliffNoiseX + 91f, cliffNoiseY + 47f);
            int index = band * 2 + (pick > 0.5f ? 0 : 1);
            if (index < 0)
                index = 0;
            if (index >= palette.Length)
                index = palette.Length - 1;

            // 4. 过滤超出可用范围的候选：从当前档位向后找第一个合法图层
            int layer = 0;
            for (int i = 0; i < palette.Length; i++)
            {
                int candidate = palette[(index + i) % palette.Length];
                if (candidate <= maxLayer)
                {
                    layer = candidate;
                    break;
                }
            }

            return Mathf.Clamp(layer, 0, maxLayer);
        }

        /// <summary>
        /// 依据高度字节重算顶点法线。
        /// 顶点世界坐标为 (vy * quadSize, height * 0.5, vx * quadSize)，
        /// 因此世界 X 方向的坡度由 vy 方向的高度差决定，世界 Z 方向由 vx 方向决定。
        /// </summary>
        private void RecomputeNormals(int quadSize)
        {
            // 法线已改为按需计算（MapData.VertexNormal，建网格时由 MapCell 逐顶点算），
            // 不再需要预写；同时保证与「从 .bin 加载」的路径使用完全同一套算法。
        }

        /// <summary>取指定顶点的地面高度字节(越界时钳制到边界)</summary>
        private int HeightAtByte(int vx, int vy)
        {
            return ComputeHeightByte(vx, vy);
        }

        /// <summary>刷新地格通行遮罩并重建地表网格</summary>
        public void RefreshGrid()
        {
            for (int cx = 0; cx < cellsX; cx++)
                for (int cy = 0; cy < cellsY; cy++)
                    map.mapGrid.BeginUpdateMovable(cx, cy);
            map.mapGrid.EndUpdateMovable();

            if (map.mapTerrain != null)
                map.mapTerrain.Rebuild();
        }
    }
}
