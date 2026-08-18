using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 根据所属城市的士兵数量来增加粮食产量
    /// minTroops : 最小士兵数，低于此数量按此数量计算
    /// maxTroops : 最大士兵数，高于此数量按此数量计算
    /// value : 最大倍率
    /// </summary>
    public class BuildingImproveFoodGainByCityTroops : BuildingActionBase
    {
        int value;
        int minTroops;
        int maxTroops;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            if (Building == null)
                return;
            GameEvent.OnBuildingCalculateFoodGain += OnBuildingCalculateFoodGain;
            value = p.Value<int>("value");
            minTroops = p.Value<int>("minTroops");
            maxTroops = p.Value<int>("maxTroops");
        }

        public override void Clear()
        {
            GameEvent.OnBuildingCalculateFoodGain -= OnBuildingCalculateFoodGain;
        }

        void OnBuildingCalculateFoodGain(BuildingBase buildingBase, OverrideData<int> overrideData)
        {
            if (Force != null && buildingBase.mBelongForce != Force) return;
            if (Building != null && Building != buildingBase) return;
            if (Building.mBelongCity == null) return;

            int troops = Math.Max(Building.mBelongCity.troops, minTroops);
            int factor = value * (troops - minTroops) / (maxTroops - minTroops);

            overrideData.Value = overrideData.Value + overrideData.Value * factor / 100;
        }
    }
}
