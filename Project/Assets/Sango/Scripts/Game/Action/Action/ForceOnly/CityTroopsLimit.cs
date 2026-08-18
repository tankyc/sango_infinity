using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某类型城池的最大士兵增加
    /// value: 值
    /// kinds: 城市类型
    /// </summary>
    public class CityTroopsLimit : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateMaxTroops += OnCityCalculateMaxTroops;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateMaxTroops -= OnCityCalculateMaxTroops;
        }

        void OnCityCalculateMaxTroops(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value += value;
        }

    }
}
