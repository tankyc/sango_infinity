using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 城池商人系统逻辑
    /// </summary>
    [GameSystem(order = 100)]
    public class CityBusiness : GameSystem
    {
        int[] businessFactor =
        {
            50, 20, 10, 10, 8, 2
        };

        int[] businessValue =
        {
            5, 6, 7, 0, 8, 9
        };

        public Building TargetBuilding { get; set; }


        public override void Init()
        {
            GameEvent.OnMonthUpdate += OnMonthStart;
            GameEvent.OnScenarioInit += OnScenarioInit;
            //GameEvent.OnSeasonUpdate += OnSeasonStart;
            //GameEvent.OnCityTurnStart += OnCityTurnStart;
        }

        public override void Clear()
        {
            GameEvent.OnMonthUpdate -= OnMonthStart;
            GameEvent.OnScenarioInit -= OnScenarioInit;
            //GameEvent.OnSeasonUpdate -= OnSeasonStart;
            //GameEvent.OnCityTurnStart -= OnCityTurnStart;
        }

        public void UpdateBusiness(Scenario scenario)
        {
            // 每个月变化一次商人系数
            scenario.citySet.ForEach(city =>
            {
                if (!city.IsCity())
                    return;

                city.hasBusiness = (byte)businessValue[GameRandom.RandomWeightIndex(businessFactor)];
            });
        }

        int GetCityLeaderInfuse(City city, int effectAttrType)
        {
            Person leader = city.Leader;
            int leaderAttrValue = 0;
            if (leader != null)
                leaderAttrValue = leader.GetAttribute(effectAttrType);
            // 计算公式
            return 100 + (int)((Mathf.Pow(Mathf.Max(40, leaderAttrValue), 1.5f) / 10 - 25) / 3f);
        }

        /// <summary>
        /// 季度
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public void OnSeasonStart(Scenario scenario)
        {
            scenario.citySet.ForEach(city =>
            {
                if (!city.IsCity() || city.mBelongCorps == null)
                    return;

                // 换季,治安降低
                Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(scenario.Variables.securityChangeOnSeasonStart);
                GameEvent.OnCitySecurityChangeOnSeasonStart?.Invoke(city, overrideData);
                city.AddSecurity(overrideData.Value);
                city.Render?.UpdateRender();
                city.Render?.ShowInfo(overrideData.Value, (int)InfoType.Security);
                overrideData.Recycle();
            });
        }

        /// <summary>
        /// 月度
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public void OnMonthStart(Scenario scenario)
        {
            // 每个月变化一次商人系数
            UpdateBusiness(scenario);
        }

        /// <summary>
        /// 月度
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public void OnScenarioInit(Scenario scenario)
        {
            if(!scenario.Info.isSave)
            {
                UpdateBusiness(scenario);
            }
        }
    }
}
