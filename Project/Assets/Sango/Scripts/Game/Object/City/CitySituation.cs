/*
 * 文件名：CitySituation.cs
 * 描述：城池态势快照。集中采集一座城市在 AI 决策时需要的全部关键指标
 *       （军事 / 经济 / 人力 / 兵装 / 设施 / 战场联动 / 势力个性），
 *       供城池 AI 的命令优先级编排使用。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城池类型（用于命令适用性过滤）。
    /// </summary>
    [System.Flags]
    public enum CityKind
    {
        None = 0,
        /// <summary>都市</summary>
        City = 1,
        /// <summary>港口</summary>
        Port = 2,
        /// <summary>关卡</summary>
        Gate = 4,
        /// <summary>全部类型</summary>
        All = City | Port | Gate,
    }

    /// <summary>
    /// 城池态势快照。
    ///
    /// 设计目标：把「当前城池处于什么状态」一次性采集完毕，后续所有命令的优先级评分都基于同一份数据，
    /// 避免每条命令各自重复读取 / 计算。
    /// </summary>
    public struct CitySituation
    {
        // ==================== 军事 ====================
        /// <summary>当前兵力</summary>
        public int troops;
        /// <summary>可容纳兵力</summary>
        public int troopsLimit;
        /// <summary>士气</summary>
        public int morale;
        /// <summary>治安</summary>
        public int security;
        /// <summary>预计算的城内敌军数量</summary>
        public int enemyCount;
        /// <summary>预计算的敌军总兵力</summary>
        public int enemyTroops;
        /// <summary>最近敌人距离（int.MaxValue 表示无）</summary>
        public int nearestEnemyDistance;
        /// <summary>是否兵临城下</summary>
        public bool isUnderSiege;
        /// <summary>本城已出征部队数量</summary>
        public int attackTroopsCount;

        // ==================== 经济 ====================
        /// <summary>粮草</summary>
        public int food;
        /// <summary>粮仓上限</summary>
        public int foodLimit;
        /// <summary>金钱</summary>
        public int gold;
        /// <summary>金库上限</summary>
        public int goldLimit;

        // ==================== 人力 ====================
        /// <summary>空闲武将数量</summary>
        public int freePersons;
        /// <summary>在野武将数量（`city.wildPersons.Count`）—— 用于"登用"命令的排序加分</summary>
        public int wildCount;
        /// <summary>未发现武将数量（本城 + 所属港关的 `invisiblePersons`）—— 用于"搜索"命令的排序加分</summary>
        public int invisibleCount;
        /// <summary>武将缺口（负值表示缺人）</summary>
        public int personHole;

        // ==================== 兵装 ====================
        /// <summary>枪 / 戟 / 弩 / 马 总数</summary>
        public int weaponCount;
        /// <summary>器械（冲车 / 投石）总数</summary>
        public int machineCount;

        // ==================== 地理 / 设施 ====================
        /// <summary>城池类型（都市 / 港口 / 关卡）</summary>
        public CityKind kind;
        /// <summary>是否边境城市</summary>
        public bool isBorderCity;
        /// <summary>是否有工坊（可造器械）</summary>
        public bool hasMachineFactory;
        /// <summary>是否有造船厂（可造船）</summary>
        public bool hasBoatFactory;
        /// <summary>是否有兵营（可征兵）</summary>
        public bool hasBarracks;
        /// <summary>是否有铁匠铺（可造兵装）</summary>
        public bool hasBlacksmith;
        /// <summary>是否有马厩（可造马）</summary>
        public bool hasStable;
        /// <summary>是否拥有下属港口</summary>
        public bool hasPort;
        /// <summary>
        /// 是否**与外界接壤**：邻接城里存在非本势力的城（含无主城）。
        ///
        /// 用途：港关（渡口 / 关隘）的"军情"判定 —— 游戏的 <c>borderLine</c> 只刷新**都市**，
        /// 港关那个字段恒为旧值，所以港关的边境只能这样现算。
        /// 资源调度（有军情才要货）与人才调度（有军情才驻守）共用同一个口径。
        /// </summary>
        public bool hasForeignNeighbor;

        // ==================== 战场联动 ====================
        /// <summary>附近需要补给的友军数量（缺粮 / 缺兵）</summary>
        public int needyAllyCount;
        /// <summary>被围攻的邻接友城数量</summary>
        public int besiegedNeighborCount;
        /// <summary>被非本势力占领的下属港关数量</summary>
        public int lostSubCityCount;
        /// <summary>是否存在仍在本城附近的威胁部队</summary>
        public bool hasThreatTroop;

        // ==================== 势力 ====================
        /// <summary>势力 AI 个性</summary>
        public ForceAI.AIPersonalityType PersonalityId;

        // ==================== 派生比率（0~1） ====================
        /// <summary>兵力充盈度</summary>
        public float troopFill;
        /// <summary>粮草充盈度</summary>
        public float foodFill;
        /// <summary>金钱充盈度</summary>
        public float goldFill;
        /// <summary>治安比率</summary>
        public float securityRate;

        /// <summary>
        /// 采集城池态势快照。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>态势快照</returns>
        public static CitySituation Collect(City city, Scenario scenario)
        {
            CitySituation s = new CitySituation();
            if (city == null || scenario == null)
                return s;

            // ---------- 地理 / 类型 ----------
            if (city.IsCity()) s.kind |= CityKind.City;
            if (city.IsPort()) s.kind |= CityKind.Port;
            if (city.IsGate()) s.kind |= CityKind.Gate;
            s.isBorderCity = city.IsBorderCity;
            s.hasPort = city.portList != null && city.portList.Count > 0;

            // ---------- 军事 ----------
            s.troops = city.troops;
            s.troopsLimit = city.TroopsLimit;
            s.morale = city.morale;
            s.security = city.security;
            s.attackTroopsCount = city.AttackTroopsCount;
            s.enemyCount = city.EnemyCount;
            s.enemyTroops = city.EnemyTroops;
            s.nearestEnemyDistance = city.NearestEnemyDistance;
            s.isUnderSiege = s.enemyCount > 0 && s.nearestEnemyDistance <= 6;

            // ---------- 经济 ----------
            s.food = city.food;
            s.foodLimit = city.FoodLimit;
            s.gold = city.gold;
            s.goldLimit = city.GoldLimit;

            // ---------- 人力 ----------
            s.freePersons = city.freePersons != null ? city.freePersons.Count : 0;
            s.wildCount = city.wildPersons != null ? city.wildPersons.Count : 0;   // 登用命令排序依据
            // 搜索命令排序依据：与部署层"搜索"岗同口径（本城 + 所属港关的未发现人才）
            s.invisibleCount = CityEstablishment.CountInvisiblePersons(city);

            // ---------- 兵装 ----------
            if (city.itemStore != null)
            {
                for (int id = (int)ItemStoreKindType.Spear; id <= (int)ItemStoreKindType.Horse; id++)
                    s.weaponCount += city.itemStore.GetNumber(id);
                s.machineCount += city.itemStore.GetNumber((int)ItemStoreKindType.Helepolis);
                s.machineCount += city.itemStore.GetNumber((int)ItemStoreKindType.Catapult);
            }

            // ---------- 设施 ----------
            // 【一次遍历】原来这里连调 5 次 HasBuilding，每次都把 allBuildings 完整走一遍 ——
            // 同一张表被扫 5 遍。而 Collect 每回合每城还要被调用多次，这个常数开销会被放大。
            CollectFacilities(city, ref s);

            // ---------- 战场联动 ----------
            s.besiegedNeighborCount = CountBesiegedNeighbors(city);
            s.lostSubCityCount = CountLostSubCities(city);
            s.hasThreatTroop = HasThreatTroop(city, scenario);
            s.needyAllyCount = CountNeedyAllies(city, scenario);
            s.hasForeignNeighbor = HasForeignNeighbor(city);

            // ---------- 势力个性 ----------
            Person commander = city.BelongCorps != null ? city.BelongCorps.mComander : null;
            s.PersonalityId = ForceAI.GetAIPersonality(commander);

            // ---------- 派生比率 ----------
            s.troopFill = Rate(s.troops, s.troopsLimit);
            s.foodFill = Rate(s.food, s.foodLimit);
            s.goldFill = Rate(s.gold, s.goldLimit);
            s.securityRate = Rate(s.security, 100);

            return s;
        }

        /// <summary>
        /// 是否存在被围攻的邻接友城。
        /// </summary>
        /// <param name="city">本城</param>
        /// <returns>被围邻城数量</returns>
        static int CountBesiegedNeighbors(City city)
        {
            int count = 0;
            if (city.NeighborList == null)
                return 0;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City n = city.NeighborList[i];
                if (n == null || !n.IsAlive)
                    continue;
                if (n.BelongForce != city.BelongForce)
                    continue;
                if (n.CheckEnemiesIfAlive())
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 是否存在被非本势力占领的下属港关。
        /// </summary>
        /// <param name="city">本城</param>
        /// <returns>丢失的港关数量</returns>
        static int CountLostSubCities(City city)
        {
            int count = 0;
            if (city.subCities == null)
                return 0;
            for (int i = 0; i < city.subCities.Count; i++)
            {
                City sub = city.subCities[i];
                if (sub == null || !sub.IsAlive)
                    continue;
                if (!sub.IsPort() && !sub.IsGate())
                    continue;
                if (sub.BelongForce != city.BelongForce)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 本势力是否仍有威胁部队停留在本城附近。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否存在可驱逐的威胁部队</returns>
        static bool HasThreatTroop(City city, Scenario scenario)
        {
            Force force = city.BelongForce;
            if (force == null || force.threatTroopIds == null || force.threatTroopIds.Count == 0)
                return false;

            int range = AIConfig.Instance.driveOutThreatRange;
            for (int i = 0; i < force.threatTroopIds.Count; i++)
            {
                Troop t = scenario.troopsSet.Get(force.threatTroopIds[i]);
                if (t == null || !t.IsAlive)
                    continue;
                if (!t.IsEnemy(city))
                    continue;
                if (scenario.Map.Distance(city.CenterCell, t.cell) <= range)
                    return true;
            }
            return false;
        }

        // ==================== "参战部队"的势力级缓存 ====================
        //
        // 【为什么要缓存】本方法原来对**每座城**都把 scenario.troopsSet（全地图、**所有势力**的部队）
        // 扫一遍，只在循环体里过滤掉"非本势力"—— 复杂度 O(城数 × 全地图部队数)；
        // 而 CitySituation.Collect 每回合**每城**要被调用多次（资源调度 / 人才调度 / 城池 AI 命令排序），
        // 于是同一批部队被反复扫描几十上百遍。这里改成"按势力一次性归集"，各城只扫本势力部队，
        // 过滤（距离 / 缺粮 / 缺兵）仍在各城现算 —— 那部分才是城独有的。
        //
        // 失效条件（任一变化即重建）：换剧本、换回合、troopsSet 长度变化（当回合新出兵的信号）。

        /// <summary>缓存所依附的剧本</summary>
        static Scenario cachedCombatScenario;
        /// <summary>缓存对应的全局回合号</summary>
        static int cachedCombatTurn = -1;
        /// <summary>建缓存时的部队总数（变化即失效 —— 当回合有新部队入场的信号）</summary>
        static int cachedCombatTroopCount = -1;
        /// <summary>势力 id → 该势力"有作战任务的部队"列表</summary>
        static Dictionary<int, List<Troop>> cachedCombatTroopsByForce;
        /// <summary>空列表常量（避免每次都 new）</summary>
        static readonly List<Troop> EmptyCombatTroops = new List<Troop>();

        /// <summary>
        /// 取某势力"有作战任务的部队"（按势力归集，同一回合只建一次）。
        /// 该势力当期没有参战部队时返回空列表（不会返回 null）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>该势力的参战部队列表（可能为空）</returns>
        static List<Troop> GetCombatTroops(Force force, Scenario scenario)
        {
            if (force == null || scenario == null || scenario.troopsSet == null)
                return EmptyCombatTroops;

            int turn = scenario.TurnCount;
            bool reuse = ReferenceEquals(cachedCombatScenario, scenario)
                && cachedCombatTurn == turn
                && cachedCombatTroopCount == scenario.troopsSet.Count
                && cachedCombatTroopsByForce != null;

            if (!reuse)
            {
                cachedCombatScenario = scenario;
                cachedCombatTurn = turn;
                cachedCombatTroopCount = scenario.troopsSet.Count;
                cachedCombatTroopsByForce = new Dictionary<int, List<Troop>>();

                for (int i = 0; i < scenario.troopsSet.Count; i++)
                {
                    Troop t = scenario.troopsSet[i];
                    if (t == null || !t.IsAlive || t.BelongForce == null)
                        continue;
                    if (t.IsTransport)
                        continue;
                    if (t.missionType <= 0)
                        continue;                                   // 只统计有作战任务的部队
                    if (t.missionType == (int)MissionType.TroopSupplyTroop)
                        continue;

                    List<Troop> bucket;
                    if (!cachedCombatTroopsByForce.TryGetValue(t.BelongForce.Id, out bucket))
                    {
                        bucket = new List<Troop>();
                        cachedCombatTroopsByForce[t.BelongForce.Id] = bucket;
                    }
                    bucket.Add(t);
                }
            }

            List<Troop> result;
            return cachedCombatTroopsByForce.TryGetValue(force.Id, out result) ? result : EmptyCombatTroops;
        }

        /// <summary>
        /// 统计附近需要补给的友军数量（缺粮 / 缺兵，且有作战任务）。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>需要补给的友军数量</returns>
        static int CountNeedyAllies(City city, Scenario scenario)
        {
            if (city == null || scenario == null)
                return 0;

            int count = 0;
            int range = AIConfig.Instance.supplySearchRange + 8;
            int foodFactor = AIConfig.Instance.supplyFoodThresholdFactor;

            // 只扫**本势力**的参战部队（见上方缓存的说明）
            List<Troop> troops = GetCombatTroops(city.BelongForce, scenario);
            for (int i = 0; i < troops.Count; i++)
            {
                Troop t = troops[i];
                if (t == null || !t.IsAlive)
                    continue;
                if (scenario.Map.Distance(city.CenterCell, t.cell) > range)
                    continue;

                if (t.food <= 0
                    || t.food < t.troops * foodFactor
                    || (t.MaxTroops > 0 && t.troops < t.MaxTroops / 2))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 安全比率（0~1）。
        /// </summary>
        /// <param name="value">当前值</param>
        /// <param name="max">上限</param>
        /// <returns>比率</returns>
        static float Rate(int value, int max)
        {
            if (max <= 0)
                return 0f;
            float r = (float)value / max;
            return r < 0f ? 0f : (r > 1f ? 1f : r);
        }

        /// <summary>
        /// 是否与外界接壤：邻接城里存在**非本势力**的城（含无主城）。
        /// 口径与游戏自己的边境判定一致（<c>Force.UpdateTurnInfo</c> 的 <c>!neighbor.IsSameForce(c)</c>），
        /// 但对都市与港关一视同仁 —— 那处只刷新都市，港关直接读会全是旧值。
        /// </summary>
        /// <param name="city">城池</param>
        /// <returns>接壤返回 true</returns>
        static bool HasForeignNeighbor(City city)
        {
            if (city == null || city.NeighborList == null)
                return false;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City n = city.NeighborList[i];
                if (n != null && n != city && !n.IsSameForce(city))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 一次性扫描城内的建筑表，把"关键设施是否存在"的 5 个标志一起置上。
        ///
        /// 【为什么合并】原实现是连调 5 次 <c>HasBuilding</c>，每次都把 allBuildings 走一遍 ——
        /// 同一张表被扫 5 遍；而 <see cref="Collect"/> 每回合每城还要被调用多次，
        /// 这个常数开销会被成倍放大。5 个标志本来就属于同一次扫描。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="s">态势快照（就地写入设施标志）</param>
        static void CollectFacilities(City city, ref CitySituation s)
        {
            // 注意：CitySituation 是**值类型**，必须用 ref 传 —— 否则写回的是副本，设施标志会丢。
            if (city == null || city.allBuildings == null)
                return;

            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building b = city.allBuildings[i];
                if (b == null || b.BuildingType == null)
                    continue;

                switch ((BuildingKindType)b.BuildingType.kind)
                {
                    case BuildingKindType.MechineFactory: s.hasMachineFactory = true; break;
                    case BuildingKindType.BoatFactory: s.hasBoatFactory = true; break;
                    case BuildingKindType.Barracks: s.hasBarracks = true; break;
                    case BuildingKindType.BlacksmithShop: s.hasBlacksmith = true; break;
                    case BuildingKindType.Stable: s.hasStable = true; break;
                }

                // 5 个都齐了就没必要继续扫（大城建筑多，这一段省得最多）
                if (s.hasMachineFactory && s.hasBoatFactory && s.hasBarracks
                    && s.hasBlacksmith && s.hasStable)
                    break;
            }
        }
    }
}
