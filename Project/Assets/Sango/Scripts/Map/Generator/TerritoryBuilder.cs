using Sango.Core;
using System.Collections.Generic;

namespace Sango.MapGen
{
    /// <summary>
    /// 领地划分参数。
    /// </summary>
    public class TerritoryGenConfig
    {
        /// <summary>是否按地形代价划分领地(否则按格数等权)。</summary>
        public bool useTerrainCost = true;
        /// <summary>是否把城址格的地形改写为"都市"。</summary>
        public bool placeCityTerrain = true;
        /// <summary>城市周边内政区的半径(格)。</summary>
        public int interiorRadius = 3;
        /// <summary>
        /// 穿行"不可通行的陆地"(山地/岸)的代价(每格)。
        /// 水域(海/浅滩/川/河)一律不可穿行；不可通行的陆地上以该高代价穿行，
        /// 保证同一陆块上的领地最终必然相接——否则领地在山地屏障处断开，
        /// 会连带把城市邻接图与道路网割裂成互不可达的多块。
        /// 代价给得足够高，领地仍会优先绕行，边界依然倾向沿山脊分布。
        /// </summary>
        public int impassableCost = 999;
        /// <summary>
        /// 是否按"领地相接"自动回填城市相邻关系。
        /// 使用真实三国城池表落位时应关闭，以保留原版邻接表，
        /// 使剧本的城市图与地图上的道路网保持一致。
        /// </summary>
        public bool linkByTerritory = true;
    }

    /// <summary>
    /// 领地划分(M3)。
    ///
    /// 算法：多源 Dijkstra（等价于带代价的 Voronoi 划分）：
    ///   1. 以所有城市所在格为源点，代价 0；
    ///   2. 沿六边形邻居扩散，代价取地形通行代价(baseCost)；
    ///   3. 每个地格归属"最先/最便宜到达"的城市 → areaId；
    ///   4. 水域地格不归属任何城市；"不可通行的陆地"(山地/岸)以高代价穿行(见 impassableCost)。
    ///
    /// 由于边界由代价决定，领地会自然沿山脊、河流等天然屏障划分；
    /// 但山地只是"高代价"而非"不可穿"，因此同一陆块上的领地必然最终相接，
    /// 城市邻接图与道路网不会被山地割裂。
    /// </summary>
    public static class TerritoryBuilder
    {
        /// <summary>
        /// 执行领地划分，并写入 ctx.cellArea / ctx.cellState，同时回填城市相邻关系。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="sites">城市据点(需已生成)</param>
        /// <param name="config">划分参数</param>
        /// <returns>成功归入势力的城市数量</returns>
        public static int Build(MapGenContext ctx, List<CitySite> sites, TerritoryGenConfig config)
        {
            if (ctx == null || ctx.cellTerrain == null || sites == null || sites.Count == 0)
                return 0;

            TerritoryGenConfig cfg = config ?? new TerritoryGenConfig();
            int cellsX = ctx.cellsX;
            int cellsY = ctx.cellsY;
            int total = cellsX * cellsY;
            if (total <= 0)
                return 0;

            SangoObjectSet<TerrainType> terrainTypes = GameData.Instance?.ScenarioCommonData?.TerrainTypes;

            int[] areaOf = new int[total];
            int[] best = new int[total];
            for (int i = 0; i < total; i++)
            {
                areaOf[i] = 0;
                best[i] = int.MaxValue;
            }

            MinHeap heap = new MinHeap(1024);
            int seedCount = 0;
            for (int s = 0; s < sites.Count; s++)
            {
                CitySite site = sites[s];
                if (site.x < 0 || site.y < 0 || site.x >= cellsX || site.y >= cellsY)
                    continue;

                int index = site.x * cellsY + site.y;
                if (best[index] <= 0)
                    continue;

                best[index] = 0;
                areaOf[index] = site.id;
                heap.Push(0, index);
                seedCount++;
            }

            // 多源 Dijkstra
            while (heap.Count > 0)
            {
                int index = heap.Pop(out int cost);
                if (cost > best[index])
                    continue;

                int cx = index / cellsY;
                int cy = index % cellsY;
                int owner = areaOf[index];

                for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                {
                    int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                    if (neighborIndex < 0)
                        continue;

                    int nt = ctx.cellTerrain[nx, ny];
                    TerrainType terrainType = terrainTypes != null ? terrainTypes.Get(nt) : null;

                    // 水域(海/浅滩/川/河)一律不可跨越；"不可通行的陆地"(山地/岸)允许以高代价穿过，
                    // 否则领地会在山地屏障处断开，进而把城市邻接图与道路网割裂成互不可达的多块。
                    if (terrainType == null || terrainType.isWater)
                        continue;

                    int weight = cfg.useTerrainCost ? System.Math.Max(1, terrainType.baseCost) : 1;
                    if (!terrainType.moveable)
                        weight = System.Math.Max(weight, System.Math.Max(1, cfg.impassableCost));

                    int newCost = cost + weight;
                    if (newCost < best[neighborIndex])
                    {
                        best[neighborIndex] = newCost;
                        areaOf[neighborIndex] = owner;
                        heap.Push(newCost, neighborIndex);
                    }
                }
            }

            // 写回上下文
            ctx.cellArea = new int[cellsX, cellsY];
            ctx.cellState = new int[cellsX, cellsY];
            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    ctx.cellArea[cx, cy] = areaOf[cx * cellsY + cy];
                }
            }

            // 城址地形改写为都市
            if (cfg.placeCityTerrain)
            {
                for (int s = 0; s < sites.Count; s++)
                {
                    CitySite site = sites[s];
                    if (site.x < 0 || site.y < 0 || site.x >= cellsX || site.y >= cellsY)
                        continue;
                    ctx.cellTerrain[site.x, site.y] = (byte)TerrainIds.City;
                }
            }

            // 城市周边内政区
            int interiorBit = 1 << (int)Render.MapGrid.GridState.Interior;
            int interiorRadius = System.Math.Max(0, cfg.interiorRadius);
            for (int s = 0; s < sites.Count; s++)
            {
                CitySite site = sites[s];
                MarkInterior(ctx, areaOf, site, interiorBit, interiorRadius);
            }

            // 统计城市相邻关系：使用真实城池表时保留原版邻接表，不按领地覆盖
            if (cfg.linkByTerritory)
                BuildNeighborList(ctx, sites, areaOf, cellsX, cellsY);

            Sango.Log.Info(string.Format("领地划分完成：{0} 座城市，{1} 个地格已归属", seedCount, CountOwned(areaOf)));
            return seedCount;
        }

        /// <summary>
        /// 从城市所在格出发，在半径内标记内政区(仅限本城领地)。
        /// </summary>
        private static void MarkInterior(MapGenContext ctx, int[] areaOf, CitySite site, int interiorBit, int radius)
        {
            int cellsX = ctx.cellsX;
            int cellsY = ctx.cellsY;
            if (site.x < 0 || site.y < 0 || site.x >= cellsX || site.y >= cellsY)
                return;

            Queue<int> queue = new Queue<int>();
            int start = site.x * cellsY + site.y;
            int[] dist = new int[cellsX * cellsY];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = -1;

            dist[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                if (dist[index] > radius)
                    continue;

                int cx = index / cellsY;
                int cy = index % cellsY;
                ctx.cellState[cx, cy] |= interiorBit;

                if (dist[index] == radius)
                    continue;

                for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                {
                    int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                    if (neighborIndex < 0)
                        continue;
                    // 只标记本城领地内、且尚未访问的格子
                    if (areaOf[neighborIndex] != site.id)
                        continue;
                    if (dist[neighborIndex] >= 0)
                        continue;

                    dist[neighborIndex] = dist[index] + 1;
                    queue.Enqueue(neighborIndex);
                }
            }
        }

        /// <summary>
        /// 统计城市之间的相邻关系，回填到 CitySite.neighbors。
        /// </summary>
        private static void BuildNeighborList(MapGenContext ctx, List<CitySite> sites, int[] areaOf, int cellsX, int cellsY)
        {
            Dictionary<int, CitySite> map = new Dictionary<int, CitySite>();
            for (int s = 0; s < sites.Count; s++)
                map[sites[s].id] = sites[s];

            HashSet<long> pairs = new HashSet<long>();
            for (int cx = 0; cx < cellsX; cx++)
            {
                for (int cy = 0; cy < cellsY; cy++)
                {
                    int owner = areaOf[cx * cellsY + cy];
                    if (owner <= 0)
                        continue;

                    for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                    {
                        int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                        if (neighborIndex < 0)
                            continue;

                        int other = areaOf[neighborIndex];
                        if (other <= 0 || other == owner)
                            continue;

                        long key = owner < other
                            ? ((long)owner << 32) | (uint)other
                            : ((long)other << 32) | (uint)owner;
                        if (!pairs.Add(key))
                            continue;

                        if (map.TryGetValue(owner, out CitySite a) && !a.neighbors.Contains(other))
                            a.neighbors.Add(other);
                        if (map.TryGetValue(other, out CitySite b) && !b.neighbors.Contains(owner))
                            b.neighbors.Add(owner);
                    }
                }
            }
        }

        /// <summary>
        /// 统计已归属的地格数量。
        /// </summary>
        private static int CountOwned(int[] areaOf)
        {
            int count = 0;
            for (int i = 0; i < areaOf.Length; i++)
            {
                if (areaOf[i] > 0)
                    count++;
            }
            return count;
        }

    }
}
