/*
 * 文件名：CityAIOutpostCommand.cs
 * 描述：据点级 AI 的人员调动与物资运输命令（CityAI 的 partial 分支）。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 移动（Move）     —— s11_game_ai_kyoten_cmd_personnel.cpp:5-40   移动命令
 *                        s11_game_ai_kyoten_cmd_personnel.cpp:42-216  find_move_dest
 *                        s11_game_ai_kyoten_cmd_personnel.cpp:218-254 find_relocation_dest_city
 *                        s11_game_ai_kyoten.cpp:230-250               get_nearest_city_building
 *   · 召唤（Summon）   —— s11_game_ai_kyoten_cmd_personnel.cpp:256-369 召唤命令
 *   · 运输（Transport）—— s11_game_ai_kyoten_cmd_mil_transport.cpp:5-207  运输命令
 *                        s11_game_ai_kyoten_cmd_mil_transport.cpp:209-255 get_transport_dest
 *                        s11_game_ai_kyoten_cmd_mil_transport.cpp:257-386 get_transport_dest_building_list
 *                        s11_game_ai_kyoten.cpp:252-272               get_extra_resource
 *
 * 说明：这三个命令是本项目原先由"人才部署 / 资源调度"两套自建系统代劳的部分，
 *       现按原版搬回据点层。判定顺序、阈值、随机分支全部照搬原版。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public partial class CityAI
    {
        // ==================================================================================
        //  Move —— 据点间武将调动
        // ==================================================================================

        /// <summary>
        /// 据点级"移动"命令：把本据点的富余武将调往更需要人的据点。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:5-40 移动命令。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true（原版忽略返回值）</returns>
        public static bool AIMove(City city, Scenario scenario)
        {
            int cost = JobType.GetJobCostAP((int)CityJobType.TransformPerson);
            // 行动力不足 —— s11_game_ai_kyoten_cmd_personnel.cpp:10-11
            if (OutpostRuntime.actionPoint < cost)
                return true;

            City target;
            List<Person> actors;
            if (!FindMoveDest(city, scenario, out target, out actors))
                return true;
            // 无人可调 / 无目标据点 —— s11_game_ai_kyoten_cmd_personnel.cpp:17-22
            if (actors.Count == 0 || target == null)
                return true;

            // 行程天数 = 两城距离，对齐 exec_move_command 的
            // time_cost = get_city_distance(building, dest)（s11_sys_person_order_personnel.cpp:80）。
            int days = city.Distance(target);
            if (days <= 0)
                days = 1;

            int moved = 0;
            for (int i = 0; i < actors.Count; i++)
            {
                Person person = actors[i];
                if (person == null || !person.IsAlive || !person.IsFree)
                    continue;
                // 下达调动命令：武将成为"在途"，逐回合推进 missionCounter，
                // 归零后由 Person.UpdateMission 的 PersonTransform 分支落位
                // （对应原版 person_set_order_timer + on_move_timer_finished）。
                person.SetMission(MissionType.PersonTransform, target, days);
                OutpostRuntime.idlePersonList.Remove(person);
                moved++;
            }
            if (moved <= 0)
                return true;

            // 记账：行动力 + 双方据点人数 —— s11_game_ai_kyoten_cmd_personnel.cpp:31-33
            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.Get(city.Id).personCount -= moved;
            OutpostRuntime.Get(target.Id).personCount += moved;
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 调动 {moved} 人 → {target.Name}（剩余行动力{OutpostRuntime.actionPoint}）");
            return true;
        }

        /// <summary>
        /// 决定"往哪送人、送几个人"。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:42-216 find_move_dest。
        /// 判定顺序照搬：邻接据点 → 港关特殊分支 → 都市（回收港关资源 / 补缺人城）→ 港关（前线撤人）。
        /// </summary>
        /// <param name="src">出发据点</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="dest">输出：目标据点</param>
        /// <param name="actors">输出：被调动的武将</param>
        /// <returns>是否找到可执行的调动</returns>
        static bool FindMoveDest(City src, Scenario scenario, out City dest, out List<Person> actors)
        {
            dest = null;
            actors = new List<Person>();

            List<Person> idle = OutpostRuntime.idlePersonList;
            OutpostInfo srcInfo = OutpostRuntime.Get(src.Id);

            // 无可用武将 —— :49-50
            if (idle.Count == 0)
                return false;
            // 战斗中不调动 —— :52-53
            if (srcInfo.state == OutpostState.Battle)
                return false;

            int maxActorCount = System.Math.Min(srcInfo.personCount - srcInfo.minPersonCount, idle.Count);

            // 邻接己方据点 —— :59-70
            List<City> neighbors = new List<City>();
            if (src.NeighborList != null)
            {
                for (int i = 0; i < src.NeighborList.Count; i++)
                {
                    City nb = src.NeighborList[i];
                    if (nb == null || !nb.IsAlive)
                        continue;
                    if (nb.BelongForce != src.BelongForce)
                        continue;
                    neighbors.Add(nb);
                }
            }

            // 没有邻接据点 —— :73-99
            if (neighbors.Count == 0)
            {
                if (!src.IsCity())
                {
                    if (srcInfo.state == OutpostState.Front)
                    {
                        // 兵 / 粮已够本据点最低需求 → 不撤 —— :81-87
                        int personCount = System.Math.Min(srcInfo.personCount, srcInfo.minPersonCount);
                        int troops = personCount * 5000;
                        int food = ForceAI.CalcExpectedFoodUse(troops, 10);
                        if (troops >= src.troops && food >= src.food && srcInfo.goldIncome >= 0)
                            return false;
                    }
                    // 全部撤到最近的都市 —— :89-95
                    dest = GetNearestCityBuilding(src);
                    if (dest != null)
                    {
                        actors.AddRange(idle);
                        return true;
                    }
                }
                return false;
            }

            if (src.IsCity())
            {
                // 人数已达最小编制 → 优先派人去回收港关资源 —— :108-121
                if (srcInfo.personCount >= (srcInfo.state == OutpostState.Front ? 3 : 2))
                {
                    City gate = FindCollectibleGate(src, true);
                    if (gate != null)
                    {
                        Person person = idle[GameRandom.Range(0, idle.Count)];
                        if (person != null)
                        {
                            actors.Add(person);
                            dest = gate;
                            return true;
                        }
                    }
                }

                // 人不够分 → 不动 —— :123-125
                if (maxActorCount <= 0)
                    return false;
                // 内政设施未达最低配置 → 不从本城调人（它还缺人建设）—— :126-128
                if (!CityFacilityRequirement.MeetsMinGovFacilityCount(src))
                    return false;

                City destCity = FindRelocationDestCity(src, scenario);
                if (destCity == null)
                    return false;

                dest = destCity;
                OutpostInfo destInfo = OutpostRuntime.Get(destCity.Id);
                int destShortage = destInfo.minPersonCount - destInfo.personCount;
                // 目标比本据点更靠后（或同深度）时的上限调整 —— :140-149
                if (srcInfo.distanceFromBorder <= destInfo.distanceFromBorder)
                {
                    if (srcInfo.distanceFromBorder == destInfo.distanceFromBorder)
                        maxActorCount = (maxActorCount + destShortage) / 2;
                    else
                        maxActorCount = System.Math.Min(maxActorCount, destShortage);
                }
            }
            else
            {
                // 港关 —— :151-203
                if (srcInfo.state == OutpostState.Front)
                {
                    if (maxActorCount > 0)
                    {
                        City destCity = FindRelocationDestCity(src, scenario);
                        if (destCity != null)
                        {
                            OutpostInfo destInfo = OutpostRuntime.Get(destCity.Id);
                            int destShortage = destInfo.minPersonCount - destInfo.personCount;
                            if (destShortage > 0)
                            {
                                dest = destCity;
                                maxActorCount = System.Math.Min(maxActorCount, destShortage);
                                PickRandomPersons(idle, maxActorCount, actors);
                                return actors.Count > 0;
                            }
                        }
                    }
                    // 没钱或兵力不足 → 全部撤走，但**已有运输队在途时不动**
                    // 对齐 s11_game_ai_kyoten_cmd_personnel.cpp:187-193：
                    //   `if ((gold_ <= 0 or troops < 3000) and get_deploy_id(src, Transport) < 0)`
                    // 原实现漏了后半个条件 —— 运输队正赶来补给时仍会把守军全部抽空，
                    // 结果是人走了、补给也白跑一趟。
                    if (OutpostRuntime.gold <= 0 || src.troops < 3000)
                    {
                        if (!IsTransporting(src))
                            maxActorCount = idle.Count;
                    }
                }

                if (maxActorCount <= 0)
                    return false;

                dest = GetNearestCityBuilding(src);
                if (dest == null)
                    return false;
            }

            PickRandomPersons(idle, maxActorCount, actors);
            return actors.Count > 0;
        }

        /// <summary>
        /// 从候选池里随机取 N 个不重复的武将。
        ///
        /// 说明：原版 s11_game_ai_kyoten_cmd_personnel.cpp:205-214 是循环取随机下标，
        /// 会取到重复值（原版自己在 :205 标了 <c>s11_suspicious(3) 重复值</c>）。
        /// 重复取到同一个人会导致"同一个人被派两次"，这里改为去重取样，
        /// 与原版意图一致且不会产生脏数据。
        /// </summary>
        /// <param name="pool">候选池</param>
        /// <param name="count">需要的数量</param>
        /// <param name="output">输出列表</param>
        static void PickRandomPersons(List<Person> pool, int count, List<Person> output)
        {
            List<Person> temp = new List<Person>(pool);
            count = System.Math.Min(count, temp.Count);
            for (int i = 0; i < count; i++)
            {
                int index = GameRandom.Range(0, temp.Count);
                output.Add(temp[index]);
                temp.RemoveAt(index);
            }
        }

        /// <summary>
        /// 找出"缺人最多、且更靠后"的己方都市。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:218-254 find_relocation_dest_city。
        /// </summary>
        /// <param name="src">出发据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>目标都市；没有合适的返回 null</returns>
        static City FindRelocationDestCity(City src, Scenario scenario)
        {
            OutpostInfo srcInfo = OutpostRuntime.Get(src.Id);
            City best = null;
            int bestShortage = int.MinValue;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City dest = scenario.citySet[i];
                if (dest == null || !dest.IsAlive || !dest.IsCity())
                    continue;
                if (dest == src)
                    continue;
                if (dest.BelongForce != src.BelongForce)
                    continue;
                // 玩家：只在本军团内调动 —— :235-236
                if (src.BelongForce != null && src.BelongForce.IsPlayer && dest.BelongCorps != src.BelongCorps)
                    continue;

                OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                if (destInfo.personCount >= 50)
                    continue;

                int shortage = destInfo.minPersonCount - destInfo.personCount;
                // 更靠后且不缺人 → 跳过 —— :241-243
                if (destInfo.distanceFromBorder >= srcInfo.distanceFromBorder && shortage <= 0)
                    continue;

                int distance = GetCityDistance(src, dest);
                if (bestShortage < shortage || (bestShortage == shortage && distance < bestDistance))
                {
                    best = dest;
                    bestShortage = shortage;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// 找"有资源可回收"的己方港关。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_support.cpp:558-678 find_collectible_gate。
        /// 判定链完整照搬：
        ///   ① 回收门槛：金 ≥2000 / 粮 ≥10000（本城收支为负时放宽到 1000 / 5000）
        ///   ② 候选范围：玩家取"本军团辖区内的子据点"，AI 取"邻接且可达的关隘 / 港口"
        ///   ③ 排除：战斗中 / 前线 / 18 回合内运输过 / 已有守将 / 运输队已在途
        ///   ④ move=true 只要"有兵"的（派人过去搬）；move=false 只要"无兵"的（派车队去搬空）
        ///   ⑤ 取金、粮最多的一个
        /// </summary>
        /// <param name="city">出发都市</param>
        /// <param name="move">true = 武将移动用；false = 运输队用</param>
        /// <returns>港关；没有返回 null</returns>
        static City FindCollectibleGate(City city, bool move)
        {
            if (city == null)
                return null;

            OutpostInfo cityInfo = OutpostRuntime.Get(city.Id);
            int minGold = 2000;
            int minFood = 10000;
            if (cityInfo.goldIncome < 0 || cityInfo.foodIncome < 0)
            {
                minGold = 1000;
                minFood = 5000;
            }

            List<City> list = new List<City>();
            bool isPlayer = city.BelongForce != null && city.BelongForce.IsPlayer;

            if (isPlayer)
            {
                // 玩家：本军团辖区内的子据点 —— :575-592
                List<City> destList = GetReachableOutpost(city, 3);
                for (int i = 0; i < destList.Count; i++)
                {
                    City dest = destList[i];
                    if (dest == null || !dest.IsAlive || dest.IsCity())
                        continue;
                    if (dest.BelongForce != city.BelongForce)
                        continue;
                    if (dest.BelongCorps != city.BelongCorps)
                        continue;
                    // 【修正】判断"这个关隘是否隶属于本城"。
                    // 原版是 `city->get_id() == src_city_id`（源都市 Id 与关隘的所属城比较）。
                    // 原实现写成 `city.BelongCity == dest` —— 源是都市，其 BelongCity 恒为 null，
                    // 条件永远为假，导致玩家侧"派人回收关隘"从不触发。
                    if (dest.BelongCity == city)
                        list.Add(dest);
                }
            }
            else
            {
                // AI：邻接的关隘 / 港口 —— :593-624
                if (city.NeighborList != null)
                {
                    for (int i = 0; i < city.NeighborList.Count; i++)
                    {
                        City dest = city.NeighborList[i];
                        if (dest == null || !dest.IsAlive)
                            continue;
                        if (dest.IsCity())
                            continue;
                        if (dest.BelongForce != city.BelongForce)
                            continue;
                        list.Add(dest);
                    }
                }
            }

            City best = null;
            int bestGold = int.MinValue;
            int bestFood = int.MinValue;

            for (int i = 0; i < list.Count; i++)
            {
                City dest = list[i];
                if (dest == null || !dest.IsAlive)
                    continue;

                OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                // 战斗中 / 前线不回收 —— :632-637
                if (destInfo.state == OutpostState.Battle || destInfo.state == OutpostState.Front)
                    continue;
                // 18 回合冷却 —— :638-639
                if (GetTurnsSinceLastTransport(dest) < 18)
                    continue;
                // 已有守将 → 跳过 —— :640-642
                if (destInfo.personCount > 0)
                    continue;
                // 运输队已在途 → 跳过 —— :643-646
                if (IsTransporting(dest))
                    continue;

                if (move)
                {
                    // 派人搬：目标必须还有兵 —— :647-653
                    if (dest.troops <= 0)
                        continue;
                }
                else
                {
                    // 派车队搬空：目标必须已无兵 —— :654-660
                    if (dest.troops > 0)
                        continue;
                }

                // 资源门槛 —— :661-668
                if (dest.gold < minGold || dest.food < minFood)
                    continue;

                // 取资源最多的一个 —— :669-675
                if (dest.gold >= bestGold || dest.food >= bestFood)
                {
                    bestGold = dest.gold;
                    bestFood = dest.food;
                    best = dest;
                }
            }
            return best;
        }

        /// <summary>
        /// 找最近的己方都市。
        ///
        /// 对齐出处：s11_game_ai_kyoten.cpp:230-250 get_nearest_city_building。
        /// 玩家势力限定同军团（原版 :244-245）。
        /// </summary>
        /// <param name="city">起点据点</param>
        /// <returns>最近的己方都市；没有返回 null</returns>
        public static City GetNearestCityBuilding(City city)
        {
            Scenario scenario = Scenario.Cur;
            if (city == null || scenario == null)
                return null;

            int bestDistance = int.MaxValue;
            City best = null;
            bool isPlayer = city.BelongForce != null && city.BelongForce.IsPlayer;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City c = scenario.citySet[i];
                if (c == null || !c.IsAlive || !c.IsCity())
                    continue;
                if (c.BelongForce != city.BelongForce)
                    continue;
                if (isPlayer && c.BelongCorps != city.BelongCorps)
                    continue;

                int distance = GetCityDistance(city, c);
                if (bestDistance < distance)
                    continue;
                bestDistance = distance;
                best = c;
            }
            return best;
        }

        // ==================================================================================
        //  Summon —— 从后方据点抽调武将
        // ==================================================================================

        /// <summary>
        /// 据点级"召唤"命令：本据点缺人时，从同军团的后方据点抽调。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:256-369 召唤命令。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true（原版忽略返回值）</returns>
        public static bool AISummon(City city, Scenario scenario)
        {
            int cost = JobType.GetJobCostAP((int)CityJobType.CallPerson);
            int goldCost = JobType.GetJobCost((int)CityJobType.CallPerson);

            // 行动力 / 金钱不足 —— :262-266
            if (OutpostRuntime.actionPoint < cost)
                return true;
            if (OutpostRuntime.gold < goldCost)
                return true;

            OutpostInfo selfInfo = OutpostRuntime.Get(city.Id);
            int shortageCount = selfInfo.minPersonCount - selfInfo.personCount;
            // 不缺人 —— :268-270（原版写的是 < 0，等于 0 时仍会尝试）
            if (shortageCount < 0)
                return true;

            List<Person> candidates = new List<Person>();

            // 候选：同军团、非战斗、人数已达标、且内政地已建满的后方据点 —— :275-301
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City src = scenario.citySet[i];
                if (src == null || !src.IsAlive)
                    continue;
                if (src.BelongCorps != city.BelongCorps)
                    continue;
                if (src == city)
                    continue;

                OutpostInfo srcInfo = OutpostRuntime.Get(src.Id);
                if (srcInfo.existEnemy3)
                    continue;
                if (srcInfo.enemyUnitCount > 0)
                    continue;
                if (srcInfo.personCount < srcInfo.minPersonCount)
                    continue;
                if (src.IsCity())
                {
                    // 还有空地可开发 → 不抽人 —— :293-299
                    // 原版 city->get_facility_count() < city->get_dev_count()，
                    // 本项目等价判定是"内政地未建满"。
                    if (!src.IsInteriorBuildFull())
                        continue;
                }

                for (int p = 0; p < src.freePersons.Count; p++)
                {
                    Person person = src.freePersons[p];
                    if (person == null || !person.IsAlive || !person.IsFree)
                        continue;
                    candidates.Add(person);
                }
            }
            if (candidates.Count == 0)
                return true;

            List<Person> actors = new List<Person>();
            int cityId = city.BelongCity != null ? city.BelongCity.Id : city.Id;

            if (candidates.Count > shortageCount && shortageCount > 0)
            {
                // 只抽缺的人数，按"统率/武力 80+ 优先、距离越远越优先"挑 —— :314-353
                for (int i = 0; i < shortageCount; i++)
                {
                    Person pick = null;
                    int pickDist = -1;
                    int pickId = int.MaxValue;
                    bool pickPower = false;

                    for (int j = 0; j < candidates.Count; j++)
                    {
                        Person p = candidates[j];
                        bool power = p.Command >= 80 || p.Strength >= 80;
                        int dist = GetCityDistance(city, p.BelongCity ?? city);
                        // 优先能力强（power=true 排前），其次距离远，最后 Id 小
                        if (pick != null)
                        {
                            if (pickPower != power)
                            {
                                if (!power)
                                    continue;
                            }
                            else if (pickDist != dist)
                            {
                                if (dist <= pickDist)
                                    continue;
                            }
                            else if (p.Id >= pickId)
                            {
                                continue;
                            }
                        }
                        pick = p;
                        pickPower = power;
                        pickDist = dist;
                        pickId = p.Id;
                    }

                    if (pick == null)
                        break;
                    actors.Add(pick);
                    candidates.Remove(pick);
                    if (pick.BelongCity != null)
                        OutpostRuntime.Get(pick.BelongCity.Id).personCount--;
                }
            }
            else
            {
                // 候选不够 → 全部召唤 —— :355-359
                for (int i = 0; i < candidates.Count; i++)
                {
                    actors.Add(candidates[i]);
                    if (candidates[i].BelongCity != null)
                        OutpostRuntime.Get(candidates[i].BelongCity.Id).personCount--;
                }
            }

            if (actors.Count == 0)
                return true;

            for (int i = 0; i < actors.Count; i++)
            {
                Person person = actors[i];
                if (person == null || !person.IsAlive)
                    continue;
                PersonStateSystem.MoveToCity(person, city);
                OutpostRuntime.idlePersonList.Remove(person);
            }

            // 记账 —— :365-367
            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.gold -= goldCost;
            selfInfo.personCount += actors.Count;
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 召唤 {actors.Count} 人（剩余行动力{OutpostRuntime.actionPoint}）");
            return true;
        }

        // ==================================================================================
        //  Transport —— 组建运输队把富余物资送往需要的地方
        // ==================================================================================

        /// <summary>
        /// 据点级"运输"命令。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:5-207 运输命令。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true（原版忽略返回值）</returns>
        public static bool AITransport(City city, Scenario scenario)
        {
            int cost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);

            // 关隘 / 港口不执行运输 —— s11_game_ai_kyoten.cpp:169-171 的注释 + 本函数 :17-19
            if (!city.IsCity())
                return true;
            // 内政设施未达最低配置 → 不作为运输源 —— :47-50
            if (!CityFacilityRequirement.MeetsMinGovFacilityCount(city))
                return true;
            // 玩家第一军团不自动运输 —— :14-16
            if (city.BelongForce != null && city.BelongForce.IsPlayer && city.BelongCorps != null
                && city.BelongForce.CurRunCorps == city.BelongCorps)
                return true;

            // 玩家指定运输源（后续"借调守将"与"终点选择"两处复用）—— :22 / :73
            bool isPlayerTransportSrc = city.BelongForce != null && city.BelongForce.IsPlayer
                && city.BelongCorps != null
                && city.BelongCorps.districtTransportSrcId == city.Id;

            // 【玩家专用：借调守将】治安 ≥ 92 时，可以临时把守将派去押运 —— :21-30
            Person borrowedGuard = null;
            if (isPlayerTransportSrc && city.security >= 92 && city.BelongForce.guardPersonIds != null)
            {
                int guardId;
                if (city.BelongForce.guardPersonIds.TryGetValue(city.Id, out guardId))
                {
                    Person g = scenario.personSet.Get(guardId);
                    if (g != null && g.IsAlive && g.IsFree && g.CurrentCity == city)
                        borrowedGuard = g;
                }
            }

            // 无可用武将（借调的守将也算一个）—— :28-30
            if (OutpostRuntime.idlePersonList.Count == 0)
            {
                if (borrowedGuard == null)
                    return true;
                // 借调落地：放进可派池并解除守将身份 —— :152-156
                OutpostRuntime.idlePersonList.Add(borrowedGuard);
                city.BelongForce.guardPersonIds.Remove(city.Id);
                if (AIConfig.Instance.logOutpostAI)
                    Sango.Log.Info($"*[据点AI]{city.Name} 借调守将 {borrowedGuard.Name} 押运");
            }
            // 行动力不足 —— :31-34
            if (OutpostRuntime.actionPoint < cost)
                return true;
            // 粮 / 兵不足 —— :35-40
            if (city.food <= 0 || city.troops <= 0)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 战斗中的据点不运输 —— :41-43
            if (info.state == OutpostState.Battle)
                return true;
            // 18 回合冷却 —— :44-46
            if (GetTurnsSinceLastTransport(city) < 18)
                return true;
            // 本据点自身粮食不够支撑（前线 20 回合 / 后方 10 回合）—— :51-55
            int turnCount = info.state == OutpostState.Front ? 20 : 10;
            int selfFood = ForceAI.CalcExpectedFoodUse(city.troops, turnCount);
            if (city.food < selfFood)
                return true;

            // 富余物资（含兵装）—— :56-69
            int extraTroops, extraFood, extraGold;
            int[] extraEquipment;
            if (!GetExtraResource(city, out extraTroops, out extraFood, out extraGold, out extraEquipment))
                return true;
            if (extraTroops < 100)
                return true;
            if (extraFood < ForceAI.CalcExpectedFoodUse(extraTroops, turnCount))
                return true;

            // 目标据点 —— :71-83
            // 【玩家指定运输源】原版在 `player && building_id == transport_src_` 时改走
            // `get_player_district_transport_dest`（s11_game_ai_kyoten_cmd_mil_transport.cpp:71-81），
            // 该路径**不受 3 跳可达限制**，直接用玩家在本军团指定的终点。
            // 本项目此前算出了 `districtTransportSrcId` 却无任何读取方，等于整条功能没接上。
            // `isPlayerTransportSrc` 已在函数开头算好（借调守将也用它）。
            City target;
            if (isPlayerTransportSrc)
            {
                int destId = city.BelongCorps.GetTransportDestCityId(scenario);
                target = destId > 0 ? scenario.citySet.Get(destId) : null;
                // 终点校验：活着 / 同势力 / 非自身（原版 :417-431 的三个条件）
                if (target != null
                    && (!target.IsAlive || target.BelongForce != city.BelongForce || target == city))
                    target = null;
            }
            else
            {
                target = GetTransportDest(city, scenario);
            }
            if (target == null)
                return true;

            // 按目标容量裁剪 —— :86-96
            extraTroops = System.Math.Min(extraTroops, target.TroopsLimit - target.troops);
            extraFood = System.Math.Min(extraFood,
                target.FoodLimit - target.food + ForceAI.CalcExpectedFoodUse(extraTroops, turnCount));
            extraGold = System.Math.Min(extraGold, target.GoldLimit - target.gold);

            // 兵装按"目标还能收多少"裁剪 —— :89-90
            // 原版：`extra[i] = min(单队上限 - 目标现有[i], extra[i])`，
            // 基础兵装用 max_unit_equipment_quantity、攻城器械/舰船用 max_unit_crew_equipment_quantity。
            ScenarioVariables variables = scenario.Variables;
            if (extraEquipment != null && target.itemStore != null)
            {
                for (int i = 1; i < extraEquipment.Length; i++)
                {
                    int cap = i <= (int)ItemStoreKindType.Horse
                        ? variables.maxUnitEquipmentQuantity
                        : variables.maxUnitCrewEquipmentQuantity;
                    int targetHas = target.itemStore.GetNumber(i);
                    extraEquipment[i] = System.Math.Min(cap - targetHas, extraEquipment[i]);
                    if (extraEquipment[i] < 0)
                        extraEquipment[i] = 0;
                }
            }

            if (extraTroops <= 0 || extraFood <= 0)
                return true;

            int sendGold = 0;
            int sendFood = 0;
            int sendTroops = 0;

            OutpostInfo targetInfo = OutpostRuntime.Get(target.Id);
            if (target.IsCity())
            {
                // 目标都市：兵按粮推算 —— :102-116
                if (targetInfo.maxTroops - target.troops <= 0)
                {
                    int maxTroops = ForceAI.CalcExpectedDeployableTroops(
                        target.food + extraFood, targetInfo.foodIncome, 20);
                    sendTroops = maxTroops - target.troops;
                    if (sendTroops < 0)
                        sendTroops = 5000;
                    extraTroops = System.Math.Min(sendTroops, extraTroops);
                }
                sendTroops = extraTroops;
                sendGold = extraGold;
                sendFood = extraFood;
            }
            else
            {
                // 目标港关：兵按统率推算，粮按 40 回合 —— :117-128
                int recTroops = ForceAI.GetMaxCommandSum(target, targetInfo.minPersonCount);
                recTroops = System.Math.Min(recTroops,
                    System.Math.Min(targetInfo.personCount * 7500, city.TroopsLimit));
                sendTroops = System.Math.Min(recTroops - target.troops, extraTroops);
                int recFood = ForceAI.CalcExpectedFoodUse(target.food, 20);
                int shortFood = recFood - city.food;
                sendFood = ForceAI.CalcExpectedFoodUse(sendTroops, 40);
                if (shortFood > 0)
                    sendFood += shortFood;
                sendFood = System.Math.Min(sendFood, extraFood);
            }

            // 单队上限 clamp —— :142-144
            // 原版：troops/food/gold 各自 clamp 到 max_unit_transport_*。
            sendTroops = System.Math.Clamp(sendTroops, 0, variables.maxUnitTransportTroops);
            sendFood = System.Math.Clamp(sendFood, 0, variables.maxUnitTransportFood);
            sendGold = System.Math.Clamp(sendGold, 0, variables.maxUnitTransportGold);

            if (sendTroops <= 0 || sendFood <= 0)
                return true;

            // 兵装随队：按 get_extra_resource 的富余量、且不超过运输兵数装载 —— :136-141
            ItemStore cargo = BuildTransportItems(extraEquipment, sendTroops);

            // 组建运输队：复用既有的运输队创建入口 —— :152-190
            Troop troop = AIMakeTransportTroop(city, target, sendTroops, sendGold, sendFood, cargo, scenario);
            if (troop == null)
                return true;

            // 记账 —— :192-205
            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.gold -= sendGold;
            OutpostRuntime.food -= sendFood;
            SetLastTransportTurn(city, scenario);
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 运输 → {target.Name} 兵{sendTroops} 粮{sendFood} 金{sendGold}");
            return true;
        }

        /// <summary>
        /// 选运输目标。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:209-255 get_transport_dest。
        /// 规则：源在国境则不发；目标不能是战斗中的据点、不能没将、必须是都市、不能比源更靠后、
        ///       不能已经在运输中；多个候选取"邻接敌据点最多"的那个。
        /// </summary>
        /// <param name="city">出发据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>目标据点；没有返回 null</returns>
        static City GetTransportDest(City city, Scenario scenario)
        {
            OutpostInfo srcInfo = OutpostRuntime.Get(city.Id);
            // 前线不运输 —— :215-217
            if (srcInfo.state == OutpostState.Front)
                return null;

            List<City> destList = GetTransportDestBuildingList(city, scenario);
            if (destList.Count == 0)
                return null;

            City best = null;
            int bestEnemyNear = -1;
            bool isPlayer = city.BelongForce != null && city.BelongForce.IsPlayer;

            for (int i = 0; i < destList.Count; i++)
            {
                City dest = destList[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (isPlayer && dest.BelongCorps != city.BelongCorps)
                    continue;

                OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                if (destInfo.state == OutpostState.Battle)
                    continue;
                if (destInfo.personCount <= 0)
                    continue;
                if (!dest.IsCity())
                    continue;
                // 比源更靠后 → 不送 —— :243-245
                if (srcInfo.distanceFromBorder < destInfo.distanceFromBorder)
                    continue;
                // 已在运输中 —— :246-249
                if (IsTransporting(dest))
                    continue;

                if (best == null || destInfo.neighborEnemyOutpostCount > bestEnemyNear)
                {
                    best = dest;
                    bestEnemyNear = destInfo.neighborEnemyOutpostCount;
                }
            }
            return best;
        }

        /// <summary>
        /// 候选运输目标（距离 3 跳以内）。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:257-386 get_transport_dest_building_list。
        /// 三类分支：源是前线都市 / 源是后方都市 / 源是港关。
        /// </summary>
        /// <param name="src">出发据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>候选目标列表</returns>
        static List<City> GetTransportDestBuildingList(City src, Scenario scenario)
        {
            List<City> result = new List<City>();
            OutpostInfo srcInfo = OutpostRuntime.Get(src.Id);
            if (srcInfo.state == OutpostState.Battle)
                return result;

            // 可达据点（≤3 跳）—— 对应 ai_table_get_transport_dest_building_list(src, 3, true)
            List<City> reachable = GetReachableOutpost(src, 3);
            if (reachable.Count == 0)
                return result;

            bool isPlayer = src.BelongForce != null && src.BelongForce.IsPlayer;

            if (src.IsCity())
            {
                if (srcInfo.state == OutpostState.Front)
                {
                    // 前线都市 → 优先补给同样在前线且资源不足的据点 —— :274-313
                    for (int i = 0; i < reachable.Count; i++)
                    {
                        City dest = reachable[i];
                        if (dest == null || !dest.IsAlive)
                            continue;
                        OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                        if (destInfo.state == OutpostState.Battle)
                            continue;
                        if (isPlayer && dest.BelongCorps != src.BelongCorps)
                            continue;
                        if (destInfo.state != OutpostState.Front)
                            continue;

                        if (dest.IsCity())
                        {
                            if (destInfo.shortage)
                                result.Add(dest);
                        }
                        else if (src.BelongCity != null && src.BelongCity.Id == dest.Id)
                        {
                            result.Add(dest);
                        }
                    }
                }
                else
                {
                    // 后方都市 → 送往更靠近国境的都市 / 前线港关 —— :315-341
                    for (int i = 0; i < reachable.Count; i++)
                    {
                        City dest = reachable[i];
                        if (dest == null || !dest.IsAlive)
                            continue;
                        OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                        if (destInfo.state == OutpostState.Battle)
                            continue;
                        if (isPlayer && dest.BelongCorps != src.BelongCorps)
                            continue;

                        if (dest.IsCity())
                        {
                            if (destInfo.distanceFromBorder < srcInfo.distanceFromBorder)
                                result.Add(dest);
                        }
                        else if (destInfo.state == OutpostState.Front)
                        {
                            result.Add(dest);
                        }
                    }
                }
            }
            else
            {
                // 港关 → 只送最近的都市 —— :343-376
                if (srcInfo.state == OutpostState.Front)
                    return result;

                int bestDistance = int.MaxValue;
                for (int i = 0; i < reachable.Count; i++)
                {
                    City dest = reachable[i];
                    if (dest == null || !dest.IsAlive || !dest.IsCity())
                        continue;
                    OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                    if (destInfo.state == OutpostState.Battle)
                        continue;
                    if (isPlayer && dest.BelongCorps != src.BelongCorps)
                        continue;

                    int distance = GetOutpostDistance(src, dest);
                    if (distance < bestDistance)
                    {
                        result.Clear();
                        result.Add(dest);
                        bestDistance = distance;
                    }
                    else if (distance == bestDistance)
                    {
                        result.Add(dest);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 计算据点"除去最低驻军需求之外"的富余物资（兵 / 粮 / 金 / 兵装）。
        ///
        /// 对齐出处：s11_game_ai_kyoten.cpp:252-272 get_extra_resource。
        /// <code>
        /// command = min(max_command_sum(building, min_person_count), 5000 * min_person_count)
        /// if (command &lt; 5000) return false
        /// troops = troops - command
        /// food   = food - expected_food_use(command, state==Front ? 20 : 10)
        /// if (gold_income &gt; 0) gold = max((gold - gold_expenses*6)*3/4, 0)
        /// for (i = 枪 .. 马) equipment[i] = max(数量[i] - command, 0)
        /// if (state != Front) for (i = 冲车 .. 投石) equipment[i] = 数量[i]   // 后方攻城器可全运
        /// return true
        /// </code>
        /// 注意：原版**不**在这里判断"有没有富余"，只要 command ≥5000 就返回 true，
        /// 富余量可能为负，由调用方（运输 / 支援）各自设阈值。这里保持一致。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="troops">输出：富余兵力</param>
        /// <param name="food">输出：富余粮食</param>
        /// <param name="gold">输出：富余金钱</param>
        /// <param name="equipmentQuantity">输出：各类兵装的可运量（按 ItemStoreKindType 索引）</param>
        /// <returns>是否算得出来（false 表示该据点连最低编制都撑不起）</returns>
        public static bool GetExtraResource(City city, out int troops, out int food, out int gold,
            out int[] equipmentQuantity)
        {
            troops = 0;
            food = 0;
            gold = 0;
            equipmentQuantity = new int[(int)ItemStoreKindType.Boat + 1];

            if (city == null || city.itemStore == null)
                return false;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            int command = ForceAI.GetMaxCommandSum(city, info.minPersonCount);
            command = System.Math.Min(command, 5000 * info.minPersonCount);
            if (command < 5000)
                return false;

            troops = city.troops - command;
            int turnCount = info.state == OutpostState.Front ? 20 : 10;
            food = city.food - ForceAI.CalcExpectedFoodUse(command, turnCount);
            if (info.goldIncome > 0)
                gold = System.Math.Max((city.gold - info.goldExpenses * OutpostRuntime.GoldReserveTurn) * 3 / 4, 0);

            // 基础兵装：留够 command 的量 —— :264-265
            for (int i = (int)ItemStoreKindType.Spear; i <= (int)ItemStoreKindType.Horse; i++)
                equipmentQuantity[i] = System.Math.Max(city.itemStore.GetNumber(i) - command, 0);

            // 后方据点的攻城器械 / 船只可以全部运走 —— :266-270
            if (info.state != OutpostState.Front)
            {
                for (int i = (int)ItemStoreKindType.Helepolis; i <= (int)ItemStoreKindType.Boat; i++)
                    equipmentQuantity[i] = city.itemStore.GetNumber(i);
            }

            return true;
        }

        /// <summary>
        /// 组装随运输队带走的兵装。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:136-141
        /// （AI 分支：<c>equipment_quantity[i] = min(富余, 运输兵数)</c>，i 取枪 / 戟 / 弩 / 马）。
        /// 富余量直接取 <see cref="GetExtraResource"/> 的输出，不再另算保留量。
        /// </summary>
        /// <param name="extraEquipment">GetExtraResource 算出的各类兵装可运量</param>
        /// <param name="sendTroops">本次运输的兵力</param>
        /// <returns>兵装清单；没有可运的返回 null</returns>
        static ItemStore BuildTransportItems(int[] extraEquipment, int sendTroops)
        {
            if (extraEquipment == null || sendTroops <= 0)
                return null;

            ItemStore cargo = new ItemStore();
            for (int i = (int)ItemStoreKindType.Spear; i <= (int)ItemStoreKindType.Horse; i++)
            {
                if (i >= extraEquipment.Length)
                    break;
                int take = System.Math.Min(extraEquipment[i], sendTroops);
                if (take > 0)
                    cargo.Add(i, take);
            }

            return cargo.TotalNumber > 0 ? cargo : null;
        }

        /// <summary>
        /// 该据点是否已有运输队在途。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:247 的 get_deploy_id(dest, DeployType_Transport)。
        /// 本项目以部队任务类型判定。
        /// </summary>
        /// <param name="city">目标据点</param>
        /// <returns>是否在途</returns>
        static bool IsTransporting(City city)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || city == null)
                return false;
            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive)
                    continue;
                if (troop.BelongForce != city.BelongForce)
                    continue;
                if (troop.missionType != (int)MissionType.TroopTransformGoodsToCity)
                    continue;
                if (troop.missionTarget == city.Id)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 距上次运输的回合数。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:45 的 ai_table_get_turns_since_last_transport。
        /// </summary>
        /// <param name="city">据点</param>
        /// <returns>回合数；从未运输过返回 int.MaxValue</returns>
        static int GetTurnsSinceLastTransport(City city)
        {
            if (city == null || city.BelongForce == null || city.BelongForce.lastTransportTurn == null)
                return int.MaxValue;
            int turn;
            if (!city.BelongForce.lastTransportTurn.TryGetValue(city.Id, out turn))
                return int.MaxValue;
            Scenario scenario = Scenario.Cur;
            int now = scenario != null ? scenario.TurnCount : 0;
            return now - turn;
        }

        /// <summary>
        /// 记录本次运输回合。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        static void SetLastTransportTurn(City city, Scenario scenario)
        {
            if (city == null || city.BelongForce == null)
                return;
            if (city.BelongForce.lastTransportTurn == null)
                city.BelongForce.lastTransportTurn = new Dictionary<int, int>();
            city.BelongForce.lastTransportTurn[city.Id] = scenario != null ? scenario.TurnCount : 0;
        }

        // ==================================================================================
        //  Merchant / MerchantExtra —— 粮食买卖（两个方向，两条命令）
        // ==================================================================================

        /// <summary>
        /// 商人：金 / 粮出现单边赤字时**卖粮换金**。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:622-703 kyoten_merchant。
        /// <para>
        /// 原版这条命令只处理"一正一负"的局面（都赤字 / 都黑字都直接返回），
        /// 负号表示卖出粮食、正号表示买入 —— 本项目 <c>City.JobTradeFood</c> 的
        /// <c>goldNum</c> 参数同样是"正买负卖"，因此可以直接映射。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIMerchant(City city, Scenario scenario)
        {
            if (city == null || !city.IsCity())
                return true;

            int cost = JobType.GetJobCostAP((int)CityJobType.TradeFood);
            // 无可用武将 —— :632-634
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;
            // 本回合已经交易过 —— :635-637
            if (city.GetJobCounter((int)CityJobType.TradeFood) > 0)
                return true;
            // 行动力不足 —— :638-640
            if (OutpostRuntime.actionPoint < cost)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 6 回合后的金 / 2 回合后的粮 —— :642-643
            int goldIncome = info.goldIncome * OutpostRuntime.GoldReserveTurn;
            int foodIncome = info.foodIncome * 2;
            // 本月粮食消耗 —— :644-646（原版按"剩余天数比例"折算，这里直接用月消耗，偏保守）
            int foodUse = info.foodUse;
            if (info.state == OutpostState.Battle)
                foodUse = ForceAI.CalcExpectedFoodUse(city.troops, 10);

            int goldAfter = city.gold + goldIncome;
            int foodAfter = city.food + foodIncome;
            int foodNight = city.food - foodUse;
            if (foodNight < foodAfter)
            {
                foodAfter = foodNight;
                foodIncome = foodUse;
            }

            // 金粮双赤字 → 无解；双黑字 → 交给 MerchantExtra —— :656-661
            if (foodAfter < 0 && goldAfter < 0)
                return true;
            if (foodAfter >= 0 && goldAfter >= 0)
                return true;

            Person actor = PickBestPolitics();
            if (actor == null)
                return true;

            if (foodAfter >= 0)
            {
                // 金赤字、粮有余 → 卖粮换金 —— :669-676
                int n = System.Math.Min(
                    (city.food - ForceAI.CalcExpectedFoodUse(city.troops, 20)) / 2,
                    foodAfter / 2);
                if (n <= 0)
                    return true;
                if (!city.JobTradeFood(new[] { actor }, -n))
                    return true;
                Sango.Log.Info($"*[据点AI]{city.Name} 卖粮{n}换金（{actor.Name}）");
            }
            else
            {
                // 粮赤字 → 买粮，交给买粮命令 —— :677-685
                return AITradeFood(city, scenario);
            }

            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.idlePersonList.Remove(actor);
            OutpostRuntime.MarkExecuted();
            return true;
        }

        /// <summary>
        /// 商人（富余）：金不算多、粮有大量余量时卖粮；粮紧金多时买粮。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:705-778 kyoten_merchant_extra，
        /// 其内部先调 :780-845 的 kyoten_merchant_buy_extra_food 再走卖粮分支。
        /// <para>
        /// 【未搬的一处】原版卖粮前会判 <c>city-&gt;get_rate() &gt; 50</c>（粮价高则惜售）。
        /// 本项目的粮价系数语义未定型（<c>City.hasBusiness</c> 是商人活跃度），
        /// 因此这里不做惜售判断 —— 差异状态：⚠️ 缺粮价判断。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIMerchantExtra(City city, Scenario scenario)
        {
            if (city == null || !city.IsCity())
                return true;

            int cost = JobType.GetJobCostAP((int)CityJobType.TradeFood);
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;
            if (city.GetJobCounter((int)CityJobType.TradeFood) > 0)
                return true;
            if (OutpostRuntime.actionPoint < cost)
                return true;

            // 先试"买粮"（金多粮少）—— 原版 kyoten_merchant_buy_extra_food
            if (TryBuyExtraFood(city))
                return true;

            // 粮价高则惜售 —— 原版 :727-729 的 <c>city-&gt;get_rate() &gt; 50</c>。
            // `City.rate` 已按原版逐月刷新（30~70，7 月最贵），语义与 get_rate 一致。
            if (city.rate > 50)
                return true;

            // 以下为卖粮分支 —— :731-770
            OutpostInfo info = OutpostRuntime.Get(city.Id);

            // 6 回合后的金；不足 10000 → 说明还算缺钱，可以卖 —— :731-736
            int gold = city.gold;
            int goldIncome = info.goldIncome * 6;
            if (goldIncome < 0)
                gold += goldIncome;
            if (gold >= 10000)
                return true;

            // 4 回合后的粮；要留够 33 回合 —— :738-746
            int food = city.food;
            int foodIncome = info.foodIncome * 4;
            if (foodIncome < 0)
                food += foodIncome;
            int troops = System.Math.Max(city.troops, 15000);
            int keepFood = ForceAI.CalcExpectedFoodUse(troops, 33);
            if (food < keepFood + 10000)
                return true;

            Person actor = PickBestPolitics();
            if (actor == null)
                return true;
            // 政治 <50 时 80% 不做 —— :754-755
            if (actor.Politics < 50 && GameRandom.Chance(80))
                return true;

            int n = food - keepFood;
            // 至少卖 10000 粮 —— :758-760
            if (n < 10000)
                return true;

            if (!city.JobTradeFood(new[] { actor }, -n))
                return true;

            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.idlePersonList.Remove(actor);
            OutpostRuntime.MarkExecuted();
            Sango.Log.Info($"*[据点AI]{city.Name} 卖出余粮{n}（{actor.Name}）");
            return true;
        }

        /// <summary>
        /// 买粮：金 >12000 且存粮不足 36 回合时补粮。
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:780-845 kyoten_merchant_buy_extra_food。
        /// </summary>
        /// <param name="city">据点</param>
        /// <returns>是否已买</returns>
        static bool TryBuyExtraFood(City city)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);

            // 6 回合后的金；不足 12000 → 不买 —— :796-801
            int gold = city.gold;
            int goldIncome = info.goldIncome * 6;
            if (goldIncome < 0)
                gold += goldIncome;
            if (gold < 12000)
                return false;

            // 4 回合后的粮；够 36 回合 → 不买 —— :803-810
            int food = city.food;
            int foodIncome = info.foodIncome * 4;
            if (foodIncome < 0)
                food += foodIncome;
            int troops = System.Math.Max(city.troops, 10000);
            int keepFood = ForceAI.CalcExpectedFoodUse(troops, 36);
            if (food > keepFood)
                return false;

            Person actor = PickBestPolitics();
            if (actor == null)
                return false;
            if (actor.Politics < 50 && GameRandom.Chance(80))
                return false;

            // 买粮量 = 缺口 ×1.25，但要给国库留一半金 —— :820-827
            int need = keepFood * 5 / 4 - food;
            int keepGold = System.Math.Max(city.gold / 2, 10000);
            int budget = System.Math.Max(city.gold - keepGold, 0);
            if (budget <= 0)
                return false;

            // 原版按"粮数"下单，本项目 JobTradeFood 按"金"下单，这里按 1 金≈1 粮折算后取小的那个
            int goldNum = System.Math.Min(budget, need);
            if (goldNum <= 0)
                return false;

            if (!city.JobTradeFood(new[] { actor }, goldNum))
                return false;

            // 行动力扣减 —— 与其它据点命令一致（原版每条命令在自己体内扣一次）。
            // 此前该分支只 MarkExecuted 未扣 AP，靠调度器兜底；调度器统一扣减移除后需在此补上。
            OutpostRuntime.actionPoint -= JobType.GetJobCostAP((int)CityJobType.TradeFood);
            OutpostRuntime.idlePersonList.Remove(actor);
            OutpostRuntime.MarkExecuted();
            return true;
        }

        /// <summary>
        /// 取政治力最高的可下令武将（原版商人类命令统一按政治力选人）。
        /// </summary>
        /// <returns>武将；没有返回 null</returns>
        static Person PickBestPolitics()
        {
            Person best = null;
            for (int i = 0; i < OutpostRuntime.idlePersonList.Count; i++)
            {
                Person p = OutpostRuntime.idlePersonList[i];
                if (p == null || !p.IsAlive || !p.IsFree)
                    continue;
                if (best == null || p.Politics > best.Politics)
                    best = p;
            }
            return best;
        }

        // ==================================================================================
        //  Support —— 支援（派兵 / 搬资源）
        // ==================================================================================

        /// <summary>
        /// 据点级"支援"命令主入口。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_support.cpp:5-42。
        /// 四段顺序照搬：
        ///   ① 派兵支援被围攻的据点
        ///   ② 港关 → 把富余资源运回最近都市
        ///   ③ 都市 → 派人 + 送粮去"有资源但没人守"的港关
        ///   ④ 都市 → 把物资送给缺资源 / 缺将的据点
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true（原版忽略返回值）</returns>
        public static bool AISupport(City city, Scenario scenario)
        {
            int cost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);

            // 玩家第一军团不自动支援 —— :13-15
            if (city.BelongForce != null && city.BelongForce.IsPlayer && city.BelongCorps != null
                && city.BelongForce.CurRunCorps == city.BelongCorps)
                return true;
            // 无可用武将 —— :16-18
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;
            // 行动力不足 —— :19-21
            if (OutpostRuntime.actionPoint < cost)
                return true;
            // 兵 / 粮不足 —— :22-27
            if (city.troops <= 0 || city.food <= 0)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 战斗中不支援 —— :28-30
            if (info.state == OutpostState.Battle)
                return true;

            if (SupportMilitary(city, scenario))
                return true;
            if (!city.IsCity())
                return SupportCollect(city, scenario);
            if (SupportForCollect(city, scenario))
                return true;
            if (SupportResource(city, scenario))
                return true;
            return true;
        }

        /// <summary>
        /// 派兵支援"正在交战且兵力不足"的据点。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_support.cpp:44-212 support_military。
        /// <para>
        /// 【动作层】原版在这一步反复调 <c>com_deploy</c>，按"目标还缺多少兵"逐支编成部队
        /// （带兵 + 带粮 + 带金 + 一件攻城器械），最多 <c>max_unit_count</c> 支。
        /// 本项目由资材侧统一招标（<see cref="AIMakeTroop"/> 按城池现有兵装/粮草决定兵种与兵力），
        /// 一次支援派一支，目标直接取上面选定的据点；后续回合若目标仍缺兵会继续派。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否已执行支援</returns>
        static bool SupportMilitary(City city, Scenario scenario)
        {
            Force force = city.BelongForce;
            if (force == null)
                return false;

            // 弱小势力 85% 不做 —— :51-53
            // 前置条件 force->is_normal()：异民族 / 贼势力不参与"弱国保守"这条策略。
            if (!force.IsPlayer && force.IsNormal
                && ForceAI.GetPowerRank(force, scenario) < 50 && GameRandom.Chance(85))
                return false;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 只在后 / 中 / 前三态 —— :56-58
            if (info.state == OutpostState.Battle)
                return false;
            // 人太少 —— :59-61
            if (info.personCount <= 3)
                return false;

            int cost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);
            int maxUnitCount = System.Math.Max(info.personCount - info.minPersonCount, 1);
            if (cost > 0)
                maxUnitCount = System.Math.Min(maxUnitCount, OutpostRuntime.actionPoint / cost);
            maxUnitCount = System.Math.Min(maxUnitCount, OutpostRuntime.idlePersonList.Count);
            if (maxUnitCount <= 0)
                return false;

            // 交战中的邻接据点 —— :69-72
            List<City> destList = GetTransportDestBattleBuildingList(city);
            if (destList.Count == 0)
                return false;

            // 筛"兵力不足"的 —— :74-95
            List<City> targetList = new List<City>();
            bool isPlayer = force.IsPlayer;
            for (int i = 0; i < destList.Count; i++)
            {
                City dest = destList[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (isPlayer && dest.BelongCorps != city.BelongCorps)
                    continue;
                if (IsTransporting(dest))
                    continue;
                // 目标兵力已过线 —— :86-89
                int minTroops = (dest.IsCity() ? 6000 : 3000) * 2;
                if (dest.troops > minTroops)
                    continue;
                // 本据点兵力还不如目标 —— :90-91
                if (city.troops < dest.troops)
                    continue;
                targetList.Add(dest);
            }
            if (targetList.Count == 0)
                return false;

            // 目标选择：都市优先 —— :97-108
            City target = null;
            for (int i = 0; i < targetList.Count; i++)
            {
                City dest = targetList[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (target == null || (dest.IsCity() && !target.IsCity()))
                    target = dest;
            }
            if (target == null)
                return false;

            // 留守兵力要求 —— :110-124
            int keepTroops = (city.IsCity() ? 6000 : 3000) * System.Math.Max(info.minPersonCount - 1, 1);
            // 同一都市辖区、本方是港关、目标是都市、且港关兵更多 → 只需留 3000 —— :112-114
            if (city.BelongCity != null && target.BelongCity != null
                && city.BelongCity.Id == target.BelongCity.Id
                && !city.IsCity() && target.IsCity()
                && city.troops > target.troops)
                keepTroops = 3000;
            if (city.troops < keepTroops + 3000)
                return false;
            if (OutpostRuntime.food < ForceAI.CalcExpectedFoodUse(keepTroops, 10))
                return false;
            if (OutpostRuntime.gold < maxUnitCount * 400)
                return false;

            // 动作层：组建一支部队、出城、挂"驰援目标据点"任务
            Troop troop = AIMakeTroop(city, 20, false, scenario);
            if (troop == null)
                return false;
            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            if (troop == null)
                return false;               // 城池被围困，无法出城

            // 【任务类型必须是"移动"，不能是"保护城池"】
            // `SupportMilitary` 的语义是**补充兵力**：把兵送到、并入目标城。
            // 而 `TroopProtectCity` 是**解围**任务，它的 `Prepare` 一上来就判
            // `isNoEnemyAlive`（目标城当前是否有敌人），无敌人时立刻 `TroopReturnCity` ——
            // 结果部队刚创建就被改判返城，表现为"出城后一直站在城头没动"。
            // `TroopMovetoCity` 到达目标城格后会 `EnterCity`，兵力并入城池，正是增援所需。
            troop.SetMission(MissionType.TroopMovetoCity, target.Id);
            troop.NeedPrepareMission();

            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.MarkExecuted();
            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 派兵支援 → {target.Name}（{troop.Leader?.Name}）");
            return true;
        }

        /// <summary>
        /// 都市派人 + 送粮去"有资源但没人守"的港关。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_support.cpp:214-277 support_for_collect。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否已执行</returns>
        static bool SupportForCollect(City city, Scenario scenario)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 人数不足 —— :220-221
            if (info.personCount < info.minPersonCount)
                return false;
            // 兵不足 —— :222-225
            if (city.troops < 3000)
                return false;
            // 粮不足 —— :226-228
            int food = ForceAI.CalcExpectedFoodUse(3000, 20);
            if (OutpostRuntime.food < food)
                return false;

            // 有资源但没人守的港关 —— :229-233
            City target = FindCollectibleGate(city, false);
            if (target == null)
                return false;
            if (IsTransporting(target))
                return false;

            Troop troop = AIMakeTransportTroop(city, target, 3000, 0, food, null, scenario);
            if (troop == null)
                return false;

            int cost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);
            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.food -= food;
            OutpostRuntime.MarkExecuted();
            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 派人驻守港关 → {target.Name}");
            return true;
        }

        /// <summary>
        /// 港关把富余资源运回最近都市。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_support.cpp:279-372 support_collect。
        /// </summary>
        /// <param name="city">港关</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true（原版如此）</returns>
        static bool SupportCollect(City city, Scenario scenario)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 战斗中 / 前线的港关不往回运 —— :285-290
            if (info.state == OutpostState.Battle || info.state == OutpostState.Front)
                return false;

            // 留守兵 / 粮 —— :292-299
            int keepTroops = city.troops > 3000 ? 3000 : System.Math.Max(city.troops - 300, 0);
            int keepFood = ForceAI.CalcExpectedFoodUse(keepTroops, 10);
            if (keepFood > city.food)
            {
                keepTroops = 0;
                keepFood = 0;
            }

            City target = GetNearestCityBuilding(city);
            if (target == null)
                return false;
            if (IsTransporting(target))
                return true;

            // 可运走的量 —— :319-321
            int troops = city.troops - keepTroops;
            int food = city.food - keepFood;
            int gold = System.Math.Max(city.gold - info.goldExpenses, 0);
            if (troops <= 0 || food <= 0)
                return true;

            // 港关回收：兵装全带走 —— :336-340（原版把每种兵装都塞进运输队）
            ItemStore cargo = city.itemStore != null ? city.itemStore.Copy() : null;
            if (cargo != null && cargo.TotalNumber <= 0)
                cargo = null;

            Troop troop = AIMakeTransportTroop(city, target, troops, gold, food, cargo, scenario);
            if (troop == null)
                return true;

            int cost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);
            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.gold -= gold;
            OutpostRuntime.food -= food;
            SetLastTransportTurn(city, scenario);
            OutpostRuntime.MarkExecuted();
            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 回收资源 → {target.Name} 兵{troops} 粮{food} 金{gold}");
            return true;
        }

        /// <summary>
        /// 把物资送给"缺资源"或"缺将"的据点。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_support.cpp:374-556 support_resource。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否已执行</returns>
        static bool SupportResource(City city, Scenario scenario)
        {
            // 都市的内政设施未达最低配置 → 不对外支援 —— :383-387
            if (city.IsCity() && !CityFacilityRequirement.MeetsMinGovFacilityCount(city))
                return false;

            OutpostInfo info = OutpostRuntime.Get(city.Id);

            // 可送达的据点 —— :389-392
            List<City> destList = GetTransportDestBuildingList(city, scenario);
            if (destList.Count == 0)
                return false;

            // 筛"缺资源"优先，其次"缺将的港关" —— :394-427
            bool resourceShortage = false;
            List<City> targetList = new List<City>();
            bool isPlayer = city.BelongForce != null && city.BelongForce.IsPlayer;

            for (int i = 0; i < destList.Count; i++)
            {
                City dest = destList[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (isPlayer && dest.BelongCorps != city.BelongCorps)
                    continue;
                if (IsTransporting(dest))
                    continue;

                OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                if (destInfo.shortage)
                {
                    if (!resourceShortage)
                        targetList.Clear();
                    resourceShortage = true;
                    targetList.Add(dest);
                    continue;
                }
                if (resourceShortage)
                    continue;
                // 只补港关的人 —— :419-423
                if (dest.IsCity())
                    continue;
                if (destInfo.personCount >= destInfo.minPersonCount)
                    continue;
                targetList.Add(dest);
            }
            if (targetList.Count == 0)
                return false;

            // 目标选择：都市优先 —— :429-440
            City target = null;
            for (int i = 0; i < targetList.Count; i++)
            {
                City dest = targetList[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                if (target == null || (dest.IsCity() && !target.IsCity()))
                    target = dest;
            }
            if (target == null)
                return false;

            int maxUnitCount;
            if (resourceShortage)
            {
                // 缺资源：本据点人数必须已达标 —— :446-449
                if (info.personCount < info.minPersonCount)
                    return false;
                maxUnitCount = 1;
            }
            else
            {
                maxUnitCount = info.personCount - info.minPersonCount;
                int cost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);
                if (cost > 0)
                    maxUnitCount = System.Math.Min(maxUnitCount, OutpostRuntime.actionPoint / cost);
                maxUnitCount = System.Math.Min(maxUnitCount, OutpostRuntime.idlePersonList.Count);
                if (maxUnitCount <= 0)
                    return false;
            }

            // 留守兵 / 粮 —— :458-471
            int turnCount = info.state == OutpostState.Front ? 20 : 10;
            if (resourceShortage)
                turnCount /= 2;
            int keepTroops = info.minPersonCount * 7500;
            int keepFood = ForceAI.CalcExpectedFoodUse(keepTroops, turnCount);
            if (city.troops <= keepTroops)
                return false;
            if (OutpostRuntime.food < keepFood)
                return false;
            if (OutpostRuntime.gold < maxUnitCount * 400)
                return false;

            // 可运量 —— :487-504
            int maxFood = target.FoodLimit - target.food;
            int sendTroops = System.Math.Min(city.troops - keepTroops, 7500);
            if (sendTroops <= 0)
                return false;
            int sendFood = System.Math.Min(ForceAI.CalcExpectedFoodUse(sendTroops, turnCount) + 0, maxFood);
            sendFood = System.Math.Min(sendFood, OutpostRuntime.food - keepFood);
            if (sendFood <= 0)
                return false;
            int sendGold = System.Math.Min(System.Math.Min(OutpostRuntime.gold, 400), target.GoldLimit - target.gold);
            if (sendGold < 400 && sendGold < 0)
                return false;

            // 攻城器械 / 船只：有就带 1 件过去 —— :519-527
            // （原版在 equipment[1] 位置塞一件船或器械，目标据点有船坞/工坊时才有意义）
            ItemStore cargo = null;
            if (city.itemStore != null)
            {
                int[] specialKinds =
                {
                    (int)ItemStoreKindType.Boat,
                    (int)ItemStoreKindType.Helepolis,
                    (int)ItemStoreKindType.Catapult,
                };
                for (int i = 0; i < specialKinds.Length; i++)
                {
                    if (city.itemStore.GetNumber(specialKinds[i]) <= 0)
                        continue;
                    cargo = new ItemStore();
                    cargo.Add(specialKinds[i], 1);
                    break;
                }
            }

            Troop troop = AIMakeTransportTroop(city, target, sendTroops, sendGold, sendFood, cargo, scenario);
            if (troop == null)
                return false;

            int apCost = JobType.GetJobCostAP((int)CityJobType.MakeTansport);
            OutpostRuntime.actionPoint -= apCost;
            OutpostRuntime.food -= sendFood;
            OutpostRuntime.gold -= sendGold;
            // 目标不再缺资源 —— :546
            OutpostRuntime.Get(target.Id).shortage = false;
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 支援物资 → {target.Name} 兵{sendTroops} 粮{sendFood} 金{sendGold}");
            return true;
        }

        /// <summary>
        /// 距离 1 跳以内、处于交战状态的己方据点。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_transport.cpp:388-415 get_transport_dest_battle_building_list。
        /// </summary>
        /// <param name="src">出发据点</param>
        /// <returns>交战中的邻接据点</returns>
        static List<City> GetTransportDestBattleBuildingList(City src)
        {
            List<City> result = new List<City>();
            if (src == null)
                return result;

            OutpostInfo srcInfo = OutpostRuntime.Get(src.Id);
            if (srcInfo.state == OutpostState.Battle)
                return result;

            List<City> reachable = GetReachableOutpost(src, 1);
            bool isPlayer = src.BelongForce != null && src.BelongForce.IsPlayer;
            for (int i = 0; i < reachable.Count; i++)
            {
                City dest = reachable[i];
                if (dest == null || !dest.IsAlive)
                    continue;
                OutpostInfo destInfo = OutpostRuntime.Get(dest.Id);
                if (destInfo.state != OutpostState.Battle)
                    continue;
                if (isPlayer && dest.BelongCorps != src.BelongCorps)
                    continue;
                result.Add(dest);
            }
            return result;
        }

        // ==================================================================================
        //  SearchMax / EmployMax —— 人才不足时的强制尝试
        // ==================================================================================

        /// <summary>
        /// 探索（保底版）：只在本据点人数低于阈值时执行。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:425-432。
        /// 阈值 <c>n = 玩家 ? 3 : 3 + 难度 * 2</c>（3..7）。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AISearchMax(City city, Scenario scenario)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            int difficulty = scenario != null ? scenario.Variables.difficulty : 0;
            int n = (city.BelongForce != null && city.BelongForce.IsPlayer) ? 3 : 3 + difficulty * 2;
            if (info.personCount < n)
                return AISearching(city, scenario);
            return true;
        }

        /// <summary>
        /// 登用（保底版）：只在本据点人数低于阈值时执行。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:529-536。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIEmployMax(City city, Scenario scenario)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            int difficulty = scenario != null ? scenario.Variables.difficulty : 0;
            int n = (city.BelongForce != null && city.BelongForce.IsPlayer) ? 3 : 3 + difficulty * 2;
            if (info.personCount < n)
                return AIRecruitPerson(city, scenario);
            return true;
        }

        // ==================================================================================
        //  EmployEnemy —— 登用敌方武将
        // ==================================================================================

        /// <summary>
        /// 登用敌方武将。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:538-704。
        /// 判定链：
        ///   ① 无可用武将 / 行动力不足 → 不做
        ///   ② 本据点人已很多（min + max(min,10) ≤ 人数）→ 不做
        ///   ③ 候选：在职武将（都督/太守/一般）、非俘虏、非异族、非本势力、非同盟、未在途
        ///   ④ 距离门槛：忠诚 &gt; 90 → 距离 ≤1；忠诚 ≤90 → 距离 ≤2；距离 3 只 25% 概率
        ///   ⑤ 按"目标价值分"取最高者，执行一次登用
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIEmployEnemy(City city, Scenario scenario)
        {
            int cost = JobType.GetJobCostAP((int)CityJobType.RecruitPerson);
            Force force = city.BelongForce;
            if (force == null)
                return true;

            // 无可用武将 —— :543-545
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;
            // 行动力不足 —— :546-549
            if (OutpostRuntime.actionPoint < cost)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            int minPerson = info.minPersonCount;
            // 人已够多 —— :553-556
            if (minPerson + System.Math.Max(minPerson, 10) <= info.personCount)
                return true;

            City areaCity = city.BelongCity ?? city;
            int difficulty = scenario != null ? scenario.Variables.difficulty : 0;

            // 收集候选 —— :562-604
            List<Person> candidates = new List<Person>();
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive)
                    continue;
                // 只要在职武将（都督 / 太守 / 一般）—— :567-568
                if (person.state != (int)PersonStateType.Commander
                    && person.state != (int)PersonStateType.Leader
                    && person.state != (int)PersonStateType.Normal)
                    continue;
                // 在途 —— :569-571
                if (!person.IsFree)
                    continue;
                // 异族 —— :572-575
                if (person.barbarianTribe > 0)
                    continue;
                Force targetForce = person.BelongForce;
                if (targetForce == null)
                    continue;
                // 本势力 —— :576-578
                if (targetForce == force)
                    continue;
                // 同盟 —— :579-581
                if (force.IsAlliance(targetForce))
                    continue;

                City targetCity = person.CurrentCity ?? person.BelongCity;
                if (targetCity == null)
                    continue;
                int distance = GetCityDistance(areaCity, targetCity);

                if (person.loyalty > 90)
                {
                    // 忠诚高：只挖 1 格内 —— :587-593
                    if (distance <= 1)
                        candidates.Add(person);
                }
                else
                {
                    // 忠诚低：2 格内，3 格 25% —— :594-603
                    if (distance <= 2)
                        candidates.Add(person);
                    else if (distance == 3 && GameRandom.Chance(25))
                        candidates.Add(person);
                }
            }
            if (candidates.Count == 0)
                return true;

            // 打分选目标 —— :606-689
            // 原版这里靠 get_persuasion_chance 预估成功率，本项目该函数在
            // Person.JobRecruitPerson 内部（无公开预估接口），因此保留原版的
            // 距离 / 忠诚 / 难度门槛作为"值不值得试"的判据，成功率交给实际执行判定。
            int difficultyCoef = (5 + difficulty * 5) * 2;   // 10 .. 30
            int minLoyalty = 92 + difficulty * 2;            // 92 .. 96
            bool isPlayer = force.IsPlayer;

            Person bestActor = null;
            Person bestTarget = null;
            int bestScore = 0;

            for (int a = 0; a < OutpostRuntime.idlePersonList.Count; a++)
            {
                Person actor = OutpostRuntime.idlePersonList[a];
                if (actor == null || !actor.IsAlive || !actor.IsFree)
                    continue;

                for (int t = 0; t < candidates.Count; t++)
                {
                    Person target = candidates[t];
                    int score = GetEmployTargetScore(target);

                    if (isPlayer)
                    {
                        // 玩家势力：低概率试探（原版仍会先算 chance，这里直接按难度抽检）
                        if (!GameRandom.Chance(100 - 3 * difficultyCoef))
                            continue;
                    }
                    else
                    {
                        if (target.BelongForce != null && target.BelongForce.IsPlayer)
                        {
                            // 目标是玩家武将：忠诚过高不挖 —— :641-665
                            if (target.loyalty >= minLoyalty)
                                continue;
                            if (target.loyalty > minLoyalty - 2 && GameRandom.Chance(70))
                                continue;
                            if (!GameRandom.Chance(difficultyCoef + System.Math.Max(100 - target.loyalty, 0)))
                                continue;
                        }
                        else
                        {
                            // 普通敌将：忠诚越低越容易动摇 —— :667-679
                            int n = 100 - target.loyalty;
                            n = n * n / 25;                  // 0 .. 400
                            if (!GameRandom.Chance(System.Math.Min(n, 50)))
                                continue;
                        }
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestActor = actor;
                        bestTarget = target;
                    }
                }
            }

            if (bestTarget == null || bestActor == null)
                return true;

            // 执行登用并记账 —— :694-703
            city.JobRecruitPerson(bestActor, bestTarget);
            OutpostRuntime.actionPoint -= cost;
            OutpostRuntime.idlePersonList.Remove(bestActor);
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 登用敌将 {bestTarget.Name}（执行者 {bestActor.Name}）");
            return true;
        }

        /// <summary>
        /// 登用敌方（保底版）：本据点人数 &lt; 3 时执行。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:706-713。
        /// 注意这里的阈值固定为 3（不复用难度的 3+difficulty*2）。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIEmployEnemyMax(City city, Scenario scenario)
        {
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            if (info.personCount < 3)
                return AIEmployEnemy(city, scenario);
            return true;
        }

        /// <summary>
        /// 登用目标的价值分。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_personnel.cpp:715-731 get_employ_target_score。
        /// <code>
        /// max = 0; sum = 0;
        /// for (i &lt;= Politics) { sum += base_stat(i); max = max(max, base_stat(i)); }
        /// return max &lt; 80 ? sum : max * 4;
        /// </code>
        /// </summary>
        /// <param name="target">目标武将</param>
        /// <returns>价值分</returns>
        public static int GetEmployTargetScore(Person target)
        {
            if (target == null)
                return 0;

            // 原版取 base_stat（不含经验成长）。本项目 PersonAttributeValue 同时保留
            // baseValue（基础值）与 value（含成长 / 年龄修正），这里取 baseValue 精确对齐。
            int[] stats =
            {
                target.command.baseValue,
                target.strength.baseValue,
                target.intelligence.baseValue,
                target.politics.baseValue,
            };
            int max = 0;
            int sum = 0;
            for (int i = 0; i < stats.Length; i++)
            {
                sum += stats[i];
                if (stats[i] > max)
                    max = stats[i];
            }
            return max < 80 ? sum : max * 4;
        }

        // ==================================================================================
        //  Destroy / Merge —— 设施拆除与升级
        // ==================================================================================

        /// <summary>
        /// 拆除无用设施。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:847-973，配套
        /// <c>get_unnecessary_facility_list</c>（:975-1015）与 <c>get_destroyable_facility_list</c>（:1017-1032）。
        /// 分支照搬：
        ///   ① 交战中的据点：只剩无将时才拆（拆掉所有"未完工"的设施，避免资敌）
        ///   ② 平时：只有"内政地已建满"才拆，且只拆冗余同类设施
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIDestroy(City city, Scenario scenario)
        {
            // 非都市不拆 —— :852-854
            if (city == null || !city.IsCity())
                return true;

            // 【军团设置 · 建筑控制】关闭时既不建也不拆。
            // 与 `CityAI.AIBuilding` 读的是同一个开关（`AppointContentType.Build == 1` 表示关闭），
            // 保证"玩家在军团设置里关掉建筑控制"后，AI 不会一边不建、一边偷偷把旧建筑拆掉。
            if (city.BelongCorps != null
                && city.BelongCorps.GetAppointValue(Corps.AppointContentType.Build) == 1)
                return true;

            int cost = JobType.GetJobCostAP((int)CityJobType.Build);
            int goldCost = JobType.GetJobCost((int)CityJobType.Build);
            // 行动力 / 金不足 —— :855-860
            if (OutpostRuntime.actionPoint < cost)
                return true;
            if (OutpostRuntime.gold < goldCost)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            int destroyed = 0;

            if (info.state == OutpostState.Battle)
            {
                // 有将 → 先打仗，不拆 —— :866-870
                if (OutpostRuntime.idlePersonList.Count > 0)
                    return true;

                // 【兵临城下时禁止拆除】对齐原版 Battle 分支里的 `can_destroy_command(building)` 过滤
                // （s11_game_ai_kyoten_cmd.cpp:879），而该函数第一条就是
                // `if (building_has_enemy(self)) return false;`（s11_sys_person_order_building.cpp:13-22）。
                // <para>
                // 【为什么必须加】不加会形成死循环：
                //   `AIBuildIntriore` 建了一座内政设施（未完工，LeftCounter 还没走完）
                //   → 下回合据点处于 Battle 且无空闲武将
                //   → 本分支把"全部未完工设施"拆掉
                //   → 再下回合武将空闲，又建同一座
                //   → 建 → 拆 → 建 …… 反复烧钱。
                // 原版之所以不出这个循环，正是因为兵临城下时 `can_destroy_command` 直接拒绝。
                // </para>
                if (city.HasEnemyNearby())
                    return true;

                // 拆掉全部未完工设施 —— :871-889
                List<Building> pending = new List<Building>();
                for (int i = 0; i < city.allBuildings.Count; i++)
                {
                    Building building = city.allBuildings[i];
                    if (building == null || !building.IsAlive)
                        continue;
                    if (building.isComplate)
                        continue;
                    pending.Add(building);
                }
                for (int i = 0; i < pending.Count; i++)
                {
                    Building building = pending[i];
                    if (building == null || !building.IsAlive)
                        continue;
                    // 非攻击性销毁（未完工拆除）走 OnFall，对齐原版的"建造中的设施不留给敌方"
                    building.OnFall(null);
                    destroyed++;
                    OutpostRuntime.actionPoint -= cost;
                    OutpostRuntime.gold -= goldCost;
                    if (OutpostRuntime.actionPoint < cost)
                        break;
                }
            }
            else
            {
                // 【还有发展余地 → 不拆】
                // 原版判据是"设施数量 >= 内政地数量"（:891-892），之所以只看数量，
                // 是因为**吸收合并会让数量下降**（3 座低级合成 1 座高级），
                // 合并后数量回落 → 又出现空位 → 可以继续发展，循环不会卡死。
                // <para>
                // 本项目没有吸收合并，建筑只能单座升级、数量永不下降。
                // 若照搬数量判据，"数量建满"就会被当成彻底饱和，AI 随即开始拆现有设施腾位 ——
                // 而实际上每块地都还能从 Lv1 升到 Lv3，远未到顶。
                // 因此改用 `IsInteriorFullyDeveloped`：**数量与总等级双双到顶**才算饱和
                // （总等级上限 = 内政地数量 × 3）。
                // </para>
                if (!city.IsInteriorFullyDeveloped())
                    return true;

                // 找冗余设施 —— :894-962
                // 原版顺序：先找"周边市场不足的铸币厂"，再找"周边农田不足的谷仓"，
                // 两者都为空且内政未达最低配置时，才按一张固定优先级表拆低价值设施腾位置。
                Building target = FindUnnecessaryBuilding(city);
                if (target == null && !CityFacilityRequirement.MeetsMinGovFacilityCount(city))
                    target = FindDestroyableByPriority(city);
                if (target == null)
                    return true;
                target.OnFall(null);
                destroyed = 1;
                OutpostRuntime.actionPoint -= cost;
                OutpostRuntime.gold -= goldCost;
            }

            if (destroyed > 0)
            {
                OutpostRuntime.MarkExecuted();
                if (AIConfig.Instance.logOutpostAI)
                    Sango.Log.Info($"*[据点AI]{city.Name} 拆除设施 {destroyed} 处");
            }
            return true;
        }

        /// <summary>
        /// 内政未达最低配置时，按原版优先级表找一处"可牺牲"的设施拆掉，为必需设施腾地。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:901-960。
        /// 原版的顺序（Lv1/Lv2/Lv3 分开列 = 同种内先拆低级的）：
        /// <code>
        /// 黑市 → 炼兵所 → 计略府 → 外交府 → 人材府 → 军事府 → 符节台 → 鱼市 → 大市场 →
        /// 军屯农 → 造币厂 → 谷仓
        /// → 造船厂（特产≠水军）
        /// → 厩舍（特产≠马）
        /// → 农场系 / 市场系（数量多的那一类先拆）
        /// → 锻冶厂 → 兵舍
        /// </code>
        /// 本项目 Lv2/Lv3 与 Lv1 共用 kind，所以"同种内先拆低级"通过同 kind 内取 level 最小实现。
        /// </summary>
        /// <param name="city">城池</param>
        /// <returns>可拆设施；没有返回 null</returns>
        static Building FindDestroyableByPriority(City city)
        {
            if (city == null || city.interiorCellList == null)
                return null;

            List<int> kinds = new List<int>(24);
            kinds.Add((int)InteriorFacilityKind.BlackMarket);
            kinds.Add((int)InteriorFacilityKind.TrainingSchool);
            kinds.Add((int)InteriorFacilityKind.StrategemOffice);
            kinds.Add((int)InteriorFacilityKind.DiplomaticOffice);
            kinds.Add((int)InteriorFacilityKind.PersonnelOffice);
            kinds.Add((int)InteriorFacilityKind.MilitaryOffice);
            kinds.Add((int)InteriorFacilityKind.TallyOffice);
            kinds.Add((int)InteriorFacilityKind.FishMarket);
            kinds.Add((int)InteriorFacilityKind.CentralMarket);
            kinds.Add((int)InteriorFacilityKind.MilitaryFarm);
            kinds.Add((int)InteriorFacilityKind.Mint);
            kinds.Add((int)InteriorFacilityKind.Garner);

            // 造船厂：水军特产不拆 —— :923-924
            if (city.specialty != 5)
                kinds.Add((int)InteriorFacilityKind.Shipyard);
            // 厩舍：马特产不拆 —— :925-930
            if (city.specialty != 3)
                kinds.Add((int)InteriorFacilityKind.Stable);

            // 农场 / 市场：数量多的那一类先拆 —— :931-948
            int[] count = new int[CityFacilityRequirement.KindArraySize];
            CityFacilityRequirement.CountGovFacility(city, count);
            if (count[(int)InteriorFacilityKind.Farm] > count[(int)InteriorFacilityKind.Market])
            {
                kinds.Add((int)InteriorFacilityKind.Farm);
                kinds.Add((int)InteriorFacilityKind.Market);
            }
            else
            {
                kinds.Add((int)InteriorFacilityKind.Market);
                kinds.Add((int)InteriorFacilityKind.Farm);
            }

            kinds.Add((int)InteriorFacilityKind.Smith);
            kinds.Add((int)InteriorFacilityKind.Barracks);

            for (int i = 0; i < kinds.Count; i++)
            {
                Building building = FindLowestLevelBuilding(city, kinds[i]);
                if (building != null)
                    return building;
            }
            return null;
        }

        /// <summary>
        /// 找本城指定种类中等级最低的一处可拆设施。
        /// 对应原版"同种 Lv1 → Lv2 → Lv3"的拆除顺序（本项目靠 level 排序实现）。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="kind">设施种类</param>
        /// <returns>设施；没有返回 null</returns>
        static Building FindLowestLevelBuilding(City city, int kind)
        {
            Building pick = null;
            int pickLevel = int.MaxValue;
            for (int i = 0; i < city.interiorCellList.Count; i++)
            {
                Cell cell = city.interiorCellList[i];
                if (cell == null || cell.building == null)
                    continue;
                Building building = cell.building as Building;
                if (building == null || !building.IsAlive || building.BuildingType == null)
                    continue;
                if (building.BuildingType.kind != kind)
                    continue;
                // 建造中 / 升级中的不拆（原版 can_destroy_command 同样排除）
                if (!building.isComplate || building.isUpgrading)
                    continue;
                if (building.BuildingType.level < pickLevel)
                {
                    pickLevel = building.BuildingType.level;
                    pick = building;
                }
            }
            return pick;
        }

        /// <summary>
        /// 找一处"冗余"的内政设施。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd.cpp:975-1032。
        /// 原版先看"铸币厂周围市场是否够 3 座"、再看"农场仓库周围农田是否够 3 座"，
        /// 不够就判为冗余；都不满足时按一张内政设施优先级表找可拆的。
        /// <para>
        /// 【实现方式】只对**铸币厂**与**谷仓**做冗余判定，且看的是"配套设施"而非同类（见 <see cref="CountNeighborFacility"/>）：
        ///   · 铸币厂周边市场 ≥3 座 → 不冗余
        ///   · 谷仓周边农田 ≥3 座 → 不冗余
        /// 其余内政设施一律不参与（原版 `get_unnecessary_facility_list` 只接受这两个 FacilityId）。
        /// 原版另有一张"内政设施优先级表"用于挑选具体拆哪一座，
        /// 本项目改为"取等级最低、Id 最小"的一座 —— 差异仅在选择顺序。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <returns>可拆设施；没有返回 null</returns>
        static Building FindUnnecessaryBuilding(City city)
        {
            if (city.allBuildings == null || city.allBuildings.Count == 0)
                return null;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null)
                return null;

            Building pick = null;
            int pickLevel = int.MaxValue;

            for (int i = 0; i < city.allBuildings.Count; i++)
            {
                Building building = city.allBuildings[i];
                if (building == null || !building.IsAlive || building.BuildingType == null)
                    continue;
                if (!building.BuildingType.IsIntrior)
                    continue;
                // 建造中的不拆（由 Battle 分支处理）
                if (!building.isComplate)
                    continue;
                // 【升级中的也不拆】
                // `JobUpgradeBuilding` 只置 `isUpgrading = true`，`isComplate` 保持 true，
                // 因此升级中的建筑会通过上面那条检查、被当成"正常完工"的建筑选中拆除 ——
                // 表现就是"AI 刚升级完某座建筑，紧接着又把它拆了"。
                // `FindLowestLevelBuilding` 早已排除 `isUpgrading`（:2282），这里补齐。
                if (building.isUpgrading)
                    continue;

                int kind = building.BuildingType.kind;
                int level = building.BuildingType.level;

                // 【只判铸币厂与谷仓，且看的是"配套设施"而不是"同类"】
                // 原版 get_unnecessary_facility_list(city, FacilityId_Mint / FacilityId_Garner)：
                //   · 铸币厂：周边 2 格内**市场**不足 3 座 → 冗余（没有市场加持就是废建筑）
                //   · 谷仓  ：周边 2 格内**农田**不足 3 座 → 冗余
                // 农场 / 市场 / 兵营 / 铁匠铺 / 马厩 / 工坊等**不参与**这个判定。
                // <para>
                // 【修正】原实现把"周边同类不足即冗余"套到了**所有**内政设施上，
                // 导致一座孤立的农场（周边没有别的农场）被判为冗余并拆掉 ——
                // 这正是"城里建筑凭空少一座"的主因。
                // </para>
                int needKind;
                if (kind == (int)BuildingKindType.Mint)
                    needKind = (int)BuildingKindType.Market;
                else if (kind == (int)BuildingKindType.Barn)
                    needKind = (int)BuildingKindType.Farm;
                else
                    continue;

                // 周边 2 格内的配套设施数量（含高低级）
                int supportNear = CountNeighborFacility(scenario, building, needKind, -1);
                if (supportNear >= 3)
                    continue;

                // 取等级最低、Id 最小的一处
                if (level < pickLevel || (level == pickLevel && pick != null && building.Id < pick.Id) || pick == null)
                {
                    pick = building;
                    pickLevel = level;
                }
            }
            return pick;
        }

        /// <summary>
        /// 统计某建筑周边 2 格内、指定 kind（及可选 level）的设施数量。
        ///
        /// 对齐出处：Open311 的 ai_table_get_neighbor_facility_count（s11_game_ai_kyoten_cmd.cpp:988 等调用）。
        /// 原版按地图邻接格统计，这里用 Map.GetSpiral 取 2 格范围。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="self">自身建筑（不计入）</param>
        /// <param name="kind">建筑种类</param>
        /// <param name="level">等级；-1 表示不限等级</param>
        /// <returns>数量</returns>
        static int CountNeighborFacility(Scenario scenario, Building self, int kind, int level)
        {
            if (self.CenterCell == null)
                return 0;

            tempNeighborCells.Clear();
            scenario.Map.GetSpiral(self.CenterCell.x, self.CenterCell.y, 2, tempNeighborCells);

            int count = 0;
            for (int i = 0; i < tempNeighborCells.Count; i++)
            {
                Cell cell = tempNeighborCells[i];
                if (cell == null || cell.building == null || cell.building == self)
                    continue;
                Building other = cell.building as Building;
                if (other == null || !other.IsAlive || other.BuildingType == null)
                    continue;
                if (other.BuildingType.kind != kind)
                    continue;
                if (level >= 0 && other.BuildingType.level != level)
                    continue;
                count++;
            }
            return count;
        }

        /// <summary>邻接统计用的临时格列表（复用避免每次分配）。</summary>
        static readonly List<Cell> tempNeighborCells = new List<Cell>(64);

        /// <summary>
        /// 设施升级 —— `AIOrderId.Merge` 在本项目的实现。
        ///
        /// 【为什么不再叫"合并"】
        /// 原版 `PersonOrderId_Merge`（s11_game_ai_kyoten_cmd_dev_merge.cpp:5-65）是**区域合并**：
        /// 把区域内 3 座同类低级设施合成为 1 座高级设施（`can_be_merge_dest` +
        /// `get_merge_dest_score` 挑目标，再销毁另外几座）。
        /// 本项目的建筑体系是**单建筑升级**（`Building.isUpgrading` + `BuildingType.nextId`），
        /// 没有"多座合成一座"这一步，因此这里**直接走建筑升级**，
        /// 效果上等同"把低级设施提升为高级"，只是不消耗另外几座。
        /// <para>
        /// 已移除的两项原版合并遗留：
        /// · **500 金门槛**（原版 <c>:19-21</c> 的合并费）—— 与升级费无关，留着会凭空多一道限制；
        /// · **`OutpostRuntime.idlePersonList` 空判** —— 升级本身按 `city.freePersons` 选人，
        ///   两套口径不一致会误拦（例如此时列表被前面的命令掏空、但城里仍有人）。
        /// </para>
        /// <para>
        /// 【返回值】透传升级结果。调度器靠它判断"是否真的做了事"来决定扣不扣行动力
        /// （见 `OutpostCommand` 的 `delegatedExecuted`），恒返回 true 会导致
        /// "没升级也扣 AP"。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>本次是否成功升级了一座建筑</returns>
        public static bool AIMerge(City city, Scenario scenario)
        {
            if (city == null || !city.IsCity())
                return true;

            // 【不受「建筑控制」约束】该开关只管"删除内政设施"与"建造军事建筑"，
            // 升级既有设施属于正常发展，关闭建筑控制时仍然允许。
            return AIBuildingLevelUp(city, scenario);
        }

        // ==================================================================================
        //  Strategy —— 计略（离间 / 协助 / 流言 三选一）
        // ==================================================================================

        /// <summary>
        /// 据点级计略命令。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_strategy.cpp:5-25。
        /// 流程：先按"君主智力 + 野心 - 义理"算一个总概率闸门，过了之后
        /// 在三种计略里随机挑一种执行。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        public static bool AIStrategy(City city, Scenario scenario)
        {
            if (city == null || city.BelongForce == null)
                return true;

            // 总概率闸门 —— :7-17
            int chance = 5;
            Person kunshu = city.BelongForce.mGovernor;
            if (kunshu != null)
            {
                int intelligence = kunshu.Intelligence / 3 + 5;     // 5 .. 38
                int giri = kunshu.mArgumentation != null ? kunshu.mArgumentation.kind : 3;
                int n = kunshu.ambition - giri;                     // 近似原版的 -5 .. 5
                chance = intelligence + n * 5;
                chance = System.Math.Max(chance, 0);
            }
            if (!GameRandom.Chance(chance))
                return true;

            // 三选一 —— :18-21（原版按命令编号顺序随机：离间 / 协助 / 流言）
            int pick = GameRandom.Range(0, 3);
            if (pick == 0)
                return AIDisrupt(city, scenario);
            if (pick == 1)
                return AICollaborate(city, scenario);
            return AIRumor(city, scenario);
        }

        /// <summary>
        /// 离间：挑拨两个第三方势力的交情（本项目对应"二虎竞食"）。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_strategy.cpp:27-182。
        /// 门槛：每回合最多 2 次 / 75% 概率 / 据点金 ≥1000 / 有将 / 行动力与金足够 /
        ///       施计者智力决定成功率 <c>clamp((智力-20)*3, 0, 100)</c>。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        static bool AIDisrupt(City city, Scenario scenario)
        {
            Force force = city.BelongForce;

            // 次数限制 —— :31-33
            if (OutpostRuntime.disruptCount >= 2)
                return true;
            // 75% 才尝试 —— :34-36
            if (!GameRandom.Chance(75))
                return true;
            // 据点金不足 —— :37-39
            if (city.gold < 1000)
                return true;
            // 无可用武将 —— :43-45
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;

            int goldCost = JobType.GetJobCost((int)CityJobType.TwoTigers);
            int apCost = JobType.GetJobCostAP((int)CityJobType.TwoTigers);
            // 金不足 —— :46-48
            if (OutpostRuntime.gold < goldCost)
                return true;
            // 行动力不足 —— :49-52
            if (OutpostRuntime.actionPoint < apCost)
                return true;

            // 施计者：智力最高 —— :54-58
            Person actor = PickSmartest();
            if (actor == null)
                return true;
            // 施计者智力决定是否动手 —— :59-62
            int chance = System.Math.Clamp((actor.Intelligence - 20) * 3, 0, 100);
            if (!GameRandom.Chance(chance))
                return true;

            // 与我方有领土争端的势力 —— :66-78
            List<Force> disputes = GetTerritorialDisputesForces(force, scenario);
            if (disputes.Count == 0)
                return true;

            // 排除已敌对者（敌对就该直接打，不用离间）—— :70-78
            List<Force> srcCandidates = new List<Force>();
            for (int i = 0; i < disputes.Count; i++)
            {
                Force f = disputes[i];
                if (f == null || !f.IsAlive)
                    continue;
                if (force.IsEnemy(f))
                    continue;
                srcCandidates.Add(f);
            }
            if (srcCandidates.Count == 0)
                return true;

            // 挑一对"交情还没坏到没法挑"的组合 —— :87-165
            // 原版用 try_disrupt 预估结果分级，本项目该预估在计略结算内部完成，
            // 这里保留原版的关系门槛（必须仍高于基准友好度才值得挑拨）。
            const int yookouThreshold = 50;     // 原版 Yookou_MiddleLow
            Force bestSrc = null;
            Force bestDest = null;
            int bestRelation = int.MaxValue;

            for (int i = 0; i < srcCandidates.Count; i++)
            {
                Force src = srcCandidates[i];
                List<Force> srcDisputes = GetTerritorialDisputesForces(src, scenario);
                for (int j = 0; j < srcDisputes.Count; j++)
                {
                    Force dest = srcDisputes[j];
                    if (dest == null || !dest.IsAlive)
                        continue;
                    if (dest == force || dest == src)
                        continue;
                    // 双方都是玩家 → 不挑 —— :108-109
                    if (src.IsPlayer && dest.IsPlayer)
                        continue;

                    int relation = scenario.GetRelation(src, dest);
                    // 已经很差 → 再挑也没空间 —— :111-114
                    if (relation <= yookouThreshold)
                        continue;
                    if (relation < bestRelation)
                    {
                        bestRelation = relation;
                        bestSrc = src;
                        bestDest = dest;
                    }
                }
            }
            if (bestSrc == null || bestDest == null)
                return true;

            City destCity = bestSrc.CapitalCity;
            if (destCity == null)
                return true;

            CityStrategyManager manager = GameSystem.GetSystem<CityStrategyManager>();
            if (manager == null)
                return true;

            // 执行 —— :170-181
            bool dispatched = manager.Dispatch(
                new CityStrategyActionTwoTigers(force, city, actor, destCity, bestSrc, bestDest));
            if (!dispatched)
                return true;

            OutpostRuntime.idlePersonList.Remove(actor);
            OutpostRuntime.actionPoint -= apCost;
            OutpostRuntime.gold -= goldCost;
            OutpostRuntime.disruptCount++;
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 离间 {bestSrc.Name} 与 {bestDest.Name}（{actor.Name}）");
            return true;
        }

        /// <summary>
        /// 协助：策反敌方据点太守，使其带城独立（本项目对应"驱虎吞狼"）。
        ///
        /// 对齐出处：AI 侧 s11_game_ai_kyoten_cmd_strategy.cpp:184-302；
        /// 效果侧 s11_sys_person_order_strategy.cpp:739-776 的 on_collaborate_success。
        /// <para>
        /// 【效果语义已核实】原版成功时调用 <c>create_force_independence(太守, false, false)</c>
        /// （s11_sys_person_order_strategy.cpp:769），即**太守带着该城独立成一个新势力**，
        /// 既不留原势力、也不投奔发起方。这一点与常见误解（"带城投诚"）不同。
        /// </para>
        /// <para>
        /// 【效果层】走 <see cref="CityStateSystem.CreateIndependence"/>（对应原版的
        /// <c>create_force_independence</c>）：太守带城脱离原势力、建立新势力，
        /// 城内武将一并改属，不迁人。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        static bool AICollaborate(City city, Scenario scenario)
        {
            Force force = city.BelongForce;
            if (force == null)
                return true;

            // 次数上限 —— :189-191
            if (OutpostRuntime.collaborateCount >= 1)
                return true;
            // 据点金 < 1000 —— :192-194
            if (city.gold < 1000)
                return true;
            // 玩家第一军团但据点非直辖 —— :195-197
            if (force.IsPlayer && force.CurRunCorps != city.BelongCorps)
                return true;
            // 无可用武将 —— :198-200
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;

            int goldCost = JobType.GetJobCost((int)CityJobType.TigerDevour);
            int apCost = JobType.GetJobCostAP((int)CityJobType.TigerDevour);
            // 金不足 —— :201-203
            if (OutpostRuntime.gold < goldCost)
                return true;
            // 行动力不足 —— :204-207
            if (OutpostRuntime.actionPoint < apCost)
                return true;

            // 非前线 —— :208-210
            OutpostInfo info = OutpostRuntime.Get(city.Id);
            if (info.state != OutpostState.Front)
                return true;

            // 施计者：智力最高 —— :212-216
            Person actor = PickSmartest();
            if (actor == null)
                return true;
            // 智力概率 clamp((智-40)*3, 0, 100) —— :217-220
            int chance = System.Math.Clamp((actor.Intelligence - 40) * 3, 0, 100);
            if (!GameRandom.Chance(chance))
                return true;

            // 目标势力：最敌视的那一个 —— :222-230
            Force targetForce = PickMostHostileForce(force, scenario);
            if (targetForce == null)
                return true;

            // 目标城：3~5 格内、该势力名下、可被策反的都市 —— :232-290
            City areaCity = city.BelongCity ?? city;
            int maxDistance = 3 + GameRandom.Range(0, 3);
            int bestDistance = int.MaxValue;
            City bestTarget = null;
            int difficulty = scenario != null ? scenario.Variables.difficulty : 0;
            bool targetIsPlayer = targetForce.IsPlayer;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City dest = scenario.citySet[i];
                if (dest == null || !dest.IsAlive || !dest.IsCity())
                    continue;
                if (dest.BelongForce != targetForce)
                    continue;
                int distance = GetCityDistance(areaCity, dest);
                if (distance > maxDistance || distance >= bestDistance)
                    continue;
                Person taishu = dest.Leader;
                if (taishu == null || !taishu.IsAlive)
                    continue;
                // 目标是玩家时按难度要求太守忠诚下限 —— :276-278
                if (targetIsPlayer && taishu.loyalty >= 90 + difficulty * 2)
                    continue;
                bestDistance = distance;
                bestTarget = dest;
            }
            if (bestTarget == null)
                return true;

            // 执行 —— :291-300
            CityStrategyManager manager = GameSystem.GetSystem<CityStrategyManager>();
            if (manager == null)
                return true;

            bool dispatched = manager.Dispatch(
                new CityStrategyActionTigerDevour(force, city, actor, bestTarget, bestTarget.BelongForce));
            if (!dispatched)
                return true;

            OutpostRuntime.idlePersonList.Remove(actor);
            OutpostRuntime.actionPoint -= apCost;
            OutpostRuntime.gold -= goldCost;
            OutpostRuntime.collaborateCount++;
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 策反 {bestTarget.Name} 太守（{bestTarget.Leader?.Name}，施计者 {actor.Name}）");
            return true;
        }

        /// <summary>
        /// 取最敌视的存活势力。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_strategy.cpp:222-230 的
        /// <c>ai_table_get_hostile_force_list</c> + 按 <c>get_adjusted_like</c> 取最小。
        /// 本项目优先用势力 AI 每回合算好的 <see cref="Force.hostileForceIds"/>，
        /// 为空时退回"接壤势力里关系最差的一个"。
        /// </summary>
        /// <param name="force">发起方势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>最敌视的势力；没有返回 null</returns>
        static Force PickMostHostileForce(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return null;

            Force best = null;
            int bestRelation = int.MaxValue;

            if (force.hostileForceIds != null && force.hostileForceIds.Count > 0)
            {
                foreach (int id in force.hostileForceIds)
                {
                    Force f = scenario.forceSet.Get(id);
                    if (f == null || !f.IsAlive || f == force)
                        continue;
                    int relation = scenario.GetRelation(force, f);
                    if (relation < bestRelation)
                    {
                        bestRelation = relation;
                        best = f;
                    }
                }
            }
            if (best != null)
                return best;

            List<Force> disputes = GetTerritorialDisputesForces(force, scenario);
            for (int i = 0; i < disputes.Count; i++)
            {
                Force f = disputes[i];
                if (f == null || !f.IsAlive || f == force || force.IsAlliance(f))
                    continue;
                int relation = scenario.GetRelation(force, f);
                if (relation < bestRelation)
                {
                    bestRelation = relation;
                    best = f;
                }
            }
            return best;
        }

        /// <summary>
        /// 流言：降低敌方据点武将忠诚（都市同时降治安）。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_strategy.cpp:304-427。
        /// 门槛：每回合最多 3 次 / 据点金 ≥1000 / 有将 / 行动力与金足够 /
        ///       必须处于前线 / 施计者智力决定成功率 <c>clamp((智力-30)*3, 0, 100)</c> /
        ///       目标在 2~4 格内的敌对据点。
        /// </summary>
        /// <param name="city">据点</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒为 true</returns>
        static bool AIRumor(City city, Scenario scenario)
        {
            Force force = city.BelongForce;

            // 次数限制 —— :309-311
            if (OutpostRuntime.rumorCount >= 3)
                return true;
            // 据点金不足 —— :312-314
            if (city.gold < 1000)
                return true;
            // 无可用武将 —— :318-320
            if (OutpostRuntime.idlePersonList.Count == 0)
                return true;

            int goldCost = JobType.GetJobCost((int)CityJobType.Rumor);
            int apCost = JobType.GetJobCostAP((int)CityJobType.Rumor);
            // 金不足 —— :321-323
            if (OutpostRuntime.gold < goldCost)
                return true;
            // 行动力不足 —— :324-327
            if (OutpostRuntime.actionPoint < apCost)
                return true;

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 只有前线才施流言 —— :328-330
            if (info.state != OutpostState.Front)
                return true;

            // 施计者：智力最高 —— :332-336
            Person actor = PickSmartest();
            if (actor == null)
                return true;
            // 智力概率 —— :337-340
            int chance = System.Math.Clamp((actor.Intelligence - 30) * 3, 0, 100);
            if (!GameRandom.Chance(chance))
                return true;

            City areaCity = city.BelongCity ?? city;
            int maxDistance = 2 + GameRandom.Range(0, 3);       // 2 .. 4 —— :348

            City bestTarget = null;
            int bestDistance = maxDistance + 1;
            int bestRelation = int.MaxValue;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City target = scenario.citySet[i];
                if (target == null || !target.IsAlive || target == city)
                    continue;
                Force targetForce = target.BelongForce;
                if (targetForce == null || targetForce == force)
                    continue;
                // 只对敌对势力施 —— :362-364
                if (!force.IsEnemy(targetForce))
                    continue;

                int distance = GetCityDistance(areaCity, target);
                if (distance > maxDistance)
                    continue;

                // 关系越差越优先（原版按预估结果 + 距离排序）—— :387-398
                int relation = scenario.GetRelation(force, targetForce);
                if (relation > bestRelation)
                    continue;
                if (relation == bestRelation && distance >= bestDistance)
                    continue;

                bestRelation = relation;
                bestDistance = distance;
                bestTarget = target;
            }
            if (bestTarget == null)
                return true;

            CityStrategyManager manager = GameSystem.GetSystem<CityStrategyManager>();
            if (manager == null)
                return true;

            // 执行 —— :416-426
            bool dispatched = manager.Dispatch(
                new CityStrategyActionRumor(force, city, actor, bestTarget, bestTarget.BelongForce));
            if (!dispatched)
                return true;

            OutpostRuntime.idlePersonList.Remove(actor);
            OutpostRuntime.actionPoint -= apCost;
            OutpostRuntime.gold -= goldCost;
            OutpostRuntime.rumorCount++;
            OutpostRuntime.MarkExecuted();

            if (AIConfig.Instance.logOutpostAI)
                Sango.Log.Info($"*[据点AI]{city.Name} 对 {bestTarget.Name} 施流言（{actor.Name}）");
            return true;
        }

        /// <summary>
        /// 取智力最高的可下令武将（原版三处计略都用同一选法）。
        /// </summary>
        /// <returns>武将；没有返回 null</returns>
        static Person PickSmartest()
        {
            Person best = null;
            for (int i = 0; i < OutpostRuntime.idlePersonList.Count; i++)
            {
                Person p = OutpostRuntime.idlePersonList[i];
                if (p == null || !p.IsAlive || !p.IsFree)
                    continue;
                if (best == null || p.Intelligence > best.Intelligence)
                    best = p;
            }
            return best;
        }

        /// <summary>
        /// 取与指定势力"领土接壤"的其它势力。
        ///
        /// 对齐出处：Open311 的 ai_table_get_territorial_disputes_force_list。
        /// 原版是预计算的邻接势力表，这里按"我方据点的邻接据点归属"现算。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>接壤势力列表</returns>
        static List<Force> GetTerritorialDisputesForces(Force force, Scenario scenario)
        {
            List<Force> result = new List<Force>();
            if (force == null || scenario == null)
                return result;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                if (city.NeighborList == null)
                    continue;
                for (int n = 0; n < city.NeighborList.Count; n++)
                {
                    City nb = city.NeighborList[n];
                    if (nb == null || !nb.IsAlive || nb.BelongForce == null)
                        continue;
                    if (nb.BelongForce == force)
                        continue;
                    if (!nb.BelongForce.IsAlive)
                        continue;
                    if (!result.Contains(nb.BelongForce))
                        result.Add(nb.BelongForce);
                }
            }
            return result;
        }

        // ==================================================================================
        //  公共工具
        // ==================================================================================

        /// <summary>
        /// 两个据点之间的距离。
        ///
        /// 对齐出处：Open311 System::get_city_distance（都市间预计算距离表）。
        /// 本项目直接用地格距离，语义一致（都是"两城之间要跑多远"）。
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
            Scenario scenario = Scenario.Cur;
            if (a.CenterCell == null || b.CenterCell == null || scenario == null || scenario.Map == null)
                return int.MaxValue;
            return scenario.Map.Distance(a.CenterCell, b.CenterCell);
        }

        /// <summary>
        /// 据点图上的跳数距离（BFS）。
        ///
        /// 对齐出处：Open311 `s11_sys.cpp:32-92 calc_kyoten_distance` 与 `:127-130 get_kyoten_distance`。
        /// <para>
        /// 【已核实】原版这张表**没有地形代价** —— 它是无权 Floyd-Warshall：
        /// 邻接边权恒为 1，只对 <c>route ∈ {-1, Normal, Sea, Poison}</c> 的邻接置 1
        /// （`Route_Shortcut` 之类不置），然后三重循环求最短路。
        /// 因此本项目的 BFS 跳数与原版语义一致。
        /// </para>
        /// <para>
        /// 唯一的细微差别：原版有一套"南蛮工军"特技用的 `kyoten_short_distance_`
        /// （所有邻接都置 1，忽略路线类型）；本项目 `City.NeighborList` 不含路线类型，
        /// 两份距离表天然合一 —— 等价于原版取 short 版。差异状态：⚠️ 仅此一处。
        /// </para>
        /// </summary>
        /// <param name="src">起点</param>
        /// <param name="dest">终点</param>
        /// <returns>跳数；不可达返回 int.MaxValue</returns>
        static int GetOutpostDistance(City src, City dest)
        {
            if (src == null || dest == null)
                return int.MaxValue;
            if (src == dest)
                return 0;

            Dictionary<int, int> dist = new Dictionary<int, int>();
            Queue<City> queue = new Queue<City>();
            dist[src.Id] = 0;
            queue.Enqueue(src);

            while (queue.Count > 0)
            {
                City cur = queue.Dequeue();
                int d = dist[cur.Id];
                if (cur.NeighborList == null)
                    continue;
                for (int i = 0; i < cur.NeighborList.Count; i++)
                {
                    City nb = cur.NeighborList[i];
                    if (nb == null || !nb.IsAlive)
                        continue;
                    if (nb.BelongForce != src.BelongForce)
                        continue;
                    if (dist.ContainsKey(nb.Id))
                        continue;
                    dist[nb.Id] = d + 1;
                    if (nb == dest)
                        return d + 1;
                    queue.Enqueue(nb);
                }
            }
            return int.MaxValue;
        }

        /// <summary>
        /// 取 maxDepth 跳以内的己方据点。
        /// 对应 Open311 ai_table_get_transport_dest_building_list(src, 3, true) 的可达集合部分。
        /// </summary>
        /// <param name="src">起点</param>
        /// <param name="maxDepth">最大跳数</param>
        /// <returns>可达据点列表（不含自身）</returns>
        static List<City> GetReachableOutpost(City src, int maxDepth)
        {
            List<City> result = new List<City>();
            if (src == null)
                return result;

            Dictionary<int, int> dist = new Dictionary<int, int>();
            Queue<City> queue = new Queue<City>();
            dist[src.Id] = 0;
            queue.Enqueue(src);

            while (queue.Count > 0)
            {
                City cur = queue.Dequeue();
                int d = dist[cur.Id];
                if (d >= maxDepth)
                    continue;
                if (cur.NeighborList == null)
                    continue;
                for (int i = 0; i < cur.NeighborList.Count; i++)
                {
                    City nb = cur.NeighborList[i];
                    if (nb == null || !nb.IsAlive)
                        continue;
                    if (nb.BelongForce != src.BelongForce)
                        continue;
                    if (dist.ContainsKey(nb.Id))
                        continue;
                    dist[nb.Id] = d + 1;
                    result.Add(nb);
                    queue.Enqueue(nb);
                }
            }
            return result;
        }
    }
}
