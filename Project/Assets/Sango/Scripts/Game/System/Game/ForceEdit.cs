using Sango.Core.Player;
using Sango.UI;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 势力编辑系统 - 打开势力编辑窗口(window_edit_force)
    /// 用于编辑势力属性: 势力颜色、势力城市
    /// 关联窗口: window_edit_force
    /// </summary>
    [GameSystem]
    public class ForceEdit : GameSystem
    {
        /// <summary>
        /// 目标势力
        /// </summary>
        public Force Target { get; private set; }

        /// <summary>
        /// 窗口名称 - 势力编辑窗口
        /// </summary>
        protected string windowName = "window_edit_force";

        /// <summary>
        /// 启动势力编辑 - 传入目标势力并进入编辑状态
        /// </summary>
        /// <param name="target">需要编辑的势力</param>
        public void Start(Force target)
        {
            Target = target;
            Push();
        }

        /// <summary>
        /// 初始化系统
        /// </summary>
        public override void Init()
        {
            Name = "势力编辑";
        }

        /// <summary>
        /// 进入编辑状态 - 打开势力编辑窗口
        /// </summary>
        public override void OnEnter()
        {
            Window.Instance.Open(windowName, Target);
        }

        /// <summary>
        /// 子命令返回时恢复窗口可见
        /// </summary>
        /// <param name="whoGone">返回的命令</param>
        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.SetVisible(windowName, true);
        }

        /// <summary>
        /// 退出编辑状态 - 隐藏势力编辑窗口
        /// </summary>
        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        /// <summary>
        /// 销毁系统 - 关闭势力编辑窗口
        /// </summary>
        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        /// <summary>
        /// 处理输入事件 - 取消/右键返回
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="cell">格子</param>
        /// <param name="clickPosition">点击位置</param>
        /// <param name="isOverUI">是否在UI上</param>
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            if (eventType == CommandEventType.Cancel || eventType == CommandEventType.RClick)
            {
                Back();
            }
        }

        /// <summary>
        /// 获取当前编辑的剧本
        /// </summary>
        /// <returns>剧本编辑系统中的剧本</returns>
        private Scenario GetScenario()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            return edit != null ? edit.Scenario : null;
        }

        /// <summary>
        /// 设置势力的旗帜颜色
        /// </summary>
        /// <param name="flag">新的旗帜</param>
        public void SetFlag(Flag flag)
        {
            if (Target == null || flag == null)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                edit.SetForceFlag(Target, flag);
            }
        }

        /// <summary>
        /// 将无势力的城市分配给目标势力(归入主军团)
        /// </summary>
        /// <param name="city">要分配的城市</param>
        public void AssignCity(City city)
        {
            if (Target == null || city == null)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                edit.AssignCityToForce(Target, city);
            }
        }

        /// <summary>
        /// 将城市从目标势力中移除(去势力化)
        /// </summary>
        /// <param name="city">要移除的城市</param>
        public void RemoveCity(City city)
        {
            if (Target == null || city == null)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                edit.RemoveCityFromForce(Target, city);
            }
        }

        /// <summary>
        /// 获取目标势力的主军团
        /// </summary>
        /// <returns>主军团</returns>
        public Corps GetMainCorps()
        {
            if (Target == null)
            {
                return null;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            return edit != null ? edit.GetMainCorps(Target) : null;
        }
    }
}
