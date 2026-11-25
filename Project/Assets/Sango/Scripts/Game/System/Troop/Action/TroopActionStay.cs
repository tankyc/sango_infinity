using Sango.Render;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopActionStay : TroopActionBase
    {
        List<Cell> MovePath { get; set; }

        public TroopActionStay()
        {
            customMenuName = "待命";
            customMenuOrder = 0;
        }

        /// <summary>
        /// 「待命」是收尾类命令，仅再移动状态下必须保留 —— 否则部队会卡在"不能攻击也不能结束"的死局。
        /// </summary>
        protected override bool AllowWhenMoveOnly => true;

        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        protected override void OnTroopActionContextMenuShow(IContextMenuData menuData, Troop troop, Cell actionCell)
        {
            if (troop.BelongForce != null && troop.BelongForce.IsPlayer && troop.BelongForce == Scenario.Cur.CurRunForce)
            {
                TargetTroop = troop;
                ActionCell = actionCell;
                //if (actionCell.building != null && actionCell.building.IsSameForce(troop) && actionCell.building.IsCityBase())
                //    menuData.Add("进入", customMenuOrder, actionCell, OnClickMenuItem, IsValid);
                //else
                    menuData.Add("待命", customMenuOrder, actionCell, OnClickMenuItem, IsValid);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            ContextMenu.CloseAll();
            GameSystem.GetSystem<TroopActionMenu>().ShowSpellRange();
            GameSystem.GetSystem<TroopActionMenu>().troopRender?.Clear();
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;

            if (MovePath.Count <= 1)
            {
                OnMoveDone();
                return;
            }

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 2656 : 2670);

            Cell start = TargetTroop.cell;
            for (int i = 1; i < MovePath.Count; i++)
            {
                bool isLast = i == MovePath.Count - 1;
                Cell dest = MovePath[i];
                TroopMoveEvent @event = RenderEvent.Instance.Create<TroopMoveEvent>();
                @event.Init(TargetTroop, start, dest, isLast, isLast ? OnMoveDone : null);
                RenderEvent.Instance.Add(@event);
                start = dest;
            }

        }

        public override void OnDestroy()
        {
            ContextMenu.CloseAll();
        }

        public void OnMoveDone()
        {
            // 【可再移动】待命是强制收尾：先把两种额外行动计数清零。
            // 否则 ActionOver 的 setter 会命中 MoveRefundCount / ActionOverCount 的退还分支，
            // 导致"点了待命却退还给行动权"——部队永远结束不了行动（表现为待命无效、死循环）。
            TargetTroop.ActionOverCount = 0;
            TargetTroop.MoveRefundCount = 0;
            TargetTroop.ActionOver = true;
            TargetTroop.Render?.UpdateRender();

            //if (ActionCell.building != null && ActionCell.building.IsSameForce(TargetTroop) && ActionCell.building.IsCityBase())
            //    TargetTroop.EnterCity(ActionCell.building as City);

            Done();
        }
    }
}
