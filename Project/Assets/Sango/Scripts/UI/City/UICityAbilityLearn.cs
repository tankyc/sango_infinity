using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力培育窗口（<c>window_city_ability_learn</c>）。
    ///
    /// 对应指令 <see cref="CityAbilityLearn"/>。本类只负责"展示 + 转发"，
    /// 所有判定在 <see cref="AbilityStudySystem.GetLearnableAbilitiesFor"/> 与
    /// <see cref="AbilityStudySystem.BeginStudy"/> 里，窗口不重复实现。
    ///
    /// 【美术接入清单】在 prefab 根节点挂本脚本，并按下表创建控件、把控件拖到同名字段：
    /// <code>
    /// 字段名              控件类型         用途
    /// ─────────────────────────────────────────────────────────
    /// windiwTitle         Text             标题（自动填 customTitleName）
    /// targetPersonItems   UIPersonItem     当前武将头像 + 名字
    /// targetStatusItem    UIStatusItem     当前武将的能力条
    /// personCountLabel    UITextField      可培育人数 / 换人提示
    /// studyCostLabel      UITextField      消耗与耗时
    /// optionRoot          RectTransform    能力列表容器（列表项挂这里）
    /// optionItemPrefab    GameObject       列表项模板（挂 UIAbilityStudyItem）
    /// emptyHint           GameObject       无项时的提示，可为空
    /// sureButton          Button           确认（未选满时置灰）
    /// cancelButton        Button           取消
    /// </code>
    /// </summary>
    public class UICityAbilityLearn : UGUIWindow
    {
        /// <summary>标题</summary>
        public Text windiwTitle;

        /// <summary>当前武将</summary>
        public UIPersonItem targetPersonItems;

        /// <summary>当前武将状态</summary>
        public UIStatusItem targetStatusItem;

        /// <summary>可培育人数 / 换人提示</summary>
        public UITextField personCountLabel;

        /// <summary>消耗与耗时</summary>
        public UITextField studyCostLabel;

        /// <summary>能力列表容器</summary>
        public RectTransform optionRoot;

        /// <summary>列表项模板（需挂 UIAbilityStudyItem）</summary>
        public GameObject optionItemPrefab;

        /// <summary>无项时的提示（可为空）</summary>
        public GameObject emptyHint;

        /// <summary>确认按钮</summary>
        public Button sureButton;

        /// <summary>取消按钮</summary>
        public Button cancelButton;

        /// <summary>当前指令</summary>
        CityAbilityLearn currentSystem;

        /// <summary>已生成的列表项（用于复用 / 清理）</summary>
        readonly List<UIAbilityStudyItem> items = new List<UIAbilityStudyItem>();

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityAbilityLearn>();
            if (currentSystem == null)
                return;

            if (windiwTitle != null)
                windiwTitle.text = currentSystem.customTitleName;
            if (studyCostLabel != null)
                studyCostLabel.text = currentSystem.GetStudyCostText();

            Refresh();
        }

        public override void OnClose()
        {
            ClearItems();
        }

        /// <summary>
        /// 整体刷新：武将区 + 列表 + 确认键状态。
        /// </summary>
        public void Refresh()
        {
            if (currentSystem == null)
                return;

            UpdatePersonArea();
            RebuildOptionList();
            UpdateSureButton();
        }

        /// <summary>刷新武将区</summary>
        void UpdatePersonArea()
        {
            Person person = currentSystem.selectedPerson;
            if (targetPersonItems != null)
                targetPersonItems.SetPerson(person);
            if (targetStatusItem != null)
                targetStatusItem.SetPerson(person);

            if (personCountLabel != null)
            {
                int count = currentSystem.learnablePersons != null ? currentSystem.learnablePersons.Count : 0;
                personCountLabel.text = count > 1
                    ? $"可培育 {count} 人（点击可更换）"
                    : (count == 1 ? "可培育 1 人" : "本城暂无可培育武将");
            }
        }

        /// <summary>重建能力列表</summary>
        void RebuildOptionList()
        {
            ClearItems();

            List<Ability> options = currentSystem.optionList;
            bool hasOption = options != null && options.Count > 0;
            if (emptyHint != null)
                emptyHint.SetActive(!hasOption);

            if (!hasOption || optionRoot == null || optionItemPrefab == null)
                return;

            for (int i = 0; i < options.Count; i++)
            {
                GameObject go = Instantiate(optionItemPrefab, optionRoot);
                go.SetActive(true);
                UIAbilityStudyItem item = go.GetComponent<UIAbilityStudyItem>();
                if (item == null)
                {
                    Destroy(go);
                    continue;
                }
                item.Setup(this, options[i]);
                items.Add(item);
            }
        }

        /// <summary>清理列表项</summary>
        void ClearItems()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    Destroy(items[i].gameObject);
            }
            items.Clear();
        }

        /// <summary>刷新确认键可用状态</summary>
        void UpdateSureButton()
        {
            if (sureButton != null)
                sureButton.interactable = currentSystem.CanSure();
        }

        /// <summary>列表项点击回调</summary>
        /// <param name="ability">被点击的能力</param>
        public void OnSelectAbility(Ability ability)
        {
            currentSystem.SelectAbility(ability);

            // 只刷新选中态，不重建列表（避免点击后滚动位置跳回顶部）
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    items[i].SetSelected(items[i].Ability == ability);
            }
            UpdateSureButton();
        }

        /// <summary>换人：弹通用武将选择器</summary>
        public void OnChangePerson()
        {
            if (currentSystem.learnablePersons == null || currentSystem.learnablePersons.Count <= 1)
                return;

            PersonSelectSystem selector = GameSystem.GetSystem<PersonSelectSystem>();
            if (selector == null)
                return;
            selector.Start(currentSystem.learnablePersons, null, 1, OnPersonChanged, null, "选择培育武将");
        }

        /// <summary>换人回调</summary>
        /// <param name="persons">选中的武将</param>
        void OnPersonChanged(List<Person> persons)
        {
            if (persons == null || persons.Count <= 0)
                return;
            currentSystem.SelectPerson(persons[0]);
            Refresh();
        }

        /// <summary>确认按钮</summary>
        public void OnSure()
        {
            currentSystem.DoJob();
        }

        /// <summary>取消按钮</summary>
        public void OnCancel()
        {
            currentSystem.Exit();
        }
    }
}
