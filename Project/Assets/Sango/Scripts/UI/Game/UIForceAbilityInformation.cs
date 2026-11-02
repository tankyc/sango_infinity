using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 势力能力情报窗口（window_information_force_ability）：左侧列出全部存活势力，
    /// 右侧只读展示被选中势力的能力路径图，右下角显示选中能力的说明。
    ///
    /// <para>
    /// 与「能力研究」窗口（<see cref="UICityAbilityResearch"/>）复用同一个路径图控件
    /// <see cref="UIAbilityStudyArea"/>：六边形布局、方向箭头、隐藏槽的出现演出都由它负责，
    /// 本类只做「换势力 → 重建图 → 刷状态」。
    /// 两者差别在于本窗口是**只读**：没有城池、没有行动力，也不下单。
    /// </para>
    ///
    /// <para>
    /// 【节点状态口径】沿用研究界面的那一套（<see cref="UIAbilityStudyArea.Show"/> 内部判定）：
    /// 已研究 = 亮色 + 已研究标记；可研究（前置满足、未研究） = 高亮层；其余压暗。
    /// 可研究候选直接问 <see cref="AbilityStudySystem.GetResearchableAbilities"/>，
    /// 不在本类里重算前置，避免"列表能选、界面却显示不可选"这类分叉。
    /// </para>
    ///
    /// <para>
    /// 【原版无对应实现】能力表界面在原版 exe 内（Open311 只有
    /// `s11_sys_scen_ability.cpp` 的判定逻辑，没有界面代码），因此本窗口按项目既有的
    /// 情报窗口范式 + 研究界面控件自研；详情区的取值口径与研究界面完全一致。
    /// </para>
    /// </summary>
    public class UIForceAbilityInformation : UGUIWindow
    {
        /// <summary>窗口标题</summary>
        public Text windiwTitle;

        /// <summary>左侧势力列表</summary>
        public UIObjectList uIObjectList;

        /// <summary>能力路径图（ScrollRect/Content 上挂的 UIAbilityStudyArea）</summary>
        public UIAbilityStudyArea abilityArea;

        /// <summary>顶部状态行：势力名</summary>
        public UITextField forceField;

        /// <summary>顶部状态行：正在研究的能力</summary>
        public UITextField researchingField;

        /// <summary>顶部状态行：研究剩余天数</summary>
        public UITextField leftTimeField;

        /// <summary>详情区：种类（基础 / 适性 / 特技）</summary>
        public UITextField kindField;

        /// <summary>详情区：研究消耗（金钱）</summary>
        public UITextField goldField;

        /// <summary>详情区：期间（月）</summary>
        public UITextField turnField;

        /// <summary>详情区：次数限制</summary>
        public UITextField timesField;

        /// <summary>详情区：特技名</summary>
        public UITextField featureField;

        /// <summary>详情区：效果说明</summary>
        public UITextField effectField;

        /// <summary>详情区：特技说明</summary>
        public UITextField featureDescField;

        /// <summary>驱动本窗口的系统</summary>
        ForceAbilityInformation currentSystem;

        /// <summary>当前查看的势力</summary>
        Force Target;

        /// <summary>详情区当前展示的能力（点过的任意节点，含未满足前置的灰节点）</summary>
        Ability displayAbility;

        protected override void Awake()
        {
            base.Awake();
            ResolveFields();
        }

        /// <summary>
        /// 兜底取控件引用：prefab 上这些节点都已存在（挂在窗口内层容器 <c>root</c> 下），
        /// Inspector 漏绑时按固定路径再找一次。
        /// </summary>
        void ResolveFields()
        {
            if (windiwTitle == null)
                windiwTitle = GetComponent<Text>("root/win_frame/title");
            if (abilityArea == null)
                abilityArea = GetComponent<UIAbilityStudyArea>("root/abilityTreeRoot/viewport/content");

            if (forceField == null)
                forceField = GetComponent<UITextField>("root/force");
            if (researchingField == null)
                researchingField = GetComponent<UITextField>("root/researching");
            if (leftTimeField == null)
                leftTimeField = GetComponent<UITextField>("root/lefttime");

            if (kindField == null)
                kindField = GetComponent<UITextField>("root/kind");
            if (goldField == null)
                goldField = GetComponent<UITextField>("root/gold");
            if (turnField == null)
                turnField = GetComponent<UITextField>("root/turn");
            if (timesField == null)
                timesField = GetComponent<UITextField>("root/times");
            if (featureField == null)
                featureField = GetComponent<UITextField>("root/feature");
            if (effectField == null)
                effectField = GetComponent<UITextField>("root/effect");
            if (featureDescField == null)
                featureDescField = GetComponent<UITextField>("root/featureDesc");
        }

        /// <summary>无参打开（Window.Open(name) 走这里）</summary>
        public override void OnOpen()
        {
            base.OnOpen();
            Initialize(GameSystem.GetSystem<ForceAbilityInformation>());
        }

        /// <summary>带参打开（ForceAbilityInformation.OnEnter 走这里）</summary>
        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);

            ForceAbilityInformation system = null;
            if (objects != null && objects.Length > 0)
                system = objects[0] as ForceAbilityInformation;
            if (system == null)
                system = GameSystem.GetSystem<ForceAbilityInformation>();

            Initialize(system);
        }

        /// <summary>窗口关闭：退订节点点击并清图，避免窗口复用后重复回调</summary>
        public override void OnClose()
        {
            if (abilityArea != null)
            {
                abilityArea.onSelect -= OnAreaSelect;
                abilityArea.Clear();
            }
            base.OnClose();
        }

        /// <summary>初始化：标题 + 左侧列表 + 右侧能力图</summary>
        /// <param name="system">势力能力情报系统</param>
        void Initialize(ForceAbilityInformation system)
        {
            currentSystem = system;
            if (currentSystem == null)
            {
                Log.Error("能力表打开失败：ForceAbilityInformation 系统不存在");
                return;
            }

            ResolveFields();

            if (windiwTitle != null)
                windiwTitle.text = string.IsNullOrEmpty(currentSystem.Name) ? "能力表" : currentSystem.Name;

            if (abilityArea != null)
            {
                abilityArea.onSelect -= OnAreaSelect;
                abilityArea.onSelect += OnAreaSelect;
            }

            // 列表为空时不能进 UIObjectList.Init：它的滚动条比例会做 totalCount 相关的除法，
            // 空数据会算出 NaN 并让 Scrollbar 布局异常。
            if (currentSystem.all_objects == null || currentSystem.all_objects.Count == 0)
            {
                Log.Warning("能力表：没有可显示的存活势力");
                ResetDetail();
                return;
            }

            if (uIObjectList != null)
            {
                uIObjectList.Init(currentSystem.all_objects, ForceSortFunction.SortByName, OnObjectSelected);
                uIObjectList.SelectDefaultObject(currentSystem.Target);
            }

            Show(currentSystem.Target);
        }

        /// <summary>左侧列表选中某个势力</summary>
        /// <param name="index">势力在 all_objects 中的下标</param>
        void OnObjectSelected(int index)
        {
            if (currentSystem == null || currentSystem.all_objects == null)
                return;
            if (index < 0 || index >= currentSystem.all_objects.Count)
                return;

            Show(currentSystem.all_objects[index] as Force);
        }

        /// <summary>
        /// 显示某个势力的能力图。
        ///
        /// <para>
        /// 能力表来自 <c>Scenario.Cur.CommonData.Abilities</c>；隐藏能力（<c>hiddenSlot &gt;= 0</c>）
        /// 是否出现由 <see cref="UIAbilityStudyArea"/> 内部按势力槽位过滤，
        /// 这里不重复判断，避免两处口径不一致。
        /// </para>
        /// </summary>
        /// <param name="force">目标势力</param>
        public void Show(Force force)
        {
            Target = force;
            displayAbility = null;

            if (abilityArea == null)
            {
                Log.Error("能力表显示失败：abilityArea 未绑定");
                return;
            }

            if (force == null)
            {
                RefreshStatus(null);
                ResetDetail();
                return;
            }

            Scenario scenario = Scenario.Cur;
            SangoObjectSet<Ability> abilities =
                scenario != null && scenario.CommonData != null ? scenario.CommonData.Abilities : null;

            // 可研究候选：与研究界面同一个入口，保证"灰/亮"判定一致
            List<Ability> researchable = AbilityStudySystem.GetResearchableAbilities(force, scenario);

            // person 传 null —— 能力研究是势力级行为，不是某个武将的已学状态
            abilityArea.Show(abilities, force, null, researchable);
            abilityArea.SetSelected(null);

            if (abilityArea.NodeCount == 0)
                Log.Warning($"能力表为空：{force.Name} 没有能力数据（检查 Abilities.json 与势力隐藏槽配置）");

            RefreshStatus(force);
            ResetDetail();
        }

        /// <summary>刷新顶部状态行：势力 / 正在研究的能力 / 剩余天数</summary>
        /// <param name="force">当前势力</param>
        void RefreshStatus(Force force)
        {
            if (forceField != null)
                forceField.text = force != null ? force.Name : "--";

            Ability researching = null;
            if (force != null && force.researchingAbilityId >= 0)
                researching = AbilityStudySystem.GetAbility(Scenario.Cur, force.researchingAbilityId);

            if (researchingField != null)
                researchingField.text = researching != null ? $"研究中：{researching.Name}" : "研究中：无";

            if (leftTimeField != null)
            {
                // researchingTurnsLeft 的单位是"回合"，项目一年 36 回合、一回合 10 天，
                // 因此显示时按 ×10 换成天数（与研究界面的「N 日」口径一致）
                leftTimeField.text = researching != null
                    ? $"{System.Math.Max(force.researchingTurnsLeft, 0) * 10}天"
                    : "--";
            }
        }

        /// <summary>路径图节点点击</summary>
        /// <param name="ability">被点击的能力</param>
        void OnAreaSelect(Ability ability)
        {
            displayAbility = ability;

            if (abilityArea != null)
                abilityArea.SetSelected(ability);

            RefreshDetail(ability);
        }

        /// <summary>详情区复位（未选中时的占位）</summary>
        void ResetDetail()
        {
            displayAbility = null;
            SetFieldText(kindField, "--");
            SetFieldText(goldField, "--");
            SetFieldText(turnField, "--");
            SetFieldText(timesField, "--");
            SetFieldText(featureField, "--");
            SetFieldText(effectField, "请选择能力");
            SetFieldText(featureDescField, string.Empty);
        }

        /// <summary>
        /// 刷新详情区：种类 / 研究消耗 / 期间 / 次数限制 / 特技 / 效果 / 特技说明。
        /// 取值口径与 <see cref="UICityAbilityResearch.RefreshDetailPanel"/> 保持一致。
        /// </summary>
        /// <param name="ability">选中的能力</param>
        void RefreshDetail(Ability ability)
        {
            if (ability == null)
            {
                ResetDetail();
                return;
            }

            Scenario scenario = Scenario.Cur;
            Feature feature = null;
            if (ability.skillId > 0 && scenario != null && scenario.CommonData != null
                && scenario.CommonData.Features != null)
            {
                feature = scenario.CommonData.Features.Get(ability.skillId);
            }

            // 种类：查看界面补一句掌握状态，否则玩家只能靠节点图标判断
            string kind = AbilityTreeBuilder.GetCategoryName(ability);
            if (Target != null && Target.HasAbility(ability.Id))
                kind = $"{kind}（已掌握）";
            SetFieldText(kindField, kind);

            // 资金：查看界面没有出资城池，只显示研究一条能力的标准消耗
            SetFieldText(goldField, AbilityStudySystem.ResearchGoldCost.ToString());

            // 期间：timeCost 单位是"月"
            SetFieldText(turnField, $"{System.Math.Max(ability.timeCost, 1)}个月");

            SetFieldText(timesField, GetLearningLimitText(ability.learningLimit));
            SetFieldText(featureField, feature != null ? feature.Name : "无");
            SetFieldText(effectField, string.IsNullOrEmpty(ability.desc) ? "--" : ability.desc);
            SetFieldText(featureDescField, feature != null ? feature.desc : string.Empty);
        }

        /// <summary>给详情区的某一行赋值（控件或 label 缺失时静默跳过）</summary>
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

        /// <summary>返回：退栈到上一层系统</summary>
        public void OnCancel()
        {
            if (currentSystem != null)
                currentSystem.Back();
        }

        /// <summary>决定：查看界面没有可提交的动作，等同返回</summary>
        public void OnSure()
        {
            if (currentSystem != null)
                currentSystem.Back();
        }

        /// <summary>势力列表上翻</summary>
        public void UpShow()
        {
            if (uIObjectList != null)
                uIObjectList.UpShow();
        }

        /// <summary>势力列表下翻</summary>
        public void DownShow()
        {
            if (uIObjectList != null)
                uIObjectList.DownShow();
        }
    }
}
