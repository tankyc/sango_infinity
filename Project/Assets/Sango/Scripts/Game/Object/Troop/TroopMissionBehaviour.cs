using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public abstract class TroopMissionBehaviour
    {
        public List<Cell> canMovedCells = new List<Cell>();
        public Troop Troop { get; set; }
        public Troop TargetTroop { get; set; }
        public Building TargetBuilding { get; set; }
        public Person TargetPerson { get; set; }
        public City TargetCity { get; set; }
        public abstract MissionType MissionType { get; }
        public abstract bool IsMissionComplete { get; }
        public abstract bool DoAI(Troop troop, Scenario scenario);
        public virtual void Prepare(Troop troop, Scenario scenario) { }
    }
}
