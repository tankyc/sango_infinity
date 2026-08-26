using Sango.UI;

namespace Sango.Core.Player
{
    public class GameSettingMenuBase : GameSystem
    {
        public string customMenuName;
        public int customMenuOrder;
        public string windowName;

        public override void Init()
        {
            GameEvent.OnGameSettingContextMenuShow += OnGameSettingContextMenuShow;
        }

        public override void Clear()
        {
            GameEvent.OnGameSettingContextMenuShow -= OnGameSettingContextMenuShow;
        }

        protected virtual void OnGameSettingContextMenuShow(IContextMenuData menuData)
        {
             menuData.Add(customMenuName, customMenuOrder, null, OnClickMenuItem);
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            GameSystemManager.Instance.Push(this);
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.RClick:
                    Back(); break;
            }

            base.HandleEvent(eventType, cell, clickPosition, isOverUI);
        }
    }
}
