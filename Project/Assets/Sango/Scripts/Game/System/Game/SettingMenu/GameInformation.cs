using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class GameInformation : GameSystem
    {

        public override void Init()
        {
            //GameEvent.OnRightMouseButtonContextMenuShow += OnRightMouseButtonContextMenuShow;
        }

        public override void Clear()
        {
            //GameEvent.OnRightMouseButtonContextMenuShow -= OnRightMouseButtonContextMenuShow;
        }

        protected virtual void OnRightMouseButtonContextMenuShow(IContextMenuData menuData)
        {
            menuData.Add("情报", 20, null, OnClickMenuItem, true);
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            //GameSystemManager.Instance.Push(this);
        }

        public override void OnEnter()
        {

        }

        public override void OnDestroy()
        {

        }
        
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

        }
    }
}
