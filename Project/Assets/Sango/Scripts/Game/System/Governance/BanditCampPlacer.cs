using System.Collections.Generic;
using Sango.Render;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 异族 / 贼「根城」（原版 nejiro）的生成逻辑。
    ///
    /// 【原版出处】<c>Game::bandit_appearance</c>（s11_game_month.cpp:1503-1583），**每月**执行一次：
    ///   ① 开局 6 个月内不生成（:1505），对象池可用武将 ≤30 也不生成（:1505）；
    ///   ② 收集全图 <c>hex.is_nejiro()</c> 标记的格，按城归类、每城最多 10 个候选位（:1514-1528）；
    ///   ③ 遍历所有城：城要存活、有主、且该城当前**没有**根城（:1533）；
    ///   ④ <c>bandit_chance</c>（:1585-1595）：治安 ≥ 威压判定门槛 → 0；否则 <c>n = (100-治安)/3</c>；
    ///   ⑤ 候选位按 <c>get_nejiro_pos_score</c> 打分，取最高分（:1541-1552）；
    ///   ⑥ 归属势力：异族范围城 → 对应异族势力；若该城「亲×」特技压制该异族 → 改贼势力；
    ///      非异族范围城 → 贼势力（:1553-1568）；
    ///   ⑦ <c>create_nejiro</c>（:1630-1679）：先摧毁目标格及 1~2 环内**非据点**建筑，
    ///      再建 FacilityId_Den（贼）/ FacilityId_Base（异族），满耐久、直接完工；
    ///   ⑧ 建成后立刻出一支部队（<c>max_gold = true</c>，:1574）。
    ///
    /// 【候选位来自哪里 —— 已完全对齐】
    /// 原版 <c>hex.is_nejiro()</c> 读的是地图数据里的 `nejiro` 字节；本项目对应的是
    /// <see cref="MapGrid.GridState.Thief"/>（`terrainState` 的 bit3），
    /// 由地图编辑器的 <c>BrushType.Thief</c> 刷出、随地图 bin 一起存档
    /// （读取链：Map.cs:131 → CellSet.SetTerrainTypeAndState → Cell.terrainState）。
    /// 当前 DefaultMap.bin 里已有 190 个 Thief 标记，所以**不存在"猜位置"的近似**。
    ///
    /// 【仍无法在代码层补齐的两处】
    /// 1. **对象池上限**：原版 `get_inactive_person_count() <= 30` 统计的是
    ///    `PersonId_TemporaryFirst~Last` 区间里**未激活的临时武将槽位**（s11_scen.cpp:1050-1066），
    ///    用途是"临时武将池快满就不再克隆，防止后续取不到空位"。
    ///    本项目 `personSet` 是动态扩容数组（`SangoObjectSet.CheckLength` 倍增），没有固定池、
    ///    也不会耗尽 → 该门槛**无对应物**，不实现（硬加只会变成"未登场武将数"的误伤）。
    /// 2. **`unit->set_alerted(true)`**：原版只是把 <c>UnitStatus_Alerted</c>（=2，"攻击警报"）
    ///    置位（s11_scen_obj_unit.cpp:658-661）。全 s11 源码里**没有任何读取点**
    ///    （`is_alerted` 零命中），属未完成的 UI/警报占位，不产生玩法效果，
    ///    因此本项目不引入这个死状态位。
    ///
    /// 【所属城】原版 <c>get_area_city_id()</c> 由格反查（派生）；本项目
    /// <see cref="BuildingBase.BelongCity"/> 是"所属城"语义（会登记进 <c>City.allBuildings</c>），
    /// 不能用来表达领地城，因此留空、用 <see cref="GetAreaCity"/> 现查。
    ///
    /// 【为什么玩家建不出来】建筑类型 15/16 的 <c>canBuild</c> 在数据里是 false，
    /// 它们只能由本类（或读档）生成，与玩家建造菜单无关。
    /// </summary>
    public static class BanditCampPlacer
    {
        /// <summary>异族据点（对应原版 FacilityId_Base，建筑类型 Id = 15，耐久 5000）</summary>
        public const int BarbarianCampTypeId = 15;

        /// <summary>盗贼据点（对应原版 FacilityId_Den，建筑类型 Id = 16，耐久 2000）</summary>
        public const int BanditCampTypeId = 16;

        /// <summary>每城候选位上限（原版 :1507 <c>MaxNejiroPosCount = 10</c>）</summary>
        const int MaxSitePerCity = 10;

        /// <summary>生成门槛：开局多少个月内不生成（原版 :1505 <c>get_elapsed_months() &lt;= 6</c>）</summary>
        const int ElapsedMonthsBeforeSpawn = 6;

        /// <summary>治安门槛（无「威压」时，原版 :1591 的 80）</summary>
        const int SecurityThreshold = 80;

        /// <summary>治安门槛（有「威压」时，原版 :1591 的 60）</summary>
        const int SecurityThresholdIatsu = 60;

        /// <summary>「威压」特技 Id（Features.json 96）</summary>
        const int FeatureIatsu = 96;

        /// <summary>每月回合数：36 回合/年 ÷ 12 月 = 3（原版 elapsed_months = turn / 3）</summary>
        const int TurnsPerMonth = 3;

        // 按城缓存的候选位（等价原版"地图上预先标好的 nejiro 位"）：key = city.Id
        static readonly Dictionary<int, List<Cell>> siteCache = new Dictionary<int, List<Cell>>();
        static Scenario siteCacheScenario;
        static bool siteCacheReady;

        static readonly List<Cell> scoreBuffer = new List<Cell>();
        static readonly List<Cell> emptySites = new List<Cell>();

        // ====================================================================
        // 对外入口
        // ====================================================================

        /// <summary>
        /// 清空候选位缓存。换剧本 / 回主菜单时必须调用，否则上局的格子会串到新地图上。
        /// </summary>
        public static void ResetCache()
        {
            siteCache.Clear();
            siteCacheScenario = null;
            siteCacheReady = false;
        }

        /// <summary>
        /// 月度入口：尝试给符合条件的城长出根城。对齐 <c>Game::bandit_appearance</c>
        /// （s11_game_month.cpp:1503-1583）。
        ///
        /// 由 <see cref="BanditCampSystem"/> 订阅 <c>GameEvent.OnMonthUpdate</c> 调用。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>本次生成的根城数量</returns>
        public static int TrySpawnMonthly(Scenario scenario)
        {
            if (scenario == null || scenario.citySet == null)
                return 0;

            // ① 开局 6 个月内不生成（:1505）
            if (scenario.TurnCount / TurnsPerMonth <= ElapsedMonthsBeforeSpawn)
                return 0;

            int spawned = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;

                // 城必须有主（:1533 `city->get_force_id() < 0` 跳过）
                if (city.BelongForce == null)
                    continue;

                // 该城已经有根城 → 跳过（:1533 `nejiro_id[city] > 0`）
                if (HasCamp(scenario, city))
                    continue;

                // ② 候选位（原版来自 hex.nejiro 标记，这里按城扫描并缓存）
                List<Cell> sites = GetSites(scenario, city);
                if (sites.Count == 0)
                    continue;

                // ③ 城的技能集合：威压（提高抑制）+ 亲×（压制对应异族）
                bool hasIatsu = CityHasFeature(city, FeatureIatsu);

                // ④ 概率
                if (!BanditChance(city, hasIatsu))
                    continue;

                // ⑤ 位置评分取最高
                Cell best = null;
                int bestScore = 0;
                for (int j = 0; j < sites.Count; j++)
                {
                    int score = CampPosScore(sites[j]);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = sites[j];
                    }
                }
                if (bestScore <= 0 || best == null)
                    continue;

                // ⑥ 归属势力（异族 / 贼，含亲×压制改判）
                Force owner = ResolveCampForce(scenario, city);
                if (owner == null)
                    continue;

                int typeId = owner.IsBandit ? BanditCampTypeId : BarbarianCampTypeId;

                // ⑦ 建根城（含摧毁周围 1~2 环的非据点建筑）
                Building camp = CreateCamp(scenario, city, best, typeId, owner);
                if (camp == null)
                    continue;

                spawned++;
                Sango.Log.Info($"*[根城]<{city.Name}> 领地内生成了 {owner.Name} 的"
                    + $" {camp.BuildingType.Name}（{best.x},{best.y}），"
                    + $"评分 {bestScore}，治安 {city.security}");

                // ⑧ 立刻出一支部队（:1574，max_gold = true）
                BanditTroopFactory.SpawnFromCamp(camp, scenario, true);
            }

            return spawned;
        }

        /// <summary>
        /// 原版 <c>bandit_chance</c>（s11_game_month.cpp:1585-1595）：
        /// 治安达到门槛（有威压 60 / 无威压 80）→ 不再生成；
        /// 否则概率 <c>(100 - 治安) / 3</c>（治安越低越容易）。
        /// </summary>
        /// <param name="city">目标城</param>
        /// <param name="hasIatsu">城内是否有「威压」特技</param>
        /// <returns>是否命中</returns>
        public static bool BanditChance(City city, bool hasIatsu)
        {
            if (city == null)
                return false;

            int threshold = hasIatsu ? SecurityThresholdIatsu : SecurityThreshold;
            if (city.security >= threshold)
                return false;

            int n = (100 - city.security) / 3;
            if (n < 0) n = 0;
            if (n > 100) n = 100;
            return GameRandom.Chance(n);
        }

        /// <summary>
        /// 原版 <c>get_nejiro_pos_score</c>（s11_game_month.cpp:1597-1620）：
        ///   · 基础 <c>rand(0,9)</c>；
        ///   · 相邻 6 格中每有一个"可通行且无部队"的格 → +181；
        ///   · 相邻全被堵死 → 返回 0（该候选位作废）；
        ///   · 第 2 环每有一个"可通行且为空"的格 → +10。
        /// 注意邻居判定只看**部队**（原版 <c>hex.get_unit_id() == -1</c>），建筑不扣分。
        /// </summary>
        /// <param name="cell">候选位</param>
        /// <returns>评分；0 = 不可用</returns>
        public static int CampPosScore(Cell cell)
        {
            if (cell == null)
                return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null)
                return 0;

            int n = GameRandom.Range(0, 10);
            bool hasEmptyPos = false;

            for (int dir = 0; dir < 6; dir++)
            {
                Cell neighbor = cell.GetNeighbor(dir);
                if (neighbor == null)
                    continue;
                TerrainType terrain = neighbor.TerrainType;
                if (terrain != null && terrain.moveable && neighbor.troop == null)
                {
                    hasEmptyPos = true;
                    n += 181;
                }
            }

            if (!hasEmptyPos)
                return 0;

            // 原版 MapRingIterator(pos, 2)：只遍历第 2 环
            scoreBuffer.Clear();
            scenario.Map.GetRing(cell.x, cell.y, 2, scoreBuffer);
            for (int i = 0; i < scoreBuffer.Count; i++)
            {
                Cell c = scoreBuffer[i];
                if (c == null)
                    continue;
                TerrainType terrain = c.TerrainType;
                if (terrain != null && terrain.moveable && c.IsEmpty())
                    n += 10;
            }

            return n;
        }

        /// <summary>
        /// 原版 <c>create_nejiro</c>（s11_game_month.cpp:1630-1679）的建造成分：
        /// 先摧毁目标格及 1~2 环内的**非据点**建筑，再在目标格上建满耐久、已完工的根城。
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="city">领地城（不写进建筑，仅日志用）</param>
        /// <param name="cell">落位格</param>
        /// <param name="typeId">建筑类型（15 / 16）</param>
        /// <param name="owner">归属势力</param>
        /// <returns>建成的根城；失败返回 null</returns>
        public static Building CreateCamp(Scenario scenario, City city, Cell cell, int typeId, Force owner)
        {
            if (scenario == null || cell == null || owner == null)
                return null;

            BuildingType campType = scenario.GetObject<BuildingType>(typeId);
            if (campType == null)
            {
                Sango.Log.Error($"*[根城] 建筑类型 {typeId} 不存在，无法生成");
                return null;
            }

            // ① 摧毁目标格（:1659）
            DestroyNonKyoten(cell);

            // ② 摧毁 1~2 环（:1660-1661 MapRangeIterator(pos, 1, 2)）
            List<Cell> around = new List<Cell>();
            scenario.Map.GetRing(cell.x, cell.y, 1, around);
            for (int i = 0; i < around.Count; i++)
                DestroyNonKyoten(around[i]);
            around.Clear();
            scenario.Map.GetRing(cell.x, cell.y, 2, around);
            for (int i = 0; i < around.Count; i++)
                DestroyNonKyoten(around[i]);

            // ③ 建根城（:1662-1667）
            return CreateCamp(owner, campType, cell);
        }

        /// <summary>
        /// 在指定格上创建一座已完工的根城。
        ///
        /// 【归属字段怎么填，以及为什么】
        ///   · <c>BelongForce = owner</c>；<c>BelongCorps = null</c>（异族 / 贼没有军团，等价原版 district_ = -1）；
        ///   · <c>BelongCity = null</c> —— **关键**。本项目 <c>BelongCity</c> 是"所属城"，
        ///     一旦赋值就会登记进 <c>City.allBuildings</c>，跟着发生的是：
        ///       ① 城陷落时 <c>City.cs:2456-2468</c> 遍历 <c>allBuildings</c>，
        ///          70% 概率把建筑 <c>ChangeCorps</c> 给攻方、否则直接 <c>OnFall</c> 拆除；
        ///       ② <c>City.UpdateCorps</c>（City.cs:2188-2191）会把 <c>allBuildings</c> 的军团整体改掉；
        ///       ③ 经典内政的城池收入会遍历 <c>allBuildings</c> 累加 <c>buildingType.foodGain</c>
        ///          （ClassicsCityWorking.cs:191-207，不按势力过滤）。
        ///     原版根城的 <c>get_area_city_id()</c> 只是"所在领地城"（派生），不是所属城。
        /// </summary>
        /// <param name="owner">归属势力</param>
        /// <param name="campType">建筑类型（15 / 16）</param>
        /// <param name="cell">落位格</param>
        /// <returns>建成的建筑；参数非法返回 null</returns>
        public static Building CreateCamp(Force owner, BuildingType campType, Cell cell)
        {
            if (owner == null || campType == null || cell == null)
                return null;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return null;

            Building building = new Building();
            building.Id = -1;                                   // 交给 SangoObjectSet.Add 分配空槽
            building.BelongForce = owner;
            building.BelongCorps = null;                        // 异族 / 贼无军团
            building.BelongCity = null;                         // 见上方注释，绝不能挂城
            building.BuildingType = campType;
            building.x = cell.x;
            building.y = cell.y;
            building.rot = GameRandom.Range(0, 10) * 90 * Mathf.Deg2Rad;

            // 原版 create_nejiro：set_hp(get_max_hp()) + building_complete
            building.durability = campType.durabilityLimit;
            building.isComplate = true;

            scenario.Add(building);                             // 进 scenario.buildingSet（存档主路径）
            building.Init(scenario);                            // 占格 + 创建渲染
            building.OnCompleted();                             // 建成收尾（15/16 是军事设施，无副作用）
            building.Render?.UpdateRender();

            return building;
        }

        /// <summary>
        /// 取建筑所在的领地城（等价原版 <c>Building::get_area_city_id()</c>）。
        /// 项目的城界信息在格子上（<see cref="Cell.BelongCity"/>），不落在建筑上。
        /// </summary>
        /// <param name="building">建筑</param>
        /// <returns>领地城；无则 null</returns>
        public static City GetAreaCity(Building building)
        {
            if (building == null)
                return null;
            return building.CenterCell != null ? building.CenterCell.BelongCity : null;
        }

        /// <summary>
        /// 取某势力已建成的根城列表（供刷兵 / 统计用）。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="result">输出列表（调用方负责清空）</param>
        /// <returns>命中的数量</returns>
        public static int GetCamps(Force force, List<Building> result)
        {
            if (force == null || result == null)
                return 0;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.buildingSet == null)
                return 0;

            int count = 0;
            scenario.buildingSet.ForEach(building =>
            {
                if (!IsRaiderCamp(building))
                    return;
                if (building.BelongForce != force)
                    return;
                result.Add(building);
                count++;
            });
            return count;
        }

        /// <summary>
        /// 是否为根城（建筑类型 15 异族营地 / 16 贼营地）。
        ///
        /// 【命名】"根城"是中文叫法；两类建筑在原版是 raider 的据点，
        /// 统一用 <c>RaiderCamp</c> 表达，避免用日韩罗马字命名。
        /// </summary>
        /// <param name="building">建筑</param>
        /// <returns>是根城返回 true</returns>
        public static bool IsRaiderCamp(Building building)
        {
            if (building == null || !building.IsAlive)
                return false;
            BuildingType type = building.BuildingType;
            if (type == null)
                return false;
            return type.kind == BarbarianCampTypeId || type.kind == BanditCampTypeId;
        }

        /// <summary>
        /// 补正势力的 <see cref="Force.forceType"/>。
        ///
        /// 【原版对照】原版势力类型来自剧本数据（<c>force->type_</c>，s11_db.cpp:394-398 只在
        /// 数据缺省时按 Id 段补一次：0x2a~0x2d = 异民族、0x2e = 贼）。
        /// 本项目剧本 forceSet 里**没有 forceType 键**，全部读成 0 = <see cref="ForceType.Normal"/>，
        /// 于是贼势力会被 <see cref="Force.IsNormal"/> 当成普通势力。
        ///
        /// 判定顺序：
        ///   1. 君主已被 <see cref="BarbarianConfig"/> 标记为异族 → 异民族；
        ///   2. 君主是临时武将且势力无城 → 贼（异族头目也是临时武将，但第 1 步已拦掉）。
        ///
        /// 只在 <see cref="Force.forceType"/> 仍为 Normal 时写入，已配置的势力不受影响。
        /// </summary>
        /// <param name="force">势力</param>
        public static void EnsureForceType(Force force)
        {
            if (force == null || force.mGovernor == null)
                return;
            if (force.forceType != (int)ForceType.Normal)
                return;

            if (force.mGovernor.barbarianTribe > 0)
            {
                force.forceType = (int)ForceType.Barbarian;
                Sango.Log.Info($"*[根城]{force.Name} 势力类型补正为异民族（剧本缺 forceType）");
                return;
            }

            if (force.mGovernor.IsTemporary && !force.HasCity)
            {
                force.forceType = (int)ForceType.Bandit;
                Sango.Log.Info($"*[根城]{force.Name} 势力类型补正为贼（剧本缺 forceType）");
            }
        }

        // ====================================================================
        // 内部实现
        // ====================================================================

        /// <summary>
        /// 该城当前是否已有根城（原版 <c>nejiro_id[city] > 0</c>，每城最多一个）。
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="city">城</param>
        /// <returns>已有返回 true</returns>
        static bool HasCamp(Scenario scenario, City city)
        {
            bool found = false;
            scenario.buildingSet.ForEach(building =>
            {
                if (found || !IsRaiderCamp(building))
                    return;
                if (GetAreaCity(building) == city)
                    found = true;
            });
            return found;
        }

        /// <summary>
        /// 取该城的候选位池。对应原版 <c>bandit_appearance</c> 里的
        /// <c>nejiro_pos[city_id][0..MaxNejiroPosCount-1]</c>（s11_game_month.cpp:1508-1528）。
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="city">城</param>
        /// <returns>候选位列表；没有返回空列表</returns>
        static List<Cell> GetSites(Scenario scenario, City city)
        {
            EnsureSiteCache(scenario);
            List<Cell> sites;
            if (siteCache.TryGetValue(city.Id, out sites))
                return sites;
            return emptySites;
        }

        /// <summary>
        /// 全图收集候选位并按城归类（缓存一次，整局复用）。
        ///
        /// 【与原版逐条对应】s11_game_month.cpp:1514-1528：
        ///   <c>if (not hex.is_nejiro() or hex.get_unit_id() >= 0 or hex.get_fire_timer()) continue;</c>
        ///   <c>city_id = hex.get_area_city_id();</c>
        ///   每城最多 <c>MaxNejiroPosCount = 10</c> 个。
        ///
        /// 本项目的 <c>hex.nejiro</c> 标记落在 <see cref="MapGrid.GridState.Thief"/> 位上
        /// （<c>terrainState</c> 的 bit3，由地图编辑器 <c>BrushType.Thief</c> 刷出、随地图 bin 存档），
        /// 所以这里直接读 <see cref="Cell.HasGridState"/>，不再靠几何猜位置。
        /// </summary>
        /// <param name="scenario">剧本</param>
        static void EnsureSiteCache(Scenario scenario)
        {
            if (siteCacheReady && siteCacheScenario == scenario)
                return;

            siteCache.Clear();
            siteCacheScenario = scenario;
            siteCacheReady = true;

            if (scenario == null || scenario.Map == null)
                return;

            Map map = scenario.Map;
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    Cell cell = map.GetCell(x, y);
                    if (cell == null)
                        continue;
                    if (!cell.HasGridState(MapGrid.GridState.Thief))
                        continue;

                    // 原版：格上不能有部队、不能着火（建筑不判，建根城时会拆）
                    if (cell.troop != null || cell.fire != null)
                        continue;

                    City area = cell.BelongCity;
                    if (area == null)
                        continue;

                    List<Cell> list;
                    if (!siteCache.TryGetValue(area.Id, out list))
                    {
                        list = new List<Cell>();
                        siteCache[area.Id] = list;
                    }

                    if (list.Count < MaxSitePerCity)
                        list.Add(cell);
                }
            }

            Sango.Log.Info($"*[根城]候选位收集完成：{siteCache.Count} 座城共 "
                + $"{CountSites()} 个 Thief 标记点");
        }

        /// <summary>统计已收集的候选位总数（仅日志用）。</summary>
        static int CountSites()
        {
            int total = 0;
            foreach (KeyValuePair<int, List<Cell>> kv in siteCache)
                total += kv.Value.Count;
            return total;
        }

        /// <summary>
        /// 摧毁指定格上的非据点建筑（原版 <c>create_nejiro</c> 内的 <c>destroy</c>，
        /// :1651-1661）。城 / 关 / 港（<c>is_kyoten()</c>）不拆。
        /// </summary>
        /// <param name="cell">格子</param>
        static void DestroyNonKyoten(Cell cell)
        {
            if (cell == null || cell.building == null)
                return;

            Building building = cell.building as Building;
            if (building == null || !building.IsAlive)
                return;

            // 城 / 港 / 关 是据点，不拆（原版 `not building->is_kyoten()`）
            if (building.IsCityBase())
                return;

            building.OnFall(null);
        }

        /// <summary>
        /// 决定根城归属势力（原版 :1553-1568）：
        ///   · 城属于异族范围 → 对应异族势力；
        ///   · 若城内任一在任武将拥有该族的「亲×」特技（原版 <c>skill_set_add(skill, city->get_building(), false)</c>）
        ///     → 压制该异族，改由贼势力生成；
        ///   · 非异族范围城 → 贼势力。
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="city">目标城</param>
        /// <returns>归属势力；找不到返回 null</returns>
        static Force ResolveCampForce(Scenario scenario, City city)
        {
            int tribe = city.barbarianTribe;
            if (tribe > 0)
            {
                int guardFeature = BarbarianConfig.GetGuardFeatureId(tribe);
                bool suppressed = guardFeature > 0 && CityHasFeature(city, guardFeature);
                if (!suppressed)
                {
                    Force barbarian = FindBarbarianForce(scenario, tribe);
                    if (barbarian != null)
                        return barbarian;
                }
            }

            return FindBanditForce(scenario);
        }

        /// <summary>
        /// 城内任意在任武将是否拥有指定特技。
        /// 对应原版 <c>skill_set_add(skill, city->get_building(), false)</c>
        /// （s11_sys_scen_skill.cpp:583-594：取该据点君主 / 都督 / 太守 / 一般武将的全部特技）。
        /// </summary>
        /// <param name="city">城</param>
        /// <param name="featureId">特技 Id</param>
        /// <returns>存在返回 true</returns>
        static bool CityHasFeature(City city, int featureId)
        {
            if (city == null || featureId <= 0 || city.allPersons == null)
                return false;

            for (int i = 0; i < city.allPersons.Count; i++)
            {
                Person person = city.allPersons[i];
                if (person == null || !person.IsAlive)
                    continue;
                if (person.HasFeatrue(featureId))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 找指定部落的异族势力。
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="tribe">部落（1~4）</param>
        /// <returns>势力；没有返回 null</returns>
        static Force FindBarbarianForce(Scenario scenario, int tribe)
        {
            Force result = null;
            scenario.forceSet.ForEach(force =>
            {
                if (result != null || force == null || !force.IsAlive)
                    return;
                EnsureForceType(force);
                if (!force.IsBarbarian)
                    return;
                if (force.mGovernor == null || force.mGovernor.barbarianTribe != tribe)
                    return;
                result = force;
            });
            return result;
        }

        /// <summary>
        /// 找贼势力（原版 ForceId_Bandit 是固定槽位，项目剧本里对应势力 47「贼将」）。
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <returns>势力；没有返回 null</returns>
        static Force FindBanditForce(Scenario scenario)
        {
            Force result = null;
            scenario.forceSet.ForEach(force =>
            {
                if (result != null || force == null || !force.IsAlive)
                    return;
                EnsureForceType(force);
                if (force.IsBandit)
                    result = force;
            });
            return result;
        }
    }
}
