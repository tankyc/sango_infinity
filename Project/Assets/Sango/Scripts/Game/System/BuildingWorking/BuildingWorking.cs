using Sango.Core.Player;
using Sango.Core.Tools;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 建筑工作模块
    /// 可指派武将到建筑工作以提升建筑的功能产出
    /// </summary>
    [GameSystem(order = 100)]
    public class BuildingWorking : GameSystem
    {
        public Building TargetBuilding { get; set; }
        public City TargetCity { get; set; }

        public List<ObjectSortTitle>[] customTitleList;
        public string customTitleName;
        public string windowName = "window_building_work_set";

        // 基于经典产出的比例
        int classiceGainFactor = 5;

        /// <summary>
        /// 0是工作制 1是经典内政
        /// </summary>
        int selectedWorkingType = 1;

        public override void Init()
        {
            Clear();

            // 增加剧本参数,用来修改内政类型, 工作模块启动的时候默认为当前类型,修改后才可以启动经典内政
            GameEvent.OnScenarioVariablesSetting += OnScenarioVariablesSetting;

            // 剧本初始化的时候启用设置好的内政类型
            GameEvent.OnScenarioInit += OnScenarioInit;
            GameEvent.OnScenarioEnd += OnScenarioEnd;
            GameEvent.OnInitBuildingMiniPanel += OnInitBuildingMiniPanel;
            GameEvent.OnGameSave += OnGameSave;

            GameEvent.OnGetJobCostAP += OnGetJobCostAP;

        }

        /// <summary>
        /// 保存自定义数据
        /// </summary>
        /// <param name="scenario"></param>
        /// <param name="index"></param>
        void OnGameSave(Scenario scenario, int index, bool isAuto)
        {
            scenario.Variables.SetExtensionData("WorkingType", selectedWorkingType);
        }

        public override void Clear()
        {
            GameEvent.OnScenarioVariablesSetting -= OnScenarioVariablesSetting;
            GameEvent.OnScenarioInit -= OnScenarioInit;
            GameEvent.OnScenarioEnd -= OnScenarioEnd;
            GameEvent.OnInitBuildingMiniPanel -= OnInitBuildingMiniPanel;
            GameEvent.OnGameSave -= OnGameSave;
            GameEvent.OnGetJobCostAP -= OnGetJobCostAP;
        }

        void OnGetJobCostAP(JobType jobType, int cost, OverrideData<int> overrideData)
        {
            if (selectedWorkingType == 1)
                return;

            // 工作制下面这些政策不消耗AP
            switch ((CityJobType)jobType.Id)
            {
                case CityJobType.Inspection:
                case CityJobType.TrainTroops:
                case CityJobType.Searching:
                case CityJobType.RecruitTroops:
                case CityJobType.CreateItems:
                case CityJobType.CreateMachine:
                case CityJobType.CreateBoat:
                case CityJobType.CreateHorse:
                    overrideData.Set(1);
                    break;
            }
        }


        void OnScenarioInit(Scenario scenario)
        {
            if (scenario.Variables.HasExtensionData("WorkingType"))
                selectedWorkingType = scenario.Variables.GetExtensionData<int>("WorkingType");

            if (selectedWorkingType == 0)
            {
                GameSystem.GetSystem<ClassicsCityWorking>().ScenarioClear();
                ScenarioInit();

                customTitleName = "建筑工作";
                customTitleList = new List<ObjectSortTitle>[]
                {
                    new List<ObjectSortTitle>()
                    {
                        PersonSortFunction.SortByName,
                        PersonSortFunction.SortByCommand,
                        PersonSortFunction.SortByWork,
                        PersonSortFunction.SortByFeatureList,
                    },
                    new List<ObjectSortTitle>()
                    {
                        PersonSortFunction.SortByName,
                        PersonSortFunction.SortByStrength,
                        PersonSortFunction.SortByWork,
                        PersonSortFunction.SortByFeatureList,
                    },
                    new List<ObjectSortTitle>()
                    {
                        PersonSortFunction.SortByName,
                        PersonSortFunction.SortByIntelligence,
                        PersonSortFunction.SortByWork,
                        PersonSortFunction.SortByFeatureList,
                    },
                    new List<ObjectSortTitle>()
                    {
                        PersonSortFunction.SortByName,
                        PersonSortFunction.SortByPolitics,
                        PersonSortFunction.SortByWork,
                        PersonSortFunction.SortByFeatureList,
                    },
                    new List<ObjectSortTitle>()
                    {
                        PersonSortFunction.SortByName,
                        PersonSortFunction.SortByGlamour,
                        PersonSortFunction.SortByWork,
                        PersonSortFunction.SortByFeatureList,
                    },

                };
            }
            else
            {
                ScenarioClear();
            }
        }

        void OnScenarioEnd(Scenario scenario)
        {
            if (selectedWorkingType == 0)
            {
                ScenarioClear();
                GameSystem.GetSystem<ClassicsCityWorking>().ScenarioInit();
            }
        }

        void ScenarioInit()
        {
            GameEvent.OnBuildingContextMenuShow += OnBuildingContextMenuShow;
            GameEvent.OnBuildingTurnEnd += OnBuildingTurnEnd;
            GameEvent.OnCityMonthStart += OnCityMonthStart;
            GameEvent.OnCitySeasonStart += OnCitySeasonStart;
            GameEvent.OnCityTurnStart += OnCityTurnStart;
            GameEvent.OnCityTurnEnd += OnCityTurnEnd;
            GameEvent.OnCityContextMenuShow += OnCityContextMenuShow;
            GameEvent.OnCityAIPrepare += OnCityAIPrepare;

            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (x.Id >= 42 && x.Id <= 43)
                    x.canBuild = true;
            });
        }

        void ScenarioClear()
        {
            GameEvent.OnBuildingContextMenuShow -= OnBuildingContextMenuShow;
            GameEvent.OnBuildingTurnEnd -= OnBuildingTurnEnd;
            GameEvent.OnCityMonthStart -= OnCityMonthStart;
            GameEvent.OnCitySeasonStart -= OnCitySeasonStart;
            GameEvent.OnCityTurnStart -= OnCityTurnStart;
            GameEvent.OnCityTurnEnd -= OnCityTurnEnd;
            GameEvent.OnCityContextMenuShow -= OnCityContextMenuShow;
            GameEvent.OnCityAIPrepare -= OnCityAIPrepare;

            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (x.Id >= 42 && x.Id <= 43)
                    x.canBuild = false;
            });

        }

        void OnCityAIPrepare(City city, Scenario scenario)
        {
            CityAI.CityBuildingTemplate = CityBuildingTemplate;
            List<System.Func<City, Scenario, bool>> AICommandList = city.AICommandList;
            if (city.IsBorderCity)
            {
                AICommandList.Add(CityAI.AIAttack);
                AICommandList.Add(CityAI.AITradeFood);

                AICommandList.Add(CityAI.AIIntrior);
                AICommandList.Add(CityAI.AITradeFood);
            }
            else
            {
                AICommandList.Add(CityAI.AITradeFood);
                // 物资输送
                AICommandList.Add(CityAI.AITransfrom);
                AICommandList.Add(CityAI.AIIntrior);
                AICommandList.Add(CityAI.AIAttack);

            }
        }

        void OnInitBuildingMiniPanel(Building building, List<ObjectSortTitle> objectSortTitles)
        {
            for (int i = 0; i < building.BuildingType.workerLimit; i++)
            {
                objectSortTitles.Add(BuildingSortFunction.GetSortByWorkSlot(i).SetAlignment((int)TextAnchor.MiddleCenter));
            }
        }

        void OnScenarioVariablesSetting(IVariablesSetting variablesSetting, Scenario scenario)
        {
            //selectedWorkingType = 1;
            variablesSetting.AddDropdownItem(GameLanguage.GetString(10000010), selectedWorkingType,
                new List<string>(new string[]
                {
                    GameLanguage.GetString(10000009),
                    GameLanguage.GetString(10000008)
                }),
                (index) =>
                {
                    selectedWorkingType = index;
                });
        }

        void OnCityContextMenuShow(IContextMenuData menuData, City city)
        {
            TargetCity = city;
            if (city.IsCity() && city.BelongForce != null && city.BelongForce.IsPlayer && city.BelongForce == Scenario.Cur.CurRunForce)
            {
                bool b = city.GetExtensionData<bool>("AppointWorking");
                if (!b)
                    menuData.Add(GameLanguage.GetString(10000003), 99999, city, OnClickMenuItem_CityAutoWorking, true);
                else
                    menuData.Add(GameLanguage.GetString(10000004), 99999, city, OnClickMenuItem_CityAutoWorking, true);

                menuData.Add("自动设置", 99998, city, OnClickMenuItem_AutoSetWorking, true);
            }
        }

        void OnClickMenuItem_AutoSetWorking(IContextMenuItem contextMenuItem)
        {
            AppointWorking(TargetCity, Scenario.Cur);
            TargetCity.allBuildings.ForEach((building) => { if (building.isComplate) building.Render?.UpdateRender(); });
        }

        void OnClickMenuItem_CityAutoWorking(IContextMenuItem contextMenuItem)
        {
            bool b = !TargetCity.GetExtensionData<bool>("AppointWorking");
            TargetCity.SetExtensionData<bool>("AppointWorking", b);
            if (b)
            {
                string[] ts = GameLanguage.GetString(10000004).Split("/");
                contextMenuItem.SetTitle(ts[ts.Length - 1]);
                AppointWorking(TargetCity, Scenario.Cur);
            }
            else
            {
                string[] ts = GameLanguage.GetString(10000003).Split("/");
                contextMenuItem.SetTitle(ts[ts.Length - 1]);
            }
        }


        protected virtual void OnBuildingContextMenuShow(IContextMenuData menuData, BuildingBase building)
        {
            if (building.BelongCity != null && building.BelongForce != null && building.BelongForce.IsPlayer && building.BelongForce == Scenario.Cur.CurRunForce)
            {
                TargetBuilding = building as Building;

                // 工作设置
                menuData.Add(GameLanguage.GetString(10000001), 10, null, OnClickMenuItem_WorkerSet, true);

                // 工作(自动)
                menuData.Add(GameLanguage.GetString(10000002), 11, null, OnClickMenuItem_AutoWorkerSet, true);

                // 清理
                menuData.Add(GameLanguage.GetString(10000005), 12, null, OnClickMenuItem_ClearWorkerSet, true);
            }
        }

        protected virtual void OnClickMenuItem_WorkerSet(IContextMenuItem contextMenuItem)
        {
            // 工作设置
            Enter();
        }

        protected virtual void OnClickMenuItem_ClearWorkerSet(IContextMenuItem contextMenuItem)
        {
            // 工作设置
            City belongCity = TargetBuilding.BelongCity;
            if (belongCity == null) return;

            if (TargetBuilding.Workers != null)
            {
                TargetBuilding.Workers.ForEach((person) =>
                {
                    person.workingBuilding = null;
                });
                TargetBuilding.Workers.Clear();
            }

            TargetBuilding.Render.UpdateRender();
        }

        protected virtual void OnClickMenuItem_AutoWorkerSet(IContextMenuItem contextMenuItem)
        {
            // 自动设置
            City belongCity = TargetBuilding.BelongCity;
            if (belongCity == null) return;

            BuildingType targetBuildingType = TargetBuilding.BuildingType;

            if (TargetBuilding.Workers != null)
            {
                TargetBuilding.Workers.ForEach((person) =>
                {
                    person.workingBuilding = null;
                });
                TargetBuilding.Workers.Clear();
            }

            List<Person> persons = new List<Person>();
            for (int i = 0; i < belongCity.FreePersonCount; i++)
            {
                Person person = belongCity.freePersons[i];
                if (person == null) continue;
                if (person.workingBuilding != null)
                    continue;
                persons.Add(person);
            }

            if (persons.Count > 0)
            {
                persons.Sort((a, b) => -a.GetAttribute(targetBuildingType.effectAttrType).CompareTo(b.GetAttribute(targetBuildingType.effectAttrType)));
                if (TargetBuilding.Workers == null)
                {
                    TargetBuilding.Workers = new SangoObjectList<Person>();
                }
            }

            for (int i = 0; i < persons.Count; i++)
            {
                if (i < targetBuildingType.workerLimit)
                {
                    Person person = persons[i];
                    TargetBuilding.Workers.Add(person);
                    person.workingBuilding = TargetBuilding;
                }
            }
            TargetBuilding.Render.UpdateRender();

        }

        public void AutoSetWorker(Building target)
        {
            City belongCity = target.BelongCity;
            if (belongCity == null) return;

            BuildingType targetBuildingType = target.BuildingType;

            if (target.Workers != null)
            {
                target.Workers.ForEach((person) =>
                {
                    person.workingBuilding = null;
                });
                target.Workers.Clear();
            }

            List<Person> persons = new List<Person>();
            for (int i = 0; i < belongCity.FreePersonCount; i++)
            {
                Person person = belongCity.freePersons[i];
                if (person == null) continue;
                if (person.workingBuilding != null)
                    continue;
                persons.Add(person);
            }

            if (persons.Count > 0)
            {
                persons.Sort((a, b) => -a.GetAttribute(targetBuildingType.effectAttrType).CompareTo(b.GetAttribute(targetBuildingType.effectAttrType)));
                if (target.Workers == null)
                {
                    target.Workers = new SangoObjectList<Person>();
                }
            }

            for (int i = 0; i < persons.Count; i++)
            {
                if (i < targetBuildingType.workerLimit)
                {
                    Person person = persons[i];
                    target.Workers.Add(person);
                    person.workingBuilding = target;
                }
            }
        }

        void AutoSetWorker(City city, List<PBuilding> pBuildings)
        {
            City belongCity = city;
            if (belongCity == null) return;

            for (int i = 0; i < pBuildings.Count; i++)
            {
                Building target = pBuildings[i].building;
                if (target.Workers != null)
                {
                    target.Workers.ForEach((person) =>
                    {
                        person.workingBuilding = null;
                    });
                    target.Workers.Clear();
                }
            }

            List<Person> persons = new List<Person>();
            for (int i = 0; i < belongCity.FreePersonCount; i++)
            {
                Person person = belongCity.freePersons[i];
                if (person == null) continue;
                if (person.workingBuilding != null)
                    continue;
                persons.Add(person);
            }

            for (int j = 0; j < 3; j++)
            {
                for (int i = 0; i < pBuildings.Count; i++)
                {
                    Building target = pBuildings[i].building;
                    BuildingType targetBuildingType = target.BuildingType;
                    if (target.Workers == null)
                        target.Workers = new SangoObjectList<Person>();
                    if (persons.Count > 0 && target.Workers.Count < targetBuildingType.workerLimit)
                    {
                        persons.Sort((a, b) => -a.GetAttribute(targetBuildingType.effectAttrType).CompareTo(b.GetAttribute(targetBuildingType.effectAttrType)));

                        Person person = persons[0];
                        target.Workers.Add(person);
                        persons.RemoveAt(0);
                        person.workingBuilding = target;
                    }
                }
            }
        }

        public int GetCityLeaderInfuse(City city, int effectAttrType)
        {
            Person leader = city.Leader;
            int leaderAttrValue = 0;
            if (leader != null)
                leaderAttrValue = leader.GetAttribute(effectAttrType);
            // 计算公式
            return 100 + (int)((Mathf.Pow(Mathf.Max(40, leaderAttrValue), 1.5f) / 10 - 25) / 3f);
        }

        public int GetPersonInfuse(Person[] workers, int effectAttrType)
        {
            int person_factor = 100;
            if (workers == null) return person_factor;
            for (int i = 0; i < workers.Length; i++)
            {
                Person person = workers[i];
                if (person != null)
                {
                    person_factor += (int)(Mathf.Pow(Mathf.Max(40, person.GetAttribute(effectAttrType)), 0.5f) * 100 / (8 * (i + 1)));
                }
            }
            return person_factor;
        }

        List<Person> worker_list = new List<Person>();
        /// <summary>
        /// 在回合末计算产出
        /// </summary>
        /// <param name="building"></param>
        /// <param name="scenario"></param>
        void OnBuildingTurnEnd(Building building, Scenario scenario)
        {
            if (!building.IsIntorBuilding())
                return;

            City belongCity = building.BelongCity;
            if (belongCity == null) return;
            if (!building.isComplate) return;
            BuildingType buildingType = building.BuildingType;

            // 计算太守对于收入的影响
            int leader_factor = GetCityLeaderInfuse(belongCity, buildingType.effectAttrType);
            bool hasWorker = false;
            worker_list.Clear();
            if (building.Workers != null)
            {
                for (int i = 0; i < buildingType.workerLimit; i++)
                {
                    Person person = null;
                    if (i < building.Workers.Count)
                        person = building.Workers.Get(i);
                    if (person != null && person.IsFree && !person.ActionOver)
                    {
                        worker_list.Add(person);
                        hasWorker = true;
                    }
                }
            }
            Person[] personArray = worker_list.ToArray();

            int person_factor = GetPersonInfuse(personArray, buildingType.effectAttrType);

            int totalFactor = leader_factor * person_factor;
            int jobId = buildingType.jobId;
            JobType jobType = Scenario.Cur.CommonData.JobTypes.Get(jobId);
            GameUtility.InitJobFeature(building.Workers, belongCity, building);
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(0);

            // 建筑累积收益
            if (buildingType.foodGain > 0)
            {
                int value = buildingType.foodGain / classiceGainFactor * totalFactor / 10000;
                overrideData.Value = value;
                GameEvent.OnBuildingCalculateFoodGain?.Invoke(building, overrideData);
                building.AccumulatedFood += overrideData.Value;
            }

            if (buildingType.goldGain > 0)
            {
                int value = buildingType.goldGain / classiceGainFactor * totalFactor / 10000;
                overrideData.Value = value;
                GameEvent.OnBuildingCalculateGoldGain?.Invoke(building, overrideData);
                building.AccumulatedGold += overrideData.Value;
            }

            if (buildingType.populationGain > 0)
            {
                int value = buildingType.populationGain / classiceGainFactor * totalFactor / 10000;
                overrideData.Value = value;
                GameEvent.OnBuildingCalculatePopulationGain?.Invoke(building, overrideData);
                building.AccumulatedPopulation += overrideData.Value;
            }

            int productCost = buildingType.productCost;
            int techniquePointGain = jobType.tpGain;
            int meritGain = jobType.meritGain;
            if (hasWorker)
            {
                // 计算工作消耗
                overrideData.Value = productCost;
                GameEvent.OnCityCheckJobCost?.Invoke(belongCity, jobId, personArray, overrideData);
                productCost = overrideData.Value;

                // 计算技巧值获取
                overrideData.Value = techniquePointGain;
                GameEvent.OnCityJobGainTechniquePoint?.Invoke(belongCity, jobId, personArray, overrideData);
                techniquePointGain = overrideData.Value;

                // 计算经验获取
                overrideData.Value = meritGain;
                GameEvent.OnCityJobGainMerit?.Invoke(belongCity, jobId, personArray, overrideData);
                techniquePointGain = overrideData.Value;

                // 经验获取
                for (int i = 0; i < personArray.Length; i++)
                {
                    Person person = personArray[i];
                    if (person == null) continue;
                    person.merit += meritGain;
                    person.GainExp(meritGain);
                    //person.ActionOver = true;
                }

                belongCity.BelongForce.GainTechniquePoint(techniquePointGain);
            }

            if (buildingType.product > 0)
            {
                if (hasWorker && belongCity.gold > productCost)
                {
                    // 计算建筑实际产出
                    int product = buildingType.product;
                    overrideData.Value = product;
                    GameEvent.OnBuildingCalculateProduct?.Invoke(building, overrideData);
                    product = overrideData.Value;

                    // 计算单独产出逻辑
                    switch (buildingType.kind)
                    {
                        case (int)BuildingKindType.Barracks:
                            {
                                int value = product * totalFactor / 10000;
                                overrideData.Value = value;
                                GameEvent.OnCityJobResult?.Invoke(belongCity, jobId, personArray, overrideData);

                                // 治安对征兵的影响
                                overrideData.Value = (int)(overrideData.Value * (1f - Mathf.Max(0, (100 - belongCity.security)) * scenario.Variables.securityInfluenceRecruitTroops));

                                // 额外降低治安
                                int s = -GameRandom.Range(1, 3);
                                belongCity.AddSecurity(s);
                                building.Render?.ShowInfo(s, (int)InfoType.Security);
                                belongCity.morale = (belongCity.troops * belongCity.morale + overrideData.Value * 30) / (belongCity.troops + overrideData.Value);
                                building.AccumulatedProduct += overrideData.Value;

                            }
                            break;
                        default:
                            {
                                int value = product * totalFactor / 10000;
                                overrideData.Value = value;

                                GameEvent.OnCityJobResult?.Invoke(belongCity, jobId, personArray, overrideData);
                                building.AccumulatedProduct += overrideData.Value;
                            }
                            break;
                    }

                    if (productCost > 0)
                    {
                        belongCity.gold -= buildingType.productCost;
                        building.Render?.ShowInfo(-buildingType.productCost, (int)InfoType.Gold);
                    }
                }
                else
                {
                    // 闲置产出
                    int value = buildingType.emptyProduct * leader_factor / 100;
                    overrideData.Value = value;

                    GameEvent.OnBuildingCalculateProduct?.Invoke(building, overrideData);
                    building.AccumulatedProduct += overrideData.Value;
                }
            }
            overrideData.Recycle();
            GameUtility.ClearJobFeature();
        }

        /// <summary>
        /// 季度收入粮食
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        void OnCitySeasonStart(City city, Scenario scenario)
        {
            if (city.BelongCorps == null)
                return;

            // 计算太守对于收入的影响
            int leader_factor = GetCityLeaderInfuse(city, (int)AttributeType.Politics);
            int totalValue = city.BaseGainFood * leader_factor / 100;
            city.allBuildings.ForEach(building =>
            {
                totalValue += building.AccumulatedFood;
                building.AccumulatedFood = 0;
            });

            // 人口对粮食收入的影响
            if (scenario.Variables.populationEnable)
            {
                totalValue += (int)(city.population * scenario.Variables.populationFoodCostFactor * 0.5f);
            }

            float totalFoodFactor = (city.IsPlayer ? scenario.Variables.playerFoodFactor : scenario.Variables.foodFactor);
            totalValue = (int)(totalValue * totalFoodFactor);

            //totalValue = GameRandom.Random(totalValue, 0.05f);
            city.AddFood(totalValue);
#if SANGO_DEBUG
            Sango.Log.Info($"城市：{city.Name}, 收获粮食：{totalValue}, 现有粮食: {city.food}");
#endif
            city.Render?.ShowInfo(totalValue, (int)InfoType.Food);
        }

        /// <summary>
        /// 月度金钱收入
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        void OnCityMonthStart(City city, Scenario scenario)
        {
            if (city.BelongCorps == null)
                return;

            // 计算太守对于收入的影响
            int leader_factor = GetCityLeaderInfuse(city, (int)AttributeType.Politics);
            int totalValue = city.BaseGainGold * leader_factor / 100;
            //Sango.Log.Error($"太守P:{leader_factor}, city.BaseGainGold{city.BaseGainGold}, t:{totalValue}");
            city.allBuildings.ForEach(building =>
            {

                if (building.AccumulatedGold > 0)
                {
                    totalValue += building.AccumulatedGold;
                    //Sango.Log.Error($"建筑:{building.Name}, AccumulatedGold{building.AccumulatedGold}, t:{totalValue}");
                    building.AccumulatedGold = 0;
                }
            });

            // 人口对金钱收入的影响
            if (scenario.Variables.populationEnable)
            {
                totalValue += (int)(city.population * scenario.Variables.populationGoldIncomeFactor);
            }

            float totalGoldFactor = (city.IsPlayer ? scenario.Variables.playerGoldFactor : scenario.Variables.goldFactor);
            totalValue = (int)(totalValue * totalGoldFactor);

            //Sango.Log.Error($"all: t:{totalValue}");
            //totalFoodt = GameRandom.Random(totalFood, 0.05f);
            city.AddGold(totalValue);

#if SANGO_DEBUG
            Sango.Log.Info($"城市：{city.Name},  收获资金：{totalValue}, 现有资金: {city.gold}");
#endif
            city.Render?.ShowInfo(totalValue, (int)InfoType.Gold);

        }

        struct PBuilding
        {
            public float p;
            public Building building;
        }


        /// <summary>
        /// 委任工作
        /// </summary>
        /// <param name="scenario"></param>
        public void AppointWorking(City city, Scenario scenario)
        {
            /*
                治安预留1人, 训练看气力, 征兵看粮食收入, 生产看兵器缺口
             */
            switch (city.workingAppointType)
            {
                // 重视内政
                // 重视军事
                // 均衡发展
            }

            city.allBuildings.ForEach((building) => { if (building.isComplate) building.Workers?.Clear(); });
            city.allPersons.ForEach((person) => { person.workingBuilding = null; });

            float targetGold = 5000f;
            float targetFood = city.troops * 2.5f;
            float targetTroop = city.food / 2f;
            float targetItemNumber = city.troops * 1.2f;
            float targetHorseNumber = city.troops;
            float targetBoatNumber = 100f;
            float targetMachineNumber = 100f;
            float targetSecurity = 90;
            float targetMorale = 100;
            if (city.IsBorderCity)
            {
                targetMorale = city.MaxMorale;
                targetGold = 500;
            }

            float goldP = city.gold / targetGold;
            float foodP = city.food / targetFood;
            float troopP = city.troops / targetTroop;
            int totalWeaponNum = city.itemStore.GetNumber(new int[] { 2, 3, 4 });
            float itemP = totalWeaponNum / targetItemNumber;
            int totalHorseNum = city.itemStore.GetNumber((int)ItemStoreKindType.Horse);
            float horseP = totalHorseNum / targetHorseNumber;
            int totalBoatNum = city.itemStore.GetNumber((int)ItemStoreKindType.Boat);
            float boatP = totalBoatNum / targetBoatNumber;
            int totalMachineNum = city.itemStore.GetNumber(new int[] { (int)ItemStoreKindType.Helepolis, (int)ItemStoreKindType.Catapult });
            float machineP = totalMachineNum / targetMachineNumber;
            float securityP = Math.Max(0, city.security - 50) / (targetSecurity - 50);
            float moraleP = city.morale / targetMorale;

            List<PBuilding> pBuildings = new List<PBuilding>();
            city.allBuildings.ForEach((building) =>
            {
                if (!building.isComplate) return;

                BuildingType buildingType = building.BuildingType;
                switch (buildingType.kind)
                {
                    case (int)BuildingKindType.Farm:
                    case (int)BuildingKindType.Barn:
                    case (int)BuildingKindType.MilitaryGarrison:
                        pBuildings.Add(new PBuilding() { p = foodP, building = building });
                        break;
                    case (int)BuildingKindType.Market:
                    case (int)BuildingKindType.FishMarket:
                    case (int)BuildingKindType.BigMarket:
                    case (int)BuildingKindType.BlackMarket:
                    case (int)BuildingKindType.Mint:
                        pBuildings.Add(new PBuilding() { p = goldP, building = building });
                        break;
                    case (int)BuildingKindType.Barracks:
                        if (troopP < 1)
                            pBuildings.Add(new PBuilding() { p = troopP, building = building });
                        break;
                    case (int)BuildingKindType.BlacksmithShop:
                        {
                            if (goldP > 1 || itemP < 1)
                            {
                                pBuildings.Add(new PBuilding() { p = itemP, building = building });
                                // 设置产出
                                // 统计适应偏向
                                int[] levelTotal = new int[3] { 1, 1, 1 };
                                city.allPersons.ForEach(x =>
                                {
                                    levelTotal[0] += x.SpearLv;
                                    levelTotal[1] += x.HalberdLv;
                                    levelTotal[2] += x.CrossbowLv;
                                });

                                int sumTotal = levelTotal[0] + levelTotal[1] + levelTotal[2];

                                for (int itemTypeId = 2; itemTypeId <= 4; itemTypeId++)
                                {
                                    int itemNum = city.itemStore.GetNumber(itemTypeId);
                                    int destNum = totalWeaponNum * levelTotal[itemTypeId - 2] / sumTotal;
                                    levelTotal[itemTypeId - 2] = Mathf.Max(1, (destNum - itemNum) / 100);
                                }

                                int targetIndex = GameRandom.RandomWeightIndex(levelTotal);
                                building.ProductItemId = targetIndex + 2;

                            }
                        }
                        break;
                    case (int)BuildingKindType.Stable:
                        if (goldP > 1 || horseP < 1)
                            pBuildings.Add(new PBuilding() { p = horseP, building = building });
                        break;
                    case (int)BuildingKindType.BoatFactory:
                        {
                            if (goldP > 1 || boatP < 1)
                            {
                                pBuildings.Add(new PBuilding() { p = boatP, building = building });
                                int targetBoatId = 12;
                                ItemType itemType = scenario.GetObject<ItemType>(targetBoatId);
                                if (itemType.IsValid(city.BelongForce))
                                {
                                    building.ProductItemId = targetBoatId;
                                }
                                else
                                {
                                    building.ProductItemId = targetBoatId - 1;
                                }
                            }
                        }
                        break;
                    case (int)BuildingKindType.MechineFactory:
                        {
                            if (goldP > 1 || machineP < 1)
                            {
                                pBuildings.Add(new PBuilding() { p = machineP, building = building });
                                int monsterNum = city.itemStore.GetNumber((int)ItemStoreKindType.Helepolis);
                                int towerNum = city.itemStore.GetNumber((int)ItemStoreKindType.Catapult);

                                int tagetItemId;
                                int totalNum;
                                ItemType targetItemType;
                                if (towerNum > monsterNum)
                                {
                                    tagetItemId = 7;
                                    totalNum = monsterNum;
                                    targetItemType = scenario.GetObject<ItemType>(tagetItemId);
                                    if (!targetItemType.IsValid(city.BelongForce))
                                        tagetItemId--;
                                }
                                else
                                {
                                    tagetItemId = 9;
                                    totalNum = towerNum;
                                    targetItemType = scenario.GetObject<ItemType>(tagetItemId);
                                    if (!targetItemType.IsValid(city.BelongForce))
                                        tagetItemId--;
                                }
                                building.ProductItemId = tagetItemId;
                            }
                        }
                        break;
                    case (int)BuildingKindType.MilitaryOffice:
                        if (securityP < 1 || moraleP < 1)
                        {
                            pBuildings.Add(new PBuilding() { p = Math.Min(securityP, moraleP), building = building });
                        }
                        break;
                    case (int)BuildingKindType.RecruitBuilding:
                        pBuildings.Add(new PBuilding() { p = 100, building = building });
                        break; ;
                }
            });

            pBuildings.Sort((a, b) => a.p.CompareTo(b.p));

            AutoSetWorker(city, pBuildings);
        }

        /// <summary>
        /// 回合结束搜索
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        void OnCityTurnEnd(City city, Scenario scenario)
        {
            if (city.BelongCorps == null)
                return;
            city.allBuildings.ForEach((building) =>
            {
                if (!building.isComplate)
                    return;

                BuildingType buildingType = building.BuildingType;
                switch (buildingType.kind)
                {
                    // 自动搜索
                    case (int)BuildingKindType.RecruitBuilding:
                        if (building.Workers != null)
                        {
                            // 人才府自动走军师推荐去登庸在野武将
                            if (city.wildPersons.Count > 0)
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

                            building.Workers.ForEach((worker) =>
                            {
                                CityPersonSearchingEvent te = RenderEvent.Instance.Create<CityPersonSearchingEvent>();
                                te.Init(city, worker);
                                te.searchingType = 1;
                                RenderEvent.Instance.Add(te);
                            });
                        }
                        break;
                }
            });

        }

        /// <summary>
        /// 回合收入
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        void OnCityTurnStart(City city, Scenario scenario)
        {
            if (city.BelongCorps == null)
                return;

            // 获取收入
            city.allBuildings.ForEach((building) =>
            {
                if (!building.isComplate)
                    return;

                BuildingType buildingType = building.BuildingType;
                switch (buildingType.kind)
                {
                    case (int)BuildingKindType.Barracks:
                        if (building.AccumulatedProduct > 0)
                        {
                            city.AddTroops(building.AccumulatedProduct);
                            city.Render?.UpdateRender();
                            building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Troop);
                            building.AccumulatedProduct = 0;
                        }
                        break;
                    case (int)BuildingKindType.BlacksmithShop:
                        if (building.AccumulatedProduct > 0)
                        {
                            int p = building.ProductItemId;
                            if (building.ProductItemId == 0)
                            {
                                // 设置产出
                                // 统计适应偏向
                                int[] levelTotal = new int[3] { 1, 1, 1 };
                                city.allPersons.ForEach(x =>
                                {
                                    levelTotal[0] += x.SpearLv;
                                    levelTotal[1] += x.HalberdLv;
                                    levelTotal[2] += x.CrossbowLv;
                                });
                                int totalWeaponNum = city.itemStore.GetNumber(new int[] { 2, 3, 4 });

                                int sumTotal = levelTotal[0] + levelTotal[1] + levelTotal[2];

                                for (int itemTypeId = 2; itemTypeId <= 4; itemTypeId++)
                                {
                                    int itemNum = city.itemStore.GetNumber(itemTypeId);
                                    int destNum = totalWeaponNum * levelTotal[itemTypeId - 2] / sumTotal;
                                    levelTotal[itemTypeId - 2] = Mathf.Max(1, (destNum - itemNum) / 100);
                                }

                                int targetIndex = GameRandom.RandomWeightIndex(levelTotal);
                                p = targetIndex + 2;
                            }

                            city.AddItem(p, building.AccumulatedProduct);
                            city.Render?.UpdateRender();
                            switch (p)
                            {
                                case 2:
                                    building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Spear);
                                    break;
                                case 3:
                                    building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Sword);
                                    break;
                                case 4:
                                    building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.CrossBow);
                                    break;
                            }
                            building.AccumulatedProduct = 0;
                        }
                        break;
                    case (int)BuildingKindType.Stable:
                        if (building.AccumulatedProduct > 0)
                        {
                            int p = building.ProductItemId;
                            if (building.ProductItemId == 0)
                                p = 5;

                            city.AddItem(p, building.AccumulatedProduct);
                            city.Render?.UpdateRender();
                            building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Horse);
                            building.AccumulatedProduct = 0;
                        }
                        break;
                    case (int)BuildingKindType.BoatFactory:
                        if (building.AccumulatedProduct > 0)
                        {
                            int targetBoatId = building.ProductItemId;
                            if (building.ProductItemId == 0)
                            {
                                ItemType _itemType = scenario.GetObject<ItemType>(targetBoatId);
                                if (_itemType.IsValid(city.BelongForce))
                                {
                                    targetBoatId = 12;
                                }
                                else
                                {
                                    targetBoatId = 11;
                                }
                            }

                            ItemType itemType = scenario.GetObject<ItemType>(targetBoatId);
                            city.AddItem(itemType.storeKind, building.AccumulatedProduct);
                            city.Render?.UpdateRender();
                            building.Render?.ShowInfo(building.AccumulatedProduct, targetBoatId);
                            building.AccumulatedProduct = 0;
                        }
                        break;
                    case (int)BuildingKindType.MechineFactory:
                        if (building.AccumulatedProduct > 0)
                        {
                            int p = building.ProductItemId;

                            if (building.ProductItemId == 0)
                            {
                                int monsterNum = city.itemStore.GetNumber((int)ItemStoreKindType.Helepolis);
                                int towerNum = city.itemStore.GetNumber((int)ItemStoreKindType.Catapult);

                                int tagetItemId;
                                int totalNum;
                                ItemType targetItemType;
                                if (towerNum > monsterNum)
                                {
                                    tagetItemId = 7;
                                    totalNum = monsterNum;
                                    targetItemType = scenario.GetObject<ItemType>(tagetItemId);
                                    if (!targetItemType.IsValid(city.BelongForce))
                                        tagetItemId--;
                                }
                                else
                                {
                                    tagetItemId = 9;
                                    totalNum = towerNum;
                                    targetItemType = scenario.GetObject<ItemType>(tagetItemId);
                                    if (!targetItemType.IsValid(city.BelongForce))
                                        tagetItemId--;
                                }

                                p = tagetItemId;
                            }

                            ItemType itemType = scenario.GetObject<ItemType>(p);
                            city.AddItem(itemType.storeKind, building.AccumulatedProduct);
                            city.Render?.UpdateRender();
                            building.Render?.ShowInfo(building.AccumulatedProduct, p - 1);
                            building.AccumulatedProduct = 0;
                        }
                        break;
                    case (int)BuildingKindType.MilitaryOffice:

                        if (building.AccumulatedProduct > 0)
                        {
                            float targetSecurity = 90;
                            float targetMorale = 100;
                            if (city.IsBorderCity)
                                targetMorale = city.MaxMorale;

                            float securityP = Math.Max(0, city.security - 50) / (targetSecurity - 50);
                            float moraleP = city.morale / targetMorale;

                            if (moraleP <= securityP)
                            {
                                city.AddMorale(GameRandom.Range(building.AccumulatedProduct, building.AccumulatedProduct * 3));
                                city.Render?.UpdateRender();
                                building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Morale);
                            }
                            else
                            {
                                city.AddSecurity(GameRandom.Range(building.AccumulatedProduct / 2, building.AccumulatedProduct));
                                city.Render?.UpdateRender();
                                building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Security);
                            }

                            if (city.portList != null)
                            {
                                city.portList.ForEach(x =>
                                {
                                    x.AddMorale(GameRandom.Range(building.AccumulatedProduct / 2, building.AccumulatedProduct));
                                    x.Render?.UpdateRender();
                                    building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Morale);
                                });
                            }
                            if (city.gateList != null)
                            {
                                city.gateList.ForEach(x =>
                                {
                                    x.AddMorale(GameRandom.Range(building.AccumulatedProduct / 2, building.AccumulatedProduct));
                                    x.Render?.UpdateRender();
                                    building.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Morale);
                                });
                            }
                            building.AccumulatedProduct = 0;
                        }
                        break;
                }
            });

            if (city.BelongCorps.IsPlayer && !city.GetExtensionData<bool>("AppointWorking"))
                return;

            AppointWorking(city, scenario);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            // 有多种产出的时候,需要设置产出界面
            if (TargetBuilding.BuildingType.productItems != null && TargetBuilding.BuildingType.productItems.Length > 0)
                Window.Instance.Open(windowName + "_product");
            else
                Window.Instance.Open(windowName);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (TargetBuilding.BuildingType.productItems != null && TargetBuilding.BuildingType.productItems.Length > 0)
                Window.Instance.Close(windowName + "_product");
            else
                Window.Instance.Close(windowName);
        }

        public void SetBuildingWorker(Building building, List<Person> workers)
        {
            if (building.Workers == null)
            {
                building.Workers = new SangoObjectList<Person>();
            }

            building.RemoveAllWorkers();

            foreach (var worker in workers)
            {
                building.AddWorker(worker);
            }
        }

        static int[][] CityBuildingTemplate = new int[][] {
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
        //        (int)BuildingKindType.TrainTroopBuilding,
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
      //          (int)BuildingKindType.TrainTroopBuilding,
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
       //         (int)BuildingKindType.TrainTroopBuilding,
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
          //      (int)BuildingKindType.TrainTroopBuilding,
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
    }
}
