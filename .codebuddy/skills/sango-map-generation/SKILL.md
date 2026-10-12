---
name: sango-map-generation
description: "Provides knowledge and tools for building Sango Infinity maps — both the in-engine procedural generator (MapGenContext / MapGenerator / MapApi) and the offline Python pipeline that converts a Koei 311 world map into a Sango .bin. Covers terrain, rivers, city placement, territory/areaId assignment, interior land (内政地), roads, sea depth zoning, vertex-height smoothing, and four-season base maps. Use this skill when generating or regenerating a map, hand-writing or rebuilding a map .bin, assigning city territory (areaId), placing or validating interior land, zoning deep sea, smoothing vertex heights, placing cities, writing generated cities into a scenario, painting or querying map cells, or when working with Sango map textures, layers, vertex colors, height bytes, or the four-season texture system."
---

# Sango Infinity 地图构建 Skill（引擎生成器 + Python 转换管线）

## Overview

Sango Infinity 的地图由「逻辑地格层」与「渲染层（顶点/图层/底图）」共同构成。本技能封装了这两层的实测结论，
以及**两条造图路径**：

1. **引擎内置程序化生成器**（`MapGenContext` / `MapGenerator` / `MapApi`，含 MCP 工具 `sango_map`）
   —— 从零生成地形/河流/城市/领地/道路/底图。见 §二~§八。
2. **离线 Python 转换管线** —— 把已有的 Koei 311 世界图数据转成 Sango `.bin`
   （自己写 mapGrid + mapData 段）。见 §九 与 `references/python-pipeline.md`。

适用场景：程序化生成世界地图、转换既有世界图并手写 `.bin`、调整/重绘地形、选址城市并划分领地（areaId）、
生成/校验内政地、铺设道路、海域分档、顶点高度平滑、把生成结果写入剧本、
为新地图初始化四季地表贴图、排查地图渲染异常（白板/破洞/水面不显示/高度不对/城市建不了东西）。

## 何时使用

- 用户要求「生成地图 / 重做地图 / 程序化生成世界 / 换一张地图」
- 用户要求「放置城市 / 划分势力领地 / 修路」
- 用户要求把生成的城市写进剧本，或询问剧本与地图如何对应
- 涉及地图贴图图层、四季贴图、底图、顶点色、高度字节的读写与调试
- 地图出现「白板、地表破洞、水面看不见、高度整体偏移」等问题
- 用 Python 管线把世界图转成 `Map/*.bin`，或需要**手写地图 `.bin`** 的 mapGrid / mapData 段
- 涉及 `terrainState`（内政位）、`areaId`（领地归属）、城市规划槽位
- 生成/校验**内政地**（Interior），或「城市建不了东西」「领地有空白」
- 海域分档（近海/远海/深海）、可行走孤岛的处理
- 顶点高度平滑、相邻可通行格高差过大

---

## 一、必须先知道的硬事实

### 1. 尺寸关系（最容易踩坑）

| 概念 | 关系 |
|---|---|
| 新建地图窗口填的「宽/高」 | **像素尺寸，不是格数** |
| 逻辑格数 | `格数 = 尺寸 / 4`（即 `Width = mapWidth/4`） |
| 顶点网格 | `(mapWidth + 1) × (mapHeight + 1)`，即 `vertex_x_max = mapWidth + 1` |
| `MapGrid.gridSize` | 引擎自动 = **20**，**不要手改**，否则与运行时读取不一致 |
| `MapGenContext` 中 | `cellsX = mapGrid.bounds.x = mapWidth/4` |

例：填 `1024` → 256×256 格 → 顶点 1025×1025（原版 DefaultMap 就是这套参数）。

### 2. 坐标、邻居与顶点映射

- 地图是 **hex（六边形）**，odd-q 排列，邻居偏移**依赖"列"奇偶**（不是行）。
- 取邻居统一用 `HexNeighbors`（内部用 `Hexagon.Coord` 换算，与引擎一致），不要自己写偏移表。
- 格 → 格中心顶点：`vx = 4*cx + 2`，`vy = 4*cy + (cx & 1) * 2 + 2`。
- 顶点世界坐标：`position = (vy * quadSize, height * 0.5, vx * quadSize)`，`quadSize = 5`（注意 X 对应 vy、Z 对应 vx）。

### 3. 高度字节标定（原版实测）

```
海底(海) = 0    河床(川/河) = 1    浅滩 = 海平面 10    水面(water 字节) = 11
陆地 = 海平面 + t^4 × (255 - 海平面)      t = InverseLerp(海平面, 1, 归一化高度)
```

- `t^4` 是关键：原版陆地高度近似"归一化高度的 4 次方"，平原≈20、丘陵≈88、p90≈170、峰值 255。
- 引擎渲染水面的条件是 **`water 字节 > height 字节`**；因此河流必须把河床**下切**到 1，否则水面画不出来。
- 参考：`MapGenConfig.seaLevelByte / waterByte / maxLandByte / landHeightCurve`。

### 4. 地表贴图图层（textureIndex）

- 地表**不是**一个 shader 混合多图，而是 **N 层 = N 个 mesh + N 个材质**，各自一张 `_BaseMap`，靠 `renderQueue = 2000 + 层号` 做 alpha 叠加。
- `MapData.VertexData.textureIndex` 就是 **`layerDatas` 的下标**；`MapCell` 只把它转成**白/黑二值顶点色 alpha 掩码**，索引本身不进入 mesh。
- **最后一层恒为水面层**（`layerDatas.Length - 1`），归属看 `water` 字节而非 `textureIndex`。
- **安全范围是 `[0, layerDatas.Length - 2]`**；越界会导致该处地表**破洞**。
- 原版 DefaultMap：**49 层**（36 地表 + 1 水面 + 12 空层），实际使用 `textureIndex` 只有 **0~34**。

### 5. 四季贴图

- `MapRender.SeasonNames = { "Autumn", "Spring", "Summer", "Winter" }`
  → **索引 0=秋、1=春、2=夏、3=冬（不是时序！）**；文件名后缀就是季节索引（`BaseMap0.png` 是秋）。
- 每个 `LayerData` 持有 4 套 `diffuse / normal / mask` 与对应名字数组；**四季同名、内容不同**（原版 37 张里 35 张四季不同）。
- 四季贴图**尺寸必须一致**，因为平铺系数 `textureScale = (32768*2 / texW, -32768*2 / texH)` 由尺寸算出。
- 切换是自动的：`MapProperty.Init()` 订阅 `map.onSeasonChange`；设置 `map.curSeason = s` 后各图层材质与底图会自动切到该季节。
- 贴图路径与三级回退：
  `Assets/Map/{WorkContent}/Terrain/{Season}/{layer_N}.png` → `Assets/Map/Default/Terrain/{Season}/...` → `Texture2D.whiteTexture`（白板）。

### 6. 底图（BaseTex）= 顶点色

- 位置：`Assets/Map/{WorkContent}/BaseTex/BaseMap0..3.png`（4 张，索引即季节）。
- 尺寸 = `vertex_width × vertex_height`（原版 1024×1024）= **每个顶点一个像素的底色图**。
- 着色器按世界坐标取 UV：`u = worldZ / _MapWidth`、`v = 1 - worldX / _MapHeight`。
- 片元里 `lerp(diffuse, baseColor * diffuse, _BaseColorIntensity=0.85)` → 底图管整体色调、图层贴图管细节。
  **因此底图应以"接近白色 + 轻微色调"为主，否则会把整张地图压暗。**

---

## 二、代码结构

```
Project/Assets/Sango/Scripts/Map/Generator/
├─ MapGenConfig.cs          地形/底图参数（含高度标定、四季底图、崖分布）
├─ MapGenContext.cs         上下文：高度场/阈值/地格场/材质映射/写回引擎
├─ MapGenerator.cs          流程编排：地形→河流→城市→领地→道路→底图
├─ MapNoise.cs              fBm 噪声 + 分位数统计
├─ MinHeap.cs               可扩容二叉最小堆（Dijkstra 用）
├─ HexNeighbors.cs          六边形邻居（与引擎坐标一致）
├─ TerrainIds.cs            地形类型 ID 常量
├─ TerrainLayerMap.cs       实测「地形 → 贴图图层」表 + 崖调色板
├─ RiverGenerator.cs        河流（汇流累积模型）
├─ CityPlacer.cs            城市选址（适宜度 + 贪心间隔）
├─ TerritoryBuilder.cs      领地划分（多源 Dijkstra）
├─ RoadGenerator.cs         道路（包围盒 Dijkstra → 主径/栈道/渡所）
├─ BaseMapGenerator.cs      四季底图（顶点色）
├─ SeasonLayerSetup.cs      四季地表贴图层初始化
├─ ScenarioCityBuilder.cs   把城市写入剧本 citySet
├─ MapApi.cs                对外 JSON 接口（19 个 op）
└─ Editor/MapMcpTool.cs     MCP 自定义工具 sango_map
```

编辑器集成：`Map/Tools/MapEditor.cs`（顶部菜单）、`Map/Tools/MapEditorWindows.cs`（参数窗口）。

## 三、生成流程

```
地形(高度场+分类) → 河流 → 城市选址 → 领地划分 → 道路 → 写回引擎 → 底图
                                                    ↑
                                    顶点高度/材质/水面/法线、地格地形+状态+归属
```

各阶段共享同一个 `MapGenContext`（`MapGenContext.Current` 缓存最近一次），
因此可以「先生成地形，再单独调河流/城市/道路/底图」。

## 四、三种使用方式

### 方式一：编辑器菜单（人工）

1. 运行游戏 → 起始界面点 **「地图编辑器」**
2. **「文件 / 新建地图」**：宽度/高度填**像素**（如 1024），MapKey 填与剧本 `Info.mapType` 一致的值
3. **「编辑 / 加载剧本」**（只在需要写剧本城市时；注意别用「从剧本加载地图」，那会用剧本的 mapType 覆盖当前地图）
4. **「文件 / 程序化生成地图」** → 调参 → 「初始化四季贴图层(36 层地表 + 水面)」→「开始生成」→「写入剧本城市」
5. **「文件 / 保存地图」** → 文件名必须是 `{MapKey}.bin`

### 方式二：MCP 工具 `sango_map`（AI 无代码调用）

```
sango_map { "command": "{\"op\":\"describe\"}" }
sango_map { "op": "setupSeasonLayers", "args": { "terrainLayerCount": 36 } }
sango_map { "op": "generateWorld", "args": { "seed": 20261010, "landRatio": 0.55,
             "river": {"flowThreshold": 14}, "city": {"cityCount": 40}, "road": {"cliffCost": 40} } }
sango_map { "op": "createScenarioCities", "args": { "autoExport": true } }
sango_map { "op": "saveMap", "args": { "path": "Map/DefaultMap.bin" } }
```

### 方式三：C# 直接调用

```csharp
Sango.MapGen.MapApi.Run("{\"op\":\"generateWorld\",\"args\":{\"landRatio\":0.6}}");
Sango.MapGen.MapGenerator.GenerateAll(map, terrainCfg, riverCfg, cityCfg, territoryCfg, roadCfg);
```

## 五、调用前置条件（脚本 / 裸编辑器）

在地图编辑器或游戏启动流程中这些 API 可直接用；**在裸编辑器会话里必须先引导**，否则报「地形类型数据未初始化」：

```csharp
Sango.Path.Init();
Sango.Mod.ModManager.Instance.Init();   // 缺这步 ModManager.EnumFiles 会空引用
Sango.Core.GameData.Instance.Init();    // 加载 TerrainTypes / ModelConfigs
```

## 六、参数速查（默认值）

| 配置 | 关键字段（默认） |
|---|---|
| `MapGenConfig` | `seed=20261010`、`landRatio=0.55`、`mountainRatio=0.20`、`shallowRatio=0.12`、`terrainFeatureSize=26`、`climateFeatureSize=70`、`continentScale=0.35`、`continentWeight=0.65`、`octaves=4`、`smoothPasses=2`、`edgeFalloffCells=12`、`seaLevelByte=10`、`waterByte=11`、`maxLandByte=255`、`landHeightCurve=4`、`renderWater=true`、`recomputeNormals=true`、`generateBaseMap=true`、`baseMapSeasonal=true`、`baseMapTint=0.35`、`baseMapShade=0.25`、`cliffLayerSpread=true` |
| `RiverGenConfig` | `flowThreshold=14`、`widenFlowFactor=4`、`allowLake=true`、`lakeThreshold=45` |
| `CityGenConfig` | `cityCount=24`、`minSpacing=6`、`interiorRadius=3`、`namePrefix="城"` |
| `TerritoryGenConfig` | `useTerrainCost=true`、`placeCityTerrain=true`、`interiorRadius=3` |
| `RoadGenConfig` | `connectNeighborsOnly=true`、`cliffCost=40`、`riverCost=25`、`blockSea=true`、`blockShallow=true` |
| `ScenarioCityGenConfig` | `templateCityId=0`(自动)、`buildingTypeId=1`、`defaultForceId=0`、`linkNeighbors=true`、`autoExport=true` |

推荐组合：快速联调用尺寸 256 + `terrainFeatureSize=10` + `smoothPasses=1`；
大世界用尺寸 2048 + `terrainFeatureSize=32` + `cityCount=80~120`；
要接近原版山地占比把 `mountainRatio` 提到 0.35~0.40。

详细参数与全部 API 见 `references/api-reference.md`。

## 七、常见坑（都是实际踩过的）

| 现象 | 原因 / 处理 |
|---|---|
| 地表白板 | 新地图 `diffuseTexName` 为空 → 调 `setupSeasonLayers` 补齐四季贴图名 |
| 地表破洞 | `textureIndex` 超出 `[0, layerDatas.Length-2]` → 用 `TerrainLayerMap` + 钳制 |
| 水面看不见 | 河床没下切（河格仍是陆地高度），或 `water 字节 <= height 字节` |
| 高度整体偏高/偏低 | 没用海平面锚定的分段映射（老的 `h * 系数` 是错的） |
| 数组越界 `IndexOutOfRange` | 图层统计数组按固定 36 分配，而原版地图有 49 层（索引可达 47） |
| 保存后游戏读不到 | `.bin` 文件名必须等于剧本 `Info.mapType` |
| `Destroy may not be called from edit mode` | 引擎 `MapRender.Clear()` 自身行为，仅非 Play 模式出现，可忽略 |
| 裸编辑器 `loadMap` 报 `MapObject.position` 空引用 | 缺少完全引导（`MapModels`/`MapObject` 依赖运行时环境），请在编辑器场景内操作 |
| 编辑器 MapKey 丢失 | `NewMap` 内部 `Init()` 会把 `WorkContent` 重置为 `"Default"`，必须在 `NewMap` **之后**再赋值 |
| **可行走格大面积没有 areaId** | Python 侧面积分配把不可行走地形当墙 / 截断了搜索距离；海/河/浅滩也被当墙 |
| **内政地生成了但槽位仍是 0** | `terrainState` 写了内政位，但 `areaId` 不是那座城 |
| **内政地挡住道路** | 内政候选没排除 `主径(11)`（它是唯一可建造的交通设施） |
| **相邻可通行格高差压不下去** | 用了就地 Gauss-Seidel → 极限环；必须用同步(Jacobi)区间投影 |
| **水面整片消失** | 平滑时没把海/河当锚点，海床被抬高顶穿了水面 |
| **引擎读到旧地形表** | 读了缓存中的 `GameData.Instance.ScenarioCommonData`；实际加载地图用的是 `Scenario.Cur.CommonData` |
| **Python 归属/距离整体错位且不报错** | `np.where` / `np.argwhere` 返回 `(dim0, dim1)`，写成了 `(y, x)` |
| **新地形静默变灰** | 只改了 `TerrainTypes.json`，漏了 `BaseMapGenerator.BuildTerrainColor()` 与 `sango_basemap.py` 的 `TC` |
| **加载别的尺寸地图时 `MapModels` 崩** | 尾部段照抄了 DefaultMap 的模型/标注，必须置空 |
| **改顶点没反应** | `VertexData` 已是 4 字节 struct，`GetVertexData()` 返回副本；要用 `SetVertexHeight/TextureIndex/Water` |

## 八、生成后验证清单

**引擎生成器：**

- [ ] 「生成结果」地形统计合理：陆地占比 ≈ `landRatio`（再扣掉河流占用格）
- [ ] 海高度全 0、川/河高度 1、浅滩 10、水面字节 11
- [ ] 平原高度 ≈ 20、山地中位 ≈ 88、峰值接近 255
- [ ] 崖顶点分布在多张岩石贴图上（不是单一图层）
- [ ] `setupSeasonLayers` 后 L0/L29 等四季贴图尺寸与原版一致且非白图
- [ ] 四季底图 4 张内容不同（冬季含积雪）
- [ ] 保存 `.bin` 后重新加载，同种子结果完全一致

**Python 管线（重建 `.bin` 之后，字节级 + 引擎侧各查一遍）：**

- [ ] **可行走格 `areaId == 0` 的数量为 0**（含海/河/浅滩；对应引擎里那句被注释掉的诊断）
- [ ] 每座城 `areaCellList` 非空；港/关也有自己的领地，且港六邻仍有可走水域
- [ ] 内政格：`terrainState & 4`、`areaId` == 所属城、地形 `canBuild`、**不压在道路上**
- [ ] 内政格离城 3~16 格、顶点与所属城同高、`InteriorCellCount > 0`
- [ ] 从任意城市格 BFS，可达格数 == 可行走格总数（无孤岛）
- [ ] 相邻可通行格超 `--max-step` 的边占比 < 1%
- [ ] 新增地形在 `TerrainTypes.json` / `TerrainIds.cs` / `BaseMapGenerator.BuildTerrainColor()` / `sango_basemap.TC` 四处一致

## 九、Python 转换管线（Koei 311 世界图 → Sango `.bin`）

引擎自带的生成器之外，另有一条**离线 Python 管线**。当前世界图（1800×1000 格 / 7201×4001 顶点 / 500 城）
就是用它产出的。运行顺序（各步可单独重跑，均就地写回 `data/sango_full/`）：

| # | 脚本 | 作用 |
|---|---|---|
| 1 | `sango_convert.py --ds 1` | 世界图原始数组 → terrain / vheight / vtex / vwater + cities.json |
| 2 | `sango_fix_roads.py --fix` | 相邻城市不通则切最省路径（跨川→渡所、跨崖→栈道） |
| 3 | `sango_sea_zones.py` | 海域分档（近海9 可走；远海21 / 深海22 不可走）+ 孤岛桥接/封锁 |
| 4 | `sango_map_fixes.py` | 城市 7 格平整化+陆地化、内政地生成、可通行格高度平滑 |
| 5 | `sango_basemap.py --mode gamut --shade 0.85` | 四季底图 |
| 6 | `sango_build_bin.py` | 写 `Map/<name>.bin`（自己写 mapGrid+mapData 段，尾部段抄 DefaultMap 并置空模型/标注） |

顺序要点：3 必须在 4 之前（内政候选要避开远海/深海）；4 必须在 5、6 之前；2 应排在 3 之前。
两个脚本都支持 `--report`（只体检不写盘），调参时先用它。

**三条引擎里查不出来、最容易踩的硬事实：**

1. **地格只在地图 `.bin` 里，不在剧本里。** 链路是
   `剧本.Info.mapType → Map/<name>.bin 的 mapGrid → CellSet.SetTerrainTypeAndState()
    → City.areaCellList → City.OnScenarioPrepare() → interiorCellList / defenceCellList`。
   剧本 JSON 只有 `citySet / corpsSet / forceSet / personSet` → 改地格不必重生成剧本。
2. **内政地要同时满足两件事才生效**：`terrainState` 置位 **4**
   （`1 << GridState.Interior`，注意 `GridState.Interior = 2`）**且** `areaId` 等于那座城的 id。
   只写第一条 → 城市建筑槽位仍然是 0。
3. **面积分配不能让不可行走地形当墙、也不能截断搜索距离**，否则大片可行走格 areaId = 0。
   引擎 `Map.cs` 里正好有一句被注释掉的诊断 `地格: areaId 可以行走,但是所属城市找不到!!`，
   **验收标准就是让这个条件计数为 0**。海/河/浅滩 `moveable=true` 也必须分配；
   领地种子要包含港(18)/关所(19)（各占 1 格并拥有自己的 areaId）。

**两条数值口径（实测标定，直接照用）：**

- **DefaultMap(256×256 中国区) → 世界图**：`world_cell = DefaultMap_cell - 28 + china_lo/china_hi`
  （`-28` 是 311 可玩区偏移；`china_lo/hi` 见 `data/world/world_info.json`），
  用 42 座三国城标定，误差 **0.29 格**。
- **DefaultMap 的实测形态**：都市(17) = **7 格**六边形块、港(18)/关所(19) 各 **1 格**、
  内政格 **591 个**（每城 2~22、中位 12、离城 2~16 格、100% 土(2)）。
  城市占地与内政地的目标形态都以它为准。

分段字节格式、运行时地格数据流、坐标换算、算法细节、验证方法与完整陷阱清单见
`references/python-pipeline.md`。

## 十、参考文件

- `references/map-data-facts.md`：地图文件格式、尺寸换算、高度/图层/四季/底图的**实测数据**与推导过程
- `references/api-reference.md`：`MapApi` 全部 op、参数 schema、返回格式，以及 MCP 工具说明
- `references/python-pipeline.md`：Python 转换管线的运行顺序、`.bin` 分段字节格式、运行时地格数据流、
  areaId/内政地规则、坐标换算、海域分档与孤岛、顶点高度平滑、顶点数据 4 字节化、验证方法
