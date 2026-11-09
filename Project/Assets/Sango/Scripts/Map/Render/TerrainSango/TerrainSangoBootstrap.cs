using UnityEngine;

namespace Sango.Render.TerrainSango
{
    /// <summary>
    /// 把 TerrainSango 接入游戏世界的唯一入口。
    ///
    /// 【接入点】<c>MapRender.OnMapLoaded</c>（MapRender.cs:440，LoadMap 末尾触发）。
    ///   此刻 mapData / mapLayer / mapGrid / mapCamera 全部就绪，TerrainSangoRenderer
    ///   用 GameMap 模式即可直接取数，不需要自己解析 .bin。
    ///   调用时机：Scenario.LoadWorld（Scenario.cs:1062）在 MapRender.Init() 之后、
    ///   LoadMap 之前调用 <see cref="Install"/>，保证事件先订阅、后触发。
    ///
    /// 【与旧地形的关系】
    ///   运行期旧地形已整体不创建：
    ///     · MapRender.Init 不再创建 "Terrain" 容器节点；
    ///     · MapTerrain.Init 在"非编辑态且 TerrainSango 开启"时直接跳过地形块（MapCell）创建。
    ///   之所以能这么做，是因为旧地形块的两个消费者都已被替换：
    ///     · 渲染 → TerrainSangoRenderer（高度纹理 + 顶点着色器位移）；
    ///     · 点击 → GameController.CheckMouseIsOnMapCell → TryGetMapHit（高度场射线求交）。
    ///   建筑/部队模型仍挂在 MapRender.modelRoot 下，不受影响。
    ///   编辑器态（MapEditor.IsEditOn）保留旧地形块：地形笔刷要用它的 meshCollider 定位格子。
    ///
    /// 【回退】把 <see cref="Enabled"/> 置 false 后重新加载地图：MapTerrain.Init 会重新创建旧地形块，
    ///   本类不再接管，渲染与点击都回到旧管线。<see cref="Uninstall"/> 只销毁新地形对象。
    ///
    /// 【原版无对应实现】Open311 只有一套分块静态地形，没有"地形后端切换"的概念。
    ///   本类是给新管线做的接入层，不承担业务语义，只负责装配与回退。
    /// </summary>
    public static class TerrainSangoBootstrap
    {
        /// <summary>
        /// 总开关：false 时完全不介入，旧地形照常渲染。
        /// 可用于"新地形出问题时一键回退"，不需要改代码重新打包。
        /// </summary>
        public static bool Enabled = false;

        /// <summary>当前的新地形渲染器（未接管时为 null）。</summary>
        public static TerrainSangoRenderer Renderer => renderer;

        /// <summary>是否已安装（已订阅地图加载事件）。</summary>
        public static bool IsInstalled => installed;

        /// <summary>新地形对象在层级里的名字。</summary>
        private const string HostName = "TerrainSango";

        private static TerrainSangoRenderer renderer;
        private static bool installed;
        private static bool legacyHidden;

        /// <summary>
        /// 安装接入（订阅地图加载完成事件）。
        /// 重复调用无副作用；必须在 MapRender.Init() 之后、LoadMap() 之前调用。
        /// </summary>
        public static void Install()
        {
            if (installed) return;

            MapRender render = MapRender.Instance;
            if (render == null)
            {
                Log.Warning("TerrainSango 接入跳过：MapRender 实例不存在", Sango.Log.LogType.World);
                return;
            }

            // 用 += 而不是 =：OnMapLoaded 是公开委托，Scenario 自己也订阅了它，
            // 直接赋值会把世界的初始化回调顶掉（MapEditor 就是这么覆盖订阅的，不能照抄）
            render.OnMapLoaded += HandleMapLoaded;

            // 季节切换走与旧管线同一个入口：MapRender.onSeasonChange（curSeason 的 setter 触发），
            // MapProperty 那一套也是这么订阅的（MapProperty.cs:32）
            render.onSeasonChange += HandleSeasonChanged;

            installed = true;

            // 兜底：调用时地图已经加载完（例如热重载/读档后重新接入）就立刻装配一次，
            // 不必等下一次 LoadMap 触发事件
            if (render.mapData != null && render.mapData.vertexDatas != null) HandleMapLoaded();

            Log.Info("TerrainSango 已安装：等待地图加载完成", Sango.Log.LogType.World);
        }

        /// <summary>
        /// 卸载接入：销毁新地形对象、恢复旧地形渲染。
        /// </summary>
        public static void Uninstall()
        {
            if (installed)
            {
                MapRender render = MapRender.Instance;
                if (render != null)
                {
                    render.OnMapLoaded -= HandleMapLoaded;
                    render.onSeasonChange -= HandleSeasonChanged;
                }
                installed = false;
            }

            if (renderer != null)
            {
                GameObject host = renderer.gameObject;
                renderer.enabled = false;
                if (Application.isPlaying) UnityEngine.Object.Destroy(host);
                else UnityEngine.Object.DestroyImmediate(host);
                renderer = null;
            }

            RestoreLegacyTerrain();
        }

        /// <summary>地图加载完成：装配新地形，接管渲染。</summary>
        private static void HandleMapLoaded()
        {
            if (!Enabled) return;

            MapRender render = MapRender.Instance;
            if (render == null || render.mapData == null)
            {
                Log.Warning("TerrainSango 接管跳过：MapRender.mapData 为空", Sango.Log.LogType.World);
                return;
            }

            if (renderer == null)
            {
                GameObject host = GameObject.Find(HostName);
                if (host == null)
                {
                    host = new GameObject(HostName);
                    // 挂在地图根节点下，地图整体卸载时一起销毁，不会留下孤儿对象
                    if (MapRender.mapRoot != null)
                        host.transform.SetParent(MapRender.mapRoot.transform, false);
                }

                renderer = host.GetComponent<TerrainSangoRenderer>();
                if (renderer == null) renderer = host.AddComponent<TerrainSangoRenderer>();
            }

            // 【打包剥离检查】TerrainSango 的着色器是运行时 Shader.Find 取的，
            // 若没列进 ProjectSettings > Graphics > Always Included Shaders，打包后会被剥离，
            // Shader.Find 返回 null → 新地形画不出来。此时绝不能接管（旧地形已被停掉，两边都没有地面）。
            if (Shader.Find("Sango/TerrainSango") == null)
            {
                FallbackToLegacyTerrain("找不到着色器 Sango/TerrainSango（多半是打包时被剥离）");
                return;
            }

            renderer.Mode = TerrainSangoRenderer.SourceMode.GameMap;
            renderer.SplitFactor = 0.25f;
            renderer.WaterAlpha = 0.55f;
            // 自动 LOD：近细远粗，按距离分裂/合并（ForceLodLevel = -1）。
            //
            // 【已知代价 1：地形轮廓随视角缓变】相邻 patch 层级不同，同一点的高度采样密度不同，
            //   morph 过渡带内顶点位置连续插值 —— 这是 CDLOD 的本质，保留 LOD 就会存在。
            //
            // 【已知代价 2：T 型接缝】相邻不同层级 patch 的共享边界上，细侧顶点密度是粗侧的 2 倍，
            //   粗侧两个顶点之间的直线边与细侧的折线之间会露出缝隙。标准 CDLOD 靠"粗层收缩索引"
            //   （stitching）解决，本实现未做；SkirtDepth 裙边从下方挡住这条缝，是同等的工程替代。
            renderer.ForceLodLevel = -1;
            // 裙边深度 = patchSize × 0.02，随层级自动缩放（叶层 320 单位 → 裙边 6.4，往上翻倍），
            // 与层级间高度差的规模匹配。0 = 关闭；出现穿透性破洞时优先调大它，而不是锁 LOD。
            renderer.SkirtDepth = 0.02f;
            // 必须显式指定游戏相机：MapCamera.Init 里若场景没有 MainCamera 标签的相机，
            // 它会自己 new 一个但不打标签，Camera.main 依旧为 null（MapCamera.cs:79-84）
            if (render.mapCamera != null && render.mapCamera.camera != null)
                renderer.TargetCamera = render.mapCamera.camera;
            else
                renderer.TargetCamera = Camera.main;

            // 数据源构建要读贴图 / 建纹理，移动端可能因格式不支持或路径不可读抛异常；
            // 这里必须兜住，否则异常会直接中断接管流程，旧地形也不会被补回来。
            try
            {
                renderer.Rebuild();
            }
            catch (System.Exception e)
            {
                FallbackToLegacyTerrain("地形数据源构建异常：" + e.Message);
                return;
            }

            if (renderer.Source == null)
            {
                // 取数失败就别藏着旧地形，否则玩家看到的是空白世界
                FallbackToLegacyTerrain("MapData 不可用，地形数据源构建失败");
                return;
            }

            HideLegacyTerrain();

            Log.Info("TerrainSango 已接管地形渲染：旧地形停止绘制，collider 与点击拾取保持不变", Sango.Log.LogType.World);
        }

        /// <summary>
        /// 接管失败时的唯一止损点：补建旧地形块并恢复旧地形的渲染开关。
        ///
        /// 【为什么必须补建】运行期 <see cref="MapTerrain.Init"/> 默认不创建旧地形块，
        /// 一旦新地形画不出来，屏幕上就彻底没有地面。这里把旧地形补回来，保证"至少能看到地图"。
        /// </summary>
        /// <param name="reason">失败原因（写进日志，便于定位是剥离、设备能力还是数据问题）</param>
        private static void FallbackToLegacyTerrain(string reason)
        {
            Log.Error("TerrainSango 接管失败：" + reason + "，回退到旧地形渲染", Sango.Log.LogType.World);

            if (renderer != null)
            {
                renderer.enabled = false;
                renderer.gameObject.SetActive(false);
            }

            // HideLegacyTerrain 可能已经把开关置 false，这里恢复
            legacyHidden = false;
            MapCell.TerrainRenderEnabled = true;

            MapRender render = MapRender.Instance;
            if (render != null && render.mapTerrain != null)
                render.mapTerrain.CreateLegacyCells();
        }

        /// <summary>季节切换：转发给新地形，只换地表贴图集合（不重建高度与 LOD）。</summary>
        private static void HandleSeasonChanged(int season)
        {
            if (renderer != null) renderer.ApplySeason(season);
        }

        /// <summary>
        /// 新地形接管后的地图拾取：用高度场做射线求交。
        ///
        /// 【为什么不能只靠 Physics.Raycast】
        ///   TerrainSango 的顶点高度是在顶点着色器里从高度纹理位移出来的，CPU 侧没有网格，
        ///   做不出与视觉一致的 collider。旧地形的 collider 虽然还留着，但那只是"点击靶子"，
        ///   一旦两份数据不同步（地图热重载、编辑器改图），点下去的位置就会偏。
        ///   这里直接用渲染所用的同一份高度场求交，与新地形天然同源。
        ///
        /// 【算法】沿射线按 QuadSize 步进（步长 = 一格顶点间距，保证不会跨过一格山脊），
        ///   高度用 <see cref="TerrainSangoSource.SampleHeightWorld"/> 双线性采样；
        ///   首次从"地形上方"穿到"下方"后，在该区间内二分 8 次收敛。
        ///   射线段完全在地图 XZ 范围外时只推进不判定，避免在地图外误命中。
        /// </summary>
        /// <param name="ray">世界空间射线（通常来自相机 ScreenPointToRay）</param>
        /// <param name="point">命中的世界坐标</param>
        /// <returns>是否命中地形</returns>
        public static bool TryGetMapHit(Ray ray, out Vector3 point)
        {
            point = Vector3.zero;

            TerrainSangoSource source = renderer != null ? renderer.Source : null;
            if (source == null) return false;

            // 世界 X 方向的边长对应 WorldHeight，世界 Z 方向对应 WorldWidth（与 Patch 的轴序一致）
            float sizeX = source.WorldHeight;
            float sizeZ = source.WorldWidth;
            if (sizeX <= 0f || sizeZ <= 0f) return false;

            float step = Mathf.Max(source.QuadSize, 1f);
            const float maxDistance = 8000f;   // 覆盖"相机拉到全局"仍能点地的距离

            float previous = 0f;
            bool previousAbove = true;
            bool hasPrevious = false;

            for (float t = 0f; t <= maxDistance; t += step)
            {
                Vector3 p = ray.GetPoint(t);
                bool inside = p.x >= 0f && p.x <= sizeX && p.z >= 0f && p.z <= sizeZ;
                if (!inside)
                {
                    // 图外不判定，但仍然推进"上一步"的基准位置
                    previous = t;
                    previousAbove = true;
                    hasPrevious = true;
                    continue;
                }

                bool above = p.y >= source.SampleHeightWorld(p.x, p.z);
                if (hasPrevious && previousAbove && !above)
                {
                    float lo = previous;
                    float hi = t;
                    for (int i = 0; i < 8; i++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        Vector3 pm = ray.GetPoint(mid);
                        if (pm.y >= source.SampleHeightWorld(pm.x, pm.z)) lo = mid;
                        else hi = mid;
                    }
                    point = ray.GetPoint(hi);
                    return true;
                }

                previous = t;
                previousAbove = above;
                hasPrevious = true;
            }

            return false;
        }

        /// <summary>
        /// 停掉旧地形的绘制开关。
        ///
        /// 运行时 MapRender.Init 不再创建 "Terrain" 容器节点、MapTerrain.Init 也不再创建地形块，
        /// 正常情况下这里没有 renderer 需要关。开关仍置 false 兜底：万一走的是"先按回退建了旧地形、
        /// 随后又启用接管"的过渡路径，<see cref="MapCell.Update"/> 也不会按可见性把 MeshRenderer 重新打开。
        /// </summary>
        private static void HideLegacyTerrain()
        {
            if (legacyHidden) return;
            legacyHidden = true;

            MapCell.TerrainRenderEnabled = false;
        }

        /// <summary>
        /// 恢复旧地形渲染开关。
        /// 注意：运行期旧地形块本来就没创建（判据见 MapTerrain.Init），
        /// 想真正回到旧地形需要把 <see cref="Enabled"/> 置 false 后重新加载地图。
        /// </summary>
        private static void RestoreLegacyTerrain()
        {
            if (!legacyHidden) return;
            legacyHidden = false;

            MapCell.TerrainRenderEnabled = true;
        }
    }
}
