using Sango.Render;
using Sango.UI;
using Sango.Render;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionSkill : TroopActionAttack
    {
        public TroopActionSkill()
        {
            customMenuName = "战法";
            customMenuOrder = 20;
        }

        protected override void OnTroopActionContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {
            if (troop.mBelongForce != null && troop.mBelongForce.IsPlayer && troop.mBelongForce == Scenario.Cur.CurRunForce)
            {
                TargetTroop = troop;
                ActionCell = actionCell;

                List<SkillInstance> list;
                if (ActionCell.TerrainType.isWater)
                    list = TargetTroop.waterSkills;
                else
                    list = TargetTroop.landSkills;
                for (int i = 0, count = list.Count; i < count; ++i)
                {
                    SkillInstance skill = list[i];
                    if (!skill.IsNormal())
                    {
                        bool isValid = skill.CanBeSpell(TargetTroop);
                        if(isValid)
                        {
                            isValid = false;
                            spellRangeCell.Clear();
                            skill.GetSpellRange(TargetTroop, ActionCell, spellRangeCell);
                            foreach (Cell c in spellRangeCell)
                            {
                                if (skill.CanSpellToHere(TargetTroop, c))
                                {
                                    isValid = true;
                                    break;
                                }
                            }
                        }
                        menuData.Add($"战法/{skill.Name}({skill.costEnergy})", skill.costEnergy, skill, OnClickMenuItem, isValid);
                    }
                }
            }
        }


        protected override void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            Start(TargetTroop, ActionCell, contextMenuItem.CustomData as SkillInstance);
        }

        public void Start(Troop troop, Cell actionCell, SkillInstance skill)
        {
            spellSkill = skill;
            base.Start(troop, actionCell);
        }

        public override void OnEnter()
        {
            GameController.Instance.RotateViewEnabled = true;
            GameController.Instance.ZoomViewEnabled = true;
            GameController.Instance.DragMoveViewEnabled = true;
            GameController.Instance.KeyboardMoveEnabled = true;
            isShow = false;
            isMoving = false;
            spellRangeCell.Clear();
            ContextMenu.SetVisible(false);
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;
            Cell stayCell = ActionCell;
            if (spellSkill.CanBeSpell(TargetTroop))
            {
                List<Cell> rangeCell = new List<Cell>();
                spellSkill.GetSpellRange(TargetTroop, stayCell, rangeCell);
                foreach (Cell c in rangeCell)
                {
                    if (spellSkill.CanSpellToHere(TargetTroop, c))
                    {
                        spellRangeCell.Add(c);
                    }
                }
            }
            ShowSpellRange();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            GameController.Instance.RotateViewEnabled = false;
            GameController.Instance.ZoomViewEnabled = false;
            GameController.Instance.DragMoveViewEnabled = false;
            GameController.Instance.KeyboardMoveEnabled = false;
        }
    }
}
