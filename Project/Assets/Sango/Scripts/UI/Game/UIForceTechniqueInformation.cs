using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 势力技巧情报窗口（window_information_force_techique）：左侧列出全部存活势力，
    /// 右侧只读展示被选中势力的技巧树，右下角显示选中技巧的说明。
    ///
    /// <para>
    /// 与「技巧研究」窗口（<see cref="UITechnique"/>）复用同一套技巧树控件
    /// <see cref="UITechniqueArea"/>：树形布局、连线、等级标题都由它生成，
    /// 本类只负责「换势力 → 重建树 → 刷状态」。
    /// 两者差别在于本窗口是**只读**，因此不做费用/队伍/可研判定以外的交互。
    /// </para>
    ///
    /// <para>
    /// 右侧状态与 <see cref="Technique"/> 的两个判定一一对应：
    /// · <see cref="Technique.IsValid"/>   → 已掌握（节点显示完成图标）；
    /// · <see cref="Technique.CanResearch"/> → 前置已满足且未掌握（否则置灰遮罩）。
    /// 已掌握的技巧因为 <c>HasTechnique</c> 为真，<c>CanResearch</c> 必为假，
    /// 所以会同时出现"完成图标 + 灰遮罩"，这与研究界面的表现一致，不是 bug。
    /// </para>
    ///
    /// <para>
    /// 【原版无对应实现】技巧表界面在原版 exe 内，Open311 未提供源码
    /// （只有 s11_enum_preset.h 的 CampaignId_Techniques 枚举），
    /// 因此详情区的"必要技巧 / 相关能力 / 效果"三行沿用研究界面
    /// （<see cref="UITechnique"/>）的取值口径：needTech 查前置技巧名、
    /// needAttr 走剧本变量的属性名（带颜色）、desc 直接显示技巧说明。
    /// </para>
    /// </summary>
    public class UIForceTechniqueInformation : UGUIWindow
    {
        /// <summary>窗口标题</summary>
        public Text windiwTitle;

        /// <summary>技巧树控件（负责布局与连线）</summary>
        public UITechniqueArea techniqueArea;

        /// <summary>技巧节点模板（池化模板，prefab 里已隐藏）</summary>
        public UITechniqueItem techniqueItem;

        /// <summary>已生成的技巧节点（每次切换势力重建）</summary>
        List<UITechniqueItem> techniqueItemList = new List<UITechniqueItem>();

        /// <summary>技巧节点的父节点</summary>
        public RectTransform techNode;

        /// <summary>技巧树滚动区域</summary>
        public ScrollRect scrollRect;

        /// <summary>等级标题的父节点</summary>
        public RectTransform titleNode;

        /// <summary>左侧势力列表</summary>
        public UIObjectList uIObjectList;

        /// <summary>详情区：君主（左上角人物条）</summary>
        public UIPersonItem governorItem;

        /// <summary>详情区：必要技巧</summary>
        public UITextField needTechField;

        /// <summary>详情区：相关能力</summary>
        public UITextField needStatusField;

        /// <summary>详情区：效果说明</summary>
        public UITextField effectField;

        /// <summary>驱动本窗口的系统</summary>
        ForceTechniqueInformation currentSystem;

        /// <summary>当前查看的势力</summary>
        Force Target;

        /// <summary>当前选中的技巧节点</summary>
        UITechniqueItem selectedItem;

        protected override void Awake()
        {
            base.Awake();
            ResolveFields();
        }

        /// <summary>
        /// 兜底取详情区引用：这几个节点在 prefab 上已存在（挂在窗口内层容器 <c>root</c> 下），
        /// 若 Inspector 漏绑，按固定路径再找一次，避免"点技巧没反应、详情永远空白"。
        /// </summary>
        void ResolveFields()
        {
            if (governorItem == null)
                governorItem = GetComponent<UIPersonItem>("root/person_2");
            if (needTechField == null)
                needTechField = GetComponent<UITextField>("root/needTech");
            if (needStatusField == null)
                needStatusField = GetComponent<UITextField>("root/needStatus");
            if (effectField == null)
                effectField = GetComponent<UITextField>("root/effect");
        }

        /// <summary>无参打开（Window.Open(name) 走这里）</summary>
        public override void OnOpen()
        {
            base.OnOpen();
            Initialize(GameSystem.GetSystem<ForceTechniqueInformation>());
        }

        /// <summary>带参打开（ForceTechniqueInformation.OnEnter 走这里）</summary>
        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);

            ForceTechniqueInformation system = null;
            if (objects != null && objects.Length > 0)
                system = objects[0] as ForceTechniqueInformation;
            if (system == null)
                system = GameSystem.GetSystem<ForceTechniqueInformation>();

            Initialize(system);
        }

        /// <summary>初始化：标题 + 左边列表 + 右侧技巧树</summary>
        /// <param name="system">势力技巧情报系统</param>
        void Initialize(ForceTechniqueInformation system)
        {
            currentSystem = system;
            if (currentSystem == null)
            {
                Log.Error("技巧表打开失败：ForceTechniqueInformation 系统不存在");
                return;
            }

            ResolveFields();

            if (windiwTitle != null)
                windiwTitle.text = string.IsNullOrEmpty(currentSystem.Name) ? "技巧表" : currentSystem.Name;

            // 列表为空时不能进 UIObjectList.Init：它的滚动条比例会做 totalCount 相关的除法，
            // 空数据会算出 NaN 并让 Scrollbar 布局异常。
            if (currentSystem.all_objects == null || currentSystem.all_objects.Count == 0)
            {
                Log.Warning("技巧表：没有可显示的存活势力");
                ResetDetail(null);
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
        /// 显示某个势力的技巧树。
        ///
        /// <para>
        /// 技巧树由 <c>Force.InitTechniquesTree</c> 在势力初始化时构建
        /// （Game/Object/Force/Force.cs:1205），其根节点来自剧本的 InitTechniques 配置。
        /// 若某势力没有配置根节点，树为空，这里只清空详情区并记一条警告，便于排查数据。
        /// </para>
        /// </summary>
        /// <param name="force">目标势力</param>
        public void Show(Force force)
        {
            Target = force;
            selectedItem = null;

            if (techniqueArea == null)
            {
                Log.Error("技巧表显示失败：techniqueArea 未绑定");
                return;
            }

            if (force == null)
            {
                ResetDetail(null);
                return;
            }

            // 重建技巧树：两个方法内部都会 Reset 各自的池，重复调用不会堆积节点
            techniqueItemList.Clear();
            if (titleNode != null)
                techniqueArea.CreateTitles(force, titleNode);
            techniqueArea.ShowForceTechnique(force, techniqueItemList);

            for (int i = 0; i < techniqueItemList.Count; i++)
            {
                UITechniqueItem item = techniqueItemList[i];
                if (item == null || item.technique == null)
                    continue;

                item.onClick = OnSelectTechniqueItem;
                item.SetValid(item.technique.IsValid(force));
                item.SetCanResearch(item.technique.CanResearch(force));
                item.SetSelected(false);
            }

            if (techniqueItemList.Count == 0)
                Log.Warning($"技巧表为空：{force.Name} 没有技巧树（检查剧本的 InitTechniques 配置）");

            ResetDetail(force);

            // 切势力后回到树的顶部，避免沿用上一个势力的滚动位置
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>详情区复位（未选中技巧时的占位）</summary>
        /// <param name="force">当前势力，用于显示君主</param>
        void ResetDetail(Force force)
        {
            if (governorItem != null)
                governorItem.SetPerson(force != null ? force.mGovernor : null);

            if (needTechField != null)
                needTechField.text = "--";
            if (needStatusField != null)
                needStatusField.text = "--";
            if (effectField != null)
                effectField.text = "请选择技巧";
        }

        /// <summary>点击技巧节点：切换高亮并刷新右下角详情</summary>
        /// <param name="item">被点击的技巧节点</param>
        public void OnSelectTechniqueItem(UITechniqueItem item)
        {
            if (item == null || item.technique == null)
                return;

            if (selectedItem != null && selectedItem != item)
                selectedItem.SetSelected(false);

            selectedItem = item;
            item.SetSelected(true);

            RefreshDetail(Target, item.technique);
        }

        /// <summary>
        /// 刷新详情区：必要技巧 / 相关能力 / 效果。
        ///
        /// <para>
        /// 「必要技巧」显示前置技巧名，并在后面标注是否已掌握 ——
        /// 只读界面没有研发入口，玩家需要靠这一行判断"为什么这个技巧不能点"。
        /// </para>
        /// </summary>
        /// <param name="force">当前势力</param>
        /// <param name="technique">选中的技巧</param>
        void RefreshDetail(Force force, Technique technique)
        {
            if (technique == null)
                return;

            Scenario scenario = Scenario.Cur;

            if (needTechField != null)
            {
                Technique parentTech = null;
                if (technique.needTech > 0 && scenario != null && scenario.CommonData != null)
                    parentTech = scenario.CommonData.Techniques.Get(technique.needTech);

                if (parentTech == null)
                    needTechField.text = "无";
                else if (force != null && parentTech.IsValid(force))
                    needTechField.text = parentTech.Name;
                else
                    needTechField.text = $"{parentTech.Name}（未掌握）";
            }

            if (needStatusField != null)
            {
                needStatusField.text = scenario != null && scenario.Variables != null
                    ? scenario.Variables.GetAttributeNameWithColor(technique.needAttr)
                    : "--";
            }

            if (effectField != null)
                effectField.text = string.IsNullOrEmpty(technique.desc) ? "--" : technique.desc;

            if (governorItem != null && force != null)
                governorItem.SetPerson(force.mGovernor);
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
