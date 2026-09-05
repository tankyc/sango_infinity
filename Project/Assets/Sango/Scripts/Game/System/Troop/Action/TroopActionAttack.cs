using Sango.Render;
using Sango.UI;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using ContextMenu = Sango.UI.ContextMenu;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionAttack : TroopActionBase
    {
        protected List<Cell> MovePath { get; set; }
        protected List<Cell> spellRangeCell = new List<Cell>();
        protected Cell spellCell;
        protected SkillInstance spellSkill;
        protected bool isShow = false;
        protected bool isMoving = false;
        protected string iconRes;
        protected List<GameObject> spellIconList = new List<GameObject>();

        public TroopActionAttack()
        {
            iconRes = "Assets/UI/Prefab/worldIcon_1.prefab"; 
            customMenuName = "攻击";
            customMenuOrder = 10;
        }

        public override bool IsValid
        {
            get
            {
                bool hasTarget = false;
                // 攻击范围内必须有可攻击目标
                Cell stayCell = ActionCell;
                spellRangeCell.Clear();
                List<Cell> rangeCell = new List<Cell>();
                if (TargetTroop.NormalSkill != null)
                {
                    SkillInstance skill = TargetTroop.NormalSkill;
                    if (skill.CanBeSpell(TargetTroop))
                    {
                        skill.GetSpellRange(TargetTroop, stayCell, rangeCell);
                        foreach (Cell c in rangeCell)
                        {
                            if (skill.CanSpellToHere(TargetTroop, c))
                            {
                                spellRangeCell.Add(c);
                                hasTarget = true;
                            }
                        }
                    }
                }
                rangeCell.Clear();
                if (TargetTroop.NormalRangeSkill != null)
                {
                    SkillInstance skill = TargetTroop.NormalRangeSkill;
                    if (skill.CanBeSpell(TargetTroop))
                    {

                        skill.GetSpellRange(TargetTroop, stayCell, rangeCell);
                        foreach (Cell c in rangeCell)
                        {
                            if (skill.CanSpellToHere(TargetTroop, c))
                            {
                                spellRangeCell.Add(c);
                                hasTarget = true;
                            }
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
                    if (TargetTroop.SpellSkill(spellSkill, spellCell))
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
            GameSystem.GetSystem<TroopActionMenu>().troopRender?.Clear();
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

                        if (spellRangeCell.Contains(cell))
                        {

                            Cell stayCell = MovePath[MovePath.Count - 1];
                            spellCell = cell;
                            if (spellSkill == null)
                            {
                                if (spellCell.Distance(stayCell) == 1)
                                    spellSkill = TargetTroop.NormalSkill;
                                else
                                    spellSkill = TargetTroop.NormalRangeSkill;
                            }

                            if (!spellSkill.CanSpellToHere(TargetTroop, cell))
                                return;

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
