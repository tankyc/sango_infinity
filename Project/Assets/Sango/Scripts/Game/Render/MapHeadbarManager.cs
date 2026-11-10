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
    /// 四个参数（美术在 prefab 上调好的值），只是把它的 <c>Update</c> 关掉，改为读取参数后统一计算，
    /// 保证视觉结果与原先完全一致。
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
            /// headbar 下的全部文字组件（Attach 时缓存一次）。
            /// 远景降级要把名字 / 兵力 / info 一起停掉，每帧去 GetComponentsInChildren 太贵。
            /// </summary>
            public UnityEngine.UI.Text[] texts;

            /// <summary>降级前的 enabled 状态快照，恢复时按原样还回去</summary>
            public bool[] textSaved;

            /// <summary>快照是否有效（false 表示当前没有处于"文字被降级隐藏"的状态）</summary>
            public bool textSavedValid;
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

        /// <summary>强制下一帧全量刷新（注册新对象 / 换相机时置位）</summary>
        bool forceRefresh = true;

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
                    // 对象池复用会换一批子节点，文字缓存必须重新取，否则会握着已销毁的引用
                    mgr.CacheTexts(mgr.entries[i]);
                    mgr.forceRefresh = true;
                    return;
                }
            }

            Entry e = new Entry();
            e.headbar = headbar.transform;
            e.anchor = anchor;
            e.billboard = headbar.GetComponent<BillboardUI>();
            e.baseOffset = headbar.transform.localPosition;
            mgr.CacheTexts(e);

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
                {
                    // 回收前把被降级隐藏的文字还回去：对象进池后还会被复用，
                    // 若一直保持 disabled，下次复用时会被误判成"业务层本来就隐藏"。
                    instance.ApplyTextVisible(list[i], true);
                    list.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 远景降级：显示 / 隐藏全部 headbar 的文字（名字、兵力、info）。
        ///
        /// 【为什么只切 Text.enabled】不改 GameObject 激活状态，避免影响布局与业务层的显隐逻辑；
        /// 而且 uGUI 的 Graphic 在 disabled 之后不会再进重建队列，
        /// 业务层继续每帧给 text 赋值也不会产生 Canvas 重建开销。
        /// </summary>
        public static void SetTextVisible(bool visible)
        {
            if (instance == null)
                return;

            instance.ApplyTextVisibleAll(visible);
        }

        void ApplyTextVisibleAll(bool visible)
        {
            for (int i = 0; i < entries.Count; i++)
                ApplyTextVisible(entries[i], visible);
        }

        /// <summary>缓存 headbar 下的全部文字组件，并按当前远景状态决定是否立即隐藏</summary>
        void CacheTexts(Entry e)
        {
            e.texts = e.headbar != null
                ? e.headbar.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                : null;
            e.textSaved = null;
            e.textSavedValid = false;

            // 远景期间新刷出来的 headbar（部队进场、建筑建成）也要遵守降级状态
            if (MapRenderLod.IsFar)
                ApplyTextVisible(e, false);
        }

        /// <summary>
        /// 切换单个 headbar 的文字显隐。隐藏时记录原有 enabled，恢复时原样还回去，
        /// 这样业务层自己关掉的部件（如城市的 info 面板）不会被误开。
        /// </summary>
        void ApplyTextVisible(Entry e, bool visible)
        {
            if (e.texts == null || e.texts.Length == 0)
                return;

            if (!visible)
            {
                if (e.textSavedValid)
                    return;

                if (e.textSaved == null || e.textSaved.Length != e.texts.Length)
                    e.textSaved = new bool[e.texts.Length];

                for (int i = 0; i < e.texts.Length; i++)
                {
                    UnityEngine.UI.Text t = e.texts[i];
                    if (t == null)
                        continue;

                    e.textSaved[i] = t.enabled;
                    t.enabled = false;
                }

                e.textSavedValid = true;
            }
            else
            {
                if (!e.textSavedValid)
                    return;

                for (int i = 0; i < e.texts.Length; i++)
                {
                    UnityEngine.UI.Text t = e.texts[i];
                    if (t == null)
                        continue;

                    t.enabled = e.textSaved[i];
                }

                e.textSavedValid = false;
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

            bool camMoved = forceRefresh
                            || camTrans.position != lastCamPos
                            || camTrans.rotation != lastCamRot;
            bool factorChanged = forceRefresh || Mathf.Abs(factor - lastFactor) > 0.0001f;

            lastCamPos = camTrans.position;
            lastCamRot = camTrans.rotation;
            lastFactor = factor;
            forceRefresh = false;

            Quaternion camRot = camTrans.rotation;

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

                Apply(e, camRot, factor);
            }
        }

        /// <summary>
        /// 写入单个 headbar 的位置 / 朝向 / 缩放。
        /// 计算口径与 <see cref="BillboardUI"/> 的原实现逐项对齐：
        ///   · 朝向 = 相机朝向（原 LookAt(cam.rotation * back, cam.rotation * up) 的等价写法）
        ///   · 偏移 = cacheOffset + Lerp(offsetFactor, zero, factor)，在锚点局部空间内
        ///   · 缩放 = Lerp(initScale * scaleFactor.x, initScale * scaleFactor.y, factor)
        /// </summary>
        void Apply(Entry e, Quaternion camRot, float factor)
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

            Transform hb = e.headbar;
            hb.position = e.anchor.TransformPoint(localOffset);
            hb.rotation = camRot;
            hb.localScale = localScale;
        }
    }
}
