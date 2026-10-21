using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队触发器容器：triggerList 决定触发时机，actionList 内的效果在该时机依次执行。
    ///
    /// 子效果自身不订阅任何事件，单独挂在特技 / 技法上不会生效。
    /// 装配逻辑已抽到 ActionContainer，建筑 / 城市分别用 BuildingTriggerAction / CityTriggerAction。
    /// </summary>
    public class TroopTriggerAction : TroopActionBase
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
            container.Init(p, sangoObjects);
        }

        /// <summary>
        /// 清理容器（退订所有触发器 + 子效果）
        /// </summary>
        public override void Clear()
        {
            container.Clear();
        }

        /// <summary>
        /// 触发器回调：依次执行容器内的效果
        /// </summary>
        /// <param name="trigger">触发事件</param>
        public virtual void OnTrigger(Trigger trigger)
        {
            container.Dispatch(trigger);
        }
    }
}
