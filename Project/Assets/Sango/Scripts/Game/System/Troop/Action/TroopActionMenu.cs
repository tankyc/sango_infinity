using Sango.Render;
using Sango.UI;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;
namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionMenu : GameSystem
    {
        public List<Cell> MovePath;

        public List<Cell> spellRangeCell = new List<Cell>();
        public Troop TargetTroop { get; set; }
        public Cell TargetCell { get; set; }

        public TroopRender troopRender;

        public void Start(Troop troop, Cell targetCell, Vector3 startPoint)
        {
            TargetTroop = troop;
            TargetCell = targetCell;
            ContextMenuData.MenuData.Clear();
            GameEvent.OnTroopActionContextMenuShow?.Invoke(ContextMenuData.MenuData, troop, targetCell);
            if (!ContextMenuData.MenuData.IsEmpty())
            {
                ContextMenu.Show(ContextMenuData.MenuData, startPoint);
                GameSystemManager.Instance.Push(this);
            }
        }

        public override void OnEnter()
        {
            troopRender = new TroopRender(TargetTroop, false);
            troopRender.SetPosition(TargetCell.Position);
            troopRender.SetForward(TargetTroop.Render.GetForward());
            troopRender.UpdateModelByCell(TargetCell);

            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;
            List<SkillInstance> list;
            if (TargetCell.TerrainType.isWater)
                list = TargetTroop.waterSkills;
            else
                list = TargetTroop.landSkills;
            for (int i = 0, count = list.Count; i < count; ++i)
            {
                SkillInstance skill = list[i];
                if (skill.CanBeSpell(TargetTroop))
                    skill.GetSpellRange(TargetTroop, TargetCell, spellRangeCell);
            }
            ShowSpellRange();
        }

        /// <summary>
        /// 离开当前命令的时候触发
        /// </summary>
        public override void OnDestroy()
        {
            if (troopRender != null)
            {
                troopRender.Clear();
                troopRender = null;
            }
            ClearShowSpellRange();
            spellRangeCell.Clear();
            ContextMenu.CloseAll();
        }

        public override void OnExit()
        {
            ClearShowSpellRange();
            ContextMenu.SetVisible(false);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            ShowSpellRange();
            ContextMenu.SetVisible(true);
        }

        public void ShowSpellRange()
        {
            MapRender mapRender = MapRender.Instance;
            for (int i = 0, count = spellRangeCell.Count; i < count; ++i)
            {
                Cell c = spellRangeCell[i];
                if (!MovePath.Contains(c))
                    mapRender.SetGridMaskColor(c.x, c.y, Color.red);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.black);
            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
            mapRender.SetDarkMask(true);
        }

        public void ClearShowSpellRange()
        {
            MapRender mapRender = MapRender.Instance;
            mapRender.SetDarkMask(false);
            if (spellRangeCell.Count == 0) return;
            for (int i = 0, count = spellRangeCell.Count; i < count; ++i)
            {
                Cell c = spellRangeCell[i];
                if (!MovePath.Contains(c))
                    mapRender.SetGridMaskColor(c.x, c.y, Color.black);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.clear);
            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        ContextMenu.CloseAll();
                        GameSystemManager.Instance.Back();
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
