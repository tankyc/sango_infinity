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
            if (clickCell == null)
                return;

            if (clickCell.building != null)
            {
                BuildingBase building = clickCell.building;
                if (building.IsCityBase()) return;

                // 【空引用】BelongForce 对无主建筑 / 陷阱 / 障碍恒为 null：
                // 它在 OnScenarioPrepare 里只在 BelongForceId > 0 时才解析（BuildingBase.cs:152-157），
                // 陷阱与障碍读档时还会被强制纠正成"建成即中立"（BuildingBase.cs:181-183）。
                // 而 Scenario.CurRunForce 在一个势力的回合结束后会被置空（Scenario.cs:1972），
                // 轮到下一家之前它一直是 null。
                // 两者同时为 null 时 `==` 返回 true，接着解引用 null.IsPlayer 就抛 NullReferenceException
                // （实战：AI 轮次间隙点地图上的无主设施）。
                // 用 BuildingBase.IsPlayer（BuildingBase.cs:113，内部 BelongForce?.IsPlayer ?? false）先挡一道，
                // 既短路掉比较，也不会再对 null 解引用。
                if (building.IsPlayer && building.BelongForce == Scenario.Cur.CurRunForce)
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
                // 【空引用】内政地块的 BelongCity 可能为 null：地图/剧本数据里该格的
                // 内政归属还没建立（自制地图、剧本编辑器直接摆放、或读档来自缺该归属的版本）。
                // 原来直接 clickCell.BelongCity.BelongCity 会抛 NullReferenceException
                //（实测栈：本方法第 50 行，点地图空地就崩）。
                City cellCity = clickCell.BelongCity;
                if (cellCity == null)
                    return;

                Force cellBelongForce = cellCity.BelongCity != null ? cellCity.BelongCity.BelongForce : cellCity.BelongForce;
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
