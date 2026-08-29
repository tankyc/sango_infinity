using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 提升粮食收入
    /// value: 百分比
    /// jobIds: 工作ID集合
    /// </summary>
    public class CityImproveFoodHarvest : CityActionBase
    {
        /// <summary>
        /// 提升百分比
        /// </summary>
        int value;

        /// <summary>
        /// 初始化城市工作效率提升动作
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCityCalculateFoodHarvest += OnCityCalculateFoodHarvest;
        }

        /// <summary>
        /// 清理城市工作效率提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityCalculateFoodHarvest -= OnCityCalculateFoodHarvest;
        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityCalculateFoodHarvest(City city, OverrideData<int> overrideData)
        {
            if (City != city) return;
            overrideData.Value = overrideData.Value * value / 100;
        }
    }
}
