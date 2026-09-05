using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 剧本编辑器主窗口 - 通过分页管理剧本的编辑
    /// 分页: 剧本信息、武将、势力、军团、城池
    /// 关联窗口: window_scenario_edit
    /// </summary>
    public class UIScenarioEdit : UGUIWindow
    {
        public GameObject menuObject;
        public GameObject contentObject;


        #region 顶部工具栏
        /// <summary>
        /// 新建剧本按钮
        /// </summary>
        public Button newButton;

        /// <summary>
        /// 加载剧本按钮
        /// </summary>
        public Button loadButton;

        /// <summary>
        /// 保存剧本按钮
        /// </summary>
        public Button saveButton;

        /// <summary>
        /// 当前剧本名称标签
        /// </summary>
        public Text scenarioNameText;
        #endregion

        #region 分页页签开关
        /// <summary>
        /// 剧本信息页签
        /// </summary>
        public Toggle infoTabButton;

        /// <summary>
        /// 武将页签
        /// </summary>
        public Toggle personTabButton;

        /// <summary>
        /// 势力页签
        /// </summary>
        public Toggle forceTabButton;

        /// <summary>
        /// 军团页签
        /// </summary>
        public Toggle corpsTabButton;

        /// <summary>
        /// 城池页签
        /// </summary>
        public Toggle cityTabButton;
        #endregion

        #region 分页内容
        /// <summary>
        /// 剧本信息分页
        /// </summary>
        public GameObject infoTabPage;

        /// <summary>
        /// 武将分页
        /// </summary>
        public GameObject personTabPage;

        /// <summary>
        /// 势力分页
        /// </summary>
        public GameObject forceTabPage;

        /// <summary>
        /// 军团分页
        /// </summary>
        public GameObject corpsTabPage;

        /// <summary>
        /// 城池分页
        /// </summary>
        public GameObject cityTabPage;
        #endregion

        #region 共享对象列表
        /// <summary>
        /// 共享对象列表 - 武将/势力/军团/城池各分页共用的UIObjectDisplayPlane,
        /// 切换分页时通过Init将列表刷新为对应分页的数据源
        /// </summary>
        public UIObjectDisplayPlane objectList;
        #endregion

        #region 公共操作按钮
        /// <summary>
        /// 新建按钮(公共) - 按当前分页执行不同的新建逻辑
        /// 武将页:新建武将 势力页:新建势力 军团页:新建军团
        /// </summary>
        public Button createButton;

        /// <summary>
        /// 删除按钮(公共) - 按当前分页删除选中的对象(支持多选)
        /// 武将页:删除武将 势力页:删除势力 军团页:删除军团
        /// </summary>
        public Button deleteButton;

        /// <summary>
        /// 导入按钮(公共) - 按当前分页执行不同的导入逻辑
        /// 城池页:导入城池数据
        /// </summary>
        public Button importButton;
        #endregion

        #region 过滤器与多选组件
        /// <summary>
        /// 过滤条件输入框 - 条目以空格分隔,条件类型与条件值以:分隔,支持比较符
        /// 示例: 类型:势力 统率&gt;70 姓名:曹 性别:1
        /// </summary>
        public InputField filterInput;

        /// <summary>
        /// 应用过滤按钮
        /// </summary>
        public Button filterButton;

        /// <summary>
        /// 清除过滤按钮
        /// </summary>
        public Button filterClearButton;

        /// <summary>
        /// 一键全选按钮 - 选中当前分页列表中的全部对象
        /// </summary>
        public Button selectAllButton;

        /// <summary>
        /// 一键取消按钮 - 清空当前分页列表的全部选中
        /// </summary>
        public Button unSelectAllButton;
        #endregion

        #region 剧本信息页组件
        /// <summary>
        /// 剧本名称输入框
        /// </summary>
        public InputField infoNameInput;

        /// <summary>
        /// 剧本描述输入框
        /// </summary>
        public InputField descriptionInput;

        /// <summary>
        /// 剧本年份按钮(点击调用 UICalculator)
        /// </summary>
        public Button yearButton;

        /// <summary>
        /// 剧本年份显示文本
        /// </summary>
        public Text yearText;

        /// <summary>
        /// 剧本月份按钮(点击调用 UICalculator)
        /// </summary>
        public Button monthButton;

        /// <summary>
        /// 剧本月份显示文本
        /// </summary>
        public Text monthText;

        // 剧本日期不开放修改,固定为0

        /// <summary>
        /// 剧本类型下拉框(0普通剧本 1玩家剧本)
        /// </summary>
        public UIDropdownField typeDropdownField;

        /// <summary>
        /// 剧本Id按钮(点击调用 UICalculator)
        /// </summary>
        public Button idButton;

        /// <summary>
        /// 剧本Id显示文本
        /// </summary>
        public Text idText;

        /// <summary>
        /// 地图类型下拉框(Mod下Map目录的bin文件名)
        /// </summary>
        public UIDropdownField mapTypeDropdownField;
        #endregion

        /// <summary>
        /// 当前分页索引(0剧本信息 1武将 2势力 3军团 4城池)
        /// </summary>
        private int currentTab = 0;

        /// <summary>
        /// 触发刷新时的标识 - 防止OnValueChanged循环触发
        /// </summary>
        private bool refreshing;

        /// <summary>
        /// 武将列表数据(过滤后,用于展示)
        /// </summary>
        private List<SangoObject> personDatas = new List<SangoObject>();

        /// <summary>
        /// 势力列表数据(过滤后,用于展示)
        /// </summary>
        private List<SangoObject> forceDatas = new List<SangoObject>();

        /// <summary>
        /// 军团列表数据(过滤后,用于展示)
        /// </summary>
        private List<SangoObject> corpsDatas = new List<SangoObject>();

        /// <summary>
        /// 城池列表数据(过滤后,用于展示)
        /// </summary>
        private List<SangoObject> cityDatas = new List<SangoObject>();

        /// <summary>
        /// 武将源数据(未过滤)
        /// </summary>
        private List<SangoObject> personSourceDatas = new List<SangoObject>();

        /// <summary>
        /// 势力源数据(未过滤)
        /// </summary>
        private List<SangoObject> forceSourceDatas = new List<SangoObject>();

        /// <summary>
        /// 军团源数据(未过滤)
        /// </summary>
        private List<SangoObject> corpsSourceDatas = new List<SangoObject>();

        /// <summary>
        /// 城池源数据(未过滤)
        /// </summary>
        private List<SangoObject> citySourceDatas = new List<SangoObject>();

        /// <summary>
        /// 当前过滤文本(条目以空格分隔,条件类型与条件值以:分隔,支持比较符)
        /// </summary>
        private string currentFilterText = "";

        /// <summary>
        /// 当前选中的武将(多选时的主选中项,即最后选中的一项)
        /// </summary>
        private Person selectedPerson;

        /// <summary>
        /// 当前选中的势力(多选时的主选中项,即最后选中的一项)
        /// </summary>
        private Force selectedForce;

        /// <summary>
        /// 当前选中的军团(多选时的主选中项,即最后选中的一项)
        /// </summary>
        private Corps selectedCorps;

        /// <summary>
        /// 当前选中的城池(多选时的主选中项,即最后选中的一项)
        /// </summary>
        private City selectedCity;

        /// <summary>
        /// 当前多选选中的武将列表
        /// </summary>
        private List<Person> selectedPersons = new List<Person>();

        /// <summary>
        /// 当前多选选中的势力列表
        /// </summary>
        private List<Force> selectedForces = new List<Force>();

        /// <summary>
        /// 当前多选选中的军团列表
        /// </summary>
        private List<Corps> selectedCorpsList = new List<Corps>();

        /// <summary>
        /// 当前多选选中的城池列表
        /// </summary>
        private List<City> selectedCities = new List<City>();

        #region 窗口生命周期
        protected override void Awake()
        {
            base.Awake();
            BindEvents();
        }

        /// <summary>
        /// 窗口打开 - 绑定事件并初始化分页
        /// </summary>
        /// <param name="objects">参数列表</param>
        public override void OnOpen()
        {
            menuObject.SetActive(true);
            contentObject.SetActive(false);
            objectList.hasId = true;
        }

        public void OnScenarioLoaded()
        {
            menuObject.SetActive(false);
            contentObject.SetActive(true);
            currentTab = 0;
            SetTab(0);
            RefreshAll();
        }

        /// <summary>
        /// 窗口关闭 - 清理监听器和引用
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();
            RemoveListeners();
            selectedPerson = null;
            selectedForce = null;
            selectedCorps = null;
            selectedCity = null;
            selectedPersons.Clear();
            selectedForces.Clear();
            selectedCorpsList.Clear();
            selectedCities.Clear();
            personDatas.Clear();
            forceDatas.Clear();
            corpsDatas.Clear();
            cityDatas.Clear();
            personSourceDatas.Clear();
            forceSourceDatas.Clear();
            corpsSourceDatas.Clear();
            citySourceDatas.Clear();
            currentFilterText = "";
        }

        /// <summary>
        /// 刷新窗口 - 刷新全部数据
        /// </summary>
        public override void OnRefresh()
        {
            RefreshAll();
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定UI事件
        /// </summary>
        private void BindEvents()
        {
            if (newButton != null) newButton.onClick.AddListener(OnNewClick);
            if (loadButton != null) loadButton.onClick.AddListener(OnLoadClick);

            if (saveButton != null) saveButton.onClick.AddListener(OnSaveClick);

            if (infoTabButton != null) infoTabButton.onValueChanged.AddListener((on) => { if (on) SetTab(0); });
            if (personTabButton != null) personTabButton.onValueChanged.AddListener((on) => { if (on) SetTab(1); });
            if (forceTabButton != null) forceTabButton.onValueChanged.AddListener((on) => { if (on) SetTab(2); });
            if (corpsTabButton != null) corpsTabButton.onValueChanged.AddListener((on) => { if (on) SetTab(3); });
            if (cityTabButton != null) cityTabButton.onValueChanged.AddListener((on) => { if (on) SetTab(4); });

            if (infoNameInput != null) infoNameInput.onEndEdit.AddListener(OnInfoNameEndEdit);
            if (descriptionInput != null) descriptionInput.onEndEdit.AddListener(OnDescriptionEndEdit);
            if (yearButton != null) yearButton.onClick.AddListener(OnYearClick);
            if (monthButton != null) monthButton.onClick.AddListener(OnMonthClick);
            if (idButton != null) idButton.onClick.AddListener(OnIdClick);

            // 绑定公共操作按钮事件
            if (createButton != null) createButton.onClick.AddListener(OnCreateObjectClick);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteObjectClick);
            if (importButton != null) importButton.onClick.AddListener(OnImportObjectClick);

            // 绑定过滤器与多选按钮事件
            if (filterButton != null) filterButton.onClick.AddListener(OnFilterApply);
            if (filterClearButton != null) filterClearButton.onClick.AddListener(OnFilterClear);
            if (filterInput != null) filterInput.onEndEdit.AddListener(OnFilterInputEndEdit);
            if (selectAllButton != null) selectAllButton.onClick.AddListener(OnSelectAllClick);
            if (unSelectAllButton != null) unSelectAllButton.onClick.AddListener(OnUnSelectAllClick);

            // 绑定共享列表的多选回调 - 根据当前分页分发到对应的处理
            if (objectList != null)
            {
                objectList.OnSelectCall = null;
                objectList.OnMultiSelectCall = OnObjectListMultiSelect;
            }
        }

        /// <summary>
        /// 清理所有事件监听器
        /// </summary>
        private void RemoveListeners()
        {
            if (newButton != null) newButton.onClick.RemoveListener(OnNewClick);
            if (loadButton != null) loadButton.onClick.RemoveListener(OnLoadClick);
            if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClick);

            if (infoTabButton != null) infoTabButton.onValueChanged.RemoveAllListeners();
            if (personTabButton != null) personTabButton.onValueChanged.RemoveAllListeners();
            if (forceTabButton != null) forceTabButton.onValueChanged.RemoveAllListeners();
            if (corpsTabButton != null) corpsTabButton.onValueChanged.RemoveAllListeners();
            if (cityTabButton != null) cityTabButton.onValueChanged.RemoveAllListeners();

            if (infoNameInput != null) infoNameInput.onEndEdit.RemoveAllListeners();
            if (descriptionInput != null) descriptionInput.onEndEdit.RemoveAllListeners();
            if (yearButton != null) yearButton.onClick.RemoveListener(OnYearClick);
            if (monthButton != null) monthButton.onClick.RemoveListener(OnMonthClick);
            if (idButton != null) idButton.onClick.RemoveListener(OnIdClick);

            if (createButton != null) createButton.onClick.RemoveListener(OnCreateObjectClick);
            if (deleteButton != null) deleteButton.onClick.RemoveListener(OnDeleteObjectClick);
            if (importButton != null) importButton.onClick.RemoveListener(OnImportObjectClick);

            if (filterButton != null) filterButton.onClick.RemoveListener(OnFilterApply);
            if (filterClearButton != null) filterClearButton.onClick.RemoveListener(OnFilterClear);
            if (filterInput != null) filterInput.onEndEdit.RemoveListener(OnFilterInputEndEdit);
            if (selectAllButton != null) selectAllButton.onClick.RemoveListener(OnSelectAllClick);
            if (unSelectAllButton != null) unSelectAllButton.onClick.RemoveListener(OnUnSelectAllClick);

            // 清理共享列表的选中回调
            if (objectList != null)
            {
                objectList.OnSelectCall = null;
                objectList.OnMultiSelectCall = null;
            }
        }

        /// <summary>
        /// 武将列表多选回调 - 更新多选列表,主选中项为最后选中的武将
        /// </summary>
        /// <param name="indexes">选中索引集合</param>
        private void OnMultiSelectPersons(List<int> indexes)
        {
            selectedPersons.Clear();
            Person primary = null;
            if (indexes != null)
            {
                for (int i = 0; i < indexes.Count; i++)
                {
                    int index = indexes[i];
                    if (personDatas != null && index >= 0 && index < personDatas.Count)
                    {
                        Person person = personDatas[index] as Person;
                        if (person != null)
                        {
                            selectedPersons.Add(person);
                            primary = person;
                        }
                    }
                }
            }
            selectedPerson = primary;
        }

        /// <summary>
        /// 势力列表多选回调 - 更新多选列表,主选中项为最后选中的势力
        /// </summary>
        /// <param name="indexes">选中索引集合</param>
        private void OnMultiSelectForces(List<int> indexes)
        {
            selectedForces.Clear();
            Force primary = null;
            if (indexes != null)
            {
                for (int i = 0; i < indexes.Count; i++)
                {
                    int index = indexes[i];
                    if (forceDatas != null && index >= 0 && index < forceDatas.Count)
                    {
                        Force force = forceDatas[index] as Force;
                        if (force != null)
                        {
                            selectedForces.Add(force);
                            primary = force;
                        }
                    }
                }
            }
            selectedForce = primary;
        }

        /// <summary>
        /// 军团列表多选回调 - 更新多选列表,主选中项为最后选中的军团
        /// </summary>
        /// <param name="indexes">选中索引集合</param>
        private void OnMultiSelectCorps(List<int> indexes)
        {
            selectedCorpsList.Clear();
            Corps primary = null;
            if (indexes != null)
            {
                for (int i = 0; i < indexes.Count; i++)
                {
                    int index = indexes[i];
                    if (corpsDatas != null && index >= 0 && index < corpsDatas.Count)
                    {
                        Corps corps = corpsDatas[index] as Corps;
                        if (corps != null)
                        {
                            selectedCorpsList.Add(corps);
                            primary = corps;
                        }
                    }
                }
            }
            selectedCorps = primary;
        }

        /// <summary>
        /// 城池列表多选回调 - 更新多选列表,主选中项为最后选中的城池
        /// </summary>
        /// <param name="indexes">选中索引集合</param>
        private void OnMultiSelectCities(List<int> indexes)
        {
            selectedCities.Clear();
            City primary = null;
            if (indexes != null)
            {
                for (int i = 0; i < indexes.Count; i++)
                {
                    int index = indexes[i];
                    if (cityDatas != null && index >= 0 && index < cityDatas.Count)
                    {
                        City city = cityDatas[index] as City;
                        if (city != null)
                        {
                            selectedCities.Add(city);
                            primary = city;
                        }
                    }
                }
            }
            selectedCity = primary;
        }

        /// <summary>
        /// 共享列表多选回调 - 根据当前分页分发到对应的处理
        /// </summary>
        /// <param name="indexes">选中索引集合</param>
        private void OnObjectListMultiSelect(List<int> indexes)
        {
            switch (currentTab)
            {
                case 1: OnMultiSelectPersons(indexes); break;
                case 2: OnMultiSelectForces(indexes); break;
                case 3: OnMultiSelectCorps(indexes); break;
                case 4: OnMultiSelectCities(indexes); break;
                default: break;
            }
        }
        #endregion

        #region 过滤器与多选操作
        /// <summary>
        /// 应用过滤按钮 - 按输入的过滤文本刷新当前分页列表
        /// </summary>
        private void OnFilterApply()
        {
            currentFilterText = filterInput != null ? filterInput.text : "";
            RefreshCurrentPage();
        }

        /// <summary>
        /// 过滤输入框输入结束 - 自动应用过滤
        /// </summary>
        /// <param name="text">过滤文本</param>
        private void OnFilterInputEndEdit(string text)
        {
            if (refreshing)
            {
                return;
            }
            currentFilterText = text;
            RefreshCurrentPage();
        }

        /// <summary>
        /// 清除过滤按钮 - 清空过滤条件并刷新当前分页列表
        /// </summary>
        private void OnFilterClear()
        {
            currentFilterText = "";
            if (filterInput != null) filterInput.text = "";
            RefreshCurrentPage();
        }

        /// <summary>
        /// 一键全选按钮 - 选中当前分页列表中的全部对象
        /// </summary>
        private void OnSelectAllClick()
        {
            if (currentTab <= 0 || objectList == null)
            {
                return;
            }
            objectList.SelectAll();
        }

        /// <summary>
        /// 一键取消按钮 - 清空当前分页列表的全部选中
        /// </summary>
        private void OnUnSelectAllClick()
        {
            if (currentTab <= 0 || objectList == null)
            {
                return;
            }
            objectList.UnSelectAll();
        }

        /// <summary>
        /// 按当前过滤文本过滤源数据到展示列表
        /// 过滤条目以空格分隔,条件类型与条件值以:分隔,支持比较符(如 类型:势力 统率&gt;70)
        /// </summary>
        /// <param name="source">源数据(未过滤)</param>
        /// <param name="dest">展示数据(过滤后)</param>
        /// <param name="sortTitles">当前分页的排序标题(中文字段名的数据来源)</param>
        private void ApplyFilter(List<SangoObject> source, List<SangoObject> dest, List<ObjectSortTitle> sortTitles)
        {
            SangoObjectFilter filter = SangoObjectFilter.Parse(currentFilterText);
            filter.Filter(source, dest, sortTitles);
        }

        /// <summary>
        /// 列表刷新后恢复多选选中状态 - 保留仍在展示列表中的选中项
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="selectedList">多选列表</param>
        /// <param name="displayDatas">展示数据(过滤后)</param>
        private void RestoreMultiSelect<T>(List<T> selectedList, List<SangoObject> displayDatas) where T : SangoObject
        {
            if (objectList == null)
            {
                return;
            }
            // 剔除已被过滤掉或失效的选中项
            selectedList.RemoveAll(x => x == null || !displayDatas.Contains(x));

            List<int> indexes = new List<int>();
            for (int i = 0; i < selectedList.Count; i++)
            {
                int index = displayDatas.IndexOf(selectedList[i]);
                if (index >= 0)
                {
                    indexes.Add(index);
                }
            }
            // SetMultiSelect会触发多选回调,同步主选中项
            objectList.SetMultiSelect(indexes);
        }
        #endregion

        #region 分页切换
        /// <summary>
        /// 切换分页
        /// </summary>
        /// <param name="index">分页索引(0剧本信息 1武将 2势力 3军团 4城池)</param>
        private void SetTab(int index)
        {
            currentTab = index;
            if (infoTabPage != null) infoTabPage.SetActive(index == 0);
            if (personTabPage != null) personTabPage.SetActive(index == 1);
            if (forceTabPage != null) forceTabPage.SetActive(index == 2);
            if (corpsTabPage != null) corpsTabPage.SetActive(index == 3);
            if (cityTabPage != null) cityTabPage.SetActive(index == 4);

            // 同步页签选中状态(不触发 onValueChanged,避免递归)
            if (infoTabButton != null) infoTabButton.SetIsOnWithoutNotify(index == 0);
            if (personTabButton != null) personTabButton.SetIsOnWithoutNotify(index == 1);
            if (forceTabButton != null) forceTabButton.SetIsOnWithoutNotify(index == 2);
            if (corpsTabButton != null) corpsTabButton.SetIsOnWithoutNotify(index == 3);
            if (cityTabButton != null) cityTabButton.SetIsOnWithoutNotify(index == 4);

            objectList.gameObject.SetActive(index > 0);

            // 按分页刷新公共按钮可用状态
            RefreshCommonButtons();

            // 切换分页后通过共享对象列表刷新当前分页的数据
            RefreshCurrentPage();
        }

        /// <summary>
        /// 按当前分页刷新公共按钮的可用状态
        /// 新建/删除: 武将/势力/军团页可用 导入: 城池页可用
        /// </summary>
        private void RefreshCommonButtons()
        {
            if (createButton != null) createButton.interactable = currentTab >= 1 && currentTab <= 3;
            if (deleteButton != null) deleteButton.interactable = currentTab >= 1 && currentTab <= 3;
            if (importButton != null) importButton.interactable = currentTab == 4;
        }
        #endregion

        #region 数据刷新
        /// <summary>
        /// 刷新全部数据
        /// </summary>
        private void RefreshAll()
        {
            RefreshScenarioName();
            RefreshInfoPage();
            RefreshCurrentPage();
        }

        /// <summary>
        /// 刷新当前分页 - 通过共享对象列表刷新对应分页的数据
        /// 各分页数据在切换到该分页时才重建,保证共享列表展示内容与分页一致
        /// </summary>
        private void RefreshCurrentPage()
        {
            switch (currentTab)
            {
                case 1: RefreshPersonPage(); break;
                case 2: RefreshForcePage(); break;
                case 3: RefreshCorpsPage(); break;
                case 4: RefreshCityPage(); break;
                default: break; // 剧本信息页不包含共享对象列表
            }
        }

        /// <summary>
        /// 刷新当前剧本名称
        /// </summary>
        private void RefreshScenarioName()
        {
            if (scenarioNameText == null)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            scenarioNameText.text = scenario != null ? scenario.Info.name : "未加载剧本";
        }

        /// <summary>
        /// 刷新剧本信息页 - 将剧本Info同步到输入框
        /// </summary>
        private void RefreshInfoPage()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null)
            {
                return;
            }
            refreshing = true;
            if (infoNameInput != null) infoNameInput.text = scenario.Info.name;
            if (descriptionInput != null) descriptionInput.text = scenario.Info.description;
            if (yearText != null) yearText.text = scenario.Info.year.ToString();
            if (monthText != null) monthText.text = scenario.Info.month.ToString();
            // 日期不开放修改,固定为0
            scenario.Info.day = 0;

            // 剧本类型下拉框(0普通剧本 1玩家剧本)
            if (typeDropdownField != null)
            {
                List<string> typeOptions = new List<string>() { "普通剧本", "玩家剧本" };
                typeDropdownField.Set("剧本类型", scenario.Info.type, typeOptions, (v) =>
                {
                    scenario.Info.type = v;
                });
            }

            // 剧本Id显示文本
            if (idText != null) idText.text = scenario.Info.id.ToString();

            // 地图类型下拉框(枚举Mod下Map目录的bin文件名)
            if (mapTypeDropdownField != null)
            {
                List<string> mapOptions = GetMapTypeOptions();
                int mapIndex = mapOptions.IndexOf(scenario.Info.mapType);
                if (mapIndex < 0)
                {
                    mapIndex = 0;
                }
                mapTypeDropdownField.Set("地图类型", mapIndex, mapOptions, (v) =>
                {
                    if (v >= 0 && v < mapOptions.Count)
                    {
                        scenario.Info.mapType = mapOptions[v];
                    }
                });
            }
            refreshing = false;
        }

        /// <summary>
        /// 枚举地图类型选项 - 所有Mod下Map目录及内容目录Map下的bin文件名(去重)
        /// </summary>
        /// <returns>地图文件名列表(不含扩展名)</returns>
        private List<string> GetMapTypeOptions()
        {
            List<string> names = new List<string>();
            // 枚举所有Mod下Map目录的bin文件
            string modRoot = Sango.Path.ModRootPath;
            if (!string.IsNullOrEmpty(modRoot) && System.IO.Directory.Exists(modRoot))
            {
                string[] modDirs = System.IO.Directory.GetDirectories(modRoot);
                for (int i = 0; i < modDirs.Length; i++)
                {
                    CollectMapFiles(string.Format("{0}/Map", modDirs[i]), names);
                }
            }
            // 枚举内容目录Map下的bin文件
            string contentRoot = Sango.Path.ContentRootPath;
            if (!string.IsNullOrEmpty(contentRoot))
            {
                CollectMapFiles(string.Format("{0}/Map", contentRoot), names);
            }
            return names;
        }

        /// <summary>
        /// 收集指定目录下所有bin地图文件名并去重追加
        /// </summary>
        /// <param name="mapDir">地图目录</param>
        /// <param name="names">文件名列表</param>
        private void CollectMapFiles(string mapDir, List<string> names)
        {
            if (!System.IO.Directory.Exists(mapDir))
            {
                return;
            }
            string[] files = System.IO.Directory.GetFiles(mapDir, "*.bin");
            for (int i = 0; i < files.Length; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(files[i]);
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        /// <summary>
        /// 刷新武将分页 - 过滤并刷新武将列表
        /// </summary>
        private void RefreshPersonPage()
        {
            personSourceDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.personSet != null)
            {
                scenario.personSet.ForEach(person =>
                {
                    if (person != null)
                    {
                        personSourceDatas.Add(person);
                    }
                });
            }

            // 应用过滤条件,刷新展示列表
            List<ObjectSortTitle> sortTitles = GetPersonSortTitles();
            ApplyFilter(personSourceDatas, personDatas, sortTitles);
            if (objectList != null)
            {
                objectList.Init(personDatas, sortTitles);
                // 恢复多选选中状态(SetMultiSelect会触发多选回调)
                RestoreMultiSelect(selectedPersons, personDatas);
            }
        }

        /// <summary>
        /// 刷新势力分页 - 过滤并刷新势力列表
        /// </summary>
        private void RefreshForcePage()
        {
            forceSourceDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.forceSet != null)
            {
                scenario.forceSet.ForEach(force =>
                {
                    if (force != null)
                    {
                        forceSourceDatas.Add(force);
                    }
                });
            }

            // 应用过滤条件,刷新展示列表
            List<ObjectSortTitle> sortTitles = GetForceSortTitles();
            ApplyFilter(forceSourceDatas, forceDatas, sortTitles);
            if (objectList != null)
            {
                objectList.Init(forceDatas, sortTitles);
                // 恢复多选选中状态(SetMultiSelect会触发多选回调)
                RestoreMultiSelect(selectedForces, forceDatas);
            }
        }

        /// <summary>
        /// 刷新军团分页 - 过滤并刷新军团列表
        /// </summary>
        private void RefreshCorpsPage()
        {
            corpsSourceDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.corpsSet != null)
            {
                scenario.corpsSet.ForEach(corps =>
                {
                    if (corps != null)
                    {
                        corpsSourceDatas.Add(corps);
                    }
                });
            }

            // 应用过滤条件,刷新展示列表
            List<ObjectSortTitle> sortTitles = GetCorpsSortTitles();
            ApplyFilter(corpsSourceDatas, corpsDatas, sortTitles);
            if (objectList != null)
            {
                objectList.Init(corpsDatas, sortTitles);
                // 恢复多选选中状态(SetMultiSelect会触发多选回调)
                RestoreMultiSelect(selectedCorpsList, corpsDatas);
            }
        }

        /// <summary>
        /// 刷新城池分页 - 过滤并刷新城池列表
        /// </summary>
        private void RefreshCityPage()
        {
            citySourceDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.citySet != null)
            {
                scenario.citySet.ForEach(city =>
                {
                    if (city != null && city.IsCity())
                    {
                        citySourceDatas.Add(city);
                    }
                });
            }

            // 应用过滤条件,刷新展示列表
            List<ObjectSortTitle> sortTitles = GetCitySortTitles();
            ApplyFilter(citySourceDatas, cityDatas, sortTitles);
            if (objectList != null)
            {
                objectList.Init(cityDatas, sortTitles);
                // 恢复多选选中状态(SetMultiSelect会触发多选回调)
                RestoreMultiSelect(selectedCities, cityDatas);
            }
        }
        #endregion

        #region 顶部工具栏事件
        /// <summary>
        /// 新建剧本按钮 - 确认后新建空白剧本
        /// </summary>
        private void OnNewClick()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null)
            {
                return;
            }

            // 打开剧本选择界面,设置返回与确认回调
            UIScenarioSelect select = Window.Instance.Open<UIScenarioSelect>("window_scenario_select");
            if (select == null)
            {
                Log.Warning("剧本选择界面打开失败");
                return;
            }
            select.OnReturnAction = OnLoadSelectReturn;
            select.OnNextAction = OnLoadSelectNext;
            // 隐藏主窗口,避免与全屏选择界面叠加
            Window.Instance.SetVisible("window_scenario_edit", false);
        }

        /// <summary>
        /// 加载剧本按钮 - 打开剧本选择界面(UIScenarioSelect)选择要加载的剧本
        /// </summary>
        private void OnLoadClick()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null)
            {
                return;
            }

            List<ShortScenario> show_scenario_list = ShortScenario.all_scenario_info_list.FindAll(x => x.Info.type == 2);

            // 打开剧本选择界面,设置返回与确认回调
            UIScenarioSelect select = Window.Instance.Open<UIScenarioSelect>("window_scenario_select", show_scenario_list, 2);
            if (select == null)
            {
                Log.Warning("剧本选择界面打开失败");
                return;
            }
            select.OnReturnAction = OnLoadSelectReturn;
            select.OnNextAction = OnLoadSelectNext2;
            // 隐藏主窗口,避免与全屏选择界面叠加
            Window.Instance.SetVisible("window_scenario_edit", false);
        }

        /// <summary>
        /// 剧本选择界面返回 - 恢复剧本编辑器主窗口
        /// </summary>
        private void OnLoadSelectReturn()
        {
            Window.Instance.SetVisible("window_scenario_edit", true);
        }

        /// <summary>
        /// 剧本选择界面确认 - 将选择的剧本(ShortScenario)转换成Scenario并加载到编辑器
        /// </summary>
        /// <param name="scenario">选择的剧本</param>
        private void OnLoadSelectNext(ShortScenario scenario)
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null || scenario == null || string.IsNullOrEmpty(scenario.FilePath))
            {
                Log.Warning("加载剧本失败:选择的剧本数据无效");
                Window.Instance.SetVisible("window_scenario_edit", true);
                return;
            }

            edit.LoadScenario(scenario.FilePath, true);
            selectedPerson = null;
            selectedForce = null;
            selectedCorps = null;
            selectedCity = null;
            selectedPersons.Clear();
            selectedForces.Clear();
            selectedCorpsList.Clear();
            selectedCities.Clear();
            OnScenarioLoaded();
            // 恢复剧本编辑器主窗口(选择界面由UIScenarioSelect自行关闭)
            Window.Instance.SetVisible("window_scenario_edit", true);
        }

        /// <summary>
        /// 剧本选择界面确认 - 将选择的剧本(ShortScenario)转换成Scenario并加载到编辑器
        /// </summary>
        /// <param name="scenario">选择的剧本</param>
        private void OnLoadSelectNext2(ShortScenario scenario)
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null || scenario == null || string.IsNullOrEmpty(scenario.FilePath))
            {
                Log.Warning("加载剧本失败:选择的剧本数据无效");
                Window.Instance.SetVisible("window_scenario_edit", true);
                return;
            }

            edit.LoadScenario(scenario.FilePath);
            selectedPerson = null;
            selectedForce = null;
            selectedCorps = null;
            selectedCity = null;
            selectedPersons.Clear();
            selectedForces.Clear();
            selectedCorpsList.Clear();
            selectedCities.Clear();
            OnScenarioLoaded();
            // 恢复剧本编辑器主窗口(选择界面由UIScenarioSelect自行关闭)
            Window.Instance.SetVisible("window_scenario_edit", true);
        }

        /// <summary>
        /// 保存剧本按钮 - 通过文件对话框选择保存路径
        /// </summary>
        private void OnSaveClick()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null || edit.Scenario == null)
            {
                Log.Warning("当前没有可保存的剧本");
                return;
            }

            edit.SaveScenario();
            RefreshScenarioName();
        }
        #endregion

        #region 剧本信息事件
        /// <summary>
        /// 剧本名称输入结束
        /// </summary>
        /// <param name="text">输入文本</param>
        private void OnInfoNameEndEdit(string text)
        {
            if (refreshing)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null && edit.Scenario != null)
            {
                edit.Scenario.Info.name = text;
                RefreshScenarioName();
            }
        }

        /// <summary>
        /// 剧本描述输入结束
        /// </summary>
        /// <param name="text">输入文本</param>
        private void OnDescriptionEndEdit(string text)
        {
            if (refreshing)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null && edit.Scenario != null)
            {
                edit.Scenario.Info.description = text;
            }
        }

        /// <summary>
        /// 年份按钮点击 - 打开UICalculator输入剧本年份
        /// </summary>
        private void OnYearClick()
        {
            if (refreshing)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null || edit.Scenario == null)
            {
                return;
            }
            int current = edit.Scenario.Info.year;
            Window.Instance.Open("window_calculator", current, 1, 9999,
                (System.Action<int>)((val) =>
                {
                    edit.Scenario.Info.year = val;
                    RefreshInfoPage();
                }),
                null);
        }

        /// <summary>
        /// 月份按钮点击 - 打开UICalculator输入剧本月份
        /// </summary>
        private void OnMonthClick()
        {
            if (refreshing)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null || edit.Scenario == null)
            {
                return;
            }
            int current = edit.Scenario.Info.month;
            Window.Instance.Open("window_calculator", current, 1, 12,
                (System.Action<int>)((val) =>
                {
                    edit.Scenario.Info.month = val;
                    RefreshInfoPage();
                }),
                null);
        }

        /// <summary>
        /// 剧本Id按钮点击 - 打开UICalculator输入剧本Id
        /// </summary>
        private void OnIdClick()
        {
            if (refreshing)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null || edit.Scenario == null)
            {
                return;
            }
            int current = edit.Scenario.Info.id;
            Window.Instance.Open("window_calculator", current, 0, 999999,
                (System.Action<int>)((val) =>
                {
                    edit.Scenario.Info.id = val;
                    RefreshInfoPage();
                }),
                null);
        }

        #endregion

        #region 公共按钮事件
        /// <summary>
        /// 新建按钮(公共) - 按当前分页执行不同的新建逻辑
        /// 武将页:新建武将 势力页:新建势力(自动选取首个无势力武将与无势力城池) 
        /// 军团页:新建军团(优先使用主选中军团所属势力,自动选取该势力的非君主武将担任军团长)
        /// </summary>
        private void OnCreateObjectClick()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null)
            {
                return;
            }
            switch (currentTab)
            {
                case 1: CreatePerson(edit); break;
                case 2: CreateForce(edit); break;
                case 3: CreateCorps(edit); break;
                case 4: Log.Warning("城池暂不支持新建"); break;
                default: break;
            }
        }

        /// <summary>
        /// 新建武将 - 在剧本中创建一个未登场的新武将并选中
        /// </summary>
        /// <param name="edit">剧本编辑系统</param>
        private void CreatePerson(ScenarioEdit edit)
        {
            Person person = edit.CreatePerson();
            if (person != null)
            {
                selectedPersons.Clear();
                selectedPersons.Add(person);
                RefreshCurrentPage();
            }
        }

        /// <summary>
        /// 新建势力 - 自动选取首个无势力武将担任君主,首个无势力城池作为都城
        /// </summary>
        /// <param name="edit">剧本编辑系统</param>
        private void CreateForce(ScenarioEdit edit)
        {
            List<Person> freePersons = edit.GetFreePersons();
            List<City> freeCities = edit.GetFreeCities();
            if (freePersons == null || freePersons.Count == 0)
            {
                Log.Warning("没有可用的无势力武将,无法新建势力");
                return;
            }
            if (freeCities == null || freeCities.Count == 0)
            {
                Log.Warning("没有可用的无势力城池,无法新建势力");
                return;
            }

            Force force = edit.CreateForce(freePersons[0], freeCities[0]);
            if (force != null)
            {
                selectedForces.Clear();
                selectedForces.Add(force);
                RefreshCurrentPage();
            }
        }

        /// <summary>
        /// 新建军团 - 优先使用主选中军团所属势力,自动选取该势力的非君主武将担任军团长
        /// </summary>
        /// <param name="edit">剧本编辑系统</param>
        private void CreateCorps(ScenarioEdit edit)
        {
            Scenario scenario = edit.Scenario;
            if (scenario == null || scenario.forceSet == null)
            {
                return;
            }

            // 确定所属势力: 优先取主选中军团的所属势力,否则取第一个势力
            Force force = selectedCorps != null ? selectedCorps.mBelongForce : null;
            if (force == null)
            {
                scenario.forceSet.ForEach(f =>
                {
                    if (force == null && f != null)
                    {
                        force = f;
                    }
                });
            }
            if (force == null)
            {
                Log.Warning("当前没有可用的势力,无法新建军团");
                return;
            }

            // 自动选取该势力的非君主武将担任军团长(君主须留在第一军团)
            Person commander = null;
            if (scenario.personSet != null)
            {
                scenario.personSet.ForEach(person =>
                {
                    if (commander == null && person != null && person.mBelongForce == force && force.mGovernor != person)
                    {
                        commander = person;
                    }
                });
            }
            if (commander == null)
            {
                Log.Warning("势力 " + force.Name + " 没有可担任军团长的武将");
                return;
            }

            Corps corps = edit.CreateCorps(force, commander);
            if (corps != null)
            {
                selectedCorpsList.Clear();
                selectedCorpsList.Add(corps);
                RefreshCurrentPage();
            }
        }

        /// <summary>
        /// 删除按钮(公共) - 按当前分页删除选中的对象(支持多选,带确认)
        /// 武将页:删除武将(跳过君主) 势力页:删除势力 军团页:删除军团(跳过主军团)
        /// </summary>
        private void OnDeleteObjectClick()
        {
            switch (currentTab)
            {
                case 1: DeleteSelectedPersons(); break;
                case 2: DeleteSelectedForces(); break;
                case 3: DeleteSelectedCorps(); break;
                case 4: Log.Warning("城池暂不支持删除"); break;
                default: break;
            }
        }

        /// <summary>
        /// 删除选中的武将(多选) - 君主不可删除,需先在势力页删除其势力
        /// </summary>
        private void DeleteSelectedPersons()
        {
            if (selectedPersons.Count == 0)
            {
                Log.Warning("请先选择要删除的武将");
                return;
            }

            // 拷贝多选列表,过滤掉君主
            List<Person> targets = new List<Person>();
            int skipCount = 0;
            for (int i = 0; i < selectedPersons.Count; i++)
            {
                Person person = selectedPersons[i];
                if (person == null)
                {
                    continue;
                }
                if (IsPersonGovernor(person))
                {
                    skipCount++;
                    continue;
                }
                targets.Add(person);
            }
            if (targets.Count == 0)
            {
                Log.Warning("选中的武将均为君主,君主不可删除,需先在势力页删除其势力");
                return;
            }

            string tip = targets.Count == 1
                ? "确定要删除武将 [" + targets[0].Name + "] 吗?\n将解除其全部从属关系并从剧本中移除。"
                : "确定要删除选中的 " + targets.Count + " 名武将吗?\n将解除其全部从属关系并从剧本中移除。";
            if (skipCount > 0)
            {
                tip += "\n(已跳过 " + skipCount + " 名君主)";
            }
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, tip,
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        for (int i = 0; i < targets.Count; i++)
                        {
                            edit.DeletePerson(targets[i]);
                        }
                        selectedPersons.Clear();
                        RefreshCurrentPage();
                    }
                },
                (System.Action)null);
        }

        /// <summary>
        /// 删除选中的势力(多选) - 同时删除其所有军团,并使所有所属都市和武将去势力化
        /// </summary>
        private void DeleteSelectedForces()
        {
            if (selectedForces.Count == 0)
            {
                Log.Warning("请先选择要删除的势力");
                return;
            }

            // 拷贝多选列表
            List<Force> targets = new List<Force>();
            for (int i = 0; i < selectedForces.Count; i++)
            {
                if (selectedForces[i] != null)
                {
                    targets.Add(selectedForces[i]);
                }
            }
            if (targets.Count == 0)
            {
                return;
            }

            string tip = targets.Count == 1
                ? "确定要删除势力 [" + targets[0].Name + "] 吗?\n将同时删除其所有军团,并使所有所属都市和武将去势力化。"
                : "确定要删除选中的 " + targets.Count + " 个势力吗?\n将同时删除其所有军团,并使所有所属都市和武将去势力化。";
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, tip,
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        for (int i = 0; i < targets.Count; i++)
                        {
                            edit.DeleteForce(targets[i]);
                        }
                        selectedForces.Clear();
                        RefreshCurrentPage();
                    }
                },
                (System.Action)null);
        }

        /// <summary>
        /// 删除选中的军团(多选) - 主军团不可删除,被删军团的武将与城池转入主军团
        /// </summary>
        private void DeleteSelectedCorps()
        {
            if (selectedCorpsList.Count == 0)
            {
                Log.Warning("请先选择要删除的军团");
                return;
            }

            // 拷贝多选列表,过滤掉主军团
            List<Corps> targets = new List<Corps>();
            int skipCount = 0;
            for (int i = 0; i < selectedCorpsList.Count; i++)
            {
                Corps corps = selectedCorpsList[i];
                if (corps == null)
                {
                    continue;
                }
                bool isMainCorps = corps.IsCaptainCorps
                    || (corps.mBelongForce != null && corps.mBelongForce.CapitalCorps == corps);
                if (isMainCorps)
                {
                    skipCount++;
                    continue;
                }
                targets.Add(corps);
            }
            if (targets.Count == 0)
            {
                Log.Warning("选中的军团均为主军团,主军团不可删除");
                return;
            }

            string tip = targets.Count == 1
                ? "确定要删除军团 [" + targets[0].Name + "] 吗?\n其所属武将与城池将转入主军团。"
                : "确定要删除选中的 " + targets.Count + " 个军团吗?\n其所属武将与城池将转入主军团。";
            if (skipCount > 0)
            {
                tip += "\n(已跳过 " + skipCount + " 个主军团)";
            }
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, tip,
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        for (int i = 0; i < targets.Count; i++)
                        {
                            edit.DeleteCorps(targets[i]);
                        }
                        selectedCorpsList.Clear();
                        RefreshCurrentPage();
                    }
                },
                (System.Action)null);
        }

        /// <summary>
        /// 导入按钮(公共) - 按当前分页执行不同的导入逻辑
        /// 城池页:从外部文件导入城池基础数据
        /// </summary>
        private void OnImportObjectClick()
        {
            switch (currentTab)
            {
                case 4: ImportCityData(); break;
                default: Log.Warning("当前分页暂不支持导入"); break;
            }
        }

        /// <summary>
        /// 导入城池数据 - 从外部文件导入城池基础数据
        /// </summary>
        private void ImportCityData()
        {
            string[] paths = WindowDialog.OpenFileDialog("导入城池数据", "城池数据文件(*.json)|*.json", false);
            if (paths == null || paths.Length == 0)
            {
                return;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                edit.ImportCityData(paths[0]);
                RefreshCurrentPage();
            }
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 获取武将列表展示列 - 展示姓名、势力、所属城市、身份、登场状态
        /// </summary>
        /// <returns>排序标题列表</returns>
        private List<ObjectSortTitle> GetPersonSortTitles()
        {
            return new List<ObjectSortTitle>
            {
                PersonSortFunction.SortById,
                PersonSortFunction.SortByName,

                PersonSortFunction.SortByBelongForce,
                PersonSortFunction.SortByBelongCorps,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByState,
                PersonSortFunction.SortByBaseCommand,
                PersonSortFunction.SortByCommandChangeType,
                PersonSortFunction.SortByBaseStrength,
                PersonSortFunction.SortByStrengthChangeType,
                PersonSortFunction.SortByBaseIntelligence,
                PersonSortFunction.SortByIntelligenceChangeType,
                PersonSortFunction.SortByBasePolitics,
                PersonSortFunction.SortByPoliticsChangeType,
                PersonSortFunction.SortByBaseGlamour,
                PersonSortFunction.SortByGlamourChangeType,
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByWaterLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByFeatureList,
                PersonSortFunction.SortByFamilyName,
                PersonSortFunction.SortByGiveName,
                PersonSortFunction.SortByNickName,
                PersonSortFunction.SortByHeadIconID,
                PersonSortFunction.SortByImage,
                PersonSortFunction.SortByImageOld,
                PersonSortFunction.SortByYearAvailable,
                PersonSortFunction.SortByYearBorn,
                PersonSortFunction.SortByYearDead,
                PersonSortFunction.SortBySex,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.SortByMerit,
                PersonSortFunction.SortByOfficial,
                PersonSortFunction.SortByCompatibility,
                PersonSortFunction.SortByPersonality,
                PersonSortFunction.SortByArgumentation,
                PersonSortFunction.SortByBirthplace,
                PersonSortFunction.SortByFather,
                PersonSortFunction.SortByMother,
                PersonSortFunction.SortByBrother,
                PersonSortFunction.SortBySpouse,
                PersonSortFunction.SortByLikePerson,
                PersonSortFunction.SortBymHatePerson,
                PersonSortFunction.SortByIdeal,
                PersonSortFunction.SortByTalent,
                PersonSortFunction.SortByTone,
                PersonSortFunction.SortByVoice,
                PersonSortFunction.SortByItem,
                PersonSortFunction.SortByEquippedWeapon,
                PersonSortFunction.SortByEquippedArmor,
                PersonSortFunction.SortByEquippedHorse,
            };
        }

        /// <summary>
        /// 获取势力列表展示列 - 展示势力名、主公、城市数量
        /// </summary>
        /// <returns>排序标题列表</returns>
        private List<ObjectSortTitle> GetForceSortTitles()
        {
            return new List<ObjectSortTitle>
            {
                ForceSortFunction.SortById,
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
                ForceSortFunction.SortByCapitalCity,
                new ForceSortFunction.SortTitle
                {
                    name = "城市",
                    width = 2.50f,
                    valueStrGetCall = x => CountForceCity(x).ToString(),
                    valueSortFunc = (a, b) => CountForceCity(a).CompareTo(CountForceCity(b)),
                    valueObjGet = x => CountForceCity(x),
                    valueObjSet = null,
                },
                ForceSortFunction.SortByFlag,
                ForceSortFunction.SortByTitle,
                ForceSortFunction.SortByAlliance,
                ForceSortFunction.SortBInitTechniques,
                ForceSortFunction.SortByTechniques,
                ForceSortFunction.SortByTechniquePoint,
                ForceSortFunction.SortByHegemonyPoint,
                ForceSortFunction.SortByPolicyType,
                ForceSortFunction.SortByStroe,
            };
        }

        /// <summary>
        /// 获取军团列表展示列 - 展示军团编号、所属势力、军团长、城市数、武将数
        /// </summary>
        /// <returns>排序标题列表</returns>
        private List<ObjectSortTitle> GetCorpsSortTitles()
        {
            return new List<ObjectSortTitle>
            {
                CorpsSortFunction.SortById,
                CorpsSortFunction.SortByBelongForce,
                CorpsSortFunction.SortByNumber,
                CorpsSortFunction.SortByLeader,
                CorpsSortFunction.SortByCityCount,
                CorpsSortFunction.SortByPersonCount,
                CorpsSortFunction.SortByGold,
                CorpsSortFunction.SortByFood,
                CorpsSortFunction.SortByTroop,
                CorpsSortFunction.SortByPersonCount,
            };
        }

        /// <summary>
        /// 获取城池列表展示列 - 展示城池名、势力、军团、士兵、治安
        /// </summary>
        /// <returns>排序标题列表</returns>
        private List<ObjectSortTitle> GetCitySortTitles()
        {
            return new List<ObjectSortTitle>
            {
                CitySortFunction.SortById,
                CitySortFunction.SortByName,
                CitySortFunction.SortByBelongForce,
                CitySortFunction.SortByBelongCorps,
                CitySortFunction.SortByBelongCity,
                CitySortFunction.SortByLeader,
                CitySortFunction.SortByGold,
                CitySortFunction.SortByBaseGoldLimit,
                CitySortFunction.SortByTroops,
                CitySortFunction.SortByBaseTroopsLimit,
                CitySortFunction.SortByFood,
                CitySortFunction.SortByBaseFoodLimit,
                CitySortFunction.SortByLevel,
                CitySortFunction.SortBySecurity,
                CitySortFunction.SortByDurability,
                CitySortFunction.SortByBaseDurabilityLimit,
                CitySortFunction.SortByMorale,
                CitySortFunction.SortByTotalGainGold,
                CitySortFunction.SortByTotalGainFood,
                CitySortFunction.SortByItemStroe,
                CitySortFunction.SortByBaseStoreLimit,
                CitySortFunction.SortByPopulation,
                CitySortFunction.SortByTroopPopulation,
                CitySortFunction.SortByProvince,
                CitySortFunction.SortByPopularSupport,
            };
        }

        /// <summary>
        /// 统计势力当前所拥有的城市数量
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <returns>城市数量</returns>
        private int CountForceCity(Force force)
        {
            if (force == null)
            {
                return 0;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null || scenario.citySet == null)
            {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city != null && city.mBelongForce == force)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 判断武将是否为某势力的君主
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <returns>是否为君主</returns>
        private bool IsPersonGovernor(Person person)
        {
            if (person == null)
            {
                return false;
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario == null || scenario.forceSet == null)
            {
                return false;
            }
            bool isGovernor = false;
            scenario.forceSet.ForEach(force =>
            {
                if (force != null && force.mGovernor == person)
                {
                    isGovernor = true;
                }
            });
            return isGovernor;
        }
        #endregion


        public void OnBack()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                edit.Done();
            }
        }
    }
}
