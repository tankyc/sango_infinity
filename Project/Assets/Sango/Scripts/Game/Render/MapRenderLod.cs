using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 地图远景降级（LOD）。
    ///
    /// 【做什么】镜头拉远到阈值之后，依次关掉两样"远景看不出来、但每个对象都要付代价"的开销：
    ///   ① 实时阴影 —— 主光不再投射阴影、兵模不再参与阴影 pass；
    ///      阴影等于把整个场景再渲一遍（ShadowCaster pass），是远景下最贵的一项；
    ///   ② 描边 —— 地形格子恢复单材质，每个格子少一个 pass。
    ///
    /// 【已取消：headbar 文字降级】过去远景还会把头顶状态栏里的名字 / 兵力 / info 一起停用。
    /// 实际玩下来，拉远后玩家恰恰要靠名字辨认城池与部队，隐掉文字反而更难用；
    /// 因此**远景不再隐藏 headbar 文字**，见 <see cref="MapHeadbarManager"/>。
    ///
    /// 【阈值口径】用 <see cref="MapCamera.cur_distance"/>（镜头到观察点的距离，
    /// 取值范围见 <see cref="MapCamera.limitDistance"/>，默认 200~630）。
    /// 超过 <see cref="farDistance"/> 进入远景模式，回到阈值以下立刻恢复。
    ///
    /// 【判脏】状态只在翻转时真正切换：切描边要遍历全部地图对象换材质，不能每帧做。
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

            // 首次判定（含地图重建后的第一次）：
            //   · 远景 → 必须真的打上降级，否则"读档时镜头已经在远景"会一直保持近景画质；
            //   · 近景 → 首次进入不去动它（避免把美术在 prefab 上关掉的描边强行打开），
            //            但如果上一套地图是降级状态（wasFar），说明残留的材质 / layer 还在，
            //            必须恢复一次，否则会"近景没描边"。
            if (!initialized)
            {
                bool wasFar = IsFar;
                initialized = true;
                IsFar = far;
                if (far)
                    Apply(true);
                else if (wasFar)
                    Apply(false);
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
        /// 让"已判定过"的状态失效，下一帧 <see cref="Tick"/> 按当前镜头距离重新判定并真正施加。
        ///
        /// 【为什么必须在地图重建时调用】降级状态是**进程级静态缓存**（<see cref="initialized"/> /
        /// <see cref="IsFar"/>），而地图渲染对象是每局重建的。读档 / 新开局时镜头距离可能仍停在
        /// 上一个场景的远景值（cam_distance 不存档，见 MapCamera.OnSave/OnLoad 里的注释），
        /// 若沿用旧的 IsFar=true，`far == IsFar` 直接 return，新建的地图永远不会被打上远景降级 ——
        /// 表现就是"读档后镜头是远景，但阴影 / 描边 / 批渲染全是近景状态"。
        ///
        /// 这里只清标志、不当场切换：调用点在 <see cref="MapRender.LoadMap"/> 末尾，
        /// 真正的材质 / layer 切换留给下一帧 Tick，避免在加载途中触碰尚未就绪的对象。
        /// </summary>
        public static void Invalidate()
        {
            initialized = false;
        }

        /// <summary>
        /// 真正切换降级状态。只在 <see cref="IsFar"/> 翻转时调用，开销可以接受。
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
        /// 【历史缺陷已于 MapCell 改造中修复，开关保持关闭】
        ///   ① "块不可见时 SetLod 只记字段、重新可见后不重建" → 已改为 lodPending 标记 +
        ///      Update 补建（见 MapCell.SetLod / Update）；
        ///   ② "SetCellLod 入队不清空不去重、反复推拉积压" → 已改为入队前清空 + 档位去重。
        ///
        /// 【为什么仍默认关闭】远景渲染已由 Instanced 批渲染（enableTerrainBatchRender）接管，
        /// CPU 顶点抽稀不再承担远景降级的职责；打开它只会额外触发一轮全图重建。
        /// 保留开关是为了调试 CPU LOD 链路（编辑器 / 中景分档）时随时可用。
        /// </summary>
        public static bool enableTerrainLod = false;

        /// <summary>
        /// 是否启用"远景 Instanced 批渲染"（MapCell 改造的新远景管线）。
        /// 激活后 MapTerrain.SetBatchRender(far)：全部块的 MeshRenderer 关闭，
        /// 由 TerrainCellBatchRenderer 以 DrawMeshInstanced + 高度纹理位移提交全图地形，
        /// 模板网格按相机距离分档（33/17/9/5 顶点每边）。
        /// 出问题时可单独关掉回退到"块各自渲染"的旧行为。
        /// </summary>
        public static bool enableTerrainBatchRender = true;

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

                    // ③ 远景 Instanced 批渲染接管：块渲染关闭、全图地形几个 draw call 画完
                    //    （模板网格按相机距离自动分档，顶视图全览也走这条路径）
                    if (enableTerrainBatchRender)
                        map.mapTerrain.SetBatchRender(far);

                    // ④ CPU 顶点抽稀（历史路径）：缺陷已修但默认关闭 —— 远景降级职责已由 ③ 接管，
                    //    这里仅在调试 CPU LOD 时打开；关闭时显式回默认档（同值时 SetCellLod 直接返回）。
                    if (enableTerrainLod)
                        map.mapTerrain.SetCellLod(far ? FarCellLod : MapTerrain.PlatformDefaultLod);
                    else
                        map.mapTerrain.SetCellLod(MapTerrain.PlatformDefaultLod);
                }

                // ② 描边层（模型）：批量 DrawMeshInstanced 的静态模型与
                //    城池/关/港/普通设施这些真实 GameObject 建筑一起切（两条路径都在
                //    MapModels.SetNoOutLineLayer 里收口），漏掉 GameObject 那条就会出现
                //    "拉远后建筑 layer 还停在 Building 层、描边照画"。
                if (map.mapModels != null)
                    map.mapModels.SetNoOutLineLayer(far);

                // ④ 实时阴影：主光不投射阴影，ShadowCaster pass 整体消失
                if (map.mapLight != null)
                    map.mapLight.SetShadowEnable(!far);
            }

            // 兵模走 Graphics.DrawMeshInstanced 单独提交，阴影开关在 TroopsRender 内部
            TroopsRender.castShadow = !far;
        }
    }
}
