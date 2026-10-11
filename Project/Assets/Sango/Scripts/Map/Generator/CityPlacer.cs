using Sango.Core;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Sango.MapGen
{
    /// <summary>
    /// 城市选址参数。
    /// </summary>
    public class CityGenConfig
    {
        /// <summary>期望生成的城市数量。</summary>
        public int cityCount = 24;
        /// <summary>城市之间的最小六边形距离(格)。</summary>
        public int minSpacing = 6;
        /// <summary>城市内政区半径(格)。</summary>
        public int interiorRadius = 3;
        /// <summary>随机种子；为 0 时沿用地形种子。</summary>
        public int seed = 0;
        /// <summary>临河加成(权重)，越大越倾向沿河建城。</summary>
        public float riversideBonus = 0.6f;
        /// <summary>临海加成(权重)，越大越倾向沿海建城。</summary>
        public float coastBonus = 0.4f;
        /// <summary>低海拔加成(权重)，越大越倾向平原建城。</summary>
        public float lowlandBonus = 0.5f;
        /// <summary>恶劣地形(湿地/砂地/荒地)惩罚(权重)。</summary>
        public float badTerrainPenalty = 0.6f;
        /// <summary>
        /// 可建城的连通块相对最大陆地连通块的最小占比(0~1)。
        /// 小于该比例的碎块视为孤岛，不参与选址，避免出现不可达的城市。
        /// </summary>
        [Range(0f, 1f)] public float mainLandMinRatio = 0.15f;
        /// <summary>城市命名前缀，最终名称为 前缀 + 编号。</summary>
        public string namePrefix = "城";
        /// <summary>
        /// 是否改用"真实三国城池表"(ChinaCityTable)落城：坐标由原版中国图的 lo/hi 等比换算得到，
        /// 名称与相邻关系直接采用原版数据，从而让地图的道路网与剧本的城市图保持一致。
        /// 开启后本类中的选址评分、最小间距等参数不再生效。
        /// </summary>
        public bool useFixedSites = false;
    }

    /// <summary>
    /// 城市据点数据。
    /// </summary>
    public class CitySite
    {
        /// <summary>城市ID(从 1 开始，与剧本 citySet 的键对应)</summary>
        public int id;
        /// <summary>地格 X 坐标</summary>
        public int x;
        /// <summary>地格 Y 坐标</summary>
        public int y;
        /// <summary>城市名称</summary>
        public string name;
        /// <summary>选址评分(调试用)</summary>
        public float score;
        /// <summary>相邻城市ID列表</summary>
        public List<int> neighbors = new List<int>();
    }

    /// <summary>
    /// 城市选址(M3)。
    ///
    /// 算法：基于"适宜度评分 + 贪心间隔"的选址：
    ///   1. 对每个可通行陆地地格计算适宜度（低海拔、临河、临海加分，恶劣地形扣分，叠加稳定噪声）；
    ///   2. 按适宜度从高到低依次尝试落城，与已选城市保持最小六边形距离；
    ///   3. 数量不足时逐步放宽最小间距，保证尽量达到目标数量。
    ///
    /// 该做法等价于"重要性采样 + 泊松盘"，实现简单且结果可控。
    /// </summary>
    public static class CityPlacer
    {
        /// <summary>
        /// 在上下文中生成城市据点。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="config">选址参数</param>
        /// <returns>城市据点列表(按ID升序)</returns>
        public static List<CitySite> Generate(MapGenContext ctx, CityGenConfig config)
        {
            List<CitySite> sites = new List<CitySite>();
            if (ctx == null || ctx.cellTerrain == null)
                return sites;

            CityGenConfig cfg = config ?? new CityGenConfig();
            int cellsX = ctx.cellsX;
            int cellsY = ctx.cellsY;
            int total = cellsX * cellsY;
            if (total <= 0)
                return sites;

            if (cfg.useFixedSites)
                return GenerateFixedSites(ctx, cfg);

            if (cfg.cityCount <= 0)
                return sites;

            float[] score = BuildSuitability(ctx, cfg);

            // 按评分从高到低排序（评分 < 0 的不可用格不参与）
            List<int> candidates = new List<int>();
            for (int i = 0; i < total; i++)
            {
                if (score[i] > 0f)
                    candidates.Add(i);
            }
            candidates.Sort((a, b) => score[b].CompareTo(score[a]));

            int spacing = Mathf.Max(1, cfg.minSpacing);

            // 逐步放宽间距，尽量凑够城市数量
            while (spacing >= 2 && sites.Count < cfg.cityCount)
            {
                for (int i = 0; i < candidates.Count && sites.Count < cfg.cityCount; i++)
                {
                    int index = candidates[i];
                    if (index < 0)
                        continue;

                    int cx = index / cellsY;
                    int cy = index % cellsY;

                    bool tooClose = false;
                    for (int s = 0; s < sites.Count; s++)
                    {
                        if (HexNeighbors.Distance(cx, cy, sites[s].x, sites[s].y) < spacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose)
                        continue;

                    sites.Add(new CitySite
                    {
                        id = sites.Count + 1,
                        x = cx,
                        y = cy,
                        name = cfg.namePrefix + (sites.Count + 1),
                        score = score[index]
                    });
                }

                if (sites.Count >= cfg.cityCount)
                    break;

                spacing--;
            }

            ctx.cities = sites;
            Sango.Log.Info(string.Format("城市选址完成：共 {0} 座城市(目标 {1})", sites.Count, cfg.cityCount));
            return sites;
        }

        /// <summary>
        /// 按真实三国城池表落城：
        ///   1. 坐标由 ChinaCityTable 的 lo/hi 经 MapGenContext.CellFromChina 等比换算；
        ///   2. 若落点不可建城(水域/河流/山地)，在附近螺旋搜索最近的可建格吸附；
        ///   3. 相邻关系直接采用原版邻接表，使地图道路网与剧本城市图一致。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="cfg">选址参数</param>
        /// <returns>城市据点列表(按ID升序)</returns>
        private static List<CitySite> GenerateFixedSites(MapGenContext ctx, CityGenConfig cfg)
        {
            List<CitySite> sites = new List<CitySite>();
            SangoObjectSet<TerrainType> terrainTypes = GameData.Instance?.ScenarioCommonData?.TerrainTypes;

            int snapped = 0;
            for (int i = 0; i < ChinaCityTable.All.Length; i++)
            {
                ChinaCityTable.Entry entry = ChinaCityTable.All[i];
                ctx.CellFromChina(entry.lo, entry.hi, out int cx, out int cy);

                int rawX = cx;
                int rawY = cy;
                if (!SnapToBuildable(ctx, terrainTypes, ref cx, ref cy))
                {
                    Sango.Log.Warning("真实城池落点附近没有可建城地格，已跳过：" + entry.name);
                    continue;
                }
                if (cx != rawX || cy != rawY)
                    snapped++;

                CitySite site = new CitySite
                {
                    id = entry.id,
                    x = cx,
                    y = cy,
                    name = string.IsNullOrEmpty(entry.name) ? (cfg.namePrefix + entry.id) : entry.name,
                    score = 1f
                };

                if (entry.neighbors != null)
                    site.neighbors.AddRange(entry.neighbors);

                sites.Add(site);
            }

            sites.Sort((a, b) => a.id.CompareTo(b.id));
            ctx.cities = sites;
            Sango.Log.Info(string.Format("真实城池落位完成：共 {0} 座(其中 {1} 座做过吸附)", sites.Count, snapped));
            return sites;
        }

        /// <summary>
        /// 把坐标吸附到最近的可建城地格(由近及远的螺旋外扩搜索)。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="terrainTypes">地形类型表</param>
        /// <param name="cx">地格 X(输入输出)</param>
        /// <param name="cy">地格 Y(输入输出)</param>
        /// <returns>找到可建格返回 true</returns>
        private static bool SnapToBuildable(MapGenContext ctx, SangoObjectSet<TerrainType> terrainTypes,
            ref int cx, ref int cy)
        {
            if (IsBuildable(ctx, terrainTypes, cx, cy))
                return true;

            const int maxRadius = 12;
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r)
                            continue;

                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (IsBuildable(ctx, terrainTypes, nx, ny))
                        {
                            cx = nx;
                            cy = ny;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 该地格是否可建城：地形可通行、非水域、非山地与河流。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="terrainTypes">地形类型表</param>
        /// <param name="cx">地格 X</param>
        /// <param name="cy">地格 Y</param>
        /// <returns>可建城返回 true</returns>
        private static bool IsBuildable(MapGenContext ctx, SangoObjectSet<TerrainType> terrainTypes, int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= ctx.cellsX || cy >= ctx.cellsY)
                return false;

            int type = ctx.cellTerrain[cx, cy];
            TerrainType tt = terrainTypes != null ? terrainTypes.Get(type) : null;
            return tt != null && tt.moveable && !tt.isWater
                && type != TerrainIds.Cliff
                && type != TerrainIds.Stream
                && type != TerrainIds.River;
        }

        /// <summary>
        /// 计算每个地格的选址适宜度；不可用格返回 0。
        /// </summary>
        private static float[] BuildSuitability(MapGenContext ctx, CityGenConfig cfg)
        {
            int cellsX = ctx.cellsX;
            int cellsY = ctx.cellsY;
            int total = cellsX * cellsY;
            float[] score = new float[total];

            SangoObjectSet<TerrainType> terrainTypes = GameData.Instance?.ScenarioCommonData?.TerrainTypes;

            // 只在大陆(足够大的连通块)上选址，避免城市落在被山/水隔断的孤岛
            bool[] mainLand = cfg.mainLandMinRatio > 0f
                ? ctx.ComputeMainLandMask(cfg.mainLandMinRatio)
                : null;

            int citySeed = cfg.seed != 0 ? cfg.seed : ctx.config.seed;
            MapNoise.MakeSeedOffset(citySeed + 991, out float offsetX, out float offsetY);

            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    int type = ctx.cellTerrain[cx, cy];
                    TerrainType terrainType = terrainTypes != null ? terrainTypes.Get(type) : null;

                    // 必须可通行、非水域(海/川/河)、非山地
                    bool usable = terrainType != null && terrainType.moveable
                        && !terrainType.isWater && type != TerrainIds.Cliff;
                    if (!usable)
                        continue;

                    if (mainLand != null && !mainLand[cx * cellsY + cy])
                        continue;

                    float value = 1f;

                    // 低海拔加分
                    float altitude = Mathf.InverseLerp(ctx.thresholds.seaLevel, 1f, ctx.CellHeightAt(cx, cy));
                    value += (1f - altitude) * cfg.lowlandBonus;

                    // 临河 / 临海加分，并识别恶劣地形
                    bool riverside = false;
                    bool coast = false;
                    for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                    {
                        int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                        if (neighborIndex < 0)
                            continue;
                        int nt = ctx.cellTerrain[nx, ny];
                        if (nt == TerrainIds.Stream || nt == TerrainIds.River)
                            riverside = true;
                        else if (nt == TerrainIds.Sea || nt == TerrainIds.Shallow)
                            coast = true;
                    }
                    if (riverside) value += cfg.riversideBonus;
                    if (coast) value += cfg.coastBonus;

                    if (type == TerrainIds.Marsh || type == TerrainIds.Sand || type == TerrainIds.Waste)
                        value -= cfg.badTerrainPenalty;

                    // 稳定噪声打散评分，避免规则化排列
                    value += MapNoise.FBM(cx * 0.31f, cy * 0.31f, 2, 0.5f, 2f, offsetX, offsetY) * 0.4f;

                    score[cx * cellsY + cy] = Mathf.Max(0.01f, value);
                }
            }

            return score;
        }
    }
}
