using Sango.Core;
using System.Collections.Generic;

namespace Sango.MapGen
{
    /// <summary>
    /// 道路生成参数。
    /// </summary>
    public class RoadGenConfig
    {
        /// <summary>是否只连接相邻城市(按领地相邻关系)。关闭时连接所有城市对。</summary>
        public bool connectNeighborsOnly = true;
        /// <summary>穿越山地(崖)的代价；跨越后该格会变成栈道。</summary>
        public int cliffCost = 40;
        /// <summary>渡河(川/河)的代价；跨越后该格会变成渡所。</summary>
        public int riverCost = 25;
        /// <summary>是否禁止道路穿过海洋。</summary>
        public bool blockSea = true;
        /// <summary>
        /// 是否禁止道路穿过"水域"。
        /// 仅对地形数据里 isWater 为真的地形生效；浅滩在数据里是可通行的滩涂(isWater=false)，
        /// 因此不会被封死。
        /// </summary>
        public bool blockShallow = true;
        /// <summary>搜索包围盒的额外外扩格数，避免路径绕行时找不到路。</summary>
        public int margin = 10;
        /// <summary>最多处理的城市对数量；0 表示不限制。</summary>
        public int maxPairs = 0;
    }

    /// <summary>
    /// 道路生成(M4)。
    ///
    /// 算法：对每一对需要连通的城市做一次"限定包围盒的 Dijkstra"：
    ///   1. 代价取地形 baseCost；山地(崖)与河流取高额代价，海洋/浅滩直接禁行；
    ///   2. 回溯最优路径，把路径上的地格按原地形改写为：
    ///        普通陆地 → 主径(11)
    ///        山地(崖) → 栈道(12)
    ///        河流(川/河) → 渡所(13)
    ///   3. 城市格与已有道路格不会被覆盖。
    ///
    /// 通过包围盒限制搜索范围，避免整图 Dijkstra 在大地图上过慢。
    /// </summary>
    public static class RoadGenerator
    {
        /// <summary>道路类型集合(避免重复覆盖)</summary>
        private static bool IsRoadTerrain(int terrainType)
        {
            return terrainType == TerrainIds.RoadMain
                || terrainType == TerrainIds.Plank
                || terrainType == TerrainIds.Ford;
        }

        /// <summary>
        /// 生成城市之间的道路。
        /// </summary>
        /// <param name="ctx">生成上下文</param>
        /// <param name="config">道路参数</param>
        /// <returns>被改写为道路的地格数量</returns>
        public static int Generate(MapGenContext ctx, RoadGenConfig config)
        {
            if (ctx == null || ctx.cellTerrain == null || ctx.cities == null || ctx.cities.Count < 2)
                return 0;

            RoadGenConfig cfg = config ?? new RoadGenConfig();
            int cellsX = ctx.cellsX;
            int cellsY = ctx.cellsY;
            int total = cellsX * cellsY;
            if (total <= 0)
                return 0;

            SangoObjectSet<TerrainType> terrainTypes = GameData.Instance?.ScenarioCommonData?.TerrainTypes;

            // 城市ID → 据点
            Dictionary<int, CitySite> byId = new Dictionary<int, CitySite>();
            for (int i = 0; i < ctx.cities.Count; i++)
                byId[ctx.cities[i].id] = ctx.cities[i];

            // 收集需要连通的城市对
            List<int> fromIds = new List<int>();
            List<int> toIds = new List<int>();

            if (cfg.connectNeighborsOnly)
            {
                for (int i = 0; i < ctx.cities.Count; i++)
                {
                    CitySite site = ctx.cities[i];
                    for (int n = 0; n < site.neighbors.Count; n++)
                    {
                        int other = site.neighbors[n];
                        if (other > site.id && byId.ContainsKey(other))
                        {
                            fromIds.Add(site.id);
                            toIds.Add(other);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < ctx.cities.Count; i++)
                {
                    for (int j = i + 1; j < ctx.cities.Count; j++)
                    {
                        fromIds.Add(ctx.cities[i].id);
                        toIds.Add(ctx.cities[j].id);
                    }
                }
            }

            if (fromIds.Count == 0)
            {
                Sango.Log.Warning("道路生成：没有需要连通的城市对");
                return 0;
            }

            int[] dist = new int[total];
            int[] prev = new int[total];
            MinHeap heap = new MinHeap(1024);
            List<int> path = new List<int>();

            int roadCount = 0;
            int pairs = cfg.maxPairs > 0 ? System.Math.Min(cfg.maxPairs, fromIds.Count) : fromIds.Count;
            int failed = 0;

            for (int p = 0; p < pairs; p++)
            {
                if (!byId.TryGetValue(fromIds[p], out CitySite a) || !byId.TryGetValue(toIds[p], out CitySite b))
                    continue;

                int startIndex = a.x * cellsY + a.y;
                int targetIndex = b.x * cellsY + b.y;

                int distance = HexNeighbors.Distance(a.x, a.y, b.x, b.y);
                int nearMargin = cfg.margin + distance / 2;

                // 先在"两城包围盒 + margin"内搜索以缩小规模；
                // 若找不到（例如必须绕远路避开海洋与山脉），再扩大到整张地图重试，
                // 保证只要两城在同一陆块上就一定铺得出路，不会出现孤城。
                bool found = false;
                for (int attempt = 0; attempt < 2 && !found; attempt++)
                {
                    int margin = attempt == 0 ? nearMargin : System.Math.Max(cellsX, cellsY);

                    int minX = System.Math.Max(0, System.Math.Min(a.x, b.x) - margin);
                    int maxX = System.Math.Min(cellsX - 1, System.Math.Max(a.x, b.x) + margin);
                    int minY = System.Math.Max(0, System.Math.Min(a.y, b.y) - margin);
                    int maxY = System.Math.Min(cellsY - 1, System.Math.Max(a.y, b.y) + margin);

                    // 重置包围盒内数据
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            int index = x * cellsY + y;
                            dist[index] = int.MaxValue;
                            prev[index] = -1;
                        }
                    }

                    heap.Clear();
                    dist[startIndex] = 0;
                    heap.Push(0, startIndex);

                    while (heap.Count > 0)
                    {
                        int cur = heap.Pop(out int cost);
                        if (cost > dist[cur])
                            continue;
                        if (cur == targetIndex)
                        {
                            found = true;
                            break;
                        }

                        int cx = cur / cellsY;
                        int cy = cur % cellsY;

                        for (int d = 0; d < HexNeighbors.DirectionCount; d++)
                        {
                            int neighborIndex = HexNeighbors.GetNeighbor(cx, cy, d, cellsX, cellsY, out int nx, out int ny);
                            if (neighborIndex < 0)
                                continue;
                            // 限制在包围盒内，缩小搜索规模
                            if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                                continue;

                            int weight = CellCost(ctx, terrainTypes, nx, ny, cfg, out bool blocked);
                            if (blocked)
                                continue;

                            int newCost = cost + weight;
                            if (newCost < dist[neighborIndex])
                            {
                                dist[neighborIndex] = newCost;
                                prev[neighborIndex] = cur;
                                heap.Push(newCost, neighborIndex);
                            }
                        }
                    }

                    if (!found)
                        continue;

                    // 回溯路径并铺路
                    path.Clear();
                    int node = targetIndex;
                    while (node >= 0)
                    {
                        path.Add(node);
                        if (node == startIndex)
                            break;
                        node = prev[node];
                    }

                    for (int i = 0; i < path.Count; i++)
                    {
                        int index = path[i];
                        if (index == startIndex || index == targetIndex)
                            continue;

                        int cx = index / cellsY;
                        int cy = index % cellsY;
                        int current = ctx.cellTerrain[cx, cy];

                        if (IsRoadTerrain(current) || current == TerrainIds.City)
                            continue;

                        int roadTerrain;
                        if (current == TerrainIds.Cliff)
                            roadTerrain = TerrainIds.Plank;
                        else if (current == TerrainIds.Stream || current == TerrainIds.River)
                            roadTerrain = TerrainIds.Ford;
                        else
                            roadTerrain = TerrainIds.RoadMain;

                        ctx.cellTerrain[cx, cy] = (byte)roadTerrain;
                        roadCount++;
                    }
                }

                if (!found)
                    failed++;
            }

            if (failed > 0)
                Sango.Log.Warning(string.Format(
                    "道路生成：{0} 对城市未能连通(已扩大搜索范围重试，仍失败说明被水域隔断)", failed));

            Sango.Log.Info(string.Format("道路生成完成：铺设 {0} 个地格", roadCount));
            return roadCount;
        }

        /// <summary>
        /// 计算某地格作为道路的通行代价；不可通行时 blocked 为 true。
        /// </summary>
        private static int CellCost(MapGenContext ctx, SangoObjectSet<TerrainType> terrainTypes,
            int x, int y, RoadGenConfig cfg, out bool blocked)
        {
            blocked = false;
            int type = ctx.cellTerrain[x, y];

            if (type == TerrainIds.City)
                return 1;

            // 崖：穿山，铺设栈道
            if (type == TerrainIds.Cliff)
                return System.Math.Max(1, cfg.cliffCost);

            // 川/河：渡河，铺设渡所
            if (type == TerrainIds.Stream || type == TerrainIds.River)
                return System.Math.Max(1, cfg.riverCost);

            TerrainType terrainType = terrainTypes != null ? terrainTypes.Get(type) : null;

            // 水域能否通行一律以地形数据为准。
            // 注意：浅滩在数据里是"可通行的滩涂"(isWater=false, moveable=true)，
            // 不能按地形ID硬编码当成海封死，否则领地能跨越浅滩相连、道路却跨不过去，
            // 会造成城市邻接图与道路网割裂(出现孤城)。
            if (terrainType != null && terrainType.isWater)
            {
                bool block = type == TerrainIds.Sea ? cfg.blockSea : cfg.blockShallow;
                if (block)
                {
                    blocked = true;
                    return 0;
                }
                return type == TerrainIds.Sea ? 20 : 12;
            }

            if (terrainType == null || !terrainType.moveable)
            {
                blocked = true;
                return 0;
            }

            return System.Math.Max(1, terrainType.baseCost);
        }
    }
}
