using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;
namespace Sango.UI
{
    /// <summary>
    /// 都市运输(物资输送部队)编成窗口。
    /// 玩家在此选择运输武将,并分配随军的士兵、资金、兵粮与兵装:
    /// 1.士兵、资金、兵粮既可以用滑条粗略分配,也可以点击 root/troop、root/gold、root/food
    ///   三个按钮调出通用数值输入器(window_calculator)精确输入;
    /// 2.兵装滑条行上的 item 按钮同样可以调出数值输入器,精确输入携带数量。
    /// </summary>
    public class UICityTransport : UGUIWindow
    {
        public UIItemTypeRect itemTypeRect;
        public UIItemTypeSliderRect itemTypeSliderRect;

        public UIPersonItem[] personItems;

        public UITextField troopsLabel;
        public UITextField goldLabel;
        public UITextField foodLabel;
        public UITextField dayTurnLabel;

        public UITextField itemTroopsLabel;
        public UITextField itemGoldLabel;
        public UITextField itemFoodLabel;
        public UITextField action_value;

        public Slider troopsSlider;
        public Slider goldSlider;
        public Slider foodSlider;
        bool showLand = true;
        public UIStatusItem statusItem;
        public UITextField typeLaebl;
        public UITextField energyLaebl;
        public UITextField[] itemLabels;

        /// <summary>
        /// 士兵数量按钮(root/troop),点击打开数值输入器精确输入随军士兵数
        /// </summary>
        Button troopsButton;

        /// <summary>
        /// 资金数量按钮(root/gold),点击打开数值输入器精确输入随军资金
        /// </summary>
        Button goldButton;

        /// <summary>
        /// 兵粮数量按钮(root/food),点击打开数值输入器精确输入随军兵粮
        /// </summary>
        Button foodButton;

        CityTransport cityTransportSys;
        Dictionary<int, UIItemType> id2UIItemType = new Dictionary<int, UIItemType>();
        City targetCity;
        Troop targetTroop;

        /// <summary>
        /// 通用数值输入器窗口名
        /// </summary>
        const string CalculatorWindowName = "window_calculator";

        /// <summary>
        /// 当前剧本参数（剧本配置 →"运输部队携带上限"四项）。
        /// 读不到剧本/参数时由各 Max* 属性回落到 <see cref="ScenarioVariables"/> 的缺省常量。
        /// </summary>
        ScenarioVariables TransportLimits => Scenario.Cur != null ? Scenario.Cur.Variables : null;

        /// <summary>单队携带兵力上限（剧本配置"最大带兵数量"；0=不限已在 Transport*Limit 里折算成 int.MaxValue）</summary>
        int MaxTransportTroops
        {
            get { return TransportLimits != null ? TransportLimits.TransportTroopLimit : ScenarioVariables.DefaultMaxUnitTransportTroops; }
        }

        /// <summary>单队携带资金上限（剧本配置"最大金钱数量"）</summary>
        int MaxTransportGold
        {
            get { return TransportLimits != null ? TransportLimits.TransportGoldLimit : ScenarioVariables.DefaultMaxUnitTransportGold; }
        }

        /// <summary>单队携带粮食上限（剧本配置"最大粮食数量"）</summary>
        int MaxTransportFood
        {
            get { return TransportLimits != null ? TransportLimits.TransportFoodLimit : ScenarioVariables.DefaultMaxUnitTransportFood; }
        }

        /// <summary>
        /// 单队某类物品的可携带上限 —— **转换上限**。
        ///
        /// 剧本配置里的"最大兵装数量"只是换算前的基准值，必须交给
        /// <see cref="ItemType.TransformLimit"/> 换算成该类物品的实际上限：
        ///   · 枪 / 戟 / 弩 / 军马（p1 = 0）→ 原值；
        ///   · 冲车 / 井阑 / 楼船等（p1 = 10）→ 基准值 × 10 / 1000。
        /// 直接拿基准值比较会把器械 / 舰船的容量放大 100 倍。
        /// </summary>
        /// <param name="itemType">物品类型</param>
        /// <returns>该类物品的可携带上限</returns>
        int GetItemCapacity(ItemType itemType)
        {
            ScenarioVariables limits = TransportLimits;
            if (limits != null)
                return limits.GetTransportItemLimit(itemType);
            return itemType != null ? itemType.TransformLimit(ScenarioVariables.DefaultMaxUnitEquipmentQuantity) : 0;
        }

        public override void OnOpen()
        {
            showLand = true; 
            cityTransportSys = GameSystem.GetSystem<CityTransport>();
            targetCity = cityTransportSys.TargetCity;
            targetTroop = cityTransportSys.TargetTroop;

            itemTypeRect.onItemTypeShow = OnItemTypeShow;
            itemTypeSliderRect.onItemTypeShow = OnItemTypeSliderShow;
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.MakeTansport)}/{cityTransportSys.TargetCity.BelongCorps.ActionPoint}";

            // 士兵/资金/兵粮三个数量按钮统一由代码接管,保证点击一定能打开数值输入器
            BindNumberPanelButtons();

            UpdateContent();
        }

        #region 士兵/资金/兵粮 数值输入器

        /// <summary>
        /// 绑定 root/troop、root/gold、root/food 三个数量按钮。
        /// 预制体上这三个按钮的持久化事件引用了已废弃的类型名(Sango.Game.Render.UI.*),
        /// 运行期不一定能触发,因此这里统一关闭持久化事件,改由代码直接绑定处理函数。
        /// </summary>
        void BindNumberPanelButtons()
        {
            troopsButton = BindNumberPanelButton("root/troop", OpenNumberPanel_troops);
            goldButton = BindNumberPanelButton("root/gold", OpenNumberPanel_gold);
            foodButton = BindNumberPanelButton("root/food", OpenNumberPanel_food);
        }

        /// <summary>
        /// 按节点路径获取数量按钮并绑定点击事件
        /// </summary>
        /// <param name="path">按钮相对窗口根节点的路径,例如 root/troop</param>
        /// <param name="onClick">点击处理函数</param>
        /// <returns>绑定成功的按钮,节点不存在时返回null</returns>
        Button BindNumberPanelButton(string path, UnityAction onClick)
        {
            Button button = GetComponent<Button>(path);
            if (button == null)
            {
                Log.Warning($"运输: 未找到数量按钮节点 {path}");
                return null;
            }

            // 关闭预制体上的持久化事件(旧类型名已失效),并清空脚本监听后重新绑定,避免重复弹出输入器
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// 是否允许打开数值输入器(与滑条保持一致: 未选择武将时不允许调整数量)
        /// </summary>
        /// <returns>可以打开返回true</returns>
        bool CanOpenNumberPanel()
        {
            if (cityTransportSys == null || targetCity == null || targetTroop == null)
                return false;

            if (cityTransportSys.personList.Count == 0)
            {
                Log.Warning("运输: 请先选择武将,再设置士兵/资金/兵粮数量");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 计算本次运输可携带的士兵上限。
        /// 运输部队只负责押运物资,不与主将带兵上限挂钩,因此口径与滑条保持一致。
        ///
        /// 【剧本配置上限】取 min(城内兵力, "最大带兵数量")：
        /// 后者对齐原版 max_unit_transport_troops（s11_db.h:200-206），默认 60000，
        /// 配置成 0 表示不限（此时等价于"以城内兵力为上限"的旧行为）。
        /// </summary>
        /// <returns>可携带士兵上限</returns>
        int GetMaxTroops()
        {
            return System.Math.Min(System.Math.Max(0, targetCity.troops), MaxTransportTroops);
        }

        /// <summary>
        /// 士兵数量按钮(root/troop) - 打开数值输入器精确输入随军士兵数
        /// 取值范围: 1 ~ 城内兵力(与滑条一致)
        /// </summary>
        public void OpenNumberPanel_troops()
        {
            if (!CanOpenNumberPanel())
                return;

            int current = targetTroop.troops;
            int max = GetMaxTroops();
            // 当前值可能超过上限(换将/换城后的残留数据),抬高上限保证不会被输入器削掉
            if (max < current) max = current;
            if (max < 1) max = 1;

            Window.Instance.Open(CalculatorWindowName, "士兵", current, 1, max,
                (Action<int>)((val) => SetTroops(val)),
                null);
        }

        /// <summary>
        /// 应用随军士兵数并刷新界面
        /// </summary>
        /// <param name="troops">期望携带的士兵数</param>
        void SetTroops(int troops)
        {
            troops = System.Math.Max(0, troops);
            int max = GetMaxTroops();
            if (troops > max) troops = max;
            targetTroop.troops = troops;
            UpdateTroopsInfo();
        }

        /// <summary>
        /// 资金数量按钮(root/gold) - 打开数值输入器精确输入随军资金
        /// 取值范围: 0 ~ 城内资金
        /// </summary>
        public void OpenNumberPanel_gold()
        {
            if (!CanOpenNumberPanel())
                return;

            int current = targetTroop.gold;
            // 【不能超过城内资金】同 UICityExpedition：资金按城内实有封顶，
            // 避免"城被扣成负数、部队却带足额"的刷钱入口。
            // 【剧本配置上限】再叠加"最大金钱数量"（默认 20000，0=不限）。
            int max = System.Math.Min(targetCity.gold, MaxTransportGold);
            if (max < 0) max = 0;
            // 当前值可能超过上限(换将/换城或剧本参数调整后的残留数据),抬高上限保证不被削掉
            if (max < current) max = current;

            Window.Instance.Open(CalculatorWindowName, "资金", current, 0, max,
                (Action<int>)((val) => SetGold(val)),
                null);
        }

        /// <summary>
        /// 应用随军资金并刷新界面
        /// </summary>
        /// <param name="gold">期望携带的资金</param>
        void SetGold(int gold)
        {
            gold = System.Math.Max(0, gold);
            int max = System.Math.Min(System.Math.Max(0, targetCity.gold), MaxTransportGold);
            if (gold > max) gold = max;
            targetTroop.gold = gold;
            UpdateTroopsInfo();
        }

        /// <summary>
        /// 兵粮数量按钮(root/food) - 打开数值输入器精确输入随军兵粮
        /// 取值范围: 0 ~ 城内兵粮
        /// </summary>
        public void OpenNumberPanel_food()
        {
            if (!CanOpenNumberPanel())
                return;

            int current = targetTroop.food;
            // 【剧本配置上限】min(城内兵粮, "最大粮食数量"，默认 2000000，0=不限)
            int max = System.Math.Min(targetCity.food, MaxTransportFood);
            if (max < 0) max = 0;
            if (max < current) max = current;

            Window.Instance.Open(CalculatorWindowName, "兵粮", current, 0, max,
                (Action<int>)((val) => SetFood(val)),
                null);
        }

        /// <summary>
        /// 应用随军兵粮并刷新界面
        /// </summary>
        /// <param name="food">期望携带的兵粮</param>
        void SetFood(int food)
        {
            food = System.Math.Max(0, food);
            int max = System.Math.Min(System.Math.Max(0, targetCity.food), MaxTransportFood);
            if (food > max) food = max;
            targetTroop.food = food;
            UpdateTroopsInfo();
        }

        #endregion

        #region 兵装

        void OnItemTypeShow(ItemType itemType, UIItemType uIItemType)
        {
            id2UIItemType.Add(itemType.Id, uIItemType);
            uIItemType.SetItemType(itemType);
            uIItemType.SetNumber(targetCity.itemStore.GetNumber(itemType));
            int troopHas = targetTroop.itemStore.GetNumber(itemType);
            uIItemType.SetUsed(troopHas);
        }

        /// <summary>
        /// 兵装滑条行初始化。
        /// 除滑条拖动外,点击行上的 item 按钮会打开数值输入器(window_calculator)精确输入携带数量。
        /// </summary>
        /// <param name="itemType">该行对应的兵装类型</param>
        /// <param name="uIItemTypeSlider">该行的界面对象</param>
        void OnItemTypeSliderShow(ItemType itemType, UIItemTypeSlider uIItemTypeSlider)
        {
            // 【剧本配置上限·转换】可携带量 = min(城内该物品库存, TransformLimit(最大兵装数量))。
            // 转换这一步不能省：器械 / 舰船(p1=10) 与兵装(p1=0) 的口径不同，见 GetItemCapacity。
            int itemNumber = System.Math.Min(targetCity.itemStore.GetNumber(itemType), GetItemCapacity(itemType));

            // item 按钮(兵装名称按钮) → 数值输入器
            // 每次刷新都会重入这里,先清空脚本监听再绑定,避免监听器重复堆积
            if (uIItemTypeSlider.item != null)
            {
                uIItemTypeSlider.item.onClick.RemoveAllListeners();
                uIItemTypeSlider.item.onClick.AddListener(() => OpenItemNumberPanel(itemType, uIItemTypeSlider));
            }

            // 上限保存在行对象上,便于其他逻辑复用
            uIItemTypeSlider.maxNumber = itemNumber;

            if (itemNumber <= 0)
            {
                uIItemTypeSlider.SetValid(false);
                uIItemTypeSlider.numberSlider.SetValueWithoutNotify(0);
                uIItemTypeSlider.numberLabel.text = "0/0";
                return;
            }
            uIItemTypeSlider.SetValid(true);

            int troopHas = targetTroop.itemStore.GetNumber(itemType);
            uIItemTypeSlider.numberSlider.SetValueWithoutNotify((float)troopHas / itemNumber);
            uIItemTypeSlider.numberLabel.text = $"{troopHas}/{itemNumber}";
            uIItemTypeSlider.numberSlider.onValueChanged.RemoveAllListeners();
            uIItemTypeSlider.numberSlider.onValueChanged.AddListener((p) =>
            {
                ApplyItemNumber(itemType, uIItemTypeSlider, (int)System.Math.Ceiling(itemNumber * p));
            });
        }

        /// <summary>
        /// 兵装数量按钮(item 按钮) - 打开数值输入器精确输入随军携带的兵装数量
        /// 取值范围: 0 ~ min(城内该兵装库存, 剧本配置的"最大兵装数量")
        /// </summary>
        /// <param name="itemType">兵装类型</param>
        /// <param name="uIItemTypeSlider">对应的滑条行(可能为空)</param>
        void OpenItemNumberPanel(ItemType itemType, UIItemTypeSlider uIItemTypeSlider)
        {
            if (itemType == null)
                return;

            if (!CanOpenNumberPanel())
                return;

            int stock = targetCity.itemStore.GetNumber(itemType);
            if (stock <= 0)
            {
                Log.Warning($"运输: 城内没有[{itemType.Name}],无法携带");
                return;
            }

            // 【剧本配置上限·转换】min(城内库存, TransformLimit(最大兵装数量))，
            // 兵装与器械/舰船口径不同，必须走转换
            int max = System.Math.Min(stock, GetItemCapacity(itemType));
            int current = targetTroop.itemStore.GetNumber(itemType);
            if (max < current) max = current;

            Window.Instance.Open(CalculatorWindowName, itemType.Name, current, 0, max,
                (Action<int>)((val) => ApplyItemNumber(itemType, uIItemTypeSlider, val)),
                null);
        }

        /// <summary>
        /// 应用兵装携带数量,并同步滑条、数量文本与库存列表的显示
        /// </summary>
        /// <param name="itemType">兵装类型</param>
        /// <param name="uIItemTypeSlider">对应的滑条行(可能为空)</param>
        /// <param name="num">期望携带的数量</param>
        void ApplyItemNumber(ItemType itemType, UIItemTypeSlider uIItemTypeSlider, int num)
        {
            // 可携带量 = min(城内库存, TransformLimit(剧本配置的"最大兵装数量"))
            int capacity = System.Math.Min(targetCity.itemStore.GetNumber(itemType), GetItemCapacity(itemType));
            num = System.Math.Max(0, System.Math.Min(num, capacity));

            targetTroop.itemStore.Set(itemType, num);

            if (uIItemTypeSlider != null)
            {
                if (capacity > 0)
                    uIItemTypeSlider.numberSlider.SetValueWithoutNotify((float)num / capacity);
                uIItemTypeSlider.numberLabel.text = $"{num}/{capacity}";
            }

            if (id2UIItemType.TryGetValue(itemType.Id, out UIItemType uIItemType))
                uIItemType.SetUsed(num);
        }

        #endregion

        /// <summary>
        /// 退出
        /// </summary>
        public void OnCancel()
        {
            cityTransportSys.Done();
        }

        public void OnOK()
        {
            cityTransportSys.MakeTroop();
        }

        public void OnTroopsSliderValueChanged(float p)
        {
            if (cityTransportSys.personList.Count == 0)
                return;

            // 滑条按"实际可携带上限"（min(城内兵力, 剧本配置上限)）取比例，与数值输入器同口径
            int troop = (int)System.Math.Ceiling(GetMaxTroops() * p);
            targetTroop.troops = troop;
            UpdateTroopsInfo();
        }

        public void OnGoldSliderValueChanged(float p)
        {
            if (cityTransportSys.personList.Count == 0)
                return;

            int max = System.Math.Min(System.Math.Max(0, targetCity.gold), MaxTransportGold);
            int gold = (int)System.Math.Ceiling(max * p);
            targetTroop.gold = gold;
            UpdateTroopsInfo();
        }

        public void OnFoodSliderValueChanged(float p)
        {
            if (cityTransportSys.personList.Count == 0)
                return;

            int max = System.Math.Min(System.Math.Max(0, targetCity.food), MaxTransportFood);
            int food = (int)System.Math.Ceiling(max * p);
            targetTroop.food = food;
            UpdateTroopsInfo();
        }

        public void UpdateContent()
        {
            id2UIItemType.Clear();
            itemTypeRect.Init();
            itemTypeSliderRect.Init();

            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < cityTransportSys.personList.Count)
                    personItems[i].SetPerson(cityTransportSys.personList[i]);
                else
                    personItems[i].SetPerson(null);
            }

            UpdateTroopStatus();
            UpdateTroopsInfo();
        }

        void SetItemLabel(UITextField label, int all, int ues)
        {
            int left = all - ues;
            if (left > 0)
            {
                if (ues == 0)
                    label.text = all.ToString();
                else
                    label.text = $"{all} → {left}";
            }
            else
            {
                if (all == 0)
                    label.text = $"<color=#ff0000>{all}</color>";
                else
                    label.text = $"{all}→<color=#ff0000>{left}</color>";
            }
        }

        void UpdateTroopsInfo()
        {
            if (targetCity.troops > 0)
            {
                troopsSlider.SetValueWithoutNotify((float)targetTroop.troops / targetCity.troops);
                troopsSlider.interactable = true;
            }
            else
            {
                troopsSlider.SetValueWithoutNotify(0);
                troopsSlider.interactable = false;
            }

            if (targetCity.gold > 0)
            {
                goldSlider.SetValueWithoutNotify((float)targetTroop.gold / targetCity.gold);
                goldSlider.interactable = true;
            }
            else
            {
                goldSlider.SetValueWithoutNotify(0);
                goldSlider.interactable = false;
            }

            if (targetCity.food > 0)
            {
                foodSlider.SetValueWithoutNotify((float)targetTroop.food / targetCity.food);
                foodSlider.interactable = true;
            }
            else
            {
                foodSlider.SetValueWithoutNotify(0);
                foodSlider.interactable = false;
            }

            troopsLabel.text = $"{targetTroop.troops}/{targetCity.troops}";
            goldLabel.text = $"{targetTroop.gold}/{targetCity.gold}";
            foodLabel.text = $"{targetTroop.food}/{targetCity.food}";

            int foodCost = targetTroop.PrepeareFoodCost();
            int turnCount = foodCost > 0 ? (int)(targetTroop.food / foodCost) : 0;
            dayTurnLabel.text = $"{turnCount * 10}日";

            SetItemLabel(itemTroopsLabel, targetCity.troops, targetTroop.troops);
            SetItemLabel(itemGoldLabel, targetCity.gold, targetTroop.gold);
            SetItemLabel(itemFoodLabel, targetCity.food, targetTroop.food);

            int itemCount = Scenario.Cur.CommonData.ItemTypeList.Count;
            int showIndex = 0;
            for (int i = 0; i < itemCount; i++)
            {
                ItemType itemType = Scenario.Cur.CommonData.ItemTypeList[i];
                if (showIndex < itemLabels.Length)

                    itemLabels[showIndex].gameObject.SetActive(true);
                int has = targetCity.itemStore.GetNumber(itemType);
                int use = 0;
                if (targetTroop.LandTroopType.costItems != null)
                {
                    for (int j = 0; j < targetTroop.LandTroopType.costItems.Length; j += 2)
                    {
                        int itemId = targetTroop.LandTroopType.costItems[j];
                        if (itemId == i)
                        {
                            int need = targetTroop.LandTroopType.costItems[j + 1];
                            use = need * targetTroop.troops / 1000;
                        }
                    }
                }
                if (targetTroop.WaterTroopType.costItems != null)
                {
                    for (int j = 0; j < targetTroop.WaterTroopType.costItems.Length; j += 2)
                    {
                        int itemId = targetTroop.WaterTroopType.costItems[j];
                        if (itemId == i)
                        {
                            int need = targetTroop.WaterTroopType.costItems[j + 1];
                            use = need * targetTroop.troops / 1000;
                        }
                    }
                }
                if (showIndex < itemLabels.Length)
                {
                    itemLabels[showIndex].SetTitle(itemType.Name);
                    SetItemLabel(itemLabels[showIndex], has, use);
                }
                showIndex++;
            }

            for (int i = showIndex; i < itemLabels.Length; i++)
            {
                itemLabels[i].gameObject.SetActive(false);
            }
        }

        public void OnPersonChange(List<Person> personList)
        {
            cityTransportSys.personList = personList;
            cityTransportSys.UpdateJobValue();

            UpdateContent();
        }

        /// <summary>打开运输选择器，默认使用搬运特性优先的排序列。</summary>
        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(cityTransportSys.TargetCity.freePersons,
                cityTransportSys.personList, 3, OnPersonChange, cityTransportSys.customTitleList, cityTransportSys.customTitleName, 1);
        }

        public void OnSlecteMax()
        {
            targetTroop.itemStore = targetCity.itemStore.Copy();
            targetTroop.troops = targetCity.troops;
            targetTroop.food = targetCity.food;
            targetTroop.gold = targetCity.gold;
            UpdateContent();
        }

        public void OnSlecteMin()
        {
            targetTroop.itemStore.Clear();
            targetTroop.troops = 0;
            targetTroop.food = 0;
            targetTroop.gold = 0;
            UpdateContent();
        }

        public void OnTroopTypeShowLand(bool b)
        {
            if (b)
            {
                showLand = true;
                UpdateTroopStatus();
            }
        }

        public void OnTroopTypeShowWater(bool b)
        {
            if (b)
            {
                showLand = false;
                UpdateTroopStatus();
            }
        }


        void UpdateTroopStatus()
        {
            int atk, def, intel, build, move;
            bool hasPeson = cityTransportSys.personList.Count > 0;
            Troop targetTroop = cityTransportSys.TargetTroop;
            if (showLand)
            {
                if (hasPeson)
                {
                    atk = targetTroop.landAttack;
                    def = targetTroop.landDefence;
                    intel = targetTroop.Intelligence;
                    build = targetTroop.BuildPower;
                    move = targetTroop.landMoveAbility;
                }
                else
                {
                    atk = targetTroop.LandTroopType.atk;
                    def = targetTroop.LandTroopType.def;
                    intel = targetTroop.Intelligence;
                    build = targetTroop.BuildPower;
                    move = targetTroop.LandTroopType.move;
                }

                typeLaebl.text = targetTroop.LandTroopType.Name;
            }
            else
            {
                if (hasPeson)
                {
                    atk = targetTroop.waterAttack;
                    def = targetTroop.waterDefence;
                    intel = targetTroop.Intelligence;
                    build = targetTroop.BuildPower;
                    move = targetTroop.waterMoveAbility;
                }
                else
                {
                    atk = targetTroop.WaterTroopType.atk;
                    def = targetTroop.WaterTroopType.def;
                    intel = targetTroop.Intelligence;
                    build = targetTroop.BuildPower;
                    move = targetTroop.WaterTroopType.move;
                }

                typeLaebl.text = targetTroop.WaterTroopType.Name;
            }


            statusItem.SetTroopStatus(atk, def, intel, build, move);
            energyLaebl.text = targetTroop.morale.ToString();
        }
    }
}
