/*
 * 文件名：DebateWindowBuilder.cs
 * 描述：舌战界面 window_debate.prefab 的编辑器构建/重绑工具
 *
 * 布局（1920×1080 参考分辨率，锚点定位）：
 *   · 顶部中央：当前话题 + 话题提示
 *   · 左上 / 右上：双方武将（头像 + 姓名 + 智力 + 体力条 + 愤怒条 + 激昂标记 + 本回合出牌）
 *   · 中央：会心抉择（追击 / 留情，平时隐藏）
 *   · 下方中央：玩家手牌 7 张（横向排列，与本回合话题一致的牌带 ★）
 *   · 右下：战报日志
 *   · 底部：提示 / 结算画面（结算时弹出，点关闭收场）
 *
 * 重要约定（照 DuelWindowBuilder 的经验）：
 *   1. prefab **已存在就只重绑节点引用**，不重建、不覆盖美术手工摆放的位置与图；
 *      要重新生成布局，先删掉 prefab 或调 Build(true)。
 *   2. 字体与飘字节点直接**借** window_duel.prefab 里已经配好的那套
 *      （Text 的字体、AnimationText 的曲线），避免凭空造一套不成形的资源。
 *   3. 新建的占位节点只给纯色 Image，不给精灵图，等美术替换。
 *
 * 用法：菜单 Sango/舌战/生成舌战界面，或 Sango/舌战/仅重绑节点引用。
 */

using Sango.Core.Debate;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.EditorTools
{
    /// <summary>舌战界面排版工具</summary>
    public static class DebateWindowBuilder
    {
        private const string PrefabPath = "Assets/Mods/Content/Assets/UI/Prefab/window_debate.prefab";
        private const string DuelPrefabPath = "Assets/Mods/Content/Assets/UI/Prefab/window_duel.prefab";

        /// <summary>参考分辨率（设计坐标基准）</summary>
        private static readonly Vector2 RefResolution = new Vector2(1920f, 1080f);

        #region 入口

        [MenuItem("Sango/舌战/生成舌战界面")]
        public static void BuildFromMenu()
        {
            Debug.Log(Build(false));
        }

        [MenuItem("Sango/舌战/强制重建舌战界面（会覆盖手工排版）")]
        public static void RebuildFromMenu()
        {
            Debug.Log(Build(true));
        }

        [MenuItem("Sango/舌战/仅重绑节点引用")]
        public static void BindOnlyFromMenu()
        {
            Debug.Log(BindOnly());
        }

        /// <summary>只重绑节点引用（不动布局、不动美术）</summary>
        public static string BindOnly()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return "prefab not found: " + PrefabPath;

            try
            {
                string bindMsg = BindView(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "window_debate 仅绑定 OK；" + bindMsg;
            }
            catch (System.Exception e)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return "bind failed: " + e.Message;
            }
        }

        /// <summary>
        /// 生成 / 重建舌战界面。
        /// </summary>
        /// <param name="forceRebuild">true = 丢弃已有 prefab 重新生成（会覆盖手工排版）</param>
        public static string Build(bool forceRebuild)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;

            if (exists && !forceRebuild)
                return "window_debate 已存在，按约定只重绑引用（要重建请用「强制重建」菜单）；" + BindOnly();

            if (exists)
                AssetDatabase.DeleteAsset(PrefabPath);

            // 借单挑界面里现成的字体与飘字节点
            Font font = null;
            GameObject duelRoot = PrefabUtility.LoadPrefabContents(DuelPrefabPath);
            GameObject floatTemplate = null;
            try
            {
                if (duelRoot != null)
                {
                    Text anyText = duelRoot.GetComponentInChildren<Text>(true);
                    if (anyText != null) font = anyText.font;

                    Transform ani = Deep(duelRoot.transform, "ani_info");
                    if (ani != null)
                    {
                        // 注意写全 UnityEngine.Object：Sango 命名空间下另有同名 Object 类
                        floatTemplate = UnityEngine.Object.Instantiate(ani.gameObject);
                        floatTemplate.name = "float";
                    }
                }
            }
            finally
            {
                if (duelRoot != null) PrefabUtility.UnloadPrefabContents(duelRoot);
            }

            if (font == null)
                Sango.Log.Warning("舌战界面：没能在 window_duel 里找到字体，文字会没有字体，请手工指定。");

            GameObject root = CreateRoot();
            try
            {
                BuildLayout(root.transform, font, floatTemplate);
                string bindMsg = BindView(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "window_debate 已生成；" + bindMsg;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (floatTemplate != null) UnityEngine.Object.DestroyImmediate(floatTemplate);
            }
        }

        #endregion

        #region 生成

        /// <summary>根节点：Canvas + 缩放 + 射线，挂 CardDebateView</summary>
        private static GameObject CreateRoot()
        {
            GameObject root = new GameObject("window_debate",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CardDebateView));

            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return root;
        }

        /// <summary>搭出整棵界面树</summary>
        private static void BuildLayout(Transform root, Font font, GameObject floatTemplate)
        {
            // 半透明遮罩
            Image mask = NewImage("mask", root, 0f, 0f, 0f, 0f, new Color(0f, 0f, 0f, 0.72f));
            Stretch(mask.rectTransform);

            // 顶部：话题
            Text topic = NewText("topic", root, 0f, 430f, 420f, 70f, 44, TextAnchor.MiddleCenter, "话题", Color.white, font);
            topic.text = "故事";
            NewText("topicHint", root, 0f, 380f, 720f, 40f, 22, TextAnchor.MiddleCenter, "", new Color(0.8f, 0.8f, 0.8f, 1f), font);

            // 两侧武将区
            BuildSide(root, "left", -560f, font);
            BuildSide(root, "right", 560f, font);

            // 会心抉择（默认隐藏，由表现层在需要时点亮）
            GameObject critical = NewNode("Critical", root, 0f, -80f, 520f, 120f);
            NewButton("BtnPushOn", critical.transform, -130f, 0f, 220f, 80f, "追击", font);
            NewButton("BtnMercy", critical.transform, 130f, 0f, 220f, 80f, "留情", font);
            critical.SetActive(false);

            // 手牌
            GameObject hand = NewNode("Hand", root, 0f, -360f, 1260f, 220f);
            HorizontalLayoutGroup layout = hand.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < Debate.MaxCardCount; i++)
            {
                Button b = NewButton("Hand" + i, hand.transform, 0f, 0f, 160f, 210f, "—", font);
                b.GetComponent<Image>().color = new Color(0.18f, 0.2f, 0.28f, 0.95f);
            }

            // 底部提示
            NewText("hint", root, 0f, -500f, 900f, 44f, 24, TextAnchor.MiddleCenter, "", new Color(1f, 0.92f, 0.7f, 1f), font);

            // 战报
            GameObject logBg = NewImage("LogBg", root, 600f, -300f, 660f, 320f, new Color(0f, 0f, 0f, 0.55f)).gameObject;
            Text log = NewText("log", logBg.transform, 0f, 0f, 620f, 280f, 20, TextAnchor.LowerLeft, "", Color.white, font);
            log.rectTransform.anchoredPosition = Vector2.zero;
            log.horizontalOverflow = HorizontalWrapMode.Wrap;
            log.verticalOverflow = VerticalWrapMode.Truncate;

            // 飘字：直接借单挑里配好的 AnimationText 节点
            if (floatTemplate != null)
            {
                floatTemplate.transform.SetParent(root, false);
                RectTransform frt = (RectTransform)floatTemplate.transform;
                frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
                frt.pivot = new Vector2(0.5f, 0.5f);
                frt.anchoredPosition = Vector2.zero;
            }
            else
            {
                Sango.Log.Warning("舌战界面：没能在 window_duel 里找到 ani_info 飘字节点，本次未生成飘字载体。");
            }

            // 结算画面（默认隐藏）
            GameObject result = NewImage("Result", root, 0f, 0f, 900f, 420f, new Color(0.06f, 0.07f, 0.1f, 0.95f)).gameObject;
            NewText("title", result.transform, 0f, 110f, 800f, 110f, 64, TextAnchor.MiddleCenter, "", new Color(1f, 0.9f, 0.6f, 1f), font);
            NewText("desc", result.transform, 0f, 20f, 800f, 60f, 28, TextAnchor.MiddleCenter, "", Color.white, font);
            NewButton("BtnClose", result.transform, 0f, -110f, 220f, 80f, "关闭", font);
            result.SetActive(false);
        }

        /// <summary>一侧武将区</summary>
        private static void BuildSide(Transform root, string sideName, float x, Font font)
        {
            GameObject side = NewNode(sideName, root, x, 250f, 640f, 260f);

            // 头像（RawImage，运行时填纹理）
            GameObject head = NewNode("head", side.transform, -240f, 0f, 160f, 200f);
            RawImage raw = head.AddComponent<RawImage>();
            raw.color = new Color(1f, 1f, 1f, 0.9f);

            NewText("name", side.transform, 60f, 90f, 420f, 60f, 36, TextAnchor.MiddleLeft, "—", Color.white, font);
            NewText("intel", side.transform, 60f, 45f, 420f, 40f, 22, TextAnchor.MiddleLeft, "智力 --", new Color(0.85f, 0.88f, 0.95f, 1f), font);

            // 体力条 / 愤怒条（Filled 横向，运行时按比例改 fillAmount）
            MakeBar(side.transform, "hpBar", "hpText", -60f, -20f, 420f, "体力", new Color(0.85f, 0.25f, 0.25f, 1f), font);
            MakeBar(side.transform, "stressBar", "stressText", -60f, -70f, 420f, "愤怒", new Color(0.95f, 0.7f, 0.2f, 1f), font);

            // 激昂标记
            Text anger = NewText("anger", side.transform, 260f, 90f, 140f, 50f, 26, TextAnchor.MiddleCenter, "激昂", new Color(1f, 0.4f, 0.35f, 1f), font);
            anger.gameObject.SetActive(false);

            // 本回合出牌
            NewText("played", side.transform, 60f, -115f, 500f, 40f, 22, TextAnchor.MiddleLeft, "本回合：—", new Color(0.9f, 0.9f, 0.9f, 1f), font);
        }

        /// <summary>做一条进度条 + 上面的数值文字</summary>
        private static void MakeBar(Transform parent, string barName, string textName, float x, float y, float width, string label, Color color, Font font)
        {
            GameObject bar = NewImage(barName, parent, x, y, width, 28f, new Color(color.r, color.g, color.b, 0.35f)).gameObject;
            Image bg = bar.GetComponent<Image>();

            GameObject fill = NewImage("fill", bar.transform, 0f, 0f, width, 28f, color).gameObject;
            Stretch(fill.GetComponent<RectTransform>());
            Image fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;
            fillImage.sprite = null;

            // 让表现层直接把 bar 当填充图用（fillAmount 作用在 bar 自己身上）
            bg.type = Image.Type.Filled;
            bg.fillMethod = Image.FillMethod.Horizontal;
            bg.fillAmount = 1f;

            // 数值文字（覆盖在条上）
            Text t = NewText(textName, bar.transform, 0f, 0f, width, 28f, 20, TextAnchor.MiddleCenter, label + " --", Color.white, font);
            t.rectTransform.anchoredPosition = Vector2.zero;
        }

        #endregion

        #region 绑定

        /// <summary>把节点引用写进 CardDebateView（随 prefab 保存，Inspector 里可见）</summary>
        private static string BindView(Transform root)
        {
            if (root == null) return "绑定：根节点为空";

            CardDebateView view = root.GetComponent<CardDebateView>();
            bool created = false;
            if (view == null)
            {
                view = root.gameObject.AddComponent<CardDebateView>();
                created = true;
            }

            view.Rebind();

            // 根节点上不应再有第二个（空的）UGUIWindow：那会让 Window.CreateWindow 拿到空壳
            UGUIWindow[] wins = root.GetComponents<UGUIWindow>();
            for (int i = 0; i < wins.Length; i++)
            {
                if (wins[i] != null && !(wins[i] is CardDebateView))
                    UnityEngine.Object.DestroyImmediate(wins[i]);
            }

            int bound = 0, total = 0;
            Count(view.topicText, ref bound, ref total);
            Count(view.topicHint, ref bound, ref total);
            Count(view.hintText, ref bound, ref total);
            Count(view.logText, ref bound, ref total);
            Count(view.floatText, ref bound, ref total);
            Count(view.criticalPanel, ref bound, ref total);
            Count(view.btnPushOn, ref bound, ref total);
            Count(view.btnMercy, ref bound, ref total);
            Count(view.resultPanel, ref bound, ref total);
            Count(view.resultTitle, ref bound, ref total);
            Count(view.resultDesc, ref bound, ref total);
            Count(view.btnClose, ref bound, ref total);
            Count(view.handButtons, ref bound, ref total);
            Count(view.handLabels, ref bound, ref total);
            CountSide(view.leftSide, ref bound, ref total);
            CountSide(view.rightSide, ref bound, ref total);

            return "绑定 CardDebateView " + bound + "/" + total + " 项" + (created ? "（新建组件）" : "");
        }

        private static void CountSide(CardDebateView.DebateSide side, ref int bound, ref int total)
        {
            if (side == null) return;
            Count(side.head, ref bound, ref total);
            Count(side.nameText, ref bound, ref total);
            Count(side.intelText, ref bound, ref total);
            Count(side.hpBar, ref bound, ref total);
            Count(side.hpText, ref bound, ref total);
            Count(side.stressBar, ref bound, ref total);
            Count(side.stressText, ref bound, ref total);
            Count(side.angerTag, ref bound, ref total);
            Count(side.playedCard, ref bound, ref total);
        }

        #endregion

        #region 建节点工具

        private static GameObject NewNode(string name, Transform parent, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static Image NewImage(string name, Transform parent, float x, float y, float w, float h, Color color)
        {
            GameObject go = NewNode(name, parent, x, y, w, h);
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text NewText(string name, Transform parent, float x, float y, float w, float h,
            int fontSize, TextAnchor anchor, string content, Color color, Font font)
        {
            GameObject go = NewNode(name, parent, x, y, w, h);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.text = content;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button NewButton(string name, Transform parent, float x, float y, float w, float h, string label, Font font)
        {
            Image image = NewImage(name, parent, x, y, w, h, new Color(0.25f, 0.3f, 0.42f, 0.95f));
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = NewText("lab", image.transform, 0f, 0f, w, h, 26, TextAnchor.MiddleCenter, label, Color.white, font);
            text.rectTransform.anchoredPosition = Vector2.zero;
            return button;
        }

        /// <summary>铺满父节点</summary>
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

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

        private static void Count(UnityEngine.Object target, ref int bound, ref int total)
        {
            total++;
            if (target != null) bound++;
        }

        private static void Count(UnityEngine.Object[] targets, ref int bound, ref int total)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                Count(targets[i], ref bound, ref total);
        }

        #endregion

        /// <summary>参考分辨率（供外部工具查询）</summary>
        public static Vector2 ReferenceResolution { get { return RefResolution; } }
    }
}
