using Sango.Core.Tools;
using Sango.Core.Player;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 在春秋季初按概率授予城市丰收状态，并在状态期间提高粮食产量。
    /// </summary>
    public class CityBumperHarvest : CityActionBase
    {
        private int chance;
        private int durationMonths;
        private int foodHarvestFactor;

        /// <summary>
        /// 读取丰收规则配置并订阅城市季节、月度及粮食结算事件。
        /// </summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            chance = p.Value<int>("chance");
            durationMonths = p.Value<int>("durationMonths");
            foodHarvestFactor = p.Value<int>("foodHarvestFactor");
            GameEvent.OnCitySeasonStart += OnCitySeasonStart;
            GameEvent.OnCityMonthStart += OnCityMonthStart;
            GameEvent.OnCityCalculateFoodHarvest += OnCityCalculateFoodHarvest;
        }

        /// <summary>
        /// 解除全部丰收相关事件订阅。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCitySeasonStart -= OnCitySeasonStart;
            GameEvent.OnCityMonthStart -= OnCityMonthStart;
            GameEvent.OnCityCalculateFoodHarvest -= OnCityCalculateFoodHarvest;
        }

        /// <summary>
        /// 仅在本城春季或秋季初、且不存在既有丰收时按配置概率开始丰收。
        /// </summary>
        private void OnCitySeasonStart(City city, Scenario scenario)
        {
            if (City != city || city.bumperHarvestRemainingMonths > 0)
            {
                return;
            }
            bool isSpringOrAutumn = scenario.CurSeason == SeasonType.Spring || scenario.CurSeason == SeasonType.Autumn;
            if (!isSpringOrAutumn || !GameRandom.Chance(chance))
            {
                return;
            }

            city.bumperHarvestRemainingMonths = durationMonths;
            // 丰收触发后，玩家与 AI 城市均写入左下角信息窗口。
            PlayerMessage.AddTextMessage($"{city.ColorName}因祈愿迎来丰收，未来三个月粮食产量提高50%。", city.BelongForce, city.x, city.y);
        }

        /// <summary>
        /// 在每月初递减本城已开始的丰收持续月数。
        /// </summary>
        private void OnCityMonthStart(City city, Scenario scenario)
        {
            if (City == city && city.bumperHarvestRemainingMonths > 0)
            {
                city.bumperHarvestRemainingMonths--;
            }
        }

        /// <summary>
        /// 在实际粮食结算时，根据本城丰收状态按配置倍率提高产量。
        /// </summary>
        private void OnCityCalculateFoodHarvest(City city, OverrideData<int> harvest)
        {
            if (City == city && city.bumperHarvestRemainingMonths > 0)
            {
                harvest.Value = harvest.Value * foodHarvestFactor / 100;
            }
        }
    }
}
