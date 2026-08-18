using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某类型城池的最大兵粮增加
    /// value: 值, 
    /// kinds: 建筑类型
    /// </summary>
    public class CityFoodLimit : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateMaxFood += OnCityCalculateMaxFood;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateMaxFood -= OnCityCalculateMaxFood;
        }

        void OnCityCalculateMaxFood(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value += value;
        }
    }
}
