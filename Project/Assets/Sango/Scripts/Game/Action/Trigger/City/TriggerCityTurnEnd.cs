namespace Sango.Core
{
    /// <summary>
    /// 城市回合结束：接入 GameEvent.OnCityTurnEnd（城市, 剧本）。
    /// </summary>
    public class TriggerCityTurnEnd : CityTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnCityTurnEnd += OnCityTurnEnd;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnCityTurnEnd -= OnCityTurnEnd;
        }

        void OnCityTurnEnd(City city, Scenario scenario)
        {
            if (!CheckCity(city))
                return;

            ResetTargets();
            triggerCall?.Invoke(this);
        }
    }
}
