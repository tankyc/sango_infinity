using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 地图远景降级（LOD）。
    ///
    /// 【做什么】镜头拉远到阈值之后，依次关掉三样"远景看不出来、但每个对象都要付代价"的开销：
    ///   ① 实时阴影 —— 主光不再投射阴影、兵模不再参与阴影 pass；
    ///      阴影等于把整个场景再渲一遍（ShadowCaster pass），是远景下最贵的一项；
    ///   ② 描边 —— 地形格子恢复单材质，每个格子少一个 pass；
    ///   ③ 头顶状态栏的文字 —— 名字 / 兵力 / info 全部停用，减少文字批次与 uGUI 重建。
    ///
    /// 【阈值口径】用 <see cref="MapCamera.cur_distance"/>（镜头到观察点的距离，
    /// 取值范围见 <see cref="MapCamera.limitDistance"/>，默认 200~630）。
    /// 超过 <see cref="farDistance"/> 进入远景模式，回到阈值以下立刻恢复。
    ///
    /// 【判脏】状态只在翻转时真正切换：切描边要遍历全部地图对象换材质、
    /// 切文字要遍历全部 headbar，不能每帧做。
    /// </summary>
    public static class MapRenderLod
    {
        /// <summary>总开关。false 时永远保持完整效果，并会把已降级的状态恢复回来。</summary>
        public static bool enabled = true;

        /// <summary>
        /// 远景阈值（世界单位）。<see cref="MapCamera.cur_distance"/> 大于等于它就降级。
        /// 镜头最近 200、最远 630（见 MapCamera.limitDistance），默认 480 大约相当于拉到 75% 的距离。
        /// </summary>
        public static float farDistance = 480f;

        /// <summary>当前是否处于远景降级状态（只读，调试用）</summary>
        public static bool IsFar { get; private set; }

        /// <summary>是否已经初始化过状态（避免第一帧无谓地触发一次全量切换）</summary>
        static bool initialized;

        /// <summary>记录已处理的帧号，同一帧被多处调用时只算一次</summary>
        static int lastFrame = -1;

        /// <summary>
        /// 每帧驱动入口，由 <see cref="MapRender.Update"/> 调用。
        /// </summary>
        public static void Tick()
        {
            if (lastFrame == Time.frameCount)
                return;
            lastFrame = Time.frameCount;

            // 编辑器地图工具里不做降级：美术调图要看到完整效果
            if (!enabled || Sango.Tools.MapEditor.IsEditOn)
            {
                if (IsFar)
                    Apply(false);
                return;
            }

            MapRender map = MapRender.Instance;
            if (map == null || map.mapCamera == null)
                return;

            bool far = map.mapCamera.cur_distance >= farDistance;

            // 首次只记录状态：如果一开始就在近景，不要去"恢复"，
            // 否则会把美术在 prefab 上关掉的描边强行打开。
            if (!initialized)
            {
                initialized = true;
                IsFar = far;
                if (far)
                    Apply(far);
                return;
            }

            if (far == IsFar)
                return;

            Apply(far);
        }

        /// <summary>外部强制恢复完整效果（例如切场景、退出战斗前）</summary>
        public static void Restore()
        {
            initialized = false;
            if (IsFar)
                Apply(false);
        }

        /// <summary>
        /// 真正切换三项状态。只在 <see cref="IsFar"/> 翻转时调用，开销可以接受。
        /// </summary>
        /// <summary>远景地形精度档位：隔三点采样，顶点数降到约 1/16</summary>
        const int FarCellLod = 2;

        /// <summary>
        /// 是否启用"地形块切 NoOutLine 层"（对应 URP Renderer 上按 LayerMask 过滤的描边 Feature）。
        /// 出问题时可以单独关掉它定位，不影响其它两项降级。
        /// </summary>
        public static bool enableTerrainLayerSwitch = true;

        /// <summary>
        /// 是否启用"远景地形降精度"（顶点抽稀重建）。
        /// 重建走 MapTerrain 的帧预算队列，出问题时可单独关掉定位。
        ///
        /// 【当前默认关闭】已知缺陷（2026-11 定位，尚未修复）：
        ///   ① <c>MapCell.SetLod</c> 在块不可见时只改 <c>lod</c> 字段、不重建网格，而块重新
        ///      可见时 <c>Update</c> 只在 <c>!IsValid()</c> 时才调 <c>Init</c> ——
        ///      "网格已建好但档位不对"的块永远不会重建，表现为"相机拉近后地形卡在低精度不恢复"；
        ///   ② <c>MapTerrain.SetCellLod</c> 入队时不清空旧队列、也不去重，反复推拉会积压，
        ///      而消费速度只有 <c>lodRebuildPerFrame = 4</c> 块/帧，大地图上要等很久才收敛。
        ///
        /// 修复方案（pending 标记 + 入队清空）确认后再打开本开关。
        /// 关闭期间几何精度固定为 <see cref="MapTerrain.PlatformDefaultLod"/>（桌面 0 / 移动 1），
        /// 描边层切换、实时阴影、头顶文字这三项远景降级不受影响，仍然照常工作。
        /// </summary>
        public static bool enableTerrainLod = false;

        static void Apply(bool far)
        {
            IsFar = far;

            MapRender map = MapRender.Instance;
            if (map != null)
            {
                // ① 描边材质：MapCell 会把格子恢复成单材质，每个格子省掉一个 pass
                map.outLineShow = !far;

                if (map.mapTerrain != null)
                {
                    // ② 描边层：切到 NoOutLine 层，让 URP Renderer（URP Asset_Renderer.asset）上
                    //    按 LayerMask 过滤的 OutlineOpaque / RenderObjects Feature 直接匹配不到本对象，
                    //    整批跳过描边 pass。与 ① 互补：① 依赖材质切换，② 依赖 Feature 的层过滤。
                    if (enableTerrainLayerSwitch)
                        map.mapTerrain.SetNoOutLineLayer(far);

                    // ③ 地形精度：拉远后顶点抽稀，块重建按帧预算排队（MapTerrain.lodRebuildPerFrame）
                    //
                    // 【关闭时也要显式回默认档】本开关原先只在"开"的时候下发档位，
                    // 于是运行中把它关掉后，已经降到 FarCellLod 的块再也没人负责恢复；
                    // 这里改成关掉时无条件下发 PlatformDefaultLod（同值时 SetCellLod 会直接返回，无开销）。
                    if (enableTerrainLod)
                        map.mapTerrain.SetCellLod(far ? FarCellLod : MapTerrain.PlatformDefaultLod);
                    else
                        map.mapTerrain.SetCellLod(MapTerrain.PlatformDefaultLod);
                }

                if (map.mapModels != null)
                    map.mapModels.SetNoOutLineLayer(far);

                // ④ 实时阴影：主光不投射阴影，ShadowCaster pass 整体消失
                if (map.mapLight != null)
                    map.mapLight.SetShadowEnable(!far);
            }

            // 兵模走 Graphics.DrawMeshInstanced 单独提交，阴影开关在 TroopsRender 内部
            TroopsRender.castShadow = !far;

            // 头顶状态栏文字：名字 / 兵力 / info
            MapHeadbarManager.SetTextVisible(!far);
        }
    }
}
