using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// AI 部队的"改道兜底"规则：入城装不下、盟友城协防不划算时，把部队改派到别处。
    ///
    /// 【原版无对应实现 —— 规则第 5 条自主设计】
    /// Open311 的 <c>unit_enter</c> 直接调用 <c>building_add_troops</c>
    /// （s11_sys_scen_unit.cpp:573），而 <c>building_add_troops</c> 对超上限部分
    /// 用 <c>clamp</c> 截断、返回实际变化量（s11_sys_scen_building.cpp:1145-1154），
    /// 调用方不接返回值 —— 也就是说**原版确实会把溢出兵力丢掉**。
    ///
    /// 本项目按玩家反馈改成：
    /// <list type="bullet">
    /// <item>入城会溢出 → AI 不进城，改道最近的有余量据点（玩家侧保持原版行为，不做处理）；</item>
    /// <item>盟友城协防不划算（打不过 / 够不着） → 立即召回最近的自有城。</item>
    /// </list>
    /// 所有阈值走 <see cref="AIConfig"/>，不硬编码。
    /// </summary>
    public static class TroopAIFallback
    {
        /// <summary>
        /// 是否为 AI 势力部队（玩家直属 / 玩家委任军团都算玩家侧）。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>AI 部队返回 true</returns>
        public static bool IsAiTroop(Troop troop)
        {
            return troop != null && troop.BelongForce != null && !troop.BelongForce.IsPlayer;
        }

        /// <summary>
        /// 据点还能再收多少兵。
        /// </summary>
        /// <param name="city">目标据点</param>
        /// <returns>剩余容量（不小于 0）</returns>
        public static int GetRoom(City city)
        {
            if (city == null)
                return 0;
            return System.Math.Max(city.TroopsLimit - city.troops, 0);
        }

        /// <summary>
        /// 入城溢出拦截：装不下就改道别处，返回 true 表示已改道（调用方不要再执行入城）。
        ///
        /// 玩家侧不调用本方法 —— 保持原版"超出上限即丢弃"的行为。
        /// </summary>
        /// <param name="troop">准备入城的部队</param>
        /// <param name="city">目标据点（已被校验为本势力）</param>
        /// <param name="scenario">场景</param>
        /// <returns>已改道返回 true；没有可去之处返回 false（由调用方按原逻辑处理）</returns>
        public static bool TryRerouteForOverflow(Troop troop, City city, Scenario scenario)
        {
            if (troop == null || city == null)
                return false;

            City reroute = FindNearestCityWithRoom(troop, city, scenario);
            if (reroute != null)
            {
                Sango.Log.Info($"[AI防吞兵]{troop.BelongForce?.Name}的[{troop.Name}]无法进入{city.Name}"
                               + $"（兵力 {city.troops}/{city.TroopsLimit}，部队 {troop.troops}），改道{reroute.Name}"
                               + $"（兵力 {reroute.troops}/{reroute.TroopsLimit}）");
                troop.SetMission(MissionType.TroopMovetoCity, reroute.Id);
                troop.NeedPrepareMission();
                return true;
            }

            // 全部据点都装不下：按原版行为丢弃溢出部分，但必须留痕，否则又变成"兵凭空消失"
            int lost = troop.troops - GetRoom(city);
            Sango.Log.Warning($"[AI防吞兵]{troop.BelongForce?.Name}的[{troop.Name}]进入{city.Name}时"
                              + $"溢出 {lost} 兵且没有其他可容纳据点，按原版规则丢弃");
            return false;
        }

        /// <summary>
        /// 找最近的、还装得下这支部队的本势力都市。
        ///
        /// 只考虑都市（<see cref="City.IsCity"/>）：关隘 / 港口容量小，作为改道目标容易再次溢出。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="exclude">排除的据点（刚被判定装不下的那座）</param>
        /// <param name="scenario">场景</param>
        /// <returns>可改道的据点；没有返回 null</returns>
        public static City FindNearestCityWithRoom(Troop troop, City exclude, Scenario scenario)
        {
            if (troop == null || scenario == null || scenario.citySet == null)
                return null;

            City best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || !city.IsCity())
                    continue;
                if (city == exclude || city.BelongForce != troop.BelongForce)
                    continue;
                // 至少要装得下这支部队，否则改道过去还是会溢出
                if (GetRoom(city) < troop.troops)
                    continue;

                int distance = GetDistance(troop, city, scenario);
                if (distance < 0 || distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = city;
            }

            return best;
        }

        /// <summary>
        /// 召回最近的自有城（不要求容量：返城可以就解散，容量问题由入城拦截再兜一次底）。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="scenario">场景</param>
        /// <param name="reason">日志里说明为什么要召回</param>
        /// <returns>已改任务返回 true</returns>
        public static bool RecallToNearestOwnCity(Troop troop, Scenario scenario, string reason)
        {
            if (troop == null || troop.BelongForce == null)
                return false;

            City target = FindNearestAnyOwnCity(troop, scenario);
            if (target == null)
                return false;

            // 已经在目标城格上：交给入城流程，不再重复改任务
            if (troop.cell != null && troop.cell.building == target)
                return false;

            Sango.Log.Info($"[AI协防]{troop.BelongForce?.Name}的[{troop.Name}]：{reason}，召回{target.Name}");
            troop.SetMission(MissionType.TroopMovetoCity, target.Id);
            troop.NeedPrepareMission();
            return true;
        }

        /// <summary>
        /// 判断这支部队能否为盟友城投入协防。
        ///
        /// 判定只有两条，都可配置、都可解释：
        /// <list type="number">
        /// <item>够得着：距离不超过 <see cref="AIConfig.allySupportMaxDistance"/>；</item>
        /// <item>打得过：(部队兵力 + 城内兵力) 与围城敌军兵力之比不低于
        ///       <see cref="AIConfig.allySupportMinTroopRatio"/>（%）。</item>
        /// </list>
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="city">盟友城</param>
        /// <param name="scenario">场景</param>
        /// <returns>值得投入协防返回 true</returns>
        public static bool CanSupportDefense(Troop troop, City city, Scenario scenario)
        {
            if (troop == null || city == null || scenario == null || scenario.Map == null)
                return false;

            AIConfig cfg = AIConfig.Instance;

            int distance = GetDistance(troop, city, scenario);
            if (distance < 0 || distance > cfg.allySupportMaxDistance)
                return false;

            int enemyTroops = SumEnemyTroopsAround(city, troop, scenario);
            // 周边没有可打的敌人：驻防不亏（真正"无敌人"由 TroopProtectCity 的任务完成判定处理）
            if (enemyTroops <= 0)
                return true;

            long ours = (long)(troop.troops + city.troops) * 100;
            long theirs = (long)enemyTroops * cfg.allySupportMinTroopRatio;
            return ours >= theirs;
        }

        /// <summary>
        /// 扫描据点周边的敌方部队兵力合计。
        ///
        /// 用 <see cref="Map.GetSpiral"/> 填充静态缓冲（避免 lambda 闭包分配），
        /// 单线程回合制下可安全复用；每次使用前 Clear。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="self">敌我判定基准（自己这支部队）</param>
        /// <param name="scenario">场景</param>
        /// <returns>敌军兵力合计；没有敌人返回 0</returns>
        static int SumEnemyTroopsAround(City city, Troop self, Scenario scenario)
        {
            if (city.CenterCell == null)
                return 0;

            int range = AIConfig.Instance.cityDefenseScanRange;
            if (range <= 0)
                range = 4;

            enemyScanBuffer.Clear();
            scenario.Map.GetSpiral(city.CenterCell, range, enemyScanBuffer);

            int total = 0;
            for (int i = 0; i < enemyScanBuffer.Count; i++)
            {
                Cell cell = enemyScanBuffer[i];
                Troop other = cell != null ? cell.troop : null;
                if (other == null || !other.IsAlive)
                    continue;
                if (other == self || !self.IsEnemy(other))
                    continue;
                total += other.troops;
            }
            return total;
        }

        /// <summary>
        /// 找最近的本势力据点（不限制容量，用于召回）。
        /// </summary>
        static City FindNearestAnyOwnCity(Troop troop, Scenario scenario)
        {
            if (scenario == null || scenario.citySet == null)
                return null;

            City best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != troop.BelongForce)
                    continue;

                int distance = GetDistance(troop, city, scenario);
                if (distance < 0 || distance >= bestDistance)
                    continue;
                bestDistance = distance;
                best = city;
            }

            // 兜底：一个都找不到时回归属城（可能有，也可能为 null）
            return best ?? troop.BelongCity;
        }

        /// <summary>
        /// 部队到据点的格距；部队或城池不在图上时返回 -1。
        /// </summary>
        static int GetDistance(Troop troop, City city, Scenario scenario)
        {
            if (troop.cell == null || city.CenterCell == null || scenario.Map == null)
                return -1;
            return scenario.Map.Distance(troop.cell, city.CenterCell);
        }

        /// <summary>
        /// 协防扫描用的单元格缓冲。
        ///
        /// 【单线程前提】回合制下不存在并发访问；每次使用前都会 Clear()。
        /// </summary>
        static readonly List<Cell> enemyScanBuffer = new List<Cell>(256);
    }
}
