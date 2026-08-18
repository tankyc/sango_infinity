namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-移动到己方城池
    /// </summary>
    [GameSystem]
    public class TroopInteractiveMoveToBuild : TroopInteractiveBase
    {
        public City TargetCity { get; set; }
        public Cell TargetCell { get; set; }

        public TroopInteractiveMoveToBuild()
        {
            customMenuName = "设置";
            customMenuOrder = 0;
        }
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

            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetCity = actionCell.building as City;
            content = string.Format("即将往{0}进行移动。\n确定吗？", actionCell.building.ColorName);
            return false;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.SetMission(MissionType.TroopMovetoBuild, TargetCity.Id);
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
