using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑触发器容器：triggerList 决定触发时机，actionList 内的效果在该时机依次执行。
    ///
    /// 与部队版（TroopTriggerAction）共用 ActionContainer，区别只是宿主对象是建筑，
    /// 因此可以配置"建筑回合开始 → 给范围内部队加 Buff"这类组合，而不必为每种建筑写死代码。
    ///
    /// 可用触发器：TriggerBuildingTurnStart / TriggerBuildingTurnEnd / TriggerBuildingComplete。
    ///
    /// 接入事件：由 triggerList 里的触发器决定；本容器自身不订阅事件。
    /// </summary>
    public class BuildingTriggerAction : BuildingActionBase
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
            if (Building == null)
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
