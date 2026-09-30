using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 城市编辑快照 - 保存City中可通过界面修改的属性的独立副本
    /// 界面操作仅修改快照,确认时才同步回Target
    /// </summary>
    internal struct CityEditSnapshot
    {
        #region 可编辑数据
        /// <summary>金钱</summary>
        public int gold;
        /// <summary>粮食</summary>
        public int food;
        /// <summary>兵力</summary>
        public int troops;
        /// <summary>士气(气力)</summary>
        public int morale;
        /// <summary>耐久</summary>
        public int durability;
        /// <summary>治安</summary>
        public int security;
        /// <summary>人口</summary>
        public int population;
        /// <summary>兵装库存(独立拷贝,避免直接改到Target)</summary>
        public ItemStore itemStore;
        #endregion

        /// <summary>
        /// 从City对象创建快照 - 拷贝所有可编辑字段
        /// </summary>
        /// <param name="city">源城市对象</param>
        /// <returns>城市编辑快照</returns>
        public static CityEditSnapshot FromCity(City city)
        {
            CityEditSnapshot snapshot = new CityEditSnapshot();

            snapshot.gold = city.gold;
            snapshot.food = city.food;
            snapshot.troops = city.troops;
            snapshot.morale = city.morale;
            snapshot.durability = city.durability;
            snapshot.security = city.security;
            snapshot.population = city.population;
            // 库存必须拷贝一份,否则界面上的增删会立刻作用到原始城市
            snapshot.itemStore = city.itemStore != null ? city.itemStore.Copy() : new ItemStore();

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

            city.gold = gold;
            city.food = food;
            city.troops = troops;
            city.morale = morale;
            city.durability = durability;
            city.security = security;
            city.population = population;
            if (itemStore != null)
            {
                city.itemStore = itemStore;
            }

            // 资源变化后重算上限/丰收等派生数据,并刷新渲染显示
            city.UpdateCalculate();
            city.Render?.UpdateRender();
        }
    }

    /// <summary>
    /// 城市(都市)编辑窗口 - 编辑城市的资源、状态、兵装库存,并查看太守与城池分布
    /// 关联窗口: window_edit_city
    /// 界面结构:
    ///   root/object_list                        左侧都市列表
    ///   root/area_all/tab/state                 页签: 基本设定
    ///   root/area_all/tab/scenario              页签: 能力设定(太守)
    ///   root/win_frame/button_ok|button_cancel  决定 / 返回
    ///   root/status_edit                        内容区(资源/归属信息/地图/兵装)
    /// 使用快照模式: 界面操作仅修改快照,确认时才同步到Target
    /// </summary>
    public class UICityEdit : UGUIWindow
    {
        /// <summary>
        /// 数值输入器允许的最大值 - 用于没有天然上限的字段(人口/兵装数量)
        /// </summary>
        private const int MaxEditValue = 9999999;

        /// <summary>
        /// 治安上限
        /// </summary>
        private const int MaxSecurityLimit = 100;

        /// <summary>
        /// 气力上限的兜底值(城市最大气力未计算时使用)
        /// </summary>
        private const int MaxMoraleLimit = 100;

        #region 基础引用
        /// <summary>
        /// 根节点
        /// </summary>
        public RectTransform root;

        /// <summary>
        /// 窗口标题(固定为"都市编辑")
        /// </summary>
        public Text windowTitle;

        /// <summary>
        /// 左侧都市列表 - 展示所有都市,点击切换编辑目标
        /// </summary>
        public UIObjectList objectList;
        #endregion

        #region 页签
        /// <summary>
        /// 页签: 基本设定(城池资源/归属信息/地图/兵装)
        /// </summary>
        public Toggle basicTabToggle;

        /// <summary>
        /// 页签: 能力设定(太守立绘与五维)
        /// </summary>
        public Toggle abilityTabToggle;

        /// <summary>
        /// 基本设定页显示的节点组(归属信息/资源状态/城池地图/兵装列表)
        /// </summary>
        public GameObject[] basicGroup;

        /// <summary>
        /// 能力设定页显示的节点组(太守立绘与五维)
        /// </summary>
        public GameObject[] abilityGroup;
        #endregion

        #region 太守
        /// <summary>
        /// 太守头像(立绘/姓名/特技)
        /// </summary>
        public UIPersonItem leaderPersonItem;

        /// <summary>
        /// 太守五维状态(统率/武力/智力/政治/魅力)
        /// </summary>
        public UIStatusItem leaderStatusItem;
        #endregion

        #region 归属与人数(只读显示)
        /// <summary>
        /// 所属势力
        /// </summary>
        public UITextField forceField;

        /// <summary>
        /// 所属军团
        /// </summary>
        public UITextField corpsField;

        /// <summary>
        /// 所属都市(都市显示自身名称,港关显示所隶属的都市)
        /// </summary>
        public UITextField cityField;

        /// <summary>
        /// 现役武将(空闲/全部)
        /// </summary>
        public UITextField personCountField;

        /// <summary>
        /// 俘虏数量
        /// </summary>
        public UITextField captiveField;
        #endregion

        #region 资源与状态(点击按钮调出数值输入器)
        /// <summary>
        /// 资金 - 点击按钮修改当前值
        /// </summary>
        public Button goldButton;

        /// <summary>
        /// 资金显示(当前/上限)
        /// </summary>
        public UITextField goldField;

        /// <summary>
        /// 兵粮 - 点击按钮修改当前值
        /// </summary>
        public Button foodButton;

        /// <summary>
        /// 兵粮显示(当前/上限)
        /// </summary>
        public UITextField foodField;

        /// <summary>
        /// 士兵 - 点击按钮修改当前值
        /// </summary>
        public Button troopsButton;

        /// <summary>
        /// 士兵显示(当前/上限)
        /// </summary>
        public UITextField troopsField;

        /// <summary>
        /// 气力 - 点击按钮修改当前值
        /// </summary>
        public Button moraleButton;

        /// <summary>
        /// 气力显示(当前/上限)
        /// </summary>
        public UITextField moraleField;

        /// <summary>
        /// 耐久 - 点击按钮修改当前值
        /// </summary>
        public Button durabilityButton;

        /// <summary>
        /// 耐久显示(当前/上限)
        /// </summary>
        public UITextField durabilityField;

        /// <summary>
        /// 治安 - 点击按钮修改当前值
        /// </summary>
        public Button securityButton;

        /// <summary>
        /// 治安显示(0-100)
        /// </summary>
        public UITextField securityField;

        /// <summary>
        /// 人口 - 点击按钮修改当前值
        /// 说明: 对应预制体里的备用节点 food_5(默认隐藏,标题为"资金"),
        /// 绑定后由代码改写标题为"人口",需要显示时在预制体里勾选激活即可
        /// </summary>
        public Button populationButton;

        /// <summary>
        /// 人口显示
        /// </summary>
        public UITextField populationField;
        #endregion

        #region 地图
        /// <summary>
        /// 城池分布地图 - 高亮当前编辑的都市
        /// </summary>
        public UIScenarioCityMap scenarioCityMap;
        #endregion

        #region 兵装(库存)
        /// <summary>
        /// 兵装列表的池子对象(items/content/item)
        /// 仅作为模板使用,不参与显示
        /// </summary>
        public Button itemObject;
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
        /// 目标城市(原始对象,仅在确认时写入)
        /// </summary>
        public City Target { get; private set; }

        /// <summary>
        /// 编辑快照 - 所有界面操作仅修改快照值
        /// </summary>
        private CityEditSnapshot snapshot;

        /// <summary>
        /// 触发刷新标识 - 防止OnValueChanged循环触发
        /// </summary>
        private bool refreshing;

        /// <summary>
        /// 全部都市对象列表
        /// </summary>
        private List<SangoObject> allCityDatas;

        /// <summary>
        /// 当前显示的是否为"基本设定"页
        /// </summary>
        private bool showBasicTab = true;

        /// <summary>
        /// 当前势力可储存的兵装类型列表
        /// 规则: 势力可用(科技满足)且可存储的道具类型,相同storeKind只保留Id最大的一项
        /// </summary>
        private readonly List<ItemType> storeItemTypes = new List<ItemType>();

        /// <summary>
        /// 兵装列表对象池
        /// </summary>
        private CreatePool<Button> itemPool;

        #region 窗口生命周期
        /// <summary>
        /// 初始化 - 创建对象池并绑定只添加一次的按钮/页签事件
        /// 说明: 页签与数值按钮使用的是闭包回调,无法用RemoveListener撤销,
        ///       而窗口实例会被反复打开,所以只在Awake里绑定一次
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            InitItemPool();
            BindFixedEvents();
        }

        /// <summary>
        /// 窗口打开 - 接收目标城市对象并创建编辑快照
        /// </summary>
        /// <param name="objects">参数列表 - objects[0] 为 City</param>
        public override void OnOpen(params object[] objects)
        {
            // 候选: 所有都市(与系统菜单的显示条件保持一致)
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

            // 目标可能是港口/关隘(右键菜单之外的入口),补进列表避免列表里找不到编辑目标
            if (!allCityDatas.Contains(Target))
            {
                allCityDatas.Add(Target);
            }

            // 左侧都市列表初始化,默认选中目标城市
            if (objectList != null)
            {
                objectList.Init(allCityDatas, CitySortFunction.SortByName, OnSelectEditCity);
                objectList.SelectDefaultObject(Target);
            }

            // 创建编辑快照 - 从Target拷贝所有可编辑数据
            snapshot = CityEditSnapshot.FromCity(Target);
            showBasicTab = true;

            BindEvents();
            ApplyTabContent();
            Refresh();
        }

        /// <summary>
        /// 左侧都市列表选择回调 - 切换编辑目标并重建快照
        /// </summary>
        /// <param name="index">选中城市在列表中的下标</param>
        private void OnSelectEditCity(int index)
        {
            if (allCityDatas == null || index < 0 || index >= allCityDatas.Count)
            {
                return;
            }
            Target = allCityDatas[index] as City;
            if (Target == null)
            {
                return;
            }
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

        #region 对象池
        /// <summary>
        /// 初始化兵装列表对象池 - 只创建一次,后续复用
        /// </summary>
        private void InitItemPool()
        {
            if (itemPool != null || itemObject == null)
            {
                return;
            }
            // 模板节点不显示,由对象池复制出实际行
            itemObject.gameObject.SetActive(false);
            itemPool = new CreatePool<Button>(itemObject);
        }
        #endregion

        #region 事件绑定
        /// <summary>
        /// 绑定只添加一次的事件(页签 + 资源数值按钮)
        /// 这些回调是闭包,无法用RemoveListener撤销,所以只允许绑定一次
        /// 注意: 不要清空按钮上的持久化监听(预制体上配置的事件)
        /// </summary>
        private void BindFixedEvents()
        {
            // 页签
            BindTabToggle(basicTabToggle, true);
            BindTabToggle(abilityTabToggle, false);

            // 资源与状态 - 点击按钮弹出数值输入器
            BindCalculator(goldButton, "资金", () => snapshot.gold, v => snapshot.gold = v,
                () => Target != null ? Target.GoldLimit : MaxEditValue);
            BindCalculator(foodButton, "兵粮", () => snapshot.food, v => snapshot.food = v,
                () => Target != null ? Target.FoodLimit : MaxEditValue);
            BindCalculator(troopsButton, "士兵", () => snapshot.troops, v => snapshot.troops = v,
                () => Target != null ? Target.TroopsLimit : MaxEditValue);
            BindCalculator(moraleButton, "气力", () => snapshot.morale, v => snapshot.morale = v,
                () => Target != null ? Target.MaxMorale : MaxMoraleLimit);
            BindCalculator(durabilityButton, "耐久", () => snapshot.durability, v => snapshot.durability = v,
                () => Target != null ? Target.DurabilityLimit : MaxEditValue);
            BindCalculator(securityButton, "治安", () => snapshot.security, v => snapshot.security = v,
                () => MaxSecurityLimit);
            BindCalculator(populationButton, "人口", () => snapshot.population, v => snapshot.population = v,
                () => MaxEditValue);
        }

        /// <summary>
        /// 绑定每次打开窗口都要重新绑定的事件(决定/返回)
        /// </summary>
        private void BindEvents()
        {
            // 窗口可能被反复打开,先清掉上一轮的监听,避免重复触发
            RemoveListeners();

            // 决定 / 返回
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClick);
        }

        /// <summary>
        /// 清理决定/返回的监听器
        /// </summary>
        private void RemoveListeners()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmClick);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClick);
        }

        /// <summary>
        /// 绑定页签切换事件
        /// </summary>
        /// <param name="toggle">页签Toggle</param>
        /// <param name="basic">是否为"基本设定"页</param>
        private void BindTabToggle(Toggle toggle, bool basic)
        {
            if (toggle == null) return;
            toggle.onValueChanged.AddListener((isOn) =>
            {
                if (refreshing || !isOn) return;
                showBasicTab = basic;
                ApplyTabContent();
            });
        }

        /// <summary>
        /// 绑定"按钮 + 数值显示": 点击按钮弹出数值输入器(window_calculator),
        /// 确认后写入快照并刷新显示
        /// </summary>
        /// <param name="button">触发按钮</param>
        /// <param name="title">数值输入器标题(字段名)</param>
        /// <param name="getter">从快照读取当前值</param>
        /// <param name="setter">向快照写入新值</param>
        /// <param name="maxGetter">取值范围上限的提供者</param>
        private void BindCalculator(Button button, string title, Func<int> getter, Action<int> setter, Func<int> maxGetter)
        {
            if (button == null) return;
            button.onClick.AddListener(() =>
            {
                if (refreshing || Target == null) return;

                int current = getter();
                int max = maxGetter != null ? maxGetter() : MaxEditValue;
                // 当前值可能超过上限(编辑器改出来的数据),保证不会被输入器压回去
                if (max < current) max = current;

                Window.Instance.Open("window_calculator", title, current, 0, max,
                    (Action<int>)((val) =>
                    {
                        setter(val);
                        RefreshValueFields();
                    }),
                    null);
            });
        }
        #endregion

        #region UI刷新
        /// <summary>
        /// 刷新窗口 - 将快照当前值同步到界面
        /// </summary>
        public override void OnRefresh()
        {
            if (Target == null) return;

            refreshing = true;
            try
            {
                RefreshTabToggles();
                RefreshLeader();
                RefreshInfoFields();
                RefreshValueFields();
                RefreshMap();
                RefreshItemList();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// 刷新页签选中状态
        /// </summary>
        private void RefreshTabToggles()
        {
            if (basicTabToggle != null) basicTabToggle.SetIsOnWithoutNotify(showBasicTab);
            if (abilityTabToggle != null) abilityTabToggle.SetIsOnWithoutNotify(!showBasicTab);
        }

        /// <summary>
        /// 按当前页签切换内容区节点的显示
        /// </summary>
        private void ApplyTabContent()
        {
            SetGroupActive(basicGroup, showBasicTab);
            SetGroupActive(abilityGroup, !showBasicTab);
        }

        /// <summary>
        /// 批量设置节点组的激活状态
        /// </summary>
        /// <param name="group">节点组</param>
        /// <param name="active">是否激活</param>
        private void SetGroupActive(GameObject[] group, bool active)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
            {
                if (group[i] != null && group[i].activeSelf != active)
                {
                    group[i].SetActive(active);
                }
            }
        }

        /// <summary>
        /// 刷新太守显示 - 头像与五维
        /// </summary>
        private void RefreshLeader()
        {
            Person leader = Target != null ? Target.Leader : null;
            if (leaderPersonItem != null) leaderPersonItem.SetPerson(leader, 2);
            if (leaderStatusItem != null) leaderStatusItem.SetPerson(leader);
        }

        /// <summary>
        /// 刷新归属与人数信息(只读)
        /// </summary>
        private void RefreshInfoFields()
        {
            City city = Target;
            // 标题带上当前编辑的城市名,方便确认编辑目标
            if (windowTitle != null) windowTitle.text = "都市编辑 - " + city.Name;
            if (forceField != null) forceField.text = CitySortFunction.SortByBelongForce.GetValueStr(city);
            if (corpsField != null) corpsField.text = CitySortFunction.SortByBelongCorps.GetValueStr(city);
            // 都市: 都市显示自身名称,港关显示所隶属的都市
            if (cityField != null) cityField.text = city.BelongCity != null ? city.BelongCity.Name : city.Name;
            if (personCountField != null) personCountField.text = CitySortFunction.SortByAllPersonCountInfo.GetValueStr(city);
            if (captiveField != null) captiveField.text = CitySortFunction.SortByCaptiveCount.GetValueStr(city);
        }

        /// <summary>
        /// 刷新资源与状态显示(当前/上限)
        /// </summary>
        private void RefreshValueFields()
        {
            City city = Target;
            if (city == null) return;

            if (goldField != null) goldField.text = $"{snapshot.gold}/{city.GoldLimit}";
            if (foodField != null) foodField.text = $"{snapshot.food}/{city.FoodLimit}";
            if (troopsField != null) troopsField.text = $"{snapshot.troops}/{city.TroopsLimit}";
            if (moraleField != null) moraleField.text = $"{snapshot.morale}/{city.MaxMorale}";
            if (durabilityField != null) durabilityField.text = $"{snapshot.durability}/{city.DurabilityLimit}";
            if (securityField != null) securityField.text = snapshot.security.ToString();
            if (populationField != null) populationField.text = snapshot.population.ToString();

            // 备用的人口节点标题在预制体里是"资金",绑定后统一改写标题,方便直接启用
            if (populationButton != null)
            {
                Text lab = FindChildText(populationButton.transform, "lab");
                if (lab != null && lab.text != "人口") lab.text = "人口";
            }
        }

        /// <summary>
        /// 刷新城池分布地图 - 高亮当前编辑的都市
        /// </summary>
        private void RefreshMap()
        {
            if (scenarioCityMap == null) return;
            Scenario cur = Scenario.Cur;
            if (cur == null) return;
            scenarioCityMap.Show(cur, Target);
        }

        /// <summary>
        /// 刷新兵装(库存)列表
        /// 依据当前势力可储存的道具类型动态生成,每行显示道具名称与库存数量
        /// </summary>
        private void RefreshItemList()
        {
            InitItemPool();
            if (itemPool == null) return;

            itemPool.Reset();
            BuildStoreItemTypes();

            for (int i = 0; i < storeItemTypes.Count; i++)
            {
                ItemType itemType = storeItemTypes[i];
                Button row = itemPool.Create();
                if (row == null) continue;

                // 命名成道具编号,方便在层级面板中排查
                row.name = itemType.Id.ToString();

                Text nameLabel = FindChildText(row.transform, "lab");
                if (nameLabel != null) nameLabel.text = itemType.Name;

                UITextField countField = FindChildComponent<UITextField>(row.transform, "textField");
                int number = snapshot.itemStore != null ? snapshot.itemStore.GetNumber(itemType) : 0;
                if (countField != null) countField.text = number.ToString();

                // 对象池会复用行,必须重建回调(闭包捕获当前道具类型)
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(() => OnItemRowClick(itemType));
            }
        }

        /// <summary>
        /// 构建当前势力可储存的兵装类型列表
        /// 规则: 遍历全部道具类型,只取"可存储"且对该势力可用(科技满足)的项;
        ///       相同storeKind只保留Id最大的一项(高阶道具优先)
        /// </summary>
        private void BuildStoreItemTypes()
        {
            storeItemTypes.Clear();
            Scenario cur = Scenario.Cur;
            if (cur == null || cur.CommonData == null || cur.CommonData.ItemTypes == null)
            {
                return;
            }

            Force force = Target != null ? Target.BelongForce : null;
            Dictionary<int, ItemType> itemMap = new Dictionary<int, ItemType>();
            cur.CommonData.ItemTypes.ForEach(itemType =>
            {
                if (itemType == null || !itemType.store)
                {
                    return;
                }
                // 归属势力存在时,只保留该势力可用(科技满足)的道具
                if (force != null && !itemType.IsValid(force))
                {
                    return;
                }

                ItemType hasItemType;
                if (itemMap.TryGetValue(itemType.storeKind, out hasItemType))
                {
                    // 相同storeKind: 取Id最大的一项
                    if (itemType.Id > hasItemType.Id)
                    {
                        itemMap[itemType.storeKind] = itemType;
                    }
                }
                else
                {
                    itemMap[itemType.storeKind] = itemType;
                }
            });

            foreach (ItemType itemType in itemMap.Values)
            {
                storeItemTypes.Add(itemType);
            }
            storeItemTypes.Sort(SangoObject.Compare);
        }

        /// <summary>
        /// 在子节点中查找文本组件
        /// </summary>
        /// <param name="parent">父节点</param>
        /// <param name="childName">子节点名</param>
        /// <returns>文本组件,找不到时返回null</returns>
        private static Text FindChildText(Transform parent, string childName)
        {
            if (parent == null) return null;
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<Text>() : null;
        }

        /// <summary>
        /// 在子节点中查找指定组件
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="childName">子节点名</param>
        /// <returns>组件,找不到时返回null</returns>
        private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
        {
            if (parent == null) return null;
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<T>() : null;
        }
        #endregion

        #region 兵装事件
        /// <summary>
        /// 点击兵装行 - 弹出数值输入器修改该道具的库存数量
        /// 取值范围: 0 ~ itemType.TransformLimit(城池仓库上限)
        /// </summary>
        /// <param name="itemType">道具类型</param>
        private void OnItemRowClick(ItemType itemType)
        {
            if (refreshing || Target == null || itemType == null || snapshot.itemStore == null)
            {
                return;
            }

            int current = snapshot.itemStore.GetNumber(itemType);
            // 道具可按比例调整仓库上限(ItemType.p1),按城池仓库上限换算出本道具的上限
            int max = itemType.TransformLimit(Target.StoreLimit);
            if (max < 0) max = 0;
            // 已有库存可能超过上限,保证不会被输入器压回去
            if (max < current) max = current;

            Window.Instance.Open("window_calculator", itemType.Name, current, 0, max,
                (Action<int>)((val) =>
                {
                    // 0 表示不库存该道具
                    if (val <= 0)
                    {
                        snapshot.itemStore.Remove(itemType.storeKind);
                    }
                    else
                    {
                        snapshot.itemStore.Set(itemType.storeKind, val);
                    }
                    RefreshItemList();
                }),
                null);
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
