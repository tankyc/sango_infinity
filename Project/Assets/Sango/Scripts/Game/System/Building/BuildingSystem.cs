using Sango.UI;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    /// <summary>
    /// 建筑系统菜单
    /// </summary>
    [GameSystem]
    public class BuildingSystem : GameSystem
    {
        public override void Init()
        {
            GameEvent.OnClick += OnClick;
        }

        void OnClick(Cell clickCell, Vector3 clickPosition, bool isOverUI)
        {

            if (clickCell.building != null)
            {
                BuildingBase building = clickCell.building;
                if (building.IsCityBase()) return;
                if (building.BelongForce == Scenario.Cur.CurRunForce && building.BelongForce.IsPlayer)
                {
                    ContextMenuData.MenuData.Clear();
                    GameEvent.OnBuildingContextMenuShow?.Invoke(ContextMenuData.MenuData, building);
                    if (!ContextMenuData.MenuData.IsEmpty())
                    {
                        TargetBuilding = building;
                        ContextMenu.Show(ContextMenuData.MenuData, clickPosition);
                        Enter();
                    }
                }
            }
            // 点击内政地块-弹出开发按钮-点击开发按钮弹出开发界面
            else if (clickCell.IsInterior && clickCell.IsEmpty())
            {
                Force cellBelongForce = clickCell.BelongCity.BelongCity != null ? clickCell.BelongCity.BelongCity.BelongForce : clickCell.BelongCity.BelongForce;
                if (cellBelongForce != null && cellBelongForce == Scenario.Cur.CurRunForce && cellBelongForce.IsPlayer)
                {
                    ContextMenuData.MenuData.Clear();
                    GameEvent.OnCellContextMenuShow?.Invoke(ContextMenuData.MenuData, clickCell);
                    if (!ContextMenuData.MenuData.IsEmpty())
                    {
                        ContextMenu.Show(ContextMenuData.MenuData, clickPosition);
                        Enter();
                    }
                }
            }
        }

        public BuildingBase TargetBuilding { get; set; }

        public override void OnEnter()
        {
            base.OnEnter();
            TargetBuilding?.Render?.SetFlash(true);
        }

        public override void OnDestroy()
        {
            TargetBuilding?.Render?.SetFlash(false);
            ContextMenu.CloseAll();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClickDown:
                    {
                        if (ContextMenu.Close())
                            Exit();
                        break;
                    }
                case CommandEventType.ClickDown:
                    {
                        if (isOverUI) return;

                        Done();
                        break;
                    }
            }
        }
    }
}
