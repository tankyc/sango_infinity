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

            if (actionCell.building.mBelongForce != troop.mBelongForce) return false;

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
            TargetTroop.ActionOver = true;
            TargetTroop.Render?.UpdateRender();

            Done();

            if (ActionCell.building != null && ActionCell.building.IsSameForce(TargetTroop) && ActionCell.building.IsCityBase())
                TargetTroop.EnterCity(ActionCell.building as City);

        }
    }
}
