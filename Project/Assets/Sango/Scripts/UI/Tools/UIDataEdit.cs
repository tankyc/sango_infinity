using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using System.Globalization;
using TKNewtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 弹出式的修改数据界面
    /// 关心一个或多个SangoObject和当前的ObjectSortTitle
    /// 根据ObjectSortTitle上配置的DataEditType（值修改类型）与DataSetType（数据集类型），
    /// 使用对应的控件编辑目标对象的属性，支持以下几种数据类型：
    /// 1.Text（文本修改） 2.IntDropdown（下拉菜单，如人物状态/势力集合/城市集合）
    /// 3.IntInput（int文本输入） 4.IntCalculator（UICalculator输入）
    /// 5.HeadIcon（头像选择） 6.Object（对象类型，复合修改）
    /// 7.CitySelect（城池选择，通过UISelectCityWorldMap世界地图选城）
    /// 8.SpouseList（配偶列表修改，特殊数据修改接口） 9.FeatureList（特技列表修改，特殊数据修改接口）
    /// 10.TextArea（多行文本） 11.BoolDropdown（布尔下拉 是/否/无）
    /// 12.FloatInput（浮点文本输入） 13.FloatCalculator（浮点计算输入，复用FloatInput控件）
    /// 14.ColorPicker（颜色修改，兼容Color与Color32） 15.ObjectList（对象列表多选，走对象选择器）
    /// 16.IdArray（Id集合数组多选，走对象选择器，写回Id数组） 17.ArrayEdit（数值数组，逗号或换行分隔）
    /// 18.JsonEdit（Json富文本修改，按原类型解析回JArray/JObject） 19.IdDropdown（对象Id下拉，写回对象Id）
    /// 规则约定：
    /// 1.有选择器的SangoObject对象统一通过对象选择器选择，并提供清空按钮
    /// 2.SangoObject Id集合或疑似Id集合的数组同样使用选择器（多选），并提供清空按钮
    /// 3.Json配置与多行文本提供多行富文本编辑
    /// 4.支持多对象编辑：同一字段结果写入全部目标对象
    /// </summary>
    public class UIDataEdit : UGUIWindow
    {
        /// <summary>窗口名称，对应Prefab：Assets/Mods/Content/Assets/UI/Prefab/window_data_edit.prefab</summary>
        public const string WindowName = "window_data_edit";

        // 基础引用
        public Text titleText;                 // 标题，显示当前修改的属性名称与对象数量
        public Button confirmButton;           // 确定按钮
        public Button cancelButton;            // 取消按钮
        public Button clearButton;             // 清空按钮（对象/选择器/颜色等可置空的编辑类型显示）

        // 1.文本编辑区（Text）
        public GameObject textEditRoot;        // 文本编辑根节点
        public InputField textInput;           // 文本输入框

        // 2.下拉菜单编辑区（IntDropdown / IdDropdown）
        public GameObject dropdownEditRoot;    // 下拉编辑根节点
        public Dropdown valueDropdown;         // 下拉控件
        public Button dropdownSelectButton;    // 选择按钮（打开对应数据集的对象选择器，与下拉等价）

        // 3.数字输入编辑区（IntInput）
        public GameObject intEditRoot;         // 数字输入根节点
        public InputField intInput;            // 数字输入框

        // 4.计算器编辑区（IntCalculator）
        public GameObject calculatorEditRoot;  // 计算器编辑根节点
        public Button calculatorButton;        // 打开计算器的按钮
        public Text calculatorValueText;       // 当前值显示

        // 5.头像选择编辑区（HeadIcon）
        public GameObject headEditRoot;        // 头像编辑根节点
        public RawImage headIconImage;         // 头像预览
        public Button headButton;              // 打开头像选择窗口的按钮

        // 6.对象类型编辑区（Object / ObjectList / IdArray / SpouseList / FeatureList）
        public GameObject objectEditRoot;      // 对象编辑根节点
        public Text objectValueText;           // 当前对象/对象列表显示
        public Button objectSelectButton;      // 打开对象选择器的按钮
        public Button objectEditButton;        // 打开对象编辑器的按钮（复合修改，仅单对象模式）

        // 7.城池选择编辑区（CitySelect）
        public GameObject citySelectEditRoot;  // 城池选择编辑根节点（世界地图所在的显示区域）
        public Dropdown cityDropdown;          // 城池下拉（只读，仅同步显示当前城池）
        public UISelectCityWorldMap cityWorldMap; // 世界地图选城组件（在window_data_edit的editWorldMap节点上）

        // 8.多行文本编辑区（TextArea / JsonEdit）
        public GameObject textAreaEditRoot;    // 多行文本编辑根节点
        public InputField textAreaInput;       // 多行文本输入框（富文本）
        public Text textAreaTipText;           // 提示文本（Json格式错误提示等，可为空）

        // 9.布尔编辑区（BoolDropdown）
        public GameObject boolEditRoot;        // 布尔编辑根节点
        public Dropdown boolDropdown;          // 布尔下拉（是/否/无）

        // 10.浮点编辑区（FloatInput / FloatCalculator）
        public GameObject floatEditRoot;       // 浮点编辑根节点
        public InputField floatInput;          // 浮点输入框

        // 11.颜色编辑区（ColorPicker）
        public GameObject colorEditRoot;       // 颜色编辑根节点
        public Image colorPreviewImage;        // 颜色预览
        public InputField colorRInput;         // 红色分量输入（0~255）
        public InputField colorGInput;         // 绿色分量输入（0~255）
        public InputField colorBInput;         // 蓝色分量输入（0~255）
        public InputField colorAInput;         // 透明分量输入（0~255）

        // 12.数组编辑区（ArrayEdit）
        public GameObject arrayEditRoot;       // 数组编辑根节点
        public InputField arrayInput;          // 数组输入框（逗号或换行分隔）

        /// <summary>当前正在编辑的目标对象（多对象编辑时为第一个目标，用于读取初始值与特殊编辑逻辑）</summary>
        public SangoObject Target { get; protected set; }

        /// <summary>当前正在编辑的全部目标对象（多对象编辑时为多个，单对象时只含Target）</summary>
        public List<SangoObject> Targets { get; protected set; } = new List<SangoObject>();

        /// <summary>当前使用的排序/编辑标题，描述了字段的读取写入方式与编辑类型</summary>
        public ObjectSortTitle SortTitle { get; protected set; }

        /// <summary>可置空选项的显示文本</summary>
        protected const string NoneOptionLabel = "无";

        /// <summary>是否处于多对象编辑模式（同一字段批量修改多个对象）</summary>
        public bool IsMultiEdit { get { return Targets != null && Targets.Count > 1; } }

        /// <summary>多对象编辑时各目标的当前值是否不一致（不一致时以第一个目标的值作为初始值）</summary>
        protected bool valuesMixed;

        /// <summary>候选数据所属的剧本，为空时自动查找当前编辑/运行剧本</summary>
        protected Scenario editScenario;

        /// <summary>当前编辑中的临时值</summary>
        protected object curValue;

        /// <summary>下拉菜单使用的选项列表</summary>
        protected readonly List<DataEditOption> options = new List<DataEditOption>();

        /// <summary>城池选择方案使用的下拉选项列表（仅用于同步显示当前城池）</summary>
        protected readonly List<DataEditOption> citySelectOptions = new List<DataEditOption>();

        /// <summary>布尔下拉使用的选项列表</summary>
        protected readonly List<DataEditOption> boolOptions = new List<DataEditOption>();

        /// <summary>Json编辑的原始文本（多对象编辑时按目标逐个解析，避免共用同一个JToken实例）</summary>
        protected string jsonText = string.Empty;

        /// <summary>当前选择器中的虚拟空对象（Id为0、名称为“无”），选中表示清空原数据</summary>
        protected SangoObject emptyOptionObject;

        /// <summary>颜色编辑时原始值是否为Color32（写回时按原类型转换）</summary>
        protected bool colorValueIsColor32;

        /// <summary>数组编辑时原始值是否为float[]（false表示int[]）</summary>
        protected bool arrayValueIsFloat;

        /// <summary>确定后的外部回调</summary>
        protected Action onConfirmAction;

        /// <summary>取消后的外部回调</summary>
        protected Action onCancelAction;

        /// <summary>当前值修改类型</summary>
        protected DataEditType EditType
        {
            get
            {
                if (SortTitle == null) return DataEditType.None;
                return SortTitle.editType;
            }
        }

        /// <summary>
        /// 打开数据编辑窗口（单对象便捷入口）
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="sortTitle">目标对象的ObjectSortTitle（需配置editType）</param>
        /// <param name="scenario">候选数据所属剧本，为空自动获取</param>
        /// <param name="onConfirm">确定回调</param>
        /// <param name="onCancel">取消回调</param>
        /// <returns>窗口实例</returns>
        public static UIDataEdit Show(SangoObject target, ObjectSortTitle sortTitle, Scenario scenario = null, Action onConfirm = null, Action onCancel = null)
        {
            if (target == null) return null;
            return Show(new List<SangoObject>() { target }, sortTitle, scenario, onConfirm, onCancel);
        }

        /// <summary>
        /// 打开数据编辑窗口（多对象入口，同一字段批量修改多个对象）
        /// 确定时会将编辑结果写入全部目标对象
        /// </summary>
        /// <param name="targets">目标对象列表（为空或全部不可编辑时返回null）</param>
        /// <param name="sortTitle">目标对象共用的ObjectSortTitle（需配置editType）</param>
        /// <param name="scenario">候选数据所属剧本，为空自动获取</param>
        /// <param name="onConfirm">确定回调</param>
        /// <param name="onCancel">取消回调</param>
        /// <returns>窗口实例</returns>
        public static UIDataEdit Show(List<SangoObject> targets, ObjectSortTitle sortTitle, Scenario scenario = null, Action onConfirm = null, Action onCancel = null)
        {
            if (targets == null || sortTitle == null) return null;
            if (!sortTitle.CanEdit)
            {
                Log.Warning("属性:" + sortTitle.name + " 未配置值修改类型,无法编辑");
                return null;
            }

            // 过滤空对象与不允许修改的对象（如君主身份不可修改）
            List<SangoObject> validTargets = new List<SangoObject>();
            for (int i = 0; i < targets.Count; i++)
            {
                SangoObject target = targets[i];
                if (target == null) continue;
                if (!sortTitle.CanSetValue(target))
                {
                    Log.Warning("属性:" + sortTitle.name + " 不允许修改对象 " + target.Name + "（如君主身份不可修改,需先删除其势力）,已跳过");
                    continue;
                }
                validTargets.Add(target);
            }
            if (validTargets.Count == 0)
            {
                Log.Warning("属性:" + sortTitle.name + " 没有可修改的目标对象");
                return null;
            }

            // 单一SangoObject编辑（Object/对象引用下拉/对象Id下拉）从入口直接分流到对象选择器，不再弹出编辑窗口
            if (TryStartDirectObjectSelect(validTargets, sortTitle, scenario, onConfirm))
            {
                return null;
            }

            return Window.Instance.Open<UIDataEdit>(WindowName, validTargets, sortTitle, scenario, onConfirm, onCancel);
        }

        /// <summary>
        /// 判断标题是否为单一SangoObject编辑（对象/对象引用下拉/对象Id下拉）
        /// 这类字段内部编辑的就是一个SangoObject，直接从入口分流到对象选择器
        /// 城池选择（CitySelect）使用世界地图选城，属于专用选择器，不参与分流
        /// </summary>
        /// <param name="sortTitle">排序/编辑标题</param>
        /// <returns>是否为单一SangoObject编辑</returns>
        public static bool IsDirectObjectSelectTitle(ObjectSortTitle sortTitle)
        {
            if (sortTitle == null)
            {
                return false;
            }
            DataEditType type = sortTitle.editType;
            if (type == DataEditType.Object || type == DataEditType.IdDropdown)
            {
                return true;
            }
            // 对象引用下拉：数据集为对象集合时（排除自定义枚举选项）内部编辑的同样是SangoObject
            if (type == DataEditType.IntDropdown
                && sortTitle.dataSetType != DataSetType.None
                && sortTitle.dataSetType != DataSetType.Custom)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 入口分流：单一SangoObject编辑时直接打开对应数据集的对象选择器
        /// 候选项首位为虚拟空对象（Id为0、名称为“无”），选中该对象表示清空原数据
        /// </summary>
        /// <param name="targets">目标对象列表</param>
        /// <param name="sortTitle">排序/编辑标题</param>
        /// <param name="scenario">候选数据所属剧本，为空自动获取</param>
        /// <param name="onConfirm">选择完成回调</param>
        /// <returns>是否已分流（true表示已打开选择器，无需再打开编辑窗口）</returns>
        public static bool TryStartDirectObjectSelect(List<SangoObject> targets, ObjectSortTitle sortTitle, Scenario scenario, Action onConfirm)
        {
            if (targets == null || targets.Count == 0 || sortTitle == null)
            {
                return false;
            }
            if (!IsDirectObjectSelectTitle(sortTitle))
            {
                return false;
            }
            if (scenario == null)
            {
                ScenarioEdit scenarioEdit = GameSystem.GetSystem<ScenarioEdit>();
                scenario = scenarioEdit != null ? scenarioEdit.Scenario : null;
                if (scenario == null)
                {
                    scenario = Scenario.Cur;
                }
            }
            if (scenario == null)
            {
                Log.Warning("没有可用剧本数据,无法打开对象选择器");
                return false;
            }

            switch (sortTitle.dataSetType)
            {
                case DataSetType.Person:
                    DirectObjectSelect(scenario.personSet, GameSystem.GetSystem<PersonSelectSystem>(), targets, sortTitle, "选择武将", onConfirm);
                    return true;
                case DataSetType.Force:
                    DirectObjectSelect(scenario.forceSet, GameSystem.GetSystem<ForceSelectSystem>(), targets, sortTitle, "选择势力", onConfirm);
                    return true;
                case DataSetType.City:
                    DirectObjectSelect(scenario.citySet, GameSystem.GetSystem<CitySelectSystem>(), targets, sortTitle, "选择城市", onConfirm);
                    return true;
                case DataSetType.Corps:
                    DirectObjectSelect(scenario.corpsSet, GameSystem.GetSystem<CorpsSelectSystem>(), targets, sortTitle, "选择军团", onConfirm);
                    return true;
                case DataSetType.Troop:
                    DirectObjectSelect(scenario.troopsSet, GameSystem.GetSystem<TroopSelectSystem>(), targets, sortTitle, "选择部队", onConfirm);
                    return true;
                case DataSetType.Feature:
                    DirectObjectSelect(scenario.CommonData.Features, GameSystem.GetSystem<FeatrueSelectSystem>(), targets, sortTitle, "选择特技", onConfirm);
                    return true;
                case DataSetType.Personality:
                    DirectObjectSelect(scenario.CommonData.Personalities, GameSystem.GetSystem<PersonalitySelectSystem>(), targets, sortTitle, "选择性格", onConfirm);
                    return true;
                case DataSetType.Official:
                    DirectObjectSelect(scenario.CommonData.Officials, GameSystem.GetSystem<OfficialSelectSystem>(), targets, sortTitle, "选择官职", onConfirm);
                    return true;
                case DataSetType.AttributeChangeType:
                    DirectObjectSelect(scenario.CommonData.AttributeChangeTypes, GameSystem.GetSystem<AttributeChangeTypeSelectSystem>(), targets, sortTitle, "选择能力变化", onConfirm);
                    return true;
                case DataSetType.Argumentation:
                    DirectObjectSelect(scenario.CommonData.Argumentations, GameSystem.GetSystem<ArgumentationSelectSystem>(), targets, sortTitle, "选择义理", onConfirm);
                    return true;
                case DataSetType.Province:
                    DirectObjectSelect(scenario.CommonData.Provinces, GameSystem.GetSystem<ProvinceSelectSystem>(), targets, sortTitle, "选择州", onConfirm);
                    return true;
                case DataSetType.Flag:
                    DirectObjectSelect(scenario.CommonData.Flags, GameSystem.GetSystem<FlagSelectSystem>(), targets, sortTitle, "选择旗帜", onConfirm);
                    return true;
                case DataSetType.Title:
                    DirectObjectSelect(scenario.CommonData.Titles, GameSystem.GetSystem<TitleSelectSystem>(), targets, sortTitle, "选择爵位", onConfirm);
                    return true;
                case DataSetType.Technique:
                    DirectObjectSelect(scenario.CommonData.Techniques, GameSystem.GetSystem<TechniqueSelectSystem>(), targets, sortTitle, "选择科技", onConfirm);
                    return true;
                case DataSetType.TerrainType:
                    DirectObjectSelect(scenario.CommonData.TerrainTypes, GameSystem.GetSystem<TerrainTypeSelectSystem>(), targets, sortTitle, "选择地形", onConfirm);
                    return true;
                case DataSetType.BuildingType:
                    DirectObjectSelect(scenario.CommonData.BuildingTypes, GameSystem.GetSystem<BuildingTypeSelectSystem>(), targets, sortTitle, "选择建筑类型", onConfirm);
                    return true;
                case DataSetType.TroopType:
                    DirectObjectSelect(scenario.CommonData.TroopTypes, GameSystem.GetSystem<TroopTypeSelectSystem>(), targets, sortTitle, "选择兵种", onConfirm);
                    return true;
                case DataSetType.ItemType:
                    DirectObjectSelect(scenario.CommonData.ItemTypes, GameSystem.GetSystem<ItemTypeSelectSystem>(), targets, sortTitle, "选择道具", onConfirm);
                    return true;
                case DataSetType.TroopAnimation:
                    DirectObjectSelect(scenario.CommonData.TroopAnimations, GameSystem.GetSystem<TroopAnimationSelectSystem>(), targets, sortTitle, "选择兵种动画", onConfirm);
                    return true;
                case DataSetType.CityLevelType:
                    DirectObjectSelect(scenario.CommonData.CityLevelTypes, GameSystem.GetSystem<CityLevelTypeSelectSystem>(), targets, sortTitle, "选择城市等级", onConfirm);
                    return true;
                case DataSetType.Region:
                    DirectObjectSelect(scenario.CommonData.Regions, GameSystem.GetSystem<RegionSelectSystem>(), targets, sortTitle, "选择区域", onConfirm);
                    return true;
                case DataSetType.Skill:
                    DirectObjectSelect(scenario.CommonData.Skills, GameSystem.GetSystem<SkillSelectSystem>(), targets, sortTitle, "选择技能", onConfirm);
                    return true;
                case DataSetType.Buff:
                    DirectObjectSelect(scenario.CommonData.Buffs, GameSystem.GetSystem<BuffSelectSystem>(), targets, sortTitle, "选择状态", onConfirm);
                    return true;
                case DataSetType.JobType:
                    DirectObjectSelect(scenario.CommonData.JobTypes, GameSystem.GetSystem<JobTypeSelectSystem>(), targets, sortTitle, "选择工作", onConfirm);
                    return true;
                case DataSetType.PersonLevel:
                    DirectObjectSelect(scenario.CommonData.PersonLevels, GameSystem.GetSystem<PersonLevelSelectSystem>(), targets, sortTitle, "选择武将等级", onConfirm);
                    return true;
                case DataSetType.PersonAttributeType:
                    DirectObjectSelect(scenario.CommonData.PersonAttributeTypes, GameSystem.GetSystem<PersonAttributeTypeSelectSystem>(), targets, sortTitle, "选择属性", onConfirm);
                    return true;
                default:
                    Log.Warning("属性:" + sortTitle.name + " 的数据集类型不支持对象选择,改为打开编辑窗口");
                    return false;
            }
        }

        /// <summary>
        /// 直接打开对象选择器（入口分流用）
        /// 候选项首位插入虚拟空对象，选中该对象表示清空原数据
        /// </summary>
        /// <typeparam name="TSystem">选择系统类型</typeparam>
        /// <typeparam name="TObject">可选对象类型</typeparam>
        /// <param name="dataSet">数据集</param>
        /// <param name="system">选择系统实例</param>
        /// <param name="targets">目标对象列表</param>
        /// <param name="sortTitle">排序/编辑标题</param>
        /// <param name="titleName">选择窗口标题</param>
        /// <param name="onConfirm">选择完成回调</param>
        private static void DirectObjectSelect<TSystem, TObject>(Database<TObject> dataSet, TSystem system, List<SangoObject> targets, ObjectSortTitle sortTitle, string titleName, Action onConfirm)
            where TObject : SangoObject, new()
            where TSystem : class, IObjectSelectSystem<TObject>
        {
            if (system == null)
            {
                Log.Warning("未找到对应的对象选择系统:" + titleName);
                return;
            }

            // 候选项：首位为虚拟空对象，其后为数据集中的全部对象
            List<TObject> candidates = new List<TObject>();
            TObject empty = CreateEmptyObject<TObject>();
            candidates.Add(empty);
            CollectSetCandidates(dataSet, candidates);

            // 多对象编辑时仅在全部目标值一致的情况下回显已选项
            List<TObject> initial = new List<TObject>();
            TObject current = GetSharedObjectValue(dataSet, targets, sortTitle);
            if (current != null)
            {
                initial.Add(current);
            }

            system.Start(candidates, initial, 1, (result) =>
            {
                if (result == null || result.Count == 0)
                {
                    return;
                }
                TObject selected = result[result.Count - 1];
                object writeValue;
                if (ReferenceEquals(selected, empty))
                {
                    // 选中虚拟空对象：清空原数据（Id外键字段写0）
                    writeValue = sortTitle.editType == DataEditType.IdDropdown ? (object)0 : null;
                }
                else if (sortTitle.editType == DataEditType.IdDropdown)
                {
                    writeValue = selected != null ? selected.Id : 0;
                }
                else
                {
                    writeValue = selected;
                }
                ApplyValueToTargets(targets, sortTitle, writeValue, onConfirm);
            }, null, titleName);
        }

        /// <summary>
        /// 创建虚拟空对象（Id为0、名称为“无”），用于在选择器中表示“清空原数据”
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>虚拟空对象</returns>
        protected static T CreateEmptyObject<T>() where T : SangoObject, new()
        {
            T empty = new T();
            empty.Id = 0;
            empty.Name = NoneOptionLabel;
            return empty;
        }

        /// <summary>
        /// 收集数据集中的候选对象（排除Id小于等于0的默认空对象）
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="dataSet">数据集</param>
        /// <param name="result">候选列表</param>
        protected static void CollectSetCandidates<T>(Database<T> dataSet, List<T> result) where T : SangoObject, new()
        {
            if (dataSet == null)
            {
                return;
            }
            dataSet.ForEach(x =>
            {
                if (x != null && x.Id > 0)
                {
                    result.Add(x);
                }
            });
        }

        /// <summary>
        /// 获取多对象编辑时共用的当前对象值
        /// 全部目标的当前值一致时返回对应对象（Id外键按Id还原对象），否则返回null
        /// </summary>
        protected static T GetSharedObjectValue<T>(Database<T> dataSet, List<SangoObject> targets, ObjectSortTitle sortTitle) where T : SangoObject, new()
        {
            if (targets == null || targets.Count == 0)
            {
                return null;
            }
            object first = ReadTitleValue(sortTitle, targets[0]);
            for (int i = 1; i < targets.Count; i++)
            {
                if (ReadTitleValue(sortTitle, targets[i]) != first)
                {
                    return null;
                }
            }
            if (first is T typed)
            {
                return typed;
            }
            // Id外键字段：按Id在数据集中还原对象
            if (first is int id && id > 0)
            {
                T found = null;
                if (dataSet != null)
                {
                    dataSet.ForEach(x =>
                    {
                        if (found == null && x != null && x.Id == id)
                        {
                            found = x;
                        }
                    });
                }
                return found;
            }
            return null;
        }

        /// <summary>
        /// 读取指定目标的属性值（读取失败返回null）
        /// </summary>
        protected static object ReadTitleValue(ObjectSortTitle sortTitle, SangoObject target)
        {
            if (sortTitle == null || target == null)
            {
                return null;
            }
            try
            {
                return sortTitle.GetValue(target);
            }
            catch (Exception e)
            {
                Log.Warning("读取属性:" + sortTitle.name + " 失败:" + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 将值写入全部目标对象并触发完成回调（入口分流的多对象写入）
        /// </summary>
        protected static void ApplyValueToTargets(List<SangoObject> targets, ObjectSortTitle sortTitle, object value, Action onConfirm)
        {
            if (targets == null || sortTitle == null)
            {
                return;
            }
            int successCount = 0;
            int failCount = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                SangoObject target = targets[i];
                if (target == null)
                {
                    continue;
                }
                try
                {
                    sortTitle.SetValue(target, value);
                    successCount++;
                }
                catch (Exception e)
                {
                    failCount++;
                    Log.Error("写入属性:" + sortTitle.name + " 到对象 " + target.Name + " 失败:" + e.Message);
                }
            }
            if (failCount > 0)
            {
                Log.Warning("批量修改属性:" + sortTitle.name + " 完成,成功 " + successCount + " 个,失败 " + failCount + " 个");
            }
            else if (targets.Count > 1)
            {
                Log.Info("批量修改属性:" + sortTitle.name + " 完成,共修改 " + successCount + " 个对象");
            }
            onConfirm?.Invoke();
        }

        /// <summary>
        /// 窗口打开时的初始化
        /// </summary>
        /// <param name="objects">objects[0]目标对象或目标对象列表，objects[1]ObjectSortTitle，objects[2]可选剧本，objects[3]确定回调，objects[4]取消回调</param>
        public override void OnOpen(params object[] objects)
        {
            List<SangoObject> targets = null;
            if (objects != null && objects.Length > 0)
            {
                // 多对象编辑传入列表，单对象编辑传入单个对象
                if (objects[0] is List<SangoObject> targetList)
                {
                    targets = targetList;
                }
                else if (objects[0] is SangoObject single)
                {
                    targets = new List<SangoObject>() { single };
                }
            }
            ObjectSortTitle sortTitle = objects != null && objects.Length > 1 ? objects[1] as ObjectSortTitle : null;
            Scenario scenario = objects != null && objects.Length > 2 ? objects[2] as Scenario : null;
            Action onConfirm = objects != null && objects.Length > 3 ? objects[3] as Action : null;
            Action onCancel = objects != null && objects.Length > 4 ? objects[4] as Action : null;
            Init(targets, sortTitle, scenario, onConfirm, onCancel);
        }

        /// <summary>
        /// 数据初始化与UI刷新
        /// </summary>
        protected void Init(List<SangoObject> targets, ObjectSortTitle sortTitle, Scenario scenario, Action onConfirm, Action onCancel)
        {
            if (targets == null || targets.Count == 0 || sortTitle == null)
            {
                Log.Error("UIDataEdit初始化失败,缺少目标对象或ObjectSortTitle");
                CloseSelf();
                return;
            }

            RemoveListeners();

            SortTitle = sortTitle;
            editScenario = scenario;
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;

            // 过滤空对象与不允许修改的对象（如君主身份不可修改）
            Targets.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                SangoObject target = targets[i];
                if (target == null) continue;
                if (!sortTitle.CanSetValue(target))
                {
                    Log.Warning("属性:" + sortTitle.name + " 不允许修改对象 " + target.Name + "（如君主身份不可修改,需先删除其势力）,已跳过");
                    continue;
                }
                Targets.Add(target);
            }
            if (Targets.Count == 0)
            {
                Log.Warning("属性:" + sortTitle.name + " 没有可修改的目标对象");
                CloseSelf();
                return;
            }
            Target = Targets[0];

            // 配偶修改存在唯一约束（一个武将只能被一个其他武将登记为配偶），不支持多对象批量修改
            if (Targets.Count > 1 && EditType == DataEditType.SpouseList)
            {
                Log.Warning("配偶不支持多对象批量修改,仅修改 " + Target.Name);
                Targets.Clear();
                Targets.Add(Target);
            }

            // 清除上一次编辑的临时状态
            jsonText = string.Empty;
            colorValueIsColor32 = false;
            arrayValueIsFloat = false;

            // 读取当前值：多对象时取第一个目标的值，并记录各目标的值是否一致
            curValue = ReadValueFrom(Target);
            if ((EditType == DataEditType.Text || EditType == DataEditType.TextArea || EditType == DataEditType.JsonEdit) && curValue == null)
            {
                curValue = ReadCurrentValueStr();
            }
            // 记录原始值形态，用于写回时还原类型
            RefreshValueTypeFlags();
            valuesMixed = IsValuesMixed();

            RefreshUI();
            BindListeners();
        }

        /// <summary>
        /// 记录当前值的形态标记（颜色为Color32还是Color、数组为float[]还是int[]）
        /// </summary>
        protected void RefreshValueTypeFlags()
        {
            colorValueIsColor32 = curValue is Color32;
            arrayValueIsFloat = curValue is float[];
        }

        /// <summary>
        /// 窗口每次重新显示时刷新当前值（例如从计算器/头像窗口返回时）
        /// </summary>
        public override void OnRefresh()
        {
            if (Target == null || SortTitle == null) return;
            curValue = ReadValueFrom(Target);
            if ((EditType == DataEditType.Text || EditType == DataEditType.TextArea || EditType == DataEditType.JsonEdit) && curValue == null)
            {
                curValue = ReadCurrentValueStr();
            }
            valuesMixed = IsValuesMixed();
            RefreshUI();
        }

        /// <summary>
        /// 窗口关闭时清理事件监听
        /// </summary>
        public override void OnClose()
        {
            RemoveListeners();
            if (cityWorldMap != null)
            {
                cityWorldMap.OnSelectCity = null;
            }
            Target = null;
            Targets.Clear();
            SortTitle = null;
            curValue = null;
            valuesMixed = false;
            emptyOptionObject = null;
            jsonText = string.Empty;
            options.Clear();
            citySelectOptions.Clear();
            boolOptions.Clear();
            base.OnClose();
        }

        // ==================== UI刷新 ====================

        /// <summary>
        /// 根据编辑类型刷新整个界面
        /// </summary>
        protected void RefreshUI()
        {
            // 标题（多对象编辑时附加对象数量与值是否一致的提示）
            if (titleText != null)
            {
                string title = SortTitle != null && !string.IsNullOrEmpty(SortTitle.name) ? "修改" + SortTitle.name : "修改数据";
                if (IsMultiEdit)
                {
                    title += "（" + Targets.Count + "个对象）";
                    if (valuesMixed)
                    {
                        title += " 值不一致";
                    }
                }
                titleText.text = title;
            }

            // 控制各区显隐
            bool showText = EditType == DataEditType.Text;
            bool showDropdown = EditType == DataEditType.IntDropdown || EditType == DataEditType.IdDropdown;
            bool showInt = EditType == DataEditType.IntInput;
            bool showCalculator = EditType == DataEditType.IntCalculator;
            bool showHead = EditType == DataEditType.HeadIcon;
            bool showObject = EditType == DataEditType.Object;
            bool showCitySelect = EditType == DataEditType.CitySelect;
            // 对象列表类编辑（配偶/特技/通用对象列表/Id集合）复用对象编辑区
            bool showSpouseList = EditType == DataEditType.SpouseList;
            bool showFeatureList = EditType == DataEditType.FeatureList;
            bool showObjectList = EditType == DataEditType.ObjectList || EditType == DataEditType.IdArray;
            bool showListEdit = showSpouseList || showFeatureList || showObjectList;
            // 多行文本类编辑（多行文本/Json）
            bool showTextArea = EditType == DataEditType.TextArea || EditType == DataEditType.JsonEdit;
            bool showBool = EditType == DataEditType.BoolDropdown;
            bool showFloat = EditType == DataEditType.FloatInput || EditType == DataEditType.FloatCalculator;
            bool showColor = EditType == DataEditType.ColorPicker;
            bool showArray = EditType == DataEditType.ArrayEdit;

            SetActive(textEditRoot, showText);
            SetActive(dropdownEditRoot, showDropdown);
            SetActive(intEditRoot, showInt);
            SetActive(calculatorEditRoot, showCalculator);
            SetActive(headEditRoot, showHead);
            SetActive(objectEditRoot, showObject || showListEdit);
            SetActive(citySelectEditRoot, showCitySelect);
            SetActive(textAreaEditRoot, showTextArea);
            SetActive(boolEditRoot, showBool);
            SetActive(floatEditRoot, showFloat);
            SetActive(colorEditRoot, showColor);
            SetActive(arrayEditRoot, showArray);

            // 清空按钮：对象类/选择器类/颜色类等可置空的编辑类型提供
            bool showClear = showObject || showListEdit || showCitySelect || showHead || showColor
                || (showDropdown && NeedNoneOption()) || showText || showTextArea || showArray
                || showInt || showCalculator || showFloat || showBool;
            SetActive(clearButton != null ? clearButton.gameObject : null, showClear);

            // 1.文本修改：回填输入框
            if (showText && textInput != null)
            {
                textInput.text = curValue != null ? curValue.ToString() : string.Empty;
            }

            // 3.int文本输入：回填输入框
            if (showInt && intInput != null)
            {
                intInput.text = GetIntValue().ToString();
            }

            // 2.下拉菜单：构建选项并回选
            if (showDropdown)
            {
                RefreshDropdown();
            }

            // 4.计算器输入：显示当前数值
            if (showCalculator && calculatorValueText != null)
            {
                calculatorValueText.text = GetIntValue().ToString();
            }

            // 5.头像选择：刷新头像预览
            if (showHead)
            {
                RefreshHeadIcon();
            }

            // 6.对象类型与对象列表类编辑：刷新对象/列表显示与按钮状态
            if (showObject || showListEdit)
            {
                RefreshObjectView();
            }

            // 7.城池选择：展示世界地图（默认所有城池均可选取），下拉同步显示当前城池
            if (showCitySelect)
            {
                RefreshCitySelect();
            }

            // 8.多行文本/Json：回填多行输入框
            if (showTextArea)
            {
                RefreshTextArea();
            }

            // 9.布尔：构建下拉并回选
            if (showBool)
            {
                RefreshBoolDropdown();
            }

            // 10.浮点：回填输入框
            if (showFloat && floatInput != null)
            {
                floatInput.text = GetFloatValue().ToString(CultureInfo.InvariantCulture);
            }

            // 11.颜色：回填分量输入与预览
            if (showColor)
            {
                RefreshColorPicker();
            }

            // 12.数组：回填数组文本
            if (showArray && arrayInput != null)
            {
                arrayInput.text = FormatArrayValue(curValue);
            }
        }

        /// <summary>
        /// 刷新下拉菜单的选项与选中项（IntDropdown/IdDropdown）
        /// </summary>
        protected void RefreshDropdown()
        {
            if (valueDropdown == null)
            {
                Log.Warning("UIDataEdit未绑定valueDropdown,无法进行下拉编辑");
                return;
            }

            options.Clear();
            BuildOptions();

            // 可置空的下拉菜单必须在首位提供“无”选项,保证可以将字段置空
            if (NeedNoneOption())
            {
                options.Insert(0, new DataEditOption(NoneOptionLabel, null));
            }

            valueDropdown.ClearOptions();
            if (options.Count == 0)
            {
                options.Add(new DataEditOption("(无可用选项)", null));
            }
            valueDropdown.AddOptions(OptionsToData(options));

            // Id下拉按Id回选，普通下拉按值回选
            int index = EditType == DataEditType.IdDropdown ? GetOptionIndexById(GetIntValue()) : GetOptionIndex(curValue);
            if (index < 0 && curValue != null)
            {
                // 当前值不在选项中时,在首位插入一个保持原值的选项
                options.Insert(0, new DataEditOption(GetDisplayString(curValue), curValue));
                valueDropdown.ClearOptions();
                valueDropdown.AddOptions(OptionsToData(options));
                index = 0;
            }
            valueDropdown.SetValueWithoutNotify(index < 0 ? 0 : index);

            // 选择按钮：数据集存在对应对象选择器时提供（与下拉等价的第二种选择方式）
            if (dropdownSelectButton != null)
            {
                dropdownSelectButton.gameObject.SetActive(IsObjectSelectable());
            }
        }

        /// <summary>
        /// 判断当前下拉菜单是否需要提供“无”选项（可将字段置空）
        /// Id下拉（外键）恒提供；其余在数据集可置空或存在空值时提供
        /// </summary>
        /// <returns>是否需要“无”选项</returns>
        protected bool NeedNoneOption()
        {
            if (EditType == DataEditType.IdDropdown)
            {
                return true;
            }
            // 当前存在空值的目标时,必须提供可回退到空值的选项
            if (HasNullValue())
            {
                return true;
            }
            return IsNullableDataSet();
        }

        /// <summary>
        /// 判断数据集类型是否为可置空的对象引用型
        /// 对象集合类型（势力/城市/军团/部队/武将/特技/旗帜/爵位等）均可置空；
        /// 自定义枚举型选项（性别/身份/音声等）不提供置空选项
        /// </summary>
        protected bool IsNullableDataSet()
        {
            if (SortTitle == null)
            {
                return false;
            }
            DataSetType type = SortTitle.dataSetType;
            switch (type)
            {
                case DataSetType.None:
                case DataSetType.Custom:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 判断是否存在属性值为空的目标对象
        /// </summary>
        protected bool HasNullValue()
        {
            if (Targets == null)
            {
                return false;
            }
            for (int i = 0; i < Targets.Count; i++)
            {
                if (ReadValueFrom(Targets[i]) == null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 把DataEditOption列表转换为Unity下拉选项数据
        /// </summary>
        protected List<Dropdown.OptionData> OptionsToData(List<DataEditOption> source)
        {
            List<Dropdown.OptionData> data = new List<Dropdown.OptionData>();
            for (int i = 0; i < source.Count; i++)
            {
                data.Add(new Dropdown.OptionData(source[i].label));
            }
            return data;
        }

        /// <summary>
        /// 刷新头像预览
        /// </summary>
        protected void RefreshHeadIcon()
        {
            int headId = GetIntValue();
            if (headIconImage != null)
            {
                headIconImage.texture = headId > 0 ? GameRenderHelper.LoadHeadIcon(headId, 2) : null;
                headIconImage.enabled = true;
            }
        }

        /// <summary>
        /// 刷新对象类型编辑视图（含配偶/特技/通用对象列表/Id集合等列表类编辑）
        /// </summary>
        protected void RefreshObjectView()
        {
            bool isObject = EditType == DataEditType.Object;
            bool isListEdit = EditType == DataEditType.SpouseList || EditType == DataEditType.FeatureList
                || EditType == DataEditType.ObjectList || EditType == DataEditType.IdArray;
            bool isIdArray = EditType == DataEditType.IdArray;

            if (objectValueText != null)
            {
                objectValueText.text = isIdArray ? FormatIdArrayText() : GetDisplayString(curValue);
            }

            if (objectSelectButton != null)
            {
                // 单选对象类型依赖数据集判断是否可打开选择器；列表类编辑始终可打开多选选择器
                objectSelectButton.gameObject.SetActive(isListEdit || IsObjectSelectable());
            }
            // 仅单选对象编辑（Object）可打开对象自身的编辑器（复合修改），列表类编辑不提供
            if (objectEditButton != null)
            {
                objectEditButton.gameObject.SetActive(isObject && curValue is SangoObject);
            }
        }

        // ==================== 多行文本与Json（TextArea / JsonEdit） ====================

        /// <summary>
        /// 刷新多行文本编辑区（多行文本直接回填；Json按原类型输出文本）
        /// </summary>
        protected void RefreshTextArea()
        {
            if (textAreaInput == null)
            {
                Log.Warning("UIDataEdit未绑定textAreaInput,无法进行多行文本编辑");
                return;
            }
            string text;
            if (EditType == DataEditType.JsonEdit)
            {
                // 编辑过程中优先使用已输入的文本,避免刷新覆盖用户输入
                if (string.IsNullOrEmpty(jsonText))
                {
                    JToken token = curValue as JToken;
                    text = token != null ? token.ToString() : (curValue != null ? curValue.ToString() : string.Empty);
                }
                else
                {
                    text = jsonText;
                }
            }
            else
            {
                text = curValue != null ? curValue.ToString() : string.Empty;
            }
            textAreaInput.text = text;

            if (textAreaTipText != null)
            {
                textAreaTipText.text = EditType == DataEditType.JsonEdit ? "Json配置：确认时按格式解析写回" : string.Empty;
            }
        }

        /// <summary>
        /// 尝试解析Json文本（空文本视为清空）
        /// </summary>
        /// <param name="text">Json文本</param>
        /// <param name="token">解析结果</param>
        /// <returns>是否解析成功</returns>
        protected bool TryParseJson(string text, out JToken token)
        {
            token = null;
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            try
            {
                token = JToken.Parse(text);
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("Json格式错误,请检查后重试:" + e.Message);
                return false;
            }
        }

        // ==================== 布尔编辑（BoolDropdown） ====================

        /// <summary>
        /// 刷新布尔下拉的选项与选中项（是/否，当前值为空时额外提供“无”）
        /// </summary>
        protected void RefreshBoolDropdown()
        {
            if (boolDropdown == null)
            {
                Log.Warning("UIDataEdit未绑定boolDropdown,无法进行布尔编辑");
                return;
            }

            boolOptions.Clear();
            if (curValue == null)
            {
                boolOptions.Add(new DataEditOption(NoneOptionLabel, null));
            }
            boolOptions.Add(new DataEditOption("是", true));
            boolOptions.Add(new DataEditOption("否", false));

            boolDropdown.ClearOptions();
            boolDropdown.AddOptions(OptionsToData(boolOptions));
            boolDropdown.SetValueWithoutNotify(GetBoolOptionIndex(curValue));
        }

        /// <summary>
        /// 根据当前值查找布尔下拉索引
        /// </summary>
        protected int GetBoolOptionIndex(object value)
        {
            for (int i = 0; i < boolOptions.Count; i++)
            {
                object optValue = boolOptions[i].value;
                if (value == null && optValue == null)
                {
                    return i;
                }
                if (value != null && optValue != null && value.Equals(optValue))
                {
                    return i;
                }
            }
            return 0;
        }

        // ==================== 颜色编辑（ColorPicker） ====================

        /// <summary>
        /// 刷新颜色编辑区：回填RGBA分量并刷新预览
        /// </summary>
        protected void RefreshColorPicker()
        {
            Color color = ToColor(curValue);
            if (colorRInput != null) colorRInput.text = ToColorByte(color.r).ToString();
            if (colorGInput != null) colorGInput.text = ToColorByte(color.g).ToString();
            if (colorBInput != null) colorBInput.text = ToColorByte(color.b).ToString();
            if (colorAInput != null) colorAInput.text = ToColorByte(color.a).ToString();
            RefreshColorPreview(color);
        }

        /// <summary>
        /// 刷新颜色预览
        /// </summary>
        protected void RefreshColorPreview(Color color)
        {
            if (colorPreviewImage != null)
            {
                colorPreviewImage.color = color;
            }
        }

        /// <summary>
        /// 颜色分量输入框编辑结束：实时刷新预览
        /// </summary>
        protected void OnColorInputEndEdit(string text)
        {
            Color color = ReadColorInputs();
            curValue = color;
            RefreshColorPreview(color);
        }

        /// <summary>
        /// 读取RGBA输入框组装颜色
        /// </summary>
        protected Color ReadColorInputs()
        {
            float r = ReadColorComponent(colorRInput, 0f);
            float g = ReadColorComponent(colorGInput, 0f);
            float b = ReadColorComponent(colorBInput, 0f);
            float a = ReadColorComponent(colorAInput, 1f);
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// 读取单个颜色分量输入框（0~255，缺省取默认值）
        /// </summary>
        protected float ReadColorComponent(InputField input, float defaultValue)
        {
            if (input == null)
            {
                return defaultValue;
            }
            if (float.TryParse(input.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return UnityEngine.Mathf.Clamp01(value / 255f);
            }
            return defaultValue;
        }

        /// <summary>
        /// 颜色转0~255的字节值
        /// </summary>
        protected int ToColorByte(float component)
        {
            return UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(component) * 255f);
        }

        /// <summary>
        /// 通用颜色转换：支持Color/Color32与其他类型（其他类型返回透明黑）
        /// </summary>
        protected Color ToColor(object value)
        {
            if (value is Color color)
            {
                return color;
            }
            if (value is Color32 color32)
            {
                return color32;
            }
            return Color.clear;
        }

        // ==================== 数组编辑（ArrayEdit / IdArray） ====================

        /// <summary>
        /// 刷新Id集合数组的显示文本（Id -> 对象名）
        /// </summary>
        protected string FormatIdArrayText()
        {
            int[] ids = GetIdArrayValue();
            if (ids == null || ids.Length == 0)
            {
                return "无";
            }
            Scenario scenario = GetEditScenario();
            List<string> names = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                SangoObject obj = FindObjectById(scenario, ids[i]);
                names.Add(obj != null ? obj.Name : ids[i].ToString());
            }
            return string.Join("，", names);
        }

        /// <summary>
        /// 将数组值格式化为可编辑文本（逗号分隔）
        /// </summary>
        protected string FormatArrayValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (value is int[] intArray)
            {
                return string.Join(", ", Array.ConvertAll(intArray, x => x.ToString()));
            }
            if (value is float[] floatArray)
            {
                return string.Join(", ", Array.ConvertAll(floatArray, x => x.ToString(CultureInfo.InvariantCulture)));
            }
            return value.ToString();
        }

        /// <summary>
        /// 解析数组编辑文本为int[]或float[]（按原始值类型决定）
        /// </summary>
        /// <param name="text">数组文本（逗号/空格/换行分隔）</param>
        /// <returns>解析后的数组</returns>
        protected object ParseArrayValue(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return arrayValueIsFloat ? (object)new float[0] : new int[0];
            }
            string[] parts = text.Split(new char[] { ',', '，', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (arrayValueIsFloat)
            {
                List<float> values = new List<float>();
                for (int i = 0; i < parts.Length; i++)
                {
                    if (float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    {
                        values.Add(v);
                    }
                }
                return values.ToArray();
            }
            List<int> intValues = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int v))
                {
                    intValues.Add(v);
                }
            }
            return intValues.ToArray();
        }

        /// <summary>
        /// 获取当前值的Id数组表示（兼容int[]/对象集合/单个对象）
        /// </summary>
        protected int[] GetIdArrayValue()
        {
            if (curValue is int[] ids)
            {
                return ids;
            }
            List<int> result = new List<int>();
            if (curValue is SangoObject sangoObj)
            {
                result.Add(sangoObj.Id);
            }
            else if (curValue is System.Collections.IEnumerable enumerable && !(curValue is string))
            {
                foreach (object item in enumerable)
                {
                    if (item is SangoObject obj)
                    {
                        result.Add(obj.Id);
                    }
                    else if (item is int id)
                    {
                        result.Add(id);
                    }
                }
            }
            return result.ToArray();
        }

        // ==================== 城池选择方案（CitySelect） ====================

        /// <summary>
        /// 刷新城池选择视图：初始化世界地图并同步下拉显示
        /// </summary>
        protected void RefreshCitySelect()
        {
            Scenario scenario = GetEditScenario();
            if (scenario == null)
            {
                Log.Warning("获取属性:" + SortTitle.name + " 的城池数据失败,当前没有可用剧本数据");
                return;
            }

            // 1.下拉同步显示（只读）：列出可选城池，回选当前值所在城池
            if (cityDropdown == null)
            {
                Log.Warning("UIDataEdit未绑定cityDropdown,城池选择方案无法同步显示当前城池");
            }
            else
            {
                FillCityDropdownOptions(scenario);
                cityDropdown.ClearOptions();
                cityDropdown.AddOptions(OptionsToData(citySelectOptions));
                // 城池修改需从地图上选取，下拉仅作显示，禁用交互
                cityDropdown.interactable = false;
                cityDropdown.SetValueWithoutNotify(GetCityOptionIndex(curValue));
            }

            // 2.世界地图：默认所有城池均可选取，当前值所在城池高亮为已选
            if (cityWorldMap == null)
            {
                Log.Warning("UIDataEdit未绑定cityWorldMap,城池选择方案无法通过世界地图选城");
                return;
            }
            cityWorldMap.SetScenario(scenario);
            cityWorldMap.maxSelectCount = 1;
            cityWorldMap.SetSelectAllCity(GetCurrentCityList());
            cityWorldMap.OnSelectCity = OnCityMapSelect;
        }

        /// <summary>
        /// 填充城池下拉选项：首位为“无”，其后为剧本中可选的全部城池
        /// </summary>
        protected void FillCityDropdownOptions(Scenario scenario)
        {
            citySelectOptions.Clear();
            citySelectOptions.Add(new DataEditOption(NoneOptionLabel, null));
            scenario.citySet.ForEach(x =>
            {
                if (x == null) return;
                if (x.Id == 0) return;
                // 与地图保持一致：只列出可选取的城池（如关口/港口等特殊建筑类型不列出）
                if (x.BuildingType != null && x.BuildingType.Id > 1) return;
                citySelectOptions.Add(new DataEditOption(x.Name, x));
            });
        }

        /// <summary>
        /// 获取当前值的城池列表，用于地图初始已选项回显
        /// </summary>
        protected List<City> GetCurrentCityList()
        {
            List<City> list = new List<City>();
            if (curValue is City)
            {
                list.Add((City)curValue);
            }
            return list;
        }

        /// <summary>
        /// 根据当前值查找城池下拉选项索引，找不到时返回0（即“无”）
        /// </summary>
        protected int GetCityOptionIndex(object value)
        {
            for (int i = 0; i < citySelectOptions.Count; i++)
            {
                object optValue = citySelectOptions[i].value;
                // 引用类型对象比较
                if (value != null && optValue != null && value is SangoObject && optValue is SangoObject)
                {
                    SangoObject cur = (SangoObject)value;
                    SangoObject opt = (SangoObject)optValue;
                    if (cur == opt || (cur.Id > 0 && cur.Id == opt.Id)) return i;
                }
                // 基本类型值比较
                else if (value != null && optValue != null && value.Equals(optValue))
                {
                    return i;
                }
                // null匹配“无”选项
                else if (value == null && optValue == null)
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>
        /// 世界地图选城回调：把最后选择的城池写入当前值，并同步下拉显示
        /// </summary>
        protected void OnCityMapSelect(List<City> cities)
        {
            if (cities == null || cities.Count == 0)
            {
                curValue = null;
                if (cityDropdown != null)
                {
                    cityDropdown.SetValueWithoutNotify(0);
                }
                return;
            }
            City city = cities[cities.Count - 1];
            if (city == null) return;
            curValue = city;
            if (cityDropdown != null)
            {
                cityDropdown.SetValueWithoutNotify(GetCityOptionIndex(city));
            }
        }

        // ==================== 选项构建 ====================

        /// <summary>
        /// 依据数据集类型构建下拉选项
        /// 自定义数据集读取customData（List&lt;string&gt;或List&lt;DataEditOption&gt;）
        /// 对象集合数据集从剧本对应集合中读取全部对象
        /// </summary>
        protected void BuildOptions()
        {
            DataSetType dataSetType = SortTitle.dataSetType;
            if (dataSetType == DataSetType.Custom)
            {
                BuildCustomOptions();
                return;
            }

            Scenario scenario = GetEditScenario();
            if (scenario == null)
            {
                Log.Warning("获取属性:" + SortTitle.name + " 的可选数据失败,当前没有可用剧本数据");
                return;
            }

            switch (dataSetType)
            {
                case DataSetType.Person:
                    AddSetOptions(scenario.personSet);
                    break;
                case DataSetType.Force:
                    AddSetOptions(scenario.forceSet);
                    break;
                case DataSetType.City:
                    AddSetOptions(scenario.citySet);
                    break;
                case DataSetType.Corps:
                    AddSetOptions(scenario.corpsSet);
                    break;
                case DataSetType.Troop:
                    AddSetOptions(scenario.troopsSet);
                    break;
                case DataSetType.Feature:
                    AddSetOptions(scenario.CommonData.Features);
                    break;
                case DataSetType.Personality:
                    AddSetOptions(scenario.CommonData.Personalities);
                    break;
                case DataSetType.Official:
                    AddSetOptions(scenario.CommonData.Officials);
                    break;
                case DataSetType.AttributeChangeType:
                    AddSetOptions(scenario.CommonData.AttributeChangeTypes);
                    break;
                case DataSetType.Argumentation:
                    AddSetOptions(scenario.CommonData.Argumentations);
                    break;
                case DataSetType.Province:
                    AddSetOptions(scenario.CommonData.Provinces);
                    break;
                case DataSetType.Flag:
                    AddSetOptions(scenario.CommonData.Flags);
                    break;
                case DataSetType.Title:
                    AddSetOptions(scenario.CommonData.Titles);
                    break;
                case DataSetType.Technique:
                    AddSetOptions(scenario.CommonData.Techniques);
                    break;
                case DataSetType.TerrainType:
                    AddSetOptions(scenario.CommonData.TerrainTypes);
                    break;
                case DataSetType.BuildingType:
                    AddSetOptions(scenario.CommonData.BuildingTypes);
                    break;
                case DataSetType.TroopType:
                    AddSetOptions(scenario.CommonData.TroopTypes);
                    break;
                case DataSetType.ItemType:
                    AddSetOptions(scenario.CommonData.ItemTypes);
                    break;
                case DataSetType.TroopAnimation:
                    AddSetOptions(scenario.CommonData.TroopAnimations);
                    break;
                case DataSetType.CityLevelType:
                    AddSetOptions(scenario.CommonData.CityLevelTypes);
                    break;
                case DataSetType.Region:
                    AddSetOptions(scenario.CommonData.Regions);
                    break;
                case DataSetType.Skill:
                    AddSetOptions(scenario.CommonData.Skills);
                    break;
                case DataSetType.Buff:
                    AddSetOptions(scenario.CommonData.Buffs);
                    break;
                case DataSetType.JobType:
                    AddSetOptions(scenario.CommonData.JobTypes);
                    break;
                case DataSetType.PersonLevel:
                    AddSetOptions(scenario.CommonData.PersonLevels);
                    break;
                case DataSetType.PersonAttributeType:
                    AddSetOptions(scenario.CommonData.PersonAttributeTypes);
                    break;
                default:
                    Log.Warning("属性:" + SortTitle.name + " 未配置可选数据集类型");
                    break;
            }
        }

        /// <summary>
        /// 从数据集收集对象选项
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="dataSet">数据集</param>
        protected void AddSetOptions<T>(Database<T> dataSet) where T : SangoObject, new()
        {
            if (dataSet == null) return;
            dataSet.ForEach(x =>
            {
                if (x != null)
                {
                    options.Add(new DataEditOption(x.Name, x));
                }
            });
        }

        /// <summary>
        /// 从数据集收集候选对象列表（用于对象选择器）
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="dataSet">数据集</param>
        /// <returns>候选对象列表</returns>
        protected List<T> CollectCandidates<T>(Database<T> dataSet) where T : SangoObject, new()
        {
            List<T> list = new List<T>();
            if (dataSet == null) return list;
            dataSet.ForEach(x =>
            {
                if (x != null)
                {
                    list.Add(x);
                }
            });
            return list;
        }

        /// <summary>
        /// 在数据集中按Id查找对象
        /// </summary>
        protected T FindInSet<T>(Database<T> dataSet, int id) where T : SangoObject, new()
        {
            if (dataSet == null || id <= 0) return null;
            T result = null;
            dataSet.ForEach(x =>
            {
                if (result == null && x != null && x.Id == id)
                {
                    result = x;
                }
            });
            return result;
        }

        /// <summary>
        /// 按当前数据集类型在剧本中查找指定Id的对象（用于Id集合回显）
        /// </summary>
        protected SangoObject FindObjectById(Scenario scenario, int id)
        {
            if (scenario == null || id <= 0) return null;
            switch (SortTitle.dataSetType)
            {
                case DataSetType.Person: return FindInSet(scenario.personSet, id);
                case DataSetType.Force: return FindInSet(scenario.forceSet, id);
                case DataSetType.City: return FindInSet(scenario.citySet, id);
                case DataSetType.Corps: return FindInSet(scenario.corpsSet, id);
                case DataSetType.Troop: return FindInSet(scenario.troopsSet, id);
                case DataSetType.Feature: return FindInSet(scenario.CommonData.Features, id);
                case DataSetType.Personality: return FindInSet(scenario.CommonData.Personalities, id);
                case DataSetType.Official: return FindInSet(scenario.CommonData.Officials, id);
                case DataSetType.AttributeChangeType: return FindInSet(scenario.CommonData.AttributeChangeTypes, id);
                case DataSetType.Argumentation: return FindInSet(scenario.CommonData.Argumentations, id);
                case DataSetType.Province: return FindInSet(scenario.CommonData.Provinces, id);
                case DataSetType.Flag: return FindInSet(scenario.CommonData.Flags, id);
                case DataSetType.Title: return FindInSet(scenario.CommonData.Titles, id);
                case DataSetType.Technique: return FindInSet(scenario.CommonData.Techniques, id);
                case DataSetType.TerrainType: return FindInSet(scenario.CommonData.TerrainTypes, id);
                case DataSetType.BuildingType: return FindInSet(scenario.CommonData.BuildingTypes, id);
                case DataSetType.TroopType: return FindInSet(scenario.CommonData.TroopTypes, id);
                case DataSetType.ItemType: return FindInSet(scenario.CommonData.ItemTypes, id);
                case DataSetType.TroopAnimation: return FindInSet(scenario.CommonData.TroopAnimations, id);
                case DataSetType.CityLevelType: return FindInSet(scenario.CommonData.CityLevelTypes, id);
                case DataSetType.Region: return FindInSet(scenario.CommonData.Regions, id);
                case DataSetType.Skill: return FindInSet(scenario.CommonData.Skills, id);
                case DataSetType.Buff: return FindInSet(scenario.CommonData.Buffs, id);
                case DataSetType.JobType: return FindInSet(scenario.CommonData.JobTypes, id);
                case DataSetType.PersonLevel: return FindInSet(scenario.CommonData.PersonLevels, id);
                case DataSetType.PersonAttributeType: return FindInSet(scenario.CommonData.PersonAttributeTypes, id);
                default: return null;
            }
        }

        /// <summary>
        /// 从customData构建自定义选项
        /// 支持List&lt;DataEditOption&gt;（自定义显示文本与值）与List&lt;string&gt;/string[]（文本即值）
        /// </summary>
        protected void BuildCustomOptions()
        {
            object data = SortTitle.customData;
            if (data is List<DataEditOption>)
            {
                List<DataEditOption> custom = (List<DataEditOption>)data;
                for (int i = 0; i < custom.Count; i++)
                {
                    options.Add(custom[i]);
                }
            }
            else if (data is List<string>)
            {
                List<string> list = (List<string>)data;
                for (int i = 0; i < list.Count; i++)
                {
                    options.Add(new DataEditOption(list[i], list[i]));
                }
            }
            else if (data is string[])
            {
                string[] array = (string[])data;
                for (int i = 0; i < array.Length; i++)
                {
                    options.Add(new DataEditOption(array[i], array[i]));
                }
            }
            else
            {
                Log.Warning("属性:" + SortTitle.name + " 使用自定义数据集但未在customData中提供List<string>或List<DataEditOption>");
            }
        }

        /// <summary>
        /// 根据当前值查找下拉选项索引
        /// </summary>
        /// <param name="value">当前值</param>
        /// <returns>选项索引，找不到返回-1</returns>
        protected int GetOptionIndex(object value)
        {
            for (int i = 0; i < options.Count; i++)
            {
                object optValue = options[i].value;
                // 引用类型对象比较
                if (value != null && optValue != null && value is SangoObject && optValue is SangoObject)
                {
                    SangoObject cur = (SangoObject)value;
                    SangoObject opt = (SangoObject)optValue;
                    if (cur == opt || (cur.Id > 0 && cur.Id == opt.Id)) return i;
                }
                // 基本类型值比较
                else if (value != null && optValue != null && value.Equals(optValue))
                {
                    return i;
                }
                // null匹配“无”选项
                else if (value == null && optValue == null)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 根据对象Id查找下拉选项索引（用于IdDropdown回选）
        /// </summary>
        /// <param name="id">对象Id</param>
        /// <returns>选项索引，找不到返回-1</returns>
        protected int GetOptionIndexById(int id)
        {
            for (int i = 0; i < options.Count; i++)
            {
                object optValue = options[i].value;
                if (optValue is SangoObject sangoObj)
                {
                    if (sangoObj.Id == id) return i;
                }
                else if (optValue == null && id <= 0)
                {
                    return i;
                }
            }
            return -1;
        }

        // ==================== 数据读取与转换 ====================

        /// <summary>
        /// 读取第一个目标对象的当前属性值
        /// </summary>
        protected object ReadCurrentValue()
        {
            return ReadValueFrom(Target);
        }

        /// <summary>
        /// 读取指定目标对象的当前属性值
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <returns>属性值，读取失败返回null</returns>
        protected object ReadValueFrom(SangoObject target)
        {
            if (target == null || SortTitle == null)
            {
                return null;
            }
            try
            {
                return SortTitle.GetValue(target);
            }
            catch (Exception e)
            {
                Log.Warning("读取属性:" + SortTitle.name + " 失败:" + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 判断多个目标对象的当前属性值是否不一致
        /// </summary>
        protected bool IsValuesMixed()
        {
            if (!IsMultiEdit)
            {
                return false;
            }
            object first = curValue;
            for (int i = 1; i < Targets.Count; i++)
            {
                object other = ReadValueFrom(Targets[i]);
                if (!IsSameValue(first, other))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 比较两个属性值是否相同（引用类型按对象与Id比较，列表逐项比较，其余按Equals比较）
        /// </summary>
        protected bool IsSameValue(object a, object b)
        {
            if (a == null && b == null)
            {
                return true;
            }
            if (a == null || b == null)
            {
                return false;
            }
            // 引用类型对象比较（按引用或Id）
            if (a is SangoObject && b is SangoObject)
            {
                SangoObject objA = (SangoObject)a;
                SangoObject objB = (SangoObject)b;
                if (objA == objB)
                {
                    return true;
                }
                return objA.Id > 0 && objA.Id == objB.Id;
            }
            // 数组与列表比较：逐项比较
            if (a is System.Collections.IEnumerable && b is System.Collections.IEnumerable
                && !(a is string) && !(b is string))
            {
                List<object> listA = new List<object>();
                List<object> listB = new List<object>();
                foreach (object item in (System.Collections.IEnumerable)a) listA.Add(item);
                foreach (object item in (System.Collections.IEnumerable)b) listB.Add(item);
                if (listA.Count != listB.Count)
                {
                    return false;
                }
                for (int i = 0; i < listA.Count; i++)
                {
                    if (!IsSameValue(listA[i], listB[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            return a.Equals(b);
        }

        /// <summary>
        /// 读取目标对象的当前显示字符串（当GetValue不可用时使用）
        /// </summary>
        protected string ReadCurrentValueStr()
        {
            try
            {
                return SortTitle.GetValueStr(Target);
            }
            catch (Exception e)
            {
                Log.Warning("读取属性:" + SortTitle.name + " 的显示文本失败:" + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 获取当前编辑值的int表示
        /// </summary>
        protected int GetIntValue()
        {
            if (curValue is int) return (int)curValue;
            if (curValue is string)
            {
                int.TryParse((string)curValue, out int result);
                return result;
            }
            return 0;
        }

        /// <summary>
        /// 获取当前编辑值的float表示
        /// </summary>
        protected float GetFloatValue()
        {
            if (curValue is float floatValue) return floatValue;
            if (curValue is double doubleValue) return (float)doubleValue;
            if (curValue is int intValue) return intValue;
            if (curValue is string str)
            {
                float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
                return result;
            }
            return 0f;
        }

        /// <summary>
        /// 获取当前编辑值的显示文本
        /// 对象列表（配偶/特技等多选）显示为顿号拼接的文本
        /// </summary>
        protected string GetDisplayString(object value)
        {
            if (value == null) return "无";
            if (value is SangoObject) return ((SangoObject)value).Name;
            if (value is string) return value.ToString();
            // 对象列表类编辑：遍历元素拼接显示（自动排除string类型）
            if (value is System.Collections.IEnumerable list)
            {
                List<string> names = new List<string>();
                foreach (object item in list)
                {
                    if (item is SangoObject) names.Add(((SangoObject)item).Name);
                    else if (item != null) names.Add(item.ToString());
                }
                return names.Count == 0 ? "无" : string.Join("，", names);
            }
            return value.ToString();
        }

        /// <summary>
        /// 获取用于获取候选数据的剧本
        /// 优先使用传入的剧本，其次使用当前编辑系统ScenarioEdit的剧本，最后使用Scenario.Cur
        /// </summary>
        protected Scenario GetEditScenario()
        {
            if (editScenario != null) return editScenario;
            ScenarioEdit scenarioEdit = GameSystem.GetSystem<ScenarioEdit>();
            if (scenarioEdit != null && scenarioEdit.Scenario != null) return scenarioEdit.Scenario;
            return Scenario.Cur;
        }

        /// <summary>
        /// 判断当前数据集类型是否支持对象选择（是否存在对应的对象选择器）
        /// 自定义枚举选项与未配置数据集时不支持
        /// </summary>
        protected bool IsObjectSelectable()
        {
            if (SortTitle == null)
            {
                return false;
            }
            DataSetType type = SortTitle.dataSetType;
            return type != DataSetType.None && type != DataSetType.Custom;
        }

        // ==================== 控件交互 ====================

        /// <summary>
        /// 绑定事件监听
        /// </summary>
        protected void BindListeners()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
            if (clearButton != null) clearButton.onClick.AddListener(OnClearClicked);
            if (valueDropdown != null) valueDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            if (dropdownSelectButton != null) dropdownSelectButton.onClick.AddListener(OnDropdownSelectClicked);
            if (calculatorButton != null) calculatorButton.onClick.AddListener(OnCalculatorButtonClicked);
            if (headButton != null) headButton.onClick.AddListener(OnHeadButtonClicked);
            if (objectSelectButton != null) objectSelectButton.onClick.AddListener(OnObjectSelectClicked);
            if (objectEditButton != null) objectEditButton.onClick.AddListener(OnObjectEditClicked);
            if (boolDropdown != null) boolDropdown.onValueChanged.AddListener(OnBoolDropdownValueChanged);
            if (colorRInput != null) colorRInput.onEndEdit.AddListener(OnColorInputEndEdit);
            if (colorGInput != null) colorGInput.onEndEdit.AddListener(OnColorInputEndEdit);
            if (colorBInput != null) colorBInput.onEndEdit.AddListener(OnColorInputEndEdit);
            if (colorAInput != null) colorAInput.onEndEdit.AddListener(OnColorInputEndEdit);
        }

        /// <summary>
        /// 移除事件监听
        /// </summary>
        protected void RemoveListeners()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (clearButton != null) clearButton.onClick.RemoveListener(OnClearClicked);
            if (valueDropdown != null) valueDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            if (dropdownSelectButton != null) dropdownSelectButton.onClick.RemoveListener(OnDropdownSelectClicked);
            if (calculatorButton != null) calculatorButton.onClick.RemoveListener(OnCalculatorButtonClicked);
            if (headButton != null) headButton.onClick.RemoveListener(OnHeadButtonClicked);
            if (objectSelectButton != null) objectSelectButton.onClick.RemoveListener(OnObjectSelectClicked);
            if (objectEditButton != null) objectEditButton.onClick.RemoveListener(OnObjectEditClicked);
            if (boolDropdown != null) boolDropdown.onValueChanged.RemoveListener(OnBoolDropdownValueChanged);
            if (colorRInput != null) colorRInput.onEndEdit.RemoveListener(OnColorInputEndEdit);
            if (colorGInput != null) colorGInput.onEndEdit.RemoveListener(OnColorInputEndEdit);
            if (colorBInput != null) colorBInput.onEndEdit.RemoveListener(OnColorInputEndEdit);
            if (colorAInput != null) colorAInput.onEndEdit.RemoveListener(OnColorInputEndEdit);
        }

        /// <summary>
        /// 下拉菜单选择改变（IdDropdown写回对象Id，其余写回选项值）
        /// </summary>
        protected void OnDropdownValueChanged(int index)
        {
            if (index < 0 || index >= options.Count)
            {
                return;
            }
            object value = options[index].value;
            if (EditType == DataEditType.IdDropdown)
            {
                curValue = value is SangoObject sangoObj ? sangoObj.Id : 0;
            }
            else
            {
                curValue = value;
            }
        }

        /// <summary>
        /// 下拉区的选择按钮 - 打开对应数据集的对象选择器（与下拉等价）
        /// </summary>
        protected void OnDropdownSelectClicked()
        {
            StartObjectSelect(GetEditScenario(), false);
        }

        /// <summary>
        /// 布尔下拉选择改变
        /// </summary>
        protected void OnBoolDropdownValueChanged(int index)
        {
            if (index >= 0 && index < boolOptions.Count)
            {
                curValue = boolOptions[index].value;
            }
        }

        /// <summary>
        /// 清空按钮 - 按编辑类型把当前值置为空值或零值
        /// </summary>
        protected void OnClearClicked()
        {
            switch (EditType)
            {
                case DataEditType.Text:
                case DataEditType.TextArea:
                    curValue = string.Empty;
                    break;
                case DataEditType.JsonEdit:
                    curValue = null;
                    jsonText = string.Empty;
                    break;
                case DataEditType.IntInput:
                case DataEditType.IntCalculator:
                case DataEditType.HeadIcon:
                case DataEditType.IdDropdown:
                    curValue = 0;
                    break;
                case DataEditType.FloatInput:
                case DataEditType.FloatCalculator:
                    curValue = 0f;
                    break;
                case DataEditType.IdArray:
                    curValue = new int[0];
                    break;
                case DataEditType.ArrayEdit:
                    curValue = arrayValueIsFloat ? (object)new float[0] : new int[0];
                    break;
                case DataEditType.ObjectList:
                    curValue = new List<SangoObject>();
                    break;
                case DataEditType.SpouseList:
                    curValue = new List<Person>();
                    break;
                case DataEditType.FeatureList:
                    curValue = new List<Feature>();
                    break;
                case DataEditType.ColorPicker:
                    curValue = Color.clear;
                    break;
                default:
                    curValue = null;
                    break;
            }
            RefreshUI();
        }

        /// <summary>
        /// 打开UICalculator输入数值
        /// </summary>
        protected void OnCalculatorButtonClicked()
        {
            int min = Math.Max(SortTitle.minValue, int.MinValue);
            int max = SortTitle.maxValue;
            // 未配置范围时使用默认范围0~99999，避免过大的滑块区间
            if (SortTitle.minValue == 0 && SortTitle.maxValue == int.MaxValue)
            {
                min = 0;
                max = 99999;
            }
            int current = GetIntValue();
            Window.Instance.Open("window_calculator", current, min, max, (Action<int>)OnCalculatorResult);
        }

        /// <summary>
        /// 计算器返回结果
        /// </summary>
        protected void OnCalculatorResult(int value)
        {
            curValue = Math.Min(Math.Max(value, SortTitle.minValue), SortTitle.maxValue);
            if (calculatorValueText != null)
            {
                calculatorValueText.text = curValue.ToString();
            }
        }

        /// <summary>
        /// 打开头像选择窗口
        /// </summary>
        protected void OnHeadButtonClicked()
        {
            Window.Instance.Open("window_create_person_image", GetIntValue(), (Action<int>)OnHeadResult);
        }

        /// <summary>
        /// 头像选择窗口返回结果
        /// </summary>
        protected void OnHeadResult(int headId)
        {
            curValue = headId;
            RefreshHeadIcon();
        }

        /// <summary>
        /// 打开对象选择器（单选/多选，按当前数据集类型分发）
        /// 多选用于对象列表与Id集合数组编辑
        /// </summary>
        protected void OnObjectSelectClicked()
        {
            Scenario scenario = GetEditScenario();
            if (scenario == null)
            {
                Log.Warning("没有可用剧本数据,无法打开对象选择器");
                return;
            }

            // 特殊数据修改接口：配偶/特技使用各自的多选选择器
            if (EditType == DataEditType.SpouseList)
            {
                StartSpouseListSelect(scenario);
                return;
            }
            if (EditType == DataEditType.FeatureList)
            {
                StartFeatureListSelect(scenario);
                return;
            }

            bool multi = EditType == DataEditType.ObjectList || EditType == DataEditType.IdArray;
            StartObjectSelect(scenario, multi);
        }

        /// <summary>
        /// 打开对象自身的编辑器（复合修改）
        /// </summary>
        protected void OnObjectEditClicked()
        {
            SangoObject value = curValue as SangoObject;
            if (value == null) return;

            // 进入对象完整编辑器前先关闭当前弹窗
            Window.Instance.Close(WindowName);

            if (value is Person)
            {
                GameSystem.GetSystem<PersonEdit>()?.Start((Person)value);
            }
            else if (value is Force)
            {
                GameSystem.GetSystem<ForceEdit>()?.Start((Force)value);
            }
            else if (value is City)
            {
                GameSystem.GetSystem<CityEdit>()?.Start((City)value);
            }
            else if (value is Corps)
            {
                GameSystem.GetSystem<CorpsEdit>()?.Start((Corps)value);
            }
            else
            {
                Log.Warning("对象类型:" + value.GetType().Name + " 没有对应的编辑器");
            }
        }

        // ==================== 对象选择器分发（单选/多选） ====================

        /// <summary>
        /// 按数据集类型启动对应的对象选择器
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="multi">是否为多选（对象列表/Id集合）</param>
        protected void StartObjectSelect(Scenario scenario, bool multi)
        {
            if (scenario == null)
            {
                Log.Warning("没有可用剧本数据,无法打开对象选择器");
                return;
            }
            switch (SortTitle.dataSetType)
            {
                case DataSetType.Person:
                    StartSelect(scenario.personSet, GameSystem.GetSystem<PersonSelectSystem>(), multi, "选择武将");
                    break;
                case DataSetType.Force:
                    StartSelect(scenario.forceSet, GameSystem.GetSystem<ForceSelectSystem>(), multi, "选择势力");
                    break;
                case DataSetType.City:
                    StartSelect(scenario.citySet, GameSystem.GetSystem<CitySelectSystem>(), multi, "选择城市");
                    break;
                case DataSetType.Corps:
                    StartSelect(scenario.corpsSet, GameSystem.GetSystem<CorpsSelectSystem>(), multi, "选择军团");
                    break;
                case DataSetType.Troop:
                    StartSelect(scenario.troopsSet, GameSystem.GetSystem<TroopSelectSystem>(), multi, "选择部队");
                    break;
                case DataSetType.Feature:
                    StartSelect(scenario.CommonData.Features, GameSystem.GetSystem<FeatrueSelectSystem>(), multi, "选择特技");
                    break;
                case DataSetType.Personality:
                    StartSelect(scenario.CommonData.Personalities, GameSystem.GetSystem<PersonalitySelectSystem>(), multi, "选择性格");
                    break;
                case DataSetType.Official:
                    StartSelect(scenario.CommonData.Officials, GameSystem.GetSystem<OfficialSelectSystem>(), multi, "选择官职");
                    break;
                case DataSetType.AttributeChangeType:
                    StartSelect(scenario.CommonData.AttributeChangeTypes, GameSystem.GetSystem<AttributeChangeTypeSelectSystem>(), multi, "选择能力变化");
                    break;
                case DataSetType.Argumentation:
                    StartSelect(scenario.CommonData.Argumentations, GameSystem.GetSystem<ArgumentationSelectSystem>(), multi, "选择义理");
                    break;
                case DataSetType.Province:
                    StartSelect(scenario.CommonData.Provinces, GameSystem.GetSystem<ProvinceSelectSystem>(), multi, "选择州");
                    break;
                case DataSetType.Flag:
                    StartSelect(scenario.CommonData.Flags, GameSystem.GetSystem<FlagSelectSystem>(), multi, "选择旗帜");
                    break;
                case DataSetType.Title:
                    StartSelect(scenario.CommonData.Titles, GameSystem.GetSystem<TitleSelectSystem>(), multi, "选择爵位");
                    break;
                case DataSetType.Technique:
                    StartSelect(scenario.CommonData.Techniques, GameSystem.GetSystem<TechniqueSelectSystem>(), multi, "选择科技");
                    break;
                case DataSetType.TerrainType:
                    StartSelect(scenario.CommonData.TerrainTypes, GameSystem.GetSystem<TerrainTypeSelectSystem>(), multi, "选择地形");
                    break;
                case DataSetType.BuildingType:
                    StartSelect(scenario.CommonData.BuildingTypes, GameSystem.GetSystem<BuildingTypeSelectSystem>(), multi, "选择建筑类型");
                    break;
                case DataSetType.TroopType:
                    StartSelect(scenario.CommonData.TroopTypes, GameSystem.GetSystem<TroopTypeSelectSystem>(), multi, "选择兵种");
                    break;
                case DataSetType.ItemType:
                    StartSelect(scenario.CommonData.ItemTypes, GameSystem.GetSystem<ItemTypeSelectSystem>(), multi, "选择道具");
                    break;
                case DataSetType.TroopAnimation:
                    StartSelect(scenario.CommonData.TroopAnimations, GameSystem.GetSystem<TroopAnimationSelectSystem>(), multi, "选择兵种动画");
                    break;
                case DataSetType.CityLevelType:
                    StartSelect(scenario.CommonData.CityLevelTypes, GameSystem.GetSystem<CityLevelTypeSelectSystem>(), multi, "选择城市等级");
                    break;
                case DataSetType.Region:
                    StartSelect(scenario.CommonData.Regions, GameSystem.GetSystem<RegionSelectSystem>(), multi, "选择区域");
                    break;
                case DataSetType.Skill:
                    StartSelect(scenario.CommonData.Skills, GameSystem.GetSystem<SkillSelectSystem>(), multi, "选择技能");
                    break;
                case DataSetType.Buff:
                    StartSelect(scenario.CommonData.Buffs, GameSystem.GetSystem<BuffSelectSystem>(), multi, "选择状态");
                    break;
                case DataSetType.JobType:
                    StartSelect(scenario.CommonData.JobTypes, GameSystem.GetSystem<JobTypeSelectSystem>(), multi, "选择工作");
                    break;
                case DataSetType.PersonLevel:
                    StartSelect(scenario.CommonData.PersonLevels, GameSystem.GetSystem<PersonLevelSelectSystem>(), multi, "选择武将等级");
                    break;
                case DataSetType.PersonAttributeType:
                    StartSelect(scenario.CommonData.PersonAttributeTypes, GameSystem.GetSystem<PersonAttributeTypeSelectSystem>(), multi, "选择属性");
                    break;
                default:
                    Log.Warning("属性:" + SortTitle.name + " 的数据集类型不支持对象选择");
                    break;
            }
        }

        /// <summary>
        /// 通用的对象选择器启动封装（按数据集收集候选并启动对应选择系统）
        /// </summary>
        /// <typeparam name="TSystem">选择系统类型</typeparam>
        /// <typeparam name="TObject">对象类型</typeparam>
        /// <param name="dataSet">数据集</param>
        /// <param name="system">选择系统实例</param>
        /// <param name="multi">是否为多选</param>
        /// <param name="titleName">选择窗口标题</param>
        protected void StartSelect<TSystem, TObject>(Database<TObject> dataSet, TSystem system, bool multi, string titleName)
            where TObject : SangoObject, new()
            where TSystem : class, IObjectSelectSystem<TObject>
        {
            if (system == null)
            {
                Log.Warning("未找到对应的对象选择系统:" + titleName);
                return;
            }

            // 候选项：首位为虚拟空对象，选中即清空（单选置空，多选清空整个列表）
            List<TObject> candidates = new List<TObject>();
            TObject empty = CreateEmptyObject<TObject>();
            candidates.Add(empty);
            CollectSetCandidates(dataSet, candidates);
            emptyOptionObject = empty;

            List<TObject> initial = GetInitialObjects(dataSet);
            if (multi)
            {
                system.Start(candidates, initial, candidates.Count, (result) => OnMultiObjectSelected(result, empty), null, titleName);
            }
            else
            {
                system.Start(candidates, initial, 1, (result) =>
                {
                    if (result.Count > 0) OnObjectSelected(result[result.Count - 1], empty);
                }, null, titleName);
            }
        }

        /// <summary>
        /// 获取选择器的初始已选项（Id集合按Id还原对象，其余直接取对象列表）
        /// </summary>
        protected List<T> GetInitialObjects<T>(Database<T> dataSet) where T : SangoObject, new()
        {
            List<T> result = new List<T>();
            if (EditType == DataEditType.IdArray)
            {
                int[] ids = GetIdArrayValue();
                for (int i = 0; i < ids.Length; i++)
                {
                    T obj = FindInSet(dataSet, ids[i]);
                    if (obj != null && !result.Contains(obj))
                    {
                        result.Add(obj);
                    }
                }
                return result;
            }
            return GetObjectListValue<T>();
        }

        /// <summary>
        /// 多选选择器的统一返回处理
        /// Id集合编辑写回Id数组，对象列表编辑写回对象列表
        /// </summary>
        protected void OnMultiObjectSelected<T>(List<T> result, SangoObject emptyOption = null) where T : SangoObject
        {
            if (result == null) return;

            // 选中虚拟空对象表示清空整个列表
            if (emptyOption != null && result.Contains(emptyOption as T))
            {
                curValue = EditType == DataEditType.IdArray ? (object)new int[0] : new List<T>();
                RefreshObjectView();
                return;
            }

            if (EditType == DataEditType.IdArray)
            {
                int[] ids = new int[result.Count];
                for (int i = 0; i < result.Count; i++)
                {
                    ids[i] = result[i] != null ? result[i].Id : 0;
                }
                curValue = ids;
            }
            else
            {
                curValue = result;
            }
            RefreshObjectView();
        }

        /// <summary>
        /// 单选选择器的统一返回处理
        /// 选中虚拟空对象时清空原数据，Id下拉编辑写回对象Id，其余写回对象引用
        /// </summary>
        /// <param name="obj">选中的对象</param>
        /// <param name="emptyOption">虚拟空对象（选中表示清空）</param>
        protected void OnObjectSelected(SangoObject obj, SangoObject emptyOption = null)
        {
            bool isEmpty = emptyOption != null && ReferenceEquals(obj, emptyOption);
            if (EditType == DataEditType.IdDropdown)
            {
                curValue = isEmpty ? 0 : (obj != null ? obj.Id : 0);
                RefreshDropdown();
                return;
            }
            curValue = isEmpty ? null : obj;
            RefreshObjectView();
        }

        // ==================== 特殊数据修改接口（配偶/特技多选） ====================

        /// <summary>
        /// 获取当前编辑值的对象列表（兼容List/SangoObjectList/单个对象/null等输入）
        /// </summary>
        protected List<T> GetObjectListValue<T>() where T : SangoObject
        {
            List<T> result = new List<T>();
            if (curValue is T single)
            {
                result.Add(single);
                return result;
            }
            if (curValue is System.Collections.IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item is T obj && !result.Contains(obj))
                    {
                        result.Add(obj);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 判断武将是否已被目标武将之外的其他武将登记为配偶
        /// </summary>
        protected bool IsPersonRegisteredAsSpouse(Scenario scenario, Person person, Person target)
        {
            bool registered = false;
            scenario.personSet.ForEach(other =>
            {
                if (other == null || other == target) return;
                if (other.mSpouseList != null && other.mSpouseList.Contains(person))
                {
                    registered = true;
                }
            });
            return registered;
        }

        /// <summary>
        /// 构建配偶候选列表：剧本中全部武将，排除目标自身与已被他人登记为配偶的武将
        /// </summary>
        protected List<Person> BuildSpouseCandidates(Scenario scenario, Person target)
        {
            List<Person> candidates = new List<Person>();
            scenario.personSet.ForEach(x =>
            {
                if (x == null || x == target) return;
                if (IsPersonRegisteredAsSpouse(scenario, x, target)) return;
                candidates.Add(x);
            });
            return candidates;
        }

        /// <summary>
        /// 启动配偶多选选择器（特殊数据修改接口：配偶）
        /// </summary>
        protected void StartSpouseListSelect(Scenario scenario)
        {
            Person target = Target as Person;
            if (target == null)
            {
                Log.Warning("配偶修改仅支持武将对象");
                return;
            }
            List<Person> candidates = BuildSpouseCandidates(scenario, target);
            List<Person> initial = GetObjectListValue<Person>();
            // 已登记在目标名下的旧配偶必须保留在候选中，保证可查看与反选
            if (target.mSpouseList != null)
            {
                foreach (Person spouse in target.mSpouseList)
                {
                    if (spouse != null && !candidates.Contains(spouse))
                    {
                        candidates.Add(spouse);
                    }
                }
            }
            PersonSelectSystem system = GameSystem.GetSystem<PersonSelectSystem>();
            if (system == null)
            {
                Log.Warning("未找到武将选择系统");
                return;
            }
            system.Start(candidates, initial, candidates.Count, OnSpouseListSelected, null, "选择配偶");
        }

        /// <summary>
        /// 配偶多选选择器返回结果
        /// </summary>
        protected void OnSpouseListSelected(List<Person> result)
        {
            if (result == null) return;
            curValue = result;
            RefreshObjectView();
        }

        /// <summary>
        /// 启动特技多选选择器（特殊数据修改接口：特技）
        /// </summary>
        protected void StartFeatureListSelect(Scenario scenario)
        {
            List<Feature> candidates = new List<Feature>();
            scenario.CommonData.Features.ForEach(x =>
            {
                if (x != null) candidates.Add(x);
            });
            List<Feature> initial = GetObjectListValue<Feature>();
            FeatrueSelectSystem system = GameSystem.GetSystem<FeatrueSelectSystem>();
            if (system == null)
            {
                Log.Warning("未找到特技选择系统");
                return;
            }
            system.Start(candidates, initial, candidates.Count, OnFeatureListSelected, null, "选择特技");
        }

        /// <summary>
        /// 特技多选选择器返回结果
        /// </summary>
        protected void OnFeatureListSelected(List<Feature> result)
        {
            if (result == null) return;
            curValue = result;
            RefreshObjectView();
        }

        // ==================== 确定与取消 ====================

        /// <summary>
        /// 确定按钮：按编辑类型收集值并写入全部目标对象
        /// </summary>
        protected void OnConfirmClicked()
        {
            if (Target == null || SortTitle == null)
            {
                CloseSelf();
                return;
            }

            if (!CollectCurValue())
            {
                return;
            }

            // 多对象编辑时将同一结果写入全部目标对象，并统计写入失败数量
            int successCount = 0;
            int failCount = 0;
            for (int i = 0; i < Targets.Count; i++)
            {
                SangoObject target = Targets[i];
                if (target == null)
                {
                    continue;
                }
                try
                {
                    SortTitle.SetValue(target, GetWriteValue(target));
                    successCount++;
                }
                catch (Exception e)
                {
                    failCount++;
                    Log.Error("写入属性:" + SortTitle.name + " 到对象 " + target.Name + " 失败:" + e.Message);
                }
            }

            if (failCount > 0)
            {
                Log.Warning("批量修改属性:" + SortTitle.name + " 完成,成功 " + successCount + " 个,失败 " + failCount + " 个");
            }
            else if (IsMultiEdit)
            {
                Log.Info("批量修改属性:" + SortTitle.name + " 完成,共修改 " + successCount + " 个对象");
            }

            // 全部写入失败时不关闭窗口,便于修正后重试
            if (successCount == 0 && failCount > 0)
            {
                return;
            }

            Action action = onConfirmAction;
            CloseSelf();
            action?.Invoke();
        }

        /// <summary>
        /// 按编辑类型从控件收集当前值
        /// </summary>
        /// <returns>收集是否成功（如Json格式错误、数值格式错误时返回false）</returns>
        protected bool CollectCurValue()
        {
            switch (EditType)
            {
                case DataEditType.Text:
                    curValue = textInput != null ? textInput.text : string.Empty;
                    break;
                case DataEditType.TextArea:
                    curValue = textAreaInput != null ? textAreaInput.text : string.Empty;
                    break;
                case DataEditType.JsonEdit:
                    {
                        string text = textAreaInput != null ? textAreaInput.text : string.Empty;
                        JToken token;
                        if (!TryParseJson(text, out token))
                        {
                            return false;
                        }
                        jsonText = text;
                        curValue = token;
                        break;
                    }
                case DataEditType.IntInput:
                    {
                        if (intInput != null && !int.TryParse(intInput.text, out int parsed))
                        {
                            Log.Warning("请输入整数");
                            return false;
                        }
                        int parsedValue = intInput != null ? int.Parse(intInput.text) : 0;
                        curValue = Math.Min(Math.Max(parsedValue, SortTitle.minValue), SortTitle.maxValue);
                        break;
                    }
                case DataEditType.FloatInput:
                case DataEditType.FloatCalculator:
                    {
                        if (floatInput != null
                            && !float.TryParse(floatInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatParsed))
                        {
                            Log.Warning("请输入数字");
                            return false;
                        }
                        float value = floatInput != null
                            ? float.Parse(floatInput.text, NumberStyles.Float, CultureInfo.InvariantCulture)
                            : 0f;
                        curValue = Math.Min(Math.Max(value, SortTitle.minValue), SortTitle.maxValue);
                        break;
                    }
                case DataEditType.ColorPicker:
                    curValue = ReadColorInputs();
                    break;
                case DataEditType.ArrayEdit:
                    curValue = ParseArrayValue(arrayInput != null ? arrayInput.text : string.Empty);
                    break;
                default:
                    // 下拉/对象/头像/城池等类型的值在选择时已写入curValue，无需再收集
                    break;
            }
            return true;
        }

        /// <summary>
        /// 获取写入指定目标的值
        /// Json按目标逐个解析（避免多对象共用同一个JToken实例）；
        /// 颜色按原始类型（Color32）还原；其余直接返回当前值
        /// </summary>
        protected object GetWriteValue(SangoObject target)
        {
            if (EditType == DataEditType.JsonEdit)
            {
                JToken token;
                if (TryParseJson(jsonText, out token))
                {
                    return token;
                }
                return curValue;
            }
            if (EditType == DataEditType.ColorPicker && colorValueIsColor32)
            {
                return (Color32)ToColor(curValue);
            }
            return curValue;
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        protected void OnCancelClicked()
        {
            Action action = onCancelAction;
            CloseSelf();
            action?.Invoke();
        }

        /// <summary>
        /// 关闭自身窗口
        /// </summary>
        protected void CloseSelf()
        {
            Window.Instance.Close(WindowName);
        }

        /// <summary>
        /// 设置GameObject显隐，引用为空时忽略
        /// </summary>
        protected void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }
    }
}
