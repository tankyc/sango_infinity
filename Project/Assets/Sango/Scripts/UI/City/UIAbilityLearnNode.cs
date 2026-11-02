using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力培育界面的一个能力节点（<c>window_city_ability_learn</c> 里
    /// <c>root/abilityTreeRoot/viewport/content/node_template</c> 的运行时副本）。
    ///
    /// <para>
    /// 【为什么和研究界面分开】培育界面是"自动网格"排版（父级 <c>content</c> 上挂
    /// <c>GridLayoutGroup</c>，位置由布局组决定），研究界面是"按能力数据 q/r 六边形坐标手工摆位 +
    /// 六个方向箭头表示前置"。两套版面差异太大，强行复用会让两边都别扭，因此培育界面单列一个节点脚本。
    /// </para>
    ///
    /// <para>
    /// 【节点语义】一条节点 = 一条**本势力已研究完毕**的能力。玩家看到的是全部可选项，
    /// 靠状态区分"这条现在能不能给当前武将学"：
    /// <list type="bullet">
    /// <item>可培育 —— 亮可培育高亮框、按钮可点；</item>
    /// <item>不可培育 —— 压未研究遮罩、按钮禁用（原因由窗口在详情区展示）。</item>
    /// </list>
    /// "剩"字控件显示**剩余可学习次数**（<see cref="Ability.LearningLimitCount"/> 减去势力已学次数）。
    /// </para>
    ///
    /// 【美术接入清单】在 <c>content/node_template</c> 上挂本脚本，并按字段名拖控件（都可为空）：
    /// <code>
    /// 字段名          控件类型     用途
    /// ──────────────────────────────────────────────
    /// bg              Image      节点底色块
    /// type            Image      种类底图（按能力的显示类型取图，见 AbilityTypeSprite）
    /// nameLabel       Text       能力名（如「统+5中」）
    /// remainField     UITextField 剩余可学习次数（prefab 里标题为「剩」）
    /// lockMask        Image      不可培育遮罩
    /// learnableMark   Image      可培育高亮
    /// selectedMark    Image      选中框（prefab 里放在 bg 下）
    /// button          Button     点击（prefab 里挂在节点根上）
    /// </code>
    /// </summary>
    public class UIAbilityLearnNode : MonoBehaviour
    {
        /// <summary>节点底色块</summary>
        public Image bg;

        /// <summary>
        /// 种类底图：按能力的"显示类型"（<see cref="Ability.displayKind"/>，未配置时按
        /// <see cref="Ability.statKind"/> / 隐藏槽种类兜底）取图，与研究界面节点同一套编码。
        /// </summary>
        public Image type;

        /// <summary>
        /// 种类底图覆盖表，下标 = 种类（0 武力 / 1 统率 / 2 政治 / 3 智力）。
        /// Inspector 里配了优先用，没配由 <see cref="AbilityTypeSprite"/> 兜底加载。
        /// </summary>
        // 注意：项目里有名为 Sprite 的命名空间，类型必须写全名 UnityEngine.Sprite
        public UnityEngine.Sprite[] typeSprites = new UnityEngine.Sprite[AbilityTypeSprite.Count];

        /// <summary>能力名</summary>
        public Text nameLabel;

        /// <summary>剩余可学习次数（prefab 里标题为「剩」的输入框）</summary>
        public UITextField remainField;

        /// <summary>不可培育遮罩</summary>
        public Image lockMask;

        /// <summary>可培育高亮</summary>
        public Image learnableMark;

        /// <summary>选中框</summary>
        public Image selectedMark;

        /// <summary>点击按钮</summary>
        public Button button;

        /// <summary>本节点代表的能力</summary>
        public Ability Ability { get; private set; }

        /// <summary>本节点此刻是否可培育（供窗口判断点击语义）</summary>
        public bool CanStudy { get; private set; }

        /// <summary>不可培育的原因（可培育时为 null）</summary>
        public string BlockReason { get; private set; }

        /// <summary>所属窗口</summary>
        UICityAbilityLearn owner;

        /// <summary>
        /// 绑定数据（每个实例只调一次）。
        /// </summary>
        /// <param name="owner">所属窗口</param>
        /// <param name="ability">能力</param>
        /// <param name="remainCount">剩余可学习次数</param>
        /// <param name="canStudy">当前武将此刻是否可培育</param>
        /// <param name="blockReason">不可培育的原因（可培育时传 null）</param>
        public void Setup(UICityAbilityLearn owner, Ability ability, int remainCount,
            bool canStudy, string blockReason)
        {
            this.owner = owner;
            Ability = ability;
            CanStudy = canStudy;
            BlockReason = blockReason;

            ApplyTypeSprite();

            if (nameLabel != null)
                nameLabel.text = ability != null ? ability.Name : string.Empty;
            if (remainField != null)
                remainField.text = remainCount.ToString();

            SetSelected(false);
            ApplyState();

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }

        /// <summary>
        /// 设置种类底图：按 <see cref="Ability.displayKind"/>（未配置时兜底到
        /// <see cref="Ability.statKind"/> / 隐藏槽种类）取 4 张底图之一，**所有节点都显示**。
        ///
        /// <para>
        /// 底图只表达"这条能力属于哪一类（武力 / 统率 / 政治 / 智力）"，
        /// 不表达"能不能学" —— 后者由 <see cref="lockMask"/> / <see cref="learnableMark"/> 负责。
        /// </para>
        /// </summary>
        void ApplyTypeSprite()
        {
            if (type == null)
                return;

            type.enabled = true;

            UnityEngine.Sprite sprite = AbilityTypeSprite.Get(AbilityTypeSprite.GetKind(Ability), typeSprites);
            if (sprite != null && type.sprite != sprite)
                type.sprite = sprite;
        }

        /// <summary>
        /// 按"可培育 / 不可培育"刷新遮罩与高亮。
        ///
        /// 注意**不要**把 button.interactable 置 false：不可培育的节点仍要能点，
        /// 玩家点了才能在右侧看到它的期间 / 效果 / 不可培育原因；
        /// "能不能按下确定"由窗口的确定键置灰负责。
        /// </summary>
        void ApplyState()
        {
            if (learnableMark != null)
                learnableMark.enabled = CanStudy;
            if (lockMask != null)
                lockMask.enabled = !CanStudy;
        }

        /// <summary>设置选中态</summary>
        /// <param name="selected">是否选中</param>
        public void SetSelected(bool selected)
        {
            if (selectedMark != null)
                selectedMark.enabled = selected;
        }

        /// <summary>
        /// 点击 → 通知窗口。
        /// 不可培育的节点也允许点：窗口会把它的期间 / 效果显示出来，
        /// 只是不会写进选中项，确定键保持置灰。
        /// </summary>
        void OnClick()
        {
            if (owner != null && Ability != null)
                owner.OnSelectAbility(Ability);
        }
    }
}
