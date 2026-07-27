namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-占领城池
    /// </summary>
    [GameSystem]
    public class TroopInteractiveOccupyCity : TroopInteractiveBase
    {
        public City TargetCity { get; set; }
        public Cell TargetCell { get; set; }

        public override bool IsValid
        {
            get
            {
                return true;
            }
        }

        protected override bool Check(Troop troop, Cell actionCell)
        {
            if(troop.IsTransport) return false;

            if (actionCell.building == null || !actionCell.building.IsCityBase()) return false;

            if (actionCell.building.BelongForce == troop.BelongForce) return false;

            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetCity = actionCell.building as City;
            content = string.Format("目标是占领{0}。\n确定吗？", actionCell.building.ColorName);
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.missionTarget = TargetCity.Id;
            TargetTroop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
            TargetTroop.Render?.UpdateRender();
        }

        public override void Update()
        {
            base.Update();
            if (!TargetTroop.IsAlive)
            {
                Done();
                return;
            }
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
