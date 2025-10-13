using Sango.Hexagon;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Game
{
    public class Cell
    {
        public Vector2Int coords;
        public int terrainType;

        public TerrainType TerrainType { get; set; }
        public Hexagon.Hex Cub { get; set; }// { get { return Hexagon.Coord.OffsetToCube(x, y); } }
        public Vector3 Position { get; set; }// { get { return Scenario.Cur.Map.Coords2Position(x, y); } }
        public int x { get { return coords.x; } }
        public int y { get { return coords.y; } }
        //public float Fertility { get; set; }
        //public float Prosperity { get; set; }

        public Cell[] Neighbors = new Cell[6];

        public Troop troop;
        public BuildingBase building;
        public Fire fire;
        public bool moveAble;

        internal int _cost = 0;
        internal bool _isZOC = false;
        internal bool _isChecked = false;

        public Cell()
        {
           
        }

        public Cell(ushort x, ushort y)
        {
            coords = new Vector2Int()
            {
                x = x,
                y = y
            };
            Cub = Hexagon.Coord.OffsetToCube(x, y);
        }

        public Cell(byte terrainTypeId, uint status, ushort x, ushort y)
        {
            terrainType = terrainTypeId;
            coords = new Vector2Int()
            {
                x = x,
                y = y
            };
            Cub = Hexagon.Coord.OffsetToCube(x, y);
        }

        public void Init(Map map)
        {
            Vector3 pos = Scenario.Cur.Map.Coords2Position(x, y);
            pos.y = MapRender.Instance.mapGrid.GetGridHeight(x, y);
            Position = pos;
            for (int i = 0; i < 6; i++)
            {
                Hexagon.Hex neighbor = Cub.Neighbor(i);
                Cell neighborCell = map.GetCell(neighbor);
                if (neighborCell != null)
                    Neighbors[i] = neighborCell;
            }
        }

        public bool CanPassThrough(Troop troops)
        {
            return (this.troop == null || this.troop.BelongForce == troops.BelongForce) &&
                         (this.building == null || this.building.BelongForce == troops.BelongForce);
        }
        public bool CanMove(Troop troops)
        {
            return TerrainType != null && TerrainType.CanMoveBy(troops);
        }
        public bool CanStay(Troop troops)
        {
            return this.troop == null && this.building == null && CanMove(troops);
        }
        public bool IsEmpty()
        {
            return this.troop == null && this.building == null;
        }

        public Cell OffsetCell(int offsetX, int offsetY)
        {
            return Scenario.Cur.Map.GetCell(x + offsetX, y + offsetY);
        }
        public int Distance(Cell other)
        {
            return Cub.Distance(other.Cub);
        }
        public void Ring(int radius, List<Cell> list)
        {
            Scenario.Cur.Map.GetRing(this, radius, list);
        }
        public void Ring(int radius, Action<Cell> action)
        {
            Scenario.Cur.Map.RingAction(this, radius, action);
        }

        public void DirectionLine(Cell to, int length, Action<Cell> action)
        {
            int dir = Cub.DirectionTo(to.Cub);
            Hex start = Cub;
            for(int i = 0; i < length; ++i)
            {
                start = start.Neighbor(dir);
                Cell cell = Scenario.Cur.Map.GetCell(start);
                if (cell != null && action != null)
                    action(cell);
            }
        }

    }
}
