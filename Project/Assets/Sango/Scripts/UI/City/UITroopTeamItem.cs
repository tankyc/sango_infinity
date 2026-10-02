using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 出征推荐队伍列表项
    /// 挂在 window_city_create_troop 的 recomand/node/Scroll View/Viewport/content/team 节点上，
    /// 用于显示一支推荐队伍的成员、说明，并提供"应用 / 删除"两个操作。
    ///
    /// 数据来源：<see cref="TroopTeamService.BuildList"/> 产出的 <see cref="TroopTeamCandidate"/>。
    /// 成员按**槽位**展示（<see cref="TroopTeamCandidate.slotPersons"/> + <see cref="TroopTeamCandidate.memberStates"/>），
    /// 每个槽位用颜色区分状态，玩家一眼能看出是哪个人不在 / 被占用：
    ///   · 可用 → <see cref="memberReadyColor"/>（默认白）；
    ///   · 被占用（在部队 / 执行任务）→ <see cref="memberBusyColor"/>（默认红）；
    ///   · 不在本城 → <see cref="memberMissingColor"/>（默认灰）。
    /// </summary>
    public class UITroopTeamItem : MonoBehaviour
    {
        /// <summary>队伍成员显示（最多 3 个，索引 0 为主将）</summary>
        public UIPersonItem[] personItems;

        /// <summary>成员可用时的显示色（默认白）</summary>
        public Color memberReadyColor = Color.white;

        /// <summary>成员被占用（在部队 / 执行任务）时的显示色（默认红）</summary>
        public Color memberBusyColor = new Color(1f, 0.35f, 0.35f);

        /// <summary>成员不在本城时的显示色（默认灰）</summary>
        public Color memberMissingColor = new Color(0.5f, 0.5f, 0.5f);

        /// <summary>队伍说明文本</summary>
        public Text descLabel;

        /// <summary>应用按钮：把该队伍套用到当前编队</summary>
        public Button applyButton;

        /// <summary>删除按钮：删除玩家自建的队伍</summary>
        public Button deleteButton;

        /// <summary>列表下标（由列表逻辑赋值，回调时用于定位）</summary>
        public int index;

        /// <summary>当前显示的队伍数据（可能为空 = 该列表项未被使用）</summary>
        public TroopTeamCandidate candidate;

        /// <summary>列表项事件回调</summary>
        /// <param name="item">触发事件的列表项</param>
        public delegate void OnItemEvent(UITroopTeamItem item);

        /// <summary>点击"应用"时回调</summary>
        public OnItemEvent onApply;

        /// <summary>点击"删除"时回调</summary>
        public OnItemEvent onDelete;

        /// <summary>
        /// 设置列表下标
        /// </summary>
        /// <param name="i">下标</param>
        public UITroopTeamItem SetIndex(int i)
        {
            index = i;
            return this;
        }

        /// <summary>
        /// 设置显示推荐的队伍
        /// </summary>
        /// <param name="data">队伍候选数据，传空表示清空该项的显示</param>
        public UITroopTeamItem SetData(TroopTeamCandidate data)
        {
            candidate = data;

            if (data == null)
            {
                SetMembers(null);
                if (descLabel != null) descLabel.text = "";
                if (applyButton != null) applyButton.interactable = false;
                if (deleteButton != null) deleteButton.interactable = false;
                return this;
            }

            // 逐槽位显示成员，并按状态上色（可用 / 被占用 / 不在本城）
            SetMembers(data);

            if (descLabel != null)
                descLabel.text = BuildDescText(data);

            // 只有可用的队伍才能套用（身份成员凑齐且兵力够）
            if (applyButton != null)
                applyButton.interactable = data.IsReady;

            // 推荐模板是只读的，只有玩家自建的队伍可以删除
            if (deleteButton != null)
                deleteButton.interactable = data.custom;

            return this;
        }

        /// <summary>
        /// 刷新成员显示：逐槽位设置人物与状态色（空槽清空）
        /// </summary>
        /// <param name="data">队伍候选数据（传空 = 全部清空）</param>
        void SetMembers(TroopTeamCandidate data)
        {
            if (personItems == null) return;

            for (int i = 0; i < personItems.Length; i++)
            {
                if (personItems[i] == null) continue;

                Person person = null;
                TroopTeamMemberState state = TroopTeamMemberState.Empty;

                if (data != null)
                {
                    if (data.slotPersons != null && i < data.slotPersons.Length)
                        person = data.slotPersons[i];
                    if (data.memberStates != null && i < data.memberStates.Length)
                        state = data.memberStates[i];
                }

                personItems[i].SetPerson(person);
                personItems[i].SetColor(GetMemberColor(state));
            }
        }

        /// <summary>
        /// 槽位状态对应的显示颜色
        /// </summary>
        /// <param name="state">槽位状态</param>
        Color GetMemberColor(TroopTeamMemberState state)
        {
            switch (state)
            {
                case TroopTeamMemberState.Busy: return memberBusyColor;
                case TroopTeamMemberState.Missing: return memberMissingColor;
                default: return memberReadyColor;
            }
        }

        /// <summary>
        /// 拼装说明文本：队伍名 + 说明 + 不可用的原因
        /// </summary>
        /// <param name="data">队伍候选数据</param>
        string BuildDescText(TroopTeamCandidate data)
        {
            string text = data.name;
            if (!string.IsNullOrEmpty(data.desc))
                text = string.IsNullOrEmpty(text) ? data.desc : text + "：" + data.desc;

            // 不可用时补充原因（兵力不足 / 人员被占用），方便玩家知道为什么套用不了
            if (!data.IsReady && !string.IsNullOrEmpty(data.statusText))
                text += "（<color=#ff0000>" + data.statusText + "</color>）";

            return text;
        }

        /// <summary>
        /// 应用按钮点击（在预制体上把 Button.onClick 绑定到本方法）
        /// </summary>
        public void OnApply()
        {
            if (candidate == null) return;

            if (!candidate.IsReady)
            {
                Log.Warning("推荐队伍[" + candidate.name + "]当前不可用,无法套用");
                return;
            }

            if (onApply != null)
                onApply(this);
        }

        /// <summary>
        /// 删除按钮点击（在预制体上把 Button.onClick 绑定到本方法）
        /// </summary>
        public void OnDelete()
        {
            if (candidate == null) return;

            if (!candidate.custom)
            {
                Log.Warning("推荐队伍[" + candidate.name + "]是预制作模板,不能删除");
                return;
            }

            if (onDelete != null)
                onDelete(this);
        }
    }
}
