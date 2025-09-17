using Sango.Tools;
using System.Collections.Generic;

namespace Sango.Game
{
    public class CityAI
    {
        static internal PriorityQueue<City> priorityQueue = new PriorityQueue<City>();

        public static bool AIAttack(City city, Scenario scenario)
        {
            if (city.BelongForce == null)
                return true;

            if (AICanDefense(city, scenario))
            {
                if (city.CurActiveTroop != null)
                {
                    if (!city.CurActiveTroop.DoAI(scenario))
                        return false;

                    city.CurActiveTroop = null;
                    return false;
                }

                city.troopTempList.Clear();
                city.AutoMakeTroop(city.troopTempList, 3, false);
                if (city.troopTempList.Count <= 0) return true;

                Troop troop = city.troopTempList[0];
                city.troopTempList.RemoveAt(0);
                troop = city.EnsureTroop(troop, scenario, 10);
                troop.missionType = (int)MissionType.ProtectCity;
                troop.missionTarget = city.Id;
                Sango.Log.Warning($"{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城防守!");
                city.CurActiveTroop = troop;
                city.Render?.UpdateRender();
                return false;
            }
            else if (AICanAttack(city, scenario))
            {

                City lastTargetCity = null;
                if (city.allTroops.Count > 0)
                {
                    if (city.allTroops[0].missionType == (int)MissionType.OccupyCity)
                    {
                        lastTargetCity = scenario.citySet.Get(city.allTroops[0].missionTarget);
                    }
                }

                if (lastTargetCity != null)
                {
                    if (city.troops < UnityEngine.Mathf.Min(lastTargetCity.troops, lastTargetCity.allPersons.Count * 5000))
                        return true;

                    List<Troop> troopList = new List<Troop>();
                    city.AutoMakeTroop(troopList, 10, false);
                    while (troopList.Count > 0)
                    {
                        Troop troop = troopList[0];
                        troopList.RemoveAt(0);
                        troop = city.EnsureTroop(troop, scenario, 20);
                        troop.missionType = (int)MissionType.OccupyCity;
                        troop.missionTarget = lastTargetCity.Id;
                        //troop.DoAI(scenario);
                        Sango.Log.Print($"{scenario.Info.year}年{scenario.Info.month}月{scenario.Info.day}日{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城 进攻{lastTargetCity.BelongForce?.Name}的{lastTargetCity.Name}!");
                        troop = null;
                    }
                    return true;
                }

                // 计算进攻概率
                priorityQueue.Clear();
                city.ForeachNeighborCities(x =>
                {
                    if (x.IsEnemy(city))
                    {
                        if (x.BelongForce == null)
                        {
                            priorityQueue.Push(x, 9999);
                        }
                        else
                        {
                            // 需要兵力充足
                            if (city.troops > UnityEngine.Mathf.Min(x.troops, x.allPersons.Count * 5000) + 5000)
                            {
                                // 范围大约在
                                int weight = (int)(1000 * (float)city.virtualFightPower / (float)x.virtualFightPower);
                                int relation = scenario.GetRelation(city.BelongForce, x.BelongForce);
                                // 8000亲密 6000友好 4000普通 2000中立 0冷漠 -2000敌对 -4000厌恶 -6000仇视 -8000不死不休
                                // 5 4 3 2 1 0 -1 -2 -3 -4 -5
                                // 0 1 2 3 4 5 6 7 8 9 10
                                weight = UnityEngine.Mathf.FloorToInt((float)weight * (1f - (float)relation / 10000f));
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
                        if (GameRandom.Changce(priority, 10000))
                        {
                            if (city.troops < UnityEngine.Mathf.Min(targetCity.troops, targetCity.allPersons.Count * 5000))
                                continue;

                            List<Troop> troopList = new List<Troop>();
                            city.AutoMakeTroop(troopList, 10, false);
                            while (troopList.Count > 0)
                            {
                                Troop troop = troopList[0];
                                troopList.RemoveAt(0);
                                troop = city.EnsureTroop(troop, scenario, 20);
                                troop.missionType = (int)MissionType.OccupyCity;
                                troop.missionTarget = targetCity.Id;
                                //troop.DoAI(scenario);
                                Sango.Log.Print($"{scenario.Info.year}年{scenario.Info.month}月{scenario.Info.day}日{city.BelongForce.Name}势力在{city.Name}由{troop.Leader.Name}率领军队出城 进攻{targetCity.BelongForce?.Name ?? ""}的{targetCity.Name}!");
                                troop = null;
                            }
                            return true;
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
            if (city.security < 60)
            {
                AISecurity(city, scenario);
            }

            // 兵临城下
            if (city.IsEnemiesRound(9))
                return true;

            AIBuilding(city, scenario);

            AIIntriorBalance(city, scenario);
            if (city.allIntriorBuildings.Count >= city.CityLevelType.outsideSlot)
            {
                AIIntriorBalance(city, scenario);
                if (city.freePersons.Count > 5 && GameRandom.Changce(50))
                    AIIntriorBalance(city, scenario);
                if (city.freePersons.Count > 8 && GameRandom.Changce(50))
                    AIIntriorBalance(city, scenario);
                AISecurity(city, scenario);
            }

            if (city.wildPersons.Count > 0 && city.freePersons.Count > 0)
            {
                foreach (Person wild in city.wildPersons)
                {
                    if (wild.beFinded)
                    {
                        city.JobRecuritPerson(city.freePersons[0], wild);
                        break;
                    }
                }
            }

            if (city.freePersons.Count > 3)
            {
                city.JobSearching(new List<Person>(city.freePersons.GetRange(0, GameRandom.Range(city.freePersons.Count / 2))));
            }

            return true;
        }

        public static bool AITransfrom(City city, Scenario scenario)
        {
            return true;
            //if (IsBorderCity)
            //{
            //    if (PersonHole > 0)
            //    {
            //        List<Person> people = new List<Person>();
            //        BelongCorps.allCities.ForEach(c =>
            //        {
            //            if (c != this && !c.IsBorderCity)
            //            {
            //                people.AddRange(c.freePersons);
            //            }
            //        });

            //        if (people.Count <= 0)
            //        {
            //            foreach (City city in BelongCorps.allCities)
            //            {
            //                if (city.IsBorderCity && PersonHole - city.PersonHole > 3)
            //                {
            //                    if (city.freePersons.Count > 0)
            //                    {
            //                        city.freePersons[0].TransformToCity(this);
            //                        break;
            //                    }
            //                }
            //            }
            //            return;
            //        }

            //        people.Sort((a, b) =>
            //        {
            //            int a_m = a.MilitaryAbility;
            //            int b_m = b.MilitaryAbility;
            //            if (a_m == b_m)
            //            {
            //                return -a.Strength.CompareTo(b.Strength);
            //            }
            //            else
            //            {
            //                return -a_m.CompareTo(b_m);
            //            }
            //        });

            //        for (int i = 0; i < PersonHole; i++)
            //        {
            //            if (i < people.Count)
            //            {
            //                Person transP = people[i];
            //                transP.TransformToCity(this);
            //            }
            //        }
            //    }
            //    else if (PersonHole < 0)
            //    {

            //    }
            //}
            //else
            //{
            //    if (allPersons.Count + trsformingPesonList.Count == 0)
            //    {
            //        foreach (City city in BelongCorps.allCities)
            //        {
            //            if (!city.IsBorderCity && city.allPersons.Count + city.trsformingPesonList.Count > 1)
            //            {
            //                if (city.freePersons.Count > 0)
            //                {
            //                    city.freePersons[0].TransformToCity(this);
            //                    break;
            //                }
            //            }
            //        }
            //    }

            //    City destCity = null;
            //    int maxLen = 9999999;
            //    BelongCorps.allCities.ForEach(c =>
            //    {
            //        if (c != this && c.IsBorderCity && !c.TroopsIsFull)
            //        {
            //            int len = CenterCell.Distance(c.CenterCell);
            //            if (len < maxLen)
            //            {
            //                destCity = c;
            //                maxLen = len;
            //            }
            //        }
            //    });
            //    if (destCity != null)
            //    {
            //        destCity.food += food;
            //        food = 0;
            //        destCity.gold += gold / 3;
            //        gold = gold / 3;
            //        destCity.troops += troops;
            //        troops = 0;
            //    }
            //}
        }

        public static bool AIBuildingByPercent(City city, BuildingType buildingType, float percent, Scenario scenario)
        {
            int maxSlot = city.CityLevelType.outsideSlot;
            int leftSlot = maxSlot - city.allIntriorBuildings.Count;
            if (leftSlot <= 0)
                return true;

            int buildMax = (int)System.Math.Ceiling(maxSlot * percent);
            int nowCount = 0;
            city.allIntriorBuildings.ForEach(building =>
            {
                if (building.BuildingType.kind == buildingType.kind)
                    nowCount++;
            });

            City.sort_by_BaseBuildAbility.RemoveAll(x => x.ActionOver == true);
            while (nowCount < buildMax)
            {
                if (city.gold < buildingType.cost)
                    break;
                if (city.freePersons.Count <= 0)
                    break;
                Cell center = null;
                Cell villageCenter = null;
                int safeCount = 0;
                while (safeCount < 100)
                {
                    safeCount++;
                    int where2Build = GameRandom.Range(0, city.effectCells.Count);
                    villageCenter = city.effectCells[where2Build];
                    if (villageCenter.building != null) continue;
                    bool too_near = false;
                    for (int j = 0; j < city.allIntriorBuildings.Count; ++j)
                    {
                        Building building = city.allIntriorBuildings[j];
                        if (villageCenter.Cub.Distance(building.CenterCell.Cub) < building.BuildingType.radius + buildingType.radius + 1)
                        {
                            too_near = true;
                            break;
                        }
                    }
                    if (too_near) continue;
                    center = villageCenter;
                    break;
                }

                if (center == null)
                {
                    for (int i = 0; i < city.effectCells.Count; i++)
                    {
                        villageCenter = city.effectCells[i];
                        if (villageCenter.building != null) continue;
                        bool too_near = false;
                        for (int j = 0; j < city.allIntriorBuildings.Count; ++j)
                        {
                            Building building = city.allIntriorBuildings[j];
                            if (villageCenter.Cub.Distance(building.CenterCell.Cub) < building.BuildingType.radius + buildingType.radius + 1)
                            {
                                too_near = true;
                                break;
                            }
                        }
                        if (too_near) continue;
                        center = villageCenter;
                        break;
                    }
                }

                if (center != null)
                {
                    Person person = City.sort_by_BaseBuildAbility[0];
                    City.sort_by_BaseBuildAbility.RemoveAt(0);
                    city.freePersons.Remove(person);
                    city.BuildBuilding(center, person, buildingType);
                    city.gold -= buildingType.cost;
                    person.ActionOver = true;
                }

                nowCount++;
            }
            return true;
        }

        public static bool AIBuildingIntriorBalance(City city, Scenario scenario)
        {
            BuildingType marketType = scenario.CommonData.BuildingTypes.Find(x => x.kind == (int)BuildingKindType.Market);
            AIBuildingByPercent(city, marketType, 0.3f, scenario);
            BuildingType farmType = scenario.CommonData.BuildingTypes.Find(x => x.kind == (int)BuildingKindType.Farm);
            AIBuildingByPercent(city, farmType, 0.3f, scenario);
            BuildingType villageType = scenario.CommonData.BuildingTypes.Find(x => x.kind == (int)BuildingKindType.Village);
            AIBuildingByPercent(city, villageType, 0.3f, scenario);
            return true;
        }

        public static bool AIBuilding(City city, Scenario scenario)
        {
            AIBuldIntriore(city, scenario);
            return true;
        }

        static List<Cell> tempCellList = new List<Cell>();

        public static bool AIBuldIntriore(City city, Scenario scenario)
        {
            if (city.allIntriorBuildings.Count >= city.CityLevelType.outsideSlot)
                return true;

            if (city.freePersons.Count <= 0)
                return true;

            if (city.IsBorderCity)
            {
                return AIBuildingIntriorBalance(city, scenario);
            }
            else
            {
                return AIBuildingIntriorBalance(city, scenario);
            }
        }

        static bool AIBuldVillage(City city, Scenario scenario)
        {
            return true;
            //if (villageList.Count >= CityLevelType.villageSlot)
            //    return;

            //if (IsEnemiesRound(8))
            //    return;

            //BuildingType villageBuildingType = Scenario.Cur.CommonData.BuildingTypes.Find(x => x.kind == (int)BuildingKindType.Village);
            //if (gold < villageBuildingType.cost)
            //    return;

            //tempCellList.Clear();
            //Map map = Scenario.Cur.Map;
            //float maxFertilityAndProsperity = 0;
            //Cell buildCenter = null;
            //for (int i = 0; i < effectCells.Count; i++)
            //{
            //    Cell villageCenter = effectCells[i];
            //    if (villageCenter.building != null) continue;

            //    bool too_near = false;
            //    for (int j = 0; j < villageList.Count; ++j)
            //    {
            //        if (villageCenter.Cub.Distance(villageList[j].CenterCell.Cub) < villageBuildingType.radius * 2 + 1)
            //        {
            //            too_near = true;
            //            break;
            //        }
            //    }

            //    if (too_near) continue;

            //    map.GetSpiral(villageCenter, villageBuildingType.radius, tempCellList);
            //    float totalFertilityAndProsperity = 0;
            //    for (int j = 0; j < tempCellList.Count; ++j)
            //    {
            //        Cell effectCell = tempCellList[j];
            //        totalFertilityAndProsperity += effectCell.Fertility;
            //        totalFertilityAndProsperity += effectCell.Prosperity;
            //    }
            //    //float totalFertilityAndProsperity = villageCenter.Fertility + villageCenter.Prosperity;
            //    if (totalFertilityAndProsperity > maxFertilityAndProsperity)
            //    {
            //        maxFertilityAndProsperity -= totalFertilityAndProsperity;
            //        buildCenter = villageCenter;
            //    }
            //}

            //if (buildCenter != null)
            //{
            //    freePersons.Sort((a, b) => { return -a.BaseBuildAbility.CompareTo(b.BaseBuildAbility); });
            //    Person builder = freePersons[0];
            //    freePersons.RemoveAt(0);
            //    Building village = BuildBuilding(buildCenter, builder, villageBuildingType);
            //    gold -= villageBuildingType.cost;
            //    villageList.Add(village);
            //}

        }


        /// <summary>
        /// 军事
        /// </summary>
        /// <param name="scenario"></param>
        public static bool AITroop(City city, Scenario scenario)
        {
            AIRecuritTroop(city, scenario);
            //AITroopCreate(scenario);
            //AITroopLevelUp(scenario);
            //AITroopMerge(scenario);
            //AITroopTrain(scenario);
            return true;
        }

        public static bool AIRecuritTroop(City city, Scenario scenario)
        {
            if (city.freePersons.Count > 0)
            {
                City.sort_by_BaseRecruitmentAbility.RemoveAll(x => x.ActionOver == true);
                if (City.sort_by_BaseRecruitmentAbility.Count > 0)
                {
                    if (city.JobRecuritTroop(City.sort_by_BaseRecruitmentAbility))
                    {
                        City.sort_by_BaseRecruitmentAbility.RemoveAt(0);
                    }
                }
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

        public static bool AICanDefense(City city, Scenario scenario)
        {
            //if (troopList.Count == 0 || transferable.food < 10000)
            //    return false;

            //if (crossbow + spear + halberd + horse < 10000)
            //    return false;

            if (city.IsRoadBlocked())
                return false;

            // 兵临城下
            if (city.IsEnemiesRound(6))
                return true;

            return false;
        }

        public static bool AICanAttack(City city, Scenario scenario)
        {
            if (city.troops < 20000)
                return false;

            if (city.freePersons.Count <= 0)
                return false;

            if (!city.IsBorderCity)
                return false;

            if (city.IsEnemiesRound())
                return false;

            List<City> enemiesCities = new List<City>();
            city.ForeachNeighborCities(x =>
            {
                if (x.IsEnemy(city))
                    enemiesCities.Add(x);
            });

            if (enemiesCities.Count == 0)
                return false;

            if (city.food < city.troops * 2)
                return false;

            //if (allTroops.Count > 0 && allTroops.Count > TroopList.Count)
            //    return false;

            if (GameRandom.Changce(90))
                return false;

            return true;
        }

        public static bool AIIntriorBalance(City city, Scenario scenario)
        {
            if (city.freePersons.Count > 0 && city.gold > 400)
            {
                if (city.commerce >= city.agriculture)
                {
                    City.sort_by_BaseAgricultureAbility.RemoveAll(x => x.ActionOver == true);
                    if (city.JobFarming(City.sort_by_BaseAgricultureAbility))
                    {

                    }
                }
                else
                {
                    City.sort_by_BaseCommerceAbility.RemoveAll(x => x.ActionOver == true);
                    if (city.JobDevelop(City.sort_by_BaseCommerceAbility))
                    {
                    }
                }
            }
            return true;
        }
        public static bool AIIntriorAgricultureFirst(City city, Scenario scenario)
        {
            if (city.freePersons.Count > 0 && city.gold > 400)
            {
                if (city.commerce < city.agriculture * 3 / 2)
                {
                    if (city.JobFarming(City.sort_by_BaseAgricultureAbility))
                    {
                    }
                }
                else
                {
                    if (city.JobDevelop(City.sort_by_BaseCommerceAbility))
                    {
                    }
                }
            }
            return true;
        }
        public static bool AIIntriorCommerceFirst(City city, Scenario scenario)
        {
            if (city.freePersons.Count > 0 && city.gold > 400)
            {
                if (city.commerce >= city.agriculture * 3 / 2)
                {
                    City.sort_by_BaseAgricultureAbility.RemoveAll(x => x.ActionOver == true);
                    if (city.JobFarming(City.sort_by_BaseAgricultureAbility))
                    {
                    }
                }
                else
                {
                    City.sort_by_BaseCommerceAbility.RemoveAll(x => x.ActionOver == true);
                    if (city.JobDevelop(City.sort_by_BaseCommerceAbility))
                    {
                    }
                }
            }
            return true;
        }

        public static bool AISecurity(City city, Scenario scenario)
        {
            if (city.freePersons.Count > 0 && city.gold > 400)
            {
                if (GameRandom.Changce((100 - city.security) * 3 / 2))
                {
                    City.sort_by_BaseSecurityAbility.RemoveAll(x => x.ActionOver == true);
                    if (city.JobInspection(City.sort_by_BaseSecurityAbility))
                    {
                    }
                }
            }
            return true;
        }
    }


}
