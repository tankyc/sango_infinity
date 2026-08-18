using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某类型城池的最大仓库存量增加
    /// value: 值
    /// kinds: 城市类型
    /// </summary>
    public class CityStoreLimit : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateMaxItemStoreSize += OnCityCalculateMaxItemStoreSize;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateMaxItemStoreSize -= OnCityCalculateMaxItemStoreSize;
        }

        void OnCityCalculateMaxItemStoreSize(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            overrideData.Value += value;
        }
    }
}
