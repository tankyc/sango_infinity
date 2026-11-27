using Newtonsoft.Json.Linq;
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
    /// 接入事件：OnBuildingCalculateFoodGain
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
            if (Force != null && buildingBase.BelongForce != Force) return;
            if (Building != null && Building != buildingBase) return;
            if (Building.BelongCity == null) return;

            // maxTroops 是硬上限：兵力超过 3 万后倍率必须锁在最大值。
            // 旧实现只有 Math.Max，没有 Math.Min；兵力被科技/测试改到上亿时
            // factor 会先发生 int 溢出变成负数，粮收入随之变负。
            if (maxTroops <= minTroops)
                return;

            int troops = Building.BelongCity.troops;
            if (troops < minTroops)
                troops = minTroops;
            else if (troops > maxTroops)
                troops = maxTroops;

            int factor = (int)((long)value * (troops - minTroops) / (maxTroops - minTroops));

            overrideData.Value = overrideData.Value + overrideData.Value * factor / 100;
        }
    }
}
