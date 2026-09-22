/*
 * 文件名：CardDebateView.cs
 * 描述：舌战 2D 表现层（卡牌式）
 *
 * 定位（与单挑的 CardDuelView 同构）：
 *   · 它是 IDebateView 的一个实现，只负责把逻辑层抛出的"表现请求"翻译成界面操作，
 *     不认识任何舌战逻辑；将来要换 3D 表现，只需再写一个 IDebateView 实现，
 *     并在 DebateIntegration 里换掉 CreateViewHandler 即可。
 *   · 逻辑层的节拍由本类的动画计时器回告：DebateIsAnimating / DebateIsMessageBoxVisible，
 *     逻辑层会停在当前步骤等（Debate.IsIdle）。
 *   · 节点全部按名字查找（AutoBind），找不到的项一律跳过、不报错，
 *     所以 prefab 还没摆好时也不会崩，只是少一块显示。
 *
 * 节点命名约定（Editor/DebateWindowBuilder.cs 会照此生成 window_debate.prefab）：
 *   window_debate
 *   ├── topic / topicHint          当前话题与提示
 *   ├── left / right               两方武将区（子节点同名：head / name / intel / hpBar / hpText
 *   │                              / stressBar / stressText / anger / played）
 *   ├── Hand / Hand0..Hand6        玩家手牌（每个是 Button，文字子节点叫 lab）
 *   ├── Critical / BtnPushOn / BtnMercy   会心抉择（追击 / 留情）
 *   ├── Result / title / desc / BtnClose  结算画面
 *   ├── hint                       底部提示
 *   ├── LogBg/log                  战报
 *   └── float                      飘字载体（AnimationText）
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
        #region 节点引用（由 AutoBind / 编辑器构建器写入）

        [Header("话题")]
        public Text topicText;
        public Text topicHint;

        [Header("两方武将区")]
        public DebateSide leftSide = new DebateSide();
        public DebateSide rightSide = new DebateSide();

        [Header("玩家手牌")]
        public Button[] handButtons;
        public Text[] handLabels;

        [Header("会心抉择 / 结算 / 提示")]
        public GameObject criticalPanel;
        public Button btnPushOn;
        public Button btnMercy;
        public GameObject resultPanel;
        public Text resultTitle;
        public Text resultDesc;
        public Button btnClose;
        public Text hintText;

        [Header("战报与飘字")]
        public Text logText;
        public AnimationText floatText;

        /// <summary>一侧武将区的节点集合</summary>
        [System.Serializable]
        public class DebateSide
        {
            /// <summary>所属队伍（0=挑战方 / 1=应战方）；由 AutoBind 填</summary>
            public int team;
            public RawImage head;
            public Text nameText;
            public Text intelText;
            public Image hpBar;
            public Text hpText;
            public Image stressBar;
            public Text stressText;
            public GameObject angerTag;
            public Text playedCard;
        }

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

        /// <summary>战报行</summary>
        protected readonly List<string> m_LogLines = new List<string>();

        /// <summary>飘字组件是否已做好运行时准备（maxTime / 图标子节点）</summary>
        protected bool m_FloatReady;

        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
            AutoBind();
            BindButtons();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshAll();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_Debate = null;
        }

        protected virtual void Update()
        {
            if (m_AnimTimer > 0f)
                m_AnimTimer -= Time.deltaTime;
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
            m_LogLines.Clear();

            if (resultPanel != null) resultPanel.SetActive(false);
            if (criticalPanel != null) criticalPanel.SetActive(false);

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

            topicText = FindComponent<Text>(FindDeep(root, "topic"));
            topicHint = FindComponent<Text>(FindDeep(root, "topicHint"));

            leftSide.team = 0;
            rightSide.team = 1;
            BindSide(root, leftSide, "left");
            BindSide(root, rightSide, "right");

            // 手牌：Hand 下按名字 Hand0..Hand6（也接受 hand0..hand6）
            List<Button> buttons = new List<Button>();
            List<Text> labels = new List<Text>();
            Transform handRoot = FindDeep(root, "Hand");
            if (handRoot != null)
            {
                for (int i = 0; i < Debate.MaxCardCount; i++)
                {
                    Transform t = Deep(handRoot, "Hand" + i);
                    if (t == null) t = Deep(handRoot, "hand" + i);
                    if (t == null) continue;
                    buttons.Add(t.GetComponent<Button>());
                    labels.Add(FindComponent<Text>(Deep(t, "lab")) ?? FindComponent<Text>(t));
                }
            }
            handButtons = buttons.ToArray();
            handLabels = labels.ToArray();

            criticalPanel = FindObject(root, "Critical");
            btnPushOn = FindButton(root, "BtnPushOn");
            btnMercy = FindButton(root, "BtnMercy");

            resultPanel = FindObject(root, "Result");
            resultTitle = FindComponent<Text>(FindDeep(FindDeep(root, "Result"), "title"));
            resultDesc = FindComponent<Text>(FindDeep(FindDeep(root, "Result"), "desc"));
            btnClose = FindButton(root, "BtnClose");

            hintText = FindComponent<Text>(FindDeep(root, "hint"));
            logText = FindComponent<Text>(FindDeep(root, "LogBg/log")) ?? FindComponent<Text>(FindDeep(root, "log"));
            floatText = FindComponent<AnimationText>(FindDeep(root, "float"));

            if (resultPanel != null) resultPanel.SetActive(false);
            if (criticalPanel != null) criticalPanel.SetActive(false);
        }

        /// <summary>绑定一侧武将区的子节点</summary>
        protected virtual void BindSide(Transform root, DebateSide side, string sideName)
        {
            Transform t = FindDeep(root, sideName);
            if (t == null) return;

            side.head = FindComponent<RawImage>(Deep(t, "head"));
            side.nameText = FindComponent<Text>(Deep(t, "name"));
            side.intelText = FindComponent<Text>(Deep(t, "intel"));
            side.hpBar = FindComponent<Image>(Deep(t, "hpBar"));
            side.hpText = FindComponent<Text>(Deep(t, "hpText"));
            side.stressBar = FindComponent<Image>(Deep(t, "stressBar"));
            side.stressText = FindComponent<Text>(Deep(t, "stressText"));
            side.angerTag = Deep(t, "anger") != null ? Deep(t, "anger").gameObject : null;
            side.playedCard = FindComponent<Text>(Deep(t, "played"));

            if (side.angerTag != null) side.angerTag.SetActive(false);
        }

        /// <summary>把按钮的 onClick 挂到本类的回调上（prefab 上没有持久化绑定时兜底）</summary>
        protected virtual void BindButtons()
        {
            for (int i = 0; i < (handButtons != null ? handButtons.Length : 0); i++)
            {
                int index = i;
                Button b = handButtons[i];
                if (b == null || b.onClick.GetPersistentEventCount() > 0) continue;
                b.onClick.AddListener(() => OnHandClick(index));
            }

            if (btnPushOn != null && btnPushOn.onClick.GetPersistentEventCount() == 0)
                btnPushOn.onClick.AddListener(OnPushOnClick);
            if (btnMercy != null && btnMercy.onClick.GetPersistentEventCount() == 0)
                btnMercy.onClick.AddListener(OnMercyClick);
            if (btnClose != null && btnClose.onClick.GetPersistentEventCount() == 0)
                btnClose.onClick.AddListener(OnCloseClick);
        }

        #endregion

        #region 按钮回调

        /// <summary>点第 index 张手牌（prefab 里也可直接绑无参版本）</summary>
        public virtual void OnHandClick(int index)
        {
            m_PendingCard = index;
        }

        /// <summary>追击</summary>
        public virtual void OnPushOnClick()
        {
            m_PendingCritical = (int)DebateCritical.DebateCritical_PushOn;
        }

        /// <summary>留情</summary>
        public virtual void OnMercyClick()
        {
            m_PendingCritical = (int)DebateCritical.DebateCritical_HaveMercy;
        }

        /// <summary>关闭结算画面（关掉后逻辑层才会真正收场）</summary>
        public virtual void OnCloseClick()
        {
            m_ResultOpen = false;
            if (resultPanel != null) resultPanel.SetActive(false);
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

            SetAnimTimer(openingDuration);
            RefreshAll();
        }

        public virtual void DebateFtk(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);

            AppendLog("【一击必杀】瞬间击溃对手");
            ShowFloat("一击必杀", new Color(1f, 0.85f, 0.3f, 1f));
            SetAnimTimer(openingDuration);
        }

        public virtual void DebateClosing(Debate debate)
        {
            if (debate != m_Debate) Bind(debate);

            // 用逻辑层当前的胜负，而不是 Param.winner —— 后者要等 ClosingPhase 末尾才写入
            int winner = debate.CurrentWinner;
            int winType = debate.CurrentWinType;

            if (resultPanel != null) resultPanel.SetActive(true);
            if (resultTitle != null)
                resultTitle.text = winner >= 0 ? PersonName(debate, winner) + " 胜" : "不分胜负";
            if (resultDesc != null)
            {
                switch (winType)
                {
                    case (int)DebateWinType.DebateWinType_PushOn: resultDesc.text = "追击 — 痛打落水狗"; break;
                    case (int)DebateWinType.DebateWinType_HaveMercy: resultDesc.text = "留情 — 手下留情"; break;
                    case (int)DebateWinType.DebateWinType_Max: resultDesc.text = "一击必杀"; break;
                    default: resultDesc.text = "普通胜利"; break;
                }
            }

            // 结算画面开着就一直阻塞逻辑层（DebateIsAnimating），点了关闭才收场
            m_ResultOpen = true;
            SetAnimTimer(closingDuration);
            RefreshAll();
        }

        public virtual void DebateAngerEnd(Debate debate, int team)
        {
            DebateSide side = SideOf(team);
            if (side != null && side.angerTag != null) side.angerTag.SetActive(false);
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
                ShowFloat(PersonName(debate, team) + " 再考", Color.white);

            SetAnimTimer(playCardDuration);
            RefreshAll();
        }

        public virtual void DebateAttackDraw(Debate debate, int stressDamage)
        {
            AppendLog($"势均力敌，双方愤怒 +{stressDamage}");
            ShowFloat("势均力敌", new Color(1f, 0.9f, 0.6f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateShout(Debate debate, int team, int hpDamage, int stressDamage)
        {
            AppendLog($"{PersonName(debate, team)} 大喝：体力-{hpDamage}、愤怒+{stressDamage}");
            ShowFloat("大喝 -" + hpDamage, new Color(1f, 0.5f, 0.3f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateTopic(Debate debate, int team, int card, int hpDamage, int stressDamage, bool reflected)
        {
            string who = reflected ? "（被诡辩反弹）" : "";
            AppendLog($"{PersonName(debate, team)} {Debate.GetCardName(card)}{who}：体力-{hpDamage}、愤怒+{stressDamage}");
            ShowFloat(Debate.GetCardName(card) + " -" + hpDamage, reflected
                ? new Color(0.8f, 0.6f, 1f, 1f) : new Color(1f, 0.5f, 0.3f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateRethink(Debate debate, int team)
        {
            AppendLog(PersonName(debate, team) + " 再考：重抽手牌");
            ShowHint("手牌已重抽");
            SetAnimTimer(playCardDuration);
            RefreshAll();
        }

        public virtual void DebateIgnore(Debate debate, int team, int stressDamage)
        {
            AppendLog($"{PersonName(debate, team)} 无视：对手愤怒+{stressDamage}");
            ShowFloat("无视", new Color(0.8f, 0.8f, 0.8f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateCompose(Debate debate, int team, int stressDamage, bool reflected)
        {
            AppendLog($"{PersonName(debate, team)} 镇静：愤怒{stressDamage:+#;-#;0}");
            ShowFloat("镇静", new Color(0.6f, 0.9f, 1f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAgitate(Debate debate, int team, int stressDamage, bool reflected)
        {
            AppendLog($"{PersonName(debate, team)} 激昂：愤怒+{stressDamage}");
            ShowFloat("激昂", new Color(1f, 0.4f, 0.4f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        #endregion

        #region IDebateView：愤怒

        public virtual void DebateAngerTrigger(Debate debate, int team, int card)
        {
            DebateSide side = SideOf(team);
            if (side != null && side.angerTag != null) side.angerTag.SetActive(true);

            AppendLog(PersonName(debate, team) + " 愤怒爆发！");
            ShowFloat(PersonName(debate, team) + " 激昂", new Color(1f, 0.45f, 0.35f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAngerReckless(Debate debate, int team, int hpDamage, int stressDamage)
        {
            AppendLog($"{PersonName(debate, team)} 莽撞爆发：体力-{hpDamage}、愤怒+{stressDamage}");
            ShowFloat("莽撞 -" + hpDamage, new Color(1f, 0.35f, 0.3f, 1f));
            SetAnimTimer(effectDuration);
            RefreshAll();
        }

        public virtual void DebateAngerTimid(Debate debate, int team, int hpDamage, int stressDamage, int comboIndex)
        {
            AppendLog($"{PersonName(debate, team)} 连打 #{comboIndex + 1}：体力-{hpDamage}");
            ShowFloat("连打 -" + hpDamage, new Color(1f, 0.4f, 0.4f, 1f));
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

            RefreshHand(debate, team);

            if (m_PendingCard < 0)
            {
                ShowHint("请出牌（点手牌）");
                return -1;      // 逻辑层会停在出牌阶段等下一次询问
            }

            int index = m_PendingCard;
            m_PendingCard = -1;
            SetAnimTimer(playCardDuration);
            return index;
        }

        public virtual int DebateSelectCritical(Debate debate, int team)
        {
            if (debate != m_Debate) Bind(debate);

            if (criticalPanel != null) criticalPanel.SetActive(true);

            if (m_PendingCritical < 0)
            {
                ShowHint("对手已被击溃，请选择：追击 / 留情");
                return -1;
            }

            int value = m_PendingCritical;
            m_PendingCritical = -1;
            if (criticalPanel != null) criticalPanel.SetActive(false);
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

            if (topicText != null)
                topicText.text = DebateGameSystem.TopicNameOf(m_Debate.CurrentTopic);

            RefreshSide(leftSide);
            RefreshSide(rightSide);
            RefreshHand(m_Debate, m_PendingCardTeam);
        }

        /// <summary>刷新一侧的武将与状态</summary>
        protected virtual void RefreshSide(DebateSide side)
        {
            if (side == null || m_Debate == null) return;

            Debate.Character c = m_Debate.GetCharacter(side.team);
            if (c == null) return;

            if (side.nameText != null)
                side.nameText.text = c.person != null ? c.person.Name : "—";
            if (side.intelText != null)
                side.intelText.text = "智力 " + (c.person != null ? c.person.Intelligence : 0);
            if (side.hpText != null)
                side.hpText.text = c.hp + " / " + Debate.MaxHP;
            if (side.hpBar != null)
                side.hpBar.fillAmount = Mathf.Clamp01((float)c.hp / Debate.MaxHP);
            if (side.stressText != null)
                side.stressText.text = c.stress + " / " + Debate.MaxStress;
            if (side.stressBar != null)
                side.stressBar.fillAmount = Mathf.Clamp01((float)c.stress / Debate.MaxStress);
            if (side.angerTag != null)
                side.angerTag.SetActive(c.angerTimer > 0);
            if (side.playedCard != null)
                side.playedCard.text = Debate.GetCardName(m_Debate.GetPlayedCard(side.team));
        }

        /// <summary>刷新某一方的手牌按钮（只有玩家方才有意义）</summary>
        protected virtual void RefreshHand(Debate debate, int team)
        {
            if (debate == null || handLabels == null || handLabels.Length == 0) return;
            if (team < 0 || team >= Debate.MaxTeamCount) return;

            Debate.Character c = debate.GetCharacter(team);
            if (c == null) return;

            for (int i = 0; i < handLabels.Length; i++)
            {
                Text label = handLabels[i];
                Button button = handButtons != null && i < handButtons.Length ? handButtons[i] : null;
                if (label == null) continue;

                if (i >= c.maxCardCount || c.card[i] < 0)
                {
                    label.text = "—";
                    if (button != null) button.interactable = false;
                    continue;
                }

                int card = c.card[i];
                bool sameTopic = Debate.GetCardTopic(card) == debate.CurrentTopic;
                label.text = Debate.GetCardName(card) + (sameTopic ? " ★" : "");
                label.color = sameTopic ? new Color(1f, 0.85f, 0.4f, 1f) : Color.white;

                // 「再考」这一合用过就不能再用（对应 C++ can_rethink_）：
                // 不禁用的话玩家可以反复点它，出牌 → 重抽 → 再出牌，回合永远走不完。
                bool usable = card != (int)DebateCard.DebateCard_Rethink || debate.CanRethink(team);
                if (button != null) button.interactable = usable;
                if (!usable) label.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }
        }

        #endregion

        #region 内部工具

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

        /// <summary>在飘字组件上飘一条（复用单挑卡面同一套 AnimationText）</summary>
        protected void ShowFloat(string text, Color color)
        {
            if (floatText == null || string.IsNullOrEmpty(text)) return;

            PrepareFloat();
            floatText.flipY = false;            // false = 向上飘
            floatText.Create(text, color, floatScale);
        }

        /// <summary>
        /// 飘字组件的运行时准备，只做一次：
        ///   1) maxTime 是 [NonSerialized]，运行时只拿到默认值 1，而曲线跑到 3 秒 —— 不按曲线重算会被半路回收；
        ///   2) 组件取 label 的第 0 个子节点当"图标位"，label 下没有子节点时会抛 IndexOutOfRange，缺了就补一个空 Text。
        /// </summary>
        protected void PrepareFloat()
        {
            if (m_FloatReady || floatText == null) return;

            floatText.maxTime = Mathf.Max(
                CurveEnd(floatText.offsetCurveX), CurveEnd(floatText.offsetCurveY),
                CurveEnd(floatText.alphaCurve), CurveEnd(floatText.scaleCurve));

            Text template = floatText.label;
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

            m_FloatReady = true;
        }

        /// <summary>取曲线的最后一个关键帧时间（飘字总时长）</summary>
        protected static float CurveEnd(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return 0f;
            return curve.keys[curve.length - 1].time;
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

        /// <summary>按队伍取对应的界面侧</summary>
        protected DebateSide SideOf(int team)
        {
            if (leftSide != null && leftSide.team == team) return leftSide;
            if (rightSide != null && rightSide.team == team) return rightSide;
            return null;
        }

        #endregion

        #region 节点查找工具

        /// <summary>递归按名字查找（支持 a/b 路径）</summary>
        protected static Transform FindDeep(Transform root, string namePath)
        {
            if (root == null || string.IsNullOrEmpty(namePath)) return null;

            int slash = namePath.IndexOf('/');
            if (slash >= 0)
            {
                Transform head = FindDeep(root, namePath.Substring(0, slash));
                return head != null ? FindDeep(head, namePath.Substring(slash + 1)) : null;
            }

            return Deep(root, namePath);
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

        protected static GameObject FindObject(Transform root, string name)
        {
            Transform t = FindDeep(root, name);
            return t != null ? t.gameObject : null;
        }

        protected static Button FindButton(Transform root, string name)
        {
            return FindComponent<Button>(FindDeep(root, name));
        }

        #endregion
    }
}
