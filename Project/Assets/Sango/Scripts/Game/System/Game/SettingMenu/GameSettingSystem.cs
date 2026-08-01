using Sango.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    [GameSystem]

    public class GameSettingSystem : GameSystem
    {
        public void Start(Vector3 startPoint)
        {
            ContextMenuData.MenuData.Clear();
            GameEvent.OnGameSettingContextMenuShow?.Invoke(ContextMenuData.MenuData);
            if (!ContextMenuData.MenuData.IsEmpty())
            {
                ContextMenu.Show(ContextMenuData.MenuData, startPoint, ContextMenuType.System);
                GameSystemManager.Instance.Push(this);
            }
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            ContextMenu.SetVisible(true);
        }


        /// <summary>
        /// 离开当前命令的时候触发
        /// </summary>
        public override void OnDestroy()
        {

            ContextMenu.CloseAll();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClickDown:
                    {
                        if (ContextMenu.Close())
                            GameSystemManager.Instance.Back();

                        break;
                    }

                case CommandEventType.ClickDown:
                    {
                        if (isOverUI)
                        {
                            //if (!ContextMenu.IsOverUI(clickPosition))
                            //{
                            //    Sango.Log.Error(111);
                            //    Done();
                            //}
                            return;
                        }

                        Done();
                        break;
                    }
            }
        }
    }
}
