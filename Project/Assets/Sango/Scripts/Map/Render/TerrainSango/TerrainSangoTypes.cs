using UnityEngine;

namespace Sango.Render.TerrainSango
{
    /// <summary>
    /// 一个被 LOD 选中的地形 patch。
    ///
    /// patch 不是"裁剪出来的地形块"，而是四叉树上的一个节点：
    /// 它只知道自己的世界矩形与层级，真正的高度由顶点着色器按高度纹理采样得到。
    /// 因此本结构体里没有 mesh、没有顶点数组——这是本方案与旧 MapCell 体系的根本区别。
    /// </summary>
    public struct TerrainSangoPatch
    {
        /// <summary>层级，0 = 叶层（最细）。</summary>
        public int Level;

        /// <summary>该层内的格子坐标（世界 X 方向）。</summary>
        public int GridX;

        /// <summary>该层内的格子坐标（世界 Z 方向）。</summary>
        public int GridZ;

        /// <summary>世界矩形原点 X。</summary>
        public float OriginX;

        /// <summary>世界矩形原点 Z。</summary>
        public float OriginZ;

        /// <summary>世界边长。</summary>
        public float Size;

        /// <summary>到观察点的平面距离（用于统计与调试）。</summary>
        public float Distance;

        /// <summary>是否被视锥剔除（剔除项不提交绘制）。</summary>
        public bool Culled;

        /// <summary>
        /// 四边"邻居更粗"标记（CDLOD 边界缝合用）。
        ///
        /// bit0 = 东（X+），bit1 = 西（X-），bit2 = 北（Z+），bit3 = 南（Z-）。
        /// 置位的边表示"这一侧没有同层邻居、贴着的是更粗层的 patch（或地图外）"，
        /// 着色器会把该边的最外圈顶点 morph 强制收敛到 1（吸附到父层格点），
        /// 使细/粗两侧的边界顶点密度与高度完全重合，消除 T 型接缝的穿透性破洞。
        /// 由 TerrainSangoLod 在每帧选择完成后统一计算。
        /// </summary>
        public int EdgeCoarse;
    }

    /// <summary>
    /// 一个地表图层的元数据（对应旧管线 MapLayer.LayerData）。
    ///
    /// 【为什么按层拆开而不是只用一张地表图】
    /// 旧管线给每个 layer 单独建一个网格、各自绑自己的贴图，靠顶点的 textureIndex
    /// 把三角形分桶到对应层。新管线保留"每层一张贴图、每层一套 tiling"这个数据口径，
    /// 改成用图层索引纹理 + Texture2DArray 在着色器里选层，从而仍然只有一次 draw call。
    /// </summary>
    public struct TerrainSangoLayerInfo
    {
        /// <summary>是否使用法线贴图着色（旧管线 isLit）。</summary>
        public bool IsLit;

        /// <summary>该层的 UV 缩放（旧管线 textureScale，Y 通常为负表示垂直翻转）。</summary>
        public float ScaleX;

        /// <summary>该层的 UV 缩放 Y 分量。</summary>
        public float ScaleY;

        /// <summary>是否水面层（旧管线里最后一层）。</summary>
        public bool IsWater;

        /// <summary>当前季节的漫反射贴图名（不含扩展名与目录）。</summary>
        public string DiffuseName;
    }

    /// <summary>
    /// 调试视图模式。
    /// </summary>
    public enum TerrainSangoDebugView
    {
        /// <summary>正常着色。</summary>
        None = 0,

        /// <summary>按 LOD 层级上色，用于确认"近处细、远处粗"。</summary>
        LodLevel = 1,

        /// <summary>Morph 系数热力图，用于确认层级过渡是连续的。</summary>
        Morph = 2,
    }

    /// <summary>
    /// 本模块用到的常量。
    ///
    /// 【为什么 patch 固定 65×65 顶点】
    /// 与 314 的 TerrainPatchInputMath 一致：一个 patch 恒为 64 格 × 64 格，
    /// LOD 通过"选哪一层节点"实现（层级越高覆盖面积越大、世界精度越低），
    /// 而不是减少顶点数。这样所有 patch 可以共用同一张网格、用实例化一次画完。
    /// </summary>
    public static class TerrainSangoConstants
    {
        /// <summary>每个 patch 覆盖的源格子数。</summary>
        public const int PatchCells = 64;

        /// <summary>每个 patch 的单边顶点数（65）。</summary>
        public const int PatchVertices = PatchCells + 1;

        /// <summary>
        /// 水面 patch 的格子数（32 = 地表的一半，即单边 33 顶点）。
        ///
        /// 【为什么水面可以只要地表一半的密度】
        ///   地表需要 64 格是因为要在顶点着色器里采样高度场表达起伏与法线；
        ///   水面不需要任何顶点动画：它的顶点只承担两件事——
        ///     ① 把整片水抬到海平面高度（来自 _LayerIndexTex 的 G 通道，本图 88.8% 的水面
        ///        高度字节都是 11，等于一个常数，顶点再密也插值不出差异）；
        ///     ② 把"水面标记"（B 通道）插值成岸线渐变（渐变带宽度 = 顶点间距）。
        ///   因此精度只影响岸线柔软度，32 格时叶层间距 = 320/32 = 10 世界单位
        ///   （半格地格），与地表 65 顶点时的误差半径同级，视觉无差。
        /// </summary>
        public const int WaterPatchCells = PatchCells / 2;

        /// <summary>水面 patch 的单边顶点数（33）。</summary>
        public const int WaterPatchVertices = WaterPatchCells + 1;

        /// <summary>一次 DrawMeshInstanced 能提交的最大实例数（Unity 上限 1023）。</summary>
        public const int MaxInstancesPerDraw = 1023;
    }
}
