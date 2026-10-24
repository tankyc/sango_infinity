/*
 * 文件名：ForceAICommand.cs
 * 描述：势力级 AI 命令 —— force_command 的九步顺序（ForceAI 的 partial 分支）。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 命令顺序总表 —— s11_game_ai_force_cmd.cpp:127-201  force_command
 *   · 流放俘虏     —— s11_game_ai_force_cmd.cpp:203-256  force_banish
 *   · 封官         —— s11_game_ai_force_cmd.cpp:258-274  force_ranks
 *   · 军师任命     —— s11_game_ai_force_cmd.cpp:276-304  force_strategist
 *   · 军团重编     —— s11_game_ai_force_cmd.cpp:306-354  force_districts
 *   · 人事（召唤守将 + 授宝）—— s11_game_ai_force_cmd.cpp:356-461
 *
 * 与 Force.cs 的 AIPrepare 对照表（原版顺序 → 本项目落点 → 状态）：
 *   ① force_banish          → AIForceBanish      ✅
 *   ② force_ranks           → AIForceRanks       ✅
 *   ③ force_strategist      → AIForceStrategist  ✅
 *   ④ force_districts       → AIForceDistricts   ✅
 *   ⑤ force_personnel       → 部分（守将召唤不适用本项目）
 *   ⑥ force_diplomacy       → ForceAI.AIDiplomacy ✅（已存在）
 *   ⑦ force_research_tech   → ForceAI.AITechniques ✅（已存在）
 *   ⑧ force_research_ability→ AIForceResearchAbility ✅（Ability 表已建，走 AbilityStudySystem）
 *   ⑨ force_learn           → AIForceLearn       ✅（三段：基础能力/兵种适性/特技）
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public partial class ForceAI
    {
        /// <summary>
        /// 流放俘虏：把"关久了、没价值、也劝不动"的俘虏放回去。
        ///
        /// 对齐出处：s11_game_ai_force_cmd.cpp:203-256 force_banish。
        /// 判定链（每座据点每回合最多放一个）：
        ///   ① 50% 概率才处理
        ///   ② 本据点金收支为正 → 关押 &gt;10 回合、且 rand(timer*4, 上限50) 命中
        ///      收支为负 → 关押 &gt;4 回合、且 rand(timer*5) 命中
        ///   ③ 四项能力任一 ≥80 的（有才）不流放
        ///   ④ 君主能说服（劝降概率 ≥10%）的不流放
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceBanish(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            Person governor = force.mGovernor;
            if (governor == null)
                return true;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                if (city.captiveList == null || city.captiveList.Count == 0)
                    continue;

                OutpostInfo info = OutpostRuntime.Find(city.Id);
                int goldIncome = info != null ? info.goldIncome : 0;

                for (int p = 0; p < city.captiveList.Count; p++)
                {
                    Person captive = city.captiveList[p];
                    if (captive == null || !captive.IsAlive || !captive.IsPrisoner)
                        continue;
                    // 有任务在身（正被押送 / 被审讯）的不处理 —— :219
                    if (captive.missionType != (int)MissionType.None)
                        continue;
                    if (!GameRandom.Chance(50))
                        continue;

                    // 关押时长门槛 —— :221-234
                    //   收支为正 → 关押 >10 月，且 rand(min(timer*4, 50)) 命中
                    //   收支为负 → 关押 >4 月，且 rand(timer*5) 命中
                    int prisonTimer = captive.prisonTimer;
                    if (goldIncome > 0)
                    {
                        if (prisonTimer <= 10)
                            continue;
                        if (!GameRandom.Chance(System.Math.Min(prisonTimer * 4, 50)))
                            continue;
                    }
                    else
                    {
                        if (prisonTimer <= 4)
                            continue;
                        if (!GameRandom.Chance(prisonTimer * 5))
                            continue;
                    }

                    // 有才的不放 —— :235-245
                    if (IsTalentPerson(captive))
                        continue;

                    // 劝得动的不放（留着招降）—— :246-248
                    if (GetPersuasionChance(captive, governor) >= 10)
                        continue;

                    // 释放：与原版 banish 同语义（离城、回到原势力或下野）
                    PersonStateSystem.Release(captive, city, PersonChangeReason.Released);
                    Sango.Log.Info($"*[势力AI]{force.Name} 流放俘虏 {captive.Name}");
                    // 每座据点每回合只放一个 —— :250-251
                    break;
                }
            }
            return true;
        }

        /// <summary>
        /// 封官：每 3 回合错峰调整一次官职。
        ///
        /// 对齐出处：s11_game_ai_force_cmd.cpp:258-274 force_ranks。
        /// <code>
        /// if (turn &lt;= 1 or turn % 3 == force_id % 3) { auto_ranks(...); }
        /// </code>
        /// 本项目实际的官职分配在 <see cref="AISetOfficial"/>，这里只补"错峰"这一层节奏，
        /// 避免每回合重排官职导致同一名武将的官职反复抖动。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceRanks(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            int turn = scenario.TurnCount;
            if (turn <= 1 || turn % 3 == force.Id % 3)
                return AISetOfficial(force, scenario);
            return true;
        }

        /// <summary>
        /// 军师任命：全势力智力最高者担任军师（需超过现任且 &gt;69）。
        ///
        /// 对齐出处：s11_game_ai_force_cmd.cpp:276-304 force_strategist。
        /// <code>
        /// best = 69; if (old_gunshi alive) best = old.intelligence;
        /// 遍历本势力非君主非俘虏者，找 intelligence &gt; best 的；
        /// 找到就 commander_-&gt;strategist(旧, 新)。
        /// </code>
        /// <para>
        /// 【为什么必须有】军师智力直接决定军团每回合的行动力倍率
        /// （见 Corps.AddActionPoint 的 counsellorFactor），没有这一步，AI 势力的
        /// 行动力永远按"无军师 = 1.0"算，比玩家少一截。
        /// </para>
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceStrategist(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            Person oldCounsellor = force.mCounsellor;
            int bestStat = 69;
            if (oldCounsellor != null && oldCounsellor.IsAlive && oldCounsellor.BelongForce == force)
                bestStat = oldCounsellor.Intelligence;

            Person newCounsellor = null;
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive)
                    continue;
                if (person.BelongForce != force)
                    continue;
                // 君主不兼任军师 —— :286
                if (person == force.mGovernor)
                    continue;
                // 俘虏不任军师 —— :286
                if (person.state == (int)PersonStateType.Prisoner)
                    continue;

                if (person.Intelligence > bestStat)
                {
                    bestStat = person.Intelligence;
                    newCounsellor = person;
                }
            }

            if (newCounsellor == null)
                return true;

            // 对齐原版 force_strategist :295-302 走 commander_->strategist 委任管线，
            // 即复用 Force.ChangeCounsellor（内部同步持久化 Id + 广播 OnForceChangeCounsellor 事件，
            // 事件订阅者 ScenarioEventManager 依赖它触发 ForceChangeCounsellor 类剧情）
            force.ChangeCounsellor(newCounsellor);

            Sango.Log.Info($"*[势力AI]{force.Name} 任命 {newCounsellor.Name} 为军师（智力{newCounsellor.Intelligence}）");
            return true;
        }

        /// <summary>
        /// 军团重编：把过大的军团拆成两个。
        ///
        /// 对齐出处：s11_game_ai_force_cmd.cpp:306-354 force_districts，
        /// 配套 s11_game_ai_district_sep.cpp:5-206
        /// （district_sep_init / district_sep_need / district_sep_num / district_create）。
        /// 流程：
        ///   ① 统计各军团的城市数与"邻接敌据点数"
        ///   ② 需要拆分（或到了定期重排的回合）→ 先解散所有 2 号以后的军团
        ///   ③ 逐个重建：每次挑"城数最少"的军团号，把它的城拆一半出去
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceDistricts(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;
            if (!AIConfig.Instance.useAutoCorpsSplit)
                return true;

            Person governor = force.mGovernor;
            if (governor == null)
                return true;
            City governorCity = governor.BelongCity ?? governor.CurrentCity;
            // 君主不在都市（出阵中）→ 不重编 —— :312-314
            if (governorCity == null || !governorCity.IsCity())
                return true;

            int[] cityCount;
            int[] enemyNear;
            bool need = ComputeCorpsSeparation(force, scenario, out cityCount, out enemyNear);

            // 定期重排：开战两年后，每月轮询一个势力强制重排一次 —— :320
            // 原版：get_elapsed_years() > 1 and force_id == (get_elapsed_months() % get_max_force_count())
            // 其中 elapsed_years = turn/36、elapsed_months = turn/3（s11_scen.cpp:509-517，turn 即旬）；
            // 本项目无原版的固定 max_force_count，以存活势力数轮询作为近似口径
            bool scheduled = scenario.TurnCount / 36 > 1
                && force.Id == (scenario.TurnCount / 3) % System.Math.Max(CountAliveForces(scenario), 1);
            if (!need && !scheduled)
                return true;

            // 先解散所有支军团 —— :323-333
            List<Corps> corpsList = new List<Corps>();
            for (int i = 0; i < scenario.corpsSet.Count; i++)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps == null || !corps.IsAlive || corps.BelongForce != force)
                    continue;
                if (corps.number <= 1)
                    continue;
                corpsList.Add(corps);
            }
            for (int i = 0; i < corpsList.Count; i++)
                force.DeleteCorps(corpsList[i]);

            // 逐个数重建 —— :334-351
            ComputeCorpsSeparation(force, scenario, out cityCount, out enemyNear);
            for (int guard = 0; guard < MaxDistrictCount; guard++)
            {
                if (!IsCorpsSeparationNeeded(cityCount))
                    break;
                if (CountCorps(force, scenario) >= MaxDistrictCount)
                    break;

                int number = PickCorpsSeparationNumber(cityCount, enemyNear);
                if (number < 0)
                    break;
                if (cityCount[number] <= 2)
                    break;

                if (!CreateCorpsBySeparation(force, scenario, number))
                    break;

                // 重建后重新统计，避免同一轮里重复拆同一个军团
                if (!ComputeCorpsSeparation(force, scenario, out cityCount, out enemyNear))
                    break;
            }
            return true;
        }

        /// <summary>原版最大军团数 —— Force::MaxDistrictCount</summary>
        // 【对齐原版】Force::MaxDistrictCount = 8（s11_scen.h:693）。
        // 原先是 6，导致 AI 最多只拆到 6 个军团；而 `ComputeCorpsSeparation` 又用
        // `corps.number > MaxDistrictCount` 过滤统计，玩家建的第 7、8 军团城池会被漏算，
        // total 偏小 → 拆分判定偏保守、拆分点算错。
        const int MaxDistrictCount = 8;

        /// <summary>
        /// 统计各军团的城市数与邻接敌据点数。
        /// 对齐 s11_game_ai_district_sep.cpp:5-24 district_sep_init。
        /// </summary>
        /// <returns>是否存在任何军团</returns>
        static bool ComputeCorpsSeparation(Force force, Scenario scenario, out int[] cityCount, out int[] enemyNear)
        {
            cityCount = new int[MaxDistrictCount + 1];
            enemyNear = new int[MaxDistrictCount + 1];
            bool any = false;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                Corps corps = city.BelongCorps;
                if (corps == null || corps.number < 0 || corps.number > MaxDistrictCount)
                    continue;
                cityCount[corps.number]++;
                OutpostInfo info = OutpostRuntime.Find(city.Id);
                if (info != null)
                    enemyNear[corps.number] += info.neighborEnemyOutpostCount;
                any = true;
            }
            return any;
        }

        /// <summary>
        /// 是否需要拆分军团。
        /// 对齐 s11_game_ai_district_sep.cpp:26-40 district_sep_need。
        /// </summary>
        /// <param name="cityCount">各军团城数</param>
        /// <returns>是否需要</returns>
        static bool IsCorpsSeparationNeeded(int[] cityCount)
        {
            int total = 0;
            for (int i = 0; i <= MaxDistrictCount; i++)
                total += cityCount[i];

            int districtCount = System.Math.Min(total / 3, MaxDistrictCount);
            if (districtCount < 2)
                return false;

            int avg = total / districtCount;
            for (int i = 0; i <= MaxDistrictCount; i++)
            {
                if (cityCount[i] <= 0)
                    continue;
                if (cityCount[i] >= avg + 2)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 选出本轮要拆的军团号：城数最少，城数相同取邻接敌据点多者。
        /// 对齐 s11_game_ai_district_sep.cpp:42-57 district_sep_num。
        /// </summary>
        /// <param name="cityCount">各军团城数</param>
        /// <param name="enemyNear">各军团邻接敌据点数</param>
        /// <returns>军团号；没有可拆的返回 -1</returns>
        static int PickCorpsSeparationNumber(int[] cityCount, int[] enemyNear)
        {
            int result = -1;
            for (int j = 2; j <= MaxDistrictCount; j++)
            {
                if (cityCount[j] > 0)
                {
                    result = j;
                    break;
                }
            }
            if (result < 0)
                return -1;

            for (int j = 2; j <= MaxDistrictCount; j++)
            {
                if (cityCount[j] <= 0)
                    continue;
                if (result >= 0 && cityCount[j] < cityCount[result])
                    result = j;
                else if (result >= 0 && cityCount[j] == cityCount[result] && enemyNear[j] > enemyNear[result])
                    result = j;
            }
            return result;
        }

        /// <summary>
        /// 按拆分规则建立新军团。
        /// 对齐 s11_game_ai_district_sep.cpp:68-206 district_create。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="number">军团号</param>
        /// <returns>是否建成</returns>
        static bool CreateCorpsBySeparation(Force force, Scenario scenario, int number)
        {
            Person governor = force.mGovernor;
            if (governor == null)
                return false;
            City oldCenter = governor.BelongCity ?? governor.CurrentCity;
            if (oldCenter == null)
                return false;

            // 收集该军团的城市 —— :80-83
            List<City> cityList = new List<City>();
            List<City> source = new List<City>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                if (city.BelongCorps == null || city.BelongCorps.number != number)
                    continue;
                source.Add(city);
            }
            if (source.Count <= 2)
                return false;

            // 找离都督城最远的城作为新中心 —— :84-120
            City newCenter = null;
            int bestDistance = -1;
            int bestBorder = int.MaxValue;
            int bestEnemyNear = -1;
            for (int i = 0; i < source.Count; i++)
            {
                City city = source[i];
                if (city == oldCenter)
                    continue;
                int distance = GetCityDistancePublic(oldCenter, city);
                if (newCenter == null
                    || distance > bestDistance
                    || (distance == bestDistance && GetBorder(city) < bestBorder)
                    || (distance == bestDistance && GetBorder(city) == bestBorder && GetEnemyNear(city) > bestEnemyNear))
                {
                    newCenter = city;
                    bestDistance = distance;
                    bestBorder = GetBorder(city);
                    bestEnemyNear = GetEnemyNear(city);
                }
            }
            if (newCenter == null)
                return false;

            // 划走约一半的城 —— :123-158
            List<City> newCityList = new List<City> { newCenter };
            source.Remove(newCenter);
            source.Remove(oldCenter);
            int count = source.Count - source.Count / 2;

            source.Sort((a, b) =>
            {
                int da = GetCityDistancePublic(a, newCenter);
                int db = GetCityDistancePublic(b, newCenter);
                if (da != db)
                    return da.CompareTo(db);
                return a.Id.CompareTo(b.Id);
            });

            for (int i = 0; i < source.Count && count > 0; i++)
            {
                City city = source[i];
                bool newNeighbor = IsNeighborCity(newCenter, city);
                bool oldNeighbor = IsNeighborCity(oldCenter, city);
                if (newNeighbor == oldNeighbor)
                {
                    if (GetCityDistancePublic(newCenter, city) > GetCityDistancePublic(oldCenter, city))
                        continue;
                }
                else if (!newNeighbor)
                {
                    continue;
                }
                newCityList.Add(city);
                count--;
            }

            // 新都督：功绩最高的太守 —— :159-173
            Person newGovernor = null;
            City actualCenter = null;
            for (int i = 0; i < newCityList.Count; i++)
            {
                City city = newCityList[i];
                if (city.Leader == null || !city.Leader.IsAlive)
                    continue;
                if (newGovernor == null || city.Leader.merit > newGovernor.merit)
                {
                    newGovernor = city.Leader;
                    actualCenter = city;
                }
            }
            if (newGovernor == null || actualCenter == null)
                return false;

            force.CreateCorps(number, newGovernor, newCityList);
            Sango.Log.Info($"*[势力AI]{force.Name} 新建军团{number}：都督 {newGovernor.Name}，城 {newCityList.Count} 座");
            return true;
        }

        /// <summary>取城距国境的距离（无态势数据时按 -1 处理）。</summary>
        static int GetBorder(City city)
        {
            OutpostInfo info = OutpostRuntime.Find(city.Id);
            return info != null ? info.distanceFromBorder : int.MaxValue;
        }

        /// <summary>取城邻接的敌据点数。</summary>
        static int GetEnemyNear(City city)
        {
            OutpostInfo info = OutpostRuntime.Find(city.Id);
            return info != null ? info.neighborEnemyOutpostCount : 0;
        }

        /// <summary>两城是否邻接。</summary>
        static bool IsNeighborCity(City a, City b)
        {
            if (a == null || b == null || a.NeighborList == null)
                return false;
            for (int i = 0; i < a.NeighborList.Count; i++)
            {
                if (a.NeighborList[i] == b)
                    return true;
            }
            return false;
        }

        /// <summary>两城之间的地格距离（供拆分算法使用）。</summary>
        static int GetCityDistancePublic(City a, City b)
        {
            if (a == null || b == null)
                return int.MaxValue;
            if (a == b)
                return 0;
            Scenario scenario = Scenario.Cur;
            if (a.CenterCell == null || b.CenterCell == null || scenario == null || scenario.Map == null)
                return int.MaxValue;
            return scenario.Map.Distance(a.CenterCell, b.CenterCell);
        }

        /// <summary>统计势力当前军团数。</summary>
        static int CountCorps(Force force, Scenario scenario)
        {
            int n = 0;
            for (int i = 0; i < scenario.corpsSet.Count; i++)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps != null && corps.IsAlive && corps.BelongForce == force)
                    n++;
            }
            return n;
        }

        /// <summary>
        /// 授宝：把国库里的珍品授予"性格喜爱它"的武将。
        ///
        /// 对齐出处：s11_game_ai_force_cmd.cpp:414-431 force_reward 与 :433-461 need_reward。
        /// 原版靠写死的 <c>ItemOwnerPreset</c>（武将-宝物对）决定"谁该拿什么"；
        /// 本项目改成读 <see cref="Personality.favoriteItemIds"/>（性格内嵌属性），
        /// 于是"关羽喜欢青龙偃月刀"只需给关羽那条性格数据填宝物 Id，无需改代码。
        /// 门槛照搬：回合 ≥36、每季度一次、君主在城内且空闲、势力技巧点足够。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceReward(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            // 开局 36 回合内不授宝 —— :416-417
            if (scenario.TurnCount < 36)
                return true;
            // 每季度一次（一年 36 回合 → 季度 9 回合）—— :418-419
            if (scenario.TurnCount % 9 != 0)
                return true;

            Person governor = force.mGovernor;
            if (governor == null || !governor.IsAlive)
                return true;
            // 君主在途 / 在部队中 / 有任务 → 跳过 —— :438
            if (!governor.IsFree)
                return true;

            // 国库可授的珍品 —— :440-451
            List<ItemType> items = new List<ItemType>();
            ItemGrantRules.GetGrantItemList(force, items);
            if (items.Count == 0)
                return true;

            for (int i = 0; i < items.Count; i++)
            {
                ItemType item = items[i];
                if (item == null)
                    continue;

                // 找"性格喜爱这件宝物、且还没有它"的本势力武将 —— :442-458
                for (int p = 0; p < scenario.personSet.Count; p++)
                {
                    Person person = scenario.personSet[p];
                    if (person == null || !person.IsAlive || person.BelongForce != force)
                        continue;
                    if (person.mPersonality == null || !person.mPersonality.IsFavoriteItem(item.Id))
                        continue;
                    // 已经持有同类道具 → 不重复授
                    if (person.itemStore != null && person.itemStore.GetNumber(item.storeKind) > 0)
                        continue;

                    City building = person.BelongCity ?? person.CurrentCity;
                    if (building == null)
                        continue;

                    string reason;
                    if (ItemGrantRules.Grant(building, person, item, out reason) == null)
                        continue;

                    Sango.Log.Info($"*[势力AI]{force.Name} 将 {item.Name} 授予 {person.Name}（性格喜爱）");
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// 能力研究（势力级）。
        ///
        /// 对齐出处：s11_game_ai_force_cmd.cpp:754-817 force_research_ability。
        /// 原版流程是"按势力四维总和排位 → 给候选 Ability 打分 → 开始研究某一条 Ability"，
        /// 研究耗时 timeCost 回合，完成后进 learnedAbilityIds，武将再逐个学会。
        /// 候选打分与推进推进都在 <see cref="AbilityStudySystem"/>（Ability 数据来自 Abilities.json）。
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceResearchAbility(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            // ① 先推进已在研究的那一条（耗时 timeCost 回合，归零进 learnedAbilityIds）
            if (AbilityStudySystem.AdvanceResearch(force, scenario))
                return true;
            // ② 空闲时挑一条开始研究（未掌握 + 前置满足 + 金与行动力够）
            AbilityStudySystem.StartResearch(force, scenario);
            return true;
        }

        /// <summary>
        /// 能力培育（势力级三段）。
        ///
        /// 对齐出处：s11_game_ai_force_cmd_learn.cpp:5-35 force_learn，三段顺序照搬：
        /// <c>force_learn_base_stat</c> → 兵种适性培育 → <c>force_learn_skill</c>。
        /// <para>
        /// 第三段（特技培育）的数据来自 Abilities.json 里 category = Skill 的条目，
        /// 由 <see cref="AbilityStudySystem.LearnSkill"/> 实现。
        /// 差异状态：✅ 三段均已对齐。
        /// </para>
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIForceLearn(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return true;

            // 第一段：基础能力（与原版一致，每回合最多一段成功）
            if (AbilityStudySystem.LearnBaseStat(force, scenario))
                return true;
            // 第二段：兵种适性
            if (AbilityStudySystem.LearnTroopAptitude(force, scenario))
                return true;
            // 第三段：特技（数据来自 Abilities.json 里 category = Skill 的条目）
            AbilityStudySystem.LearnSkill(force, scenario);
            return true;
        }

        /// <summary>
        /// 判断武将是否"有才"（四项能力任一 ≥80）。
        /// 对齐出处：s11_game_ai_force_cmd.cpp:235-245。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>是否有才</returns>
        static bool IsTalentPerson(Person person)
        {
            if (person == null)
                return false;
            if (person.Command >= 80) return true;
            if (person.Strength >= 80) return true;
            if (person.Intelligence >= 80) return true;
            if (person.Politics >= 80) return true;
            return false;
        }

        /// <summary>
        /// 劝降成功率（0~100）。
        ///
        /// 对齐出处：s11_sys_scen_person.cpp:2201-2234
        /// 的 <c>get_normal_persuasion_chance</c> / <c>get_persuasion_chance</c>（type = Touyou_Normal）。
        /// <code>
        /// loyalty = target.loyalty
        /// if (在野 或 已亡势力俘虏) loyalty = 难度==Easy ? 60 : 70
        /// n  = (target 到自家君主相性距 - target 到对方君主相性距) / 5
        /// n -= 5 * loyalty * (giri + 18) / 100
        /// n += max(actor.魅力, 30) * 3 / 5
        /// n -= target 亲爱自家君主 ? 15 : 0
        /// n -= target 与自家君主父子 ? 15 : 0
        /// n += target 厌恶自家君主 ? 15 : 0
        /// n += rand(0, Giri_Max - giri)          // 0..(4-giri)
        /// n += target 是俘虏 ? 15 : 0
        /// n += 45
        /// return max(n, 0)
        /// </code>
        /// 义理取值换算：原版 <c>Giri_*</c> 是 0..4，本项目 <c>Argumentation.kind</c> 是 1..5，故减 1。
        /// </summary>
        /// <param name="target">目标武将（俘虏 / 在野）</param>
        /// <param name="actor">执行劝降的武将（势力级授宝/流放判断里传君主）</param>
        /// <returns>成功率（0~155，未截断到 100，与原版一致）</returns>
        static int GetPersuasionChance(Person target, Person actor)
        {
            if (target == null || actor == null || !target.IsAlive || !actor.IsAlive)
                return 0;

            bool isWild = target.IsWild;
            // 已亡势力的俘虏按"在野"处理 —— :2208-2210
            bool ruinedPrisoner = target.IsPrisoner && target.BelongForce == null;

            int loyalty = target.loyalty;
            if (isWild || ruinedPrisoner)
            {
                int difficulty = Scenario.Cur != null ? Scenario.Cur.Variables.difficulty : 1;
                loyalty = difficulty == 0 ? 60 : 70;
            }

            Person actorKunshu = actor.BelongForce != null ? actor.BelongForce.mGovernor : null;
            Person targetKunshu = target.BelongForce != null ? target.BelongForce.mGovernor : null;

            // 相性距离：0..75，无君主时按 25 —— :2213-2214
            int targetKunshuAishou = targetKunshu != null ? target.CompatibilityDistance(targetKunshu) : 25;
            int actorKunshuAishou = actorKunshu != null ? target.CompatibilityDistance(actorKunshu) : 25;

            // 义理 0..4（原版 Giri_Worst..Giri_Best），在野按 Giri_Normal=2 —— :2215
            int giri = isWild
                ? 2
                : (target.mArgumentation != null ? target.mArgumentation.kind - 1 : 2);
            if (giri < 0) giri = 0;
            if (giri > 4) giri = 4;

            int n = (targetKunshuAishou - actorKunshuAishou) / 5;
            n -= 5 * loyalty * (giri + 18) / 100;
            n += System.Math.Max(actor.Glamour, 30) * 3 / 5;
            if (targetKunshu != null && target.IsLike(targetKunshu))
                n -= 15;
            if (targetKunshu != null && IsOyako(target, targetKunshu))
                n -= 15;
            if (targetKunshu != null && target.IsHate(targetKunshu))
                n += 15;
            // rand_int(Giri_Max - giri) → [0, 4-giri]
            n += GameRandom.Range(0, System.Math.Max(5 - giri, 1));
            if (target.IsPrisoner)
                n += 15;
            n += 45;

            return System.Math.Max(n, 0);
        }

        /// <summary>
        /// 是否为父子关系（任一方是另一方的父亲或母亲）。
        /// 对齐出处：Open311 <c>Person::is_oyako</c>。
        /// </summary>
        /// <param name="a">武将 A</param>
        /// <param name="b">武将 B</param>
        /// <returns>是否父子 / 母子</returns>
        static bool IsOyako(Person a, Person b)
        {
            if (a == null || b == null)
                return false;
            if (a.Father == b || a.Mother == b)
                return true;
            if (b.Father == a || b.Mother == a)
                return true;
            return false;
        }
    }
}
