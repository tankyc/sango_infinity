using Sango.Core.Tools;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 城池治安系统逻辑
    /// </summary>
    [GameSystem(order = 100)]
    public class CitySecurity : GameSystem
    {
        /// <summary>
        /// 最大工作人数
        /// </summary>
        public static int Max_Working_Person_Count = 1;
        public Building TargetBuilding { get; set; }

        public override void Init()
        {
            GameEvent.OnMonthUpdate += OnMonthStart;
            GameEvent.OnSeasonUpdate += OnSeasonStart;
            GameEvent.OnCityTurnStart += OnCityTurnStart;
        }

        public override void Clear()
        {
            GameEvent.OnMonthUpdate -= OnMonthStart;
            GameEvent.OnSeasonUpdate -= OnSeasonStart;
            GameEvent.OnCityTurnStart -= OnCityTurnStart;
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
        /// 季度治安降低
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public void OnSeasonStart(Scenario scenario)
        {
            scenario.citySet.ForEach(city =>
            {
                if (!city.IsCity() || city.mCorps == null)
                    return;

                // 换季,治安降低
                Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(
                   -GameRandom.Range(1, -scenario.Variables.securityChangeOnSeasonStart));
                GameEvent.OnCitySecurityChangeOnSeasonStart?.Invoke(city, overrideData);
                if (overrideData.Value != 0)
                {
                    city.AddSecurity(overrideData.Value);
                    city.Render?.UpdateRender();
                    city.Render?.ShowInfo(overrideData.Value, (int)InfoType.Security);
                }
                overrideData.Recycle();
            });
        }

        /// <summary>
        /// 月度治安概率降低
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public void OnMonthStart(Scenario scenario)
        {
            scenario.citySet.ForEach(city =>
            {
                if (!city.IsCity() || city.mCorps == null)
                    return;

                // 换季,治安降低
                Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(scenario.Variables.securityChangeOnMonthStart);
                GameEvent.OnCitySecurityChangeOnMonthStart?.Invoke(city, overrideData);
                if(overrideData.Value != 0)
                {
                    city.AddSecurity(overrideData.Value);
                    city.Render?.UpdateRender();
                    city.Render?.ShowInfo(overrideData.Value, (int)InfoType.Security);
                }
                overrideData.Recycle();
            });
        }

        /// <summary>
        /// 建筑对治安的提升
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public void OnCityTurnStart(City city, Scenario scenario)
        {
            //if (city.BelongCorps == null)
            //    return;

            //city.allBuildings.ForEach(building =>
            //{
            //    if (building.isComplate && building.BuildingType.kind == (int)BuildingKindType.PatrolBureau)
            //    {
            //        if (building.AccumulatedProduct > 0)
            //        {
            //            city.AddSecurity(building.AccumulatedProduct);
            //            city.Render?.UpdateRender();
            //            city.Render?.ShowInfo(building.AccumulatedProduct, (int)InfoType.Security);
            //            building.AccumulatedProduct = 0;
            //        }
            //    }
            //});
        }
    }
}
