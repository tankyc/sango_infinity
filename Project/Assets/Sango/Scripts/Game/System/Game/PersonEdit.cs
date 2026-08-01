using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 武将编辑系统 - 打开武将编辑窗口(window_edit_person)
    /// 用于修改武将的各项属性和数据
    /// </summary>
    [GameSystem]
    public class PersonEdit : GameSystem
    {
        /// <summary>
        /// 目标武将
        /// </summary>
        public Person Target;

        public List<SangoObject> all_objects = new List<SangoObject>();

        /// <summary>
        /// 窗口名称 - 武将编辑窗口
        /// </summary>
        protected string windowName = "window_edit_person";

        /// <summary>
        /// 启动武将编辑 - 传入目标武将并进入编辑状态
        /// </summary>
        /// <param name="target">需要编辑的武将</param>
        public void Start(Person target)
        {
            Target = target;
            Push();
        }

        /// <summary>
        /// 初始化 - 注册情报菜单中的菜单项
        /// </summary>
        public override void Init()
        {
            Name = "武将编辑";
            GameEvent.OnGameInformationContextMenuShow += OnGameInformationContextMenuShow;
        }

        /// <summary>
        /// 清理 - 取消注册菜单项事件
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnGameInformationContextMenuShow -= OnGameInformationContextMenuShow;
        }

        /// <summary>
        /// 情报菜单显示时注册武将编辑菜单项
        /// </summary>
        /// <param name="menuData">上下文菜单数据</param>
        protected virtual void OnGameInformationContextMenuShow(IContextMenuData menuData)
        {
            menuData.Add(Name, 400, null, OnClickMenuItem);
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
        /// 进入编辑状态 - 打开武将编辑窗口
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
                        GameSystemManager.Instance.Back();
                        break;
                    }
            }
        }
    }
}
