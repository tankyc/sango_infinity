using Sango.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sango.Core
{
    public class CityAI
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

            // ---------- 一、防守优先 ----------
            // 【修复】本城受到直接威胁时,无论当前是否正在进攻他城,都必须优先转为防守。
            // 原逻辑仅在 TroopMissionType == None 时才检查防守,导致"正在进攻他城"的城市
            // 即便老家被围攻也不会出城防守。
            bool needDefense = AICanDefense(city, scenario);
            if (needDefense && city.TroopMissionType != MissionType.TroopProtectCity)
            {
                city.TroopMissionType = MissionType.TroopProtectCity;
                city.TroopMissionTargetId = city.Id;
                // 召回在外进攻的本城部队回援
                RecallAttackingTroops(city, scenario);
            }

            // ---------- 二、已有军事任务:持续派遣部队 ----------
            if (city.TroopMissionType != MissionType.None)
                return ContinueMilitaryMission(city, scenario);

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
            City prioritySubCity = FindPrioritySubCity(city);
            if (prioritySubCity != null)
            {
                city.TroopMissionType = MissionType.TroopOccupyCity;
                city.TroopMissionTargetId = prioritySubCity.Id;
                return false;
            }

            // 3.4 挑选进攻目标
            return DecideAttackTarget(city, scenario);
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
        /// 进攻任务：按目标城池规模决定出兵上限并派遣。
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        static void DispatchOccupyTroop(City city, Scenario scenario)
        {
            City targetCity = scenario.citySet.Get(city.TroopMissionTargetId);
            if (targetCity == null)
                return;

            // 【优化】按目标城池兵力确定出兵上限(打大城派更多部队)，
            // 替代原先每次判定都随机抖动的出兵门槛。
            AIConfig aiConfig = AIConfig.Instance;
            int maxAttackTroops = targetCity.BelongForce == null
                ? aiConfig.attackWhiteCityTroopCount
                : aiConfig.attackBaseTroopCount + targetCity.troops / Math.Max(1, aiConfig.attackTroopsPerTargetTroops);
            // 【修复】使用"正在进攻的部队数"而不是"所有在外部队数"，
            // 否则去求援 / 撤离中的部队会占用进攻名额，导致城市误判"已派够"而不再补派。
            if (city.AttackingTroopsCount >= maxAttackTroops)
                return;

            Troop troop = AIMakeTroop(city, 20, true, scenario);
            if (troop == null)
                return;

            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领{troop.TroopType.Name}军队出城 进攻{targetCity.BelongForce?.Name}的{targetCity.Name}!");
        }

        /// <summary>
        /// 防守任务：本城周边有敌情时派遣守军出城。
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
            if (city.AttackTroopsCount >= Math.Max(3, defenseEnemyCount + 2))
                return;

            Troop troop = AIMakeTroop(city, 20, false, scenario);
            if (troop == null)
                return;

            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            Sango.Log.Info($"{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城防守!");
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

            Troop troop = AIMakeTroop(city, 10, false, scenario);
            if (troop == null)
                return true;

            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
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

                // 需要兵力充足
                if (city.troops < 20000)
                    return;

                int weight = (int)(2500 * (float)city.virtualFightPower / x.virtualFightPower);
                weight = weight * x.DurabilityLimit / x.durability;
                int relation = scenario.GetRelation(city.BelongForce, x.BelongForce);
                // 8000亲密 6000友好 4000普通 2000中立 0冷漠 -2000敌对 -4000厌恶 -6000仇视 -8000不死不休
                // 关系越好权重越低(越不愿进攻)，越敌对权重越高
                weight = UnityEngine.Mathf.FloorToInt((float)weight * (1f - (float)relation / 10000f));
                if (x.BelongForce.IsPlayer)
                    weight += 1500;

                priorityQueue.Push(x, weight);
            });

            int count = GameRandom.Range(0, UnityEngine.Mathf.Max(0, priorityQueue.Count) + 1);
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
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 【修复】召回本城在外进攻的部队回援本城。
        /// 当本城受到攻击时,正在执行 TroopOccupyCity 的本城部队应转为协防本城。
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

            Troop troop = AIMakeTroop(city, 10, false, scenario);
            if (troop != null)
            {
                troop = CityTroopFactory.EmitTroop(city, troop, scenario);
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领援军出城 支援{target.Name}!");
            }

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
            // 兵临城下时不进行内政建设
            if (city.IsEnemiesRound(9))
                return true;

            AIBuilding(city, scenario);
            return true;
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

            if (city.freePersons.Count == 0)
                return true;

            // 【优化】原先为 (有在野 && Chance(80)) || Chance(20)，两个随机条件叠加，
            // 实际概率约 84% 且难以预期。现改为单一明确概率：有在野武将时提高搜索意愿。
            AIConfig cfg = AIConfig.Instance;
            int chance = city.invisiblePersons.Count > 0
                ? cfg.searchBaseChance + 20
                : cfg.searchBaseChance;
            if (chance <= 0)
                return true;

            if (GameRandom.Chance(Math.Min(100, chance)))
            {
                Person[] recommandList = ForceAI.CounsellorRecommendSearching(city.freePersons, city, recommandSearchingFeatrues);
                if (recommandList != null && recommandList.Length > 0)
                {
                    city.JobSearching(recommandList.ToArray());
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
            int rewarded = 0;
            for (int i = 0; i < city.allPersons.Count; i++)
            {
                Person person = city.allPersons[i];
                if (person == null || person.mBelongTroop != null)
                    continue;
                if (person.loyalty > cfg.rewardLoyaltyThreshold)
                    continue;
                if (city.gold <= cfg.rewardGoldKeep)
                    break;

                city.JobRewardPerson(person);
                rewarded++;
                if (rewarded >= cfg.rewardMaxPersonPerTurn)
                    break;
            }
            return true;
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

            // 【前期优先】开局前 N 回合再放宽 1 个名额（与部署层同口径）
            int forceTurn = DeploymentState.currentForceTurn;
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

        /// <summary>
        /// AI物资运输逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AITransfrom(City city, Scenario scenario)
        {
            if (city.IsBorderCity) return true;

            if (city.freePersons.Count == 0) return true;

            AIConfig cfg = AIConfig.Instance;
            if (city.troops < cfg.transportMinTroops || city.food < cfg.transportMinFood)
                return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.TransportDisable) == 1)
            {
                return true;
            }

            int appointValue = city.BelongCorps.GetAppointValue(Corps.AppointContentType.Transport);
            City targetTransportCity = null;
            if (appointValue > 0)
            {
                targetTransportCity = scenario.citySet.Get(appointValue);
            }

            City target = null;
            if (targetTransportCity != null)
            {
                if (targetTransportCity == city)
                    return true;

                // 使用寻路方法获取距离
                List<City> path = scenario.FindShortestPathInForce(city, targetTransportCity);
                if (path == null || path.Count == 0)
                {
                    return true;
                }

                if (path[0] == city && path.Count == 1)
                {
                    return true;
                }

                if (path[0] == city)
                    target = path[1];
                else
                    target = path[0];
            }
            else
            {
                // 找到更近的圈层
                List<City> list = new List<City>();
                for (int i = 0; i < city.NeighborList.Count; i++)
                {
                    City neighbor = city.NeighborList[i];
                    if (neighbor.borderLine < city.BorderLine)
                    {
                        list.Add(neighbor);
                    }
                }

                if (list.Count == 0) return true;
                target = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    City neighbor = list[i];
                    if (neighbor.troops < target.troops)
                        target = neighbor;
                }
            }

            if (target == null)
                return true;

            if (target.troops >= target.TroopsLimit || target.food >= target.foodLimit)
                return true;

            TroopType troopType = TroopType.GetTransportType(scenario, city.BelongForce);
            if (troopType == null) return true;

            //运输比例
            int part = scenario.Variables.TransportPercent - Math.Max(2 - city.BorderLine, 0) * 20;

            // 【修复】原先直接取 persons[0]，推荐结果为空时会抛 NullReferenceException
            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(city.freePersons);
            if (persons == null || persons.Length == 0 || persons[0] == null)
                return true;
            Person leader = persons[0];
            city.freePersons.Remove(leader);

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            //troop.MaxMorale = city.MaxMorale;
            troop.Leader = leader;
            troop.TroopType = troopType;
            if (target.troops < target.TroopsLimit)
            {
                int left = target.TroopsLimit - target.troops;
                int troops = Math.Min(left, city.troops * part / 100);
                city.troops -= troops;
                troop.troops = troops;
            }
            else
            {
                // 最低运输兵力
                int troops = 1;
                city.troops -= troops;
                troop.troops = troops;
            }

            if (target.gold < target.GoldLimit)
            {
                int left = target.GoldLimit - target.gold;
                int gold = Math.Min(left, city.gold * part / 100);
                city.gold -= gold;
                troop.gold = gold;
            }
            if (target.food < target.FoodLimit)
            {
                int left = target.FoodLimit - target.food;
                int food = Math.Min(left, city.food * part / 100);
                city.food -= food;
                troop.food = food;
            }
            troop.Member1 = null;
            troop.Member2 = null;
            troop.itemStore = city.itemStore.Split(part);
            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领运输队{troop.troops}出城 向{target.BelongForce?.Name}的{target.Name}运输物资!");
            troop.SetMission(MissionType.TroopTransformGoodsToCity, target.Id);
            return true;
        }

        /// <summary>
        /// AI向所属城市运输物资逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AITransfromToBelongCity(City city, Scenario scenario)
        {
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.TransportDisable) == 1)
            {
                return true;
            }

            if (city.IsEnemiesRound()) return true;

            if (city.BelongCity == null) return true;

            if (city.food > city.FoodLimit * 95 / 100 && city.gold > city.GoldLimit * 95 / 100) return true;

            //if (city.BelongCity.BelongForce != city.BelongForce)
            //    return true;

            // 必须军团一致性
            if (city.BelongCity.BelongCorps != city.BelongCorps)
                return true;

            // 寻找最近的附属城市
            //City target = city.GetNearnestForceCity();
            City target = city.BelongCity;

            if (target == null) return true;

            // 边境城市不做运输
            if (target.IsBorderCity) return true;

            AIConfig cfg = AIConfig.Instance;
            int goldLine = cfg.belongTransportGoldLine;
            int foodLine = cfg.belongTransportFoodLine;

            // 资源不够, 人员进入附属城池
            if (city.gold <= goldLine && city.food <= foodLine)
            {
                if (city.freePersons.Count > 0)
                {
                    for (int i = city.freePersons.Count - 1; i >= 0; i--)
                    {
                        Person x = city.freePersons[i];
                        if (x == null) continue;

                        // 【Phase C】统一过执行层闸门：港关 / 前线的守备下限不满足时不再抽人
                        // （这条路径原本会把港关守将直接迁走，是"港关只出不进"的元凶）
                        DeploymentWeights deployCfg = AIConfig.Instance != null ? AIConfig.Instance.deployment : null;
                        if (!DeploymentExecutor.CanTransfer(x, target, deployCfg).allowed)
                            continue;

                        x.TransformToCity(target);

                        // 【Phase C 收口 · 修 Clear 造成的连带丢失】
                        // 原写法是循环后 city.freePersons.Clear()：被闸门 continue 掉、
                        // **仍留在本城**的人也会被一并从空闲表删除 → 内政命令 / 登用 / 探索
                        // 等系统从此"看不到"他们（人要等下次重建空闲表才会再出现）。
                        // 改为只移除"确实被调走"的那个人；若 TransformToCity 内部已移除，
                        // 这里做一次带边界与引用的校验，避免误删他人。
                        if (i < city.freePersons.Count && ReferenceEquals(city.freePersons[i], x))
                            city.freePersons.RemoveAt(i);
                    }
                }
                return true;
            }

            if (target.gold >= target.GoldLimit * 9 / 10 && target.food >= target.FoodLimit * 9 / 10)
                return true;
            if (city.food <= foodLine && city.gold <= goldLine)
                return true;

            // 检查通路（途经敌方建筑会阻断运输）
            if (!IsPathClear(city, target, scenario))
                return true;

            // 资源够运输, 但是兵力不够, 请求兵力输送, 需要军团一致性
            if (city.troops < 500)
            {
                // 有正在运输的部队,不再请求运输队
                for (int i = 0; i < scenario.troopsSet.Count; ++i)
                {
                    var c = scenario.troopsSet[i];
                    if (c != null && c.IsAlive && c.BelongForce == city.BelongForce)
                    {
                        if (c.IsTransport && c.missionType == (int)MissionType.TroopTransformGoodsToCity && c.missionTarget == city.Id)
                            return true;
                    }
                }

                // 从主城运输兵力过来
                Troop transport = AIMakeTransportTroop(target, city, 1000, 0, 3000, null, scenario);
                if (transport != null)
                {
                    transport.missionParams1 = 1;
                    transport = CityTroopFactory.EmitTroop(target, transport, scenario);
                    city.CurActiveTroop = transport;
                    Sango.Log.Info($"{scenario.GetDateStr()}{target.BelongForce.Name}势力在{target.Name}由{transport.Leader.Name}率领运输队出城 向{city.BelongForce?.Name}的{city.Name}运输物资!");
                }
                return true;
            }
            Person[] persons;
            if (city.allPersons.Count == 0)
            {
                // 求人
                persons = ForceAI.CounsellorRecommendTransportTroop(target.freePersons);
                if (persons == null)
                {
                    return true;
                }
                Person who = persons[0];
                if (who != null)
                    who.TransformToCity(city);
                return true;
            }
            //运输比例
            int part = scenario.Variables.TransportPercent;
            int gold = 0, food = 0;
            if (target.gold < target.GoldLimit)
            {
                gold = Math.Min(target.GoldLimit - target.gold, city.gold * part / 100);
            }
            if (target.food < target.FoodLimit)
            {
                food = Math.Min(target.FoodLimit - target.food, city.food * part / 100);
            }
            ItemStore itemStore = city.itemStore.Split(part, true);
            Troop troop = AIMakeTransportTroop(city, target, 100, gold, food, itemStore, scenario);
            if (troop != null)
            {
                troop = CityTroopFactory.EmitTroop(city, troop, scenario);
                troop.missionParams1 = 1;
                city.CurActiveTroop = troop;
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领运输队出城 向{target.BelongForce?.Name}的{target.Name}运输物资!");
            }
            return true;
        }

        /// <summary>
        /// 检查两城之间的直线通路是否畅通（途经的敌方建筑会阻断运输）。
        /// </summary>
        /// <param name="city">出发城市</param>
        /// <param name="target">目标城市</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>通路是否畅通</returns>
        static bool IsPathClear(City city, City target, Scenario scenario)
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

            if (city.IsEnemiesRound())
                return true;

            int count = 1;
            if (city.freePersons.Count > 6)
                count = count + (city.freePersons.Count - 3) / 3;

            // 【优化】原先忽略子调用返回值，即使一次建设都没发生也会继续循环。
            // 现按实际结果提前结束，避免无效调用。
            for (int i = 0; i < count; i++)
            {
                if (!AIBuildIntriore(city, scenario))
                    break;
            }
            for (int i = 0; i < count; i++)
            {
                if (!AIBuildingLevelUp(city, scenario))
                    break;
            }

            AIBuildMilitaryBuilding(city, scenario);
            return true;
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
            if (city.areaCellList.Count == 0)
                return true;

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
            if (AIConfig.Instance.useFrontBuilding
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

            BuildingType buildingType = BattleSituation.SelectFrontBuildingType(best, force, scenario);
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

            // 建址在派遣期间被占用（例如被别的部队占据）则放弃
            if (!dest.CanBuild || !dest.IsEmpty() || dest.IsInterior
                || dest.SpiralHasBuilding(Math.Max(1, scenario.Variables.BuildingSpace)))
                return false;

            TroopType troopType = scenario.GetObject<TroopType>(1);

            // 组建修建队伍
            Person[] builders = ForceAI.CounsellorRecommendBuild(city.freePersons, buildingType);
            if (builders == null || builders.Length == 0)
                return false;

            int maxTroopNum = 3000;
            int food = (int)(maxTroopNum * scenario.Variables.baseFoodCostInTroop * 20);

            // 资金：固定额度优先，否则按造价倍率推算
            int carrayGold = cfg.frontBuildBudget > 0
                ? cfg.frontBuildBudget
                : buildingType.cost * Math.Max(100, cfg.frontBuildGoldCostPercent) / 100;
            if (carrayGold > city.gold)
                carrayGold = city.gold;
            // 连一座都建不起就没必要出征
            if (carrayGold < buildingType.cost)
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
            // 【角色】显式标记为工兵：EmitTroop 只在 role == Auto 时才自动推导，
            // 因此这里设定后不会被覆盖，评分体系会按工兵权重使其极力规避接战。
            troop.role = TroopRole.Engineer;
            if (builders.Length > 1) troop.Member1 = builders[1];
            // 【修正】原实现此处误写为 Member1，导致第二名副将被丢弃
            if (builders.Length > 2) troop.Member2 = builders[2];
            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            city.CurActiveTroop = troop;
            troop.SetMission(MissionType.TroopBuildBuilding, buildingType.Id);
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
            if (city.IsInteriorBuildFull())
                return false; // 返回false表示没有建设

            if (city.freePersons.Count < 1) // 至少需要1个武将进行建设
                return false;

            if (city.gold < AIConfig.Instance.buildMinGold) // 建设所需的最低金钱
                return false;

            // 根据城市类型选择建筑模板
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

        /// <summary>
        /// AI根据模板建造建筑逻辑
        /// </summary>
        /// <param name="templateId">模板ID</param>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIBuildingTemplate(int templateId, City city, Scenario scenario)
        {
            if (city.gold < AIConfig.Instance.buildMinGold)
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
                    return false;

                Cell bestPlace = buildingType.GetBestPlace(city);
                if (bestPlace == null)
                    return false;

                Person[] people = ForceAI.CounsellorRecommendBuild(city.freePersons, buildingType);
                if (people != null && people.Length > 0)
                {
                    int buildAbility = GameUtility.Method_PersonBuildAbility(people);
                    int turnCount = buildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
                    int buildCount = Math.Min(Scenario.Cur.Variables.BuildMaxTurn, buildingType.durabilityLimit / buildAbility + turnCount);
                    // 【性格】建造效率：按执行武将性格缩短工期
                    buildCount = city.ApplyPersonalityBuildCounter(buildCount, people);
                    city.JobBuildBuilding(bestPlace, people, buildingType, buildCount);
                    return true; // 成功建设
                }
            }

            return false; // 没有建设
        }

        /// <summary>
        /// AI升级建筑逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIBuildingLevelUp(City city, Scenario scenario)
        {
            if (city.gold < AIConfig.Instance.buildMinGold)
                return false;

            if (city.freePersons.Count < 1)
                return false;

            // 需要全部建造完毕
            if (!city.IsInteriorBuildFull())
                return false;

            for (int i = 0; i < city.allBuildings.Count; ++i)
            {
                Building building = city.allBuildings[i];
                if (building.isComplate && !building.isUpgrading && building.BuildingType.nextId > 0)
                {
                    BuildingType nextBuildingType = scenario.GetObject<BuildingType>(building.BuildingType.nextId);
                    int cost = nextBuildingType.cost;
                    if (city.gold < cost)
                        return false;

                    Person[] people = ForceAI.CounsellorRecommendBuild(city.freePersons, nextBuildingType);
                    if (people != null && people.Length > 0)
                    {
                        int buildAbility = GameUtility.Method_PersonBuildAbility(people);
                        int turnCount = nextBuildingType.durabilityLimit % buildAbility == 0 ? 0 : 1;
                        int buildCount = Math.Min(Scenario.Cur.Variables.BuildMaxTurn, nextBuildingType.durabilityLimit / buildAbility + turnCount);
                        // 【性格】建造效率：按执行武将性格缩短工期
                        buildCount = city.ApplyPersonalityBuildCounter(buildCount, people);
                        if (buildCount <= AIConfig.Instance.buildUpgradeMaxTurn)
                        {
                            city.JobUpgradeBuilding(building, people, nextBuildingType, buildCount);
                            return true; // 成功升级
                        }
                    }
                }
            }
            return false; // 没有升级
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

            // 轻视士兵,70%概率不考虑此行动
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Store_Troops) == 1)
            {
                if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
                {
                    if (GameRandom.Chance(80))
                        return true;
                }
                else
                {
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
        /// AI是否可以攻击
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否可以攻击</returns>
        public static bool AICanAttack(City city, Scenario scenario)
        {
            if (scenario.TurnCount < scenario.Variables.AIAttackProtectedCount)
                return false;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Attack) == 1)
            {
                city.TroopMissionType = MissionType.None;
                city.TroopMissionTargetId = 0;
                return false;
            }

            // 获取AI个性
            ForceAI.AIPersonalityType PersonalityId = GetAIPersonality(city);

            // 根据AI个性调整兵力要求
            int minTroops = PersonalityId == ForceAI.AIPersonalityType.Aggressive ? 8000 :
                           PersonalityId == ForceAI.AIPersonalityType.Defensive ? 12000 : 10000;
            if (city.troops < minTroops)
                return false;

            if (city.morale < 60 || city.security < 50)
                return false;

            if (city.freePersons.Count < 2)
                return false;

            if (!city.IsBorderCity)
                return false;

            if (city.IsEnemiesRound(6))
                return false;

            List<City> enemiesCities = new List<City>();
            city.ForeachNeighborCities(x =>
            {
                if (x.IsEnemy(city))
                    enemiesCities.Add(x);
            });

            if (enemiesCities.Count == 0)
                return false;

            // 根据AI个性调整攻击概率
            int attackChance = PersonalityId == ForceAI.AIPersonalityType.Aggressive ? 50 :
                              PersonalityId == ForceAI.AIPersonalityType.Defensive ? 80 : 70;
            if (GameRandom.Chance(attackChance))
                return false;

            return true;
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
        /// AI生产兵装逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AICreateItems(City city, Scenario scenario)
        {
            if (city.freePersons.Count == 0) return true;

            if (city.gold < AIConfig.Instance.createItemsMinGold)
                return true;

            Building freeBlacksmithShop = city.GetFreeBuilding((int)BuildingKindType.BlacksmithShop);
            Building freeStable = city.GetFreeBuilding((int)BuildingKindType.Stable);
            if (freeBlacksmithShop == null && freeStable == null)
                return true;

            int totalNum = 0;
            for (int itemTypeId = 2; itemTypeId <= 5; itemTypeId++)
                // 获取总兵装
                totalNum += city.itemStore.GetNumber(itemTypeId);

            // 达到一定兵装后,如果金钱太少则有概率跳过
            if (totalNum > city.troops * 2 / 3 && city.gold < AIConfig.Instance.createItemsMinGold && GameRandom.Chance(30))
                return true;

            // 统计适应偏向
            int[] levelTotal = new int[4] { 1, 1, 1, 1 };

            if (city.allPersons.Count > 8)
            {
                city.allPersons.ForEach(x =>
                {
                    levelTotal[0] += x.SpearLv;
                    levelTotal[1] += x.HalberdLv;
                    levelTotal[2] += x.CrossbowLv;
                    levelTotal[3] += x.RideLv;
                });
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

                int itemNum = city.itemStore.GetNumber(itemTypeId);
                if (itemNum >= city.storeLimit) continue;

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
                return true;

            int[] pr = new int[validItem.Count];
            for (int i = 0; i < pr.Length; i++)
                pr[i] = levelTotal[validItem[i] - 2];

            int targetItemId = validItem[GameRandom.RandomWeightIndex(pr)];
            Person[] people = ForceAI.CounsellorRecommendCreateItems(city.freePersons);
            if (people == null) return true;
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
            if (totalNum >= targetItemType.TransformLimit(city.StoreLimit)) return true;

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

            if (totalNum >= targetItemType.TransformLimit(city.StoreLimit)) return true;

            if (totalNum > (city.troops / 2) * targetItemType.p1 / 1000 + 1 && GameRandom.Chance(20) )
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

            int minEquipNeed = 5000;
            if (isAttack)
            {
                if (city.freePersons.Count < 2) return null;
                if (city.troops < scenario.Variables.minTroopsKeepWhenAttack) return null;
                if (city.food < scenario.Variables.minFoodKeepWhenAttack) return null;
            }
            else
            {
                if (city.troops < scenario.Variables.minTroopsKeepWhenDefence) return null;
                if (city.food < scenario.Variables.minTroopsKeepWhenDefence) return null;
                minEquipNeed = 1500;
            }

            // 所有满足5000兵的部队类型
            List<TroopType> costEnoughTroopTypes = new List<TroopType>();
            TroopType.GetCostEnoughTroopTypeList(city, costEnoughTroopTypes, minEquipNeed);

            // 去掉剑兵(进攻不允许)
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

            if (costEnoughTroopTypes.Count == 0)
                return null;

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

            // 【修复】推荐结果为空时直接返回，避免后续 people[0] 抛 NullReferenceException
            Person[] people = ForceAI.CounsellorRecommendMakeTroop(city.freePersons, spType, maxPersonCount);
            if (people == null || people.Length == 0 || people[0] == null)
                return null;

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            troop.Leader = people[0];
            troop.TroopType = spType;

            if (people.Length > 1) troop.Member1 = people[1];
            if (people.Length > 2) troop.Member2 = people[2];

            // 计算最大兵力
            troop.CalculateMaxTroops();

            // 确定兵数
            int maxTroopNum = troop.MaxTroops;
            if (city.troops < maxTroopNum * 2)
                maxTroopNum = (city.troops / 2000) * 1000;

            maxTroopNum = city.itemStore.CheckCostMin(spType.costItems, maxTroopNum);

            // 粮食
            int turnCostFood = (int)(maxTroopNum * scenario.Variables.baseFoodCostInTroop);
            int food = turnCostFood * minTurn;
            while (city.food < food)
            {
                if (isAttack) return null;

                minTurn = minTurn - 4;
                if (minTurn < 5)
                    return null;

                food = turnCostFood * minTurn;
            }
            troop.WaterTroopType = scenario.GetObject<TroopType>(8);
            troop.LandTroopType.Cost(city, maxTroopNum);
            troop.WaterTroopType.Cost(city, maxTroopNum);
            city.troops -= maxTroopNum;
            city.food -= food;
            for (int p = 0; p < people.Length; p++)
                city.freePersons.Remove(people[p]);

            troop.troops = maxTroopNum;
            troop.food = food;

            // 【前线建筑 / B3 随军增筑】让作战部队携带少量资金。
            // 有了现金，前线部队才可能在战场就地修建军乐台 / 砦等辅助建筑，
            // 而不必等待专职工程队从后方赶来。
            // 携带额同时受"占总兵力比例"与"城池余额"双重限制，避免小部队带走过多资金。
            AIConfig aiCfg = AIConfig.Instance;
            if (aiCfg.troopCarryGold > 0 && city.gold > aiCfg.frontBuildMinCityGold)
            {
                int carry = aiCfg.troopCarryGold;
                if (aiCfg.troopCarryGoldPerTroops > 0)
                {
                    int byTroops = maxTroopNum * aiCfg.troopCarryGoldPerTroops / 10000;
                    if (byTroops < carry)
                        carry = byTroops;
                }
                // 保留城池运转所需的资金
                int usable = city.gold - aiCfg.frontBuildMinCityGold;
                if (carry > usable)
                    carry = usable;
                if (carry > 0)
                {
                    troop.gold = carry;
                    city.gold -= carry;
                }
            }

            // 渲染刷新统一由 CityTroopFactory.EmitTroop 负责（此时部队尚未登记进场景）
            troop.SetMission(city.TroopMissionType, city.TroopMissionTargetId);
            return troop;
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
            city.freePersons.Remove(leader);

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

            city.troops -= carryTroops;
            city.food -= carryFood;

            troop = CityTroopFactory.EmitTroop(city, troop, scenario);
            troop.SetMission(MissionType.TroopSupplyTroop, needy.Id);
            troop.NeedPrepareMission();
            city.CurActiveTroop = troop;
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
            if (city.freePersons.Count <= 0) return null;
            if (city.troops <= 0) return null;
            if (troops <= 0)
            {
                Sango.Log.Error("why 00!!");
                return null;
            }
            TroopType troopType = TroopType.GetTransportType(scenario, city.BelongForce);
            if (troopType == null) return null;

            // 【修复】原先直接取 persons[0]，推荐结果为空时会抛 NullReferenceException
            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(city.freePersons);
            if (persons == null || persons.Length == 0 || persons[0] == null)
                return null;
            Person leader = persons[0];
            city.freePersons.Remove(leader);

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            //troop.MaxMorale = city.MaxMorale;
            troop.IsAlive = true;
            troop.Leader = leader;
            troop.TroopType = troopType;
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
            troop.SetMission(MissionType.TroopTransformGoodsToCity, target.Id);
            return troop;
        }

    }


}
