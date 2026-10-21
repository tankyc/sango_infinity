namespace Sango.Core
{
    /// <summary>
    /// 势力回合结束：接入 GameEvent.OnForceTurnEnd（势力, 剧本）。
    ///
    /// 适合做"回合收尾"：清理临时状态、结算势力级资源消耗等。
    /// </summary>
    public class TriggerForceTurnEnd : ForceTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnForceTurnEnd += OnForceTurnEnd;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnForceTurnEnd -= OnForceTurnEnd;
        }

        void OnForceTurnEnd(Force force, Scenario scenario)
        {
            if (!CheckForce(force))
                return;

            triggerCall?.Invoke(this);
        }
    }
}
