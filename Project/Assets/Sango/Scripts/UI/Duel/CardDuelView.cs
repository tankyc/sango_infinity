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

        [Header("确认面板")]
        public GameObject confirmPanel;
        public Text confirmText;
        public Button btnWatch;
        public Button btnSkip;

        [Header("命令面板")]
        public GameObject commandPanel;
        /// <summary>额外的按钮容器：按钮被分散摆放时（方针在一处、停止在另一处）与 commandPanel 一起显隐</summary>
        public GameObject commandPanelAlt;
        public Button[] stanceButtons = new Button[4];
        public Button btnSpecial;
        public Button btnSpecialCancel;
        public Button[] switchButtons = new Button[3];
        public Button btnPlay;
        public Text skipHint;

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

        /// <summary>玩家已选择、尚未被逻辑层取走的行动方针</summary>
        protected int m_PendingStance = -1;

        /// <summary>玩家已选择、尚未被逻辑层取走的必杀</summary>
        protected int m_PendingSpecial = -1;

        /// <summary>玩家已选择、尚未被逻辑层取走的交替武将</summary>
        protected int m_PendingSwitchChara = -1;

        /// <summary>
        /// 各阵营后台武将的**真实索引**（[阵营, 后台位]，对应"交替"按钮 0 / 1）。
        /// 按钮序号与武将序号不是一回事：前台武将可能是 chara[1]，
        /// 后台顺序也就随之变化，所以每次刷新都要按阵营分别重算。
        /// </summary>
        protected int[,] m_BackstageChara = NewBackstageTable();

        private static int[,] NewBackstageTable()
        {
            int[,] table = new int[Duel.MaxTeamCount, 2];
            for (int t = 0; t < table.GetLength(0); t++)
                for (int i = 0; i < table.GetLength(1); i++)
                    table[t, i] = -1;
            return table;
        }

        /// <summary>气力每满这个值就点亮一个刻度</summary>
        public const int SpiritPipValue = 100;

        /// <summary>气力刻度的数量上限（keep1 / keep2）</summary>
        public const int SpiritPipMax = 2;

        /// <summary>必杀按钮是否被按下</summary>
        protected bool m_SpecialPushed = false;

        /// <summary>必杀取消按钮是否被按下</summary>
        protected bool m_SpecialCancelPushed = false;

        /// <summary>日志行</summary>
        protected readonly List<string> m_LogLines = new List<string>();

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
            m_StopPushed = false;
            m_PlayPushed = false;
            m_LogLines.Clear();
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
        /// 查找刻意做得"宽松"：美术把按钮或武将位挪到别的容器下、或者给主将位改名，
        /// 只要名字还在，就能绑上，不必改代码。查找顺序为
        ///   指定容器内 → 整棵树 → 别名。
        /// </summary>
        protected virtual void AutoBind()
        {
            Transform root = transform;

            Transform left = FindDeep(root, "LeftPanel");
            Transform right = FindDeep(root, "RightPanel");
            leftCharaSlots = CollectCharaSlots(left, root, "person_left");
            rightCharaSlots = CollectCharaSlots(right, root, "person_right");
            leftSpiritPips = CollectSpiritPips(leftCharaSlots);
            rightSpiritPips = CollectSpiritPips(rightCharaSlots);

            blowCounterText = FindText(root, "BlowCounter");
            blowCounterTen = FindComponent<Image>(FindDeep(root, "BlowCounter_ten"));
            blowCounterOne = FindComponent<Image>(FindDeep(root, "BlowCounter_one"));
            logText = FindComponent<Text>(FindDeep(root, "LogBg/log")) ?? FindText(root, "log");

            Transform confirm = FindDeep(root, "ConfirmPanel");
            if (confirm != null)
            {
                confirmPanel = confirm.gameObject;
                confirmText = FindComponent<Text>(FindDeep(confirm, "ConfirmText"));
                btnWatch = FindComponent<Button>(FindDeep(confirm, "BtnWatch"));
                btnSkip = FindComponent<Button>(FindDeep(confirm, "BtnSkip"));
            }

            // 命令按钮不限定在 CommandPanel 之下（美术可能把它们单独放进一个容器）
            Transform command = FindDeep(root, "CommandPanel");
            commandPanel = command != null ? command.gameObject : null;

            Transform stance0Transform = FindDeep(root, "Stance0");

            if (command == null)
            {
                // 没有 CommandPanel 时，退而使用方针按钮所在容器整体显隐
                if (stance0Transform != null && stance0Transform.parent != null)
                    commandPanel = stance0Transform.parent.gameObject;
            }
            else if (stance0Transform != null && !stance0Transform.IsChildOf(command))
            {
                // 方针按钮不在 CommandPanel 里 → 记下它所在的容器，刷新时一起显隐，
                // 否则会出现"停止按钮藏了、方针按钮还留着"的半隐藏状态
                commandPanelAlt = stance0Transform.parent != null
                    ? stance0Transform.parent.gameObject : null;
            }

            stanceButtons = new Button[4];
            for (int i = 0; i < 4; i++)
                stanceButtons[i] = FindButton(root, "Stance" + i);

            btnSpecial = FindButton(root, "BtnSpecial");
            btnSpecialCancel = FindButton(root, "BtnSpecialCancel");

            switchButtons = new Button[3];
            for (int i = 0; i < 3; i++)
                switchButtons[i] = FindButton(root, "Switch" + i);

            btnPlay = FindButton(root, "BtnPlay");
            skipHint = FindComponent<Text>(FindDeep(root, "SkipHint"));
        }

        /// <summary>
        /// 收集一个阵营的 3 个武将位。
        /// 主将位优先在面板内找 "person"，找不到则在整棵树里找 mainAlias
        /// （例如美术把主将单独放在根节点上的 person_left / person_right）。
        /// </summary>
        protected virtual RectTransform[] CollectCharaSlots(Transform panel, Transform root, string mainAlias)
        {
            RectTransform[] slots = new RectTransform[3];

            // 优先用别名：美术把前台武将单独放在根节点上（person_left / person_right），
            // 面板里可能还残留一个旧的 person，那不是台面，必须让位。
            RectTransform main = root != null ? FindDeep(root, mainAlias) as RectTransform : null;
            if (main != null)
            {
                HideLegacyMain(panel);
            }
            else
            {
                main = FindDeep(panel, "person") as RectTransform;
            }

            slots[0] = main;
            slots[1] = FindDeep(panel, "person_1") as RectTransform;
            slots[2] = FindDeep(panel, "person_2") as RectTransform;
            return slots;
        }

        /// <summary>藏掉面板里残留的旧主将位（已被根节点上的前台武将面板取代），避免出现无人管理的行</summary>
        protected static void HideLegacyMain(Transform panel)
        {
            RectTransform legacy = FindDeep(panel, "person") as RectTransform;
            if (legacy != null && legacy.gameObject.activeSelf)
                legacy.gameObject.SetActive(false);
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
            if (stanceButtons != null)
            {
                for (int i = 0; i < stanceButtons.Length; i++)
                {
                    int index = i;
                    BindButton(stanceButtons[i], () => OnStanceClick(index));
                }
            }

            if (switchButtons != null)
            {
                for (int i = 0; i < switchButtons.Length; i++)
                {
                    int index = i;
                    BindButton(switchButtons[i], () => OnSwitchClick(index));
                }
            }

            BindButton(btnSpecial, OnSpecialClick);
            BindButton(btnSpecialCancel, OnSpecialCancelClick);
            BindButton(btnPlay, OnPlayClick);
            BindButton(btnWatch, OnWatchClick);
            BindButton(btnSkip, OnSkipClick);
        }

        /// <summary>挂按钮监听；prefab 上已有持久化 onClick 时不重复挂</summary>
        protected static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            if (button.onClick.GetPersistentEventCount() > 0) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        #region 按钮回调（同时供 prefab 的持久化 onClick 使用，因此必须是 public）

        /// <summary>选择第 index 条行动方针</summary>
        public virtual void OnStanceClick(int index)
        {
            if (m_Duel == null) return;
            m_PendingStance = index;
            SetAnimTimer(stepDuration);
        }

        public void OnStance0Click() { OnStanceClick(0); }
        public void OnStance1Click() { OnStanceClick(1); }
        public void OnStance2Click() { OnStanceClick(2); }
        public void OnStance3Click() { OnStanceClick(3); }

        /// <summary>
        /// 让第 index 个后台武将**立刻**交替上场。
        ///
        /// 不走"记下待处理、由逻辑层的 SwitchAnim 稍后执行"那条路，
        /// 而是当场改掉逻辑层的当前出战武将，界面随即刷新成新人；
        /// 玩家再按"继续"时，后面的回合就是由这位替换完成的武将接着打。
        /// </summary>
        public virtual void OnSwitchClick(int index)
        {
            if (m_Duel == null) return;

            int team = GetManualTeam();
            if (team < 0) return;
            if (index < 0 || index >= m_BackstageChara.GetLength(1)) return;

            int chara = m_BackstageChara[team, index];
            if (chara < 0) return;

            int oldChara = m_Duel.GetCurrentChara(team);
            if (chara == oldChara) return;

            // 立刻换人（ChangeCurrentChara 内部会回调 DuelChangeCurrentChara 刷新界面）
            if (!m_Duel.ChangeCurrentChara(team, chara)) return;

            // 已经换过了：清掉待处理标记，免得逻辑层的 SwitchAnim 再换一次
            m_PendingSwitchChara = -1;

            DuelSwitch(m_Duel, team, oldChara);
            SetAnimTimer(stepDuration);
        }

        /// <summary>由玩家手动操作的那个阵营（没有则为 -1，"观战"模式）</summary>
        protected int GetManualTeam()
        {
            if (m_Duel == null) return -1;
            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                if (m_Duel.IsManual(i)) return i;
            }
            return -1;
        }

        public void OnSwitch0Click() { OnSwitchClick(0); }
        public void OnSwitch1Click() { OnSwitchClick(1); }
        public void OnSwitch2Click() { OnSwitchClick(2); }

        /// <summary>按下必杀</summary>
        public virtual void OnSpecialClick()
        {
            m_SpecialPushed = true;
            SetAnimTimer(stepDuration);
        }

        /// <summary>取消必杀</summary>
        public virtual void OnSpecialCancelClick()
        {
            m_SpecialCancelPushed = true;
        }

        /// <summary>
        /// 停止 / 继续（同一个按钮，按当前状态切换）。
        /// 未停在指令阶段时按下 = 停止（进入等待指示）；已停在指令阶段时按下 = 继续（执行并恢复播放）。
        /// </summary>
        public virtual void OnPlayClick()
        {
            if (m_Duel == null) return;

            if (m_Paused)
                m_PlayPushed = true;
            else
                m_StopPushed = true;
        }

        /// <summary>观战</summary>
        public virtual void OnWatchClick()
        {
            m_Paused = false;
            SetAnimTimer(stepDuration);
        }

        /// <summary>跳过当前动画</summary>
        public virtual void OnSkipClick()
        {
            m_AnimTimer = 0f;
            m_Paused = false;
        }

        #endregion

        #endregion

        #region 计时与刷新

        protected virtual void Update()
        {
            if (m_AnimTimer > 0f && !m_Paused)
                m_AnimTimer -= Time.deltaTime;
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

        /// <summary>刷新左右两侧的武将面板</summary>
        protected virtual void RefreshCharaPanels()
        {
            if (m_Duel == null) return;
            RefreshTeamPanel((int)DuelTeam.DuelTeam_Challenger, leftCharaSlots);
            RefreshTeamPanel((int)DuelTeam.DuelTeam_Challenged, rightCharaSlots);
        }

        /// <summary>
        /// 刷新一个阵营。
        /// 第 0 位（最上面那块）显示**当前出战**的武将；第 1 / 2 位依次显示后台武将。
        /// 后台武将的真实索引会记进 m_BackstageChara，供"交替"按钮使用。
        /// </summary>
        protected virtual void RefreshTeamPanel(int team, RectTransform[] slots)
        {
            if (slots == null || m_Duel == null) return;

            Duel.Team teamData = m_Duel.GetTeam(team);
            int current = m_Duel.GetCurrentChara(team);

            // 前台 = 当前出战；后台 = 其余武将按原顺序补齐
            int[] order = new int[Duel.MaxTeamCharaCount];
            int count = 0;
            if (current >= 0 && current < Duel.MaxTeamCharaCount)
                order[count++] = current;
            for (int i = 0; i < Duel.MaxTeamCharaCount; i++)
            {
                if (i == current) continue;
                order[count++] = i;
            }

            m_BackstageChara[team, 0] = -1;
            m_BackstageChara[team, 1] = -1;
            int backIndex = 0;

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
                bool joined = person != null;
                slot.gameObject.SetActive(joined);
                if (!joined) continue;

                bool onStage = (s == 0);
                if (!onStage && backIndex < m_BackstageChara.GetLength(1))
                    m_BackstageChara[team, backIndex++] = chara;

                // 未出场的后台武将半透明显示
                CanvasGroup group = slot.GetComponent<CanvasGroup>();
                if (group == null && !onStage)
                    group = slot.gameObject.AddComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = onStage ? 1f : 0.55f;

                // 名字 / 武力 / 头像
                SetText(slot, "name", person.Name);
                SetText(slot, "Strength", "武力 " + person.Strength);
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
                    ApplySpiritPips(GetSpiritPips(team), spirit);
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

        /// <summary>按气力点亮 keep1 / keep2</summary>
        protected static void ApplySpiritPips(Image[] pips, int spirit)
        {
            if (pips == null) return;
            int pipCount = Mathf.Clamp(spirit / SpiritPipValue, 0, SpiritPipMax);
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] != null)
                    pips[i].gameObject.SetActive(i < pipCount);
            }
        }

        /// <summary>设置武将头像；没有贴图时置为全透明，避免渲染成白色实心块</summary>
        protected static void ApplyHead(Transform slot, Person person)
        {
            RawImage head = FindComponent<RawImage>(FindDeep(slot, "head"));
            if (head == null) return;

            Texture texture = null;
            if (GetHeadTexture != null)
                texture = GetHeadTexture(person);
            else if (person != null)
                texture = GameRenderHelper.LoadHeadIcon(person.headIconID, 1);

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

        /// <summary>刷新命令面板的可用性</summary>
        protected virtual void RefreshCommandPanel()
        {
            if (m_Duel == null) return;

            // 只有"停在指令阶段等玩家下令"时才显示方针 / 交替 / 必杀。
            bool commanding = m_Duel.IsManual() && m_Paused;
            int manualTeam = GetManualTeam();

            // 关键：「停止 / 继续」是玩家进入指令阶段的**唯一入口**，
            // 任何情况下都不能被收起。若它恰好和方针们放在同一个容器里，
            // 就整块保留、只逐个收起按钮，否则会出现"开始后没有停止按钮"。
            bool panelHoldsPlay = btnPlay != null && commandPanel != null
                && btnPlay.transform.IsChildOf(commandPanel.transform);

            if (commandPanel != null)
                commandPanel.SetActive(panelHoldsPlay || commanding);
            if (commandPanelAlt != null)
                commandPanelAlt.SetActive(commanding);

            // 四条行动方针
            for (int i = 0; i < 4; i++)
            {
                if (stanceButtons == null || stanceButtons[i] == null) continue;
                stanceButtons[i].gameObject.SetActive(commanding);
                stanceButtons[i].interactable = commanding;
            }

            // 必杀 / 取消必杀
            if (btnSpecial != null) btnSpecial.gameObject.SetActive(commanding);
            if (btnSpecialCancel != null) btnSpecialCancel.gameObject.SetActive(commanding);

            // 交替按钮只在存在对应后台武将时出现（一共 3 个武将位、1 个在场，故最多 2 个）
            if (switchButtons != null)
            {
                for (int i = 0; i < switchButtons.Length; i++)
                {
                    if (switchButtons[i] == null) continue;
                    bool usable = commanding
                        && manualTeam >= 0
                        && i < m_BackstageChara.GetLength(1)
                        && m_BackstageChara[manualTeam, i] >= 0;
                    switchButtons[i].gameObject.SetActive(usable);
                    switchButtons[i].interactable = usable;
                }
            }

            if (skipHint != null)
                skipHint.gameObject.SetActive(commanding);

            // 兜底：万一"停止"按钮被别的逻辑关掉了，这里重新点亮
            if (btnPlay != null && m_Duel.IsManual() && !btnPlay.gameObject.activeSelf)
                btnPlay.gameObject.SetActive(true);

            // 同一个按钮两种用途，标签跟着切换
            if (btnPlay != null)
            {
                Text label = FindComponent<Text>(FindDeep(btnPlay.transform, "Label"));
                if (label != null)
                    label.text = m_Paused ? ContinueLabel : StopLabel;
            }
        }

        /// <summary>「停止」按钮上的文字</summary>
        public const string StopLabel = "停止";

        /// <summary>同一个按钮在指令阶段显示的文字</summary>
        public const string ContinueLabel = "继续";

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
            return false;
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
            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelClosing(Duel duel)
        {
            AppendLog("—— 单挑结束 ——");
            SetAnimTimer(emphasisDuration);
            RefreshAll();
        }

        public virtual void DuelDraw(Duel duel)
        {
            AppendLog("双方不分胜负。");
            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelRetreat(Duel duel)
        {
            AppendLog("有人退却了。");
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
            ApplyBlowAnim(queue, count);
            RefreshBlowCounter();
            SetAnimTimer(stepDuration);
        }

        public virtual void DuelHpAnim(Duel duel, Duel.HPAnim[] queue, int count)
        {
            ApplyHpAnim(queue, count);
            RefreshCharaPanels();
            SetAnimTimer(stepDuration);
        }

        public virtual void DuelSpiritAnim(Duel duel, Duel.SpiritAnim[] queue, int count)
        {
            ApplySpiritAnim(queue, count);
            RefreshCharaPanels();
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
                    m_Duel.CalcActionRatio();

                queue[i] = new Duel.HPAnim();

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
            if (person != null)
                AppendLog(person.Name + " 出战！");
            RefreshCharaPanels();
            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelSwitch(Duel duel, int team, int oldChara)
        {
            Person person = m_Duel.GetCurrentPerson(team);
            if (person != null)
                AppendLog(person.Name + " 交替上场。");
            RefreshCharaPanels();
            SetAnimTimer(emphasisDuration);
        }

        public virtual void DuelChangeCurrentChara(Duel duel, int team)
        {
            RefreshCharaPanels();
        }

        public virtual void DuelFtk(Duel duel, int team, int chara, int ftkType, int opponentTeam, int opponentChara)
        {
            Person person = m_Duel.GetPerson(team, chara);
            Person opponent = m_Duel.GetPerson(opponentTeam, opponentChara);
            if (person != null && opponent != null)
                AppendLog("【一击必杀】" + person.Name + " 一举击倒了 " + opponent.Name + "！");
            RefreshCharaPanels();
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
            RefreshCommandPanel();
        }

        public virtual void DuelPlay(Duel duel)
        {
            m_PlayPushed = false;
            m_Paused = false;
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

        public virtual int DuelGetStance(Duel duel, int team)
        {
            int stance = m_PendingStance;
            m_PendingStance = -1;
            return stance;
        }

        public virtual int DuelGetSpecial(Duel duel, int team)
        {
            int special = m_PendingSpecial;
            m_PendingSpecial = -1;
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

        public virtual bool DuelIsSpecialCancelButtonPushed(Duel duel)
        {
            bool pushed = m_SpecialCancelPushed;
            m_SpecialCancelPushed = false;
            return pushed;
        }

        public virtual bool YesNo(string text)
        {
            ShowConfirm(text);
            return true;
        }

        /// <summary>显示确认面板</summary>
        public virtual void ShowConfirm(string text)
        {
            if (confirmPanel != null) confirmPanel.SetActive(true);
            if (confirmText != null) confirmText.text = text;
        }

        #endregion
    }
}
