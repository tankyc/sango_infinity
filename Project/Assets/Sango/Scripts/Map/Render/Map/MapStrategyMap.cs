using Sango.Core;
using Sango.Hexagon;
using Sango.MapGen;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 战略地图图层：把「每格归属」烘焙成一张与地格信息掩图同尺寸的归属贴图。
    ///
    /// 尺寸约定：贴图宽高 = <see cref="MapGrid"/> 的 bounds（也就是 GridMaskTexture 的尺寸），
    /// 一个地格严格对应一个像素，颜色完全由 CPU 端逐格 SetPixels32 决定，
    /// 不使用任何 shader / Blit 参与生成（仅用 Graphics.CopyTexture 原样拷到 RenderTexture）。
    ///
    /// 颜色规则：
    /// 1. 地格有所属（<see cref="Cell.BelongCity"/> 非空）→ 所属势力的旗色；
    ///    城池无势力时用白色兜底；
    /// 2. 无归属海面（<see cref="TerrainIds.Sea"/>）→ 不参与，保持透明；
    /// 3. 无归属其他水体（河 / 川 / 浅滩）→ 水体色，透明度减半，透出一点地形；
    /// 4. 无归属且可以通行 → 白色兜底；
    /// 5. 无归属且不可通行（高山 / 绝壁）→ 默认透明；只要紧邻已着色区域，就作为边缘涂成纯黑不透明；
    /// 6. 地域边界（相邻两格所属城池不同）→ 整格纯黑，宽度正好一个地格。
    ///
    /// 坐标约定与 <see cref="MapGrid.GridMaskTexture"/> 完全一致：
    /// 纹理 x 轴是地格列，纹理 y 轴需要翻转（<c>py = 高度 - 1 - 格 y</c>），
    /// 因此可以直接复用 shader 里 gridMaskUV 的 odd-q 采样公式，不需要再做世界坐标换算。
    /// </summary>
    public class MapStrategyMap : MapProperty
    {
        /// <summary>生成的势力归属贴图（shader 中通过全局纹理 _FactionMap 使用）</summary>
        public RenderTexture FactionMapRT { get; private set; }

        /// <summary>整体不透明度（0~1），用于把战略图层压在地形之上</summary>
        public float opacity = 1f;

        /// <summary>是否绘制地域边界（相邻格属于不同城池辖区，整格纯黑），默认开启</summary>
        public bool showAreaBorder = true;

        /// <summary>边界格颜色，默认纯黑</summary>
        public Color32 borderColor = new Color32(0, 0, 0, 255);

        /// <summary>无势力但可通行地格的兜底色，默认白色</summary>
        public Color32 neutralColor = new Color32(255, 255, 255, 255);

        /// <summary>无归属水体的颜色，默认黑色（水体同样参与着色，不留透明空洞）</summary>
        public Color32 waterColor = new Color32(0, 0, 0, 255);

        /// <summary>当前是否可见</summary>
        bool _visible;

        /// <summary>归属数据是否已过期（城池易主、切换剧本等）</summary>
        bool _dirty = true;

        /// <summary>当前贴图宽度（像素 = 地格列数）</summary>
        int _texWidth;

        /// <summary>当前贴图高度（像素 = 地格行数）</summary>
        int _texHeight;

        /// <summary>CPU 侧的中间贴图，最终原样拷贝到 <see cref="FactionMapRT"/></summary>
        Texture2D _sourceTexture;

        /// <summary>CPU 侧像素缓冲，复用以避免每次重建都分配大数组</summary>
        Color32[] _pixels;

        /// <summary>逐格归属城池缓存（地域边界的判定依据，即 areaId 对应的城池）</summary>
        City[] _cellCities;

        /// <summary>逐格是否需要着色（水域 / 高山等不可行走地形同样参与，保证整图无空洞）</summary>
        bool[] _cellPainted;

        /// <summary>逐格底色缓存（势力色 / 白色兜底 / 水体色 / 不可行走黑色），下标 = x * 高度 + y</summary>
        Color32[] _cellBaseColors;

        /// <summary>构造战略地图图层</summary>
        /// <param name="map">所属的地图渲染对象</param>
        public MapStrategyMap(MapRender map) : base(map)
        {
        }

        /// <summary>初始化：注册归属变化事件，并保证 shader 侧处于关闭状态</summary>
        public override void Init()
        {
            base.Init();
            // 先退订再订阅：GameEvent 是静态总线，重复 Init 不能把同一回调挂两次
            GameEvent.OnCityFall -= OnCityFall;
            GameEvent.OnCityFall += OnCityFall;
            GameEvent.OnScenarioInit -= OnScenarioInit;
            GameEvent.OnScenarioInit += OnScenarioInit;
            Shader.SetGlobalFloat("_FactionMapAlpha", 0f);
        }

        /// <summary>清理：退订事件并释放贴图资源</summary>
        public override void Clear()
        {
            base.Clear();
            GameEvent.OnCityFall -= OnCityFall;
            GameEvent.OnScenarioInit -= OnScenarioInit;
            ReleaseTexture();
            Shader.SetGlobalTexture("_FactionMap", null);
            Shader.SetGlobalFloat("_FactionMapAlpha", 0f);
            _visible = false;
        }

        /// <summary>每帧更新：只在需要时重建贴图，避免战斗中连续易主时的重复开销</summary>
        public override void Update()
        {
            if (_visible && _dirty)
                Rebuild();
        }

        /// <summary>显示 / 隐藏战略地图图层</summary>
        /// <param name="visible">是否显示</param>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible && _dirty)
                Rebuild();
            Shader.SetGlobalFloat("_FactionMapAlpha", visible ? opacity : 0f);
        }

        /// <summary>当前是否显示战略地图</summary>
        public bool IsVisible => _visible;

        /// <summary>归属贴图是否需要重建（城池易主等变化后为 true，UI 预览可据此驱动刷新）</summary>
        public bool IsDirty => _dirty;

        /// <summary>当前贴图的宽高比（宽 / 高），未生成贴图时返回 1</summary>
        public float Aspect => FactionMapRT != null && FactionMapRT.height > 0
            ? (float)FactionMapRT.width / FactionMapRT.height
            : 1f;

        /// <summary>设置整体不透明度</summary>
        /// <param name="value">0~1</param>
        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            if (_visible)
                Shader.SetGlobalFloat("_FactionMapAlpha", opacity);
        }

        /// <summary>开关地域边界（城池辖区界线，会立即重建贴图）</summary>
        /// <param name="show">是否显示</param>
        public void SetAreaBorderVisible(bool show)
        {
            if (showAreaBorder == show)
                return;
            showAreaBorder = show;
            RebuildIfNeeded();
        }

        /// <summary>打脏标记：归属数据发生变化时调用，下一帧自动重建</summary>
        public void MarkDirty()
        {
            _dirty = true;
            if (_visible)
                Rebuild();
        }

        /// <summary>可见时立即重建</summary>
        void RebuildIfNeeded()
        {
            _dirty = true;
            if (_visible)
                Rebuild();
        }

        /// <summary>城池易主：归属色需要刷新</summary>
        void OnCityFall(City city, Force lastForce, Troop troop)
        {
            MarkDirty();
        }

        /// <summary>切换 / 初始化剧本：整张图重来</summary>
        void OnScenarioInit(Scenario scenario)
        {
            MarkDirty();
        }

        /// <summary>
        /// 重建势力归属贴图。
        /// 流程：逐格归集归属 → 逐像素设置 Color → SetPixels32 上传 → CopyTexture 拷到 RenderTexture。
        /// </summary>
        public void Rebuild()
        {
            _dirty = false;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null || map == null || map.mapGrid == null)
            {
                // 没有剧本（例如地图编辑器场景）时不做任何着色，保持地形原貌。
                // 保持脏标记，等剧本数据就绪后由下一帧自动补一次重建。
                _dirty = true;
                Shader.SetGlobalTexture("_FactionMap", null);
                Shader.SetGlobalFloat("_FactionMapAlpha", 0f);
                return;
            }

            // 贴图尺寸与地格信息掩图（GridMaskTexture）保持一致：一格一像素
            int cellsX = map.mapGrid.bounds.x;
            int cellsY = map.mapGrid.bounds.y;
            if (cellsX <= 0 || cellsY <= 0)
                return;

            EnsureTexture(cellsX, cellsY);

            int cellCount = cellsX * cellsY;
            EnsureCellCache(cellCount);

            // ---------- 一、逐格确定底色（水域 / 不可通行地形同样参与） ----------
            for (int x = 0; x < cellsX; ++x)
            {
                for (int y = 0; y < cellsY; ++y)
                {
                    Cell cell = scenario.Map.GetCell(x, y);
                    if (cell == null || cell.TerrainType == null)
                        continue;

                    int idx = x * cellsY + y;
                    City city = cell.BelongCity;

                    if (city == null)
                    {
                        // 海不参与战略图逻辑，保持透明，让地形自己显示
                        if (cell.TerrainType.Id == TerrainIds.Sea)
                            continue;

                        if (cell.TerrainType.isWater)
                        {
                            // 其他水体（河 / 川 / 浅滩）：透明度减半，透出一点地形
                            Color32 water = waterColor;
                            water.a = (byte)(water.a * 0.5f);
                            _cellPainted[idx] = true;
                            _cellBaseColors[idx] = water;
                            continue;
                        }

                        // 无归属且不可通行（高山 / 绝壁）：透明化，不参与绘制
                        if (!cell.TerrainType.moveable)
                            continue;

                        // 无归属但可以通行的荒野：兜底白色，避免出现空洞
                        _cellPainted[idx] = true;
                        _cellBaseColors[idx] = neutralColor;
                        continue;
                    }

                    // 只要地格有所属（有 areaId）就上色，不再看地形能不能走
                    Force force = city.IsAlive ? city.BelongForce : null;
                    _cellPainted[idx] = true;
                    _cellCities[idx] = city;
                    _cellBaseColors[idx] = force != null ? GetForceColor(force) : neutralColor;
                }
            }

            // ---------- 二、边缘补齐：紧邻着色区域的不可行走地格涂黑 ----------
            // 海完全不参与；高山 / 绝壁本身透明，只有贴着已着色区域的那一圈才作为边缘涂黑。
            // 先收集下标再统一写入，避免边扫描边扩散、黑边越扩越粗。
            System.Collections.Generic.List<int> edgeCells = new System.Collections.Generic.List<int>();
            for (int x = 0; x < cellsX; ++x)
            {
                for (int y = 0; y < cellsY; ++y)
                {
                    int idx = x * cellsY + y;
                    if (_cellPainted[idx])
                        continue;

                    Cell cell = scenario.Map.GetCell(x, y);
                    if (cell == null || cell.TerrainType == null)
                        continue;

                    // 海不参与逻辑，哪怕贴着陆地也不描边
                    if (cell.TerrainType.Id == TerrainIds.Sea)
                        continue;

                    // 只处理不可行走的陆地（其他水体在归集阶段已经处理）
                    if (cell.TerrainType.isWater || cell.TerrainType.moveable)
                        continue;

                    if (HasPaintedNeighbor(x, y, cellsX, cellsY))
                        edgeCells.Add(idx);
                }
            }

            for (int i = 0; i < edgeCells.Count; ++i)
            {
                int idx = edgeCells[i];
                // 边缘必须是纯黑不透明，不能跟着水体一起半透明
                Color32 edgeColor = borderColor;
                edgeColor.a = 255;
                _cellPainted[idx] = true;
                _cellBaseColors[idx] = edgeColor;
            }

            // ---------- 三、逐格写颜色（叠加地域边界） ----------
            Color32[] pixels = _pixels;
            System.Array.Clear(pixels, 0, pixels.Length);

            int paintedCount = 0;
            int borderCount = 0;

            for (int x = 0; x < cellsX; ++x)
            {
                for (int y = 0; y < cellsY; ++y)
                {
                    int idx = x * cellsY + y;
                    if (!_cellPainted[idx])
                        continue;

                    paintedCount++;

                    Color32 color = _cellBaseColors[idx];

                    if (showAreaBorder && CollectBorders(x, y, cellsX, cellsY, _cellCities[idx]))
                    {
                        color = borderColor;
                        borderCount++;
                    }

                    // 纹理 y 轴翻转：格 y 越大，写到纹理越靠下的行
                    pixels[(cellsY - 1 - y) * cellsX + x] = color;
                }
            }

            // ---------- 四、上传到 GPU（纯数据拷贝，不经过 shader） ----------
            _sourceTexture.SetPixels32(pixels);
            _sourceTexture.Apply(false, false);
            Graphics.CopyTexture(_sourceTexture, FactionMapRT);

            Shader.SetGlobalTexture("_FactionMap", FactionMapRT);
            Shader.SetGlobalFloat("_FactionMapAlpha", _visible ? opacity : 0f);

            Log.Info($"战略地图重建: 尺寸={cellsX}x{cellsY}, 上色格={paintedCount}, 地域边界格={borderCount}, 不可行走边缘格={edgeCells.Count}, 边界色={borderColor}, 水体色={waterColor}, 地域边界开关={showAreaBorder}");
        }

        /// <summary>检查六邻居中是否存在已着色的地格</summary>
        bool HasPaintedNeighbor(int x, int y, int cellsX, int cellsY)
        {
            Hex center = Coord.OffsetToCube(x, y);
            for (int d = 0; d < 6; ++d)
            {
                Coord neighborCoord = Coord.OffsetFromCube(center.Neighbor(d));
                int nx = neighborCoord.col;
                int ny = neighborCoord.row;

                if (nx < 0 || ny < 0 || nx >= cellsX || ny >= cellsY)
                    continue;

                if (_cellPainted[nx * cellsY + ny])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断当前格是否属于地域边界格（边界整格涂黑，宽度正好一个地格）。
        ///
        /// 约定：
        /// · 只和「同样需要着色的邻居」比较，水域 / 山地无主地 / 地图外不参与，
        ///   否则整条海岸线都会变成黑边；
        /// · 一条交界线只由坐标较小的一侧画，避免两侧各画一格、宽度翻倍；
        /// · 只按城池辖区（areaId 对应的 <see cref="Cell.BelongCity"/>）判定，不区分势力。
        /// </summary>
        bool CollectBorders(int x, int y, int cellsX, int cellsY, City city)
        {
            Hex center = Coord.OffsetToCube(x, y);
            for (int d = 0; d < 6; ++d)
            {
                Coord neighborCoord = Coord.OffsetFromCube(center.Neighbor(d));
                int nx = neighborCoord.col;
                int ny = neighborCoord.row;

                if (nx < 0 || ny < 0 || nx >= cellsX || ny >= cellsY)
                    continue;

                int nidx = nx * cellsY + ny;
                if (!_cellPainted[nidx])
                    continue;

                // 一条交界只由坐标较小的一侧出黑格，保证边界线总宽为一格
                if (!(y < ny || (y == ny && x < nx)))
                    continue;

                // 地域边界：两侧都属于某个城池，且城池不同（即 areaId 不同）
                if (city != null && _cellCities[nidx] != null
                    && !ReferenceEquals(_cellCities[nidx], city))
                    return true;
            }

            return false;
        }

        /// <summary>取得势力的旗帜色，缺失时返回透明</summary>
        static Color32 GetForceColor(Force force)
        {
            if (force == null || force.mFlag == null)
                return new Color32(0, 0, 0, 0);
            return force.mFlag.color;
        }

        /// <summary>保证逐格归属缓存长度够用</summary>
        void EnsureCellCache(int cellCount)
        {
            if (_cellCities != null && _cellCities.Length == cellCount)
            {
                System.Array.Clear(_cellCities, 0, cellCount);
                System.Array.Clear(_cellPainted, 0, cellCount);
                return;
            }

            _cellCities = new City[cellCount];
            _cellPainted = new bool[cellCount];
            _cellBaseColors = new Color32[cellCount];
        }

        /// <summary>保证 CPU 贴图 / RenderTexture / 像素缓冲尺寸正确（一格一像素）</summary>
        void EnsureTexture(int width, int height)
        {
            if (width == _texWidth && height == _texHeight && _sourceTexture != null && FactionMapRT != null)
                return;

            ReleaseTexture();

            _texWidth = width;
            _texHeight = height;

            _sourceTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            _sourceTexture.name = "StrategyFactionMapSource";
            _sourceTexture.wrapMode = TextureWrapMode.Clamp;
            _sourceTexture.filterMode = FilterMode.Point;

            _pixels = new Color32[width * height];

            FactionMapRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            FactionMapRT.name = "StrategyFactionMap";
            FactionMapRT.wrapMode = TextureWrapMode.Clamp;
            FactionMapRT.filterMode = FilterMode.Point;
            FactionMapRT.useMipMap = false;
            FactionMapRT.autoGenerateMips = false;
            FactionMapRT.Create();
        }

        /// <summary>释放与贴图相关的所有托管 / 非托管资源</summary>
        void ReleaseTexture()
        {
            if (_sourceTexture != null)
            {
                UnityEngine.Object.Destroy(_sourceTexture);
                _sourceTexture = null;
            }

            if (FactionMapRT != null)
            {
                FactionMapRT.Release();
                UnityEngine.Object.Destroy(FactionMapRT);
                FactionMapRT = null;
            }

            _pixels = null;
            _texWidth = 0;
            _texHeight = 0;
        }
    }
}
