using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某类型城池的最大资金增加
    /// value: 值, 
    /// kinds: 建筑类型
    /// </summary>
    public class CityGoldLimit : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateMaxGold += OnCityCalculateMaxGold;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateMaxGold -= OnCityCalculateMaxGold;
        }

        void OnCityCalculateMaxGold(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value += value;
        }
    }
}
