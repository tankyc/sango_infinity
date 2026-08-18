using Sango.Hexagon;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 单元格类，管理游戏地图中的单个单元格
    /// </summary>
    public class Cell
    {
        /// <summary>
        /// 坐标
        /// </summary>
        public Vector2Int coords;

        /// <summary>
        /// 地形状态
        /// </summary>
        public int terrainState;

        /// <summary>
        /// 天气类型 (待实现)
        /// </summary>
        public int weatherType;

        /// <summary>
        /// 区域ID
        /// </summary>
        public int areaId;

        /// <summary>
        /// 所属城市
        /// </summary>
        public City BelongCity { get; internal set; }
        /// <summary>
        /// 地形类型对象
        /// </summary>
        public TerrainType TerrainType { get; set; }
        /// <summary>
        /// 立方体坐标
        /// </summary>
        public Hexagon.Hex Cub { get; set; }// { get { return Hexagon.Coord.OffsetToCube(x, y); } }
        /// <summary>
        /// 位置
        /// </summary>
        public Vector3 Position { get; set; }// { get { return Scenario.Cur.Map.Coords2Position(x, y); } }
        /// <summary>
        /// X坐标
        /// </summary>
        public int x { get { return coords.x; } }
        /// <summary>
        /// Y坐标
        /// </summary>
        public int y { get { return coords.y; } }
        //public float Fertility { get; set; }
        //public float Prosperity { get; set; }

        /// <summary>
        /// 邻居单元格数组
        /// </summary>
        public Cell[] Neighbors = new Cell[6];

        /// <summary>
        /// 部队
        /// </summary>
        public Troop troop;
        /// <summary>
        /// 建筑
        /// </summary>
        public BuildingBase building;
        /// <summary>
        /// 火焰
        /// </summary>
        public Fire fire;
        /// <summary>
        /// 是否可移动
        /// </summary>
        public bool moveAble;
        /// <summary>
        /// 内政模型
        /// </summary>
        public MapObject interiorModel;

        /// <summary>
        /// 成本
        /// </summary>
        internal int _cost = 0;
        /// <summary>
        /// 是否在势力范围
        /// </summary>
        internal bool _isZOC = false;
        /// <summary>
        /// 是否已检查
        /// </summary>
        internal bool _isChecked = false;

        /// <summary>
        /// 是否为内政区域
        /// </summary>
        public bool IsInterior => HasGridState(Sango.Render.MapGrid.GridState.Interior);

        /// <summary>
        /// 是否是水块
        /// </summary>
        public bool IsWater => TerrainType.isWater;

        /// <summary>
        /// 能否建造
        /// </summary>
        public bool CanBuild => TerrainType.canBuild;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public Cell()
        {

        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        public Cell(ushort x, ushort y)
        {
            coords = new Vector2Int()
            {
                x = x,
                y = y
            };
            Cub = Hexagon.Coord.OffsetToCube(x, y);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="terrainTypeId">地形类型ID</param>
        /// <param name="status">状态</param>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        public Cell(byte terrainTypeId, uint status, ushort x, ushort y)
        {
            //terrainType = terrainTypeId;
            coords = new Vector2Int()
            {
                x = x,
                y = y
            };
            Cub = Hexagon.Coord.OffsetToCube(x, y);
        }

        /// <summary>
        /// 初始化单元格
        /// </summary>
        /// <param name="map">地图对象</param>
        public void Init(Map map)
        {
            Vector3 pos = Scenario.Cur.Map.Coords2Position(x, y);
            if (TerrainType.isWater)
                pos.y = MapRender.Instance.mapGrid.GetGridWaterHeight(x, y);
            else
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

        /// <summary>
        /// 检查部队是否可以通过该单元格
        /// </summary>
        /// <param name="troops">部队</param>
        /// <returns>是否可以通过</returns>
        public bool CanPassThrough(Troop troops)
        {
            return (this.troop == null || this.troop.mBelongForce == troops.mBelongForce) &&
                         (this.building == null || this.building.mBelongForce == troops.mBelongForce);
        }
        /// <summary>
        /// 检查部队是否可以移动到该单元格
        /// </summary>
        /// <param name="troops">部队</param>
        /// <returns>是否可以移动</returns>
        public bool CanMove(Troop troops)
        {
            return TerrainType != null && TerrainType.CanMoveBy(troops);
        }
        /// <summary>
        /// 检查部队是否可以停留在该单元格
        /// </summary>
        /// <param name="troops">部队</param>
        /// <returns>是否可以停留</returns>
        public bool CanStay(Troop troops)
        {
            return this.troop == null && this.building == null && CanMove(troops);
        }
        /// <summary>
        /// 检查单元格是否为空
        /// </summary>
        /// <returns>是否为空</returns>
        public bool IsEmpty()
        {
            return this.troop == null && this.building == null;
        }

        /// <summary>
        /// 获取偏移后的单元格
        /// </summary>
        /// <param name="offsetX">X偏移量</param>
        /// <param name="offsetY">Y偏移量</param>
        /// <returns>偏移后的单元格</returns>
        public Cell OffsetCell(int offsetX, int offsetY)
        {
            return Scenario.Cur.Map.GetCell(x + offsetX, y + offsetY);
        }
        /// <summary>
        /// 计算到另一个单元格的距离
        /// </summary>
        /// <param name="other">另一个单元格</param>
        /// <returns>距离</returns>
        public int Distance(Cell other)
        {
            return Cub.Distance(other.Cub);
        }
        /// <summary>
        /// 获取环形区域的单元格
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="list">单元格列表</param>
        public void Ring(int radius, List<Cell> list)
        {
            Scenario.Cur.Map.GetRing(this, radius, list);
        }
        /// <summary>
        /// 对环形区域的单元格执行操作
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="action">操作</param>
        public void Ring(int radius, Action<Cell> action)
        {
            Scenario.Cur.Map.RingAction(this, radius, action);
        }
        /// <summary>
        /// 对螺旋区域的单元格执行操作
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="action">操作</param>
        public void Spiral(int radius, Action<Cell> action)
        {
            Scenario.Cur.Map.SpiralAction(this, radius, action);
        }

        /// <summary>
        /// 对螺旋区域的单元格执行操作
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="action">操作</param>
        public bool SpiralHasBuilding(int radius)
        {
            for (int i = 1; i <= radius; i++)
            {
                Cell cell = this.Neighbors[4];
                for (int j = 1; j < i; j++)
                {
                    Cell temp = cell.Neighbors[4];
                    if (cell == null)
                        break;
                    cell = temp;
                }

                int dir = 0;
                for (int j = 0; j < 6; j++)
                {
                    if (cell == null)
                        break;
                    int dir_i = dir + j;
                    if (dir_i < 0)
                    {
                        dir_i += 6;
                    }
                    else if (dir_i > 5)
                    {
                        dir_i -= 6;
                    }
                    for (int k = 0; k < i; k++)
                    {
                        if (cell.building != null)
                            return true;
                        cell = cell.Neighbors[dir_i];
                        if (cell == null)
                            break;
                    }
                }
            }

            return false;
        }



        /// <summary>
        /// 对螺旋区域的单元格执行操作
        /// </summary>
        /// <param name="radius">半径</param>
        /// <param name="action">操作</param>
        public bool SpiralHasBuilding(int radius, System.Func<BuildingBase, bool> checkFunc)
        {
            for (int i = 1; i <= radius; i++)
            {
                Cell cell = this.Neighbors[4];
                for (int j = 1; j < i; j++)
                {
                    Cell temp = cell.Neighbors[4];
                    if (cell == null)
                        break;
                    cell = temp;
                }

                int dir = 0;
                for (int j = 0; j < 6; j++)
                {
                    if (cell == null)
                        break;
                    int dir_i = dir + j;
                    if (dir_i < 0)
                    {
                        dir_i += 6;
                    }
                    else if (dir_i > 5)
                    {
                        dir_i -= 6;
                    }
                    for (int k = 0; k < i; k++)
                    {
                        if (cell.building != null)
                        {
                            if (checkFunc(cell.building))
                                return true;
                        }
                        cell = cell.Neighbors[dir_i];
                        if (cell == null)
                            break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 沿方向线对单元格执行操作
        /// </summary>
        /// <param name="to">目标单元格</param>
        /// <param name="length">长度</param>
        /// <param name="action">操作</param>
        public void DirectionLine(Cell to, int length, Action<Cell> action)
        {
            int dir = Cub.DirectionTo(to.Cub);
            if (length < 0)
            {
                dir += 3;
                while (dir > 5)
                    dir -= 6;
            }
            Hex start = Cub;
            int absLength = Math.Abs(length);
            for (int i = 0; i < absLength; ++i)
            {
                start = start.Neighbor(dir);
                Cell cell = Scenario.Cur.Map.GetCell(start);
                if (cell != null && action != null)
                    action(cell);
            }
        }

        /// <summary>
        /// 获取方向线上的单元格
        /// </summary>
        /// <param name="dir">方向</param>
        /// <param name="length">长度</param>
        /// <param name="action">单元格列表</param>
        public void GetDirectionLine(int dir, int length, List<Cell> action)
        {
            Cell dest = this;
            for (int i = 0; i < length; ++i)
            {
                dest = dest.GetNeighbor(dir);
                if (dest != null && action != null)
                    action.Add(dest);
            }
        }

        /// <summary>
        /// 沿方向线对单元格执行操作
        /// </summary>
        /// <param name="dir">方向</param>
        /// <param name="length">长度</param>
        /// <param name="action">操作</param>
        public void DirectionLine(int dir, int length, Action<Cell> action)
        {
            Cell dest = this;
            for (int i = 0; i < length; ++i)
            {
                dest = dest.GetNeighbor(dir);
                if (dest != null && action != null)
                    action(dest);
            }
        }

        /// <summary>
        /// 获取方向线的有效终点
        /// </summary>
        /// <param name="dir">方向</param>
        /// <param name="length">长度</param>
        /// <returns>终点单元格</returns>
        public Cell DirectionLineValidEnd(int dir, int length)
        {
            Cell dest = this;
            for (int i = 0; i < length; ++i)
                dest = dest.GetNeighbor(dir);
            return dest;
        }

        /// <summary>
        /// 获取到另一个单元格的方向
        /// </summary>
        /// <param name="to">目标单元格</param>
        /// <returns>方向</returns>
        public int DirectionTo(Cell to)
        {
            return Cub.DirectionTo(to.Cub);
        }

        /// <summary>
        /// 获取指定方向的邻居单元格
        /// </summary>
        /// <param name="dir">方向</param>
        /// <returns>邻居单元格</returns>
        public Cell GetNeighbor(int dir)
        {
            while (dir < 0)
                dir += 6;
            while (dir >= 6)
                dir -= 6;
            return Neighbors[dir];
        }

        /// <summary>
        /// 获取所有邻居单元格
        /// </summary>
        /// <param name="predicate">可选的条件过滤器</param>
        /// <returns>邻居单元格列表</returns>
        public List<Cell> GetNeighbors(Func<Cell, bool> predicate = null)
        {
            List<Cell> neighbors = new List<Cell>();
            for (int i = 0; i < 6; i++)
            {
                Cell neighbor = Neighbors[i];
                if (neighbor != null)
                {
                    if (predicate == null || predicate(neighbor))
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }
            return neighbors;
        }

        /// <summary>
        /// 获取所有邻居单元格
        /// </summary>
        /// <param name="action">对每个邻居执行的操作</param>
        public void GetNeighbors(Action<Cell> action)
        {
            for (int i = 0; i < 6; i++)
            {
                Cell neighbor = Neighbors[i];
                if (neighbor != null)
                {
                    action(neighbor);
                }
            }
        }

        /// <summary>
        /// 检查是否具有指定的网格状态
        /// </summary>
        /// <param name="state">网格状态</param>
        /// <returns>是否具有指定状态</returns>
        public bool HasGridState(MapGrid.GridState state)
        {
            int stateValue = (1 << (int)state);
            return (terrainState & stateValue) == stateValue;
        }

        /// <summary>
        /// 创建内政模型
        /// </summary>
        public void CreateInteriorModel()
        {
            if (interiorModel == null)
            {
                interiorModel = MapObject.Create($"内政地{x}-{y}");
                interiorModel.objType = 0;
                interiorModel.modelId = 0;
                interiorModel.modelAsset = $"Assets/Model/Prefab/4622.prefab";
                interiorModel.transform.position = Position + new Vector3(0, 0.7f, 0);
                interiorModel.transform.rotation = Quaternion.Euler(new Vector3(0, GameRandom.Range(0, 10) * 90, 0));
                interiorModel.transform.localScale = Vector3.one;
                interiorModel.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
                MapRender.Instance.AddStatic(interiorModel);
            }
        }

        /// <summary>
        /// 清除内政模型
        /// </summary>
        public void ClearInteriorModel()
        {
            if (interiorModel != null)
            {
                interiorModel.Clear();
                interiorModel = null;
            }
        }

        /// <summary>
        /// 设置内政模型的可见性
        /// </summary>
        /// <param name="b">是否可见</param>
        public void SetInteriorModelVisible(bool b)
        {
            if (interiorModel != null)
            {
                interiorModel.gameObject.SetActive(b);
            }
        }

        /// <summary>
        /// 获取顶点数据
        /// </summary>
        /// <param name="vectors">顶点数组</param>
        public void GetVertexs(Vector3[] vectors)
        {
            MapRender mapRender = MapRender.Instance;
            int x_start = x * 4;
            int y_start = y * 4 - (y % 2) * 2;

            for (int j = 0; j < 5; j++)
            {
                for (int i = 0; i < 5; i++)
                {
                    int vIndex = j * 5 + i;
                    MapData.VertexData vertexData = mapRender.mapData.GetVertexData(x_start + i, y_start + j);
                    Vector3 v = vectors[vIndex];
                    v.y = vertexData.position.y;
                    vectors[vIndex] = v;
                }
            }
        }

        public bool IsQRSLine(Cell cell)
        {
            Hex otherCub = cell.Cub;
            return (Cub.q == otherCub.q && Cub.s != otherCub.s && Cub.r != otherCub.r) ||
                (Cub.q != otherCub.q && Cub.s == otherCub.s && Cub.r != otherCub.r)
                || (Cub.q != otherCub.q && Cub.s != otherCub.s && Cub.r == otherCub.r);
        }
    }
}
