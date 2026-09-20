/*
 * 文件名：BattleSituation.cs
 * 描述：统一战场态势模型，集中收集与计算敌我实力、局部威胁等战场信息，
 *       供 Force / Corps / City / Troop 各层 AI 复用，避免重复遍历与重复计算。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 统一战场态势模型。
    ///
    /// 设计目标：
    /// 1. 把散落在各层 AI 中的实力计算、威胁评估收敛到同一入口，避免同一份数据被反复遍历；
    /// 2. 以值类型快照(struct)返回结果，不产生额外的堆分配；
    /// 3. 各层 AI(Force / Corps / City / Troop)共用同一套口径，便于统一调参与维护。
    /// </summary>
    public static class BattleSituation
    {
        /// <summary>
        /// 势力实力快照。
        /// </summary>
        public struct ForceSnapshot
        {
            /// <summary>城池数量</summary>
            public int cityCount;
            /// <summary>总兵力</summary>
            public int troops;
            /// <summary>总金钱</summary>
            public int gold;
            /// <summary>总粮草</summary>
            public int food;
            /// <summary>综合战力(城市数加权 + 兵力 + 金钱 / 10)</summary>
            public int fightPower;

            /// <summary>
            /// 重置快照。
            /// </summary>
            public void Clear()
            {
                cityCount = 0;
                troops = 0;
                gold = 0;
                food = 0;
                fightPower = 0;
            }
        }

        /// <summary>
        /// 局部威胁快照。
        /// </summary>
        public struct ThreatSnapshot
        {
            /// <summary>敌方部队数量</summary>
            public int enemyCount;
            /// <summary>敌方总兵力</summary>
            public int enemyTroops;
            /// <summary>最近敌人的距离(int.MaxValue 表示范围内没有敌人)</summary>
            public int nearestDistance;
            /// <summary>威胁等级(越高越危险)</summary>
            public int threatLevel;

            /// <summary>
            /// 重置快照。
            /// </summary>
            public void Clear()
            {
                enemyCount = 0;
                enemyTroops = 0;
                nearestDistance = int.MaxValue;
                threatLevel = 0;
            }

            /// <summary>
            /// 范围内是否存在敌人。
            /// </summary>
            public bool HasEnemy { get { return enemyCount > 0; } }
        }

        #region 势力实力

        /// <summary>
        /// 采集势力的实力快照(仅遍历一次城市集合)。
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>实力快照</returns>
        public static ForceSnapshot EvaluateForceSnapshot(Force force)
        {
            ForceSnapshot snapshot = new ForceSnapshot();
            snapshot.Clear();
            if (force == null)
                return snapshot;

            int cityCount = 0;
            int troops = 0;
            int gold = 0;
            int food = 0;
            force.ForEachCity(city =>
            {
                if (city == null)
                    return;
                cityCount++;
                troops += city.troops;
                gold += city.gold;
                food += city.food;
            });

            snapshot.cityCount = cityCount;
            snapshot.troops = troops;
            snapshot.gold = gold;
            snapshot.food = food;
            snapshot.fightPower = cityCount * 1000 + troops + gold / 10;
            return snapshot;
        }

        /// <summary>
        /// 计算两个势力的实力比(自身 / 目标)。
        /// </summary>
        /// <param name="self">自身势力</param>
        /// <param name="other">目标势力</param>
        /// <returns>实力比;目标实力为 0 时返回 float.MaxValue</returns>
        public static float GetPowerRatio(Force self, Force other)
        {
            int selfPower = EvaluateForceSnapshot(self).fightPower;
            int otherPower = EvaluateForceSnapshot(other).fightPower;
            if (otherPower <= 0)
                return selfPower <= 0 ? 1f : float.MaxValue;
            return (float)selfPower / otherPower;
        }

        #endregion

        #region 局部兵力对比（部队分级策略）

        /// <summary>
        /// 局部兵力对比快照。
        /// 用于让部队判断自己所在战场是优势、均势还是劣势，从而采取不同的分级策略。
        /// </summary>
        public struct BalanceSnapshot
        {
            /// <summary>我方兵力合计</summary>
            public int myTroops;
            /// <summary>敌方兵力合计</summary>
            public int enemyTroops;
            /// <summary>我方部队数量</summary>
            public int myCount;
            /// <summary>敌方部队数量</summary>
            public int enemyCount;
            /// <summary>我方兵力占比（0~100）；双方均无部队时为 50（视为均势）</summary>
            public int balancePercent;

            /// <summary>
            /// 重置快照。
            /// </summary>
            public void Clear()
            {
                myTroops = 0;
                enemyTroops = 0;
                myCount = 0;
                enemyCount = 0;
                balancePercent = 50;
            }

            /// <summary>范围内是否存在敌人。</summary>
            public bool HasEnemy { get { return enemyCount > 0; } }

            /// <summary>范围内是否存在友军。</summary>
            public bool HasAlly { get { return myCount > 0; } }
        }

        /// <summary>
        /// 评估指定格子周围的**局部敌我兵力对比**（一次遍历同时统计双方）。
        ///
        /// 与 <see cref="EvaluateThreat(Cell, int, Troop, Scenario)"/> 的区别：
        /// 本方法同时统计我方与敌方，给出"我方兵力占比"，
        /// 用于部队态势分档（见 <see cref="TroopBattleTier"/>）。
        /// </summary>
        /// <param name="center">评估中心（通常是部队所在格）</param>
        /// <param name="range">搜索范围（格），建议 8-15</param>
        /// <param name="selfForce">我方势力；为 null 时所有部队都计入敌方</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>兵力对比快照</returns>
        public static BalanceSnapshot EvaluateBalance(Cell center, Force selfForce, int range, Scenario scenario)
        {
            BalanceSnapshot snapshot = new BalanceSnapshot();
            snapshot.Clear();
            if (center == null || scenario == null || scenario.Map == null || range <= 0)
                return snapshot;

            int myTroops = 0;
            int enemyTroops = 0;
            int myCount = 0;
            int enemyCount = 0;

            scenario.Map.SpiralAction(center, range, (cell) =>
            {
                Troop troop = cell.troop;
                if (troop == null || !troop.IsAlive)
                    return;

                if (selfForce != null && troop.mBelongForce == selfForce)
                {
                    myCount++;
                    myTroops += troop.troops;
                }
                else if (selfForce == null || selfForce.IsEnemy(troop.mBelongForce))
                {
                    enemyCount++;
                    enemyTroops += troop.troops;
                }
            });

            snapshot.myTroops = myTroops;
            snapshot.enemyTroops = enemyTroops;
            snapshot.myCount = myCount;
            snapshot.enemyCount = enemyCount;
            snapshot.balancePercent = CalcBalancePercent(myTroops, enemyTroops);
            return snapshot;
        }

        /// <summary>
        /// 计算我方兵力占比（0~100）。双方均无兵力时返回 50（视为均势）。
        /// </summary>
        /// <param name="myTroops">我方兵力</param>
        /// <param name="enemyTroops">敌方兵力</param>
        /// <returns>我方占比</returns>
        public static int CalcBalancePercent(int myTroops, int enemyTroops)
        {
            long total = (long)myTroops + enemyTroops;
            if (total <= 0)
                return 50;
            return (int)(myTroops * 100 / total);
        }

        /// <summary>
        /// 依据我方兵力占比与 AIConfig 中的阈值，把态势划分为档位。
        /// </summary>
        /// <param name="balancePercent">我方兵力占比（0~100）</param>
        /// <returns>态势档位</returns>
        public static TroopBattleTier GetTier(int balancePercent)
        {
            AIConfig cfg = AIConfig.Instance;
            if (balancePercent >= cfg.tierDecisivePercent)
                return TroopBattleTier.Decisive;
            if (balancePercent >= cfg.tierAdvantagedPercent)
                return TroopBattleTier.Advantaged;
            if (balancePercent >= cfg.tierEvenPercent)
                return TroopBattleTier.Even;
            if (balancePercent >= cfg.tierDisadvantagedPercent)
                return TroopBattleTier.Disadvantaged;
            return TroopBattleTier.Critical;
        }

        #endregion

        #region 局部威胁

        /// <summary>
        /// 评估指定格子周围的威胁(以城市为敌我判定基准)。
        /// </summary>
        /// <param name="center">中心格</param>
        /// <param name="range">搜索范围(建议 2-8,过大会显著增加遍历开销)</param>
        /// <param name="selfCity">自身城市,为 null 时把所有部队都视为敌人</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>威胁快照</returns>
        public static ThreatSnapshot EvaluateThreat(Cell center, int range, City selfCity, Scenario scenario)
        {
            ThreatSnapshot snapshot = new ThreatSnapshot();
            snapshot.Clear();
            if (center == null || scenario == null || scenario.Map == null || range <= 0)
                return snapshot;

            int enemyCount = 0;
            int enemyTroops = 0;
            int nearestDistance = int.MaxValue;

            scenario.Map.SpiralAction(center, range, (cell) =>
            {
                Troop troop = cell.troop;
                if (troop == null || !troop.IsAlive)
                    return;
                // 只统计敌方部队
                if (selfCity != null && !troop.IsEnemy(selfCity))
                    return;

                enemyCount++;
                enemyTroops += troop.troops;
                int distance = center.Distance(cell);
                if (distance < nearestDistance)
                    nearestDistance = distance;
            });

            FillThreat(ref snapshot, enemyCount, enemyTroops, nearestDistance);
            return snapshot;
        }

        /// <summary>
        /// 评估指定格子周围的威胁(以部队为敌我判定基准)。
        /// </summary>
        /// <param name="center">中心格</param>
        /// <param name="range">搜索范围(建议 2-8)</param>
        /// <param name="selfTroop">自身部队,为 null 时把所有部队都视为敌人</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>威胁快照</returns>
        public static ThreatSnapshot EvaluateThreat(Cell center, int range, Troop selfTroop, Scenario scenario)
        {
            ThreatSnapshot snapshot = new ThreatSnapshot();
            snapshot.Clear();
            if (center == null || scenario == null || scenario.Map == null || range <= 0)
                return snapshot;

            int enemyCount = 0;
            int enemyTroops = 0;
            int nearestDistance = int.MaxValue;

            scenario.Map.SpiralAction(center, range, (cell) =>
            {
                Troop troop = cell.troop;
                if (troop == null || !troop.IsAlive)
                    return;
                // 只统计敌方部队
                if (selfTroop != null && !selfTroop.IsEnemy(troop))
                    return;

                enemyCount++;
                enemyTroops += troop.troops;
                int distance = center.Distance(cell);
                if (distance < nearestDistance)
                    nearestDistance = distance;
            });

            FillThreat(ref snapshot, enemyCount, enemyTroops, nearestDistance);
            return snapshot;
        }

        /// <summary>
        /// 评估城市的威胁快照。
        ///
        /// 与 <see cref="EvaluateThreat(Cell, int, City, Scenario)"/> 不同,
        /// 本方法**复用城市 AI 准备阶段已预计算的敌人列表**,无需再次扫描地图,
        /// 因此适合在城市回合的多个决策点反复调用。
        /// </summary>
        /// <param name="city">城市</param>
        /// <returns>威胁快照</returns>
        public static ThreatSnapshot EvaluateCityThreat(City city)
        {
            ThreatSnapshot snapshot = new ThreatSnapshot();
            snapshot.Clear();
            if (city == null)
                return snapshot;

            snapshot.enemyCount = city.EnemyCount;
            snapshot.enemyTroops = city.EnemyTroops;
            snapshot.nearestDistance = city.NearestEnemyDistance;
            FillThreatLevel(ref snapshot);
            return snapshot;
        }

        /// <summary>
        /// 根据快照当前字段计算威胁等级。
        /// </summary>
        /// <param name="snapshot">威胁快照(会写入 threatLevel)</param>
        static void FillThreatLevel(ref ThreatSnapshot snapshot)
        {
            int proximityFactor = snapshot.nearestDistance == int.MaxValue
                ? 0
                : System.Math.Max(1, 10 - snapshot.nearestDistance);
            snapshot.threatLevel = snapshot.enemyCount * 1000 + snapshot.enemyTroops / 100 + proximityFactor * 200;
        }

        /// <summary>
        /// 填充威胁快照的统一口径(数量 / 兵力 / 最近距离 / 威胁等级)。
        /// </summary>
        /// <param name="snapshot">待填充的快照</param>
        /// <param name="enemyCount">敌方部队数量</param>
        /// <param name="enemyTroops">敌方总兵力</param>
        /// <param name="nearestDistance">最近敌人距离</param>
        static void FillThreat(ref ThreatSnapshot snapshot, int enemyCount, int enemyTroops, int nearestDistance)
        {
            snapshot.enemyCount = enemyCount;
            snapshot.enemyTroops = enemyTroops;
            snapshot.nearestDistance = nearestDistance;
            FillThreatLevel(ref snapshot);
        }

        #endregion
    }
}
