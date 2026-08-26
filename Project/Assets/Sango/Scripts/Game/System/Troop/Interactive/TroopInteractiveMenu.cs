using Sango.Render;
using Sango.UI;
using System.Collections.Generic;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;
namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopInteractiveMenu : GameSystem
    {
        public Troop TargetTroop { get; set; }
        public Cell TargetCell { get; set; }

        public void Start(Troop troop, Cell targetCell, Vector3 startPoint)
        {
            TargetTroop = troop;
            TargetCell = targetCell;
            ContextMenuData.MenuData.Clear();
            GameEvent.OnInteractiveContextMenuShow?.Invoke(ContextMenuData.MenuData, troop, targetCell);
            if (!ContextMenuData.MenuData.IsEmpty() && ContextMenuData.MenuData.headList.Count > 1)
            {
                ContextMenu.Show(ContextMenuData.MenuData, startPoint);
                GameSystemManager.Instance.Push(this);
            }
            else
            {
                GameSystem.GetSystem<TroopInteractiveDialog>().Start(TargetTroop, targetCell, startPoint);
            }
        }

        public override void OnEnter()
        {
           
        }

        /// <summary>
        /// 离开当前命令的时候触发
        /// </summary>
        public override void OnDestroy()
        {
            ContextMenu.CloseAll();
        }

        public override void OnExit()
        {
            ContextMenu.SetVisible(false);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            ContextMenu.SetVisible(true);
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        ContextMenu.CloseAll();
                        Back();
                        break;
                    }

                case CommandEventType.Click:
                    {
                        break;
                    }
            }
        }
    }
}
