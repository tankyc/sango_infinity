using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action 
{ 
    /// <summary>
    /// 降低研究费用（value 为百分比）。
    ///
    /// 接入事件：OnCityResearchCost（研究费用计算时）。
    /// </summary>
    public class CityImproveResearchCost : CityActionBase
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
            GameEvent.OnCityResearchCost += OnCityResearchCost;
        }

        /// <summary>
        /// 清理城市工作效率提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityResearchCost -= OnCityResearchCost;
        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="jobId">工作ID</param>
        /// <param name="persons">参与工作的人员</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityResearchCost(City city, Person[] persons, Technique tech, OverrideData<int> goldOverride, OverrideData<int> tpOverride, OverrideData<int> turnCountOveride)
        {
            if (City != city) return;
            goldOverride.Value = goldOverride.Value * value / 100;
        }
    }
}
