/*
 * 文件名：CityEconomyEffectsExt.cs
 * 描述：科技的"经济收益"类扩展效果——金/粮收获、月度治安、建筑人口与产出
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【两条收获链路要分清】
 *   OnCityCalculateGoldHarvest / FoodHarvest 是"计算中"，
 *   OnCityCalculate...HarvestAfter 是"计算完的最后一刀"。
 *   BuildingWorking 里两者共用同一个 OverrideData 并最终写回 city.totalGainGold/Food，
 *   因此在 After 上做乘算最安全：不会与城内加成、太守加成互相覆盖。
 *   现有 CityImproveGoldHarvest / CityImproveFoodHarvest 挂的是"计算中"那一档，
 *   本文件补的是"最后一刀"，两者可以同时存在。
 *
 * 【月度治安是加值而非百分比】
 *   OnCitySecurityChangeOnMonthStart 的初始值是剧本变量 securityChangeOnMonthStart
 *   （通常为 0 或负数）。对它做百分比没有意义（0 × 任何数仍是 0），
 *   因此本效果用"加值"语义：value = 1 表示每月少掉 1 点治安，value = -1 表示多掉 1 点。
 *
 * 【建筑产出为什么只用于科技】
 *   OnBuildingCalculatePopulationGain / Product 的上下文是"某座建筑在结算产出"，
 *   归属判定走 building.BelongForce；特技装配时 Force 为 null，天然不适用。
 */

using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 城市金钱收获的最终修正（value 为百分比）。
    ///
    /// 接入事件：OnCityCalculateGoldHarvestAfter。
    /// 典型用法：科技「商业振兴」value = 15 → 全势力城市金收入 +15%。
    /// </summary>
    public class CityGoldHarvestAfter : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅金收获最终计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCityCalculateGoldHarvestAfter += OnGoldHarvestAfter;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnCityCalculateGoldHarvestAfter -= OnGoldHarvestAfter;
        }

        /// <summary>金收获最终计算回调</summary>
        /// <param name="city">城市</param>
        /// <param name="overrideData">收获值覆盖数据</param>
        void OnGoldHarvestAfter(City city, OverrideData<int> overrideData)
        {
            if (city == null || Force == null) return;
            if (city.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }

    /// <summary>
    /// 城市粮草收获的最终修正（value 为百分比）。
    ///
    /// 接入事件：OnCityCalculateFoodHarvestAfter。
    /// 典型用法：科技「屯田制」value = 20 → 全势力城市粮收入 +20%。
    /// </summary>
    public class CityFoodHarvestAfter : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅粮收获最终计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCityCalculateFoodHarvestAfter += OnFoodHarvestAfter;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnCityCalculateFoodHarvestAfter -= OnFoodHarvestAfter;
        }

        /// <summary>粮收获最终计算回调</summary>
        /// <param name="city">城市</param>
        /// <param name="overrideData">收获值覆盖数据</param>
        void OnFoodHarvestAfter(City city, OverrideData<int> overrideData)
        {
            if (city == null || Force == null) return;
            if (city.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }

    /// <summary>
    /// 月度治安变化的加值修正。
    ///
    /// 接入事件：OnCitySecurityChangeOnMonthStart。
    /// 语义是"加值"而不是百分比：value = 1 让每月治安少掉 1 点，value = -1 则多掉 1 点。
    /// 典型用法：特技/科技「治安维持」value = 1。
    /// </summary>
    public class CitySecurityChangeMonth : ForceActionBase
    {
        /// <summary>加值</summary>
        int value;

        /// <summary>初始化：订阅月度治安变化</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCitySecurityChangeOnMonthStart += OnSecurityChangeMonth;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnCitySecurityChangeOnMonthStart -= OnSecurityChangeMonth;
        }

        /// <summary>月度治安变化回调</summary>
        /// <param name="city">城市</param>
        /// <param name="overrideData">治安变化量覆盖数据</param>
        void OnSecurityChangeMonth(City city, OverrideData<int> overrideData)
        {
            if (city == null || Force == null) return;
            if (city.BelongForce != Force) return;

            overrideData.Value += value;
        }
    }

    /// <summary>
    /// 建筑人口产出的修正（value 为百分比）。
    ///
    /// 接入事件：OnBuildingCalculatePopulationGain。
    /// 该 hook 每座建筑每回合结算一次，值是"本回合应增加的人口"，
    /// 因此百分比加成会随建筑数量线性放大，value 建议不超过 30。
    /// </summary>
    public class BuildingPopulationGain : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅建筑人口产出</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnBuildingCalculatePopulationGain += OnPopulationGain;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingCalculatePopulationGain -= OnPopulationGain;
        }

        /// <summary>人口产出计算回调</summary>
        /// <param name="building">结算中的建筑（可能是城池 / 关港）</param>
        /// <param name="overrideData">产出值覆盖数据</param>
        void OnPopulationGain(BuildingBase building, OverrideData<int> overrideData)
        {
            if (building == null || Force == null) return;
            if (building.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }

    /// <summary>
    /// 建筑产出的修正（value 为百分比）。
    ///
    /// 接入事件：OnBuildingCalculateProduct。
    /// 这里的 product 是"建筑自身产出"（兵营产兵、锻冶产兵器等），
    /// 与人口产出是两条独立链路，可分别配置。
    /// </summary>
    public class BuildingProductGain : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅建筑产出</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnBuildingCalculateProduct += OnProductGain;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingCalculateProduct -= OnProductGain;
        }

        /// <summary>建筑产出计算回调</summary>
        /// <param name="building">结算中的建筑</param>
        /// <param name="overrideData">产出值覆盖数据</param>
        void OnProductGain(BuildingBase building, OverrideData<int> overrideData)
        {
            if (building == null || Force == null) return;
            if (building.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }
}
