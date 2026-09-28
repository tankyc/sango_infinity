using Sango.Render;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionReturn : TroopActionBase
    {
        public TroopActionReturn()
        {
            customMenuName = "取消";
            customMenuOrder = 99999;
        }

        public override void Init()
        {
            base.Init();
            GameEvent.OnTroopContextMenuShow += OnTroopContextMenuShow;
        }

        public override void Clear()
        {
            base.Clear();
            GameEvent.OnTroopContextMenuShow -= OnTroopContextMenuShow;
        }

        protected virtual void OnTroopContextMenuShow(IContextMenuData menuData, Troop troop)
        {
            TargetTroop = troop;
            //if(!troop.ActionOver)
            //    menuData.Add(customMenuName, customMenuOrder, troop, OnClickMenuItem, true);
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
#if UNITY_ANDROID || UNITY_IPHONE
                menuData.Add(customMenuName, customMenuOrder, actionCell, OnClickMenuItem, IsValid);
#endif
            }
        }

        protected override void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            if (!GameSystemManager.Instance.BackTo(GameSystem.GetSystem<CityExpedition>()))
            {
                if (!GameSystemManager.Instance.BackTo(GameSystem.GetSystem<CityTransport>()))
                {
                    GameSystemManager.Instance.Done();
                }
            }
        }
    }
}
