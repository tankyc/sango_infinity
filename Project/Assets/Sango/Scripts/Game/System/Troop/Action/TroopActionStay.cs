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
            GameSystem.GetSystem<TroopActionMenu>().troopRender?.Clear();
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;

            // 【出城兜底】部队不允许停留在城池 / 关隘 / 港口等建筑格上（统一规则，推广自补给队）。
            // 玩家部队出征创建后直接"待命"时仍站在城头上，此时本回合只做一件事：
            // 移动到移动范围内最近的可停留空地，而不是原地待命（那会让部队永远滞留城头）。
            if (TargetTroop != null && TroopAIUtility.IsOnBuildingCell(TargetTroop))
            {
                Cell dest = TroopAIUtility.FindLeaveBuildingCell(TargetTroop, Scenario.Cur);
                if (dest != null)
                {
                    List<Cell> path = new List<Cell>();
                    Scenario.Cur.Map.GetMovePath(TargetTroop, dest, path);

                    Cell leaveStart = TargetTroop.cell;
                    for (int i = 1; i < path.Count; i++)
                    {
                        bool isLast = i == path.Count - 1;
                        Cell d = path[i];
                        TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                        @event.Init(TargetTroop, leaveStart, d, isLast, isLast ? OnMoveDone : null);
                        RenderEvent.Instance.Add(@event);
                        leaveStart = d;
                    }
                    return;
                }
                // 被围死无处可去：退回原有待命流程（原地结束行动）
            }

            if (MovePath.Count <= 1)
            {
                OnMoveDone();
                return;
            }

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 2656 : 2670);

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
