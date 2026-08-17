using Sango.Render;
using Sango.UI;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionSupply : TroopActionBase
    {
        protected List<Cell> MovePath { get; set; }
        protected List<Cell> spellRangeCell = new List<Cell>();
        protected Cell spellCell;
        protected SkillInstance spellSkill;
        protected bool isShow = false;
        protected bool isMoving = false;
        protected string iconRes;
        protected List<GameObject> spellIconList = new List<GameObject>();

        Troop supplyTarget;
        ItemStore itemStore;
        int food;
        int gold;
        int troops;

        public TroopActionSupply()
        {
            iconRes = "Assets/UI/Prefab/worldIcon_3.prefab"; 
            customMenuName = "补给";
            customMenuOrder = 15;
        }

        public override bool IsValid
        {
            get
            {
                bool hasTarget = false;
                // 攻击范围内必须有可补给目标
                Cell stayCell = ActionCell;
                spellRangeCell.Clear();

                for(int i = 0; i < stayCell.Neighbors.Length; i++)
                {
                    Cell dst = stayCell.Neighbors[i];
                    if (dst != null)
                    {
                        if (dst.troop != null && dst.troop.IsFight && dst.troop.IsSameForce(TargetTroop))
                        {
                            spellRangeCell.Add(dst);
                            hasTarget = true;
                        }
                    }
                }
                return hasTarget;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            spellSkill = null;
            isShow = false;
            isMoving = false;
            ContextMenu.SetVisible(false);
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;
            ShowSpellRange();
        }

        protected void ShowSpellRange()
        {
            MapRender mapRender = MapRender.Instance;
            mapRender.SetDarkMask(true);
            if (spellRangeCell.Count == 0) return;
            for (int i = 0, count = spellRangeCell.Count; i < count; ++i)
            {
                Cell c = spellRangeCell[i];
                if (!MovePath.Contains(c))
                    mapRender.SetGridMaskColor(c.x, c.y, Color.red);
                mapRender.SetDarkMaskColor(c.x, c.y, Color.black);

                GameObject resObj = PoolManager.Create(iconRes);
                if (resObj != null)
                {
                    spellIconList.Add(resObj);
                    resObj.transform.SetParent(null);
                    resObj.transform.position = c.Position;
                    if (!resObj.activeSelf)
                        resObj.SetActive(true);
                }

            }
            mapRender.EndSetGridMask();
            mapRender.EndSetDarkMask();
        }

        protected void ClearShowSpellRange()
        {
            for (int i = 0, count = spellIconList.Count; i < count; ++i)
            {
                PoolManager.Recycle(spellIconList[i]);
            }
            spellIconList.Clear();
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

        public override void OnDestroy()
        {
            ClearShowSpellRange();
            spellRangeCell.Clear();
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
                    TargetTroop.SupplyTroop(supplyTarget, itemStore, gold, food,  troops);
                    Done();
                }
            }
        }

        void Action()
        {
            GameSystem.GetSystem<TroopActionMenu>().troopRender.Clear();
            ContextMenu.CloseAll();
            Cell start = TargetTroop.cell;
            Cell stayCell = ActionCell;
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

                        if (spellRangeCell.Contains(cell))
                        {
                            if(cell.troop != null && cell.troop.IsSameForce(TargetTroop))
                            {
                                spellCell = cell;
                                supplyTarget = cell.troop;
                                Window.Instance.Open("window_troop_supply", TargetTroop, cell.troop, (System.Action<ItemStore, int, int, int>)OnSupply, (System.Action)OnCancelUI);
                            }
                        }
                        break;
                    }
            }
        }

        void OnSupply(ItemStore itemStore, int gold, int food,  int troops)
        {
            this.itemStore = itemStore;
            this.gold = gold;
            this.troops = troops;
            this.food = food;
            Action();
        }

        void OnCancelUI()
        {
            GameSystemManager.Instance.BackTo(GameSystem.GetSystem<TroopSystem>());
        }
    }
}


