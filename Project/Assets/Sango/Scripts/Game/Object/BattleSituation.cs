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

                if (selfForce != null && troop.BelongForce == selfForce)
                {
                    myCount++;
                    myTroops += troop.troops;
                }
                else if (selfForce == null || selfForce.IsEnemy(troop.BelongForce))
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

        #region 前线建址评估（战略辅助建筑）

        /// <summary>
        /// 前线建址的评估快照。
        /// 综合"己方覆盖收益 / 战略要道 / 敌方威胁 / 理想距离带"给出一个可比较的评分，
        /// 供 AI 决定"该把军乐台、砦、箭楼等辅助建筑修在哪里"。
        /// </summary>
        public struct FrontSiteInfo
        {
            /// <summary>候选格</summary>
            public Cell cell;
            /// <summary>综合评分（越高越值得修建，&lt;= 0 表示应放弃）</summary>
            public int score;

            /// <summary>覆盖范围内的己方兵力合计</summary>
            public int coverTroops;
            /// <summary>覆盖范围内的己方部队数量</summary>
            public int coverCount;
            /// <summary>覆盖范围内己方部队的平均气力百分比（0~100）</summary>
            public int avgMoralePercent;
            /// <summary>覆盖范围内"粮草紧张"的己方部队占比（0~100）</summary>
            public int lowFoodPercent;

            /// <summary>威胁范围内的敌方兵力合计</summary>
            public int enemyTroops;
            /// <summary>到最近敌方部队的距离（格）；没有敌人时为 -1</summary>
            public int nearestEnemyDist;

            /// <summary>是否邻近战略要道（关 / 港 / 城池）</summary>
            public bool hasChoke;

            /// <summary>该建址是否可用（通过全部硬性校验）</summary>
            public bool isValid;

            /// <summary>重置快照</summary>
            public void Clear()
            {
                cell = null;
                score = 0;
                coverTroops = 0;
                coverCount = 0;
                avgMoralePercent = 100;
                lowFoodPercent = 0;
                enemyTroops = 0;
                nearestEnemyDist = -1;
                hasChoke = false;
                isValid = false;
            }
        }

        /// <summary>
        /// 评估某个格子作为"前线战略建筑"建址的价值。
        ///
        /// 【硬性排除】以下情况直接判定不可用：
        ///   · 地形不可建造（<see cref="Cell.CanBuild"/>）
        ///   · 格上已有部队或建筑（<see cref="Cell.IsEmpty"/>）
        ///   · 内城格、或与既有建筑间距不足（<c>BuildingSpace</c>）
        ///   · 距最近敌人近于 <c>frontBuildSafeMinDist</c>（修了也会被立刻拆除）
        ///   · 覆盖范围内己方兵力低于 <c>frontBuildMinCoverTroops</c>（没人受益）
        ///
        /// 【评分维度】
        ///   1. 覆盖收益：覆盖范围内己方兵力越多越值（建筑效果作用于此范围）
        ///   2. 战略要道：邻近关 / 港 / 城池时加成（扼守要冲）
        ///   3. 敌方威胁：威胁范围内敌方兵力越多越危险，作扣分
        ///   4. 理想距离带：距敌人过近或过远都要扣分
        /// </summary>
        /// <param name="cell">候选格</param>
        /// <param name="selfForce">己方势力（用于敌我判定）；不可为 null</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="coverRange">覆盖范围半径（格），一般取建筑作用的 bound+1</param>
        /// <returns>评估快照；不合格时 isValid = false</returns>
        public static FrontSiteInfo EvaluateFrontSite(Cell cell, Force selfForce, Scenario scenario, int coverRange = 3)
        {
            FrontSiteInfo info = new FrontSiteInfo();
            info.Clear();
            if (cell == null || selfForce == null || scenario == null || scenario.Map == null)
                return info;

            AIConfig cfg = AIConfig.Instance;
            Map map = scenario.Map;

            // ---------- 硬性校验：能否在此建造 ----------
            if (!cell.CanBuild || !cell.IsEmpty() || cell.IsInterior)
                return info;

            int buildSpace = System.Math.Max(1, scenario.Variables.BuildingSpace);
            if (cell.SpiralHasBuilding(buildSpace))
                return info;

            if (coverRange <= 0)
                coverRange = 3;

            // ---------- 覆盖统计：范围内的己方部队 ----------
            int coverTroops = 0;
            int coverCount = 0;
            int moraleSum = 0;
            int lowFoodCount = 0;

            // ---------- 威胁统计：范围内的敌方部队 ----------
            int enemyTroops = 0;
            int nearestEnemyDist = -1;

            // 威胁范围取"覆盖范围"与配置威胁范围的较大者，保证一次遍历覆盖两种用途
            int scanRange = System.Math.Max(coverRange, cfg.frontBuildThreatRange);
            int centerX = cell.x;
            int centerY = cell.y;

            map.SpiralAction(cell, scanRange, (c) =>
            {
                Troop troop = c.troop;
                if (troop == null || !troop.IsAlive)
                    return;

                int dist = System.Math.Abs(c.x - centerX) + System.Math.Abs(c.y - centerY);

                if (troop.BelongForce == selfForce)
                {
                    if (dist <= coverRange)
                    {
                        coverCount++;
                        coverTroops += troop.troops;
                        moraleSum += troop.MaxMorale > 0
                            ? troop.morale * 100 / troop.MaxMorale
                            : 100;
                        if (troop.IsWithOutFood() == 1)
                            lowFoodCount++;
                    }
                }
                else if (selfForce.IsEnemy(troop.BelongForce))
                {
                    if (dist <= cfg.frontBuildThreatRange)
                        enemyTroops += troop.troops;
                    if (nearestEnemyDist < 0 || dist < nearestEnemyDist)
                        nearestEnemyDist = dist;
                }
            });

            info.cell = cell;
            info.coverTroops = coverTroops;
            info.coverCount = coverCount;
            info.avgMoralePercent = coverCount > 0 ? moraleSum / coverCount : 100;
            info.lowFoodPercent = coverCount > 0 ? lowFoodCount * 100 / coverCount : 0;
            info.enemyTroops = enemyTroops;
            info.nearestEnemyDist = nearestEnemyDist;

            // ---------- 安全校验：离敌人太近会被拆 ----------
            if (nearestEnemyDist >= 0 && nearestEnemyDist < cfg.frontBuildSafeMinDist)
                return info;

            // ---------- 收益校验：没有己方部队受益则不值得修 ----------
            if (coverTroops < cfg.frontBuildMinCoverTroops)
                return info;

            // ---------- 战略要道：邻近关 / 港 / 城池 ----------
            cell.Spiral(2, (c) =>
            {
                BuildingBase b = c.building;
                if (b == null)
                    return;
                if (b.IsCity() || b.IsPort() || b.IsGate())
                    info.hasChoke = true;
            });

            // ---------- 综合评分 ----------
            long score = 0;

            // 1. 覆盖收益：以 10000 兵力为满档，线性计分
            const int coverTroopsFull = 10000;
            int coverRatio = coverTroops >= coverTroopsFull
                ? 100
                : coverTroops * 100 / coverTroopsFull;
            score += (long)coverRatio * cfg.frontCoverWeight;

            // 2. 战略要道加成
            if (info.hasChoke)
                score += cfg.frontChokeWeight;

            // 3. 距离带：落在理想区间得满分，偏离则按偏差比例扣分
            int idealMin = cfg.frontBuildIdealMinDist;
            int idealMax = cfg.frontBuildIdealMaxDist;
            if (idealMax < idealMin)
                idealMax = idealMin;
            if (info.nearestEnemyDist >= 0)
            {
                int deviation;
                if (info.nearestEnemyDist < idealMin)
                    deviation = idealMin - info.nearestEnemyDist;
                else if (info.nearestEnemyDist > idealMax)
                    deviation = info.nearestEnemyDist - idealMax;
                else
                    deviation = 0;

                // 每偏离 1 格扣 20%，最多扣满
                int penalty = System.Math.Min(100, deviation * 20);
                score += (long)(100 - penalty) * cfg.frontDistanceWeight;
            }

            // 4. 敌方威胁扣分：威胁范围内敌方兵力越多越危险
            //    以 20000 兵力为满档，扣分不超过该维度权重
            const int threatTroopsFull = 20000;
            int threatRatio = enemyTroops >= threatTroopsFull
                ? 100
                : enemyTroops * 100 / threatTroopsFull;
            score -= (long)threatRatio * cfg.frontThreatWeight;

            if (score < int.MinValue) score = int.MinValue;
            if (score > int.MaxValue) score = int.MaxValue;
            info.score = (int)score;
            info.isValid = info.score >= cfg.frontBuildSiteMinScore;
            return info;
        }

        /// <summary>
        /// 依据建址的战场特征挑选最合适的战略辅助建筑类型。
        ///
        /// 【态势驱动】替代原先的"纯权重随机"，按优先级依次判定：
        ///   1. 平均气力过低 → 军乐台（回气力）
        ///   2. 缺粮部队占比过高 → 阵 / 砦 / 城塞（降粮耗 + 加防御）
        ///   3. 邻近要道且敌人不远 → 箭楼 / 连弩楼 / 投石台（自动输出）
        ///   4. 我方占优 → 太鼓台（加攻击，推进更快）
        ///   5. 兜底 → frontBuildPreferredTypes 顺序中第一个可建的
        /// </summary>
        /// <param name="info">建址评估快照</param>
        /// <param name="force">己方势力（其 canBuildMilitaryBuildingType 决定可选范围）</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>选中的建筑类型；无可用类型时返回 null</returns>
        public static BuildingType SelectFrontBuildingType(FrontSiteInfo info, Force force, Scenario scenario)
        {
            if (force == null || force.canBuildMilitaryBuildingType == null
                || force.canBuildMilitaryBuildingType.Count == 0)
                return null;

            AIConfig cfg = AIConfig.Instance;
            List<BuildingType> candidates = force.canBuildMilitaryBuildingType;

            // 按 kind 查找（kind 即 BuildingType.Id 语义上的建筑种类）
            BuildingType PickByKind(params int[] kinds)
            {
                for (int i = 0; i < kinds.Length; i++)
                {
                    BuildingType t = candidates.Find(x => x.kind == kinds[i]);
                    if (t != null)
                        return t;
                }
                return null;
            }

            // 1. 气力不足 → 军乐台(kind 13)
            if (info.avgMoralePercent > 0 && info.avgMoralePercent < cfg.frontBuildLowMoralePercent)
            {
                BuildingType morale = PickByKind(13);
                if (morale != null)
                    return morale;
            }

            // 2. 粮草紧张 → 阵(4) / 砦(5) / 城塞(6)：降粮耗且加防御
            if (info.lowFoodPercent >= cfg.frontBuildLowFoodPercent)
            {
                BuildingType food = PickByKind(4, 5, 6);
                if (food != null)
                    return food;
            }

            // 3. 扼守要道且敌人已在附近 → 箭楼(7) / 连弩楼(8) / 投石台(11)
            if (info.hasChoke && info.nearestEnemyDist >= 0 && info.nearestEnemyDist <= cfg.frontBuildThreatRange)
            {
                BuildingType tower = PickByKind(7, 8, 11);
                if (tower != null)
                    return tower;
            }

            // 4. 我方兵力占优 → 太鼓台(12)：加攻击，利于推进
            if (info.enemyTroops > 0 && info.coverTroops * 100 / (info.coverTroops + info.enemyTroops) >= cfg.tierAdvantagedPercent)
            {
                BuildingType drum = PickByKind(12);
                if (drum != null)
                    return drum;
            }

            // 5. 兜底：按配置的优先序列取第一个本势力可建的类型
            int[] preferred = cfg.frontBuildPreferredTypes;
            if (preferred != null && preferred.Length > 0)
            {
                BuildingType fallback = PickByKind(preferred);
                if (fallback != null)
                    return fallback;
            }

            // 最终兜底：任取一个可建类型
            return candidates[0];
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
