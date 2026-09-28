using TKNewtonsoft.Json;
using Sango.Hexagon;
using Sango.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 地图类，管理游戏地图的加载、创建和路径查找等功能
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Map
    {
        /// <summary>
        /// 地图宽度
        /// </summary>
        [JsonProperty] public int Width { get; internal set; }
        /// <summary>
        /// 地图高度
        /// </summary>
        [JsonProperty] public int Height { get; internal set; }
        /// <summary>
        /// 网格大小
        /// </summary>
        [JsonProperty] public float GridSize { get; internal set; }
        /// <summary>
        /// 地图名称
        /// </summary>
        [JsonProperty] public string Name { get; internal set; }
        /// <summary>
        /// 内容目录
        /// </summary>
        [JsonProperty] public string ContentDir { get; internal set; }
        /// <summary>
        /// 单元格集合
        /// </summary>
        [JsonProperty] public CellSet CellSet { get; internal set; }

        /// <summary>
        /// 六边形世界
        /// </summary>
        public HexWorld HexWorld { get; internal set; }
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; internal set; }

        /// <summary>
        /// 清空地图数据
        /// </summary>
        public void Clear()
        {
            CellSet.Clear();
            CellSet = null;
        }

        /// <summary>
        /// 加载地图
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public void Load(Scenario scenario)
        {
            string mapName = scenario.Info.mapType;
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
                        int terrainTypeId = reader.ReadByte();
                        int terrainState = reader.ReadInt32();
                        int areaId = reader.ReadUInt16();
                        TerrainType terrainType = scenario.CommonData.TerrainTypes.Get(terrainTypeId);
                        if (terrainType == null)
                            terrainType = scenario.CommonData.TerrainTypes.Get(0);
                        City city = scenario.citySet.Get(areaId);
                        CellSet.SetTerrainTypeAndState(x, y, terrainType, terrainState, city);
                    }
                }
                reader.Close();
                fs.Close();
                reader.Dispose();
                fs.Dispose();
            }
        }
        /// <summary>
        /// 初始化地图
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public void Init(Scenario scenario)
        {
            CellSet.Init(this);
        }

        /// <summary>
        /// 创建地图
        /// </summary>
        /// <param name="w">宽度</param>
        /// <param name="h">高度</param>
        /// <param name="gridSize">网格大小</param>
        public void Create(int w, int h, float gridSize)
        {
            Width = w;
            Height = h;
            CellSet = new CellSet();
            CellSet.Init(w, h);
            GridSize = gridSize;
            HexWorld = new Hexagon.HexWorld(new Hexagon.Point(gridSize, gridSize), new Hexagon.Point(0, 0));
        }
        /// <summary>
        /// 根据坐标获取单元格
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>单元格对象</returns>
        public Cell GetCell(int x, int y)
        {
            return CellSet.GetCell(x, y);
        }
        /// <summary>
        /// 根据立方体坐标获取单元格
        /// </summary>
        /// <param name="cub">立方体坐标</param>
        /// <returns>单元格对象</returns>
        public Cell GetCell(Hexagon.Hex cub)
        {
            Coord coords = Coord.OffsetFromCube(cub);
            return CellSet.GetCell(coords.col, coords.row);
        }
        /// <summary>
        /// 根据位置获取单元格
        /// </summary>
        /// <param name="position">位置</param>
        /// <returns>单元格对象</returns>
        public Cell GetCell(UnityEngine.Vector3 position)
        {
            Vector2Int coords = HexWorld.PositionToCoords(position);
            return CellSet.GetCell(coords.x, coords.y);
        }
        /// <summary>
        /// 获取单元格的邻居
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="dir">方向</param>
        /// <returns>邻居单元格</returns>
        public Cell GetNeighbor(Cell cell, int dir)
        {
            return cell.Neighbors[dir];
        }
        /// <summary>
        /// 获取单元格的所有邻居
        /// </summary>
        /// <param name="cell">单元格</param>
        /// <param name="neighborList">邻居列表</param>
        public void GetNeighbors(Cell cell, List<Cell> neighborList)
        {
            for (int i = 0; i < 6; i++)
            {
                Cell c = GetNeighbor(cell, i);
                if (c != null)
                    neighborList.Add(c);
            }
        }
        /// <summary>
        /// 坐标转换为位置
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>位置</returns>
        public Vector3 Coords2Position(int x, int y)
        {
            return HexWorld.CoordsToPosition(x, y);
        }
        /// <summary>
        /// 位置转换为坐标
        /// </summary>
        /// <param name="position">位置</param>
        /// <returns>坐标</returns>
        public Vector2Int Position2Coords(Vector3 position)
        {
            return HexWorld.PositionToCoords(position);
        }
        /// <summary>
        /// 检查是否在势力范围
        /// </summary>
        /// <param name="troops">部队</param>
        /// <param name="cell">单元格</param>
        /// <returns>是否在势力范围</returns>
        public bool IsZOC(Troop troops, Cell cell)
        {
            // 判断忽略ZOC
            if ((troops.ignoreLandZOC && !cell.IsWater) || (troops.ignoreWaterZOC && cell.IsWater))
                return false;

            if (cell.building != null && !cell.building.IsEnemy(troops))
                return false;

            for (int i = 0; i < 6; i++)
            {
                Cell next = GetNeighbor(cell, i);
                if (next != null)
                {
                    if ((next.troop != null && troops.mBelongForce != next.troop.mBelongForce) ||
                        (next.building != null && troops.mBelongForce != next.building.mBelongForce))
                        return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 计算两个立方体坐标之间的距离
        /// </summary>
        /// <param name="start">起始立方体坐标</param>
        /// <param name="end">结束立方体坐标</param>
        /// <returns>距离</returns>
        public int Distance(Hexagon.Hex start, Hexagon.Hex end)
        {
            return start.Distance(end);
        }
        /// <summary>
        /// 计算两个单元格之间的距离
        /// </summary>
        /// <param name="start">起始单元格</param>
        /// <param name="end">结束单元格</param>
        /// <returns>距离</returns>
        public int Distance(Cell start, Cell end)
        {
            return start.Cub.Distance(end.Cub);
        }
        /// <summary>
        /// 计算两个坐标之间的距离
        /// </summary>
        /// <param name="ax">起始X坐标</param>
        /// <param name="ay">起始Y坐标</param>
        /// <param name="bx">结束X坐标</param>
        /// <param name="by">结束Y坐标</param>
        /// <returns>距离</returns>
        public int Distance(int ax, int ay, int bx, int by)
        {
            return Distance(Coord.OffsetToCube(ax, ay), Coord.OffsetToCube(bx, by));
        }
        /// <summary>
        /// 获取环形区域的单元格
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="radius">半径</param>
        /// <param name="cellList">单元格列表</param>
        /// <param name="checkMoveAble">是否检查可移动性</param>
        public void GetRing(int x, int y, int radius, List<Cell> cellList, bool checkMoveAble = false)
        {
            GetRing(Coord.OffsetToCube(x, y), radius, cellList, checkMoveAble);
        }
        /// <summary>
        /// 获取环形区域的单元格
        /// </summary>
        /// <param name="start">起始单元格</param>
        /// <param name="radius">半径</param>
        /// <param name="cellList">单元格列表</param>
        /// <param name="checkMoveAble">是否检查可移动性</param>
        public void GetRing(Cell start, int radius, List<Cell> cellList, bool checkMoveAble = false)
        {
            GetRing(start.Cub, radius, cellList, checkMoveAble);
        }
        /// <summary>
        /// 获取环形区域的单元格
        /// </summary>
        /// <param name="cub">立方体坐标</param>
        /// <param name="radius">半径</param>
        /// <param name="cellList">单元格列表</param>
        /// <param name="checkMoveAble">是否检查可移动性</param>
        public void GetRing(Hexagon.Hex cub, int radius, List<Cell> cellList, bool checkMoveAble = false)
        {
            cub.Ring(radius, (c =>
            {
                Cell find = GetCell(c);
                if (find != null)
                {
                    if(!checkMoveAble || (checkMoveAble && find.moveAble))
                        cellList.Add(find);
                }
            }));
        }

        /// <summary>
        /// 对环形区域的单元格执行操作
        /// </summary>
        /// <param name="start">起始单元格</param>
        /// <param name="radius">半径</param>
        /// <param name="action">操作</param>
        /// <param name="startDir">起始方向</param>
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

        /// <summary>
        /// 对螺旋区域的单元格执行操作
        /// </summary>
        /// <param name="start">起始单元格</param>
        /// <param name="radius">半径</param>
        /// <param name="action">操作</param>
        /// <param name="startDir">起始方向</param>
        public void SpiralAction(Cell start, int radius, Action<Cell> action, int startDir = 4)
        {
            action?.Invoke(start);
            for (int i = 1; i <= radius; i++)
            {
                RingAction(start, i, action, startDir);
            }
        }

        /// <summary>
        /// 获取螺旋区域的单元格
        /// </summary>
        /// <param name="start">起始单元格</param>
        /// <param name="radius">半径</param>
        /// <param name="cellList">单元格列表</param>
        public void GetSpiral(Cell start, int radius, List<Cell> cellList)
        {
            GetSpiral(start.Cub, radius, cellList);
        }
        /// <summary>
        /// 获取螺旋区域的单元格
        /// </summary>
        /// <param name="cub">立方体坐标</param>
        /// <param name="radius">半径</param>
        /// <param name="cellList">单元格列表</param>
        public void GetSpiral(Hexagon.Hex cub, int radius, List<Cell> cellList)
        {
            cub.Spiral(radius, (c =>
            {
                Cell find = GetCell(c);
                if (find != null)
                    cellList.Add(find);
            }));
        }

        /// <summary>
        /// 获取螺旋区域的单元格
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="radius">半径</param>
        /// <param name="cellList">单元格列表</param>
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
                        if (!next._isChecked)
                        {
                            bool isZoc = IsZOC(troops, next);
                            bool isWater = next.IsWater;
                            int moveAttr = isWater? troops.waterMoveAbility : troops.landMoveAbility;
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

            for (int i = 0; i < cellList.Count; i++)
            {
                Cell cell = cellList[i];
                cell._cost = 0;
                cell._isZOC = false;
                cell._isChecked = false;
            }
        }



        /// <summary>
        /// 该方法确定dest一定是在troop的移动范围内可到达
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetMovePath(Troop troop, Cell dest, List<Cell> cellList)
        {
            //Cell c = dest;
            //while (c != null)
            //{
            //    cellList.Insert(0, c);
            //    if (came_from.TryGetValue(c, out Cell parent))
            //        c = parent;
            //    else
            //        c = null;
            //}
            GetDirectPath(troop.cell, dest, cellList, (next) =>
            {
                return next.CanMove(troop) && next.CanPassThrough(troop) && troop.MoveRange.Contains(next);
            });
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

        /// <summary>
        /// 单元格检查委托
        /// </summary>
        /// <param name="checkCell">要检查的单元格</param>
        /// <returns>是否通过检查</returns>
        public delegate bool CellCheck(Cell checkCell);
        /// <summary>
        /// 关闭列表
        /// </summary>
        List<Cell> closeList = new List<Cell>();
        /// <summary>
        /// 开放列表
        /// </summary>
        List<Cell> openList = new List<Cell>();
        //PriorityQueue<Cell> frontier = new PriorityQueue<Cell>();


        /// <summary>
        /// 优先队列
        /// </summary>
        System.Collections.Generic.PriorityQueue<Cell, int> frontier = new PriorityQueue<Cell, int>(new LowPriorit());
        /// <summary>
        /// 路径来源字典
        /// </summary>
        Dictionary<Cell, Cell> came_from = new Dictionary<Cell, Cell>();
        /// <summary>
        /// 成本字典
        /// </summary>
        Dictionary<Cell, Cell> cost_so_far = new Dictionary<Cell, Cell>();

        /// <summary>
        /// 低优先级比较器
        /// </summary>
        public class LowPriorit : IComparer<int>
        {
            /// <summary>
            /// 比较两个整数
            /// </summary>
            /// <param name="x">第一个整数</param>
            /// <param name="y">第二个整数</param>
            /// <returns>比较结果</returns>
            public int Compare(int x, int y)
            {
                return x.CompareTo(y);
            }
        }


        /// <summary>
        /// 单元格临时信息
        /// </summary>
        class cellTempInfo
        {
            /// <summary>
            /// 成本
            /// </summary>
            public int cost;
            /// <summary>
            /// 是否在势力范围
            /// </summary>
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
            frontier.Clear();
            came_from.Clear();
            int safe_count = Scenario.Cur.Variables.pathfindingSafeCount;
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
                        if (came_from.TryGetValue(c, out Cell parent))
                            c = parent;
                        else
                            c = null;
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
        /// 获取周围的路径,仅判断是否可以行走,最大寻路范围为len
        /// </summary>
        /// <param name="troops"></param>
        /// <param name="dest"></param>
        /// <param name="cellList"></param>
        public void GetDirectSpiral(Cell start, int length, List<Cell> cellList)
        {
            cost_so_far.Clear();
            openList.Clear();
            int leftLen = length;
            openList.Add(start);
            while (openList.Count > 0)
            {
                int count = openList.Count;
                for (int j = 0; j < count; j++)
                {
                    Cell current = openList[j];
                    if (!cost_so_far.ContainsKey(current))
                    {
                        cellList.Add(current);
                        cost_so_far[current] = current;
                        for (int i = 0; i < 6; i++)
                        {
                            Cell next = current.Neighbors[i];
                            // 一定是目标所占格也可以进判断
                            if (next != null && next.moveAble && !cost_so_far.ContainsKey(next))
                            {
                                openList.Add(next);
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

        /// <summary>
        /// 获取指定范围内的螺旋路径
        /// </summary>
        /// <param name="start">起始单元格</param>
        /// <param name="startLen">起始长度</param>
        /// <param name="endLen">结束长度</param>
        /// <param name="cellList">路径单元格列表</param>
        public void GetDirectSpiral(Cell start, int startLen, int endLen, List<Cell> cellList)
        {
            cost_so_far.Clear();
            openList.Clear();
            int begin = 0;
            openList.Add(start);
            while (openList.Count > 0)
            {
                int count = openList.Count;
                for (int j = 0; j < count; j++)
                {
                    Cell current = openList[j];
                    if (!cost_so_far.ContainsKey(current))
                    {
                        if (begin >= startLen)
                            cellList.Add(current);
                        cost_so_far[current] = current;
                        for (int i = 0; i < 6; i++)
                        {
                            Cell next = current.Neighbors[i];
                            // 一定是目标所占格也可以进判断
                            if (next != null && next.moveAble && !cost_so_far.ContainsKey(next))
                            {
                                openList.Add(next);
                            }
                        }
                    }
                }
                openList.RemoveRange(0, count);
                if (begin >= endLen)
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
            frontier.Clear();
            came_from.Clear();
            closeList.Clear();
            int safe_count = Scenario.Cur.Variables.pathfindingSafeCount;
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
                        if (came_from.TryGetValue(c, out Cell parent))
                            c = parent;
                        else
                            c = null;
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

        /// <summary>
        /// 获取无阻挡的移动路径
        /// </summary>
        /// <param name="troops">部队</param>
        /// <param name="dest">目标单元格</param>
        /// <param name="cellList">路径单元格列表</param>
        public void GetDirectNoBlockMovePath(Troop troops, Cell dest, List<Cell> cellList)
        {
            GetDirectMovePath(troops, dest, cellList, (next) =>
            {
                return next.troop == null;
            });
        }
    }
}
