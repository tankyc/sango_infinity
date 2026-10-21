using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 让城市每回合额外获得资金（征税类效果）。
    ///
    /// 接入事件：OnCityGainGoldHarvest（把常规资金收入按 value 缩放）
    /// + OnCityTurnStart（月内按天补充）。
    /// </summary>
    public class CityGoldHarvestEveryTurn : CityActionBase
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
            GameEvent.OnCityGainGoldHarvest += OnCityGainGoldHarvest;
            GameEvent.OnCityTurnStart += OnCityTurnStart;
        }

        /// <summary>
        /// 清理城市工作效率提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityGainGoldHarvest -= OnCityGainGoldHarvest;
            GameEvent.OnCityTurnStart -= OnCityTurnStart;

        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="jobId">工作ID</param>
        /// <param name="persons">参与工作的人员</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityGainGoldHarvest(City city, OverrideData<int> overrideData)
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
        void OnCityTurnStart(City city, Scenario scenario)
        {
            if (City != city) return;

            if (scenario.Info.day > 10)
            {
                int v = city.totalGainGold * value / 100 / 3;
                city.AddGold(v);

            Sango.Log.Info($"城市：{city.Name}, 武将人数:{city.allPersons.Count}, 收入<-- 金钱:{v},  现有金钱: {city.gold}");
                city.Render?.ShowInfo(v, (int)InfoType.Gold);
            }
        }
    }
}
