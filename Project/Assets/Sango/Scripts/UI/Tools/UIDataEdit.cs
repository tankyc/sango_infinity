using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 弹出式的修改数据界面
    /// 关心一个SangoObject和当前的ObjectSortTitle
    /// 根据ObjectSortTitle上配置的DataEditType（值修改类型）与DataSetType（数据集类型），
    /// 使用对应的控件编辑目标对象的属性，支持以下几种数据类型：
    /// 1.Text（文本修改） 2.IntDropdown（下拉菜单，如人物状态/势力集合/城市集合）
    /// 3.IntInput（int文本输入） 4.IntCalculator（UICalculator输入）
    /// 5.HeadIcon（头像选择） 6.Object（对象类型，复合修改）
    /// 7.CitySelect（城池选择，通过UISelectCityWorldMap世界地图选城，Dropdown仅同步显示当前城池）
    /// 8.SpouseList（配偶列表修改，特殊数据修改接口：多选武将，写回时自动解除原配偶关系并建立新关系）
    /// 9.FeatureList（特技列表修改，特殊数据修改接口：调用特技选择器多选特技）
    /// 对象列表类编辑（8/9）复用“对象编辑区”，列表内容显示为拼接文本，按钮打开对应的多选选择器
    /// </summary>
    public class UIDataEdit : UGUIWindow
    {
        /// <summary>窗口名称，对应Prefab：Assets/Mods/Content/Assets/UI/Prefab/window_data_edit.prefab</summary>
        public const string WindowName = "window_data_edit";

        // 基础引用
        public Text titleText;                 // 标题，显示当前修改的属性名称
        public Button confirmButton;           // 确定按钮
        public Button cancelButton;            // 取消按钮

        // 1.文本编辑区（Text）
        public GameObject textEditRoot;        // 文本编辑根节点
        public InputField textInput;           // 文本输入框

        // 2.下拉菜单编辑区（IntDropdown）
        public GameObject dropdownEditRoot;    // 下拉编辑根节点
        public Dropdown valueDropdown;         // 下拉控件

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

        // 6.对象类型编辑区（Object）
        public GameObject objectEditRoot;      // 对象编辑根节点
        public Text objectValueText;           // 当前对象显示
        public Button objectSelectButton;      // 打开对象选择器的按钮
        public Button objectEditButton;        // 打开对象编辑器的按钮（复合修改）

        // 7.城池选择编辑区（CitySelect）: 编辑时展示世界地图（UISelectCityWorldMap），
        // 默认所有城池均可选取，点击地图上的城池完成修改；Dropdown仅用于同步显示当前所选城池
        public GameObject citySelectEditRoot;  // 城池选择编辑根节点（世界地图所在的显示区域）
        public Dropdown cityDropdown;          // 城池下拉（只读，仅同步显示当前所选城池）
        public UISelectCityWorldMap cityWorldMap; // 世界地图选城组件（在window_data_edit的editWorldMap节点上）

        /// <summary>当前正在编辑的目标对象</summary>
        public SangoObject Target { get; protected set; }

        /// <summary>当前使用的排序/编辑标题，描述了字段的读取写入方式与编辑类型</summary>
        public ObjectSortTitle SortTitle { get; protected set; }

        /// <summary>候选数据所属的剧本，为空时自动查找当前编辑/运行剧本</summary>
        protected Scenario editScenario;

        /// <summary>当前编辑中的临时值</summary>
        protected object curValue;

        /// <summary>下拉菜单使用的选项列表</summary>
        protected readonly List<DataEditOption> options = new List<DataEditOption>();

        /// <summary>城池选择方案使用的下拉选项列表（仅用于同步显示当前城池）</summary>
        protected readonly List<DataEditOption> citySelectOptions = new List<DataEditOption>();

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
        /// 打开数据编辑窗口（便捷入口）
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="sortTitle">目标对象的ObjectSortTitle（需配置editType）</param>
        /// <param name="scenario">候选数据所属剧本，为空自动获取</param>
        /// <param name="onConfirm">确定回调</param>
        /// <param name="onCancel">取消回调</param>
        /// <returns>窗口实例</returns>
        public static UIDataEdit Show(SangoObject target, ObjectSortTitle sortTitle, Scenario scenario = null, Action onConfirm = null, Action onCancel = null)
        {
            if (target == null || sortTitle == null) return null;
            if (!sortTitle.CanEdit)
            {
                Log.Warning("属性:" + sortTitle.name + " 未配置值修改类型,无法编辑");
                return null;
            }
            // 编辑前拦截特殊数据修改：如君主身份不可修改
            if (!sortTitle.CanSetValue(target))
            {
                Log.Warning("属性:" + sortTitle.name + " 不允许修改当前对象（如君主身份不可修改,需先删除其势力）");
                return null;
            }
            return Window.Instance.Open<UIDataEdit>(WindowName, target, sortTitle, scenario, onConfirm, onCancel);
        }

        /// <summary>
        /// 窗口打开时的初始化
        /// </summary>
        /// <param name="objects">objects[0]目标对象，objects[1]ObjectSortTitle，objects[2]可选剧本，objects[3]确定回调，objects[4]取消回调</param>
        public override void OnOpen(params object[] objects)
        {
            SangoObject target = objects != null && objects.Length > 0 ? objects[0] as SangoObject : null;
            ObjectSortTitle sortTitle = objects != null && objects.Length > 1 ? objects[1] as ObjectSortTitle : null;
            Scenario scenario = objects != null && objects.Length > 2 ? objects[2] as Scenario : null;
            Action onConfirm = objects != null && objects.Length > 3 ? objects[3] as Action : null;
            Action onCancel = objects != null && objects.Length > 4 ? objects[4] as Action : null;
            Init(target, sortTitle, scenario, onConfirm, onCancel);
        }

        /// <summary>
        /// 数据初始化与UI刷新
        /// </summary>
        protected void Init(SangoObject target, ObjectSortTitle sortTitle, Scenario scenario, Action onConfirm, Action onCancel)
        {
            if (target == null || sortTitle == null)
            {
                Log.Error("UIDataEdit初始化失败,缺少目标对象或ObjectSortTitle");
                CloseSelf();
                return;
            }

            // 编辑前拦截特殊数据修改：如君主身份不可修改
            if (!sortTitle.CanSetValue(target))
            {
                Log.Warning("属性:" + sortTitle.name + " 不允许修改当前对象（如君主身份不可修改,需先删除其势力）");
                CloseSelf();
                return;
            }

            RemoveListeners();

            Target = target;
            SortTitle = sortTitle;
            editScenario = scenario;
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;

            // 读取当前值,文本模式下若读取不到则回退到显示字符串
            curValue = ReadCurrentValue();
            if (EditType == DataEditType.Text && curValue == null)
            {
                curValue = ReadCurrentValueStr();
            }

            RefreshUI();
            BindListeners();
        }

        /// <summary>
        /// 窗口每次重新显示时刷新当前值（例如从计算器/头像窗口返回时）
        /// </summary>
        public override void OnRefresh()
        {
            if (Target == null || SortTitle == null) return;
            curValue = ReadCurrentValue();
            if (EditType == DataEditType.Text && curValue == null)
            {
                curValue = ReadCurrentValueStr();
            }
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
            SortTitle = null;
            curValue = null;
            options.Clear();
            citySelectOptions.Clear();
            base.OnClose();
        }

        // ==================== UI刷新 ====================

        /// <summary>
        /// 根据编辑类型刷新整个界面
        /// </summary>
        protected void RefreshUI()
        {
            // 标题
            if (titleText != null)
            {
                titleText.text = SortTitle != null && !string.IsNullOrEmpty(SortTitle.name) ? "修改" + SortTitle.name : "修改数据";
            }

            // 控制各区显隐
            bool showText = EditType == DataEditType.Text;
            bool showDropdown = EditType == DataEditType.IntDropdown;
            bool showInt = EditType == DataEditType.IntInput;
            bool showCalculator = EditType == DataEditType.IntCalculator;
            bool showHead = EditType == DataEditType.HeadIcon;
            bool showObject = EditType == DataEditType.Object;
            bool showCitySelect = EditType == DataEditType.CitySelect;
            // 对象列表类编辑（配偶/特技多选）复用对象编辑区
            bool showSpouseList = EditType == DataEditType.SpouseList;
            bool showFeatureList = EditType == DataEditType.FeatureList;
            bool showListEdit = showSpouseList || showFeatureList;

            SetActive(textEditRoot, showText);
            SetActive(dropdownEditRoot, showDropdown);
            SetActive(intEditRoot, showInt);
            SetActive(calculatorEditRoot, showCalculator);
            SetActive(headEditRoot, showHead);
            SetActive(objectEditRoot, showObject || showListEdit);
            SetActive(citySelectEditRoot, showCitySelect);

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
        }

        /// <summary>
        /// 刷新下拉菜单的选项与选中项
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

            valueDropdown.ClearOptions();
            if (options.Count == 0)
            {
                options.Add(new DataEditOption("(无可用选项)", null));
            }
            valueDropdown.AddOptions(OptionsToData(options));

            int index = GetOptionIndex(curValue);
            if (index < 0 && curValue != null)
            {
                // 当前值不在选项中时,在首位插入一个保持原值的选项
                options.Insert(0, new DataEditOption(GetDisplayString(curValue), curValue));
                valueDropdown.ClearOptions();
                valueDropdown.AddOptions(OptionsToData(options));
                index = 0;
            }
            valueDropdown.SetValueWithoutNotify(index < 0 ? 0 : index);
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
            }
        }

        /// <summary>
        /// 刷新对象类型编辑视图（含配偶/特技等对象列表类编辑）
        /// </summary>
        protected void RefreshObjectView()
        {
            bool isObject = EditType == DataEditType.Object;
            bool isListEdit = EditType == DataEditType.SpouseList || EditType == DataEditType.FeatureList;

            if (objectValueText != null)
            {
                objectValueText.text = GetDisplayString(curValue);
            }

            // 单选对象类型依赖数据集类型判断是否可打开选择器；对象列表类编辑始终可打开多选选择器
            if (objectSelectButton != null)
            {
                objectSelectButton.gameObject.SetActive(isListEdit || IsObjectSelectable());
            }
            // 仅单选对象编辑（Object）可打开对象自身的编辑器（复合修改），对象列表类编辑不提供
            if (objectEditButton != null)
            {
                objectEditButton.gameObject.SetActive(isObject && curValue is SangoObject);
            }
        }

        // ==================== 城池选择方案（CitySelect） ====================

        /// <summary>
        /// 刷新城池选择视图：初始化世界地图并同步下拉显示
        /// 打开即展示世界地图，默认所有城池均可选取；修改时点击地图上的城池，
        /// 下拉列表仅用于同步显示当前所选城池
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
        /// 填充城池下拉选项：首位为“(无)”，其后为剧本中可选的全部城池（与地图可选城池一致）
        /// </summary>
        protected void FillCityDropdownOptions(Scenario scenario)
        {
            citySelectOptions.Clear();
            citySelectOptions.Add(new DataEditOption("(无)", null));
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
        /// 根据当前值查找城池下拉选项索引，找不到时返回0（即“(无)”）
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
                // null匹配“(无)”选项
                else if (value == null && optValue == null)
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>
        /// 世界地图选城回调：把最后选择的城池写入当前值，并同步下拉显示
        /// 当取消选择（列表为空）时当前值置空，表示不选任何城池
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

        // ==================== 数据读取与转换 ====================

        /// <summary>
        /// 读取目标对象的当前属性值
        /// </summary>
        protected object ReadCurrentValue()
        {
            try
            {
                return SortTitle.GetValue(Target);
            }
            catch (Exception e)
            {
                Log.Warning("读取属性:" + SortTitle.name + " 失败:" + e.Message);
                return null;
            }
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
        /// 判断当前数据集类型是否支持对象选择
        /// </summary>
        protected bool IsObjectSelectable()
        {
            DataSetType type = SortTitle.dataSetType;
            return type == DataSetType.Person || type == DataSetType.Force || type == DataSetType.City
                || type == DataSetType.Corps || type == DataSetType.Troop || type == DataSetType.Feature;
        }

        // ==================== 控件交互 ====================

        /// <summary>
        /// 绑定事件监听
        /// </summary>
        protected void BindListeners()
        {
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
            if (valueDropdown != null) valueDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            if (calculatorButton != null) calculatorButton.onClick.AddListener(OnCalculatorButtonClicked);
            if (headButton != null) headButton.onClick.AddListener(OnHeadButtonClicked);
            if (objectSelectButton != null) objectSelectButton.onClick.AddListener(OnObjectSelectClicked);
            if (objectEditButton != null) objectEditButton.onClick.AddListener(OnObjectEditClicked);
        }

        /// <summary>
        /// 移除事件监听
        /// </summary>
        protected void RemoveListeners()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClicked);
            if (valueDropdown != null) valueDropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
            if (calculatorButton != null) calculatorButton.onClick.RemoveListener(OnCalculatorButtonClicked);
            if (headButton != null) headButton.onClick.RemoveListener(OnHeadButtonClicked);
            if (objectSelectButton != null) objectSelectButton.onClick.RemoveListener(OnObjectSelectClicked);
            if (objectEditButton != null) objectEditButton.onClick.RemoveListener(OnObjectEditClicked);
        }

        /// <summary>
        /// 下拉菜单选择改变
        /// </summary>
        protected void OnDropdownValueChanged(int index)
        {
            if (index >= 0 && index < options.Count)
            {
                curValue = options[index].value;
            }
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
        /// 打开对象选择器
        /// </summary>
        protected void OnObjectSelectClicked()
        {
            Scenario scenario = GetEditScenario();
            if (scenario == null)
            {
                Log.Warning("没有可用剧本数据,无法打开对象选择器");
                return;
            }

            // 特殊数据修改接口：配偶/特技使用多选选择器
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

            switch (SortTitle.dataSetType)
            {
                case DataSetType.Person:
                    StartPersonSelect(scenario);
                    break;
                case DataSetType.Force:
                    StartForceSelect(scenario);
                    break;
                case DataSetType.City:
                    StartCitySelect(scenario);
                    break;
                case DataSetType.Corps:
                    StartCorpsSelect(scenario);
                    break;
                case DataSetType.Troop:
                    StartTroopSelect(scenario);
                    break;
                case DataSetType.Feature:
                    StartFeatureSelect(scenario);
                    break;
                default:
                    Log.Warning("属性:" + SortTitle.name + " 的数据集类型不支持对象选择");
                    break;
            }
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

        // ==================== 各对象选择器的启动 ====================

        /// <summary>
        /// 启动武将选择器
        /// </summary>
        protected void StartPersonSelect(Scenario scenario)
        {
            List<Person> candidates = new List<Person>();
            scenario.personSet.ForEach(x => { if (x != null) candidates.Add(x); });
            List<Person> initial = new List<Person>();
            if (curValue is Person) initial.Add((Person)curValue);
            PersonSelectSystem system = GameSystem.GetSystem<PersonSelectSystem>();
            if (system == null) { Log.Warning("未找到武将选择系统"); return; }
            system.Start(candidates, initial, 1, (result) => { if (result.Count > 0) OnObjectSelected(result[0]); }, null, "选择武将");
        }

        /// <summary>
        /// 启动势力选择器
        /// </summary>
        protected void StartForceSelect(Scenario scenario)
        {
            List<Force> candidates = new List<Force>();
            scenario.forceSet.ForEach(x => { if (x != null) candidates.Add(x); });
            List<Force> initial = new List<Force>();
            if (curValue is Force) initial.Add((Force)curValue);
            ForceSelectSystem system = GameSystem.GetSystem<ForceSelectSystem>();
            if (system == null) { Log.Warning("未找到势力选择系统"); return; }
            system.Start(candidates, initial, 1, (result) => { if (result.Count > 0) OnObjectSelected(result[0]); }, null, "选择势力");
        }

        /// <summary>
        /// 启动城市选择器
        /// </summary>
        protected void StartCitySelect(Scenario scenario)
        {
            List<City> candidates = new List<City>();
            scenario.citySet.ForEach(x => { if (x != null) candidates.Add(x); });
            List<City> initial = new List<City>();
            if (curValue is City) initial.Add((City)curValue);
            CitySelectSystem system = GameSystem.GetSystem<CitySelectSystem>();
            if (system == null) { Log.Warning("未找到城市选择系统"); return; }
            system.Start(candidates, initial, 1, (result) => { if (result.Count > 0) OnObjectSelected(result[0]); }, null, "选择城市");
        }

        /// <summary>
        /// 启动军团选择器
        /// </summary>
        protected void StartCorpsSelect(Scenario scenario)
        {
            List<Corps> candidates = new List<Corps>();
            scenario.corpsSet.ForEach(x => { if (x != null) candidates.Add(x); });
            List<Corps> initial = new List<Corps>();
            if (curValue is Corps) initial.Add((Corps)curValue);
            CorpsSelectSystem system = GameSystem.GetSystem<CorpsSelectSystem>();
            if (system == null) { Log.Warning("未找到军团选择系统"); return; }
            system.Start(candidates, initial, 1, (result) => { if (result.Count > 0) OnObjectSelected(result[0]); }, null, "选择军团");
        }

        /// <summary>
        /// 启动部队选择器
        /// </summary>
        protected void StartTroopSelect(Scenario scenario)
        {
            List<Troop> candidates = new List<Troop>();
            scenario.troopsSet.ForEach(x => { if (x != null) candidates.Add(x); });
            List<Troop> initial = new List<Troop>();
            if (curValue is Troop) initial.Add((Troop)curValue);
            TroopSelectSystem system = GameSystem.GetSystem<TroopSelectSystem>();
            if (system == null) { Log.Warning("未找到部队选择系统"); return; }
            system.Start(candidates, initial, 1, (result) => { if (result.Count > 0) OnObjectSelected(result[0]); }, null, "选择部队");
        }

        /// <summary>
        /// 启动特技选择器
        /// </summary>
        protected void StartFeatureSelect(Scenario scenario)
        {
            List<Feature> candidates = new List<Feature>();
            scenario.CommonData.Features.ForEach(x => { if (x != null) candidates.Add(x); });
            List<Feature> initial = new List<Feature>();
            if (curValue is Feature) initial.Add((Feature)curValue);
            FeatrueSelectSystem system = GameSystem.GetSystem<FeatrueSelectSystem>();
            if (system == null) { Log.Warning("未找到特技选择系统"); return; }
            system.Start(candidates, initial, 1, (result) => { if (result.Count > 0) OnObjectSelected(result[0]); }, null, "选择特技");
        }

        /// <summary>
        /// 对象选择器返回后的处理
        /// </summary>
        protected void OnObjectSelected(SangoObject obj)
        {
            curValue = obj;
            RefreshObjectView();
        }

        // ==================== 特殊数据修改接口（配偶/特技多选） ====================

        /// <summary>
        /// 获取当前编辑值的对象列表（兼容List/SangoObjectList/单个对象/null等输入）
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <returns>对象列表</returns>
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
        /// 用于维持配偶编辑的唯一约束：一个武将最多只能被一个其他武将登记为配偶
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="person">被检查的武将</param>
        /// <param name="target">当前编辑的目标武将</param>
        /// <returns>是否已被他人登记</returns>
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
        /// 目标武将自己登记的配偶会保留在候选中（支持查看与反选）
        /// </summary>
        /// <param name="scenario">剧本</param>
        /// <param name="target">当前编辑的目标武将</param>
        /// <returns>候选武将列表</returns>
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
        /// 可多选配偶，写回时由SortTitle的写回逻辑统一解除原关系并建立新关系
        /// </summary>
        /// <param name="scenario">剧本</param>
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
        /// <param name="result">选中的配偶列表</param>
        protected void OnSpouseListSelected(List<Person> result)
        {
            if (result == null) return;
            curValue = result;
            RefreshObjectView();
        }

        /// <summary>
        /// 启动特技多选选择器（特殊数据修改接口：特技）
        /// 可多选特技，写回时整体替换武将的特技集合
        /// </summary>
        /// <param name="scenario">剧本</param>
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
        /// <param name="result">选中的特技列表</param>
        protected void OnFeatureListSelected(List<Feature> result)
        {
            if (result == null) return;
            curValue = result;
            RefreshObjectView();
        }

        // ==================== 确定与取消 ====================

        /// <summary>
        /// 确定按钮：收集值并写入目标对象
        /// </summary>
        protected void OnConfirmClicked()
        {
            if (Target == null || SortTitle == null)
            {
                CloseSelf();
                return;
            }

            // 文本与数字输入在确认时才从输入框收集
            if (EditType == DataEditType.Text)
            {
                curValue = textInput != null ? textInput.text : string.Empty;
            }
            else if (EditType == DataEditType.IntInput)
            {
                if (intInput != null && !int.TryParse(intInput.text, out int parsed))
                {
                    Log.Warning("请输入整数");
                    return;
                }
                int parsedValue = intInput != null ? int.Parse(intInput.text) : 0;
                curValue = Math.Min(Math.Max(parsedValue, SortTitle.minValue), SortTitle.maxValue);
            }

            try
            {
                SortTitle.SetValue(Target, curValue);
            }
            catch (Exception e)
            {
                Log.Error("写入属性:" + SortTitle.name + " 失败:" + e.Message);
                return;
            }

            Action action = onConfirmAction;
            CloseSelf();
            action?.Invoke();
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
