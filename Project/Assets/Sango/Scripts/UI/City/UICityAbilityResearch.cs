using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力研究窗口（<c>window_city_ability_research</c>）。
    ///
    /// 对应指令 <see cref="CityAbilityResearch"/>。本类只负责"展示 + 转发"：
    /// 候选与前置判定全部在 <see cref="AbilityStudySystem.GetResearchableAbilities"/> /
    /// <see cref="AbilityStudySystem.StartResearchAt"/> 里，窗口不重复实现。
    ///
    /// 与 <see cref="UICityAbilityLearn"/> 共用同一套路径图控件
    /// （<see cref="UIAbilityStudyArea"/> + <see cref="UIAbilityStudyNode"/>），区别只在于状态口径：
    /// <list type="bullet">
    /// <item>已研究 —— 亮色 + 已研究标记</item>
    /// <item>可研究（前置已满足、未研究、隐藏槽已发现）—— 亮色 + 高亮层</item>
    /// <item>前置未满足 / 隐藏未发现 —— 压暗遮罩</item>
    /// </list>
    ///
    /// 【美术接入清单】在 prefab 根节点挂本脚本，并按字段名拖控件（除 abilityArea 外都可为空）：
    /// <code>
    /// 字段名        控件类型           用途
    /// ──────────────────────────────────────────────────────
    /// windiwTitle   Text               标题（自动填 customTitleName）
    /// abilityArea   UIAbilityStudyArea 能力路径图（必需）
    /// costLabel     UITextField        消耗与耗时
    /// hintLabel     UITextField        操作提示（可为空）
    /// sureButton    Button             确定（未选中时置灰）
    /// cancelButton  Button             取消
    /// </code>
    /// </summary>
    public class UICityAbilityResearch : UGUIWindow
    {
        /// <summary>标题</summary>
        public Text windiwTitle;

        /// <summary>能力路径图</summary>
        public UIAbilityStudyArea abilityArea;

        /// <summary>消耗与耗时</summary>
        public UITextField costLabel;

        /// <summary>操作提示</summary>
        public Text hintLabel;

        /// <summary>确定按钮</summary>
        public Button sureButton;

        /// <summary>取消按钮</summary>
        public Button cancelButton;

        /// <summary>详情面板：种类（基础 / 适性 / 特技）</summary>
        public UITextField kindField;

        /// <summary>详情面板：资金（研究消耗 / 城池持有金，形如「300/6457」）</summary>
        public UITextField goldField;

        /// <summary>详情面板：期间（月）</summary>
        public UITextField turnField;

        /// <summary>详情面板：次数限制（1次 / 3次 / 5次 / 不限）</summary>
        public UITextField timesField;

        /// <summary>详情面板：效果（<see cref="Ability.desc"/>）</summary>
        public UITextField effectField;

        /// <summary>详情面板：特技名（<see cref="Ability.skillId"/> 指向的特技）</summary>
        public UITextField featureField;

        /// <summary>详情面板：特技说明（<see cref="Feature.desc"/>）</summary>
        public UITextField featureDescField;

        /// <summary>
        /// 详情面板当前展示的能力。
        /// 与 <see cref="CityAbilityResearch.selectedAbility"/> 不同：后者只装"真的可研究"的那条，
        /// 前者装"玩家刚点过的任意节点"（灰节点也要能看到它的种类/资金/特效说明）。
        /// </summary>
        Ability displayAbility;

        /// <summary>当前指令</summary>
        CityAbilityResearch currentSystem;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityAbilityResearch>();
            if (currentSystem == null)
                return;

            if (windiwTitle != null)
                windiwTitle.text = currentSystem.customTitleName;
            if (hintLabel != null)
                hintLabel.text = "点击节点选择研究项目，灰色为前置未满足";

            if (abilityArea != null)
            {
                abilityArea.onSelect -= OnAreaSelect;
                abilityArea.onSelect += OnAreaSelect;
            }

            // 窗口可能被复用：清掉上一条浏览记录，详情面板回到"当前已选"或空
            displayAbility = null;

            // 详情面板的控件由美术在 prefab 里按名字摆好（kind/gold/turn/times/effect/feature/featureDesc），
            // 没在 Inspector 里拖的按名字兜底查找一次
            EnsureDetailFields();

            Refresh();
        }

        public override void OnClose()
        {
            if (abilityArea != null)
            {
                abilityArea.onSelect -= OnAreaSelect;
                abilityArea.Clear();
            }
        }

        /// <summary>整体刷新：路径图 + 消耗文案 + 按钮状态</summary>
        public void Refresh()
        {
            if (currentSystem == null)
                return;

            if (abilityArea != null)
            {
                Scenario scenario = Scenario.Cur;
                SangoObjectSet<Ability> abilities =
                    scenario != null && scenario.CommonData != null ? scenario.CommonData.Abilities : null;
                Force force = currentSystem.TargetCity != null ? currentSystem.TargetCity.BelongForce : null;
                // 研究是势力级行为，没有"某个武将"这层 → person 传 null，
                // 节点上不会出现"该武将已学"标记
                abilityArea.Show(abilities, force, null, currentSystem.optionList);
                abilityArea.SetSelected(currentSystem.selectedAbility);
            }

            // 行动力行（studyCostLabel）只放「消耗/当前」，消耗与耗时明细见下方详情面板
            RefreshActionPoint();
            RefreshDetailPanel();
            UpdateSureButton();
        }

        /// <summary>路径图节点点击 → 统一回调</summary>
        /// <param name="ability">被点击的能力</param>
        void OnAreaSelect(Ability ability)
        {
            OnSelectAbility(ability);
        }

        /// <summary>
        /// 节点点击：只有"当前真的可研究"的那条才写入选中项，
        /// 其余只做浏览（高亮跟随点击，便于顺着前置链看依赖）。
        /// 否则玩家点灰节点按确定会收到"无法开始研究"。
        /// </summary>
        /// <param name="ability">被点击的能力</param>
        public void OnSelectAbility(Ability ability)
        {
            // 灰节点也算"浏览"：详情面板照样展示它的种类/资金/效果
            displayAbility = ability;
            currentSystem.SelectAbility(IsResearchableOption(ability) ? ability : null);

            if (abilityArea != null)
                abilityArea.SetSelected(ability);

            RefreshActionPoint();
            RefreshDetailPanel();
            UpdateSureButton();
        }

        /// <summary>
        /// 详情面板控件兜底查找：美术在 prefab 里已经把 6 个 textField 实例改名成
        /// <c>kind / gold / turn / times / effect / feature</c>、特技说明叫 <c>featureDesc</c>，
        /// 这里按 GameObject 名找，Inspector 里拖过的字段优先。
        /// </summary>
        void EnsureDetailFields()
        {
            kindField = kindField != null ? kindField : FindTextField("kind");
            goldField = goldField != null ? goldField : FindTextField("gold");
            turnField = turnField != null ? turnField : FindTextField("turn");
            timesField = timesField != null ? timesField : FindTextField("times");
            effectField = effectField != null ? effectField : FindTextField("effect");
            featureField = featureField != null ? featureField : FindTextField("feature");
            featureDescField = featureDescField != null ? featureDescField : FindTextField("featureDesc");
        }

        /// <summary>按 GameObject 名在窗口内找一个 <see cref="UITextField"/></summary>
        /// <param name="objectName">控件物体名</param>
        /// <returns>控件；找不到返回 null</returns>
        UITextField FindTextField(string objectName)
        {
            UITextField[] fields = GetComponentsInChildren<UITextField>(true);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] != null && fields[i].name == objectName)
                    return fields[i];
            }
            return null;
        }

        /// <summary>
        /// 刷新「行动力」行：显示 <c>研究消耗/军团当前行动力</c>（如 <c>20/59</c>）。
        /// </summary>
        void RefreshActionPoint()
        {
            if (costLabel == null)
                return;

            int cost = AbilityStudySystem.ResearchActionPointCost;
            City city = currentSystem != null ? currentSystem.TargetCity : null;
            Corps corps = city != null ? city.BelongCorps : null;
            costLabel.text = corps != null ? $"{cost}/{corps.ActionPoint}" : cost.ToString();
        }

        /// <summary>
        /// 刷新下方详情面板：种类 / 资金 / 期间 / 次数限制 / 效果 / 特技 / 特技说明。
        /// 没有选中项时逐行清空。
        /// </summary>
        void RefreshDetailPanel()
        {
            Ability ability = displayAbility;
            if (ability == null && currentSystem != null)
                ability = currentSystem.selectedAbility;

            City city = currentSystem != null ? currentSystem.TargetCity : null;
            Scenario scenario = Scenario.Cur;
            Feature feature = null;
            if (ability != null && ability.skillId > 0
                && scenario != null && scenario.CommonData != null && scenario.CommonData.Features != null)
            {
                feature = scenario.CommonData.Features.Get(ability.skillId);
            }

            SetFieldText(kindField, ability != null ? AbilityTreeBuilder.GetCategoryName(ability) : string.Empty);
            // 资金：研究消耗 / 城池持有金
            SetFieldText(goldField, ability != null
                ? $"{AbilityStudySystem.ResearchGoldCost}/{(city != null ? city.gold : 0)}"
                : string.Empty);
            // 期间：timeCost 单位是"月"，界面按原版显示成「N个月」
            SetFieldText(turnField, ability != null
                ? $"{System.Math.Max(ability.timeCost, 1)}个月"
                : string.Empty);
            SetFieldText(timesField, ability != null ? GetLearningLimitText(ability.learningLimit) : string.Empty);
            SetFieldText(effectField, ability != null ? ability.desc : string.Empty);
            SetFieldText(featureField, feature != null ? feature.Name : string.Empty);
            SetFieldText(featureDescField, feature != null ? feature.desc : string.Empty);
        }

        /// <summary>给详情面板的某一行赋值（控件或 label 缺失时静默跳过）</summary>
        /// <param name="field">控件</param>
        /// <param name="text">文本</param>
        static void SetFieldText(UITextField field, string text)
        {
            if (field == null || field.label == null)
                return;
            field.text = text ?? string.Empty;
        }

        /// <summary>学习次数限制的显示文本</summary>
        /// <param name="learningLimit"><see cref="AbilityLearningLimit"/> 的值</param>
        /// <returns>「1次 / 3次 / 5次 / 不限」</returns>
        static string GetLearningLimitText(int learningLimit)
        {
            switch ((AbilityLearningLimit)learningLimit)
            {
                case AbilityLearningLimit.Once: return "1次";
                case AbilityLearningLimit.Three: return "3次";
                case AbilityLearningLimit.Five: return "5次";
                default: return "不限";
            }
        }

        /// <summary>某条能力是否在当前势力的可研究列表里</summary>
        /// <param name="ability">能力</param>
        /// <returns>可研究返回 true</returns>
        bool IsResearchableOption(Ability ability)
        {
            if (ability == null || currentSystem == null)
                return false;
            List<Ability> options = currentSystem.optionList;
            if (options == null)
                return false;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == ability)
                    return true;
            }
            return false;
        }

        /// <summary>刷新确定键可用状态</summary>
        void UpdateSureButton()
        {
            if (sureButton != null)
                sureButton.interactable = currentSystem != null && currentSystem.CanSure();
        }

        /// <summary>确定按钮</summary>
        public void OnSure()
        {
            if (currentSystem != null)
                currentSystem.DoJob();
        }

        /// <summary>取消按钮</summary>
        public void OnCancel()
        {
            if (currentSystem != null)
                currentSystem.Exit();
        }
    }
}
