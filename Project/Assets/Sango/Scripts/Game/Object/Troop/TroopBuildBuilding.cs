using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public class TroopBuildBuilding : TroopMissionBehaviour
    {
        Cell FinalCell { get; set; }
        public override MissionType MissionType { get { return MissionType.TroopBuildBuilding; } }
        public override bool IsMissionComplete
        {
            get
            {
                return (
                    TargetCell == null
                    || (TargetCell.building != null && TargetCell.building.IsSameForce(Troop) && TargetCell.building.isComplate) 
                    || (TargetCell.building != null && !TargetCell.building.IsSameForce(Troop))
                    || !TargetBuildingType.CanBuildToHere(TargetCell)
                    || Troop.gold < TargetBuildingType.cost
                    );
            }
        }

        public override void Prepare(Troop troop, Scenario scenario)
        {
            if (Troop != troop) Troop = troop;
            if (TargetBuildingType == null || TargetBuildingType.Id != troop.missionTarget) TargetBuildingType = scenario.GetObject<BuildingType>(Troop.missionTarget);
            if (TargetCity == null) TargetCity = troop.BelongCity;
            if (TargetCell == null) TargetCell = troop.missionTargetCell;

            // 任务完成后,如果城池被友军拿取则回到创建城池,否则将进入己方目标城池
            if (IsMissionComplete)
            {
                if (Troop.BelongCity.IsSameForce(Troop))
                {
                    Troop.SetMission(MissionType.TroopReturnCity, Troop.BelongCity.Id);
                }
                else
                {
                    Troop.SetMission(MissionType.TroopOccupyCity, TargetCity.Id);
                }
                Troop.NeedPrepareMission();
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
                Cell cell = TargetCell.Neighbors[i];
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
                Troop.NeedPrepareMission();
                return false;
            }

            if (FinalCell != null)
            {
                if (!troop.MoveTo(FinalCell))
                    return false;

                if (!troop.BuildBuilding(TargetCell, TargetBuildingType))
                    return false;
                return true;

            }

            return troop.TryCloseTo(TargetCell);
        }
    }
}
