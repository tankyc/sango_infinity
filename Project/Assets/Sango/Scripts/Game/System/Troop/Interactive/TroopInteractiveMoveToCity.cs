namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-移动到己方城池
    /// </summary>
    [GameSystem]
    public class TroopInteractiveMoveToCity : TroopInteractiveBase
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
            if (actionCell.building == null || !actionCell.building.IsCityBase()) return false;

            if (actionCell.building.BelongForce != troop.BelongForce) return false;

            if (troop.MoveRange.Contains(actionCell)) return false;

            TargetCity = actionCell.building as City;

            // 【吞兵提示 · 交互阶段 · 仅港关】目标港关装不下这支部队时，在"移动前"的确认框里写清会损失多少兵。
            // 为什么放在这里而不是抵达时：抵达再弹窗，玩家已经没有选择余地了。
            // 溢出规则出处：City.AddTroops 把超出 TroopsLimit 的部分直接截断
            // （对齐 Open311 building_add_troops，s11_sys_scen_building.cpp:1145-1154）。
            // 城池不提示：城兵满时部队照样要能回城，溢出沿用原版 clamp，
            // 不该用"会损失"把玩家吓退（对齐 Troop.EnterCity 的港关限定）。
            int room = TroopAIFallback.GetRoom(TargetCity);
            if (TargetCity != null && TargetCity.IsPortOrGate() && room < troop.troops)
            {
                content = string.Format(
                    "即将往{0}进行移动。\n该据点最多只能再容纳 {1} 兵，本部队共 {2} 兵，\n抵达后超出部分约 {3} 兵会损失。\n确定吗？",
                    actionCell.building.ColorName, room, troop.troops, troop.troops - room);
            }
            else
            {
                content = string.Format("即将往{0}进行移动。\n确定吗？", actionCell.building.ColorName);
            }
            return true;

        }

        public override void OnEnter()
        {
            base.OnEnter();

            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);
            TargetTroop.SetMission(MissionType.TroopMovetoCity, TargetCity.Id);
            TargetTroop.Render?.UpdateRender();
        }

        public override void Update()
        {
            base.Update();
            if(!TargetTroop.IsAlive || TargetTroop.ActionOver)
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
