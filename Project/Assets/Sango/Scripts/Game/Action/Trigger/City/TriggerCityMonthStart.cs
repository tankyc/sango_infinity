namespace Sango.Core
{
    /// <summary>
    /// 城市月初：接入 GameEvent.OnCityMonthStart（城市, 剧本），每月触发一次。
    ///
    /// 与 OnCityTurnStart（每回合，一年 36 次）的区别：本事件一年只触发 12 次，
    /// 做"按月结算"的玩法（月俸、月度人口增长）请用这个。
    /// </summary>
    public class TriggerCityMonthStart : CityTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnCityMonthStart += OnCityMonthStart;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnCityMonthStart -= OnCityMonthStart;
        }

        void OnCityMonthStart(City city, Scenario scenario)
        {
            if (!CheckCity(city))
                return;

            ResetTargets();
            triggerCall?.Invoke(this);
        }
    }
}
