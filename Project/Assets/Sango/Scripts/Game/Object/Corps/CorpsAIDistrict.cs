/*
 * 文件名：CorpsAIDistrict.cs
 * 描述：军团级 AI —— 行动力分配与据点轮转调度（CorpsAI 的 partial 分支）。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 军团主流程   —— s11_game_ai_district.cpp:5-41   district_ai
 *   · 军团数据装载 —— s11_game_ai_district.cpp:43-64  load_district_data
 *   · 军团数据更新 —— s11_game_ai_district.cpp:66-133 update_district_data
 *   · 军团命令调度 —— s11_game_ai_district_cmd.cpp:5-54 district_command
 *
 * 关键概念（与旧实现的差别）：
 *   旧版 CorpsAI 只是"遍历自己的城，逐个调 DoAI"，没有任何预算概念，
 *   于是每座城都会把当回合所有能做的事全做一遍。
 *   原版是"军团有一笔行动力预算 → 按据点优先级切成若干份 → 逐据点花到花完为止"，
 *   据点之间是**争抢**关系，这才是两家 AI 行为差异的根源。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public partial class CorpsAI
    {
        /// <summary>
        /// 军团 AI 第一段：装载军团数据并分配本回合的行动力预算。
        ///
        /// 对齐出处：s11_game_ai_district.cpp:43-64 load_district_data
        /// <code>
        /// district->set_action_point(std::max(n, city_count * 2 * 20));   // 仅对非玩家军团
        /// </code>
        /// 注意：原版这里对**玩家**军团不做兜底，玩家军团的行动力来自 AddActionPoint 的自然增长；
        ///       AI 军团的行动力则每回合被强行拉到 city_count*40 —— 这是"AI 不会因为没钱而瘫掉"的保险。
        /// </summary>
        /// <param name="corps">当前军团</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIDistrictPrepare(Corps corps, Scenario scenario)
        {
            if (corps == null || scenario == null || corps.BelongForce == null)
                return true;

            OutpostRuntime.corps = corps;

            int cityCount = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == corps)
                    cityCount++;
            }

            // AI 军团行动力兜底：对齐 s11_game_ai_district.cpp:52-62
            if (!corps.IsPlayer)
            {
                int n = corps.ActionPoint;
                corps.ActionPoint = System.Math.Max(n, cityCount * 2 * 20);
            }
            return true;
        }

        /// <summary>
        /// 军团 AI 第二段：按据点优先级切分行动力，并生成据点执行顺序。
        ///
        /// 对齐出处：s11_game_ai_district.cpp:66-133 update_district_data。
        /// 优先级算法（原版原样）：
        ///   n = 状态值(0~3)；都市再 +1；收支为负 +1；人数超过最小需求 +1；治安 &lt; 90 +1。
        /// 行动力切分：按 idle_person_count 占比，向下取整到 20 的倍数；
        ///   都市只要有人就至少给 20；分配完即停（剩余 &lt;= 0 时中断）。
        /// </summary>
        /// <param name="corps">当前军团</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="outpostList">输出：按优先级降序排列的据点列表</param>
        /// <returns>是否处理完成</returns>
        public static bool UpdateDistrictData(Corps corps, Scenario scenario, List<City> outpostList)
        {
            outpostList.Clear();
            if (corps == null || scenario == null)
                return true;

            List<City> all = new List<City>();
            int totalIdle = 0;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive)
                    continue;
                if (city.BelongCorps != corps)
                    continue;

                OutpostInfo info = OutpostRuntime.Find(city.Id);
                if (info == null)
                    continue;

                // 优先级累加：对齐 s11_game_ai_district.cpp:80-93
                int n = (int)info.state;
                if (city.IsCity())
                {
                    n++;
                    if (info.goldIncome < 0 || info.foodIncome < 0)
                        n++;
                    if (info.personCount > info.minPersonCount)
                        n++;
                    // 原版 city->get_public_order() 对应本项目 City.security（治安）
                    if (city.security < 90)
                        n++;
                }
                totalIdle += info.idlePersonCount;
                info.priority = System.Math.Clamp(n, 0, 16);
                all.Add(city);
            }

            if (all.Count == 0)
                return true;

            // 排序：优先级降序，同优先级按 Id 升序 —— s11_game_ai_district.cpp:99-105
            all.Sort((a, b) =>
            {
                OutpostInfo ai = OutpostRuntime.Get(a.Id);
                OutpostInfo bi = OutpostRuntime.Get(b.Id);
                if (ai.priority != bi.priority)
                    return bi.priority.CompareTo(ai.priority);
                return a.Id.CompareTo(b.Id);
            });

            // 如果既没有据点也没有可下令武将，原版直接不切分行动力，
            // 但仍然把据点放进列表（行动力保持 0）。
            if (totalIdle <= 0)
            {
                outpostList.AddRange(all);
                return true;
            }

            int remaining = corps.ActionPoint;
            const int unit = 20;
            int maxDistrictActionPoint = scenario.Variables.ActionPointLimit;

            for (int i = 0; i < all.Count; i++)
            {
                City city = all[i];
                OutpostInfo info = OutpostRuntime.Get(city.Id);

                // 按可下令人数占比切分 —— s11_game_ai_district.cpp:114-115
                int n = corps.ActionPoint * info.idlePersonCount / totalIdle;
                n = n / unit * unit;

                // 都市只要有人就至少 20 —— s11_game_ai_district.cpp:122-124
                if (city.IsCity() && info.idlePersonCount > 0 && n < unit)
                    n = unit;

                n = System.Math.Clamp(n, 0, maxDistrictActionPoint);
                info.actionPoint = n;
                remaining -= n;

                outpostList.Add(city);
                if (remaining <= 0)
                    break;
            }

            return true;
        }

        /// <summary>
        /// 军团 AI 第三段：按顺序让每个据点花掉自己的行动力配额。
        ///
        /// 对齐出处：s11_game_ai_district_cmd.cpp:5-54 district_command。
        /// <code>
        /// while (据点列表.size()) {
        ///     building = front(); erase front;
        ///     n = district->action_point - Σ(尚未执行的据点的 action_point);
        ///     action_point_ = min(max(n, outpost[building].action_point), district->action_point);
        ///     据点主流程(building);
        ///     if (not district->action_point) break;
        /// }
        /// </code>
        /// 这段的语义：**先跑的据点可以吃掉后面据点的配额**（因为 n 会随着剩余据点减少而变大），
        /// 所以排序（优先级）决定了谁能抢到预算。
        /// </summary>
        /// <param name="corps">当前军团</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        public static bool AIDistrictCommand(Corps corps, Scenario scenario)
        {
            if (corps == null || scenario == null || corps.BelongForce == null)
                return true;

            // 兜底采集：玩家势力的 Force.DoAI 不会执行（Force.Run 里 `if (!IsPlayer)`），
            // 但玩家的下属军团仍要走原版调度，因此这里补一次据点态势采集。
            if (OutpostRuntime.force != corps.BelongForce)
                ForceAI.AIPrepareForceData(corps.BelongForce, scenario);

            List<City> outpostList = tempOutpostList;
            UpdateDistrictData(corps, scenario, outpostList);

            OutpostRuntime.corps = corps;

            // ---- 玩家子军团专属两段：对齐 s11_game_ai_district_cmd.cpp:10-30 ----
            // 原版只对"玩家势力且非 1 号军团"执行：先把军团预算用于派人补空城、赏赐忠诚低的武将，
            // 剩下的才是各据点的经济预算。AI 势力的这两件事由据点级 Move / Bonus 命令承担。
            if (corps.BelongForce.IsPlayer && corps.number != 1)
            {
                // 先按方针算出"出击据点 / 运输源 / 守将" —— 对齐 s11_game_ai_district.cpp:135-225
                UpdatePlayerSubDistrictData(corps, scenario);

                OutpostRuntime.actionPoint = corps.ActionPoint;
                DistrictMove(corps, scenario);

                // 剩余额度 = 军团预算 - 尚未执行的据点配额之和 —— :18-24
                int remain = corps.ActionPoint;
                for (int i = 0; i < outpostList.Count; i++)
                {
                    City rest = outpostList[i];
                    if (rest == null || !rest.IsAlive)
                        continue;
                    remain -= OutpostRuntime.Get(rest.Id).actionPoint;
                }
                OutpostRuntime.actionPoint = System.Math.Min(remain, corps.ActionPoint);
                DistrictBonus(corps, scenario);
            }

            if (AIConfig.Instance.logOutpostAI)
            {
                Sango.Log.Info($"*[军团AI]{corps.BelongForce.Name} 军团{corps.Index} 行动力{corps.ActionPoint} 据点{outpostList.Count}");
                for (int i = 0; i < outpostList.Count; i++)
                {
                    City c = outpostList[i];
                    if (c == null)
                        continue;
                    OutpostInfo ci = OutpostRuntime.Get(c.Id);
                    Sango.Log.Info($"*[军团AI]  {c.Name} 状态{ci.state} 优先级{ci.priority} " +
                        $"分得行动力{ci.actionPoint} 可下令人{ci.idlePersonCount} 人数{ci.personCount}");
                }
            }

            int index = 0;
            while (index < outpostList.Count)
            {
                City city = outpostList[index];
                index++;

                if (city == null || !city.IsAlive || city.BelongCorps != corps)
                    continue;

                OutpostInfo info = OutpostRuntime.Get(city.Id);

                // 剩余行动力 = 军团总预算 - 尚未执行的据点的配额之和
                int n = corps.ActionPoint;
                for (int i = index; i < outpostList.Count; i++)
                {
                    City rest = outpostList[i];
                    if (rest == null || !rest.IsAlive)
                        continue;
                    n -= OutpostRuntime.Get(rest.Id).actionPoint;
                }

                // 注意：这里不能用 clamp 统一处理。outpost.action_point 可能大于军团总预算，
                // 原版显式分两步手写（s11_game_ai_district_cmd.cpp:43-44）。
                OutpostRuntime.actionPoint = System.Math.Min(System.Math.Max(n, info.actionPoint), corps.ActionPoint);

                if (!city.DoOutpostAI(scenario))
                {
                    // 中断退出也要清临时列表 —— OutpostState 的退出约定：任何出口不残留引用
                    outpostList.Clear();
                    return false;
                }

                if (corps.ActionPoint <= 0)
                    break;
            }

            // 本轮跑完，清掉临时列表，避免残留引用
            outpostList.Clear();
            return true;
        }

        /// <summary>
        /// 玩家子军团的方针落地：算出"出击据点 / 运输源"。
        ///
        /// 对齐出处：s11_game_ai_district.cpp:135-225 update_player_sub_district_data。
        /// 三段：
        ///   ① 都市攻略（appoint = OccupyCity）→ 辖下离目标最近的都市 = 出击据点
        ///   ② 开启输送（appointSetting[Transport] = 1）→ 辖下有富余资源且够 18 回合的据点 = 运输源
        ///   ③ 每个辖下都市指定守将
        /// <para>
        /// 【③ 的落点】守将本来就由势力级 `ForceAIOutpost.SetGuard` 统一指派
        /// （写入 `Force.guardPersonIds`），子军团层再设一遍会互相覆盖，
        /// 因此这里不重复实现，只在日志里说明。
        /// </para>
        /// <para>
        /// 【② 的富余量】直接复用 <see cref="CityAI.GetExtraResource"/> ——
        /// 与据点级运输命令同一份口径（兵 / 粮 / 金 / 9 类兵装），
        /// 发车阈值按原版 :189-201 的十项判定。
        /// </para>
        /// </summary>
        /// <param name="corps">军团</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        static bool UpdatePlayerSubDistrictData(Corps corps, Scenario scenario)
        {
            if (corps == null || scenario == null)
                return true;
            if (corps.BelongForce == null || !corps.BelongForce.IsPlayer)
                return true;
            if (corps.number == 1)
                return true;

            corps.districtTargetCityId = -1;
            corps.districtTransportSrcId = -1;

            List<City> cities = new List<City>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city != null && city.IsAlive && city.BelongCorps == corps)
                    cities.Add(city);
            }
            if (cities.Count == 0)
                return true;

            // ① 都市攻略：辖下离目标最近的都市 —— :143-159
            if (corps.appoint == (int)Corps.AppointType.OccupyCity && corps.appoint_target > 0)
            {
                City target = scenario.citySet.Get(corps.appoint_target);
                if (target != null && target.IsAlive)
                {
                    int minDistance = int.MaxValue;
                    for (int i = 0; i < cities.Count; i++)
                    {
                        City city = cities[i];
                        if (!city.IsCity())
                            continue;
                        int distance = city.Distance(target);
                        if (distance < 0)
                            continue;
                        if (distance <= minDistance)
                        {
                            minDistance = distance;
                            corps.districtTargetCityId = city.Id;
                        }
                    }
                }
            }

            // ② 物资输送：辖下有富余资源的据点 —— :160-209
            if (corps.GetAppointValue(Corps.AppointContentType.Transport) == 1)
            {
                // 运输终点的三级回退统一由 Corps.GetTransportDestCityId 提供
                // （指定据点 → 目标军团都督据点 → 中期目标城），
                // 与据点级 AITransport 的玩家运输路径共用同一份口径。
                int destId = corps.GetTransportDestCityId(scenario);
                City dest = destId > 0 ? scenario.citySet.Get(destId) : null;
                if (dest != null && dest.IsAlive && dest.BelongForce == corps.BelongForce)
                {
                    for (int i = 0; i < cities.Count; i++)
                    {
                        City city = cities[i];
                        if (city == dest)
                            continue;

                        OutpostInfo info = OutpostRuntime.Find(city.Id);
                        if (info != null && info.state == OutpostState.Battle)
                            continue;
                        if (city.troops <= 0 || city.food <= 0)
                            continue;
                        // 内政未达标 → 不往外运 —— :176
                        if (city.IsCity() && !CityFacilityRequirement.MeetsMinGovFacilityCount(city))
                            continue;

                        int turnCount = info != null && info.state == OutpostState.Front ? 20 : 10;
                        if (city.food < ForceAI.CalcExpectedFoodUse(city.troops, turnCount))
                            continue;
                        // 18 回合冷却 —— :181
                        if (GetTurnsSinceLastTransport(city, scenario) < 18)
                            continue;

                        // 富余量：复用据点命令的精确口径 —— :187-188
                        int extraTroops, extraFood, extraGold;
                        int[] extraEquipment;
                        if (!CityAI.GetExtraResource(city, out extraTroops, out extraFood,
                                out extraGold, out extraEquipment))
                            continue;

                        // 发车阈值 —— :189-201（兵 / 粮 / 金 / 四项基础兵装 / 器械 / 船只）
                        if (extraTroops >= 10000
                            || extraFood >= 50000
                            || extraGold >= 5000
                            || GetEquipment(extraEquipment, ItemStoreKindType.Spear) >= 10000
                            || GetEquipment(extraEquipment, ItemStoreKindType.Halberd) >= 10000
                            || GetEquipment(extraEquipment, ItemStoreKindType.Crossbow) >= 10000
                            || GetEquipment(extraEquipment, ItemStoreKindType.Horse) >= 10000
                            || GetEquipment(extraEquipment, ItemStoreKindType.Helepolis) >= 1
                            || GetEquipment(extraEquipment, ItemStoreKindType.Catapult) >= 1
                            || GetEquipment(extraEquipment, ItemStoreKindType.Boat) >= 1)
                        {
                            corps.districtTransportSrcId = city.Id;
                            break;
                        }
                    }
                }
            }

            if (AIConfig.Instance.logOutpostAI && corps.districtTargetCityId > 0)
                Sango.Log.Info($"*[军团AI]{corps.BelongForce.Name} 军团{corps.Index} 出击据点 → {scenario.citySet.Get(corps.districtTargetCityId)?.Name}");
            return true;
        }

        /// <summary>
        /// 距上次从该据点发出运输队的回合数。
        /// 数据源：<c>Force.lastTransportTurn</c>（18 回合冷却的持久记录）。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>回合数；从未发过返回 int.MaxValue</returns>
        static int GetTurnsSinceLastTransport(City city, Scenario scenario)
        {
            if (city == null || city.BelongForce == null || city.BelongForce.lastTransportTurn == null)
                return int.MaxValue;
            if (!city.BelongForce.lastTransportTurn.TryGetValue(city.Id, out int turn))
                return int.MaxValue;
            int now = scenario != null ? scenario.TurnCount : 0;
            return System.Math.Max(now - turn, 0);
        }

        /// <summary>
        /// 从兵装富余数组里按 kind 取值（越界返回 0）。
        /// </summary>
        /// <param name="equipmentQuantity">GetExtraResource 的输出</param>
        /// <param name="kind">兵装种类</param>
        /// <returns>可运数量</returns>
        static int GetEquipment(int[] equipmentQuantity, ItemStoreKindType kind)
        {
            if (equipmentQuantity == null)
                return 0;
            int index = (int)kind;
            return index >= 0 && index < equipmentQuantity.Length ? equipmentQuantity[index] : 0;
        }

        /// <summary>
        /// 军团级派人：把人多据点的富余武将调去没人驻守的据点。
        ///
        /// 对齐出处：s11_game_ai_district_cmd.cpp:56-112 district_move。
        /// 只服务玩家子军团（AI 势力的同类动作由据点级 Move 命令承担）。
        /// </summary>
        /// <param name="corps">军团</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        static bool DistrictMove(Corps corps, Scenario scenario)
        {
            int cost = JobType.GetJobCostAP((int)CityJobType.TransformPerson);
            // 行动力不足 —— :60-61
            if (OutpostRuntime.actionPoint < cost)
                return true;

            List<City> srcList = new List<City>();
            List<City> destList = new List<City>();

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongCorps != corps)
                    continue;
                OutpostInfo info = OutpostRuntime.Get(city.Id);

                // 没人 → 待补 —— :69-73
                if (info.personCount <= 0)
                {
                    destList.Add(city);
                    continue;
                }
                // 有 2 人以上、且都不忙、且近处无敌人 → 可作为输出方 —— :74-75
                if (!info.existEnemy3 && info.personCount >= 2 && info.idlePersonCount >= 2)
                    srcList.Add(city);
            }

            if (srcList.Count == 0 || destList.Count == 0)
                return true;

            for (int d = 0; d < destList.Count; d++)
            {
                City dest = destList[d];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (OutpostRuntime.actionPoint < cost)
                    break;

                // 取人最多的输出方 —— :83-85
                City src = null;
                int bestCount = -1;
                for (int s = 0; s < srcList.Count; s++)
                {
                    City candidate = srcList[s];
                    if (candidate == null || !candidate.IsAlive)
                        continue;
                    OutpostInfo si = OutpostRuntime.Get(candidate.Id);
                    if (si.personCount > bestCount)
                    {
                        bestCount = si.personCount;
                        src = candidate;
                    }
                }
                if (src == null)
                    break;

                // 从该据点挑一个空闲武将 —— :88-91
                Person person = null;
                for (int p = 0; p < src.freePersons.Count; p++)
                {
                    Person candidate = src.freePersons[p];
                    if (candidate == null || !candidate.IsAlive || !candidate.IsFree)
                        continue;
                    person = candidate;
                    break;
                }
                if (person == null)
                {
                    srcList.Remove(src);
                    continue;
                }

                // 走"有行程的调动"（与原版 district_move 共用同一个 move 命令）：
                // 距离即天数，归零后由 Person.UpdateMission 落位。
                int days = src.Distance(dest);
                if (days <= 0)
                    days = 1;
                person.SetMission(MissionType.PersonTransform, dest, days);

                OutpostRuntime.actionPoint -= cost;
                OutpostRuntime.MarkExecuted();

                OutpostInfo srcInfo = OutpostRuntime.Get(src.Id);
                OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                srcInfo.personCount--;
                destInfo.personCount++;

                if (AIConfig.Instance.logOutpostAI)
                    Sango.Log.Info($"*[军团AI]{corps.BelongForce.Name} 军团{corps.Index} 派人 {src.Name} → {dest.Name}（{person.Name}）");

                // 输出方人数不足 2 → 移出候选 —— :101-106
                if (srcInfo.personCount < 2)
                {
                    srcList.Remove(src);
                    if (srcList.Count == 0)
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// 军团级赏赐：把忠诚偏低的武将按"最低优先"补赏。
        ///
        /// 对齐出处：s11_game_ai_district_cmd.cpp:114-160 district_bonus。
        /// 只服务玩家子军团。
        /// </summary>
        /// <param name="corps">军团</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否处理完成</returns>
        static bool DistrictBonus(Corps corps, Scenario scenario)
        {
            int apCost = JobType.GetJobCostAP((int)CityJobType.Reward);
            int goldCost = JobType.GetJobCost((int)CityJobType.Reward);

            int maxCount = 32;
            if (apCost > 0)
            {
                maxCount = OutpostRuntime.actionPoint / apCost;
                if (maxCount <= 0)
                    return true;
            }

            // 收集"忠诚未满、且所在城有钱赏"的武将 —— :126-139
            List<Person> personList = new List<Person>();
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive)
                    continue;
                if (person.BelongForce != corps.BelongForce)
                    continue;
                if (person.BelongCorps != corps)
                    continue;

                City service = person.BelongCity ?? person.CurrentCity;
                if (service == null || service.gold < goldCost)
                    continue;

                // 太守以上要求更高忠诚 —— :134-135
                int loyaltyLine = IsLeaderOrHigher(person) ? 96 : 90;
                if (person.loyalty >= loyaltyLine)
                    continue;

                personList.Add(person);
            }

            maxCount = System.Math.Min(maxCount, personList.Count);
            for (int i = 0; i < maxCount; i++)
            {
                if (personList.Count == 0)
                    break;

                // 忠诚最低的先赏 —— :143-145
                Person pick = null;
                for (int j = 0; j < personList.Count; j++)
                {
                    if (pick == null || personList[j].loyalty < pick.loyalty)
                        pick = personList[j];
                }
                if (pick == null)
                    break;

                City service = pick.BelongCity ?? pick.CurrentCity;
                if (service != null && service.gold >= goldCost)
                {
                    bool ok = service.JobRewardPerson(pick);
                    if (!ok)
                        break;
                    OutpostRuntime.actionPoint -= apCost;
                    OutpostRuntime.MarkExecuted();
                }
                personList.Remove(pick);
            }
            return true;
        }

        /// <summary>
        /// 是否为太守及以上身份（君主 / 都督 / 太守）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>是否</returns>
        static bool IsLeaderOrHigher(Person person)
        {
            if (person == null)
                return false;
            return person.state == (int)PersonStateType.Governor
                || person.state == (int)PersonStateType.Commander
                || person.state == (int)PersonStateType.Leader;
        }

        /// <summary>
        /// 军团内的据点调度临时列表（复用，避免每回合分配）。
        /// </summary>
        static readonly List<City> tempOutpostList = new List<City>();
    }
}
