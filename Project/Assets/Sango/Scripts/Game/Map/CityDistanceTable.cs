using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 据点距离双表 —— 对应 Open311 的 `kyoten_distance` / `kyoten_short_distance`
    /// （`s11_sys.cpp:31-92` 的 Floyd–Warshall，边权恒为 1）。
    ///
    /// 【两套边】
    ///   · **Normal 表**：只走"目标格可通行"的邻接边（原版按 Normal / Sea / Poison 地形过滤）；
    ///   · **Short 表**：全部邻接边（含被普通表排除的边，供「难所行军」类科技使用）。
    ///
    /// 【与 Scenario.GetCityDistance 的关系】后者是"按需 BFS + 逐对缓存"（`cityPathMap`），
    /// 每次只算一对；本类是"开局一次算全表"，对应原版 `ai_.kyoten_distance` 的用法 ——
    /// AI 每回合要取大量城对距离，逐个 BFS 会把开销放大到不可接受。
    ///
    /// 【为什么用 FW 而不是每对 BFS】n（据点总数）在三国志量级只有几十，
    /// O(n³) 一次算完 ≈ 十几万次加法；而"每对 BFS"在 AI 回合里是 O(n² × 边数)，
    /// 且无法复用。原版选 FW 就是这个原因。
    /// </summary>
    public class CityDistanceTable
    {
        /// <summary>不可达（对应原版距离表里的 INF 值；对外统一返回 -1）</summary>
        public const int Unreachable = 999;

        City[] cities;
        Dictionary<int, int> idToIndex;
        int[,] normal;
        int[,] shortRoute;

        /// <summary>表的规模（参与计算的据点数）</summary>
        public int Count { get { return cities != null ? cities.Length : 0; } }

        /// <summary>
        /// 建表。幂等：重复调用会整表重建。
        ///
        /// 调用时机：`Scenario.Init` 里、所有对象 `Init` 完成之后（此时 `City.NeighborList`
        /// 已经填好）。运行时城数变化（攻陷不销毁据点）不影响邻接，无需重建。
        /// </summary>
        public void Build(Scenario scenario)
        {
            cities = null;
            idToIndex = null;
            normal = null;
            shortRoute = null;

            if (scenario == null || scenario.citySet == null)
                return;

            List<City> list = new List<City>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City c = scenario.citySet[i];
                if (c != null)
                    list.Add(c);
            }

            int n = list.Count;
            if (n == 0)
                return;

            cities = list.ToArray();
            idToIndex = new Dictionary<int, int>(n);
            for (int i = 0; i < n; i++)
                idToIndex[cities[i].Id] = i;

            normal = new int[n, n];
            shortRoute = new int[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    normal[i, j] = i == j ? 0 : Unreachable;
                    shortRoute[i, j] = i == j ? 0 : Unreachable;
                }
            }

            for (int i = 0; i < n; i++)
            {
                City c = cities[i];
                if (c.NeighborList == null) continue;

                for (int k = 0; k < c.NeighborList.Count; k++)
                {
                    City nb = c.NeighborList[k];
                    if (nb == null) continue;

                    int j;
                    if (!idToIndex.TryGetValue(nb.Id, out j))
                        continue;

                    // 短表：所有邻接边
                    shortRoute[i, j] = 1;
                    shortRoute[j, i] = 1;

                    // 普通表：默认建立所有邻接边（与短表一致）。
                    // 【对齐说明】原版 calc_kyoten_distance 判断的是"邻居 route"（s11_sys.cpp:67-76：
                    //   Route_Normal / Route_Sea / Route_Poison 建立边，Route_Cliffroad / Route_Shortcut /
                    //   Route_Ford 不建立），而不是"邻居城中心格地形"。城中心格永远是"城"地形，
                    //   用它判断会把 normal 表全置成 Unreachable（999），导致 AI 攻击权重被除以 10。
                    //   本项目 Open311ScenarioParser 目前只导入 Neighbors 的 area id、未导入 route，
                    //   故先以"邻接即建立边"近似；待 route 导入后再精确排除栈道/小径/渡口。
                    normal[i, j] = 1;
                    normal[j, i] = 1;
                }
            }

            Floyd(normal, n);
            Floyd(shortRoute, n);
        }

        /// <summary>
        /// 该地形是否允许作为普通表的边 —— 对应原版 `kyoten_distance` 的
        /// Normal / Sea / Poison 三类（`s11_sys.cpp:31-92`）。
        ///
        /// 【暂未使用】原版判断的是"邻居 route"而非"城中心格地形"，城中心格永远是"城"地形，
        /// 用本方法判断会把 normal 表全置为不可达（见 <see cref="Build"/> 里的说明）。
        /// 现 Build 已改为"邻接即建立边"，本方法保留仅供 route 数据导入后恢复精确判断。
        ///
        /// 【Id 映射】项目地形 Id 比原版 TerrainId **大 1**（项目多了一个占位），
        /// 所以这里统一 `-1` 换算后再查表。原版地形顺序（`TerrainType.fireCoef` 注释同源）：
        ///   0 草原 1 土 2 砂 3 湿地 4 毒泉 5 森林 6 小河 7 河 8 海 9 荒地
        ///   10 大路 11 栈道 12 桥 13 渡口 14 水边 15 崖 16 城 17 港 18 关 19 小径
        /// 排除：栈道(11)、水边(14)、崖(15)、城(16)、港(17)、关(18)。
        /// </summary>
        /// <param name="projectTerrainId">项目地形 Id</param>
        static bool IsKyotenDistanceTerrain(int projectTerrainId)
        {
            int o311 = projectTerrainId - 1;

            switch (o311)
            {
                // Normal（陆地可走）
                case 0:  // 草原
                case 1:  // 土
                case 2:  // 砂
                case 3:  // 湿地
                case 5:  // 森林
                case 9:  // 荒地
                case 10: // 大路
                case 19: // 小径
                    return true;

                // Sea（水上可走：小河 / 河 / 海 / 桥 / 渡口）
                case 6:
                case 7:
                case 8:
                case 12:
                case 13:
                    return true;

                // Poison（毒泉也可作为边，只是通行有代价）
                case 4:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>标准 Floyd–Warshall（边权 1，就地更新）</summary>
        static void Floyd(int[,] dist, int n)
        {
            for (int k = 0; k < n; k++)
            {
                for (int i = 0; i < n; i++)
                {
                    int dik = dist[i, k];
                    if (dik >= Unreachable) continue;

                    for (int j = 0; j < n; j++)
                    {
                        int dkj = dist[k, j];
                        if (dkj >= Unreachable) continue;

                        int nd = dik + dkj;
                        if (nd < dist[i, j])
                            dist[i, j] = nd;
                    }
                }
            }
        }

        /// <summary>
        /// 取两据点之间的距离。
        /// </summary>
        /// <param name="a">起点据点</param>
        /// <param name="b">终点据点</param>
        /// <param name="shortRoute">true = 用 Short 表（对应科技「难所行军」）</param>
        /// <returns>距离；不可达或表未建返回 -1</returns>
        public int GetDistance(City a, City b, bool shortRoute)
        {
            if (a == null || b == null || idToIndex == null)
                return -1;

            if (a == b)
                return 0;

            int i, j;
            if (!idToIndex.TryGetValue(a.Id, out i)) return -1;
            if (!idToIndex.TryGetValue(b.Id, out j)) return -1;

            int d = shortRoute ? shortRouteTable(i, j) : normalTable(i, j);
            return d >= Unreachable ? -1 : d;
        }

        int shortRouteTable(int i, int j) { return shortRoute[i, j]; }
        int normalTable(int i, int j) { return normal[i, j]; }
    }
}
