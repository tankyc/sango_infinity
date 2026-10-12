using Sango.Render;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopInteractiveCityEnter : TroopInteractiveBase
    {
        List<Cell> MovePath { get; set; }
        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        protected override bool Check(Troop troop, Cell actionCell)
        {
            if (actionCell.building == null || !actionCell.building.IsCityBase()) return false;

            if (actionCell.building.BelongForce != troop.BelongForce) return false;

            if (!troop.MoveRange.Contains(actionCell)) return false;

            content = string.Format("即将往{0}进行移动。\n确定吗？", actionCell.building.ColorName);
            return true;

        }

        public override void OnEnter()
        {
            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            base.OnEnter();
            MovePath = GameSystem.GetSystem<TroopSystem>().movePath;
            if (MovePath.Count <= 1)
            {
                OnMoveDone();
                return;
            }
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

        }

        public void OnMoveDone()
        {
            // 【防御】部队可能在移动动画期间被 Clear（被歼灭 / 势力灭亡清扫等）。
            // 此时不能再执行进城结算，否则残余兵力 / 钱粮会作为"幽灵资源"并入城池。
            if (TargetTroop == null || !TargetTroop.IsAlive)
            {
                Done();
                return;
            }

            TargetTroop.ActionOver = true;
            TargetTroop.Render?.UpdateRender();

            Done();

            if (ActionCell.building != null && ActionCell.building.IsSameForce(TargetTroop) && ActionCell.building.IsCityBase())
                TargetTroop.EnterCity(ActionCell.building as City);

        }
    }
}
