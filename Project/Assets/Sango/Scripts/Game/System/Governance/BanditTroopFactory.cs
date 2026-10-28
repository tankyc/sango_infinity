using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 根城出兵工厂。
    ///
    /// 【原版出处】两处调用同一个函数：
    ///   · 月度 <c>Game::create_nejiro</c> 建成后立刻出一支部队（s11_game_month.cpp:1574，<c>max_gold = true</c>）；
    ///   · 每周 <c>Game::on_week</c> 遍历所有根城，按概率增援（s11_game_week.cpp:636-653，<c>max_gold = false</c>）。
    ///
    /// 【兵力生成细节】<c>System::create_bandit_unit</c>（s11_sys_scen_unit.cpp:84-154）：
    ///   · 模板按根城归属势力分支：乌丸/羌 → 骑兵，山越/南蛮 → 枪/戟，贼 → 剑兵（:95-115）；
    ///   · <c>create_bandit_person</c> 克隆一个临时武将（:116，见 s11_sys_scen_person.cpp:5-24）；
    ///   · 五维各 + rand(0,5)（:128-129）；
    ///   · 兵力 (3 + rand(0,5)) × 1000 = 3000~8000（:137）；
    ///   · 金 = <c>max_gold ? 10000 : 800 + rand(0,400)</c>（:138）；
    ///   · 粮取全局上限 <c>max_unit_food_</c>（:140）；
    ///   · 命令 <c>UnitOrderId_Subjugate</c> = 攻略目标城（:147），目标城由 <c>nejiro->get_area_city_id()</c> 取得。
    ///
    /// 【每周刷兵的判定】<c>Game::bandit_unit_chance</c>（s11_game_week.cpp:655-663）：
    ///   · 城必须属于**普通势力**（异族/贼的城不再互相刷）；
    ///   · <c>n = max((治安上限 - 治安) / 5, 5)</c>，即 5%~20%；
    ///   · 根城 1 环内必须有"可通行且空"的格（:638-640）；
    ///   · 该势力在编人数上限：贼 30 / 异族 10（:643-645）。
    ///
    /// 【本项目必须偏离的两处】
    /// 1. 原版部队初始与根城**同格**（<c>set_pos(building->get_pos())</c>，s11_sys_scen_unit.cpp:77），
    ///    因为原版建筑与部队是两个独立字段。本项目 <see cref="Cell.CanStay"/> 要求
    ///    <c>building == null</c> —— 部队不允许停在建筑格上，所以直接落在根城周围空格。
    /// 2. 原版 <c>leader->set_service_id(nejiro->get_id())</c> 把武将挂在建筑上（:119）。
    ///    本项目 <see cref="Person.BelongForce"/> 派生自 <see cref="Person.BelongCorps"/>
    ///    （Person.cs:48），所以武将只能挂到势力的第 1 军团（剧本 corpsSet 43~47 已有）。
    /// </summary>
    public static class BanditTroopFactory
    {
        // ==================== 武将模板（PersonLibrary.json） ====================
        // 与 BarbarianConfig.Entries 一致：每族一个头目 + 一个武将。
        // 贼势力用 746「贼将」——原版是 PersonId_Hizoku = 0x2e9(745)，
        // 本项目同段 Id 整体 +1（745 = 卫兵），贼将落在 746。
        const int UganBossId = 722;
        const int UganOfficerId = 723;
        const int KyouBossId = 724;
        const int KyouOfficerId = 725;
        const int SanetsuBossId = 726;
        const int SanetsuOfficerId = 727;
        const int NanbanBossId = 728;
        const int NanbanOfficerId = 729;
        const int BanditBossId = 746;

        // ==================== 兵种（TroopTypes.json） ====================
        const int TroopTypeSword = 1;      // 剑兵
        const int TroopTypeSpear = 2;      // 枪兵
        const int TroopTypeHalberd = 3;    // 戟兵
        const int TroopTypeHorse = 5;      // 骑兵

        /// <summary>异族势力在编人数上限（原版 s11_game_week.cpp:643 的 10）</summary>
        const int MaxPersonCountBarbarian = 10;

        /// <summary>贼势力在编人数上限（原版 s11_game_week.cpp:643 的 30）</summary>
        const int MaxPersonCountBandit = 30;

        /// <summary>治安上限（原版 <c>City::get_max_public_order()</c> 恒为 100）</summary>
        const int MaxSecurity = 100;

        /// <summary>根城周围搜索空位的环数（原版 <c>range_contains(pos, 1, ...)</c>，:638）</summary>
        const int SpawnRing = 1;

        /// <summary>
        /// 单个领地城最多同时存在的部队数（原版 <c>unit_count[city] &gt;= 2</c>，s11_game_week.cpp:631）。
        /// 统计口径见 <see cref="CountUnitsInArea"/>：领地内**所有势力**的部队都算。
        /// </summary>
        const int MaxUnitPerAreaCity = 2;

        /// <summary>
        /// 每月回合数：36 回合/年 ÷ 12 月 = 3（与 <see cref="BanditCampPlacer"/> 的 TurnsPerMonth 同口径）
        /// </summary>
        const int TurnsPerMonth = 3;

        /// <summary>首次出兵的金（原版 <c>max_unit_gold_</c>；s11_sys.h:1272 注释即「金 10000」）</summary>
        const int MaxUnitGold = 10000;

        static readonly List<Building> campBuffer = new List<Building>();
        static readonly List<Cell> ringBuffer = new List<Cell>();

        // ====================================================================
        // 对外入口
        // ====================================================================

        /// <summary>
        /// 每回合（原版每周）遍历所有根城尝试增援。对齐 <c>Game::on_week</c>
        /// （s11_game_week.cpp:636-653）。
        ///
        /// 由 <see cref="BanditCampSystem"/> 订阅 <c>GameEvent.OnTurnStart</c> 调用。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>本次生成的部队数</returns>
        public static int TrySpawnAll(Scenario scenario)
        {
            if (scenario == null || scenario.buildingSet == null)
                return 0;

            // 【月初第一个回合不刷】原版 <c>bandit_unit_appearance</c> 开头
            // <code>
            // // 본거지 생성 턴에는 부대가 등장하지 않음
            // if (is_new_months_day())
            //     return;
            // </code>
            // （s11_game_week.cpp:619-621）：月度 <c>bandit_appearance</c> 刚建好根城并
            // 立刻出了一支部队（s11_game_month.cpp:1574），同一 tick 不再重复判定。
            // <para>
            // 本项目 <c>OnMonthUpdate</c> 在跨月那次 IncreaseDate（回合末）触发、
            // <c>OnTurnStart</c> 在下一回合开始触发，两者相邻 —— 等价为"每个月的第 1 个回合跳过"。
            // 不跳过的话，跨月这一回合会出现「建城必出 1 支」+「回合开始按概率再出 1 支」两次机会。
            // </para>
            if (scenario.TurnCount > 0 && scenario.TurnCount % TurnsPerMonth == 0)
                return 0;

            // 先快照：建队过程不会改 buildingSet，但避免"遍历中对象池变化"的隐患
            campBuffer.Clear();
            scenario.buildingSet.ForEach(building =>
            {
                if (BanditCampPlacer.IsRaiderCamp(building))
                    campBuffer.Add(building);
            });

            int spawned = 0;
            for (int i = 0; i < campBuffer.Count; i++)
            {
                if (SpawnFromCamp(campBuffer[i], scenario, false) != null)
                    spawned++;
            }

            campBuffer.Clear();
            return spawned;
        }

        /// <summary>
        /// 从单座根城尝试出一支部队。月度建成后的首刷传 <paramref name="maxGold"/> = true
        /// （原版 <c>create_bandit_unit(nejiro, true)</c>），每周增援传 false。
        /// </summary>
        /// <param name="camp">根城（建筑类型 15 / 16）</param>
        /// <param name="scenario">当前剧本</param>
        /// <param name="maxGold">是否给满金</param>
        /// <returns>生成的部队；未命中返回 null</returns>
        public static Troop SpawnFromCamp(Building camp, Scenario scenario, bool maxGold)
        {
            if (!BanditCampPlacer.IsRaiderCamp(camp) || scenario == null)
                return null;

            Force force = camp.BelongForce;
            if (force == null || !force.IsAlive)
                return null;

            // 领地城：原版 nejiro->get_area_city_id()
            City areaCity = BanditCampPlacer.GetAreaCity(camp);
            if (areaCity == null || !areaCity.IsAlive)
                return null;

            // 原版 bandit_unit_chance 的 `city->is_normal_force()`：目标城须属于普通势力
            if (areaCity.BelongForce == null || !areaCity.BelongForce.IsNormal)
                return null;

            // 【该领地已有 2 支部队就不再增援】
            // 对齐 s11_game_week.cpp:631 `unit_count[city->get_id()] >= 2` 与
            // System::get_unit_count（s11_sys_scen_city.cpp:680-697）：按 <c>unit->get_area_city_id()</c>
            // 统计**全图所有存活部队**（不分势力，敌我双方都算），不是只数贼兵。
            // 这是"贼兵被灭后从根城刷太快"的主要缺口 —— 少了这道闸，只要在编人数没到 30，
            // 每个回合都会重新掷一次 5%~20%。
            // 【只作用于每周增援】月度建城后的首刷（maxGold = true）不查这条 ——
            // 原版 <c>bandit_appearance</c> 在 <c>create_nejiro</c> 后直接建队（s11_game_month.cpp:1574），
            // 不经过 <c>unit_count</c> 判断。
            if (!maxGold && CountUnitsInArea(areaCity, scenario) >= MaxUnitPerAreaCity)
                return null;

            // 原版 bandit_unit_chance 的概率部分（:661-662）：n = max((100 - 治安) / 5, 5)
            int chance = System.Math.Max((MaxSecurity - areaCity.security) / 5, 5);
            if (!GameRandom.Chance(chance))
                return null;

            // 原版要求在编人数未达上限（:643-645）
            int maxPerson = force.IsBandit ? MaxPersonCountBandit : MaxPersonCountBarbarian;
            if (CountPersons(force, scenario) >= maxPerson)
                return null;

            // 原版要求根城周围 1 格内有可通行空格（:638-640）
            Cell spawnCell = FindSpawnCell(camp, scenario);
            if (spawnCell == null)
                return null;

            // 武将必须挂军团才能派生 BelongForce（Person.cs:48）
            Corps corps = force.mGovernor != null ? force.mGovernor.BelongCorps : null;
            if (corps == null)
            {
                Sango.Log.Warning($"*[根城]{force.Name} 没有军团，无法出兵（主将无 BelongCorps）");
                return null;
            }

            // 克隆主将
            Person leader = CreateLeader(force, corps, scenario);
            if (leader == null)
                return null;

            // 建队 + 下令
            Troop troop = CreateTroop(leader, spawnCell, maxGold, scenario);
            if (troop == null)
            {
                PersonStateSystem.LeaveToWild(leader);
                return null;
            }

            troop.SetMission(MissionType.TroopBanditRazeCity, areaCity.Id);

            Sango.Log.Info($"*[根城]{force.Name} 的 {camp.BuildingType.Name}"
                + $"（{camp.x},{camp.y}）杀出 {troop.Name} 部队 {troop.troops} 兵，"
                + $"攻略 <{areaCity.Name}>（治安 {areaCity.security}，概率 {chance}%）");

            return troop;
        }

        // ====================================================================
        // 内部实现
        // ====================================================================

        /// <summary>
        /// 从模板克隆一个临时武将并登记到剧本。
        ///
        /// 【对齐原版 create_bandit_person（s11_sys_scen_person.cpp:5-24）】
        ///   <c>get_inactive_person()</c> → 本项目 <c>new Person()</c> + <c>personSet.Add</c> 分配空槽；
        ///   <c>copy_from(preset)</c>     → <see cref="Person.FormLib2"/>（Person.cs:3884 的深拷贝）；
        ///   <c>set_type(Temporary)</c>   → 保持模板的 type（722~729 / 746 数据里就是 2）；
        ///   <c>set_mibun(Zaiya)</c>      → 这里直接置 Normal（原版随后 :127 会改成一般）。
        /// </summary>
        /// <param name="force">归属势力</param>
        /// <param name="corps">该势力的第 1 军团</param>
        /// <param name="scenario">剧本</param>
        /// <returns>新建的主将；模板缺失返回 null</returns>
        static Person CreateLeader(Force force, Corps corps, Scenario scenario)
        {
            int templateId = PickTemplateId(force);
            PersonLib lib = scenario.CommonData.PersonLibrary.Get(templateId);
            if (lib == null)
            {
                Sango.Log.Error($"*[根城]{force.Name} 出兵失败：武将模板 {templateId} 不存在");
                return null;
            }

            Person leader = Person.FormLib2(lib);
            leader.Id = -1;                                         // 交给 personSet 分配空槽
            leader.state = (int)PersonStateType.Normal;              // 原版 :127 person_set_mibun(Ippan)
            leader.type = (int)PersonType.Temporary;

            // 异族标记必须手动带上：FixupBarbarianTribe 用「Id + 名字」双校验（Person.cs:852），
            // 克隆体 Id 变了就匹配不上，会被当成普通武将送去归属巡检（BelongCity 为 null → 被下野）。
            leader.barbarianTribe = force.IsBandit ? 0 : force.mGovernor.barbarianTribe;

            // 五维各 + rand(0,5)（原版 :128-129）
            leader.command.baseValue += GameRandom.Range(0, 6);
            leader.strength.baseValue += GameRandom.Range(0, 6);
            leader.intelligence.baseValue += GameRandom.Range(0, 6);
            leader.politics.baseValue += GameRandom.Range(0, 6);
            leader.glamour.baseValue += GameRandom.Range(0, 6);

            // 生卒年（原版 :130-135）：30 岁登场；临时武将不参与寿命结算（HasLifeCycle = false），
            // 这里只保证年龄 / 能力衰减有个合理起点。
            int year = scenario.Info != null ? scenario.Info.year : 200;
            leader.yearBorn = year - 30;
            leader.yearDead = leader.yearBorn + 99;
            leader.appearance = year - 10;

            // 归属：只挂军团（BelongForce 由军团派生），不挂任何城
            leader.BelongCorps = corps;
            leader.BelongCorpsId = corps.Id;
            leader.BelongForceId = force.Id;
            leader.BelongCity = null;
            leader.BelongCityId = 0;
            leader.CurrentCity = null;
            leader.CurrentCityId = 0;
            leader.IsAlive = true;

            scenario.Add(leader);
            // 必须先入池再 Prepare：Prepare 里按 Id 解析军团 / 计算五维最终值 / 装配个人关系
            leader.OnScenarioPrepare(scenario);

            return leader;
        }

        /// <summary>
        /// 在指定格上组建一支部队并完成初始化（不经过 <c>City.EnsureTroop</c> ——
        /// 那是"从城里出兵"的路径，会把部队先塞进城市中心格，而根城部队没有城）。
        /// </summary>
        /// <param name="leader">主将</param>
        /// <param name="cell">出生格（必须是可停留的空格）</param>
        /// <param name="maxGold">是否给满金</param>
        /// <param name="scenario">剧本</param>
        /// <returns>初始化完成的部队；失败返回 null</returns>
        static Troop CreateTroop(Person leader, Cell cell, bool maxGold, Scenario scenario)
        {
            if (leader == null || cell == null)
                return null;

            Force force = leader.BelongForce;
            if (force == null)
                return null;

            int troopTypeId = PickTroopTypeId(force);
            TroopType landType = scenario.GetObject<TroopType>(troopTypeId);
            if (landType == null)
                landType = scenario.GetObject<TroopType>(TroopTypeSword);

            // 兵力 (3 + rand(0,5)) × 1000 = 3000~8000（原版 :137）
            int troops = (3 + GameRandom.Range(0, 6)) * 1000;

            Troop troop = new Troop();
            troop.Id = -1;
            troop.Leader = leader;
            troop.LandTroopType = landType;
            troop.troops = troops;
            troop.morale = 100;
            troop.energy = 100;
            // 粮：原版取 max_unit_food_；本项目没有该上限变量，
            // 沿用 CityExpedition.SetTroops（CityExpedition.cs:616）的口径「20 回合的粮」。
            troop.food = (int)(troops * scenario.Variables.baseFoodCostInTroop * 20);
            // 金：原版 max_gold ? 10000 : 800 + rand(0,400)（:138）
            troop.gold = maxGold ? MaxUnitGold : 800 + GameRandom.Range(0, 401);
            troop.IsAlive = true;
            troop.ActionOver = false;

            // 先定格再入池：Troop.Init 要用 cell 建渲染、抛 OnTroopEnterCell
            troop.cell = cell;
            cell.troop = troop;
            scenario.Add(troop);
            troop.Init(scenario);

            // 编制上限要等 Init 里的 CalculateAttribute 之后才有效
            if (troop.MaxTroops > 0 && troop.troops > troop.MaxTroops)
                troop.troops = troop.MaxTroops;

            return troop;
        }

        /// <summary>
        /// 找根城周围可停留的空格。
        /// 判定与 <see cref="Cell.CanStay"/> 同口径：无部队 / 无建筑 / 地形可走；
        /// 额外排除火焰格与内政地（内政地上有占位模型，部队站上去会与其重叠）。
        /// </summary>
        /// <param name="camp">根城</param>
        /// <param name="scenario">剧本</param>
        /// <returns>可用格；没有返回 null</returns>
        static Cell FindSpawnCell(Building camp, Scenario scenario)
        {
            if (camp == null || scenario.Map == null)
                return null;

            ringBuffer.Clear();
            scenario.Map.GetRing(camp.x, camp.y, SpawnRing, ringBuffer);
            for (int i = 0; i < ringBuffer.Count; i++)
            {
                Cell cell = ringBuffer[i];
                if (cell == null)
                    continue;
                if (cell.building != null || cell.troop != null || cell.fire != null)
                    continue;
                if (cell.IsInterior)
                    continue;

                TerrainType terrain = cell.TerrainType;
                if (terrain == null || !terrain.moveable)
                    continue;

                return cell;
            }

            return null;
        }

        /// <summary>
        /// 按部落选模板：头目 30% / 武将 70%（原版 <c>rand_bool(30)</c>，:98-111）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>武将库模板 Id</returns>
        static int PickTemplateId(Force force)
        {
            if (force.IsBandit)
                return BanditBossId;

            bool boss = GameRandom.Chance(30);
            switch ((BarbarianConfig.Tribe)force.mGovernor.barbarianTribe)
            {
                case BarbarianConfig.Tribe.Wuhuan: return boss ? UganBossId : UganOfficerId;
                case BarbarianConfig.Tribe.Qiang: return boss ? KyouBossId : KyouOfficerId;
                case BarbarianConfig.Tribe.Shanyue: return boss ? SanetsuBossId : SanetsuOfficerId;
                case BarbarianConfig.Tribe.Nanman: return boss ? NanbanBossId : NanbanOfficerId;
                default: return BanditBossId;
            }
        }

        /// <summary>
        /// 按部落选兵种（原版 :95-115 的装备分支）：
        ///   乌丸 / 羌 → 骑兵；山越 → 枪 / 戟；南蛮 → 枪 / 戟；贼 → 剑兵。
        /// </summary>
        /// <param name="force">势力</param>
        /// <returns>兵种 Id</returns>
        static int PickTroopTypeId(Force force)
        {
            if (force.IsBandit)
                return TroopTypeSword;

            switch ((BarbarianConfig.Tribe)force.mGovernor.barbarianTribe)
            {
                case BarbarianConfig.Tribe.Wuhuan:
                case BarbarianConfig.Tribe.Qiang:
                    return TroopTypeHorse;
                case BarbarianConfig.Tribe.Shanyue:
                case BarbarianConfig.Tribe.Nanman:
                    return GameRandom.Chance(50) ? TroopTypeSpear : TroopTypeHalberd;
                default:
                    return TroopTypeSword;
            }
        }

        /// <summary>
        /// 统计某座城领地内的存活部队数（不分势力）。
        ///
        /// 【原版对齐】<c>System::get_unit_count</c>（s11_sys_scen_city.cpp:680-697）遍历
        /// <c>get_active_units()</c>，对每支部队取 <c>unit->get_area_city_id()</c> 累加计数 ——
        /// 既包括贼 / 异族自己的部队，也包括玩家与其它势力的部队。
        /// 本项目部队所在格的领地城即 <see cref="Cell.BelongCity"/>，
        /// 与 <see cref="BanditCampPlacer.GetAreaCity"/> 用的是同一套派生口径。
        /// <para>
        /// 【为什么要数敌我双方】原版这是"根城增援"的节流条件：领地内已经有 2 支部队
        /// （哪怕全是来围剿的敌军）时，根城就不再往外派兵。
        /// </para>
        /// </summary>
        /// <param name="city">领地城</param>
        /// <param name="scenario">剧本</param>
        /// <returns>部队数量</returns>
        static int CountUnitsInArea(City city, Scenario scenario)
        {
            if (city == null || scenario == null || scenario.troopsSet == null)
                return 0;

            int count = 0;
            scenario.troopsSet.ForEach(troop =>
            {
                if (troop == null || !troop.IsAlive || troop.cell == null)
                    return;
                if (troop.cell.BelongCity == city)
                    count++;
            });
            return count;
        }

        /// <summary>
        /// 统计势力当前在编人数（含部队中的人）。
        /// 用全局武将池扫描而不是 Force.ForEachPerson：后者按军团 / 城遍历，
        /// 无城势力的临时武将更容易漏算，导致人数上限失效。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">剧本</param>
        /// <returns>在编人数</returns>
        static int CountPersons(Force force, Scenario scenario)
        {
            if (scenario == null || scenario.personSet == null)
                return 0;

            int count = 0;
            scenario.personSet.ForEach(person =>
            {
                if (person == null || !person.IsAlive)
                    return;
                if (person.BelongForce != force)
                    return;
                count++;
            });
            return count;
        }
    }
}
