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

            if (city.TroopMissionType != MissionType.None)
            {
                if (city.TroopMissionType == MissionType.TroopOccupyCity)
                {
                    City targetCity = scenario.citySet.Get(city.TroopMissionTargetId);
                    if ((targetCity.BelongForce != null && city.AttackTroopsCount < GameRandom.Range(8, 30)) || (targetCity.BelongForce == null && city.AttackTroopsCount < 2))
                    {
                        // 白城只去2支部队
                        Troop troop = AIMakeTroop(city, 20, true, scenario);
                        if (troop != null)
                        {
                            troop = city.EnsureTroop(troop, scenario);
                            city.CurActiveTroop = troop;
#if SANGO_DEBUG

                            Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}3势力在{city.Name}由{troop.Leader.Name}率领{troop.TroopType.Name}军队出城 进攻{targetCity.BelongForce?.Name}的{targetCity.Name}!");
#endif
                        }
                    }
                }
                else if (city.TroopMissionType == MissionType.TroopProtectCity)
                {
                    if (city.CheckEnemiesIfAlive())
                    {
                        if (city.AttackTroopsCount < Math.Max(3, city.EnemyCount + 2))
                        {
                            Troop troop = AIMakeTroop(city, 20, false, scenario);
                            if (troop != null)
                            {
                                troop = city.EnsureTroop(troop, scenario);
                                city.CurActiveTroop = troop;
#if SANGO_DEBUG
                                Sango.Log.Info($"{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城防守!");
#endif
                            }
                        }
                    }
                }

                if (city.CurActiveTroop == null)
                {
                    city.TroopMissionType = MissionType.None;
                    return true;
                }
                else
                {
                    //city.EnsureTroop(city.CurActiveTroop, scenario);
                    city.Render?.UpdateRender();
                }
                return false;
            }

            if (AICanDefense(city, scenario))
            {
                city.TroopMissionType = MissionType.TroopProtectCity;
                city.TroopMissionTargetId = city.Id;
                return false;
            }
            else if (AICanAttack(city, scenario))
            {
                City lastTargetCity = null;
                Troop activedTroop = scenario.troopsSet.Find(x => x.IsAlive && x.BelongCity == city);
                if (activedTroop != null)
                {
                    if (activedTroop.missionType == (int)MissionType.TroopOccupyCity)
                    {
                        lastTargetCity = scenario.citySet.Get(activedTroop.missionTarget);
                    }
                }

                if (lastTargetCity != null)
                {
                    city.TroopMissionType = MissionType.TroopOccupyCity;
                    city.TroopMissionTargetId = lastTargetCity.Id;
                    return false;
                }

                // 计算进攻概率
                priorityQueue.Clear();
                city.ForeachNeighborCities(x =>
                {
                    if (x.IsEnemy(city) && city.BelongCorps.CheckTargetIsAppointTarget(x))
                    {
                        if (x.BelongForce == null)
                        {
                            priorityQueue.Push(x, 9999);
                        }
                        else
                        {
                            // 需要兵力充足
                            //if (city.troops > 30000 || city.troops > x.troops - 5000)
                            if (city.troops >= 20000)
                            {
                                // 范围大约在
                                int weight = (int)(2500 * (float)city.virtualFightPower / x.virtualFightPower);
                                weight = weight * x.DurabilityLimit / x.durability;
                                int relation = scenario.GetRelation(city.BelongForce, x.BelongForce);
                                // 8000亲密 6000友好 4000普通 2000中立 0冷漠 -2000敌对 -4000厌恶 -6000仇视 -8000不死不休
                                // 5 4 3 2 1 0 -1 -2 -3 -4 -5
                                // 0 1 2 3 4 5 6 7 8 9 10
                                weight = UnityEngine.Mathf.FloorToInt((float)weight * (1f - (float)relation / 10000f));
                                if (x.BelongForce.IsPlayer)
                                {
                                    weight += 1500;
                                }
                                priorityQueue.Push(x, weight);
                            }
                        }
                    }
                });

                int count = GameRandom.Range(0, UnityEngine.Mathf.Max(0, priorityQueue.Count) + 1);
                for (int i = 0; i < count; i++)
                {
                    int priority = 0;
                    City targetCity = priorityQueue.Higher(out priority);
                    if (targetCity != null)
                    {
                        if (GameRandom.Chance(priority, 10000))
                        {
                            city.TroopMissionType = MissionType.TroopOccupyCity;
                            city.TroopMissionTargetId = targetCity.Id;
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 内政
        /// </summary>
        /// <param name="scenario"></param>
        public static bool AIIntrior(City city, Scenario scenario)
        {
            //AIRewardPerson(city, scenario);

            // 兵临城下
            if (city.IsEnemiesRound(9))
                return true;

            //AIResearch(city, scenario);
            AIBuilding(city, scenario);
            //AIIntriorBalance(city, scenario);

            //AISearching(city, scenario);
            //AIRecruitPerson(city, scenario);
            //AITrainTroop(city, scenario);
            //AICreateBoat(city, scenario);
            //AICreateMachine(city, scenario);
            //if (city.freePersons.Count > 3)
            //{
            //    city.JobSearching(city.freePersons.GetRange(0, GameRandom.Range(city.freePersons.Count / 2)).ToArray());
            //}
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
                return false;

            if ((city.invisiblePersons.Count > 0 && city.freePersons.Count > 0 && GameRandom.Chance(80)) || GameRandom.Chance(20))
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
        /// AI褒奖武将逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIRewardPerson(City city, Scenario scenario)
        {
            if (GameRandom.Chance(80))
            {
                city.allPersons.ForEach(x =>
                {
                    if (x.BelongTroop != null && city.gold > 500 && x.loyalty <= 95)
                    {
                        city.JobRewardPerson(x);
                    }
                });
            }
            return true;
        }

        /// <summary>
        /// AI招募武将逻辑
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public static bool AIRecruitPerson(City city, Scenario scenario)
        {
            if (city.wildPersons.Count > 0 && city.freePersons.Count > 0)
            {
                for (int i = city.wildPersons.Count - 1; i >= 0; i--)
                {
                    Person target = city.wildPersons[i];
                    Person recommandPerson = ForceAI.CounsellorRecommendRecruitPerson(city.freePersons, target, null);
                    if (recommandPerson != null)
                    {
                        city.JobRecruitPerson(recommandPerson, target);
                    }
                }
            }

            //TODO: 招募其他势力的武将

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
            int appointValue = city.BelongCorps.GetAppointValue(Corps.AppointContentType.Transport);
            City targetTransportCity = null;
            if (appointValue > 0)
            {
                targetTransportCity = scenario.citySet.Get(appointValue);
            }

            if (city.IsBorderCity) return true;

            if (city.freePersons.Count == 0) return true;

            if (city.troops < 20000 || city.food < 20000 || city.itemStore.TotalNumber < 20000)
            {
                return true;
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

            TroopType troopType = scenario.GetObject<TroopType>(6);

            //运输比例
            int part = 70;

            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(city.freePersons);
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
            city.Render?.UpdateRender();
            troop = city.EnsureTroop(troop, scenario);
            city.CurActiveTroop = troop;
#if SANGO_DEBUG
            Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领运输队{troop.troops}出城 向{target.BelongForce?.Name}的{target.Name}运输物资!");
#endif
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
            if (city.IsEnemiesRound()) return true;

            if (city.BelongCity == null) return true;

            if (city.food > city.FoodLimit * 95 / 100 && city.gold > city.GoldLimit * 95 / 100) return true;

            // 寻找最近的附属城市
            City target = city.GetNearnestForceCity();

            if (target == null) return true;

            if (target.IsEnemiesRound()) return true;

            int goldLine = 2500;
            int foodLine = 20000;

            // 资源不够, 人员进入附属城池
            if (city.gold <= goldLine && city.food <= foodLine)
            {
                if (city.freePersons.Count > 0)
                {
                    for (int i = city.freePersons.Count - 1; i >= 0; i--)
                    {
                        Person x = city.freePersons[i];
                        x.TransformToCity(target);
                    }
                    city.freePersons.Clear();
                }
                return true;
            }

            if (target.gold >= target.GoldLimit && city.food <= foodLine)
                return true;
            if (target.food >= target.FoodLimit && city.gold <= goldLine)
                return true;

            // 检查通路
            tempCellList.Clear();
            scenario.Map.GetDirectPath(city.CenterCell, target.CenterCell, tempCellList);
            for (int i = 0; i < tempCellList.Count; ++i)
            {
                Cell road = tempCellList[i];
                if (road.building != null && !road.building.IsCity() && !road.building.IsSameForce(city))
                    return true;
            }

            // 资源够运输, 但是兵力不够, 请求兵力输送
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
                    transport = target.EnsureTroop(transport, scenario);
                    city.CurActiveTroop = transport;
                    target.Render?.UpdateRender();
#if SANGO_DEBUG
                    Sango.Log.Info($"{scenario.GetDateStr()}{target.BelongForce.Name}势力在{target.Name}由{transport.Leader.Name}率领运输队出城 向{city.BelongForce?.Name}的{city.Name}运输物资!");
#endif
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
            int part = 70;
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
                troop = city.EnsureTroop(troop, scenario);
                troop.missionParams1 = 1;
                city.CurActiveTroop = troop;
                city.Render?.UpdateRender();
#if SANGO_DEBUG
                Sango.Log.Info($"{scenario.GetDateStr()}{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领运输队出城 向{target.BelongForce?.Name}的{target.Name}运输物资!");
#endif
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

            for (int i = 0; i < count; i++)
                AIBuildIntriore(city, scenario);
            for (int i = 0; i < count; i++)
                AIBuildingLevelUp(city, scenario);

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
                Troop checkTroop = person.BelongTroop;
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

            TroopType troopType = scenario.GetObject<TroopType>(1);

            // 组建修建队伍
            Person[] builders = ForceAI.CounsellorRecommendBuild(city.freePersons, buildingType);
            if (builders == null || builders.Length == 0)
                return true;

            int maxTroopNum = 3000;
            int food = (int)(maxTroopNum * scenario.Variables.baseFoodCostInTroop * 20);
            int carrayGold = buildingType.cost;

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
            //troop.MaxMorale = city.MaxMorale;
            troop.Leader = builders[0];
            troop.TroopType = troopType;
            troop.troops = maxTroopNum;
            troop.food = food;
            troop.gold = carrayGold;
            troop.missionType = (int)MissionType.TroopBuildBuilding;
            troop.missionTargetCell = dest;
            if (builders.Length > 1) troop.Member1 = builders[1];
            if (builders.Length > 2) troop.Member1 = builders[2];
            city.Render?.UpdateRender();
            troop = city.EnsureTroop(troop, scenario);
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

            if (city.gold < 500) // 至少需要500金
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
            if (city.gold < 500)
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
            if (city.gold < 500)
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
                        if (buildCount <= 6)
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

            if (city.freePersons.Count <= 2) return true;

            int expectationTroops = (city.food / 2);
            if (city.troops >= expectationTroops)
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
            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Donot_Store_Gold) == 1)
                return true;

            if (city.BelongCorps.GetAppointValue(Corps.AppointContentType.Store_Foood) == 1)
                return true;

            if (city.freePersons.Count <= 0) return true;
            if (city.gold <= 2000) return true;

            int expectationFood = (city.troops * 2);
            if (city.food > expectationFood)
                return true;

            Person[] people = ForceAI.CounsellorRecommendTrade(city.freePersons);
            if (people == null) return true;

            if (city.JobTradeFood(people, (city.gold - 2000) * 2 / 3))
            {

            }
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
            if (city.IsRoadBlocked())
                return false;

            City.EnemyInfo enemyInfo;
            // 兵临城下且敌军存活
            if (city.IsEnemiesRound(15) && city.CheckEnemiesIfAlive(out enemyInfo))
                return true;

            return false;
        }

        /// <summary>
        /// 获取势力的AI个性
        /// </summary>
        private static ForceAI.AIPersonalityType GetAIPersonality(City city)
        {
            if (city.BelongForce != null)
            {
                return ForceAI.GetAIPersonality(city.BelongForce);
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
            ForceAI.AIPersonalityType personality = GetAIPersonality(city);

            // 根据AI个性调整兵力要求
            int minTroops = personality == ForceAI.AIPersonalityType.Aggressive ? 8000 :
                           personality == ForceAI.AIPersonalityType.Defensive ? 12000 : 10000;
            if (city.troops < minTroops)
                return false;

            if (city.morale < 60 || city.security < 50)
                return false;

            if (city.freePersons.Count < 2)
                return false;

            if (!city.IsBorderCity)
                return false;

            if (city.IsEnemiesRound(15))
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
            int attackChance = personality == ForceAI.AIPersonalityType.Aggressive ? 50 :
                              personality == ForceAI.AIPersonalityType.Defensive ? 80 : 70;
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
            if (city.freePersons.Count < 2 || city.gold < 400)
                return true;

            if (city.GetJobCounter((int)CityJobType.Inspection) > 0) return true;
            //int barracksNum = city.GetIntriorBuildingComplateMaxLevel((int)BuildingKindType.PatrolBureau);
            //if (barracksNum <= 0) return true;

            if (GameRandom.Chance((100 - city.security) * 4))
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
            if (city.freePersons.Count < 2)
                return true;

            if (city.GetJobCounter((int)CityJobType.TrainTroops) > 0) return true;

            if (city.morale < 50)
            {
                Person[] people = ForceAI.CounsellorRecommendTrainTroops(city.freePersons);
                if (people == null) return true;
                city.JobTrainTroops(people);
            }
            else
            {
                if (GameRandom.Chance((95 - city.morale) * 3 / 2))
                {
                    Person[] people = ForceAI.CounsellorRecommendTrainTroops(city.freePersons);
                    if (people == null) return true;
                    city.JobTrainTroops(people);
                }
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


            if (city.freePersons.Count < 2 || city.gold < 1000)
                return true;

            if (city.itemStore.TotalNumber >= city.StoreLimit - 1000) return true;

            Building freeBlacksmithShop = city.GetFreeBuilding((int)BuildingKindType.BlacksmithShop);
            Building freeStable = city.GetFreeBuilding((int)BuildingKindType.Stable);
            if (freeBlacksmithShop == null && freeStable == null)
                return true;

            int totalNum = 0;
            for (int itemTypeId = 2; itemTypeId <= 5; itemTypeId++)
                // 获取总兵装
                totalNum += city.itemStore.GetNumber(itemTypeId);

            if (totalNum > city.troops * (2 + city.gold / 5000))
                return true;

            // 统计适应偏向
            int[] levelTotal = new int[4] { 1, 1, 1, 1 };
            city.allPersons.ForEach(x =>
            {
                levelTotal[0] += x.SpearLv;
                levelTotal[1] += x.HalberdLv;
                levelTotal[2] += x.CrossbowLv;
                levelTotal[3] += x.RideLv;
            });

            int sumTotal = 0;
            if (freeBlacksmithShop != null)
                sumTotal = sumTotal + levelTotal[0] + levelTotal[1] + levelTotal[2];

            if (freeStable != null)
                sumTotal = sumTotal + levelTotal[3];

            for (int itemTypeId = 2; itemTypeId <= 5; itemTypeId++)
            {
                if (itemTypeId == 5 && freeStable == null)
                    continue;

                if (itemTypeId < 5 && freeBlacksmithShop == null)
                    continue;

                if (city.BelongCorps.GetAppointValue((Corps.AppointContentType)(itemTypeId - 2)) == 1)
                    continue;

                int itemNum = city.itemStore.GetNumber(itemTypeId);
                if (itemNum < levelTotal[itemTypeId - 2] * city.troops / sumTotal + 5000)
                {
                    Person[] people = ForceAI.CounsellorRecommendCreateItems(city.freePersons);
                    if (people == null) return true;
                    ItemType itemType = Scenario.Cur.GetObject<ItemType>(itemTypeId);
                    city.JobCreateItems(people, itemType, itemTypeId == 5 ? freeStable : freeBlacksmithShop);
                    return true;
                }
            }

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

            if (city.freePersons.Count < 2 || city.gold < 1500)
                return true;

            if (city.itemStore.TotalNumber >= city.StoreLimit - 1000) return true;

            Building BoatFactory = city.GetFreeBuilding((int)BuildingKindType.BoatFactory);
            if (BoatFactory == null)
                return true;

            if (city.allPersons.Find(x => x.missionType == (int)MissionType.PersonCreateBoat) != null)
                return true;

            ItemType targetItemType = scenario.GetObject<ItemType>(12);
            if (!targetItemType.IsValid(city.BelongForce))
                targetItemType = scenario.GetObject<ItemType>(11);

            // 获取总兵装
            int totalNum = city.itemStore.GetNumber(targetItemType);
            if (totalNum > (city.troops / 2) * targetItemType.p1 / 1000 + 1)
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

            if (city.freePersons.Count < 2 || city.gold < 1500)
                return true;

            if (city.itemStore.TotalNumber >= city.StoreLimit - 1000) return true;

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

            if (totalNum > (city.troops / 6) * targetItemType.p1 / 1000 + 1)
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
                if (city.freePersons.Count < 3) return null;
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
                city.freePersons.Sort((a, b) => b.MilitaryAbility.CompareTo(a.MilitaryAbility));
                Person person = city.freePersons[0];

                for (int i = 0; i < costEnoughTroopTypes.Count; i++)
                {
                    TroopType troopType = costEnoughTroopTypes[i];
                    if (Troop.CheckTroopTypeLevel(troopType, person) >= 3)
                    {
                        spType = troopType;
                        break;
                    }
                }
                if (spType == null)
                    spType = costEnoughTroopTypes[GameRandom.Range(0, costEnoughTroopTypes.Count)];
            }

            Person[] people = ForceAI.CounsellorRecommendMakeTroop(city.freePersons, spType, maxPersonCount);

            Troop troop = scenario.CreateTroop();
            troop.energy = city.energy;
            troop.morale = city.morale;
            //troop.MaxMorale = city.MaxMorale;
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
            city.Render?.UpdateRender();
            troop.SetMission(city.TroopMissionType, city.TroopMissionTargetId);
            return troop;
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
            TroopType troopType = scenario.GetObject<TroopType>(6);
            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(city.freePersons);
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
            troop.itemStore = itemStore;
            city.itemStore.Remove(itemStore);
            troop.Member1 = null;
            troop.Member2 = null;
            city.Render?.UpdateRender();
            troop.SetMission(MissionType.TroopTransformGoodsToCity, target.Id);
            return troop;
        }

    }


}
