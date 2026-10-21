namespace Sango.Core
{
    /// <summary>
    /// 建筑回合开始：接入 GameEvent.OnBuildingTurnStart（建筑, 剧本）。
    ///
    /// 走本触发器时可以做的事：给范围内部队加状态 / 回气力 / 扣耐久等，
    /// 但此时建筑的产出结算还没发生，不要在这里改收入。
    /// </summary>
    public class TriggerBuildingTurnStart : BuildingTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnBuildingTurnStart += OnBuildingTurnStart;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingTurnStart -= OnBuildingTurnStart;
        }

        void OnBuildingTurnStart(Building building, Scenario scenario)
        {
            if (!CheckBuilding(building))
                return;

            triggerCall?.Invoke(this);
        }
    }
}
