namespace Sango.Core
{
    /// <summary>
    /// 建筑回合结束：接入 GameEvent.OnBuildingTurnEnd（建筑, 剧本）。
    ///
    /// 适合做"结算完之后"的动作，例如给范围内部队回气力、追加状态。
    /// </summary>
    public class TriggerBuildingTurnEnd : BuildingTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnBuildingTurnEnd += OnBuildingTurnEnd;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingTurnEnd -= OnBuildingTurnEnd;
        }

        void OnBuildingTurnEnd(Building building, Scenario scenario)
        {
            if (!CheckBuilding(building))
                return;

            triggerCall?.Invoke(this);
        }
    }
}
