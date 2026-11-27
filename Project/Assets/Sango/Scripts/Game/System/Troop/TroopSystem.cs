using Sango.Render;
using Sango.UI;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopSystem : GameSystem
    {
        public override void Init()
        {
            GameEvent.OnClick += OnClick;
        }

        void OnClick(Cell clickCell, Vector3 clickPosition, bool isOverUI)
        {
            if (clickCell.troop == null) return;

            Troop troop = clickCell.troop;
            if (!troop.IsAlive) return;
            TargetTroop = troop;
            if (!troop.ActionOver && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                ContextMenuData.MenuData.Clear();
                GameEvent.OnTroopContextMenuShow?.Invoke(ContextMenuData.MenuData, troop);
                if (!ContextMenuData.MenuData.IsEmpty())
                {
                    UI.ContextMenu.Show(ContextMenuData.MenuData, clickPosition);
                }
                Enter();
            }
            else
            {
                GameSystem.GetSystem<TroopMenu>().Start(troop, clickPosition);
            }
        }

        public List<Cell> movePath = new List<Cell>();

        /// <summary>
        /// 已经涂过色的移动范围格。
        ///
        /// 【为什么不能直接用 TargetTroop.MoveRange 清】
        /// "行动完再移动"时 <see cref="Troop.ActionOver"/> 会先执行 MoveRange.Clear()，
        /// 之后 TroopActionMove 才重建 MoveRange；旧实现 ClearShowMoveRange 里
        /// `if (TargetTroop.MoveRange.Count == 0) return;` 会直接跳过，
        /// 旧范围的绿色就永远留在格子上（再移动后新旧范围叠加）。
        /// 这里单独记录"实际涂过哪些格"，清色只按这份快照。
        /// </summary>
        readonly List<Cell> shownMoveRange = new List<Cell>(256);

        /// <summary>已经涂过色的移动路径格（movePath 会被行动体清空/重建，同样不能拿它当清色依据）</summary>
        readonly List<Cell> shownMovePath = new List<Cell>(64);

        public Troop TargetTroop { get; set; }

        public void Start(Troop troop)
        {
            Start(troop, Vector3.zero);
        }

        public void Start(Troop troop, Vector3 startPoint)
        {
            TargetTroop = troop;
            ContextMenuData.MenuData.Clear();
            GameEvent.OnTroopContextMenuShow?.Invoke(ContextMenuData.MenuData, troop);
            if (!ContextMenuData.MenuData.IsEmpty())
            {
                UI.ContextMenu.Show(ContextMenuData.MenuData, startPoint);
            }
            Enter();
        }

        public override void OnEnter()
        {
            if (TargetTroop.Leader.IsGovernor)
                GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, 3160);
            else
                GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3132 : 3146);

            TargetTroop.Render?.SetFlash(true);

            TargetTroop.MoveRange.Clear();
            Scenario.Cur.Map.GetMoveRange(TargetTroop, TargetTroop.MoveRange);
            OnBack(null);

        }

        public override void OnExit()
        {
            GameController.Instance.RotateViewEnabled = false;
            GameController.Instance.ZoomViewEnabled = false;
            GameController.Instance.KeyboardMoveEnabled = false;
            GameController.Instance.DragMoveViewEnabled = false;
            GameController.Instance.BorderMoveViewEnabled = false;
            ClearShowMoveRange();
            ShowMovePath();
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            GameController.Instance.RotateViewEnabled = true;
            GameController.Instance.ZoomViewEnabled = true;
            GameController.Instance.KeyboardMoveEnabled = true;
            GameController.Instance.DragMoveViewEnabled = true;
            GameController.Instance.BorderMoveViewEnabled = true;
            ShowMoveRange();
        }

        /// <summary>
        /// 离开当前命令的时候触发
        /// </summary>
        public override void OnDestroy()
        {
            TargetTroop.Render?.SetFlash(false);

            // 这个一定先清理
            ClearShowMovePath();
            ClearShowMoveRange();
            movePath.Clear();
        }

        public void ShowMoveRange()
        {
            // 重画前先把上一份快照清掉：OnBack / 再次移动 / 对话框进入都会重复调用 ShowMoveRange
            ClearShowMoveRange();

            MapRender mapRender = MapRender.Instance;
            for (int i = 0, count = TargetTroop.MoveRange.Count; i < count; ++i)
            {
                Cell cell = TargetTroop.MoveRange[i];
                mapRender.SetGridMaskColor(cell.x, cell.y, Color.green);
                shownMoveRange.Add(cell);
            }
            mapRender.EndSetGridMask();
        }

        public void ClearShowMoveRange()
        {
            if (shownMoveRange.Count == 0) return;
            MapRender mapRender = MapRender.Instance;
            for (int i = 0, count = shownMoveRange.Count; i < count; ++i)
            {
                Cell cell = shownMoveRange[i];
                mapRender.SetGridMaskColor(cell.x, cell.y, Color.black);
            }
            mapRender.EndSetGridMask();
            shownMoveRange.Clear();
        }
        public void ShowMovePath()
        {
            // 与 ShowMoveRange 同理：movePath 会被 Clear / 重算，清色必须按上次实际涂过的快照
            ClearShowMovePath();

            MapRender mapRender = MapRender.Instance;
            for (int i = 0, count = movePath.Count; i < count; ++i)
            {
                Cell c = movePath[i];
                mapRender.SetGridMaskColor(c.x, c.y, Color.blue);
                shownMovePath.Add(c);
            }
            mapRender.EndSetGridMask();
        }

        public void ClearShowMovePath()
        {
            if (shownMovePath.Count == 0) return;
            MapRender mapRender = MapRender.Instance;
            for (int i = 0, count = shownMovePath.Count; i < count; ++i)
            {
                Cell c = shownMovePath[i];
                mapRender.SetGridMaskColor(c.x, c.y, Color.black);
            }
            mapRender.EndSetGridMask();
            shownMovePath.Clear();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        Back();
                        break;
                    }

                case CommandEventType.Click:
                    {
                        if (isOverUI) return;

                        if (!TargetTroop.MoveRange.Contains(cell))
                        {
                            movePath.Clear();
                            Scenario.Cur.Map.GetMovePath(TargetTroop, cell, movePath);
                            ShowMovePath();
                            GameSystem.GetSystem<TroopInteractiveMenu>().Start(TargetTroop, cell, clickPosition);
                            return;
                        }

                        // 排除出征城市,出征城市无法原地进行行为
                        if ((cell == TargetTroop.cell && cell.building == null) || cell.IsEmpty())
                        {
                            movePath.Clear();
                            Scenario.Cur.Map.GetMovePath(TargetTroop, cell, movePath);
                            GameSystem.GetSystem<TroopActionMenu>().Start(TargetTroop, cell, clickPosition);
                            return;
                        }

                        // 排除出征城市
                        if (TargetTroop.cell.building == TargetTroop.BelongCity && cell.building == TargetTroop.BelongCity)
                            return;

                        movePath.Clear();
                        Scenario.Cur.Map.GetMovePath(TargetTroop, cell, movePath);
                        // 进入
                        GameSystem.GetSystem<TroopInteractiveDialog>().Start(TargetTroop, cell, clickPosition);


                        //if (cell.building != null)
                        //{
                        //    if(cell.building.IsCityBase())
                        //    {
                        //        if(cell.building.IsSameForce(TargetTroop))
                        //        {
                        //                 }
                        //        else
                        //        {
                        //            // 委任攻击
                        //        }
                        //    }
                        //    else
                        //    {
                        //        if (cell.building.IsSameForce(TargetTroop) && !cell.building.isComplte)
                        //        {
                        //            // 修建

                        //            return;
                        //        }
                        //        else if(cell.building.IsEnemy(TargetTroop))
                        //        {
                        //            // 委任攻击
                        //        }
                        //        else
                        //        {
                        //            // 接近
                        //        }
                        //    }
                        //    return;
                        //}

                        //if (cell.troop != null)
                        //{
                        //    if (cell.building.IsEnemy(TargetTroop))
                        //    {
                        //        // 歼灭 或者 驱逐
                        //    }
                        //    else
                        //    {
                        //        // 接近
                        //    }
                        //}

                        break;
                    }
            }
        }
    }
}
