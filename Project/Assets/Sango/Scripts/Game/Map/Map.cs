using Newtonsoft.Json;
using Sango.Hexagon;
using Sango.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Map
    {
        [JsonProperty] public int Width { get; internal set; }
        [JsonProperty] public int Height { get; internal set; }
        [JsonProperty] public float GridSize { get; internal set; }
        [JsonProperty] public string Name { get; internal set; }
        [JsonProperty] public string ContentDir { get; internal set; }
        [JsonProperty] public CellSet CellSet { get; internal set; }
        public HexWorld HexWorld { get; internal set; }
        public string FileName { get; internal set; }

        public void Load(string mapName)
        {
            FileName = Path.FindFile($"Map/{mapName}.bin");
            if (File.Exists(FileName))
            {
                Name = mapName;
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                BinaryReader reader = new BinaryReader(fs);
                int versionCode = reader.ReadInt32();
                if (versionCode < 6)
                {
                    return;
                }
                ContentDir = reader.ReadString();
                int mapWidth = reader.ReadInt32();
                int mapHeight = reader.ReadInt32();
                int grid_size = reader.ReadInt32();
                Width = mapWidth / 4;
                Height = mapHeight / 4;
                GridSize = grid_size;
                Create(Width, Height, grid_size);
                for (int x = 0; x < Width; ++x)
                {
                    for (int y = 0; y < Height; ++y)
                    {
                        int terrainType = reader.ReadByte();
                        CellSet.SetTerrainType(x, y, terrainType);
                    }
                }

            }
        }
        public void Init(Scenario scenario)
        {
            CellSet.Init(this);
        }

        public void Create(int w, int h, float gridSize)
        {
            Width = w;
            Height = h;
            CellSet = new CellSet();
            CellSet.Init(w, h);
            GridSize = gridSize;
            HexWorld = new Hexagon.HexWorld(new Hexagon.Point(gridSize, gridSize), new Hexagon.Point(0, 0));
        }
        public Cell GetCell(int x, int y)
        {
            return CellSet.GetCell(x, y);
        }
        public Cell GetCell(Hexagon.Hex cub)
        {
            Coord coords = Coord.OffsetFromCube(cub);
            return CellSet.GetCell(coords.col, coords.row);
        }
        public Cell GetCell(UnityEngine.Vector3 position)
        {
            Vector2Int coords = HexWorld.PositionToCoords(position);
            return CellSet.GetCell(coords.x, coords.y);
        }
        public Cell GetNeighbor(Cell cell, int dir)
        {
            return cell.Neighbors[dir];
        }
        public void GetNeighbors(Cell cell, List<Cell> neighborList)
        {
            for (int i = 0; i < 6; i++)
            {
                Cell c = GetNeighbor(cell, i);
                if (c != null)
                    neighborList.Add(c);
            }
        }
        public Vector3 Coords2Position(int x, int y)
        {
            return HexWorld.CoordsToPosition(x, y);
        }
        public Vector2Int Position2Coords(Vector3 position)
        {
            return HexWorld.PositionToCoords(position);
        }
        public bool IsZOC(Troop troops, Cell cell)
        {
            for (int i = 0; i < 6; i++)
            {
                Cell next = GetNeighbor(cell, i);
                if (next != null)
                {
                    if ((next.troop != null && troops.BelongForce != next.troop.BelongForce) ||
                        (next.building != null && troops.BelongForce != next.building.BelongForce))
                        return true;
                }
            }
            return false;
        }
        public int Distance(Hexagon.Hex start, Hexagon.Hex end)
        {
            return start.Distance(end);
        }
        public int Distance(Cell start, Cell end)
        {
            return start.Cub.Distance(end.Cub);
        }
        public int Distance(int ax, int ay, int bx, int by)
        {
            return Distance(Coord.OffsetToCube(ax, ay), Coord.OffsetToCube(bx, by));
        }
        public void GetRing(int x, int y, int radius, List<Cell> cellList)
        {
            GetRing(Coord.OffsetToCube(x, y), radius, cellList);
        }
        public void GetRing(Cell start, int radius, List<Cell> cellList)
        {
            GetRing(start.Cub, radius, cellList);
        }
        public void GetRing(Hexagon.Hex cub, int radius, List<Cell> cellList)
        {
            cub.Ring(radius, (c =>
            {
                Cell find = GetCell(c);
                if (find != null)
                    cellList.Add(find);
            }));
        }

        public void RingAction(Cell start, int radius, Action<Cell> action, int startDir = 4)
        {
            if (radius == 0)
            {
                action?.Invoke(start);
                return;
            }

            Cell cell = start.Neighbors[startDir];
            for (int i = 1; i < radius; i++)
            {
                Cell temp = cell.Neighbors[startDir];
                if (cell == null)
                    return;
                cell = temp;
            }

            int dir = startDir - 4;
            for (int i = 0; i < 6; i++)
            {
                if (cell == null)
                    break;
                int dir_i = dir + i;
                if (dir_i < 0)
                {
                    dir_i += 6;
                }
                else if (dir_i > 5)
                {
                    dir_i -= 6;
                }
                for (int j = 0; j < radius; j++)
                {
                    action?.Invoke(cell);
                    cell = cell.Neighbors[dir_i];
                    if (cell == null)
                        break;
                }
            }
        }

        public void SpiralAction(Cell start, int radius, Action<Cell> action, int startDir = 4)
        {
            action?.Invoke(start);
            for (int i = 1; i <= radius; i++)
            {
                RingAction(start, i, action, startDir);
            }
        }

        public void GetSpiral(Cell start, int radius, List<Cell> cellList)
        {
            GetSpiral(start.Cub, radius, cellList);
        }
        public void GetSpiral(Hexagon.Hex cub, int radius, List<Cell> cellList)
        {
            cub.Spiral(radius, (c =>
            {
                Cell find = GetCell(c);
                if (find != null)
                    cellList.Add(find);
            }));
        }

        public void GetSpiral(int x, int y, int radius, List<Cell> cellList)
        {
            Hexagon.Hex cub = Hexagon.Coord.OffsetToCube(x, y);
            GetSpiral(cub, radius, cellList);
        }

        struct MoveCostData
        {
            public Cell dest;
            public Cell src;
            public int cost;
            public MoveCostData(Cell cell, Cell src, int c)
            {
                dest = cell; cost = c; this.src = src;
            }
        }

        /// <summary>
        /// 获取移动范围
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="cellList"></param>
        public void GetMoveRange(Troop troops, List<Cell> cellList)
        {
            frontier.Clear();
            came_from.Clear();
            cost_so_far.Clear();
            cellList.Add(troops.cell);
            int moveAttr = troops.MoveAbility;
            came_from[troops.cell] = null;
            frontier.Enqueue(troops.cell, 0);
            troops.cell._isChecked = true;
            while (frontier.Count > 0)
            {
                Cell current = frontier.Dequeue();
                if (current._isZOC)
                    continue;

                int cost_current = current._cost;
                for (int i = 0; i < 6; i++)
                {
                    Cell next = GetNeighbor(current, i);
                    if (next != null && next.CanMove(troops) && next.CanPassThrough(troops))
                    {
                        if(!next._isChecked)
                        {
                            bool isZoc = IsZOC(troops, next);
                            int new_cost;
                            if (isZoc)
                                new_cost = moveAttr;
                            else
                                new_cost = cost_current + troops.MoveCost(next);

                            if (new_cost > moveAttr)
                                continue;

                            next._cost = new_cost;
                            next._isZOC = isZoc;
                            next._isChecked = true;

                            came_from[next] = current;
                            frontier.Enqueue(next, new_cost);
                            cellList.Add(next);
#if SANGO_DEBUG_AI
                            GameAIDebug.Instance.ShowCellCost(next, new_cost, troops);
#endif
                        }
//                        else if(!next._isZOC)
//                        {
//                            int new_cost = cost_current + troops.MoveCost(next);
//                            if (new_cost < next._cost)
//                            {
//                                next._cost = new_cost;
//                                came_from[next] = current;
//                                frontier.Enqueue(next, new_cost);
//#if SANGO_DEBUG_AI
//                                GameAIDebug.Instance.ShowCellCost(next, new_cost, troops);
//#endif
//                            }
//                        }
                    }
                }
            }

            for(int i = 0; i < cellList.Count; i++)
            {
                Cell cell = cellList[i];
                cell._cost = 0;
                cell._isZOC = false;
                cell._isChecked = false;
            }
        }

//        public void GetMoveRange2(Troop troops, List<Cell> cellList)
//        {

//            frontier.Clear();
//            came_from.Clear();
//            cost_so_far.Clear();
//            cellList.Add(troops.cell);
//            int moveAttr = troops.MoveAbility;
//            came_from[troops.cell] = null;
//            frontier.Enqueue(troops.cell, 0);
//            cost_so_far[troops.cell] = new cellTempInfo()
//            {
//                cost = 0,
//                isZOC = false
//            };
//            while (frontier.Count > 0)
//            {
//                Cell current = frontier.Dequeue();
//                cellTempInfo cellTempInfo = cost_so_far[current] as cellTempInfo;

//                if (cellTempInfo.isZOC)
//                    continue;

//                int cost_current = cellTempInfo.cost;
//                for (int i = 0; i < 6; i++)
//                {
//                    Cell next = GetNeighbor(current, i);
//                    if (next != null && next.CanMove(troops) && next.CanPassThrough(troops))
//                    {

//                        cellTempInfo cellTempInfo_next = cost_so_far[next] as cellTempInfo;
//                        if (cellTempInfo_next == null)
//                        {
//                            bool isZoc = IsZOC(troops, next);
//                            int new_cost;
//                            if (isZoc)
//                                new_cost = moveAttr;
//                            else
//                                new_cost = cost_current + troops.MoveCost(next);

//                            if (new_cost > moveAttr)
//                                continue;

//                            cost_so_far.Add(next, new cellTempInfo()
//                            {
//                                cost = new_cost,
//                                isZOC = isZoc
//                            });
//                            int priority = new_cost;
//                            came_from[next] = current;
//                            frontier.Enqueue(next, priority);
//                            cellList.Add(next);
//#if SANGO_DEBUG_AI
//                            GameAIDebug.Instance.ShowCellCost(next, priority, troops);
//#endif
//                        }
//                        else
//                        {
//                            int new_cost = 0;
//                            bool isZoc = cellTempInfo_next.isZOC;
//                            if (isZoc)
//                                new_cost = moveAttr;
//                            else
//                                new_cost = cost_current + troops.MoveCost(next);

//                            if (new_cost < cellTempInfo_next.cost)
//                            {
//                                cellTempInfo_next.cost = new_cost;
//                                int priority = new_cost;
//                                came_from[next] = current;
//                                frontier.Enqueue(next, priority);
//#if SANGO_DEBUG_AI
//                                GameAIDebug.Instance.ShowCellCost(next, priority, troops);
//#endif
//                            }

//                        }
//                    }
//                }
//            }
//        }

        /// <summary>
        /// 该方法确定dest一定是在troop的移动范围内可到达
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetMovePath(Troop troops, Cell dest, List<Cell> cellList)
        {
            Cell c = dest;
            while (c != null)
            {
                cellList.Insert(0, c);
                c = came_from[c] as Cell;
            }
            //GetMinCostMovePath(troops, dest, cellList);
        }

        //PriorityQueue<Cell> priorityClosest = new PriorityQueue<Cell>();
        /// <summary>
        /// 该方法用来获取一个最接近目标的位置
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetClosestMovePath(Troop troops, Cell dest, List<Cell> cellList)
        {
            // 先获取一个直接路径
            GetDirectMovePath(troops, dest, cellList);
        }


        /// <summary>
        /// 该方法寻找Troop到B的最小Cost路径
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetMinCostMovePath(Troop troops, Cell dest, List<Cell> cellList)
        {
            GetDirectMovePath(troops, dest, cellList, (next) =>
            {
                return (next.CanPassThrough(troops) || (next.building != null && next.building == dest.building) || (next.troop != null && next.troop == dest.troop));
            });
        }

        public delegate bool CellCheck(Cell checkCell);
        List<Cell> closeList = new List<Cell>();
        List<Cell> openList = new List<Cell>();
        //PriorityQueue<Cell> frontier = new PriorityQueue<Cell>();


        System.Collections.Generic.PriorityQueue<Cell, int> frontier = new PriorityQueue<Cell, int>(new LowPriorit());
        Hashtable came_from = new Hashtable();
        //Dictionary<Cell, cellTempInfo> cost_so_far = new Dictionary<Cell, cellTempInfo>();
        Hashtable cost_so_far = new Hashtable();

        public class LowPriorit : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                return x.CompareTo(y);
            }
        }


        class cellTempInfo
        {
            public int cost;
            public bool isZOC;
        }


        /// <summary>
        /// 获取一个格子之间的路径,仅判断是否可以行走
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetDirectPath(Cell start, Cell dest, List<Cell> cellList, CellCheck action = null)
        {

            if (start.Cub.Distance(dest.Cub) > 40)
            {
                UnityEngine.Debug.LogError($"寻路距离: 40 At:<{start.x},{start.y}> => <{dest.x},{dest.y}>]");
                return;
            }

            frontier.Clear();
            came_from.Clear();
            int safe_count = 1000;
            frontier.Enqueue(start, 0);
            came_from[start] = null;

            while (frontier.Count > 0)
            {
                Cell current = frontier.Dequeue();
                safe_count--;

                if (safe_count < 0)
                {
                    UnityEngine.Debug.LogError($"寻路超出安全次数: At:<{start.x},{start.y}> => <{dest.x},{dest.y}>]");
                    return;
                }

                if (current == dest)
                {
                    Cell c = current;
                    while (c != null)
                    {
                        cellList.Insert(0, c);
                        c = came_from[c] as Cell;
                    }
                    return;
                }

                for (int i = 0; i < 6; i++)
                {
                    Cell next = current.Neighbors[i];
                    // 一定是目标所占格也可以进判断
                    if ((next != null && next.moveAble && (action == null || action(next))))
                    {
                        if (!came_from.ContainsKey(next))
                        {
                            came_from[next] = current;
                            frontier.Enqueue(next, Distance(next, dest));
                            if (next == dest)
                                break;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// 获取一个格子之间的路径,仅判断是否可以行走,最大寻路范围为len
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetDirectSpiral(Cell start, int length, List<Cell> cellList)
        {
            came_from.Clear();
            openList.Clear();
            int leftLen = length;
            openList.Add(start);
            while (openList.Count > 0)
            {
                int count = openList.Count;
                for (int j = 0; j < count; j++)
                {
                    Cell current = openList[j];
                    if (!came_from.ContainsKey(current))
                    {
                        cellList.Add(current);
                        came_from.Add(current, current);
                        for (int i = 0; i < 6; i++)
                        {
                            Cell next = current.Neighbors[i];
                            // 一定是目标所占格也可以进判断
                            if (next != null && next.moveAble && !came_from.ContainsKey(next))
                            {
                                openList.Add((Cell)next);
                            }
                        }
                    }
                }
                openList.RemoveRange(0, count);
                if (leftLen <= 0)
                {
                    return;
                }

                leftLen--;
            }
        }

        public void GetDirectSpiral(Cell start, int startLen, int endLen, List<Cell> cellList)
        {
            came_from.Clear();
            openList.Clear();
            int begin = 0;
            openList.Add(start);
            while (openList.Count > 0)
            {
                int count = openList.Count;
                for (int j = 0; j < count; j++)
                {
                    Cell current = openList[j];
                    if (!came_from.ContainsKey(current))
                    {
                        if (begin >= startLen) 
                            cellList.Add(current);
                        came_from.Add(current, current);
                        for (int i = 0; i < 6; i++)
                        {
                            Cell next = current.Neighbors[i];
                            // 一定是目标所占格也可以进判断
                            if (next != null && next.moveAble && !came_from.ContainsKey(next))
                            {
                                openList.Add((Cell)next);
                            }
                        }
                    }
                }
                openList.RemoveRange(0, count);
                if (begin > endLen)
                {
                    return;
                }

                begin++;
            }
        }

        /// <summary>
        /// 该方法确定dest一定是在troop的移动范围内可到达
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetDirectMovePath(Troop troops, Cell dest, List<Cell> cellList, CellCheck action = null)
        {
            //if (troops.cell.Distance(dest) > 200)
            //{
            //    UnityEngine.Debug.LogError($"寻路超出安全次数: [{troops.Name},At:<{troops.x},{troops.y}> => <{dest.x},{dest.y}>] count:{troops.cell.Distance(dest)}");
            //    return;
            //}

            frontier.Clear();
            came_from.Clear();
            closeList.Clear();
            int safe_count = 10000;
            frontier.Enqueue(troops.cell, 0);
            came_from[troops.cell] = null;
            troops.cell._isChecked = true;
            closeList.Add(troops.cell);

            while (frontier.Count > 0)
            {
                Cell current = frontier.Dequeue();

                if (safe_count < 0)
                {
                    UnityEngine.Debug.LogError($"寻路超出安全次数: [{troops.Name},At:<{troops.x},{troops.y}> => <{dest.x},{dest.y}>]");
                    for (int i = 0; i < closeList.Count; i++)
                    {
                        Cell cell = closeList[i];
                        cell._cost = 0;
                        cell._isZOC = false;
                        cell._isChecked = false;
                    }
                    return;
                }

                if (current == dest)
                {
                    Cell c = current;
                    while (c != null)
                    {
                        cellList.Insert(0, c);
                        c = came_from[c] as Cell;
                    }
                    for (int i = 0; i < closeList.Count; i++)
                    {
                        Cell cell = closeList[i];
                        cell._cost = 0;
                        cell._isZOC = false;
                        cell._isChecked = false;
                    }
                    return;
                }

                int cost_current = current._cost;

                for (int i = 0; i < 6; i++)
                {
                    Cell next = GetNeighbor(current, i);
                    // 一定是目标所占格也可以进判断
                    if (next == dest || (next != null && next.CanMove(troops) && (action == null || action(next))))
                    {
                        int next_move_cost = troops.MoveCost(next);
                        int new_cost = cost_current + next_move_cost;

                        if (!next._isChecked)
                        {
                            safe_count--;

                            next._cost = new_cost;
                            next._isChecked = true;
                            closeList.Add(next);
                            int priority = new_cost + Distance(next, dest) * 2;
                            came_from[next] = current;
                            frontier.Enqueue(next, priority);
                        }
                        //else if (new_cost < next._cost)
                        //{
                        //    next._cost = new_cost;
                        //    int priority = new_cost + Distance(next, dest) * 2;
                        //    came_from[next] = current;
                        //    frontier.Enqueue(next, priority);
                        //}
                    }
                }
            }

            for (int i = 0; i < closeList.Count; i++)
            {
                Cell cell = closeList[i];
                cell._cost = 0;
                cell._isZOC = false;
                cell._isChecked = false;
            }
        }

        public void GetDirectNoBlockMovePath(Troop troops, Cell dest, List<Cell> cellList)
        {
            GetDirectMovePath(troops, dest, cellList, (next) =>
            {
                return next.troop == null;
            });
        }
    }
}
