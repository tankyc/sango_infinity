using Sango.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 据点 AI 命令集合。
    /// <para>
    /// partial 拆分：CityAIOutpost.cs 承载"按据点状态生成固定命令队列"
    /// （对齐原版 s11_game_ai_outpost*.cpp 的 据点主流程 / 据点命令表生成）。
    /// </para>
    /// </summary>
    public partial class CityAI
    {
        static internal PriorityQueue<City> priorityQueue = new PriorityQueue<City>();
        public static List<Cell> tempCellList = new List<Cell>();
        /// <summary>
        /// AI攻击逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIAttack(City city, Scenario scenario)
        {
            if (city.BelongForce == null)
                return true;

            // ---------- 零、对外用兵开关：军团委任 · 禁止攻击 / 全局和平期 ----------
            // 【本次修复】原先的"禁止攻击"判定只写在 DecideAttackTarget（只有"无军事任务"时才走到），
            // 于是上一回合留下的 TroopOccupyCity 任务会从下面的"持续派遣部队"分支继续派兵攻城
            // —— 玩家看到的就是"设了禁止攻击，部队还是被派出去打别的城市"。
            //
            // 【全局和平回合】除军团委任外，AI 的对外用兵还必须受剧本变量 AIAttackProtectedCount
            // 的"全局和平回合数"约束：开局前若干回合内所有 AI 势力一律不主动发起攻击。
            // 两道开关在这里合并处理，避免规则分裂：
            //   ① 撤掉"向外进攻"的城市级任务（守城/回援用的 TroopProtectCity 保留）；
            //   ② 召回已派到野外的各类进攻性用兵：攻城 / 跨城支援 / 战场补给队 / 前线工程队
            //      （见 RecallOffensiveTroops）。
            // 防御性用兵不受影响：守城、回援、驱逐侵入领地的敌军交给下面各分支照常执行。
            bool attackForbidden = IsAttackForbidden(city) || scenario.IsPeacePeriod;
            if (attackForbidden)
            {
                if (city.TroopMissionType == MissionType.TroopOccupyCity)
                {
                    city.TroopMissionType = MissionType.None;
                    city.TroopMissionTargetId = 0;
                }
                RecallOffensiveTroops(city, scenario);
            }

            // ---------- 一、防守优先 ----------
            // 【修复】本城受到直接威胁时,无论当前是否正在进攻他城,都必须优先转为防守。
            // 原逻辑仅在 TroopMissionType == None 时才检查防守,导致"正在进攻他城"的城市
            // 即便老家被围攻也不会出城防守。
            bool needDefense = AICanDefense(city, scenario);
            if (needDefense && city.TroopMissionType != MissionType.TroopProtectCity)
            {
                city.TroopMissionType = MissionType.TroopProtectCity;
                city.TroopMissionTargetId = city.Id;

                // 【玩家反馈 · 先城内派兵，守军够用就不动前线】
                // 原版只有"部队站在自家领地上时就地转防卫"（s11_game_ai_unit.cpp:185-198），
                // 没有"把在外攻城的部队拉回老家"这条。此前只要城附近（6~8 格）扫到任何敌军，
                // 就把本城所有 TroopOccupyCity 部队改派回防 —— 玩家看到的就是
                // "明明身边有敌军、粮草也够，一群人却集体往回跑"。
                //
                // 现在的口径：
                //   ① 先让城内组队出城迎敌（不等下一回合的 ContinueMilitaryMission）；
                //   ② 只有敌人逼近到 cityRecallScanRange(4) 格内、且守军仍不满足防守需求时，
                //      才召回前线部队 —— 正在交战的部队拉回来的代价很大。
                DispatchDefenseTroop(city, scenario);
                if (NeedRecallForDefense(city, scenario))
                    RecallAttackingTroops(city, scenario);
            }

            // ---------- 二、已有军事任务:持续派遣部队 ----------
            if (city.TroopMissionType != MissionType.None)
                return ContinueMilitaryMission(city, scenario);

            // ---------- 二点五、内政设施门槛：只守不攻 ----------
            // 对齐出处：s11_game_ai_kyoten_cmd_mil_attack.cpp:77-79
            //   if (not ai_table_meets_min_gov_facility_count(city_id)) return true;
            // 原版这条判定在 attack 的都市分支里。本项目把攻防合并进本方法，
            // 因此放在"选新进攻目标"之前 —— 上面已成立的防守/回援任务不受影响。
            if (city.IsCity() && !CityFacilityRequirement.MeetsMinGovFacilityCount(city))
            {
                // 该门槛原版就有（s11_game_ai_kyoten_cmd_mil_attack.cpp:77-79），
                // 但原版和本项目都无日志 —— 这里补一条，避免"某城怎么都不出兵"无从排查。
                LogAttack(city, "内政设施未达最低配置（市场/农场/兵舍/锻冶缺项），不出兵");
                return true;
            }

            // ---------- 二点六、玩家子军团：只有"出击据点"主动开战 ----------
            // 对齐出处：Open311 update_player_sub_district_data（s11_game_ai_district.cpp:143-159）
            // 为玩家子军团算出的 outpost_（离攻略目标最近的城）。
            // 原版只有该据点承担进攻任务，其余城只做内政与防守 —— 否则一个军团会多点开花。
            if (city.BelongForce != null && city.BelongForce.IsPlayer
                && city.BelongCorps != null && city.BelongCorps.number != 1
                && city.BelongCorps.districtTargetCityId > 0
                && city.Id != city.BelongCorps.districtTargetCityId)
                return true;

            // ---------- 三、无任务:按优先级决定新的军事目标 ----------

            // 3.1 防守
            if (needDefense)
            {
                city.TroopMissionType = MissionType.TroopProtectCity;
                city.TroopMissionTargetId = city.Id;
                return false;
            }

            // 3.2 驱逐对本势力实施过威胁行为、且仍位于本势力领地内的敌方部队
            Troop threat = FindThreatTroopInRange(city, scenario);
            if (threat != null)
            {
                city.TroopMissionType = MissionType.TroopDestroyTroop;
                city.TroopMissionTargetId = threat.Id;
                return false;
            }

            // 3.3 优先夺回被敌方占领的本城下属港关(不受"仅边境城市进攻"的限制)
            // 禁止攻击 / 全局和平期时跳过：它同样是"派兵去攻打一座被敌方占据的据点"，属向外用兵。
            City prioritySubCity = attackForbidden ? null : FindPrioritySubCity(city);
            if (prioritySubCity != null)
            {
                city.TroopMissionType = MissionType.TroopOccupyCity;
                city.TroopMissionTargetId = prioritySubCity.Id;
                // 【同回合出阵】原版 kyoten_attack 在 deploy_id < 0 时选完目标就**直接**进
                // while 出阵循环（s11_game_ai_kyoten_cmd_mil_attack.cpp:114-266），
                // 不是把出阵推到下一回合。旧实现只设任务不出兵，白等一回合。
                DispatchOccupyTroop(city, scenario);
                return false;
            }

            // 3.4 挑选进攻目标
            if (DecideAttackTarget(city, scenario))
                return true;
            // 选定目标的同一回合立即出阵 —— 同上（:114-266）。
            // 注意这里直接调 DispatchOccupyTroop 而不是 ContinueMilitaryMission：
            // 后者在"一支都没出成"时会清掉任务，而原版此时 deploy 仍保留、下回合继续补。
            DispatchOccupyTroop(city, scenario);
            return false;
        }

        /// <summary>
        /// 已有军事任务时持续派遣部队（进攻 / 防守 / 驱逐）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        static bool ContinueMilitaryMission(City city, Scenario scenario)
        {
            switch (city.TroopMissionType)
            {
                case MissionType.TroopOccupyCity:
                    DispatchOccupyTroop(city, scenario);
                    break;

                case MissionType.TroopProtectCity:
                    DispatchDefenseTroop(city, scenario);
                    break;

                case MissionType.TroopDestroyTroop:
                    // 目标已被歼灭,任务结束
                    if (!DispatchDriveOutTroop(city, scenario))
                    {
                        city.TroopMissionType = MissionType.None;
                        city.TroopMissionTargetId = 0;
                        return true;
                    }
                    break;
            }

            if (city.CurActiveTroop == null)
            {
                city.TroopMissionType = MissionType.None;
                return true;
            }

            city.Render?.UpdateRender();
            return false;
        }

        /// <summary>
        /// 一个据点、一个目标的一次进攻编组，最多容纳多少支部队（槽位硬上限）。
        ///
        /// 【原版对应】<c>DeployInfo::MaxUnitCount</c>，出处 s11_game_ai.h:506
        /// <c>static constexpr int MaxUnitCount = 16;</c>。
        ///
        /// 【本项目调整】放宽到 50。原因：原版 16 是按"一个编组打一场战役"的规模定的，
        /// 本项目地图更大、单城在籍武将更多，单支兵力又受主将统率限制，
        /// 16 支会在"所需兵力"达标前提前截断堆兵。其余钳制层（行动力 / 可下令人数 /
        /// 据点留守人数）仍按原版公式生效，这个常量只是最后一道保险。
        /// </summary>
        const int MaxAttackingTroopPerCity = 50;

        /// <summary>
        /// 单次「防守 / 支援」调度最多组建几支的硬上限（最后一道保险）。
        ///
        /// 正常的停止条件由"在外守军支数 / 支援上限 / 留守兵力 / 粮草 / 行动力"决定，
        /// 这里只防异常情况（例如剑兵兜底时不进 <c>allAttackTroops</c>，导致
        /// <see cref="City.AttackTroopsCount"/> 不增长）把同一次调度变成无限出兵。
        /// </summary>
        const int MaxEmitTroopPerDispatch = 10;

        /// <summary>
        /// 进攻任务：一次命令内**反复组建部队**，直到"在外进攻兵力"达到所需兵力。
        ///
        /// 对齐出处：Open311 s11_game_ai_kyoten_cmd_mil_attack.cpp:5-420 <c>kyoten_attack</c>。
        /// <para>
        /// 【旧实现错在哪】旧版每次只调一次 <see cref="AIMakeTroop"/>，一回合固定只出 **1 支**；
        /// 且用"在外部队**支数**"当判据（12 + 目标兵力/3000，白城 2），
        /// 而原版用的是"在外部队**兵力总量**"（<c>troops &gt; min_troops</c> 才停，:269）。
        /// 打大城时旧实现的堆兵速度只有原版的 1/N。
        /// </para>
        /// <para>
        /// 【最大支数钳制 — 原版三层，全部照搬】
        ///   ① <c>max_unit_count = min(据点行动力 / 单次出阵成本, 可下令人数)</c>（:32-37）；
        ///   ② 再取 <c>min(, 据点人数 − 最少留守人数)</c>（:86）；
        ///   ③ 循环尾部的弱势力随机衰减 <c>rand_bool(clamp(n*20-20,0,100))</c>（:412-417）。
        /// 另有编组槽位硬上限（<see cref="MaxAttackingTroopPerCity"/>：原版 16，本项目放宽到 50）。
        /// </para>
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        static void DispatchOccupyTroop(City city, Scenario scenario)
        {
            // 【兜底】军团委任 · 禁止攻击 / 全局和平期：本方法是"持续派遣"分支唯一的出城攻击出口，
            // 任务若被别的路径重新设回来，这里再挡一道，保证"禁止攻击"与"和平期"都不会被绕过。
            if (IsAttackForbidden(city) || scenario.IsPeacePeriod)
                return;

            City targetCity = scenario.citySet.Get(city.TroopMissionTargetId);
            if (targetCity == null)
            {
                LogAttack(city, "出兵取消：目标城已不存在（被灭/易主）");
                return;
            }

            OutpostInfo info = OutpostRuntime.Get(city.Id);
            // 单次出阵的行动力成本：走 AIConfig（默认 5），与调度器的预估同源，
            // 细节见 AIConfig.aiAttackActionPointCost 的说明。
            int actionPointCost = AIConfig.Instance.GetAttackActionPointCost(city);

            // 【兼容】旧命令链（City.DoAI 直调 AIAttack）没有据点预算上下文，
            // 此时不做行动力门禁与扣减，其余门槛照常；据点调度路径下 actionPoint 一定有值。
            bool hasActionPoint = OutpostRuntime.actionPoint > 0;

            // 单次出阵成本门禁 —— :29-30
            if (hasActionPoint && OutpostRuntime.actionPoint < actionPointCost)
            {
                LogAttack(city, $"行动力不足（剩余 {OutpostRuntime.actionPoint}，单次出阵需要 {actionPointCost}）");
                return;
            }

            // 可下令人数 —— :35
            int idleCount = OutpostRuntime.idlePersonList.Count;
            if (idleCount <= 0)
                idleCount = city.freePersons.Count;

            // 本回合最多能出几支 —— :32-37
            // 没有据点预算上下文（旧命令链）时退回 1 支，保持旧路径行为不变；
            // 成本配成 0 属于配置缺失，同样按 1 支保守处理。
            int maxUnitCount = hasActionPoint && actionPointCost > 0
                ? OutpostRuntime.actionPoint / actionPointCost
                : 1;
            maxUnitCount = System.Math.Min(maxUnitCount, idleCount);
            if (maxUnitCount <= 0)
            {
                LogAttack(city, "行动力或可下令武将不足，本回合不出兵");
                return;
            }

            if (city.troops <= 0)
                return;

            // 首次出阵门槛 —— :118-121
            // 原版 deploy_id < 0（还没有进攻编组）时要求：据点必须是前线、且城防 >= 80。
            // 本项目用"本城还没有任何进攻部队在外"等价于 deploy_id < 0。
            if (city.AttackingTroopsCount <= 0)
            {
                if (info.state != OutpostState.Front)
                {
                    LogAttack(city, $"首次出阵被拒：据点状态 {info.state}，不是前线(Front)");
                    return;
                }
                //if (city.energy < 80)
                //{
                //    LogAttack(city, $"首次出阵被拒：城防 {city.energy}<80");
                //    return;
                //}
            }

            // ---- 最少留守人数 / 兵力 / 粮 —— :43-72 ----
            int seikaku = GetSeikaku(city);
            int powerRank = ForceAI.GetPowerRank(city.BelongForce, scenario);
            bool weak = city.BelongForce != null && !city.BelongForce.IsPlayer && powerRank < 50;

            // 周边能打过来的敌军兵力 —— :43 ai_table_get_expected_max_enemy_troops
            int maxEnemyTroops = ExpectedMaxEnemyTroops(city, scenario);

            int minKeepPerson = 0;
            int minKeepTroops = 0;
            int minKeepFood = 0;
            if (city.IsCity())
            {
                // 最少留守武将数 = max(2, 周边敌军兵力 / 1000) —— :45-47
                minKeepPerson = 2;
                if (maxEnemyTroops > 0)
                    minKeepPerson = System.Math.Max(minKeepPerson, maxEnemyTroops / 1000);
                if (weak)
                    minKeepPerson = System.Math.Max(GameRandom.Range(0, minKeepPerson), 1);

                // 最少留守兵力 —— :57-70
                int recKeepTroops = minKeepPerson * 1000;
                minKeepTroops = recKeepTroops * powerRank / 100;
                minKeepTroops = System.Math.Max(minKeepTroops, 1500 * (7 - seikaku) + GameRandom.Range(0, 1001));
                // 【本次补上】留守兵力不能低于周边敌军的可投入兵力 —— :63
                minKeepTroops = System.Math.Max(minKeepTroops, maxEnemyTroops);
                minKeepTroops = System.Math.Min(minKeepTroops, 15000 + GameRandom.Range(0, 5000));
                if (weak)
                    minKeepTroops = minKeepTroops * (50 + GameRandom.Range(0, 41)) / 100;

                minKeepFood = ForceAI.CalcExpectedFoodUse(minKeepTroops, 10);

                // 治安不足 —— :74-76
                int needSecurity = weak ? 70 : 80;
                if (city.security < needSecurity)
                {
                    LogAttack(city, $"治安 {city.security}<{needSecurity}，不出兵");
                    return;
                }
            }

            // 武将不足 —— :82-84
            if (info.personCount <= minKeepPerson)
            {
                LogAttack(city, $"守将不足（据点 {info.personCount} 人 <= 留守 {minKeepPerson} 人），不出兵");
                return;
            }
            // 再钳一次：不能把留守人数都抽空 —— :86
            maxUnitCount = System.Math.Min(maxUnitCount, info.personCount - minKeepPerson);

            // 兵力 / 粮不足 —— :88-93
            if (city.troops < minKeepTroops)
            {
                LogAttack(city, $"留守兵力不足（{city.troops}<{minKeepTroops}），不出兵");
                return;
            }
            if (hasActionPoint && OutpostRuntime.food < minKeepFood)
            {
                LogAttack(city, $"留守粮草不足（{OutpostRuntime.food}<{minKeepFood}），不出兵");
                return;
            }
            if (maxUnitCount <= 0)
                return;

            // ---- 跨区（跨都市）进攻门槛 —— :126-211 ----
            // 原版 same_area 比的是"据点所属都市"（get_area_city_id）：
            // 都市返回自身，关隘/港口返回其所属都市。
            bool sameArea = GetAreaCityId(city) == GetAreaCityId(targetCity);

            if (!sameArea && (city.BelongForce == null || !city.BelongForce.IsPlayer))
            {
                // 跨区出征概率 —— :129-181
                int chance = 100;
                Person kunshu = city.BelongForce != null ? city.BelongForce.mGovernor : null;
                if (kunshu != null)
                {
                    int difficulty = scenario.Variables != null ? scenario.Variables.difficulty : 1;
                    // 难度 + 性格 —— :136
                    chance = (100 * (difficulty + 3) / 4) * (2 + seikaku) / 5;

                    // 野心（0~4，Ambition_Middle = 2）—— :137-142
                    int ambition = kunshu.ambition;
                    if (ambition < 2)
                        chance = chance / (3 - ambition);
                    else
                        chance = chance * (3 + ambition) / 5;

                    // 战略倾向 —— :143-158
                    // 0 UnifyChina / 1 UnifyRegion / 2 UnifyProvince / 3 MaintainStatus
                    switch (kunshu.strategic_tendency)
                    {
                        case 3:
                            chance = 5;
                            break;
                        case 2:
                            if (GetProvinceUnifiedCount(city.BelongForce, scenario) >= 1)
                                chance = 5;
                            break;
                        case 1:
                            if (GetProvinceUnifiedCount(city.BelongForce, scenario) >= 2)
                                chance = 5;
                            break;
                    }

                    // 久未用兵则更想打，超级强国反而收敛 —— :159-172
                    //if (!weak)
                    //{
                    //    int turns = System.Math.Min(city.BelongForce.GetTurnsSinceLastAttack(scenario), 100);
                    //    chance += turns * 3;
                    //    if (powerRank >= 90)
                    //        chance += (100 - powerRank) * 5;
                    //}
                    chance = System.Math.Clamp(chance, 5, 100);
                }
                if (!GameRandom.Chance(chance))
                {
                    LogAttack(city, $"跨区进攻放弃（概率 {chance}%）：{city.Name} → {targetCity.Name}");
                    return;
                }

                // 跨区的支数 / 兵力 / 粮草门槛 —— :203-211
                int minUnitCount = 1 + (weak ? 0 : 2);
                if (maxUnitCount < minUnitCount)
                {
                    LogAttack(city, $"跨区可出阵支数不足（{maxUnitCount}<{minUnitCount}），不出兵");
                    return;
                }
                // 注意原版这里是 min()：取"支数×7500"与"前 N 名统率之和"的较小者
                int minDeployTroops = System.Math.Min(minUnitCount * 7500, MaxCommandSum(city, minUnitCount));
                if (city.troops - minKeepTroops < minDeployTroops
                    || (OutpostRuntime.food - minKeepFood) / 2 < minDeployTroops)
                {
                    LogAttack(city, $"跨区兵力/粮草不足（需 {minDeployTroops}），不出兵");
                    return;
                }

                // 兵装不足（仅都市）—— :204-211，按千件向下取整后累加枪/戟/弩/马
                if (city.IsCity())
                {
                    int arms = 0;
                    if (city.itemStore != null)
                    {
                        for (int id = (int)ItemStoreKindType.Spear; id <= (int)ItemStoreKindType.Horse; id++)
                            arms += city.itemStore.GetNumber(id) / 1000 * 1000;
                    }
                    if (arms < minDeployTroops)
                    {
                        LogAttack(city, $"跨区兵装不足（{arms}<{minDeployTroops}），不出兵");
                        return;
                    }
                }
            }

            // ---- 所需兵力：目标兵力 ×(1 + r%)，r 由君主性格与势力排名决定 —— :245-264 ----
            // target_troops 取 ai_table_get_max_troops(target)
            // （= min(守将统率总和, 城内兵力) + 区域内本势力部队兵力，s11_sys_scen_ai.cpp:1428-1446），
            // 见 CityAIOutpost.MaxTroops。
            int targetTroops = MaxTroops(targetCity, scenario);
            int r = (3 - seikaku) * 15 + powerRank / 2;
            int needTroops = targetTroops + targetTroops * r / 100;
            // 放宽到需要对面1/2的兵力
            needTroops = System.Math.Max(needTroops, targetCity.troops);

            // 已在外的进攻兵力（同一目标的编组）—— :250-261
            int attackingTroops = 0;
            int attackingUnitCount = 0;
            for (int i = 0; i < city.allAttackTroops.Count; i++)
            {
                Troop t = city.allAttackTroops[i];
                if (t == null || !t.IsAlive)
                    continue;
                if (t.missionType != (int)MissionType.TroopOccupyCity)
                    continue;
                if (t.missionTarget != targetCity.Id)
                    continue;
                attackingTroops += t.troops;
                attackingUnitCount++;
            }

            List<Cell> path = scenario.GetCityDirectPathToOther(city, targetCity);
            int turnCount = System.Math.Max(20, 16 + path.Count / 4);

            // ---- 出阵循环 —— :266-418 ----
            int createdCount = 0;
            while (createdCount < maxUnitCount)
            {
                // 兵力已够 —— :268-270
                if (attackingTroops > needTroops)
                    break;
                // 编组槽位硬上限（原版 s11_game_ai.h:506 为 16，本项目放宽到 50）
                if (attackingUnitCount >= MaxAttackingTroopPerCity)
                    break;
                // 原版在"组建下一支之前"判这三项（:375-377 / :291-294 / :406-410）。
                // 本项目 AIMakeTroop 组建时就把兵/粮扣掉了，只能放在循环开头判，语义等价
                // 且不会把城市掏空。
                if (city.troops <= minKeepTroops)
                    break;
                if (hasActionPoint && OutpostRuntime.food <= minKeepFood)
                    break;
                if (hasActionPoint && OutpostRuntime.actionPoint < actionPointCost)
                    break;
                if (city.freePersons.Count < 2)
                    break;

                Troop troop = AIMakeTroop(city, turnCount, true, scenario);
                if (troop == null)
                    break;

                troop = CityTroopFactory.EmitTroop(city, troop, scenario);
                if (troop == null)
                    break;                              // 城池被围困,无法出城,本次出征已取消

                // 【立即出城】不能停在城头等下一回合（对齐 com_deploy 里的 run_order）
                if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
                {
                    Sango.Log.Warning($"{city.BelongForce?.Name}在{city.Name}组建的进攻部队无法出城（城头无可停留格或寻路失败），本次出征取消");
                    CityTroopFactory.RevokeEmit(city, troop);
                    break;
                }

                // 每支单独扣据点行动力 —— :383
                if (hasActionPoint && actionPointCost > 0)
                {
                    OutpostRuntime.actionPoint -= actionPointCost;
                    // 自扣型命令：告诉 OutpostCommand 不要再补扣一次（否则成本翻倍）
                    OutpostRuntime.MarkExecuted();
                }

                createdCount++;
                attackingUnitCount++;
                attackingTroops += troop.troops;

                // 记录"本势力刚出征" —— :403 ai_table_update_last_attack_turn
                city.BelongForce?.MarkAttackTurn(scenario);
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领{troop.TroopType.Name}军队出城 进攻{targetCity.BelongForce?.Name}的{targetCity.Name}!");

                // ---- 循环尾部再判一次 —— :404-417 ----
                if (OutpostRuntime.idlePersonList.Count > 0)
                {
                    // 把本队成员从可下令池里摘掉（原版 :386-391），保证同一武将不被复用
                    troop.ForEachPerson(p => OutpostRuntime.idlePersonList.Remove(p));
                    if (OutpostRuntime.idlePersonList.Count <= 0)
                        break;
                }
                if (city.freePersons.Count < 2)
                    break;
                if (hasActionPoint && OutpostRuntime.actionPoint < actionPointCost)
                    break;
                if (hasActionPoint && OutpostRuntime.food <= minKeepFood)
                    break;
                if (city.troops <= minKeepTroops)
                    break;
                // 弱势力随机衰减 —— :412-417
                if (weak)
                {
                    int n = System.Math.Clamp(attackingUnitCount * 20 - 20, 0, 100);
                    if (GameRandom.Chance(n))
                        break;
                }
            }

            if (createdCount <= 0)
                LogAttack(city, $"本回合未能组建任何进攻部队（在外兵力 {attackingTroops}/{needTroops}，据点状态 {info.state}）");
            else if (createdCount > 1)
                LogAttack(city, $"本回合连续出阵 {createdCount} 支，在外进攻兵力 {attackingTroops}/{needTroops}");
        }

        /// <summary>
        /// 出征用性格编号（0~3），对应原版 <c>seikaku_t</c>：
        /// 0 胆小(Shoushin) / 1 冷静(Reisei) / 2 刚胆(Goutan) / 3 莽撞(Chototsu)
        /// （s11_enum_scen.h:387-402）。**数值越大越冲动、越敢带少量兵出击。**
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_mil_attack.cpp:38-40
        /// <code>
        /// seikaku_t seikaku = Seikaku_Reisei;
        /// if (kunshu_id >= 0 and not building->is_player())
        ///     seikaku = game_->get_person(kunshu_id)->get_seikaku();
        /// </code>
        /// 用途：:262 <c>r = (3 - seikaku) * 15 + power_rank / 2</c>（所需兵力膨胀率）、
        /// :63 <c>1500 * (7 - seikaku)</c>（留守兵力下限）、:136 跨区进攻概率。
        /// </summary>
        /// <param name="city">据点</param>
        /// <returns>0~3</returns>
        static int GetSeikaku(City city)
        {
            if (city == null || city.BelongForce == null || city.BelongForce.IsPlayer)
                return 1;   // Seikaku_Reisei
            Person kunshu = city.BelongForce.mGovernor;
            if (kunshu == null)
                return 1;
            return System.Math.Clamp(kunshu.Seikaku, 0, 3);
        }

        /// <summary>
        /// 防守任务：本城周边有敌情时派遣守军出城。
        ///
        /// 【一次出多支】与 <see cref="DispatchOccupyTroop"/> 完全对称：同一条命令内循环组建，
        /// 每支都经出城队列（<see cref="CityTroopFactory.EmitTroop"/> + <see cref="CityTroopFactory.DriveOutOfCity"/>）
        /// 立即出城，直到"在外守军支数"达到上限，或兵 / 粮 / 武将耗尽、城头停不下为止。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        static void DispatchDefenseTroop(City city, Scenario scenario)
        {
            // 【修复】不依赖预计算的敌人列表，实时扫描兜底，避免"城池在挨打却不出兵"
            BattleSituation.ThreatSnapshot defenseThreat = BattleSituation.EvaluateThreat(
                city.CenterCell, AIConfig.Instance.cityDefenseScanRange, city, scenario);
            if (!defenseThreat.HasEnemy)
                return;

            int defenseEnemyCount = Math.Max(city.EnemyCount, defenseThreat.enemyCount);
            // 在外守军上限（原判据）：敌军越多，允许出城的守军越多
            int maxDefenseTroopCount = Math.Max(3, defenseEnemyCount + 4);

            int createdCount = 0;
            // 【口径修正】名额只看"守本城的部队"，不含远在前线攻城的部队。
            // 原来用 city.AttackTroopsCount（所有在外战斗部队）：前线部队越多 → 越"看起来守军已够"
            // → 城内不派守军，只能靠召回前线部队，玩家看到的就是"一群人在外往回跑"。
            // 新组建的部队在 AIMakeTroop 里就按 city.TroopMissionType 设好了守城任务，
            // 会立即计入 GetDefenseTroopInfo，所以这里每次重新统计即可。
            while (GetDefenseTroopInfo(city, out _) < maxDefenseTroopCount)
            {
                // 硬上限：防剑兵兜底（不进 allAttackTroops）时计数值不增长导致同一帧无限出兵
                if (createdCount >= MaxEmitTroopPerDispatch)
                    break;
                // 【门槛对齐】原版 kyoten_defense（s11_game_ai_kyoten_cmd_mil_defense.cpp:26-28）
                // 只要求"有可下令人或守将"（≥1 人）就允许出城，留守人数由 com_deploy /
                // AIMakeTroop 内部再判。原先这里要求 freePersons ≥ 2 —— 都督带兵出城后
                // 城里只剩 1 人时直接放弃出兵，玩家看到的就是"都督不在城里就没人抵抗"。
                if (city.freePersons.Count <= 0)
                {
                    Sango.Log.Info($"{city.BelongForce?.Name}在{city.Name}无可用武将（城内空闲 0 人），无法出城防守");
                    break;
                }
                if (city.troops <= 0)
                {
                    Sango.Log.Info($"{city.BelongForce?.Name}在{city.Name}无兵可出（城内兵力 0），无法出城防守");
                    break;
                }

                Troop troop = AIMakeTroop(city, 20, false, scenario);
                if (troop == null)
                    break;

                troop = CityTroopFactory.EmitTroop(city, troop, scenario);
                if (troop == null)
                    break;                          // 城池被围困,无法出城,本次出征已取消
                // 【立即出城】不能停在城头等下一回合（对齐 com_deploy 里的 run_order）
                if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
                {
                    Sango.Log.Warning($"{city.BelongForce?.Name}在{city.Name}组建的防守部队无法出城（城头无可停留格或寻路失败），本次出征取消");
                    CityTroopFactory.RevokeEmit(city, troop);
                    break;
                }

                createdCount++;
                // 防御出阵同样计入"最后出征回合" —— 对齐 s11_game_ai_kyoten_cmd_mil_defense.cpp:263
                city.BelongForce?.MarkAttackTurn(scenario);
                Sango.Log.Info($"{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城防守!");
            }

            if (createdCount > 1)
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce?.Name}在{city.Name}连续出城防守 {createdCount} 支（守军 {GetDefenseTroopInfo(city, out _)}/{maxDefenseTroopCount}）");
        }

        /// <summary>
        /// 驱逐任务：目标仍存活则持续派兵追击。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>任务是否仍然有效（false 表示目标已被歼灭）</returns>
        static bool DispatchDriveOutTroop(City city, Scenario scenario)
        {
            Troop threatTroop = scenario.troopsSet.Get(city.TroopMissionTargetId);
            if (threatTroop == null || !threatTroop.IsAlive)
                return false;

            if (city.AttackTroopsCount >= AIConfig.Instance.driveOutMaxTroopPerCity)
                return true;

            Troop troop = AIMakeTroop(city, 20, false, scenario);
            if (troop == null)
                return true;

            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            if (troop == null)
                return true;                        // 城池被围困,无法出城,本次出征已取消
            // 【立即出城】不能停在城头等下一回合（对齐 com_deploy 里的 run_order）
            if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
            {
                Sango.Log.Warning($"{city.BelongForce?.Name}在{city.Name}组建的驱逐部队无法出城（城头无可停留格或寻路失败），本次出征取消");
                CityTroopFactory.RevokeEmit(city, troop);
                return true;
            }
            city.BelongForce?.MarkAttackTurn(scenario);
            Sango.Log.Info($"{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城 驱逐{threatTroop.BelongForce?.Name}的{threatTroop.Name}!");
            return true;
        }

        /// <summary>
        /// 无军事任务时评估并选择进攻目标（含"延续上次目标"逻辑）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        static bool DecideAttackTarget(City city, Scenario scenario)
        {
            if (!AICanAttack(city, scenario))
                return true;

            // 延续上次的进攻目标，避免每回合重新评估导致部队反复改道
            City lastTargetCity = null;
            Troop activedTroop = scenario.troopsSet.Find(x => x.IsAlive && x.BelongCity == city);
            if (activedTroop != null && activedTroop.missionType == (int)MissionType.TroopOccupyCity)
                lastTargetCity = scenario.citySet.Get(activedTroop.missionTarget);

            if (lastTargetCity != null)
            {
                city.TroopMissionType = MissionType.TroopOccupyCity;
                city.TroopMissionTargetId = lastTargetCity.Id;
                LogAttack(city, $"延续上次进攻目标 {lastTargetCity.Name}");
                return false;
            }

            // 按"兵力对比 + 城池耐久 + 外交关系"给邻城打分
            priorityQueue.Clear();
            city.ForeachNeighborCities(x =>
            {
                if (!x.IsEnemy(city) || !city.BelongCorps.CheckTargetIsAppointTarget(x))
                    return;

                if (x.BelongForce == null)
                {
                    priorityQueue.Push(x, 9999);
                    return;
                }

                // 需要兵力充足（外置门槛：AICanAttack 通过但这里卡住时，候选队列恒空、AICanAttack 会显示"不出兵"）
                if (city.troops < AIConfig.Instance.attackTargetMinTroops)
                    return;

                int weight = (int)(2500 * (float)city.virtualFightPower / x.virtualFightPower);
                weight = weight * x.DurabilityLimit / x.durability;
                int relation = scenario.GetRelation(city.BelongForce, x.BelongForce);
                // 【0~100 尺度】50 = 中立；关系越好权重越低（越不愿进攻），越敌对权重越高。
                // 系数 = 1 + (0.5 - relation/100)：险恶 0 → ×1.5，中立 50 → ×1.0，亲密 100 → ×0.5。
                // （旧注释列出的 8000/6000/… 是 raw ±5000 时代的档位，已随关系表改造失效。）
                weight = UnityEngine.Mathf.FloorToInt((float)weight * (1f + (0.5f - (float)relation / 100f)));
                if (x.BelongForce.IsPlayer)
                    weight += AIConfig.Instance.attackTargetPlayerBonus;

                // 【距离因子】原版 `ai_table_get_attack_dest_building_list` 是按
                // `kyoten_distance` 由近及远给出的候选，近的据点优先。
                // 这里用 `CityDistanceTable`（FW 双表，与原版同构）折算权重：
                //   距离 1 → ×0.9，距离 6 及以上 → ×0.4（留下限，避免远方目标被完全排除）；
                //   不可达（-1，被地形或势力隔断）→ 权重降一个量级，基本不选。
                //
                // 说明：这一层是**城级 AI**，此时还没有部队，
                // 用不了 `Map.GetPathToRing`（它需要 Troop 决定消耗与 ZOC）。
                // 部队级的"去哪一格"由 `Map.GetPathToRing` / `Map.GetPathReverseAStar` 负责。
                CityDistanceTable distanceTable = scenario.cityDistanceTable;
                if (distanceTable != null)
                {
                    int dist = distanceTable.GetDistance(city, x, false);
                    if (dist > 0)
                    {
                        float factor = 1f - 0.1f * UnityEngine.Mathf.Min(dist, 6);
                        if (factor < 0.4f) factor = 0.4f;
                        weight = UnityEngine.Mathf.FloorToInt(weight * factor);
                    }
                    else if (dist < 0)
                    {
                        weight /= 10;
                    }
                }

                priorityQueue.Push(x, weight);
            });

            int count = GameRandom.Range(0, UnityEngine.Mathf.Max(0, priorityQueue.Count) + 1);
            if (count == 0)
                LogAttack(city, $"随机未尝试候选(count=0，候选{priorityQueue.Count})，本回合不出兵");

            for (int i = 0; i < count; i++)
            {
                int priority = 0;
                City targetCity = priorityQueue.Higher(out priority);
                if (targetCity == null)
                    continue;

                // 权重越高越可能被选中
                if (GameRandom.Chance(priority, 10000))
                {
                    city.TroopMissionType = MissionType.TroopOccupyCity;
                    city.TroopMissionTargetId = targetCity.Id;
                    LogAttack(city, $"决定进攻 {targetCity.Name}(权重{priority})");
                    return false;
                }
            }
            if (count > 0)
                LogAttack(city, $"候选未命中(count={count}，候选{priorityQueue.Count})，本回合不出兵");
            return true;
        }

        /// <summary>
        /// 统计本城当前的**守军**：任务为本城守城（<see cref="MissionType.TroopProtectCity"/>
        /// 且目标就是本城）的存活部队。
        ///
        /// 不能用 <see cref="City.AttackTroopsCount"/>：那是"所有在外战斗部队"，
        /// 含远在前线攻城的部队。前线部队越多，反而越会"看起来守军已经够了"而不派兵 ——
        /// 这正是"一群人在外往回跑、城里却不出兵"的成因之一。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="troops">守军兵力合计（输出）</param>
        /// <returns>守军支数</returns>
        static int GetDefenseTroopInfo(City city, out int troops)
        {
            troops = 0;
            if (city == null || city.allAttackTroops == null)
                return 0;

            int count = 0;
            for (int i = 0; i < city.allAttackTroops.Count; i++)
            {
                Troop t = city.allAttackTroops[i];
                if (t == null || !t.IsAlive)
                    continue;
                if (t.missionType != (int)MissionType.TroopProtectCity)
                    continue;
                if (t.missionTarget != city.Id)
                    continue;
                count++;
                troops += t.troops;
            }
            return count;
        }

        /// <summary>
        /// 是否需要召回前线部队回援本城。
        ///
        /// 两条规则（玩家反馈）：
        ///   ① 距离口子收紧：只有敌人在城池 <see cref="AIConfig.cityRecallScanRange"/>（默认 4）格内，
        ///      才算"兵临城下"，才考虑动前线部队；
        ///   ② 先由城内派守军（调用方已先执行 <see cref="DispatchDefenseTroop"/>）：
        ///      守军支数不少于敌军支数、或守军兵力不少于敌军兵力，任一满足就不再召回 ——
        ///      前线部队正在交战，白跑一趟的代价远大于再派几支守军。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>需要召回返回 true</returns>
        static bool NeedRecallForDefense(City city, Scenario scenario)
        {
            AIConfig cfg = AIConfig.Instance;
            BattleSituation.ThreatSnapshot threat = BattleSituation.EvaluateThreat(
                city.CenterCell, cfg.cityRecallScanRange, city, scenario);

            // ① 4 格内没有敌人 → 敌人还没逼到城下，前线部队继续作战
            if (!threat.HasEnemy)
                return false;

            // ② 守军已经能满足防守需求 → 不动前线
            int defenderTroops;
            int defenderCount = GetDefenseTroopInfo(city, out defenderTroops);
            if (defenderCount >= threat.enemyCount)
                return false;
            if (threat.enemyTroops > 0 && defenderTroops >= threat.enemyTroops)
                return false;

            return true;
        }

        /// <summary>
        /// 【修复】召回本城在外进攻的部队回援本城。
        /// 当本城受到攻击时,正在执行 TroopOccupyCity 的本城部队应转为协防本城。
        ///
        /// 【调用收窄】只有 <see cref="NeedRecallForDefense"/> 判定"敌人已进入 4 格且守军不足"时才调用，
        /// 不再"见敌就召回"（见 Update 里防守分支的注释）。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        static void RecallAttackingTroops(City city, Scenario scenario)
        {
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive)
                    continue;
                if (troop.BelongCity != city)
                    continue;
                if (troop.missionType != (int)MissionType.TroopOccupyCity)
                    continue;

                troop.SetMission(MissionType.TroopProtectCity, city.Id);
                troop.NeedPrepareMission();
                Sango.Log.Info($"{scenario.GetDateStr()}{city.Name}受到攻击,召回{troop.Leader?.Name}的部队回防!");
            }
        }

        /// <summary>
        /// 【军团委任 · 禁止攻击】召回本城一切"在外用兵"的部队回城待命。
        ///
        /// 覆盖已派到野外的四类进攻性用兵：
        ///   · 攻城部队（TroopOccupyCity）；
        ///   · 跨城支援部队（TroopProtectCity 且协防目标不是本城）；
        ///   · 战场补给队（TroopSupplyTroop）—— 随队携带的粮草 / 兵力 / 兵装
        ///     会在部队进城时由 <c>Troop.EnterCity</c> 原样返还，不会丢失；
        ///   · 前线工程队（TroopBuildBuilding / TroopMovetoBuild，且建址不在本城辖区内）。
        ///
        /// **不召回防御性部队**：守城（TroopProtectCity 目标为本城）、
        /// 驱逐侵入领地的敌军（TroopDestroyTroop）、辖区内的本地建造工程队一律不动。
        ///
        /// 统一改派为 TroopProtectCity(本城)：本城有敌情时它就是守军；无敌情时
        /// <see cref="TroopProtectCity"/> 的 Prepare 会自行转成返城并进城，与既有召回口径一致。
        /// 改派后这些部队不再命中本方法的判定，因此重复调用是幂等的、不会刷日志。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        static void RecallOffensiveTroops(City city, Scenario scenario)
        {
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive)
                    continue;
                if (troop.BelongCity != city)
                    continue;
                if (!IsOutwardExpedition(city, troop))
                    continue;

                troop.SetMission(MissionType.TroopProtectCity, city.Id);
                troop.NeedPrepareMission();
                Sango.Log.Info($"{scenario.GetDateStr()}{city.Name}：军团委任已禁止攻击，召回{troop.Leader?.Name}的部队回城待命!");
            }
        }

        /// <summary>
        /// 该部队当前是否在"向外用兵"（禁止攻击时需要召回的口径）。
        /// 防御性任务（守本城、驱逐侵入领地的敌军）不算；辖区内的本地建造工程队也不算。
        /// </summary>
        /// <param name="city">本城（部队的归属城）</param>
        /// <param name="troop">部队</param>
        /// <returns>属向外用兵返回 true</returns>
        static bool IsOutwardExpedition(City city, Troop troop)
        {
            switch ((MissionType)troop.missionType)
            {
                case MissionType.TroopOccupyCity:
                    return true;

                case MissionType.TroopProtectCity:
                    // 协防目标不是本城 = 跨城支援（召回）；目标就是本城 = 守城（保留）
                    return troop.missionTarget != city.Id;

                case MissionType.TroopSupplyTroop:
                    return true;

                case MissionType.TroopBuildBuilding:
                case MissionType.TroopMovetoBuild:
                    // 只有"建址不在本城辖区"的前线工程队才召回；
                    // 辖区内的本地建造是这座城自己的建设，不算对外用兵
                    return !IsBuildSiteInOwnArea(city, troop);

                default:
                    // 驱逐侵入领地的敌军、返城、待命等一律不动
                    return false;
            }
        }

        /// <summary>
        /// 工程队的建址是否落在本城辖区内（<c>areaCellList</c>）。
        /// 建址未知（<c>missionTargetCell</c> 为空）时按"辖区内"处理，避免把本地建造误召回。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="troop">工程队</param>
        /// <returns>建址在本城辖区内（或无从判断）返回 true</returns>
        static bool IsBuildSiteInOwnArea(City city, Troop troop)
        {
            Cell site = troop.missionTargetCell;
            if (site == null || city.areaCellList == null)
                return true;
            return city.areaCellList.Contains(site);
        }

        /// <summary>
        /// 【新增】查找需要驱逐的威胁部队。
        /// 威胁部队 = 曾攻击本势力(城池 / 建筑 / 部队)的敌方部队;
        /// 只要其仍存活、位于本势力领地内、且靠近本城,就需要派兵歼灭。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>需要驱逐的威胁部队,没有则返回 null</returns>
        static Troop FindThreatTroopInRange(City city, Scenario scenario)
        {
            Force force = city.BelongForce;
            if (force == null || force.threatTroopIds == null || force.threatTroopIds.Count == 0)
                return null;

            int range = AIConfig.Instance.driveOutThreatRange;
            Troop result = null;
            int bestDistance = int.MaxValue;

            for (int i = force.threatTroopIds.Count - 1; i >= 0; i--)
            {
                Troop troop = scenario.troopsSet.Get(force.threatTroopIds[i]);
                // 懒清理:目标已阵亡或不存在
                if (troop == null || !troop.IsAlive)
                {
                    force.threatTroopIds.RemoveAt(i);
                    continue;
                }

                Cell troopCell = troop.cell;
                if (troopCell == null)
                    continue;

                // 只处理位于本势力领地内的敌军
                City ownerCity = troopCell.BelongCity;
                if (ownerCity == null || ownerCity.BelongForce != force)
                    continue;

                // 领地归属是"地"的数据,判断不了这支部队现在是谁的人:
                // 登庸主将会让敌军部队原地换旗,此时它站在我方领地上却已经是我军,
                // 必须在这里补一次敌我判定并顺手从驱逐名单里摘掉,否则 AI 会派兵打自己人。
                if (!troop.IsEnemy(city))
                {
                    force.threatTroopIds.RemoveAt(i);
                    continue;
                }

                // 只处理靠近本城的敌军,避免一城跨越整个势力追击
                int distance = scenario.Map.Distance(city.CenterCell, troopCell);
                if (distance > range)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = troop;
                }
            }

            return result;
        }

        /// <summary>
        /// 【新增】查找需要优先夺回的本城下属港关。
        /// 当本城的下属港口 / 关卡被敌方(或非本势力)占领时,应优先派兵夺回。
        /// </summary>
        /// <param name="city">本城</param>
        /// <returns>需要夺回的港关,没有则返回 null</returns>
        static City FindPrioritySubCity(City city)
        {
            List<City> subCities = city.subCities;
            if (subCities == null || subCities.Count == 0)
                return null;

            City result = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < subCities.Count; i++)
            {
                City sub = subCities[i];
                if (sub == null || !sub.IsAlive)
                    continue;
                // 只考虑港口 / 关卡
                if (!sub.IsPort() && !sub.IsGate())
                    continue;
                // 仍属于本势力,无需夺回
                if (sub.BelongForce == city.BelongForce)
                    continue;

                // 选择距离本城最近的一个作为优先目标
                int distance = Scenario.Cur.Map.Distance(city.CenterCell, sub.CenterCell);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = sub;
                }
            }
            return result;
        }

        /// <summary>支援候选城市缓存,避免每次调用分配内存</summary>
        static readonly List<City> reinforceCandidates = new List<City>(16);

        /// <summary>
        /// 【新增】AI 支援逻辑:当同势力的邻近城市被围攻、而本城暂无战事时,派出部队前往支援。
        /// </summary>
        /// <param name="city">本城</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIReinforce(City city, Scenario scenario)
        {
            if (city.BelongForce == null)
                return true;

            // 军团委任 · 禁止攻击 / 全局和平期:跨城支援属"向外用兵",一并禁止。
            // 注意本城自己挨打时的守城不在这里 —— 那由 AIAttack 的"防守优先"分支负责,不受影响。
            if (IsAttackForbidden(city) || scenario.IsPeacePeriod)
                return true;

            // 本城自身已处于军事任务中(进攻或防守),不对外支援
            if (city.TroopMissionType != MissionType.None)
                return true;

            // 本城附近有敌人,自保优先
            if (city.IsEnemiesRound(15))
                return true;

            // 人手 / 兵力 / 粮食不足,无力支援
            AIConfig aiConfig = AIConfig.Instance;
            if (city.freePersons.Count < aiConfig.reinforceMinFreePersons)
                return true;
            if (city.troops < aiConfig.reinforceMinTroops || city.food < aiConfig.reinforceMinFood)
                return true;

            // 统计本城已派出的支援部队,设置上限,避免"抽干"老家守军
            int reinforcing = 0;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                Troop t = scenario.troopsSet[i];
                if (t != null && t.IsAlive && t.BelongCity == city
                    && t.missionType == (int)MissionType.TroopProtectCity
                    && t.missionTarget != city.Id)
                {
                    reinforcing++;
                }
            }
            if (reinforcing >= aiConfig.reinforceMaxTroopPerCity)
                return true;

            // 收集被围攻的邻近友城
            reinforceCandidates.Clear();
            for (int i = 0; i < city.NeighborList.Count; ++i)
            {
                City neighbor = city.NeighborList[i];
                if (neighbor == null || !neighbor.IsAlive)
                    continue;
                if (neighbor.BelongForce != city.BelongForce)
                    continue;
                if (!neighbor.CheckEnemiesIfAlive())
                    continue;
                reinforceCandidates.Add(neighbor);
            }

            // 【援军请求】追加"向我方求援的同盟"里正被围攻的城
            //   约定：Force.requestForceId = 我向谁求援（见 Force.requestForceId 注释）。
            //   所以"谁向我方求援"= 遍历势力表找 requestForceId == 我方 Id。
            //   原版对应 try_reinforce 成功后同盟需在约定回合内出兵（s11_game_week.cpp:292-311）。
            if (scenario.forceSet != null && scenario.citySet != null)
            {
                for (int fi = 0; fi < scenario.forceSet.Count; fi++)
                {
                    Force ally = scenario.forceSet[fi];
                    if (ally == null || ally == city.BelongForce) continue;
                    if (ally.requestForceId != city.BelongForce.Id) continue;   // 不是向我方求援
                    if (!ally.IsAlliance(city.BelongForce)) continue;           // 关系已破裂，不派

                    for (int ci = 0; ci < scenario.citySet.Count; ci++)
                    {
                        City allyCity = scenario.citySet[ci];
                        if (allyCity == null || !allyCity.IsAlive) continue;
                        if (allyCity.BelongForce != ally) continue;
                        if (!allyCity.CheckEnemiesIfAlive()) continue;
                        if (reinforceCandidates.Contains(allyCity)) continue;
                        reinforceCandidates.Add(allyCity);
                    }
                }
            }

            if (reinforceCandidates.Count == 0)
                return true;

            // 选择"敌情越重、守军越弱"的城市优先支援
            City target = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < reinforceCandidates.Count; ++i)
            {
                City candidate = reinforceCandidates[i];
                // 【统一态势】复用候选城市已预计算的敌人信息,避免重复扫描地图
                BattleSituation.ThreatSnapshot threat = BattleSituation.EvaluateCityThreat(candidate);
                float score = threat.threatLevel - candidate.troops * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    target = candidate;
                }
            }

            if (target == null)
                return true;

            // 临时指定任务,供 AIMakeTroop 组建部队
            city.TroopMissionType = MissionType.TroopProtectCity;
            city.TroopMissionTargetId = target.Id;

            List<Cell> path = scenario.GetCityDirectPathToOther(city, target);
            int turnCount = Math.Max(20, 16 + path.Count / 4);

            // 跨势力支援 = 援军：记录"响应谁的求援"，供 Troop.UpdateAIOrder 的援军分支
            // 与 CorpsAI 的"援军优先行动"排序使用（原版 unit.set_request_force_id）。
            int requestForce = target.BelongForce != null && target.BelongForce != city.BelongForce
                ? target.BelongForce.Id
                : -1;

            // 【一次出多支】本城还能派几支援军 = 单城上限 − 已在外的援军数。
            // 原实现只组建 1 支，被围友城经常等不到第二波就被打掉。
            int canEmitCount = aiConfig.reinforceMaxTroopPerCity - reinforcing;
            int createdCount = 0;
            while (createdCount < canEmitCount)
            {
                // 硬上限 + 资源兜底（AIMakeTroop 内部还有一层留守判定，这里先挡，避免无谓组建）
                if (createdCount >= MaxEmitTroopPerDispatch)
                    break;
                if (city.freePersons.Count < aiConfig.reinforceMinFreePersons)
                    break;
                if (city.troops < aiConfig.reinforceMinTroops || city.food < aiConfig.reinforceMinFood)
                    break;

                Troop troop = AIMakeTroop(city, turnCount, false, scenario);
                if (troop == null)
                    break;

                troop.requestForceId = requestForce;

                troop = CityTroopFactory.EmitTroop(city, troop, scenario);
                if (troop == null)
                    break;                              // 城池被围困,无法出城,本次出征已取消

                troop.requestForceId = requestForce;    // EmitTroop 后再次确保（登记阶段可能重建对象）
                // 【立即出城】援军不能停在城头等下一回合（对齐 com_deploy 里的 run_order）
                if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
                {
                    Sango.Log.Warning($"{city.BelongForce?.Name}在{city.Name}组建的援军无法出城（城头无可停留格或寻路失败），本次出征取消");
                    CityTroopFactory.RevokeEmit(city, troop);
                    break;
                }

                createdCount++;
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领援军出城 支援{target.BelongForce?.Name}的{target.Name}!");
            }

            if (createdCount > 1)
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce?.Name}在{city.Name}连续派出援军 {createdCount} 支 → {target.Name}");

            // 恢复本城任务状态,避免影响后续 AIAttack 判断
            city.TroopMissionType = MissionType.None;
            city.TroopMissionTargetId = 0;
            return true;
        }

        /// <summary>
        /// 内政
        /// </summary>
        /// <param name="scenario"></param>
        public static bool AIIntrior(City city, Scenario scenario)
        {
            // 透传真实结果：调度器靠它判断"是否真的做了事"来决定要不要补扣行动力
            return AIBuilding(city, scenario);
        }

        static int[] recommandSearchingFeatrues = new int[] { 86 };
        /// <summary>
        /// AI搜索逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AISearching(City city, Scenario scenario)
        {
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Person) == 1)
                return true;

            int freeCount = city.freePersons.Count;
            if (freeCount == 0)
                return true;

            // 【优化】原先为 (有在野 && Chance(80)) || Chance(20)，两个随机条件叠加，
            // 实际概率约 84% 且难以预期。现改为单一明确概率：
            // 未发现武将越多，搜索意愿越强（每人 +searchInvisibleChancePerPerson，整体封顶 100）。
            AIConfig cfg = AIConfig.Instance;
            int invisible = city.invisiblePersons.Count;
            int chance = cfg.searchBaseChance + invisible * cfg.searchInvisibleChancePerPerson;
            if (chance <= 0)
                return true;

            if (GameRandom.Chance(Math.Min(100, chance)))
            {
                Person[] recommandList = ForceAI.CounsellorRecommendSearching(city.freePersons, city, recommandSearchingFeatrues);
                if (recommandList != null && recommandList.Length > 0)
                {
                    city.JobSearching(recommandList.ToArray());
                }
                else
                {
                    city.JobSearching(city.freePersons.GetRange(0, Math.Min(city.freePersons.Count, invisible)).ToArray());
                }
            }
            return true;
        }

        /// <summary>
        /// AI褒奖武将逻辑。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIRewardPerson(City city, Scenario scenario)
        {
            // 【优化】原先用全局 Chance(80) 决定是否褒奖，并对每个低忠诚武将都执行一次，
            // 可能一次性耗尽金钱。现改为：仅在"确有低忠诚武将"时确定执行，且单回合限制人数。
            AIConfig cfg = AIConfig.Instance;

            // 【流言响应】被 AI 互喷打低忠诚的武将优先褒奖，并临时放宽单回合褒奖人数，
            // 让 AI 有能力与被流言削弱的速度赛跑（参数见 ScenarioVariables.cityStrategyRumorVictimReward*）。
            ScenarioVariables variables = scenario != null ? scenario.Variables : null;
            int victimTurns = variables != null ? variables.cityStrategyRumorVictimRewardTurns : 0;
            int victimBoost = variables != null ? variables.cityStrategyRumorVictimRewardBoost : 0;
            int now = scenario != null ? scenario.TurnCount : 0;
            bool rumorResponseOn = victimTurns > 0 && victimBoost > 0;

            // 先筛出"最近被流言动摇且忠诚已低于褒奖线"的人，作为第一优先序列
            int maxReward = Math.Max(1, cfg.rewardMaxPersonPerTurn);
            List<Person> victims = null;
            if (rumorResponseOn)
            {
                for (int i = 0; i < city.allPersons.Count; i++)
                {
                    Person person = city.allPersons[i];
                    if (person == null || person.mBelongTroop != null)
                        continue;
                    if (person.loyalty > cfg.rewardLoyaltyThreshold)
                        continue;
                    if (!IsRumorVictim(person, now, victimTurns))
                        continue;
                    victims = victims ?? new List<Person>();
                    victims.Add(person);
                }
            }

            int rewarded = 0;
            // 第一轮：确有流言受害者时才放宽人数上限，并优先补他们的忠诚（忠诚最低者优先）
            if (victims != null && victims.Count > 0)
            {
                maxReward += victimBoost;
                victims.Sort((a, b) => a.loyalty.CompareTo(b.loyalty));
                for (int i = 0; i < victims.Count; i++)
                {
                    if (rewarded >= maxReward || city.gold <= cfg.rewardGoldKeep)
                        break;
                    city.JobRewardPerson(victims[i]);
                    rewarded++;
                }
            }

            // 第二轮：其余低忠诚武将照旧补位（跳过第一轮已处理的流言受害者，避免重复褒奖同一人）
            for (int i = 0; i < city.allPersons.Count; i++)
            {
                if (rewarded >= maxReward || city.gold <= cfg.rewardGoldKeep)
                    break;
                Person person = city.allPersons[i];
                if (person == null || person.mBelongTroop != null)
                    continue;
                if (rumorResponseOn && IsRumorVictim(person, now, victimTurns))
                    continue;
                if (person.loyalty > cfg.rewardLoyaltyThreshold)
                    continue;

                city.JobRewardPerson(person);
                rewarded++;
            }
            return true;
        }

        /// <summary>
        /// 该武将是否"最近被流言动摇"（用于 AI 褒奖优先序列）。
        /// </summary>
        /// <param name="person">候选武将</param>
        /// <param name="now">当前回合数</param>
        /// <param name="victimTurns">标记有效期（回合）</param>
        /// <returns>在有效期内返回 true</returns>
        private static bool IsRumorVictim(Person person, int now, int victimTurns)
        {
            return person != null && person.lastRumorTurn > 0 && now - person.lastRumorTurn <= victimTurns;
        }

        /// <summary>
        /// AI招募武将逻辑。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIRecruitPerson(City city, Scenario scenario)
        {
            if (city.wildPersons.Count == 0 || city.freePersons.Count == 0)
                return true;

            // 【优化】原先对每个在野武将都尝试招募，会一次性占用全部空闲武将；
            // 现限制单回合招募人数，把机会留给后续回合。
            int recruited = 0;
            int maxPerTurn = Math.Max(1, AIConfig.Instance.recruitPersonMaxPerTurn);

            // 【在野驱动】在野越多，单回合登用越多 —— 直接按"在野人数"调整调用登用命令的密度
            // （上限仍受 maxPerTurn 的基数与 scoreRecruitPersonWildBonusMax 之类的封顶约束，
            //   避免一回合把空闲武将全占满）。
            int wildCount = city.wildPersons.Count;
            if (wildCount >= 4)
            {
                int byWild = (int)Math.Ceiling(wildCount * 0.5);      // 在野的一半，向上取整
                if (byWild > maxPerTurn)
                    maxPerTurn = byWild;
            }

            // 【前期优先】开局前 N 回合再放宽 1 个名额
            // （原来读 DevploymentState.currentForceTurn，该状态随人才部署系统一并移除，
            //   改用剧本回合数 —— 与原版"开局抢人才"的时点判断同源）
            int forceTurn = scenario != null ? scenario.TurnCount : 0;
            if (forceTurn > 0 && forceTurn <= AIConfig.Instance.cityOrder.recruitPersonEarlyTurns)
                maxPerTurn++;
            for (int i = 0; i < city.wildPersons.Count; i++)
            {
                Person target = city.wildPersons[i];
                if (target == null)
                    continue;

                Person recommandPerson = ForceAI.CounsellorRecommendRecruitPerson(city.freePersons, target, null);
                if (recommandPerson == null)
                    continue;

                city.JobRecruitPerson(recommandPerson, target);
                recruited++;
                if (recruited >= maxPerTurn)
                    break;
            }
            return true;
        }

        // ==================== 城池运输已移交资源调度 ====================
        // 原先的 AITransfrom（非边境城 → 一环邻城，按本城库存比例发货）与
        // AITransfromToBelongCity（港关 → 归属都市）已删除，原因：
        //   ① 只看得到一环邻城，没有全域缺口视图，做不到"前线优先"；
        //   ② 按本城固定比例发货，不看目标城的在途量，货到了常常超过上限被丢掉；
        //   ③ 港关与都市各运各的，互相不知情。
        // 现在统一由 ResourceDispatcher 以**势力 / 军团**为单位调度（见 Game/Object/Dispatch）：
        // 圈层水位（前线防御/进攻 → 次前线支援 → 后方储备）、在途记账、安全余量、
        // 溢出安抚、以及"人员需求交给人才调度"都在那里。
        // 本文件只保留 AIMakeTransportTroop（组建运输队）与 IsPathClear 两个被复用的工具方法。

        // 港关 → 归属都市的运输（原 AITransfromToBelongCity）同样已移交给资源调度：
        // 港关与归属都市现在是同一个资源单元（BFS 连通 + 调运成本优惠），
        // "港关富余上交都市 / 都市缺货从自家港关调 / 港关缺人时从都市补人"都由 ResourceDispatcher 统一处理，
        // 且不再依赖"向归属城市运输"这一条只对港关生效的专用路径。


        /// <summary>
        /// 检查两城之间的直线通路是否畅通（途经的敌方建筑会阻断运输）。
        ///
        /// 【为什么是 internal】资源调度（<c>ResourceTransfer.Ship</c>）也要用同一条判定：
        /// 旧的城池运输命令一直会先检查通路再派车，换成资源调度后必须保留，
        /// 否则运输队会被半路吃掉、整车物资全丢。
        /// </summary>
        /// <param name="city">出发城市</param>
        /// <param name="target">目标城市</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>通路是否畅通</returns>
        internal static bool IsPathClear(City city, City target, Scenario scenario)
        {
            tempCellList.Clear();
            scenario.Map.GetDirectPath(city.CenterCell, target.CenterCell, tempCellList);
            for (int i = 0; i < tempCellList.Count; ++i)
            {
                Cell road = tempCellList[i];
                if (road.building != null && !road.building.IsCity() && !road.building.IsSameForce(city))
                    return false;
            }
            return true;
        }

        public static bool AIBuilding(City city, Scenario scenario)
        {
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Build) == 1)
                return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
            {
                if (GameRandom.Chance(50))
                    return true;
            }

            // 【一回合可建多座，但每座各扣一次 AP】
            // 原版 `kyoten_develop`（s11_game_ai_kyoten_cmd.cpp:100-147）单次调用只建一座，
            // 但**同一回合会被多次调用**：`exec_develop_command` 会给每个参与者
            // `person_set_action_done(person, true)`（s11_sys_person_order_city.cpp:108），
            // 用掉 1~3 名武将；只要队列里还有 Develop、还有空闲武将、AP 还够，就会继续建。
            // 真正的限制是"武将行动力 + AP 余额"，不是"一回合一座"。
            // <para>
            // 本项目队列里 `Develop` 只登记一次，若命令体也硬限 1 座，
            // 等于把"10 个空闲武将 + 满行动力"的城压成每回合 1 座，明显不合理。
            // </para>
            // <para>
            // 【AP 控制】每建一座立刻扣一次 `JobType.GetJobCostAP(Build)`，
            // 扣完后调 `MarkExecuted()` 让调度器跳过"委托型补扣"，避免重复扣减。
            // 升级仍走独立命令 `AIOrderId.Merge → AIMerge`，
            // 军事建筑仍走 `AIOrderId.Build → AIBuildMilitaryBuilding`，各自独立记账。
            // </para>
            int cost = JobType.GetJobCostAP((int)CityJobType.Build);
            int built = 0;
            const int MaxPerCommand = 3;      // 软上限：单条命令最多建 3 座，避免霸占整个队列

            bool logBuild = AIConfig.Instance.logOutpostAI;
            while (built < MaxPerCommand)
            {
                if (city.freePersons.Count <= 0)
                {
                    if (logBuild)
                        Sango.Log.Info($"*[内政建造]{city.Name} 跳过：无空闲武将（built={built}）");
                    break;
                }
                if (cost > 0 && OutpostRuntime.actionPoint < cost)
                {
                    // 这是"前线城/后方城有钱有人却一直空着"的最常见原因：
                    // Attack 排在队列最前，连出多支把据点行动力吃光，轮到 Develop 时已无额度。
                    if (logBuild)
                        Sango.Log.Info($"*[内政建造]{city.Name} 跳过：据点行动力 {OutpostRuntime.actionPoint} < 单座成本 {cost}（built={built}）");
                    break;
                }
                if (!AIBuildIntriore(city, scenario))
                {
                    if (logBuild)
                    {
                        int goldNow = OutpostRuntime.gold != 0 ? OutpostRuntime.gold : city.gold;
                        Sango.Log.Info($"*[内政建造]{city.Name} 跳过：AIBuildIntriore 判定不该建"
                            + $"（城池金 {city.gold}，据点可用金 {goldNow}，空闲武将 {city.freePersons.Count}，"
                            + $"内政地已满 {city.IsInteriorBuildFull()}，据点状态 {OutpostRuntime.Get(city.Id)?.state}）");
                    }
                    break;
                }

                OutpostRuntime.actionPoint -= cost;
                built++;
            }

            if (built > 0)
            {
                // 已按"一座一成本"逐次扣减 → 通知调度器不要再补扣
                OutpostRuntime.MarkExecuted();
                return true;
            }
            return false;
        }


        static int[] buildTypes = new int[] { (int)BuildingKindType.ArrowTower, (int)BuildingKindType.Camp, 11, 12, 13 };
        static int[] build_weight = new int[] { 50, 40, 20, 20, 20 };
        /// <summary>
        /// AI建造军事建筑逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIBuildMilitaryBuilding(City city, Scenario scenario)
        {
            // 【军团设置 · 建筑控制】关闭时禁止建造军事建筑（箭楼 / 阵 / 军事府等）。
            // 该开关只管两件事：**删除内政设施** 与 **建造军事建筑**；
            // 内政设施的新建与升级不受它约束。
            if (city.BelongCorps != null
                && city.BelongCorps.GetAppointValue(Corps.AppointContentType.Build) == 1)
                return true;

            if (city.areaCellList.Count == 0)
                return true;

            // 【规则】内政地未建设满时，不允许建设城外的军事设施：
            // 该拦截同时覆盖"辖区内军事建筑"与"前线军事建筑"（TryBuildFrontBuilding
            // 只在通过本检查后才会被调用），保证 AI 先把内政地建满再考虑军事工事。
            if (!city.IsInteriorBuildFull())
                return true;

            if (city.freePersons.Count < 1)
                return true;

            if (city.gold < 2000)
                return true;

            if (city.troops < 10000)
                return true;

            if (city.food < 10000)
                return true;

            //建筑概率 40%
            if (GameRandom.Chance(60))
                return true;

            // 最大允许两只建设队伍
            int buildMax = 1;
            for (int i = 0; i < city.allPersons.Count; i++)
            {
                Person person = city.allPersons[i];
                Troop checkTroop = person.mBelongTroop;
                if (checkTroop != null)
                {
                    if (checkTroop.missionType == (int)MissionType.TroopBuildBuilding)
                    {
                        buildMax--;
                        if (buildMax == 0)
                            return true;
                    }
                }
            }

            // 先统计已经去人建造的地块
            int buildSpace = Math.Max(1, Scenario.Cur.Variables.BuildingSpace);
            List<Cell> troop_dst_cell = new List<Cell>();
            for (int m = 0; m < city.allTroops.Count; m++)
            {
                Troop t = city.allTroops[m];
                if (t.missionType == (int)MissionType.TroopBuildBuilding)
                {
                    troop_dst_cell.Add(t.missionTargetCell);
                }
            }
            // 【前线战略建筑】优先尝试把辅助建筑修到真正的前沿战区，
            // 突破"只能建在本城辖区(areaCellList)"的限制。
            // 若前线暂无可建点（如全面劣势 / 钱不够），自动回退到下面的旧逻辑。
            // 军团委任 · 禁止攻击 / 全局和平期：不向前线派工程队（同属"向外用兵"）；
            // 下面"辖区内的本地建造"仍然保留，那是本城自己的建设，不算对外用兵。
            if (!IsAttackForbidden(city) && !scenario.IsPeacePeriod
                && AIConfig.Instance.useFrontBuilding
                && TryBuildFrontBuilding(city, scenario, troop_dst_cell))
                return true;

            Cell dest = null;
            for (int i = 0; i < city.areaCellList.Count; i++)
            {
                Cell c = city.areaCellList[i];
                if (!c.CanBuild || !c.IsEmpty() || c.IsInterior || c.SpiralHasBuilding(buildSpace))
                    continue;
                if (troop_dst_cell.Contains(c))
                    continue;

                bool toNear = false;
                for (int j = 0; j < troop_dst_cell.Count; j++)
                {
                    if (c.Distance(troop_dst_cell[j]) < buildSpace)
                    {
                        toNear = true;
                        break;
                    }
                }

                if (toNear) { continue; }

                // 找到能建造的地方
                dest = c;
            }

            if (dest == null)
                return true;

            int randomType = buildTypes[GameRandom.RandomWeightIndex(build_weight, 150)];

            BuildingType buildingType = city.BelongForce.canBuildMilitaryBuildingType.Find(x => x.kind == randomType);
            if (buildingType == null)
                return true;

            return DispatchBuildingTroop(city, scenario, dest, buildingType);
        }

        /// <summary>
        /// 【前线战略建筑】在城池周边的战区中评选最优建址，并派遣工程队前往修建。
        ///
        /// 与旧逻辑（<see cref="AIBuildMilitaryBuilding"/> 内的辖区遍历）的差别：
        ///   · 扫描范围：以城池为中心 <c>frontBuildSearchRange</c> 格，覆盖真正的前沿
        ///   · 选点方式：用 <see cref="BattleSituation.EvaluateFrontSite"/> 综合评分取最优，
        ///     而非"辖区里随便挑一个"
        ///   · 选型方式：用 <see cref="BattleSituation.SelectFrontBuildingType"/> 依战场态势决定，
        ///     而非纯权重随机
        /// </summary>
        /// <param name="city">出兵城池</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="occupiedCells">已有工程队在建设中的目标格（避免重复派遣）</param>
        /// <returns>是否成功派出工程队</returns>
        static bool TryBuildFrontBuilding(City city, Scenario scenario, List<Cell> occupiedCells)
        {
            AIConfig cfg = AIConfig.Instance;
            Cell origin = city.CenterCell;
            Force force = city.BelongForce;
            if (origin == null || force == null || scenario == null || scenario.Map == null)
                return false;

            // 本势力当前没有可建的军事建筑（科技未解锁等）时直接放弃
            if (force.canBuildMilitaryBuildingType == null || force.canBuildMilitaryBuildingType.Count == 0)
                return false;

            BattleSituation.FrontSiteInfo best = new BattleSituation.FrontSiteInfo();
            best.Clear();

            int range = Math.Max(1, cfg.frontBuildSearchRange);
            scenario.Map.SpiralAction(origin, range, (cell) =>
            {
                // 跳过已有工程队在建的格子，避免两队抢同一位置
                if (occupiedCells != null && occupiedCells.Contains(cell))
                    return;

                BattleSituation.FrontSiteInfo info =
                    BattleSituation.EvaluateFrontSite(cell, force, scenario);
                if (!info.isValid || info.cell == null)
                    return;

                if (best.cell == null || info.score > best.score)
                    best = info;
            });

            if (!best.isValid || best.cell == null)
                return false;

            // 【陷阱选型对齐原版】原版陷阱由「部队（self）」在行动时自主建造（add_build_action :1253-1267），
            // 城池派建造部队（kyoten_build → DeployType_Build）只负责派出、不选型，选型发生在部队到达后。
            // 故此处（派队前尚无 self）builder 传 null 跳过陷阱选型，陷阱改由
            // 「作战部队就地增筑」（Troop.TryBuildFieldBuilding）与「工程队连建」（TroopBuildBuilding.TryFindNextSite）触发。
            BuildingType buildingType = BattleSituation.SelectFrontBuildingType(best, null, force, scenario);
            if (buildingType == null)
                return false;

            return DispatchBuildingTroop(city, scenario, best.cell, buildingType);
        }

        /// <summary>
        /// 组建并派出一支"工程队"（携带资金前往指定格修建指定建筑）。
        ///
        /// 【资金】携带额优先取 <c>frontBuildBudget</c>；为 0 时按
        /// "目标建筑造价 × frontBuildGoldCostPercent%" 推算，
        /// 使工程队一次出行能连续修建多座建筑（配合 <c>frontBuildContinueAfterDone</c>）。
        /// 出征时从城池金库实际扣除，受城池余额限制。
        /// </summary>
        /// <param name="city">出兵城池</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="dest">目标建造格（建筑的邻居格之一，部队将落脚于此）</param>
        /// <param name="buildingType">要修建的建筑类型</param>
        /// <returns>是否成功派出</returns>
        static bool DispatchBuildingTroop(City city, Scenario scenario, Cell dest, BuildingType buildingType)
        {
            if (dest == null || buildingType == null)
                return false;

            AIConfig cfg = AIConfig.Instance;

            // 建址在派遣期间被占用（例如被别的部队占据）则放弃。
            // 【口径统一】与 BattleSituation.EvaluateFrontSite / BuildingType.CanBuildToHere 一致：
            // 只避让军事建筑 / 爆炸物，允许紧挨障碍物与内政建筑；
            // 否则会出现"评估判定可建、派遣却被拒"的不一致。
            if (!dest.CanBuild || !dest.IsEmpty() || dest.IsInterior
                || dest.SpiralHasBuilding(Math.Max(1, scenario.Variables.BuildingSpace),
                    (b) => b.BuildingType != null && !b.BuildingType.IsObstacle && !b.BuildingType.IsIntrior))
                return false;

            TroopType troopType = scenario.GetObject<TroopType>(1);

            // 组建修建队伍
            Person[] builders = ForceAI.CounsellorRecommendBuild(city.freePersons, buildingType);
            if (builders == null || builders.Length == 0)
                return false;

            int maxTroopNum = 3000;
            int food = (int)(maxTroopNum * scenario.Variables.baseFoodCostInTroop * 20);

            // 【军事府：领地内建军事设施 8 折】
            // 对齐原版 get_facility_gold_cost（s11_sys_scen_unit.cpp:1607-1614）：
            // 部队所属势力在本城领地里建军事设施时，造价 × 8/10。
            int effectiveCost = buildingType.cost;
            if (CityFacilityRequirement.HasGovFacility(city, InteriorFacilityKind.MilitaryOffice))
                effectiveCost = effectiveCost * 8 / 10;

            // 资金：固定额度优先，否则按造价倍率推算
            int carrayGold = cfg.frontBuildBudget > 0
                ? cfg.frontBuildBudget
                : effectiveCost * Math.Max(100, cfg.frontBuildGoldCostPercent) / 100;
            if (carrayGold > city.gold)
                carrayGold = city.gold;
            // 连一座都建不起就没必要出征
            if (carrayGold < effectiveCost)
                return false;

            city.troops -= maxTroopNum;
            city.food -= food;
            city.gold -= carrayGold;
            for (int p = 0; p < builders.Length; p++)
            {
                Person person = builders[p];
                if (person != null)
                {
                    city.freePersons.Remove(person);
                }
            }

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            troop.Leader = builders[0];
            troop.TroopType = troopType;
            troop.troops = maxTroopNum;
            troop.food = food;
            troop.gold = carrayGold;
            troop.missionType = (int)MissionType.TroopBuildBuilding;
            troop.missionTargetCell = dest;
            // 【意图层】工程队对应原版 DeployType_Build（kyoten_cmd_mil_build.cpp:112-114），
            // deploy_target 是出发据点（原版同样传 building_id）。
            troop.deployType = (int)TroopDeployType.Build;
            troop.deployTarget = city.Id;
            // 【角色】显式标记为工兵：EmitTroop 只在 role == Auto 时才自动推导，
            // 因此这里设定后不会被覆盖，评分体系会按工兵权重使其极力规避接战。
            troop.role = TroopRole.Engineer;
            if (builders.Length > 1) troop.Member1 = builders[1];
            // 【修正】原实现此处误写为 Member1，导致第二名副将被丢弃
            if (builders.Length > 2) troop.Member2 = builders[2];
            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            if (troop == null)
                return true;                        // 城池被围困,无法出城,本次出征已取消
            city.CurActiveTroop = troop;
            troop.SetMission(MissionType.TroopBuildBuilding, buildingType.Id);
            // 【立即出城】工程队不能停在城头等下一回合（对齐 com_deploy 里的 run_order）
            if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
            {
                Sango.Log.Warning($"{city.BelongForce?.Name}在{city.Name}组建的工程队无法出城（城头无可停留格或寻路失败），本次出征取消");
                CityTroopFactory.RevokeEmit(city, troop);
                return true;
            }
            return true;
        }

        public static int[][] CityBuildingTemplate = new int[][] {
            // 后方城市
            new int[] {
                // 基础建筑
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.MilitaryOffice,
               // (int)BuildingKindType.TrainTroopBuilding,
                (int)BuildingKindType.RecruitBuilding,
                (int)BuildingKindType.Farm,// 10小城

                (int)BuildingKindType.MechineFactory,
                (int)BuildingKindType.Market, // 12小城

                (int)BuildingKindType.Stable,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 16中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.BoatFactory,
                (int)BuildingKindType.Farm,// 20中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 24大城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 28巨巨城
            },

            // 边境城市
            new int[] {
                // 基础建筑
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.MilitaryOffice,
               // (int)BuildingKindType.TrainTroopBuilding,
                (int)BuildingKindType.RecruitBuilding,
                (int)BuildingKindType.Farm,// 10小城

                (int)BuildingKindType.MechineFactory,
                (int)BuildingKindType.Market, // 12小城

                (int)BuildingKindType.Stable,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 16中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.BoatFactory,
                (int)BuildingKindType.Farm,// 20中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 24大城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 28巨巨城
            },

            // 后方港口城市
            new int[] {
                // 基础建筑
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.MilitaryOffice,
                //(int)BuildingKindType.TrainTroopBuilding,
                (int)BuildingKindType.RecruitBuilding,
                (int)BuildingKindType.Farm,// 10小城

                (int)BuildingKindType.MechineFactory,
                (int)BuildingKindType.BoatFactory, // 12小城

                (int)BuildingKindType.Stable,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 16中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Farm,// 20中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 24大城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 28巨巨城
            },

             // 边境港口城市
            new int[] {
                // 基础建筑
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.MilitaryOffice,
               // (int)BuildingKindType.TrainTroopBuilding,
                (int)BuildingKindType.RecruitBuilding,
                (int)BuildingKindType.Farm,// 10小城

                (int)BuildingKindType.MechineFactory,
                (int)BuildingKindType.BoatFactory, // 12小城

                (int)BuildingKindType.Stable,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 16中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 20中城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 24大城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 28巨巨城
            },
        };

        /// <summary>
        /// AI建造内政建筑逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIBuildIntriore(City city, Scenario scenario)
        {
            if (city.freePersons.Count < 1) // 至少需要1个武将进行建设
                return false;

            // 【金门槛 · 严格对齐】原版 s11_game_ai_kyoten_cmd.cpp:96-98：
            // <code>
            // // 금 부족
            // if (gold_ &lt; 0)
            //     return true;
            // </code>
            // gold_ 是**据点可用金**（库存 + 负收入按储备回合折抵，可为负），原版只要求它"非负"，
            // 并没有"够某个最低值才建"的门槛。
            // 【旧实现】用 city.gold &lt; buildMinGold(500) 作门槛，比原版严：钱在 0~499 时原版会建、本项目不建。
            // 买不起的具体类型由选型阶段的 check_gold_cost 剔除
            //（本项目 = BuildingPlanner.ClearUnaffordable + BuildOne 的造价检查），无需在此一刀切。
            int availableGold = OutpostRuntime.gold != 0 ? OutpostRuntime.gold : city.gold;
            if (availableGold < 0)
                return false;

            // 【"建址 3 格内有敌"的判定落在 BuildOne，本方法不再重复判】
            //
            // 原版 kyoten_develop（s11_game_ai_kyoten_cmd.cpp:83-121）的完整顺序是：
            //   ① 无闲人 → 不建；② gold_ < 0 → 不建；③ 行动力不足 → 不建；
            //   ④ 内政地已满 → 不建；⑤ 有防守部队出阵 → 30% 不建；
            //   ⑥ 选类型 → 选位置 → 位置非法 → 不建；
            //   ⑦ **map_has_enemy(pos, 3) && rand_bool(50) → 不建**（判的是建址，不是据点）。
            //
            // 其中 ⑦ 本项目已由唯一落地入口 <see cref="BuildOne"/> 严格实现
            // （`city.HasEnemyInRange(bestPlace, 3) && GameRandom.Chance(50)`），
            // 且模板路径与规划器路径共用同一份代码，不会漏判。
            //
            // 【此前上层那道"Battle 态禁建"已删除】它把判据从"建址"放宽成"整城"，
            // 比原版严格得多：前线城长期 Battle，内政地会永久空置。
            //
            // 仅保留原版确实存在的 ⑤：有防守部队已出阵时 30% 概率不建 —— :105-107
            int defenseTroops = 0;
            for (int i = 0; i < city.allTroops.Count; i++)
            {
                Troop t = city.allTroops[i];
                if (t != null && t.IsAlive && t.missionType == (int)MissionType.TroopProtectCity)
                    defenseTroops++;
            }
            if (defenseTroops > 0 && GameRandom.Chance(30))
                return false;

            // ---------- 一、还有空位：直接新建 ----------
            if (!city.IsInteriorBuildFull())
            {
                // 【动态规划 · 规划器启用时完全接管】
                // 注意：规划器返回 null 表示"当前没有需求"（钱粮已够 / 产出建筑名额已满 …），
                // 这时**不建才是正确行为**，绝不能回退到旧模板 ——
                // 否则等于把"按需求建"又变回"按固定队列建"，需求之外还会硬塞建筑。
                if (BuildingPlanner.IsEnabled)
                {
                    BuildingType planned = BuildingPlanner.PickNextBuilding(city, scenario);
                    return planned != null && BuildOne(city, scenario, planned);
                }

                // 【回退】规划器未启用 → 沿用旧的固定模板
                int templateId = 0;
                if (city.IsBorderCity)
                {
                    templateId = city.portList.Count > 0 ? 3 : 1;
                }
                else
                {
                    templateId = city.portList.Count > 0 ? 2 : 0;
                }

                // 执行建设
                return AIBuildingTemplate(templateId, city, scenario);
            }

            // ---------- 二、内政地已建满 ----------
            // 【对齐原版：建满时**不在建造流程里拆任何东西**】
            //
            // 原版是两条**独立命令**，靠"空地"这个中间状态解耦（s11_game_ai_kyoten_cmd.cpp:891-971）：
            //   · `kyoten_destroy`：建满时只拆"废设施" —— 铸币厂周边无市场、谷仓周边无农田，
            //     或内政未达最低配置时按固定优先级表挑一座；**拆完立即结束，不在同一函数里建新的**。
            //   · `dev_ai`：下一次执行时**发现有平地**才按需求权重建一座。
            //
            // 本项目原先走 `TryReplaceBuilding`：规划器一次算出"拆 A 建 B"、同函数内先拆后建。
            // 这条路径是自创的（原版没有），且与 `BuildingPlan` 的需求模型耦合，
            // 会绕开原版"只拆废设施"的判定 —— 表现为"城里建筑凭空少一座"。
            //
            // 现在改为直接返回 false，把拆除完全交回 `AIOrderId.Destroy`（其判定已对齐原版），
            // 建满且无废设施时就是**什么都不做**，与原版一致。
            return false;
        }

        /// <summary>
        /// 执行一次"拆旧建新"：先由规划器判定（拆哪座、建哪座），再由本方法落地。
        ///
        /// 【为什么判定与执行分家】规划器只回答"该不该换、换哪两座"，**不碰建筑表** ——
        /// 拆除会修改 <c>city.allBuildings</c>（OnBuildingDestroy → Remove），
        /// 必须在遍历之外进行：SangoObjectList.ForEach 是倒序索引循环，
        /// 在其中删除"非当前元素"会重复处理甚至越界。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成了一次更换</returns>
        public static bool TryReplaceBuilding(City city, Scenario scenario)
        {
            // 【军团设置 · 建筑控制】"拆旧建新"包含"删除内政设施"这一步，
            // 因此与 `AIDestroy` 一样受该开关约束（开关只管"删除内政"与"建造军事建筑"，
            // 不含内政的新建与升级 —— 那两条走的是别的分支）。
            if (city.BelongCorps != null
                && city.BelongCorps.GetAppointValue(Corps.AppointContentType.Build) == 1)
                return false;

            Building outgoing;
            BuildingType incoming;
            if (!BuildingPlanner.TryPlanReplace(city, scenario, out outgoing, out incoming))
                return false;

            // 先拆：OnFall 会移除建筑、释放工人、维护 buildingCountMap 并重算产出。
            // 注意它不退还建造花费 —— 这正是"更换的代价"。
            // 【必须打日志】这条路径此前**静默拆除**：`OnFall` 不产生任何记录，
            // 玩家只会看到"城里的建筑凭空少了一座"，却查不到是谁拆的、为什么拆。
            if (AIConfig.Instance.logOutpostAI)
            {
                Sango.Log.Info($"*[据点AI]{city.Name} 拆除 {outgoing.Name}"
                    + $"（为改建 {incoming.Name} 腾位）");
            }
            outgoing.OnFall(null);

            // 再建：此时内政地已腾出一格。
            if (!BuildOne(city, scenario, incoming))
            {
                // 预检（空闲武将 / 金钱 / 有空位）已保证此处基本不会失败；
                // 万一失败也只是白拆一座 —— 刻意**不占用冷却**，下一轮会补建。
                return false;
            }

            // 只有真正拆建成功才登记冷却
            BuildingPlanState.MarkReplaced(city.Id, scenario.TurnCount);

            // 调试日志（与建筑规划器同一取舍：Info(object) / Warning 带
            // [Conditional("SANGO_DEBUG")] 会被静默丢弃，所以用带 LogType 的 Info 重载）
            Sango.Log.Info(string.Format("[建筑规划] {0}({1}) 更换建筑: 拆 {2} → 建 {3}",
                city.Name,
                city.BelongForce != null ? city.BelongForce.Name : "-",
                outgoing.Name, incoming.Name), Sango.Log.LogType.None);

            return true;
        }

        /// <summary>
        /// AI根据模板建造建筑逻辑
        /// </summary>
        /// <param name="templateId">模板ID</param>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIBuildingTemplate(int templateId, City city, Scenario scenario)
        {
            // 新建路径无最低金门槛，只要求据点可用金非负 ——
            // 同 AIBuildIntriore，对齐原版 kyoten_develop:96-98 `if (gold_ < 0) return true;`
            int availableGold = OutpostRuntime.gold != 0 ? OutpostRuntime.gold : city.gold;
            if (availableGold < 0)
                return false;

            if (city.freePersons.Count < 1)
                return false;

            Dictionary<int, int> buildingCountMap = new Dictionary<int, int>();
            foreach (Building building in city.allBuildings)
            {
                if (buildingCountMap.ContainsKey(building.BuildingType.kind))
                {
                    buildingCountMap[building.BuildingType.kind]++;
                }
                else
                {
                    buildingCountMap[building.BuildingType.kind] = 1;
                }
            }

            int[] building_list = CityBuildingTemplate[templateId];
            int[] buildingFlag = new int[city.InteriorCellCount];

            // 先排除确定的建筑
            for (int i = 0; i < city.InteriorCellCount; i++)
            {
                int buildKindId = i < building_list.Length ? building_list[i] : 0;
                if (buildKindId > 0)
                {
                    if (buildingCountMap.TryGetValue(buildKindId, out int num) && num > 0)
                    {
                        buildingCountMap[buildKindId] = num - 1;
                        buildingFlag[i] = buildKindId;
                    }
                }
            }

            // 再排除不确定的建筑
            for (int i = 0; i < city.InteriorCellCount; i++)
            {
                int buildTypeId = i < building_list.Length ? building_list[i] : 0;
                if (buildTypeId == 0)
                {
                    bool findAny = false;
                    foreach (int buildKindId in buildingCountMap.Keys)
                    {
                        if (buildingCountMap[buildKindId] > 0)
                        {
                            buildingCountMap[buildKindId]--;
                            buildingFlag[i] = buildKindId;
                            findAny = true;
                            break;
                        }
                    }
                    if (findAny)
                        continue;
                }
            }

            // 修建未入坑的
            for (int i = 0; i < city.InteriorCellCount; i++)
            {
                if (buildingFlag[i] > 0)
                    continue;

                int buildKindId = i < building_list.Length ? building_list[i] : 0;
                BuildingType buildingType = scenario.GetObject<BuildingType>(buildKindId);
                if (buildingType == null || buildingType.Id == 0)
                {
                    buildingType = scenario.GetObject<BuildingType>(GameRandom.Range((int)BuildingKindType.Market, (int)BuildingKindType.MilitaryGarrison + 1));
                }

                if (city.gold < buildingType.cost)
                    return false; // 金钱不够，后面也不用再试

                // 落地逻辑统一走 BuildOne（与动态规划路径共用同一份实现）
                if (BuildOne(city, scenario, buildingType))
                    return true; // 成功建设
            }

            return false; // 没有建设
        }

        /// <summary>
        /// 按指定建筑类型落地一次建造
        /// （选位 → 荐人 → 算工期 → 性格修正 → 扣钱扣行动点 → 挂建造任务）。
        ///
        /// 【为什么抽出来】这是建造的**唯一下沉点** —— 动态规划路径（<see cref="BuildingPlanner"/>）
        /// 与回退的固定模板路径都走它，避免两套落地逻辑漂移
        /// （比如只有一边做了性格工期修正，另一边漏了）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="buildingType">要建的内政设施类型</param>
        /// <returns>是否成功挂上建造任务</returns>
        public static bool BuildOne(City city, Scenario scenario, BuildingType buildingType)
        {
            if (city == null || scenario == null || buildingType == null)
                return false;

            // 【建造合法性兜底】这是 AI 建造的唯一下沉点，规划器（BuildingPlanner）、
            // 原版权重表（PickNextBuildingByOutpostRule）与旧固定模板三条路都汇到这里，
            // 但只有规划器的候选扫描调过 CanBuildInteriorAt —— 权重表与旧模板都可能
            // 交进"本城不该建"的类型（典型：内陆城的鱼市）。
            // 原版每次落地都要过 can_build_facility（s11_sys_scen_city.cpp:276-280，
            // 鱼市要求 `city->has_port()`），这里补上同一道闸。
            if (!buildingType.CanBuildInteriorAt(city))
                return false;

            if (city.gold < buildingType.cost)
                return false;

            Cell bestPlace = buildingType.GetBestPlace(city);
            if (bestPlace == null)
                return false;

            // 【对齐原版 kyoten_develop（s11_game_ai_kyoten_cmd.cpp:119-121）】
            // 建设位周边 3 格有敌人 → 50% 概率放弃本次建设（map_has_enemy(pos, 3) && rand_bool(50)）。
            // 命令层 CityBuildBuilding.IsValid 还会用 HasEnemyNearby 硬拦截，两层各自独立。
            if (city.HasEnemyInRange(bestPlace, 3) && GameRandom.Chance(50))
                return false;

            Person[] people = ForceAI.CounsellorRecommendBuild(city.freePersons, buildingType);
            if (people == null || people.Length == 0)
                return false;

            int buildAbility = GameUtility.Method_PersonBuildAbility(people);
            int turnCount = buildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
            int buildCount = Math.Min(Scenario.Cur.Variables.BuildMaxTurn,
                buildingType.durabilityLimit / buildAbility + turnCount);
            // 【性格】建造效率：按执行武将性格缩短工期
            buildCount = city.ApplyPersonalityBuildCounter(buildCount, people);
            city.JobBuildBuilding(bestPlace, people, buildingType, buildCount);
            return true;
        }

        /// <summary>
        /// AI升级建筑逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIBuildingLevelUp(City city, Scenario scenario)
        {
            // 【升级不设资金闸门】
            // 原版 `kyoten_merge`（s11_game_ai_kyoten_cmd_dev_merge.cpp:19-21）确有一道
            // <code>
            // // 금 여유 없음
            // if (gold_ &lt; 500)
            //     return true;
            // </code>
            // 但那是**合成**（3 座同类设施合成 1 座高级设施）的费用余量，
            // 本项目没有合成这一步（见 <see cref="CityAIOutpostCommand.AIMerge"/> 注释），
            // 升级的真实支出是 `nextBuildingType.cost`。
            // 此前照搬这道 500 金闸门，效果是"城池资金 0~499 时永远不升级"，
            // 加上 `OutpostRuntime.gold` 还会按负收入折抵（s11_game_ai_kyoten.cpp:30-32），
            // 等于门槛实际更高 —— 故移除，改为**逐座**判断买不买得起（见 <see cref="UpgradeOneBuilding"/>）。
            // 扣款前的最终复核仍由 `City.JobUpgradeBuilding`（City.cs:3217）兜底。
            if (city.freePersons.Count < 1)
                return false;

            // 【内政地占用达 4/5 即可升级 —— 对齐原版 kyoten_merge:34-50】
            // 原版（s11_game_ai_kyoten_cmd_dev_merge.cpp）：
            // <code>
            // // 遍历可合并目标时，兵营 / 铁匠铺 / 马厩（含 Lv2）会置 always = true
            // if (not always and city->get_facility_count() &lt; city->get_dev_count() * 4 / 5)
            //     return true;
            // </code>
            // 即：候选里存在可升 3 级的军事内政设施时**不受**这条限制；
            // 否则内政地占用率达到 dev 数的 4/5 即可。
            // 【此前用 IsInteriorBuildFull()（= 100%）】比原版严：本项目没有吸收合并，
            // 内政地只要有一格因敌人阻挡 / 地形限制建不上，升级就永久停摆 —— 属误卡。
            if (!HasMilitaryUpgradeTarget(city))
            {
                int capacity = city.InteriorCellCount;
                if (capacity > 0 && city.GetInteriorCellUsedCount() * 5 < capacity * 4)
                    return false;
            }

            // 【一回合可升多座，但每座各扣一次 AP】—— 与建造 <see cref="AIBuilding"/> 对齐。
            // 原版 `kyoten_merge`（s11_game_ai_kyoten_cmd_dev_merge.cpp:5-98）单次调用只合并一座，
            // 但**同一回合会被多次调用**：每合并一座就 `action_point_ -= get_merge_action_point_cost()`
            // 并 `std::erase(idle_person_list_, actor)`（:94-96），只要队列里还有 Merge、还有空闲武将、
            // AP 还够，就会继续合并。真正的限制是"武将行动力 + AP 余额"，不是"一回合一座"。
            // <para>
            // 本项目队列里 `Merge` 只登记一次（见 <see cref="UpdateOutpostData"/>），
            // 若命令体也硬限 1 座，等于把"多空闲武将 + 满行动力"的城压成每回合 1 座，明显不合理。
            // </para>
            // <para>
            // 【AP 控制】每升一座立刻扣一次 `JobType.GetJobCostAP(UpgradeBuilding)`，
            // 扣完后调 `MarkExecuted()` 让调度器跳过"委托型补扣"，避免重复扣减。
            // </para>
            int apCost = JobType.GetJobCostAP((int)CityJobType.UpgradeBuilding);
            int upgraded = 0;
            const int MaxPerCommand = 3;      // 软上限：单条命令最多升 3 座，避免霸占整个队列

            while (upgraded < MaxPerCommand)
            {
                if (city.freePersons.Count < 1)
                    break;
                if (apCost > 0 && OutpostRuntime.actionPoint < apCost)
                    break;
                if (!UpgradeOneBuilding(city, scenario))
                    break;

                OutpostRuntime.actionPoint -= apCost;
                upgraded++;
            }

            if (upgraded > 0)
            {
                // 已按"一座一成本"逐次扣减 → 通知调度器不要再补扣
                OutpostRuntime.MarkExecuted();
                return true;
            }
            return false; // 没有升级
        }

        /// <summary>
        /// 判断是否存在"军事内政设施"的可升级目标（兵营 / 铁匠铺 / 马厩）。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_dev_merge.cpp:34-44 —— 原版遍历可合并目标时，
        /// 只要命中 <c>FacilityId_Barracks / Smith / Stable</c>（以及三者 Lv2）就置
        /// <c>always = true</c>，随后的 <c>:49-50</c>
        /// 「内政地占用不足 4/5 就放弃」这条判据会被跳过。
        /// <para>
        /// 本项目把三种设施合并为单一 kind（等级靠 <c>BuildingType.level</c> 区分），
        /// 已升到顶级的 <c>nextId == 0</c>，天然被排除，等价于原版"Lv1/Lv2 才算"。
        /// </para>
        /// </summary>
        /// <param name="city">据点</param>
        /// <returns>存在可升级的兵营 / 铁匠铺 / 马厩返回 true</returns>
        static bool HasMilitaryUpgradeTarget(City city)
        {
            if (city.allBuildings == null)
                return false;

            for (int i = 0; i < city.allBuildings.Count; ++i)
            {
                Building b = city.allBuildings[i];
                if (b == null || !b.isComplate || b.isUpgrading || b.BuildingType == null)
                    continue;
                if (b.BuildingType.nextId <= 0)
                    continue;

                int kind = b.BuildingType.kind;
                if (kind == (int)BuildingKindType.Barracks
                    || kind == (int)BuildingKindType.BlacksmithShop
                    || kind == (int)BuildingKindType.Stable)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 升级一座建筑（选建筑 → 荐人 → 算工期 → 性格修正 → 扣钱扣行动点 → 挂升级任务）。
        ///
        /// 单次调用只升级一座；是否继续升级由 <see cref="AIBuildingLevelUp"/> 的循环控制。
        /// 与建造的 <see cref="AIBuildIntriore"/> 一一对应：命令体负责"落地一座"，
        /// 循环体负责"放行次数 + AP 钳制"。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功升级了一座建筑</returns>
        static bool UpgradeOneBuilding(City city, Scenario scenario)
        {
            for (int i = 0; i < city.allBuildings.Count; ++i)
            {
                Building building = city.allBuildings[i];
                if (building.isComplate && !building.isUpgrading && building.BuildingType.nextId > 0)
                {
                    BuildingType nextBuildingType = scenario.GetObject<BuildingType>(building.BuildingType.nextId);
                    if (nextBuildingType == null)
                        continue;

                    // 【买不起这一座就跳过，不整体中止】
                    // 旧实现是 `return false` —— 只要顺序遍历到第一座买不起的建筑，
                    // 后面所有更便宜的升级目标全被放弃，表现为"AI 明明有钱却不升级"。
                    // 真正的扣款复核在 City.JobUpgradeBuilding（City.cs:3217，`gold < cost` 则返回 null），
                    // 此处只是筛掉必然失败的候选，避免刷警告日志。
                    if (city.gold < nextBuildingType.cost)
                        continue;

                    Person[] people = ForceAI.CounsellorRecommendBuild(city.freePersons, nextBuildingType);
                    if (people != null && people.Length > 0)
                    {
                        int buildAbility = GameUtility.Method_PersonBuildAbility(people);
                        if (buildAbility <= 0)
                            continue;
                        int turnCount = nextBuildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
                        int buildCount = Math.Min(Scenario.Cur.Variables.BuildMaxTurn, nextBuildingType.durabilityLimit / buildAbility + turnCount);
                        // 【性格】建造效率：按执行武将性格缩短工期
                        buildCount = city.ApplyPersonalityBuildCounter(buildCount, people);
                        if (buildCount <= AIConfig.Instance.buildUpgradeMaxTurn)
                        {
                            // 【必须判返回值】本次循环前面几座升级可能已经把资金吃光，
                            // JobUpgradeBuilding 复核不过会返回 null；此时不能算"升了一座"，
                            // 否则调用方会白扣一次据点行动力。
                            if (city.JobUpgradeBuilding(building, people, nextBuildingType, buildCount) != null)
                                return true; // 成功升级一座
                        }
                    }
                }
            }
            return false; // 没有可升级的建筑
        }

        /// <summary>
        /// AI招募士兵逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIRecruitTroop(City city, Scenario scenario)
        {
            if (city.freePersons.Count == 0) return true;

            // 轻视士兵,按概率不行为
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Store_Troops) == 1)
            {
                // 重视资金
                if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
                {
                    // 80%概率不行为
                    if (GameRandom.Chance(80))
                        return true;
                }
                else
                {
                    // 轻视资金,60%概率
                    if (GameRandom.Chance(60))
                        return true;
                }
            }

            int totalNum = 0;
            for (int itemTypeId = 2; itemTypeId <= 5; itemTypeId++)
                // 获取总兵装
                totalNum += city.itemStore.GetNumber(itemTypeId);

            // 【P2】个性影响:侵略 / 防御型更积极扩军(降低期望门槛),经济 / 外交型更保守
            ForceAI.AIPersonalityType PersonalityId = GetAIPersonality(city);
            bool militarist = PersonalityId == ForceAI.AIPersonalityType.Aggressive
                           || PersonalityId == ForceAI.AIPersonalityType.Defensive;
            int equipExpectMultiplier = militarist ? 2 : 3;
            int expectationTroops = Math.Max(city.food / 2, totalNum * equipExpectMultiplier / 2);
            if (city.troops >= expectationTroops)
                return true;

            if (city.troops >= city.TroopsLimit)
                return true;

            if (scenario.Variables.populationEnable && city.troopPopulation <= 500) return true;
            if (city.security < 70) return true;
            if (city.troops > city.food) return true;

            Building barracks = city.GetFreeBuilding((int)BuildingKindType.Barracks);
            if (barracks == null) return true;

            Person[] people = ForceAI.CounsellorRecommendRecruitTroop(city.freePersons);
            if (people == null) return true;
            city.JobRecruitTroop(people, barracks);
            return true;
        }

        /// <summary>
        /// AI交易粮食
        /// </summary>
        /// <param name="city"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static bool AITradeFood(City city, Scenario scenario)
        {
            AIConfig cfg = AIConfig.Instance;
            if (city.freePersons.Count <= 0) return true;
            if (city.gold <= cfg.tradeFoodKeepGold) return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
                return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Store_Foood) == 1)
                return true;

            // 【P2】个性影响:经济型更积极囤粮,其余个性保持默认节奏
            ForceAI.AIPersonalityType PersonalityId = GetAIPersonality(city);
            int foodExpectMultiplier = PersonalityId == ForceAI.AIPersonalityType.Economic ? 3 : 2;
            int expectationFood = city.troops * foodExpectMultiplier;
            if (city.food > expectationFood)
                return true;

            Person[] people = ForceAI.CounsellorRecommendTrade(city.freePersons);
            if (people == null) return true;

            city.JobTradeFood(people, (city.gold - cfg.tradeFoodKeepGold) * 2 / 3);
            return true;
        }

        //public void AITroopLevelUp(Scenario scenario)
        //{

        //}

        //public void AITroopMerge(Scenario scenario)
        //{

        //}
        //public void AITroopTrain(Scenario scenario)
        //{

        //}

        /// <summary>
        /// AI是否可以防御
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否可以防御</returns>
        public static bool AICanDefense(City city, Scenario scenario)
        {
            // 【修复】原先此处用 city.IsRoadBlocked() 直接否决防守,而该方法会把城池周围的
            // 己方建筑误判为"道路封锁",导致大城(周边建筑密集)几乎永远不出兵防守。
            // 现在不再以 IsRoadBlocked 否决,只要城池周边存在敌方部队就允许出城防守。

            int alertRange = AIConfig.Instance.cityDefenseAlertRange;

            // 1) 复用城市已预计算的敌人信息(零遍历开销)
            BattleSituation.ThreatSnapshot threat = BattleSituation.EvaluateCityThreat(city);
            if (threat.HasEnemy && threat.nearestDistance <= alertRange)
                return true;

            // 2) 预计算信息未覆盖时(如敌军刚抵达、正在攻击本城建筑),实时扫描城池周边兜底
            BattleSituation.ThreatSnapshot live = BattleSituation.EvaluateThreat(
                city.CenterCell, AIConfig.Instance.cityDefenseScanRange, city, scenario);
            return live.HasEnemy;
        }

        /// <summary>
        /// 获取势力的AI个性
        /// </summary>
        private static ForceAI.AIPersonalityType GetAIPersonality(City city)
        {
            if (city.BelongForce != null)
            {
                if (city.BelongCorps != null)
                    return ForceAI.GetAIPersonality(city.BelongCorps.mComander);
            }
            return ForceAI.AIPersonalityType.Balanced;
        }

        /// <summary>
        /// 军团委任是否"禁止攻击"（<see cref="Corps.AppointContentType.Attack"/> == 1）。
        ///
        /// 【语义】禁的是"**向外用兵**"的全部进攻性行为：
        ///   · 出城攻打敌方城池（<see cref="AIAttack"/>）；
        ///   · 跨城支援友军（<see cref="AIReinforce"/>）；
        ///   · 组建战场补给队送物资上前线（<see cref="AIMakeSupplyTroop"/>）；
        ///   · 派工程队到前线筑垒（<see cref="AIBuildMilitaryBuilding"/> 的"前线战略建筑"部分）。
        ///
        /// **不禁止防御性用兵**：本城 / 邻城被围攻时的守城与回援（TroopProtectCity）、
        /// 驱逐侵入本势力领地的敌军（TroopDestroyTroop）照常执行；
        /// 辖区内（areaCellList）的本地建设也不受影响。
        ///
        /// 判定收敛在这一处，各入口共用，避免规则分裂。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>该城的军团是否禁止对外用兵</returns>
        public static bool IsAttackForbidden(City city)
        {
            if (city == null || city.BelongCorps == null || city.BelongCorps.IsCaptainCorps)
                return false;
            return city.BelongCorps.GetAppointValue(Corps.AppointContentType.Attack) == 1;
        }

        /// <summary>
        /// AI是否可以攻击
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否可以攻击</returns>
        public static bool AICanAttack(City city, Scenario scenario)
        {
            // 全局和平回合数：开局前若干回合内 AI 不主动进攻（统一判定入口见 Scenario.IsPeacePeriod）
            if (scenario.IsPeacePeriod)
            {
                LogAttack(city, "全局和平期，不出兵");
                return false;
            }

            if (IsAttackForbidden(city))
            {
                // 顺带把残留的进攻任务撤掉（判定与各入口共用 IsAttackForbidden，规则不再分裂）
                if (city.TroopMissionType == MissionType.TroopOccupyCity)
                {
                    city.TroopMissionType = MissionType.None;
                    city.TroopMissionTargetId = 0;
                }
                LogAttack(city, "军团委任禁攻，不出兵");
                return false;
            }

            // 获取AI个性
            ForceAI.AIPersonalityType PersonalityId = GetAIPersonality(city);

            // 根据AI个性调整兵力要求（外置到 AIConfig，便于整体调节进攻欲望）
            AIConfig aiCfg = AIConfig.Instance;
            int minTroops = PersonalityId == ForceAI.AIPersonalityType.Aggressive ? aiCfg.attackMinTroopsAggressive :
                           PersonalityId == ForceAI.AIPersonalityType.Defensive ? aiCfg.attackMinTroopsDefensive :
                           aiCfg.attackMinTroopsBalanced;
            if (city.troops < minTroops)
            {
                LogAttack(city, $"兵力不足 {city.troops}<{minTroops}，不出兵");
                return false;
            }

            if (city.morale < 60 || city.security < 50)
            {
                LogAttack(city, $"士气/治安不足 士气{city.morale} 治安{city.security}，不出兵");
                return false;
            }

            if (city.freePersons.Count < 2)
            {
                LogAttack(city, $"空闲武将不足 {city.freePersons.Count}<2，不出兵");
                return false;
            }

            if (!city.IsBorderCity)
            {
                LogAttack(city, "非边境城，不出兵");
                return false;
            }

            if (city.IsEnemiesRound(6))
            {
                LogAttack(city, "6 格内有敌军→转防守，不出兵");
                return false;
            }

            List<City> enemiesCities = new List<City>();
            city.ForeachNeighborCities(x =>
            {
                if (x.IsEnemy(city))
                    enemiesCities.Add(x);
            });

            if (enemiesCities.Count == 0)
            {
                LogAttack(city, "无相邻敌城，不出兵");
                return false;
            }

            // 根据AI个性调整攻击概率（可配：值越低越积极出兵）
            int attackChance = PersonalityId == ForceAI.AIPersonalityType.Aggressive ? AIConfig.Instance.attackSkipChanceAggressive :
                              PersonalityId == ForceAI.AIPersonalityType.Defensive ? AIConfig.Instance.attackSkipChanceDefensive :
                              AIConfig.Instance.attackSkipChanceBalanced;
            if (GameRandom.Chance(attackChance))
            {
                LogAttack(city, $"攻击概率跳过({attackChance}%)，不出兵");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 出兵决策日志（受 AIConfig.logAttackAI 控制）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="reason">日志内容</param>
        [Conditional("SANGO_DEBUG")]
        static void LogAttack(City city, string reason)
        {
            if (AIConfig.Instance.logAttackAI)
                Sango.Log.Info($"[出兵AI]{city.Name} {reason}");
        }

        /// <summary>
        /// AI内政平衡逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIIntriorBalance(City city, Scenario scenario)
        {
            if (city.freePersons.Count <= 1) return true;

            if (city.commerce >= city.agriculture)
            {
                Person[] people = ForceAI.CounsellorRecommendFarming(city.freePersons);
                if (people == null) return true;
                city.JobFarming(people);
            }
            else
            {
                Person[] people = ForceAI.CounsellorRecommendDevelop(city.freePersons);
                if (people == null) return true;
                city.JobDevelop(people);
            }
            return true;
        }

        /// <summary>
        /// AI治安管理逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AISecurity(City city, Scenario scenario)
        {
            AIConfig cfg = AIConfig.Instance;
            if (city.freePersons.Count < 2 || city.gold < cfg.internalMinGold)
                return true;

            if (city.GetJobCounter((int)CityJobType.Inspection) > 0) return true;

            // 【修复】原先概率为 (100 - security) * 4，治安为 0 时高达 400%，越界且行为异常。
            // 现做上限钳制，保证概率落在 0~100 之间。
            int chance = Math.Min(100, (100 - city.security) * 4);
            if (chance <= 0)
                return true;

            if (GameRandom.Chance(chance))
            {
                Person[] people = ForceAI.CounsellorRecommendDevelop(city.freePersons);
                if (people == null) return true;
                city.JobInspection(people);
            }
            return true;
        }

        /// <summary>
        /// AI训练士兵逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AITrainTroop(City city, Scenario scenario)
        {
            if (city.freePersons.Count == 0) return true;

            if (city.GetJobCounter((int)CityJobType.TrainTroops) > 0) return true;

            // 【优化】原先两个分支各自重复调用 CounsellorRecommendTrainTroops + JobTrainTroops，
            // 现合并为「先算概率，再统一执行」，消除重复代码并做概率上限钳制。
            AIConfig cfg = AIConfig.Instance;
            int chance;
            if (city.morale < cfg.cityMoraleLow)
                chance = 100;
            else
                chance = Math.Min(100, (cfg.trainMoraleBase - city.morale) * 3 / 2);

            if (chance <= 0)
                return true;

            if (GameRandom.Chance(chance))
            {
                Person[] people = ForceAI.CounsellorRecommendTrainTroops(city.freePersons);
                if (people == null) return true;
                city.JobTrainTroops(people);
            }
            return true;
        }

        /// <summary>
        /// AI生产兵装逻辑（一回合可能生产多次）。
        ///
        /// 【紧急扩产】兵装严重不足且资金富余时允许多产几次，让多建的锻冶厂 / 厩舍同时动起来。
        /// 次数上限 = 1 + <c>buildingPlan.armsEmergencyMaxExtra</c>；实际还受"空闲建筑数"与
        /// "空闲武将数"约束（每次生产占用一座建筑 + 一批武将，武将执行后即退出空闲池）。
        ///
        /// 【为什么不必考虑行动点】<c>Corps.ReduceActionPoint</c> 对非玩家势力**直接 return**
        /// （见 Corps.cs），AI 的行动点根本扣不动 —— 这里的次数上限只为节奏与观感而设，
        /// 不是资源约束。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>恒定 true（与其它城池 AI 命令一致，不阻断命令队列）</returns>
        public static bool AICreateItems(City city, Scenario scenario)
        {
            int maxRounds = 1;
            if (BuildingPlanner.ShouldEmergencyExpandArms(city, scenario))
            {
                BuildingPlanWeights w = AIConfig.Instance.buildingPlan;
                if (w != null)
                    maxRounds += w.armsEmergencyMaxExtra;
            }

            for (int round = 0; round < maxRounds; round++)
            {
                if (!ProduceArmsOnce(city, scenario))
                    break;
            }

            return true;
        }

        /// <summary>
        /// 尝试生产**一次**兵装：取一座空闲锻冶厂 / 厩舍，按武将兵种适应度挑一种仍有缺口的兵装，
        /// 派人生成。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>本次是否成功挂上生产任务</returns>
        public static bool ProduceArmsOnce(City city, Scenario scenario)
        {
            if (city.freePersons.Count == 0) return false;

            if (city.gold < AIConfig.Instance.createItemsMinGold)
                return false;

            Building freeBlacksmithShop = city.GetFreeBuilding((int)BuildingKindType.BlacksmithShop);
            Building freeStable = city.GetFreeBuilding((int)BuildingKindType.Stable);
            if (freeBlacksmithShop == null && freeStable == null)
                return false;

            int totalNum = 0;
            for (int itemTypeId = 2; itemTypeId <= 5; itemTypeId++)
                // 获取总兵装
                totalNum += city.itemStore.GetNumber(itemTypeId);

            // 达到一定兵装后,如果金钱太少则有概率跳过
            if (totalNum > city.troops * 2 / 3 && city.gold < AIConfig.Instance.createItemsMinGold && GameRandom.Chance(30))
                return false;

            // 统计适应偏向
            int[] levelTotal = new int[4] { 1, 1, 1, 1 };

            if (city.allPersons.Count > 8)
            {
                // 【性能】原 ForEach(lambda) 捕获 levelTotal，改为按下标遍历。
                for (int i = 0; i < city.allPersons.Count; i++)
                {
                    Person x = city.allPersons[i];
                    if (x == null)
                        continue;
                    levelTotal[0] += x.SpearLv;
                    levelTotal[1] += x.HalberdLv;
                    levelTotal[2] += x.CrossbowLv;
                    levelTotal[3] += x.RideLv;
                }
            }
            else
            {
                levelTotal = new int[4] { 100, 100, 100, 100 };
            }

            int sumTotal = 0;
            if (freeBlacksmithShop != null)
                sumTotal = sumTotal + levelTotal[0] + levelTotal[1] + levelTotal[2];

            if (freeStable != null)
                sumTotal = sumTotal + levelTotal[3];

            List<int> validItem = new List<int>();
            for (int itemTypeId = 2; itemTypeId <= 5; itemTypeId++)
            {
                if (itemTypeId == 5 && freeStable == null)
                    continue;

                if (itemTypeId < 5 && freeBlacksmithShop == null)
                    continue;

                if (city.BelongCorps.GetAppointValue((Corps.AppointContentType)(itemTypeId - 2)) == 1)
                    continue;

                // 【容器上限 · 与船 / 器械同口径】上限 = 仓库上限 × 该道具的 p1 千分比
                // （<see cref="ItemType.TransformLimit"/>），且用**总值** StoreLimit
                // （基础值 + 等级加成 + 额外加成），不再用基础字段 storeLimit。
                //
                // 【两类共用同一个公式，靠 p1 区分】
                //   · 兵装四类（枪 / 戟 / 弓 / 军马）在 ItemTypes.json 里**没有 p1**（= 0）→
                //     TransformLimit 直接返回满额 —— 即"每类都能存到整个仓库上限"，四类 1:1 并存；
                //   · 船 / 冲车 / 井阑这类带 p1 = 10 的道具 → 上限 = 仓库上限的 1%，
                //     免得一座城堆出几万辆投石车。
                // 与运输侧 ResourceBalance.GetItemGroupLimit（同样 StoreLimit × p1 / 1000，
                // p1 缺失按满额）保持同一口径。
                int itemNum = city.itemStore.GetNumber(itemTypeId);
                ItemType capacityItem = scenario != null ? scenario.GetObject<ItemType>(itemTypeId) : null;
                int itemCapacity = capacityItem != null
                    ? capacityItem.TransformLimit(city.StoreLimit)
                    : city.StoreLimit;

                // 【对齐原版】达到容量上限就不再生产 —— 见 s11_game_ai_kyoten_cmd.cpp:290-294：
                //   if (city->get_equipment_quantity(i) >= city->get_max_equipment_quantity(i)) weight[i] = 0;
                // 原版没有"满了继续造、靠运输消化"的概念，满了就是权重清零。
                if (itemCapacity > 0 && itemNum >= itemCapacity)
                    continue;

                if (itemNum < levelTotal[itemTypeId - 2] * city.troops / sumTotal + 5000)
                    validItem.Add(itemTypeId);

                //{
                //    Person[] people = ForceAI.CounsellorRecommendCreateItems(city.freePersons);
                //    if (people == null) return true;
                //    ItemType itemType = Scenario.Cur.GetObject<ItemType>(itemTypeId);
                //    city.JobCreateItems(people, itemType, itemTypeId == 5 ? freeStable : freeBlacksmithShop);
                //    return true;
                //}
            }

            if (validItem.Count == 0)
                return false;

            int[] pr = new int[validItem.Count];
            for (int i = 0; i < pr.Length; i++)
                pr[i] = levelTotal[validItem[i] - 2];

            int targetItemId = validItem[GameRandom.RandomWeightIndex(pr)];
            Person[] people = ForceAI.CounsellorRecommendCreateItems(city.freePersons);
            if (people == null) return false;
            ItemType itemType = Scenario.Cur.GetObject<ItemType>(targetItemId);
            city.JobCreateItems(people, itemType, targetItemId == 5 ? freeStable : freeBlacksmithShop);

            return true;
        }

        /// <summary>
        /// AI生产船只逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AICreateBoat(City city, Scenario scenario)
        {
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.MakeItem_Boat) == 1)
                return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
            {
                if (GameRandom.Chance(50))
                    return true;
            }

            if (city.portList.Count == 0) return true;

            if (city.freePersons.Count < 2 || city.gold < AIConfig.Instance.createMachineMinGold)
                return true;

            ItemType targetItemType = scenario.GetObject<ItemType>(12);

            int totalNum = city.itemStore.GetNumber((int)ItemStoreKindType.Boat);
            // 【后方满仓后不应当停产】容器满只说明"该往外运了"，不是停产的信号 ——
            // 资源调度会把水位以上的富余推向前线（ResourceDispatcher）。
            // 因此这里只在"满了、**而且这批货运不出去**"时才停产：
            //   · 本城没被资源调度覆盖（功能关闭 / 军团禁止运输 / 玩家直辖军团…）；
            //   · 或上一趟调度里这类资源有**背压**（有富余却找不到接收方）。
            // 两者都不成立就往死里造也无所谓 —— 反正下一趟调度会把它搬空。
            int boatCapacity = targetItemType.TransformLimit(city.StoreLimit);
            if (boatCapacity > 0 && totalNum >= boatCapacity)
                return true;

            Building BoatFactory = city.GetFreeBuilding((int)BuildingKindType.BoatFactory);
            if (BoatFactory == null)
                return true;

            if (city.allPersons.Find(x => x.missionType == (int)MissionType.PersonCreateBoat) != null)
                return true;

            if (!targetItemType.IsValid(city.BelongForce))
                targetItemType = scenario.GetObject<ItemType>(11);


            // 获取总兵装
            if (totalNum > (city.troops / 2) * targetItemType.p1 / 1000 + 1 && GameRandom.Chance(20))
                return true;

            Person[] people = ForceAI.CounsellorRecommendCreateItems(city.freePersons);
            if (people == null) return true;
            city.JobCreateBoat(people, targetItemType, BoatFactory);
            return true;
        }

        /// <summary>
        /// AI生产器械逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AICreateMachine(City city, Scenario scenario)
        {
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.MakeItem_Machine) == 1)
                return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
            {
                if (GameRandom.Chance(50))
                    return true;
            }

            if (city.freePersons.Count < 2 || city.gold < AIConfig.Instance.createMachineMinGold)
                return true;


            Building MechineFactory = city.GetFreeBuilding((int)BuildingKindType.MechineFactory);
            if (MechineFactory == null)
                return true;

            if (city.allPersons.Find(x => x.missionType == (int)MissionType.PersonCreateMachine) != null)
                return true;

            int monsterNum = city.itemStore.GetNumber((int)ItemStoreKindType.Helepolis);
            int towerNum = city.itemStore.GetNumber((int)ItemStoreKindType.Catapult);

            int totalNum;
            ItemType targetItemType;
            if (towerNum > monsterNum)
            {
                totalNum = monsterNum;
                targetItemType = scenario.GetObject<ItemType>(7);
                if (!targetItemType.IsValid(city.BelongForce))
                    targetItemType = scenario.GetObject<ItemType>(6);
            }
            else
            {
                totalNum = towerNum;
                targetItemType = scenario.GetObject<ItemType>(9);
                if (!targetItemType.IsValid(city.BelongForce))
                    targetItemType = scenario.GetObject<ItemType>(8);
            }

            // 【后方满仓后不应当停产】同 AICreateBoat：满了也继续造，只要资源调度还能把富余运走；
            // 只有"没被调度覆盖"或"上一趟有背压（运不出去）"时才按原口径停产。
            int machineCapacity = targetItemType.TransformLimit(city.StoreLimit);
            if (machineCapacity > 0 && totalNum >= machineCapacity)
                return true;

            if (totalNum > (city.troops / 2) * targetItemType.p1 / 1000 + 1 && GameRandom.Chance(20))
                return true;

            Person[] people = ForceAI.CounsellorRecommendCreateItems(city.freePersons);
            if (people == null) return true;
            city.JobCreateMachine(people, targetItemType, MechineFactory);
            return true;
        }

        /// <summary>
        /// AI创建部队逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="minTurn">最小回合数</param>
        /// <param name="isAttack">是否为攻击</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>创建的部队</returns>
        public static Troop AIMakeTroop(City city, int minTurn, bool isAttack, Scenario scenario)
        {
            if (city.troops == 0) return null;

            // 【出城队列】这里**不再**因为"上一支部队还没走出城头"就拒绝组建。
            // 同回合连续派多支是允许的：新部队由 CityTroopFactory.EnqueueOutTroop 排队，
            // 队列保证一支出完再出下一支（见 CityTroopFactory.ActivatePendingOutTroops），
            // 城池中心格的占位由 City.EnsureTroop 做"不抢占已有引用"保护。

            int minEquipNeed = 5000;

            // 兵装不足改用剑兵兜底时，剑兵队伍至少要能编成这么多人才值得出征；
            // 连这个数都凑不齐就固守城池，避免小股剑兵出城被白吃。
            const int SwordFallbackMinTroops = 3000;

            if (isAttack)
            {
                // 组建进攻部队失败的三个前置条件，逐个打日志，避免"决定进攻却不出城"无从排查
                if (city.freePersons.Count < 2)
                {
                    LogAttack(city, $"组建进攻部队失败：空闲武将不足 {city.freePersons.Count}<2");
                    return null;
                }
                if (city.troops < scenario.Variables.minTroopsKeepWhenAttack)
                {
                    LogAttack(city, $"组建进攻部队失败：兵力 {city.troops}<留守门槛 {scenario.Variables.minTroopsKeepWhenAttack}");
                    return null;
                }
                if (city.food < scenario.Variables.minFoodKeepWhenAttack)
                {
                    LogAttack(city, $"组建进攻部队失败：粮草 {city.food}<留守门槛 {scenario.Variables.minFoodKeepWhenAttack}");
                    return null;
                }
            }
            else
            {
                if (city.troops < scenario.Variables.minTroopsKeepWhenDefence) return null;
                // 【修正】原先误用兵力阈值 minTroopsKeepWhenDefence 判粮食，
                // 现改回粮食专属阈值 minFoodKeepWhenDefence，与进攻分支的写法一致。
                if (city.food < scenario.Variables.minFoodKeepWhenDefence) return null;
                minEquipNeed = 1500;
            }

            // 所有满足5000兵的部队类型
            List<TroopType> costEnoughTroopTypes = new List<TroopType>();
            TroopType.GetCostEnoughTroopTypeList(city, costEnoughTroopTypes, minEquipNeed);

            // 先摘下剑兵(Id=1)备用：剑兵不消耗任何兵装(其 costItems 为空)，
            // 是防守方在兵装不足时唯一还能组建的兵种，详见下方列表为空时的兜底分支。
            TroopType swordTroopType = costEnoughTroopTypes.Find(x => x.Id == 1);

            // 去掉剑兵(进攻不允许)与非陆地兵种
            costEnoughTroopTypes.RemoveAll(x => x.Id == 1 || !x.isLand);

            // 小于4支部队不带器械, 防守不组建器械
            if (city.AttackTroopsCount < 4 || !isAttack)
                costEnoughTroopTypes.RemoveAll(x => x.IsMachine());
            else
            {
                if (city.AttackTroopsCount > 4)
                {
                    // 如果器械少于2-3个,则补充器械
                    if (city.allAttackTroops.FindAll(x => x.LandTroopType.IsMachine()).Count < GameRandom.Range(1, 4))
                    {
                        if (costEnoughTroopTypes.Find(x => x.IsMachine()) != null)
                            costEnoughTroopTypes.RemoveAll(x => !x.IsMachine());
                    }
                }
            }

            // 兵装不足的兜底：防守方在"以 minEquipNeed(防守=1500)兵为基准，也组建不出
            // 任何一种兵装兵种"时，退而使用不需兵装的剑兵出城迎敌 —— 城池宁可派剑兵上阵，
            // 也不能因为装备短缺而放任敌军围城；进攻方依然严格禁止组建剑兵。
            bool useSwordFallback = false;
            if (costEnoughTroopTypes.Count == 0)
            {
                if (isAttack || swordTroopType == null)
                {
                    if (isAttack)
                        LogAttack(city, "组建进攻部队失败：兵装不足，无法组建任何兵装兵种（进攻禁用剑兵）");
                    return null;
                }

                // 城里兵太少，连一支阈值以上的剑兵队伍都拉不出来 → 不如固守
                if (city.troops <= SwordFallbackMinTroops)
                    return null;

                costEnoughTroopTypes.Add(swordTroopType);
                useSwordFallback = true;
                Sango.Log.Info($"{city.BelongForce.Name}势力在{city.Name}兵装不足，改用剑兵组建队伍出城防守!");
            }

            int maxPersonCount = city.freePersons.Count > 13 ? 3 : (city.freePersons.Count < 6 ? 1 : 2);

            // 优先组建特殊
            TroopType spType = costEnoughTroopTypes.Find(x => x.validItemId > 0);

            // 尝试移除不够组建的特殊兵种
            while (spType != null && !spType.CheckCost(city, 5000))
            {
                costEnoughTroopTypes.Remove(spType);
                spType = costEnoughTroopTypes.Find(x => x.validItemId > 0);
            }

            if (spType == null)
            {
                // 【修复】原先直接取 freePersons[0]，列表为空时会抛异常；
                // 兜底兵种也由"随机挑选"改为"挑选与主将适应性最高的兵种"。
                city.freePersons.Sort((a, b) => b.MilitaryAbility.CompareTo(a.MilitaryAbility));
                Person person = city.freePersons.Count > 0 ? city.freePersons[0] : null;
                if (person != null)
                {
                    int bestLevel = -1;
                    for (int i = 0; i < costEnoughTroopTypes.Count; i++)
                    {
                        TroopType troopType = costEnoughTroopTypes[i];
                        int level = Troop.CheckTroopTypeLevel(troopType, person);
                        if (level > bestLevel)
                        {
                            bestLevel = level;
                            spType = troopType;
                        }
                    }
                }
                if (spType == null)
                    spType = costEnoughTroopTypes[0];
            }

            // 【推荐队伍优先】本城能凑齐某支推荐队伍（且队伍兵种在当前允许组建的范围内）
            // → 直接用队伍的兵种与成员，跳过下面的常规挑选。队伍不限兵种时沿用上面选出的 spType。
            Person[] people = null;
            TroopTeamCandidate teamPick;
            if (TroopTeamService.TryPickForAI(city, costEnoughTroopTypes,
                    city.ResolveRing(), out teamPick))
            {
                if (teamPick.troopType != null)
                    spType = teamPick.troopType;
                people = teamPick.members.ToArray();
            }

            // 【修复】推荐结果为空时直接返回，避免后续 people[0] 抛 NullReferenceException
            if (people == null || people.Length == 0 || people[0] == null)
                people = ForceAI.CounsellorRecommendMakeTroop(city.freePersons, spType, maxPersonCount, city);
            if (people == null || people.Length == 0 || people[0] == null)
            {
                LogAttack(city, $"组建{(isAttack ? "进攻" : "防御")}部队失败：无可编入部队的武将");
                return null;
            }

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            troop.Leader = people[0];
            troop.TroopType = spType;

            if (people.Length > 1) troop.Member1 = people[1];
            if (people.Length > 2) troop.Member2 = people[2];

            // 计算最大兵力
            troop.CalculateMaxTroops(scenario);

            // 确定兵数
            int maxTroopNum = troop.MaxTroops;
            if (city.troops < maxTroopNum * 2)
                maxTroopNum = (city.troops / 2000) * 1000;

            maxTroopNum = city.itemStore.CheckCostMin(spType.costItems, maxTroopNum);

            // 【水军】挑一个可用的水上兵种（大船优先，兵力不足则回落走舸），
            // 并按舰船再约束一次兵力 —— 下面 WaterTroopType.Cost 会真的扣船，
            // 而它不做任何检查，不在这里先卡住会把舰船库存扣成负数。
            TroopType waterTroopType = PickWaterTroopType(scenario, city, maxTroopNum);
            if (waterTroopType == null)
                return null;    // 兵种数据缺失，放弃本次出征（避免后续 Cost 空引用）
            maxTroopNum = city.itemStore.CheckCostMin(waterTroopType.costItems, maxTroopNum);

            // 剑兵兜底(兵装不足改派剑兵)：实际能编成的兵力必须超过阈值才出征，
            // 否则出城也只是送掉一支弱旅，不如全师固守城池。
            if (useSwordFallback && maxTroopNum <= SwordFallbackMinTroops)
            {
                Sango.Log.Info($"{city.BelongForce.Name}势力在{city.Name}兵装短缺，剑兵队伍最多只能编成{maxTroopNum}人，放弃出城防守");
                return null;
            }

            // 粮食
            int turnCostFood = (int)(maxTroopNum * scenario.Variables.baseFoodCostInTroop);
            int food = turnCostFood * minTurn;
            while (city.food < food)
            {
                if (isAttack)
                {
                    LogAttack(city, $"组建进攻部队失败：出征粮草不足 {city.food}<{food}");
                    return null;
                }

                minTurn = minTurn - 4;
                if (minTurn < 5)
                    return null;

                food = turnCostFood * minTurn;
            }
            troop.WaterTroopType = waterTroopType;
            troop.LandTroopType.Cost(city, maxTroopNum);
            troop.WaterTroopType.Cost(city, maxTroopNum);
            city.troops -= maxTroopNum;
            city.food -= food;
            for (int p = 0; p < people.Length; p++)
                city.freePersons.Remove(people[p]);

            troop.troops = maxTroopNum;
            troop.food = food;

            // 【出征携款 · 严格对齐原版】s11_game_ai_kyoten_cmd_mil_attack.cpp:296-334
            //
            // 原版三类分支：
            //   ① 目标是**敌势力据点**且主兵种不是攻城兵器：
            //       据点可用金 >= 10000 → 75% 概率带 100*(15+rand(6)) = 1500~2000
            //       据点可用金 >= 2000  → 50% 概率带 1000
            //       否则                → 不带
            //   ② 目标是**无主关隘/港口**：必带"随军成员俸禄 × 8"（金不够时原版直接不出阵）
            //   ③ 其余（无主都市等）→ 不带
            //
            // 【旧实现错在哪】旧公式 `maxTroopNum * troopCarryGoldPerTroops / 10000`，
            // 参数 troopCarryGoldPerTroops 注释写的是"占总兵力的比例(%)"，
            // 但除数用了 10000 而不是 100 —— 8000 兵只剩 4 金，
            // 连最便宜的建筑都修不起，"随军增筑 / 前线建陷阱"整条链路被彻底堵死。
            City carryTarget = scenario.citySet.Get(city.TroopMissionTargetId);
            int carryGold = 0;
            string carryReason = "无携款条件";
            if (carryTarget != null)
            {
                if (carryTarget.BelongForce != null)
                {
                    // ① 敌势力据点 —— :298-313
                    bool isSiege = troop.LandTroopType != null && troop.LandTroopType.siegeRole != 0;
                    if (isSiege)
                    {
                        carryReason = "攻城兵器不带金";
                    }
                    else
                    {
                        int outpostGold = OutpostRuntime.gold > 0 ? OutpostRuntime.gold : city.gold;
                        if (outpostGold >= 10000)
                        {
                            if (GameRandom.Chance(75))
                                carryGold = 100 * (15 + GameRandom.Range(0, 6));   // 1500 ~ 2000
                            else
                                carryReason = "据点金>=10000，但未命中 75%";
                        }
                        else if (outpostGold >= 2000)
                        {
                            if (GameRandom.Chance(50))
                                carryGold = 1000;
                            else
                                carryReason = "据点金>=2000，但未命中 50%";
                        }
                        else
                        {
                            carryReason = $"据点可用金 {outpostGold}<2000";
                        }
                    }
                }
                else if (!carryTarget.IsCity())
                {
                    // ② 无主关隘/港口：俸禄 × 8 —— :316-333
                    List<Person> members = new List<Person>(3);
                    if (troop.Leader != null) members.Add(troop.Leader);
                    if (troop.Member1 != null) members.Add(troop.Member1);
                    if (troop.Member2 != null) members.Add(troop.Member2);
                    int pay = HoryoShoguu.SumPay(members);
                    int need = pay * 8;
                    if (need > 0 && city.gold >= need)
                        carryGold = need;
                    else
                        carryReason = $"空关隘需俸禄×8={need}，城池仅 {city.gold}";
                }
            }
            else
            {
                carryReason = "无进攻目标";
            }

            // 城池余额兜底：原版由据点 gold_ 记账保证，本项目直接卡在城库上
            if (carryGold > city.gold)
                carryGold = city.gold;

            if (carryGold > 0)
            {
                troop.gold = carryGold;
                city.gold -= carryGold;
                // 【链路日志 ①】出征带金 —— 这是"前线建陷阱"的启动资金，没有它后面全部断链
                Sango.Log.Info($"{city.BelongForce?.Name}在{city.Name}出征的[{troop.Name}]携带资金 {carryGold}（城池余 {city.gold}）");
            }
            else
            {
                Sango.Log.Info($"{city.BelongForce?.Name}在{city.Name}出征的[{troop.Name}]未带资金：{carryReason}（城池余 {city.gold}）");
            }

            // 【意图层】对齐原版：出征时就定下 deploy_type / deploy_target
            // （s11_game_ai_kyoten_cmd_mil_attack.cpp:396-398 等据点 AI 出征命令写入），
            // 部队层只读它做改派（s11_game_ai_unit.cpp:124-130）。
            // 注意必须在 SetMission 之前写：SetMission 可能触发 Prepare 级联，那里会读意图。
            troop.deployType = Troop.ToDeployType((int)city.TroopMissionType);
            troop.deployTarget = city.TroopMissionTargetId;

            // 渲染刷新统一由 CityTroopFactory.EmitTroop 负责（此时部队尚未登记进场景）
            troop.SetMission(city.TroopMissionType, city.TroopMissionTargetId);
            return troop;
        }

        /// <summary>
        /// 本次出征"至少要有多少兵"才值得启用大船：低于此值一律用不耗船的走舸。
        /// </summary>
        const int MinTroopsForBigShip = 3000;

        /// <summary>
        /// 为出征部队挑一个可用的**水上兵种**：优先大船（斗舰 / 楼船），兵力不足则回落走舸。
        ///
        /// 【原来写死了什么】旧代码是 <c>scenario.GetObject&lt;TroopType&gt;(8)</c> —— 那是**走舸**，
        /// 一条 <c>costItems</c> 为空、**不消耗舰船**的小船。于是城里攒下的楼船 / 斗舰永远用不上，
        /// 造船厂产出的舰船只能烂在仓库里。
        ///
        /// 【挑法】遍历所有可用的水上兵种（<c>!isLand &amp;&amp; isFight</c>），逐个判断：
        ///   ① **兵力够** —— 本次出征兵力低于 <see cref="MinTroopsForBigShip"/> 时**一律不选大船**：
        ///      小部队占用舰船不划算，而走舸不耗船、战力损失有限；
        ///   ② **科技**满足 —— 见 <see cref="TroopType.IsValid"/>：
        ///      斗舰要科技 23；楼船的 <c>validTechId = -23</c> 表示"该科技之前可用"，是它的下位替代；
        ///   ③ **舰船够** —— 按它的 <c>costItems</c> 折算这支部队能装备多少兵
        ///      （<see cref="ItemStore.CheckCostMin"/>），少于本次兵力就说明船不够，
        ///      硬用会把编成拖小（且 <c>Cost</c> 不做检查，会把库存扣成负数）。
        /// 在满足 ①②③ 的大船里取 **atk 最高**的一种（斗舰 100 &gt; 楼船 90）。
        /// 一艘都不满足时回落到**不耗船**的水上兵种（走舸），保证永远有船可用。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="city">出发城市</param>
        /// <param name="maxTroopNum">本次出征的最大兵力</param>
        /// <returns>选中的水上兵种（读不到数据时返回 null）</returns>
        static TroopType PickWaterTroopType(Scenario scenario, City city, int maxTroopNum)
        {
            if (scenario == null || scenario.CommonData == null)
                return null;

            Force force = city != null ? city.BelongForce : null;
            TroopType fallback = null;      // 不耗船的保底（走舸）
            TroopType best = null;
            int bestAtk = -1;

            // 兵力不足以支撑大船的舰船消耗 → 直接走保底，不再挑大船
            bool allowBigShip = city != null && maxTroopNum >= MinTroopsForBigShip;

            var types = scenario.CommonData.TroopTypes;
            for (int i = 0; i < types.Count; i++)
            {
                TroopType t = types[i];
                if (t == null || t.isLand || !t.isFight)
                    continue;                                   // 只看可战斗的水上兵种

                if (force != null && !t.IsValid(force))
                    continue;                                   // 科技不满足

                // 不消耗舰船的（走舸）只作保底，不参与大船竞争
                if (t.costItems == null || t.costItems.Length == 0)
                {
                    if (fallback == null)
                        fallback = t;
                    continue;
                }

                if (!allowBigShip)
                    continue;                                   // 兵力不足，不选大船

                // 船不足以装备本次全部兵力 → 不选大船
                int canEquip = city.itemStore.CheckCostMin(t.costItems, maxTroopNum);
                if (canEquip < maxTroopNum)
                    continue;

                if (t.atk > bestAtk)
                {
                    bestAtk = t.atk;
                    best = t;
                }
            }

            if (best != null)
                return best;
            if (fallback != null)
                return fallback;

            // 兜底：数据异常时仍给走舸，保持旧行为不退化
            return scenario.GetObject<TroopType>(8);
        }

        /// <summary>
        /// 【新增】AI 组建战场补给队。
        ///
        /// 触发条件：
        /// 1. 本城资源(兵力 / 粮草)充足；
        /// 2. 附近存在需要补给的己方战部队(缺粮或兵力不足)；
        /// 3. 本城派出的补给队数量未超过上限。
        ///
        /// 组建后的补给队携带粮草 / 兵力 / 兵装,执行 TroopSupplyTroop 任务：
        /// 自动寻找需要补给的友军,保持在友军后方并规避威胁,靠近后完成补给。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIMakeSupplyTroop(City city, Scenario scenario)
        {
            if (city.BelongForce == null)
                return true;

            // 军团委任:禁止运输时不派补给队
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.TransportDisable) == 1)
                return true;

            // 军团委任 · 禁止攻击 / 全局和平期:战场补给队要把物资送上前线,同属"向外用兵",一并禁止。
            // 与上面"禁止运输"的区别：那条禁的是城内资源运输，这条禁的是把部队派到前线。
            if (IsAttackForbidden(city) || scenario.IsPeacePeriod)
                return true;

            AIConfig aiConfig = AIConfig.Instance;

            // 资源门槛
            if (city.freePersons.Count < 1)
                return true;
            if (city.troops < aiConfig.supplyMinCityTroops)
                return true;
            if (city.food < aiConfig.supplyMinCityFood)
                return true;

            // 本城已派出的补给队数量上限
            int supplyCount = 0;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                Troop t = scenario.troopsSet[i];
                if (t != null && t.IsAlive && t.BelongCity == city
                    && t.missionType == (int)MissionType.TroopSupplyTroop)
                {
                    supplyCount++;
                }
            }
            if (supplyCount >= aiConfig.supplyMaxPerCity)
                return true;

            // 【配比门槛】附近需要有足够多的待补给友军,才值得派出一支"战场级"补给队,
            // 避免出现"一支部队配一个补给队"的浪费。
            List<Troop> needyTroops = new List<Troop>();
            CollectNeedyTroops(needyTroops, city, scenario);
            if (needyTroops.Count < aiConfig.supplyMinNeedyTroops)
                return true;
            Troop needy = needyTroops[0];

            // 【游戏规则】部队不允许停留在城市格上。若本城周边战场我方已处于大劣，
            // 补给队组建后也只能立刻解散或白跑一趟，因此直接不组建。
            if (TroopSupplyTroop.IsForceLosingAt(city.CenterCell, city.BelongForce, scenario))
                return true;

            // 运输队兵种
            TroopType troopType = TroopType.GetTransportType(scenario, city.BelongForce);
            if (troopType == null)
                return true;

            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(city.freePersons);
            if (persons == null || persons.Length == 0 || persons[0] == null)
                return true;
            Person leader = persons[0];

            // 【容量】携带兵力:最多取城池一半,且不超过配置上限(战场级)
            int carryTroops = System.Math.Min(city.troops / 2, aiConfig.supplyTroopAmount);
            if (carryTroops <= 0)
                carryTroops = 1;

            // ---------- 出征条件：兵装必须能装备随队兵力 ----------
            // 换算与部队组建一致（1 件兵装装备 1 名士兵）：
            //   · 通用兵装 = 枪 + 戟 + 弩 + 战马（船与器械不能装备普通部队，故不计入）
            //   · 走水路时船只还需单独满足同等门槛
            bool needBoat = needy.IsInWater || city.IsPort();
            int affordable = GetAffordableTroops(city, needBoat);
            if (affordable < aiConfig.supplyMinCarryTroops)
                return true;                       // 兵装不足以保障最低出征规模，取消出征
            if (carryTroops > affordable)
                carryTroops = affordable;

            // ---------- 兵装 : 兵力 = 1 : 1 ----------
            // 带多少兵就配多少兵装，避免出现"13000 兵只带 3000 兵装"这类失衡出征。
            // 只抽取通用兵装（枪 / 戟 / 弩 / 战马）：船只必须留给水军组建、
            // 器械属攻城装备，都不能被当作补给物资搬走。
            int landItems = GetAffordableTroops(city, false);
            if (landItems <= 0)
                return true;
            if (carryTroops > landItems)
                carryTroops = landItems;

            // 按"所需兵装 / 现有兵装"折算抽取比例（至少 1%）
            int part = Math.Min(100, Math.Max(1, carryTroops * 100 / landItems));
            ItemStore carryItems = SplitSupplyItems(city, part);
            if (carryItems == null || carryItems.TotalNumber < carryTroops)
                carryTroops = carryItems == null ? 0 : carryItems.TotalNumber;

            if (carryTroops < aiConfig.supplyMinCarryTroops)
            {
                // 兵装因逐类取整被削得过多 → 取消出征，并把已抽出的兵装原样退回城市
                ReturnItems(city, carryItems);
                return true;
            }

            // ---------- 粮草：按「兵力 × 每兵粮耗 × 可支撑回合数」估算 ----------
            int carryFood = (int)(carryTroops * scenario.Variables.baseFoodCostInTroop * aiConfig.supplyFoodTurnCount);
            if (carryFood > aiConfig.supplyFoodAmount)
                carryFood = aiConfig.supplyFoodAmount;
            if (carryFood > city.food * 2 / 3)
                carryFood = city.food * 2 / 3;
            if (carryFood < 0)
                carryFood = 0;

            // 【一致性】随队兵力 / 粮草不得低于补给队的"返回线"（supplyReturnTroops / supplyReturnFood），
            // 否则补给队刚组建就会在 TroopSupplyTroop.IsMissionComplete 中判定为资源不足，
            // 立刻返城 —— 而它就在本城内，返城等于进城解散，表现为"刚出城就消失"。
            if (carryTroops < aiConfig.supplyReturnTroops || carryFood < aiConfig.supplyReturnFood)
            {
                ReturnItems(city, carryItems);
                return true;
            }

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            troop.IsAlive = true;
            troop.Leader = leader;
            troop.TroopType = troopType;
            troop.troops = carryTroops;
            troop.food = carryFood;
            troop.Member1 = null;
            troop.Member2 = null;
            troop.itemStore = carryItems;
            // 【不写 deployType】补给队的 missionTarget 是**友军部队 Id**（needy 是 Troop），
            // 而 deployTarget 的语义是"城 / 建筑 Id"（见 Troop.deployTarget 注释）。
            // 两者不同域：若写成 Transport，UpdateAIOrder 会拿部队 Id 去找城 → 找不到 → 任务被改判，
            // 补给队会变成"运物资到城"的运输队并在本城直接解散。保持 None 走 default 分支才是对的。
            city.troops -= carryTroops;
            city.food -= carryFood;

            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            if (troop == null)
                return true;                        // 城池被围困,无法出城,本次出征已取消
            troop.SetMission(MissionType.TroopSupplyTroop, needy.Id);
            troop.NeedPrepareMission();
            city.freePersons.Remove(leader);
            city.CurActiveTroop = troop;

            // 【立即出城】补给队同样不能停在城头等下一个回合。
            // 对齐 s11_sys_person_order_military.cpp:331-353（unit_set_order 后立刻 run_order）。
            if (!CityTroopFactory.DriveOutOfCity(troop, scenario))
            {
                Sango.Log.Warning($"{scenario.GetDateStr()}{city.BelongForce.Name}在{city.Name}组建的补给队无法出城（城头无可停留格或寻路失败），本次支援取消，物资已退回");
                CityTroopFactory.RevokeEmit(city, troop);
                return true;
            }

            Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领补给队出城 支援{needy.Name}!");
            return true;
        }

        /// <summary>
        /// 收集本城附近需要补给的己方战部队(缺粮或兵力不足)。
        /// 用于判断是否值得派出一支战场级补给队(配比门槛)。
        /// </summary>
        /// <param name="result">结果列表(会先清空再填充)</param>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        public static void CollectNeedyTroops(List<Troop> result, City city, Scenario scenario)
        {
            result.Clear();
            // 【一致性】与 TroopSupplyTroop.FindSupplyTarget 使用同一范围，
            // 避免出现"出征时判定有目标、出征后却找不到目标"而白跑一趟。
            int range = AIConfig.Instance.supplySearchRange;
            int foodFactor = AIConfig.Instance.supplyFoodThresholdFactor;

            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || !troop.IsAlive)
                    continue;
                // 只补给同势力的部队
                if (troop.BelongForce != city.BelongForce)
                    continue;
                if (troop.IsTransport)
                    continue;
                if (troop.missionType == (int)MissionType.TroopSupplyTroop)
                    continue;
                // 不补给无任务的城内驻守部队
                if (troop.missionType <= 0)
                    continue;

                if (scenario.Map.Distance(city.CenterCell, troop.cell) > range)
                    continue;

                // 缺粮 或 兵力缺口明显
                // 缺粮 / 兵力缺口明显 / 兵装不足，任一成立即视为需要补给
                bool lowFood = troop.food <= 0 || troop.food < troop.troops * foodFactor;
                bool lowTroops = troop.MaxTroops > 0 && troop.troops < troop.MaxTroops / 2;
                if (lowFood || lowTroops || IsItemShortage(troop))
                {
                    result.Add(troop);
                }
            }
        }

        /// <summary>通用陆军兵装种类（枪 / 戟 / 弩）</summary>
        static readonly int[] supplyWeaponKinds = new int[]
        {
            (int)ItemStoreKindType.Spear,
            (int)ItemStoreKindType.Halberd,
            (int)ItemStoreKindType.Crossbow,
        };

        /// <summary>
        /// 计算城市当前通用兵装可装备的兵力上限。
        ///
        /// 【换算规则】与 <see cref="ItemStore.CheckCostMin"/> / <see cref="ItemStore.CheckItemEnough"/>
        /// 完全一致：兵种 costItems 形如 [兵装种类, 每千人消耗]，例如枪兵 [2, 1000] 表示
        /// 每 1000 兵消耗 1000 件枪 —— 即 **1 件兵装装备 1 名士兵**。
        /// 因此可装备兵力 = 兵装数量。
        ///
        /// 通用兵装 = 枪 + 戟 + 弩 + 战马；船与器械不计入（不能用于装备普通部队）。
        /// 若 <paramref name="requireBoat"/> 为真，则同时受船只数量约束（同样按 1:1）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="requireBoat">是否要求满足船只需求（走水路时）</param>
        /// <returns>可装备的兵力上限</returns>
        public static int GetAffordableTroops(City city, bool requireBoat)
        {
            if (city == null || city.itemStore == null)
                return 0;

            int landItems = city.itemStore.GetNumber(supplyWeaponKinds)
                          + city.itemStore.GetNumber((int)ItemStoreKindType.Horse);
            int limit = landItems;

            // 【船的需求】走水路时，船只必须单独满足，不能用陆地兵装替代
            if (requireBoat)
            {
                int boat = city.itemStore.GetNumber((int)ItemStoreKindType.Boat);
                limit = Math.Min(limit, boat);
            }

            return Math.Max(0, limit);
        }

        /// <summary>
        /// 【补给队出征条件】判断城市总兵装是否足以装备指定兵力。
        ///
        /// 换算规则与部队组建一致（1 件兵装装备 1 名士兵）：
        ///   · 通用兵装（枪 / 戟 / 弩 / 战马）必须满足；
        ///   · <paramref name="requireBoat"/> 为真时，船只必须**单独**满足同一门槛
        ///     （船属水军装备，不可被陆地兵装替代）。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="troops">待装备的兵力</param>
        /// <param name="requireBoat">是否要求满足船只需求（走水路时）</param>
        /// <returns>兵装是否足够</returns>
        public static bool HasItemsForTroops(City city, int troops, bool requireBoat)
        {
            if (city == null || troops <= 0)
                return true;
            return GetAffordableTroops(city, requireBoat) >= troops;
        }

        /// <summary>
        /// 【前线兵装比】判断前线部队的兵装是否明显不足：
        /// 部队实际持有的通用兵装（枪 / 戟 / 弩 / 战马 / 船）覆盖率低于配置阈值（默认 50%）。
        /// </summary>
        /// <param name="troop">前线部队</param>
        /// <returns>兵装是否不足</returns>
        public static bool IsItemShortage(Troop troop)
        {
            if (troop == null || troop.troops <= 0)
                return false;

            AIConfig cfg = AIConfig.Instance;
            if (cfg.supplyNeedyItemPercent <= 0)
                return false;

            // 换算与部队组建一致（1 件兵装装备 1 名士兵）
            int need = troop.troops;
            if (need <= 0)
                return false;

            int have = 0;
            if (troop.IsTransport)
            {
                // 运输 / 补给队：兵装就是"装在 itemStore 里、准备送往前线的物资"，按实际携带量计。
                if (troop.itemStore != null)
                {
                    have += troop.itemStore.GetNumber(supplyWeaponKinds);
                    have += troop.itemStore.GetNumber((int)ItemStoreKindType.Horse);
                    have += troop.itemStore.GetNumber((int)ItemStoreKindType.Boat);
                }
            }
            else
            {
                // 【修复】普通战斗部队的兵装不记在 itemStore 里，而是随兵力齐备
                // （见 Troop.GetItemNumber：按「兵力 × 兵种消耗系数」推算）。
                // 原实现直接读 itemStore，对战斗部队恒为 0，于是**所有前线部队永远"兵装不足"**：
                //   · CollectNeedyTroops 把每一支有任务的部队都算成待补给；
                //   · CalcSupplyNeed 因此恒 > 0 → 补给队永远找得到目标、永不返城，
                //     长期黏着前线部队（甚至在城头反复补给不再前进），即"补给队滞留"。
                have += troop.troops + troop.woundedTroops;

                // 额外接收到的补给兵装会记在 itemStore 里，一并计入
                if (troop.itemStore != null)
                {
                    have += troop.itemStore.GetNumber(supplyWeaponKinds);
                    have += troop.itemStore.GetNumber((int)ItemStoreKindType.Horse);
                    have += troop.itemStore.GetNumber((int)ItemStoreKindType.Boat);
                }
            }

            // 兵装覆盖率低于阈值 → 视为不足
            return have * 100 < need * cfg.supplyNeedyItemPercent;
        }

        /// <summary>
        /// 取消出征时把已抽出的兵装原样退回城市，避免城市凭空损失物资。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="items">已抽出的兵装</param>
        static void ReturnItems(City city, ItemStore items)
        {
            if (city == null || items == null || items.TotalNumber <= 0)
                return;
            city.itemStore.Add(items);
        }

        /// <summary>
        /// 【兵装携带】按比例抽取可用于补给前线的兵装。
        ///
        /// 只抽取通用兵装（枪 / 戟 / 弩 / 战马）：
        ///   · **不含船只** —— 船需保留给水军组建，不能被当作补给物资搬走；
        ///   · **不含器械**（冲车 / 投石）—— 属攻城装备，不属于前线补给物资。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="part">抽取比例（1~100）</param>
        /// <returns>抽出的兵装</returns>
        public static ItemStore SplitSupplyItems(City city, int part)
        {
            ItemStore result = new ItemStore();
            if (city == null || city.itemStore == null || part <= 0)
                return result;
            if (part > 100)
                part = 100;

            for (int i = 0; i < supplyWeaponKinds.Length; i++)
                MovePartItems(city, result, supplyWeaponKinds[i], part);

            MovePartItems(city, result, (int)ItemStoreKindType.Horse, part);
            return result;
        }

        /// <summary>按比例把城市某一类兵装移入目标容器。</summary>
        /// <param name="city">城市对象</param>
        /// <param name="dest">目标容器</param>
        /// <param name="kind">兵装种类</param>
        /// <param name="part">比例（1~100）</param>
        static void MovePartItems(City city, ItemStore dest, int kind, int part)
        {
            int have = city.itemStore.GetNumber(kind);
            if (have <= 0)
                return;

            int give = have * part / 100;
            if (give <= 0)
                return;

            city.itemStore.Remove(kind, give);
            dest.Add(kind, give);
        }

        /// <summary>
        /// AI创建运输部队逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="target">目标城市</param>
        /// <param name="troops">兵力</param>
        /// <param name="gold">金钱</param>
        /// <param name="food">粮食</param>
        /// <param name="itemStore">物品库存</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>创建的运输部队</returns>
        public static Troop AIMakeTransportTroop(City city, City target, int troops, int gold, int food, ItemStore itemStore, Scenario scenario)
        {
            // 【日志约定】本方法原先有 5 处静默 return，"运输队没出现"时无法定位卡在哪一步。
            // 现统一打 `*[运输AI]` 前缀：失败用 Warning/Error 并写明原因，成功用 Info 播报。
            if (city == null || target == null || scenario == null)
            {
                Sango.Log.Error("*[运输AI] 组建运输队失败：出兵城 / 目标城 / 场景为空");
                return null;
            }
            if (city.freePersons.Count <= 0)
            {
                Sango.Log.Warning($"*[运输AI]{city.Name} 组建运输队失败：无空闲武将");
                return null;
            }
            if (city.troops <= 0)
            {
                Sango.Log.Warning($"*[运输AI]{city.Name} 组建运输队失败：城中无兵");
                return null;
            }
            if (troops <= 0)
            {
                Sango.Log.Warning($"*[运输AI]{city.Name} 组建运输队失败：计划运送的兵力为 {troops}");
                return null;
            }

            // 【运输携带上限】剧本配置 →"运输部队携带上限"（对齐原版 max_unit_transport_*，
            // s11_db.h:200-206）。资源调度 / 港关回收 / 占位部队等多个调用方各自算运量，
            // 部分支线并没有自行夹取，统一在这里夹到剧本配置上限 + 城内库存，避免漏夹超载。
            ScenarioVariables limits = scenario.Variables;
            if (limits != null)
            {
                troops = System.Math.Min(troops, System.Math.Min(city.troops, limits.TransportTroopLimit));
                if (gold < 0) gold = 0;
                gold = System.Math.Min(gold, System.Math.Min(System.Math.Max(0, city.gold), limits.TransportGoldLimit));
                if (food < 0) food = 0;
                food = System.Math.Min(food, System.Math.Min(city.food, limits.TransportFoodLimit));

                // 兵装按种类逐个夹取：min(城内库存, 该类物品的**转换上限**)。
                // 与玩家运输同一口径：TransformLimit 对 p1=0(枪/戟/弩/马) 取原值，
                // 对 p1=10(器械/舰船) 取 1/100，不能直接拿配置值比较。
                if (itemStore != null && itemStore.Items.Count > 0)
                {
                    List<int> kinds = new List<int>(itemStore.Items.Keys);
                    for (int i = 0; i < kinds.Count; i++)
                    {
                        int kind = kinds[i];
                        ItemType itemType = scenario.GetObject<ItemType>(kind);
                        // 物品表缺项时不做夹取，避免把 AI 计划运量清成 0
                        if (itemType == null) continue;
                        int num = itemStore.GetNumber(kind);
                        int cap = System.Math.Min(city.itemStore != null ? city.itemStore.GetNumber(kind) : 0,
                                                  limits.GetTransportItemLimit(itemType));
                        if (num > cap) itemStore.Set(kind, cap);
                    }
                }
            }

            if (troops <= 0 || food <= 0)
            {
                Sango.Log.Warning($"*[运输AI]{city.Name} 组建运输队失败：运量被运输上限裁剪为 0（兵 {troops} 粮 {food}）");
                return null;
            }

            TroopType troopType = TroopType.GetTransportType(scenario, city.BelongForce);
            if (troopType == null)
            {
                Sango.Log.Error($"*[运输AI]{city.Name} 组建运输队失败：势力[{city.BelongForce?.Name}]没有可用的运输兵种配置");
                return null;
            }

            // 【修复】原先直接取 persons[0]，推荐结果为空时会抛 NullReferenceException
            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(city.freePersons);
            if (persons == null || persons.Length == 0 || persons[0] == null)
            {
                Sango.Log.Warning($"*[运输AI]{city.Name} 组建运输队失败：主将推荐为空（空闲武将 {city.freePersons.Count} 人）");
                return null;
            }
            Person leader = persons[0];
            city.freePersons.Remove(leader);

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            //troop.MaxMorale = city.MaxMorale;
            troop.IsAlive = true;
            troop.Leader = leader;
            troop.TroopType = troopType;
            // 【水战兜底】运输队跨水时用的是另一套兵种（走舸），与玩家手动组建一致
            // （CityTransport.cs:107 的 `WaterTroopType = scenario.GetObject<TroopType>(8)`）。
            // 缺了它，`Troop.TroopType` 在水上取值为 null（IsInWater ? WaterTroopType : LandTroopType），
            // 表现为"运输队一下水就走不动 / 寻路失败"。
            troop.WaterTroopType = ResolveTransportWaterType(scenario, city.BelongForce);
            // 【意图层】运输队对应原版 DeployType_Transport
            // （s11_game_ai_kyoten_cmd_mil_transport.cpp:181-186 的 param.deploy_type / deploy_target）。
            // 写了它，Troop.UpdateAIOrder 的输送分支才能在目标失效时把任务纠正回"运到目标城"；
            // 必须在 SetMission 之前写，避免 Prepare 级联读到 None 意图。
            troop.deployType = (int)TroopDeployType.Transport;
            troop.deployTarget = target.Id;
            city.troops -= troops;
            city.gold -= gold;
            city.food -= food;
            troop.food = food;
            troop.gold = gold;
            troop.troops = troops;
            if (itemStore != null)
            {
                troop.itemStore = itemStore;
                city.itemStore.Remove(itemStore);
            }
            troop.Member1 = null;
            troop.Member2 = null;
            city.Render?.UpdateRender();

            // 【关键修复】原实现在此直接 return 一个只存在于内存的 Troop：
            // 它没有 cell、没有 Render、不在 scenario.troopsSet 里 —— 地图上永远看不到，
            // 但调用方已经扣了行动力 / 粮 / 金并 MarkExecuted()，表现为"物资凭空蒸发、没有运输队"。
            // 对齐原版 s11_game_ai_kyoten_cmd_mil_transport.cpp:188 的 `commander_->com_deploy(&param)`：
            // 原版 com_deploy 会真正 unit_create + unit_init + 放到地图上；本项目的等价入口是
            // CityTroopFactory.EmitTroop（EnsureTroop 完成初始化 + 出城可行性校验 + 失败自动退回）。
            Troop emitted = CityTroopFactory.EmitTroop(city, troop, scenario);
            if (emitted == null)
            {
                // EmitTroop 内部已把兵 / 粮 / 金 / 兵装 / 武将退回城池（CancelEmitTroop）
                Sango.Log.Warning($"*[运输AI]{city.Name} 组建运输队失败：部队无法出城（城池被围困或城头无可停留格），物资已退回");
                return null;
            }

            emitted.SetMission(MissionType.TroopTransformGoodsToCity, target.Id);
            emitted.NeedPrepareMission();

            // 【立即出城】项目硬规则：部队不允许停留在城市格。组建常常发生在部队行动阶段之后，
            // 不在这里推一次，运输队会杵在城头整整一个回合。
            // 对齐 s11_sys_person_order_military.cpp:331-353（unit_set_order 后立刻 run_order）。
            if (!CityTroopFactory.DriveOutOfCity(emitted, scenario))
            {
                Sango.Log.Warning($"*[运输AI]{city.Name} 运输队组建后无法出城（城头无可停留格或寻路失败），本次运输取消，物资已退回");
                CityTroopFactory.RevokeEmit(city, emitted);
                return null;
            }

            Sango.Log.Info($"*[运输AI]{scenario.GetDateStr()}{city.BelongForce?.Name}在{city.Name}组建运输队「{emitted.Name}」" +
                           $"（{leader.Name} 押运，兵{troops} 粮{food} 金{gold}）→ {target.Name}");
            return emitted;
        }

        /// <summary>
        /// 运输队的水上兵种 —— 走舸兜底。
        ///
        /// 【原版依据】原版运输队只有一个单位类型 <c>UnitType_Transport</c>，水陆通用
        /// （s11_game_ai_kyoten_cmd_mil_transport.cpp:181 `param.type = UnitType_Transport`）。
        /// 本项目把"陆上运输 / 水上运输"拆成两个兵种字段，陆上取运输（kind=7），
        /// 水上必须另配船只，否则 <see cref="Troop.TroopType"/> 在水上会取到 null。
        ///
        /// 【选型】与玩家手动组建运输队保持一致，固定取走舸（Id = 8，见 TroopTypes.json）；
        /// 走舸缺失（Mod 删改数据）时退而求其次取任意可用水上兵种；
        /// 仍找不到就返回 null，交给 <c>Troop.Init</c> 的既有兜底（Troop.cs:1380-1381）。
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <param name="force">所属势力（用于兵种可用性校验）</param>
        /// <returns>水上兵种；数据缺失时返回 null</returns>
        static TroopType ResolveTransportWaterType(Scenario scenario, Force force)
        {
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.TroopTypes == null)
                return null;

            // 首选走舸（Id = 8）—— 玩家手动运输队用的就是它
            TroopType walkBoat = scenario.GetObject<TroopType>(8);
            if (walkBoat != null && !walkBoat.isLand)
                return walkBoat;

            // 兜底：任意可用水上兵种
            for (int i = 1; i < scenario.CommonData.TroopTypes.Count; i++)
            {
                TroopType type = scenario.CommonData.TroopTypes[i];
                if (type != null && !type.isLand && type.IsValid(force))
                    return type;
            }

            return walkBoat;
        }

    }


}
