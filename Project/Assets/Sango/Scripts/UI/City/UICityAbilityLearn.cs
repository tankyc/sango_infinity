using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力培育窗口（<c>window_city_ability_learn</c>），由 <see cref="CityAbilityLearn"/> 的
    /// 二级菜单（培育能力 / 培育适应 / 培育特技）三路共用，只靠 <c>learnCategory</c> 切换内容。
    ///
    /// 本类只负责"展示 + 转发"：
    /// <list type="bullet">
    /// <item>能不能点某条能力、确定键能不能按 —— <see cref="AbilityStudySystem.CanStudy"/>；</item>
    /// <item>点确定之后真正派任务 —— <see cref="AbilityStudySystem.BeginStudy"/>。</item>
    /// </list>
    /// 二者同源，因此不会出现"界面点得动、执行时却说条件不满足"。
    ///
    /// 【版面】<c>abilityTreeRoot/viewport/content</c> 是子项父节点（挂 <c>GridLayoutGroup</c> 自动排版），
    /// <c>node_template</c> 是创建模板（挂 <see cref="UIAbilityLearnNode"/>），运行时按能力条数复制。
    /// 与研究界面（六边形路径图）是两套版面，互不复用。
    ///
    /// 【美术接入清单】在 prefab 根节点挂本脚本，并按字段名拖控件（都可为空）：
    /// <code>
    /// 字段名              控件类型     用途
    /// ─────────────────────────────────────────────────────────
    /// windiwTitle         Text         标题（自动填当前分类名）
    /// targetPersonItems   UIPersonItem 对象武将头像 + 名字（点"对象武将"按钮换人）
    /// targetStatusItem    UIStatusItem 对象武将的能力条
    /// actionPointField    UITextField  行动力（消耗/当前，形如 20/59）
    /// turnField           UITextField  期间（研习耗时）
    /// effectField         UITextField  效果（能力说明；不可培育时附带原因）
    /// nodeRoot            RectTransform 子项父节点（GridLayoutGroup 容器）
    /// nodeTemplate        GameObject    节点模板（挂 UIAbilityLearnNode）
    /// sureButton          Button       确定（选中且可执行时才高亮）
    /// cancelButton        Button       取消
    /// </code>
    /// </summary>
    public class UICityAbilityLearn : UGUIWindow
    {
        /// <summary>标题</summary>
        public Text windiwTitle;

        /// <summary>对象武将（头像 + 名字）</summary>
        public UIPersonItem targetPersonItems;

        /// <summary>对象武将状态条</summary>
        public UIStatusItem targetStatusItem;

        /// <summary>行动力（消耗/当前）</summary>
        public UITextField actionPointField;

        /// <summary>期间（研习耗时）</summary>
        public UITextField turnField;

        /// <summary>效果（能力说明）</summary>
        public UITextField effectField;

        /// <summary>子项父节点（节点容器，挂 GridLayoutGroup）</summary>
        public RectTransform nodeRoot;

        /// <summary>节点模板（挂 <see cref="UIAbilityLearnNode"/>）</summary>
        public GameObject nodeTemplate;

        /// <summary>确定按钮</summary>
        public Button sureButton;

        /// <summary>取消按钮</summary>
        public Button cancelButton;

        /// <summary>当前指令</summary>
        CityAbilityLearn currentSystem;

        /// <summary>已生成的节点（用于刷新选中态 / 清理）</summary>
        readonly List<UIAbilityLearnNode> nodes = new List<UIAbilityLearnNode>();

        /// <summary>
        /// 详情区当前展示的能力。
        /// 与 <see cref="CityAbilityLearn.selectedAbility"/> 不同：后者只装"真的能培育"的那条，
        /// 前者装"玩家刚点过的任意节点"，灰节点也能看到它的期间 / 效果 / 不可培育原因。
        /// </summary>
        Ability displayAbility;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityAbilityLearn>();
            if (currentSystem == null)
                return;

            // 模板本体不能留在容器里，否则 GridLayoutGroup 会把它当成一个占位格子
            if (nodeTemplate != null)
                nodeTemplate.SetActive(false);

            displayAbility = null;
            Refresh();
        }

        public override void OnClose()
        {
            ClearNodes();
            displayAbility = null;
        }

        /// <summary>整体刷新：武将区 + 节点网格 + 详情 + 确定键</summary>
        public void Refresh()
        {
            if (currentSystem == null)
                return;

            if (windiwTitle != null)
                windiwTitle.text = currentSystem.CategoryTitle;

            UpdatePersonArea();
            RebuildNodes();
            RefreshDetail();
            UpdateSureButton();
        }

        /// <summary>刷新对象武将区</summary>
        void UpdatePersonArea()
        {
            Person person = currentSystem.selectedPerson;
            if (targetPersonItems != null)
                targetPersonItems.SetPerson(person);
            if (targetStatusItem != null)
                targetStatusItem.SetPerson(person);
        }

        /// <summary>
        /// 重建节点网格：一条**势力已研究完毕**的能力一个节点。
        /// 节点上的"剩"显示剩余可学习次数，能否点击由 <see cref="AbilityStudySystem.CanStudy"/> 决定。
        /// </summary>
        void RebuildNodes()
        {
            ClearNodes();

            if (nodeRoot == null || nodeTemplate == null)
                return;

            List<Ability> options = currentSystem.optionList;
            if (options == null || options.Count == 0)
                return;

            Person person = currentSystem.selectedPerson;
            City city = currentSystem.TargetCity;
            Scenario scenario = Scenario.Cur;
            Force force = city != null ? city.BelongForce : (person != null ? person.BelongForce : null);

            for (int i = 0; i < options.Count; i++)
            {
                Ability ability = options[i];
                if (ability == null)
                    continue;

                int remain = AbilityStudySystem.GetRemainLearnCount(force, ability);
                bool canStudy = AbilityStudySystem.CanStudy(person, ability, city, scenario,
                    out int gain, out string reason);

                GameObject go = Instantiate(nodeTemplate, nodeRoot);
                go.SetActive(true);
                go.name = "node_" + ability.Id;

                UIAbilityLearnNode node = go.GetComponent<UIAbilityLearnNode>();
                if (node == null)
                {
                    Destroy(go);
                    continue;
                }
                node.Setup(this, ability, remain, canStudy, reason);
                node.SetSelected(ability == currentSystem.selectedAbility);
                nodes.Add(node);
            }
        }

        /// <summary>清理已生成的节点与列表项</summary>
        void ClearNodes()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                    Destroy(nodes[i].gameObject);
            }
            nodes.Clear();
        }

        /// <summary>
        /// 刷新详情区：行动力 / 期间 / 效果。
        /// 没选任何节点时逐行清空。
        /// </summary>
        void RefreshDetail()
        {
            Ability ability = displayAbility != null ? displayAbility : currentSystem.selectedAbility;

            // 行动力：本条指令消耗 / 军团当前行动力（对齐研究界面的显示口径）
            City city = currentSystem != null ? currentSystem.TargetCity : null;
            Corps corps = city != null ? city.BelongCorps : null;
            int cost = AbilityStudySystem.LearnActionPointCost;
            SetFieldText(actionPointField, corps != null ? $"{cost}/{corps.ActionPoint}" : cost.ToString());

            // 期间：研习闭关时长
            SetFieldText(turnField, ability != null ? $"{AbilityStudySystem.StudyTurns}旬" : string.Empty);

            // 效果：能力说明；不可培育时把原因附在后面，玩家点灰节点也能知道差在哪
            if (ability == null)
            {
                SetFieldText(effectField, string.Empty);
                return;
            }

            string desc = ability.desc ?? string.Empty;
            if (displayAbility != null && !CanStudyAbility(displayAbility))
                desc = desc + "\n（" + GetBlockReason(displayAbility) + "）";
            SetFieldText(effectField, desc);
        }

        /// <summary>给详情区某个控件赋值（控件或 label 缺失时静默跳过）</summary>
        /// <param name="field">控件</param>
        /// <param name="text">文本</param>
        static void SetFieldText(UITextField field, string text)
        {
            if (field == null || field.label == null)
                return;
            field.text = text ?? string.Empty;
        }

        /// <summary>刷新确定键：只有"选中且此刻真的可执行"才高亮</summary>
        void UpdateSureButton()
        {
            if (sureButton != null)
                sureButton.interactable = currentSystem != null && currentSystem.CanSure();
        }

        /// <summary>某条能力对当前武将是否可培育</summary>
        /// <param name="ability">能力</param>
        /// <returns>可培育返回 true</returns>
        bool CanStudyAbility(Ability ability)
        {
            if (ability == null || currentSystem == null)
                return false;
            return AbilityStudySystem.CanStudy(currentSystem.selectedPerson, ability,
                currentSystem.TargetCity, Scenario.Cur, out int gain, out string reason);
        }

        /// <summary>取不可培育原因</summary>
        /// <param name="ability">能力</param>
        /// <returns>原因文案</returns>
        string GetBlockReason(Ability ability)
        {
            if (ability == null || currentSystem == null)
                return "未选择能力";
            AbilityStudySystem.CanStudy(currentSystem.selectedPerson, ability,
                currentSystem.TargetCity, Scenario.Cur, out int gain, out string reason);
            return reason ?? "条件不满足";
        }

        /// <summary>
        /// 节点点击。
        ///
        /// 只有"当前真的能培育"的那条才写入 <c>selectedAbility</c>（决定确定键是否高亮），
        /// 其余只做浏览 —— 详情区照样显示它的期间 / 效果 / 不可培育原因。
        /// </summary>
        /// <param name="ability">被点击的能力</param>
        public void OnSelectAbility(Ability ability)
        {
            displayAbility = ability;
            currentSystem.SelectAbility(CanStudyAbility(ability) ? ability : null);

            // 只刷新选中态，不重建节点（避免点击后网格跳动）
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                    nodes[i].SetSelected(nodes[i].Ability == ability);
            }

            RefreshDetail();
            UpdateSureButton();
        }

        /// <summary>对象武将按钮：弹出武将选择界面（只在可培育武将里挑）</summary>
        public void OnSelectPerson()
        {
            if (currentSystem == null || currentSystem.learnablePersons == null
                || currentSystem.learnablePersons.Count == 0)
                return;

            PersonSelectSystem selector = GameSystem.GetSystem<PersonSelectSystem>();
            if (selector == null)
                return;
            // 第三个之后的参数是"已选结果列表"，选择器内部会直接 new List(...) 并 RemoveAll，
            // 传 null 会抛 ArgumentNullException —— 必须给一个空列表。
            selector.Start(currentSystem.learnablePersons, new List<Person>(), 1,
                OnPersonChanged, null, "选择培育武将");
        }

        /// <summary>换人回调</summary>
        /// <param name="persons">选中的武将</param>
        void OnPersonChanged(List<Person> persons)
        {
            if (persons == null || persons.Count <= 0)
                return;
            currentSystem.SelectPerson(persons[0]);
            displayAbility = null;
            Refresh();
        }

        /// <summary>确定：完成任务链并派发研习任务</summary>
        public void OnSure()
        {
            if (currentSystem != null)
                currentSystem.DoJob();
        }

        /// <summary>取消：完成任务链</summary>
        public void OnCancel()
        {
            if (currentSystem != null)
                currentSystem.CancelJob();
        }
    }
}
