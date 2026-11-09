using System;
using System.IO;
using Sango.Core;
using UnityEngine;

namespace Sango.Render.TerrainSango
{
    /// <summary>
    /// Sango 地形数据源：把现有地图数据整理成"GPU 可直接采样的高度纹理 + 四叉树参数"。
    ///
    /// 【为什么要有这一层】
    /// 旧管线（MapTerrain / MapCell）把地形切成 64×64 顶点的块，每个块在 CPU 上生成 mesh；
    /// 新管线改成"一张平面网格 + 顶点着色器采样高度纹理位移"，于是第一件事必须是把
    /// 高度数据搬到 GPU 上，并确定四叉树的分层参数。
    ///
    /// 【坐标约定（务必与 MapData 一致）】
    ///   MapData.VertexPosition(vx, vy) = (vy * quadSize, height * 0.5f, vx * quadSize)
    ///   即：vx（数组第 1 维）= 世界 Z，vy（数组第 2 维）= 世界 X。
    ///   因此高度纹理的 u 轴对应世界 Z、v 轴对应世界 X，采样时不能把两个分量写反，
    ///   否则地形会整体旋转 90°。
    ///
    /// 【为什么高度用 R8 而不是 R16】
    /// 源数据 VertexData.height 本身就是 1 字节（0~255），世界高度 = height * 0.5，
    /// 量化步长 0.5 世界单位。R8_UNorm 恰好是 256 级，与源数据逐位等价；
    /// 叶层 patch 的顶点与 texel 1:1 对齐，双线性权重恒为 1/0，采样无损。
    /// 换成 R16 只是把"插值后落在 texel 中间"的粗层平滑一点点，代价是显存翻倍，
    /// 因此默认 R8，格式留成可配以便将来高度数据升级。
    /// </summary>
    public sealed class TerrainSangoSource : IDisposable
    {
        /// <summary>源顶点数（世界 Z 方向，即纹理 u 轴）。</summary>
        public int VertexWidth { get; private set; }

        /// <summary>源顶点数（世界 X 方向，即纹理 v 轴）。</summary>
        public int VertexHeight { get; private set; }

        /// <summary>相邻顶点的世界间距（MapData.quadSize）。</summary>
        public float QuadSize { get; private set; }

        /// <summary>byte 高度 → 世界高度的系数（MapData 用 0.5）。</summary>
        public float HeightScale { get; private set; } = 0.5f;

        /// <summary>地图世界宽度（世界 Z 方向）。</summary>
        public float WorldWidth => (VertexWidth - 1) * QuadSize;

        /// <summary>地图世界高度（世界 X 方向）。</summary>
        public float WorldHeight => (VertexHeight - 1) * QuadSize;

        /// <summary>最低高度（世界单位）。</summary>
        public float MinimumHeight { get; private set; }

        /// <summary>最高高度（世界单位）。</summary>
        public float MaximumHeight { get; private set; }

        /// <summary>四叉树层数（叶层为 0，根节点为 LevelCount - 1）。</summary>
        public int LevelCount { get; private set; }

        /// <summary>根节点在单轴上覆盖的 patch 数（2 的幂）。</summary>
        public int RootPatchCount { get; private set; }

        /// <summary>叶层 patch 的世界边长。</summary>
        public float LeafWorldSize => TerrainSangoConstants.PatchCells * QuadSize;

        /// <summary>GPU 高度纹理（R8，1 texel = 1 顶点）。</summary>
        public Texture2D HeightTexture { get; private set; }

        /// <summary>高度纹理的尺寸（供着色器换算 UV）。</summary>
        public Vector2 HeightTextureSize
            => HeightTexture != null ? new Vector2(HeightTexture.width, HeightTexture.height) : Vector2.one;

        /// <summary>CPU 镜像：布局为 [v * VertexWidth + u]，u = 世界 Z，v = 世界 X。</summary>
        private byte[] heights;

        /// <summary>CPU 镜像：每顶点的地表图层索引（MapData.textureIndex），布局同 heights。</summary>
        private byte[] layerIndexes;

        /// <summary>
        /// CPU 镜像：每顶点的水面高度字节（MapData.water），**不是布尔标记**：
        /// 世界高度 = water * 0.5（见 MapTerrain.VertexPosition 的 `d.water * 0.5f`），0 表示无水面。
        /// </summary>
        private byte[] waterIndexes;

        /// <summary>
        /// CPU 镜像：海面高度字节（含邻居回退）。
        /// 岸边的陆顶点自身 water 为 0，若直接用它当高度，水面会从岸边一路斜插到 0。
        /// 旧管线 MapTerrain.VertexPosition 的做法是：自身 water 为 0 时依次看周围 8 个邻居，
        /// 取第一个 water &gt; 0 的邻居的高度，从而让水面在岸线保持水平。这里在 CPU 预计算同一件事。
        /// </summary>
        private byte[] waterHeights;

        /// <summary>
        /// 每格是否可通行（对应旧管线 MapGrid.GridMaskTexture 的 alpha 口径 = <c>TerrainType.moveable</c>）。
        ///
        /// 索引 = <c>格 Z 下标 * cellCountX + 格 X 下标</c>，与地图文件格子块、
        /// 以及 GridMaskTexture 的存储顺序一致（内存布局同 MapGrid.OnLoad 的双层循环）。
        ///
        /// 【为什么不能再用顶点 water 标记判可通行】
        ///   水不等于不可通行：本工程 TerrainTypes.json 里「河」Id 8、「海」Id 9 都是
        ///   <c>moveable = true</c>，水军可以走；而「川」Id 7、「远海」Id 21、「深海」Id 22 是 false。
        ///   按顶点 water 判会把整片水域的格线全部抹掉，连可通行的水面也画不出格子。
        ///   为 null 表示拿不到地形类型表，此时 CreateGridMaskTexture 回退到旧的 water 代理。
        /// </summary>
        private bool[] cellMoveable;

        /// <summary>格 Z 方向的格数（= WorldWidth / GridSize）。</summary>
        private int cellCountZ;

        /// <summary>格 X 方向的格数（= WorldHeight / GridSize）。</summary>
        private int cellCountX;

        /// <summary>
        /// 每层四季的贴图名缓存（<c>[层][季节]</c>）。
        ///
        /// 季节切换时只需要把 <c>Layers[i].DiffuseName</c> 换成新季节那一条，
        /// 不必重新读 MapLayer —— 也就避免了对 MapRender 的长期引用。
        /// 独立读 .bin 时为 null（那时贴图名已经按当前季节固定）。
        /// </summary>
        private string[][] layerDiffuseNames;

        /// <summary>
        /// GPU 图层索引纹理（RGBA32，1 texel = 1 顶点，必须点采样）。
        /// 通道：R = 地表图层索引，G = 海面高度字节（已做邻居回退），B = 水面标记（water &gt; 0 ? 255 : 0）。
        /// 水面着色器 TerrainSangoWater 只读 G/B，地表着色器 TerrainSango 只读 R。
        /// </summary>
        public Texture2D LayerIndexTexture { get; private set; }

        /// <summary>
        /// 水面图层在图层数组里的下标（旧管线最后一层 water_*），没有水面层时为 -1。
        /// </summary>
        public int WaterLayerIndex { get; private set; } = -1;

        /// <summary>每顶点图层索引的 CPU 镜像（供图层集合统计"哪些层真的被用到"）。</summary>
        internal byte[] RawLayerIndexes => layerIndexes;

        /// <summary>地表图层元数据；程序化地形或解析失败时为 null。</summary>
        public TerrainSangoLayerInfo[] Layers { get; private set; }

        /// <summary>地图内容目录名（Assets/Map/{WorkContent}/...），默认 Default。</summary>
        public string WorkContent { get; private set; } = "Default";

        /// <summary>地格边长（世界单位），即文件头里的 gridSize；地格显示用它画六边形网格。</summary>
        public int GridSize { get; private set; } = 20;

        /// <summary>地图文件所在的 Content 根目录（用于定位 Terrain 贴图）；可能为空。</summary>
        public string ContentRoot { get; private set; }

        /// <summary>纹理格式（R8 默认；将来高度数据升级时可换 R16）。</summary>
        public TextureFormat HeightFormat { get; private set; } = TextureFormat.R8;

        /// <summary>
        /// 从现有地图数据构建数据源（阶段 3 接入游戏时走这条路径）。
        /// </summary>
        /// <param name="data">已加载的地图数据。</param>
        /// <param name="quadSize">顶点间距，通常直接取 data.quadSize。</param>
        /// <returns>数据源实例。</returns>
        public static TerrainSangoSource FromMapData(MapData data, int quadSize)
        {
            if (data == null || data.vertexDatas == null)
                throw new ArgumentNullException(nameof(data), "地图数据为空，无法构建 TerrainSango 数据源");

            // 顶点数必须取 vertexDatas 的真实维度：vertex_width/vertex_height 返回的是 bounds（格数），
            // 顶点数 = 格数 + 1（见 MapData.Create 里的 vertex_x_max = w + 1）。
            // 用格数当顶点数会整整丢掉最后一行/一列顶点，地图边缘少一格。
            int width = data.vertexDatas.Length;
            int height = 0;
            for (int x = 0; x < width; x++)
            {
                MapData.VertexData[] column = data.vertexDatas[x];
                if (column != null && column.Length > height) height = column.Length;
            }
            if (width < 2 || height < 2)
                throw new ArgumentException($"地图顶点数非法: {width}x{height}");

            var source = new TerrainSangoSource
            {
                VertexWidth = width,
                VertexHeight = height,
                QuadSize = Mathf.Max(quadSize, 0.001f),
                HeightScale = 0.5f,
            };
            source.heights = new byte[width * height];

            byte min = byte.MaxValue;
            byte max = byte.MinValue;
            for (int x = 0; x < width; x++)
            {
                MapData.VertexData[] column = data.vertexDatas[x];
                if (column == null) continue;
                int limit = Mathf.Min(height, column.Length);
                for (int y = 0; y < limit; y++)
                {
                    byte h = column[y].height;
                    // x = 世界 Z（u），y = 世界 X（v）
                    source.heights[y * width + x] = h;
                    if (h < min) min = h;
                    if (h > max) max = h;
                }
            }

            source.MinimumHeight = min * source.HeightScale;
            source.MaximumHeight = max * source.HeightScale;
            source.BuildLayout();
            source.BuildHeightTexture();
            return source;
        }

        /// <summary>
        /// 用游戏内已加载的 MapRender 数据补齐 <see cref="FromMapData"/> 缺失的部分。
        ///
        /// 【为什么 FromMapData 单独不够】
        ///   它只遍历了顶点高度，而地表要正确显示还需要三样东西：
        ///     · 每顶点图层索引（textureIndex）与水面标记（water）—— 否则整张图都是图层 0、没有水；
        ///     · 每层元数据（贴图名 / tiling / isLit / isWater）—— 否则 TerrainSangoLayerSet
        ///       在 Build 开头就直接返回 null，地表退化成单色；
        ///     · WorkContent 与 ContentRoot —— 否则定位不到 Assets/Map/{WorkContent}/Terrain/{Season}，
        ///       贴图同样加载不出来。
        ///   这些数据在旧管线里已经解析好了，直接搬过来比再解析一次 .bin 更可靠
        ///   （也会自动跟随 Mod 对 MapLayer 的覆盖）。
        /// </summary>
        /// <param name="render">已加载完成的 MapRender（mapData / mapLayer 必须非空）</param>
        public void FillFromGameMap(MapRender render)
        {
            if (render == null) return;

            WorkContent = string.IsNullOrEmpty(render.WorkContent) ? "Default" : render.WorkContent;
            ContentRoot = Sango.Path.ContentRootPath;

            MapLayer layer = render.mapLayer;
            if (layer != null && layer.curSeason >= 0 && layer.curSeason < SeasonNames.Length)
                Season = layer.curSeason;

            // ---- 每顶点：图层索引 + 水面标记 ----
            MapData data = render.mapData;
            if (data != null && data.vertexDatas != null)
            {
                int width = VertexWidth;
                int height = VertexHeight;
                int count = width * height;
                if (layerIndexes == null || layerIndexes.Length != count)
                    layerIndexes = new byte[count];
                if (waterIndexes == null || waterIndexes.Length != count)
                    waterIndexes = new byte[count];

                int columns = Mathf.Min(data.vertexDatas.Length, width);
                for (int x = 0; x < columns; x++)
                {
                    MapData.VertexData[] column = data.vertexDatas[x];
                    if (column == null) continue;
                    int limit = Mathf.Min(height, column.Length);
                    for (int y = 0; y < limit; y++)
                    {
                        MapData.VertexData vertex = column[y];
                        // x = 世界 Z（u），y = 世界 X（v），与 BuildLayout / 高度图同序
                        int index = y * width + x;
                        layerIndexes[index] = vertex.textureIndex;
                        waterIndexes[index] = vertex.water;
                    }
                }

                // 内部会先做 8 邻居海面高度回退，再上传 RGB(图层, 海面高度, 水面标记)
                BuildLayerIndexTexture();
            }

            // ---- 每层元数据（贴图名 / tiling / isLit / isWater）----
            if (layer != null && layer.layerDatas != null)
            {
                int count = layer.layerDatas.Length;
                var list = new TerrainSangoLayerInfo[count];
                layerDiffuseNames = new string[count][];
                for (int i = 0; i < count; i++)
                {
                    MapLayer.LayerData info = layer.layerDatas[i];
                    if (info == null)
                    {
                        list[i] = new TerrainSangoLayerInfo();
                        continue;
                    }

                    // 缓存四季贴图名，季节切换时直接用，不必再回读 MapLayer
                    layerDiffuseNames[i] = info.diffuseTexName;

                    list[i] = new TerrainSangoLayerInfo
                    {
                        IsLit = info.isLit,
                        ScaleX = info.textureScale.x,
                        ScaleY = info.textureScale.y,
                        DiffuseName = info.diffuseTexName != null && Season < info.diffuseTexName.Length
                            ? info.diffuseTexName[Season]
                            : null,
                        IsWater = info.isWaterLayer,
                    };
                }

                Layers = list;

                // 水面层下标：旧管线把它作为最后一个覆盖层，用 per-vertex water>0 当 alpha 叠在地表上
                WaterLayerIndex = -1;
                for (int i = 0; i < list.Length; i++)
                {
                    if (!list[i].IsWater) continue;
                    WaterLayerIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// 切换季节：只改"每层用哪张贴图"和贴图目录，不动高度、不动 LOD。
        ///
        /// 【谁调用】TerrainSangoRenderer.ApplySeason ← TerrainSangoBootstrap 订阅
        ///   MapRender.onSeasonChange（MapRender.cs:32 的委托，curSeason setter 触发）。
        /// 旧管线同一件事由 MapProperty.OnSeasonChange → MapLayer.UpdateRender 完成，
        /// 这里对齐的只是"换季换图"这一层语义。
        ///
        /// 【独立读 .bin 时】layerDiffuseNames 为 null，本方法直接返回（贴图名已按当前季节固定）。
        /// </summary>
        /// <param name="season">季节索引（0 = Autumn，与 MapRender.SeasonNames 同序）</param>
        public void SetSeason(int season)
        {
            Season = Mathf.Clamp(season, 0, SeasonNames.Length - 1);
            if (Layers == null || layerDiffuseNames == null) return;

            for (int i = 0; i < Layers.Length; i++)
            {
                string[] names = i < layerDiffuseNames.Length ? layerDiffuseNames[i] : null;
                if (names == null || Season >= names.Length) continue;

                TerrainSangoLayerInfo info = Layers[i];
                info.DiffuseName = names[Season];   // struct：改完要写回数组
                Layers[i] = info;
            }
        }

        /// <summary>
        /// 按世界 XZ 采样地形高度（双线性，返回世界单位）。
        ///
        /// 【用途】新地形没有 collider（顶点高度在 GPU 上位移，CPU 拿不到精确网格），
        /// 鼠标点地格只能靠高度场做射线求交，见 TerrainSangoBootstrap.TryGetMapHit。
        /// 【轴向】世界 Z → u（列），世界 X → v（行），与 heights / 高度纹理的存储顺序一致。
        /// </summary>
        /// <param name="worldX">世界 X</param>
        /// <param name="worldZ">世界 Z</param>
        /// <returns>该点地形高度（世界单位）；数据缺失时返回 0</returns>
        public float SampleHeightWorld(float worldX, float worldZ)
        {
            if (heights == null || VertexWidth < 2 || VertexHeight < 2 || QuadSize <= 0f) return 0f;

            float u = Mathf.Clamp(worldZ / QuadSize, 0f, VertexWidth - 1.001f);
            float v = Mathf.Clamp(worldX / QuadSize, 0f, VertexHeight - 1.001f);
            int u0 = (int)u;
            int v0 = (int)v;
            int u1 = Mathf.Min(u0 + 1, VertexWidth - 1);
            int v1 = Mathf.Min(v0 + 1, VertexHeight - 1);
            float fu = u - u0;
            float fv = v - v0;

            int row0 = v0 * VertexWidth;
            int row1 = v1 * VertexWidth;
            float h00 = heights[row0 + u0];
            float h10 = heights[row0 + u1];
            float h01 = heights[row1 + u0];
            float h11 = heights[row1 + u1];

            float h = Mathf.Lerp(Mathf.Lerp(h00, h10, fu), Mathf.Lerp(h01, h11, fu), fv);
            return h * HeightScale;
        }

        /// <summary>
        /// 直接从地图 .bin 文件构建数据源。
        ///
        /// 【为什么要单独写一个解析器】
        ///   MapRender.LoadMap 会把格子、图层、地形块、光照、迷雾、模型、标签等 11 个子系统
        ///   全部反序列化并建出一整套旧管线的渲染对象。只想预览高度场时那是纯浪费，
        ///   而且旧地形会和新地形叠着画。这里只读 MapGrid（跳过）与 MapData 的顶点块，
        ///   不碰 MapRender / GameData，因此在编辑器里、甚至在还没进游戏时也能用。
        ///
        /// 【文件布局】与 MapRender.SaveMap 一一对应：
        ///   VERSION(int) / WorkContent(string, v≥6)
        ///   mapWidth(int) / mapHeight(int)              ← 格数，顶点数 = 格数 + 1
        ///   gridSize(int) / 每格字节(terrainType 1 + terrainState 4(v≥7) + areaId 2(v≥8))
        ///   gridTextureName(string, v≥6)
        ///   quadSize(int) / 每顶点(height byte, textureIndex byte, water byte)
        ///
        /// 【格数依赖 quadSize 的鸡生蛋问题】
        ///   格数 = mapWidth * quadSize / gridSize，而 quadSize 写在顶点块之前、格块之后。
        ///   这里用"试探 + 自校验"求解：依次假设 quadSize = 1..64，算出格块长度，
        ///   再验证该位置的字符串（gridTextureName）与紧随其后的 int 是否正好等于这个假设值。
        ///   命中才认为解析成功，避免偏移错位后读出满屏垃圾高度。
        /// </summary>
        /// <param name="filePath">地图路径；可为绝对路径、相对 Content 目录、或相对工程 Build/Content</param>
        /// <returns>数据源实例。</returns>
        public static TerrainSangoSource FromMapBin(string filePath)
        {
            string fullPath = ResolveMapBinPath(filePath);
            if (string.IsNullOrEmpty(fullPath))
                throw new FileNotFoundException(
                    $"找不到地图文件: {filePath}（游戏 Content 目录 = {Sango.Path.ContentRootPath ?? "未初始化"}）");

            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version < 6)
                    throw new NotSupportedException(
                        $"地图版本 {version} 过旧，独立读取只支持 v6 及以上（v6 起格子块与顶点块布局固定）");

                string workContent = reader.ReadString();   // 贴图目录 Assets/Map/{WorkContent}/Terrain/...
                int mapWidth = reader.ReadInt32();
                int mapHeight = reader.ReadInt32();
                int gridSize = reader.ReadInt32();
                if (mapWidth <= 0 || mapHeight <= 0 || gridSize <= 0)
                    throw new InvalidDataException($"地图尺寸非法: {mapWidth}x{mapHeight}, gridSize={gridSize}");

                long gridStart = stream.Position;
                int bytesPerCell = 1;
                if (version >= 7) bytesPerCell += 4;       // terrainState
                if (version >= 8) bytesPerCell += 2;       // areaId

                int quadSize = 0;
                long vertexStart = -1;
                for (int guess = 1; guess <= 64 && quadSize == 0; guess++)
                {
                    int cellsX = mapWidth * guess / gridSize;
                    int cellsY = mapHeight * guess / gridSize;
                    if (cellsX <= 0 || cellsY <= 0) continue;
                    long cellEnd = gridStart + (long)cellsX * cellsY * bytesPerCell;
                    if (cellEnd + 5 >= stream.Length) continue;
                    if (TryProbeQuadSize(stream, reader, cellEnd, guess, out long start))
                    {
                        quadSize = guess;
                        vertexStart = start;
                    }
                }

                if (quadSize == 0 || vertexStart < 0)
                    throw new InvalidDataException("地图格块长度与 quadSize 对不上，无法定位顶点数据（文件可能已损坏）");

                // ---- 格子块：每格首字节是地形 Id（MapGrid.OnLoad 的读取口径）----
                // 取它只是为了判"这格可不可通行"：旧管线 MapGrid.cs:444-450 按
                // TerrainTypes.Get(terrainType).moveable 决定是否画格线，水（河/海）也可通行。
                // 读失败不影响地形本身，只是地格遮罩回退到旧的 water 代理。
                int gridCountZ = mapWidth * quadSize / gridSize;
                int gridCountX = mapHeight * quadSize / gridSize;
                bool[] cellMoveable = null;
                if (gridCountZ > 0 && gridCountX > 0)
                {
                    long cellBytes = (long)gridCountZ * gridCountX * bytesPerCell;
                    if (gridStart + cellBytes <= stream.Length)
                    {
                        try
                        {
                            stream.Position = gridStart;
                            byte[] cellBuffer = reader.ReadBytes((int)cellBytes);
                            if (cellBuffer.Length == cellBytes)
                            {
                                byte[] terrainIds = new byte[gridCountZ * gridCountX];
                                // 每格只取首字节（terrainType），terrainState/areaId 与格线无关
                                for (int i = 0; i < terrainIds.Length; i++)
                                    terrainIds[i] = cellBuffer[i * bytesPerCell];
                                cellMoveable = ResolveCellMoveable(terrainIds, ResolveContentRoot(fullPath));
                            }
                        }
                        catch (System.Exception exception)
                        {
                            Log.Warning($"TerrainSango 读取地图格子块失败，地格遮罩回退为水面代理: {exception.Message}");
                            cellMoveable = null;
                        }
                    }
                }

                int width = mapWidth + 1;                  // 顶点数 = 格数 + 1
                int height = mapHeight + 1;
                long vertexCount = (long)width * height;
                if (stream.Length - vertexStart < vertexCount * 3)
                    throw new InvalidDataException(
                        $"地图顶点数据不足: 需要 {vertexCount * 3} 字节, 实际 {stream.Length - vertexStart} 字节");

                // 逐行读取：磁盘布局是 x 主序（行 = 世界 Z），内存布局是 [v * width + u]（u = 世界 Z）
                // 每顶点 3 字节 = height / textureIndex / water，其中 textureIndex 决定地表图层。
                byte[] lane = new byte[height * 3];
                byte[] data = new byte[vertexCount];
                byte[] layerIndex = new byte[vertexCount];
                byte[] waterIndex = new byte[vertexCount];
                byte minHeight = byte.MaxValue;
                byte maxHeight = byte.MinValue;
                stream.Position = vertexStart;
                for (int x = 0; x < width; x++)
                {
                    if (stream.Read(lane, 0, lane.Length) < lane.Length)
                        throw new InvalidDataException("地图顶点数据不完整，文件可能已损坏");
                    for (int y = 0; y < height; y++)
                    {
                        byte h = lane[y * 3];
                        int i = y * width + x;
                        data[i] = h;
                        layerIndex[i] = lane[y * 3 + 1];
                        waterIndex[i] = lane[y * 3 + 2];
                        if (h < minHeight) minHeight = h;
                        if (h > maxHeight) maxHeight = h;
                    }
                }

                var source = new TerrainSangoSource
                {
                    VertexWidth = width,
                    VertexHeight = height,
                    QuadSize = Mathf.Max(quadSize, 0.001f),
                    HeightScale = 0.5f,
                    heights = data,
                    layerIndexes = layerIndex,
                    waterIndexes = waterIndex,
                    WorkContent = string.IsNullOrEmpty(workContent) ? "Default" : workContent,
                    ContentRoot = ResolveContentRoot(fullPath),
                    GridSize = gridSize,
                };
                source.MinimumHeight = minHeight * source.HeightScale;
                source.MaximumHeight = maxHeight * source.HeightScale;
                source.SetCellMoveable(cellMoveable, gridCountZ, gridCountX);
                source.BuildLayout();
                source.BuildHeightTexture();
                source.BuildLayerIndexTexture();

                // 图层元数据是可选块：读失败只让地表退化成单色，不影响高度场
                try
                {
                    source.ReadLayerBlock(reader, stream);
                }
                catch (System.Exception exception)
                {
                    Log.Warning($"TerrainSango 图层元数据解析失败，地表将退化为单色: {exception.Message}");
                    source.Layers = null;
                }
                return source;
            }
        }

        /// <summary>
        /// 试探某段偏移处是否是 gridTextureName + quadSize，并返回顶点块起点。
        ///
        /// 判据三条：① 7 bit 长度前缀合理；② 字符串全为可打印 ASCII；③ 紧随其后的 int 等于假设的 quadSize。
        /// 三条同时成立才接受，单靠长度容易在损坏文件上误命中。
        /// </summary>
        /// <param name="stream">文件流（本方法会移动其位置）</param>
        /// <param name="reader">读取器</param>
        /// <param name="position">候选偏移（格块结束处）</param>
        /// <param name="expectQuadSize">假设的 quadSize</param>
        /// <param name="vertexStart">命中时输出顶点块起点</param>
        /// <returns>命中返回 true</returns>
        private static bool TryProbeQuadSize(FileStream stream, BinaryReader reader, long position, int expectQuadSize,
            out long vertexStart)
        {
            vertexStart = -1;
            try
            {
                stream.Position = position;
                int nameLength = reader.ReadByte();
                if (nameLength < 0 || nameLength > 128) return false;
                for (int i = 0; i < nameLength; i++)
                {
                    int b = reader.ReadByte();
                    if (b < 32 || b > 126) return false;
                }
                long quadPos = stream.Position;
                if (quadPos + 4 > stream.Length) return false;
                if (reader.ReadInt32() != expectQuadSize) return false;
                vertexStart = stream.Position;
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>
        /// 季节名表，顺序与 MapRender.SeasonNames 严格一致。
        /// </summary>
        public static readonly string[] SeasonNames = { "Autumn", "Spring", "Summer", "Winter" };

        /// <summary>
        /// 独立读 .bin 时使用的季节索引（0 = Autumn，与 MapRender.curSeason 默认值一致）。
        /// </summary>
        public const int CurrentSeason = 0;

        /// <summary>
        /// 本实例使用的季节索引。
        ///
        /// 【为什么要跟着游戏走】游戏内由 <c>MapLayer.curSeason</c> 决定地表贴图与底图，
        /// 写死 0 会在春季/夏季/冬季时加载错季节的贴图（颜色整体不对）。
        /// 独立读 .bin 时没有游戏状态，退回 <see cref="CurrentSeason"/>。
        /// </summary>
        public int Season { get; private set; } = CurrentSeason;

        /// <summary>当前季节名（用于拼 Terrain/{Season}/ 目录）。</summary>
        public string SeasonName => SeasonNames[Mathf.Clamp(Season, 0, SeasonNames.Length - 1)];

        /// <summary>
        /// 把每顶点的图层索引与水面标记上传成 GPU 纹理（RGBA32，1 texel = 1 顶点）。
        ///
        /// 【通道分配】R = 图层索引（MapData.textureIndex），G = 水面标记（water > 0 ? 255 : 0）。
        /// 两者合成一张图，着色器取一次样就能同时拿到"这块地是什么地表"和"这块地有没有水"，
        /// 4 角混合时的采样次数不翻倍。
        ///
        /// 【必须点采样】索引不是颜色，双线性过滤会把 12 和 13 混成非法值，
        /// 表现为图块之间随机错层。点采样 + 着色器里按 4 个角自行插值，才能复现旧管线的顶点 alpha 过渡。
        /// </summary>
        private void BuildLayerIndexTexture()
        {
            if (layerIndexes == null) return;
            ReleaseLayerIndexTexture();

            BuildWaterHeights();

            int count = layerIndexes.Length;
            var pixels = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                byte waterHeight = waterHeights != null ? waterHeights[i] : (byte)0;
                byte waterFlag = waterIndexes != null && waterIndexes[i] > 0 ? (byte)255 : (byte)0;
                pixels[i] = new Color32(layerIndexes[i], waterHeight, waterFlag, 255);
            }

            var texture = new Texture2D(VertexWidth, VertexHeight, TextureFormat.RGBA32, false, true)
            {
                name = "TerrainSangoLayerIndex",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            LayerIndexTexture = texture;
        }

        /// <summary>
        /// 预计算"海面高度字节"：自身有水面就用自己的，否则按旧管线的邻居顺序回退。
        ///
        /// 【为什么必须回退】岸边的陆顶点 water = 0，若直接把 0 当海面高度，
        /// 一个跨岸线的水面三角形就会从海平面斜插到 y=0，岸边出现"水面塌下去"的斜坡。
        /// 旧管线 MapTerrain.VertexPosition 用 8 邻居回退正是为了避免这个。
        /// </summary>
        private void BuildWaterHeights()
        {
            if (waterIndexes == null)
            {
                waterHeights = null;
                return;
            }

            int width = VertexWidth;
            int height = VertexHeight;
            var result = new byte[waterIndexes.Length];

            // 邻居探测顺序与旧管线一致：(左, 上, 左上, 右, 右上, 下, 右下, 左下)
            int[] offsetsU = { -1, 0, -1, 1, 1, 0, 1, -1 };
            int[] offsetsV = { 0, -1, -1, 0, -1, 1, 1, 1 };

            for (int v = 0; v < height; v++)
            {
                int row = v * width;
                for (int u = 0; u < width; u++)
                {
                    byte own = waterIndexes[row + u];
                    if (own > 0)
                    {
                        result[row + u] = own;
                        continue;
                    }

                    for (int k = 0; k < 8; k++)
                    {
                        int nu = u + offsetsU[k];
                        int nv = v + offsetsV[k];
                        if (nu < 0 || nu >= width || nv < 0 || nv >= height) continue;
                        byte neighbour = waterIndexes[nv * width + nu];
                        if (neighbour > 0)
                        {
                            result[row + u] = neighbour;
                            break;
                        }
                    }
                }
            }
            waterHeights = result;
        }

        /// <summary>释放图层索引纹理。</summary>
        private void ReleaseLayerIndexTexture()
        {
            if (LayerIndexTexture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(LayerIndexTexture);
            else UnityEngine.Object.DestroyImmediate(LayerIndexTexture);
            LayerIndexTexture = null;
        }

        /// <summary>
        /// 解析 MapLayer 块（紧跟顶点块之后）。布局见 MapLayer.OnLoad：
        ///   layerSize(int) / 每层[ isLit(bool), textureScale(2 float),
        ///                         4 个季节 × (diffuse(string), normal(string), mask(string)) ]
        ///
        /// 旧管线把最后一层当水面层（构造时传入 waterMat），这里按贴图名是否以 "water" 开头判定，
        /// 比“最后一层”更稳，避免文件层数变化时误判。
        /// </summary>
        private void ReadLayerBlock(BinaryReader reader, FileStream stream)
        {
            if (stream.Position + 4 > stream.Length) return;
            int layerSize = reader.ReadInt32();
            if (layerSize <= 0 || layerSize > 1024) return;

            var list = new TerrainSangoLayerInfo[layerSize];
            for (int i = 0; i < layerSize; i++)
            {
                bool isLit = reader.ReadBoolean();
                float scaleX = reader.ReadSingle();
                float scaleY = reader.ReadSingle();

                string diffuse = null;
                for (int season = 0; season < 4; season++)
                {
                    string name = reader.ReadString();
                    reader.ReadString();      // normalTexName
                    reader.ReadString();      // maskTexName
                    if (season == CurrentSeason) diffuse = name;
                }

                list[i] = new TerrainSangoLayerInfo
                {
                    IsLit = isLit,
                    ScaleX = scaleX,
                    ScaleY = scaleY,
                    DiffuseName = diffuse,
                    IsWater = !string.IsNullOrEmpty(diffuse) && diffuse.StartsWith("water"),
                };
            }
            Layers = list;

            // 水面层下标：旧管线把它作为最后一个覆盖层，用 per-vertex water>0 当 alpha 叠在地表上
            WaterLayerIndex = -1;
            for (int i = 0; i < list.Length; i++)
            {
                if (!list[i].IsWater) continue;
                WaterLayerIndex = i;
                break;
            }
        }

        /// <summary>
        /// 生成地格遮罩纹理（对应旧管线 MapGrid.GridMaskTexture）。
        ///
        /// 【旧管线口径】MapGrid.cs:444-450 只给"可通行"格子写 alpha=255
        /// （<c>TerrainTypes.Get(terrainType).moveable</c>），不可通行的格子 alpha 保持 0，
        /// 网格线就不会画在那里。
        ///
        /// 【关键：水不等于不可通行】本工程 TerrainTypes.json 里「河」Id 8、「海」Id 9 的
        /// moveable = true（水军可走），「川」Id 7、「远海」Id 21、「深海」Id 22 为 false。
        /// 因此必须按格子的 terrainType 判，不能按顶点的 water 标记判 —— 后者会把整片
        /// 水域的格线全部抹掉，可通行的水面也就没有格子了。
        ///
        /// 【两种数据来源】
        ///   ① cellMoveable 已有值（独立读 .bin 时由 FromMapBin 解析格子块得到，
        ///      游戏内由 TerrainSangoRenderer 从 MapGrid.GridMaskTexture 注入）→ 直接用它；
        ///   ② 拿不到时回退到"顶点 water 标记"代理（旧实现），保证编辑器里仍能出图。
        ///
        /// 【索引约定】必须与着色器的 maskUV 一致：texel.x = 格 Z 下标，
        /// texel.y = 格数Y - 1 - 格X 下标（旧管线 Y 轴垂直翻转存法）。
        /// </summary>
        public Texture2D CreateGridMaskTexture()
        {
            if (GridSize <= 0 || QuadSize <= 0f) return null;

            // 有逐格数据时直接以它的格数为准：游戏内路径的格数来自 MapGrid.GridMaskTexture，
            // 与该地图真实网格严格一致，不依赖 WorldWidth / GridSize 的取整推算
            bool hasCells = cellMoveable != null && cellCountZ > 0 && cellCountX > 0
                            && cellMoveable.Length == cellCountZ * cellCountX;
            int cellsZ = hasCells ? cellCountZ : Mathf.Max(Mathf.RoundToInt(WorldWidth / GridSize), 1);
            int cellsX = hasCells ? cellCountX : Mathf.Max(Mathf.RoundToInt(WorldHeight / GridSize), 1);
            bool useCells = hasCells;
            if (!useCells && waterIndexes == null) return null;

            int step = Mathf.Max(Mathf.RoundToInt(GridSize / QuadSize), 1);   // 一格 = 几个顶点
            int half = step / 2;

            var pixels = new Color32[cellsZ * cellsX];
            for (int cx = 0; cx < cellsX; cx++)
            {
                int texelY = cellsX - 1 - cx;
                int v = cx * step + half;
                if (v >= VertexHeight) v = VertexHeight - 1;
                for (int cz = 0; cz < cellsZ; cz++)
                {
                    bool moveable;
                    if (useCells)
                    {
                        // 与格子块同序：索引 = 格Z * cellsX + 格X
                        moveable = cellMoveable[cz * cellsX + cx];
                    }
                    else
                    {
                        int u = cz * step + half;
                        if (u >= VertexWidth) u = VertexWidth - 1;
                        moveable = waterIndexes[v * VertexWidth + u] == 0;
                    }
                    pixels[texelY * cellsZ + cz] = new Color32(0, 0, 0, moveable ? (byte)255 : (byte)0);
                }
            }

            var texture = new Texture2D(cellsZ, cellsX, TextureFormat.RGBA32, false, true)
            {
                name = "TerrainSangoGridMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// 注入"每格是否可通行"。
        ///
        /// 【谁注入】
        ///   ① 独立读 .bin（FromMapBin）：自己解析格子块 + 查 TerrainTypes 得到，构造时已填好；
        ///   ② 游戏内（FromMapData）：由 TerrainSangoRenderer 从旧管线已算好的
        ///      MapGrid.GridMaskTexture 逐像素读取后注入 —— 直接复用 MapGrid.cs:444-450
        ///      按 moveable 生成的结果，无需再次查地形表，也保证新旧管线完全一致。
        /// </summary>
        /// <param name="moveable">索引 = 格Z * countX + 格X（与地图文件格子块同序）</param>
        /// <param name="countZ">格 Z 方向格数</param>
        /// <param name="countX">格 X 方向格数</param>
        public void SetCellMoveable(bool[] moveable, int countZ, int countX)
        {
            if (moveable == null || countZ <= 0 || countX <= 0) return;
            if (moveable.Length != countZ * countX) return;
            cellMoveable = moveable;
            cellCountZ = countZ;
            cellCountX = countX;
        }

        /// <summary>
        /// 设置地格边长（游戏内路径以 <c>MapGrid.gridSize</c> 为准）。
        ///
        /// 【为什么需要】FromMapData 只有顶点数据，拿不到地图真实格边长，
        /// 若沿用默认 20 而地图实际是别的值，格线位置会整体错位。
        /// </summary>
        /// <param name="gridSize">地格边长（世界单位）</param>
        public void SetGridSize(int gridSize)
        {
            if (gridSize > 0) GridSize = gridSize;
        }

        /// <summary>
        /// 把"每格地形 Id"翻译成"每格是否可通行"。
        ///
        /// 【口径来源】MapGrid.cs:444-450：
        ///   <c>TerrainType t = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(data.terrainType);
        ///   if (t != null &amp;&amp; t.moveable) maskPixels[...].a = 255;</c>
        /// 即：取不到地形类型（Id 越界/表缺项）时视为不可通行，不画格线。
        /// </summary>
        /// <param name="terrainIds">每格地形 Id，长度 = 格数</param>
        /// <param name="contentRoot">Content 根目录，用于独立加载地形类型表</param>
        /// <returns>每格可通行标记；地形类型表不可用时返回 null（调用方回退到 water 代理）</returns>
        private static bool[] ResolveCellMoveable(byte[] terrainIds, string contentRoot)
        {
            if (terrainIds == null || terrainIds.Length == 0) return null;
            bool[] table = ResolveMoveableTable(contentRoot);
            if (table == null) return null;

            bool[] result = new bool[terrainIds.Length];
            for (int i = 0; i < terrainIds.Length; i++)
            {
                int id = terrainIds[i];
                result[i] = id < table.Length && table[id];
            }
            return result;
        }

        /// <summary>
        /// 取"地形 Id → 是否可通行"的查表数组（下标 = 地形 Id）。
        ///
        /// 【为什么要两条来源】
        ///   ① 游戏内已加载剧本数据时，<c>GameData.Instance.ScenarioCommonData.TerrainTypes</c>
        ///      是最终生效的表（含 Mod 覆盖），必须优先，否则会与实际寻路不一致；
        ///   ② 编辑器里没进游戏（独立读 .bin 预览地图）时 GameData 的 ScenarioCommonData 为 null，
        ///      此时从 <c>{Content}/Data/Common/TerrainTypes.json</c> 独立解析一份 ——
        ///      ScenarioCommonData.Load 是普通的 JSON PopulateObject，不依赖任何游戏运行时状态。
        ///   ③ 两条都拿不到则返回 null，让地格遮罩回退到旧的 water 代理，保证不崩。
        /// </summary>
        private static bool[] ResolveMoveableTable(string contentRoot)
        {
            SangoObjectSet<TerrainType> set = null;

            GameData game = GameData.Instance;
            if (game != null && game.ScenarioCommonData != null)
                set = game.ScenarioCommonData.TerrainTypes;

            if (set == null)
            {
                string root = !string.IsNullOrEmpty(contentRoot) ? contentRoot : Sango.Path.ContentRootPath;
                if (string.IsNullOrEmpty(root)) return null;
                string file = System.IO.Path.Combine(root, "Data", "Common", "TerrainTypes.json");
                if (!File.Exists(file)) return null;
                try
                {
                    ScenarioCommonData common = new ScenarioCommonData();
                    common.Load(file);
                    set = common.TerrainTypes;
                }
                catch (System.Exception exception)
                {
                    Log.Warning($"TerrainSango 读取地形类型表失败，地格遮罩回退为水面代理: {exception.Message}");
                    return null;
                }
            }

            TerrainType[] objects = set != null ? set.objects : null;
            if (objects == null || objects.Length == 0) return null;

            bool[] table = new bool[objects.Length];
            for (int i = 0; i < objects.Length; i++)
                table[i] = objects[i] != null && objects[i].moveable;
            return table;
        }

        /// <summary>
        /// 当前季节的地表贴图目录：{Content}/Assets/Map/{WorkContent}/Terrain/{Season}，
        /// 找不到时回落到 Default。定位失败返回 null（地表退化为单色）。
        /// </summary>
        public string ResolveTerrainTextureDirectory()
        {
            string root = ResolveMapAssetDirectory();
            if (string.IsNullOrEmpty(root)) return null;
            string mine = System.IO.Path.Combine(root, "Terrain", SeasonName);
            if (Directory.Exists(mine)) return mine;
            string fallback = System.IO.Path.Combine(ContentRoot, "Assets", "Map", "Default", "Terrain", SeasonName);
            return Directory.Exists(fallback) ? fallback : null;
        }

        /// <summary>
        /// 地图资源根目录：{Content}/Assets/Map/{WorkContent}。
        /// 底图在 BaseTex/、地格在 Grid/、地表贴图在 Terrain/{Season}/，都挂在它下面。
        /// </summary>
        public string ResolveMapAssetDirectory()
        {
            if (string.IsNullOrEmpty(ContentRoot)) return null;
            string baseDir = System.IO.Path.Combine(ContentRoot, "Assets", "Map");
            string mine = System.IO.Path.Combine(baseDir, WorkContent);
            if (Directory.Exists(mine)) return mine;
            string fallback = System.IO.Path.Combine(baseDir, "Default");
            return Directory.Exists(fallback) ? fallback : null;
        }

        /// <summary>
        /// 由地图文件路径反推 Content 根目录（约定：地图放在 {Content}/Map/xxx.bin）。
        /// </summary>
        private static string ResolveContentRoot(string binFullPath)
        {
            string mapDir = System.IO.Path.GetDirectoryName(binFullPath);
            if (string.IsNullOrEmpty(mapDir)) return null;
            string content = System.IO.Path.GetDirectoryName(mapDir);
            return string.IsNullOrEmpty(content) ? null : content;
        }

        /// <summary>
        /// 解析地图路径：绝对路径 → Sango.Path 搜索路径 → Content 根目录 → 工程 Build/Content。
        /// 全部落空返回 null。
        /// </summary>
        private static string ResolveMapBinPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            // ① 绝对路径 / 当前工作目录相对路径
            if (File.Exists(filePath))
                return System.IO.Path.GetFullPath(filePath);

            // ② 游戏内注册的搜索路径（模组、Content 等）
            string found = Sango.Path.FindFile(filePath);
            if (!string.IsNullOrEmpty(found) && File.Exists(found))
                return found;

            // ③ Content 根目录（Path.Init 之后才有值）
            string content = Sango.Path.ContentRootPath;
            if (!string.IsNullOrEmpty(content))
            {
                string candidate = System.IO.Path.Combine(content, filePath);
                if (File.Exists(candidate)) return candidate;
            }

            // ④ 编辑器里 Path.Init 可能还没执行过，按工程布局逐层向上找 Build/Content
            //    （工程可能被放在 <根>/Project 下，也可能是 <根>/ 本身就是 Unity 工程）
            string[] ups = { "..", "../..", "../../.." };
            for (int i = 0; i < ups.Length; i++)
            {
                string candidate = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, ups[i], "Build", "Content", filePath));
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        /// <summary>
        /// 生成一份程序化地形（阶段 1 的独立验证用，不依赖任何地图资源）。
        ///
        /// 用确定性的多倍频值噪声，保证每次运行结果一致，方便对比 LOD 是否稳定。
        /// </summary>
        /// <param name="width">顶点数（世界 Z 方向）。</param>
        /// <param name="height">顶点数（世界 X 方向）。</param>
        /// <param name="quadSize">顶点间距。</param>
        /// <param name="seed">噪声种子。</param>
        /// <returns>数据源实例。</returns>
        public static TerrainSangoSource CreateProcedural(int width, int height, float quadSize, int seed = 1337)
        {
            width = Mathf.Max(width, 128);
            height = Mathf.Max(height, 128);

            var source = new TerrainSangoSource
            {
                VertexWidth = width,
                VertexHeight = height,
                QuadSize = Mathf.Max(quadSize, 0.001f),
                HeightScale = 0.5f,
                heights = new byte[width * height],
            };

            // 多倍频值噪声：基础起伏 + 山脊 + 细节，幅度都压在 0..255
            for (int v = 0; v < height; v++)
            {
                for (int u = 0; u < width; u++)
                {
                    float nx = u / (float)(width - 1);
                    float nz = v / (float)(height - 1);
                    float h = 0f;
                    h += ValueNoise(nx * 3f, nz * 3f, seed) * 0.55f;
                    h += ValueNoise(nx * 7f, nz * 7f, seed + 17) * 0.28f;
                    h += ValueNoise(nx * 19f, nz * 19f, seed + 31) * 0.12f;
                    h += ValueNoise(nx * 53f, nz * 53f, seed + 47) * 0.05f;
                    // 抬一点基座，避免整图压在 0 高度
                    h = Mathf.Clamp01(h * 1.15f + 0.12f);
                    source.heights[v * width + u] = (byte)Mathf.Clamp(Mathf.RoundToInt(h * 255f), 0, 255);
                }
            }

            source.MinimumHeight = 0f;
            source.MaximumHeight = 255f * source.HeightScale;
            source.BuildLayout();
            source.BuildHeightTexture();
            return source;
        }

        /// <summary>
        /// 计算四叉树参数。
        ///
        /// 叶 patch 覆盖 64 格；每升一层，单边格数翻倍。
        /// 单轴需要的叶节点数向上取到 2 的幂，保证根节点正好是正方形，
        /// 超出地图范围的节点在 LOD 选择阶段按"与地图矩形不相交"直接剪掉。
        /// </summary>
        private void BuildLayout()
        {
            int leafPerAxis = Mathf.Max(
                Mathf.CeilToInt((VertexWidth - 1) / (float)TerrainSangoConstants.PatchCells),
                Mathf.CeilToInt((VertexHeight - 1) / (float)TerrainSangoConstants.PatchCells));
            leafPerAxis = Mathf.Max(leafPerAxis, 1);

            RootPatchCount = Mathf.NextPowerOfTwo(leafPerAxis);
            LevelCount = Mathf.RoundToInt(Mathf.Log(RootPatchCount, 2f)) + 1;
        }

        /// <summary>把 CPU 高度数组上传成 GPU 纹理。</summary>
        private void BuildHeightTexture()
        {
            ReleaseTexture();
            // linear = true：这是数据不是颜色，走 sRGB 解码会把高度值算错
            var texture = new Texture2D(VertexWidth, VertexHeight, HeightFormat, false, true)
            {
                name = "TerrainSangoHeight",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixelData(heights, 0);
            texture.Apply(false, false);
            HeightTexture = texture;
        }

        /// <summary>
        /// 按世界坐标取地形高度（CPU 侧，双线性）。
        ///
        /// 与着色器的采样口径一致：u = 世界 Z / QuadSize，v = 世界 X / QuadSize。
        /// 阶段 3 接游戏后，地格层、模型贴地、植被都走这个入口，
        /// 保证 GPU 位移与 CPU 查询结果不会分叉。
        /// </summary>
        public float SampleWorldHeight(float worldX, float worldZ)
        {
            if (heights == null) return 0f;

            float fx = worldZ / QuadSize;
            float fy = worldX / QuadSize;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, VertexWidth - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, VertexHeight - 1);
            int x1 = Mathf.Min(x0 + 1, VertexWidth - 1);
            int y1 = Mathf.Min(y0 + 1, VertexHeight - 1);
            float tx = Mathf.Clamp01(fx - x0);
            float ty = Mathf.Clamp01(fy - y0);

            float h00 = heights[y0 * VertexWidth + x0];
            float h10 = heights[y0 * VertexWidth + x1];
            float h01 = heights[y1 * VertexWidth + x0];
            float h11 = heights[y1 * VertexWidth + x1];

            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(h0, h1, ty) * HeightScale;
        }

        /// <summary>确定性值噪声（哈希插值），只用于程序化演示地形。</summary>
        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float tx = x - xi;
            float ty = y - yi;
            // smoothstep 让插值连续
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            float v00 = Hash01(xi, yi, seed);
            float v10 = Hash01(xi + 1, yi, seed);
            float v01 = Hash01(xi, yi + 1, seed);
            float v11 = Hash01(xi + 1, yi + 1, seed);

            return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
        }

        /// <summary>整数坐标 → [0,1] 的确定性哈希。</summary>
        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695040;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        /// <summary>释放 GPU 纹理。</summary>
        public void ReleaseTexture()
        {
            if (HeightTexture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(HeightTexture);
            else UnityEngine.Object.DestroyImmediate(HeightTexture);
            HeightTexture = null;
        }

        /// <summary>释放全部资源。</summary>
        public void Dispose()
        {
            ReleaseTexture();
            ReleaseLayerIndexTexture();
            heights = null;
            layerIndexes = null;
            waterIndexes = null;
            waterHeights = null;
            cellMoveable = null;
            layerDiffuseNames = null;
            Layers = null;
        }
    }
}
