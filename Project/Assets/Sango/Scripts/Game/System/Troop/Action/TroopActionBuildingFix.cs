using Sango.Render;
using Sango.UI;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionBuildingFix : TroopActionBase
    {
        public List<Cell> MovePath { get; set; }
        public List<Cell> fixBuildingCell = new List<Cell>();

        public Cell targetBuildCell;

        protected bool isShow = false;
        protected bool isMoving = false;
        public TroopActionBuildingFix()
        {
            customMenuName = "修补";
            customMenuOrder = 40;
        }

        protected override void OnTroopActionContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {
            if (troop.mBelongForce != null && troop.mBelongForce.IsPlayer && troop.mBelongForce == Scenario.Cur.CurRunForce)
            {
                // 兵器没有修复指令
                if (troop.IsMachine) return;

                TargetTroop = troop;
                ActionCell = actionCell;

                // 周围一格是可以建造的地块
                Cell stayCell = ActionCell;
                fixBuildingCell.Clear();
                for (int i = 0; i < stayCell.Neighbors.Length; i++)
                {
                    Cell n = stayCell.Neighbors[i];
                    if (n != null && n.building != null && !n.building.IsCityBase() && n.building.IsSameForce(TargetTroop) && n.building.durability < n.building.DurabilityLimit)
                    {
                        fixBuildingCell.Add(n);
                    }
                }


                if(fixBuildingCell.Count > 0)
                    menuData.Add(customMenuName, customMenuOrder, actionCell, OnClickMenuItem, IsValid);
            }
        }

        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        public override void OnEnter()
        {
            GameController.Instance.RotateViewEnabled = true;
            GameController.Instance.ZoomViewEnabled = true;
            GameController.Instance.DragMoveViewEnabled = true;
            GameController.Instance.KeyboardMoveEnabled = true;
            base.OnEnter();
            isShow = false;
            isMoving = false;
            ContextMenu.SetVisible(false);
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;
            ShowFixCells();
        }

        protected void ShowFixCells()
        {
            MapRender mapRender = MapRender.Instance;
            mapRender.SetDarkMask(true);
            if (fixBuildingCell.Count == 0) return;
            for (int i = 0, count = fixBuildingCell.Count; i < count; ++i)
            {
                Cell c = fixBuildingCell[i];
                mapRender.SetGridMaskColor(c.x, c.y, Color.red);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.black);
            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
        }

        protected void ClearShowFixCells()
        {
            MapRender mapRender = MapRender.Instance;
            mapRender.SetDarkMask(false);
            if (fixBuildingCell.Count == 0) return;
            for (int i = 0, count = fixBuildingCell.Count; i < count; ++i)
            {
                Cell c = fixBuildingCell[i];
                mapRender.SetGridMaskColor(c.x, c.y, Color.black);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.clear);

            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
        }

        public override void OnDestroy()
        {
            GameController.Instance.RotateViewEnabled = false;
            GameController.Instance.ZoomViewEnabled = false;
            GameController.Instance.DragMoveViewEnabled = false;
            GameController.Instance.KeyboardMoveEnabled = false;
            ClearShowFixCells();
            fixBuildingCell.Clear();
        }

        protected void OnMoveDone()
        {
            isMoving = false;
        }

        public override void Update()
        {
            if (isShow)
            {
                if (!isMoving)
                {
                    if (TargetTroop.BuildBuilding(targetBuildCell, null))
                    {
                        TargetTroop.ActionOver = true;
                        TargetTroop.Render?.UpdateRender();
                        Done();
                    }
                }
            }
        }
        void Action(Cell stayCell)
        {
            GameSystem.GetSystem<TroopActionMenu>().troopRender.Clear();
            ContextMenu.CloseAll();
            Cell start = TargetTroop.cell;

            if (start == stayCell)
            {
                isShow = true;
                isMoving = false;
                return;
            }

            for (int i = 1; i < MovePath.Count; i++)
            {
                bool isLast = i == MovePath.Count - 1;
                Cell dest = MovePath[i];
                TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                @event.Init(TargetTroop, start, dest, isLast, isLast ? OnMoveDone : null);
                RenderEvent.Instance.Add(@event);
                start = dest;
            }
            isShow = true;
            isMoving = true;
        }
        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            if (isShow) return;

            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        GameSystemManager.Instance.BackTo(GameSystem.GetSystem<TroopSystem>());
                        break;
                    }

                case CommandEventType.Click:
                    {
                        if (isOverUI) return;

                        if (fixBuildingCell.Contains(cell))
                        {

                            Cell stayCell = MovePath[MovePath.Count - 1];
                            targetBuildCell = cell;

//#if UNITY_ANDROID || UNITY_IPHONE
//                            ContextMenuData.MenuData.Clear();
//                            ContextMenuData.MenuData.Add("确认", 0, TargetTroop, (item)=> {
//                                Action(stayCell);
//                            });
//                            ContextMenuData.MenuData.Add("返回", 1, TargetTroop, (item) => {
//                                ContextMenu.CloseAll();
//                                GameSystemManager.Instance.BackTo(GameSystem.GetSystem<TroopSystem>());
//                            });
//                            ContextMenu.Show(ContextMenuData.MenuData, clickPosition);
//#else
//                            Action(stayCell);
//#endif
                            Action(stayCell);

                        }
                        break;
                    }
            }
        }
    }
}
