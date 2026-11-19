using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// AI 部队的"改道兜底"规则：盟友城协防不划算时，把部队召回最近的自有城。
    ///
    /// 【原版无对应实现 —— 规则第 5 条自主设计】
    /// Open311 的 <c>unit_enter</c> 直接调用 <c>building_add_troops</c>
    /// （s11_sys_scen_unit.cpp:573），而 <c>building_add_troops</c> 对超上限部分
    /// 用 <c>clamp</c> 截断、返回实际变化量（s11_sys_scen_building.cpp:1145-1154），
    /// 调用方不接返回值 —— 也就是说**原版确实会把溢出兵力丢掉**。
    ///
    /// 本项目按玩家反馈补的规则：
    /// <list type="bullet">
    /// <item>盟友城协防不划算（打不过 / 够不着） → 立即召回最近的自有城。</item>
    /// </list>
    ///
    /// 【已取消】早前还实现过"入城会溢出 → AI 不进港关、改道别处"，现已按需求移除：
    /// 满兵港关必须照常接收自己的部队，否则部队会永远悬在地图上；
    /// 装不下的部分按原版 clamp 丢弃，并由 <see cref="Troop.EnterCity"/> 打 warning 留痕。
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
        /// 召回最近的自有城（不要求容量：返城解散，容量问题由入城时按原版 clamp 处理）。
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
