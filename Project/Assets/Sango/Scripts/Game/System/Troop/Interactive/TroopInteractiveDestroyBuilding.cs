namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-占领城池
    /// </summary>
    [GameSystem]
    public class TroopInteractiveDestroyBuilding : TroopInteractiveBase
    {
        public Building TargetBuilding { get; set; }
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

            if (actionCell.building == null || actionCell.building.IsCityBase()) return false;

            // 【盟友 / 停战势力的建筑不可摧毁】与 <see cref="TroopInteractiveOccupyCity"/> 同一口径：
            // 只有敌对或无人占领的建筑才允许攻击（Open311 `move_to_building`，
            // s11_sys_unit_order.cpp:176-181）。非敌对的建筑改由"移动"靠过去。
            if (!troop.IsEnemy(actionCell.building)) return false;

            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetBuilding = actionCell.building as Building;
            content = string.Format("前往摧毁建筑{0}。\n确定吗？", actionCell.building.ColorName);
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.SetMission(MissionType.TroopDestroyBuilding, TargetBuilding.Id);
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
