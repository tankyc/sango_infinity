namespace Sango.Core.Player
{
    public class TroopInteractiveBase : GameSystem
    {
        public Troop TargetTroop { get; set; }
        public Cell ActionCell { get; set; }

        public string content;
        public virtual bool IsValid { get; protected set; }

        public string customMenuName;
        public int customMenuOrder;

        public override void Init()
        {
            GameEvent.OnInteractiveContextMenuShow += OnInteractiveContextMenuShow;
            GameEvent.OnTroopInteractiveContextDialogShow += OnTroopInteractiveContextDialogShow;
        }

        public override void Clear()
        {
            GameEvent.OnInteractiveContextMenuShow -= OnInteractiveContextMenuShow;
            GameEvent.OnTroopInteractiveContextDialogShow -= OnTroopInteractiveContextDialogShow;
        }

        protected virtual void OnTroopInteractiveContextDialogShow(ITroopInteractiveDialogData dialogData, Troop troop, Cell actionCell)
        {

            if (troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                TargetTroop = troop;
                ActionCell = actionCell;

                if (Check(troop, actionCell))
                {
                    dialogData.SetContent(content);
                    dialogData.SetSureAction(OnSure);
                }
            }
        }

        protected virtual void OnInteractiveContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {

            if (!string.IsNullOrEmpty(customMenuName) && troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                TargetTroop = troop;
                ActionCell = actionCell;
                if (Check(troop, actionCell))
                    menuData.Add(customMenuName, customMenuOrder, actionCell, OnClickMenuItem, IsValid);
            }
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            Start(TargetTroop, ActionCell);
        }

        protected virtual bool Check(Troop troop, Cell actionCell)
        {
            return false;
        }

        protected virtual void OnSure()
        {
            Start(TargetTroop, ActionCell);
        }

        public virtual void Start(Troop troop, Cell actionCell)
        {
            TargetTroop = troop;
            ActionCell = actionCell;
            if (troop.IsPlayerControl)
                troop.ClearMission();
            GameSystemManager.Instance.Push(this);
        }
    }
}
