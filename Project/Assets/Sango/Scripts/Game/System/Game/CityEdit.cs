using Sango.Core.Action;
using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城市编辑系统 - 打开城市编辑窗口(window_edit_city)
    /// 用于修改城市的各项属性和数据,使用快照模式,确认时才写入数据
    /// </summary>
    [GameSystem]
    public class CityEdit : GameSystem
    {
        /// <summary>
        /// 目标城市
        /// </summary>
        public City Target;

        /// <summary>
        /// 全部城市对象列表
        /// </summary>
        public List<SangoObject> all_objects = new List<SangoObject>();

        /// <summary>
        /// 窗口名称 - 城市编辑窗口
        /// </summary>
        protected string windowName = "window_edit_city";

        /// <summary>
        /// 启动城市编辑 - 传入目标城市并进入编辑状态
        /// </summary>
        /// <param name="target">需要编辑的城市</param>
        public void Start(City target)
        {
            Target = target;
            Push();
        }

        /// <summary>
        /// 启动城市编辑 - 传入目标城市与自定义城市列表
        /// </summary>
        /// <param name="target">需要编辑的城市</param>
        /// <param name="city_list">城市列表</param>
        public void Start(City target, List<SangoObject> city_list)
        {
            Target = target;
            all_objects = city_list ?? new List<SangoObject>();
            Push();
        }

        /// <summary>
        /// 初始化 - 注册城市右键菜单中的编辑菜单项
        /// </summary>
        public override void Init()
        {
            Name = "城市编辑";
            Clear();
            GameEvent.OnCityRightMouseButtonContextMenuShow += OnCityRightMouseButtonContextMenuShow;
        }

        /// <summary>
        /// 清理 - 取消注册菜单项事件
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityRightMouseButtonContextMenuShow -= OnCityRightMouseButtonContextMenuShow;
        }

        /// <summary>
        /// 城市右键菜单是否可以显示
        /// </summary>
        protected virtual bool CityMenuCanShow()
        {
            return Target != null && Target.IsCity();
        }

        /// <summary>
        /// 城市右键菜单显示时注册城市编辑菜单项
        /// </summary>
        /// <param name="menuData">上下文菜单数据</param>
        /// <param name="city">当前右键的城市</param>
        protected virtual void OnCityRightMouseButtonContextMenuShow(IContextMenuData menuData, City city)
        {
            Target = city;
            if (CityMenuCanShow())
                menuData.Add("城市编辑", 100, null, OnClickMenuItem, true);
        }

        /// <summary>
        /// 点击菜单项 - 关闭上下文菜单并推入当前系统
        /// </summary>
        /// <param name="contextMenuItem">上下文菜单项</param>
        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            Push();
        }

        /// <summary>
        /// 进入编辑状态 - 打开城市编辑窗口
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
        /// 退出编辑状态 - 隐藏编辑窗口
        /// </summary>
        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        /// <summary>
        /// 销毁编辑系统 - 关闭编辑窗口
        /// </summary>
        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        /// <summary>
        /// 处理输入事件 - 取消/右键返回上一级
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="cell">点击的格子</param>
        /// <param name="clickPosition">点击位置</param>
        /// <param name="isOverUI">是否在UI上方</param>
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        Back();
                        break;
                    }
            }
        }
    }
}
