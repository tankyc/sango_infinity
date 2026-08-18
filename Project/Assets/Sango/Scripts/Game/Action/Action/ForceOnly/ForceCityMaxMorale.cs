using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 增加势力城池的士气上限 
    /// value: 值  kinds: 城市类型
    /// </summary>
    public class ForceCityMaxMorale : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateMaxMorale += OnCityCalculateMaxMorale;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateMaxMorale -= OnCityCalculateMaxMorale;
        }

        void OnCityCalculateMaxMorale(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value += value;
        }

    }
}
