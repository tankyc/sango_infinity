namespace Sango.Core
{
    /// <summary>
    /// 城市换季：接入 GameEvent.OnCitySeasonStart（城市, 剧本），每 3 个月一次。
    ///
    /// 适合做季节相关的玩法：秋收加成、换季治安变化、冬季补给等。
    /// </summary>
    public class TriggerCitySeasonStart : CityTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnCitySeasonStart += OnCitySeasonStart;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnCitySeasonStart -= OnCitySeasonStart;
        }

        void OnCitySeasonStart(City city, Scenario scenario)
        {
            if (!CheckCity(city))
                return;

            ResetTargets();
            triggerCall?.Invoke(this);
        }
    }
}
