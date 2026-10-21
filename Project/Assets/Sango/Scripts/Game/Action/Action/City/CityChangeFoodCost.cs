using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 把据点士兵的粮食消耗改为固定值 value。
    ///
    /// 接入事件：OnCityCalculateFoodCost（据点士兵粮食消耗计算时）。
    /// </summary>
    public class CityChangeFoodCost : CityActionBase
    {
        /// <summary>
        /// 提升百分比
        /// </summary>
        int value;

        List<int> cityKinds;

        /// <summary>
        /// 初始化城市工作效率提升动作
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            JArray kindsArray = p.Value<JArray>("cityKinds");
            if (kindsArray != null)
            {
                cityKinds = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    cityKinds.Add(kindsArray[i].ToObject<int>());
            }
            GameEvent.OnCityCalculateFoodCost += OnCityCalculateFoodCost;
        }

        /// <summary>
        /// 清理城市工作效率提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityCalculateFoodCost -= OnCityCalculateFoodCost;
        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="jobId">工作ID</param>
        /// <param name="persons">参与工作的人员</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityCalculateFoodCost(City city, Scenario scenario, OverrideData<int> result)
        {
            if (City != city) return;
            if (cityKinds != null && !cityKinds.Contains(city.BuildingType.kind)) return;
            result.Value = value;
        }
    }
}
