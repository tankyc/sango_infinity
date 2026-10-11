# Sango Infinity 地图数据实测事实

本文件是 `sango-map-generation` 技能的参考材料，记录**实测**（而非推断）得来的地图数据结构与渲染机制。
数据来源：解析 `Build/Content/Map/DefaultMap.bin`（原版地图，version 10）+ 阅读引擎源码。

---

## 1. 地图文件（`.bin`）字段顺序

由 `MapRender.SaveMap` 写出，顺序如下：

| 顺序 | 字段 | 说明 |
|---|---|---|
| 1 | `VERSION` (int) | 当前 = 10 |
| 2 | `WorkContent` (string) | 地图标识（对应 `Assets/Map/{WorkContent}/` 目录） |
| 3 | `mapWidth` (int) | 像素宽（格数 × 4） |
| 4 | `mapHeight` (int) | 像素高 |
| 5 | `mapGrid` | `gridSize`(int) + 每格 `terrainType`(byte) + `terrainState`(int) + `areaId`(ushort) + `gridTextureName`(string) |
| 6 | `mapData` | `quadSize`(int) + 逐顶点 `height`(byte) + `textureIndex`(byte) + `water`(byte) |
| 7 | `mapLayer` | `layerCount`(int) + 每层 `isLit`(bool) + `textureScale`(float×2) + 4 季 × (diffuse/normal/mask 名字) |
| 8 | `mapTerrain` | `cellSize`(int) |
| 9 | light / fog / baseColor / skybox / camera / models / labels | 各模块自身序列化 |

运行时 `Map.Load` 只解析第 1~5 项（网格部分），其余被忽略。

### 实测头信息（DefaultMap.bin）

```
version=10  content="Default"  mapWidth=1024  mapHeight=1024  gridSize=20
cells=256×256  vertex=1025×1025  quadSize=5  cellSize=64  文件大小=3,740,851 字节
```

---

## 2. 尺寸换算

| 概念 | 公式 | 1024 地图 |
|---|---|---|
| 逻辑格数 | `mapWidth / 4` | 256 × 256 |
| 顶点数 | `(mapWidth + 1) × (mapHeight + 1)` | 1025 × 1025 |
| `MapGrid.gridSize` | 由 `MapGrid.quadSize = 20` 决定（私有默认值） | 20 |
| `MapGrid.bounds` | `world_width / gridSize = (mapWidth × 5) / 20 = mapWidth / 4` | 256 × 256 |
| `MapData.quadSize` | 顶点间距（世界单位） | 5 |

**结论**：只要不手改 `gridSize`，`格数 = 尺寸 / 4` 恒成立，与运行时 `Map.Load` 一致。

格 → 顶点：
- 格中心顶点：`vx = 4*cx + 2`，`vy = 4*cy + (cx & 1)*2 + 2`
- 顶点世界坐标：`(x = vy*5, y = height*0.5, z = vx*5)`

---

## 3. 高度字节与水面（原版实测）

按"地形 → 地格中心顶点"统计（DefaultMap）：

| 地形 | h_min | h_p50 | h_max | water_p50 |
|---|---|---|---|---|
| 海 | 0 | **0** | 0 | **11** |
| 河 | 0 | 0 | 1 | 11 |
| 川 | 0 | 1 | 70 | 11 |
| 浅滩 | 10 | 11 | 15 | 12 |
| 岸 | 0 | 16 | 172 | 11 |
| 草地 | 0 | 20 | 170 | 0 |
| 土 | 0 | 20 | 147 | 0 |
| 砂地 | 0 | 22 | 91 | 0 |
| 森 | 1 | 26 | 139 | 0 |
| 荒地 | 3 | 24 | 110 | 0 |
| 湿地 | 0 | 20 | 66 | 0 |
| 主径 | 0 | 20 | 145 | 0 |
| 栈道 | 81 | **113** | 116 | 0 |
| 渡所 | 17 | 24 | 69 | 0 |
| 都市 | 0 | 20 | 147 | 0 |
| 关所 | 20 | 91 | 125 | 0 |
| 小径 | 19 | 117 | 197 | 0 |
| 崖 | 0 | **88** | **255** | 0 |
| 毒泉 | 49 | 54 | 66 | 56（特例） |

总体高度分位：`p10=0  p25=10  p50=52  p75=100  p90=160  p99=235  max=255`

**推导出的标定公式**：

```
海 → 0        川/河 → 1(河床)     浅滩 → 海平面
陆地 → 海平面 + t^4 × (255 - 海平面)，  t = InverseLerp(海平面, 1, 归一化高度)
海平面 = 10   水面字节 = 11
```

`t^4` 的验证：`0.5^4=0.0625 → 10+15.3=25`（实测平原 20~26）；
`0.75^4=0.316 → 88`（实测崖中位 88）；`0.9^4=0.656 → 171`（实测 p90 160~174）。

**引擎渲染水面的条件**：`water 字节 > height 字节`（见 `MapCell.AddVertex` / `MapRender` 的水面分支）。
所以河流必须把河床下切到 1，否则水面不会显示。

---

## 4. 地表图层（textureIndex）

### 4.1 渲染架构

- 每层一个 mesh + 一个材质（`Sango/terrain_urp`），各自一张 `_BaseMap`
- `Blend SrcAlpha OneMinusSrcAlpha` + `material.renderQueue = 2000 + 层号` 逐层叠加
- `MapCell.AddVertex` 写入的**顶点色只有白/黑二值**：
  ```csharp
  if (vtex.textureIndex == layer || layer == lowestLayer || (layerIsWater && vtex.water > 0))
      colorCache.Add(Color.white);   // (1,1,1,1)
  else
      colorCache.Add(new Color(0,0,0,0));
  ```
- Shader 侧只用顶点色的 **`.a`**（RGB 恒等于 A，未被使用）
- **水面层恒为 `layerDatas.Length - 1`**，归属由 `water > 0` 决定，LOD 固定为 2

### 4.2 `textureIndex` 的合法范围

`textureIndex` 是 `byte`，但**安全范围是 `[0, layerDatas.Length - 2]`**（排除水面层）。
越界 → 该处地表不会被任何层收录 → **破洞**。

### 4.3 原版 DefaultMap 图层清单

- `layerDatas.Length = **49**`
- L0 ~ L47：地表层，其中 **12 层贴图名为空**（回退 whiteTexture）
- L48：**水面层**，贴图 `water_0`
- 顶点实际使用的 `textureIndex`：**只有 0 ~ 34**（35 个值）
- 磁盘每个季节目录：**37 张**（`layer_0`~`layer_35` + `water_0`）
- 贴图尺寸：64×64 / 128×128 / 256×256
- 平铺系数：`textureScale = (32768*2 / texW, -32768*2 / texH)`

### 4.4 顶点 textureIndex 分布（原版，占比）

```
L0=24.8%  L30=8.4%  L3=8.0%  L20=6.1%  L22=5.7%  L6=5.1%  L8=5.0%
L18=4.9%  L15=4.8%  L29=4.0%  ... 其余 <4%
带水面(water>0)的顶点占 26.7%
```

### 4.5 地形 → 图层（实测主导层）

| 地形 | 主导层 | 占比 | 次层 |
|---|---|---|---|
| 草地 | **L29** | 70% | L15 20% |
| 土 | **L27** | 58% | L13 35% |
| 砂地 | **L3** | 61% | L4 37% |
| 湿地 | **L24** | 82% | L10 15% |
| 毒泉 | **L1** | 100% | — |
| 森 | **L30** | 88% | L23 11% |
| 川/河/海 | **L0** | 93/99/100% | — |
| 荒地 | **L21** | 45% | L30 30% / L26 13% |
| 主径 | **L34** | 69% | L33 28% |
| 栈道 | **L34** | 83% | L33 17% |
| 渡所 | **L34** | 39% | L27 18% / L33 17% |
| 浅滩 | **L5** | 100% | — |
| 岸 | **L2** | 89% | L3 3% |
| 都市 | **L28** | 39% | L27 28% / L13 15% |
| 港 | **L3** | 69% | L2 31% |
| 关所 | **L27** | 60% | L13 40% |
| 小径 | **L20** | 66% | L21 15% |
| **崖** | **L18** | 16% | L20 13% / L6 13% / L8 12%…**混杂** |

**两个关键结论**：
1. L0 是水体贴图（川/河/海 100% 用它），其占比 24.8% ≈ 带水面顶点占比 26.7%。
2. **崖没有主导图层**——美术按岩石形态用多张贴图拼贴。
   代码中对应 `TerrainLayerMap.GetCliffPalette() = {18, 8, 20, 6, 22, 23}`，
   按"坡度分档（分位数自适应）+ 噪声"选取，实测生成分布 L6 17% / L8 16% / L18 13% / L20 14% / L22 16% / L23 21%。

### 4.6 贴图路径与回退

```
Assets/Map/{WorkContent}/Terrain/{Season}/{name}.png
   ↓ 找不到
Assets/Map/Default/Terrain/{Season}/{name}.png
   ↓ 找不到
Texture2D.whiteTexture
```

`MapLayer.LayerData.AutoLoadDiffuse()` 会一次性加载四季贴图，是"四季贴图加载器"。

---

## 5. 四季贴图

### 5.1 季节索引顺序（易错）

```csharp
MapRender.SeasonNames = { "Autumn", "Spring", "Summer", "Winter" };
// 索引：0=秋, 1=春, 2=夏, 3=冬   ← 不是时序
```

- 目录名用季节名：`Terrain/Autumn/`、`Terrain/Spring/`、`Terrain/Summer/`、`Terrain/Winter/`
- 底图文件名后缀就是索引：`BaseMap0.png`=秋、`BaseMap3.png`=冬

### 5.2 实测差异

| 项 | 结果 |
|---|---|
| 37 张地表贴图中四季**不同**的 | **35 张** |
| 四季相同的 | `water_0`、`layer_35` |
| 尺寸 | 每层四季**同尺寸**（layer_0 都 64×64、layer_29 都 256×256） |
| 底图 | `BaseMap0..3.png` 4 张**互不相同** |

### 5.3 切换机制

```
MapProperty.Init()  →  map.onSeasonChange += OnSeasonChange      (Clear() 里退订)
map.curSeason = s   →  onSeasonChange(s)
    ├─ MapLayer.UpdateRender()      → 逐层 UpdateMaterial(s)：切 _BaseMap/_BumpMap/_MaskMap
    ├─ MapBaseColor.UpdateRender()  → _BaseTex = texture[s]
    └─ MapSkyBox                    → 切天空贴图
```

切换入口：编辑器「视图 / 季节 / 秋|春|夏|冬」、参数窗口季节按钮、`EditorFreeCamera`。

---

## 6. 底图（BaseTex / 顶点色）

| 项 | 实测 |
|---|---|
| 路径 | `Assets/Map/{WorkContent}/BaseTex/BaseMap0..3.png` |
| 尺寸 | `vertex_width × vertex_height` = **1024×1024**（原版）= 每顶点 1 像素 |
| Shader UV | `u = worldZ / _MapWidth`，`v = 1 - worldX / _MapHeight`（`_MapWidth = vertex_width * quadSize`） |
| 混合 | `lerp(diffuse, baseColor * diffuse, _BaseColorIntensity=0.85)` |
| 保存 | `MapBaseColor.OnSave` **只在 texture 是 RenderTexture 时**导出 png（笔刷绘制）；手工放的 png 直接读 |

**实践含义**：底图是"整图色调层"，必须以接近白色 + 轻微色调为主（`baseMapTint≈0.35`），
否则 0.85 的强混合会把地形压暗。

生成时像素排布：`px = vx`，`py = (height - 1) - vy`（对应 `v = 1 - worldX/_MapHeight`）。

---

## 7. 关键源码位置

| 内容 | 文件 |
|---|---|
| 地图逻辑类 / 六边形邻居 / 坐标转换 | `Game/Map/Map.cs`、`Game/Map/Cell.cs`、`Game/Map/CellSet.cs` |
| 渲染总控 / 版本 / 季节名 | `Map/Render/Map/MapRender.cs` |
| 网格数据（terrainType/state/areaId） | `Map/Render/Map/MapGrid.cs` |
| 顶点数据（height/textureIndex/water） | `Map/Render/Map/MapData.cs` |
| 地表 mesh 构建（顶点色掩码） | `Map/Render/Map/MapCell.cs` |
| 图层与四季贴图 | `Map/Render/Map/MapLayer.cs` |
| 底图 | `Map/Render/Map/MapBaseColor.cs` |
| 分块与 LOD | `Map/Render/Map/MapTerrain.cs` |
| 地形着色器 | `Sango/Shader/terrain.shader` + `Sango/Shader/sango_shaderLib.hlsl` |
| 编辑器笔刷（含四季贴图批量替换工具） | `Map/Tools/Brush/TerrainBrush.cs` |
