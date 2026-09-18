/*
 * 文件名：DuelWindowBuilder.cs
 * 描述：单挑界面 window_duel.prefab 的编辑器重排工具
 *
 * 目标：把单挑界面的元素摆放成《三国志11》原版单挑界面的样子：
 *   · 左上 / 右上：双方武将头像 + 姓名 + 武力 + 体力条 + 斗志条（右侧整体镜像）
 *   · 顶部中央   ：合数
 *   · 中央       ：卡牌展示区（原版此位置是两名武将骑马对砍，这里换成左右卡牌对垒）
 *   · 左下方     ：四条行动方针斜向排列（重视攻击 → 重视防守 → 重视斗志 → 重视一发）
 *   · 左下角     ：秘杀（必杀）　　下方中央：停止　　最下方：可对武将下达指示
 *   · 右侧       ：交替（换将）按钮
 *   · 右下角     ：战报日志（本项目新增，用于卡牌表现的文字反馈）
 *
 * 重要约定：
 *   1. 只调整 RectTransform 的位置 / 尺寸与 Text 的内容，**不覆盖任何已有 Image 的 sprite 与颜色**，
 *      找不到对应精灵图的元素一律保持原样（仅摆位置），避免把美术资源改坏。
 *   2. 所有坐标以 1920×1080 为参考分辨率，配合锚点定位，在其它分辨率下等比缩放。
 *   3. 缺少的容器节点（卡牌区等）会新建，新建节点不带美术，仅作为占位与布局用。
 *
 * 用法：菜单 Sango/单挑/重排单挑界面，或由 MCP 反射调用 Build()。
 */

using Sango.Core.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.EditorTools
{
    /// <summary>单挑界面排版工具</summary>
    public static class DuelWindowBuilder
    {
        private const string PrefabPath = "Assets/Mods/Content/Assets/UI/Prefab/window_duel.prefab";

        /// <summary>参考分辨率（设计坐标基准）</summary>
        private static readonly Vector2 RefResolution = new Vector2(1920f, 1080f);

        #region 入口

        [MenuItem("Sango/单挑/重排单挑界面")]
        public static void BuildFromMenu()
        {
            Build();
        }

        /// <summary>
        /// 只重新绑定节点引用（不动任何排版）。
        /// 美术手工摆过 prefab 之后用它把引用写回去，不会被重排覆盖。
        /// </summary>
        [MenuItem("Sango/单挑/仅重绑节点引用")]
        public static void BindOnlyFromMenu()
        {
            BindOnly();
        }

        /// <summary>只做绑定：把 CardDuelView 的引用与合数数字图写入 prefab</summary>
        public static string BindOnly()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return "prefab not found: " + PrefabPath;

            try
            {
                string bindMsg = BindDuelView(root.transform);
                string digitMsg = BindBlowCounterDigits(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "仅绑定 OK；" + bindMsg + "；" + digitMsg;
            }
            catch (System.Exception e)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return "bind failed: " + e.Message;
            }
        }

        /// <summary>
        /// 把 4849-1 图集里的 0~9 数字图填进 CardDuelView.blowCounterDigits，
        /// 供「十位 / 个位」两张图片按合数换图。
        /// </summary>
        private static string BindBlowCounterDigits(Transform root)
        {
            CardDuelView view = root != null ? root.GetComponent<CardDuelView>() : null;
            if (view == null) return "合数数字图：没有 CardDuelView";

            if (view.blowCounterDigits == null || view.blowCounterDigits.Length < 10)
                view.blowCounterDigits = new UnityEngine.Sprite[10];

            int found = 0;
            for (int d = 0; d < 10; d++)
            {
                UnityEngine.Sprite sp = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(
                    "Assets/Mods/Content/Assets/UI/AtlasTexture/4849-1/4849-1_" + d + ".png");
                if (sp != null) found++;
                // 找不到也照写，保留已有的引用
                if (sp != null || view.blowCounterDigits[d] == null)
                    view.blowCounterDigits[d] = sp;
            }

            return "合数数字图 " + found + "/10";
        }

        /// <summary>按原版示意图重排单挑界面</summary>
        public static string Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return "prefab not found: " + PrefabPath;

            try
            {
                Transform t = root.transform;

                // 清理历史残留节点（占位进度条、旧合数 / 提示 / 日志 / 标题）
                string cleanMsg = DuelWindowCleanup.Run(root);

                LayoutLeftPanel(t);
                LayoutRightPanel(t);
                LayoutBlowCounter(t);
                LayoutCardArea(t);
                LayoutCommandPanel(t);
                LayoutConfirmPanel(t);
                LayoutLog(t);

                // 套用图集中的单挑 UI 图片（水墨条 / 墨板按钮等）
                string spriteMsg = DuelWindowSprites.Apply(root);

                // 排版后的收尾：底板半透明、隐藏旧窗口框、合数置顶
                string polishMsg = DuelWindowCleanup.Polish(root);

                // 把 CardDuelView 的节点引用固化进 prefab（否则 Inspector 里看全是空的）
                string bindMsg = BindDuelView(t);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "window_duel laid out OK；" + cleanMsg + "；" + spriteMsg + "；" + polishMsg + "；" + bindMsg;
            }
            catch (System.Exception e)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return "layout failed: " + e.Message;
            }
        }

        #endregion

        #region 查找工具

        /// <summary>递归按名字查找</summary>
        private static Transform Deep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = Deep(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>按相对路径查找（以 / 分隔）</summary>
        private static Transform Path(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            int i = path.IndexOf('/');
            if (i < 0) return FindChild(root, path);
            Transform head = FindChild(root, path.Substring(0, i));
            return head == null ? null : Path(head, path.Substring(i + 1));
        }

        /// <summary>查找直接子节点</summary>
        private static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }
            return null;
        }

        /// <summary>按路径查找，不存在则逐级创建</summary>
        private static RectTransform EnsurePath(Transform root, string path)
        {
            Transform cur = root;
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                Transform next = FindChild(cur, parts[i]);
                if (next == null)
                {
                    GameObject go = new GameObject(parts[i], typeof(RectTransform), typeof(CanvasRenderer));
                    go.transform.SetParent(cur, false);
                    next = go.transform;
                }
                cur = next;
            }
            return (RectTransform)cur;
        }

        #endregion

        #region 排版工具

        /// <summary>
        /// 设置矩形：锚点、轴心、位置（以 1920×1080 设计像素给出）
        /// </summary>
        private static void Rect(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            RectTransform rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        /// <summary>铺满父节点，并留出四周内边距</summary>
        private static void Stretch(Transform t, float padding)
        {
            RectTransform rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            rt.localScale = Vector3.one;
        }

        /// <summary>铺满父节点</summary>
        private static void StretchFull(Transform t)
        {
            RectTransform rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>只改文字内容，保留字体 / 字号 / 颜色 / 对齐等已有设置</summary>
        private static void SetText(Transform t, string content)
        {
            if (t == null) return;
            Text txt = t.GetComponent<Text>();
            if (txt != null && !string.IsNullOrEmpty(content)) txt.text = content;
        }

        /// <summary>
        /// 校验节点存在；不存在则返回 false（用于"找不到就不处理"的元素）
        /// </summary>
        private static bool Has(Transform t)
        {
            return t != null;
        }

        #endregion

        #region 左右武将条

        /// <summary>状态条与武将条的版面常量（1920×1080 设计像素）</summary>
        private static class Hud
        {
            /// <summary>面板贴屏幕左上/右上的边距</summary>
            public const float PanelMarginX = 36f;
            public const float PanelMarginY = 8f;
            /// <summary>面板整体尺寸（需容纳主将头像 + 两条状态条 + 两行副将）</summary>
            public const float PanelWidth = 900f;
            public const float PanelHeight = 340f;

            /// <summary>主将头像框</summary>
            public const float HeadWidth = 200f;
            public const float HeadHeight = 150f;

            /// <summary>主将姓名 / 武力（相对头像右边缘的起点）</summary>
            public const float MainNameX = 206f;
            public const float MainStrengthX = 462f;

            /// <summary>主将体力 / 斗志的墨迹长条</summary>
            public const float BarInkX = 200f;
            public const float BarWidth = 620f;
            public const float MainHpY = -58f;
            public const float BarInkHeight = 84f;
            public const float MainMpY = -150f;
            public const float SpiritInkHeight = 38f;

            /// <summary>
            /// 墨迹长条 sprite（4849-1_14，452×120）中空槽所占的归一化区间。
            /// 长条左侧约 35% 是水墨飞白，右段才是可填充的凹槽。
            /// </summary>
            public const float BarGrooveLeft = 0.355f;
            public const float BarGrooveRight = 1f;
            public const float BarGrooveTop = 0.38f;
            public const float BarGrooveBottom = 0.70f;

            /// <summary>细槽素材（4849-1_14 之外的第二类条状图）上几乎满幅的凹槽区间</summary>
            public const float SlotGrooveLeft = 0.03f;
            public const float SlotGrooveRight = 0.97f;
            public const float SlotGrooveTop = 0.16f;
            public const float SlotGrooveBottom = 0.84f;

            /// <summary>副将行</summary>
            public const float SubTopY = -196f;
            public const float SubRowStep = 72f;
            public const float SubWidth = 720f;
            public const float SubHeight = 64f;
            public const float SubHeadSize = 56f;
            public const float SubNameX = 62f;
            public const float SubStrengthX = 206f;
            public const float SubBarInkX = 58f;
            public const float SubBarInkY = -42f;
            public const float SubBarWidth = 340f;
            public const float SubBarInkHeight = 34f;

            /// <summary>合数</summary>
            public const int BlowCounterFontSize = 72;
        }

        /// <summary>左侧武将条（左上角，整体靠左对齐）</summary>
        private static void LayoutLeftPanel(Transform root)
        {
            Transform panel = Deep(root, "LeftPanel");
            if (!Has(panel)) return;

            Rect(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Hud.PanelMarginX, -Hud.PanelMarginY),
                new Vector2(Hud.PanelWidth, Hud.PanelHeight));

            BuildCharaStrip(root, panel, false);

            Transform title = Deep(panel, "Title");
            if (Has(title)) title.gameObject.SetActive(false);
        }

        /// <summary>右侧武将条（右上角，整体镜像）</summary>
        private static void LayoutRightPanel(Transform root)
        {
            Transform panel = Deep(root, "RightPanel");
            if (!Has(panel)) return;

            Rect(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Hud.PanelMarginX, -Hud.PanelMarginY),
                new Vector2(Hud.PanelWidth, Hud.PanelHeight));

            BuildCharaStrip(root, panel, true);

            Transform title = Deep(panel, "Title");
            if (Has(title)) title.gameObject.SetActive(false);
        }

        /// <summary>
        /// 摆放一个阵营的武将信息：主将在上（头像 + 姓名 + 武力 + 体力 / 斗志条），
        /// 副将以小头像行依次排在下方。
        ///
        /// 状态条的层次（对齐原版：水墨长条压在下面，条槽与填充浮在其上）：
        ///   hpInk / mpInk   墨迹长条（视觉底，来自图集 4849-1）
        ///   hp / mp         条槽范围（透明，运行时按名字 "hp" / "mp" 找到它）
        ///     fill / fill_r 填充图（左侧阵营自左向右涨，右侧阵营自右向左涨）
        ///     txt           数值文本
        /// </summary>
        private static void BuildCharaStrip(Transform root, Transform panel, bool mirrored)
        {
            // 水平方向的符号：左侧为 +1（向右生长），右侧为 -1（向左生长）
            float s = mirrored ? -1f : 1f;
            Vector2 anchorX = new Vector2(mirrored ? 1f : 0f, 1f);

            // ---- 主将 ----
            Transform main = Path(panel, "person");
            if (Has(main))
            {
                Rect(main, anchorX, anchorX, anchorX, Vector2.zero,
                    new Vector2(Hud.PanelWidth, Hud.PanelHeight));

                BuildPortrait(main, anchorX, Vector2.zero, Hud.HeadWidth, Hud.HeadHeight, 7f);
                BuildNamePlate(main, anchorX, new Vector2(Hud.MainNameX * s, -8f), new Vector2(300f, 54f), 1);

                SetRect(Path(main, "name"), anchorX, new Vector2((Hud.MainNameX + 8f) * s, -6f),
                    new Vector2(250f, 44f), mirrored);
                SetRect(Path(main, "Strength"), anchorX, new Vector2(Hud.MainStrengthX * s, -12f),
                    new Vector2(210f, 36f), mirrored);

                BuildStatusBar(main, anchorX, mirrored, "hp",
                    new Vector2(Hud.BarInkX * s, Hud.MainHpY), new Vector2(Hud.BarWidth, Hud.BarInkHeight));
                // 斗志条用的是普通细槽素材，横向与体力条对齐，纵向占满槽面
                BuildStatusBar(main, anchorX, mirrored, "mp",
                    new Vector2(Hud.BarInkX * s, Hud.MainMpY), new Vector2(Hud.BarWidth, Hud.SpiritInkHeight),
                    Hud.BarGrooveLeft, Hud.BarGrooveRight - Hud.BarGrooveLeft,
                    Hud.SlotGrooveTop, Hud.SlotGrooveBottom - Hud.SlotGrooveTop);
            }

            // ---- 副将（小行）----
            for (int i = 1; i <= 2; i++)
            {
                Transform sub = Path(panel, "person_" + i);
                if (!Has(sub)) continue;

                float y = Hud.SubTopY - (i - 1) * Hud.SubRowStep;
                Rect(sub, anchorX, anchorX, anchorX, new Vector2(0f, y),
                    new Vector2(Hud.SubWidth, Hud.SubHeight));

                BuildPortrait(sub, anchorX, new Vector2(0f, -2f), Hud.SubHeadSize, Hud.SubHeadSize, 5f);
                BuildNamePlate(sub, anchorX, new Vector2(Hud.SubNameX * s, -6f), new Vector2(210f, 38f), 1);

                SetRect(Path(sub, "name"), anchorX, new Vector2((Hud.SubNameX + 6f) * s, -4f),
                    new Vector2(140f, 30f), mirrored);
                SetRect(Path(sub, "Strength"), anchorX, new Vector2(Hud.SubStrengthX * s, -8f),
                    new Vector2(120f, 28f), mirrored);

                BuildStatusBar(sub, anchorX, mirrored, "hp",
                    new Vector2(Hud.SubBarInkX * s, Hud.SubBarInkY),
                    new Vector2(Hud.SubBarWidth, Hud.SubBarInkHeight));
            }
        }

        /// <summary>头像：底框（headbg）+ 头像本体（head）</summary>
        private static void BuildPortrait(Transform owner, Vector2 anchor, Vector2 pos, float w, float h, float pad)
        {
            Transform headbg = Path(owner, "headbg");
            if (!Has(headbg)) return;

            Rect(headbg, anchor, anchor, anchor, pos, new Vector2(w, h));
            EnsureImage(headbg);
            Transform head = Path(headbg, "head");
            if (Has(head)) Stretch(head, pad);
        }

        /// <summary>姓名条底：在 name 之前插入一块墨迹底板（已存在则只调位置）</summary>
        private static void BuildNamePlate(Transform owner, Vector2 anchor, Vector2 pos, Vector2 size, int order)
        {
            Transform name = Path(owner, "name");
            if (!Has(name)) return;

            Transform plate = EnsureImage(EnsurePath(owner, "NamePlate"));
            plate.SetSiblingIndex(Mathf.Max(0, name.GetSiblingIndex() - order));
            Rect(plate, anchor, anchor, anchor, pos, size);
        }

        /// <summary>
        /// 摆放一条状态条。底板素材的凹槽（归一化区间）决定条槽范围，
        /// 从而让填充恰好落在水墨条的空槽里。
        /// </summary>
        private static void BuildStatusBar(Transform owner, Vector2 anchor, bool mirrored, string barName,
            Vector2 inkPos, Vector2 inkSize)
        {
            BuildStatusBar(owner, anchor, mirrored, barName, inkPos, inkSize,
                Hud.BarGrooveLeft, Hud.BarGrooveRight - Hud.BarGrooveLeft,
                Hud.BarGrooveTop, Hud.BarGrooveBottom - Hud.BarGrooveTop);
        }

        /// <summary>按给定的凹槽区间摆放状态条</summary>
        private static void BuildStatusBar(Transform owner, Vector2 anchor, bool mirrored, string barName,
            Vector2 inkPos, Vector2 inkSize,
            float grooveLeft, float grooveWidth, float grooveTop, float grooveHeight)
        {
            Transform slot = Path(owner, barName);
            if (!Has(slot)) return;

            // 底板（视觉底），插在条槽之前，避免盖住填充
            Transform ink = EnsureImage(EnsurePath(owner, barName + "Ink"));
            if (ink.GetSiblingIndex() > slot.GetSiblingIndex())
                ink.SetSiblingIndex(slot.GetSiblingIndex());
            Rect(ink, anchor, anchor, anchor, inkPos, inkSize);

            // 凹槽范围 → 条槽：左侧阵营自凹槽左端向右量，右侧阵营与底板右端对齐
            float slotX = mirrored ? inkPos.x : inkPos.x + grooveLeft * inkSize.x;
            float slotY = inkPos.y - grooveTop * inkSize.y;
            Rect(slot, anchor, anchor, anchor, new Vector2(slotX, slotY),
                new Vector2(grooveWidth * inkSize.x, grooveHeight * inkSize.y));

            // 条槽本体不要美术（墨迹长条已经承担视觉）
            Image slotImg = slot.GetComponent<Image>();
            if (slotImg != null)
            {
                slotImg.sprite = null;
                slotImg.color = new Color(1f, 1f, 1f, 0f);
            }

            // 填充子节点：左侧阵营叫 fill（自左向右），右侧阵营叫 fill_r（自右向左）
            string fillName = mirrored ? "fill_r" : "fill";
            Transform fill = FindChild(slot, "fill") ?? FindChild(slot, "fill_r") ?? FindChild(slot, barName);
            if (fill != null && fill.name != fillName) fill.name = fillName;
            if (fill != null) StretchFull(fill);

            Transform txt = FindChild(slot, "txt");
            if (Has(txt)) Stretch(txt, 2f);
        }

        /// <summary>设置矩形并按阵营对齐文字</summary>
        private static void SetRect(Transform t, Vector2 anchor, Vector2 pos, Vector2 size, bool mirrored)
        {
            if (!Has(t)) return;
            Rect(t, anchor, anchor, anchor, pos, size);
            SetAlign(t, mirrored);
        }

        /// <summary>确保节点带 Image（新建节点时不带美术，只给组件）</summary>
        private static Transform EnsureImage(Transform t)
        {
            if (t == null) return null;
            if (t.GetComponent<Image>() == null && t.GetComponent<RawImage>() == null)
                t.gameObject.AddComponent<Image>();
            return t;
        }

        /// <summary>
        /// 把 CardDuelView 的节点引用固化进 prefab。
        ///
        /// 该 prefab 原先一条引用都没写，全靠运行时 Awake() 里的名字查找，
        /// 结果 Inspector 里看全是 None，像是"没绑定"；运行时一旦查找失败就毫无表现。
        /// 这里显式绑定一次并随 prefab 保存，同时补齐项目其它窗口都有的 UGUIWindow。
        /// </summary>
        private static string BindDuelView(Transform root)
        {
            if (root == null) return "绑定：根节点为空";

            CardDuelView view = root.GetComponent<CardDuelView>();
            bool created = false;
            if (view == null)
            {
                view = root.gameObject.AddComponent<CardDuelView>();
                created = true;
            }

            // 按名字查找并写入序列化字段
            view.Rebind();

            // CardDuelView 本身就是 UGUIWindow，根节点上不应再有第二个（空的）UGUIWindow：
            // 那会让 Window.CreateWindow 取到空壳而不是表现层本体。这里顺手清掉历史遗留。
            UGUIWindow[] wins = root.GetComponents<UGUIWindow>();
            for (int i = 0; i < wins.Length; i++)
            {
                if (wins[i] != null && !(wins[i] is CardDuelView))
                    UnityEngine.Object.DestroyImmediate(wins[i]);
            }

            int bound = 0;
            int total = 0;
            Count(view.blowCounterText, ref bound, ref total);
            Count(view.logText, ref bound, ref total);
            Count(view.confirmPanel, ref bound, ref total);
            Count(view.commandPanel, ref bound, ref total);
            Count(view.commandPanelAlt, ref bound, ref total);
            Count(view.btnPlay, ref bound, ref total);
            Count(view.btnSpecial, ref bound, ref total);
            Count(view.confirmText, ref bound, ref total);
            Count(view.btnWatch, ref bound, ref total);
            Count(view.btnSkip, ref bound, ref total);
            Count(view.skipHint, ref bound, ref total);
            Count(view.stanceButtons, ref bound, ref total);
            Count(view.switchButtons, ref bound, ref total);
            Count(view.leftCharaSlots, ref bound, ref total);
            Count(view.rightCharaSlots, ref bound, ref total);
            Count(view.leftSpiritPips, ref bound, ref total);
            Count(view.rightSpiritPips, ref bound, ref total);

            string buttonMsg = BindButtonEvents(root, view);

            return "绑定 CardDuelView " + bound + "/" + total + " 项"
                + (created ? "（新建组件）" : "") + "；" + buttonMsg;
        }

        /// <summary>
        /// 把按钮的 onClick 持久化接到 CardDuelView 的公开回调上。
        /// 这样 Inspector 里能看到完整的事件绑定，也不再依赖运行时 AddListener。
        /// </summary>
        private static string BindButtonEvents(Transform root, CardDuelView view)
        {
            if (view == null) return "按钮事件：没有 CardDuelView";

            int count = 0;
            for (int i = 0; i < 4; i++)
                count += Wire(root, "Stance" + i, view, "OnStance" + i + "Click");
            for (int i = 0; i < 3; i++)
                count += Wire(root, "Switch" + i, view, "OnSwitch" + i + "Click");

            count += Wire(root, "BtnSpecial", view, "OnSpecialClick");
            count += Wire(root, "BtnSpecialCancel", view, "OnSpecialCancelClick");
            count += Wire(root, "BtnPlay", view, "OnPlayClick");
            count += Wire(root, "BtnWatch", view, "OnWatchClick");
            count += Wire(root, "BtnSkip", view, "OnSkipClick");

            return "按钮事件 " + count + " 个";
        }

        /// <summary>把某个按钮的 onClick 接到 view 上的一个无参方法</summary>
        private static int Wire(Transform root, string buttonName, CardDuelView view, string methodName)
        {
            Transform t = Deep(root, buttonName);
            if (t == null) return 0;

            Button button = t.GetComponent<Button>();
            if (button == null) return 0;

            System.Reflection.MethodInfo method = typeof(CardDuelView).GetMethod(methodName);
            if (method == null) return 0;

            UnityEngine.Events.UnityAction action = System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), view, method) as UnityEngine.Events.UnityAction;
            if (action == null) return 0;

            // 先清掉旧的持久化监听，避免重复挂导致一次点击触发多次
            while (button.onClick.GetPersistentEventCount() > 0)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);

            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
            return 1;
        }

        /// <summary>统计单个引用是否绑定成功</summary>
        private static void Count(UnityEngine.Object target, ref int bound, ref int total)
        {
            total++;
            if (target != null) bound++;
        }

        /// <summary>统计一组引用是否绑定成功</summary>
        private static void Count(UnityEngine.Object[] targets, ref int bound, ref int total)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                Count(targets[i], ref bound, ref total);
        }

        /// <summary>右侧阵营的文字右对齐，左侧左对齐</summary>
        private static void SetAlign(Transform t, bool mirrored)
        {
            Text txt = t.GetComponent<Text>();
            if (txt == null) return;
            txt.alignment = mirrored ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        }

        #endregion

        #region 合数

        private static void LayoutBlowCounter(Transform root)
        {
            Vector2 anchor = new Vector2(0.5f, 1f);

            Transform bg = Deep(root, "BlowCounter_bg");
            if (Has(bg))
            {
                Rect(bg, anchor, anchor, anchor, new Vector2(0f, -12f), new Vector2(440f, 116f));

                // 单位「合」：原版写作「N 合」，数字用大字、单位用图集里的「合」字
                Transform unit = EnsureImage(EnsurePath(bg, "BlowCounterUnit"));
                Rect(unit, anchor, anchor, anchor, new Vector2(126f, -50f), new Vector2(56f, 56f));
            }

            // 数字右对齐，使位数变化时右侧始终对齐「合」字
            Transform counter = Deep(root, "BlowCounter");
            if (Has(counter))
            {
                Rect(counter, anchor, anchor, anchor, new Vector2(-100f, -12f), new Vector2(420f, 104f));
                Text txt = counter.GetComponent<Text>();
                if (txt != null)
                {
                    txt.alignment = TextAnchor.UpperRight;
                    txt.fontSize = Hud.BlowCounterFontSize;
                }
            }
        }

        #endregion

        #region 卡牌展示区

        /// <summary>中央卡牌区：替换原版"两名武将骑马对砍"的位置</summary>
        private static void LayoutCardArea(Transform root)
        {
            RectTransform area = EnsurePath(root, "CardArea");
            // 对应原版骑马画面的范围：屏幕中部偏上，宽 1344、高 700
            Rect(area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -30f), new Vector2(1344f, 700f));

            BuildCard(area, "CardLeft", -280f);
            BuildCard(area, "CardRight", 280f);

            // 中央对峙标记
            RectTransform vs = EnsurePath(area, "CardCenter");
            Rect(vs, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(180f, 90f));
        }

        /// <summary>一张卡牌：卡面 → 头像 / 名称 / 数值</summary>
        private static void BuildCard(RectTransform area, string name, float x)
        {
            RectTransform card = EnsurePath(area, name);
            Rect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 0f), new Vector2(300f, 430f));

            RectTransform face = EnsurePath(card, "face");
            Stretch(face, 6f);

            RectTransform head = EnsurePath(face, "head");
            Rect(head, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -26f), new Vector2(190f, 190f));
            EnsureRawImage(head);

            RectTransform title = EnsurePath(face, "name");
            Rect(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -228f), new Vector2(260f, 40f));

            RectTransform detail = EnsurePath(face, "detail");
            Rect(detail, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(260f, 130f));
        }

        /// <summary>新建节点时才补 RawImage，已存在则保持原样</summary>
        private static void EnsureRawImage(Transform t)
        {
            if (t.GetComponent<RawImage>() == null && t.GetComponent<Image>() == null)
                t.gameObject.AddComponent<RawImage>();
        }

        #endregion

        #region 命令面板

        /// <summary>
        /// 命令按钮按原版示意图摆放：
        /// 四条方针在左下方斜向排开，秘杀在左下角，停止在下方中央，交替在右侧，提示在最底部。
        /// </summary>
        private static void LayoutCommandPanel(Transform root)
        {
            Transform panel = Deep(root, "CommandPanel");
            if (!Has(panel)) return;

            // 命令面板本身铺满全屏，子节点各自贴屏幕角落定位
            StretchFull(panel);

            // 四条行动方针：自左上向右下斜排，与原版一致
            //   重视攻击 (58,245) → 重视防守 (134,183) → 重视斗志 (202,129) → 重视一发 (278,72)
            Vector2[] stancePos =
            {
                new Vector2(58f, 245f),
                new Vector2(134f, 183f),
                new Vector2(202f, 129f),
                new Vector2(278f, 72f),
            };
            Vector2[] stanceSize =
            {
                new Vector2(212f, 56f),
                new Vector2(204f, 58f),
                new Vector2(212f, 58f),
                new Vector2(212f, 58f),
            };
            for (int i = 0; i < 4; i++)
            {
                Transform stance = FindChild(panel, "Stance" + i);
                if (!Has(stance)) continue;
                Rect(stance, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), stancePos[i], stanceSize[i]);
                StretchFull(FindChild(stance, "Label"));
            }

            // 秘杀：左下角
            Transform special = FindChild(panel, "BtnSpecial");
            if (Has(special))
            {
                Rect(special, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(16f, 26f), new Vector2(180f, 126f));
                StretchFull(FindChild(special, "Label"));
            }

            // 取消：紧挨秘杀右侧
            Transform specialCancel = FindChild(panel, "BtnSpecialCancel");
            if (Has(specialCancel))
            {
                Rect(specialCancel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(206f, 26f), new Vector2(112f, 48f));
                StretchFull(FindChild(specialCancel, "Label"));
            }

            // 停止：下方中央
            Transform play = FindChild(panel, "BtnPlay");
            if (Has(play))
            {
                Rect(play, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(60f, 54f), new Vector2(240f, 104f));
                StretchFull(FindChild(play, "Label"));
            }

            // 交替：右侧，纵向排在右上武将条（含副将行）下方
            for (int i = 0; i < 3; i++)
            {
                Transform sw = FindChild(panel, "Switch" + i);
                if (!Has(sw)) continue;
                Rect(sw, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-70f, -376f - i * 64f), new Vector2(58f, 58f));
                StretchFull(FindChild(sw, "Label"));
            }

            // 操作提示：最底部中央
            Transform hint = FindChild(panel, "SkipHint");
            if (Has(hint))
            {
                Rect(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 8f), new Vector2(760f, 34f));
            }
        }

        #endregion

        #region 确认面板与战报

        private static void LayoutConfirmPanel(Transform root)
        {
            Transform panel = Deep(root, "ConfirmPanel");
            if (!Has(panel)) return;

            Rect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 40f), new Vector2(620f, 220f));

            Rect(Deep(panel, "ConfirmText"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(560f, 100f));

            Rect(Deep(panel, "BtnWatch"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-92f, 24f), new Vector2(160f, 48f));
            StretchFull(Deep(panel, "BtnWatch/Label"));

            Rect(Deep(panel, "BtnSkip"), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(92f, 24f), new Vector2(160f, 48f));
            StretchFull(Deep(panel, "BtnSkip/Label"));
        }

        /// <summary>战报日志：右下角，本项目新增，避免遮挡卡牌区</summary>
        private static void LayoutLog(Transform root)
        {
            Transform bg = Deep(root, "LogBg");
            if (!Has(bg)) return;

            Rect(bg, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-24f, 36f), new Vector2(430f, 210f));

            Transform content = FindChild(bg, "content");
            if (Has(content)) Stretch(content, 10f);

            // 清理后日志文本是 LogBg 的直接子节点，铺满并留出内边距
            Transform log = Deep(bg, "log");
            if (Has(log)) Stretch(log, 12f);
        }

        #endregion
    }
}
