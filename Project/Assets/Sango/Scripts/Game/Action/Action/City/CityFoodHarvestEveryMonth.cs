using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 提升粮食收入
    /// value: 百分比
    /// jobIds: 工作ID集合
    /// </summary>
    public class CityFoodHarvestEveryMonth : CityActionBase
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
            GameEvent.OnCityGainFoodHarvest += OnCityGainFoodHarvest;
            GameEvent.OnCityMonthStart += OnCityMonthStart;
        }

        /// <summary>
        /// 清理城市工作效率提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityGainFoodHarvest -= OnCityGainFoodHarvest;
            GameEvent.OnCityMonthStart -= OnCityMonthStart;

        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="jobId">工作ID</param>
        /// <param name="persons">参与工作的人员</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityGainFoodHarvest(City city, OverrideData<int> overrideData)
        {
            if (City != city) return;
            overrideData.Value = overrideData.Value * value / 100 / 3;
        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="jobId">工作ID</param>
        /// <param name="persons">参与工作的人员</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityMonthStart(City city, Scenario scenario)
        {
            if (City != city) return;
            int lastMonth = scenario.Info.month - 1;
            if (lastMonth < 1)
                lastMonth = 12;

            SeasonType last_season = GameDefine.SeasonInMonth[lastMonth - 1];
            SeasonType cur_season = GameDefine.SeasonInMonth[scenario.Info.month - 1];
            if (cur_season == last_season)
            {
                int v = city.totalGainFood * value / 100 / 3;
                city.AddFood(v);
                city.Render?.ShowInfo(v, (int)InfoType.Food);
#if SANGO_DEBUG
            Sango.Log.Info($"城市：{city.Name}, 收获粮食：{v}, 现有粮食: {city.food}");
#endif
            }
        }
    }
}
