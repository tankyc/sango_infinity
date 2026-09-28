using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopFixBuilding : TroopMissionBehaviour
    {
        Cell FinalCell { get; set; }
        public override MissionType MissionType { get { return MissionType.TroopFixBuilding; } }
        public override bool IsMissionComplete
        {
            get
            {
                return (TargetBuilding == null || !TargetBuilding.IsAlive || TargetBuilding.durability >= TargetBuilding.DurabilityLimit);
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetBuilding == null || TargetBuilding.Id != troop.missionTarget) TargetBuilding = scenario.GetObject<Building>(Troop.missionTarget);
            if (TargetCity == null) TargetCity = troop.BelongCity;
            // 任务完成后,待命
            if (IsMissionComplete)
            {
                // 【说明】原先此处有 `if (troop.IsPlayerControl) ClearMission()` 的补丁。
                // 玩家第一军团现已改走 PlayerTroopFixBuilding，
                // 由该类型负责"交回玩家"，故此处只保留势力 AI 的处理。
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
                return;
            }

            if (troop.MoveRange.Count == 0)
            {
                scenario.Map.GetMoveRange(troop, troop.MoveRange);
#if SANGO_DEBUG_AI
                GameAIDebug.Instance.ShowMoveRange(troop.MoveRange, troop);
#endif
            }

            List<Cell> emptyCell = new List<Cell>();
            for (int i = 0; i < 6; i++)
            {
                Cell cell = TargetBuilding.CenterCell.Neighbors[i];
                if (cell == troop.cell)
                {
                    FinalCell = cell;
                    return;
                }
                else if (cell.IsEmpty() && troop.MoveRange.Contains(cell))
                {
                    emptyCell.Add(cell);
                }
            }

            if (emptyCell.Count > 0)
            {
                emptyCell.Sort((a, b) => a.Distance(troop.cell).CompareTo(b.Distance(troop.cell)));
                FinalCell = emptyCell[0];
            }
            else
            {
                FinalCell = null;
            }
        }

        public override bool DoAI(Troop troop, Scenario scenario)
        {
            if (IsMissionComplete)
            {
                Troop.actionRenderEvent = null;
                // 【说明】同 Prepare：玩家第一军团已改走 PlayerTroopFixBuilding
                troop.SetMission(MissionType.TroopReturnCity, troop.BelongCity.Id);
                troop.NeedPrepareMission();
                return true;
            }

            if (FinalCell != null)
            {
                if (!troop.MoveTo(FinalCell))
                    return false;

                if (!troop.BuildBuilding(TargetBuilding.CenterCell, null))
                    return false;
                return true;

            }

            return troop.TryCloseTo(TargetBuilding.CenterCell);
        }
    }
}
