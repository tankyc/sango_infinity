using System;
using System.Collections.Generic;
using static Sango.Core.TroopAIUtility;

namespace Sango.Core
{
    public abstract class TroopMissionBehaviour
    {
        public List<Cell> canMovedCells = new List<Cell>();
        public Troop Troop { get; set; }
        public Troop TargetTroop { get; set; }
        public Building TargetBuilding { get; set; }
        public Person TargetPerson { get; set; }
        public City TargetCity { get; set; }
        public Cell TargetCell { get; set; }
        public BuildingType TargetBuildingType { get; set; }
        public abstract MissionType MissionType { get; }
        public abstract bool IsMissionComplete { get; }
        public abstract bool DoAI(Troop troop, Scenario scenario);
        public virtual void Prepare(Troop troop, Scenario scenario) { }

        protected PriorityActionData priorityActionData;

        public static TroopMissionBehaviour Create(int missionType)
        {
            switch (missionType)
            {
                case (int)MissionType.TroopDestroyTroop:
                    return new TroopDestroyTroop();
                case (int)MissionType.TroopDestroyBuilding:
                    return new TroopDestroyBuilding();
                case (int)MissionType.TroopOccupyCity:
                    return new TroopOccupyCity();
                case (int)MissionType.TroopBanishTroop:
                    return new TroopBanishTroop();
                case (int)MissionType.TroopProtectBuilding:
                    return new TroopProtectBuilding();
                case (int)MissionType.TroopProtectTroop:
                    return new TroopProtectTroop();
                case (int)MissionType.TroopProtectCity:
                    return new TroopProtectCity();
                case (int)MissionType.TroopMovetoCity:
                    return new TroopMovetoCity();
                case (int)MissionType.TroopReturnCity:
                    return new TroopReturnCity();
                case (int)MissionType.TroopBuildBuilding:
                    return new TroopBuildBuilding();
                case (int)MissionType.TroopFixBuilding:
                    return new TroopFixBuilding();
                case (int)MissionType.TroopTransformGoodsToCity:
                    return new TroopTransformGoodsToCity();
                case (int)MissionType.TroopMovetoCell:
                    return new TroopMovetoCell();
                case (int)MissionType.TroopMovetoBuild:
                    return new TroopMovetoBuild();
                case (int)MissionType.TroopSupplyTroop:
                    return new TroopSupplyTroop();
                case (int)MissionType.TroopAskSupply:
                    return new TroopAskSupply();
                case (int)MissionType.TroopStay:
                    return new TroopStay();
                default:
                    return new TroopReturnCity();
            }
        }

    }
}
