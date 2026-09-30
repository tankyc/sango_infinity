/*
 * 文件名：DebateWindowBuilder.cs
 * 描述：舌战界面 window_debate.prefab 的编辑器构建/重绑工具
 *
 * 节点结构与 window_debate.prefab（美术版，与单挑的 window_duel.prefab 同源）对齐：
 *   · CardArea/CardLeft、CardRight        左右两张武将卡（头像 / 姓名 / 明细 / 飘字 / 受击特效）
 *   · CardArea/CardCenter                 中央文字：本回合话题（结算时改成胜负标题）
 *   · CardArea/win、lose                  结算大字（默认隐藏，由表现层亮一个）
 *   · left_top/person_left                挑战方武将条（头像 / 姓名 / 智力 / 性格 / hp / mp）
 *   · right_top/person_right              应战方武将条
 *   · lfetCommand/buttons/Stance1..7      挑战方手牌（Toggle，与本回合话题一致的带 ★ 与特效）
 *   · rightCommand/buttons/Stance1..7     应战方手牌
 *   · BlowCounter_bg/Dialogue1、Dialogue2 会心抉择（追击 / 留情，平时隐藏）
 *   · BlowCounter_bg/stopBtn/stopBtn      「中止」按钮：**全程不显示**（当前需求不需要它）
 *   · down/info                           底部操作提示
 *   · outBtn/img                          「退出」按钮（只在收场对白播完后才亮）
 *
 * 重要约定（照 DuelWindowBuilder 的经验）：
 *   1. prefab **已存在就只重绑节点引用**，不重建、不覆盖美术手工摆放的位置与图；
 *      要重新生成布局，先删掉 prefab 或调 Build(true)。
 *      —— 现在的 window_debate.prefab 就是美术版，正常只应该用「仅重绑节点引用」。
 *   2. 字体**借** window_duel.prefab 里已经配好的那套，避免凭空造一套不成形的资源。
 *   3. 新建的占位节点只给纯色 Image，不给精灵图，等美术替换。
 *
 * 用法：菜单 Sango/舌战/仅重绑节点引用（日常），或 Sango/舌战/生成舌战界面（重建占位）。
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
            Sango.Log.Info(Build(false));
        }

        [MenuItem("Sango/舌战/强制重建舌战界面（会覆盖手工排版）")]
        public static void RebuildFromMenu()
        {
            Sango.Log.Info(Build(true));
        }

        [MenuItem("Sango/舌战/仅重绑节点引用")]
        public static void BindOnlyFromMenu()
        {
            Sango.Log.Info(BindOnly());
        }

        /// <summary>只重绑节点引用（不动布局、不动美术）</summary>
        public static string BindOnly()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) return "找不到 prefab：" + PrefabPath;

            try
            {
                string bindMsg = BindView(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "window_debate 仅绑定完成；" + bindMsg;
            }
            catch (System.Exception e)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return "绑定失败：" + e.Message;
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

            // 借单挑界面里现成的字体（舌战与单挑同字体，避免凭空造一套不成形的资源）
            Font font = null;
            GameObject duelRoot = PrefabUtility.LoadPrefabContents(DuelPrefabPath);
            try
            {
                if (duelRoot != null)
                {
                    Text anyText = duelRoot.GetComponentInChildren<Text>(true);
                    if (anyText != null) font = anyText.font;
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
                BuildLayout(root.transform, font);
                string bindMsg = BindView(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                return "window_debate 已生成；" + bindMsg;
            }
            finally
            {
                // 注意写全 UnityEngine.Object：Sango 命名空间下另有同名 Object 类
                UnityEngine.Object.DestroyImmediate(root);
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

        /// <summary>
        /// 搭出整棵占位界面树。
        /// 节点名严格按 CardDebateView.AutoBind 的约定来（与美术版 window_debate.prefab 对齐），
        /// 这样生成出来的占位界面和美术版能共用同一套绑定逻辑。
        /// </summary>
        private static void BuildLayout(Transform root, Font font)
        {
            // 半透明遮罩
            Image mask = NewImage("mask", root, 0f, 0f, 0f, 0f, new Color(0f, 0f, 0f, 0.72f));
            Stretch(mask.rectTransform);

            // ---- CardArea：左右两张武将卡 + 中央文字 + 结算大字 ----
            GameObject cardArea = NewNode("CardArea", root, 0f, 0f, 1344f, 700f);
            BuildCard(cardArea.transform, "CardLeft", -248f, font);
            BuildCard(cardArea.transform, "CardRight", 248f, font);

            Text center = NewText("CardCenter", cardArea.transform, 0f, -20.5f, 320f, 49f, 32,
                TextAnchor.MiddleCenter, "话题：故事", Color.white, font);
            center.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 结算大字：默认隐藏，由表现层在收场时亮一个
            NewRawImage("win", cardArea.transform, 0f, 188f, 256f, 128f).gameObject.SetActive(false);
            NewRawImage("lose", cardArea.transform, 0f, 188f, 256f, 128f).gameObject.SetActive(false);

            // ---- 左右两侧武将条 ----
            BuildPersonBar(root, "left_top", "person_left", -600f, 400f, font);
            BuildPersonBar(root, "right_top", "person_right", 600f, 400f, font);

            // ---- 左右两套手牌（每套 7 格 Toggle + ToggleGroup）----
            BuildHand(root, "lfetCommand", -400f, -300f, font);
            BuildHand(root, "rightCommand", 400f, -300f, font);

            // ---- 顶部中央：合数计数器 + 会心抉择 + 中止（中止全程隐藏，只占位）----
            GameObject blow = NewNode("BlowCounter_bg", root, 0f, 520f, 440f, 116f);
            GameObject counter = NewNode("GameObject", blow.transform, 0f, 0f, 440f, 116f);
            NewText("BlowCounter_ten", counter.transform, 0f, 20f, 200f, 60f, 48, TextAnchor.MiddleCenter, "", Color.white, font);
            NewText("Blow", counter.transform, 90f, 20f, 60f, 60f, 48, TextAnchor.MiddleCenter, "", Color.white, font);
            // 话题特效：颜色随当前话题变（序号/合数用的是美术图，这里只占位）
            NewImage("eft", counter.transform, 0f, 0f, 128f, 128f, new Color(0.68f, 0.97f, 0.5f, 1f));

            // 双方本回合出牌展示（纯展示：卡面只是个 Image，按牌的类型换成高亮图）
            NewToggleNode("card1", blow.transform, -300f, -160f, 128f, 58f, "--", font, out Toggle cardA);
            NewToggleNode("card2", blow.transform, 300f, -160f, 128f, 58f, "--", font, out Toggle cardB);
            if (cardA != null) cardA.group = null;
            if (cardB != null) cardB.group = null;

            // 退出按钮（收场对白播完后才亮，点了离开舌战）
            GameObject outBg = NewNode("outBtn", root, -742f, 91f, 132f, 50f);
            Button outButton = NewButton("img", outBg.transform, 0f, 0f, 74f, 40f, "", font);
            NewText("lab", outBg.transform, 0f, 0f, 90f, 30f, 22, TextAnchor.MiddleCenter, "退出", Color.white, font);
            if (outButton != null) outButton.gameObject.SetActive(true);
            outBg.SetActive(false);

            // 「中止」：当前需求是全程不显示，这里照样搭出节点（与美术版同名）但默认收起来
            GameObject stopBg = NewNode("stopBtn", blow.transform, 0f, -160f, 132f, 50f);
            NewButton("stopBtn", stopBg.transform, 0f, 0f, 132f, 50f, "中止", font);
            stopBg.SetActive(false);

            // ---- 底部提示（down/info）----
            GameObject down = NewNode("down", root, 0f, -500f, 900f, 44f);
            NewText("info", down.transform, 0f, 0f, 900f, 44f, 24, TextAnchor.MiddleCenter, "", new Color(1f, 0.92f, 0.7f, 1f), font);

            // ---- 中央飘字（左右卡面的飘字由 BuildCard 各自生成）----
            NewFloat("float", root, font);
        }

        /// <summary>一张武将卡（CardLeft / CardRight）：脸框 + 头像 + 姓名 + 明细 + 飘字 + 受击特效</summary>
        private static void BuildCard(Transform parent, string cardName, float x, Font font)
        {
            Image frame = NewImage(cardName, parent, x, 0f, 220f, 300f, new Color(0.14f, 0.16f, 0.22f, 0.95f));

            GameObject face = NewNode("face", frame.transform, 0f, 0f, 200f, 230f);
            NewRawImage("head", face.transform, 0f, 20f, 180f, 180f);
            NewText("name", face.transform, 0f, -70f, 200f, 40f, 28, TextAnchor.MiddleCenter, "—", Color.white, font);
            NewText("detail", face.transform, 0f, -105f, 200f, 60f, 18, TextAnchor.UpperCenter, "", new Color(0.9f, 0.9f, 0.9f, 1f), font);

            NewFloat("ani_info", face.transform, font);

            GameObject hit = NewNode("hit", face.transform, 0f, 0f, 128f, 128f);
            Image hitImage = hit.AddComponent<Image>();
            hitImage.raycastTarget = false;
            hitImage.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            hit.SetActive(false);

            // 激昂特效（美术版叫 anger，就是这一块）：默认隐藏，激昂期间由表现层打开
            GameObject anger = NewNode("anger", face.transform, 0f, 0f, 128f, 128f);
            Image angerImage = anger.AddComponent<Image>();
            angerImage.raycastTarget = false;
            angerImage.color = new Color(1f, 0.55f, 0.3f, 0.5f);
            anger.SetActive(false);

            // 台词气泡（挂在武将卡上，寒暄 / 出牌 / 受击的随机台词都从这里出）
            GameObject dialogue = NewImage("Dialogue", frame.transform, 260f, 240f, 332f, 96f, new Color(1f, 1f, 1f, 0.95f)).gameObject;
            NewText("Label", dialogue.transform, 0f, 0f, 300f, 80f, 20, TextAnchor.MiddleCenter, "", Color.white, font);
            dialogue.SetActive(false);
        }

        /// <summary>一侧武将条：left_top/person_left、right_top/person_right</summary>
        private static void BuildPersonBar(Transform root, string topName, string personName, float x, float y, Font font)
        {
            GameObject top = NewNode(topName, root, x, y, 512f, 116f);
            GameObject person = NewNode(personName, top.transform, 0f, 0f, 512f, 116f);

            NewRawImage("head", person.transform, -170f, 0f, 66f, 80f);
            NewText("name", person.transform, 0f, 32f, 200f, 40f, 30, TextAnchor.MiddleLeft, "—", Color.white, font);
            NewText("Intelligence", person.transform, 0f, 0f, 200f, 30f, 20, TextAnchor.MiddleLeft, "智力 0", new Color(0.85f, 0.88f, 0.95f, 1f), font);
            NewText("Personality", person.transform, 0f, -30f, 200f, 30f, 20, TextAnchor.MiddleLeft, "—", new Color(0.85f, 0.88f, 0.95f, 1f), font);

            // 体力条（hp/fill + hp/txt）与愤怒条（mp/fill + mp/txt）
            MakeBar(person.transform, "hp", -60f, -0f, 260f, "体力", new Color(0.85f, 0.25f, 0.25f, 1f), font);
            GameObject mp = MakeBar(person.transform, "mp", -60f, -36f, 260f, "愤怒", new Color(0.95f, 0.7f, 0.2f, 1f), font);

            // 常驻气势（mp/eft_1）与满怒特效（mp/eft_3）：与美术版同名，由表现层按状态开关
            if (mp != null)
            {
                NewImage("eft_1", mp.transform, 0f, 0f, 64f, 64f, new Color(1f, 0.8f, 0.5f, 0.5f)).raycastTarget = false;
                Image full = NewImage("eft_3", mp.transform, 0f, 0f, 64f, 64f, new Color(1f, 0.4f, 0.3f, 0.6f));
                full.raycastTarget = false;
                full.gameObject.SetActive(false);
            }

            // 激昂特效（美术版叫 anger）：默认隐藏，激昂期间由表现层打开
            GameObject anger = NewNode("anger", person.transform, 130f, 0f, 64f, 64f);
            Image angerImage = anger.AddComponent<Image>();
            angerImage.raycastTarget = false;
            angerImage.color = new Color(1f, 0.5f, 0.3f, 0.6f);
            anger.SetActive(false);
        }

        /// <summary>一套手牌：命令区根 + buttons(ToggleGroup) + Stance1..7</summary>
        private static void BuildHand(Transform root, string commandName, float x, float y, Font font)
        {
            GameObject command = NewNode(commandName, root, x, y, 408f, 284f);
            GameObject buttons = NewNode("buttons", command.transform, 0f, 0f, 408f, 284f);
            ToggleGroup group = buttons.AddComponent<ToggleGroup>();
            group.allowSwitchOff = true;

            const float radius = 110f;
            for (int i = 0; i < Debate.MaxCardCount; i++)
            {
                float angle = Mathf.PI * 0.5f + Mathf.PI * i / (Debate.MaxCardCount - 1);
                float cx = Mathf.Cos(angle) * radius;
                float cy = Mathf.Sin(angle) * radius * 0.6f;

                GameObject cell = NewToggleNode("Stance" + (i + 1), buttons.transform, cx, cy, 96f, 42f, "—", font, out Toggle toggle);
                if (toggle != null) toggle.group = group;
            }
        }

        /// <summary>做一条进度条（bar/fill + bar/txt），供体力与愤怒共用</summary>
        private static GameObject MakeBar(Transform parent, string barName, float x, float y, float width, string label, Color color, Font font)
        {
            GameObject bar = NewNode(barName, parent, x, y, width, 28f);

            Image fill = NewImage("fill", bar.transform, 0f, 0f, width, 28f, color);
            Stretch(fill.rectTransform);
            // 填充统一"垂直、由下往上"（与美术版一致），运行时 SetBar 也会兜一遍
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;

            Text t = NewText("txt", bar.transform, 0f, 0f, width, 28f, 20, TextAnchor.MiddleCenter, label, Color.white, font);
            t.rectTransform.anchoredPosition = Vector2.zero;

            // 返回条节点：调用方要往里面挂气势 / 满怒特效（mp/eft_1、mp/eft_3）
            return bar;
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
            string skinMsg = AssignCardSkins(view);

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
            Count(view.floatLeft, ref bound, ref total);
            Count(view.floatRight, ref bound, ref total);
            Count(view.floatText, ref bound, ref total);
            Count(view.sceneImage, ref bound, ref total);
            Count(view.topicEft, ref bound, ref total);
            Count(view.btnOut, ref bound, ref total);
            Count(view.outRoot, ref bound, ref total);
            Count(view.handRootLeft, ref bound, ref total);
            Count(view.handRootRight, ref bound, ref total);
            Count(view.dialogueFrameNormal, ref bound, ref total);
            Count(view.dialogueFrameExcited, ref bound, ref total);
            CountDialogue(view.dialogueLeft, ref bound, ref total);
            CountDialogue(view.dialogueRight, ref bound, ref total);
            Count(view.resultWin, ref bound, ref total);
            Count(view.resultLose, ref bound, ref total);
            Count(view.btnClose, ref bound, ref total);
            Count(view.stopRoot, ref bound, ref total);
            CountSkins(view.cardSkins, ref bound, ref total);
            CountCard(view.cardLeft, ref bound, ref total);
            CountCard(view.cardRight, ref bound, ref total);
            CountPlayedCard(view.playedCardLeft, ref bound, ref total);
            CountPlayedCard(view.playedCardRight, ref bound, ref total);
            CountSide(view.leftSide, ref bound, ref total);
            CountSide(view.rightSide, ref bound, ref total);
            CountHand(view.leftHand, ref bound, ref total);
            CountHand(view.rightHand, ref bound, ref total);

            return "绑定 CardDebateView " + bound + "/" + total + " 项"
                + (created ? "（新建组件）" : "")
                + (string.IsNullOrEmpty(skinMsg) ? "" : "；" + skinMsg);
        }

        /// <summary>统计一张武将卡的绑定情况</summary>
        private static void CountCard(CardDebateView.DebateCardFace card, ref int bound, ref int total)
        {
            if (card == null) return;
            Count(card.root, ref bound, ref total);
            Count(card.head, ref bound, ref total);
            Count(card.nameText, ref bound, ref total);
            Count(card.detailText, ref bound, ref total);
            Count(card.hitFx, ref bound, ref total);
            Count(card.angerFx, ref bound, ref total);
            Count(card.aniInfo, ref bound, ref total);
        }

        /// <summary>统计一侧武将条的绑定情况</summary>
        private static void CountSide(CardDebateView.DebateSide side, ref int bound, ref int total)
        {
            if (side == null) return;
            Count(side.head, ref bound, ref total);
            Count(side.nameText, ref bound, ref total);
            Count(side.intelText, ref bound, ref total);
            Count(side.personalityText, ref bound, ref total);
            Count(side.hpBar, ref bound, ref total);
            Count(side.hpText, ref bound, ref total);
            Count(side.stressBar, ref bound, ref total);
            Count(side.stressText, ref bound, ref total);
            Count(side.standFx, ref bound, ref total);
            Count(side.angerFx, ref bound, ref total);
            Count(side.fullStressFx, ref bound, ref total);
            Count(side.playedCard, ref bound, ref total);
        }

        /// <summary>统计一格台词气泡的绑定情况</summary>
        private static void CountDialogue(CardDebateView.DebateDialogue dlg, ref int bound, ref int total)
        {
            if (dlg == null) return;
            Count(dlg.root, ref bound, ref total);
            Count(dlg.frame, ref bound, ref total);
            Count(dlg.label, ref bound, ref total);
        }

        /// <summary>统计一套手牌的绑定情况</summary>
        private static void CountHand(CardDebateView.DebateHand hand, ref int bound, ref int total)
        {
            if (hand == null || hand.cells == null) return;
            Count(hand.group, ref bound, ref total);

            for (int i = 0; i < hand.cells.Length; i++)
            {
                CardDebateView.DebateHandCell cell = hand.cells[i];
                if (cell == null) continue;
                Count(cell.frame, ref bound, ref total);
                Count(cell.toggle, ref bound, ref total);
                Count(cell.label, ref bound, ref total);
                Count(cell.fx, ref bound, ref total);
                Count(cell.sel, ref bound, ref total);
            }
        }

        /// <summary>
        /// 卡牌套图的精灵文件名，顺序与 <see cref="CardDebateView.cardSkins"/> 的下标一致：
        /// 故事 / 道理 / 时节 / 特殊，每类 4 张：普通 / 按下 / 高亮 / 不可选。
        /// </summary>
        private static readonly string[] SkinSpriteNames =
        {
            "4848-2_12", "4848-2_11", "4848-2_10", "4848-2_4",    // 故事
            "4848-2_16", "4848-2_15", "4848-2_14", "4848-2_3",    // 道理
            "4848-2_8",  "4848-2_7",  "4848-2_6",  "4848-2_2",    // 时节
            "4848-2_23", "4848-2_22", "4848-2_21", "4848-2_20",   // 特殊
        };

        /// <summary>卡牌套图所在目录（美术切好的单张 PNG）</summary>
        private const string SkinSpriteFolder = "Assets/Mods/Content/Assets/UI/AtlasTexture/4848-2";

        /// <summary>把美术给的卡牌套图填进表现层（缺图只报数，不报错）</summary>
        private static string AssignCardSkins(CardDebateView view)
        {
            if (view == null) return "卡牌套图：表现层为空";

            int count = SkinSpriteNames.Length / 4;
            if (view.cardSkins == null || view.cardSkins.Length != count)
                view.cardSkins = new CardDebateView.DebateCardSkin[count];
            for (int i = 0; i < view.cardSkins.Length; i++)
                if (view.cardSkins[i] == null) view.cardSkins[i] = new CardDebateView.DebateCardSkin();

            int miss = 0;
            for (int i = 0; i < view.cardSkins.Length; i++)
            {
                int b = i * 4;
                CardDebateView.DebateCardSkin skin = view.cardSkins[i];
                skin.normal = LoadSkinSprite(SkinSpriteNames[b + 0], ref miss);
                skin.pressed = LoadSkinSprite(SkinSpriteNames[b + 1], ref miss);
                skin.highlighted = LoadSkinSprite(SkinSpriteNames[b + 2], ref miss);
                skin.disabled = LoadSkinSprite(SkinSpriteNames[b + 3], ref miss);
            }

            // 台词底板：普通 4848-2_1 / 激动 4848-2_0
            if (view.dialogueFrameNormal == null)
                view.dialogueFrameNormal = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(SkinSpriteFolder + "/4848-2_1.png");
            if (view.dialogueFrameExcited == null)
                view.dialogueFrameExcited = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(SkinSpriteFolder + "/4848-2_0.png");

            return "卡牌套图 " + (SkinSpriteNames.Length - miss) + "/" + SkinSpriteNames.Length
                + (miss > 0 ? "（缺 " + miss + " 张）" : "");
        }

        /// <summary>按文件名取套图精灵（切好的单张 PNG，用文件名当资产名）。注意写全 UnityEngine.Sprite</summary>
        private static UnityEngine.Sprite LoadSkinSprite(string name, ref int miss)
        {
            UnityEngine.Sprite sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(SkinSpriteFolder + "/" + name + ".png");
            if (sprite == null) miss++;
            return sprite;
        }

        /// <summary>统计卡牌套图的绑定情况</summary>
        private static void CountSkins(CardDebateView.DebateCardSkin[] skins, ref int bound, ref int total)
        {
            if (skins == null) return;
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] == null) continue;
                Count(skins[i].normal, ref bound, ref total);
                Count(skins[i].pressed, ref bound, ref total);
                Count(skins[i].highlighted, ref bound, ref total);
                Count(skins[i].disabled, ref bound, ref total);
            }
        }

        /// <summary>统计一格出牌展示的绑定情况</summary>
        private static void CountPlayedCard(CardDebateView.DebatePlayedCard show, ref int bound, ref int total)
        {
            if (show == null) return;
            Count(show.root, ref bound, ref total);
            Count(show.frame, ref bound, ref total);
            Count(show.label, ref bound, ref total);
            Count(show.fx, ref bound, ref total);
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

        /// <summary>建一个带 RawImage 的节点（头像 / 结算大字用）</summary>
        private static RawImage NewRawImage(string name, Transform parent, float x, float y, float w, float h)
        {
            GameObject go = NewNode(name, parent, x, y, w, h);
            RawImage raw = go.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        /// <summary>
        /// 建一个手牌/抉择格：根上是 Image + Toggle，子节点是 sel（选中图）、Label（文字）、eft（特效，默认关）。
        /// </summary>
        private static GameObject NewToggleNode(string name, Transform parent, float x, float y, float w, float h,
            string label, Font font, out Toggle toggle)
        {
            Image image = NewImage(name, parent, x, y, w, h, new Color(0.18f, 0.2f, 0.28f, 0.95f));
            image.raycastTarget = true;

            Image selImage = NewImage("sel", image.transform, 0f, 0f, 0f, 0f, new Color(1f, 1f, 1f, 0.16f));
            selImage.raycastTarget = false;
            Stretch(selImage.rectTransform);

            NewText("Label", image.transform, 0f, 0f, w, h, 22, TextAnchor.MiddleCenter, label, Color.white, font);

            Image eftImage = NewImage("eft", image.transform, 0f, 0f, 128f, 48f, new Color(1f, 0.85f, 0.4f, 0.35f));
            eftImage.raycastTarget = false;
            eftImage.gameObject.SetActive(false);

            toggle = image.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = image;
            toggle.graphic = selImage;

            return image.gameObject;
        }

        /// <summary>建一个飘字载体（AnimationText + label 子节点）</summary>
        private static AnimationText NewFloat(string name, Transform parent, Font font)
        {
            GameObject go = NewNode(name, parent, 0f, 0f, 260f, 130f);
            Text label = NewText("label", go.transform, 0f, 0f, 140f, 100f, 26, TextAnchor.MiddleCenter, "", Color.white, font);
            label.rectTransform.anchoredPosition = Vector2.zero;

            AnimationText ani = go.AddComponent<AnimationText>();
            ani.label = label;
            return ani;
        }

        /// <summary>铺满父节点</summary>
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
