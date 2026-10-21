namespace Sango.Core
{
    /// <summary>
    /// 势力回合开始：接入 GameEvent.OnForceTurnStart（势力, 剧本）。
    ///
    /// 适合做"每回合一次的势力级结算"：发放资源、刷新全局状态等。
    /// </summary>
    public class TriggerForceTurnStart : ForceTrigger
    {
        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnForceTurnStart += OnForceTurnStart;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnForceTurnStart -= OnForceTurnStart;
        }

        void OnForceTurnStart(Force force, Scenario scenario)
        {
            if (!CheckForce(force))
                return;

            triggerCall?.Invoke(this);
        }
    }
}
