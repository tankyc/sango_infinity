using Newtonsoft.Json.Linq;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 建筑范围可视化：鼠标指向建筑时，把它的三种范围铺在地面上。
    ///
    /// 【绘制方式】完全复用 <see cref="CellSelector"/>（鼠标悬停地块）那套做法：
    /// 每个格子在局部空间铺 5x5 顶点 / 4x4 个 quad，逐顶点采样地形高度贴地，
    /// 用一张半透明材质叠在地表之上。
    ///
    /// 【渲染层级：地形之上、建筑之下】
    /// 三个材质的深度测试用 **LEqual（_ZTest = 4）** 而不是 CellSelector 的 Always（_ZTest = 0），
    /// 并且关闭深度写入（_ZWrite = 0）：
    ///   · 面片比地表高 0.2，LEqual 通过 → 压在地形之上；
    ///   · 建筑是不透明物体、先于透明队列渲染并写入深度 → 面片在建筑处 LEqual 失败被挡掉，
    ///     于是范围不会糊在城池 / 建筑模型正面；
    ///   · ZWrite 关闭，避免半透面片把深度写脏、影响后续透明物体的排序。
    /// 区别只是这里一层范围一个 Mesh，并且同一次要铺很多格子。
    ///
    /// 【三层范围与配色】
    ///   · 占地范围 —— 半透黄色，数据源 <see cref="BuildingBase.OccupyCellList"/>，
    ///     由 <see cref="BuildingType.radius"/> 经 <c>Map.GetSpiral</c> 生成；
    ///   · 攻击范围 —— 半透红褐色，数据源 <see cref="BuildingBase.effectCells"/>，
    ///     只对**会自主攻击**的建筑显示，判据取自 <see cref="BuildingBase.CanAutoAttack"/>
    ///     （城 / 关 / 港 + troopAtk &gt; 0 的阵 / 砦 / 城塞 / 箭楼 / 连弩楼 / 投石台）；
    ///   · 效果范围 —— 半透天蓝色，两种来源合并显示：
    ///     ① <see cref="BuildingType.actionEntities"/> 非空即"有效果的建筑"，
    ///        按各 Action 配置的 <c>bound</c> 取格（<c>-1</c> 用 effectCells，<c>&gt;0</c> 用中心格半径圆）；
    ///     ② <see cref="BuildingType.debuffRange"/> 的压制圈，**仅当**
    ///        <see cref="BuildingType.enemyDamageFactor"/> &lt; 100 时才算（阵 85 / 砦 75 / 城塞 65）。
    ///
    /// 【效果范围的口径】两种来源都按**运行期真实判定**取格，不是估算：
    ///   · actionEntities 的 bound —— 与 <c>BuildingAddBuff</c> / <c>BuildingAddTroopMorale</c>
    ///     的取格逻辑一一对应；
    ///   · debuffRange + enemyDamageFactor —— 与 <c>CombatFormula_Open311.CalcEnemyCampFactor</c>
    ///     的两条前置判定（范围 + 系数 &lt; 100）一一对应。
    /// 并集之后再剔除占地格，避免整片压在黄色层里看不出层次。
    ///
    /// 【触发时机】跟随「当前点中的格子」（<c>GameController.mouseOverCell</c>）：
    ///   · 桌面端 —— 鼠标悬停在哪座建筑上就显示哪座；
    ///   · 移动端 —— 没有悬停，取最近一次点中的格子（<c>TouchPhase.Ended</c> 时同步），
    ///     所以点一下建筑即锁定显示，点别处才切换。
    /// 建筑被视野裁剪回收（<c>Render</c> 为空 / <c>IsVisible()</c> 为假）时自动隐藏，
    /// 重新进入视野或被再次点中时自动恢复。
    /// </summary>
    [GameSystem]
    internal class BuildingRangeSelector : GameSystem
    {
        /// <summary>
        /// 一层范围对应的渲染资源。
        /// 顶点与三角形都用 List 累积，因为一次要铺的格子数随建筑类型变化
        /// （城池 radius=2 占地 19 格，攻击圈再叠 90 格左右）。
        /// </summary>
        class Layer
        {
            /// <summary>网格对象</summary>
            public Mesh mesh;
            /// <summary>渲染器</summary>
            public MeshRenderer renderer;
            /// <summary>节点</summary>
            public Transform trans;
            /// <summary>节点 GameObject（空范围时整体隐藏）</summary>
            public GameObject go;
            /// <summary>顶点缓存</summary>
            public List<Vector3> verts = new List<Vector3>(8192);
            /// <summary>索引缓存</summary>
            public List<int> tris = new List<int>(32768);

            /// <summary>清空本层缓存</summary>
            public void Reset()
            {
                verts.Clear();
                tris.Clear();
            }
        }

        /// <summary>占地范围（黄）</summary>
        Layer occupyLayer;
        /// <summary>攻击范围（红褐）</summary>
        Layer attackLayer;
        /// <summary>效果范围（天蓝）</summary>
        Layer effectLayer;

        /// <summary>当前显示范围的建筑</summary>
        BuildingBase current;

        /// <summary>单个 quad 的世界尺寸，由地图数据给出</summary>
        int quadSize;

        /// <summary>索引抽稀步长（移动端隔行抽稀，与 CellSelector 保持一致）</summary>
        int lodL = 1;

        /// <summary>
        /// 贴地高度偏移。
        ///
        /// 【为什么不是 0.2】地形按 LOD 抽稀后，一个大三角形跨越很多格子，内部是线性插值高度，
        /// 而面片是逐格采样原始顶点高度铺出来的，两者在格子中间对不齐；
        /// 0.2 的偏移量不足以覆盖这个误差，面片会在凹陷处沉进地形被挡掉。
        /// 抬到 0.5 先吃掉大部分误差，剩下的交给材质里的 <c>Offset -1, -1</c> 在深度上处理。
        /// </summary>
        const float BaseYOffset = 0.5f;

        /// <summary>三层之间的高度微偏移，避免半透面片完全共面导致的排序闪烁</summary>
        const float LayerYStep = 0.02f;

        /// <summary>效果范围临时容器，避免每次悬停都分配</summary>
        readonly List<Cell> tempEffectCells = new List<Cell>(64);

        /// <summary>按 bound 半径扫描时的临时容器</summary>
        readonly List<Cell> tempBoundCells = new List<Cell>(64);

        public override void Init()
        {
            GameEvent.OnScenarioStart += OnScenarioStart;
            GameEvent.OnScenarioEnd += OnScenarioEnd;

            // 占地：半透黄
            occupyLayer = CreateLayer("buildingRangeOccupy", "buildingRangeOccupyMat", BaseYOffset);
            // 攻击：半透红褐
            attackLayer = CreateLayer("buildingRangeAttack", "buildingRangeAttackMat", BaseYOffset + LayerYStep);
            // 效果：半透天蓝
            effectLayer = CreateLayer("buildingRangeEffect", "buildingRangeEffectMat", BaseYOffset + LayerYStep * 2);

#if UNITY_ANDROID || UNITY_IPHONE
            lodL = 2;
#endif
        }

        public override void Clear()
        {
            GameEvent.OnScenarioStart -= OnScenarioStart;
            GameEvent.OnScenarioEnd -= OnScenarioEnd;

            DestroyLayer(occupyLayer);
            DestroyLayer(attackLayer);
            DestroyLayer(effectLayer);
            occupyLayer = null;
            attackLayer = null;
            effectLayer = null;
            current = null;
        }

        /// <summary>
        /// 创建一层范围渲染节点。
        /// </summary>
        /// <param name="name">节点名</param>
        /// <param name="materialName">Resources 下的材质名</param>
        /// <param name="yOffset">贴地高度偏移</param>
        /// <returns>层对象</returns>
        Layer CreateLayer(string name, string materialName, float yOffset)
        {
            GameObject go = new GameObject(name);
            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();

            Transform trans = go.transform;
            // 与 CellSelector 相同的朝向修正：地图网格坐标与地形网格存在 (-1,1,1) 镜像 + 90 度旋转
            trans.localScale = new Vector3(-1, 1, 1);
            trans.rotation = Quaternion.Euler(0, 90, 0);
            GameObject.DontDestroyOnLoad(go);

            Material mat = Resources.Load<Material>(materialName);
            if (mat == null)
            {
                Sango.Log.Error($"*[建筑范围] 材质 Resources/{materialName} 缺失，{name} 将不可见");
            }
            meshRenderer.sharedMaterial = mat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            Mesh mesh = new Mesh();
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;

            Layer layer = new Layer();
            layer.mesh = mesh;
            layer.renderer = meshRenderer;
            layer.trans = trans;
            layer.go = go;
            layer.go.SetActive(false);
            return layer;
        }

        /// <summary>
        /// 销毁一层。
        /// </summary>
        /// <param name="layer">层对象</param>
        static void DestroyLayer(Layer layer)
        {
            if (layer == null)
                return;
            if (layer.go != null)
                GameObject.Destroy(layer.go);
            if (layer.mesh != null)
                GameObject.Destroy(layer.mesh);
        }

        void OnScenarioStart(Scenario scenario)
        {
            if (MapRender.Instance != null && MapRender.Instance.mapData != null)
                quadSize = MapRender.Instance.mapData.quadSize;
            GameEvent.OnScenarioTick += OnTick;
        }

        void OnScenarioEnd(Scenario scenario)
        {
            GameEvent.OnScenarioTick -= OnTick;
            current = null;
            ClearAll();
        }

        /// <summary>
        /// 每帧根据"当前点中的格子"维护范围显示。
        ///
        /// 【为什么不用 onCellOverEnter / onCellOverExit】
        /// 那两个回调是给桌面鼠标悬停设计的，移动端直接订阅会出两类问题：
        /// <list type="number">
        /// <item>移动端没有悬停，只在 <c>TouchPhase.Ended</c> 时把 <c>mouseOverCell</c>
        ///       同步成点中的格子；而且"再次点同一格"走的是 <c>onCellOverExit</c> 分支
        ///       （GameController.cs:632-635），第二次点同一座城反而会把范围清掉；</item>
        /// <item>建筑被视野裁剪回收时（<see cref="ObjectRender.MapObject"/> 不可见 / <c>Render</c> 置空）
        ///       不会触发任何悬停回调，范围会僵在屏幕上不走。</item>
        /// </list>
        /// 改成每帧读一次"当前点中格"做状态机，只在目标变化时重建网格，
        /// 平时开销只是一次引用比较，桌面与移动端共用同一套逻辑。
        /// </summary>
        void OnTick(Scenario scenario, float deltaTime)
        {
            if (scenario == null)
                return;

            BuildingBase target = ResolveTarget();
            if (target == current)
                return;

            current = target;
            if (current == null)
                ClearAll();
            else
                Rebuild();
        }

        /// <summary>
        /// 从"当前点中的格子"解析出需要显示范围的建筑。
        ///
        /// <c>GameController.mouseOverCell</c> 同时承担两种语义：桌面端是鼠标所在格，
        /// 移动端是最近一次点中的格（<c>TouchPhase.Ended</c> 时同步），正是需求里说的"当前点中的 cell"。
        ///
        /// 【回收判定】建筑已销毁、或渲染器被视野裁剪回收（<see cref="ObjectRender.IsVisible"/>
        /// 为假）时一律当作"没有目标"，范围立即隐藏；等它重新进入视野 / 被重新点中，
        /// target 会从 null 变回该建筑，从而自动重画。
        /// </summary>
        /// <returns>需要显示范围的建筑，没有则返回 null</returns>
        BuildingBase ResolveTarget()
        {
            GameController controller = GameController.Instance;
            Cell cell = controller != null ? controller.mouseOverCell : null;
            BuildingBase building = cell != null ? cell.building : null;
            if (building == null)
                return null;

            // 已经不在场上的建筑（被拆除 / 被占领后重建）不再显示
            if (!building.IsAlive || building.CenterCell == null || building.BuildingType == null)
                return null;

            // 视野回收：Render 被销毁，或 MapObject 被可见性管理隐藏
            if (building.Render == null || !building.Render.IsVisible())
                return null;

            return building;
        }

        /// <summary>
        /// 按当前建筑重建三层范围。
        /// </summary>
        void Rebuild()
        {
            ClearAll();

            if (current == null || current.CenterCell == null || current.BuildingType == null)
                return;

            BuildingType type = current.BuildingType;
            Cell center = current.CenterCell;

            // ---------- ① 占地范围（黄） ----------
            if (current.OccupyCellList != null && current.OccupyCellList.Count > 0)
                AddCells(occupyLayer, current.OccupyCellList, center);

            // ---------- ② 攻击范围（红褐） ----------
            // 【判据必须与攻击行为同源】自动攻击发生在 BuildingBase.OnForceTurnStart，
            // 判定就是 CanAutoAttack（attackMaxRange > 0 且 "troopAtk > 0 || 是据点"）。
            // 只看 attackMaxRange 会把太鼓台(3) / 军乐台(2) / 石兵八阵(1) / 根据地(2)
            // 这些 troopAtk = 0 的效果建筑画成红色攻击圈，和它们自己的蓝色效果圈叠在一起，
            // 表现就是"完全没区分攻击范围和效果范围"。
            //
            // effectCells 由 Building.Init 按 radius+attackMinRange .. radius+attackMaxRange
            // 生成（City.cs:1567-1571 / Building.cs:175-179），以 GetDirectSpiral 沿可通行格
            // 扩散（贴水/城墙时会缺格），这里直接消费预计算列表，保证与实际开火范围一致。
            if (current.CanAutoAttack && current.effectCells != null && current.effectCells.Count > 0)
                AddCells(attackLayer, current.effectCells, center);

            // ---------- ③ 效果范围（天蓝） ----------
            if (TryBuildEffectCells(current, type, center, tempEffectCells))
                AddCells(effectLayer, tempEffectCells, center);

            Apply(occupyLayer, center, BaseYOffset);
            Apply(attackLayer, center, BaseYOffset + LayerYStep);
            Apply(effectLayer, center, BaseYOffset + LayerYStep * 2);
        }

        /// <summary>
        /// 收集建筑的「效果作用范围」。
        ///
        /// 【判定口径】只要 <see cref="BuildingType.actionEntities"/> 非空，就认为这座建筑"有效果"，
        /// 必须显示作用范围。范围本身按 Action 配置的 <c>bound</c> 决定，
        /// 与 <c>BuildingAddBuff</c> / <c>BuildingAddTroopMorale</c> 运行期的取格逻辑完全一致：
        /// <list type="bullet">
        /// <item><c>bound == -1</c> → 用建筑自身的 <see cref="BuildingBase.effectCells"/>（跳过中心格）；</item>
        /// <item><c>bound &gt; 0</c> → 以 <see cref="BuildingBase.CenterCell"/> 为圆心、半径 <c>bound</c> 格；</item>
        /// <item>多个 Action 取**并集**（同时存在 <c>-1</c> 与具体格数时两层都要画）。</item>
        /// </list>
        ///
        /// 【两种兜底】
        ///   · <c>actionEntities</c> 非空但一条 <c>bound</c> 都没配 → 退回 effectCells，
        ///     这类 Action 在设计上就是"作用于建筑周围"，不画会让人误以为没效果；
        ///   · <see cref="BuildingType.debuffRange"/> 单独收一次 —— 阵 / 砦 / 城塞的压制圈
        ///     走的是伤害公式硬判定（<c>CombatFormula_Open311.CalcEnemyCampFactor</c>），
        ///     不经过 actionEntities，但同样属于"效果范围"；
        ///     前置条件是 <see cref="BuildingType.enemyDamageFactor"/> &lt; 100（真的会削弱敌人），
        ///     否则箭楼 / 投石台 / 火种这类 debuffRange 非 0 但不削弱的建筑会凭空多出一圈蓝色。
        /// </summary>
        /// <param name="building">当前建筑</param>
        /// <param name="type">建筑类型数据</param>
        /// <param name="center">建筑中心格</param>
        /// <param name="result">输出用的格子容器（会被清空）</param>
        /// <returns>存在可显示的效果范围返回 true</returns>
        bool TryBuildEffectCells(BuildingBase building, BuildingType type, Cell center, List<Cell> result)
        {
            result.Clear();

            JArray actions = type.actionEntities;
            bool hasActions = actions != null && actions.Count > 0;
            bool hasBound = false;
            bool useEffectCells = false;
            int maxBound = 0;

            if (hasActions)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    JObject action = actions[i] as JObject;
                    if (action == null)
                        continue;

                    JToken boundToken = action["bound"];
                    if (boundToken == null)
                        continue;

                    hasBound = true;
                    int bound = boundToken.Value<int>();
                    if (bound < 0)
                        useEffectCells = true;      // -1 = 用建筑自身的 effectCells
                    else if (bound > maxBound)
                        maxBound = bound;
                }

                if (!hasBound)
                    useEffectCells = true;
            }

            // 阵 / 砦 / 城塞的压制圈：必须同时满足 debuffRange > 0 且 enemyDamageFactor < 100。
            // 【判据与运行期完全一致】CombatFormula_Open311.CalcEnemyCampFactor 里是
            //     if (type.debuffRange <= 0 || type.enemyDamageFactor >= factor) return;   // factor 初值 100
            // 只看 debuffRange 会给箭楼 / 连弩楼 / 投石台 / 太鼓台 / 军乐台 / 石兵八阵 /
            // 根据地 / 火种… 这些 enemyDamageFactor 缺省 = 100（不削弱）的建筑凭空画一圈蓝圈，
            // 与它们的红色攻击圈叠在一起，正是"效果范围和攻击范围没区分"的另一半原因。
            bool hasDebuff = type.debuffRange > 0 && type.enemyDamageFactor < 100;
            if (hasDebuff && type.debuffRange > maxBound)
                maxBound = type.debuffRange;

            if (!hasActions && !hasDebuff)
                return false;

            Map map = Scenario.Cur != null ? Scenario.Cur.Map : null;

            // effectCells 分支：与运行期一样跳过第 0 个（中心格自身）。
            // 【只对非攻击建筑生效】effectCells 的语义由 attackMaxRange 决定：会攻击的建筑里
            // 它是"攻击环带"（radius+attackMinRange .. radius+attackMaxRange），
            // 当成效果范围会和红色攻击层完全重叠。非攻击建筑的 effectCells 恒为空
            // （Building.Init 只在 attackMaxRange > 0 时生成），因此这里不会漏画。
            if (useEffectCells && !building.CanAutoAttack
                && building.effectCells != null && building.effectCells.Count > 0)
            {
                for (int i = 0; i < building.effectCells.Count; i++)
                {
                    Cell cell = building.effectCells[i];
                    if (cell == null || cell == center || result.Contains(cell))
                        continue;
                    result.Add(cell);
                }
            }

            // bound / debuffRange 分支：以中心格为圆心画满整个圆
            if (maxBound > 0 && map != null)
            {
                tempBoundCells.Clear();
                map.GetSpiral(center, maxBound, tempBoundCells);
                for (int i = 0; i < tempBoundCells.Count; i++)
                {
                    Cell cell = tempBoundCells[i];
                    if (cell == null || result.Contains(cell))
                        continue;
                    result.Add(cell);
                }
            }

            // 剔除占地格：效果圈压在黄色占地层里看不出层次
            for (int i = result.Count - 1; i >= 0; i--)
            {
                if (result[i] == null || result[i].building == building)
                    result.RemoveAt(i);
            }

            return result.Count > 0;
        }

        /// <summary>
        /// 把一组格子拼进某一层。
        ///
        /// 坐标换算沿用 <see cref="CellSelector.UpdateSelectMesh"/> 的公式：
        /// 每格 4x4 个 quad，共 5x5 顶点，顶点在局部空间按 quadSize 排布，
        /// 高度取地图顶点数据的实际高度（* 0.5 与地形渲染保持同一缩放）。
        /// 六边形地图存在行偏移，故 Y 方向起点要加 <c>(x % 2) * 2</c>。
        /// </summary>
        /// <param name="layer">目标层</param>
        /// <param name="cells">格子集合</param>
        /// <param name="center">建筑中心格（作为局部原点）</param>
        void AddCells(Layer layer, List<Cell> cells, Cell center)
        {
            if (layer == null || cells == null || quadSize <= 0)
                return;

            MapData mapData = MapRender.Instance != null ? MapRender.Instance.mapData : null;
            if (mapData == null)
                return;

            int centerXStart = center.x * 4;
            int centerYStart = center.y * 4 + (center.x % 2) * 2;

            for (int c = 0; c < cells.Count; c++)
            {
                Cell cell = cells[c];
                if (cell == null)
                    continue;

                int baseIndex = layer.verts.Count;
                int xStart = cell.x * 4;
                int yStart = cell.y * 4 + (cell.x % 2) * 2;

                for (int j = 0; j < 5; j++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        int vx = xStart + i;
                        int vy = yStart + j;
                        float localX = (vx - (centerXStart + 2)) * quadSize;
                        float localZ = (vy - (centerYStart + 2)) * quadSize;
                        float height = mapData.GetVertexData(vx, vy).height * 0.5f;
                        layer.verts.Add(new Vector3(localX, height, localZ));
                    }
                }

                for (int j = 0; j < 4; j += lodL)
                {
                    for (int i = 0; i < 4; i += lodL)
                    {
                        int vStart = baseIndex + j * 5 + i;

                        layer.tris.Add(vStart);
                        layer.tris.Add(vStart + 5 * lodL);
                        layer.tris.Add(vStart + 6 * lodL);

                        layer.tris.Add(vStart);
                        layer.tris.Add(vStart + 6 * lodL);
                        layer.tris.Add(vStart + lodL);
                    }
                }
            }
        }

        /// <summary>
        /// 把某一层的缓存提交到 Mesh 并摆到建筑中心。
        /// </summary>
        /// <param name="layer">目标层</param>
        /// <param name="center">建筑中心格</param>
        /// <param name="yOffset">贴地高度</param>
        void Apply(Layer layer, Cell center, float yOffset)
        {
            if (layer == null || layer.go == null)
                return;

            if (layer.verts.Count == 0)
            {
                layer.go.SetActive(false);
                return;
            }

            layer.mesh.Clear();
            layer.mesh.SetVertices(layer.verts);
            layer.mesh.SetTriangles(layer.tris, 0);
            layer.mesh.RecalculateBounds();

            Vector3 pos = center.Position;
            pos.y = yOffset;
            layer.trans.position = pos;
            layer.go.SetActive(true);
        }

        /// <summary>
        /// 隐藏并清空全部层。
        /// </summary>
        void ClearAll()
        {
            HideLayer(occupyLayer);
            HideLayer(attackLayer);
            HideLayer(effectLayer);
        }

        /// <summary>
        /// 隐藏单层并清缓存。
        /// </summary>
        /// <param name="layer">目标层</param>
        static void HideLayer(Layer layer)
        {
            if (layer == null)
                return;
            layer.Reset();
            if (layer.mesh != null)
                layer.mesh.Clear();
            if (layer.go != null)
                layer.go.SetActive(false);
        }
    }
}
