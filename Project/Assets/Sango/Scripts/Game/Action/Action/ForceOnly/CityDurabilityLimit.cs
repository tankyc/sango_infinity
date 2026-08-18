using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某类型城池的最大耐久增加
    /// value: 值, kinds: 建筑类型
    /// </summary>
    public class CityDurabilityLimit : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateMaxDurability += OnCityCalculateMaxDurability;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateMaxDurability -= OnCityCalculateMaxDurability;
        }

        void OnCityCalculateMaxDurability(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value += value;
        }
    }
}
