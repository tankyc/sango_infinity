/*
 * 文件名：CardDuelView.cs
 * 描述：单挑的"卡牌式"表现层实现（window_duel.prefab）
 *
 * 设计说明：
 *   · 本类只实现 IDuelView，是单挑逻辑层与 UI 之间的唯一桥梁，逻辑层不认识任何 UI 组件。
 *   · 节点绑定采用"按名字自动查找"，找不到的节点不会报错，只是该项表现缺失，
 *     因此美术/策划调整 prefab 结构时只要保持节点名不变即可继续工作。
 *   · 动画节奏由本类自己控制：每次表现请求压入一个计时器，计时未走完时
 *     DuelIsAnimating 返回 true，逻辑层会停在当前阶段等待。
 *   · 未来要换成 3D 表现，只需再实现一个 IDuelView（例如 DuelView3D）并在
 *     DuelManager.CreateViewHandler 里换掉即可，逻辑层零改动。
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core.Duel
{
    /// <summary>
    /// 单挑卡牌表现。
    ///
    /// 继承 Sango.UGUIWindow（项目统一的窗口基类）：
    ///   · 窗口 prefab 根节点挂上它即可被 Window.CreateWindow 认作窗口本体，
    ///     不再需要在根节点额外补一个空 UGUIWindow；
    ///   · 窗口打开时通过 OnOpen 走一遍刷新，关闭时由基类的 Close 负责隐藏。
    /// 逻辑层仍然只认识 IDuelView，换 3D 表现不受影响。
    /// </summary>
    public class CardDuelView : UGUIWindow, IDuelView
    {
        #region 单步动画时长

        /// <summary>普通步骤的停留时间（秒）</summary>
        public float stepDuration = 0.7f;

        /// <summary>一击必杀 / 退却等重要步骤的停留时间（秒）</summary>
        public float emphasisDuration = 1.4f;

        /// <summary>日志最多保留的行数</summary>
        public int maxLogLines = 60;

        /// <summary>获得增益时的提升对白只给玩家方向播（关掉则双方都播）</summary>
        public bool buffLineForPlayerOnly = true;

        /// <summary>必杀台词是否双方都播（关掉则只播玩家方向，AI 出必杀不打断节奏）</summary>
        public bool specialLineBothSides = true;

        /// <summary>开场是否播武将之间的寒暄（关掉则一开局就直接准备单挑）</summary>
        public bool openingLines = true;

        #endregion

        #region 节点引用

        [Header("左右两侧武将位：第 0 位是前台（当前出战），第 1/2 位是后台（可交替）")]
        public RectTransform[] leftCharaSlots = new RectTransform[3];
        public RectTransform[] rightCharaSlots = new RectTransform[3];

        [Header("前台武将的气力刻度（每满 100 点一个，索引 0 = keep1，索引 1 = keep2）")]
        public Image[] leftSpiritPips = new Image[2];
        public Image[] rightSpiritPips = new Image[2];

        [Header("回合与日志")]
        public Text blowCounterText;
        public Text logText;

        [Header("合数（数字图片样式：十位 / 个位，索引即数字）")]
        public Image blowCounterTen;
        public Image blowCounterOne;
        // 用全名限定：项目里存在名为 Sprite 的命名空间，直接写 Sprite 会被解析成命名空间
        public UnityEngine.Sprite[] blowCounterDigits = new UnityEngine.Sprite[10];

        [Header("左右两套命令区（右侧固定为玩家方向；左侧若也是玩家则同样可操作）")]
        public DuelCommandSide leftSide = new DuelCommandSide();
        public DuelCommandSide rightSide = new DuelCommandSide();

        /// <summary>
        /// 必杀格顺序：下标＝格子节点序号（Stance0_N），值＝DuelSpecial。
        /// 默认按当前美术排布：必杀技 / 要害 / 无双 / 伪退 / 暗器 / 坚守 / 集气 / 退却。
        /// 美术拖动格子后只需同步改这里，逻辑层不必动。
        /// </summary>
        public int[] specialCellTypes =
        {
            (int)DuelSpecial.DuelSpecial_Hissatsuwaza,  // 必杀技
            (int)DuelSpecial.DuelSpecial_Kyuusho,       // 要害
            (int)DuelSpecial.DuelSpecial_Musou,         // 无双
            (int)DuelSpecial.DuelSpecial_Nisetaikyaku,  // 伪退
            (int)DuelSpecial.DuelSpecial_Anki,          // 暗器
            (int)DuelSpecial.DuelSpecial_Kenshu,        // 坚守
            (int)DuelSpecial.DuelSpecial_Kiai,          // 集气
            (int)DuelSpecial.DuelSpecial_Taikyaku,      // 退却
        };

        [Header("底部单一按钮：决定 / 停止 / 终止 / 替换 四态合一")]
        public Button btnPlay;
        public Text skipHint;

        [Header("左右两张武将卡（CardArea/CardLeft、CardArea/CardRight）")]
        public DuelCard cardLeft = new DuelCard();
        public DuelCard cardRight = new DuelCard();

        /// <summary>
        /// 卡面头像（CardArea/CardLeft/face/head、CardArea/CardRight/face/head）取图时
        /// 传给 <see cref="GameRenderHelper.LoadHeadIcon(int, int)"/> 的**第二个参数**：
        /// 1 → Assets/Face/{武将头像编号}_1。设为 0 则用默认图（与武将条一致，即 _2）。
        /// </summary>
        public int cardHeadIconType = 1;

        // ---- 卡牌演出参数：时长 / 幅度 / 配色，都可调 ----

        /// <summary>出招前冲的时长（秒）</summary>
        public float cardAttackDuration = 0.24f;
        /// <summary>受击 / 闪避 / 登场交替的时长（秒）</summary>
        public float cardHitDuration = 0.34f;
        /// <summary>受击序列帧（face/hit）显示多久后自动隐藏（秒）</summary>
        public float hitFxDuration = 0.2f;
        /// <summary>卡面飘字的缩放（模板字号 25，1 倍即可）</summary>
        public float cardFloatScale = 1f;
        /// <summary>卡面体力低于这个比例（0.3 = 30%）时转成警示色</summary>
        [Range(0f, 1f)] public float cardLowHpRatio = 0.3f;
        /// <summary>必杀演出的时长（秒）</summary>
        public float cardSpecialDuration = 0.55f;
        /// <summary>一击必杀 / 胜负结算的时长（秒）</summary>
        public float cardResultDuration = 0.9f;
        /// <summary>出招的前冲距离（像素）</summary>
        public float cardAttackDistance = 56f;
        /// <summary>出招时的放大比例</summary>
        public float cardAttackScale = 0.14f;
        /// <summary>受击的后仰距离（像素）</summary>
        public float cardHitBack = 26f;
        /// <summary>受击的抖动幅度（像素）</summary>
        public float cardHitShake = 9f;

        /// <summary>受击时卡面闪红</summary>
        public static readonly Color CardHitTint = new Color(1f, 0.35f, 0.3f, 1f);
        /// <summary>必杀 / 一击必杀时卡面泛金</summary>
        public static readonly Color CardSpecialTint = new Color(1f, 0.85f, 0.42f, 1f);
        /// <summary>落败后卡面压暗</summary>
        public static readonly Color CardDownTint = new Color(0.42f, 0.42f, 0.48f, 1f);
        /// <summary>闪避文字的颜色</summary>
        public static readonly Color CardDodgeTint = new Color(0.75f, 0.88f, 1f, 1f);
        /// <summary>飘字：受到的伤害（沿用工程"数值减少"的橙红）</summary>
        public static readonly Color FloatDamageTint = new Color(1f, 0.479f, 0.231f, 1f);

        [Header("结算大字（CardArea/win、CardArea/lose）：按我方胜负砸下来")]
        public DuelResultStamp resultWin = new DuelResultStamp();
        public DuelResultStamp resultLose = new DuelResultStamp();

        /// <summary>砸下来的总时长（秒）：下落 → 落地压扁 → 回弹余震</summary>
        public float resultSlamDuration = 0.6f;
        /// <summary>起手高度（像素，从原位置上方多少开始落）</summary>
        public float resultSlamHeight = 640f;
        /// <summary>起手的放大倍数（落到 1 倍）</summary>
        public float resultSlamScale = 1.9f;
        /// <summary>起手的倾斜角度（度，落到 0）</summary>
        public float resultSlamAngle = 14f;

        /// <summary>上一合 BlowAnim 的命中/闪避标记（BlowAnim 不带队伍，靠下标与 HPAnim 配对）</summary>
        protected readonly int[] m_BlowValues = new int[Duel.MaxAnimQueueSize];

        /// <summary>本次体力结算里"谁被谁打了"的清单，供卡牌演出使用（ApplyHpAnim 里填）</summary>
        protected readonly List<Duel.HPAnim> m_CardHits = new List<Duel.HPAnim>();



        /// <summary>正在砸下来的结算大字</summary>
        protected DuelResultStamp m_StampShown;
        protected float m_StampTime;
        protected float m_StampDuration;

        /// <summary>
        /// 卡牌演出种类。演出只动表现（位置 / 缩放 / 配色 / 卡面文字），不改任何逻辑数据。
        /// </summary>
        public enum DuelCardFx
        {
            None = 0,
            /// <summary>出招：向对方前冲一下再回位</summary>
            Attack,
            /// <summary>受击：后仰 + 抖动 + 卡面闪红</summary>
            Hit,
            /// <summary>闪避开：原地侧闪</summary>
            Dodge,
            /// <summary>放必杀：大幅前冲 + 卡面泛金</summary>
            Special,
            /// <summary>一击必杀：胜方放大泛金（配合败方的 Down）</summary>
            Ftk,
            /// <summary>落败 / 退却：卡面压暗、下沉</summary>
            Down,
            /// <summary>平局：原地轻晃</summary>
            Draw,
            /// <summary>登场 / 交替：新卡弹出</summary>
            Swap,
        }

        /// <summary>
        /// 一张武将卡（CardArea/CardLeft、CardArea/CardRight）。
        ///
        /// 职责有两块：
        ///   1) 信息同步——把当前出战的武将刷到卡面：头像、名字、武力 / 体力 / 斗志；
        ///      名字带伤标红，明细里武力带伤标红、体力只在过低时变色（伤病不在这显示）。
        ///   2) 演出——出招、受击、闪避、必杀、负伤、胜负时播放"位置 + 缩放 + 卡面配色 + 飘字"的动画。
        ///
        /// 演出时长会压进视图的 m_AnimTimer，逻辑层因此会等这段表演播完再推进（与既有动画一致）。
        /// </summary>
        [System.Serializable]
        public class DuelCard
        {
            /// <summary>卡牌根节点（CardLeft / CardRight）</summary>
            public RectTransform root;

            /// <summary>卡面图（演出时闪色用；就是卡牌根节点上的 Image）</summary>
            public Image frame;

            /// <summary>受击的序列帧特效（face/hit）。自带 UIImageAnimation，激活即开始播</summary>
            public GameObject hitFx;

            /// <summary>卡面飘字系统（face/ani_info 上的 AnimationText）：伤害、斗气、各种演出文字都走它</summary>
            public AnimationText aniInfo;

            // ---- 以下都是运行期状态，不参与序列化 ----

            /// <summary>这张卡代表哪个阵营（左＝挑战方 0，右＝应战方 1）</summary>
            [System.NonSerialized] public int team = -1;

            /// <summary>原始位置 / 缩放 / 配色是否已经记下（演出结束要还原回去）</summary>
            [System.NonSerialized] public bool cached;
            [System.NonSerialized] public Vector2 homePos;
            [System.NonSerialized] public Vector3 homeScale = Vector3.one;
            [System.NonSerialized] public Color homeFrame = Color.white;

            /// <summary>当前演出</summary>
            [System.NonSerialized] public DuelCardFx fx = DuelCardFx.None;
            [System.NonSerialized] public float fxTime;
            [System.NonSerialized] public float fxDuration;

            /// <summary>受击序列帧还要显示多久（> 0 表示正在亮）</summary>
            [System.NonSerialized] public float hitFxTime;

            /// <summary>飘字系统是否已经做好运行时准备（maxTime / 图标子节点）</summary>
            [System.NonSerialized] public bool aniInfoReady;

            /// <summary>
            /// 绑定一张卡。节点名找不到时全部留空，后续刷新与演出自动跳过，不会报错。
            /// </summary>
            public void Bind(Transform cardRoot)
            {
                root = cardRoot as RectTransform;
                frame = root != null ? root.GetComponent<Image>() : null;

                Transform hit = root != null ? FindDeep(root, "hit") : null;
                hitFx = hit != null ? hit.gameObject : null;

                Transform ani = root != null ? FindDeep(root, "ani_info") : null;
                aniInfo = ani != null ? ani.GetComponent<AnimationText>() : null;

                cached = false;
                aniInfoReady = false;
                hitFxTime = 0f;
                fx = DuelCardFx.None;
                fxTime = 0f;
                fxDuration = 0f;
            }
        }

        /// <summary>
        /// 结算大字（CardArea/win、CardArea/lose）。
        /// 平时隐藏，结算时按胜负选一个"砸下来"：从屏幕上方落下、落地压扁、回弹余震。
        /// 落地后就一直留着，直到窗口关闭 / 下一场开始。
        /// </summary>
        [System.Serializable]
        public class DuelResultStamp
        {
            /// <summary>大字节点（win / lose）</summary>
            public RectTransform node;

            // ---- 运行期（不序列化）----
            [System.NonSerialized] public bool cached;
            [System.NonSerialized] public Vector2 homePos;
            [System.NonSerialized] public Vector3 homeScale = Vector3.one;
            [System.NonSerialized] public CanvasGroup group;

            /// <summary>绑定节点，原位置/原大小留到第一次演出时再记</summary>
            public void Bind(RectTransform target)
            {
                node = target;
                cached = false;
                group = null;
            }

            /// <summary>记下原始位置 / 大小，并补一个不吃点击的 CanvasGroup（淡入用）</summary>
            public void EnsureHome()
            {
                if (node == null || cached) return;

                homePos = node.anchoredPosition;
                homeScale = node.localScale;

                group = node.GetComponent<CanvasGroup>();
                if (group == null) group = node.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;      // 大字绝不吃点击，别挡住底下的「离开」

                cached = true;
            }

            public void Hide()
            {
                if (node != null) node.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 一套命令区。左右各一份，结构完全相同，因此可以各自独立驱动。
        ///   specialBg      必杀技按钮的外层底（进入必杀选择后与方针一起收起）
        ///   btnSpecial     必杀技按钮本体（气满 1 格后点亮，点它进入必杀选择）
        ///   stanceGroup    四条行动方针（ToggleGroup，必须选中一个）
        ///   specialPanel   八个必杀格（默认隐藏，点必杀技后才展开）
        ///   switchButtons  交替按钮（默认隐藏，点「替换」后才展开）
        ///   stanceEffects  每条方针上的选中特效（StanceN/eft，跟着选中状态开关）
        ///   specialEffect  必杀技按钮上的特效（BtnSpecial/eft，跟着按钮可用性开关）
        /// </summary>
        [System.Serializable]
        public class DuelCommandSide
        {
            /// <summary>这套命令区对应哪个阵营（左＝挑战方 0，右＝应战方 1）</summary>
            public int team;

            public GameObject root;
            public GameObject specialBg;
            public Button btnSpecial;
            public GameObject stanceGroup;
            public ToggleGroup stanceToggleGroup;
            public Toggle[] stanceToggles = new Toggle[4];
            public GameObject specialPanel;
            public Button[] specialCells = new Button[(int)DuelSpecial.DuelSpecial_Max];
            public Button[] switchButtons = new Button[3];

            /// <summary>
            /// 每一条方针上的「选中特效」——StanceN 节点下那个叫 eft 的节点（哪一条没放就是 null）。
            /// 当前选中的那条打开，其余关掉。
            /// </summary>
            public GameObject[] stanceEffects = new GameObject[4];

            /// <summary>
            /// 必杀技按钮上的特效——BtnSpecial 下那个叫 eft 的节点（没放则为 null）。
            /// 按钮可点（气满一层）时打开，不可点时关掉。
            /// </summary>
            public GameObject specialEffect;

            /// <summary>取第 index 个必杀格上的按钮</summary>
            public Button GetCell(int index)
            {
                if (specialCells == null) return null;
                return (index >= 0 && index < specialCells.Length) ? specialCells[index] : null;
            }

            /// <summary>这个按钮是不是本命令区的（prefab 的持久化回调只给按钮，得靠它反查左右）</summary>
            public bool Owns(Button button)
            {
                if (button == null) return false;
                if (button == btnSpecial) return true;

                if (specialCells != null)
                {
                    for (int i = 0; i < specialCells.Length; i++)
                        if (specialCells[i] == button) return true;
                }

                if (switchButtons != null)
                {
                    for (int i = 0; i < switchButtons.Length; i++)
                        if (switchButtons[i] == button) return true;
                }

                if (stanceToggles != null)
                {
                    for (int i = 0; i < stanceToggles.Length; i++)
                        if (stanceToggles[i] != null && stanceToggles[i].gameObject == button.gameObject) return true;
                }

                return false;
            }

            /// <summary>
            /// 绑定一套命令区。side 允许为空（美术关掉了某一侧），
            /// 为空时所有引用保持 null，后续刷新整块跳过，不会报错。
            /// troops 是这一侧武将条所在容器——交替按钮不在命令区里，而在武将条那边。
            /// </summary>
            public void Bind(Transform side, Transform troops)
            {
                if (side == null) return;

                root = side.gameObject;

                // 必杀技：外层 BtnSpecial_bg 是底，里面的 BtnSpecial 才是要点的那个
                Transform bg = Child(side, "BtnSpecial_bg");
                specialBg = bg != null ? bg.gameObject : null;
                btnSpecial = bg != null ? ButtonOn(bg, "BtnSpecial") : null;
                if (btnSpecial == null) btnSpecial = ButtonOn(side, "BtnSpecial");

                // 四条行动方针：ToggleGroup 保证必须选中其中一个
                Transform groups = Child(side, "buttons");
                stanceGroup = groups != null ? groups.gameObject : null;
                stanceToggleGroup = groups != null ? groups.GetComponent<ToggleGroup>() : null;
                stanceToggles = new Toggle[4];
                for (int i = 0; i < 4; i++)
                    stanceToggles[i] = ToggleOn(groups, "Stance" + i);

                // 每条方针下的选中特效（StanceN/eft）：没放特效的那条留 null，刷新时跳过
                stanceEffects = new GameObject[4];
                for (int i = 0; i < 4; i++)
                {
                    Transform eft = stanceToggles[i] != null ? Deep(stanceToggles[i].transform, "eft") : null;
                    stanceEffects[i] = eft != null ? eft.gameObject : null;
                }

                // 必杀技按钮上的可用特效（BtnSpecial/eft）
                Transform specialEft = btnSpecial != null ? Deep(btnSpecial.transform, "eft") : null;
                specialEffect = specialEft != null ? specialEft.gameObject : null;

                // 八个必杀格（默认隐藏，点必杀技后才展开）
                Transform cells = Child(side, "buttons_Special");
                specialPanel = cells != null ? cells.gameObject : null;
                int cellCount = (int)DuelSpecial.DuelSpecial_Max;
                specialCells = new Button[cellCount];
                for (int i = 0; i < cellCount; i++)
                    specialCells[i] = ButtonOn(cells, "Stance0_" + i);

                // 交替按钮：在武将条那一侧。左稿第一个叫 lSwitch0，右稿叫 Switch0
                switchButtons = new Button[3];
                for (int i = 0; i < 3; i++)
                {
                    switchButtons[i] = FindAnyButton(troops, "Switch" + i, "lSwitch" + i);
                    if (switchButtons[i] == null)
                        switchButtons[i] = FindAnyButton(side, "Switch" + i, "lSwitch" + i);
                }
            }

            /// <summary>直接子节点里按名字找</summary>
            private static Transform Child(Transform parent, string name)
            {
                if (parent == null) return null;
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent.GetChild(i).name == name) return parent.GetChild(i);
                }
                return null;
            }

            private static Button ButtonOn(Transform parent, string name)
            {
                Transform t = Child(parent, name);
                return t != null ? t.GetComponent<Button>() : null;
            }

            private static Toggle ToggleOn(Transform parent, string name)
            {
                Transform t = Child(parent, name);
                return t != null ? t.GetComponent<Toggle>() : null;
            }

            /// <summary>按候选名在整棵子树里找按钮（递归查找，换容器也能命中）</summary>
            private static Button FindAnyButton(Transform parent, params string[] names)
            {
                if (parent == null || names == null) return null;
                for (int i = 0; i < names.Length; i++)
                {
                    Transform found = Deep(parent, names[i]);
                    if (found == null) continue;

                    Button btn = found.GetComponent<Button>();
                    if (btn != null) return btn;
                }
                return null;
            }

            /// <summary>递归按名字查子节点</summary>
            private static Transform Deep(Transform parent, string name)
            {
                if (parent == null) return null;
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform c = parent.GetChild(i);
                    if (c.name == name) return c;

                    Transform r = Deep(c, name);
                    if (r != null) return r;
                }
                return null;
            }
        }

        /// <summary>武将头像获取钩子。项目暂未统一武将头像接口，接入后在此实现即可</summary>
        public static System.Func<Person, Texture> GetHeadTexture;

        #endregion

        #region 运行时状态

        /// <summary>当前单挑</summary>
        protected Duel m_Duel;

        /// <summary>动画计时器，> 0 表示正在"播放"</summary>
        protected float m_AnimTimer;

        /// <summary>是否处于"等待玩家下达指示"的阶段（由逻辑层的 DuelStop / DuelPlay 切换）</summary>
        protected bool m_Paused;

        /// <summary>
        /// 玩家按下了「停止」。
        /// 一次性标志：逻辑层看到后调用 DuelStop，此时立刻清空，避免下次进入指令阶段又被误判。
        /// </summary>
        protected bool m_StopPushed;

        /// <summary>玩家按下了「继续」。同样是一次性标志，由 DuelPlay 清空。</summary>
        protected bool m_PlayPushed;

        /// <summary>玩家已选择、尚未被逻辑层取走的行动方针（左右两侧各存一份的"待交付槽"）</summary>
        protected readonly int[] m_PendingStance = { -1, -1 };

        /// <summary>
        /// 玩家选定的方针。方针是"必须选中一个"的 Toggle，所以这里长期保留上次的选择；
        /// DuelGetStance 只清待交付槽，界面显示不受影响。
        /// </summary>
        protected readonly int[] m_StanceChoice = { -1, -1 };

        /// <summary>玩家已选择、尚未被逻辑层取走的必杀</summary>
        protected int m_PendingSpecial = -1;

        /// <summary>已预约必杀所属的阵营（DuelGetSpecial 要按阵营回给逻辑层）</summary>
        protected int m_PendingSpecialTeam = -1;

        /// <summary>正在做必杀选择的是哪一侧（左右各一套面板，要知道开在哪边）</summary>
        protected DuelCommandSide m_SpecialSelectingSide;

        /// <summary>玩家已选择、尚未被逻辑层取走的交替武将</summary>
        protected int m_PendingSwitchChara = -1;

        /// <summary>
        /// 开场寒暄台词。单挑开始时挑战方先说一句、应战方接一句，
        /// 两句都读完才亮出左下方针与下方「决定」，也就是"寒暄之后再准备单挑"。
        /// 各自随机取一句，想加直接往数组里加；数组为空则该方不说话。
        /// </summary>
        public static readonly string[] OpeningLinesChallenger =
        {
            "久闻阁下武艺高强，今日特来讨教！",
            "两军阵前，可敢一战？",
            "你我今日，便在此分个高下！",
        };

        /// <summary>开场寒暄：应战方接的那一句</summary>
        public static readonly string[] OpeningLinesChallenged =
        {
            "既已到此，岂有退理！",
            "求之不得！",
            "放马过来！",
        };

        /// <summary>
        /// 副将登场（助战）台词。占位符：{0} 本将名、{1} 本将字、{2} 同队主将名。
        /// 挑了引用字号的句子而这位武将没填「字」时，会改挑别的（见 JoinLine）。
        /// </summary>
        public static readonly string[] JoinLines =
        {
            "{0}前来助战！",
            "{0}来也！",
            "援军已到，{2}勿忧！",
            "某乃{0}，字{1}，特来相助！",
            "将军稍待，{0}助你一臂之力！",
            "{2}将军，{0}来助阵！",
        };

        /// <summary>结算台词：胜利者说的，连着播几句</summary>
        public static readonly string[] WinLines =
        {
            "承让了。",
            "胜负已分，退下吧。",
            "就这点本事，也敢来挑战？",
            "天下英雄，不过如此！",
        };

        /// <summary>结算台词：落败者说的</summary>
        public static readonly string[] LoseLines =
        {
            "技不如人……",
            "今日之败，来日再讨！",
            "好厉害的手段！",
        };

        /// <summary>结算台词：平局时双方各说一句</summary>
        public static readonly string[] DrawLines =
        {
            "你我棋逢对手。",
            "再打下去也难分胜负。",
        };

        /// <summary>结算时胜利者连说几句</summary>
        public int resultWinLineCount = 2;

        /// <summary>结算是否播台词（关掉则打完直接给「离开」）</summary>
        public bool resultLines = true;

        /// <summary>
        /// 取一句副将助战台词。
        /// 台词里可以引「名字 / 字 / 主将称呼」——{0} 本将名、{1} 本将字、{2} 同队主将名。
        /// 没填「字」的武将不会被挑到引用字号的句子，避免出现"某乃张飞，字张飞"这种话。
        /// </summary>
        public static string JoinLine(Person person, Person leader)
        {
            if (person == null || JoinLines == null || JoinLines.Length == 0) return string.Empty;

            bool hasNick = !string.IsNullOrEmpty(person.nickName);

            string picked = null;
            for (int i = 0; i < JoinLines.Length * 2 && picked == null; i++)
            {
                string candidate = JoinLines[UnityEngine.Random.Range(0, JoinLines.Length)];
                if (!hasNick && candidate.Contains("{1}")) continue;   // 没字号就换一句
                picked = candidate;
            }
            if (picked == null) picked = JoinLines[0];

            string name = string.IsNullOrEmpty(person.Name) ? "某" : person.Name;
            string nick = hasNick ? person.nickName : name;
            string leaderName = leader != null && !string.IsNullOrEmpty(leader.Name) ? leader.Name : "将军";
            return string.Format(picked, name, nick, leaderName);
        }

        /// <summary>从一组台词里随机取一句（空数组返回空串，PlayLine 会跳过）</summary>
        protected static string PickLine(string[] lines)
        {
            if (lines == null || lines.Length == 0) return string.Empty;
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        /// <summary>随机取一句开场寒暄（side 0 = 挑战方，1 = 应战方）</summary>
        public static string OpeningLine(int side)
        {
            string[] lines = side == 0 ? OpeningLinesChallenger : OpeningLinesChallenged;
            if (lines == null || lines.Length == 0) return string.Empty;

            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        /// <summary>气力每满这个值就点亮一个刻度</summary>
        public const int SpiritPipValue = 100;

        /// <summary>气力刻度的数量上限（keep1 / keep2）</summary>
        public const int SpiritPipMax = 2;

        /// <summary>
        /// 开局默认选中的行动方针。
        /// 方针是"必须选中一个"的 Toggle，玩家还没点过时就用这个值：
        ///   0 重视攻击 / 1 重视防御 / 2 重视斗志 / 3 重视一击
        /// 默认给「重视斗志」，开局就能攒斗志；要换直接改 Inspector 里的下拉。
        /// </summary>
        [Header("开局默认选中的行动方针")]
        public DuelStance defaultStance = DuelStance.DuelStance_Spirit;

        /// <summary>必杀名，下标与 DuelSpecial 一致</summary>
        public static readonly string[] SpecialNames =
        {
            "必杀技", "气合", "坚守", "退却", "急所", "无双", "暗器", "伪退却",
        };

        /// <summary>
        /// 必杀台词，下标与 DuelSpecial 一致（必杀技 / 气合 / 坚守 / 退却 / 急所 / 无双 / 暗器 / 伪退却）。
        /// 释放前随机挑一句弹对话框，玩家把对话框关掉之后逻辑层才结算伤害。
        /// 想加台词直接往对应那一行里加；空数组则这一手不播台词（也不会卡住推进）。
        /// </summary>
        public static readonly string[][] SpecialLines =
        {
            new string[] { "接招！", "受死！", "看招！" },                    // 0 必杀技
            new string[] { "气合——！", "气满丹田！", "蓄势！" },            // 1 气合
            new string[] { "坚守阵脚！", "休想撼动我半分！", "稳住！" },      // 2 坚守
            new string[] { "今日到此为止！", "恕不奉陪！", "先走一步！" },    // 3 退却
            new string[] { "中门大开！", "破绽就在这里！", "看准了！" },      // 4 急所
            new string[] { "无双——！！", "天下无双！", "挡我者死！" },       // 5 无双
            new string[] { "看镖！", "兵不厌诈！", "暗器伺候！" },            // 6 暗器
            new string[] { "想走？", "机会来了！", "佯退诱敌！" },            // 7 伪退却
        };

        /// <summary>已被预约的必杀即将吃掉的刻度，在气条上标成这个颜色</summary>
        public static readonly Color SpiritSpendTint = new Color(1f, 0.35f, 0.3f, 1f);

        /// <summary>受伤武将的名字与武力用红字表示（与武将情报面板共用同一个红）</summary>
        public static readonly Color InjuredTint = PersonSortFunction.InjuredTint;

        /// <summary>卡面明细里"受伤"的富文本色（与 <see cref="InjuredTint"/> 同一个红）</summary>
        public static readonly string InjuredTag = "#" + ColorUtility.ToHtmlStringRGB(InjuredTint);

        /// <summary>卡面明细里"体力过低"的警示色</summary>
        public const string LowHpTag = "#FF4D4D";

        /// <summary>必杀按钮是否被按下（逻辑层据此在下一回合进入必杀指令阶段）</summary>
        protected bool m_SpecialPushed = false;

        /// <summary>必杀取消按钮是否被按下（对应底部按钮的「终止」）</summary>
        protected bool m_SpecialCancelPushed = false;

        /// <summary>
        /// 开局那一下「决定」是否已按过。
        /// 未按过时逻辑层被压在第一个指令阶段，画面停在第 0 合等玩家发令。
        /// </summary>
        protected bool m_Started;

        /// <summary>是否处于必杀技选择状态：方针收起、八个必杀格展开、底部按钮变「终止」</summary>
        protected bool m_SpecialSelecting;

        /// <summary>是否处于替换选人状态：交替按钮展开、底部按钮变「决定」</summary>
        protected bool m_ReplaceSelecting;

        /// <summary>上一次看到的增益状态，用来识别"刚刚获得 BUFF"并播提升对白</summary>
        protected readonly bool[,] m_BuffSeen = new bool[Duel.MaxTeamCount, (int)DuelBuffType.DuelBuffType_Max];

        /// <summary>
        /// 底部那一个按钮当前承担的职责。决定 / 停止 / 终止 / 替换 四态合一，
        /// 由所处情境决定文字与点击效果。
        /// </summary>
        public enum DuelButtonAction
        {
            /// <summary>决定：开局发令 / 换人模式下确认 / 暂停且无副将时继续</summary>
            Decide = 0,
            /// <summary>停止：暂停，进入下达指示</summary>
            Stop = 1,
            /// <summary>终止：退出必杀技选择</summary>
            Terminate = 2,
            /// <summary>替换：展开交替按钮去换人</summary>
            Replace = 3,
            /// <summary>离开：单挑已分胜负，结算台词读完后按它退场</summary>
            Leave = 4,
        }

        /// <summary>底部按钮五种文字</summary>
        public const string DecideLabel = "决定";
        public const string StopLabel = "停止";
        public const string TerminateLabel = "终止";
        public const string ReplaceLabel = "替换";
        public const string LeaveLabel = "离开";

        /// <summary>日志行</summary>
        protected readonly List<string> m_LogLines = new List<string>();

        /// <summary>
        /// 武将条各文字在 prefab 里的原色。
        /// 受伤时把名字 / 武力标红，伤好了要能还原回去，所以在第一次改动之前先把原色记下来。
        /// </summary>
        protected readonly Dictionary<Text, Color> m_TextDefaultColors = new Dictionary<Text, Color>();

        /// <summary>
        /// 正在播放的武将对白条数。> 0 时 DuelIsMessageBoxVisible 返回 true，
        /// 逻辑层会停在原地等玩家点完，所以对白不会和动画抢进度。
        /// </summary>
        protected int m_LineCount;

        /// <summary>
        /// 开场寒暄是否已经走完。
        /// 没走完之前不亮出左下方针与下方「决定」——双方先把话说几句，再说"准备单挑"。
        /// </summary>
        protected bool m_IntroDone;

        /// <summary>
        /// 结算台词是否已经播完（播完底部按钮才变「离开」）。
        /// 单挑打完不再自动退场，玩家点了「离开」才收场。
        /// </summary>
        protected bool m_ResultReady;

        /// <summary>玩家已经按过「离开」，交给 DuelManager 收场</summary>
        protected bool m_LeavePushed;

        /// <summary>
        /// 已进入结算（DuelClosing 已经调过，直到下一场 Bind 才会清掉）。
        /// 命令区在这一刻就该收起，底部按钮等台词读完才换成「离开」。
        ///
        /// 注意这里**不能**用 Duel.IsFinished 判断：ClosingPhase 只写 param.result，
        /// 从不写 Duel.result，所以平局 / 退却等结局下 IsFinished 会一直是 false，
        /// 结算按钮和命令区就都不会按预期变化。
        /// </summary>
        protected bool m_ResultPending;

        /// <summary>开场寒暄是否正在播（没播之前 <see cref="CheckIntroFinished"/> 不该有反应）</summary>
        protected bool m_IntroPlaying;

        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
            AutoBind();
            BindButtons();
        }

        /// <summary>窗口打开：按当前单挑刷新一遍</summary>
        public override void OnOpen()
        {
            base.OnOpen();
            RefreshAll();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_Duel = null;
        }

        /// <summary>把表现层与某场单挑绑定</summary>
        public virtual void Bind(Duel duel)
        {
            m_Duel = duel;
            m_AnimTimer = 0f;
            m_Paused = false;
            m_PlayPushed = false;
            m_LogLines.Clear();

            // 全清一遍，避免上一场的残留影响这一场
            m_Started = false;
            m_SpecialSelecting = false;
            m_SpecialSelectingSide = null;
            m_ReplaceSelecting = false;
            m_SpecialPushed = false;
            m_SpecialCancelPushed = false;
            m_PendingSpecial = -1;
            m_PendingSpecialTeam = -1;
            m_PendingSwitchChara = -1;
            m_LineCount = 0;
            // 开场寒暄没走完之前不亮命令区；DuelOpening 会播寒暄，读完（或逻辑层开始等指令）才亮
            m_IntroDone = false;
            m_IntroPlaying = false;
            // 结算状态每一场都要重来
            m_ResultReady = false;
            m_ResultPending = false;
            m_LeavePushed = false;
            for (int i = 0; i < m_PendingStance.Length; i++) m_PendingStance[i] = -1;
            for (int i = 0; i < m_StanceChoice.Length; i++) m_StanceChoice[i] = -1;
            for (int t = 0; t < m_BuffSeen.GetLength(0); t++)
                for (int b = 0; b < m_BuffSeen.GetLength(1); b++)
                    m_BuffSeen[t, b] = false;

            // 第 0 合先在第一个指令阶段停住等玩家按「决定」，不自动开打
            m_StopPushed = true;

            // 卡牌演出状态复位：位置/缩放/配色还原成 prefab 原样，原值下次刷新时重新缓存
            ResetCardFx(cardLeft);
            ResetCardFx(cardRight);
            for (int i = 0; i < m_BlowValues.Length; i++) m_BlowValues[i] = -1;
            m_CardHits.Clear();

            // 结算大字收起（上一场可能还留着「胜 / 败」）
            HideResultStamp();

            RefreshAll();
        }

        #endregion

        #region 节点自动查找

        /// <summary>
        /// 按名字重新绑定全部节点，并把结果写回序列化字段。
        /// 编辑器排版工具（DuelWindowBuilder）会调用它，把引用固化进 prefab，
        /// 这样 Inspector 里能看到完整绑定，也不再依赖运行时查找。
        /// </summary>
        public void Rebind()
        {
            AutoBind();
        }

        /// <summary>
        /// 按名字自动查找节点。
        ///
        /// 查找刻意做得"宽松"：美术把按钮或武将位挪到别的容器下、或者给节点改名，
        /// 只要名字还在（或命中候选名），就能绑上，不必改代码。
        /// </summary>
        protected virtual void AutoBind()
        {
            Transform root = transform;

            // ---- 左右两侧武将条 ----
            Transform left = FindDeep(root, "left_top");
            Transform right = FindDeep(root, "right_top");
            leftCharaSlots = CollectCharaSlots(root, "left");
            rightCharaSlots = CollectCharaSlots(root, "right");
            leftSpiritPips = CollectSpiritPips(leftCharaSlots);
            rightSpiritPips = CollectSpiritPips(rightCharaSlots);

            // ---- 合数与战报 ----
            blowCounterText = FindText(root, "BlowCounter");
            blowCounterTen = FindComponent<Image>(FindDeep(root, "BlowCounter_ten"));
            blowCounterOne = FindComponent<Image>(FindDeep(root, "BlowCounter_one"));
            logText = FindComponent<Text>(FindDeep(root, "LogBg/log")) ?? FindText(root, "log");

            // ---- 左右两套命令区：各自独立（右侧固定玩家方向，左侧若也是玩家则同样可操作）----
            leftSide.Bind(FindDeep(root, "lfetCommand"), left);
            rightSide.Bind(FindDeep(root, "rightCommand"), right);
            // 左＝挑战方(0)，右＝应战方(1)。哪一侧归玩家管由 Duel.IsManual 决定：
            // 右侧固定是玩家方向；左侧只有在同样由玩家操作时才可点。
            leftSide.team = (int)DuelTeam.DuelTeam_Challenger;
            rightSide.team = (int)DuelTeam.DuelTeam_Challenged;

            // ---- 底部那个四态合一的按钮与操作提示 ----
            btnPlay = FindButton(root, "BtnPlayPauseSwitch") ?? FindButton(root, "BtnPlay");
            skipHint = FindComponent<Text>(FindDeep(root, "info"))
                ?? FindComponent<Text>(FindDeep(root, "SkipHint"));

            // ---- 左右两张武将卡（CardArea 下，名字保持即可，挪位置不影响） ----
            cardLeft.Bind(FindDeep(root, "CardLeft"));
            cardRight.Bind(FindDeep(root, "CardRight"));

            // ---- 结算大字（默认关闭，结算时才亮） ----
            resultWin.Bind(FindDeep(root, "win") as RectTransform);
            resultLose.Bind(FindDeep(root, "lose") as RectTransform);
        }

        /// <summary>
        /// 收集一个阵营的 3 个武将位：第 0 位主将、第 1/2 位副将。
        /// 名字在不同稿里叫法不同（person_left / person_left_lit_1），所以每位都给一串候选名。
        /// </summary>
        protected virtual RectTransform[] CollectCharaSlots(Transform root, string side)
        {
            string[] mainNames = { "person_" + side, "person" };
            string[] sub1Names = { "person_" + side + "_lit_1", "person_" + side + "_1", "person_1", "person_lit_1" };
            string[] sub2Names = { "person_" + side + "_lit_2", "person_" + side + "_2", "person_2", "person_lit_2" };

            RectTransform[] slots = new RectTransform[3];
            slots[0] = FindAny<RectTransform>(root, mainNames);
            slots[1] = FindAny<RectTransform>(root, sub1Names);
            slots[2] = FindAny<RectTransform>(root, sub2Names);
            return slots;
        }

        /// <summary>收集前台武将位上的气力刻度（keep1 / keep2）</summary>
        protected virtual Image[] CollectSpiritPips(RectTransform[] slots)
        {
            Image[] pips = new Image[2];
            RectTransform front = slots != null && slots.Length > 0 ? slots[0] : null;
            if (front == null) return pips;

            pips[0] = FindComponent<Image>(FindDeep(front, "keep1"));
            pips[1] = FindComponent<Image>(FindDeep(front, "keep2"));
            return pips;
        }

        /// <summary>按候选名依次查找，返回第一个命中的组件</summary>
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

        /// <summary>按名字在整棵树里找一个 Button</summary>
        protected static Button FindButton(Transform root, string name)
        {
            return FindComponent<Button>(FindDeep(root, name));
        }

        /// <summary>递归按名字查找子节点</summary>
        protected static Transform FindDeep(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;

            int split = path.IndexOf('/');
            if (split < 0)
            {
                if (root.name == path) return root;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform found = FindDeep(root.GetChild(i), path);
                    if (found != null) return found;
                }
                return null;
            }

            string head = path.Substring(0, split);
            string tail = path.Substring(split + 1);
            if (root.name == head)
                return FindDeep(root, tail);
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), path);
                if (found != null) return found;
            }
            return null;
        }

        protected static T FindComponent<T>(Transform t) where T : Component
        {
            return t == null ? null : t.GetComponent<T>();
        }

        protected static Text FindText(Transform root, string name)
        {
            Transform t = FindDeep(root, name);
            return t == null ? null : t.GetComponent<Text>();
        }

        #endregion

        #region 按钮事件

        /// <summary>
        /// 绑定按钮事件。
        ///
        /// prefab 上已经用持久化 onClick 接好的按钮会被跳过，避免一次点击触发两次
        /// （编辑器工具 DuelWindowBuilder 会把下面这些 On*Click 方法写进 prefab 的 onClick）。
        /// 没接的按钮在运行时补挂监听，保证界面一定可用。
        /// </summary>
        protected virtual void BindButtons()
        {
            BindSide(leftSide);
            BindSide(rightSide);

            // 底部那一个按钮：决定 / 停止 / 终止 / 替换，四态合一
            BindButton(btnPlay, OnPlayClick);
        }

        /// <summary>给一套命令区挂监听（prefab 上已有持久化绑定时自动跳过）</summary>
        protected virtual void BindSide(DuelCommandSide side)
        {
            if (side == null) return;

            // 行动方针改成了 Toggle：必须选中一个，所以用 onValueChanged 而不是 onClick。
            // 关掉的那一次回调要忽略，否则"取消选中"会被当成选了第 0 条。
            if (side.stanceToggles != null)
            {
                for (int i = 0; i < side.stanceToggles.Length; i++)
                {
                    Toggle toggle = side.stanceToggles[i];
                    if (toggle == null) continue;
                    if (toggle.onValueChanged.GetPersistentEventCount() > 0) continue;

                    int index = i;
                    DuelCommandSide owner = side;
                    toggle.onValueChanged.RemoveAllListeners();
                    toggle.onValueChanged.AddListener(on => { if (on) OnStanceClick(owner, index); });
                }
            }

            if (side.switchButtons != null)
            {
                for (int i = 0; i < side.switchButtons.Length; i++)
                {
                    int index = i;
                    DuelCommandSide owner = side;
                    BindButton(side.switchButtons[i], () => OnSwitchClick(owner, index));
                }
            }

            if (side.specialCells != null)
            {
                for (int i = 0; i < side.specialCells.Length; i++)
                {
                    int index = i;
                    DuelCommandSide owner = side;
                    BindButton(side.specialCells[i], () => OnSpecialCellClick(owner, index));
                }
            }

            DuelCommandSide ownerOfSpecial = side;
            BindButton(side.btnSpecial, () => OnSpecialClick(ownerOfSpecial));
        }

        /// <summary>从按钮反查它属于哪一套命令区（prefab 的持久化 onClick 只传按钮，得自己认主人）</summary>
        protected virtual DuelCommandSide SideOf(Button button)
        {
            if (button == null) return null;
            if (rightSide != null && rightSide.Owns(button)) return rightSide;
            if (leftSide != null && leftSide.Owns(button)) return leftSide;
            return null;
        }

        /// <summary>
        /// 挂按钮监听。
        ///
        /// prefab 上已经有**有效**的持久化 onClick 时跳过，避免一次点击触发两次；
        /// 但如果持久化回调指向的方法在当前代码里已经不存在（重构时改过方法名，
        /// prefab 里会留下死绑定：Inspector 里看着有接线，点下去却毫无反应），
        /// 就把它清掉改挂运行时的——否则这个按钮永远是死的。
        /// </summary>
        protected static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            if (button.onClick.GetPersistentEventCount() > 0 && HasValidPersistentListener(button.onClick))
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>
        /// 持久化回调是否都指向真实存在的方法。
        /// onClick 是**无参** UnityEvent，所以只在无参重载里查找；
        /// 只要有一条找不到，就判定整份绑定已失效。
        /// </summary>
        protected static bool HasValidPersistentListener(UnityEngine.Events.UnityEventBase evt)
        {
            if (evt == null) return false;

            int count = evt.GetPersistentEventCount();
            if (count <= 0) return false;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;

            for (int i = 0; i < count; i++)
            {
                UnityEngine.Object target = evt.GetPersistentTarget(i);
                if (target == null) return false;

                string method = evt.GetPersistentMethodName(i);
                if (string.IsNullOrEmpty(method)) return false;

                if (target.GetType().GetMethod(method, flags, null, System.Type.EmptyTypes, null) == null)
                    return false;
            }
            return true;
        }

        #region 按钮回调（同时供 prefab 的持久化 onClick 使用，因此必须是 public）

        /// <summary>选择某一条行动方针（方针是 Toggle，一旦选中就必然有一个生效）</summary>
        public virtual void OnStanceClick(DuelCommandSide side, int index)
        {
            if (m_Duel == null || side == null) return;

            int team = side.team;
            if (!m_Duel.IsManual(team)) return;      // 这一侧不归玩家管

            // 上界是方针的条数（DuelStance_Max = 4）。
            // 注意别拿 m_PendingStance.Length 当界——那是"阵营数"(2)，
            // 会把「重视斗志」「重视一击」这两条直接挡掉，点了没反应、界面随后还被刷回旧值。
            if (index < 0 || index >= (int)DuelStance.DuelStance_Max) return;

            SetStance(team, index);

            // 立刻同步这一侧的四条方针：选中项与它上面的特效要马上跟着走，
            // 不然得等下一次整体刷新（例如按下停止）才更新。
            RefreshStanceToggles(side, team);

            SetAnimTimer(stepDuration);
        }

        /// <summary>记下一个方针：既更新界面选中项，也放进待交付槽给逻辑层取</summary>
        protected virtual void SetStance(int team, int stance)
        {
            if (team < 0 || team >= m_StanceChoice.Length) return;
            m_StanceChoice[team] = stance;
            m_PendingStance[team] = stance;
        }

        /// <summary>
        /// 按下第 index 个「交替」按钮（index 是副将位：0 = 副将位1，1 = 副将位2）。
        ///
        /// 效果是**对调**：把这一位上的武将扶上当前出战位，原来出战的那位落到这个副将位上。
        /// 不走"记下待处理、由逻辑层的 SwitchAnim 稍后执行"那条路，
        /// 而是当场改掉逻辑层的当前出战武将，界面随即刷新；
        /// 换完之后底部按钮回到「决定」，再按一下才继续打。
        /// </summary>
        public virtual void OnSwitchClick(DuelCommandSide side, int index)
        {
            if (m_Duel == null || side == null) return;

            int team = side.team;
            if (!m_Duel.IsManual(team)) return;

            // 按钮下标是"副将位"，换算成部队里第几位武将
            int chara = GetSubSlotChara(team, index);
            if (chara < 0) return;
            if (!m_Duel.IsJoined(team, chara)) return;

            int oldChara = m_Duel.GetCurrentChara(team);
            if (chara == oldChara) return;

            // 立刻换人（ChangeCurrentChara 内部会回调 DuelChangeCurrentChara 刷新界面）
            if (!m_Duel.ChangeCurrentChara(team, chara)) return;

            // 已经换过了：清掉待处理标记，免得逻辑层的 SwitchAnim 再换一次
            m_PendingSwitchChara = -1;

            // 新上场的武将身上没带方针（默认是「重视斗志」），把玩家选的那条补给他，
            // 否则一换人方针就被换掉了。
            if (m_StanceChoice[team] >= 0)
                m_Duel.SetStance(team, chara, m_StanceChoice[team]);

            DuelSwitch(m_Duel, team, oldChara);
            SetAnimTimer(stepDuration);
        }

        /// <summary>第一个由玩家操作的阵营（没有则为 -1，"观战"模式）</summary>
        protected int GetManualTeam()
        {
            if (m_Duel == null) return -1;
            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                if (m_Duel.IsManual(i)) return i;
            }
            return -1;
        }

        /// <summary>
        /// 按下必杀。
        ///
        /// 这里只"预约"，不立即释放：
        /// 逻辑层在 TurnStartPhase 会调 CalcSpecialTry()，看到玩家按过必杀按钮就把本方排进
        /// 必杀指令阶段，也就是**下一回合**才真正释放。玩家不点则永远不会排进来。
        /// </summary>
        /// <summary>
        /// 按下某个必杀按钮（special 为 DuelSpecial 序号）。
        ///
        /// 这里只"预约"，不立即释放：逻辑层在 TurnStartPhase 会调 CalcSpecialTry()，
        /// 看到玩家按过必杀按钮就把本方排进必杀指令阶段，
        /// 也就是**下一回合**才真正释放。玩家不点则永远不会排进来。
        ///
        /// 已预约时再按同一个 = 取消；按另一个 = 改选（一次只能预约一种）。
        /// </summary>
        /// <summary>
        /// 按下必杀技按钮 → 进入**必杀技选择状态**：
        /// 必杀技底与四条方针一起收起，八个必杀格展开，底部按钮变「终止」。
        /// 前置条件是集气至少满一层（斗志 ≥ 100）。
        /// </summary>
        public virtual void OnSpecialClick(DuelCommandSide side)
        {
            if (m_Duel == null || side == null) return;

            int team = side.team;
            if (!m_Duel.IsManual(team)) return;

            // 已经在本侧的选择状态里了：再点一次当作「终止」
            if (m_SpecialSelecting && m_SpecialSelectingSide == side)
            {
                OnSpecialCancelClick();
                return;
            }

            // 集气不足一层，必杀技按钮本来就点不动，这里再兜一次
            if (!HasSpiritForSpecial(team)) return;

            m_SpecialSelecting = true;
            m_SpecialSelectingSide = side;
            m_SpecialPushed = true;      // 通知逻辑层：下一回合把本方排进必杀指令阶段

            RefreshAll();
            SetAnimTimer(stepDuration);
        }

        /// <summary>集气是否已满一层（每层 100 点）——满了必杀技按钮才可点</summary>
        public virtual bool HasSpiritForSpecial(int team)
        {
            if (m_Duel == null || team < 0) return false;
            return m_Duel.GetSpirit(team, m_Duel.GetCurrentChara(team)) >= SpiritPipValue;
        }

        /// <summary>
        /// 在一个必杀格上点了一下：记下要发哪一种，收起选择面板，进入必杀释放阶段。
        /// 格子序号经 specialCellTypes 换算成 DuelSpecial，所以美术拖动格子不必改逻辑。
        /// </summary>
        public virtual void OnSpecialCellClick(DuelCommandSide side, int cellIndex)
        {
            if (m_Duel == null || side == null) return;
            if (specialCellTypes == null || cellIndex < 0 || cellIndex >= specialCellTypes.Length) return;

            int team = side.team;
            if (!m_Duel.IsManual(team)) return;
            if (!m_SpecialSelecting || m_SpecialSelectingSide != side) return;

            int special = specialCellTypes[cellIndex];
            if (!IsSpecialSelectable(team, special)) return;   // 逻辑层不允许，点了也不算

            m_PendingSpecial = special;
            m_PendingSpecialTeam = team;
            m_SpecialSelecting = false;                        // 选完收起面板，进入释放阶段
            m_SpecialSelectingSide = null;

            RefreshAll();
            SetAnimTimer(stepDuration);
        }

        // 供 prefab 的持久化 onClick 绑定（左右两套必杀技按钮各接一个）
        public void OnLeftSpecialClick() { OnSpecialClick(leftSide); }
        public void OnRightSpecialClick() { OnSpecialClick(rightSide); }

        /// <summary>
        /// 终止必杀技选择（底部按钮的「终止」）。
        /// 必须把 m_SpecialPushed 一起收回去：否则逻辑层的 CalcSpecialTry 仍会把本方排进
        /// 必杀指令阶段，然后在里面等一个永远不会到来的确认信号，卡死。
        /// </summary>
        public virtual void OnSpecialCancelClick()
        {
            m_PendingSpecial = -1;
            m_PendingSpecialTeam = -1;
            m_SpecialPushed = false;
            m_SpecialSelecting = false;
            m_SpecialSelectingSide = null;
            m_SpecialCancelPushed = true;      // 逻辑层的 SpecialCommandPhase 靠这个回到播放态
            RefreshAll();
        }

        /// <summary>
        /// 某个必杀当前是否允许玩家选用（纯逻辑层判定）。
        /// 完全交给 IsSpecialEnabled，已含剩余次数、斗志消耗、
        /// 伪退却的合数门槛、以及禁用设置，界面不重复实现规则。
        /// </summary>
        public virtual bool IsSpecialSelectable(int team, int special)
        {
            if (m_Duel == null || team < 0) return false;
            if (special < 0 || special >= (int)DuelSpecial.DuelSpecial_Max) return false;
            return m_Duel.IsSpecialEnabled(team, m_Duel.GetCurrentChara(team), special);
        }

        /// <summary>
        /// 该必杀要吃掉几格气力（每格 SpiritPipValue=100）。
        /// 0 表示不占格——原版里只有暗器 / 伪退却是不耗斗志的（代价是要带宝物、一局一次）。
        /// </summary>
        public virtual int GetSpecialCells(int special)
        {
            if (m_Duel == null) return 0;
            if (special < 0 || special >= (int)DuelSpecial.DuelSpecial_Max) return 0;
            return m_Duel.GetSpecialSpiritCost(special) / SpiritPipValue;
        }

        /// <summary>该必杀消耗的斗志点数（界面显示用）</summary>
        public virtual int GetSpecialCost(int special)
        {
            if (m_Duel == null) return 0;
            if (special < 0 || special >= (int)DuelSpecial.DuelSpecial_Max) return 0;
            return m_Duel.GetSpecialSpiritCost(special);
        }

        /// <summary>
        /// 找出当前出战武将**第一个**可用的必杀，返回 DuelSpecial 序号；-1 表示一个都用不了。
        /// 保留给"能不能叫必杀"这类整体判断（例如外部脚本 / 教程提示）。
        /// </summary>
        public virtual int FindUsableSpecial(int team)
        {
            if (m_Duel == null || team < 0) return -1;

            for (int sp = 0; sp < (int)DuelSpecial.DuelSpecial_Max; sp++)
            {
                if (IsSpecialSelectable(team, sp))
                    return sp;
            }
            return -1;
        }

        /// <summary>
        /// 底部那一个按钮：决定 / 停止 / 终止 / 替换 四态合一，按当前情境决定文字与点击效果。
        ///   开局（尚未发令）        决定 → 开始单挑
        ///   播放中                  停止 → 暂停，进入下达指示
        ///   暂停后（有副将）        替换 → 展开交替按钮去换人
        ///   暂停后（无副将）/换人中 决定 → 继续
        ///   必杀技选择中            终止 → 退出必杀选择
        /// </summary>
        public virtual void OnPlayClick()
        {
            if (m_Duel == null) return;

            switch (GetButtonAction())
            {
                case DuelButtonAction.Stop:
                    m_StopPushed = true;
                    break;

                case DuelButtonAction.Terminate:
                    OnSpecialCancelClick();
                    return;

                case DuelButtonAction.Replace:
                    // 有副将时这个按钮兼任「停止」：先让逻辑层停下来，再展开交替按钮
                    if (!m_Paused) m_StopPushed = true;
                    m_ReplaceSelecting = true;
                    RefreshAll();
                    break;

                case DuelButtonAction.Leave:
                    // 结算看完了，玩家要退场：置位后由 DuelManager 收场并关掉窗口
                    m_LeavePushed = true;
                    break;

                default:   // Decide
                    // 换人模式中按决定 = 收起交替按钮并继续；开局按决定 = 发令开打
                    m_ReplaceSelecting = false;
                    m_Started = true;
                    m_PlayPushed = true;
                    RefreshAll();
                    break;
            }

            SetAnimTimer(stepDuration);
        }

        /// <summary>
        /// 底部按钮此刻该担什么职责（是否在选必杀 / 是否暂停 / 有没有副将 / 是否已分胜负）。
        ///
        /// 有副将可换时，播放中的按钮直接就是「替换」——它兼任「停止」：
        /// 按下去先让逻辑层停下，同时展开交替按钮，一步到位，不用再先停一下再换。
        /// 单挑分出胜负后只剩「离开」。
        /// </summary>
        public virtual DuelButtonAction GetButtonAction()
        {
            // 结算台词已经读完：只剩「离开」，点了才退场
            if (m_ResultReady) return DuelButtonAction.Leave;

            if (m_SpecialSelecting) return DuelButtonAction.Terminate;
            if (m_ReplaceSelecting) return DuelButtonAction.Decide;

            bool hasSub = HasBackstage(GetManualTeam());

            // 播放中：有副将就只给「替换」（＝停止＋替换），没有副将才给「停止」
            if (!m_Paused)
            {
                if (!m_Started) return DuelButtonAction.Decide;   // 开局发令
                return hasSub ? DuelButtonAction.Replace : DuelButtonAction.Stop;
            }

            // 暂停中：有副将可换就给「替换」，否则给「决定」继续
            return hasSub ? DuelButtonAction.Replace : DuelButtonAction.Decide;
        }

        /// <summary>底部按钮此刻该显示的文字</summary>
        public virtual string GetButtonLabel()
        {
            switch (GetButtonAction())
            {
                case DuelButtonAction.Stop: return StopLabel;
                case DuelButtonAction.Terminate: return TerminateLabel;
                case DuelButtonAction.Replace: return ReplaceLabel;
                case DuelButtonAction.Leave: return LeaveLabel;
                default: return DecideLabel;
            }
        }

        /// <summary>副将位的个数：一队最多 3 人，减去当前出战的 1 位</summary>
        public const int SubSlotCount = Duel.MaxTeamCharaCount - 1;

        /// <summary>
        /// 第 slot 个「副将位」上站着的是部队里第几位武将。
        ///
        /// 副将位 = 武将条里**当前出战之外**的那几个位置（第 1、2 位），
        /// 顺序与武将条同一个来源（<see cref="FillOrderedChara"/>），所以两边不会错位。
        /// 未入场的副将不占位，只有真正排在那一格上的武将才能被换上场。
        /// </summary>
        protected virtual int GetSubSlotChara(int team, int slot)
        {
            if (m_Duel == null || team < 0) return -1;
            if (slot < 0 || slot >= SubSlotCount) return -1;

            int[] order = new int[Duel.MaxTeamCharaCount];
            int count = FillOrderedChara(team, order);

            // 第 0 位是当前出战，副将位从第 1 位算起
            if (slot + 1 >= count) return -1;
            return order[slot + 1];
        }

        /// <summary>
        /// 第 slot 个副将位上的武将此刻能否换上场。
        /// 必须已经入场（未入场的副将按原版规则不参战）、且还没倒下——
        /// IsJoined 对「未参战 / 已倒下」都返回 false，所以一个判定就够。
        /// </summary>
        public virtual bool CanSwitchTo(int team, int slot)
        {
            int member = GetSubSlotChara(team, slot);
            if (member < 0) return false;
            return m_Duel.IsJoined(team, member);
        }

        /// <summary>某个阵营是否还有可以交替上场的武将（决定底部按钮给「替换」还是「停止 / 决定」）</summary>
        public virtual bool HasBackstage(int team)
        {
            if (m_Duel == null || team < 0) return false;
            for (int i = 0; i < SubSlotCount; i++)
            {
                if (CanSwitchTo(team, i)) return true;
            }
            return false;
        }

        #endregion

        #endregion

        #region 计时与刷新

        protected virtual void Update()
        {
            if (m_AnimTimer > 0f && !m_Paused)
                m_AnimTimer -= Time.deltaTime;

            // 卡牌演出逐帧推进。演出是纯表现，暂停中也要把已经开始的那段播完，
            // 否则停在指令阶段时卡牌会卡在半路上。
            TickCards(Time.deltaTime);
            TickResultStamp(Time.deltaTime);
        }

        protected void SetAnimTimer(float duration)
        {
            if (duration > m_AnimTimer)
                m_AnimTimer = duration;
        }

        protected void AppendLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            m_LogLines.Add(text);
            while (m_LogLines.Count > maxLogLines)
                m_LogLines.RemoveAt(0);

            if (logText != null)
                logText.text = string.Join("\n", m_LogLines.ToArray());
        }

        /// <summary>刷新全部界面</summary>
        protected virtual void RefreshAll()
        {
            RefreshBlowCounter();
            RefreshCharaPanels();
            RefreshCommandPanel();
        }

        protected virtual void RefreshBlowCounter()
        {
            if (m_Duel == null) return;

            int blow = m_Duel.BlowCounter;

            // 样式一：文本
            if (blowCounterText != null)
            {
                // 只输出合数本身，单位「合」由界面上的图片承担（与原版一致）
                blowCounterText.text = blow.ToString();
                return;
            }

            // 样式二：数字图片（原版做法，十位 + 个位两张图）
            bool hasDigits = blowCounterDigits != null && blowCounterDigits.Length >= 10;

            if (blowCounterTen != null)
            {
                bool showTen = hasDigits && blow >= 10;
                blowCounterTen.gameObject.SetActive(showTen);
                if (showTen) blowCounterTen.sprite = blowCounterDigits[(blow / 10) % 10];
            }

            if (blowCounterOne != null)
            {
                blowCounterOne.gameObject.SetActive(hasDigits);
                if (hasDigits) blowCounterOne.sprite = blowCounterDigits[blow % 10];
            }
        }

        /// <summary>
        /// 刷新左右两侧的武将呈现：武将条 + 卡牌。
        /// 两者是同一份数据的两种画法，所有调用点都希望它们一起更新，所以收在一处。
        /// </summary>
        protected virtual void RefreshCharaPanels()
        {
            if (m_Duel == null) return;
            RefreshTeamPanel((int)DuelTeam.DuelTeam_Challenger, leftCharaSlots);
            RefreshTeamPanel((int)DuelTeam.DuelTeam_Challenged, rightCharaSlots);
            RefreshCards();
        }

        /// <summary>
        /// 按「当前出战 + 其余已入场武将（部队顺序）」填出武将位，返回有效个数。
        ///
        /// 这个顺序既是武将条的排布，也是「交替」按钮的下标：
        ///   第 0 位 = 当前出战（自己不用换自己），第 1 位 = 副将位1，第 2 位 = 副将位2。
        ///
        /// 未入场的副将**不占位置**——按原版规则他还没参战，本来就不该出现在武将条上。
        /// 否则前面挂着一个没入场的副将时，已经上过场的那位会被顶到后面去
        /// （"换下场回不到第一个副将位"就是这么来的）。
        /// </summary>
        protected virtual int FillOrderedChara(int team, int[] order)
        {
            if (m_Duel == null || order == null) return 0;

            int count = 0;
            int current = m_Duel.GetCurrentChara(team);

            // 当前出战的那位永远排第一；哪怕已经倒下，也要把他的状态显示出来
            if (current >= 0 && current < Duel.MaxTeamCharaCount && count < order.Length)
                order[count++] = current;

            for (int i = 0; i < Duel.MaxTeamCharaCount && count < order.Length; i++)
            {
                if (i == current) continue;
                if (m_Duel.GetPerson(team, i) == null) continue;   // 这一位没有武将
                if (!m_Duel.IsJoined(team, i)) continue;           // 还没入场：不占副将位
                order[count++] = i;
            }
            return count;
        }

        /// <summary>
        /// 刷新一个阵营。
        /// 第 0 位（最上面那块）显示**当前出战**的武将，第 1 / 2 位依次是其余已入场的武将；
        /// 顺序与「交替」按钮完全一致（都走 <see cref="FillOrderedChara"/>），
        /// 所以哪个武将在哪个副将位、用哪个按钮换他，两边永远对得上。
        /// </summary>
        protected virtual void RefreshTeamPanel(int team, RectTransform[] slots)
        {
            if (slots == null || m_Duel == null) return;

            Duel.Team teamData = m_Duel.GetTeam(team);

            int[] order = new int[Duel.MaxTeamCharaCount];
            int count = FillOrderedChara(team, order);

            for (int s = 0; s < slots.Length; s++)
            {
                RectTransform slot = slots[s];
                if (slot == null) continue;

                if (s >= count)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                int chara = order[s];
                Person person = teamData != null ? teamData.chara[chara].person : null;
                bool onStage = (s == 0);

                bool joined = person != null;
                slot.gameObject.SetActive(joined);
                if (!joined) continue;

                // 后台武将不做压暗处理：副将的名字 / 数值要和出战武将一样清楚
                CanvasGroup group = slot.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = 1f;

                // 名字 / 武力 / 头像。
                // 带伤（进场前就带伤，或单挑中被必杀打伤）→ 名字与武力标红；
                // 伤病在单挑中会变，而每次变化都会走 DuelHpAnim → RefreshCharaPanels，所以负伤当场就能看到。
                bool injured = m_Duel.TeamGetShoubyou(teamData, chara) > (int)Shoubyou.Kenkou;
                SetText(slot, "name", person.Name, injured);
                SetText(slot, "Strength", "武力 " + person.Strength, injured);
                ApplyHead(slot, person);

                // 生命
                int hp = m_Duel.GetHp(team, chara);
                SetBar(slot, "hp", hp, Duel.MaxHP);
                SetBarText(slot, "hp", hp.ToString());

                // 气：只有前台武将显示（后台没有气力刻度）
                if (onStage)
                {
                    int spirit = m_Duel.GetSpirit(team, chara);
                    SetBar(slot, "mp", SpiritBarValue(spirit), SpiritPipValue);
                    SetBarText(slot, "mp", spirit.ToString());

                    // 已预约必杀时，把"这一下要花掉哪几格"提前标出来
                    ApplySpiritPips(GetSpiritPips(team), spirit, GetSpiritSpendPreview(team));
                }
            }
        }

        /// <summary>取某一方的气力刻度图片（0 = 挑战方/左，1 = 应战方/右）</summary>
        protected Image[] GetSpiritPips(int team)
        {
            return team == (int)DuelTeam.DuelTeam_Challenger ? leftSpiritPips : rightSpiritPips;
        }

        /// <summary>
        /// 气条应当显示的数值。
        /// 每满 100 点就点亮一个刻度并把气条清空，所以气条只显示不足 100 的余数；
        /// 刻度最多 2 个，因此 300 气 = 2 个刻度 + 一条满的气条（300 − 200 = 100）。
        /// </summary>
        protected static int SpiritBarValue(int spirit)
        {
            int pips = Mathf.Clamp(spirit / SpiritPipValue, 0, SpiritPipMax);
            return spirit - pips * SpiritPipValue;
        }

        /// <summary>
        /// 已预约的必杀将要从**本方**气力里吃掉多少点；没预约、或问的不是玩家那一方则返回 0。
        /// 用来在气条上预览"这一下要花掉几格"。
        /// </summary>
        public virtual int GetSpiritSpendPreview(int team)
        {
            if (m_PendingSpecial < 0) return 0;
            if (team != GetManualTeam()) return 0;
            return GetSpecialCost(m_PendingSpecial);
        }

        /// <summary>
        /// 按气力点亮 keep1 / keep2。
        /// spend 是即将被必杀吃掉的点数（0 表示没有预约）；
        /// 会被吃掉的那些刻度染成警示色，让玩家一眼看出"这一下花掉几格"。
        /// </summary>
        protected static void ApplySpiritPips(Image[] pips, int spirit, int spend = 0)
        {
            if (pips == null) return;

            int pipCount = Mathf.Clamp(spirit / SpiritPipValue, 0, SpiritPipMax);
            int remainCount = Mathf.Clamp((spirit - spend) / SpiritPipValue, 0, SpiritPipMax);

            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;

                bool lit = i < pipCount;
                pips[i].gameObject.SetActive(lit);
                if (lit)
                    pips[i].color = i < remainCount ? Color.white : SpiritSpendTint;
            }
        }

        /// <summary>
        /// 设置武将头像；没有贴图时置为全透明，避免渲染成白色实心块。
        ///
        /// headIconType &gt; 0 时用它当 <see cref="GameRenderHelper.LoadHeadIcon(int, int)"/> 的第二个参数
        /// （卡面 CardLeft/face/head、CardRight/face/head 走 1 号图），否则用默认图（武将条）。
        /// </summary>
        protected static void ApplyHead(Transform slot, Person person, int headIconType = 0)
        {
            RawImage head = FindComponent<RawImage>(FindDeep(slot, "head"));
            if (head == null) return;

            Texture texture = null;
            if (GetHeadTexture != null)
                texture = GetHeadTexture(person);
            else if (person != null)
                texture = headIconType > 0
                    ? GameRenderHelper.LoadHeadIcon(person.headIconID, headIconType)
                    : GameRenderHelper.LoadHeadIcon(person.headIconID);

            head.texture = texture;
            head.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        protected static void SetText(Transform slot, string childName, string value)
        {
            if (string.IsNullOrEmpty(childName)) return;
            Text text = FindText(slot, childName);
            if (text != null) text.text = value;
        }

        /// <summary>
        /// 设置文字并按伤病状态标色。
        ///
        /// 受伤（无论进场前就带伤，还是单挑中被必杀打伤）的武将，名字与武力都用 <see cref="InjuredTint"/> 红字，
        /// 一眼能看出"这人带着伤在打"；伤好（或数据复位）时还原 prefab 原本的颜色。
        /// </summary>
        protected void SetText(Transform slot, string childName, string value, bool injured)
        {
            if (string.IsNullOrEmpty(childName)) return;

            Text text = FindText(slot, childName);
            if (text == null) return;

            text.text = value ?? string.Empty;

            Color def;
            if (!m_TextDefaultColors.TryGetValue(text, out def))
            {
                def = text.color;                       // 第一次改动前先记下 prefab 的原色
                m_TextDefaultColors[text] = def;
            }
            text.color = injured ? InjuredTint : def;
        }

        /// <summary>
        /// 设置 hp / mp 条。
        /// 条的结构约定为"条槽 + 填充"：
        ///   · 填充子节点叫 fill 时自左向右涨（左侧阵营），叫 fill_r 时自右向左涨（右侧阵营）；
        ///   · 都找不到时退回与条同名的子节点、再退回第一个非 txt 子节点（兼容旧美术）。
        /// 填充优先用 Image 的 Filled 模式（才能真正从右往左裁），无 Image 时退化为改锚点宽度。
        /// </summary>
        protected static void SetBar(Transform slot, string barName, int value, int max)
        {
            Transform bar = FindDeep(slot, barName);
            if (bar == null) return;

            bool fromRight = false;
            Transform fill = FindChild(bar, "fill");
            if (fill == null)
            {
                fill = FindChild(bar, "fill_r");
                if (fill != null) fromRight = true;
            }
            if (fill == null) fill = FindChild(bar, barName);
            if (fill == null)
            {
                for (int i = 0; i < bar.childCount; i++)
                {
                    if (bar.GetChild(i).name != "txt") { fill = bar.GetChild(i); break; }
                }
            }
            if (fill == null) return;

            float ratio = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;

            Image img = fill.GetComponent<Image>();
            if (img != null)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = fromRight
                    ? (int)Image.OriginHorizontal.Right
                    : (int)Image.OriginHorizontal.Left;
                img.fillAmount = ratio;
                return;
            }

            RectTransform rect = fill as RectTransform;
            if (rect == null) return;
            rect.anchorMin = new Vector2(fromRight ? 1f - ratio : 0f, 0f);
            rect.anchorMax = new Vector2(fromRight ? 1f : ratio, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>查找直接子节点</summary>
        protected static Transform FindChild(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }
            return null;
        }

        protected static void SetBarText(Transform slot, string barName, string value)
        {
            Transform bar = FindDeep(slot, barName);
            if (bar == null) return;
            Text text = FindText(bar, "txt");
            if (text != null) text.text = value;
        }

        /// <summary>
        /// 刷新两套命令区与底部按钮。
        ///
        /// 显隐规则：
        ///   开场寒暄还没走完 → 命令区一律不亮（先让武将把话说完，再准备单挑）；
        ///   某一侧归玩家管（Duel.IsManual(team)）→ 该侧按钮常显；
        ///   不归玩家管 → 该侧命令区整块收起，只留下武将条给对方看；
        ///   正在选必杀的那一侧：必杀技底与方针收起，八个必杀格展开；
        ///   交替按钮平时藏起来，点了「替换」才展开，且只显示真实存在的副将个数。
        /// </summary>
        /// <summary>
        /// 命令区（方针 / 必杀技 / 交替按钮）此刻该不该亮：
        /// 开场寒暄得先说完，而且单挑不能已经进入结算（打完了没什么可下令的）。
        /// </summary>
        protected virtual bool CommandUIVisible
        {
            get { return m_IntroDone && !m_ResultPending; }
        }

        /// <summary>
        /// 底部那一个按钮此刻该不该显示：
        /// 平时按命令区规则；进入结算后只在**台词读完**（可离开）时显示，此时它的文字是「离开」。
        /// </summary>
        protected virtual bool PlayButtonVisible
        {
            get
            {
                if (!m_IntroDone || m_Duel == null) return false;
                if (!m_ResultPending) return true;
                return m_ResultReady;
            }
        }

        protected virtual void RefreshCommandPanel()
        {
            if (m_Duel == null) return;

            bool anyManual = m_Duel.IsManual();

            // 寒暄期间什么都不给点；打完进入结算时只留「离开」
            bool ready = anyManual && PlayButtonVisible;

            RefreshSide(leftSide);
            RefreshSide(rightSide);

            if (skipHint != null)
                skipHint.gameObject.SetActive(ready);

            // 底部那一个按钮：四态合一，文字跟着情境走
            if (btnPlay != null)
            {
                btnPlay.gameObject.SetActive(ready);
                btnPlay.interactable = ready;

                Text label = FindComponent<Text>(FindDeep(btnPlay.transform, "Label"));
                if (label != null) label.text = GetButtonLabel();
            }
        }

        /// <summary>刷新一套命令区（左右共用同一套规则）</summary>
        protected virtual void RefreshSide(DuelCommandSide side)
        {
            if (side == null || side.root == null) return;

            int team = side.team;
            bool manual = m_Duel.IsManual(team);

            // 必杀技选择状态只可能出现在归玩家管的那一侧
            bool selecting = m_SpecialSelecting && m_SpecialSelectingSide == side;

            // 不归玩家管 / 开场寒暄还没走完 / 已进入结算 → 整块收起
            // （必杀技、四条方针、八个必杀格一并隐藏）
            side.root.SetActive(manual && CommandUIVisible);

            // 行动方针：进入必杀选择后收起
            if (side.stanceGroup != null) side.stanceGroup.SetActive(!selecting);
            if (side.stanceToggles != null)
            {
                for (int i = 0; i < side.stanceToggles.Length; i++)
                {
                    if (side.stanceToggles[i] != null) side.stanceToggles[i].interactable = manual && !selecting;
                }
                RefreshStanceToggles(side, team);
            }

            // 必杀技按钮：进选择状态时连外层的底一起收起
            if (side.specialBg != null) side.specialBg.SetActive(!selecting);
            if (side.btnSpecial != null)
            {
                side.btnSpecial.gameObject.SetActive(!selecting);
                RefreshSpecialButtonVisual(side, team, selecting);
            }

            // 八个必杀格：只有进入选择状态的那一侧才展开
            if (side.specialPanel != null) side.specialPanel.SetActive(selecting);
            if (selecting) RefreshSpecialCells(side, team);

            // 交替按钮：点了「替换」才展开，且只显示真实存在的副将
            if (side.switchButtons != null)
            {
                for (int i = 0; i < side.switchButtons.Length; i++)
                {
                    Button btn = side.switchButtons[i];
                    if (btn == null) continue;

                    bool usable = manual && m_ReplaceSelecting && CanSwitchTo(team, i);
                    btn.gameObject.SetActive(usable);
                    btn.interactable = usable;
                }
            }
        }

        /// <summary>
        /// 方针是 Toggle，必须选中一个。还没选过时默认选中第一条并同步给逻辑层，
        /// 之后以玩家的点击为准（m_PendingStance 是权威）。
        /// </summary>
        protected virtual void RefreshStanceToggles(DuelCommandSide side, int team)
        {
            if (side.stanceToggles == null) return;
            if (team < 0 || team >= m_PendingStance.Length) return;

            int stance = m_StanceChoice[team];
            if (stance < 0)
            {
                stance = (int)defaultStance;
                SetStance(team, stance);
            }

            for (int i = 0; i < side.stanceToggles.Length; i++)
            {
                Toggle toggle = side.stanceToggles[i];
                if (toggle == null) continue;

                // 用 SetIsOnWithoutNotify：否则反手触发 onValueChanged，把方针又写回去
                toggle.SetIsOnWithoutNotify(i == stance);

                // 选中特效：跟着这一条方针的选中状态开关
                if (side.stanceEffects != null && i < side.stanceEffects.Length
                    && side.stanceEffects[i] != null)
                    side.stanceEffects[i].SetActive(i == stance);
            }
        }

        /// <summary>
        /// 必杀技按钮只有两态：能点 / 不能点，全靠 Button 的 interactable。
        /// 集气满一层以上、且这一侧归玩家管时点亮，其余情况置灰不可点。
        /// 外观交给 Button 组件自己表现（美术配好的 disabled 色），脚本不换图也不改色。
        /// </summary>
        protected virtual void RefreshSpecialButtonVisual(DuelCommandSide side, int team, bool selecting)
        {
            if (side.btnSpecial == null) return;

            side.btnSpecial.interactable = !selecting && m_Duel.IsManual(team) && HasSpiritForSpecial(team);

            // 按钮上挂的特效跟着"能不能点"走：气满一层亮起来，用掉/不足就关掉
            // （整个按钮被收起时特效是它的子节点，自然也看不见）
            if (side.specialEffect != null)
                side.specialEffect.SetActive(side.btnSpecial.interactable);
        }

        /// <summary>
        /// 八个必杀格同样只有两态：能点 / 不能点，全靠各自的 interactable。
        /// 格数门槛（斗志 100=1 格 / 200=2 格 / 300=3 格）、暗器与伪退却的宝物 / 合数条件
        /// 都由逻辑层的 IsSpecialEnabled 判定，界面不重复实现，也不动美术挑的底板。
        /// </summary>
        protected virtual void RefreshSpecialCells(DuelCommandSide side, int team)
        {
            if (side.specialCells == null || specialCellTypes == null) return;

            for (int cell = 0; cell < side.specialCells.Length && cell < specialCellTypes.Length; cell++)
            {
                Button btn = side.specialCells[cell];
                if (btn == null) continue;

                btn.interactable = IsSpecialSelectable(team, specialCellTypes[cell]);
            }
        }

        #endregion

        #region 卡牌表现（信息同步 + 演出）

        /// <summary>取某一方的卡牌（左＝挑战方 0，右＝应战方 1）</summary>
        protected DuelCard CardOf(int team)
        {
            if (team == (int)DuelTeam.DuelTeam_Challenger) return cardLeft;
            if (team == (int)DuelTeam.DuelTeam_Challenged) return cardRight;
            return null;
        }

        /// <summary>记下卡牌的原始位置 / 缩放 / 配色——演出结束要还原回这一套，所以只能记一次</summary>
        protected virtual void EnsureCardHome(DuelCard card)
        {
            if (card == null || card.root == null || card.cached) return;

            card.homePos = card.root.anchoredPosition;
            card.homeScale = card.root.localScale;
            if (card.frame != null) card.homeFrame = card.frame.color;

            // 受击序列帧在 prefab 里是**开着**的（UIImageAnimation 靠 OnEnable 起播），
            // 所以第一次刷新就要收起来，之后只在挨打的那一刻亮 0.2 秒。
            if (card.hitFx != null) card.hitFx.SetActive(false);
            card.hitFxTime = 0f;

            card.cached = true;
        }

        /// <summary>把卡牌复位（换场用）：还原位置 / 缩放 / 配色并清掉正在播的演出</summary>
        protected virtual void ResetCardFx(DuelCard card)
        {
            if (card == null) return;

            if (card.cached && card.root != null)
            {
                card.root.anchoredPosition = card.homePos;
                card.root.localScale = card.homeScale;
                if (card.frame != null) card.frame.color = card.homeFrame;
            }

            // 把飘字与受击特效一起收掉，别把上一场的残留带到下一场
            if (card.aniInfo != null) card.aniInfo.Clear();
            if (card.hitFx != null) card.hitFx.SetActive(false);
            card.hitFxTime = 0f;

            card.cached = false;
            card.fx = DuelCardFx.None;
            card.fxTime = 0f;
            card.fxDuration = 0f;
        }

        /// <summary>同步左右两张卡的信息</summary>
        protected virtual void RefreshCards()
        {
            RefreshCard(cardLeft, (int)DuelTeam.DuelTeam_Challenger);
            RefreshCard(cardRight, (int)DuelTeam.DuelTeam_Challenged);
        }

        /// <summary>把某一方当前出战的武将刷到卡面：头像、名字、武力 / 体力 / 斗志</summary>
        protected virtual void RefreshCard(DuelCard card, int team)
        {
            if (card == null || card.root == null || m_Duel == null) return;

            card.team = team;
            EnsureCardHome(card);

            int chara = m_Duel.GetCurrentChara(team);
            Person person = m_Duel.GetCurrentPerson(team);

            // 用**单挑侧**的伤病等级：person.injury 要等结算才写回，
            // 单挑中挨必杀打出来的当场伤只有这里是最新的（-1 表示不在场）。
            int shoubyou = m_Duel.TeamGetShoubyou(m_Duel.GetTeam(team), chara);
            bool injured = shoubyou > (int)Shoubyou.Kenkou;

            // 名字仍是整块标红（单独节点）；明细块内部各字段自己带颜色标签，
            // 所以这块 Text 本身的颜色用原色即可，别让它一红到底。
            SetText(card.root, "name", person != null ? person.Name : string.Empty, injured);
            SetText(card.root, "detail",
                BuildCardDetail(team, chara, person, injured, DefaultTag(card.root, "detail")), false);
            ApplyHead(card.root, person, cardHeadIconType);     // 卡面头像走 1 号图（见 cardHeadIconType）
        }

        /// <summary>
        /// 卡面明细：武力 / 体力 / 斗志。颜色全部写在串里的富文本标签上，互不牵连：
        ///   武力——受伤时红字（与武将条同一口径）；
        ///   体力——平时与其它文字同色，**只在"体力过低"时才换色**：低于
        ///         <see cref="cardLowHpRatio"/> 的比例（默认 30%）即转警示色；
        ///   斗志——固定本色。
        /// 伤病不在这块显示：带伤由名字与武力的红字表达。
        /// </summary>
        protected virtual string BuildCardDetail(int team, int chara, Person person, bool injured, string baseTag)
        {
            if (person == null || m_Duel == null) return string.Empty;

            // 阈值先取整再比：MaxHP * 0.3f 是 30.000002，直接比会把"正好 30%"也算成过低
            int hp = m_Duel.GetHp(team, chara);
            bool lowHp = hp < Mathf.RoundToInt(Duel.MaxHP * cardLowHpRatio);

            return "武力 " + ColorTag(person.Strength.ToString(), injured ? InjuredTag : baseTag)
                + "\n体力 " + ColorTag(hp + " / " + Duel.MaxHP, lowHp ? LowHpTag : baseTag)
                + "\n斗志 " + ColorTag(m_Duel.GetSpirit(team, chara).ToString(), baseTag);
        }

        /// <summary>给一段文字套上富文本颜色标签（卡面这几块 Text 都勾了 Rich Text）</summary>
        protected static string ColorTag(string text, string color)
        {
            return "<color=" + color + ">" + text + "</color>";
        }

        /// <summary>
        /// 取某个文本在 prefab 里的原色，转成富文本标签（"#RRGGBB"）。
        /// 这样"本色"跟着美术调，不会因为代码里写死一个白而和旁边的字对不上。
        /// </summary>
        protected string DefaultTag(Transform root, string childName)
        {
            Text text = FindText(root, childName);
            if (text == null) return "#FFFFFF";

            Color color;
            if (!m_TextDefaultColors.TryGetValue(text, out color))
            {
                color = text.color;
                m_TextDefaultColors[text] = color;
            }
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        /// <summary>
        /// 播放一段卡牌演出。时长会压进 m_AnimTimer，所以逻辑层会停下来等这段表演播完
        /// （与既有的 stepDuration / emphasisDuration 同一套机制）。
        /// </summary>
        protected virtual void PlayCardFx(DuelCard card, DuelCardFx fx, float duration, Color infoTint, string infoText)
        {
            if (card == null || card.root == null) return;

            EnsureCardHome(card);

            card.fx = fx;
            card.fxTime = 0f;
            card.fxDuration = Mathf.Max(0.01f, duration);

            // 演出文字交给卡面的飘字系统（ani_info），它自己管淡入淡出与回收
            if (!string.IsNullOrEmpty(infoText))
                ShowCardFloat(card, infoText, infoTint);

            SetAnimTimer(card.fxDuration);
        }

        /// <summary>逐帧推进两张卡的演出（卡牌动作 + 受击序列帧）</summary>
        protected virtual void TickCards(float deltaTime)
        {
            TickCard(cardLeft, 1f, deltaTime);      // 左卡朝右（对方）冲
            TickCard(cardRight, -1f, deltaTime);

            TickHitFx(cardLeft, deltaTime);
            TickHitFx(cardRight, deltaTime);
        }

        /// <summary>受击序列帧：亮 hitFxDuration 秒后自动隐藏</summary>
        protected virtual void TickHitFx(DuelCard card, float deltaTime)
        {
            if (card == null || card.hitFx == null || card.hitFxTime <= 0f) return;

            card.hitFxTime -= deltaTime;
            if (card.hitFxTime <= 0f)
            {
                card.hitFxTime = 0f;
                card.hitFx.SetActive(false);
            }
        }

        /// <summary>
        /// 显示一次受击序列帧（face/hit）。节点一激活，UIImageAnimation 就会自己起播，
        /// 所以这里只负责"亮"和记时，隐藏交给 TickHitFx。
        /// </summary>
        protected virtual void PlayHitFx(DuelCard card)
        {
            if (card == null || card.hitFx == null) return;

            card.hitFx.SetActive(true);
            card.hitFxTime = Mathf.Max(0.01f, hitFxDuration);
        }

        /// <summary>
        /// 卡面飘字。用的是工程通用组件 AnimationText（face/ani_info 上挂的那个），
        /// 默认向上飘，颜色与缩放由调用方给。
        /// </summary>
        protected virtual void ShowCardFloat(DuelCard card, string text, Color color)
        {
            if (card == null || card.aniInfo == null) return;
            if (string.IsNullOrEmpty(text)) return;

            PrepareAniInfo(card);

            card.aniInfo.flipY = false;         // false = 向上飘（offsetCurveY 是 0 → 40）
            card.aniInfo.Create(text, color, cardFloatScale);
        }

        /// <summary>
        /// 飘字组件的运行时准备，只做一次：
        ///
        ///   1) maxTime 是 [NonSerialized]，运行时只会拿到默认值 1，而曲线跑到 3 秒 ——
        ///      不按曲线重算的话，飘字会在半路被回收（编辑器的 Inspector 就是这么算的）。
        ///   2) 组件生成飘字副本时会取 label 的**第 0 个子节点**当"图标位"（工程惯例是给
        ///      数字配上升 / 下降箭头和兵种图标），当前 prefab 的 label 没有子节点，
        ///      直接调 Create 会抛 IndexOutOfRange，所以缺了就补一个空 Text 上去。
        /// </summary>
        protected virtual void PrepareAniInfo(DuelCard card)
        {
            if (card == null || card.aniInfo == null || card.aniInfoReady) return;

            AnimationText ani = card.aniInfo;

            ani.maxTime = Mathf.Max(
                CurveEnd(ani.offsetCurveX), CurveEnd(ani.offsetCurveY),
                CurveEnd(ani.alphaCurve), CurveEnd(ani.scaleCurve));

            Text template = ani.label;
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

            card.aniInfoReady = true;
        }

        /// <summary>取曲线的最后一个关键帧时间（飘字总时长）</summary>
        protected static float CurveEnd(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            return curve.keys[curve.length - 1].time;
        }

        /// <summary>某一位武将是不是这一方当前出战的那位（飘字只飘在当值武将的卡上）</summary>
        protected virtual bool IsCurrentChara(int team, int chara)
        {
            return m_Duel != null && team >= 0 && chara >= 0 && m_Duel.GetCurrentChara(team) == chara;
        }

        /// <summary>
        /// 推进一张卡。所有演出都是"位置 + 缩放 + 卡面配色 + 中央文字"四种通道的组合，
        /// dir 是"朝对方"的方向（左卡 +1、右卡 -1）。
        /// </summary>
        protected virtual void TickCard(DuelCard card, float dir, float deltaTime)
        {
            if (card == null || card.root == null) return;
            EnsureCardHome(card);
            if (card.fx == DuelCardFx.None) return;

            card.fxTime += deltaTime;
            float t = card.fxDuration > 0f ? Mathf.Clamp01(card.fxTime / card.fxDuration) : 1f;

            Vector2 pos = card.homePos;
            Vector3 scale = card.homeScale;
            Color frame = card.homeFrame;

            switch (card.fx)
            {
                case DuelCardFx.Attack:
                    {
                        // 前 35% 冲出去，之后收回来
                        float k = t < 0.35f
                            ? Mathf.Sin(t / 0.35f * Mathf.PI * 0.5f)
                            : Mathf.Cos((t - 0.35f) / 0.65f * Mathf.PI * 0.5f);
                        pos += new Vector2(cardAttackDistance * dir * k, 0f);
                        scale *= 1f + cardAttackScale * k;
                        break;
                    }
                case DuelCardFx.Special:
                    {
                        float k = t < 0.3f
                            ? Mathf.Sin(t / 0.3f * Mathf.PI * 0.5f)
                            : Mathf.Cos((t - 0.3f) / 0.7f * Mathf.PI * 0.5f);
                        pos += new Vector2(cardAttackDistance * 1.6f * dir * k, cardAttackDistance * 0.25f * k);
                        scale *= 1f + cardAttackScale * 2.2f * k;
                        frame = Color.Lerp(card.homeFrame, CardSpecialTint, k);
                        break;
                    }
                case DuelCardFx.Hit:
                    {
                        float k = Mathf.Sin(t * Mathf.PI);          // 0 → 1 → 0
                        pos += new Vector2(-cardHitBack * dir * k, 0f);
                        pos += new Vector2(Mathf.Sin(t * 42f) * cardHitShake * k, 0f);
                        scale *= 1f - 0.06f * k;
                        frame = Color.Lerp(card.homeFrame, CardHitTint, k);
                        break;
                    }
                case DuelCardFx.Dodge:
                    {
                        float k = Mathf.Sin(t * Mathf.PI);
                        pos += new Vector2(-cardHitBack * 0.6f * dir * k, cardHitShake * 1.4f * k);
                        break;
                    }
                case DuelCardFx.Ftk:
                    {
                        float k = t < 0.25f ? Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f) : 1f;
                        pos += new Vector2(cardAttackDistance * 1.8f * dir * k, cardAttackDistance * 0.3f * k);
                        scale *= 1f + cardAttackScale * 3f * k;
                        frame = Color.Lerp(card.homeFrame, CardSpecialTint, k * 0.9f);
                        break;
                    }
                case DuelCardFx.Down:
                    {
                        float k = Mathf.Clamp01(t * 1.6f);
                        pos += new Vector2(-cardHitBack * 0.5f * dir * k, -26f * k);
                        scale *= 1f - 0.12f * k;
                        frame = Color.Lerp(card.homeFrame, CardDownTint, k);
                        break;
                    }
                case DuelCardFx.Draw:
                    {
                        pos += new Vector2(0f, cardHitShake * Mathf.Sin(t * Mathf.PI * 2f) * 0.6f);
                        break;
                    }
                case DuelCardFx.Swap:
                    {
                        float k = Mathf.Sin(t * Mathf.PI);
                        scale *= 1f + 0.18f * k;
                        frame = Color.Lerp(card.homeFrame, Color.white, k * 0.5f);
                        break;
                    }
            }

            card.root.anchoredPosition = pos;
            card.root.localScale = scale;
            if (card.frame != null) card.frame.color = frame;

            // 演出结束：复位（演出文字由 ani_info 的飘字系统自己回收，这里不用管）
            if (t >= 1f)
            {
                card.fx = DuelCardFx.None;
                card.fxTime = 0f;
                card.fxDuration = 0f;
                card.root.anchoredPosition = card.homePos;
                card.root.localScale = card.homeScale;
                if (card.frame != null) card.frame.color = card.homeFrame;
            }
        }

        /// <summary>
        /// 把这一合的体力结算翻译成卡牌演出：
        ///   命中 → 攻方出招，守方受击（序列帧特效 + 伤害飘字，挨了必杀再飘一个「负伤」）；
        ///   被闪避 → 守方原地侧闪、攻方空挥一下。
        /// </summary>
        protected virtual void PlayCardHitFx()
        {
            if (m_Duel == null || m_CardHits.Count == 0) return;

            for (int i = 0; i < m_CardHits.Count; i++)
            {
                Duel.HPAnim hit = m_CardHits[i];
                bool dodged = i < m_BlowValues.Length && m_BlowValues[i] == 0;

                PlayCardFx(CardOf(hit.atkTeam), DuelCardFx.Attack, cardAttackDuration, Color.white, null);

                DuelCard defCard = CardOf(hit.defTeam);

                if (dodged)
                {
                    PlayCardFx(defCard, DuelCardFx.Dodge, cardHitDuration, CardDodgeTint, "闪避");
                    continue;
                }

                PlayCardFx(defCard, DuelCardFx.Hit, cardHitDuration, CardHitTint, null);

                // 受击序列帧：亮 hitFxDuration 秒
                PlayHitFx(defCard);

                // 飘字只飘在"当值武将"的卡上——挨打的若不是当前出战，飘上去会张冠李戴。
                // 一次挨打只飘一条（伤害与负伤合并）：飘字起点是同一个点，
                // 两条同时飘会完全叠在一起，看不清也没法分辨。
                if (!IsCurrentChara(hit.defTeam, hit.defChara)) continue;

                bool wounded = hit.shoubyouDamage > 0;
                string text = hit.damage > 0 ? "-" + hit.damage : null;
                if (wounded) text = string.IsNullOrEmpty(text) ? "负伤" : text + " 负伤";

                if (!string.IsNullOrEmpty(text))
                    ShowCardFloat(defCard, text, wounded ? InjuredTint : FloatDamageTint);
            }

            for (int i = 0; i < m_BlowValues.Length; i++) m_BlowValues[i] = -1;
            m_CardHits.Clear();
        }

        #endregion

        #region 结算大字（win / lose）

        /// <summary>
        /// 玩家这一侧（归玩家操作的那一方）。两边都不归玩家（纯 AI，理论上不会带界面）时按挑战方算。
        /// </summary>
        protected virtual int PlayerTeam()
        {
            if (m_Duel == null) return (int)DuelTeam.DuelTeam_Challenger;
            if (m_Duel.IsManual((int)DuelTeam.DuelTeam_Challenger)) return (int)DuelTeam.DuelTeam_Challenger;
            if (m_Duel.IsManual((int)DuelTeam.DuelTeam_Challenged)) return (int)DuelTeam.DuelTeam_Challenged;
            return (int)DuelTeam.DuelTeam_Challenger;
        }

        /// <summary>收起结算大字（换场、平局时用）</summary>
        protected virtual void HideResultStamp()
        {
            if (resultWin != null) resultWin.Hide();
            if (resultLose != null) resultLose.Hide();
            m_StampShown = null;
            m_StampTime = 0f;
            m_StampDuration = 0f;
        }

        /// <summary>
        /// 结算时按"我方胜负"选一个大字砸下来（win / lose）。
        /// 平局或胜负未定时两个都不显示，让卡牌上的「平局」去交代结果。
        /// </summary>
        protected virtual void PlayResultStamp(bool win)
        {
            DuelResultStamp show = win ? resultWin : resultLose;
            DuelResultStamp hide = win ? resultLose : resultWin;

            if (hide != null) hide.Hide();
            if (show == null || show.node == null) return;

            show.EnsureHome();
            show.node.gameObject.SetActive(true);
            if (show.group != null) show.group.alpha = 0f;

            // 起手：从高处落下、放大、略带倾斜
            show.node.anchoredPosition = show.homePos + new Vector2(0f, resultSlamHeight);
            show.node.localScale = show.homeScale * resultSlamScale;
            show.node.localEulerAngles = new Vector3(0f, 0f, resultSlamAngle);

            m_StampShown = show;
            m_StampTime = 0f;
            m_StampDuration = Mathf.Max(0.01f, resultSlamDuration);

            // 让逻辑层等这一下砸完
            SetAnimTimer(m_StampDuration);
        }

        /// <summary>
        /// 推进结算大字：0~62% 加速下落 → 62~78% 落地压扁 → 78~100% 回弹余震。
        /// 播完停在原位不再动，一直留到关窗 / 下一场。
        /// </summary>
        protected virtual void TickResultStamp(float deltaTime)
        {
            DuelResultStamp stamp = m_StampShown;
            if (stamp == null || stamp.node == null) return;

            stamp.EnsureHome();

            m_StampTime += deltaTime;
            float t = m_StampDuration > 0f ? Mathf.Clamp01(m_StampTime / m_StampDuration) : 1f;

            const float fallEnd = 0.62f;        // 下落结束
            const float squashEnd = 0.78f;      // 落地压扁结束

            Vector2 pos = stamp.homePos;
            Vector3 scale = stamp.homeScale;
            float angle = 0f;

            if (t < fallEnd)
            {
                float k = t / fallEnd;
                float ease = k * k;                                     // 越落越快
                pos += new Vector2(0f, resultSlamHeight * (1f - ease));
                scale = stamp.homeScale * Mathf.Lerp(resultSlamScale, 1.05f, ease);
                angle = resultSlamAngle * (1f - ease);
            }
            else if (t < squashEnd)
            {
                float k = (t - fallEnd) / (squashEnd - fallEnd);
                scale = new Vector3(
                    stamp.homeScale.x * Mathf.Lerp(1.05f, 1.12f, k),
                    stamp.homeScale.y * Mathf.Lerp(1.05f, 0.86f, k),
                    stamp.homeScale.z);
            }
            else
            {
                float k = Mathf.Clamp01((t - squashEnd) / Mathf.Max(0.0001f, 1f - squashEnd));
                float wobble = Mathf.Sin(k * Mathf.PI * 2f) * (1f - k) * 9f;   // 回弹余震
                scale = new Vector3(
                    stamp.homeScale.x * Mathf.Lerp(1.12f, 1f, k),
                    stamp.homeScale.y * Mathf.Lerp(0.86f, 1f, k),
                    stamp.homeScale.z);
                pos += new Vector2(0f, wobble);
            }

            stamp.node.anchoredPosition = pos;
            stamp.node.localScale = scale;
            stamp.node.localEulerAngles = new Vector3(0f, 0f, angle);

            // 前 18% 快速淡入
            if (stamp.group != null)
                stamp.group.alpha = Mathf.Clamp01(t / 0.18f);
        }

        #endregion

        #region IDuelView：表现

        public virtual bool DuelIsAnimating(Duel duel)
        {
            if (duel == null) return false;
            if (duel != m_Duel) Bind(duel);
            return m_AnimTimer > 0f;
        }

        public virtual bool DuelIsMessageBoxVisible(Duel duel)
        {
            return m_LineCount > 0;
        }

        /// <summary>
        /// 播一段武将对白。用 ClickPersonSay 风格（带人物立绘），玩家点一下继续。
        /// 播放期间 DuelIsMessageBoxVisible 返回 true，逻辑层会停在原地等玩家点完。
        /// </summary>
        public virtual void PlayLine(Person person, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (GameDialog.Instance == null)
            {
                // 没有对话框系统时退化为写战报，绝不阻塞推进
                AppendLog(text);
                return;
            }

            // sure / cancel 挂同一个回调，并用一次性标记保证只减一次：
            // 任何一种关闭方式漏掉都会让 m_LineCount 一直大于 0，整场单挑就此卡住。
            bool done = false;
            System.Action onDialogClosed = () =>
            {
                if (done) return;
                done = true;
                m_LineCount = Mathf.Max(0, m_LineCount - 1);
                CheckIntroFinished();     // 最后一句开场寒暄可能就是这一句
                CheckResultFinished();    // 最后一句结算台词也可能是这一句
            };

            m_LineCount++;
            GameDialog.Instance.Open(
                GameDialog.DialogStyle.ClickPersonSay,
                text,
                onDialogClosed,
                onDialogClosed,
                person);

            // 对话框窗口没真的开出来（缺 prefab / 没有 UI 上下文）→ 把计数退回去，
            // 否则 DuelIsMessageBoxVisible 会一直是 true，逻辑层就永远停在原地
            // 等一个玩家根本点不到的框——整场单挑会卡死。
            if (!GameDialog.Instance.IsDialogAlive() && !done)
            {
                done = true;
                m_LineCount = Mathf.Max(0, m_LineCount - 1);
                AppendLog(text);      // 框没出来，至少把这句话留进战报
            }
        }

        public virtual void DuelResetAnim(Duel duel)
        {
            m_AnimTimer = 0f;
        }

        public virtual void DuelUpdateBlowCounter(Duel duel)
        {
            if (duel != m_Duel) Bind(duel);
            RefreshBlowCounter();
        }

        public virtual void DuelOpening(Duel duel)
        {
            if (duel != m_Duel) Bind(duel);
            AppendLog("—— 单挑开始 ——");

            // 开场寒暄：双方各说一句，全部读完才亮出左下方针与下方「决定」。
            // 没配台词 / 没有对话框系统时这里会返回 0（PlayLine 退化成写战报），直接进入准备。
            if (PlayOpeningLines() == 0)
                FinishIntro();

            SetAnimTimer(emphasisDuration);
        }

        /// <summary>
        /// 播开场寒暄，返回**实际弹出了几条对话框**。
        /// 用 m_LineCount 的增量来数：PlayLine 在没有对话框系统时会退化成写战报，不产生对话框，
        /// 那种情况下不能让界面一直等一句永远等不到的"关闭"。
        /// </summary>
        protected virtual int PlayOpeningLines()
        {
            if (!openingLines || m_Duel == null) return 0;

            m_IntroPlaying = true;
            int before = m_LineCount;

            int challengerTeam = (int)DuelTeam.DuelTeam_Challenger;
            int challengedTeam = (int)DuelTeam.DuelTeam_Challenged;
            PlayLine(m_Duel.GetPerson(challengerTeam, m_Duel.GetCurrentChara(challengerTeam)), OpeningLine(0));
            PlayLine(m_Duel.GetPerson(challengedTeam, m_Duel.GetCurrentChara(challengedTeam)), OpeningLine(1));

            return m_LineCount - before;
        }

        /// <summary>开场寒暄读完了没有；读完就把命令区亮出来</summary>
        protected virtual void CheckIntroFinished()
        {
            if (!m_IntroPlaying) return;      // 寒暄还没开始播，别被别的台词顺手触发了
            if (m_IntroDone) return;
            if (m_LineCount > 0) return;
            FinishIntro();
        }

        /// <summary>结束开场寒暄：亮出命令区，开始准备单挑</summary>
        protected virtual void FinishIntro()
        {
            m_IntroDone = true;
            RefreshAll();
        }

        /// <summary>
        /// 单挑结束：**不自动退场**。先把结算台词播完（胜利者几句 + 落败者一句），
        /// 台词读完底部按钮才变成「离开」，玩家点了才真正收场（由 DuelManager 关窗口）。
        /// </summary>
        public virtual void DuelClosing(Duel duel)
        {
            AppendLog("—— 单挑结束 ——");

            // 进入结算：命令区立刻收起，底部按钮等台词读完再换成「离开」
            m_ResultPending = true;
            m_ResultReady = false;
            m_LeavePushed = false;
            if (PlayResultLines() == 0)
                FinishResult();          // 没台词可播（或没有对话框系统）→ 直接给「离开」

            // 卡牌演出：败方压暗下沉、胜方亮一下；中间再砸一个结算大字（按我方胜负）
            int winner = m_Duel != null ? m_Duel.WinnerTeam : -1;
            if (m_Duel != null && winner >= 0 && winner < Duel.MaxTeamCount)
            {
                PlayCardFx(CardOf(winner), DuelCardFx.Swap, cardResultDuration, CardSpecialTint, "胜");
                PlayCardFx(CardOf(m_Duel.GetOpponentTeam(winner)), DuelCardFx.Down, cardResultDuration, CardDownTint, "败");
                PlayResultStamp(winner == PlayerTeam());
            }
            else
            {
                HideResultStamp();          // 平局 / 胜负未定：不砸大字
            }

            SetAnimTimer(emphasisDuration);
            RefreshAll();
        }

        /// <summary>
        /// 播结算台词，返回实际弹出的对话框条数（0 表示没播，调用方直接进结算态）。
        /// 胜负已定：胜利者连说几句、落败者应一句；平局则双方各一句。
        /// </summary>
        protected virtual int PlayResultLines()
        {
            if (!resultLines || m_Duel == null) return 0;

            int before = m_LineCount;

            int winner = m_Duel.WinnerTeam;
            if (winner < 0 || winner >= Duel.MaxTeamCount)
            {
                PlayLine(m_Duel.GetCurrentPerson((int)DuelTeam.DuelTeam_Challenger), PickLine(DrawLines));
                PlayLine(m_Duel.GetCurrentPerson((int)DuelTeam.DuelTeam_Challenged), PickLine(DrawLines));
                return m_LineCount - before;
            }

            int loser = winner == (int)DuelTeam.DuelTeam_Challenger
                ? (int)DuelTeam.DuelTeam_Challenged
                : (int)DuelTeam.DuelTeam_Challenger;

            for (int i = 0; i < resultWinLineCount; i++)
                PlayLine(m_Duel.GetCurrentPerson(winner), PickLine(WinLines));
            PlayLine(m_Duel.GetCurrentPerson(loser), PickLine(LoseLines));

            return m_LineCount - before;
        }

        /// <summary>结算台词读完了没有；读完就把底部按钮换成「离开」</summary>
        protected virtual void CheckResultFinished()
        {
            if (m_ResultReady) return;
            // 只有"已经进入结算"时才轮到这里——
            // 否则别的台词（开场寒暄 / 必杀 / 助战）读完也会被误判成可以离开。
            if (!m_ResultPending) return;
            if (m_LineCount > 0) return;
            FinishResult();
        }

        /// <summary>进入「可离开」状态：底部按钮变「离开」，等玩家点它退场</summary>
        protected virtual void FinishResult()
        {
            m_ResultReady = true;
            RefreshAll();
        }

        /// <summary>玩家是否按过「离开」（DuelManager 据此收场并关窗口）</summary>
        public virtual bool DuelIsLeavePushed(Duel duel)
        {
            return m_LeavePushed;
        }

        public virtual void DuelDraw(Duel duel)
        {
            AppendLog("双方不分胜负。");

            // 卡牌演出：两边都轻轻晃一下，中央亮「平局」
            PlayCardFx(cardLeft, DuelCardFx.Draw, cardResultDuration, CardSpecialTint, "平局");
            PlayCardFx(cardRight, DuelCardFx.Draw, cardResultDuration, CardSpecialTint, "平局");

            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelRetreat(Duel duel)
        {
            AppendLog("有人退却了。");

            // 退却方 = 非胜方；胜负还没落定时（两边都退）就都标一下
            int winner = m_Duel != null ? m_Duel.WinnerTeam : -1;
            if (winner >= 0 && winner < Duel.MaxTeamCount)
            {
                PlayCardFx(CardOf(m_Duel.GetOpponentTeam(winner)), DuelCardFx.Down, cardResultDuration, CardDodgeTint, "退却");
                PlayCardFx(CardOf(winner), DuelCardFx.Swap, cardHitDuration, CardSpecialTint, "进逼");
            }
            else
            {
                PlayCardFx(cardLeft, DuelCardFx.Down, cardResultDuration, CardDodgeTint, "退却");
                PlayCardFx(cardRight, DuelCardFx.Down, cardResultDuration, CardDodgeTint, "退却");
            }

            SetAnimTimer(emphasisDuration);
        }

        /// <summary>
        /// 播放合数动画。
        ///
        /// 注意：带表现层时逻辑层的 PlayBlowAnim 只把队列交过来，**自己不结算**，
        /// 由表现层逐条 ApplyBlowAnim 应用（无表现层时才走逻辑层那段）。下面三个动画同理。
        /// </summary>
        public virtual void DuelBlowAnim(Duel duel, Duel.BlowAnim[] queue, int count)
        {
            // 先记下"这一合是命中还是闪避"：BlowAnim 里没有攻守双方，
            // 紧接着的 DuelHpAnim 才带队伍信息，而两张队列是同一循环生成的、下标一一对应。
            for (int i = 0; i < count && queue != null && i < queue.Length && i < m_BlowValues.Length; i++)
                m_BlowValues[i] = queue[i] != null ? queue[i].value : -1;

            ApplyBlowAnim(queue, count);
            RefreshBlowCounter();
            RefreshCharaPanels();
            CheckBuffLines();
            SetAnimTimer(stepDuration);
        }

        /// <summary>
        /// 识别"刚获得 BUFF"并播一段提升对白。
        ///
        /// 逻辑层没有专门的增益回调（只有清除用的 DuelResetBuff），
        /// 所以在每次行动结算后比对一次快照：从没有变有的那一下就是要的事件。
        /// </summary>
        protected virtual void CheckBuffLines()
        {
            if (m_Duel == null) return;

            for (int team = 0; team < Duel.MaxTeamCount; team++)
            {
                // 默认只给玩家方向播，免得 AI 每次加 buff 都把整场打断
                if (buffLineForPlayerOnly && !m_Duel.IsManual(team)) continue;

                Person person = m_Duel.GetCurrentPerson(team);
                if (person == null) continue;

                for (int buff = 0; buff < (int)DuelBuffType.DuelBuffType_Max; buff++)
                {
                    bool has = m_Duel.HasBuff(team, buff);
                    if (has && !m_BuffSeen[team, buff])
                        PlayLine(person, person.Name + " " + BuffLine(buff));

                    m_BuffSeen[team, buff] = has;
                }
            }
        }

        /// <summary>各种增益对应的提升对白</summary>
        public static string BuffLine(int buff)
        {
            switch (buff)
            {
                case (int)DuelBuffType.DuelBuffType_Attack: return "气合！气势大涨！";
                case (int)DuelBuffType.DuelBuffType_Defense: return "坚守！稳如泰山！";
                case (int)DuelBuffType.DuelBuffType_CriticalChance: return "看招！胜负就在一击！";
                default: return "气势提升！";
            }
        }

        public virtual void DuelHpAnim(Duel duel, Duel.HPAnim[] queue, int count)
        {
            m_CardHits.Clear();
            ApplyHpAnim(queue, count);

            // 这一合的攻守双方演给卡牌看（攻方出招 / 守方受击或闪避）
            PlayCardHitFx();

            RefreshCharaPanels();
            SetAnimTimer(stepDuration);
        }

        public virtual void DuelSpiritAnim(Duel duel, Duel.SpiritAnim[] queue, int count)
        {
            ApplySpiritAnim(queue, count);
            RefreshCharaPanels();

            // 斗志一变，必杀技按钮的可用性就可能翻转：涨过一层（100）要立刻点亮，
            // 释放必杀扣掉一层后要立刻压暗，不必等玩家去点别的按钮。
            // 逻辑层所有斗志变化（含释放必杀时的扣减）都收在 PlaySpiritAnim 里，
            // 所以在这一处刷就够，不用逐帧盯。
            RefreshCommandPanel();
            SetAnimTimer(stepDuration * 0.5f);
        }

        /// <summary>结算合数增减。对应逻辑层无表现层时的那段循环</summary>
        protected virtual void ApplyBlowAnim(Duel.BlowAnim[] queue, int count)
        {
            if (queue == null || m_Duel == null) return;
            for (int i = 0; i < count && i < queue.Length; i++)
            {
                Duel.BlowAnim anim = queue[i];
                if (anim == null || anim.value < 0) continue;

                m_Duel.AddBlowCounter(anim.value);
                // calc_result 会用到队列内容，用完必须复位（与逻辑层保持一致）
                queue[i] = new Duel.BlowAnim();
            }
        }

        /// <summary>结算体力与伤病增减</summary>
        protected virtual void ApplyHpAnim(Duel.HPAnim[] queue, int count)
        {
            if (queue == null || m_Duel == null) return;
            for (int i = 0; i < count && i < queue.Length; i++)
            {
                Duel.HPAnim anim = queue[i];
                if (anim == null) continue;
                if (anim.defTeam < 0 || anim.defTeam >= Duel.MaxTeamCount) continue;

                m_Duel.AddHp(anim.defTeam, anim.defChara, -anim.damage);
                m_Duel.AddShoubyou(anim.defTeam, anim.defChara, anim.shoubyouDamage);
                if (anim.shoubyouDamage > 0)
                {
                    m_Duel.CalcActionRatio();

                    // 负伤当场记一笔（武将条的标红由 DuelHpAnim 随后的 RefreshCharaPanels 负责）
                    Person hurt = m_Duel.GetPerson(anim.defTeam, anim.defChara);
                    if (hurt != null)
                        AppendLog(hurt.Name + " 负伤！");
                }

                queue[i] = new Duel.HPAnim();

                // 卡牌演出要用攻守双方，而上面刚把队列条目清空，所以先留一份拷贝
                m_CardHits.Add(new Duel.HPAnim
                {
                    damage = anim.damage,
                    atkTeam = anim.atkTeam,
                    atkChara = anim.atkChara,
                    defTeam = anim.defTeam,
                    defChara = anim.defChara,
                    shoubyouDamage = anim.shoubyouDamage,
                });

                if (anim.damage != 0)
                {
                    Person atk = m_Duel.GetPerson(anim.atkTeam, anim.atkChara);
                    Person def = m_Duel.GetPerson(anim.defTeam, anim.defChara);
                    AppendLog((atk != null ? atk.Name : "？") + " → "
                        + (def != null ? def.Name : "？") + "  体力 -" + anim.damage);
                }
            }
        }

        /// <summary>结算斗志增减（攻守双方各有一份变化量）</summary>
        protected virtual void ApplySpiritAnim(Duel.SpiritAnim[] queue, int count)
        {
            if (queue == null || m_Duel == null) return;
            for (int i = 0; i < count && i < queue.Length; i++)
            {
                Duel.SpiritAnim anim = queue[i];
                if (anim == null) continue;
                if (anim.atkTeam < 0 || anim.atkTeam >= Duel.MaxTeamCount) continue;

                m_Duel.AddSpirit(anim.atkTeam, anim.atkChara, anim.atkValue);
                m_Duel.AddSpirit(anim.defTeam, anim.defChara, anim.defValue);
                queue[i] = new Duel.SpiritAnim();

                Person atk = m_Duel.GetPerson(anim.atkTeam, anim.atkChara);
                if (atk != null && anim.atkValue != 0)
                    AppendLog(atk.Name + " 斗志 " + (anim.atkValue > 0 ? "+" : "") + anim.atkValue);
            }
        }

        public virtual void DuelJoin(Duel duel, int team, int oldChara)
        {
            if (duel != m_Duel) Bind(duel);

            Person person = m_Duel.GetCurrentPerson(team);
            if (person != null) AppendLog(person.Name + " 出战！");

            // 副将那几行要等他真的入场了才显示，所以刷新放在这里
            RefreshCharaPanels();

            // 入场之后他才是可交替的对象，底部按钮也要从「决定」变成「替换」
            RefreshCommandPanel();

            // 开局第 0 合的主将登场不播对白；中途入场的副将随机报一句助战台词
            // （可以引他的名字 / 字号 / 对主将的称呼）
            if (person != null && m_Duel.BlowCounter > 0)
                PlayLine(person, JoinLine(person, m_Duel.GetPerson(team, 0)));

            // 卡牌演出：新上场的武将弹一下（开局第 0 合也走这里，所以卡片一出场就有戏）
            PlayCardFx(CardOf(team), DuelCardFx.Swap, cardHitDuration, Color.white, "出战");

            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelSwitch(Duel duel, int team, int oldChara)
        {
            Person person = m_Duel.GetCurrentPerson(team);
            if (person != null)
                AppendLog(person.Name + " 交替上场。");
            RefreshCharaPanels();

            // 交替按钮的显隐是按"谁是当前出战"算的，换完人必须跟着刷，
            // 否则刚换下去的那位不会重新亮出按钮（就没法再换回来了）
            RefreshCommandPanel();

            // 卡牌演出：换上来的人弹一下
            PlayCardFx(CardOf(team), DuelCardFx.Swap, cardHitDuration, Color.white, "交替");

            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelChangeCurrentChara(Duel duel, int team)
        {
            RefreshCharaPanels();
        }

        /// <summary>
        /// 必杀即将释放：随机喊一句该必杀的台词。
        ///
        /// 逻辑层（DuelPhase.SpecialPhase）会调完这里就停在原地，
        /// 一直等对话框关掉（DuelIsMessageBoxVisible 为 false）才结算伤害——
        /// 所以台词是"释放前"的最后一道闸。
        ///
        /// 顺带把暂停解除：必杀已经定下来了，接着就该释放，
        /// 释放完这一回合照常往下走，不用玩家再按一次「决定」。
        /// </summary>
        public virtual void DuelSpecialBegin(Duel duel, int team, int chara, int special)
        {
            if (duel == null) return;
            if (duel != m_Duel) Bind(duel);

            if (m_Paused)
            {
                m_StopPushed = false;
                m_PlayPushed = true;
            }
            m_Paused = false;
            RefreshCommandPanel();

            // 卡牌演出：施放者大幅前冲、卡面泛金，卡片中央亮出必杀名
            PlayCardFx(CardOf(team), DuelCardFx.Special, cardSpecialDuration, CardSpecialTint, SpecialName(special));

            if (!specialLineBothSides && !m_Duel.IsManual(team)) return;

            Person person = m_Duel.GetPerson(team, chara);
            if (person == null) return;

            PlayLine(person, SpecialLine(special));
        }

        /// <summary>
        /// 随机取一句该必杀的台词。
        /// 返回空串时 PlayLine 会直接跳过（不留对话框，也就不会卡住逻辑层推进）。
        ///
        /// 这里刻意用 UnityEngine.Random 而不是 DuelRandom：台词纯属表现，
        /// 不需要跟随单挑的种子复现；而 DuelRandom 未设种子时会转发到 GameRandom，
        /// 在没有游戏上下文的场合（测试 / 编辑器）可能还没初始化而抛异常——
        /// 挑一句台词而已，不该有把整场单挑带崩的可能。
        /// </summary>
        public static string SpecialLine(int special)
        {
            if (SpecialLines == null) return string.Empty;
            if (special < 0 || special >= SpecialLines.Length) return string.Empty;

            string[] lines = SpecialLines[special];
            if (lines == null || lines.Length == 0) return string.Empty;

            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }

        /// <summary>必杀名（卡牌中央的演出文字用；越界返回空串，卡片就只播动作不出字）</summary>
        public static string SpecialName(int special)
        {
            if (SpecialNames == null || special < 0 || special >= SpecialNames.Length) return string.Empty;
            return SpecialNames[special];
        }

        public virtual void DuelFtk(Duel duel, int team, int chara, int ftkType, int opponentTeam, int opponentChara)
        {
            Person person = m_Duel.GetPerson(team, chara);
            Person opponent = m_Duel.GetPerson(opponentTeam, opponentChara);
            if (person != null && opponent != null)
                AppendLog("【一击必杀】" + person.Name + " 一举击倒了 " + opponent.Name + "！");
            RefreshCharaPanels();

            // 卡牌演出：胜方大幅冲出泛金、败方剧烈后仰压暗（也算受击，序列帧照亮）
            PlayCardFx(CardOf(team), DuelCardFx.Ftk, cardResultDuration, CardSpecialTint, "一击必杀");
            PlayCardFx(CardOf(opponentTeam), DuelCardFx.Hit, cardResultDuration, CardHitTint, null);
            PlayHitFx(CardOf(opponentTeam));

            SetAnimTimer(emphasisDuration * 1.5f);
        }

        public virtual void DuelResetInvulnerable(Duel duel, int team)
        {
            RefreshCharaPanels();
        }

        public virtual void DuelResetBuff(Duel duel, int team, int buff)
        {
            RefreshCharaPanels();
        }

        #endregion

        #region IDuelView：播放控制

        public virtual void DuelStop(Duel duel)
        {
            m_StopPushed = false;
            m_Paused = true;
            m_Started = true;          // 逻辑层肯停下来就说明已经开打了

            // 逻辑层已经在等玩家下令了，开场寒暄无论如何都要收场，
            // 否则万一 DuelOpening 没走到（或对白出错没回调），命令区就永远不亮。
            m_IntroDone = true;
            RefreshCommandPanel();
        }

        public virtual void DuelPlay(Duel duel)
        {
            m_PlayPushed = false;
            m_Paused = false;
            m_ReplaceSelecting = false;   // 恢复播放时顺手收起交替按钮
            RefreshCommandPanel();
        }

        public virtual bool DuelIsStopButtonPushed(Duel duel)
        {
            return m_StopPushed;
        }

        public virtual bool DuelIsPlayButtonPushed(Duel duel)
        {
            return m_PlayPushed;
        }

        #endregion

        #region IDuelView：玩家输入

        /// <summary>
        /// 交付玩家选定的方针。
        ///
        /// 交的是**玩家当前的选择**（m_StanceChoice），不再是一次性的待交付值：
        /// 逻辑层每个指令阶段都会来问一次，所以"切过方针之后，之后的回合就按新方针执行"
        /// 天然成立——哪怕中途换了出战武将、或某一回合没走到这个问询点，也不会退回默认方针。
        /// SetStance 对相同的值是幂等的（不会反复重置方针持续回合数），重复交付没有副作用。
        /// </summary>
        public virtual int DuelGetStance(Duel duel, int team)
        {
            if (team < 0 || team >= m_StanceChoice.Length) return -1;
            m_PendingStance[team] = -1;      // 待交付槽已无用，顺手清干净
            return m_StanceChoice[team];
        }

        public virtual int DuelGetSpecial(Duel duel, int team)
        {
            if (team != m_PendingSpecialTeam) return -1;
            int special = m_PendingSpecial;
            m_PendingSpecial = -1;
            m_PendingSpecialTeam = -1;
            return special;
        }

        public virtual int DuelGetSwitchingChara(Duel duel, int team)
        {
            int chara = m_PendingSwitchChara;
            m_PendingSwitchChara = -1;
            return chara;
        }

        public virtual bool DuelIsSpecialButtonPushed(Duel duel, int team)
        {
            bool pushed = m_SpecialPushed;
            m_SpecialPushed = false;
            return pushed;
        }

        /// <summary>
        /// 必杀指令阶段的"继续执行"信号。
        ///
        /// 逻辑层的 SpecialCommandPhase 会停在 DuelState_SpecialCommand 等它返回 true
        /// （名字叫 Cancel，实际作用是确认进入执行）。
        /// 玩家既然已经在高亮时点过必杀，这里直接放行，不再要求二次点击。
        /// </summary>
        public virtual bool DuelIsSpecialCancelButtonPushed(Duel duel)
        {
            if (m_PendingSpecial >= 0)
                return true;

            bool pushed = m_SpecialCancelPushed;
            m_SpecialCancelPushed = false;
            return pushed;
        }

        /// <summary>
        /// 是否确认。
        ///
        /// 单挑界面里已经没有确认面板了：「是否观看」这类选择由单挑**外部**决定
        /// （决定完之后才创建单挑，看 / 不看分别走 withView 的两条路），
        /// 所以这里不再弹窗，默认放行。需要真弹窗时给 YesNoHandler 挂个实现即可。
        /// </summary>
        public virtual bool YesNo(string text)
        {
            return YesNoHandler == null || YesNoHandler(text);
        }

        /// <summary>外部确认钩子：返回 true 表示同意。留空即一律放行</summary>
        public static System.Func<string, bool> YesNoHandler;

        #endregion
    }
}
