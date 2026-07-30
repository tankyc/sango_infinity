using Sango.Core.Player;
using Sango.Render;
using Sango.Core.Tools;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Sango.Core
{
    /// <summary>
    /// 经典内政系统
    /// 可指派武将到建筑工作以提升建筑的功能产出
    /// </summary>
    [GameSystem(order = 99)]
    public class ClassicsCityWorking : GameSystem
    {
        public Building TargetBuilding { get; set; }
        public City TargetCity { get; set; }

        public override void Init()
        {
            // 增加建筑菜单
            //GameSystem.GetSystem<CityRecruitTroops>().Init();   // 征兵
            //GameSystem.GetSystem<CityCreateItems>().Init();
            //Singleton<CityDevelop>.Instance.Init();
            //Singleton<CityFarming>.Instance.Init();
            //GameSystem.GetSystem<CityInspection>().Init();
            //GameSystem.GetSystem<CityTrainTroops>().Init();     // 训练

            //GameSystem.GetSystem<CitySeraching>().Init();
            ScenarioInit();
        }

        public override void Clear()
        {
            ScenarioClear();
        }

        public void ScenarioInit()
        {
            GameSystem.GetSystem<CityRecruitTroops>().Init();   // 征兵
            GameSystem.GetSystem<CityCreateItems>().Init();
            //Singleton<CityDevelop>.Instance.Init();
            //Singleton<CityFarming>.Instance.Init();
            GameSystem.GetSystem<CityInspection>().Init();
            GameSystem.GetSystem<CityTrainTroops>().Init();     // 训练

            GameSystem.GetSystem<CitySeraching>().Init();

            GameEvent.OnCityMonthStart += OnCityMonthStart;
            GameEvent.OnCitySeasonStart += OnCitySeasonStart;
            GameEvent.OnCityCalculateHarvest += OnCityCalculateHarvest;
            GameEvent.OnCityAIPrepare += OnCityAIPrepare;
        }

        public void ScenarioClear()
        {
            // 增加建筑菜单
            GameSystem.GetSystem<CityRecruitTroops>().Clear();   // 征兵
            GameSystem.GetSystem<CityCreateItems>().Clear();
            //Singleton<CityDevelop>.Instance.Clear();
            //Singleton<CityFarming>.Instance.Clear();
            GameSystem.GetSystem<CityInspection>().Clear();
            GameSystem.GetSystem<CityTrainTroops>().Clear();     // 训练

            GameSystem.GetSystem<CitySeraching>().Clear();

            GameEvent.OnCityMonthStart -= OnCityMonthStart;
            GameEvent.OnCitySeasonStart -= OnCitySeasonStart;
            GameEvent.OnCityCalculateHarvest -= OnCityCalculateHarvest;
            GameEvent.OnCityAIPrepare -= OnCityAIPrepare;



        }


        void OnCityAIPrepare(City city, Scenario scenario)
        {
            CityAI.CityBuildingTemplate = CityBuildingTemplate;
            List<System.Func<City, Scenario, bool>> AICommandList = city.AICommandList;
            if (city.IsBorderCity)
            {

                AICommandList.Add(CityAI.AIRewardPerson);
                AICommandList.Add(CityAI.AISearching);
                AICommandList.Add(CityAI.AIRecruitPerson);
                AICommandList.Add(CityAI.AIAttack);
                AICommandList.Add(CityAI.AITradeFood);
                AICommandList.Add(CityAI.AISecurity);
                AICommandList.Add(CityAI.AITrainTroop);

                if (city.troops < 20000)
                {
                    AICommandList.Add(CityAI.AIRecruitTroop);
                    AICommandList.Add(CityAI.AIIntrior);
                }
                else
                {
                    if (scenario.Info.day == 10)
                    {
                        AICommandList.Add(CityAI.AIRecruitTroop);
                        AICommandList.Add(CityAI.AICreateItems);
                        AICommandList.Add(CityAI.AIIntrior);
                    }
                    else if (scenario.Info.day == 20)
                    {
                        AICommandList.Add(CityAI.AIIntrior);
                        AICommandList.Add(CityAI.AIRecruitTroop);
                        AICommandList.Add(CityAI.AICreateItems);
                    }
                    else
                    {
                        AICommandList.Add(CityAI.AICreateItems);
                        AICommandList.Add(CityAI.AIRecruitTroop);
                        AICommandList.Add(CityAI.AIIntrior);
                    }
                }
                AICommandList.Add(CityAI.AITradeFood);
            }
            else
            {
                // 物资输送
                AICommandList.Add(CityAI.AIRewardPerson);
                AICommandList.Add(CityAI.AISearching);
                AICommandList.Add(CityAI.AIRecruitPerson);
                AICommandList.Add(CityAI.AITransfrom);

                AICommandList.Add(CityAI.AISecurity);
                AICommandList.Add(CityAI.AITradeFood);
                AICommandList.Add(CityAI.AITrainTroop);
                AICommandList.Add(CityAI.AICreateItems);
                AICommandList.Add(CityAI.AIRecruitTroop);
                AICommandList.Add(CityAI.AIIntrior);
                AICommandList.Add(CityAI.AIAttack);

            }
        }

        void OnCityCalculateHarvest(City city)
        {
            if (city.BelongCorps == null)
                return;

            ScenarioVariables variables = Scenario.Cur.Variables;

            // 人口增长率
            city.population_increase_factor = variables.populationIncreaseBaseFactor;

            // 计算基础收入
            city.totalGainFood = city.BaseGainFood + city.agriculture * variables.agriculture_add_food;
            city.totalGainGold = city.BaseGainGold + city.commerce * variables.commerce_add_gold;

            // 人口对金钱收入的影响
            if (variables.populationEnable)
            {
                city.totalGainGold += (int)(city.population * variables.populationGoldIncomeFactor);
                city.totalGainFood += (int)(city.population * variables.populationFoodCostFactor * 0.5f);
            }

            // 计算建筑收入
            city.allBuildings.ForEach(x =>
            {
                if (x.isComplate)
                {
                    Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(x.BuildingType.foodGain);
                    GameEvent.OnBuildingCalculateFoodGain?.Invoke(x, overrideData);
                    city.totalGainFood += overrideData.ValueAndRecycle;

                    overrideData = Tools.OverrideData<int>.Create(x.BuildingType.goldGain);
                    GameEvent.OnBuildingCalculateGoldGain?.Invoke(x, overrideData);
                    city.totalGainGold += overrideData.ValueAndRecycle;

                    overrideData = Tools.OverrideData<int>.Create(x.BuildingType.populationGain);
                    GameEvent.OnBuildingCalculatePopulationGain?.Invoke(x, overrideData);
                    city.population_increase_factor += overrideData.ValueAndRecycle;
                }
            });

            float securityInfluence = (((float)city.security / variables.securityInfluenceMax) - 1) * variables.securityInfluence;
            float popularSupportInfluence = (((float)city.popularSupport / variables.popularSupportInfluenceMax) - 1) * variables.popularSupportInfluence;
            float leftInfluence = 1.0f + securityInfluence + popularSupportInfluence;

            float totalFoodFactor = (city.IsPlayer ? variables.playerFoodFactor : variables.foodFactor);
            float totalGoldFactor = (city.IsPlayer ? variables.playerGoldFactor : variables.goldFactor);

            //totalGainFood = Mathf.CeilToInt(leftInfluence * totalGainFood * (variables.foodFactor + extraGainFoodFactor));
            //totalGainGold = Mathf.CeilToInt(leftInfluence * totalGainGold * (variables.goldFactor + extraGainGoldFactor));
            city.totalGainFood = Mathf.CeilToInt(city.totalGainFood * (totalFoodFactor + city.extraGainFoodFactor));
            city.totalGainGold = Mathf.CeilToInt(city.totalGainGold * (totalGoldFactor + city.extraGainGoldFactor));

            city.population_increase_factor *= city.extraPopulationFactor;
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

            int harvest = GameRandom.Random(city.totalGainFood, 0.05f);
            city.Render?.ShowInfo(harvest, (int)InfoType.Food);
            city.food += harvest;
#if SANGO_DEBUG
            Sango.Log.Info($"城市：{city.Name}, 收获粮食：{harvest}, 现有粮食: {city.food}");
#endif
            city.Render?.UpdateRender();
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

            int inComingGold = GameRandom.Random(city.totalGainGold, 0.05f);
            city.Render?.ShowInfo(inComingGold, (int)InfoType.Gold);
            city.AddGold(inComingGold);

#if SANGO_DEBUG
            Sango.Log.Info($"城市：{city.Name}, 武将人数:{city.allPersons.Count}, 收入<-- 金钱:{inComingGold},  现有金钱: {city.gold}");
#endif
            city.Render?.UpdateRender();
        }

        static int[][] CityBuildingTemplate = new int[][] {
            // 后方城市
            new int[] {
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
               // (int)BuildingKindType.PatrolBureau,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Stable,
                (int)BuildingKindType.MechineFactory,// 12小城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.BoatFactory,
                (int)BuildingKindType.CustomKind,// 16中城


                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.CustomKind,
                (int)BuildingKindType.Market,// 20大城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 24巨城
            },

            // 边境城市
            new int[] {
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Farm,
               // (int)BuildingKindType.PatrolBureau,
                (int)BuildingKindType.Stable,
                (int)BuildingKindType.MechineFactory,
                (int)BuildingKindType.BoatFactory,// 12小城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,// 16中城


                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 20大城

                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Stable,// 24巨城
            },

            // 后方港口城市
            new int[] {
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
               // (int)BuildingKindType.PatrolBureau,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.BoatFactory,
                (int)BuildingKindType.MechineFactory,// 12小城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.BoatFactory,
                (int)BuildingKindType.CustomKind,// 16中城


                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.CustomKind,
                (int)BuildingKindType.Market,// 20大城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 24巨城
            },

             // 边境港口城市
            new int[] {
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Farm,
               // (int)BuildingKindType.PatrolBureau,
                (int)BuildingKindType.BoatFactory,
                (int)BuildingKindType.MechineFactory,
                (int)BuildingKindType.BoatFactory,// 12小城

                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Barracks,
                (int)BuildingKindType.BlacksmithShop,// 16中城


                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,// 20大城

                (int)BuildingKindType.Market,
                (int)BuildingKindType.Farm,
                (int)BuildingKindType.Market,
                (int)BuildingKindType.Stable,// 24巨城
            },
        };
    }
}
