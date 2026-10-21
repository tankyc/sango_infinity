namespace Sango.Core
{
    /// <summary>
    /// 城市沦陷：接入 GameEvent.OnCityFall（城市, 新归属势力, 攻方部队）。
    ///
    /// 注意触发时机：此时城市的归属**已经**变成新势力，所以：
    ///   ActionCity 仍是这座城（宿主引用），
    ///   ActionForce 取的是 City.BelongForce（= 新势力），
    ///   TargetForce 是事件参数里的新势力，TargetTroop 是攻方部队。
    /// 想判断"原势力"请在效果里用变量或条件自行记录，不要读 ActionForce。
    /// </summary>
    public class TriggerCityFall : CityTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnCityFall += OnCityFall;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnCityFall -= OnCityFall;
        }

        void OnCityFall(City city, Force newForce, Troop atkTroop)
        {
            if (!CheckCity(city))
                return;

            ResetTargets();
            targetForce = newForce;
            targetTroop = atkTroop;
            triggerCall?.Invoke(this);
        }
    }
}
