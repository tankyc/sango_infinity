namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-移动到己方城池
    /// </summary>
    [GameSystem]
    public class TroopInteractiveMoveToCell : TroopInteractiveBase
    {
        public Cell TargetCell { get; set; }

        public TroopInteractiveMoveToCell()
        {
            customMenuName = "移动";
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
            if (!actionCell.CanStay(troop) || !actionCell.moveAble) return false;
            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetCell = actionCell;
            content = string.Format("即将往坐标<{0},{1}>进行移动。\n确定吗？", actionCell.x, actionCell.y);
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.SetMission(MissionType.TroopMovetoCell, 0);
            TargetTroop.missionParams1 = TargetCell.x;
            TargetTroop.missionParams2 = TargetCell.y;
            TargetTroop.NeedPrepareMission();
            TargetTroop.Render?.UpdateRender();
        }

        public override void Update()
        {
            base.Update();
            if (!TargetTroop.IsAlive || TargetTroop.ActionOver)
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
