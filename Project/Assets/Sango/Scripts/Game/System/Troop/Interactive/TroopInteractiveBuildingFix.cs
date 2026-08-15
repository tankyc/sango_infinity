
using Sango.Render;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 部队委任-建造建筑
    /// </summary>
    [GameSystem]
    public class TroopInteractiveBuildingFix : TroopInteractiveBase
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
            if (actionCell.building == null || actionCell.building.IsCityBase() || actionCell.building.mForce != troop.BelongForce) return false;
            if (actionCell.building.isUpgrading || actionCell.building.durability >= actionCell.building.DurabilityLimit) return false;
            TargetBuilding = actionCell.building as Building;
            TargetCell = actionCell;

            content = string.Format("即前往将对{0}进行修补。\n确定吗？", actionCell.building.Name);
            return true;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            GameMedia.Instance.PlayPersonSay(TargetTroop.Leader, GameRandom.Chance(50) ? 3188 : 3202);

            TargetTroop.SetMission(MissionType.TroopFixBuilding, TargetBuilding.Id);
            TargetTroop.Render?.UpdateRender();
        }

        public override void OnDestroy()
        {

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
