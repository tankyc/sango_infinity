using System.Collections.Generic;
using UnityEngine;
using Sango.UI;

namespace Sango.Render
{
    /// <summary>
    /// 地图头顶状态条（部队 / 城市 / 关隘 / 港口 / 设施）的集中驱动。
    ///
    /// 【为什么需要它】
    /// 原先每个 headbar 上各挂一个 <see cref="BillboardUI"/>，各自在自己的 Update 里做
    /// <c>LookAt</c>、缩放与偏移计算。地图一拉开，同屏 headbar 动辄上百个，于是每帧产生：
    ///   ① 上百次 MonoBehaviour.Update 调用；
    ///   ② 上百次 <c>transform.localPosition / localScale / rotation</c> 写入 ——
    ///      而 headbar 自身就是一个 World Space Canvas，每一次 transform 变化都会把
    ///      该 Canvas 标脏并触发重建批次，这是"地图一移动就掉帧"的主要来源之一。
    ///
    /// 这里把驱动收敛成一份：全部 headbar 在同一处批量更新，并且做两级早退：
    ///   · 相机没动、缩放因子没变、锚点也没动 → 整帧不写任何 transform；
    ///   · 只有真正变化的 headbar 才写 transform。
    ///
    /// 【与 BillboardUI 的关系】
    /// 仍复用 BillboardUI 上已经配好的 <c>initScale / scaleFactor / offsetFactor / cacheOffset</c>
    /// 四个参数（美术在 prefab 上调好的值），只是把它的 <c>Update</c> 关掉，改为读取参数后统一计算。
    ///
    /// 【远景屏幕保底】在上面这套插值之外，再乘一个按**每个 headbar 自己的视深**算出的放大系数，
    /// 保证镜头拉远时不会小到读不出字（比例见 <see cref="minScreenHeightRatio"/>）。
    /// 近景算出的系数恒为 1，因此除"远景不再无限缩小"外，观感与原先一致。
    ///
    /// 【不合并 Canvas 的原因】
    /// headbar 每帧要跟随 3D 锚点移动，一旦合并进同一个 Canvas，任何一次位置写入都会让
    /// 整张 Canvas（全部 headbar）重新合批，代价比现在的小 Canvas 更高。所以这里保留
    /// "一个 headbar 一个 World Space Canvas"的结构，只消除重复的驱动开销。
    /// </summary>
    public class MapHeadbarManager : MonoBehaviour
    {
        /// <summary>
        /// 一个已注册的头顶状态条。
        /// </summary>
        class Entry
        {
            /// <summary>headbar 根节点（World Space Canvas 的 Transform）</summary>
            public Transform headbar;

            /// <summary>世界空间锚点，通常是模型根节点</summary>
            public Transform anchor;

            /// <summary>参数来源；可能为空（此时沿用它注册时的 localPosition）</summary>
            public BillboardUI billboard;

            /// <summary>
            /// 注册时刻的局部偏移。BillboardUI 存在时该值会被其 offsetFactor 覆盖（与原实现一致），
            /// 只有 prefab 上没有 BillboardUI 时才作为固定偏移使用。
            /// </summary>
            public Vector3 baseOffset;

            /// <summary>上一帧锚点位置，用于"对象自己移动了"的判脏</summary>
            public Vector3 lastAnchorPos;

            /// <summary>上一帧锚点朝向</summary>
            public Quaternion lastAnchorRot;

            /// <summary>
            /// headbar 自身的高度（<see cref="RectTransform.rect"/> 的高度，单位 = 施加缩放前的世界单位）。
            /// 远景"屏幕保底"要用它反推当前在屏幕上占多少像素，Attach 时取一次即可
            /// （对象池复用会换 prefab 尺寸，所以重复注册时也要重取）。
            /// </summary>
            public float localHeight;
        }

        static MapHeadbarManager instance;

        /// <summary>
        /// 全局单例。首次访问时自动创建，随场景卸载一起销毁（销毁后再次访问会重建）。
        /// </summary>
        public static MapHeadbarManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("MapHeadbarManager");
                    instance = go.AddComponent<MapHeadbarManager>();
                }
                return instance;
            }
        }

        /// <summary>已注册的 headbar 列表，容量按常见同屏数量预分配，避免运行期扩容</summary>
        readonly List<Entry> entries = new List<Entry>(160);

        /// <summary>当前驱动相机</summary>
        Camera cam;

        Vector3 lastCamPos;
        Quaternion lastCamRot;
        float lastFactor = float.NaN;

        /// <summary>上一帧屏幕高度（像素）。分辨率 / 窗口尺寸变了，屏幕保底必须重算</summary>
        int lastPixelHeight = -1;

        /// <summary>上一帧相机 FOV。透视换算常量依赖它，改了也要重算</summary>
        float lastFieldOfView = float.NaN;

        /// <summary>强制下一帧全量刷新（注册新对象 / 换相机时置位）</summary>
        bool forceRefresh = true;

        /// <summary>
        /// 远景保底：headbar 在屏幕上至少占**屏幕高度**的这个比例（0 = 关闭保底）。
        ///
        /// <para>
        /// 【为什么需要】headbar 是 World Space Canvas，屏幕尺寸与相机视深成反比：
        /// 拉到最远（<see cref="MapCamera.cur_distance"/> 630）时只剩近景的 1/3 左右，
        /// 名字基本读不出来。仅靠 <see cref="BillboardUI.scaleFactor"/> 的两端插值补不回来，
        /// 因为同屏 headbar 的视深并不一致（地图边缘比中心远得多）。
        /// </para>
        /// <para>
        /// 这里对**每个 headbar 各自的视深**算一次投影后的像素高度，低于阈值就等比放大到阈值。
        /// 因此保底是按"屏幕像素比"而不是"世界距离"给的，换分辨率、改 FOV 都不会失真；
        /// 只放大不缩小 —— 近景像素本来就够，算出来的系数恒为 1。
        /// </para>
        /// <para>
        /// 【取值依据】按现有 prefab 标定（fov 30、1080p）：
        /// 城池条 rect 高 43、scaleFactor 0.2→0.4；部队条 rect 高 55.5、scaleFactor 0.15→0.35。
        /// 锚点恰好落在镜头观察距离上时，最远处约剩 5.1% / 5.7% 屏高（≈55~62 px），
        /// 但画面上方那些 headbar 的视深更大，会掉到 3% 以下 —— 那才是真正看不清的一批。
        /// 取 5%（1080p ≈ 54 px）等于"最远处保持现有观感，只把更远的抬回来"；
        /// 想更醒目调到 0.06~0.08，只想救最边缘的调到 0.03。
        /// </para>
        /// </summary>
        public static float minScreenHeightRatio = 0.05f;

        /// <summary>
        /// 一帧的相机投影参数，供远景保底换算"世界高度 → 屏幕像素"用。
        /// 整帧只算一次，避免每个 headbar 各算一次 <c>Tan(fov/2)</c>。
        /// </summary>
        struct ScreenScale
        {
            /// <summary>屏幕高度 × <see cref="minScreenHeightRatio"/>，即保底像素高度；&lt;= 0 表示关闭</summary>
            public float minPixelHeight;

            /// <summary>
            /// 透视：<c>pixelHeight / (2·tan(fovY/2))</c>，实际换算时还要除以视深；
            /// 正交：<c>pixelHeight / (2·orthographicSize)</c>，与视深无关。
            /// </summary>
            public float pixelsPerUnitK;

            /// <summary>true = 透视投影（换算要除以视深）</summary>
            public bool depthAware;
        }

        /// <summary>
        /// 注册一个 headbar。锚点取 headbar 当前的父节点（调用方已把 headbar 挂到模型下）。
        /// </summary>
        /// <param name="headbar">headbar 根对象</param>
        public static void Attach(GameObject headbar)
        {
            if (headbar == null)
                return;

            Transform anchor = headbar.transform.parent;
            if (anchor == null)
                return;

            MapHeadbarManager mgr = Instance;

            // 同一个对象重复注册会让它每帧被写两次，这里先剔除旧记录
            for (int i = 0; i < mgr.entries.Count; i++)
            {
                if (mgr.entries[i].headbar == headbar.transform)
                {
                    mgr.entries[i].anchor = anchor;
                    mgr.entries[i].billboard = headbar.GetComponent<BillboardUI>();
                    // 对象池复用可能换成别的 headbar 尺寸，保底用的高度必须重取
                    mgr.entries[i].localHeight = GetLocalHeight(headbar);
                    mgr.forceRefresh = true;
                    return;
                }
            }

            Entry e = new Entry();
            e.headbar = headbar.transform;
            e.anchor = anchor;
            e.billboard = headbar.GetComponent<BillboardUI>();
            e.baseOffset = headbar.transform.localPosition;
            e.localHeight = GetLocalHeight(headbar);

            // 关掉各自的 Update：朝向与缩放改由本管理器统一计算
            if (e.billboard != null)
                e.billboard.enabled = false;

            mgr.entries.Add(e);
            mgr.forceRefresh = true;
        }

        /// <summary>
        /// 注销一个 headbar。必须在 <see cref="PoolManager.Recycle"/> 之前调用，
        /// 否则回收后的对象会被本管理器继续驱动（位置错乱且白白标脏 Canvas）。
        /// </summary>
        /// <param name="headbar">headbar 根对象</param>
        public static void Detach(GameObject headbar)
        {
            if (headbar == null)
                return;

            if (instance == null)
                return;

            Transform t = headbar.transform;
            List<Entry> list = instance.entries;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].headbar == t)
                    list.RemoveAt(i);
            }
        }

        /// <summary>
        /// 请求下一帧全量刷新（例如相机被整体切换、headbar 参数被改动）。
        /// </summary>
        public static void RefreshAll()
        {
            if (instance != null)
                instance.forceRefresh = true;
        }

        void LateUpdate()
        {
            int count = entries.Count;
            if (count == 0)
                return;

            MapRender mapRender = MapRender.Instance;
            if (mapRender == null)
                return;

            MapCamera mapCamera = mapRender.mapCamera;
            if (mapCamera == null || mapCamera.camera == null)
                return;

            Camera current = mapCamera.camera;
            if (cam != current)
            {
                cam = current;
                forceRefresh = true;
            }

            Transform camTrans = current.transform;
            float factor = mapCamera.cameraDistanceFactor;

            // 屏幕保底依赖"屏幕像素高度 / FOV"，这两者变了即使相机没动也要重算一遍缩放
            bool projectionChanged = forceRefresh
                                     || current.pixelHeight != lastPixelHeight
                                     || Mathf.Abs(current.fieldOfView - lastFieldOfView) > 0.0001f;

            bool camMoved = projectionChanged
                            || camTrans.position != lastCamPos
                            || camTrans.rotation != lastCamRot;
            bool factorChanged = forceRefresh || Mathf.Abs(factor - lastFactor) > 0.0001f;

            lastCamPos = camTrans.position;
            lastCamRot = camTrans.rotation;
            lastFactor = factor;
            lastPixelHeight = current.pixelHeight;
            lastFieldOfView = current.fieldOfView;
            forceRefresh = false;

            Quaternion camRot = camTrans.rotation;

            // 远景"屏幕保底"的投影常量：整帧算一次，循环里只做一次除法
            ScreenScale screen;
            screen.minPixelHeight = minScreenHeightRatio > 0f
                ? current.pixelHeight * minScreenHeightRatio
                : 0f;
            screen.depthAware = !current.orthographic;
            if (screen.depthAware)
            {
                float halfTan = Mathf.Tan(current.fieldOfView * 0.5f * Mathf.Deg2Rad);
                screen.pixelsPerUnitK = current.pixelHeight / (2f * Mathf.Max(0.0001f, halfTan));
            }
            else
            {
                screen.pixelsPerUnitK = current.pixelHeight / (2f * Mathf.Max(0.0001f, current.orthographicSize));
            }

            for (int i = count - 1; i >= 0; i--)
            {
                Entry e = entries[i];

                // 对象被销毁（退出战斗、切场景）时顺手清理，避免空引用
                if (e.headbar == null || e.anchor == null)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                Vector3 anchorPos = e.anchor.position;
                Quaternion anchorRot = e.anchor.rotation;

                // 三级早退：相机、缩放因子、锚点都没变化 → 这一帧完全不写 transform
                if (!camMoved && !factorChanged
                    && anchorPos == e.lastAnchorPos
                    && anchorRot == e.lastAnchorRot)
                {
                    continue;
                }

                e.lastAnchorPos = anchorPos;
                e.lastAnchorRot = anchorRot;

                Apply(e, camTrans, camRot, factor, screen);
            }
        }

        /// <summary>
        /// 写入单个 headbar 的位置 / 朝向 / 缩放。
        /// 计算口径与 <see cref="BillboardUI"/> 的原实现逐项对齐：
        ///   · 朝向 = 相机朝向（原 LookAt(cam.rotation * back, cam.rotation * up) 的等价写法）
        ///   · 偏移 = cacheOffset + Lerp(offsetFactor, zero, factor)，在锚点局部空间内
        ///   · 缩放 = Lerp(initScale * scaleFactor.x, initScale * scaleFactor.y, factor)
        ///           再乘一个按视深算出的屏幕保底系数（见 <see cref="CalcMinScreenScale"/>）
        /// </summary>
        void Apply(Entry e, Transform camTrans, Quaternion camRot, float factor, ScreenScale screen)
        {
            BillboardUI b = e.billboard;
            Vector3 localOffset;
            Vector3 localScale;

            if (b != null)
            {
                localOffset = b.cacheOffset + Vector3.Lerp(b.offsetFactor, Vector3.zero, factor);
                localScale = Vector3.Lerp(
                    b.initScale * b.scaleFactor.x,
                    b.initScale * b.scaleFactor.y,
                    factor);
            }
            else
            {
                // 没有 BillboardUI 的 prefab：沿用调用方设置好的固定偏移，不擅自清零
                localOffset = e.baseOffset;
                localScale = Vector3.one;
            }

            // 远景保底：拉远到像素高度不足阈值时等比放大（近景系数恒为 1，不改变原有观感）
            localScale *= CalcMinScreenScale(e, camTrans, localScale.y, screen);

            Transform hb = e.headbar;
            hb.position = e.anchor.TransformPoint(localOffset);
            hb.rotation = camRot;
            hb.localScale = localScale;
        }

        /// <summary>
        /// 计算"屏幕保底"的等比放大系数（恒 &gt;= 1）。
        ///
        /// <para>
        /// 透视投影下，世界高度 h 在视深 d 处的像素高度 = <c>h · pixelHeight / (2·d·tan(fovY/2))</c>；
        /// 低于保底像素高度时按比值放大，正好把屏幕占比抬到 <see cref="minScreenHeightRatio"/>。
        /// 正交相机不随距离缩小，按 <c>h · pixelHeight / (2·orthographicSize)</c> 换算即可。
        /// </para>
        /// <para>
        /// 视深取"锚点到相机的向量在相机前向上的投影"，而不是两点直线距离 ——
        /// 俯视镜头下同一屏幕高度上的格子直线距离差别很大，投影深度才是投影变换真正使用的量。
        /// </para>
        /// <para>
        /// 只放大不缩小：近景算出的像素高度本来就高于阈值，直接返回 1，
        /// 保证这段逻辑不会改变近景既有的美术观感。
        /// </para>
        /// </summary>
        /// <param name="e">headbar 记录</param>
        /// <param name="camTrans">相机 Transform</param>
        /// <param name="scaleY">插值算出的基准缩放的 Y 分量</param>
        /// <param name="screen">本帧的投影常量</param>
        /// <returns>放大系数；不需要放大时返回 1</returns>
        static float CalcMinScreenScale(Entry e, Transform camTrans, float scaleY, ScreenScale screen)
        {
            if (screen.minPixelHeight <= 0f || screen.pixelsPerUnitK <= 0f)
                return 1f;
            if (e.localHeight <= 0f || scaleY <= 0f)
                return 1f;
            if (camTrans == null || e.anchor == null)
                return 1f;

            float pixelsPerUnit = screen.pixelsPerUnitK;
            if (screen.depthAware)
            {
                float depth = Vector3.Dot(e.anchor.position - camTrans.position, camTrans.forward);
                if (depth < 0.01f)
                    depth = 0.01f;
                pixelsPerUnit /= depth;
            }

            // 锚点（模型根）本身可能被整体缩放，headbar 的世界高度要把它一并算进来
            float anchorScaleY = Mathf.Abs(e.anchor.lossyScale.y);
            if (anchorScaleY <= 0f)
                anchorScaleY = 1f;

            float pixelHeight = e.localHeight * scaleY * anchorScaleY * pixelsPerUnit;
            if (pixelHeight <= 0f || pixelHeight >= screen.minPixelHeight)
                return 1f;

            return screen.minPixelHeight / pixelHeight;
        }

        /// <summary>
        /// 取 headbar 的本地高度（World Space Canvas 根 RectTransform 的 rect 高度）。
        /// 取不到（没有 RectTransform）时返回 0，表示该 headbar 不参与屏幕保底。
        /// </summary>
        /// <param name="headbar">headbar 根对象</param>
        /// <returns>rect 高度；无法取得返回 0</returns>
        static float GetLocalHeight(GameObject headbar)
        {
            if (headbar == null)
                return 0f;
            RectTransform rt = headbar.transform as RectTransform;
            if (rt == null)
                rt = headbar.GetComponent<RectTransform>();
            return rt != null ? rt.rect.height : 0f;
        }
    }
}
