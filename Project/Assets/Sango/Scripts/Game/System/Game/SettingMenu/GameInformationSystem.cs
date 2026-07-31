using Sango.UI;
using UnityEngine;

namespace Sango.Core.Player
{
    /// <summary>
    /// 情报系统 - 管理游戏中的情报菜单显示和生命周期
    /// 当玩家点击情报按钮时，收集所有订阅OnGameInformationContextMenuShow事件的菜单项并显示
    /// </summary>
    [GameSystem]
    public class GameInformationSystem : GameSystem
    {
        /// <summary>
        /// 启动情报菜单 - 收集菜单数据并显示上下文菜单
        /// </summary>
        /// <param name="startPoint">菜单显示的起始屏幕坐标</param>
        public void Start(Vector3 startPoint)
        {
            ContextMenuData.MenuData.Clear();
            GameEvent.OnGameInformationContextMenuShow?.Invoke(ContextMenuData.MenuData);

            if (!ContextMenuData.MenuData.IsEmpty())
            {
                UI.ContextMenu.Show(ContextMenuData.MenuData, startPoint, ContextMenuType.System);
                GameSystemManager.Instance.Push(this);
            }
        }

        /// <summary>
        /// 当子命令返回时重新显示情报菜单
        /// </summary>
        /// <param name="whoGone">返回的命令</param>
        public override void OnBack(ICommandEvent whoGone)
        {
            UI.ContextMenu.SetVisible(true);
        }

        /// <summary>
        /// 离开当前命令时关闭所有上下文菜单
        /// </summary>
        public override void OnDestroy()
        {
            UI.ContextMenu.CloseAll();
        }

        /// <summary>
        /// 处理输入事件 - 取消/右键关闭菜单，左键点击空白区域退出
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
                case CommandEventType.RClickDown:
                    {
                        if (UI.ContextMenu.Close())
                            GameSystemManager.Instance.Back();

                        break;
                    }

                case CommandEventType.ClickDown:
                    {
                        if (isOverUI)
                        {
                            if (!UI.ContextMenu.IsOverUI(clickPosition))
                                Done();
                            return;
                        }

                        Done();
                        break;
                    }
            }
        }
    }
}
