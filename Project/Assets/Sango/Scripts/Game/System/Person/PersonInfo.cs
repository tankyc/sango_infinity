using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class PersonInfo : GameSystem
    {
        public PersonInfo() {
            
        }

        //public override void Init()
        //{
        //    GameEvent.OnRightMouseButtonContextMenuShow += OnRightMouseButtonContextMenuShow;
        //}

        //public override void Clear()
        //{
        //    GameEvent.OnRightMouseButtonContextMenuShow -= OnRightMouseButtonContextMenuShow;
        //}

        //protected virtual void OnRightMouseButtonContextMenuShow(IContextMenuData menuData)
        //{
        //    menuData.Add("结束回合", 0, null, OnClickMenuItem, true);
        //}

        //protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        //{
        //    ContextMenu.CloseAll();
        //    GameSystemManager.Instance.Push(this);
        //}

        //public override void OnEnter()
        //{
        //    GameDialog.Open("是否需要结束玩家回合", () =>
        //    {
        //        Scenario.Cur.CurRunForce.CurRunCorps.ActionOver = true;
        //        GameDialog.Close();
        //        Done();
        //    }).cancelAction = ()=>
        //    {
        //        GameDialog.Close();
        //        Done();
        //    };
        //}

        //public override void OnDestroy()
        //{
        //    GameDialog.Close();
        //}

        //public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        //{
        //    switch (eventType)
        //    {
        //        case CommandEventType.Cancel:
        //        case CommandEventType.RClick:
        //            Back(); break;
        //    }

        //}
    }
}
