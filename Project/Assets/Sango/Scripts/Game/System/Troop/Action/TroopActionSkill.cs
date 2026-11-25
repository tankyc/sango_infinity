using Sango.Render;
using Sango.UI;
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
            // 【可再移动】仅再移动状态下不显示战法（Start 里还有一层硬保护）
            if (troop != null && troop.MoveRefundCount > 0)
                return;

            if (troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
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
                        // 显示实际消耗（含特技光环减耗），与 CanBeSpell / 扣气力口径一致
                        int costEnergy = skill.GetCostEnergy(TargetTroop);
                        menuData.Add($"战法/{skill.Name}({costEnergy})", costEnergy, skill, OnClickMenuItem, isValid);
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
