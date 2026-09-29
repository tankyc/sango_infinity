/*
 * 文件名：CardDebateView.cs
 * 描述：舌战 2D 表现层（卡牌式，window_debate.prefab）
 *
 * 定位（与单挑的 CardDuelView 同构）：
 *   · 它是 IDebateView 的一个实现，只负责把逻辑层抛出的"表现请求"翻译成界面操作，
 *     不认识任何舌战逻辑；将来要换 3D 表现，只需再写一个 IDebateView 实现，
 *     并在 DebateIntegration 里换掉 CreateViewHandler 即可。
 *   · 逻辑层的节拍由本类的动画计时器回告：DebateIsAnimating / DebateIsMessageBoxVisible，
 *     逻辑层会停在当前步骤等（Debate.IsIdle）。
 *   · 节点全部按名字查找（AutoBind），找不到的项一律跳过、不报错，
 *     所以美术挪位置、改容器都不影响，只是少一块显示。
 *
 * 节点命名约定（window_debate.prefab，与单挑的 window_duel.prefab 同源，只多一套手牌）：
 *   window_debate
 *   ├── scene                                    背景场景（RawImage，可留空）
 *   ├── CardArea
 *   │   ├── CardLeft  / CardRight                左右两张武将卡
 *   │   │   ├── face/ head(头像) name(姓名) detail(明细) ani_info(飘字) hit(受击特效)
 *   │   │   └── Dialogue                          该方的台词气泡（美术资源，本类不驱动）
 *   │   ├── CardCenter                           中央文字（原为 "VS"，现用于显示话题 / 胜负）
 *   │   └── win / lose                           结算大字（默认隐藏，结算时亮一个）
 *   ├── LogBg/log                                战报
 *   ├── BlowCounter_bg                           顶部中央：合数计数器 + 双方出牌 + 中止
 *   │   ├── GameObject/BlowCounter_ten, Blow      合数（美术资源，逻辑层未使用）
 *   │   ├── card1 / card2                         双方本回合出的牌（只是个 Image：按牌的类型换高亮图）
 *   │   └── stopBtn/stopBtn                       中止 / 关闭结算（Button）
 *   ├── down/info                                底部操作提示
 *   ├── lfetCommand/buttons/Stance1..7            挑战方（左）手牌
 *   ├── rightCommand/buttons/Stance1..7           应战方（右）手牌
 *   ├── left_top/person_left                      挑战方武将条
 *   └── right_top/person_right                    应战方武将条
 *
 * 两侧对应关系：左＝挑战方(队伍 0)，右＝应战方(队伍 1)，哪一侧归玩家点由 Character.control 决定。
 *
 * 手牌是 Toggle（不是 Button）：它是"这一合要出的牌"的选中表达。但选中态不靠 Toggle 的 sel 覆盖图，
 * 而是按牌的类型换底图（见 ApplyCardSkin 与 cardSkins），所以运行时会把每格的 sel 关掉、
 * 并把 ToggleGroup.allowSwitchOff 打开（否则程序化清空选中会被组规则顶回来）。
 *
 * 会心（追击 / 留情）按名字找 Dialogue1 / Dialogue2（老版）或 BtnPushOn / BtnMercy（生成版），
 * 两个都没找到时按 criticalFallbackHaveMercy 兜底，避免逻辑层卡在会心阶段。
 */

using Sango.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core.Debate
{
    /// <summary>舌战 2D 表现层（卡牌式）</summary>
    public class CardDebateView : UGUIWindow, IDebateView
    {
        #region 节点引用（由 AutoBind 按名字查找；编辑器构建器会把结果写回 prefab）

        /// <summary>
        /// 卡牌套图类型。下标刻意与 <see cref="Sango.Core.Debate.Topic"/> 对齐：
        ///   0 故事 / 1 道理 / 2 时势（美术叫"时节"）/ 3 特殊（再考、大喝、诡辩、无视、镇静、激昂）。
        /// 这样「牌的套图下标」就是 GetCardTopic(牌)，话题牌不用额外映射，只有话术牌落到 3。
        /// </summary>
        public enum DebateCardSkinType
        {
            Story = 0,   // 故事
            Logic = 1,   // 道理
            Trend = 2,   // 时势（时节）
            Special = 3, // 特殊
        }

        /// <summary>一张卡的显示态（美术给的四种图）</summary>
        public enum DebateCardState
        {
            Normal = 0,      // 普通
            Pressed = 1,     // 按下
            Highlighted = 2, // 高亮（= 玩家点中的那张）
            Disabled = 3,    // 不可选择
        }

        /// <summary>一种卡牌类型的四态图（普通 / 按下 / 高亮 / 不可选）</summary>
        [System.Serializable]
        public class DebateCardSkin
        {
            /// <summary>普通</summary>
            public UnityEngine.Sprite normal;
            /// <summary>按下</summary>
            public UnityEngine.Sprite pressed;
            /// <summary>高亮（选中）</summary>
            public UnityEngine.Sprite highlighted;
            /// <summary>不可选择</summary>
            public UnityEngine.Sprite disabled;

            /// <summary>按显示态取图（注意写全 UnityEngine.Sprite：Sango.Sprite 是框架里的精灵管理命名空间）</summary>
            public UnityEngine.Sprite Get(DebateCardState state)
            {
                switch (state)
                {
                    case DebateCardState.Pressed: return pressed;
                    case DebateCardState.Highlighted: return highlighted;
                    case DebateCardState.Disabled: return disabled;
                    default: return normal;
                }
            }
        }

        /// <summary>
        /// 卡牌套图：下标 = <see cref="DebateCardSkinType"/>。
        /// 美术给的对应关系（类型 普通 / 按下 / 高亮 / 不可选）：
        ///   故事 4848-2_12 / 4848-2_11 / 4848-2_10 / 4848-2_4
        ///   时节 4848-2_8  / 4848-2_7  / 4848-2_6  / 4848-2_2
        ///   道理 4848-2_16 / 4848-2_15 / 4848-2_14 / 4848-2_3
        ///   特殊 4848-2_23 / 4848-2_22 / 4848-2_21 / 4848-2_20
        /// </summary>
        [Header("卡牌套图（故事 / 道理 / 时节 / 特殊）")]
        public DebateCardSkin[] cardSkins = NewCardSkins();

        /// <summary>话题文字（CardArea/CardCenter）；结算时同一位改成显示胜负标题</summary>
        [Header("话题 / 提示")]
        public Text topicText;
        /// <summary>话题提示（prefab 未单独留位时为 null，自动跳过）</summary>
        public Text topicHint;
        /// <summary>底部操作提示（down/info）</summary>
        public Text hintText;

        /// <summary>战报（LogBg/log）</summary>
        [Header("战报与飘字")]
        public Text logText;
        /// <summary>左方卡面飘字（CardLeft/face/ani_info）</summary>
        public AnimationText floatLeft;
        /// <summary>右方卡面飘字（CardRight/face/ani_info）</summary>
        public AnimationText floatRight;
        /// <summary>中央飘字（可选；prefab 里有 float 节点时才会绑到）</summary>
        public AnimationText floatText;

        [Header("左右两张武将卡（CardArea/CardLeft、CardArea/CardRight）")]
        public DebateCardFace cardLeft = new DebateCardFace();
        public DebateCardFace cardRight = new DebateCardFace();

        /// <summary>
        /// 卡面头像取图时传给 <see cref="GameRenderHelper.LoadHeadIcon(int, int)"/> 的第二个参数
        /// （与单挑一致走 1 号图）；设为 0 则与武将条用同一张默认图。
        /// </summary>
        public int cardHeadIconType = 1;

        [Header("左右两侧武将条（left_top/person_left、right_top/person_right）")]
        public DebateSide leftSide = new DebateSide();
        public DebateSide rightSide = new DebateSide();

        [Header("左右两套手牌（lfetCommand|rightCommand/buttons/Stance1..7）")]
        public DebateHand leftHand = new DebateHand();
        public DebateHand rightHand = new DebateHand();

        /// <summary>左方本回合出的牌（BlowCounter_bg/card1，只有图与牌名）</summary>
        [Header("双方出牌展示（BlowCounter_bg/card1、card2）")]
        public DebatePlayedCard playedCardLeft = new DebatePlayedCard();
        /// <summary>右方本回合出的牌（BlowCounter_bg/card2）</summary>
        public DebatePlayedCard playedCardRight = new DebatePlayedCard();

        /// <summary>追击（BlowCounter_bg/Dialogue1；美术若改了位置/名字，按名字找不到就留空）</summary>
        [Header("会心抉择（追击 / 留情）")]
        public Toggle togglePushOn;
        /// <summary>留情（Dialogue2）</summary>
        public Toggle toggleMercy;
        /// <summary>追击按钮上的文字</summary>
        public Text togglePushOnLabel;
        /// <summary>留情按钮上的文字</summary>
        public Text toggleMercyLabel;

        /// <summary>
        /// 追击/留情两个按钮一个都没接上时的兜底：
        ///   true  = 直接按「留情」结算（逻辑层照常往下走，不会卡在会心阶段）；
        ///   false = 一直停在会心阶段等玩家（UI 没接好时会卡死，只用于调试）。
        /// </summary>
        public bool criticalFallbackHaveMercy = true;

        /// <summary>我方胜利大字（CardArea/win，默认隐藏）</summary>
        [Header("结算（CardArea/win、CardArea/lose、BlowCounter_bg/stopBtn/stopBtn）")]
        public RectTransform resultWin;
        /// <summary>我方失败大字（CardArea/lose，默认隐藏）</summary>
        public RectTransform resultLose;
        /// <summary>关闭结算画面的按钮（stopBtn）</summary>
        public Button btnClose;

        /// <summary>背景场景贴图节点（scene，可留空）</summary>
        [Header("场景背景（scene，可留空）")]
        public RawImage sceneImage;

        #endregion

        #region 演出参数（美术/策划可在 Inspector 调）

        /// <summary>开场演出时长（秒）</summary>
        public float openingDuration = 0.6f;
        /// <summary>出牌演出时长（秒）</summary>
        public float playCardDuration = 0.35f;
        /// <summary>一次伤害/效果飘字的演出时长（秒）</summary>
        public float effectDuration = 0.5f;
        /// <summary>结算画面弹出后的停留时长（秒）；玩家点关闭可提前结束</summary>
        public float closingDuration = 0.8f;
        /// <summary>战报最多保留多少行</summary>
        public int maxLogLines = 8;
        /// <summary>飘字缩放（模板字号由 prefab 上的 AnimationText 决定）</summary>
        public float floatScale = 0.7f;
        /// <summary>受击序列帧（face/hit）显示多久后自动隐藏（秒）</summary>
        public float hitFxDuration = 0.3f;
        /// <summary>
        /// 点牌到真正出牌之间的停顿（秒）。
        /// 逻辑层是"每帧问一次选哪张牌"，点完立刻交出去的话"选中态"只存在一帧、玩家根本看不见，
        /// 所以先亮 cardConfirmDuration 秒的选中图再交出下标；设 0 = 点了立刻出牌。
        /// </summary>
        public float cardConfirmDuration = 0.25f;

        /// <summary>话题一致的手牌高亮色（★ 用的金色）</summary>
        public static readonly Color TopicCardTint = new Color(1f, 0.85f, 0.4f, 1f);
        /// <summary>不可用（再考用尽）手牌的灰字</summary>
        public static readonly Color DisabledCardTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        #endregion

        #region 运行时状态

        /// <summary>当前绑定的舌战</summary>
        protected Debate m_Debate;

        /// <summary>动画节拍：> 0 时 DebateIsAnimating 返回 true，逻辑层停在当前步骤</summary>
        protected float m_AnimTimer;

        /// <summary>结算画面是否还开着（开着就一直阻塞逻辑层收场）</summary>
        protected bool m_ResultOpen;

        /// <summary>消息框计数（> 0 时 DebateIsMessageBoxVisible 为 true）</summary>
        protected int m_LineCount;

        /// <summary>玩家点了第几张手牌（-1 = 还没点）</summary>
        protected int m_PendingCard = -1;
        /// <summary>上面那个选择属于哪一方</summary>
        protected int m_PendingCardTeam = -1;
        /// <summary>玩家选的会心（-1 = 还没选）</summary>
        protected int m_PendingCritical = -1;

        /// <summary>会心兜底只提示一次，别每帧刷日志</summary>
        protected bool m_CriticalFallbackWarned;

        /// <summary>点了牌之后"选中态"还要亮多久（> 0 表示先让玩家看见再出牌）</summary>
        protected float m_CardConfirmTimer;

        /// <summary>程序化改动 Toggle 选中态期间置位，用来屏蔽 onValueChanged，避免"刷新"被当成"玩家出牌"</summary>
        protected bool m_SuppressToggle;

        /// <summary>战报行</summary>
        protected readonly List<string> m_LogLines = new List<string>();

        /// <summary>已经做过运行时准备的飘字组件（maxTime / 图标子节点）</summary>
        protected readonly List<AnimationText> m_PreparedFloats = new List<AnimationText>();

        /// <summary>文字的原色（第一次改动前记下来，恢复时用）</summary>
        protected readonly Dictionary<Text, Color> m_TextDefaultColors = new Dictionary<Text, Color>();

        /// <summary>两侧卡面受击特效还要亮多久（下标为队伍，> 0 表示正在亮）</summary>
        protected readonly float[] m_HitFxTime = new float[Debate.MaxTeamCount];

        /// <summary>
        /// 某方是否正在激昂（下标为队伍）。
        ///
        /// 为什么不直接看 Character.angerTimer：逻辑层是**先**回调 DebateAngerTrigger、
        /// **后**才在 Anger() 里做 CharacterSetAngerTimer 的（见 Debate.AngerTrigger / Anger），
        /// 若表现层在触发器里点亮标记后立刻按 angerTimer 反推一遍，刚亮的标记会被自己刷灭。
        /// 所以这里跟逻辑层一样用"显式开关"，由 DebateAngerTrigger / DebateAngerEnd 成对维护。
        /// </summary>
        protected readonly bool[] m_AngerOn = new bool[Debate.MaxTeamCount];

        #endregion

        #region 生命周期

        /// <summary>唤醒：按名字绑定全部节点，并挂上按钮回调</summary>
        protected override void Awake()
        {
            base.Awake();
            AutoBind();
            BindButtons();
        }

        /// <summary>窗口打开：按当前舌战刷新一遍</summary>
        public override void OnOpen()
        {
            base.OnOpen();
            RefreshAll();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_Debate = null;
            m_PreparedFloats.Clear();
            m_TextDefaultColors.Clear();
        }

        /// <summary>推进动画计时器与受击序列帧的计时</summary>
        protected virtual void Update()
        {
            if (m_AnimTimer > 0f)
                m_AnimTimer -= Time.deltaTime;

            if (m_CardConfirmTimer > 0f)
                m_CardConfirmTimer -= Time.deltaTime;

            for (int i = 0; i < m_HitFxTime.Length; i++)
            {
                if (m_HitFxTime[i] <= 0f) continue;

                m_HitFxTime[i] -= Time.deltaTime;
                if (m_HitFxTime[i] <= 0f)
                {
                    m_HitFxTime[i] = 0f;
                    DebateCardFace card = CardOf(i);
                    if (card != null && card.hitFx != null) card.hitFx.SetActive(false);
                }
            }
        }

        #endregion

        #region 嵌套类型：卡面 / 武将条 / 手牌

        /// <summary>
        /// 一张武将卡（CardArea/CardLeft、CardArea/CardRight）。
        /// 与单挑同结构：脸框 + 头像 + 姓名 + 明细 + 飘字 + 受击特效。
        /// </summary>
        [System.Serializable]
        public class DebateCardFace
        {
            /// <summary>所属队伍（0=挑战方 / 1=应战方）</summary>
            public int team;
            /// <summary>卡牌根节点（CardLeft / CardRight）</summary>
            public RectTransform root;
            /// <summary>卡面图（就是根节点上的 Image）</summary>
            public Image frame;
            /// <summary>卡面头像（face/head）</summary>
            public RawImage head;
            /// <summary>姓名（face/name）</summary>
            public Text nameText;
            /// <summary>明细（face/detail）：智力 / 体力 / 愤怒</summary>
            public Text detailText;
            /// <summary>受击序列帧（face/hit），激活即开始播</summary>
            public GameObject hitFx;
            /// <summary>卡面飘字（face/ani_info）</summary>
            public AnimationText aniInfo;

            /// <summary>绑定一张卡。节点名找不到时全部留空，后续刷新自动跳过，不会报错。</summary>
            public void Bind(Transform cardRoot)
            {
                root = cardRoot as RectTransform;
                frame = root != null ? root.GetComponent<Image>() : null;

                Transform face = root != null ? FindDeep(root, "face") : null;
                Transform scope = face != null ? face : root;

                head = FindComponent<RawImage>(FindDeep(scope, "head"));
                nameText = FindComponent<Text>(FindDeep(scope, "name"));
                detailText = FindComponent<Text>(FindDeep(scope, "detail"));

                Transform hit = FindDeep(scope, "hit");
                hitFx = hit != null ? hit.gameObject : null;
                // 受击序列帧在 prefab 里是**开着**的（UIImageAnimation 靠 OnEnable 起播），
                // 所以绑定就要收起来，之后只在挨打的那一刻亮一下。
                if (hitFx != null) hitFx.SetActive(false);

                aniInfo = FindComponent<AnimationText>(FindDeep(scope, "ani_info"));
            }
        }

        /// <summary>
        /// 一侧武将条（left_top/person_left、right_top/person_right）。
        /// 体力条走 hp/fill、愤怒条走 mp/fill，数值走同名 txt。
        /// </summary>
        [System.Serializable]
        public class DebateSide
        {
            /// <summary>所属队伍（0=挑战方 / 1=应战方）</summary>
            public int team;
            /// <summary>武将条根节点</summary>
            public RectTransform root;
            /// <summary>头像（head）</summary>
            public RawImage head;
            /// <summary>姓名（name）</summary>
            public Text nameText;
            /// <summary>智力（Intelligence）</summary>
            public Text intelText;
            /// <summary>性格（Personality）</summary>
            public Text personalityText;
            /// <summary>体力条填充（hp/fill）</summary>
            public Image hpBar;
            /// <summary>体力数值（hp/txt）</summary>
            public Text hpText;
            /// <summary>愤怒条填充（mp/fill）</summary>
            public Image stressBar;
            /// <summary>愤怒数值（mp/txt）</summary>
            public Text stressText;
            /// <summary>激昂标记（mp/eft_1、mp/eft_3 之类的特效节点）</summary>
            public GameObject angerTag;
            /// <summary>本回合出牌（played，prefab 未留位时为 null）</summary>
            public Text playedCard;
        }

        /// <summary>
        /// 一套手牌（lfetCommand|rightCommand 下的 buttons + Stance1..7）。
        /// 每格是 Toggle：Label 写字、eft 是"与本回合话题一致"的特效。
        /// </summary>
        [System.Serializable]
        public class DebateHand
        {
            /// <summary>所属队伍（0=挑战方 / 1=应战方）</summary>
            public int team;
            /// <summary>命令区根节点（lfetCommand / rightCommand）</summary>
            public GameObject root;
            /// <summary>手牌容器上的 ToggleGroup（buttons）</summary>
            public ToggleGroup group;
            /// <summary>手牌格（Stance1..Stance7）</summary>
            public DebateHandCell[] cells = NewCells();

            /// <summary>按名字绑定一套手牌；找不到的格留空</summary>
            public void Bind(Transform parent, string[] commandNames, string cellPrefix, int firstIndex)
            {
                root = null;
                group = null;
                cells = NewCells();

                if (parent == null) return;

                Transform command = FindDeepAny(parent, commandNames);
                if (command == null) return;
                root = command.gameObject;

                Transform container = FindDeep(command, "buttons");
                if (container == null) container = command;
                group = container.GetComponent<ToggleGroup>();

                for (int i = 0; i < Debate.MaxCardCount; i++)
                {
                    Transform cellRoot = FindDeep(container, cellPrefix + (i + firstIndex));
                    if (cellRoot == null) continue;

                    cells[i].Bind(cellRoot);
                }
            }
            }

            /// <summary>一格手牌：底图 + Toggle + 牌名 + 话题一致特效（+ 美术的选中覆盖图）</summary>
            [System.Serializable]
            public class DebateHandCell
            {
            /// <summary>格子根节点（Stance1..7）</summary>
            public RectTransform root;
            /// <summary>底图（就是根节点上的 Image，四态图换的就是它）</summary>
            public Image frame;
            /// <summary>点选用的 Toggle</summary>
            public Toggle toggle;
            /// <summary>牌名（Label）</summary>
            public Text label;
            /// <summary>与本回合话题一致的特效（eft）</summary>
            public GameObject fx;
            /// <summary>美术的选中覆盖图（sel）</summary>
            public GameObject sel;

            /// <summary>绑定一格；找不到的项留空</summary>
            public void Bind(Transform cellRoot)
            {
                root = cellRoot as RectTransform;
                if (root == null) return;

                frame = root.GetComponent<Image>();
                toggle = root.GetComponent<Toggle>();
                label = FindComponent<Text>(FindDeep(root, "Label")) ?? FindComponent<Text>(FindDeep(root, "lab"));

                Transform eft = FindDeep(root, "eft");
                fx = eft != null ? eft.gameObject : null;

                Transform selNode = FindDeep(root, "sel");
                sel = selNode != null ? selNode.gameObject : null;

                // 美术在每格上放了一张 sel 覆盖图（原本当 Toggle 的选中图用），
                // 但它的图不随卡牌类型变，选中时会把"该类型的高亮图"盖掉，
                // 所以停用它：选中态统一由底图换成高亮图来表达（见 CardDebateView.ApplyCardSkin）。
                if (sel != null) sel.SetActive(false);
                if (toggle != null) toggle.graphic = null;

                if (fx != null) fx.SetActive(false);
            }
            }

        /// <summary>
        /// 一方本回合出的牌（BlowCounter_bg/card1、card2）。
        /// 美术说这格"只是个 Image"，所以这里不碰 Toggle 的选中态，只按牌的类型把底图换成高亮图。
        /// </summary>
        [System.Serializable]
        public class DebatePlayedCard
        {
            /// <summary>所属队伍（0=挑战方 / 1=应战方）</summary>
            public int team;
            /// <summary>根节点（card1 / card2）</summary>
            public RectTransform root;
            /// <summary>底图（就是根节点上的 Image）</summary>
            public Image frame;
            /// <summary>牌名（Label）</summary>
            public Text label;
            /// <summary>与本回合话题一致的特效（eft）</summary>
            public GameObject fx;
            /// <summary>没出牌时的占位字</summary>
            public string emptyText = "—";

            /// <summary>绑定一格；找不到的项留空</summary>
            public void Bind(Transform cardRoot)
            {
                root = cardRoot as RectTransform;
                if (root == null) return;

                frame = root.GetComponent<Image>();
                label = FindComponent<Text>(FindDeep(root, "Label")) ?? FindComponent<Text>(FindDeep(root, "lab"));

                Transform eft = FindDeep(root, "eft");
                fx = eft != null ? eft.gameObject : null;

                // 纯展示格：
                //   1) 底图不吃点击，免得误点把 Toggle 的选中态点乱；
                //   2) 美术版里 card1/card2 的 Toggle 被挂到了右手命令区的 ToggleGroup 上
                //      （跟手牌共用一个组，会互相抢选中），这里直接摘出来。
                if (frame != null) frame.raycastTarget = false;
                DetachFromGroup(root.GetComponent<Toggle>());

                if (label != null) label.text = emptyText;
                if (fx != null) fx.SetActive(false);
            }
        }

        #endregion

        #region 绑定 / 自动查找

        /// <summary>把表现层与某场舌战绑定</summary>
        public virtual void Bind(Debate debate)
        {
            m_Debate = debate;
            m_AnimTimer = 0f;
            m_ResultOpen = false;
            m_LineCount = 0;
            m_PendingCard = -1;
            m_PendingCardTeam = -1;
            m_PendingCritical = -1;
            m_SuppressToggle = false;
            m_LogLines.Clear();

            ApplyStaticTexts();
            ShowCritical(false);
            ShowResultStamp(0);
            if (logText != null) logText.text = string.Empty;
            ClearHandSelection();
            ClearHitFx();
            ClearAngerMarks();

            RefreshAll();
        }

        /// <summary>
        /// 摆好界面上那几处固定文案（美术版留的是占位字，例如会心抉择的「请多多指教」）。
        /// AutoBind 之后调一次；Bind 时再调一次，保证无论有没有走过 Awake，进舌战前文案都是对的。
        /// </summary>
        protected void ApplyStaticTexts()
        {
            if (togglePushOnLabel != null) togglePushOnLabel.text = "追击";
            if (toggleMercyLabel != null) toggleMercyLabel.text = "留情";
        }

        /// <summary>按名字重新绑定全部节点（编辑器构建器会调用它把引用固化进 prefab）</summary>
        public void Rebind()
        {
            AutoBind();
        }

        /// <summary>
        /// 按名字自动查找节点。查找刻意做得宽松：美术挪位置、改容器都不影响，
        /// 找不到的项留空，运行时自动跳过。
        /// </summary>
        protected virtual void AutoBind()
        {
            Transform root = transform;

            // ---- 话题 / 提示 ----
            topicText = FindAny<Text>(root, "CardCenter", "topic");
            topicHint = FindAny<Text>(root, "topicHint");
            hintText = FindAny<Text>(root, "info", "hint");

            // ---- 战报 / 飘字 ----
            logText = FindAny<Text>(root, "LogBg/log", "log");
            floatLeft = FindComponent<AnimationText>(FindDeep(root, "CardLeft/face/ani_info"));
            floatRight = FindComponent<AnimationText>(FindDeep(root, "CardRight/face/ani_info"));
            floatText = FindComponent<AnimationText>(FindDeep(root, "float"));
            m_PreparedFloats.Clear();

            // ---- 左右两张武将卡 ----
            cardLeft.team = (int)DebateTeam.DebateTeam_Challenger;
            cardRight.team = (int)DebateTeam.DebateTeam_Challenged;
            cardLeft.Bind(FindDeep(root, "CardLeft"));
            cardRight.Bind(FindDeep(root, "CardRight"));

            // ---- 左右两侧武将条 ----
            leftSide.team = (int)DebateTeam.DebateTeam_Challenger;
            rightSide.team = (int)DebateTeam.DebateTeam_Challenged;
            BindSide(root, leftSide, new string[] { "left_top/person_left", "person_left", "left" });
            BindSide(root, rightSide, new string[] { "right_top/person_right", "person_right", "right" });

            // ---- 左右两套手牌 ----
            leftHand.team = (int)DebateTeam.DebateTeam_Challenger;
            rightHand.team = (int)DebateTeam.DebateTeam_Challenged;
            leftHand.Bind(root, new string[] { "lfetCommand", "leftCommand" }, "Stance", 1);
            rightHand.Bind(root, new string[] { "rightCommand" }, "Stance", 1);

            // 手牌选中态由本类接管：打开 allowSwitchOff，否则"清空选中"会被 ToggleGroup 顶回来。
            if (leftHand.group != null) leftHand.group.allowSwitchOff = true;
            if (rightHand.group != null) rightHand.group.allowSwitchOff = true;

            // ---- 双方本回合出牌展示 ----
            playedCardLeft.team = (int)DebateTeam.DebateTeam_Challenger;
            playedCardRight.team = (int)DebateTeam.DebateTeam_Challenged;
            playedCardLeft.Bind(FindDeep(root, "card1"));
            playedCardRight.Bind(FindDeep(root, "card2"));

            // ---- 会心抉择 ----
            togglePushOn = FindAny<Toggle>(root, "Dialogue1", "BtnPushOn");
            toggleMercy = FindAny<Toggle>(root, "Dialogue2", "BtnMercy");
            togglePushOnLabel = FindCellLabel(togglePushOn);
            toggleMercyLabel = FindCellLabel(toggleMercy);
            // 追击/留情 与右手命令区共用一个 ToggleGroup（会互相抢选中），摘出来自己管，
            // 选中态由 SetCriticalChoice 手动保持二选一。
            DetachFromGroup(togglePushOn);
            DetachFromGroup(toggleMercy);
            ApplyStaticTexts();

            // ---- 结算 ----
            resultWin = FindDeep(root, "win") as RectTransform;
            resultLose = FindDeep(root, "lose") as RectTransform;
            btnClose = FindComponent<Button>(FindDeep(root, "stopBtn/stopBtn")) ?? FindAny<Button>(root, "BtnClose");

            // ---- 背景场景（可留空）----
            sceneImage = FindComponent<RawImage>(FindDeep(root, "scene"));

            ShowCritical(false);
            ShowResultStamp(0);
        }

        /// <summary>绑定一侧武将条的子节点</summary>
        protected virtual void BindSide(Transform root, DebateSide side, string[] rootNames)
        {
            Transform t = FindDeepAny(root, rootNames);
            if (t == null) return;

            side.root = t as RectTransform;
            side.head = FindComponent<RawImage>(FindDeep(t, "head"));
            side.nameText = FindComponent<Text>(FindDeep(t, "name"));
            side.intelText = FindAny<Text>(t, "Intelligence", "intel");
            side.personalityText = FindComponent<Text>(FindDeep(t, "Personality"));
            side.hpBar = FindComponent<Image>(FindDeep(t, "hp/fill"));
            side.hpText = FindComponent<Text>(FindDeep(t, "hp/txt"));
            side.stressBar = FindComponent<Image>(FindDeep(t, "mp/fill"));
            side.stressText = FindComponent<Text>(FindDeep(t, "mp/txt"));
            side.playedCard = FindComponent<Text>(FindDeep(t, "played"));

            Transform anger = FindDeepAny(t, new string[] { "mp/eft_1", "mp/eft_3", "mp/eft_2", "eft_2", "anger" });
            side.angerTag = anger != null ? anger.gameObject : null;
            if (side.angerTag != null) side.angerTag.SetActive(false);
        }

        /// <summary>把交互回调挂到本类的方法上（prefab 上没有持久化绑定时兜底）</summary>
        protected virtual void BindButtons()
        {
            BindHandToggles(leftHand);
            BindHandToggles(rightHand);

            if (togglePushOn != null)
                togglePushOn.onValueChanged.AddListener(on => { if (on && !m_SuppressToggle) SetCriticalChoice((int)DebateCritical.DebateCritical_PushOn); });
            if (toggleMercy != null)
                toggleMercy.onValueChanged.AddListener(on => { if (on && !m_SuppressToggle) SetCriticalChoice((int)DebateCritical.DebateCritical_HaveMercy); });

            if (btnClose != null && btnClose.onClick.GetPersistentEventCount() == 0)
                btnClose.onClick.AddListener(OnCloseClick);
        }

        /// <summary>给一套手牌挂上"点了就出这张牌"的回调</summary>
        protected void BindHandToggles(DebateHand hand)
        {
            if (hand == null || hand.cells == null) return;

            for (int i = 0; i < hand.cells.Length; i++)
            {
                DebateHandCell cell = hand.cells[i];
                if (cell == null || cell.toggle == null) continue;

                int index = i;
                int team = hand.team;
                cell.toggle.onValueChanged.AddListener(on =>
                {
                    if (!on || m_SuppressToggle) return;
                    PickCard(index, team);
                });
            }
        }

        #endregion

        #region 按钮回调

        /// <summary>点第 index 张手牌（prefab 里也可直接绑无参版本）</summary>
        public virtual void OnHandClick(int index)
        {
            PickCard(index, m_PendingCardTeam >= 0 ? m_PendingCardTeam : PlayerTeam(m_Debate));
        }

        /// <summary>
        /// 记下玩家点的那张牌并起"选中态"的计时（见 cardConfirmDuration）。
        /// 停顿结束后，逻辑层下一次询问 DebateSelectCard 才会拿到这个下标、真的出牌。
        /// </summary>
        protected void PickCard(int index, int team)
        {
            m_PendingCard = index;
            m_PendingCardTeam = team;
            m_CardConfirmTimer = Mathf.Max(0f, cardConfirmDuration);
        }

        /// <summary>追击</summary>
        public virtual void OnPushOnClick()
        {
            SetCriticalChoice((int)DebateCritical.DebateCritical_PushOn);
        }

        /// <summary>留情</summary>
        public virtual void OnMercyClick()
        {
            SetCriticalChoice((int)DebateCritical.DebateCritical_HaveMercy);
        }

        /// <summary>关闭结算画面（关掉后逻辑层才会真正收场）</summary>
        public virtual void OnCloseClick()
        {
            m_ResultOpen = false;
            ShowResultStamp(0);
        }

        #endregion

        #region IDebateView：节奏回告

        public virtual bool DebateIsAnimating(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);
            return m_AnimTimer > 0f || m_ResultOpen;
        }

        public virtual bool DebateIsMessageBoxVisible(Debate debate)
        {
            return m_LineCount > 0;
        }

        #endregion

        #region IDebateView：整体流程

        public virtual void DebateOpening(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);

            string a = PersonName(debate, 0);
            string b = PersonName(debate, 1);
            AppendLog($"舌战开始：{a} VS {b}");
            ShowHint("舌战开始");
            if (topicHint != null) topicHint.text = "与本回合话题一致的卡牌威力更高";

            ShowCritical(false);
            SetAnimTimer(openingDuration);
            RefreshAll();
        }

        public virtual void DebateFtk(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);

            AppendLog("【一击必杀】瞬间击溃对手");
            ShowCenterFloat("一击必杀", new Color(1f, 0.85f, 0.3f, 1f));
            SetAnimTimer(openingDuration);
        }

        public virtual void DebateClosing(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);

            // 用逻辑层当前的胜负，而不是 Param.winner —— 后者要等 ClosingPhase 末尾才写入
            int winner = debate.CurrentWinner;
            int winType = debate.CurrentWinType;
            int player = PlayerTeam(debate);

            // 结算画面开着就一直阻塞逻辑层（DebateIsAnimating），点了关闭才收场
            m_ResultOpen = true;
            ShowCritical(false);
            // 收场时把玩家点过、但没被逻辑层消费掉的输入清掉（最后一合出的牌 / 会心），免得留到下一场
            m_PendingCritical = -1;
            ClearHandSelection();
            RefreshAll();

            // 高低两位都在 CardArea/CardCenter：先让它显示胜负标题，再亮对应的大字
            if (topicText != null)
                topicText.text = winner >= 0 ? PersonName(debate, winner) + " 胜" : "不分胜负";
            ShowResultStamp(player < 0 || winner < 0 ? 0 : (winner == player ? 1 : -1));

            AppendLog($"舌战结束：{Debate.GetWinTypeName(winType)}");
            ShowHint("点「中止」关闭结算");
            SetAnimTimer(closingDuration);
        }

        public virtual void DebateAngerEnd(Debate debate, int team)
        {
            SetAnger(team, false);
            AppendLog(PersonName(debate, team) + " 的激昂状态结束");
        }

        #endregion

        #region IDebateView：出牌与卡牌效果

        public virtual void DebatePlayCard(Debate debate, int team, int index)
        {
            if (debate != m_Debate) Bind(debate);

            DebateCardName(debate, team, out string cardName, out int card);
            AppendLog($"{PersonName(debate, team)} 打出「{cardName}」");
            if (card == (int)DebateCard.DebateCard_Rethink)
                ShowFloat(team, PersonName(debate, team) + " 再考", Color.white);

            // 这一手已经出掉了：清掉手牌选中，下一合重新点
            ClearHandSelection();

            SetAnimTimer(playCardDuration);
            RefreshAll();
        }

        public virtual void DebateAttackDraw(Debate debate, int stressDamage)
        {
            AppendLog($"势均力敌，双方愤怒 +{stressDamage}");
            ShowCenterFloat("势均力敌", new Color(1f, 0.9f, 0.6f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateShout(Debate debate, int team, int hpDamage, int stressDamage)
        {
            // 大喝打的是对手（见 Debate.Shout：targetTeam = GetOpponentTeam(team)）
            int target = 1 - team;
            AppendLog($"{PersonName(debate, team)} 大喝：{PersonName(debate, target)} 体力-{hpDamage}、愤怒+{stressDamage}");
            ShowFloat(target, "大喝 -" + hpDamage, new Color(1f, 0.5f, 0.3f, 1f));
            if (hpDamage > 0) PlayHitFx(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateTopic(Debate debate, int team, int card, int hpDamage, int stressDamage, bool reflected)
        {
            // 被诡辩反弹时这张牌打的是自己（见 Debate.TopicCard：reflected 时 targetTeam = team）
            int target = reflected ? team : 1 - team;
            string who = reflected ? "（被诡辩反弹）" : "";
            AppendLog($"{PersonName(debate, team)} {Debate.GetCardName(card)}{who}：{PersonName(debate, target)} 体力-{hpDamage}、愤怒+{stressDamage}");
            ShowFloat(target, Debate.GetCardName(card) + " -" + hpDamage, reflected
                ? new Color(0.8f, 0.6f, 1f, 1f) : new Color(1f, 0.5f, 0.3f, 1f));
            if (hpDamage > 0) PlayHitFx(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateRethink(Debate debate, int team)
        {
            AppendLog(PersonName(debate, team) + " 再考：重抽手牌");
            ShowHint("手牌已重抽");
            ShowFloat(team, "再考", new Color(0.75f, 0.9f, 1f, 1f));
            SetAnimTimer(playCardDuration);
            RefreshAll();
        }

        public virtual void DebateIgnore(Debate debate, int team, int stressDamage)
        {
            AppendLog($"{PersonName(debate, team)} 无视：对手愤怒+{stressDamage}");
            ShowFloat(team, "无视", new Color(0.8f, 0.8f, 0.8f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateCompose(Debate debate, int team, int stressDamage, bool reflected)
        {
            AppendLog($"{PersonName(debate, team)} 镇静：愤怒{stressDamage:+#;-#;0}");
            ShowFloat(reflected ? 1 - team : team, "镇静", new Color(0.6f, 0.9f, 1f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAgitate(Debate debate, int team, int stressDamage, bool reflected)
        {
            AppendLog($"{PersonName(debate, team)} 激昂：愤怒+{stressDamage}");
            ShowFloat(reflected ? 1 - team : team, "激昂", new Color(1f, 0.4f, 0.4f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        #endregion

        #region IDebateView：愤怒

        public virtual void DebateAngerTrigger(Debate debate, int team, int card)
        {
            // 注意：这时 angerTimer 还是 0（逻辑层在随后的 Anger() 里才设），所以标记走显式状态
            SetAnger(team, true);

            AppendLog(PersonName(debate, team) + " 愤怒爆发！");
            ShowFloat(team, PersonName(debate, team) + " 激昂", new Color(1f, 0.45f, 0.35f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAngerReckless(Debate debate, int team, int hpDamage, int stressDamage)
        {
            // 莽撞（猪突）打的是对手（见 Debate 的猪突分支：受伤的是 opponentCharacter）
            int target = 1 - team;
            AppendLog($"{PersonName(debate, team)} 莽撞爆发：{PersonName(debate, target)} 体力-{hpDamage}、愤怒+{stressDamage}");
            ShowFloat(target, "莽撞 -" + hpDamage, new Color(1f, 0.35f, 0.3f, 1f));
            if (hpDamage > 0) PlayHitFx(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAngerTimid(Debate debate, int team, int hpDamage, int stressDamage, int comboIndex)
        {
            // 胆小的连续攻击同样打在对手身上（见 Debate.ComboAttack）
            int target = 1 - team;
            AppendLog($"{PersonName(debate, team)} 连打 #{comboIndex + 1}：{PersonName(debate, target)} 体力-{hpDamage}");
            ShowFloat(target, "连打 -" + hpDamage, new Color(1f, 0.4f, 0.4f, 1f));
            if (hpDamage > 0) PlayHitFx(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        #endregion

        #region IDebateView：玩家操作

        public virtual int DebateSelectCard(Debate debate, int team)
        {
            if (debate != m_Debate) Bind(debate);

            // 换人出牌就把上一次的待选清掉
            if (m_PendingCardTeam != team)
            {
                m_PendingCard = -1;
                m_PendingCardTeam = team;
            }

            RefreshAll();

            if (m_PendingCard < 0)
            {
                ShowHint("请出牌（点手牌）");
                return -1;      // 逻辑层会停在出牌阶段等下一次询问
            }

            // 刚点下去：先把"选中态"亮完，再交出下标（否则高亮只闪一帧，玩家看不见）
            if (m_CardConfirmTimer > 0f)
                return -1;

            int index = m_PendingCard;
            m_PendingCard = -1;
            ClearHandSelection();
            SetAnimTimer(playCardDuration);
            return index;
        }

        public virtual int DebateSelectCritical(Debate debate, int team)
        {
            if (debate != m_Debate) Bind(debate);

            // 追击/留情两个按钮都没接上（美术还在改这两格）时给个兜底，
            // 否则逻辑层会一直停在会心阶段等一个永远等不到的选择。
            if (togglePushOn == null && toggleMercy == null && criticalFallbackHaveMercy)
            {
                if (!m_CriticalFallbackWarned)
                {
                    m_CriticalFallbackWarned = true;
                    Sango.Log.Warning("舌战界面：追击/留情按钮没接上，会心一律按「留情」结算。");
                }
                return (int)DebateCritical.DebateCritical_HaveMercy;
            }

            if (m_PendingCritical < 0)
            {
                ShowCritical(true);
                ShowHint("对手已被击溃，请选择：追击 / 留情");
                return -1;
            }

            int value = m_PendingCritical;
            m_PendingCritical = -1;
            ShowCritical(false);
            SetAnimTimer(effectDuration);
            return value;
        }

        /// <summary>
        /// 是否确认框。与单挑表现层同做法：默认放行，
        /// 需要真正弹框时给 <see cref="YesNoHandler"/> 赋值即可（对话框是异步的，无法在这里同步等待）。
        /// </summary>
        public virtual bool YesNo(string text)
        {
            return YesNoHandler == null || YesNoHandler(text);
        }

        /// <summary>自定义是否确认框实现</summary>
        public static System.Func<string, bool> YesNoHandler;

        #endregion

        #region 刷新

        /// <summary>刷新全部界面</summary>
        public virtual void RefreshAll()
        {
            if (m_Debate == null) return;

            // 结算画面开着时中央文字归胜负标题所有，别被话题刷回去
            if (topicText != null && !m_ResultOpen)
                topicText.text = "话题：" + DebateGameSystem.TopicNameOf(m_Debate.CurrentTopic);

            RefreshSide(leftSide);
            RefreshSide(rightSide);
            RefreshCard(cardLeft);
            RefreshCard(cardRight);

            int player = PlayerTeam(m_Debate);
            RefreshHand(m_Debate, leftHand.team, player);
            RefreshHand(m_Debate, rightHand.team, player);

            RefreshPlayedCard(playedCardLeft);
            RefreshPlayedCard(playedCardRight);
        }

        /// <summary>刷新一侧的武将与状态</summary>
        protected virtual void RefreshSide(DebateSide side)
        {
            if (side == null || m_Debate == null) return;

            Debate.Character c = m_Debate.GetCharacter(side.team);
            if (c == null) return;

            Person person = c.person;
            bool right = side.team == (int)DebateTeam.DebateTeam_Challenged;

            if (side.nameText != null)
                side.nameText.text = person != null ? person.Name : "—";
            if (side.intelText != null)
                side.intelText.text = "智力 " + (person != null ? person.Intelligence : 0);
            if (side.personalityText != null)
                // 注意：Character.PersonalityId 默认是 -1（只是个可选覆盖值），
                // 真实性格要走 CharacterGetPersonality —— 它在越界时会回退到 person.GetPersonality()
                side.personalityText.text = DebateGameSystem.PersonalityNameOf(m_Debate.CharacterGetPersonality(c));
            if (side.hpText != null)
                side.hpText.text = c.hp + " / " + Debate.MaxHP;
            SetBar(side.hpBar, c.hp, Debate.MaxHP, right);
            if (side.stressText != null)
                side.stressText.text = c.stress + " / " + Debate.MaxStress;
            SetBar(side.stressBar, c.stress, Debate.MaxStress, right);
            if (side.angerTag != null)
                side.angerTag.SetActive(IsAngerOn(side.team) || c.angerTimer > 0);
            if (side.playedCard != null)
                side.playedCard.text = "本回合：" + Debate.GetCardName(m_Debate.GetPlayedCard(side.team));

            ApplyHead(side.head, person, 0);
        }

        /// <summary>刷新一张武将卡（头像 / 姓名 / 明细）</summary>
        protected virtual void RefreshCard(DebateCardFace card)
        {
            if (card == null || card.root == null || m_Debate == null) return;

            Debate.Character c = m_Debate.GetCharacter(card.team);
            if (c == null) return;

            Person person = c.person;
            if (card.nameText != null)
                card.nameText.text = person != null ? person.Name : "—";

            if (card.detailText != null)
            {
                int intel = person != null ? person.Intelligence : 0;
                card.detailText.text =
                    "智力 <color=#9FD8FF>" + intel + "</color>\n" +
                    "体力 <color=#FF9A6B>" + c.hp + "/" + Debate.MaxHP + "</color>\n" +
                    "愤怒 <color=#FFD24D>" + c.stress + "/" + Debate.MaxStress + "</color>";
            }

            ApplyHead(card.head, person, cardHeadIconType);
        }

        /// <summary>
        /// 刷新某一方的手牌。
        ///
        /// 四态套图的分工（美术给的 普通 / 按下 / 高亮 / 不可选）：
        ///   · 玩家点中的那张 → 高亮图（= 选中态）；
        ///   · 不是玩家操作的一方（以及用尽的「再考」）→ 不可选图，且不可点；
        ///   · 其余 → 普通图，按住时由 Selectable 的 SpriteSwap 换成按下图。
        /// 另外，出过牌后手牌会变少，空出来的格子直接收起来，不留灰牌。
        /// </summary>
        protected virtual void RefreshHand(Debate debate, int team, int playerTeam)
        {
            DebateHand hand = HandOf(team);
            if (hand == null || debate == null || hand.cells == null) return;

            Debate.Character c = debate.GetCharacter(team);
            if (c == null) return;

            bool isPlayer = team == playerTeam;
            bool thisSideSelected = isPlayer && m_PendingCardTeam == team;

            for (int i = 0; i < hand.cells.Length; i++)
            {
                DebateHandCell cell = hand.cells[i];
                if (cell == null) continue;

                // 手牌变少 → 这一格收掉（不是画成灰牌）
                bool exists = i < c.maxCardCount;
                if (cell.root != null && cell.root.gameObject.activeSelf != exists)
                    cell.root.gameObject.SetActive(exists);
                if (!exists) continue;

                int card = c.card != null && i < c.card.Length ? c.card[i] : -1;
                bool sameTopic = card >= 0 && Debate.GetCardTopic(card) == debate.CurrentTopic;

                // 「再考」这一合用过就不能再用（对应 C++ can_rethink_）：
                // 不禁用的话玩家可以反复点它，出牌 → 重抽 → 再出牌，回合永远走不完。
                bool usable = card >= 0 && (card != (int)DebateCard.DebateCard_Rethink || debate.CanRethink(team));

                DebateCardState state;
                if (!usable || !isPlayer) state = DebateCardState.Disabled;                  // 空位 / AI 侧 / 再考用尽
                else if (thisSideSelected && i == m_PendingCard) state = DebateCardState.Highlighted; // 玩家点中的那张
                else state = DebateCardState.Normal;

                ApplyCardSkin(cell.frame, cell.toggle, card, state);

                if (cell.label != null)
                {
                    cell.label.text = card >= 0 ? Debate.GetCardName(card) + (sameTopic ? " ★" : "") : "—";
                    cell.label.color = state == DebateCardState.Disabled
                        ? DisabledCardTint
                        : (sameTopic ? TopicCardTint : DefaultTextColor(cell.label));
                }
                if (cell.toggle != null) cell.toggle.interactable = usable && isPlayer;

                if (cell.fx != null) cell.fx.SetActive(usable && sameTopic);
            }

            // 选中态（isOn）本类已不再用于显示，但保持同步，方便美术日后改回 sel 方案
            SelectToggle(hand, thisSideSelected ? m_PendingCard : -1);
        }

        /// <summary>
        /// 把一格手牌刷成指定显示态。
        /// 普通 / 高亮（选中）/ 不可选 三态直接换底图；「按下」态交给 Selectable 的 SpriteSwap
        /// （spriteState.pressedSprite）——这样按住时自动换按下图、松手自动回到底图，不用自己接指针事件。
        /// </summary>
        protected void ApplyCardSkin(Image frame, Toggle toggle, int card, DebateCardState state)
        {
            if (frame != null)
            {
                UnityEngine.Sprite sprite = SkinOf(card, state);
                if (sprite != null && frame.sprite != sprite) frame.sprite = sprite;
            }

            if (toggle == null) return;

            UnityEngine.Sprite pressed = SkinOf(card, DebateCardState.Pressed);
            SpriteState spriteState = toggle.spriteState;
            if (spriteState.pressedSprite == pressed && toggle.transition == Selectable.Transition.SpriteSwap) return;

            spriteState.pressedSprite = pressed;
            spriteState.highlightedSprite = null;   // 悬停不抢：选中态由底图表达，别让高亮图在鼠标划过时乱闪
            spriteState.selectedSprite = null;
            spriteState.disabledSprite = null;      // 不可选也由底图表达
            toggle.spriteState = spriteState;
            toggle.transition = Selectable.Transition.SpriteSwap;
        }

        /// <summary>刷一方本回合出的牌：有牌 → 换成该类型的高亮图；没牌 → 只把牌名写成占位字</summary>
        protected virtual void RefreshPlayedCard(DebatePlayedCard show)
        {
            if (show == null || show.root == null || m_Debate == null) return;

            int card = m_Debate.GetPlayedCard(show.team);
            bool has = card >= 0;
            bool sameTopic = has && Debate.GetCardTopic(card) == m_Debate.CurrentTopic;

            if (has && show.frame != null)
            {
                UnityEngine.Sprite sprite = SkinOf(card, DebateCardState.Highlighted);
                if (sprite != null && show.frame.sprite != sprite) show.frame.sprite = sprite;
            }

            if (show.label != null)
                show.label.text = has ? Debate.GetCardName(card) : show.emptyText;

            if (show.fx != null) show.fx.SetActive(sameTopic);
        }

        /// <summary>把一侧体力/愤怒条刷成"当前值 / 上限"</summary>
        protected static void SetBar(Image bar, int value, int max, bool fromRight)
        {
            if (bar == null) return;

            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillOrigin = fromRight ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            bar.fillAmount = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
        }

        /// <summary>设置头像；没有贴图时置全透明，避免渲染成白色实心块</summary>
        protected static void ApplyHead(RawImage head, Person person, int iconType)
        {
            if (head == null) return;

            Texture texture = null;
            if (person != null)
                texture = iconType > 0
                    ? GameRenderHelper.LoadHeadIcon(person.headIconID, iconType)
                    : GameRenderHelper.LoadHeadIcon(person.headIconID);

            head.texture = texture;
            head.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        #endregion

        #region 内部工具

        /// <summary>建套图表（4 种类型，每类 4 态）；prefab 里没存过时才用得上，保证 Inspector 有 4 行可拖</summary>
        protected static DebateCardSkin[] NewCardSkins()
        {
            DebateCardSkin[] skins = new DebateCardSkin[(int)DebateCardSkinType.Special + 1];
            for (int i = 0; i < skins.Length; i++)
                skins[i] = new DebateCardSkin();
            return skins;
        }

        /// <summary>建 7 个手牌格</summary>
        protected static DebateHandCell[] NewCells()
        {
            DebateHandCell[] cells = new DebateHandCell[Debate.MaxCardCount];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = new DebateHandCell();
            return cells;
        }

        /// <summary>牌 → 套图下标（话题牌取话题，话术牌统一落 Special）</summary>
        public static int SkinTypeOf(int card)
        {
            int topic = Debate.GetCardTopic(card);
            return topic >= 0 && topic <= (int)DebateCardSkinType.Trend ? topic : (int)DebateCardSkinType.Special;
        }

        /// <summary>取某张牌某状态的套图；没配图时返回 null（调用方保持原图，不闪白）</summary>
        protected UnityEngine.Sprite SkinOf(int card, DebateCardState state)
        {
            if (card < 0 || cardSkins == null) return null;

            int type = SkinTypeOf(card);
            if (type < 0 || type >= cardSkins.Length || cardSkins[type] == null) return null;
            return cardSkins[type].Get(state);
        }

        /// <summary>把某个 Toggle 从它所在的 ToggleGroup 里摘出来（美术版里几处格子误挂了同一个组）</summary>
        protected static void DetachFromGroup(Toggle toggle)
        {
            if (toggle != null && toggle.group != null) toggle.group = null;
        }

        /// <summary>设置动画节拍（只取更大值，避免后到的短演出把长的压掉）</summary>
        protected void SetAnimTimer(float duration)
        {
            if (duration > m_AnimTimer)
                m_AnimTimer = duration;
        }

        /// <summary>底部提示</summary>
        protected void ShowHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        /// <summary>会心抉择的显隐；收起时顺手清掉上一次的选中</summary>
        protected void ShowCritical(bool on)
        {
            if (togglePushOn != null) togglePushOn.gameObject.SetActive(on);
            if (toggleMercy != null) toggleMercy.gameObject.SetActive(on);

            if (!on) SetCriticalChoice(-1);
        }

        /// <summary>
        /// 记录会心选择并同步两个按钮的选中态。
        /// 追击/留情 原本和右手命令区共用一个 ToggleGroup（已摘掉），所以这里手动保持二选一。
        /// </summary>
        protected void SetCriticalChoice(int value)
        {
            m_PendingCritical = value;

            m_SuppressToggle = true;
            if (togglePushOn != null) togglePushOn.isOn = value == (int)DebateCritical.DebateCritical_PushOn;
            if (toggleMercy != null) toggleMercy.isOn = value == (int)DebateCritical.DebateCritical_HaveMercy;
            m_SuppressToggle = false;
        }

        /// <summary>结算大字的显隐：state 1 = 亮"胜"，-1 = 亮"负"，0 = 全收</summary>
        protected void ShowResultStamp(int state)
        {
            if (resultWin != null) resultWin.gameObject.SetActive(state > 0);
            if (resultLose != null) resultLose.gameObject.SetActive(state < 0);
        }

        /// <summary>把一套手牌里的第 index 格选中（其余全关）；index 为负则全关</summary>
        protected void SelectToggle(DebateHand hand, int index)
        {
            if (hand == null || hand.cells == null) return;

            m_SuppressToggle = true;
            for (int i = 0; i < hand.cells.Length; i++)
            {
                DebateHandCell cell = hand.cells[i];
                if (cell == null || cell.toggle == null) continue;
                cell.toggle.isOn = i == index;
            }
            m_SuppressToggle = false;
        }

        /// <summary>清掉两侧手牌的选中（出了牌 / 新开一局时用）</summary>
        protected void ClearHandSelection()
        {
            SelectToggle(leftHand, -1);
            SelectToggle(rightHand, -1);
            m_PendingCard = -1;
            m_CardConfirmTimer = 0f;
        }

        /// <summary>
        /// 显示一次某方的受击序列帧（face/hit）。
        /// 节点一激活 UIImageAnimation 就会自己起播，所以这里只负责"亮"和记时，
        /// 隐藏交给 Update 里的计时。
        /// </summary>
        protected void PlayHitFx(int team)
        {
            if (team < 0 || team >= m_HitFxTime.Length) return;

            DebateCardFace card = CardOf(team);
            if (card == null || card.hitFx == null) return;

            card.hitFx.SetActive(true);
            m_HitFxTime[team] = Mathf.Max(0.01f, hitFxDuration);
        }

        /// <summary>某方此刻是否显示激昂标记</summary>
        protected bool IsAngerOn(int team)
        {
            return team >= 0 && team < m_AngerOn.Length && m_AngerOn[team];
        }

        /// <summary>设置某方的激昂标记（由 DebateAngerTrigger / DebateAngerEnd 成对调用）</summary>
        protected void SetAnger(int team, bool on)
        {
            if (team < 0 || team >= m_AngerOn.Length) return;
            m_AngerOn[team] = on;

            DebateSide side = SideOf(team);
            if (side != null && side.angerTag != null) side.angerTag.SetActive(on);
        }

        /// <summary>收掉两侧的激昂标记（新开一局时用）</summary>
        protected void ClearAngerMarks()
        {
            for (int i = 0; i < m_AngerOn.Length; i++)
                SetAnger(i, false);
        }

        /// <summary>把两侧的受击特效与计时都清掉（新开一局时用）</summary>
        protected void ClearHitFx()
        {
            for (int i = 0; i < m_HitFxTime.Length; i++)
            {
                m_HitFxTime[i] = 0f;
                DebateCardFace card = CardOf(i);
                if (card != null && card.hitFx != null) card.hitFx.SetActive(false);
            }
        }

        /// <summary>按队伍取武将卡</summary>
        protected DebateCardFace CardOf(int team)
        {
            if (cardLeft != null && cardLeft.team == team) return cardLeft;
            if (cardRight != null && cardRight.team == team) return cardRight;
            return null;
        }

        /// <summary>追加一行战报</summary>
        protected void AppendLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            m_LogLines.Add(text);
            while (m_LogLines.Count > maxLogLines)
                m_LogLines.RemoveAt(0);

            if (logText != null)
                logText.text = string.Join("\n", m_LogLines.ToArray());
        }

        /// <summary>在某一侧的卡面上飘一条</summary>
        protected void ShowFloat(int team, string text, Color color)
        {
            AnimationText target = team == (int)DebateTeam.DebateTeam_Challenged ? floatRight : floatLeft;
            if (target == null) target = floatText;
            ShowFloatOn(target, text, color);
        }

        /// <summary>在中央飘一条（没有中央飘字节点就退回左卡面）</summary>
        protected void ShowCenterFloat(string text, Color color)
        {
            AnimationText target = floatText != null ? floatText : floatLeft;
            ShowFloatOn(target, text, color);
        }

        /// <summary>在指定飘字组件上飘一条（复用单挑卡面同一套 AnimationText）</summary>
        protected void ShowFloatOn(AnimationText target, string text, Color color)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;

            PrepareFloat(target);
            target.flipY = false;            // false = 向上飘
            target.Create(text, color, floatScale);
        }

        /// <summary>
        /// 飘字组件的运行时准备，每个组件只做一次：
        ///   1) maxTime 是 [NonSerialized]，运行时只拿到默认值 1，而曲线跑到 3 秒 —— 不按曲线重算会被半路回收；
        ///   2) 组件取 label 的第 0 个子节点当"图标位"，label 下没有子节点时会抛 IndexOutOfRange，缺了就补一个空 Text。
        /// </summary>
        protected void PrepareFloat(AnimationText target)
        {
            if (target == null || m_PreparedFloats.Contains(target)) return;

            target.maxTime = Mathf.Max(
                CurveEnd(target.offsetCurveX), CurveEnd(target.offsetCurveY),
                CurveEnd(target.alphaCurve), CurveEnd(target.scaleCurve));

            Text template = target.label;
            if (template != null && template.transform.childCount == 0)
            {
                GameObject icon = new GameObject("head", typeof(RectTransform), typeof(Text));
                icon.transform.SetParent(template.transform, false);
                ((RectTransform)icon.transform).anchoredPosition = Vector2.zero;

                Text head = icon.GetComponent<Text>();
                head.font = template.font;
                head.fontSize = template.fontSize;
                head.alignment = TextAnchor.MiddleCenter;
                head.raycastTarget = false;
                head.text = string.Empty;
            }

            m_PreparedFloats.Add(target);
        }

        /// <summary>取曲线的最后一个关键帧时间（飘字总时长）</summary>
        protected static float CurveEnd(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            return curve.keys[curve.length - 1].time;
        }

        /// <summary>取某个文字节点的 prefab 原色（第一次改动前记下来）</summary>
        protected Color DefaultTextColor(Text text)
        {
            if (text == null) return Color.white;

            Color color;
            if (!m_TextDefaultColors.TryGetValue(text, out color))
            {
                color = text.color;
                m_TextDefaultColors[text] = color;
            }
            return color;
        }

        /// <summary>玩家（被玩家操作）的队伍；没有则返回 -1</summary>
        protected static int PlayerTeam(Debate debate)
        {
            if (debate == null) return -1;

            for (int i = 0; i < Debate.MaxTeamCount; i++)
            {
                Debate.Character c = debate.GetCharacter(i);
                if (c != null && c.control) return i;
            }
            return -1;
        }

        /// <summary>某方武将的姓名</summary>
        protected string PersonName(Debate debate, int team)
        {
            Debate.Character c = debate != null ? debate.GetCharacter(team) : null;
            return c != null && c.person != null ? c.person.Name : ("队伍" + team);
        }

        /// <summary>某方本回合打出的牌名与牌值</summary>
        protected void DebateCardName(Debate debate, int team, out string name, out int card)
        {
            card = debate != null ? debate.GetPlayedCard(team) : -1;
            name = Debate.GetCardName(card);
        }

        /// <summary>按队伍取武将条</summary>
        protected DebateSide SideOf(int team)
        {
            if (leftSide != null && leftSide.team == team) return leftSide;
            if (rightSide != null && rightSide.team == team) return rightSide;
            return null;
        }

        /// <summary>按队伍取手牌</summary>
        protected DebateHand HandOf(int team)
        {
            if (leftHand != null && leftHand.team == team) return leftHand;
            if (rightHand != null && rightHand.team == team) return rightHand;
            return null;
        }

        #endregion

        #region 节点查找工具

        /// <summary>
        /// 递归按名字查找（支持 a/b 路径）。
        /// 每段都只在自己**之下**找：像 BlowCounter_bg/stopBtn/stopBtn 这种"容器与子节点同名"的结构，
        /// 若允许命中自己，第一段就会停在外层容器上（它没有 Button），后面那段就永远找不到。
        /// </summary>
        protected static Transform FindDeep(Transform root, string namePath)
        {
            if (root == null || string.IsNullOrEmpty(namePath)) return null;

            Transform cur = root;
            int start = 0;
            while (start < namePath.Length)
            {
                int slash = namePath.IndexOf('/', start);
                string seg = slash < 0 ? namePath.Substring(start) : namePath.Substring(start, slash - start);

                cur = DeepChild(cur, seg);
                if (cur == null) return null;

                if (slash < 0) break;
                start = slash + 1;
            }
            return cur;
        }

        /// <summary>只在子节点里按名字递归查找（不含 root 自己）</summary>
        protected static Transform DeepChild(Transform root, string name)
        {
            if (root == null) return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = Deep(root.GetChild(i), name);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>按候选路径依次查找，返回第一个命中的节点</summary>
        protected static Transform FindDeepAny(Transform root, string[] namePaths)
        {
            if (root == null || namePaths == null) return null;

            for (int i = 0; i < namePaths.Length; i++)
            {
                Transform found = FindDeep(root, namePaths[i]);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>递归按名字查找</summary>
        protected static Transform Deep(Transform root, string name)
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

        protected static T FindComponent<T>(Transform t) where T : Component
        {
            return t != null ? t.GetComponent<T>() : null;
        }

        /// <summary>按候选名依次查找组件（第一个命中的就返回）</summary>
        protected static T FindAny<T>(Transform root, params string[] names) where T : Component
        {
            if (root == null || names == null) return null;

            for (int i = 0; i < names.Length; i++)
            {
                T found = FindComponent<T>(FindDeep(root, names[i]));
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>取一个 Toggle 上的文字子节点（Label / lab 两种叫法都认）</summary>
        protected static Text FindCellLabel(Toggle toggle)
        {
            if (toggle == null) return null;
            return FindComponent<Text>(FindDeep(toggle.transform, "Label"))
                ?? FindComponent<Text>(FindDeep(toggle.transform, "lab"));
        }

        #endregion
    }
}
