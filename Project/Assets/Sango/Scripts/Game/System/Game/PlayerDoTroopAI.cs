using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class PlayerDoTroopAI : GameSystem
    {

        public override void Init()
        {
            GameEvent.OnRightMouseButtonContextMenuShow += OnRightMouseButtonContextMenuShow;
        }

        public override void Clear()
        {
            GameEvent.OnRightMouseButtonContextMenuShow -= OnRightMouseButtonContextMenuShow;
        }

        bool IsValid {
            get
            {
                Force force = Scenario.Cur.CurRunForce;
                Scenario scenario = Scenario.Cur;
                for (int i = 0; i < scenario.troopsSet.Count; ++i)
                {
                    var c = scenario.troopsSet[i];
                    if (c != null && c.IsAlive && c.mBelongForce == force && !c.ActionOver && c.missionType > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
        
        }

        protected virtual void OnRightMouseButtonContextMenuShow(IContextMenuData menuData)
        {
            menuData.Add("委任部队行动", 10, null, OnClickMenuItem, IsValid);
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            GameSystemManager.Instance.Push(this);
        }

        public override void OnEnter()
        {
            
        }

        public override void OnDestroy()
        {

        }

        public override void Update()
        {
            base.Update();
            Force force = Scenario.Cur.CurRunForce;
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.mBelongForce == force && !c.ActionOver && c.missionType > 0)
                {
                    if (!c.DoAI(scenario))
                        return;
                    c.Render?.UpdateRender();
                }
            }
            Done();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

        }
    }
}
