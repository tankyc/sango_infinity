/*
 * 文件名：ForceAIOutpost.cs
 * 描述：势力级 AI —— 据点态势采集与守将指派（ForceAI 的 partial 分支）。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 主入口        —— s11_game_ai_force.cpp:145-155  load_force_data
 *   · 据点态势      —— s11_game_ai_force.cpp:157-344  势力据点态势装载
 *   · 势力级更新    —— s11_game_ai_force.cpp:412-430  update_force_data
 *   · 守将指派      —— s11_game_ai_force.cpp:700-761  set_guard
 *   · 守将评分      —— s11_game_ai_force.cpp:797-814  get_guard_stat_score
 *
 * 说明：本文件只负责"势力开打之前把情报算清楚"，不含具体命令执行。
 *       命令执行在 CorpsAI（军团层）与 CityAI（据点层）。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public partial class ForceAI
    {
        /// <summary>
        /// 势力 AI 第一段：采集全势力据点态势。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:145-155
        /// <code>
        /// bool GameAI::load_force_data() {
        ///     game_-&gt;ai_table_reset_hostile_force(force_id);
        ///     game_-&gt;ai_table_reset_friendly_force(force_id);
        ///     势力据点态势装载();
        ///     load_force_unit_data();
        /// }
        /// </code>
        /// 差异：原版 `load_force_unit_data`（部队出击分组）对应本项目的 Troop 任务系统，
        ///       已在 TroopMissionBehaviour 里独立实现，这里不重复搬运。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIPrepareForceData(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            // 回合级缓存复位：对齐 GameAI::setup()（s11_game_ai.cpp:355-377）
            OutpostRuntime.Reset();
            OutpostRuntime.force = force;

            LoadForceOutpostData(force, scenario);

            // 敌对 / 友好势力表：对齐 update_force_data（s11_game_ai_force.cpp:412-430）
            //   ① set_hostile_force —— 本轮重点打谁
            //   ② set_friendly_force —— 本轮拉拢谁
            // 放在据点态势之后：friendly 判定依赖 Front/Battle 据点数。
            UpdateHostileAndFriendly(force, scenario);

            // ③ set_guard —— 据点守将
            SetGuard(force, scenario);
            return true;
        }

        /// <summary>
        /// 采集全势力据点态势。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:157-344 势力据点态势装载。
        /// 处理顺序严格照搬原版：人数 → 部队 → 逐据点 → 敌方本阵/军事设施 → 距国境距离 → 状态归类。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        public static void LoadForceOutpostData(Force force, Scenario scenario)
        {
            int forceId = force.Id;

            // ---- 步骤 1：统计每个据点的人数与可下令人数 ----
            // 原版 s11_game_ai_force.cpp:163-176：遍历武将池，跳过俘虏，按 service_id 归到据点。
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive)
                    continue;
                if (person.BelongForce != force)
                    continue;
                // 俘虏不计入据点人数（原版 person->is_horyo() 跳过）
                if (person.state == (int)PersonStateType.Prisoner)
                    continue;
                City service = person.BelongCity ?? person.CurrentCity;
                if (service == null)
                    continue;
                OutpostInfo info = OutpostRuntime.Get(service.Id);
                info.personCount++;
                // 可下令 = 本回合没有任何任务在身（原版 game_->can_receive_order）
                if (person.IsFree)
                    info.idlePersonCount++;
            }

            // ---- 步骤 2：统计每个据点的敌我部队数量 ----
            // 原版 s11_game_ai_force.cpp:177-206：按部队所在地格的所属据点归并。
            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive)
                    continue;
                City owner = FindOutpostOfTroop(troop, scenario);
                if (owner == null)
                    continue;
                OutpostInfo info = OutpostRuntime.Get(owner.Id);
                if (troop.BelongForce == force)
                {
                    info.ownUnitCount++;
                }
                else
                {
                    if (troop.BelongForce != null && troop.BelongForce.IsPlayer)
                        info.playerUnitCount++;
                    if (troop.BelongForce == null || force.IsEnemy(troop.BelongForce))
                    {
                        info.enemyUnitCount++;
                        // 邻接据点的"周边敌军数"：原版 s11_game_ai_force.cpp:197-203
                        for (int n = 0; n < owner.NeighborList.Count; n++)
                        {
                            City nb = owner.NeighborList[n];
                            if (nb == null || !nb.IsAlive)
                                continue;
                            OutpostRuntime.Get(nb.Id).neighborEnemyUnitCount++;
                        }
                    }
                }
            }

            // ---- 步骤 3：逐据点计算战场环境与收支 ----
            // 原版 s11_game_ai_force.cpp:207-257
            List<City> ownOutpost = new List<City>();
            force.ForEachCityBase(c =>
            {
                if (c != null && c.IsAlive)
                    ownOutpost.Add(c);
            });

            for (int i = 0; i < ownOutpost.Count; i++)
            {
                City city = ownOutpost[i];
                OutpostInfo info = OutpostRuntime.Get(city.Id);

                // 据点 AI 的公共预处理：新调度路径不再走 City.AIPrepare（那是旧命令链的入口），
                // 因此这里补上它原本承担的三件事，否则 AIAttack / AIReinforce 拿不到敌军快照。
                city.PrepareEnemiesInfo(scenario);
                city.UpdateActiveTroopTypes();
                city.UpdateFightPower();

                // 3~5 格内的敌军：原版 building_has_enemy / map_has_enemy(5 或 4)
                // 都市判 3/5 格，关隘港口判 2/4 格 —— s11_game_ai_force.cpp:220-221
                int nearRound = city.IsCity() ? 3 : 2;
                int farRound = city.IsCity() ? 5 : 4;
                // PrepareEnemiesInfo 已按距离填好 enemiesRound，直接查表即可，
                // 不必再对全地图部队池做一遍距离计算。
                info.existEnemy3 = city.IsEnemiesRound(nearRound);
                info.existEnemy5 = city.IsEnemiesRound(farRound);

                // 邻接敌方据点数量：原版 s11_game_ai_force.cpp:222-248 的 attack_dest_building_list
                CountNeighborEnemyOutpost(city, force, info);

                // 收支：原版 get_gold_revenue / get_gold_expenses / get_food_harvest / get_food_use
                // 对应本项目 City.BaseGainGold / GoldCost / BaseGainFood / FoodCost
                info.goldRevenue = city.BaseGainGold;
                info.goldExpenses = city.GoldCost(scenario);
                info.goldIncome = info.goldRevenue - info.goldExpenses;
                info.foodHarvest = city.BaseGainFood;
                info.foodUse = city.FoodCost(scenario);
                info.foodIncome = info.foodHarvest - info.foodUse;

                // 兵力上限：原版 ai_table_calc_expected_deployable_troops(food, food_income, 20)
                // 公式见 s11_sys_scen_ai.cpp:318-323 —— (food + food_income) * 10 / turns
                info.maxTroops = CalcExpectedDeployableTroops(city.food, info.foodIncome, 20);
                info.maxUnitCount = info.maxTroops / 7500;
            }

            // ---- 步骤 4：敌方本阵（阵/砦）与敌方军事设施 ----
            // 原版 s11_game_ai_force.cpp:258-292
            //   · FacilityId_Den / FacilityId_Base 建在我方城域内 → 记为该据点的 mainCamp
            //   · is_military_type 的敌方建筑 → 所属据点 enemy_facility_count++
            // 本项目把"阵/砦"合并为 BuildingKindType.Camp（见 BuildingKindType.cs:68-71），
            // 因此 mainCamp 与军事设施统计共用同一次遍历。
            for (int i = 0; i < ownOutpost.Count; i++)
            {
                City city = ownOutpost[i];
                OutpostInfo info = OutpostRuntime.Get(city.Id);
                if (city.allBuildings == null)
                    continue;
                for (int b = 0; b < city.allBuildings.Count; b++)
                {
                    Building building = city.allBuildings[b];
                    if (building == null || !building.IsAlive)
                        continue;
                    Force owner = building.BelongForce;
                    if (owner != null && !force.IsEnemy(owner))
                        continue;
                    if (building.BuildingType == null)
                        continue;
                    if (building.BuildingType.kind == (int)BuildingKindType.Camp)
                    {
                        info.mainCamp = building.Id;
                        info.enemyFacilityCount++;
                    }
                }
            }

            // ---- 步骤 5：距国境的距离 ----
            // 原版 s11_game_ai_force.cpp:293-308：以"有敌邻的据点"为 0，向己方据点扩散最短跳数。
            ComputeDistanceFromBorder(ownOutpost, force);

            // ---- 步骤 6：状态归类与补给判定 ----
            // 原版 s11_game_ai_force.cpp:309-343
            for (int i = 0; i < OutpostRuntime.stateCount.Length; i++)
                OutpostRuntime.stateCount[i] = 0;

            for (int i = 0; i < ownOutpost.Count; i++)
            {
                City city = ownOutpost[i];
                OutpostInfo info = OutpostRuntime.Get(city.Id);

                OutpostState state;
                if (info.existEnemy3 || info.existEnemy5 || info.enemyFacilityCount > 0
                    || info.mainCamp >= 0 || info.enemyUnitCount > 0)
                    state = OutpostState.Battle;
                else if (info.neighborEnemyOutpostCount > 0)
                    state = OutpostState.Front;
                else if (info.distanceFromBorder == 1)
                    state = OutpostState.Middle;
                else
                    state = OutpostState.Rear;
                info.state = state;

                // 需要的最小武将数：前线 / 战斗 / 都市 —— s11_game_ai_force.cpp:326-327
                if (state == OutpostState.Front || state == OutpostState.Battle || city.IsCity())
                    info.minPersonCount = 3 + info.neighborEnemyOutpostCount * 2;

                OutpostRuntime.stateCount[(int)state]++;

                // 前线补给判定 —— s11_game_ai_force.cpp:329-342
                if (state == OutpostState.Front)
                {
                    int gold = city.gold + info.goldIncome * OutpostRuntime.GoldReserveTurn;
                    int food = city.food + info.foodIncome * OutpostRuntime.FoodReserveTurn;
                    if (gold < 0 || food < 0)
                        info.shortage = true;

                    // 关隘 / 港口：按人数与统率推算应有的驻军
                    if (!city.IsCity())
                    {
                        int personCount = System.Math.Min(info.personCount, info.minPersonCount);
                        int requiredTroops = System.Math.Min(personCount * 5000,
                            GetMaxCommandSum(city, personCount));
                        if (city.troops < requiredTroops)
                            info.shortage = true;
                    }
                }
            }

            if (AIConfig.Instance.logOutpostAI)
            {
                Sango.Log.Info($"*[据点态势]{force.Name} 后{OutpostRuntime.stateCount[(int)OutpostState.Rear]} " +
                    $"中{OutpostRuntime.stateCount[(int)OutpostState.Middle]} " +
                    $"前{OutpostRuntime.stateCount[(int)OutpostState.Front]} " +
                    $"战{OutpostRuntime.stateCount[(int)OutpostState.Battle]} " +
                    $"据点{OutpostRuntime.outposts.Count}");
            }
        }

        /// <summary>
        /// 指派据点守将。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:700-761 set_guard。
        /// 原版先处理"拥有异民族抑制特技但被派到别处"的武将，再处理普通据点，
        /// 最后把空据点统一交给 place_guard 按治安升序补人。本项目的特技分派（set_guard 前半段）
        /// 依赖 ai_table_get_skill_area_building_list，暂无对应索引表，本阶段只实现后半段。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        public static void SetGuard(Force force, Scenario scenario)
        {
            if (force.guardPersonIds == null)
                force.guardPersonIds = new Dictionary<int, int>();

            List<City> emptyList = new List<City>();

            force.ForEachCityBase(c =>
            {
                if (c == null || !c.IsAlive)
                    return;

                // 已经有守将且仍在城内 → 保留
                int guardId;
                Person guard = null;
                if (force.guardPersonIds.TryGetValue(c.Id, out guardId))
                    guard = scenario.personSet.Get(guardId);
                if (guard != null && guard.IsAlive && guard.CurrentCity == c && guard.IsFree)
                    return;

                // 城内挑一个空闲武将（统率越高越优先，避免把猛将钉在城里）
                // 原版 据点可下令人列表 + get_guard_stat_score
                Person best = null;
                int bestScore = int.MinValue;
                for (int i = 0; i < c.freePersons.Count; i++)
                {
                    Person p = c.freePersons[i];
                    if (p == null || !p.IsAlive)
                        continue;
                    int score = GetGuardStatScore(p);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }
                if (best != null)
                    force.guardPersonIds[c.Id] = best.Id;
                else
                    emptyList.Add(c);
            });

            // 空据点：按治安升序（治安越差越优先补人），从其它据点的闲置武将里调
            // 原版 s11_game_ai_force.cpp:763-795 place_guard：
            //   距离近的优先，距离相同时 get_guard_stat_score 大者在前（:786-788 `a_score >= b_score`）
            //   —— score 大 = 能力低（s11_game_ai.h:173 "능력치가 높을 수록 낮음"），即不拿猛将守空城
            if (emptyList.Count == 0)
                return;

            emptyList.Sort((a, b) =>
            {
                int c = a.security.CompareTo(b.security);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });

            List<Person> candidates = new List<Person>();
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person p = scenario.personSet[i];
                if (p == null || !p.IsAlive || p.BelongForce != force)
                    continue;
                if (p.state == (int)PersonStateType.Prisoner || !p.IsFree)
                    continue;
                // 已经担任别的据点守将的不动
                bool isGuard = false;
                foreach (KeyValuePair<int, int> kv in force.guardPersonIds)
                {
                    if (kv.Value == p.Id)
                    {
                        isGuard = true;
                        break;
                    }
                }
                if (isGuard)
                    continue;
                candidates.Add(p);
            }

            for (int i = 0; i < emptyList.Count; i++)
            {
                if (candidates.Count == 0)
                    break;
                City city = emptyList[i];
                Person pick = null;
                int pickDistance = int.MaxValue;
                int pickScore = int.MinValue;
                for (int j = 0; j < candidates.Count; j++)
                {
                    Person p = candidates[j];
                    City from = p.BelongCity ?? p.CurrentCity;
                    int distance = from == null ? int.MaxValue : GetCityDistance(city, from);
                    int score = GetGuardStatScore(p);
                    // 距离相同取 score 最大者（能力最低）——对齐原版 place_guard :786-788
                    if (distance < pickDistance || (distance == pickDistance && score > pickScore))
                    {
                        pick = p;
                        pickDistance = distance;
                        pickScore = score;
                    }
                }
                if (pick == null)
                    break;
                force.guardPersonIds[city.Id] = pick.Id;
                candidates.Remove(pick);
            }
        }

        /// <summary>
        /// 守将适配度评分：数值越高越不适合当守将（原版语义如此，取 max 反而选到能力最低的）。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:797-814 get_guard_stat_score。
        /// <code>
        /// int n = get_max_person_stat();
        /// if (leadership &lt; 60) n = leadership + 40;
        /// for (i in 所有属性) { if (leadership &lt;= 69) continue; n -= max(stat - 69, 0); }
        /// </code>
        /// 注意：原版这段有明显可疑处（循环里判断的是 leadership 而不是当前 stat，见 s11_suspicious(2) 注释），
        /// 这里按原版**实际执行**的逻辑搬运，不做"修正"。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>评分</returns>
        public static int GetGuardStatScore(Person person)
        {
            if (person == null)
                return int.MaxValue;

            int maxStat = 100;
            int leadership = person.Command;
            int n = maxStat;
            if (leadership < 60)
                n = leadership + 40;

            int[] stats = { person.Command, person.Strength, person.Intelligence, person.Politics, person.Glamour };
            for (int i = 0; i < stats.Length; i++)
            {
                if (leadership <= 69)
                    continue;
                n -= System.Math.Max(stats[i] - 69, 0);
            }
            return n;
        }

        /// <summary>
        /// 据点内可带兵统率之和（取前 N 名武将）。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:1629-1644 System::ai_table_get_max_command_sum。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="maxPersonCount">最多统计的武将数</param>
        /// <returns>统率之和</returns>
        public static int GetMaxCommandSum(City city, int maxPersonCount)
        {
            if (city == null || maxPersonCount <= 0)
                return 0;

            List<int> commands = new List<int>();
            for (int i = 0; i < city.allPersons.Count; i++)
            {
                Person p = city.allPersons[i];
                if (p == null || !p.IsAlive)
                    continue;
                if (p.state == (int)PersonStateType.Prisoner)
                    continue;
                commands.Add(p.Command);
            }
            if (commands.Count == 0)
                return 0;

            commands.Sort((a, b) => b.CompareTo(a));
            int n = 0;
            int count = System.Math.Min(maxPersonCount, commands.Count);
            for (int i = 0; i < count; i++)
                n += commands[i];
            return n;
        }

        /// <summary>
        /// 按粮食推算可出兵数量。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:318-323 System::ai_table_calc_expected_deployable_troops。
        /// <code>if (turns &gt; 0 and (food + food_income) &gt; 0) return (food + food_income) * 10 / turns;</code>
        /// </summary>
        /// <param name="food">现有粮食</param>
        /// <param name="foodIncome">每回合粮食净增</param>
        /// <param name="turns">预期支撑回合数</param>
        /// <returns>可出兵数</returns>
        public static int CalcExpectedDeployableTroops(int food, int foodIncome, int turns)
        {
            if (turns > 0 && (food + foodIncome) > 0)
                return (food + foodIncome) * 10 / turns;
            return 0;
        }

        /// <summary>
        /// 按粮食推算可供养回合数。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:325-330 System::ai_table_calc_expected_food_use。
        /// <code>if (turns &gt; 0) return turns * (troops / 10);</code>
        /// </summary>
        /// <param name="troops">兵力</param>
        /// <param name="turns">回合数</param>
        /// <returns>粮食消耗</returns>
        public static int CalcExpectedFoodUse(int troops, int turns)
        {
            if (turns > 0)
                return turns * (troops / 10);
            return 0;
        }

        /// <summary>
        /// 统计邻接敌方（含无主）据点数量。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:222-248。
        /// 原版用 ai_table_get_attack_dest_building_list(id, 1 + (都市?2:0), false) 取可达邻接据点，
        /// 再按"敌方 / 空城 / 不可达"三种情况累加。本项目直接用 City.NeighborList，
        /// 语义等价（都是地图邻接关系）。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="force">当前势力</param>
        /// <param name="info">据点态势数据</param>
        static void CountNeighborEnemyOutpost(City city, Force force, OutpostInfo info)
        {
            if (city.NeighborList == null)
                return;
            bool empty = false;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City nb = city.NeighborList[i];
                if (nb == null || !nb.IsAlive)
                    continue;
                Force nbForce = nb.BelongForce;
                if (nbForce == null)
                {
                    // 无主据点：原版算作"可以打"，累加 1
                    empty = true;
                    continue;
                }
                if (nbForce == force)
                    continue;
                if (force.IsEnemy(nbForce))
                    info.neighborEnemyOutpostCount++;
            }
            if (empty)
                info.neighborEnemyOutpostCount++;
        }

        /// <summary>
        /// 计算各据点距国境的距离（跳数）。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:293-308：
        /// 有敌邻的据点为 0，其余据点取"到任一 0 号据点的最短跳数"。
        /// 原版用 据点距离表 的预计算距离表，这里用据点邻接图 BFS，
        /// 语义一致（都是图上最短路径），差异是原版距离表含地形代价，本阶段用跳数近似。
        /// </summary>
        /// <param name="ownOutpost">本方据点列表</param>
        /// <param name="force">当前势力</param>
        static void ComputeDistanceFromBorder(List<City> ownOutpost, Force force)
        {
            Queue<City> queue = new Queue<City>();
            for (int i = 0; i < ownOutpost.Count; i++)
            {
                City city = ownOutpost[i];
                OutpostInfo info = OutpostRuntime.Get(city.Id);
                if (info.neighborEnemyOutpostCount > 0)
                {
                    info.distanceFromBorder = 0;
                    queue.Enqueue(city);
                }
            }

            // 说明：原版只对"有敌邻的据点"做 distance=0，且默认 distance 是 int.MaxValue，
            // 因此完全内陆的势力（没有任何邻接敌人）所有据点都保持 MaxValue → 全部判为 Rear。
            while (queue.Count > 0)
            {
                City current = queue.Dequeue();
                OutpostInfo curInfo = OutpostRuntime.Get(current.Id);
                if (current.NeighborList == null)
                    continue;
                for (int i = 0; i < current.NeighborList.Count; i++)
                {
                    City nb = current.NeighborList[i];
                    if (nb == null || !nb.IsAlive)
                        continue;
                    if (nb.BelongForce != force)
                        continue;
                    OutpostInfo nbInfo = OutpostRuntime.Get(nb.Id);
                    int next = curInfo.distanceFromBorder + 1;
                    if (next < nbInfo.distanceFromBorder)
                    {
                        nbInfo.distanceFromBorder = next;
                        queue.Enqueue(nb);
                    }
                }
            }
        }

        /// <summary>
        /// 找出部队当前所在地格归属的据点（原版 <c>hex.get_area_building_id(false)</c>）。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>归属据点，找不到返回 null</returns>
        static City FindOutpostOfTroop(Troop troop, Scenario scenario)
        {
            if (troop == null || troop.cell == null)
                return null;

            // 站在据点格上：直接归该据点
            if (troop.cell.building is City)
                return troop.cell.building as City;

            // 辖区归属：对应原版 hex.get_area_building_id(false)（s11_scen_map_hex.cpp:86-89），
            // Cell.BelongCity 就是那份预计算表。注意这里**不归并到母城** ——
            // 原版传的是 effective=false，关隘 / 港口辖区的部队就归该关隘。
            return troop.cell.BelongCity;
        }

        /// <summary>
        /// 两个据点之间的距离（同城为 0）。用于守将调派的就近原则。
        /// </summary>
        /// <param name="a">据点 A</param>
        /// <param name="b">据点 B</param>
        /// <returns>距离</returns>
        static int GetCityDistance(City a, City b)
        {
            if (a == null || b == null)
                return int.MaxValue;
            if (a == b)
                return 0;
            if (a.CenterCell == null || b.CenterCell == null || Scenario.Cur?.Map == null)
                return int.MaxValue;
            return Scenario.Cur.Map.Distance(a.CenterCell, b.CenterCell);
        }
    }
}
