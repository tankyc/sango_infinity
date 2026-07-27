using Sango.Render;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionStay : TroopActionBase
    {
        List<Cell> MovePath { get; set; }

        public TroopActionStay()
        {
            customMenuName = "待命";
            customMenuOrder = 0;
        }

        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        protected override void OnTroopActionContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {
            if (troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                TargetTroop = troop;
                ActionCell = actionCell;
                //if (actionCell.building != null && actionCell.building.IsSameForce(troop) && actionCell.building.IsCityBase())
                //    menuData.Add("进入", customMenuOrder, actionCell, OnClickMenuItem, IsValid);
                //else
                    menuData.Add("待命", customMenuOrder, actionCell, OnClickMenuItem, IsValid);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            ContextMenu.CloseAll();
            GameSystem.GetSystem<TroopActionMenu>().ShowSpellRange();
            GameSystem.GetSystem<TroopActionMenu>().troopRender.Clear();
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;

            if (MovePath.Count <= 1)
            {
                OnMoveDone();
                return;
            }

            Cell start = TargetTroop.cell;
            for (int i = 1; i < MovePath.Count; i++)
            {
                bool isLast = i == MovePath.Count - 1;
                Cell dest = MovePath[i];
                TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                @event.Init(TargetTroop, start, dest, isLast, isLast ? OnMoveDone : null);
                RenderEvent.Instance.Add(@event);
                start = dest;
            }

        }

        public override void OnDestroy()
        {
            ContextMenu.CloseAll();
        }

        public void OnMoveDone()
        {
            TargetTroop.ActionOver = true;
            TargetTroop.Render?.UpdateRender();

            //if (ActionCell.building != null && ActionCell.building.IsSameForce(TargetTroop) && ActionCell.building.IsCityBase())
            //    TargetTroop.EnterCity(ActionCell.building as City);

            Done();
        }
    }
}
