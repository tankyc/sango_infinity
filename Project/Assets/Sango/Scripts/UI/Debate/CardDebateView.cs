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
 *   │   │   ├── face/ head(头像) name(姓名) detail(明细) ani_info(飘字) hit(受击特效) anger(激昂特效)
 *   │   │   └── Dialogue                          该方的台词气泡（美术资源，本类不驱动）
 *   │   ├── CardCenter                           中央文字（原为 "VS"，现用于显示话题 / 胜负）
 *   │   └── win / lose                           结算大字（默认隐藏，结算时亮一个）
 *   ├── BlowCounter_bg                           顶部中央：合数计数器 + 双方出牌
 *   │   ├── GameObject/BlowCounter_ten, Blow      合数（美术资源，逻辑层未使用）
 *   │   ├── card1 / card2                         双方本回合出的牌（只是个 Image：按牌的类型换高亮图）
 *   │   └── stopBtn/stopBtn                       「中止」按钮：**当前需求是全程不显示**，绑定时就收起
 *   ├── LogBg/log                                战报（功能已删除：绑定时把这个节点收起来，prefab 里可以删掉）
 *   ├── down/info                                底部操作提示
 *   ├── lfetCommand/buttons/Stance1..7            挑战方（左）手牌
 *   ├── rightCommand/buttons/Stance1..7           应战方（右）手牌
 *   ├── left_top/person_left                      挑战方武将条（head name Personality hp/fill mp/{fill,eft_1,eft_3} anger）
 *   ├── right_top/person_right                    应战方武将条（同结构）
 *   └── outBtn/img                                退出舌战（只在收场对白播完后才亮）
 *
 * 两侧对应关系：左＝挑战方(队伍 0)，右＝应战方(队伍 1)，哪一侧归玩家点由 Character.control 决定。
 *
 * 手牌是 Toggle（不是 Button）：它是"这一合要出的牌"的选中表达。但选中态不靠 Toggle 的 sel 覆盖图，
 * 而是按牌的类型换底图（见 ApplyCardSkin 与 cardSkins），所以运行时会把每格的 sel 关掉、
 * 并把 ToggleGroup.allowSwitchOff 打开（否则程序化清空选中会被组规则顶回来）。
 * card1/card2（上方出牌展示区）与手牌同一套做法：底图按牌的类型换高亮图，同样要把 sel 覆盖图收掉，
 * 否则常亮的 sel 会把换好的底板整个盖住、看起来"底板不换"。
 *
 * 牌名的可见性（hideOpponentCardNames）：
 *   · 玩家自己那边：手牌与出牌展示区都写全名（"故事·大"）；
 *   · 对手那边：手牌只写**类别**（"故事"，不给大小）；出牌展示区只亮底板、
 *     要等双方都出完牌才揭晓真正的牌名（见 CanShowCardName）。
 *
 * 手牌末格（Stance7）固定是「熟虑」（再考）：手牌里没有它也照样显示，
 * 能不能用（CanRethink）决定它显示成普通还是不可用（见 BuildSlotOrder / RefreshHand）。
 *
 * 收场节奏：ClosingPhase 进来先只说收场对白（赢家按追击/留情分两套、输家接一句），
 * 胜负大字与「退出」按钮要对白全播完才亮（见 UpdateResultReveal / RevealResult）；
 * 这段等待期靠 m_ResultOpen 继续把逻辑层挡在收场阶段，所以不会提前收场。
 *
 * 激昂（愤怒）表现（见 DebateAngerTrigger / RefreshAngerLook / ClearAngerLook）：
 *   · 爆发那一刻：该方卡面飘「激昂」大字 + 抖一下 + 按性格喊一句（胆小 / 冷静 / 刚胆 / 莽撞 各一套台词）；
 *   · 激昂期间（逻辑层 angerTimer > 0）：整张卡面与姓名染上 angerTint / angerNameColor，
 *     美术给的 anger 特效（CardLeft|Right/face/anger、person_left|right/anger）与满怒特效（mp/eft_3）一起亮着；
 *   · 状态是用 angerTimer 推导的，所以激昂怎么结束（回合到 / 顶掉 / 收场）都能自己收回来，不依赖"谁结束的"。
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

        /// <summary>左方卡面飘字（CardLeft/face/ani_info）</summary>
        [Header("飘字")]
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

        /// <summary>
        /// 追击/留情 交给 PlayerChoice（window_choice）来问：
        ///   · 该方是玩家操作 → 弹选择肢，等玩家点；
        ///   · 该方不是玩家操作（或 PlayerChoice 不可用）→ 直接按「留情」自动结算，不会卡住。
        /// </summary>
        [Header("会心抉择（追击 / 留情）")]
        public bool criticalFallbackHaveMercy = true;

        /// <summary>我方胜利大字（CardArea/win，默认隐藏）</summary>
        [Header("结算（CardArea/win、CardArea/lose、BlowCounter_bg/stopBtn/stopBtn）")]
        public RectTransform resultWin;
        /// <summary>我方失败大字（CardArea/lose，默认隐藏）</summary>
        public RectTransform resultLose;
        /// <summary>
        /// 「中止」按钮（BlowCounter_bg/stopBtn/stopBtn）。
        /// 当前需求是**全程不显示**（暂时不需要它），所以绑定时就把整块收起来，没有任何地方再打开。
        /// </summary>
        public Button btnClose;
        /// <summary>「中止」按钮的整块容器（BlowCounter_bg/stopBtn）："中止"两个字在容器上，光关按钮会留下两个字</summary>
        public GameObject stopRoot;

        /// <summary>背景场景贴图节点（scene，可留空）</summary>
        [Header("场景背景（scene，可留空）")]
        public RawImage sceneImage;

        /// <summary>左方台词气泡（CardArea/CardLeft/Dialogue）</summary>
        [Header("台词气泡（CardLeft|CardRight/Dialogue）")]
        public DebateDialogue dialogueLeft = new DebateDialogue();
        /// <summary>右方台词气泡（CardArea/CardRight/Dialogue）</summary>
        public DebateDialogue dialogueRight = new DebateDialogue();
        /// <summary>普通底板（4848-2_1）</summary>
        public UnityEngine.Sprite dialogueFrameNormal;
        /// <summary>激动底板（4848-2_0）</summary>
        public UnityEngine.Sprite dialogueFrameExcited;

        /// <summary>话题特效（BlowCounter_bg/GameObject/eft）：颜色随当前话题变</summary>
        [Header("话题特效 / 收场")]
        public Image topicEft;
        /// <summary>话题特效颜色，下标与 Topic 一致（故事 / 道理 / 时势）</summary>
        public Color[] topicEftColors = NewTopicColors();

        /// <summary>退出舌战的按钮（outBtn/img）</summary>
        public Button btnOut;
        /// <summary>
        /// 「退出」按钮的整块容器（outBtn）。
        /// prefab 里"退出"两个字挂在容器上、图片与按钮在子节点 img 上，
        /// 只开关 btnOut 的话那两个字会一直留在界面上，所以收场前要连容器一起收。
        /// </summary>
        public GameObject outRoot;
        /// <summary>左方手牌根节点（lfetCommand），结束后收起</summary>
        public GameObject handRootLeft;
        /// <summary>右方手牌根节点（rightCommand），结束后收起</summary>
        public GameObject handRootRight;

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
        /// <summary>
        /// 别把对手的牌提前告诉玩家：
        ///   · 手牌：对手那侧只写**类别**（"故事"），不写大小（"故事·大"）；
        ///   · 上方出牌展示区：对手出的牌只亮底板（按牌类型换的图）不写牌名，
        ///     要等**双方都出完牌**才揭晓真正的牌名（见 CanShowCardName）。
        /// 关掉它就恢复"全部照实显示"，排查显示问题时用。
        /// </summary>
        public bool hideOpponentCardNames = true;

        /// <summary>
        /// 手牌末格那张固定牌上写什么字。
        /// 逻辑层叫它「再考」（DebateCard_Rethink），美术版界面上写的是「熟虑」，以美术为准。
        /// </summary>
        public string rethinkCardName = "熟虑";
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

        /// <summary>一条台词在气泡里停留多久（秒）</summary>
        public float dialogueHoldDuration = 1.1f;
        /// <summary>台词气泡进场/退场动画时长（秒）</summary>
        public float dialogueAnimDuration = 0.16f;
        /// <summary>出牌展示格的出牌动画时长（秒）</summary>
        public float playedCardAnimDuration = 0.28f;
        /// <summary>受击抖动时长（秒）</summary>
        public float shakeDuration = 0.4f;
        /// <summary>受击抖动幅度（像素）</summary>
        public float shakeStrength = 16f;
        /// <summary>胜负大字的出场动画时长（秒）</summary>
        public float resultAnimDuration = 0.4f;
        /// <summary>失败方卡牌的变暗色（乘在卡面与头像上）</summary>
        public Color loserTint = new Color(0.42f, 0.42f, 0.5f, 1f);

        /// <summary>激昂（愤怒）状态的染色：卡面与头像乘这个颜色，整段激昂期间一直保持</summary>
        [Header("激昂（愤怒）表现")]
        public Color angerTint = new Color(1f, 0.62f, 0.45f, 1f);
        /// <summary>激昂期间武将条姓名的颜色</summary>
        public Color angerNameColor = new Color(1f, 0.38f, 0.3f, 1f);
        /// <summary>激昂爆发大字的缩放倍数（乘在 floatScale 上）</summary>
        public float angerBannerScale = 1.6f;
        /// <summary>激昂爆发的演出时长（秒）；这段里逻辑层会等</summary>
        public float angerBurstDuration = 0.9f;

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

        /// <summary>
        /// 待揭晓的胜负大字（1 胜 / -1 负 / 0 = 只亮「退出」按钮，例如不分胜负）。
        /// 配合 <see cref="m_ResultWaiting"/> 使用。
        /// </summary>
        protected int m_ResultPending;

        /// <summary>
        /// 结果是否在等收场对白播完（true = 大字与「退出」按钮都还没亮）。
        /// 这段等待期靠 m_ResultOpen 继续把逻辑层挡在收场阶段（见 DebateIsAnimating）。
        /// </summary>
        protected bool m_ResultWaiting;

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

        /// <summary>会心（追击/留情）是否已经问过玩家（PlayerChoice 是异步的，只问一次）</summary>
        protected bool m_CriticalAsked;

        /// <summary>抖动槽位数：每方两个（武将条 + 卡面）</summary>
        protected const int ShakeSlotCount = Debate.MaxTeamCount * 2;

        /// <summary>受击抖动：时间一到就把锚点位置还原</summary>
        protected readonly float[] m_ShakeTimer = new float[ShakeSlotCount];
        /// <summary>受击抖动作用的锚点（武将条 / 卡面）</summary>
        protected readonly RectTransform[] m_ShakeTarget = new RectTransform[ShakeSlotCount];
        /// <summary>抖动目标的原始位置</summary>
        protected readonly Vector2[] m_ShakeHome = new Vector2[ShakeSlotCount];

        /// <summary>胜负大字的原始缩放（美术不是 1，动画要乘在它上面）</summary>
        protected Vector3 m_ResultWinScale = Vector3.one;
        /// <summary>胜负大字的原始缩放（美术不是 1，动画要乘在它上面）</summary>
        protected Vector3 m_ResultLoseScale = Vector3.one;

        /// <summary>胜负大字的出场动画进度（-1 = 已播完）</summary>
        protected float m_ResultAnimTimer = -1f;
        /// <summary>当前亮的是哪个大字：1 胜 / -1 负 / 0 都没亮</summary>
        protected int m_ResultStampState;

        /// <summary>程序化改动 Toggle 选中态期间置位，用来屏蔽 onValueChanged，避免"刷新"被当成"玩家出牌"</summary>
        protected bool m_SuppressToggle;

        /// <summary>已经做过运行时准备的飘字组件（maxTime / 图标子节点）</summary>
        protected readonly List<AnimationText> m_PreparedFloats = new List<AnimationText>();

        /// <summary>文字的原色（第一次改动前记下来，恢复时用）</summary>
        protected readonly Dictionary<Text, Color> m_TextDefaultColors = new Dictionary<Text, Color>();

        /// <summary>两侧卡面受击特效还要亮多久（下标为队伍，> 0 表示正在亮）</summary>
        protected readonly float[] m_HitFxTime = new float[Debate.MaxTeamCount];

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

        /// <summary>推进所有演出计时：动画节拍、确认停顿、受击序列帧、台词气泡、抖动、出牌与结算动画</summary>
        protected virtual void Update()
        {
            float dt = Time.deltaTime;

            if (m_AnimTimer > 0f)
                m_AnimTimer -= dt;

            if (m_CardConfirmTimer > 0f)
                m_CardConfirmTimer -= dt;

            UpdateDialogue(dialogueLeft, dt);
            UpdateDialogue(dialogueRight, dt);
            UpdateShakes(dt);
            UpdatePlayedCardAnim(playedCardLeft, dt);
            UpdatePlayedCardAnim(playedCardRight, dt);
            UpdateResultAnim(dt);
            UpdateResultReveal();

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
            /// <summary>激昂特效（美术新加的 face/anger）：只在激昂期间亮</summary>
            public GameObject angerFx;
            /// <summary>是否处于"失败方变暗"状态（收场给输家压暗；变暗的卡不吃激昂染色）</summary>
            [System.NonSerialized] public bool dimmed;
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

                // 激昂特效（face/anger）同理：prefab 里开着，绑定时先收起来，等真的激昂了再亮（见 RefreshAngerLook）
                Transform anger = FindDeep(scope, "anger");
                angerFx = anger != null ? anger.gameObject : null;
                if (angerFx != null) angerFx.SetActive(false);

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
            /// <summary>常驻气势（mp/eft_1）：整场一直亮着</summary>
            public GameObject standFx;
            /// <summary>激昂特效（美术新加的 anger 节点）：只在激昂期间亮</summary>
            public GameObject angerFx;
            /// <summary>满怒特效（mp/eft_3）：怒气满了（或激昂期间）亮</summary>
            public GameObject fullStressFx;
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

            /// <summary>
            /// 槽位 → 手牌下标。排序规则：话题牌在前、特殊牌（再考 / 话术）放最后，
            /// 这样 Stance7 优先显示特殊牌；玩家点了哪个槽，回给逻辑层的还是真实的 <see cref="Debate.Character.card"/> 下标。
            /// </summary>
            [System.NonSerialized] public int[] slotToHand = new int[Debate.MaxCardCount];

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

        /// <summary>一句台词：文本 + 情绪（激动时换激动底板）</summary>
        public struct DebateLine
        {
            /// <summary>台词文本</summary>
            public string text;
            /// <summary>是否激动（true = 用激动底板，否则用普通底板）</summary>
            public bool excited;

            public DebateLine(string text, bool excited)
            {
                this.text = text;
                this.excited = excited;
            }
        }

        /// <summary>
        /// 一方的台词气泡（CardArea/CardLeft/Dialogue、CardArea/CardRight/Dialogue）。
        /// 寒暄 / 出牌 / 受击 / 收场的随机台词都从这里出，底板按情绪换（普通 4848-2_1 / 激动 4848-2_0）。
        /// </summary>
        [System.Serializable]
        public class DebateDialogue
        {
            /// <summary>所属队伍（0=挑战方 / 1=应战方）</summary>
            public int team;
            /// <summary>根节点（Dialogue）</summary>
            public RectTransform root;
            /// <summary>底板（就是根节点上的 Image）</summary>
            public Image frame;
            /// <summary>台词文字（Label）</summary>
            public Text label;

            [System.NonSerialized] public Vector2 home;              // 原始位置（进场动画用）
            [System.NonSerialized] public Vector3 homeScale = Vector3.one;   // 原始缩放（美术可能不是 1）
            [System.NonSerialized] public Color frameHomeColor = Color.white;
            [System.NonSerialized] public Color labelHomeColor = Color.white;
            [System.NonSerialized] public float timer;               // 已播时长
            [System.NonSerialized] public bool playing;              // 正在播
            [System.NonSerialized] public bool inPhase = true;       // true = 进场段，false = 退场段

            /// <summary>绑定一格气泡；找不到的项留空</summary>
            public void Bind(Transform node)
            {
                root = node as RectTransform;
                if (root == null) return;

                home = root.anchoredPosition;
                homeScale = root.localScale;
                frame = root.GetComponent<Image>();
                label = FindComponent<Text>(FindDeep(root, "Label")) ?? FindComponent<Text>(FindDeep(root, "lab"));

                if (frame != null) frameHomeColor = frame.color;
                if (label != null) labelHomeColor = label.color;

                root.gameObject.SetActive(false);
                timer = 0f;
                playing = false;
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
            public string emptyText = "--";

            [System.NonSerialized] public Vector2 home;      // 原始位置（出牌动画的起点）
            [System.NonSerialized] public Vector3 homeScale = Vector3.one;   // 原始缩放（美术可能不是 1）
            [System.NonSerialized] public float fxTimer;    // 出牌动画剩余时长（<= 0 表示没在演）

            /// <summary>绑定一格；找不到的项留空</summary>
            public void Bind(Transform cardRoot)
            {
                root = cardRoot as RectTransform;
                if (root == null) return;

                frame = root.GetComponent<Image>();
                label = FindComponent<Text>(FindDeep(root, "Label")) ?? FindComponent<Text>(FindDeep(root, "lab"));

                Transform eft = FindDeep(root, "eft");
                fx = eft != null ? eft.gameObject : null;

                // 美术在这两格上放了一张常亮的 sel 覆盖图，会把"按牌类型换的高亮底板"整个盖住，
                // 于是看起来底板永远不换。这里收起来 —— 高亮底板统一由根节点换图表达（与手牌格同一套做法）。
                Transform selNode = FindDeep(root, "sel");
                if (selNode != null) selNode.gameObject.SetActive(false);

                home = root.anchoredPosition;
                homeScale = root.localScale;

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
            m_ResultPending = 0;
            m_ResultWaiting = false;
            m_LineCount = 0;
            m_PendingCard = -1;
            m_PendingCardTeam = -1;
            m_PendingCritical = -1;
            m_SuppressToggle = false;

            ClearResultStamp();
            ClearHandSelection();
            ClearHitFx();
            ClearDialogues();
            ClearShakes();
            SetHandsVisible(true);
            SetOutVisible(false);
            SetStopVisible(false);
            m_CriticalAsked = false;
            ClearAngerLook();
            SetCardDimmed(cardLeft, false);
            SetCardDimmed(cardRight, false);

            RefreshAll();
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

            // ---- 飘字 ----
            floatLeft = FindComponent<AnimationText>(FindDeep(root, "CardLeft/face/ani_info"));
            // 战报功能已按需求删除：prefab 里那块战报背景板（LogBg）就不再显示了，
            // 免得屏幕右下角空着一块黑框。美术把节点从 prefab 删掉后这里自然空转（FindNodeObject 返回 null）。
            GameObject logRoot = FindNodeObject(root, "LogBg");
            if (logRoot != null) logRoot.SetActive(false);
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

            // ---- 结算 ----
            resultWin = FindDeep(root, "win") as RectTransform;
            resultLose = FindDeep(root, "lose") as RectTransform;
            if (resultWin != null) m_ResultWinScale = resultWin.localScale;
            if (resultLose != null) m_ResultLoseScale = resultLose.localScale;
            btnClose = FindComponent<Button>(FindDeep(root, "stopBtn/stopBtn")) ?? FindAny<Button>(root, "BtnClose");
            // 「中止」整块容器也记下来（"中止"两个字在容器上，只关按钮会留下两个字）；当前需求是全程不显示
            stopRoot = FindNodeObject(root, "stopBtn");
            if (stopRoot == null && btnClose != null && btnClose.transform.parent != null)
                stopRoot = btnClose.transform.parent.gameObject;

            // ---- 背景场景（可留空）----
            sceneImage = FindComponent<RawImage>(FindDeep(root, "scene"));

            // ---- 台词气泡（挂在各自的武将卡上）----
            dialogueLeft.team = (int)DebateTeam.DebateTeam_Challenger;
            dialogueRight.team = (int)DebateTeam.DebateTeam_Challenged;
            dialogueLeft.Bind(FindDeep(root, "CardLeft/Dialogue"));
            dialogueRight.Bind(FindDeep(root, "CardRight/Dialogue"));

            // ---- 话题特效 / 收场 ----
            topicEft = FindComponent<Image>(FindDeep(root, "BlowCounter_bg/GameObject/eft"));
            btnOut = FindAny<Button>(root, "outBtn/img", "outBtn", "BtnOut");
            outRoot = FindNodeObject(root, "outBtn");
            if (outRoot == null && btnOut != null && btnOut.transform.parent != null)
                outRoot = btnOut.transform.parent.gameObject;
            handRootLeft = FindNodeObject(root, "lfetCommand");
            handRootRight = FindNodeObject(root, "rightCommand");

            ClearResultStamp();
            ClearDialogues();
            SetHandsVisible(true);
            // 「退出」只在结算阶段出现：绑定时先收起来（prefab 里它是开着的，方便美术排版）
            SetOutVisible(false);
            // 「中止」当前需求是全程不显示，绑定时就收起来，没有任何地方会再打开它
            SetStopVisible(false);
        }

        /// <summary>按名字找一个节点上的 GameObject</summary>
        protected static GameObject FindNodeObject(Transform root, string name)
        {
            Transform t = FindDeep(root, name);
            return t != null ? t.gameObject : null;
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

            // 常驻气势（mp/eft_1）：整场亮着。
            // 注意候选名里**不能**带 "anger" —— 美术新加的愤怒特效就叫 anger，混进来会被当成常驻特效一直亮着。
            Transform stand = FindDeepAny(t, new string[] { "mp/eft_1", "mp/eft_2", "eft_2" });
            side.standFx = stand != null ? stand.gameObject : null;

            // 激昂特效（美术新加的 anger 节点）与满怒特效（mp/eft_3）：都只在激昂期间亮（见 RefreshAngerLook）
            Transform anger = FindDeep(t, "anger");
            side.angerFx = anger != null ? anger.gameObject : null;

            Transform fullStress = FindDeepAny(t, new string[] { "mp/eft_3" });
            side.fullStressFx = fullStress != null ? fullStress.gameObject : null;

            if (side.standFx != null) side.standFx.SetActive(true);
            if (side.angerFx != null) side.angerFx.SetActive(false);
            if (side.fullStressFx != null) side.fullStressFx.SetActive(false);
        }

        /// <summary>把交互回调挂到本类的方法上（prefab 上没有持久化绑定时兜底）</summary>
        protected virtual void BindButtons()
        {
            BindHandToggles(leftHand);
            BindHandToggles(rightHand);

            // 「中止」当前全程隐藏，点了也到不了（绑着只是为了它日后回来时行为一致）
            if (btnClose != null && btnClose.onClick.GetPersistentEventCount() == 0)
                btnClose.onClick.AddListener(OnCloseClick);

            // 退出按钮：收起结算画面，逻辑层随之收场并关掉窗口
            if (btnOut != null && btnOut.onClick.GetPersistentEventCount() == 0)
                btnOut.onClick.AddListener(OnCloseClick);

            // 「退出」整块先收起来：它只在结算阶段（对白播完后）才亮，前面阶段不显示
            SetOutVisible(false);
        }

        /// <summary>给一套手牌挂上"点了就出这张牌"的回调</summary>
        protected void BindHandToggles(DebateHand hand)
        {
            if (hand == null || hand.cells == null) return;

            for (int i = 0; i < hand.cells.Length; i++)
            {
                DebateHandCell cell = hand.cells[i];
                if (cell == null || cell.toggle == null) continue;

                int slot = i;
                int team = hand.team;
                cell.toggle.onValueChanged.AddListener(on =>
                {
                    if (!on || m_SuppressToggle) return;
                    // 点的是槽位，交回逻辑层的必须是真实的手牌下标
                    PickCard(SlotToHandIndex(hand, slot), team);
                });
            }
        }

        #endregion

        #region 按钮回调

        /// <summary>点第 slot 个手牌槽位（prefab 里也可直接绑；内部会换算成真实的手牌下标）</summary>
        public virtual void OnHandClick(int slot)
        {
            int team = m_PendingCardTeam >= 0 ? m_PendingCardTeam : PlayerTeam(m_Debate);
            PickCard(SlotToHandIndex(HandOf(team), slot), team);
        }

        /// <summary>槽位 → 手牌下标（找不到返回 -1）</summary>
        protected static int SlotToHandIndex(DebateHand hand, int slot)
        {
            if (hand == null || hand.slotToHand == null || slot < 0 || slot >= hand.slotToHand.Length) return -1;
            return hand.slotToHand[slot];
        }

        /// <summary>
        /// 这一方手上第 handIndex 张牌现在能不能出。
        /// 与 <see cref="RefreshHand"/> 里算可用性的口径一致：空位不行，「再考」这一合用过了也不行。
        /// </summary>
        protected bool IsCardUsable(int team, int handIndex)
        {
            if (m_Debate == null || handIndex < 0) return false;

            Debate.Character c = m_Debate.GetCharacter(team);
            if (c == null || c.card == null || handIndex >= c.card.Length) return false;

            int card = c.card[handIndex];
            if (card < 0) return false;
            if (card == (int)DebateCard.DebateCard_Rethink) return m_Debate.CanRethink(team);
            return true;
        }

        /// <summary>
        /// 记下玩家点的那张牌并起"选中态"的计时（见 cardConfirmDuration）。
        /// 停顿结束后，逻辑层下一次询问 DebateSelectCard 才会拿到这个下标、真的出牌。
        /// </summary>
        protected void PickCard(int index, int team)
        {
            if (index < 0) return;      // 空格子点不动

            // 兜底：这一合已经用不了的牌不接。玩家侧的逻辑层没有"再考用尽"的判断，
            // 放过去就会变成"出牌 → 重抽 → 再出牌"的死循环（见 RefreshHand 的注释）；
            // 界面那边虽然已经把格子置灰、不可点，这里再挡一道，免得美术日后改了交互又漏出来。
            if (!IsCardUsable(team, index)) return;

            m_PendingCard = index;
            m_PendingCardTeam = team;
            m_CardConfirmTimer = Mathf.Max(0f, cardConfirmDuration);
        }

        /// <summary>追击（也可以由别的地方直接调它给答案）</summary>
        public virtual void OnPushOnClick()
        {
            m_PendingCritical = (int)DebateCritical.DebateCritical_PushOn;
        }

        /// <summary>留情（也可以由别的地方直接调它给答案）</summary>
        public virtual void OnMercyClick()
        {
            m_PendingCritical = (int)DebateCritical.DebateCritical_HaveMercy;
        }

        /// <summary>退出舌战（收起结算画面，逻辑层随之收场并关闭窗口）</summary>
        public virtual void OnCloseClick()
        {
            m_ResultOpen = false;
            m_ResultWaiting = false;
            m_ResultPending = 0;
            ClearResultStamp();
            SetOutVisible(false);
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

            ShowHint("舌战开始");
            // 开场阶段不该有「退出」按钮，收场对白播完才亮（见 UpdateResultReveal）
            m_ResultPending = 0;
            m_ResultWaiting = false;
            SetOutVisible(false);
            if (topicHint != null) topicHint.text = "与本回合话题一致的卡牌威力更高";

            // 开场寒暄：挑战方先叫阵，应战方回一句（随机台词，气泡按顺序播）
            SayRandom(0, OpeningLinesChallenger);
            SayRandom(1, OpeningLinesChallenged);

            SetAnimTimer(openingDuration + LineTotalDuration(2));
            RefreshAll();
        }

        public virtual void DebateFtk(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);

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

            // 结算画面开着就一直阻塞逻辑层（DebateIsAnimating），点了「退出」才收场
            m_ResultOpen = true;
            // 收场时把玩家点过、但没被逻辑层消费掉的输入清掉（最后一合出的牌 / 会心），免得留到下一场
            m_PendingCritical = -1;
            ClearHandSelection();
            RefreshAll();

            // 收场：收起双方手牌、失败方的卡变暗（先把激昂的染色/满怒特效收干净，免得跟压暗叠在一起）
            SetHandsVisible(false);
            ClearAngerLook();
            SetCardDimmed(cardLeft, player >= 0 && winner >= 0 && winner != leftSide.team);
            SetCardDimmed(cardRight, player >= 0 && winner >= 0 && winner != rightSide.team);

            // 先只说收场对白。胜负大字与「退出」按钮**等对白播完**才亮（见 UpdateResultReveal）——
            // 结果和台词同时砸出来会互相抢，也对不上"最后一句说完才揭晓"的节奏。
            m_ResultPending = player < 0 || winner < 0 ? 0 : (winner == player ? 1 : -1);
            m_ResultWaiting = true;
            SetOutVisible(false);
            SayClosingLines(debate, winner, winType);

            ShowHint("舌战结束");

            // 等多久：收场对白最多两条（赢家一句 + 输家一句），再加一点揭晓后的停留
            SetAnimTimer(closingDuration + LineTotalDuration(2));
        }

        /// <summary>
        /// 说收场对白。刻意与中场的随机台词（出牌 / 受击那几套）分开：
        /// 赢家有专门的收尾台词、按"追击 / 留情"再分两套，输家接一句认负，不分胜负则双方各叹一句。
        /// </summary>
        protected virtual void SayClosingLines(Debate debate, int winner, int winType)
        {
            if (winner < 0)
            {
                SayRandom(0, ClosingDrawLines);
                SayRandom(1, ClosingDrawLines);
                return;
            }

            bool pushOn = winType == (int)DebateWinType.DebateWinType_PushOn;
            SayRandom(winner, pushOn ? ClosingWinPushOnLines : ClosingWinHaveMercyLines);
            SayRandom(1 - winner, ClosingLoseLines);
        }

        public virtual void DebateAngerEnd(Debate debate, int team)
        {
            if (debate != m_Debate) Bind(debate);

            // 激昂结束：刷新一遍，RefreshAngerLook 会把染色、满怒特效按当前状态收回去
            // （激昂期的状态表现全部由 RefreshAngerLook 按 angerTimer 推导，所以这里不用逐个还原）
            RefreshAll();
        }

        /// <summary>
        /// 激昂爆发时喊的一句，按性格分四套：
        /// 胆小（发作后连打）、冷静（怒而更冷静）、刚胆（愤怒抢攻）、莽撞（猪突猛进）。
        /// </summary>
        protected virtual void SayAngerLine(Debate debate, int team)
        {
            Debate.Character c = debate.GetCharacter(team);
            int personality = m_Debate != null ? m_Debate.CharacterGetPersonality(c) : -1;

            switch (personality)
            {
                case (int)Personality.Personality_Timid: SayRandom(team, AngerLinesTimid); break;
                case (int)Personality.Personality_Calm: SayRandom(team, AngerLinesCalm); break;
                case (int)Personality.Personality_Reckless: SayRandom(team, AngerLinesReckless); break;
                default: SayRandom(team, AngerLinesBold); break;
            }
        }

        /// <summary>
        /// 激昂（愤怒）期间的表现：卡面与头像染 angerTint、姓名换 angerNameColor、
        /// 双方各自的 anger 特效（face/anger、person_left|right/anger）与满怒特效（mp/eft_3）亮着。
        /// 状态直接从 angerTimer 推导（不是"谁触发了"），所以激昂怎么结束都能自己收回来；
        /// 失败方变暗的卡（收场）不吃这个色。
        /// </summary>
        /// <param name="forceAngry">
        /// 无视 angerTimer、直接按"正激昂"处理。只在爆发那一帧用（那时逻辑层还没落 angerTimer）。
        /// </param>
        protected virtual void RefreshAngerLook(int team, Debate.Character c, DebateSide side, DebateCardFace card, bool forceAngry = false)
        {
            bool angry = forceAngry || (c != null && c.angerTimer > 0);

            if (card != null && card.root != null)
            {
                // 激昂特效（美术新加的 face/anger）：随激昂开关，跟染色各管一摊
                if (card.angerFx != null) card.angerFx.SetActive(angry);

                if (!card.dimmed)
                {
                    Color tint = angry ? angerTint : Color.white;
                    if (card.frame != null) card.frame.color = tint;
                    if (card.head != null)
                    {
                        // 保留 ApplyHead 定的透明度：没有头像图时它是全透明的，别染出一个色块
                        tint.a = card.head.color.a;
                        card.head.color = tint;
                    }
                }
            }

            if (side == null) return;

            if (side.nameText != null)
                side.nameText.color = angry ? angerNameColor : DefaultTextColor(side.nameText);
            if (side.standFx != null && !side.standFx.activeSelf) side.standFx.SetActive(true);

            // 激昂特效（美术新加的 person_left|right/anger）
            if (side.angerFx != null) side.angerFx.SetActive(angry);
            // 满怒特效（mp/eft_3）：激昂期间亮，怒气满（还没进激昂）时也亮
            if (side.fullStressFx != null)
                side.fullStressFx.SetActive(angry || (c != null && c.stress >= Debate.MaxStress));
        }

        /// <summary>收场 / 新开一局时把激昂的着色与特效收干净</summary>
        protected void ClearAngerLook()
        {
            for (int i = 0; i < Debate.MaxTeamCount; i++)
            {
                DebateSide side = SideOf(i);
                if (side != null)
                {
                    if (side.nameText != null) side.nameText.color = DefaultTextColor(side.nameText);
                    if (side.angerFx != null) side.angerFx.SetActive(false);
                    if (side.fullStressFx != null) side.fullStressFx.SetActive(false);
                }

                DebateCardFace card = CardOf(i);
                if (card == null) continue;

                if (card.angerFx != null) card.angerFx.SetActive(false);
                if (card.frame != null) card.frame.color = Color.white;
                if (card.head != null)
                {
                    Color white = Color.white;
                    white.a = card.head.color.a;
                    card.head.color = white;
                }
            }
        }

        #endregion

        #region IDebateView：出牌与卡牌效果

        public virtual void DebatePlayCard(Debate debate, int team, int index)
        {
            if (debate != m_Debate) Bind(debate);

            int card = debate.GetPlayedCard(team);
            if (card == (int)DebateCard.DebateCard_Rethink)
                ShowFloat(team, PersonName(debate, team) + " " + rethinkCardName, Color.white);

            // 这一手已经出掉了：清掉手牌选中，下一合重新点
            ClearHandSelection();

            // 出牌表现：上方展示格滑进来 + 该方说一句出牌台词
            SetAnimTimer(playCardDuration);
            RefreshAll();
            PlayPlayedCardAnim(team);
            SayRandom(team, PlayLines);
        }

        public virtual void DebateAttackDraw(Debate debate, int stressDamage)
        {
            ShowCenterFloat("势均力敌", new Color(1f, 0.9f, 0.6f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateShout(Debate debate, int team, int hpDamage, int stressDamage)
        {
            // 大喝打的是对手（见 Debate.Shout：targetTeam = GetOpponentTeam(team)）
            int target = 1 - team;
            ShowFloat(target, "大喝 -" + hpDamage, new Color(1f, 0.5f, 0.3f, 1f));
            if (hpDamage > 0) PlayHurt(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateTopic(Debate debate, int team, int card, int hpDamage, int stressDamage, bool reflected)
        {
            // 被诡辩反弹时这张牌打的是自己（见 Debate.TopicCard：reflected 时 targetTeam = team）
            int target = reflected ? team : 1 - team;
            ShowFloat(target, Debate.GetCardName(card) + " -" + hpDamage, reflected
                ? new Color(0.8f, 0.6f, 1f, 1f) : new Color(1f, 0.5f, 0.3f, 1f));
            if (hpDamage > 0) PlayHurt(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateRethink(Debate debate, int team)
        {
            ShowHint("手牌已重抽");
            ShowFloat(team, "再考", new Color(0.75f, 0.9f, 1f, 1f));
            SetAnimTimer(playCardDuration);
            RefreshAll();
        }

        public virtual void DebateIgnore(Debate debate, int team, int stressDamage)
        {
            ShowFloat(team, "无视", new Color(0.8f, 0.8f, 0.8f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateCompose(Debate debate, int team, int stressDamage, bool reflected)
        {
            ShowFloat(reflected ? 1 - team : team, "镇静", new Color(0.6f, 0.9f, 1f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAgitate(Debate debate, int team, int stressDamage, bool reflected)
        {
            ShowFloat(reflected ? 1 - team : team, "激昂", new Color(1f, 0.4f, 0.4f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        #endregion

        #region IDebateView：愤怒

        public virtual void DebateAngerTrigger(Debate debate, int team, int card)
        {
            // 激昂爆发：大字 + 抖一下 + 按性格喊一句（爆发之后有一段"激昂期"，状态表现见 RefreshAngerLook）
            AnimationText banner = floatText;
            if (banner == null) banner = team == (int)DebateTeam.DebateTeam_Challenged ? floatRight : floatLeft;
            if (banner == null) banner = floatLeft != null ? floatLeft : floatRight;
            ShowFloatOn(banner, "激昂", angerTint, floatScale * angerBannerScale);

            ShakeTeam(team);
            SayAngerLine(debate, team);
            SetAnimTimer(angerBurstDuration);
            RefreshAll();

            // 逻辑层是先叫表现层（就是这里）、再落 angerTimer（CharacterSetAngerTimer 在 Anger() 里），
            // 所以这一帧 angerTimer 可能还是 0：直接按"已经激昂"把这方的着色与满怒特效亮上，
            // 不等下一帧刷新（之后的维持/收回仍由 RefreshAngerLook 按 angerTimer 自己管）。
            RefreshAngerLook(team, debate.GetCharacter(team), SideOf(team), CardOf(team), true);
        }

        public virtual void DebateAngerReckless(Debate debate, int team, int hpDamage, int stressDamage)
        {
            // 莽撞（猪突）打的是对手（见 Debate 的猪突分支：受伤的是 opponentCharacter）
            int target = 1 - team;
            ShowFloat(target, "莽撞 -" + hpDamage, new Color(1f, 0.35f, 0.3f, 1f));
            if (hpDamage > 0) PlayHurt(target);
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAngerTimid(Debate debate, int team, int hpDamage, int stressDamage, int comboIndex)
        {
            // 胆小的连续攻击同样打在对手身上（见 Debate.ComboAttack）
            int target = 1 - team;
            ShowFloat(target, "连打 -" + hpDamage, new Color(1f, 0.4f, 0.4f, 1f));
            if (hpDamage > 0) PlayHurt(target);
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

            // 追击/留情 用 PlayerChoice 问；不是玩家操作的一方（或没有选择系统）直接自动选。
            if (m_PendingCritical < 0 && !m_CriticalAsked)
            {
                m_CriticalAsked = true;
                if (!AskCriticalChoice(debate, team))
                    m_PendingCritical = (int)DebateCritical.DebateCritical_HaveMercy;
            }

            if (m_PendingCritical < 0)
            {
                ShowHint("对手已被击溃，请选择：追击 / 留情");
                return -1;
            }

            int value = m_PendingCritical;
            m_PendingCritical = -1;
            m_CriticalAsked = false;

            // 追不追、留不留：选定之后就喊一句（这是中场台词，与收场对白分开）
            SayRandom(team, value == (int)DebateCritical.DebateCritical_PushOn ? PushOnLines : HaveMercyLines);
            SetAnimTimer(effectDuration);
            return value;
        }

        /// <summary>
        /// 弹出「追击 / 留情」的选择肢。返回 false 表示没有问（不是玩家操作 / 没有 PlayerChoice），
        /// 调用方按兜底值（留情）处理。
        /// </summary>
        protected virtual bool AskCriticalChoice(Debate debate, int team)
        {
            Debate.Character c = debate != null ? debate.GetCharacter(team) : null;
            if (c == null || !c.control) return false;      // 不是玩家亲自操作：交给 AI

            PlayerChoice choice = GameSystem.GetSystem<PlayerChoice>();
            if (choice == null)
            {
                if (!m_CriticalFallbackWarned)
                {
                    m_CriticalFallbackWarned = true;
                    Sango.Log.Warning("舌战界面：PlayerChoice 不可用，追击/留情一律按「留情」结算。");
                }
                return false;
            }

            // 默认选第 0 项（留情），玩家直接关掉窗口时也走它
            PlayerChoice.ChoiceData[] choices = new PlayerChoice.ChoiceData[]
            {
                new PlayerChoice.ChoiceData
                {
                    lab = "留情",
                    call = () => m_PendingCritical = (int)DebateCritical.DebateCritical_HaveMercy
                },
                new PlayerChoice.ChoiceData
                {
                    lab = "追击",
                    call = () => m_PendingCritical = (int)DebateCritical.DebateCritical_PushOn
                },
            };
            choice.Start(choices);
            return true;
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

            // 话题特效的颜色跟着当前话题走
            RefreshTopicEft();

            RefreshSide(leftSide);
            RefreshSide(rightSide);
            RefreshCard(cardLeft);
            RefreshCard(cardRight);

            // 激昂（愤怒）的着色必须放在上面之后：RefreshSide / RefreshCard 会重设姓名、卡面与头像的颜色
            for (int i = 0; i < Debate.MaxTeamCount; i++)
                RefreshAngerLook(i, m_Debate.GetCharacter(i), SideOf(i), CardOf(i));

            int player = PlayerTeam(m_Debate);
            RefreshHand(m_Debate, leftHand.team, player);
            RefreshHand(m_Debate, rightHand.team, player);

            RefreshPlayedCard(playedCardLeft);
            RefreshPlayedCard(playedCardRight);
        }

        /// <summary>话题特效（BlowCounter_bg/GameObject/eft）随当前话题换色</summary>
        protected virtual void RefreshTopicEft()
        {
            if (topicEft == null || m_Debate == null) return;

            int topic = m_Debate.CurrentTopic;
            if (topicEftColors == null || topic < 0 || topic >= topicEftColors.Length) return;

            topicEft.color = topicEftColors[topic];
        }

        /// <summary>刷新一侧的武将与状态</summary>
        protected virtual void RefreshSide(DebateSide side)
        {
            if (side == null || m_Debate == null) return;

            Debate.Character c = m_Debate.GetCharacter(side.team);
            if (c == null) return;

            Person person = c.person;

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
            SetBar(side.hpBar, c.hp, Debate.MaxHP);
            if (side.stressText != null)
                side.stressText.text = c.stress + " / " + Debate.MaxStress;
            SetBar(side.stressBar, c.stress, Debate.MaxStress);
            if (side.playedCard != null)
                // 与出牌展示区同一个口径：对手的牌名要等双方都出完才亮（prefab 没这个节点时是空操作）
                side.playedCard.text = "本回合：" + (CanShowCardName(side.team)
                    ? CardDisplayName(m_Debate.GetPlayedCard(side.team)) : "—");

            // mp/eft_1 常驻气势；mp/eft_3（满怒特效）与激昂染色由 RefreshAngerLook 统一管（按 angerTimer）

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
        ///   · 用不了的（不是玩家操作的一方、空位、熟虑已用过）→ 不可选图，且不可点；
        ///   · 其余 → 普通图，按住时由 Selectable 的 SpriteSwap 换成按下图。
        ///
        /// 排布：格子**一律显示**（没牌就写占位字）；**最后一格固定是「熟虑」**（见 BuildSlotOrder），
        /// 其余格子放这一合的手牌（话题牌在前、话术牌在后）。玩家点的是槽位，回给逻辑层的仍是真实的手牌下标。
        ///
        /// 牌名：玩家自己写全名（"故事·大"），对手只写类别（"故事"）—— 大小是这一合要比的信息，不给对手看。
        /// </summary>
        protected virtual void RefreshHand(Debate debate, int team, int playerTeam)
        {
            DebateHand hand = HandOf(team);
            if (hand == null || debate == null || hand.cells == null) return;

            Debate.Character c = debate.GetCharacter(team);
            if (c == null) return;

            bool isPlayer = team == playerTeam;
            bool thisSideSelected = isPlayer && m_PendingCardTeam == team;

            BuildSlotOrder(hand, c);

            int lastSlot = hand.cells.Length - 1;

            for (int slot = 0; slot < hand.cells.Length; slot++)
            {
                DebateHandCell cell = hand.cells[slot];
                if (cell == null) continue;

                if (cell.root != null && !cell.root.gameObject.activeSelf)
                    cell.root.gameObject.SetActive(true);

                int cardIndex = hand.slotToHand != null && slot < hand.slotToHand.Length ? hand.slotToHand[slot] : -1;
                int card = cardIndex >= 0 && c.card != null && cardIndex < c.card.Length ? c.card[cardIndex] : -1;
                bool hasCard = card >= 0;

                if (cell.fx != null) cell.fx.SetActive(false);

                // ---- 最后一格：固定显示「熟虑」（再考）----
                // 手牌里没有它（这一合已经用过、或被移除）也照样显示，只是变"不可用"：
                // 它在手牌里恒占一格（CharacterFillCards 每次都把 card[0] 设成再考），所以位置固定最直观。
                if (slot == lastSlot)
                {
                    int rethink = (int)DebateCard.DebateCard_Rethink;
                    bool canUse = hasCard && card == rethink && debate.CanRethink(team);

                    DebateCardState rethinkState;
                    if (!canUse) rethinkState = DebateCardState.Disabled;
                    else if (thisSideSelected && cardIndex == m_PendingCard) rethinkState = DebateCardState.Highlighted;
                    else rethinkState = DebateCardState.Normal;

                    ApplyCardSkin(cell.frame, cell.toggle, rethink, rethinkState);

                    if (cell.label != null)
                    {
                        cell.label.text = rethinkCardName;
                        cell.label.color = canUse ? DefaultTextColor(cell.label) : DisabledCardTint;
                    }
                    if (cell.toggle != null) cell.toggle.interactable = canUse && isPlayer;
                    continue;
                }

                // 「再考」这一合用过就不能再用（对应 C++ can_rethink_）：
                // 不禁用的话玩家可以反复点它，出牌 → 重抽 → 再出牌，回合永远走不完。
                bool usable = hasCard && (card != (int)DebateCard.DebateCard_Rethink || debate.CanRethink(team));

                DebateCardState state;
                if (!usable || !isPlayer) state = DebateCardState.Disabled;                       // 空位 / AI 侧 / 再考用尽
                else if (thisSideSelected && cardIndex == m_PendingCard) state = DebateCardState.Highlighted;  // 玩家点中的那张
                else state = DebateCardState.Normal;

                ApplyCardSkin(cell.frame, cell.toggle, card, state);

                // 牌名：玩家全名、对手只给类别（见 HandCardLabel）；空位写占位字
                if (cell.label != null)
                {
                    cell.label.text = !hasCard ? "--"
                        : (isPlayer || !hideOpponentCardNames) ? CardDisplayName(card) : HandCardLabel(card);
                    cell.label.color = usable && isPlayer ? DefaultTextColor(cell.label) : DisabledCardTint;
                }
                if (cell.toggle != null) cell.toggle.interactable = usable && isPlayer;
            }

            // 选中态（isOn）本类已不再用于显示，但保持同步，方便美术日后改回 sel 方案
            SelectToggle(hand, thisSideSelected ? SlotOfHandIndex(hand, m_PendingCard) : -1);
        }

        /// <summary>
        /// 对手手牌上写的字：**只给类别**（"故事（大）" → "故事"）。
        /// 大小（小 / 中 / 大）是这一合要比的信息，玩家自己看得到，对手的藏起来；
        /// 类别本来就由底板图（按类型换的图）露出来了，写出来不算多给信息。
        /// 话术牌（大喝 / 诡辩 / 无视 / 镇静 / 激昂）名字里没有大小，本身就只是类型，照写。
        /// </summary>
        protected string HandCardLabel(int card)
        {
            string full = CardDisplayName(card);

            // 截到括号（或旧格式的点）为止："故事（大）" → "故事"
            int cut = full.IndexOf('（');
            if (cut < 0) cut = full.IndexOf('(');
            if (cut < 0) cut = full.IndexOf('·');
            return cut > 0 ? full.Substring(0, cut) : full;
        }

        /// <summary>
        /// 一张牌在界面上的显示名。
        /// 逻辑层的名字表里，那张牌叫"再考"，美术版界面上写的是「熟虑」（见 rethinkCardName）；
        /// 手牌末格与上方出牌展示区都走这里，免得同一样东西两处叫法不一样。
        /// </summary>
        protected string CardDisplayName(int card)
        {
            if (card == (int)DebateCard.DebateCard_Rethink) return rethinkCardName;
            return Debate.GetCardName(card);
        }

        /// <summary>
        /// 排出手牌到槽位的映射。
        /// **最后一格固定给「熟虑」（再考）**，手牌里没有它也给（slotToHand 记 -1，显示成不可用）；
        /// 其余格子按"话题牌在前、话术牌在后"排，再考本身不再占用这些格子。
        /// </summary>
        protected static void BuildSlotOrder(DebateHand hand, Debate.Character c)
        {
            if (hand.slotToHand == null || hand.slotToHand.Length != Debate.MaxCardCount)
                hand.slotToHand = new int[Debate.MaxCardCount];

            for (int i = 0; i < hand.slotToHand.Length; i++)
                hand.slotToHand[i] = -1;

            int lastSlot = hand.slotToHand.Length - 1;
            int rethinkIndex = -1;
            int n = 0;

            // 先排话题牌（再考单独记下来，最后放到末格）
            for (int i = 0; i < c.maxCardCount && i < c.card.Length; i++)
            {
                if (c.card[i] < 0) continue;
                if (c.card[i] == (int)DebateCard.DebateCard_Rethink) { rethinkIndex = i; continue; }
                if (Debate.GetCardTopic(c.card[i]) < 0) continue;
                if (n < lastSlot) hand.slotToHand[n++] = i;
            }
            // 话术牌（大喝 / 诡辩 / 无视 / 镇静 / 激昂）排在后面
            for (int i = 0; i < c.maxCardCount && i < c.card.Length; i++)
            {
                if (c.card[i] < 0) continue;
                if (c.card[i] == (int)DebateCard.DebateCard_Rethink) continue;
                if (Debate.GetCardTopic(c.card[i]) >= 0) continue;
                if (n < lastSlot) hand.slotToHand[n++] = i;
            }

            // 末格 = 熟虑（再考）。手牌里没有它就留 -1：照样显示「熟虑」，只是不可用。
            if (hand.slotToHand.Length > 0) hand.slotToHand[lastSlot] = rethinkIndex;
        }

        /// <summary>手牌下标 → 槽位（找不到返回 -1）</summary>
        protected static int SlotOfHandIndex(DebateHand hand, int cardIndex)
        {
            if (hand == null || hand.slotToHand == null || cardIndex < 0) return -1;

            for (int i = 0; i < hand.slotToHand.Length; i++)
                if (hand.slotToHand[i] == cardIndex) return i;
            return -1;
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

        /// <summary>
        /// 刷一方本回合出的牌：有牌 → 底板换成**该牌类型的高亮图**（与手牌的选中态同一套图）；
        /// 牌名则要过 <see cref="CanShowCardName"/> —— 对手出的牌只亮底板亮类型，等双方都出完才揭晓牌名。
        /// </summary>
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
                show.label.text = !has ? show.emptyText
                    : (CanShowCardName(show.team) ? CardDisplayName(card) : string.Empty);

            if (show.fx != null) show.fx.SetActive(sameTopic);
        }

        /// <summary>
        /// 这一方的牌名现在能不能亮。
        ///   · 玩家自己那边：随时能看（自己出的牌没必要藏）；
        ///   · 对手那边：只亮底板（按牌类型换图），要等**双方都出完牌**才揭晓真正的牌名 ——
        ///     所以对手先出牌时同样看不到名字（见 hideOpponentCardNames）。
        /// </summary>
        protected virtual bool CanShowCardName(int team)
        {
            if (!hideOpponentCardNames) return true;
            if (m_Debate == null) return true;
            if (team == PlayerTeam(m_Debate)) return true;
            return BothPlayed();
        }

        /// <summary>双方本回合是否都已出牌（都出完才揭晓牌名）</summary>
        protected bool BothPlayed()
        {
            if (m_Debate == null) return false;

            for (int i = 0; i < Debate.MaxTeamCount; i++)
                if (m_Debate.GetPlayedCard(i) < 0) return false;
            return true;
        }

        /// <summary>
        /// 把一侧体力/愤怒条刷成"当前值 / 上限"。
        /// 填充方式统一为**垂直、由下往上**（所有 fill 图都这么做，左右两侧一样）。
        /// </summary>
        protected static void SetBar(Image bar, int value, int max)
        {
            if (bar == null) return;

            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Vertical;
            bar.fillOrigin = (int)Image.OriginVertical.Bottom;
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

        /// <summary>受击表现：亮受击序列帧 + 抖一下 + 说一句受击台词</summary>
        protected void PlayHurt(int team)
        {
            PlayHitFx(team);
            ShakeTeam(team);
            SayRandom(team, HurtLines);
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
        /// <param name="scale">字号缩放；&lt;= 0 时用 floatScale</param>
        protected void ShowFloatOn(AnimationText target, string text, Color color, float scale = 0f)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;

            PrepareFloat(target);
            target.flipY = false;            // false = 向上飘
            target.Create(text, color, scale > 0f ? scale : floatScale);
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

        #region 台词 / 动画

        /// <summary>开场寒暄：挑战方</summary>
        protected static readonly DebateLine[] OpeningLinesChallenger =
        {
            new DebateLine("今日便要与你论个高下！", true),
            new DebateLine("久闻阁下大名，特来讨教。", false),
            new DebateLine("此事关乎大义，不得不辩。", false),
            new DebateLine("哼，凭你也配与我争？", true),
        };

        /// <summary>开场寒暄：应战方</summary>
        protected static readonly DebateLine[] OpeningLinesChallenged =
        {
            new DebateLine("正合我意，放马过来！", true),
            new DebateLine("既如此，我便洗耳恭听。", false),
            new DebateLine("口舌之利，不足为凭。", false),
            new DebateLine("大胆！竟敢在此放肆。", true),
        };

        /// <summary>出牌的随机台词</summary>
        protected static readonly DebateLine[] PlayLines =
        {
            new DebateLine("看这一手！", true),
            new DebateLine("此理昭然，何须多言。", false),
            new DebateLine("容我细说分明。", false),
            new DebateLine("如此浅见，也敢献丑？", true),
            new DebateLine("哼，不合时宜。", false),
        };

        /// <summary>受击的随机台词</summary>
        protected static readonly DebateLine[] HurtLines =
        {
            new DebateLine("唔……", false),
            new DebateLine("竟有此事……", true),
            new DebateLine("可恶！", true),
            new DebateLine("这……不可能。", false),
        };

        /// <summary>追击台词（玩家选了「追击」时喊的一句，收场对白另有一套）</summary>
        protected static readonly DebateLine[] PushOnLines =
        {
            new DebateLine("穷寇莫追？我偏要追！", true),
            new DebateLine("你已无话可说了吧！", true),
        };

        /// <summary>留情台词（玩家选了「留情」时喊的一句，收场对白另有一套）</summary>
        protected static readonly DebateLine[] HaveMercyLines =
        {
            new DebateLine("罢了，留你一命。", false),
            new DebateLine("得饶人处且饶人。", false),
        };

        /// <summary>收场对白：追击取胜的赢家</summary>
        protected static readonly DebateLine[] ClosingWinPushOnLines =
        {
            new DebateLine("哈哈哈！这般见识也敢与我争辩？", true),
            new DebateLine("既已哑口无言，便别再站在这儿了。", true),
            new DebateLine("胜负已分 —— 回去再读几年书吧。", true),
        };

        /// <summary>收场对白：留情取胜的赢家</summary>
        protected static readonly DebateLine[] ClosingWinHaveMercyLines =
        {
            new DebateLine("承让了。道理讲明，何必再争。", false),
            new DebateLine("今日到此为止，望君回去细想。", false),
            new DebateLine("胜负不过一时，还请教正。", false),
        };

        /// <summary>收场对白：落败方接的一句</summary>
        protected static readonly DebateLine[] ClosingLoseLines =
        {
            new DebateLine("……是我输了。", false),
            new DebateLine("技不如人，无话可说。", false),
            new DebateLine("今日之事，我记下了。", true),
        };

        /// <summary>收场对白：不分胜负（双方各叹一句）</summary>
        protected static readonly DebateLine[] ClosingDrawLines =
        {
            new DebateLine("今日谁也说服不了谁。", false),
            new DebateLine("罢了，各有各的道理。", false),
        };

        /// <summary>激昂台词：胆小（发作后开始连打）</summary>
        protected static readonly DebateLine[] AngerLinesTimid =
        {
            new DebateLine("别、别逼我！……我和你拼了！", true),
            new DebateLine("欺人太甚！我、我也不是好惹的！", true),
            new DebateLine("我不怕你……我一点都不怕你！", true),
        };

        /// <summary>激昂台词：冷静（怒而更冷静，会再考）</summary>
        protected static readonly DebateLine[] AngerLinesCalm =
        {
            new DebateLine("……看来不动真格是不行了。", false),
            new DebateLine("好，那就把话讲到底。", false),
            new DebateLine("失礼了 —— 接下来我不会再让。", false),
        };

        /// <summary>激昂台词：刚胆（愤怒时抢攻、封住话术）</summary>
        protected static readonly DebateLine[] AngerLinesBold =
        {
            new DebateLine("忍耐已尽，接招吧！", true),
            new DebateLine("既如此，我便不再留情。", true),
            new DebateLine("话说到这份上，不必再绕弯子了！", true),
        };

        /// <summary>激昂台词：莽撞（当场猪突猛进）</summary>
        protected static readonly DebateLine[] AngerLinesReckless =
        {
            new DebateLine("啊啊啊——看我把你驳倒！", true),
            new DebateLine("管你什么道理，先吃我一喝！", true),
            new DebateLine("废话少说，来辩！", true),
        };

        /// <summary>待播台词序列：按顺序一条一条播（双方共用一条时间线，不会互相盖住）</summary>
        protected readonly List<KeyValuePair<int, DebateLine>> m_SayQueue = new List<KeyValuePair<int, DebateLine>>();

        /// <summary>队列最多压几条，防止战况太快时台词堆积</summary>
        protected const int MaxSayQueue = 3;

        /// <summary>话题特效默认配色（下标与 Topic 一致：故事 / 道理 / 时势）</summary>
        protected static Color[] NewTopicColors()
        {
            return new Color[]
            {
                new Color(0.68f, 0.97f, 0.50f, 1f),   // 故事
                new Color(0.55f, 0.80f, 1.00f, 1f),   // 道理
                new Color(1.00f, 0.78f, 0.45f, 1f),   // 时势
            };
        }

        /// <summary>让某一方说一句台词：没人在说就立刻说，否则排队</summary>
        protected void Say(int team, DebateLine line)
        {
            if (string.IsNullOrEmpty(line.text)) return;

            DebateDialogue dlg = DialogueOf(team);
            if (dlg == null || dlg.root == null) return;

            if (!AnyDialoguePlaying())
            {
                StartDialogue(team, line);
                return;
            }

            if (m_SayQueue.Count >= MaxSayQueue) return;
            m_SayQueue.Add(new KeyValuePair<int, DebateLine>(team, line));
        }

        /// <summary>随机取一条台词来说</summary>
        protected void SayRandom(int team, DebateLine[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            Say(team, lines[UnityEngine.Random.Range(0, lines.Length)]);
        }

        /// <summary>有没有一方正在说话</summary>
        protected bool AnyDialoguePlaying()
        {
            return IsDialoguePlaying(0) || IsDialoguePlaying(1);
        }

        /// <summary>某一方的气泡是否在播</summary>
        protected bool IsDialoguePlaying(int team)
        {
            DebateDialogue dlg = DialogueOf(team);
            return dlg != null && dlg.playing;
        }

        /// <summary>开始播一条台词（进场动画 → 停留 → 退场动画）</summary>
        protected void StartDialogue(int team, DebateLine line)
        {
            DebateDialogue dlg = DialogueOf(team);
            if (dlg == null || dlg.root == null) return;

            dlg.playing = true;
            dlg.timer = 0f;
            dlg.inPhase = true;
            dlg.root.gameObject.SetActive(true);

            if (line.excited)
            {
                if (dlg.frame != null && dialogueFrameExcited != null) dlg.frame.sprite = dialogueFrameExcited;
            }
            else if (dlg.frame != null && dialogueFrameNormal != null)
            {
                dlg.frame.sprite = dialogueFrameNormal;
            }

            if (dlg.label != null) dlg.label.text = line.text;
            ApplyDialogueAlpha(dlg, 0f);
            dlg.root.localScale = dlg.homeScale * 0.85f;
        }

        /// <summary>推进一方气泡的动画</summary>
        protected void UpdateDialogue(DebateDialogue dlg, float dt)
        {
            if (dlg == null || dlg.root == null || !dlg.playing) return;

            dlg.timer += dt;
            float anim = Mathf.Max(0.01f, dialogueAnimDuration);

            if (dlg.inPhase)
            {
                float t = Mathf.Clamp01(dlg.timer / anim);
                ApplyDialogueAlpha(dlg, t);
                dlg.root.localScale = dlg.homeScale * Mathf.Lerp(0.85f, 1f, t);

                if (dlg.timer >= anim + dialogueHoldDuration)
                {
                    dlg.inPhase = false;
                    dlg.timer = 0f;
                }
                return;
            }

            float outT = Mathf.Clamp01(dlg.timer / anim);
            ApplyDialogueAlpha(dlg, 1f - outT);
            if (outT >= 1f)
            {
                dlg.playing = false;
                dlg.root.gameObject.SetActive(false);
                PlayNextQueuedLine();
            }
        }

        /// <summary>气泡整体透明度（底板 + 文字一起淡）</summary>
        protected static void ApplyDialogueAlpha(DebateDialogue dlg, float alpha)
        {
            if (dlg == null) return;

            if (dlg.frame != null)
            {
                Color c = dlg.frameHomeColor;
                c.a = dlg.frameHomeColor.a * alpha;
                dlg.frame.color = c;
            }
            if (dlg.label != null)
            {
                Color c = dlg.labelHomeColor;
                c.a = dlg.labelHomeColor.a * alpha;
                dlg.label.color = c;
            }
        }

        /// <summary>一句说完，接着播队列里的下一条</summary>
        protected void PlayNextQueuedLine()
        {
            if (m_SayQueue.Count == 0) return;

            KeyValuePair<int, DebateLine> next = m_SayQueue[0];
            m_SayQueue.RemoveAt(0);
            StartDialogue(next.Key, next.Value);
        }

        /// <summary>N 条台词一共要占多久（开场时用它决定逻辑层要等多久）</summary>
        protected float LineTotalDuration(int lineCount)
        {
            return (dialogueAnimDuration * 2f + dialogueHoldDuration) * Mathf.Max(1, lineCount);
        }

        /// <summary>按队伍取台词气泡</summary>
        protected DebateDialogue DialogueOf(int team)
        {
            if (dialogueLeft != null && dialogueLeft.team == team) return dialogueLeft;
            if (dialogueRight != null && dialogueRight.team == team) return dialogueRight;
            return null;
        }

        /// <summary>清掉所有台词（新开一局时用）</summary>
        protected void ClearDialogues()
        {
            m_SayQueue.Clear();
            ClearDialogue(dialogueLeft);
            ClearDialogue(dialogueRight);
        }

        /// <summary>收起一格气泡并还原透明度</summary>
        protected static void ClearDialogue(DebateDialogue dlg)
        {
            if (dlg == null || dlg.root == null) return;

            dlg.playing = false;
            dlg.timer = 0f;
            dlg.inPhase = true;
            dlg.root.localScale = dlg.homeScale;
            dlg.root.gameObject.SetActive(false);
            ApplyDialogueAlpha(dlg, 1f);
        }

        #region 受击抖动 / 出牌动画 / 收场动画

        /// <summary>让某一方抖一下（武将条 + 卡面一起抖）</summary>
        protected void ShakeTeam(int team)
        {
            DebateSide side = SideOf(team);
            if (side != null) ShakeNode(side.root, team * 2);

            DebateCardFace card = CardOf(team);
            if (card != null) ShakeNode(card.root, team * 2 + 1);
        }

        /// <summary>把某个节点登记为抖动目标（同一目标重复登记只重置计时）</summary>
        protected void ShakeNode(RectTransform target, int slot)
        {
            if (target == null || slot < 0 || slot >= m_ShakeTarget.Length) return;

            if (m_ShakeTarget[slot] != target)
            {
                m_ShakeTarget[slot] = target;
                m_ShakeHome[slot] = target.anchoredPosition;
            }
            m_ShakeTimer[slot] = Mathf.Max(0.01f, shakeDuration);
        }

        /// <summary>推进抖动：衰减正弦，时间到就还原位置</summary>
        protected void UpdateShakes(float dt)
        {
            for (int i = 0; i < m_ShakeTarget.Length; i++)
            {
                if (m_ShakeTimer[i] <= 0f) continue;

                RectTransform target = m_ShakeTarget[i];
                m_ShakeTimer[i] -= dt;

                if (target == null || m_ShakeTimer[i] <= 0f)
                {
                    m_ShakeTimer[i] = 0f;
                    if (target != null) target.anchoredPosition = m_ShakeHome[i];
                    continue;
                }

                float k = m_ShakeTimer[i] / Mathf.Max(0.01f, shakeDuration);   // 1 → 0
                float offset = Mathf.Sin(m_ShakeTimer[i] * 60f) * shakeStrength * k;
                target.anchoredPosition = m_ShakeHome[i] + new Vector2(offset, 0f);
            }
        }

        /// <summary>清掉抖动并还原位置</summary>
        protected void ClearShakes()
        {
            for (int i = 0; i < m_ShakeTarget.Length; i++)
            {
                m_ShakeTimer[i] = 0f;
                if (m_ShakeTarget[i] != null) m_ShakeTarget[i].anchoredPosition = m_ShakeHome[i];
            }
        }

        /// <summary>出牌展示格的出牌动画：从自己那一侧滑进来 + 弹一下</summary>
        protected void PlayPlayedCardAnim(int team)
        {
            DebatePlayedCard show = PlayedCardOf(team);
            if (show == null || show.root == null) return;

            show.root.anchoredPosition = show.home;
            show.fxTimer = Mathf.Max(0.01f, playedCardAnimDuration);
        }

        /// <summary>推进出牌展示格的动画</summary>
        protected void UpdatePlayedCardAnim(DebatePlayedCard show, float dt)
        {
            if (show == null || show.root == null || show.fxTimer <= 0f) return;

            show.fxTimer -= dt;
            float total = Mathf.Max(0.01f, playedCardAnimDuration);
            float t = 1f - Mathf.Clamp01(show.fxTimer / total);      // 0 → 1

            // 从自己那一侧滑过来（左方从更左、右方从更右），同时弹一下缩放
            // 注意乘在原始缩放上：美术把 card2 的 x 缩放设成了 -1（镜像），直接写死会把镜像弄丢
            float dir = show.team == (int)DebateTeam.DebateTeam_Challenged ? 1f : -1f;
            show.root.anchoredPosition = show.home + new Vector2(dir * 90f * (1f - t), 0f);
            float k = t < 0.7f ? Mathf.Lerp(0.75f, 1.12f, t / 0.7f) : Mathf.Lerp(1.12f, 1f, (t - 0.7f) / 0.3f);
            show.root.localScale = show.homeScale * k;

            if (show.fxTimer <= 0f)
            {
                show.fxTimer = 0f;
                show.root.anchoredPosition = show.home;
                show.root.localScale = show.homeScale;
            }
        }

        /// <summary>按队伍取出牌展示格</summary>
        protected DebatePlayedCard PlayedCardOf(int team)
        {
            if (playedCardLeft != null && playedCardLeft.team == team) return playedCardLeft;
            if (playedCardRight != null && playedCardRight.team == team) return playedCardRight;
            return null;
        }

        /// <summary>把结算大字收起来（同时停掉出场动画）</summary>
        protected void ClearResultStamp()
        {
            m_ResultStampState = 0;
            m_ResultAnimTimer = -1f;

            if (resultWin != null)
            {
                resultWin.localScale = m_ResultWinScale;
                resultWin.gameObject.SetActive(false);
            }
            if (resultLose != null)
            {
                resultLose.localScale = m_ResultLoseScale;
                resultLose.gameObject.SetActive(false);
            }
        }

        /// <summary>亮结算大字（1 胜 / -1 负），带从大到小的出场动画</summary>
        protected void ShowResultStampAnimated(int state)
        {
            ClearResultStamp();
            if (state == 0) return;

            RectTransform stamp = state > 0 ? resultWin : resultLose;
            if (stamp == null) return;

            m_ResultStampState = state;
            m_ResultAnimTimer = Mathf.Max(0.01f, resultAnimDuration);
            stamp.gameObject.SetActive(true);
            stamp.localScale = ResultScale(state) * 1.6f;
        }

        /// <summary>胜负大字的原始缩放</summary>
        protected Vector3 ResultScale(int state)
        {
            return state > 0 ? m_ResultWinScale : m_ResultLoseScale;
        }

        /// <summary>推进结算大字的出场动画</summary>
        protected void UpdateResultAnim(float dt)
        {
            if (m_ResultAnimTimer <= 0f) return;

            RectTransform stamp = m_ResultStampState > 0 ? resultWin : resultLose;
            m_ResultAnimTimer -= dt;

            if (stamp == null)
            {
                m_ResultAnimTimer = -1f;
                return;
            }

            float total = Mathf.Max(0.01f, resultAnimDuration);
            float t = 1f - Mathf.Clamp01(m_ResultAnimTimer / total);
            Vector3 home = ResultScale(m_ResultStampState);
            stamp.localScale = home * Mathf.Lerp(1.6f, 1f, t);

            if (m_ResultAnimTimer <= 0f)
            {
                m_ResultAnimTimer = -1f;
                stamp.localScale = home;
            }
        }

        /// <summary>收起/展开双方手牌（收场时收起）</summary>
        protected void SetHandsVisible(bool visible)
        {
            if (handRootLeft != null) handRootLeft.SetActive(visible);
            if (handRootRight != null) handRootRight.SetActive(visible);
        }

        /// <summary>
        /// 收场时每帧看一眼：收场对白（含排队中的）都播完了，才把结果揭晓出来。
        /// 这一段仍然靠 m_ResultOpen 把逻辑层挡在收场阶段，所以"等对白"不会让舌战提前结束。
        /// </summary>
        protected virtual void UpdateResultReveal()
        {
            if (!m_ResultWaiting) return;

            // 还有人在说话、或还有台词排着队 → 再等等，结果不能抢在对白前面
            if (AnyDialoguePlaying() || m_SayQueue.Count > 0) return;

            m_ResultWaiting = false;
            int stamp = m_ResultPending;
            m_ResultPending = 0;
            RevealResult(stamp);
        }

        /// <summary>揭晓结果：中央文字改成胜负标题、亮胜负大字、亮「退出」按钮</summary>
        protected virtual void RevealResult(int stamp)
        {
            if (m_Debate != null && topicText != null)
            {
                int winner = m_Debate.CurrentWinner;
                topicText.text = winner >= 0 ? PersonName(m_Debate, winner) + " 胜" : "不分胜负";
            }

            ShowResultStampAnimated(stamp);
            SetOutVisible(true);
            ShowHint("点「退出」离开舌战");
        }

        /// <summary>
        /// 开/关「退出」按钮。
        /// 注意要把整块容器（outRoot）一起开关：prefab 里"退出"两个字在容器上、按钮在子节点 img 上，
        /// 只关 btnOut 会剩下孤零零的两个字。
        /// </summary>
        protected void SetOutVisible(bool visible)
        {
            if (outRoot != null) outRoot.SetActive(visible);
            if (btnOut != null) btnOut.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 开/关「中止」按钮。当前需求是**全程不显示**（暂时不需要它）：
        /// 只在绑定时调用一次 SetStopVisible(false)，没有任何地方会再打开它。
        /// 同样要把整块容器（stopRoot）一起开关 —— "中止"两个字在容器上、按钮在子节点上。
        /// </summary>
        protected void SetStopVisible(bool visible)
        {
            if (stopRoot != null) stopRoot.SetActive(visible);
            if (btnClose != null) btnClose.gameObject.SetActive(visible);
        }

        /// <summary>把一张卡整体变暗/还原（失败方的卡）</summary>
        protected void SetCardDimmed(DebateCardFace card, bool dimmed)
        {
            if (card == null) return;

            card.dimmed = dimmed;
            if (card.frame != null) card.frame.color = dimmed ? loserTint : Color.white;
            if (card.head != null) card.head.color = dimmed ? loserTint : Color.white;
        }

        #endregion

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
