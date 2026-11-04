using Newtonsoft.Json;
using Sango.Hexagon;
using Sango.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 地图类，管理游戏地图的加载、创建和路径查找等功能
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Map
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
            // 加载失败、或存档反序列化只留下空壳时 CellSet 可能为 null，
            // 这里必须判空：否则剧本收尾链路（Scenario.Clear → Map.Clear）会再抛一次空引用
            if (CellSet != null)
            {
                CellSet.Clear();
                CellSet = null;
            }
        }

        /// <summary>
        /// 兜底构建六边形世界
        ///
        /// 只在 <see cref="Load"/> 无法解析地图文件（文件缺失 / 版本过低）时调用。
        /// HexWorld 是 <see cref="GetCell(UnityEngine.Vector3)"/> 的必要前提，缺了它地图交互会每帧空引用，
        /// 这里刻意只补 HexWorld、不重建 CellSet —— 读档时 CellSet 可能已由存档 JSON 反序列化出来，
        /// 重建会把上层已挂接的数据一起丢掉。
        /// </summary>
        void EnsureHexWorld()
        {
            if (HexWorld != null) return;
            if (Width <= 0 || Height <= 0)
            {
                Sango.Log.Error("地图尺寸未知(Width/Height 为 0)，无法构建六边形世界，地图交互将不可用");
                return;
            }

            float gridSize = GridSize > 0 ? GridSize : 20f;
            // 不改变任何已有数据，只补上坐标换算所需的世界对象
            HexWorld = new Hexagon.HexWorld(new Hexagon.Point(gridSize, gridSize), new Hexagon.Point(0, 0));
        }

        /// <summary>
        /// 加载地图
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public void Load(Scenario scenario)
        {
            string mapName = scenario.Info.mapType;
            FileName = Path.FindFile($"Map/{mapName}.bin");
            if (!File.Exists(FileName))
            {
                // 过去这里是静默返回：Map 非 null 但 HexWorld 永远为 null，
                // 之后每一帧的地图交互都会空引用，而且日志里没有任何线索
                Sango.Log.Error($"地图文件不存在:Map/{mapName}.bin，地图逻辑数据不可用");
                EnsureHexWorld();
                return;
            }

            Name = mapName;
            // 用 using 保证任何提前返回（例如版本过低）都能释放文件句柄
            using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                int versionCode = reader.ReadInt32();
                if (versionCode < 6)
                {
                    Sango.Log.Error($"地图文件版本过低:{versionCode}(需要>=6)，地图逻辑数据不可用");
                    EnsureHexWorld();
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
            // CellSet 可能为 null：地图文件缺失（Load 走 EnsureHexWorld 兜底，不建 CellSet）、
            // 被 Clear 之后、或剧本编辑器加载早期地图尚未就绪。
            // 与 GetCell(Vector3) 保持同一约定：拿不到就返回 null，调用方自行判空。
            if (CellSet == null)
                return null;
            return CellSet.GetCell(x, y);
        }
        /// <summary>
        /// 根据立方体坐标获取单元格
        /// </summary>
        /// <param name="cub">立方体坐标</param>
        /// <returns>单元格对象</returns>
        public Cell GetCell(Hexagon.Hex cub)
        {
            if (CellSet == null)
                return null;
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
            // 分帧加载的前几帧 HexWorld / CellSet 还没建好，此时若被射线命中会直接空引用，
            // 统一在这里兜住，调用方只需对返回值判空（约定见 GetNeighbor）
            if (HexWorld == null || CellSet == null)
                return null;
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
            // cell 为空时统一返回 null，调用方需自行判空
            if (cell == null)
                return null;
            return cell.GetNeighbor(dir);
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
        /// 检查目标格是否处于 ZOC（敌方控制区域）内。
        ///
        /// 【对齐原版】Open311 `System::is_zoc_pos`（s11_sys_unit_path.cpp:345-367）：
        ///   只看目标格的 6 个邻格，**先判建筑、没有再判部队**；
        ///   建筑条件为 `building->is_completed() and self->is_enemy(building)`，
        ///   部队条件为 `self->is_enemy(unit)`。
        ///   而 `is_enemy` 对**无主对象**恒为 false：
        ///     · `System::is_enemy(a, b)`（s11_sys_scen_force.cpp:881-892）：任一 force 不存在 → false；
        ///     · `Force::is_enemy(force_id)`（s11_scen_obj_force.cpp:571-581）：`force_id < 0`（中立）→ false。
        ///   所以**中立建筑、中立部队都不产生 ZOC**。
        ///
        /// 【本项目曾经的偏差】此前用 `troops.BelongForce != next.building.BelongForce` 判"势力不同"，
        ///   而中立建筑的 BelongForce 为 null（见 Building.OnCompleted / GovernanceValidator 的
        ///   "建成即中立"），null 与任何势力都"不等"，于是陷阱 / 土垒 / 箭楼这类中立设施
        ///   也会造成 ZOC —— 表现为移动范围一进到它旁边就被一次吃光移动力。
        ///   注意不能改用 `SangoObject.IsEnemy`：它在任一方为 null 时返回 **true**
        ///   （SangoObject.cs:304-309，为"中立建筑可被攻击"而设），同样会把中立当敌人。
        ///
        /// 【未对齐】原版还要求建筑 `is_completed()`（建造中不产生 ZOC），本项目未加该判断。
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
                if (next == null)
                    continue;

                // 与原版同序：建筑优先，其次部队
                if (next.building != null)
                {
                    if (IsHostileForce(troops.BelongForce, next.building.BelongForce))
                        return true;
                }
                else if (next.troop != null)
                {
                    // 混乱部队不产生 ZOC —— 原版 `is_zoc_pos`：
                    //   `if (unit->get_state() != UnitState_Konran and self->is_enemy(unit))`
                    //   （s11_sys_unit_path.cpp:359-364）。
                    // 状态位由 BuffManager 在 Buff 增删时重算（Buff.noZoc → TroopStateFlag.Chaos），
                    // 这里只读一个 bit，避免在最热的寻路循环里遍历 Buff 列表。
                    if (!next.troop.isChaos && IsHostileForce(troops.BelongForce, next.troop.BelongForce))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// ZOC 专用的敌对判定 —— **中立（无主）不算敌人**。
        ///
        /// 【为什么不能直接用 SangoObject.IsEnemy】
        ///   `IsEnemy(Force, Force)`（SangoObject.cs:304-309）在任一势力为 null 时返回 true，
        ///   这是为"中立建筑可被攻击"（原版 `Unit::can_attack`：`force_id &lt; 0 or is_enemy`，
        ///   s11_scen_obj_unit.cpp:342-354）而设的语义；直接拿来做 ZOC 会把中立建筑算成敌人。
        ///
        /// 【这里对齐的是】`Force::is_enemy(force_id)`（s11_scen_obj_force.cpp:571-581）：
        ///   `force_id >= 0`（有主）且不是自己、非同盟、非停战，才算敌对。
        ///   同盟 / 停战不产生 ZOC 也来自同一处（原版 `is_enemy` 内含 `is_ally` 与 `ceasefire` 检查）。
        /// </summary>
        /// <param name="self">部队所属势力</param>
        /// <param name="other">邻格上的部队 / 建筑所属势力</param>
        /// <returns>是否敌对（会构成 ZOC）</returns>
        static bool IsHostileForce(Force self, Force other)
        {
            if (self == null || other == null) return false;    // 中立 / 无主：不产生 ZOC
            if (self == other) return false;                    // 同势力
            if (self.IsAlliance(other)) return false;           // 同盟
            if (self.IsTruce(other)) return false;              // 停战期
            return true;
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
            // 【性能】原实现是 cub.Ring(radius, (c => { … }))：lambda 捕获了 this / radius / checkMoveAble / cellList，
            // 每调用一次就现分配一个闭包对象 + 一个 Action<Hex> 委托（Profiler: Map.GetRing 168 B/次）。
            // 而 GetRing 在部队 AI 的目标扫描里被反复调用 —— SkillInstance.GetSpellRange 一次就要 2 个环（336 B），
            // TroopAIUtility.PriorityAction 单帧 132.6 KB GC 里绝大部分来自这里。
            //
            // 改法：先把环上坐标收进复用的 Hex 列表，再逐个映射成 Cell，全程零分配。
            // 注意两点等价性：
            //   ① Hex.Ring 的 Action 重载默认 startDir = 3，List 重载默认是 4，这里必须显式传 3，否则起点不同；
            //   ② radius == 0 时 Action 重载会把自身作为唯一元素回调，List 重载则什么都不加，这里单独补上。
            if (radius == 0)
            {
                Cell self = GetCell(cub);
                if (self != null && (!checkMoveAble || self.moveAble))
                    cellList.Add(self);
                return;
            }

            ringHexBuffer.Clear();
            cub.Ring(radius, ringHexBuffer, 3);
            for (int i = 0; i < ringHexBuffer.Count; i++)
            {
                Cell find = GetCell(ringHexBuffer[i]);
                if (find != null && (!checkMoveAble || find.moveAble))
                    cellList.Add(find);
            }
        }

        /// <summary>
        /// <see cref="GetRing(Hexagon.Hex, int, List{Cell}, bool)"/> 的环形坐标中转缓冲。
        ///
        /// 提成静态字段是为了复用内部数组容量 —— 该方法在部队 AI 的每回合目标扫描里
        /// 会被调用上百次，若每次都新建 List 会产生与闭包同量级的 GC。
        /// 单线程回合制下不存在并发访问，且每次使用前都会 Clear()。
        /// </summary>
        static readonly List<Hexagon.Hex> ringHexBuffer = new List<Hexagon.Hex>();

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

            // 邻居可能为 null（地图边界），必须先判空再向外推进，否则空引用
            Cell cell = GetNeighbor(start, startDir);
            for (int i = 1; i < radius; i++)
            {
                if (cell == null)
                    return;
                cell = GetNeighbor(cell, startDir);
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
                    if (cell == null)
                        break;
                    action?.Invoke(cell);
                    cell = GetNeighbor(cell, dir_i);
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
            if (start == null || cellList == null)
                return;
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
            troops.cell._depth = 0;
            while (frontier.Count > 0)
            {
                Cell current = frontier.Dequeue();
                if (current._isZOC)
                    continue;

                // 【深度上限】性能保底：移动范围是逐层展开，深度越大格子越多。
                // 设 20 跳（原版是 10）。取 20 的理由：
                //   · 项目的高机动部队（骑兵+加成）移动力常超过 10，硬限 10 会让它"走不远"，
                //     而原版没有这个问题（它的移动力本身就只有几格）；
                //   · 20 跳足以覆盖正常作战范围，同时挡住"移动力异常大"时的爆炸式展开。
                if (current._depth >= MaxMoveRangeDepth)
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
                            {
                                // ZOC 格：一次性吃掉该地形（水/陆）的全部移动力 —— 原版行为
                                new_cost = moveAttr;
                            }
                            else
                            {
                                // 【代价口径对齐原版 get_range】原版移动范围只算地形消耗
                                // （get_terrain_cost，s11_sys_unit_path.cpp:231），友方建筑格
                                // 不额外 +16 —— 那个 +16 只在 get_path（寻路）里用（同文件 :146-147）。
                                // 之前误用了 PathAlignment.GetStepCost（含友方建筑 +16 / 敌军 +32 / ZOC），
                                // 导致部队出城经过自己的城格被 +16 吃掉移动力而卡在城头。
                                // 敌军阻挡由展开条件里的 CanPassThrough 把关，无需在移动范围再加代价。
                                new_cost = cost_current + troops.MoveCost(next);
                            }

                            if (new_cost > moveAttr)
                                continue;

                            next._cost = new_cost;
                            next._isZOC = isZoc;
                            next._isChecked = true;
                            next._depth = current._depth + 1;

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
                cell._depth = 0;
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
            // 【性能】原实现每次调用都现建一个捕获 troop 的闭包 + 一个 CellCheck 委托
            // （Profiler: Map.GetMovePath 152 B/次）。GetMovePath 在部队移动与 AI 试走里是高频调用，
            // 这里把 troop 放进字段、过滤器缓存成实例委托，全程只在首次调用时分配一次。
            if (movePathFilter == null)
                movePathFilter = MovePathCellCheck;

            movePathFilterTroop = troop;
            GetDirectPath(troop.cell, dest, cellList, movePathFilter);
            movePathFilterTroop = null;
        }

        /// <summary>
        /// <see cref="GetMovePath"/> 复用的过滤委托（绑定到本 Map 实例，只分配一次）。
        /// </summary>
        CellCheck movePathFilter;

        /// <summary>
        /// <see cref="GetMovePath"/> 当前正在过滤的部队 —— 用来把 troop 传给缓存的委托，
        /// 避免每次寻路都新建闭包。
        /// </summary>
        Troop movePathFilterTroop;

        /// <summary>
        /// <see cref="GetMovePath"/> 的通行判定：目标格可进入、可穿过、且在部队移动范围内。
        /// 与原内联 lambda 的条件、求值顺序完全一致。
        /// </summary>
        bool MovePathCellCheck(Cell next)
        {
            Troop troop = movePathFilterTroop;
            return next.CanMove(troop) && next.CanPassThrough(troop) && troop.MoveRange.Contains(next);
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
        /// 寻路容器的预设容量。
        ///
        /// 【为什么需要】下面四个容器是"每次寻路 Clear() 后重填"的复用字段，Clear 会保留容量，
        /// 所以扩容只在"本次搜索触及的格子数创了新高"时才发生 —— 但那一刻是**连续翻倍**：
        /// 3→7→17→…，每翻一次都要新分配一个桶/条目数组。单次远距离寻路（上千格）就能翻出
        /// 上百 KB（Profiler: Map.GetMinCostDirectPath 单次 182.6 KB，全部来自
        /// Dictionary.set_Item ×3 与 HashSet.Add ×2）。
        /// 一次性给足容量后，整局都在同一个容量上跑，不再产生扩容分配。
        /// 1024 覆盖绝大多数寻路（部队移动范围通常只有几十到几百格）。
        /// </summary>
        const int PathfindCapacity = 1024;

        /// <summary>
        /// 路径来源字典
        /// </summary>
        Dictionary<Cell, Cell> came_from = new Dictionary<Cell, Cell>(PathfindCapacity);
        /// <summary>
        /// 成本字典
        /// </summary>
        Dictionary<Cell, Cell> cost_so_far = new Dictionary<Cell, Cell>(PathfindCapacity);

        /// <summary>
        /// 【最小消耗寻路】已知最优代价（cell → 累计消耗）。未出现的格子 = 尚未触及。
        /// </summary>
        Dictionary<Cell, int> minCostSoFar = new Dictionary<Cell, int>(PathfindCapacity);
        /// <summary>
        /// 【最小消耗寻路】已确定（settled）的格子。
        /// 用于"Dijkstra + 懒删除"：出队时若已确定，说明这条是更贵的历史副本，直接丢弃。
        /// </summary>
        HashSet<Cell> settledCells = new HashSet<Cell>(PathfindCapacity);

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
                    Cell next = current.GetNeighbor(i);
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
        /// **最小消耗路径**：从部队当前位置到目标格，按"部队移动消耗"最短的那条路。
        ///
        /// 【判定口径（刻意最轻）】只看**部队能不能走这个格**：
        ///   · 地形 + 该部队通行性 = <see cref="Cell.CanMove"/>（水格按水军、陆格按陆军）；
        ///   · **不看阻挡**（<c>CanPassThrough</c>：谁占着格、是谁的建筑）；
        ///   · **不看能不能停留**（<c>CanStay</c>：格上有没有部队 / 建筑）；
        ///   · **不看 ZOC**（完全不碰 <c>Map.IsZOC</c>）。
        /// 它回答的是"**这条路在地形上通不通、要多少消耗**"，不是"此刻能不能走过去"
        /// —— 后者要另外判阻挡与 ZOC（例如运输队"能不能过河"该看本方法，
        /// "路上有没有敌人挡着"另判 <see cref="Cell.CanPassThrough"/>）。
        ///
        /// 【代价】每格取 <see cref="Troop.MoveCost"/>（水格用 <c>WaterTroopType</c>、
        /// 陆格用 <c>LandTroopType</c> 的消耗），起点代价为 0。
        ///
        /// 【与 <see cref="GetDirectMovePath"/> 的区别 · 关键】
        ///   · 那个是"先到先得"的近似 A*：`if (!next._isChecked)` 就入队，**从不做代价松弛**
        ///     —— 找到的只是"第一条走通的"，不保证最小消耗（绕远路是常态）；
        ///   · 本方法用 **Dijkstra**（可对已入队格松弛代价 + 已确定的格跳过），
        ///     得到的是真正的**最小消耗路径**；因此**不加启发式**，免得启发式高估导致次优。
        ///
        /// 【终点特例】目标格本身**不判 <c>CanMove</c>** —— 目标常常是城 / 港关 / 被占格，
        /// 那些格本来就"不能停留"，但它们正是要到达的地方（与 <see cref="GetDirectMovePath"/>
        /// 里"一定是目标所占格也可以进判断"同一约定）。
        ///
        /// 【无法到达】地形上就走不通时 <paramref name="cellList"/> 为**空列表**
        /// —— 调用方可据此判定"这条路不通"，而不是像旧实现那样当成"移动完成"。
        /// </summary>
        /// <param name="troops">部队（决定可走性与每格消耗）</param>
        /// <param name="dest">目标格</param>
        /// <param name="cellList">输出：起点 → 目标 的路径（含两端）；**地形上不可达时为空**</param>
        public void GetMinCostDirectPath(Troop troops, Cell dest, List<Cell> cellList)
        {
            if (troops == null)
                return;
            GetMinCostDirectPath(troops, troops.cell, dest, cellList);
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
                            Cell next = current.GetNeighbor(i);
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
                            Cell next = current.GetNeighbor(i);
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
                    // 邻居可能为空（地图边界），先判空；一定是目标所占格也可以进判断
                    if (next != null && (next == dest || (next.CanMove(troops) && (action == null || action(next)))))
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
        /// **最小消耗路径**（指定起点版）：从 <paramref name="start"/> 到 <paramref name="dest"/>。
        ///
        /// 用于"某个候选落点离目标到底要多少消耗"这类判断（起终点都不是部队当前位置时）。
        /// 判定口径与另一个重载完全一致：只看能不能走，不看阻挡 / 停留 / ZOC。
        /// </summary>
        /// <param name="troops">部队（决定可走性与每格消耗）</param>
        /// <param name="start">起点格</param>
        /// <param name="dest">目标格</param>
        /// <param name="cellList">输出：起点 → 目标 的路径（含两端）；**地形上不可达时为空**</param>
        public void GetMinCostDirectPath(Troop troops, Cell start, Cell dest, List<Cell> cellList)
        {
            if (troops == null || start == null || dest == null || cellList == null)
                return;

            cellList.Clear();
            if (start == dest)
            {
                cellList.Add(dest);
                return;
            }

            frontier.Clear();
            came_from.Clear();
            minCostSoFar.Clear();
            settledCells.Clear();

            // 【性能 · A* 启发式】h(n) = 六边形格距 × 每步移动消耗的下界。
            //   · 下界取该部队水 / 陆两张地形消耗表里的最小值 → h 永远不会高估真实代价（可采纳）；
            //   · 又因为"每步代价 ≥ 下界、而格距每步至多变 1"，h 满足一致性条件 ——
            //     所以 A* 给出的仍是与纯 Dijkstra 完全一致的最小消耗路径，只是把搜索收拢到目标方向。
            //   · 下界为 0（配置缺表 / 含 0 消耗地形）时 h 恒为 0，自动退化成原来的纯 Dijkstra。
            int stepCostFloor = MoveCostFloor(troops);

            minCostSoFar[start] = 0;
            came_from[start] = null;
            frontier.Enqueue(start, stepCostFloor * Distance(start, dest));

            int safe_count = Scenario.Cur.Variables.pathfindingSafeCount;
            bool found = false;

            while (frontier.Count > 0)
            {
                Cell current = frontier.Dequeue();
                if (!settledCells.Add(current))
                    continue;                                   // 迟到副本（更贵的旧记录）→ 丢弃
                if (current == dest)
                {
                    found = true;
                    break;                                      // Dijkstra：目标出队即最优
                }

                int cur_cost;
                if (!minCostSoFar.TryGetValue(current, out cur_cost))
                    continue;

                for (int i = 0; i < 6; i++)
                {
                    Cell next = GetNeighbor(current, i);
                    if (next == null)
                        continue;
                    if (settledCells.Contains(next))
                        continue;                               // 已确定 → 不再松弛
                    // 只判"部队能不能走这格"；目标格例外（城 / 港关 / 被占格本就不能停留）
                    if (next != dest && !next.CanMove(troops))
                        continue;

                    int new_cost = cur_cost + troops.MoveCost(next);
                    int old_cost;
                    if (minCostSoFar.TryGetValue(next, out old_cost) && old_cost <= new_cost)
                        continue;                               // 已有更便宜或同样便宜的路 → 不松弛

                    minCostSoFar[next] = new_cost;
                    came_from[next] = current;
                    // f = g + h；h 为 0 时即纯 g，与旧行为完全一致
                    frontier.Enqueue(next, new_cost + stepCostFloor * Distance(next, dest));

                    if (--safe_count < 0)
                    {
                        UnityEngine.Debug.LogError(string.Format(
                            "最小消耗寻路超出安全次数: [{0}, <{1},{2}> => <{3},{4}>]",
                            troops.Name, start.x, start.y, dest.x, dest.y));
                        return;                                 // cellList 保持为空
                    }
                }
            }

            if (!found)
                return;                                         // 地形上不可达 → 空列表（调用方可据此判定"过不去"）

            // 【性能】原先每步 Insert(0) 都要整体搬移已有元素（路径长 n → O(n²) 拷贝），
            // 改为尾部追加后一次 Reverse。
            Cell c = dest;
            while (c != null)
            {
                cellList.Add(c);
                if (!came_from.TryGetValue(c, out Cell parent))
                    break;
                c = parent;
            }
            cellList.Reverse();
        }

        /// <summary>
        /// 【最小消耗寻路的 A* 启发式系数】"每步移动消耗"的下界。
        ///
        /// 取该部队水 / 陆两张地形消耗表里的最小值；两张表都不可用时返回 0。
        /// 返回 0 表示给不出正的下界（配置缺失，或地形消耗表里存在 0），
        /// 调用方据此退化为纯 Dijkstra —— 正确性永远不受影响，只是少了剪枝。
        /// </summary>
        /// <param name="troops">部队</param>
        /// <returns>每步消耗下界（≥ 0）</returns>
        static int MoveCostFloor(Troop troops)
        {
            if (troops == null)
                return 0;

            int min = int.MaxValue;
            int land = MinMoveCostOf(troops.LandTroopType);
            int water = MinMoveCostOf(troops.WaterTroopType);
            if (land < min) min = land;
            if (water < min) min = water;

            if (min == int.MaxValue || min < 0)
                return 0;
            return min;
        }

        /// <summary>
        /// 取某兵种地形消耗表里"可通行地形"的最小消耗值；表缺失时返回 <see cref="int.MaxValue"/>。
        ///
        /// 【必须只统计正消耗】表里的负值（-1）表示该地形**不可通行**，不是"零成本/负成本"。
        /// 若把它当成下界，h 会高估真实代价、破坏可采纳性，A* 就可能返回非最优路径。
        /// </summary>
        /// <param name="type">兵种（可为 null）</param>
        /// <returns>可通行地形的最小消耗；无可通行地形时返回 <see cref="int.MaxValue"/></returns>
        static int MinMoveCostOf(TroopType type)
        {
            if (type == null || type.moveCost == null || type.moveCost.Length == 0)
                return int.MaxValue;

            int min = int.MaxValue;
            for (int i = 0; i < type.moveCost.Length; i++)
            {
                int cost = type.moveCost[i];
                if (cost > 0 && cost < min)
                    min = cost;
            }
            return min;
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
