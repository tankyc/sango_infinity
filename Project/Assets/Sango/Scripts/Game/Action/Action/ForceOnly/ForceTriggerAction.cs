using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 势力触发器容器：triggerList 决定触发时机，actionList 内的效果在该时机依次执行。
    ///
    /// 与部队 / 建筑 / 城市版共用 ActionContainer，宿主是势力（技法装配）。
    ///
    /// 可用触发器：TriggerForceTurnStart / TriggerForceTurnEnd。
    ///
    /// 接入事件：由 triggerList 里的触发器决定；本容器自身不订阅事件。
    /// </summary>
    public class ForceTriggerAction : ForceActionBase
    {
        /// <summary>容器内容</summary>
        readonly ActionContainer container = new ActionContainer();

        /// <summary>
        /// 初始化容器
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            if (Force == null)
                return;

            container.Init(p, sangoObjects);
        }

        /// <summary>
        /// 清理容器（退订所有触发器 + 子效果）
        /// </summary>
        public override void Clear()
        {
            container.Clear();
        }
    }
}
