using Sango.Core;
using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 城市编辑快照 - 保存City可编辑属性的独立副本
    /// 界面操作仅修改快照,确认时才同步回Target
    /// </summary>
    internal struct CityEditSnapshot
    {
        #region 基础信息
        /// <summary>城市名称</summary>
        public string name;
        #endregion

        #region 归属与建筑
        /// <summary>所属势力ID</summary>
        public int belongForce;
        /// <summary>所属势力引用</summary>
        public Force mBelongForce;
        /// <summary>所属军团ID</summary>
        public int belongCorps;
        /// <summary>所属军团引用</summary>
        public Corps mBelongCorps;
        /// <summary>所属城池ID</summary>
        public int belongCity;
        /// <summary>所属城池引用</summary>
        public City mBelongCity;
        /// <summary>建筑类型</summary>
        public BuildingType buildingType;
        /// <summary>当前耐久</summary>
        public int durability;
        /// <summary>最大耐久</summary>
        public int durabilityLimit;
        /// <summary>模型</summary>
        public string model;
        #endregion

        #region 城市资源
        /// <summary>粮食</summary>
        public int food;
        /// <summary>金钱</summary>
        public int gold;
        /// <summary>人口</summary>
        public int population;
        /// <summary>兵役人口</summary>
        public int troopPopulation;
        /// <summary>库存(独立拷贝)</summary>
        public ItemStore itemStore;
        /// <summary>治安</summary>
        public int security;
        /// <summary>士气</summary>
        public int morale;
        /// <summary>当前兵力</summary>
        public int troops;
        /// <summary>可容纳兵力</summary>
        public int troopsLimit;
        /// <summary>仓库大小</summary>
        public int storeLimit;
        /// <summary>金库大小</summary>
        public int goldLimit;
        /// <summary>粮仓大小</summary>
        public int foodLimit;
        /// <summary>基础金钱收入</summary>
        public int baseGainGold;
        /// <summary>基础粮食收入</summary>
        public int baseGainFood;
        #endregion

        #region 州与等级
        /// <summary>所属州</summary>
        public Province province;
        /// <summary>城市等级</summary>
        public CityLevelType cityLevelType;
        #endregion

        /// <summary>
        /// 从City对象创建快照 - 拷贝所有可编辑字段
        /// </summary>
        /// <param name="city">源城市对象</param>
        /// <returns>城市编辑快照</returns>
        public static CityEditSnapshot FromCity(City city)
        {
            CityEditSnapshot snapshot = new CityEditSnapshot();

            // 基础信息
            snapshot.name = city.Name;

            // 归属与建筑
            snapshot.belongForce = city.BelongForce;
            snapshot.mBelongForce = city.mBelongForce;
            snapshot.belongCorps = city.BelongCorps;
            snapshot.mBelongCorps = city.mBelongCorps;
            snapshot.belongCity = city.BelongCity;
            snapshot.mBelongCity = city.mBelongCity;
            snapshot.buildingType = city.BuildingType;
            snapshot.durability = city.durability;
            snapshot.durabilityLimit = city.durabilityLimit;
            snapshot.model = city.model;

            // 城市资源
            snapshot.food = city.food;
            snapshot.gold = city.gold;
            snapshot.population = city.population;
            snapshot.troopPopulation = city.troopPopulation;
            snapshot.itemStore = city.itemStore != null ? city.itemStore.Copy() : new ItemStore();
            snapshot.security = city.security;
            snapshot.morale = city.morale;
            snapshot.troops = city.troops;
            snapshot.troopsLimit = city.troopsLimit;
            snapshot.storeLimit = city.storeLimit;
            snapshot.goldLimit = city.goldLimit;
            snapshot.foodLimit = city.foodLimit;
            snapshot.baseGainGold = city.baseGainGold;
            snapshot.baseGainFood = city.baseGainFood;

            // 州与等级
            snapshot.province = city.province;
            snapshot.cityLevelType = city.CityLevelType;

            return snapshot;
        }

        /// <summary>
        /// 将快照数据同步回City对象 - 仅在确认时调用
        /// </summary>
        /// <param name="city">目标城市对象</param>
        public void ApplyTo(City city)
        {
            if (city == null)
            {
                return;
            }

            // 记录归属旧值,用于同步城内武将
            int oldBelongForce = city.BelongForce;
            int oldBelongCorps = city.BelongCorps;

            // 基础信息
            city.Name = name;

            // 归属与建筑
            city.BelongForce = belongForce;
            city.mBelongForce = mBelongForce;
            city.BelongCorps = belongCorps;
            city.mBelongCorps = mBelongCorps;
            city.BelongCity = belongCity;
            city.mBelongCity = mBelongCity;
            city.BuildingType = buildingType;
            city.durability = durability;
            city.durabilityLimit = durabilityLimit;
            city.model = model;

            // 城市资源
            city.food = food;
            city.gold = gold;
            city.population = population;
            city.troopPopulation = troopPopulation;
            if (itemStore != null)
            {
                city.itemStore = itemStore;
            }
            city.security = security;
            city.morale = morale;
            city.troops = troops;
            city.troopsLimit = troopsLimit;
            city.storeLimit = storeLimit;
            city.goldLimit = goldLimit;
            city.foodLimit = foodLimit;
            city.baseGainGold = baseGainGold;
            city.baseGainFood = baseGainFood;

            // 州与等级
            city.province = province;
            city.CityLevelType = cityLevelType;

            // 同步城内武将的归属势力/军团(仅在归属发生变化时)
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.personSet != null)
            {
                foreach (Person person in cur.personSet)
                {
                    if (person != null && person.mBelongCity == city)
                    {
                        if (oldBelongForce != belongForce)
                        {
                            person.BelongForce = belongForce;
                            person.mBelongForce = mBelongForce;
                            // 归属势力变化时,武将军团跟随城市军团
                            person.BelongCorps = belongCorps;
                            person.mBelongCorps = mBelongCorps;
                        }
                        else if (oldBelongCorps != belongCorps)
                        {
                            person.BelongCorps = belongCorps;
                            person.mBelongCorps = mBelongCorps;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 城市编辑窗口 - 编辑城市的各类属性、归属、资源、库存等
    /// 关联窗口: window_edit_city
    /// 使用快照模式: 界面操作仅修改快照,确认时才同步到Target
    /// </summary>
    public class UICityEdit : UGUIWindow
    {
        #region 基础引用
        /// <summary>
        /// 根节点
        /// </summary>
        public RectTransform root;

        /// <summary>
        /// 当前城市名称标签(不可编辑,仅显示)
        /// </summary>
        public Text cityNameLabel;
        #endregion

        #region 左侧城市列表
        /// <summary>
        /// 城市对象列表 - 展示所有城市,点击切换编辑目标
        /// </summary>
        public UIObjectList objectList;
        #endregion

        #region 基础信息
        /// <summary>
        /// 城市名称输入框
        /// </summary>
        public InputField nameInput;
        #endregion

        #region 归属与建筑
        /// <summary>
        /// 所属势力下拉框
        /// </summary>
        public Dropdown belongForceDropdown;

        /// <summary>
        /// 所属军团下拉框
        /// </summary>
        public Dropdown belongCorpsDropdown;

        /// <summary>
        /// 所属城池下拉框
        /// </summary>
        public Dropdown belongCityDropdown;

        /// <summary>
        /// 建筑类型下拉框
        /// </summary>
        public Dropdown buildingTypeDropdown;

        /// <summary>
        /// 当前耐久输入框
        /// </summary>
        public InputField durabilityInput;

        /// <summary>
        /// 最大耐久输入框
        /// </summary>
        public InputField durabilityLimitInput;

        /// <summary>
        /// 模型输入框
        /// </summary>
        public InputField modelInput;
        #endregion

        #region 城市资源
        /// <summary>
        /// 粮食输入框
        /// </summary>
        public InputField foodInput;

        /// <summary>
        /// 金钱输入框
        /// </summary>
        public InputField goldInput;

        /// <summary>
        /// 人口输入框
        /// </summary>
        public InputField populationInput;

        /// <summary>
        /// 兵役人口输入框
        /// </summary>
        public InputField troopPopulationInput;

        /// <summary>
        /// 治安输入框(0-100)
        /// </summary>
        public InputField securityInput;

        /// <summary>
        /// 士气输入框(0-100)
        /// </summary>
        public InputField moraleInput;

        /// <summary>
        /// 当前兵力输入框
        /// </summary>
        public InputField troopsInput;

        /// <summary>
        /// 可容纳兵力输入框
        /// </summary>
        public InputField troopsLimitInput;

        /// <summary>
        /// 仓库大小输入框
        /// </summary>
        public InputField storeLimitInput;

        /// <summary>
        /// 金库大小输入框
        /// </summary>
        public InputField goldLimitInput;

        /// <summary>
        /// 粮仓大小输入框
        /// </summary>
        public InputField foodLimitInput;

        /// <summary>
        /// 基础金钱收入输入框
        /// </summary>
        public InputField baseGainGoldInput;

        /// <summary>
        /// 基础粮食收入输入框
        /// </summary>
        public InputField baseGainFoodInput;
        #endregion

        #region 州与等级
        /// <summary>
        /// 所属州下拉框
        /// </summary>
        public Dropdown provinceDropdown;

        /// <summary>
        /// 城市等级下拉框
        /// </summary>
        public Dropdown cityLevelTypeDropdown;
        #endregion

        #region 库存
        /// <summary>
        /// 库存内容显示标签
        /// </summary>
        public Text storeLabel;

        /// <summary>
        /// 道具选择下拉框
        /// </summary>
        public Dropdown storeItemDropdown;

        /// <summary>
        /// 道具添加数量输入框
        /// </summary>
        public InputField storeItemCountInput;

        /// <summary>
        /// 添加道具按钮
        /// </summary>
        public Button storeAddButton;
        #endregion

        #region 决定/返回
        /// <summary>
        /// 决定按钮 - 保存修改
        /// </summary>
        public Button confirmButton;

        /// <summary>
        /// 返回按钮 - 取消修改
        /// </summary>
        public Button cancelButton;
        #endregion

        /// <summary>
        /// 目标城市（原始对象，仅在确认时写入）
        /// </summary>
        public City Target { get; private set; }

        /// <summary>
        /// 编辑快照 - 所有UI操作仅修改快照值
        /// </summary>
        private CityEditSnapshot snapshot;

        /// <summary>
        /// 触发刷新标识 - 防止OnValueChanged循环触发
        /// </summary>
        private bool refreshing = false;

        /// <summary>
        /// 全部城市对象列表
        /// </summary>
        private List<SangoObject> allCityDatas;

        #region 下拉候选缓存
        /// <summary>所属势力候选列表</summary>
        private List<SangoObject> belongForceCandidates = new List<SangoObject>();
        /// <summary>所属军团候选列表</summary>
        private List<SangoObject> belongCorpsCandidates = new List<SangoObject>();
        /// <summary>所属城池候选列表</summary>
        private List<SangoObject> belongCityCandidates = new List<SangoObject>();
        /// <summary>建筑类型候选列表</summary>
        private List<SangoObject> buildingTypeCandidates = new List<SangoObject>();
        /// <summary>所属州候选列表</summary>
        private List<SangoObject> provinceCandidates = new List<SangoObject>();
        /// <summary>城市等级候选列表</summary>
        private List<SangoObject> cityLevelTypeCandidates = new List<SangoObject>();
        /// <summary>道具候选列表</summary>
        private List<SangoObject> storeItemCandidates = new List<SangoObject>();
        #endregion

        #region 窗口生命周期
        /// <summary>
        /// 窗口打开 - 接收目标城市对象并创建编辑快照
        /// </summary>
        /// <param name="objects">参数列表 - objects[0] 为 City</param>
        public override void OnOpen(params object[] objects)
        {
            // 候选: 所有城市
            allCityDatas = new List<SangoObject>();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.citySet != null)
            {
                cur.citySet.ForEach(city =>
                {
                    if (city != null && city.IsCity())
                    {
                        allCityDatas.Add(city);
                    }
                });
            }

            if (objects == null || objects.Length == 0 || objects[0] == null)
            {
                Target = allCityDatas.Count > 0 ? allCityDatas[0] as City : null;
            }
            else
            {
                Target = objects[0] as City;
                if (Target == null)
                {
                    Log.Error("UICityEdit.OnOpen 传入的对象不是 City 类型");
                    return;
                }
            }

            if (Target == null)
            {
                Log.Error("UICityEdit.OnOpen 没有可编辑的城市");
                return;
            }

            // 左侧城市列表初始化,默认选中目标城市
            if (objectList != null)
            {
                objectList.Init(allCityDatas, CitySortFunction.SortByName, OnSelectEditCity);
                objectList.SelectDefaultObject(Target);
            }

            // 创建编辑快照 - 从Target拷贝所有可编辑数据
            snapshot = CityEditSnapshot.FromCity(Target);

            BindEvents();
            Refresh();
        }

        /// <summary>
        /// 左侧城市列表选择回调 - 切换编辑目标并重建快照
        /// </summary>
        /// <param name="index">选中城市在列表中的下标</param>
        void OnSelectEditCity(int index)
        {
            if (index < 0 || index >= allCityDatas.Count)
            {
                return;
            }
            Target = allCityDatas[index] as City;
            snapshot = CityEditSnapshot.FromCity(Target);
            Refresh();
        }

        /// <summary>
        /// 窗口关闭 - 清理监听器和引用
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();
            RemoveListeners();
            Target = null;
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定UI事件 - 所有修改操作直接作用于快照
        /// </summary>
        private void BindEvents()
        {
            // 基础信息
            BindSnapshotStringInput(nameInput, () => snapshot.name, (v) => snapshot.name = v);

            // 归属与建筑
            BindObjectDropdownSelection(belongForceDropdown, belongForceCandidates, (obj) =>
            {
                Force force = obj as Force;
                int newForceId = force != null ? force.Id : 0;
                // 归属势力发生变化时,同步军团为该势力的主军团
                if (newForceId != snapshot.belongForce)
                {
                    if (force != null)
                    {
                        Corps mainCorps = GetMainCorps(force);
                        snapshot.belongCorps = mainCorps != null ? mainCorps.Id : 0;
                        snapshot.mBelongCorps = mainCorps;
                    }
                    else
                    {
                        // 没有所属势力时,军团必须置空
                        snapshot.belongCorps = 0;
                        snapshot.mBelongCorps = null;
                    }
                }
                snapshot.belongForce = newForceId;
                snapshot.mBelongForce = force;
                // 刷新军团下拉 - 候选随所属势力过滤
                RefreshBelongCorpsDropdown();
            });
            BindObjectDropdownSelection(belongCorpsDropdown, belongCorpsCandidates, (obj) =>
            {
                Corps corps = obj as Corps;
                snapshot.belongCorps = corps != null ? corps.Id : 0;
                snapshot.mBelongCorps = corps;
            });
            BindObjectDropdownSelection(belongCityDropdown, belongCityCandidates, (obj) =>
            {
                City city = obj as City;
                snapshot.belongCity = city != null ? city.Id : 0;
                snapshot.mBelongCity = city;
            });
            BindObjectDropdownSelection(buildingTypeDropdown, buildingTypeCandidates, (obj) => snapshot.buildingType = obj as BuildingType);
            BindSnapshotInput(durabilityInput, () => snapshot.durability, (v) => snapshot.durability = v);
            BindSnapshotInput(durabilityLimitInput, () => snapshot.durabilityLimit, (v) => snapshot.durabilityLimit = v);
            BindSnapshotStringInput(modelInput, () => snapshot.model, (v) => snapshot.model = v);

            // 城市资源
            BindSnapshotInput(foodInput, () => snapshot.food, (v) => snapshot.food = v);
            BindSnapshotInput(goldInput, () => snapshot.gold, (v) => snapshot.gold = v);
            BindSnapshotInput(populationInput, () => snapshot.population, (v) => snapshot.population = v);
            BindSnapshotInput(troopPopulationInput, () => snapshot.troopPopulation, (v) => snapshot.troopPopulation = v);
            BindSnapshotInput(securityInput, () => snapshot.security, (v) => snapshot.security = v, 0, 100);
            BindSnapshotInput(moraleInput, () => snapshot.morale, (v) => snapshot.morale = v, 0, 100);
            BindSnapshotInput(troopsInput, () => snapshot.troops, (v) => snapshot.troops = v);
            BindSnapshotInput(troopsLimitInput, () => snapshot.troopsLimit, (v) => snapshot.troopsLimit = v);
            BindSnapshotInput(storeLimitInput, () => snapshot.storeLimit, (v) => snapshot.storeLimit = v);
            BindSnapshotInput(goldLimitInput, () => snapshot.goldLimit, (v) => snapshot.goldLimit = v);
            BindSnapshotInput(foodLimitInput, () => snapshot.foodLimit, (v) => snapshot.foodLimit = v);
            BindSnapshotInput(baseGainGoldInput, () => snapshot.baseGainGold, (v) => snapshot.baseGainGold = v);
            BindSnapshotInput(baseGainFoodInput, () => snapshot.baseGainFood, (v) => snapshot.baseGainFood = v);

            // 州与等级
            BindObjectDropdownSelection(provinceDropdown, provinceCandidates, (obj) => snapshot.province = obj as Province);
            BindObjectDropdownSelection(cityLevelTypeDropdown, cityLevelTypeCandidates, (obj) => snapshot.cityLevelType = obj as CityLevelType);

            // 库存
            if (storeAddButton != null) storeAddButton.onClick.AddListener(OnStoreAddClick);

            // 决定 / 返回
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClick);
        }

        /// <summary>
        /// 清理所有按钮监听器
        /// </summary>
        private void RemoveListeners()
        {
            if (storeAddButton != null) storeAddButton.onClick.RemoveListener(OnStoreAddClick);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClick);
        }

        /// <summary>
        /// 绑定InputField结束编辑事件 - 验证后写入快照
        /// </summary>
        /// <param name="input">输入框</param>
        /// <param name="getter">从快照读取值的函数</param>
        /// <param name="setter">向快照写入值的函数</param>
        /// <param name="minValue">取值范围下限</param>
        /// <param name="maxValue">取值范围上限</param>
        private void BindSnapshotInput(InputField input, System.Func<int> getter, System.Action<int> setter, int minValue = int.MinValue, int maxValue = int.MaxValue)
        {
            if (input == null) return;
            input.onEndEdit.AddListener((text) =>
            {
                if (Target == null) return;
                if (int.TryParse(text, out int v))
                {
                    // 限制数值范围
                    v = System.Math.Max(minValue, System.Math.Min(maxValue, v));
                    setter(v);
                }
                // 回显快照中的当前值
                input.text = getter().ToString();
            });
        }

        /// <summary>
        /// 绑定字符串InputField结束编辑事件 - 写入快照
        /// </summary>
        /// <param name="input">输入框</param>
        /// <param name="getter">从快照读取值的函数</param>
        /// <param name="setter">向快照写入值的函数</param>
        private void BindSnapshotStringInput(InputField input, System.Func<string> getter, System.Action<string> setter)
        {
            if (input == null) return;
            input.onEndEdit.AddListener((text) =>
            {
                if (Target == null) return;
                setter(text);
                input.text = getter();
            });
        }

        /// <summary>
        /// 绑定对象引用下拉框选择事件 - 选择时通过回调写入快照
        /// </summary>
        /// <param name="dropdown">下拉框</param>
        /// <param name="candidates">候选对象列表</param>
        /// <param name="setter">选中对象回调(index为0表示无)</param>
        private void BindObjectDropdownSelection(Dropdown dropdown, List<SangoObject> candidates, System.Action<SangoObject> setter)
        {
            if (dropdown == null) return;
            dropdown.onValueChanged.AddListener((index) =>
            {
                if (refreshing) return;
                if (index < 0 || index > candidates.Count) return;
                SangoObject selected = index == 0 ? null : candidates[index - 1];
                setter(selected);
            });
        }
        #endregion

        #region UI刷新 - 从快照读取数据
        /// <summary>
        /// 刷新窗口 - 将快照当前值同步到UI
        /// </summary>
        public override void OnRefresh()
        {
            if (Target == null) return;

            refreshing = true;
            try
            {
                // 名称 - 从Target读取(标题显示)与快照(可编辑)
                if (cityNameLabel != null) cityNameLabel.text = Target.Name;
                if (nameInput != null) nameInput.text = snapshot.name;

                // 归属与建筑 - 从快照读取
                RefreshBelongForceDropdown();
                RefreshBelongCorpsDropdown();
                RefreshBelongCityDropdown();
                RefreshBuildingTypeDropdown();
                if (durabilityInput != null) durabilityInput.text = snapshot.durability.ToString();
                if (durabilityLimitInput != null) durabilityLimitInput.text = snapshot.durabilityLimit.ToString();
                if (modelInput != null) modelInput.text = snapshot.model ?? "";

                // 城市资源 - 从快照读取
                if (foodInput != null) foodInput.text = snapshot.food.ToString();
                if (goldInput != null) goldInput.text = snapshot.gold.ToString();
                if (populationInput != null) populationInput.text = snapshot.population.ToString();
                if (troopPopulationInput != null) troopPopulationInput.text = snapshot.troopPopulation.ToString();
                if (securityInput != null) securityInput.text = snapshot.security.ToString();
                if (moraleInput != null) moraleInput.text = snapshot.morale.ToString();
                if (troopsInput != null) troopsInput.text = snapshot.troops.ToString();
                if (troopsLimitInput != null) troopsLimitInput.text = snapshot.troopsLimit.ToString();
                if (storeLimitInput != null) storeLimitInput.text = snapshot.storeLimit.ToString();
                if (goldLimitInput != null) goldLimitInput.text = snapshot.goldLimit.ToString();
                if (foodLimitInput != null) foodLimitInput.text = snapshot.foodLimit.ToString();
                if (baseGainGoldInput != null) baseGainGoldInput.text = snapshot.baseGainGold.ToString();
                if (baseGainFoodInput != null) baseGainFoodInput.text = snapshot.baseGainFood.ToString();

                // 州与等级
                RefreshProvinceDropdown();
                RefreshCityLevelTypeDropdown();

                // 库存
                RefreshStoreLabel();
                RefreshStoreItemDropdown();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// 刷新所属势力下拉框
        /// </summary>
        private void RefreshBelongForceDropdown()
        {
            belongForceCandidates.Clear();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.forceSet != null)
            {
                cur.forceSet.ForEach(force =>
                {
                    if (force != null)
                    {
                        belongForceCandidates.Add(force);
                    }
                });
            }
            RefreshObjectDropdown(belongForceDropdown, belongForceCandidates, snapshot.mBelongForce);
        }

        /// <summary>
        /// 刷新所属军团下拉框 - 只能选择所属势力下的军团
        /// 没有所属势力时,军团置空且无法选择
        /// </summary>
        private void RefreshBelongCorpsDropdown()
        {
            if (belongCorpsDropdown == null) return;
            belongCorpsCandidates.Clear();
            List<Corps> corpsList = GetForceCorpsList(snapshot.mBelongForce);
            for (int i = 0; i < corpsList.Count; i++)
            {
                belongCorpsCandidates.Add(corpsList[i]);
            }

            // 没有所属势力时,军团必须置空
            if (snapshot.mBelongForce == null)
            {
                snapshot.belongCorps = 0;
                snapshot.mBelongCorps = null;
            }
            // 军团与所属势力不一致时,同样置空
            else if (snapshot.mBelongCorps != null && snapshot.mBelongCorps.mBelongForce != snapshot.mBelongForce)
            {
                snapshot.belongCorps = 0;
                snapshot.mBelongCorps = null;
            }

            RefreshObjectDropdown(belongCorpsDropdown, belongCorpsCandidates, snapshot.mBelongCorps);
            // 只有存在所属势力且势力下有军团时才能选择军团
            belongCorpsDropdown.interactable = snapshot.mBelongForce != null && belongCorpsCandidates.Count > 0;
        }

        /// <summary>
        /// 获取势力下的所有军团列表
        /// </summary>
        /// <param name="force">所属势力,为空时返回全部军团</param>
        /// <returns>军团列表</returns>
        private List<Corps> GetForceCorpsList(Force force)
        {
            List<Corps> corpsList = new List<Corps>();
            Scenario cur = Scenario.Cur;
            if (cur == null || cur.corpsSet == null)
            {
                return corpsList;
            }
            cur.corpsSet.ForEach(corps =>
            {
                if (corps != null && (force == null || corps.mBelongForce == force))
                {
                    corpsList.Add(corps);
                }
            });
            return corpsList;
        }

        /// <summary>
        /// 获取势力的主军团 - 优先君主所在军团,其次第一军团,最后势力下第一个军团
        /// </summary>
        /// <param name="force">所属势力</param>
        /// <returns>主军团,势力不存在或无军团时返回null</returns>
        private Corps GetMainCorps(Force force)
        {
            if (force == null)
            {
                return null;
            }
            // 优先君主所在军团(首都军团)
            if (force.CapitalCorps != null)
            {
                return force.CapitalCorps;
            }
            // 其次第一军团(number == 1)
            List<Corps> corpsList = GetForceCorpsList(force);
            for (int i = 0; i < corpsList.Count; i++)
            {
                if (corpsList[i].IsCaptainCorps)
                {
                    return corpsList[i];
                }
            }
            // 最后势力下第一个军团
            return corpsList.Count > 0 ? corpsList[0] : null;
        }

        /// <summary>
        /// 刷新所属城池下拉框
        /// </summary>
        private void RefreshBelongCityDropdown()
        {
            belongCityCandidates.Clear();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.citySet != null)
            {
                cur.citySet.ForEach(city =>
                {
                    if (city != null)
                    {
                        belongCityCandidates.Add(city);
                    }
                });
            }
            RefreshObjectDropdown(belongCityDropdown, belongCityCandidates, snapshot.mBelongCity);
        }

        /// <summary>
        /// 刷新建筑类型下拉框
        /// </summary>
        private void RefreshBuildingTypeDropdown()
        {
            buildingTypeCandidates.Clear();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData != null && cur.CommonData.BuildingTypes != null)
            {
                foreach (BuildingType type in cur.CommonData.BuildingTypes)
                {
                    if (type != null)
                    {
                        buildingTypeCandidates.Add(type);
                    }
                }
            }
            RefreshObjectDropdown(buildingTypeDropdown, buildingTypeCandidates, snapshot.buildingType);
        }

        /// <summary>
        /// 刷新所属州下拉框
        /// </summary>
        private void RefreshProvinceDropdown()
        {
            provinceCandidates.Clear();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData != null && cur.CommonData.Provinces != null)
            {
                foreach (Province province in cur.CommonData.Provinces)
                {
                    if (province != null)
                    {
                        provinceCandidates.Add(province);
                    }
                }
            }
            RefreshObjectDropdown(provinceDropdown, provinceCandidates, snapshot.province);
        }

        /// <summary>
        /// 刷新城市等级下拉框
        /// </summary>
        private void RefreshCityLevelTypeDropdown()
        {
            cityLevelTypeCandidates.Clear();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData != null && cur.CommonData.CityLevelTypes != null)
            {
                foreach (CityLevelType level in cur.CommonData.CityLevelTypes)
                {
                    if (level != null)
                    {
                        cityLevelTypeCandidates.Add(level);
                    }
                }
            }
            RefreshObjectDropdown(cityLevelTypeDropdown, cityLevelTypeCandidates, snapshot.cityLevelType);
        }

        /// <summary>
        /// 刷新对象引用下拉框 - 重新填充选项并选中当前值
        /// </summary>
        /// <param name="dropdown">下拉框</param>
        /// <param name="candidates">候选对象列表</param>
        /// <param name="currentValue">当前选中值(可为空)</param>
        private void RefreshObjectDropdown(Dropdown dropdown, List<SangoObject> candidates, SangoObject currentValue)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData> { new Dropdown.OptionData("无") };
            for (int i = 0; i < candidates.Count; i++)
            {
                SangoObject obj = candidates[i];
                options.Add(new Dropdown.OptionData($"{obj.Id}.{obj.Name}"));
            }
            dropdown.AddOptions(options);
            int index = 0;
            if (currentValue != null)
            {
                int idx = candidates.IndexOf(currentValue);
                if (idx >= 0)
                {
                    index = idx + 1;
                }
            }
            dropdown.SetValueWithoutNotify(index);
        }

        /// <summary>
        /// 刷新库存显示标签 - 从快照库存读取道具列表
        /// </summary>
        private void RefreshStoreLabel()
        {
            if (storeLabel == null) return;
            if (snapshot.itemStore == null || snapshot.itemStore.Items.Count == 0)
            {
                storeLabel.text = "空";
                return;
            }
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, int> pair in snapshot.itemStore.Items)
            {
                ItemType itemType = GetItemType(pair.Key);
                string itemName = itemType != null ? itemType.Name : $"道具{pair.Key}";
                if (sb.Length > 0)
                {
                    sb.Append("\n");
                }
                sb.Append($"{itemName} x{pair.Value}");
            }
            storeLabel.text = sb.ToString();
        }

        /// <summary>
        /// 刷新道具选择下拉框
        /// </summary>
        private void RefreshStoreItemDropdown()
        {
            if (storeItemDropdown == null) return;
            storeItemCandidates.Clear();
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.CommonData != null && cur.CommonData.ItemTypes != null)
            {
                cur.CommonData.ItemTypes.ForEach(itemType =>
                {
                    if (itemType != null)
                    {
                        storeItemCandidates.Add(itemType);
                    }
                });
            }
            storeItemDropdown.ClearOptions();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();
            for (int i = 0; i < storeItemCandidates.Count; i++)
            {
                SangoObject obj = storeItemCandidates[i];
                options.Add(new Dropdown.OptionData($"{obj.Id}.{obj.Name}"));
            }
            storeItemDropdown.AddOptions(options);
            storeItemDropdown.SetValueWithoutNotify(0);
        }
        #endregion

        #region 库存事件
        /// <summary>
        /// 添加道具按钮点击 - 向快照库存中添加道具
        /// </summary>
        public void OnStoreAddClick()
        {
            if (snapshot.itemStore == null || storeItemDropdown == null)
            {
                return;
            }
            int index = storeItemDropdown.value;
            if (index < 0 || index >= storeItemCandidates.Count)
            {
                return;
            }
            ItemType itemType = storeItemCandidates[index] as ItemType;
            if (itemType == null)
            {
                return;
            }
            int number = 1;
            if (storeItemCountInput != null && int.TryParse(storeItemCountInput.text, out number) && number <= 0)
            {
                number = 1;
            }
            snapshot.itemStore.Add(itemType, number);
            RefreshStoreLabel();
        }

        /// <summary>
        /// 根据道具存储ID获取道具类型
        /// </summary>
        /// <param name="storeKindId">道具存储ID</param>
        /// <returns>道具类型</returns>
        private ItemType GetItemType(int storeKindId)
        {
            Scenario cur = Scenario.Cur;
            if (cur == null || cur.CommonData == null || cur.CommonData.ItemTypes == null)
            {
                return null;
            }
            return cur.CommonData.ItemTypes.Get(storeKindId);
        }
        #endregion

        #region 决定/返回事件
        /// <summary>
        /// 决定按钮 - 将快照数据同步到Target并关闭窗口
        /// </summary>
        public void OnConfirmClick()
        {
            if (Target == null) return;
            // 将快照所有修改同步回原始City对象
            snapshot.ApplyTo(Target);
            Log.Info("保存城市编辑: " + Target.Name);
            GameSystem.GetSystem<CityEdit>()?.Back();
        }

        /// <summary>
        /// 返回按钮 - 放弃修改,直接关闭窗口(不写入Target)
        /// </summary>
        public void OnCancelClick()
        {
            Log.Info("取消城市编辑: " + (Target != null ? Target.Name : "null"));
            GameSystem.GetSystem<CityEdit>()?.Back();
        }
        #endregion
    }
}
