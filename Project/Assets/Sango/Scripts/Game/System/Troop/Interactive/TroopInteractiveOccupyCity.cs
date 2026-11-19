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

            // 【军团委任 · 禁止攻击】该部队所属军团被设为禁止对外用兵时，攻城指令直接不可用。
            // 判定与 AI 侧同口径（CityAI.IsAttackForbidden），否则玩家手动下令后
            // TroopOccupyCity.Prepare 会立刻把任务撤销，表现为"点了没反应"。
            // 要打，请先在军团委任界面把"攻击"改回允许。
            if (CityAI.IsAttackForbidden(troop.BelongCity)) return false;

            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetCity = actionCell.building as City;
            content = string.Format("目标是占领{0}。\n确定吗？", actionCell.building.ColorName);
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
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
