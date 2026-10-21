namespace Sango.Core
{
    /// <summary>
    /// 城市回合开始：接入 GameEvent.OnCityTurnStart（城市, 剧本）。
    ///
    /// 走本触发器时可以改城市状态、给城内武将加 Buff，但收入 / 工作结算还没发生。
    /// </summary>
    public class TriggerCityTurnStart : CityTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnCityTurnStart += OnCityTurnStart;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnCityTurnStart -= OnCityTurnStart;
        }

        void OnCityTurnStart(City city, Scenario scenario)
        {
            if (!CheckCity(city))
                return;

            ResetTargets();
            triggerCall?.Invoke(this);
        }
    }
}
