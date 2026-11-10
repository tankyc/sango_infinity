using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Render
{
    /// <summary>
    /// 地图标注（<see cref="MapLabel"/>）渲染器：所有标注共用**同一个 World Space Canvas**。
    ///
    /// 【为什么是"同一个 Canvas"】uGUI 只在同一个 Canvas 内部合批。
    /// 旧实现给每个标注实例化一个 <c>Resources/MapLabel</c>（根节点自带 Canvas），
    /// 同屏 N 个标注就是 N 个 DrawCall + N 次 Canvas 批次重算；
    /// 而所有标注用的都是同一份字体材质，本该合成 1 个 DrawCall。
    /// 现在把 Text 全部挂到同一个 Canvas 下，uGUI 自动合批（同字体 + 同材质 = 1 个 DrawCall），
    /// 同时保留 uGUI 的全部能力（字体、Outline、RichText），不需要自己烘顶点。
    ///
    /// 【结构】
    /// <code>
    /// MapLabelRenderer            （本组件的 GameObject，场景根节点）
    ///  └─ MapLabelCanvas          （Canvas，World Space）
    ///      └─ MapLabelView_N      （RectTransform，只承载"跟随哪个世界坐标"）
    ///          └─ label           （Text + Outline，克隆自 Resources/MapLabel）
    /// </code>
    ///
    /// · 中间节点 <c>MapLabelView_N</c>：每帧从 <see cref="MapLabel"/> 的 transform 读世界坐标写进来；
    /// · 里层 <c>label</c>：朝向、随镜头距离的缩放与上浮偏移由本类驱动，
    ///   参数从 prefab 上原有的 <see cref="Sango.UI.BillboardUI"/> 读出来，观感与旧实现一致。
    ///
    /// 【为什么不用 prefab 上的 BillboardUI】<see cref="Sango.UI.BillboardUI"/> 用的是 <c>Camera.main</c>，
    /// 而地图相机并不一定是 MainCamera。一旦不是，标注会朝错方向（背面朝相机 = 整片看不见），
    /// 所以这里改用地图相机自己算，参数仍取自那个组件，视觉不变。
    ///
    /// 【为什么不再自绘】自绘合批（自己拼顶点 + <c>Graphics.DrawMesh</c>）虽然也只有 1 个 DrawCall，
    /// 但它绕开了 uGUI，字体图集重建、Shader 变体、渲染管线与相机细节都要自己兜，
    /// 任何一个环节对不上就是"整片标注看不见"。共享 Canvas 方案全部走 uGUI 既有通路，稳定性最好。
    /// </summary>
    public class MapLabelRenderer : MonoBehaviour
    {
        // ───────────────────────────── 单例与注册表 ─────────────────────────────

        static MapLabelRenderer instance;

        /// <summary>全局单例。首次注册标注时自动创建，随场景卸载一起销毁。</summary>
        public static MapLabelRenderer Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("MapLabelRenderer");
                    instance = go.AddComponent<MapLabelRenderer>();
                }
                return instance;
            }
        }

        /// <summary>
        /// Canvas 排序层级。World Space Canvas 之间按 sortingOrder 比较，
        /// 取一个大于 0 的值，保证标注压在地形与其它 World Space Canvas 之上。
        /// </summary>
        const int CanvasSortingOrder = 10;

        /// <summary>视图对象池上限。标注数量通常只有几十个，够用即可。</summary>
        const int ViewPoolLimit = 128;

        /// <summary>当前需要显示的标注（与 <see cref="views"/> 同下标一一对应）。</summary>
        readonly List<MapLabel> labels = new List<MapLabel>(64);

        /// <summary>与 <see cref="labels"/> 一一对应的视图；元素为 null 表示尚未创建。</summary>
        readonly List<LabelView> views = new List<LabelView>(64);

        /// <summary>回收的视图，下次注册直接复用，避免频繁 Instantiate / Destroy。</summary>
        readonly List<LabelView> viewPool = new List<LabelView>(16);

        /// <summary>共享 Canvas 的 RectTransform（也是所有视图节点的父级）。</summary>
        RectTransform canvasRect;

        /// <summary>从 Resources/MapLabel 里取出的 Text 模板（不实例化整个 prefab）。</summary>
        GameObject labelTemplate;

        /// <summary>模板上的字号；标注自身未指定字号时使用。</summary>
        int templateFontSize = 32;

        /// <summary>模板是否已尝试加载（失败也不重复尝试，避免每帧打日志）。</summary>
        bool templateLoaded;

        /// <summary>内容（文字 / 颜色 / 字号）是否需要刷新；位置与朝向每帧同步，不依赖此标记。</summary>
        bool dirty = true;

        // ───────────────────────────── billboard 参数（取自 prefab） ─────────────────────────────

        /// <summary>基准缩放（原 prefab 上 BillboardUI.initScale，x 为负用于镜像观感）</summary>
        Vector3 initScale = new Vector3(-0.2f, 0.2f, 0.2f);

        /// <summary>随镜头距离插值的缩放系数</summary>
        Vector2 scaleFactor = new Vector2(0.4f, 1f);

        /// <summary>随镜头距离插值的上浮偏移（factor = 0 时的值）</summary>
        Vector3 offsetFactor = new Vector3(0f, -5f, 0f);

        /// <summary>固定偏移（相对标注自身位置）</summary>
        Vector3 cacheOffset = new Vector3(0f, 15f, 0f);

        /// <summary>
        /// 单个标注的视图。
        /// </summary>
        class LabelView
        {
            /// <summary>中间节点：只承载世界坐标，自身不带任何渲染组件。</summary>
            public GameObject root;

            /// <summary>中间节点的 RectTransform（写 localPosition 用）。</summary>
            public RectTransform rootRect;

            /// <summary>克隆出来的 Text 组件。</summary>
            public Text text;

            /// <summary>克隆出来的 Text 节点的 Transform（写朝向 / 缩放 / 偏移用）。</summary>
            public Transform textTrans;

            /// <summary>上次写入的世界坐标；初值 NaN 保证首帧必定写一次。</summary>
            public Vector3 lastPos = new Vector3(float.NaN, float.NaN, float.NaN);

            /// <summary>上次写入朝向时的相机旋转</summary>
            public Quaternion lastCamRot;

            /// <summary>是否已经写过一次朝向（首个有效帧必须强制写）</summary>
            public bool hasCamRot;

            /// <summary>上次写入缩放 / 偏移时的镜头距离系数；NaN 保证首帧必写</summary>
            public float lastFactor = float.NaN;
        }

        // ───────────────────────────── 对外接口（MapLabel 调用） ─────────────────────────────

        /// <summary>注册一个标注（由 MapLabel 在可见时调用）</summary>
        public static void Register(MapLabel label)
        {
            if (label == null)
                return;

            MapLabelRenderer r = Instance;
            if (r.labels.Contains(label))
                return;

            r.labels.Add(label);
            r.views.Add(null);   // 视图延迟到 LateUpdate 创建，此时 prefab / Canvas 未必就绪
            r.dirty = true;
        }

        /// <summary>注销一个标注（不可见 / 销毁时调用）</summary>
        public static void Unregister(MapLabel label)
        {
            if (label == null || instance == null)
                return;

            int index = instance.labels.IndexOf(label);
            if (index < 0)
                return;

            LabelView view = instance.views[index];
            instance.labels.RemoveAt(index);
            instance.views.RemoveAt(index);
            instance.RecycleView(view);
            instance.dirty = true;
        }

        /// <summary>标注内容发生变化，通知刷新文字 / 颜色 / 字号</summary>
        public static void MarkDirty()
        {
            if (instance != null)
                instance.dirty = true;
        }

        // ───────────────────────────── 每帧同步 ─────────────────────────────

        /// <summary>
        /// 每帧把标注的世界坐标与 billboard 姿态同步到视图。
        /// 放在 LateUpdate：此时地图相机与标注自身的位置在本帧内已经稳定，
        /// 不会出现"标注比地形晚一帧"的抖动。
        /// </summary>
        void LateUpdate()
        {
            if (labels.Count == 0)
                return;

            EnsureCanvas();
            if (canvasRect == null)
                return;

            bool refreshContent = dirty;
            dirty = false;

            for (int i = labels.Count - 1; i >= 0; i--)
            {
                MapLabel label = labels[i];

                // 场景卸载 / 标注被 Destroy 后这里是 Unity 的"伪 null"
                if (label == null)
                {
                    RecycleView(views[i]);
                    labels.RemoveAt(i);
                    views.RemoveAt(i);
                    continue;
                }

                LabelView view = views[i];
                bool isNew = false;
                if (view == null)
                {
                    view = CreateView();
                    if (view == null)
                        continue;

                    views[i] = view;
                    isNew = true;
                }

                Apply(view, label, refreshContent || isNew);
            }
        }

        /// <summary>把标注的位置、姿态与内容写进视图</summary>
        /// <param name="view">目标视图</param>
        /// <param name="label">数据来源</param>
        /// <param name="refreshContent">是否需要重写文字 / 颜色 / 字号</param>
        void Apply(LabelView view, MapLabel label, bool refreshContent)
        {
            // ① 位置：标注本身只是场景里的一个空节点，世界坐标由它决定
            Vector3 worldPos = label.transform.position;
            if (worldPos != view.lastPos)
            {
                view.lastPos = worldPos;
                // Canvas 位于原点且无旋转 / 缩放，这里仍走 InverseTransformPoint，
                // 万一将来 Canvas 被挂到带变换的父级下也不会错位
                view.rootRect.localPosition = canvasRect.InverseTransformPoint(worldPos);
            }

            // ② 姿态：朝向地图相机 + 随镜头距离缩放与上浮（等价于 prefab 上的 BillboardUI）
            ApplyBillboard(view);

            if (!refreshContent)
                return;

            // ③ 内容：文字为空或标注不可见时整棵视图关掉（关 GameObject 比关 Text 更彻底）
            bool show = label.CanRenderLabel;
            if (view.root.activeSelf != show)
                view.root.SetActive(show);
            if (!show)
                return;

            Text text = view.text;
            if (text == null)
                return;

            // uGUI 的 Text 各 setter 内部都做了相等判断，重复赋值不会触发重建
            if (text.text != label.labelText)
                text.text = label.labelText;

            Color color = label.textColor;
            if (text.color != color)
                text.color = color;

            int fontSize = label.fontSize > 0 ? label.fontSize : templateFontSize;
            if (text.fontSize != fontSize)
                text.fontSize = fontSize;
        }

        /// <summary>
        /// billboard 姿态：文字正面朝相机，并按镜头距离插值缩放 / 上浮。
        ///
        /// 【判脏的必要性】每写一次 rotation / localScale，所属 Canvas 就会被标脏并重算批次；
        /// 相机静止时重复写就是纯浪费。朝向只由相机朝向决定，缩放只由 distanceFactor 决定，
        /// 两者不变就完全不用写。
        /// </summary>
        void ApplyBillboard(LabelView view)
        {
            if (view.textTrans == null)
                return;

            Camera cam = GetMapCamera();
            if (cam == null)
                return;

            Transform camTrans = cam.transform;
            Quaternion camRot = camTrans.rotation;

            // 文字平面法线是 +Z，要让正面朝相机，forward 必须指向相机（即 -camForward）
            if (!view.hasCamRot || camRot != view.lastCamRot)
            {
                view.hasCamRot = true;
                view.lastCamRot = camRot;
                view.textTrans.rotation = Quaternion.LookRotation(-camTrans.forward, camTrans.up);
            }

            float factor = GetCameraDistanceFactor();
            if (factor != view.lastFactor)
            {
                view.lastFactor = factor;
                view.textTrans.localScale = Vector3.Lerp(
                    initScale * scaleFactor.x,
                    initScale * scaleFactor.y,
                    factor);

                // localPosition 相对中间节点写入，与旧实现"相对 Canvas 写入"等价
                // （中间节点相对 Canvas 没有额外变换）
                view.textTrans.localPosition = cacheOffset + Vector3.Lerp(offsetFactor, Vector3.zero, factor);
            }
        }

        /// <summary>取地图相机（不用 Camera.main：地图相机不一定挂 MainCamera 标签）</summary>
        static Camera GetMapCamera()
        {
            MapRender map = MapRender.Instance;
            if (map == null || map.mapCamera == null)
                return null;

            return map.mapCamera.camera;
        }

        /// <summary>取镜头距离系数（0 = 最近，1 = 最远）；取不到时返回 0</summary>
        static float GetCameraDistanceFactor()
        {
            MapRender map = MapRender.Instance;
            if (map == null || map.mapCamera == null)
                return 0f;

            float factor = map.mapCamera.cameraDistanceFactor;
            if (float.IsNaN(factor))
                return 0f;

            return Mathf.Clamp01(factor);
        }

        // ───────────────────────────── Canvas / 视图创建 ─────────────────────────────

        /// <summary>
        /// 创建共享 Canvas（只创建一次）。
        /// 不挂 GraphicRaycaster：标注只显示、不参与点击，挂了反而会参与 UI 事件检测。
        /// </summary>
        void EnsureCanvas()
        {
            if (canvasRect != null)
                return;

            GameObject canvasGo = new GameObject("MapLabelCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = CanvasSortingOrder;

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            // World Space Canvas 的尺寸不参与裁剪（没有 Mask 就不裁），
            // 这里给一个够大的值，纯粹是为了 RectTransform 有合理数值便于排查
            canvasRect.sizeDelta = new Vector2(2000f, 2000f);
        }

        /// <summary>从对象池取一个视图，池空则新建</summary>
        LabelView CreateView()
        {
            EnsureTemplate();
            if (labelTemplate == null)
                return null;

            LabelView view = null;

            if (viewPool.Count > 0)
            {
                view = viewPool[viewPool.Count - 1];
                viewPool.RemoveAt(viewPool.Count - 1);
                view.lastFactor = float.NaN;   // 重新启用后必须重写一次缩放
                view.hasCamRot = false;
            }
            else
            {
                GameObject rootGo = new GameObject("MapLabelView", typeof(RectTransform));
                rootGo.transform.SetParent(canvasRect, false);

                RectTransform rootRect = (RectTransform)rootGo.transform;
                rootRect.localPosition = Vector3.zero;
                rootRect.localRotation = Quaternion.identity;
                rootRect.localScale = Vector3.one;

                // 克隆的是 prefab 里的 label 子节点（Text + Outline），
                // 不带根节点那个 Canvas —— 否则又变回"每个标注一个 Canvas"
                GameObject textGo = UnityEngine.Object.Instantiate(labelTemplate, rootRect, false);
                textGo.name = "label";

                // 克隆体上不需要 BillboardUI：它用 Camera.main 决定朝向，遇到"地图相机不是 MainCamera"
                // 的场景会把标注转到背面（UI 材质背面剔除，等于整片看不见）。
                // 朝向统一交给 ApplyBillboard 按地图相机处理。
                // 注意只能在克隆体上移除，模板本身是 Resources 资源，销毁资源会被 Unity 拒绝。
                Sango.UI.BillboardUI billboard = textGo.GetComponent<Sango.UI.BillboardUI>();
                if (billboard != null)
                {
                    billboard.enabled = false;
                    UnityEngine.Object.Destroy(billboard);
                }

                textGo.transform.localPosition = cacheOffset;
                textGo.transform.localScale = Vector3.Lerp(
                    initScale * scaleFactor.x,
                    initScale * scaleFactor.y,
                    GetCameraDistanceFactor());

                view = new LabelView();
                view.root = rootGo;
                view.rootRect = rootRect;
                view.text = textGo.GetComponent<Text>();
                view.textTrans = textGo.transform;
                view.lastPos = new Vector3(float.NaN, float.NaN, float.NaN);
            }

            if (!view.root.activeSelf)
                view.root.SetActive(true);

            return view;
        }

        /// <summary>把视图退回对象池（不销毁 GameObject，避免反复 Instantiate）</summary>
        void RecycleView(LabelView view)
        {
            if (view == null)
                return;

            if (view.root != null && view.root.activeSelf)
                view.root.SetActive(false);

            if (viewPool.Count < ViewPoolLimit)
                viewPool.Add(view);
            else if (view.root != null)
                UnityEngine.Object.Destroy(view.root);
        }

        /// <summary>
        /// 从 <c>Resources/MapLabel</c> 取 Text 模板并读出 billboard 参数（只做一次）。
        /// 只取 label 子节点，根节点的 Canvas 正是要消掉的东西。
        /// </summary>
        void EnsureTemplate()
        {
            if (templateLoaded)
                return;

            templateLoaded = true;

            GameObject prefab = Resources.Load<GameObject>("MapLabel");
            if (prefab == null)
            {
                Sango.Log.Error("*[地图标注] 未找到 Resources/MapLabel，标注无法显示");
                return;
            }

            // billboard 参数（缩放 / 偏移）从 prefab 上读，保持与旧实现一致的观感
            Sango.UI.BillboardUI billboard = prefab.GetComponentInChildren<Sango.UI.BillboardUI>(true);
            if (billboard != null)
            {
                initScale = billboard.initScale;
                scaleFactor = billboard.scaleFactor;
                offsetFactor = billboard.offsetFactor;
                cacheOffset = billboard.cacheOffset;
            }

            Transform child = prefab.transform.childCount > 0 ? prefab.transform.GetChild(0) : null;
            GameObject src = child != null ? child.gameObject : prefab;

            // 兜底：万一把根节点（带 Canvas）当成了模板，会重新裂成 N 个 Canvas，这里直接挡掉
            if (src.GetComponent<Canvas>() != null)
            {
                Sango.Log.Error("*[地图标注] Resources/MapLabel 缺少纯 Text 子节点，标注无法显示");
                return;
            }

            Text text = src.GetComponent<Text>();
            if (text == null)
            {
                Sango.Log.Error("*[地图标注] Resources/MapLabel 的 label 节点上没有 Text 组件，标注无法显示");
                return;
            }

            templateFontSize = text.fontSize > 0 ? text.fontSize : 32;

            labelTemplate = src;
        }

        /// <summary>销毁时清空引用，共享 Canvas 作为子节点会随本对象一起销毁</summary>
        void OnDestroy()
        {
            labels.Clear();
            views.Clear();
            viewPool.Clear();
            canvasRect = null;
            labelTemplate = null;
            instance = null;
        }
    }
}
