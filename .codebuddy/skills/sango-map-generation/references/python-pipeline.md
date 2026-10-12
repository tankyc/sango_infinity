# Python 转换管线：Koei 311 世界图 → Sango `.bin`

本文件是 `sango-map-generation` 技能的参考材料，记录**离线 Python 管线**的做法与实测结论。
引擎自带的程序化生成器（`MapGenContext` / `MapGenerator` / `MapApi`）见 `SKILL.md`；
本文件讲的是另一条路：把已有的 311 世界地图数据转成 Sango 的地图文件。

- 工具目录：`C:\Users\Administrator\Downloads\san11-world-main\san11-world-main`
- 数据目录：`data/sango_full/`（DS=1，1800×1000 格 / 7201×4001 顶点 / 500 城）；`data/sango/` 是 DS=2 的 900×500 版
- 产物：`<sango_infinity>/Build/Content/Map/world_full.bin` + `Assets/Map/world_full/BaseTex/`

---

## 1. 运行顺序

每一步都可单独重跑，且都是**就地写回** `data/sango_full/`（各自留 `*_orig.npy` 备份）。

| # | 脚本 | 作用 |
|---|---|---|
| 1 | `sango_convert.py --ds 1 --out data\sango_full` | 世界图原始数组 → `terrain / vheight / vtex / vwater .npy` + `cities.json` + `meta.json` |
| 2 | `sango_fix_roads.py --fix` | 相邻城市不通则切最省路径：跨川(7)→渡所(13)、跨崖(16)→栈道(12) |
| 3 | `sango_sea_zones.py --src data\sango_full` | 海域分档（近海9 可走；远海21 / 深海22 不可走）+ 可行走孤岛桥接/封锁 |
| 4 | `sango_map_fixes.py --src data\sango_full` | 城市 7 格平整化+陆地化、内政地生成、可通行格高度平滑 |
| 5 | `sango_basemap.py --src data\sango_full --out ...\BaseTex --mode gamut --shade 0.85` | 四季底图 |
| 6 | `sango_build_bin.py --src data\sango_full --out ...\world_full.bin` | 写 `.bin`：自己写 mapGrid + mapData 段，尾部段抄 DefaultMap 并置空模型/标注 |

顺序要点：**3 必须在 4 之前**（内政地候选要避开远海/深海）；**4 必须在 5、6 之前**（底图与 `.bin` 都基于最终地形）。
2 应排在 3 之前（修路会改地形，进而影响可行走连通性）。

`--report`（`sango_map_fixes.py` / `sango_sea_zones.py`）只体检不写盘，改参数时先用它。

---

## 2. `.bin` 分段字节格式（version 10）

`MapRender.LoadMap` 的读取顺序（与 `MapRender.VERSION = 10` 对应）：

```
int32 versionCode
[versionCode>=6] string WorkContent            ← 7-bit 长度前缀 + UTF8（Unity BinaryWriter）
int32 mapWidth, int32 mapHeight                ← 顶点尺寸，见下
[versionCode>5]  mapGrid.OnLoad
                 mapData.OnLoad
   ── 以下为「尾部段」，sango_build_bin.py 直接从 DefaultMap.bin 抄字节 ──
                 mapLayer / mapTerrain / mapLight / mapFog / mapBaseColor /
                 mapSkyBox / mapCamera / mapModels / mapLabelSet
[versionCode<=5] mapGrid.OnLoad
```

### 2.1 mapGrid

```
int32 gridSize                    ← 引擎固定 20
每格 7 字节（x 外层、y 内层）：
    byte   terrainType
    int32  terrainState           ← 小端，字节 1..4
    uint16 areaId                 ← 小端，字节 5..6
string gridTextureName            ← 一般 "grid"
格数 = mapWidth * 5 / gridSize = mapWidth / 4
```

`terrainState` 位（`MapGrid.SetGridState` 用 `1 << (int)state`）：

| GridState | 值 | 位 |
|---|---|---|
| Defence | 1 | `1<<1` = **2** |
| **Interior（内政）** | 2 | `1<<2` = **4** |
| Thief | 3 | `1<<3` = **8** |

### 2.2 mapData

```
int32 quadSize                    ← 5
逐顶点 3 字节（x 外层、y 内层）：height, textureIndex, water
顶点数 = (mapWidth+1) × (mapHeight+1)
```

**`mapWidth / mapHeight` 是「顶点数 - 1」，不是像素、不是格数。** 例：`mapWidth=7200` → 格数 7200/4=1800 → 顶点 7201×4001。

### 2.3 尾部段（照抄 DefaultMap 时必须改的两处）

`mapModels` 与 `mapLabelSet` **必须写成 0 个**。DefaultMap 自带 2352 个模型 + 177 个标注，
挂到别的尺寸的地图上会让 `MapModels.OnLoad` 崩（`MapObject.position` 空引用 / 模型越界）。
其余段（layer/terrain/light/fog/skybox/camera）原样保留即可，观感与 DefaultMap 一致。

单模型 52 字节（含 v>=4 的 bindId）；单标注 19 字节 + 字符串。

---

## 3. 运行时地格数据从哪来（最重要的认知）

**地格只在地图 `.bin` 里，不在剧本里。** 链路：

```
剧本.Info.mapType
  → Map/<mapType>.bin 的 mapGrid（terrainType + terrainState + areaId）
  → CellSet.SetTerrainTypeAndState(x, y, terrainType, terrainState, city)
  → city.AddAreaCell(cell)                     ← 建立 City.areaCellList
  → City.OnScenarioPrepare(scenario)           ← 遍历 areaCellList
      · HasGridState(Defence) → defenceCellList
      · IsInterior            → interiorCellList（若 BelongCity != null 则转给母城）
  → City.InteriorCellCount = interiorCellList.Count   ← UI 的「建 x/y」槽位数
```

- 剧本 JSON 的顶层键只有 `Id / Name / Info / citySet / corpsSet / forceSet / personSet`，
  **没有地格数据** → 改地格只需要重生成 `.bin`，不必重生成剧本。
- `City.AddAreaCell` 会**同时**加到自己和 `BelongCity`（港/关的格子因此也会进母城）。
- 港(18)/关所(19) 不建内政，它们的 `interiorCellList` 为 0 是正常的。

### 3.1 内政地生效的双条件

```csharp
public bool IsInterior => HasGridState(MapGrid.GridState.Interior);   // terrainState 第 2 位
// City.OnScenarioPrepare 里：
for (int i = 0; i < areaCellList.Count; i++)
    if (areaCellList[i].IsInterior) interiorCellList.Add(areaCellList[i]);
```

所以内政格必须**同时**满足：

1. 该格 `terrainState` 置 **4**；
2. 该格 `areaId` **等于那座城的 id**（`areaCellList` 是从「本城 areaId 的格子」里筛出来的）。

只写第一条 → 城市建筑槽位仍然是 0。只写第二条 → 格子归了城但不是内政地。

---

## 4. 面积分配（areaId）的正确做法

`Map.cs` 里有一句被注释掉的诊断，正是验收标准：

```csharp
//if (terrainType.moveable && city == null)
//    stringBuilder.AppendLine($"地格:<{x},{y}> areaId:{areaId} 可以行走,但是所属城市找不到!!");
```

**目标是让「可行走但无所属城市」的格子数为 0。** 三条硬要求：

1. **不能把不可行走地形当墙。** 崖(16)/川(7)/岸(15) 占全图 20%+，当墙会把地图切成上千个互不相连的碎块，
   块内若没有城市，那里的可行走格永远拿不到归属。→ 改成「可以穿过、但代价高」（`BYPASS_COST = 6.0`）。
2. **不能截断搜索距离。** 旧的 `MAX_COST = 90` 会让离城偏远的内陆格（森/荒地/湿地）大片落空。
3. **海/河/浅滩 `moveable = true`**（船能走），所以它们也必须分配 areaId。把它们当墙是错的
   （实测：整片海域 79.9 万格 areaId=0）。

另外：**领地种子要包含全部城市 —— 都市 + 港(18) + 关所(19)**，且必须用**剧本里的真实城 id**
（引擎是 `scenario.citySet.Get(areaId)` 反查的）。港/关各占 1 格并拥有自己的 areaId；
DefaultMap 里 35 个港映射到 25 个不同 areaId，存在「某 areaId 的领地 152 格、含 1 个港关格、0 个都市格」这种港关自己当种子的情况。

实测收益：可行走格无归属 **1,022,725 → 0**；全图 180 万格全部有归属；耗时 ~5s。

---

## 5. 坐标与换算

### 5.1 格 ↔ 顶点

- 格 `(cx, cy)` 的高度采样顶点 = `(4*cx + 2, 4*cy + 2*(cx & 1) + 2)`（与 `MapGrid.GetGridHeight` 一致）。
- 每格覆盖 **5×5 顶点块**（4 步长）。**相邻格共享 1 列顶点** → 两个城若相邻且压到不同高度，必有一格不平。
- 六边形 odd-q，邻居偏移**依赖列奇偶**：

```python
NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}
```

### 5.2 世界图 ↔ 中国块 ↔ DefaultMap

已用襄平独立验证：

```
world_cell = floor((local + china_lo + 0.5) / DS)        # local 是「相对中国块」坐标
world_cell = DefaultMap_cell - 28 + china_lo/china_hi    # -28 = 311 可玩区在 DefaultMap 里的偏移
```

- `china_lo / china_hi` 取自 `data/world/world_info.json`（当前 `1234 / 436`）。
- 襄平：`lo=174` → `world_x = 174 + 1234 = 1408` ✓ 与 `cities.json` 一致。
- 标定物：DefaultMap 的 42 个「都市(17) 七格块」中心 ↔ `data/cities_georef.json` 的 42 座三国城
  → **平均误差 0.29 格、最大 0.29 格**。

⚠️ **`np.where` 返回 `(dim0, dim1)`，即 `(x, y)`**。写成 `(y, x)` 不会报错，但会让归属/距离整体错位
（本次踩了两次：距离复检算出「离城 1644 格」；都市块顺序搞反导致 580 格全归错城）。
另外 **`np.argwhere` 的行序是按 x 行主序**，与 `cities_georef.json` 的文件顺序不同，
必须按坐标一一配对，不能直接 `i+1` 对应。

---

## 6. DefaultMap 实测形态（城市/内政地的目标样本）

对 `DefaultMap.bin`（256×256 格，中国区）的实测：

| 项 | 实测 |
|---|---|
| 都市(17) | **7 格六边形块**（中心 + 周围 1 圈），42 个 |
| 港(18) | **1 格**，35 个 |
| 关所(19) | **1 格**，10 个 |
| 内政格（`terrainState & 4`） | **591 格**；53 座城有；每城 2~22、**中位 12** |
| 内政格地形 | **100% 是 土(2)** |
| 内政格离城 | 2~16 格（中位 6） |
| 内政格的 areaId | 全部 > 0 |

城市占地与内政地的目标形态都以这张表为准。世界图的实际产出（500 城）：
每城内政地 3~22（中位 12）、离城 3~16 格。

---

## 7. 海域分档与可行走孤岛

开放海域 `海(9)` 在 `TerrainTypes.json` 里 `moveable = true` → 单位可以横渡太平洋。分档做法：

| 分档 | 判定（到最近陆地的六边形距离 d） | 通行 |
|---|---|---|
| 海(9) 近海 | `d <= 12` | 可走（船） |
| 远海(21) | `12 < d <= 40` | **不可走** |
| 深海(22) | `d > 40` | **不可走** |

阈值依据（实测）：**`d <= 8` 是「500 座城仍在同一连通域」的临界值**，`d < 8` 会切掉 6~9 座城；
取 12 留 1.5 倍余量。分档后屏蔽掉 66.9% 的开放海域。

**分档后必然产生孤块**（封闭海 + 无城小岛；实测 1693 个块，除主块外 1692 块共 48720 格、全部无城市）。
处理策略（分级）：

- **≥ 200 格的孤块 → 从主块做最小代价 Dijkstra 桥接**：路径上的格子改成可行走地形
  （远海/深海→海9、崖→栈道12、岸→小径20、川→渡所13）。实测 50 个里 45 个成功、**总共只改写 423 格**。
- **其余（小碎点 + 桥接超代价上限的封闭海）→ 整块封锁**（水→深海22、陆→崖16），
  否则会留下船进不去、单位可能搁浅的死水域。

复检：连通块 **1693 → 1**，可走格 924,293；**35 座港六邻全部仍有可走水域（0 个死港）**。

### 7.1 新增地形必须同步改的四处

加 `远海(21) / 深海(22)` 时改了四个地方，漏一处就会出问题：

1. `Build/Content/Data/Common/TerrainTypes.json` — 条目本身（`isWater: true`、`moveable: false`）
2. `Map/Generator/TerrainIds.cs` — `FarSea = 21` / `DeepSea = 22` 常量
3. `Map/Generator/BaseMapGenerator.cs` — `BuildTerrainColor()` 里的底图颜色
   （**漏了不会报错**，会静默用默认灰 `150,150,150`）
4. `sango_basemap.py` 的 `TC` 表 — 同样的颜色

---

## 8. 顶点高度平滑（可通行格高差限制）

目标：可通行格自身的顶点高差不过大，且相邻可通行格能平滑衔接。做法是**同步（Jacobi）区间投影**：

```
每轮：lo = max(邻居) - M, hi = min(邻居) + M   → h = clip(h, lo, hi)
```

- **必须同步**（一轮内 `h` 不改）。就地 Gauss-Seidel 在邻格要求互相矛盾时会进极限环
  （实测每轮稳定摆动 240、500 轮仍有超限）。
- `M = 10` 字节（= 5 世界单位 / 20 单位格宽 ≈ 14°）。实测 `3.6% → 0.1%` 超限边。
- **海/河 + 城市格 + 内政地必须作为硬锚点**（自己不动、只约束邻居），
  否则海床会被陆地抬起来顶穿水面（水面按 `water > height` 判定，会直接消失）。
- 只有「陆地可通行格」会移动。

---

## 9. 城市与内政地的处理规则

### 9.1 城市占地

- 每城 = **中心 + 周围 1 圈 = 7 格**，统一地形 `都市(17)`，顶点 `water = 0`（陆地化）。
- 顶点高度压成一个平台：取该 7 格顶点高度的中位、夹到 `[4, 80]`。
- **必须逐格压**：只压中心格的 5×5 顶点块的话，外围一圈仍是原高度，城市不平。
- 城市格必须在平滑**之前**写进地形，否则落在海/崖上的城市格不算可通行，约束会漏掉它们。

### 9.2 内政地

- 不能落在 `都市(17)/港(18)/关所(19)` 上（它们 `canBuild=false`，会被改成 `土(2)` 而毁掉一座港）。
- **不能压在道路上**：`主径(11)` 是可建造地形里唯一的交通设施。`canBuild=true` 的四种是
  `草地(1)/土(2)/砂地(3)/主径(11)`，实际可用只有前三种。
  （改进前 6698 格里有 **2380 格压在主径上**。）
- **不能离城太近**：最小格距 3（旧版 2 紧贴城墙）。
- 相邻格若已属别城则跳过（顶点块会互相压平打架）。
- 内政地与其所属城**同平台高度**（整片城区是平的），并作为硬锚点参与平滑。
- 中国区（42 座三国城）直接采用 DefaultMap 转换过来的内政格坐标；其余城市按统计规律程序化生成。
  实测落地 585/591 格（少数因被占或在水上跳过），42 座中国城全覆盖。

---

## 10. 顶点数据 4 字节化（MapData.VertexData）

原实现里 `VertexData` 是 48 字节的 **class**，每顶点存 `height/textureIndex/water/waterIndex` + `position/waterPosition/uv/normal` 各一份。
1800×1000 图上仅这一项就要约 1.4 GB。现改为 **4 字节 struct**，只存真实数据，派生量按需计算：

| 派生量 | 计算入口 |
|---|---|
| `position` | `MapData.VertexPosition(height, vx, vy)` |
| `uv` | `MapData.VertexUV(vx, vy)` |
| `waterPosition` | `MapData.VertexWaterPosition(v, vx, vy)` |
| `normal` | `MapData.VertexNormal(v, vx, vy)` |

**注意事项：**

- `GetVertexData()` 返回的是 **struct 副本**，改它不会影响地图 → 必须用
  `SetVertexHeight / SetVertexTextureIndex / SetVertexWater` 写回；直接写 `vertexDatas[x][y].字段` 也可以（数组元素是左值）。
- 法线统一走 `MapData.VertexNormal`（与「从 `.bin` 加载」同一套算法），不要再预写。
- 加载路径不再需要两份 `VertexDataNative(48字节/顶点)` 中转数组和两个 Burst Job。

实测（1800×1000）：加载后托管堆增量 **370MB → 37MB**，瞬时峰值 ~700MB → 162MB，加载 6.3s → 1.66s。

---

## 11. 验证方法（改完必做）

### 11.1 字节级（Python 直接解析产物）

```python
# 解析 mapGrid 段，逐项断言
assert (aid[moveable_cells] == 0).sum() == 0        # 可行走格必须有 areaId
assert (st == 4).sum() == expected_interior_count   # 内政位只应有 4（2/8 是防守/贼）
assert 都市/港/关 格数 与预期一致
assert interior_owner == 剧本城 id（与 interior.npy 比对）
```

### 11.2 引擎侧（Unity MCP `execute_code`）

```csharp
Sango.Path.Init(); Sango.Mod.ModManager.Instance.Init(); Sango.Core.GameData.Instance.Init();
var sc = new Sango.Core.Scenario(@".../Scenario/Scenario_world_full.json");
sc.LoadContent();                                   // 这里才加载 Map 与地形表
var cur = Sango.Core.Scenario.Cur;
cur.citySet.ForEach(c => c.OnScenarioPrepare(cur)); // 这里才填 interiorCellList
// 断言：city.areaCellList.Count > 0、interiorCellList.Count > 0、
//       地格 TerrainType.canBuild、不在道路上、从某城格 BFS 可达格数 == 可行走格总数
```

`Map.Load(scenario)` 用的是 **`scenario.CommonData`**（每次 `LoadNewCommonData()` 新建），
而 `GameData.Instance.ScenarioCommonData` 是**按需缓存的**（`LoadCommonData()` 里有 `if (ScenarioCommonData != null) return;`）。
排查地形表改动是否生效时必须读前者，读后者会拿到旧值而误判。

---

## 12. 陷阱清单

| 现象 | 原因 |
|---|---|
| 可行走格大面积没有 areaId | 面积分配把不可行走地形当墙 / 截断了搜索距离；或海/河/浅滩被当墙 |
| 内政地生成了但槽位仍是 0 | `terrainState` 写了内政位，但 `areaId` 不是那座城 |
| 内政地挡住道路 | 内政候选没排除 `主径(11)` |
| 相邻可通行格高差压不下去 | 用了就地 Gauss-Seidel → 极限环；必须同步区间投影 |
| 水面整片消失 | 平滑时没把海/河当锚点，海床被抬高顶穿了水面 |
| 引擎读到旧地形表 | 读了缓存中的 `GameData.Instance.ScenarioCommonData`，应读 `Scenario.Cur.CommonData` |
| Python 归属/距离整体错位且不报错 | `np.where` / `np.argwhere` 返回 `(dim0, dim1)`，写成了 `(y, x)` |
| 都市块与城市名对不上 | `np.argwhere` 的 x 行主序 ≠ `cities_georef.json` 的文件顺序，需按坐标配对 |
| 新地形静默变灰 | 只改了 `TerrainTypes.json`，没改 `BaseMapGenerator.BuildTerrainColor()` 与 `sango_basemap.py` 的 `TC` |
| 加载别的尺寸地图时 `MapModels` 崩 | 尾部段照抄了 DefaultMap 的模型/标注，必须置空 |
| 城市/内政地不平 | 只压了中心格的顶点块；「1+6」七格必须逐格压 |
| 城市格没参与平滑约束 | 地形写成 `都市(17)` 的时机晚于平滑 |
