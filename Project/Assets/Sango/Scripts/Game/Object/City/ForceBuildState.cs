/*
 * 文件名：ForceBuildState.cs
 * 描述：势力级资源水位快照 —— 供内政建筑规划器按"势力整体的钱粮兵装余缺"
 *       动态调整各类产出建筑的份额比例。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力资源水位快照（**不序列化**，换剧本由 <see cref="ScenarioLifecycle"/> 复位）。
    ///
    /// 【为什么需要它】内政建筑规划是**逐城**求解的（<see cref="BuildingPlanner"/>），
    /// 而"该多建农田还是多建市场"取决于**整个势力**的钱粮余缺：
    /// 只看本城会得到"局部最优、全局很糟"的结论（前线城屯满粮、后方城闹粮荒）。
    /// 但若让每座城各自遍历全势力城池，就是 O(城数²)。
    ///
    /// 于是折中：**每回合重建一次**（一次遍历 citySet，按势力归集），各城查询命中缓存。
    /// 复杂度 O(城数) / 回合，与势力数无关。
    /// </summary>
    public static class ForceBuildState
    {
        /// <summary>单个势力的资源水位快照（百分比 0~100）</summary>
        public struct Snapshot
        {
            /// <summary>资金水位：势力金库总额 ÷ 金库总容量 × 100</summary>
            public int goldFill;
            /// <summary>粮草水位：势力粮仓总额 ÷ 粮仓总容量 × 100</summary>
            public int foodFill;
            /// <summary>兵装水位：势力兵装总数 ÷ 守军总兵力 × 100</summary>
            public int armsFill;
        }

        /// <summary>每势力的归集槽位数：0金 1金上限 2粮 3粮上限 4兵装 5兵力</summary>
        const int SlotCount = 6;

        /// <summary>势力 id → 水位快照</summary>
        static readonly Dictionary<int, Snapshot> snapshots = new Dictionary<int, Snapshot>();

        /// <summary>缓存所依附的剧本（换剧本即失效）</summary>
        static Scenario cachedScenario;

        /// <summary>缓存对应的全局回合号（换回合即失效）</summary>
        static int cachedTurn = -1;

        /// <summary>
        /// 取某势力的资源水位快照（同一回合内只重建一次，之后全部命中缓存）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>水位快照（势力无城时各项为 0）</returns>
        public static Snapshot Get(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return default(Snapshot);

            EnsureCache(scenario);

            Snapshot s;
            if (snapshots.TryGetValue(force.Id, out s))
                return s;
            return default(Snapshot);
        }

        /// <summary>
        /// 确保本回合的水位表已建立。失效条件（任一变化即重建）：换剧本、换回合。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        static void EnsureCache(Scenario scenario)
        {
            int turn = scenario.TurnCount;
            if (ReferenceEquals(cachedScenario, scenario) && cachedTurn == turn)
                return;

            cachedScenario = scenario;
            cachedTurn = turn;
            snapshots.Clear();

            if (scenario.citySet == null)
                return;

            // 一次遍历，按势力归集"分子 / 分母"（避免按势力分别遍历 → O(势力数 × 城数)）
            Dictionary<int, int[]> acc = new Dictionary<int, int[]>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City c = scenario.citySet[i];
                if (c == null || !c.IsAlive || c.BelongForce == null)
                    continue;

                int forceId = c.BelongForce.Id;
                int[] a;
                if (!acc.TryGetValue(forceId, out a))
                {
                    a = new int[SlotCount];
                    acc[forceId] = a;
                }

                a[0] += c.gold;
                a[1] += c.GoldLimit;
                a[2] += c.food;
                a[3] += c.FoodLimit;
                a[4] += CountArms(c.itemStore);
                a[5] += c.troops;
            }

            foreach (KeyValuePair<int, int[]> kv in acc)
            {
                int[] a = kv.Value;
                Snapshot s;
                s.goldFill = Rate(a[0], a[1]);
                s.foodFill = Rate(a[2], a[3]);
                s.armsFill = Rate(a[4], a[5]);
                snapshots[kv.Key] = s;
            }
        }

        /// <summary>
        /// 统计兵装数量（枪 / 戟 / 弩 / 军马，与 <see cref="CitySituation"/> 同一口径）。
        /// </summary>
        /// <param name="store">兵器库</param>
        /// <returns>兵装总数</returns>
        static int CountArms(ItemStore store)
        {
            if (store == null)
                return 0;

            int total = 0;
            for (int id = (int)ItemStoreKindType.Spear; id <= (int)ItemStoreKindType.Horse; id++)
                total += store.GetNumber(id);
            return total;
        }

        /// <summary>百分比水位（0~100），分母 &lt;= 0 时视为 100（无容量概念即不构成缺口）。</summary>
        /// <param name="value">分子</param>
        /// <param name="max">分母</param>
        /// <returns>0~100 的百分比</returns>
        static int Rate(int value, int max)
        {
            if (max <= 0)
                return 100;

            int r = value * 100 / max;
            return r < 0 ? 0 : (r > 100 ? 100 : r);
        }

        /// <summary>清空全部运行期状态（换剧本时由 <see cref="ScenarioLifecycle"/> 调用）。</summary>
        public static void Clear()
        {
            snapshots.Clear();
            cachedScenario = null;
            cachedTurn = -1;
        }
    }
}
