using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某类型城池的治安下降值比例
    /// value: 值(百分比) kinds: 城市类型
    /// </summary>
    public class CitySecurityChange : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCitySecurityChangeOnSeasonStart += OnCitySecurityChangeOnSeasonStart;
        }

        public override void Clear()
        {
            GameEvent.OnCitySecurityChangeOnSeasonStart -= OnCitySecurityChangeOnSeasonStart;
        }

        void OnCitySecurityChangeOnSeasonStart(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value = (int)System.Math.Ceiling(overrideData.Value * value / 100f);

        }
    }
}
