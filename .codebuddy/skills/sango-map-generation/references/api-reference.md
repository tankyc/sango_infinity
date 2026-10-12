# MapApi 接口参考

面向 AI / 脚本的 JSON 接口层。所有方法入参、出参均为 **JSON 字符串**，便于通过 Unity MCP 的
`execute_code` 或任何外部脚本调用，无需了解 C# 类型。

- 代码位置：`Project/Assets/Sango/Scripts/Map/Generator/MapApi.cs`
- MCP 工具：`sango_map`（`Project/Assets/Sango/Scripts/Map/Generator/Editor/MapMcpTool.cs`）

---

## 1. 调用方式

### 1.1 统一入口

```csharp
Sango.MapGen.MapApi.Run("{\"op\":\"generateWorld\",\"args\":{\"landRatio\":0.6}}");
```

### 1.2 MCP 工具（AI 无代码调用）

```
sango_map { "command": "{\"op\":\"generateWorld\",\"args\":{...}}" }
sango_map { "op": "generateWorld", "args": { "landRatio": 0.6 } }
```

### 1.3 直接调用具体方法

```csharp
Sango.MapGen.MapApi.GenerateWorld("{\"landRatio\":0.6}");
Sango.MapGen.MapApi.GetMapInfo();
```

### 1.4 前置条件（脚本 / 裸编辑器）

```csharp
Sango.Path.Init();
Sango.Mod.ModManager.Instance.Init();
Sango.Core.GameData.Instance.Init();
```

### 1.5 统一返回格式

```json
{ "success": true, "message": "中文提示", "data": { } }
```

---

## 2. 操作清单（19 个）

| op | args | 说明 |
|---|---|---|
| `describe` | — | 返回全部 op 清单 + 各 config 的参数 schema |
| `mapInfo` | — | 地图标识、像素尺寸、格数、顶点数、gridSize |
| `createNewMap` | `{width:int, height:int, key:string}` | 新建地图；**宽高是像素**（格数 = 尺寸/4），需能被 4 整除 |
| `loadMap` | `{path:string}` | 加载 `.bin`；相对路径相对 Content 根目录 |
| `saveMap` | `{path?:string}` | 保存 `.bin`；省略路径时存到 `Map/{标识}.bin` |
| `generateTerrain` | MapGenConfig | 只生成地形（高度场 + 地格分类 + 顶点数据） |
| `generateRiver` | RiverGenConfig | 在已有地形上补河流 |
| `generateCities` | `{...CityGenConfig, territory?:TerritoryGenConfig}` | 选址城市 + 划分领地 |
| `generateRoads` | RoadGenConfig | 在已有城市间铺路（主径/栈道/渡所） |
| `generateBaseMap` | MapGenConfig（`baseMap*` 生效） | 生成四季底图并写 png |
| `setupSeasonLayers` | `{terrainLayerCount?:int, loadTextures?:bool}` | 初始化四季地表贴图层（缺图回退 Default 目录） |
| `generateWorld` | `{...MapGenConfig, river?, city?, territory?, road?}` | **一次生成**：地形+河流+城市+领地+道路+底图 |
| `paintTerrain` | `{x,y,terrain}` 或 `{x,y,w,h,terrain}` 或 `{points:[{x,y,terrain}]}` | 手改地形 |
| `queryCell` | `{x:int, y:int}` | 单格地形/状态/归属/通行性 |
| `terrainStats` | — | 各类地形数量与占比 |
| `terrainTypes` | — | 全部地形类型（id/名称/水域/可通行/可建造/baseCost） |
| `listCities` | — | 当前上下文中生成的城市（坐标/评分/相邻城市） |
| `syncScenarioCities` | — | 只把坐标同步到 `Scenario.Cur` 的城市并导出 |
| `createScenarioCities` | ScenarioCityGenConfig | **写入剧本**：缺失则按模板新建、存在则更新、可自动导出 |

---

## 3. 参数 schema

### 3.1 `MapGenConfig`（地形 + 底图）

| 字段 | 类型 | 默认 | 说明 |
|---|---|---|---|
| `seed` | int | 20261010 | 随机种子（同种子完全可复现） |
| `landRatio` | float | 0.55 | 陆地占比 0.05~0.95 |
| `mountainRatio` | float | 0.20 | 山地占陆地比例（原版约 0.37） |
| `shallowRatio` | float | 0.12 | 浅滩占水域比例 |
| `wetThreshold` | float | 0.56 | 森林湿度阈值 |
| `dryThreshold` | float | 0.42 | 荒地湿度阈值 |
| `sandThreshold` | float | 0.26 | 砂地湿度阈值 |
| `swampThreshold` | float | 0.74 | 湿地湿度阈值 |
| `swampMaxAltitude` | float | 0.5 | 湿地最大相对海拔 |
| `soilThreshold` | float | 0.5 | 草地/土分割阈值 |
| `terrainFeatureSize` | float | 26 | 地形特征尺寸（格） |
| `climateFeatureSize` | float | 70 | 气候特征尺寸（格） |
| `continentScale` | float | 0.35 | 大陆频率（越小大陆越大） |
| `continentWeight` | float | 0.65 | 大陆层权重 |
| `octaves` | int | 4 | fBm 倍频数 |
| `persistence` | float | 0.5 | 振幅衰减 |
| `lacunarity` | float | 2 | 频率增长 |
| `smoothPasses` | int | 2 | 平滑次数 |
| `edgeFalloffCells` | float | 12 | 边缘衰减带宽（格） |
| `seaLevelByte` | int | 10 | **海平面高度字节**（海底 = 0） |
| `waterByte` | int | 11 | **水面高度字节**（须 > 地面高度才绘制） |
| `maxLandByte` | int | 255 | 陆地最高高度字节 |
| `landHeightCurve` | float | 4 | 陆地高度曲线指数（越大平原越多、山峰越尖） |
| `renderWater` | bool | true | 是否为水域渲染水面 |
| `recomputeNormals` | bool | true | 是否重算顶点法线 |
| `generateBaseMap` | bool | true | 是否生成底图 |
| `baseMapTint` | float | 0.35 | 底图中地形色权重 |
| `baseMapShade` | float | 0.25 | 底图高度明暗强度 |
| `baseMapSeasonal` | bool | true | 底图是否区分四季（冬季含积雪） |
| `cliffLayerSpread` | bool | true | 崖是否在多张岩石贴图间分布 |
| `cliffSteepSlope` | float | 16 | 陡坡阈值（高度字节差） |
| `cliffGentleSlope` | float | 6 | 缓坡阈值 |
| `noiseOffsetX/Y` | float | 0 | 噪声偏移 |

### 3.2 `RiverGenConfig`

| 字段 | 默认 | 说明 |
|---|---|---|
| `flowThreshold` | 14 | 成河最小汇流量（格）；越小河越多 |
| `widenFlowFactor` | 4 | 大江加宽倍数 |
| `allowLake` | true | 是否生成湖泊 |
| `lakeThreshold` | 45 | 成湖最小汇流量 |

### 3.3 `CityGenConfig`

| 字段 | 默认 | 说明 |
|---|---|---|
| `cityCount` | 24 | 目标城市数量 |
| `minSpacing` | 6 | 城市最小六边形距离 |
| `interiorRadius` | 3 | 内政区半径 |
| `seed` | 0 | 0 = 沿用地形种子 |
| `riversideBonus` / `coastBonus` / `lowlandBonus` / `badTerrainPenalty` | 0.6 / 0.4 / 0.5 / 0.6 | 选址评分权重 |
| `namePrefix` | "城" | 城市名前缀 |

### 3.4 `TerritoryGenConfig`

| 字段 | 默认 | 说明 |
|---|---|---|
| `useTerrainCost` | true | 按地形代价划分（否则等权） |
| `placeCityTerrain` | true | 城址格改为"都市"地形 |
| `interiorRadius` | 3 | 内政区半径 |

### 3.5 `RoadGenConfig`

| 字段 | 默认 | 说明 |
|---|---|---|
| `connectNeighborsOnly` | true | 只连接相邻城市 |
| `cliffCost` | 40 | 穿山代价（生成栈道）；调小可强制走山 |
| `riverCost` | 25 | 渡河代价（生成渡所） |
| `blockSea` / `blockShallow` | true | 禁行海洋 / 浅滩 |
| `margin` | 10 | 搜索包围盒外扩格数 |
| `maxPairs` | 0 | 最多处理的城市对（0 = 不限） |

### 3.6 `ScenarioCityGenConfig`

| 字段 | 默认 | 说明 |
|---|---|---|
| `templateCityId` | 0 | 模板城市 ID；0 = 自动取剧本中第一座城市 |
| `buildingTypeId` | 1 | 新城市建筑类型（1 通常为都市） |
| `defaultForceId` | 0 | 新城市所属势力（0 = 无主） |
| `linkNeighbors` | true | 是否写入相邻城市关系 |
| `overwriteExisting` | true | 已存在城市是否更新坐标 |
| `removeExtraCities` | false | 是否删除超出生成范围的旧城市 |
| `autoExport` | true | 是否自动导出剧本文件 |
| `exportPath` | null | 导出路径；空则用剧本自身 `FilePath` |

---

## 4. 返回数据示例

### 4.1 `generateWorld`

```json
{
  "success": true,
  "message": "地图生成完成：256x256 格(含河流、含城市、含道路)，耗时 77 毫秒",
  "data": {
    "cellsX": 256, "cellsY": 256, "elapsedMs": 77,
    "hasRivers": true, "hasBaseMap": true, "roadCount": 103, "cityCount": 8,
    "cities": [ { "id": 1, "name": "城1", "x": 50, "y": 12, "score": 2.80, "neighbors": [6] } ],
    "terrainCounts": [ { "id": 1, "name": "草地", "count": 404 } ]
  }
}
```

### 4.2 `createScenarioCities`

```json
{
  "success": true,
  "message": "剧本城池写入完成：新建 8 座、更新 0 座、删除 0 座，当前共 87 座，已导出剧本",
  "data": { "created": 8, "updated": 0, "removed": 0, "templateCityId": 9,
            "totalCities": 87, "exported": true, "exportPath": "..." }
}
```

### 4.3 `setupSeasonLayers`

```json
{
  "success": true,
  "message": "四季贴图层初始化完成：共 37 层(地表 36 + 水面 1)",
  "data": { "layers": 37, "terrainLayers": 36, "maxUsableLayer": 35,
            "seasons": ["Autumn","Spring","Summer","Winter"], "currentSeason": 0 }
}
```

---

## 5. 典型工作流

### 5.1 从零生成一张可用的剧本地图

```
1. createNewMap   { width:1024, height:1024, key:"DefaultMap" }
2. setupSeasonLayers { terrainLayerCount:36 }      // 地表从白板 → 原版四季纹理
3. generateWorld  { seed:..., landRatio:0.55, river:{...}, city:{cityCount:40}, road:{...} }
4. createScenarioCities { autoExport:true }        // 写入剧本 citySet
5. saveMap        { path:"Map/DefaultMap.bin" }    // 文件名必须 = 剧本 Info.mapType
```

### 5.2 分步微调

```
1. generateTerrain  { ... }        // 只做地形
2. generateRiver    { flowThreshold:10 }
3. generateCities   { cityCount:50, minSpacing:5 }
4. generateRoads    { cliffCost:25 }
5. generateBaseMap  { baseMapSeasonal:true }
```

### 5.3 局部修补

```
paintTerrain { points:[{x:10,y:20,terrain:1},{x:11,y:20,terrain:6}] }
queryCell    { x:10, y:20 }
terrainStats
```

---

## 6. 引擎侧相关接口（配合使用）

| 接口 | 说明 |
|---|---|
| `MapRender.Instance.NewMap(w,h)` | 新建（会创建渲染对象；内部 `Init()` 会把 `WorkContent` 重置为 `"Default"`，需在其后重新赋值） |
| `MapRender.Instance.SaveMap(path)` / `LoadMap(path)` | 读写 `.bin`。也可以按 `references/python-pipeline.md` 记录的分段格式**在 Python 里手写**（当前世界图就是这么产出的） |
| `MapGrid.SetTerrainType(x,y,id)` / `GetGridData(x,y)` | 地格地形读写（`terrainState` / `areaId` 直接改返回对象的字段；`GetGridData` 返回的是内部对象引用，不是副本） |
| `MapData.vertexDatas[vx][vy]` | 顶点 `height` / `textureIndex` / `water`。**`VertexData` 已是 4 字节 struct，`position`/`uv`/`normal` 不再存储**，改由下面几个入口现算 |
| `MapData.VertexPosition / VertexUV / VertexWaterPosition / VertexNormal` | 由 `(height, water, vx, vy)` 现算派生量（与从 `.bin` 加载时同一套算法） |
| `MapData.SetVertexHeight / SetVertexTextureIndex / SetVertexWater` | 改顶点的写接口（`GetVertexData()` 返回的是 struct 副本，改它无效） |
| `MapTerrain.Rebuild()` | 高度改变后重建地表网格 |
| `MapLayer.layerDatas` / `LayerData.AutoLoadDiffuse()` | 图层与四季贴图 |
| `MapBaseColor.texture[4]` / `UpdateRender()` | 四季底图 |
| `Map.curSeason` / `ChangeSeason(s)` | 季节切换（0=秋,1=春,2=夏,3=冬） |
