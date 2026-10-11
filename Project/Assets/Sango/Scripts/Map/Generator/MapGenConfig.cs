using UnityEngine;

namespace Sango.MapGen
{
    /// <summary>
    /// 程序化地图生成的参数配置。
    /// 所有参数均为归一化比例或频率，便于在不同地图尺寸下复用同一套配置。
    /// 生成结果通过"占比对齐(quantile)"的方式保证地形比例稳定，不依赖噪声本身的绝对取值范围。
    /// </summary>
    public class MapGenConfig
    {
        /// <summary>
        /// 随机种子，用于保证同一配置可复现出完全相同的地图。
        /// </summary>
        public int seed = 20261010;

        // ------------------------------------------------------------------
        // 一、地形比例（通过分位数对齐，最直观的调节项）
        // ------------------------------------------------------------------

        /// <summary>
        /// 陆地占比(0~1)。高度场中高于该比例的顶点被判定为陆地，其余为水域。
        /// 例如 0.55 表示约 55% 的顶点为陆地。
        /// </summary>
        [Range(0.05f, 0.95f)] public float landRatio = 0.55f;

        /// <summary>
        /// 山地占陆地的比例(0~1)。陆地顶点高度排名前该比例的顶点被判定为山脉(崖)。
        /// </summary>
        [Range(0f, 0.6f)] public float mountainRatio = 0.20f;

        /// <summary>
        /// 浅滩占水域的比例(0~1)。水域中较浅的部分判定为浅滩，其余为海。
        /// </summary>
        [Range(0f, 0.6f)] public float shallowRatio = 0.12f;

        // ------------------------------------------------------------------
        // 二、气候与地表覆盖
        // ------------------------------------------------------------------

        /// <summary>
        /// 湿度阈值上限：湿度高于此值的陆地判定为森林。
        /// </summary>
        [Range(0.3f, 0.9f)] public float wetThreshold = 0.56f;

        /// <summary>
        /// 湿度阈值下限：湿度低于此值的陆地判定为荒地。
        /// </summary>
        [Range(0.1f, 0.7f)] public float dryThreshold = 0.42f;

        /// <summary>
        /// 极干阈值：湿度低于此值的陆地判定为砂地（沙漠）。
        /// </summary>
        [Range(0f, 0.4f)] public float sandThreshold = 0.26f;

        /// <summary>
        /// 极湿且低海拔的陆地判定为湿地。
        /// </summary>
        [Range(0.5f, 0.95f)] public float swampThreshold = 0.74f;

        /// <summary>
        /// 湿地允许的最大相对海拔(0~1)，超过则不会生成湿地。
        /// </summary>
        [Range(0f, 1f)] public float swampMaxAltitude = 0.5f;

        /// <summary>
        /// 草地/土 的次级噪声分割阈值(0~1)，高于该值判定为土，否则为草地。
        /// </summary>
        [Range(0f, 1f)] public float soilThreshold = 0.5f;

        // ------------------------------------------------------------------
        // 三、噪声参数
        // ------------------------------------------------------------------

        /// <summary>
        /// 地形特征尺寸，单位为"格"。越小地形越破碎，越大越平缓。
        /// </summary>
        [Range(4f, 128f)] public float terrainFeatureSize = 26f;

        /// <summary>
        /// 气候(湿度)特征尺寸，单位为"格"。
        /// </summary>
        [Range(8f, 256f)] public float climateFeatureSize = 70f;

        /// <summary>
        /// 大陆轮廓频率系数(0.05~2)。越小则大陆越完整、越大；越大则越容易出现群岛与多块大陆。
        /// 该系数作用在"地形特征尺寸"之上，因此实际大陆尺度 = terrainFeatureSize / continentScale。
        /// </summary>
        [Range(0.05f, 2f)] public float continentScale = 0.35f;

        /// <summary>
        /// 大陆层在高度场中的权重(0~1)。权重越大，地图越趋向大块连续陆地；
        /// 权重越小，地形越破碎(以 detail 层为主)。
        /// </summary>
        [Range(0f, 1f)] public float continentWeight = 0.65f;

        /// <summary>
        /// fBm 噪声的倍频数。
        /// </summary>
        [Range(1, 8)] public int octaves = 4;

        /// <summary>
        /// fBm 噪声的振幅衰减系数。
        /// </summary>
        [Range(0.2f, 0.8f)] public float persistence = 0.5f;

        /// <summary>
        /// fBm 噪声的频率增长系数。
        /// </summary>
        [Range(1.5f, 3.5f)] public float lacunarity = 2f;

        /// <summary>
        /// 高度场平滑次数，用于消除噪声锯齿，让地形更连贯。
        /// </summary>
        [Range(0, 6)] public int smoothPasses = 2;

        /// <summary>
        /// 边缘衰减带宽度，单位为"格"。地图四周该宽度内的地形会逐渐沉入水中，避免出现被切断的陆地。
        /// </summary>
        [Range(0f, 80f)] public float edgeFalloffCells = 12f;

        // ------------------------------------------------------------------
        // 四、顶点高度与水面
        // ------------------------------------------------------------------

        /// <summary>
        /// 海平面高度字节。水域(海/川/河/浅滩)的地面高度以此为上限，海底为 0。
        /// 标定自原版地图：海平面 = 10、海底 = 0。
        /// </summary>
        [Range(1, 60)] public int seaLevelByte = 10;

        /// <summary>
        /// 海平面水面高度字节(海与浅滩共用同一水平面)。引擎仅在"水面高度 > 地面高度"时绘制水面。
        /// 标定自原版地图：海平面水面 = 11、海底 = 0，差值为 11 字节(5.5 世界单位)。
        /// </summary>
        [Range(1, 60)] public int waterByte = 11;

        /// <summary>
        /// 河川(川/河)水面相对河床的落差字节。引擎以"水面 > 地面"判定是否绘制水面，
        /// 该值保证水面稳定高于河床、两者不重叠。
        /// 标定自原版地图：川/河的落差为 11(河床 0、水面 11)。
        /// </summary>
        [Range(1, 60)] public int riverDrop = 11;

        /// <summary>
        /// 河川水面相对其"自然地面高度"的下沉字节。
        /// 河川水面 = 自然地面 - 该值，且不低于海平面；
        /// 因此上游河段水位随海拔抬升、下游汇入海平面，与原版"水面随地形抬升"一致。
        /// </summary>
        [Range(0, 30)] public int riverBankDrop = 2;

        /// <summary>
        /// 陆地最高高度字节(山峰上限)。标定自原版地图：255。
        /// </summary>
        [Range(30, 255)] public int maxLandByte = 255;

        /// <summary>
        /// 陆地高度曲线(指数)：先对归一化陆地高度取 curve 次幂，再映射到 [海平面, 最高高度]。
        /// 数值越大则平原越多、山峰越尖。经验值 4 可较好贴合原版地图的高度分布
        /// (原版：平原≈20、丘陵≈88、p90≈170、峰值 255)。
        /// </summary>
        [Range(1f, 8f)] public float landHeightCurve = 4f;

        /// <summary>
        /// 是否为水域(海/浅滩/川/河)生成水面数据。
        /// </summary>
        public bool renderWater = true;

        /// <summary>
        /// 是否重算顶点法线。高度改变后必须重算，否则地表光照会呈现不自然的平面感。
        /// </summary>
        public bool recomputeNormals = true;

        /// <summary>
        /// 是否生成底图(BaseTex：逐顶点的底色图，四个季节各写一份 png)。
        /// 着色器会以 0.85 的强度把底图与地形贴图相乘，用于统一整体色调。
        /// </summary>
        public bool generateBaseMap = true;

        /// <summary>
        /// 底图中"地形色"的权重(0~1)。越大底图颜色越浓，整体越暗。
        /// 建议 0.25~0.45，其余部分保持接近白色，避免压暗地形贴图。
        /// </summary>
        [Range(0f, 1f)] public float baseMapTint = 0.35f;

        /// <summary>
        /// 底图的高度明暗强度(0~1)。越大低地越暗、高低落差越明显。
        /// </summary>
        [Range(0f, 1f)] public float baseMapShade = 0.25f;

        /// <summary>
        /// 底图是否区分四季。开启时按 春/夏/秋/冬 生成 4 张不同色调的底图
        /// (冬季含去饱和与高海拔积雪)，否则四季共用同一张。
        /// </summary>
        public bool baseMapSeasonal = true;

        /// <summary>
        /// 崖(山地)是否在多种岩石贴图间分布。开启后按"坡度分档 + 噪声"从
        /// TerrainLayerMap 的崖调色板中选取贴图，更贴近原版美术的混杂拼贴。
        /// </summary>
        public bool cliffLayerSpread = true;

        /// <summary>
        /// 判定为"陡坡岩壁"的坡度阈值(相邻顶点的高度字节差)。越大则陡坡档越少。
        /// </summary>
        [Range(2f, 60f)] public float cliffSteepSlope = 16f;

        /// <summary>
        /// 判定为"缓坡土石"的坡度阈值(相邻顶点的高度字节差)。越小则缓坡档越少。
        /// </summary>
        [Range(0f, 30f)] public float cliffGentleSlope = 6f;

        /// <summary>
        /// 噪声坐标偏移量，用于在同一随机种子下生成互不相关的多组噪声。
        /// </summary>
        public float noiseOffsetX = 0f;

        /// <summary>
        /// 噪声坐标偏移量(纵向)，与 noiseOffsetX 配合使用。
        /// </summary>
        public float noiseOffsetY = 0f;

        // ------------------------------------------------------------------
        // 五、原版骨架（用真实中国地图做形貌与高度基准）
        // ------------------------------------------------------------------

        /// <summary>
        /// 是否使用"原版地图骨架"：从已有地图 .bin 提取真实的海陆轮廓、山系与河道、
        /// 逐顶点高度，作为本次生成的高度基准与形貌掩码；
        /// 地表类型(森林/荒地/草地…)、城市、道路、领地仍由生成器重绘。
        /// </summary>
        public bool useSkeleton = false;

        /// <summary>
        /// 骨架来源地图（相对 Content 目录）。默认取 mod 里的原版中国图：
        /// 其有效区域自动探测为 200×200 的中国区域，区域之外是"域外"哨兵值 32。
        /// </summary>
        public string skeletonPath = "Mods/PK22/Map/pk22.bin";

        /// <summary>骨架有效区域起点 X；0 表示自动探测。</summary>
        public int skeletonOffsetX = 0;

        /// <summary>骨架有效区域起点 Y；0 表示自动探测。</summary>
        public int skeletonOffsetY = 0;

        /// <summary>
        /// 高度场中"原版高度"的权重(0~1)。
        /// 1 = 完全沿用原版高度；0 = 纯噪声；中间值表示在原版高度基准上叠加分形噪声扰动细节。
        /// </summary>
        [Range(0f, 1f)] public float skeletonWeight = 0.8f;

        /// <summary>
        /// 是否直接沿用骨架中的山系(崖)。关闭时改由程序化高度阈值判定山地。
        /// </summary>
        public bool skeletonUseMountains = true;

        /// <summary>
        /// 是否直接沿用骨架中的河道(川/河)。关闭时改由汇流累积模型重新生成河道。
        /// </summary>
        public bool skeletonUseRivers = true;

        /// <summary>
        /// 创建一份参数副本，避免生成过程中修改到界面上的原始配置。
        /// </summary>
        /// <returns>参数副本</returns>
        public MapGenConfig Clone()
        {
            return (MapGenConfig)MemberwiseClone();
        }
    }
}
