namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-占领城池
    /// </summary>
    [GameSystem]
    public class TroopInteractiveBanishTroop : TroopInteractiveBase
    {
        public Troop DestTroop { get; set; }

        public Cell TargetCell { get; set; }

        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        public TroopInteractiveBanishTroop()
        {
            customMenuName = "驱逐";
            customMenuOrder = 0;
        }

        protected override bool Check(Troop troop, Cell actionCell)
        {
            if (troop.IsTransport) return false;

            if (actionCell.troop == null) return false;

            if (!actionCell.troop.IsEnemy(TargetTroop)) return false;

            if (troop.MoveRange.Contains(actionCell)) return false;

            if (!actionCell.troop.cell.BelongCity.IsSameForce(troop))
                return false;

            DestTroop = actionCell.troop as Troop;
            content = string.Format("前往驱逐敌方部队{0}。\n确定吗？", DestTroop.ColorName);
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();
            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.SetMission(MissionType.TroopBanishTroop, DestTroop.Id);
            TargetTroop.Render?.UpdateRender();
        }

        public override void Update()
        {
            base.Update();
            if (!TargetTroop.DoAI(Scenario.Cur))
                return;

            OnAIDone();
        }

        public override void OnDestroy()
        {

        }

        public void OnAIDone()
        {
            TargetTroop.ActionOver = true;
            TargetTroop.Render?.UpdateRender();
            Done();
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

        }

    }
}
