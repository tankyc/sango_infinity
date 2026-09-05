using Sango.Core;
using Sango.Core.Player;
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

        #region 武将页组件
        /// <summary>
        /// 武将名称标签
        /// </summary>
        public Text personNameText;

        /// <summary>
        /// 武将登场状态标签
        /// </summary>
        public Text personStateText;

        /// <summary>
        /// 武将所属势力标签
        /// </summary>
        public Text personForceText;

        /// <summary>
        /// 武将所在城市标签
        /// </summary>
        public Text personCityText;

        /// <summary>
        /// 登场城市选择下拉框
        /// </summary>
        public Dropdown appearCityDropdown;

        /// <summary>
        /// 武将登场按钮
        /// </summary>
        public Button appearButton;

        /// <summary>
        /// 通过PersonEdit编辑武将按钮
        /// </summary>
        public Button editPersonButton;

        /// <summary>
        /// 新建武将按钮
        /// </summary>
        public Button newPersonButton;

        /// <summary>
        /// 删除武将按钮
        /// </summary>
        public Button deletePersonButton;
        #endregion

        #region 势力页组件
        /// <summary>
        /// 势力详情标签
        /// </summary>
        public Text forceDetailText;

        /// <summary>
        /// 新建势力君主下拉框(候选为无势力武将)
        /// </summary>
        public Dropdown newForceGovernorDropdown;

        /// <summary>
        /// 新建势力都城下拉框(候选为无势力城市)
        /// </summary>
        public Dropdown newForceCapitalDropdown;

        /// <summary>
        /// 新建势力按钮
        /// </summary>
        public Button newForceButton;

        /// <summary>
        /// 删除势力按钮
        /// </summary>
        public Button deleteForceButton;

        /// <summary>
        /// 通过ForceEdit编辑势力按钮
        /// </summary>
        public Button editForceButton;
        #endregion

        #region 军团页组件
        /// <summary>
        /// 军团详情标签
        /// </summary>
        public Text corpsDetailText;

        /// <summary>
        /// 新建军团所属势力下拉框(候选为全部势力)
        /// </summary>
        public Dropdown newCorpsForceDropdown;

        /// <summary>
        /// 新建军团军团长下拉框(候选为所选势力的非君主武将)
        /// </summary>
        public Dropdown newCorpsCommanderDropdown;

        /// <summary>
        /// 新建军团按钮
        /// </summary>
        public Button newCorpsButton;

        /// <summary>
        /// 删除军团按钮
        /// </summary>
        public Button deleteCorpsButton;

        /// <summary>
        /// 通过CorpsEdit编辑军团按钮
        /// </summary>
        public Button editCorpsButton;
        #endregion

        #region 城池页组件
        /// <summary>
        /// 城池详情标签
        /// </summary>
        public Text cityDetailText;

        /// <summary>
        /// 导入城池数据按钮
        /// </summary>
        public Button importCityButton;

        /// <summary>
        /// 通过CityEdit编辑城池按钮
        /// </summary>
        public Button editCityButton;
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
        /// 武将列表数据
        /// </summary>
        private List<SangoObject> personDatas = new List<SangoObject>();

        /// <summary>
        /// 势力列表数据
        /// </summary>
        private List<SangoObject> forceDatas = new List<SangoObject>();

        /// <summary>
        /// 军团列表数据
        /// </summary>
        private List<SangoObject> corpsDatas = new List<SangoObject>();

        /// <summary>
        /// 城池列表数据
        /// </summary>
        private List<SangoObject> cityDatas = new List<SangoObject>();

        /// <summary>
        /// 登场城市候选列表
        /// </summary>
        private List<City> appearCityCandidates = new List<City>();

        /// <summary>
        /// 新建势力君主候选列表(无势力武将)
        /// </summary>
        private List<Person> freeGovernorCandidates = new List<Person>();

        /// <summary>
        /// 新建势力都城候选列表(无势力城市)
        /// </summary>
        private List<City> freeCapitalCandidates = new List<City>();

        /// <summary>
        /// 新建军团所属势力候选列表(全部势力)
        /// </summary>
        private List<Force> newCorpsForceCandidates = new List<Force>();

        /// <summary>
        /// 新建军团军团长候选列表(所选势力的非君主武将)
        /// </summary>
        private List<Person> newCorpsCommanderCandidates = new List<Person>();

        /// <summary>
        /// 当前选中的武将
        /// </summary>
        private Person selectedPerson;

        /// <summary>
        /// 当前选中的势力
        /// </summary>
        private Force selectedForce;

        /// <summary>
        /// 当前选中的军团
        /// </summary>
        private Corps selectedCorps;

        /// <summary>
        /// 当前选中的城池
        /// </summary>
        private City selectedCity;

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
            personDatas.Clear();
            forceDatas.Clear();
            corpsDatas.Clear();
            cityDatas.Clear();
            appearCityCandidates.Clear();
            freeGovernorCandidates.Clear();
            freeCapitalCandidates.Clear();
            newCorpsForceCandidates.Clear();
            newCorpsCommanderCandidates.Clear();
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

            if (appearButton != null) appearButton.onClick.AddListener(OnAppearClick);
            if (editPersonButton != null) editPersonButton.onClick.AddListener(OnEditPersonClick);
            if (newPersonButton != null) newPersonButton.onClick.AddListener(OnNewPersonClick);
            if (deletePersonButton != null) deletePersonButton.onClick.AddListener(OnDeletePersonClick);

            if (newForceButton != null) newForceButton.onClick.AddListener(OnNewForceClick);
            if (deleteForceButton != null) deleteForceButton.onClick.AddListener(OnDeleteForceClick);
            if (editForceButton != null) editForceButton.onClick.AddListener(OnEditForceClick);

            if (newCorpsButton != null) newCorpsButton.onClick.AddListener(OnNewCorpsClick);
            if (deleteCorpsButton != null) deleteCorpsButton.onClick.AddListener(OnDeleteCorpsClick);
            if (editCorpsButton != null) editCorpsButton.onClick.AddListener(OnEditCorpsClick);
            // 新建军团所属势力切换时联动刷新军团长候选
            if (newCorpsForceDropdown != null)
            {
                newCorpsForceDropdown.onValueChanged.AddListener((v) =>
                {
                    if (!refreshing)
                    {
                        RefreshCorpsCommanderDropdown();
                    }
                });
            }

            if (importCityButton != null) importCityButton.onClick.AddListener(OnImportCityClick);
            if (editCityButton != null) editCityButton.onClick.AddListener(OnEditCityClick);

            // 绑定共享列表的选中回调 - 根据当前分页分发到对应的处理
            if (objectList != null) objectList.OnSelectCall = OnObjectListSelect;
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

            if (appearButton != null) appearButton.onClick.RemoveListener(OnAppearClick);
            if (editPersonButton != null) editPersonButton.onClick.RemoveListener(OnEditPersonClick);
            if (newPersonButton != null) newPersonButton.onClick.RemoveListener(OnNewPersonClick);
            if (deletePersonButton != null) deletePersonButton.onClick.RemoveListener(OnDeletePersonClick);

            if (newForceButton != null) newForceButton.onClick.RemoveListener(OnNewForceClick);
            if (deleteForceButton != null) deleteForceButton.onClick.RemoveListener(OnDeleteForceClick);
            if (editForceButton != null) editForceButton.onClick.RemoveListener(OnEditForceClick);

            if (newCorpsButton != null) newCorpsButton.onClick.RemoveListener(OnNewCorpsClick);
            if (deleteCorpsButton != null) deleteCorpsButton.onClick.RemoveListener(OnDeleteCorpsClick);
            if (editCorpsButton != null) editCorpsButton.onClick.RemoveListener(OnEditCorpsClick);
            if (newCorpsForceDropdown != null) newCorpsForceDropdown.onValueChanged.RemoveAllListeners();

            if (importCityButton != null) importCityButton.onClick.RemoveListener(OnImportCityClick);
            if (editCityButton != null) editCityButton.onClick.RemoveListener(OnEditCityClick);

            // 清理共享列表的选中回调
            if (objectList != null) objectList.OnSelectCall = null;
        }

        /// <summary>
        /// 武将列表选中回调
        /// </summary>
        /// <param name="index">选中索引</param>
        private void OnSelectPerson(int index)
        {
            if (personDatas == null || index < 0 || index >= personDatas.Count)
            {
                selectedPerson = null;
                return;
            }
            selectedPerson = personDatas[index] as Person;
            RefreshPersonDetail();
        }

        /// <summary>
        /// 势力列表选中回调
        /// </summary>
        /// <param name="index">选中索引</param>
        private void OnSelectForce(int index)
        {
            if (forceDatas == null || index < 0 || index >= forceDatas.Count)
            {
                selectedForce = null;
                return;
            }
            selectedForce = forceDatas[index] as Force;
            RefreshForceDetail();
        }

        /// <summary>
        /// 军团列表选中回调
        /// </summary>
        /// <param name="index">选中索引</param>
        private void OnSelectCorps(int index)
        {
            if (corpsDatas == null || index < 0 || index >= corpsDatas.Count)
            {
                selectedCorps = null;
                return;
            }
            selectedCorps = corpsDatas[index] as Corps;
            RefreshCorpsDetail();
        }

        /// <summary>
        /// 城池列表选中回调
        /// </summary>
        /// <param name="index">选中索引</param>
        private void OnSelectCity(int index)
        {
            if (cityDatas == null || index < 0 || index >= cityDatas.Count)
            {
                selectedCity = null;
                return;
            }
            selectedCity = cityDatas[index] as City;
            RefreshCityDetail();
        }

        /// <summary>
        /// 共享列表选中回调 - 根据当前分页分发到对应的处理
        /// </summary>
        /// <param name="index">选中索引</param>
        private void OnObjectListSelect(int index)
        {
            switch (currentTab)
            {
                case 1: OnSelectPerson(index); break;
                case 2: OnSelectForce(index); break;
                case 3: OnSelectCorps(index); break;
                case 4: OnSelectCity(index); break;
                default: break;
            }
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

            // 切换分页后通过共享对象列表刷新当前分页的数据
            RefreshCurrentPage();
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
        /// 刷新武将分页 - 刷新武将列表、登场城市下拉与详情
        /// </summary>
        private void RefreshPersonPage()
        {
            personDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.personSet != null)
            {
                scenario.personSet.ForEach(person =>
                {
                    if (person != null)
                    {
                        personDatas.Add(person);
                    }
                });
            }
            if (objectList != null) objectList.Init(personDatas, GetPersonSortTitles(), true);
            if (selectedPerson != null)
            {
                if (objectList != null) objectList.SelectIndex(personDatas.IndexOf(selectedPerson));
            }
            else if (personDatas.Count > 0)
            {
                if (objectList != null) objectList.SelectIndex(0);
            }

            RefreshAppearCityDropdown();
            RefreshPersonDetail();
        }

        /// <summary>
        /// 刷新登场城市下拉框 - 候选为全部城池
        /// </summary>
        private void RefreshAppearCityDropdown()
        {
            if (appearCityDropdown == null)
            {
                return;
            }
            refreshing = true;
            appearCityCandidates.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.citySet != null)
            {
                scenario.citySet.ForEach(city =>
                {
                    if (city != null && city.IsCity())
                    {
                        appearCityCandidates.Add(city);
                    }
                });
            }

            appearCityDropdown.ClearOptions();
            if (appearCityCandidates.Count == 0)
            {
                appearCityDropdown.options.Add(new Dropdown.OptionData("无可用城市"));
            }
            else
            {
                for (int i = 0; i < appearCityCandidates.Count; i++)
                {
                    appearCityDropdown.options.Add(new Dropdown.OptionData(appearCityCandidates[i].Name));
                }
            }
            appearCityDropdown.value = 0;
            appearCityDropdown.RefreshShownValue();
            refreshing = false;
        }

        /// <summary>
        /// 刷新武将详情
        /// </summary>
        private void RefreshPersonDetail()
        {
            if (selectedPerson == null)
            {
                if (personNameText != null) personNameText.text = "未选择武将";
                if (personStateText != null) personStateText.text = "";
                if (personForceText != null) personForceText.text = "";
                if (personCityText != null) personCityText.text = "";
                if (appearButton != null) appearButton.gameObject.SetActive(false);
                if (editPersonButton != null) editPersonButton.gameObject.SetActive(false);
                if (deletePersonButton != null) deletePersonButton.gameObject.SetActive(false);
                return;
            }

            Window.Instance.Open("window_create_person", selectedPerson);

            if (personNameText != null) personNameText.text = selectedPerson.Name;
            if (personStateText != null) personStateText.text = "状态: " + GetPersonStateText(selectedPerson);
            if (personForceText != null)
            {
                personForceText.text = "所属势力: " + (selectedPerson.mBelongForce != null ? selectedPerson.mBelongForce.Name : "无");
            }
            if (personCityText != null)
            {
                personCityText.text = "所在城市: " + (selectedPerson.mBelongCity != null ? selectedPerson.mBelongCity.Name : "无");
            }

            // 只有未登场的武将才能点击登场按钮
            bool canAppear = !selectedPerson.IsValid;
            if (appearButton != null) appearButton.gameObject.SetActive(canAppear);
            if (editPersonButton != null) editPersonButton.gameObject.SetActive(true);
            // 君主不可删除,需先在势力页删除其势力
            if (deletePersonButton != null)
            {
                deletePersonButton.gameObject.SetActive(true);
                deletePersonButton.interactable = !IsPersonGovernor(selectedPerson);
            }
        }

        /// <summary>
        /// 刷新势力分页 - 刷新势力列表、新建势力下拉与详情
        /// </summary>
        private void RefreshForcePage()
        {
            forceDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.forceSet != null)
            {
                scenario.forceSet.ForEach(force =>
                {
                    if (force != null)
                    {
                        forceDatas.Add(force);
                    }
                });
            }

            if (objectList != null) objectList.Init(forceDatas, GetForceSortTitles(), true);
            if (selectedForce != null)
            {
                if (objectList != null) objectList.SelectIndex(forceDatas.IndexOf(selectedForce));
            }
            else if (forceDatas.Count > 0)
            {
                if (objectList != null) objectList.SelectIndex(0);
            }

            RefreshNewForceDropdowns();
            RefreshForceDetail();
        }

        /// <summary>
        /// 刷新新建势力下拉框 - 君主候选为无势力武将,都城候选为无势力城市
        /// </summary>
        private void RefreshNewForceDropdowns()
        {
            if (newForceGovernorDropdown == null || newForceCapitalDropdown == null)
            {
                return;
            }
            refreshing = true;
            freeGovernorCandidates.Clear();
            freeCapitalCandidates.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                freeGovernorCandidates.AddRange(edit.GetFreePersons());
                freeCapitalCandidates.AddRange(edit.GetFreeCities());
            }

            // 君主下拉
            newForceGovernorDropdown.ClearOptions();
            if (freeGovernorCandidates.Count == 0)
            {
                newForceGovernorDropdown.options.Add(new Dropdown.OptionData("无可用武将"));
            }
            else
            {
                for (int i = 0; i < freeGovernorCandidates.Count; i++)
                {
                    newForceGovernorDropdown.options.Add(new Dropdown.OptionData(freeGovernorCandidates[i].Name));
                }
            }
            newForceGovernorDropdown.value = 0;
            newForceGovernorDropdown.RefreshShownValue();

            // 都城下拉
            newForceCapitalDropdown.ClearOptions();
            if (freeCapitalCandidates.Count == 0)
            {
                newForceCapitalDropdown.options.Add(new Dropdown.OptionData("无可用城市"));
            }
            else
            {
                for (int i = 0; i < freeCapitalCandidates.Count; i++)
                {
                    newForceCapitalDropdown.options.Add(new Dropdown.OptionData(freeCapitalCandidates[i].Name));
                }
            }
            newForceCapitalDropdown.value = 0;
            newForceCapitalDropdown.RefreshShownValue();
            refreshing = false;
        }

        /// <summary>
        /// 刷新势力详情
        /// </summary>
        private void RefreshForceDetail()
        {
            if (forceDetailText == null)
            {
                return;
            }
            if (selectedForce == null)
            {
                forceDetailText.text = "未选择势力";
                if (deleteForceButton != null) deleteForceButton.interactable = false;
                if (editForceButton != null) editForceButton.interactable = false;
                return;
            }

            // 统计势力城市数量
            int cityCount = 0;
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.citySet != null)
            {
                scenario.citySet.ForEach(city =>
                {
                    if (city != null && city.mBelongForce == selectedForce)
                    {
                        cityCount++;
                    }
                });
            }

            string flagName = selectedForce.mFlag != null ? selectedForce.mFlag.Id.ToString() : "无";
            forceDetailText.text = "势力: " + selectedForce.Name
                + "\n君主: " + (selectedForce.mGovernor != null ? selectedForce.mGovernor.Name : "无")
                + "\n旗帜: " + flagName
                + "\n城市数量: " + cityCount;
            if (deleteForceButton != null) deleteForceButton.interactable = true;
            if (editForceButton != null) editForceButton.interactable = true;
        }

        /// <summary>
        /// 刷新军团分页 - 刷新军团列表与详情
        /// </summary>
        private void RefreshCorpsPage()
        {
            corpsDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.corpsSet != null)
            {
                scenario.corpsSet.ForEach(corps =>
                {
                    if (corps != null)
                    {
                        corpsDatas.Add(corps);
                    }
                });
            }

            if (objectList != null) objectList.Init(corpsDatas, GetCorpsSortTitles(), true);
            if (selectedCorps != null)
            {
                if (objectList != null) objectList.SelectIndex(corpsDatas.IndexOf(selectedCorps));
            }
            else if (corpsDatas.Count > 0)
            {
                if (objectList != null) objectList.SelectIndex(0);
            }
            RefreshNewCorpsDropdowns();
            RefreshCorpsDetail();
        }

        /// <summary>
        /// 刷新军团详情
        /// </summary>
        private void RefreshCorpsDetail()
        {
            if (corpsDetailText == null)
            {
                return;
            }
            if (selectedCorps == null)
            {
                corpsDetailText.text = "未选择军团";
                if (editCorpsButton != null) editCorpsButton.interactable = false;
                if (deleteCorpsButton != null) deleteCorpsButton.interactable = false;
                return;
            }

            // 统计军团城市数量
            int cityCount = 0;
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.citySet != null)
            {
                scenario.citySet.ForEach(city =>
                {
                    if (city != null && city.mBelongCorps == selectedCorps)
                    {
                        cityCount++;
                    }
                });
            }

            corpsDetailText.text = "军团: " + selectedCorps.Name
                + "\n所属势力: " + (selectedCorps.mBelongForce != null ? selectedCorps.mBelongForce.Name : "无")
                + "\n军团长: " + (selectedCorps.mComander != null ? selectedCorps.mComander.Name : "无")
                + "\n军团编号: " + selectedCorps.number
                + "\n城市数量: " + cityCount;
            if (editCorpsButton != null) editCorpsButton.interactable = true;
            // 第一主军团不可删除
            bool isMainCorps = selectedCorps.IsCaptainCorps
                || (selectedCorps.mBelongForce != null && selectedCorps.mBelongForce.CapitalCorps == selectedCorps);
            if (deleteCorpsButton != null) deleteCorpsButton.interactable = !isMainCorps;
        }

        /// <summary>
        /// 刷新新建军团下拉框 - 所属势力候选为全部势力,军团长候选随所选势力联动
        /// </summary>
        private void RefreshNewCorpsDropdowns()
        {
            if (newCorpsForceDropdown == null || newCorpsCommanderDropdown == null)
            {
                return;
            }
            refreshing = true;
            newCorpsForceCandidates.Clear();
            newCorpsCommanderCandidates.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.forceSet != null)
            {
                scenario.forceSet.ForEach(force =>
                {
                    if (force != null)
                    {
                        newCorpsForceCandidates.Add(force);
                    }
                });
            }

            // 所属势力下拉
            newCorpsForceDropdown.ClearOptions();
            if (newCorpsForceCandidates.Count == 0)
            {
                newCorpsForceDropdown.options.Add(new Dropdown.OptionData("无可用势力"));
            }
            else
            {
                for (int i = 0; i < newCorpsForceCandidates.Count; i++)
                {
                    Force force = newCorpsForceCandidates[i];
                    string forceName = force.Name;
                    if (string.IsNullOrEmpty(forceName))
                    {
                        forceName = "势力" + force.Id;
                    }
                    newCorpsForceDropdown.options.Add(new Dropdown.OptionData(forceName));
                }
            }
            newCorpsForceDropdown.value = 0;
            newCorpsForceDropdown.RefreshShownValue();
            refreshing = false;

            // 军团长下拉(联动刷新)
            RefreshCorpsCommanderDropdown();
        }

        /// <summary>
        /// 刷新新建军团军团长下拉框 - 候选为所选势力的非君主武将
        /// </summary>
        private void RefreshCorpsCommanderDropdown()
        {
            if (refreshing)
            {
                return;
            }
            if (newCorpsCommanderDropdown == null)
            {
                return;
            }
            refreshing = true;
            newCorpsCommanderCandidates.Clear();
            Force force = null;
            int forceIndex = newCorpsForceDropdown != null ? newCorpsForceDropdown.value : 0;
            if (newCorpsForceCandidates != null && forceIndex >= 0 && forceIndex < newCorpsForceCandidates.Count)
            {
                force = newCorpsForceCandidates[forceIndex];
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (force != null && scenario != null && scenario.personSet != null)
            {
                scenario.personSet.ForEach(person =>
                {
                    // 军团长不能是该势力君主(君主须留在第一军团)
                    if (person != null && person.mBelongForce == force && force.mGovernor != person)
                    {
                        newCorpsCommanderCandidates.Add(person);
                    }
                });
            }

            newCorpsCommanderDropdown.ClearOptions();
            if (newCorpsCommanderCandidates.Count == 0)
            {
                newCorpsCommanderDropdown.options.Add(new Dropdown.OptionData("无可用武将"));
            }
            else
            {
                for (int i = 0; i < newCorpsCommanderCandidates.Count; i++)
                {
                    Person person = newCorpsCommanderCandidates[i];
                    string personName = person.Name;
                    if (string.IsNullOrEmpty(personName))
                    {
                        personName = "武将" + person.Id;
                    }
                    newCorpsCommanderDropdown.options.Add(new Dropdown.OptionData(personName));
                }
            }
            newCorpsCommanderDropdown.value = 0;
            newCorpsCommanderDropdown.RefreshShownValue();
            refreshing = false;
        }

        /// <summary>
        /// 刷新城池分页 - 刷新城池列表与详情
        /// </summary>
        private void RefreshCityPage()
        {
            cityDatas.Clear();
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            Scenario scenario = edit != null ? edit.Scenario : null;
            if (scenario != null && scenario.citySet != null)
            {
                scenario.citySet.ForEach(city =>
                {
                    if (city != null && city.IsCity())
                    {
                        cityDatas.Add(city);
                    }
                });
            }

            if (objectList != null) objectList.Init(cityDatas, GetCitySortTitles(), true);
            if (selectedCity != null)
            {
                if (objectList != null) objectList.SelectIndex(cityDatas.IndexOf(selectedCity));
            }
            else if (cityDatas.Count > 0)
            {
                if (objectList != null) objectList.SelectIndex(0);
            }
            RefreshCityDetail();
        }

        /// <summary>
        /// 刷新城池详情
        /// </summary>
        private void RefreshCityDetail()
        {
            if (cityDetailText == null)
            {
                return;
            }
            if (selectedCity == null)
            {
                cityDetailText.text = "未选择城池";
                if (editCityButton != null) editCityButton.interactable = false;
                return;
            }

            cityDetailText.text = "城池: " + selectedCity.Name
                + "\n所属势力: " + (selectedCity.mBelongForce != null ? selectedCity.mBelongForce.Name : "无")
                + "\n所属军团: " + (selectedCity.mBelongCorps != null ? selectedCity.mBelongCorps.Name : "无");
            if (editCityButton != null) editCityButton.interactable = true;
        }
        #endregion

        #region 顶部工具栏事件
        /// <summary>
        /// 新建剧本按钮 - 确认后新建空白剧本
        /// </summary>
        private void OnNewClick()
        {
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                "确定要新建空白剧本吗?\n当前未保存的修改将丢失。",
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        edit.NewScenario();
                        selectedPerson = null;
                        selectedForce = null;
                        selectedCorps = null;
                        selectedCity = null;
                        RefreshAll();
                    }
                },
                (System.Action)null);
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

            edit.LoadScenario(scenario.FilePath);
            selectedPerson = null;
            selectedForce = null;
            selectedCorps = null;
            selectedCity = null;
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
            string path = WindowDialog.SaveFileDialog("保存剧本", edit.Scenario.Info.name + ".json", "剧本文件(*.json)|*.json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            edit.SaveScenario(path);
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

        #region 武将页事件
        /// <summary>
        /// 武将登场按钮 - 让选中的未登场武将登场
        /// </summary>
        private void OnAppearClick()
        {
            if (selectedPerson == null)
            {
                Log.Warning("请先选择要登场的武将");
                return;
            }
            City city = null;
            int index = appearCityDropdown != null ? appearCityDropdown.value : 0;
            if (appearCityCandidates != null && index >= 0 && index < appearCityCandidates.Count)
            {
                city = appearCityCandidates[index];
            }
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                edit.MakePersonAppear(selectedPerson, city);
                RefreshPersonPage();
            }
        }

        /// <summary>
        /// 编辑武将按钮 - 通过PersonEdit编辑武将属性
        /// </summary>
        private void OnEditPersonClick()
        {
            if (selectedPerson == null)
            {
                Log.Warning("请先选择要编辑的武将");
                return;
            }
            GameSystem.GetSystem<PersonEdit>().Start(selectedPerson);
        }

        /// <summary>
        /// 新建武将按钮 - 在剧本中创建一个未登场的新武将并选中,可再登场或编辑属性
        /// </summary>
        private void OnNewPersonClick()
        {
            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit == null)
            {
                return;
            }
            Person person = edit.CreatePerson();
            if (person != null)
            {
                selectedPerson = person;
                RefreshPersonPage();
            }
        }

        /// <summary>
        /// 删除武将按钮 - 确认后解除该武将的全部从属关系并从剧本中移除
        /// </summary>
        private void OnDeletePersonClick()
        {
            if (selectedPerson == null)
            {
                Log.Warning("请先选择要删除的武将");
                return;
            }
            Person person = selectedPerson;
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                "确定要删除武将 [" + person.Name + "] 吗?\n将解除其全部从属关系并从剧本中移除。",
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        edit.DeletePerson(person);
                        selectedPerson = null;
                        RefreshPersonPage();
                    }
                },
                (System.Action)null);
        }
        #endregion

        #region 势力页事件
        /// <summary>
        /// 新建势力按钮 - 使用君主与都城下拉选中项创建势力
        /// </summary>
        private void OnNewForceClick()
        {
            Person governor = null;
            int governorIndex = newForceGovernorDropdown != null ? newForceGovernorDropdown.value : 0;
            if (freeGovernorCandidates != null && governorIndex >= 0 && governorIndex < freeGovernorCandidates.Count)
            {
                governor = freeGovernorCandidates[governorIndex];
            }
            City capital = null;
            int capitalIndex = newForceCapitalDropdown != null ? newForceCapitalDropdown.value : 0;
            if (freeCapitalCandidates != null && capitalIndex >= 0 && capitalIndex < freeCapitalCandidates.Count)
            {
                capital = freeCapitalCandidates[capitalIndex];
            }

            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                Force force = edit.CreateForce(governor, capital);
                if (force != null)
                {
                    selectedForce = force;
                    RefreshCurrentPage();
                }
            }
        }

        /// <summary>
        /// 删除势力按钮 - 确认后删除势力及其军团,去势力化相关都市与武将
        /// </summary>
        private void OnDeleteForceClick()
        {
            if (selectedForce == null)
            {
                Log.Warning("请先选择要删除的势力");
                return;
            }
            Force force = selectedForce;
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                "确定要删除势力 [" + force.Name + "] 吗?\n将同时删除其所有军团,并使所有所属都市和武将去势力化。",
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        edit.DeleteForce(force);
                        selectedForce = null;
                        RefreshCurrentPage();
                    }
                },
                (System.Action)null);
        }

        /// <summary>
        /// 编辑势力按钮 - 通过ForceEdit编辑势力属性
        /// </summary>
        private void OnEditForceClick()
        {
            if (selectedForce == null)
            {
                Log.Warning("请先选择要编辑的势力");
                return;
            }
            GameSystem.GetSystem<ForceEdit>().Start(selectedForce);
        }
        #endregion

        #region 军团页事件
        /// <summary>
        /// 新建军团按钮 - 使用势力与军团长下拉选中项为势力创建分军团
        /// </summary>
        private void OnNewCorpsClick()
        {
            Force force = null;
            int forceIndex = newCorpsForceDropdown != null ? newCorpsForceDropdown.value : 0;
            if (newCorpsForceCandidates != null && forceIndex >= 0 && forceIndex < newCorpsForceCandidates.Count)
            {
                force = newCorpsForceCandidates[forceIndex];
            }
            Person commander = null;
            int commanderIndex = newCorpsCommanderDropdown != null ? newCorpsCommanderDropdown.value : 0;
            if (newCorpsCommanderCandidates != null && commanderIndex >= 0 && commanderIndex < newCorpsCommanderCandidates.Count)
            {
                commander = newCorpsCommanderCandidates[commanderIndex];
            }

            ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
            if (edit != null)
            {
                Corps corps = edit.CreateCorps(force, commander);
                if (corps != null)
                {
                    selectedCorps = corps;
                    RefreshCurrentPage();
                }
            }
        }

        /// <summary>
        /// 删除军团按钮 - 确认后删除选中的分军团,其所属武将与城池转入主军团
        /// </summary>
        private void OnDeleteCorpsClick()
        {
            if (selectedCorps == null)
            {
                Log.Warning("请先选择要删除的军团");
                return;
            }
            Corps corps = selectedCorps;
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                "确定要删除军团 [" + corps.Name + "] 吗?\n其所属武将与城池将转入主军团。",
                () =>
                {
                    ScenarioEdit edit = GameSystem.GetSystem<ScenarioEdit>();
                    if (edit != null)
                    {
                        edit.DeleteCorps(corps);
                        selectedCorps = null;
                        RefreshCurrentPage();
                    }
                },
                (System.Action)null);
        }

        /// <summary>
        /// 编辑军团按钮 - 通过CorpsEdit编辑军团信息
        /// </summary>
        private void OnEditCorpsClick()
        {
            if (selectedCorps == null)
            {
                Log.Warning("请先选择要编辑的军团");
                return;
            }
            GameSystem.GetSystem<CorpsEdit>().Start(selectedCorps);
        }
        #endregion

        #region 城池页事件
        /// <summary>
        /// 导入城池数据按钮 - 从外部文件导入城池基础数据
        /// </summary>
        private void OnImportCityClick()
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

        /// <summary>
        /// 编辑城池按钮 - 通过CityEdit编辑城池信息
        /// </summary>
        private void OnEditCityClick()
        {
            if (selectedCity == null)
            {
                Log.Warning("请先选择要编辑的城池");
                return;
            }
            GameSystem.GetSystem<CityEdit>().Start(selectedCity, cityDatas);
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

        /// <summary>
        /// 获取武将登场状态的中文描述
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <returns>状态描述</returns>
        private string GetPersonStateText(Person person)
        {
            if (person == null)
            {
                return "未知";
            }
            switch ((PersonStateType)person.state)
            {
                case PersonStateType.Governor: return "君主";
                case PersonStateType.Commander: return "都督";
                case PersonStateType.Leader: return "太守";
                case PersonStateType.Normal: return "一般";
                case PersonStateType.Unemployed: return "在野";
                case PersonStateType.Prisoner: return "俘虏";
                case PersonStateType.Invalid: return "未登场";
                case PersonStateType.Dead: return "已死亡";
                case PersonStateType.Invisible: return "未出现";
                default: return "未知";
            }
        }
        #endregion
    }
}
