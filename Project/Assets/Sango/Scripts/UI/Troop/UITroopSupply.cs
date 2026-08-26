using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    /// <summary>
    /// 部队补给界面（运输队向目标部队输送兵粮、资金、兵力与兵装）。
    /// 本脚本仅实现 UI 展示与交互，实际的数据转移由外部传入的回调处理。
    /// 说明：兵装不单独设置滑动条，而是根据“目标部队当前兵种(TroopType)的 costItems”
    /// 与“转移的士兵人数”联动计算；转移的士兵数受兵装可装备的最大士兵数限制。
    /// </summary>
    public class UITroopSupply : UGUIWindow
    {
        #region 兵装存储类型（storeKind 与 ItemStore 保持一致：枪=2、戟=3、弩=4、马=5）
        /// <summary>枪兵装对应的 storeKind</summary>
        private const int StoreKindSpear = 2;
        /// <summary>戟兵装对应的 storeKind</summary>
        private const int StoreKindHalberd = 3;
        /// <summary>弩兵装对应的 storeKind</summary>
        private const int StoreKindCrossbow = 4;
        /// <summary>骑兵装对应的 storeKind</summary>
        private const int StoreKindHorse = 5;
        /// <summary>冲车（器械）对应的 storeKind</summary>
        private const int StoreKindRam = 6;
        /// <summary>井阑（器械）对应的 storeKind</summary>
        private const int StoreKindSiegeTower = 7;
        /// <summary>楼船（水面部队）对应的 storeKind</summary>
        private const int StoreKindTowerShip = 8;
        #endregion

        #region 回调与补给数据
        /// <summary>点击“决定”时的回调，参数为(兵装容器, 资金, 兵粮, 兵力)</summary>
        private Action<ItemStore, int, int, int> sureAction;
        /// <summary>点击“返回”时的回调</summary>
        private Action cancelAction;
        /// <summary>源部队（运输队，提供补给的一方）</summary>
        private Troop srcTroop;
        /// <summary>目标部队（接收补给的一方）</summary>
        private Troop targetTroop;
        /// <summary>当前设定的补给量：兵粮</summary>
        private int food;
        /// <summary>当前设定的补给量：资金</summary>
        private int gold;
        /// <summary>当前设定的补给量：兵力（士兵）</summary>
        private int troops;
        /// <summary>兵装补给容器，仅记录要转移的兵装数量（由 troops 与兵种成本联动计算）</summary>
        private ItemStore itemStore = new ItemStore();
        #endregion

        #region 源部队信息显示（由 Prefab 绑定）
        public Text srcNameText;       // 部队名
        public Text srcMoraleText;     // 士气
        public Text srcSoldiersText;   // 士兵
        public Text srcGoldText;       // 资金
        public Text srcFoodText;       // 兵粮
        public Text srcSpearText;      // 枪（仅显示，随士兵联动）
        public Text srcHalberdText;    // 戟（仅显示，随士兵联动）
        public Text srcCrossbowText;   // 弩（仅显示，随士兵联动）
        public Text srcHorseText;      // 马（仅显示，随士兵联动）
        public Text srcRamText;         // 冲车（器械，仅显示，随士兵联动）
        public Text srcSiegeTowerText;  // 井阑（器械，仅显示，随士兵联动）
        public Text srcTowerShipText;   // 楼船（水面部队，仅显示，随士兵联动）
        // 五维能力，顺序固定为：统率、武力、智力、政治、魅力
        public UIStatusItem srcStatuItem;
        #endregion

        #region 目标部队信息显示（由 Prefab 绑定）
        public Text targetNameText;
        public Text targetMoraleText;
        public Text targetSoldiersText;
        public Text targetGoldText;
        public Text targetFoodText;
        public UIBuildingTypeItem targetLandTypeItem;
        public UIBuildingTypeItem targetWaterypeItem;

        // 五维能力，顺序固定为：统率、武力、智力、政治、魅力
        public UIStatusItem targetStatuItem;

        #endregion

        #region 补给滑块与数值显示（兵装无独立滑动条，随士兵联动）
        public Slider foodSlider; public Text foodValueText; public Text srcfoodValueText; public Text srcfoodDaysText; public Text targetfoodDaysText;
        public Slider goldSlider; public Text goldValueText; public Text srcgoldValueText;
        public Slider troopsSlider; public Text troopsValueText; public Text srctroopsValueText;
        #endregion

        #region 底部按钮（由 Prefab 绑定）
        public Button maxButton;   // 最大补给
        public Button resetButton; // 初始化
        public Button sureButton;  // 决定
        public Button cancelButton;// 返回
        #endregion

        /// <summary>
        /// 窗口打开时初始化并显示。
        /// objects[0]=源部队, objects[1]=目标部队, objects[2]=决定回调, objects[3]=返回回调
        /// </summary>
        public override void OnOpen(params object[] objects)
        {
            if (objects == null || objects.Length < 4)
            {
                Sango.Log.Error("打开部队补给界面时参数缺失");
                return;
            }
            srcTroop = (Troop)objects[0];
            targetTroop = (Troop)objects[1];
            sureAction = (Action<ItemStore, int, int, int>)objects[2];
            cancelAction = (Action)objects[3];

            // 清空补给数据并归零
            itemStore.Clear();
            food = 0;
            gold = 0;
            troops = 0;
            srcStatuItem.SetTroop(srcTroop);
            targetLandTypeItem.SetTroopType(targetTroop.LandTroopType);
            targetWaterypeItem.SetTroopType(targetTroop.WaterTroopType);
            targetStatuItem.SetTroop(targetTroop);
            SetText(srcMoraleText, srcTroop.morale);
            SetText(targetNameText, targetTroop.Name);
            SetText(srcNameText, srcTroop.Name);
            BindButtonEvents();
            InitSliders();
            RefreshAll();

            Sango.Log.Info("打开部队补给界面");
        }

        /// <summary>绑定底部四个按钮的点击事件</summary>
        private void BindButtonEvents()
        {
            if (maxButton != null)
            {
                maxButton.onClick.RemoveAllListeners();
                maxButton.onClick.AddListener(OnMaxSupply);
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnReset);
            }
            if (sureButton != null)
            {
                sureButton.onClick.RemoveAllListeners();
                sureButton.onClick.AddListener(OnSure);
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancel);
            }
        }

        /// <summary>初始化各补给滑块的取值范围，并订阅数值变化事件</summary>
        private void InitSliders()
        {
            // 兵粮、资金上限为源部队现有数量
            SetupSlider(foodSlider, OnFoodChanged, srcTroop != null ? srcTroop.food : 0);
            SetupSlider(goldSlider, OnGoldChanged, srcTroop != null ? srcTroop.gold : 0);
            // 士兵上限受“源部队现有士兵”与“兵装可装备的最大士兵数”共同限制
            SetupSlider(troopsSlider, OnTroopsChanged, GetMaxEquippableSoldiers());
        }

        /// <summary>设置单个滑块：移除旧监听、设置范围、归零（不触发监听）、添加新监听</summary>
        private void SetupSlider(Slider slider, UnityEngine.Events.UnityAction<float> onChanged, int maxValue)
        {
            if (slider == null) return;
            slider.onValueChanged.RemoveAllListeners();
            slider.minValue = 0;
            slider.maxValue = UnityEngine.Mathf.Max(0, maxValue);
            // 先归零，此时尚未添加监听，不会触发回调
            slider.value = 0;
            slider.onValueChanged.AddListener(onChanged);
        }

        /// <summary>
        /// 计算“受兵装限制可装备的最大士兵数”。
        /// 上限 = min(源部队现有士兵, 按目标陆地兵种消耗推导上限, 按目标水面兵种消耗推导上限)。
        /// 补给时需同时判断陆地与水上的需求，士兵补给数被限制在两者的消耗范围内。
        /// </summary>
        private int GetMaxEquippableSoldiers()
        {
            if (srcTroop == null) return 0;
            int limit = srcTroop.troops;
            if (targetTroop == null) return limit;
            // 同时判断陆地与水上兵种的消耗需求，取两者限制下的较小值
            limit = LimitByCostItems(limit, targetTroop.LandTroopType);
            limit = LimitByCostItems(limit, targetTroop.WaterTroopType);
            limit = System.Math.Min(limit, targetTroop.MaxTroops - targetTroop.troops);
            return limit;
        }

        /// <summary>
        /// 按照指定兵种类型的 costItems，用源部队兵装推算可装备士兵上限，
        /// 取当前 limit 与该兵种限制之间的较小值；兵种无效时直接返回原 limit。
        /// </summary>
        private int LimitByCostItems(int limit, TroopType troopType)
        {
            if (troopType == null || troopType.costItems == null || troopType.costItems.Length == 0)
                return limit;
            return srcTroop.itemStore.CheckCostMin(troopType.costItems, limit);
        }

        /// <summary>
        /// 根据“当前转移士兵数”与目标部队陆地/水面兵种的 costItems 联动计算兵装需求量，
        /// 并写入兵装补给容器 itemStore（每1000士兵消耗 costPer1000，需求量 = costPer1000 * troops / 1000）。
        /// 补给时同时判断陆地与水上的需求，累加两类兵装的需求量；
        /// 器械部队(冲车/井阑)与水面部队(楼船)同样通过 costItems 联动计算。
        /// </summary>
        private void UpdateSupplyItemStore()
        {
            itemStore.Clear();
            if (troops <= 0 || targetTroop == null) return;
            TroopType landType = targetTroop.LandTroopType;
            TroopType waterType = targetTroop.WaterTroopType;
            // 累加陆地兵种需求
            AccumulateCostItems(landType);
            // 水面兵种与陆地兵种不相同时，再累加水面兵种需求（避免与陆地兵种重复计算）
            if (waterType != null && waterType != landType)
                AccumulateCostItems(waterType);
        }

        /// <summary>
        /// 将单个兵种类型的 costItems 转换为兵装需求量并累加到 itemStore。
        /// </summary>
        private void AccumulateCostItems(TroopType troopType)
        {
            if (troopType == null || troopType.costItems == null || troopType.costItems.Length == 0) return;
            int[] costItems = troopType.costItems;
            for (int i = 0; i < costItems.Length; i += 2)
            {
                int itemId = costItems[i];
                int costPer1000 = costItems[i + 1];
                int need = costPer1000 * troops / 1000;
                if (need > 0)
                    itemStore.Add(itemId, need);
            }
        }

        /// <summary>刷新所有显示：滑块数值文本 + 两侧部队信息</summary>
        private void RefreshAll()
        {
            RefreshSliderTexts();
            RefreshPanels();
        }

        /// <summary>刷新所有滑块旁的数值文本</summary>
        private void RefreshSliderTexts()
        {
            SetText(foodValueText, food);
            SetText(goldValueText, gold);
            SetText(troopsValueText, troops);
        }

        void SetItemKindType(Text text, int stroeKind)
        {
            int num = itemStore.GetNumber(stroeKind);
            int have = srcTroop.GetItemNumber(stroeKind);
            if (num > 0)
                SetText(text, $"{have}→{have - num}");
            else
                SetText(text, have);
        }

        /// <summary>刷新源/目标两侧部队的数值显示（实时反映当前补给量）</summary>
        private void RefreshPanels()
        {
            if (srcTroop != null)
            {
                // 源部队：基础值 - 已分配（兵装 = 实际兵装 - 已转移兵装）
                SetText(srcSoldiersText, srcTroop.troops - troops);
                SetText(srcGoldText, srcTroop.gold - gold);
                SetText(srcFoodText, srcTroop.food - food);

                SetItemKindType(srcSpearText, StoreKindSpear);
                SetItemKindType(srcHalberdText, StoreKindHalberd);
                SetItemKindType(srcCrossbowText, StoreKindCrossbow);
                SetItemKindType(srcHorseText, StoreKindHorse);
                SetItemKindType(srcRamText, StoreKindRam);
                SetItemKindType(srcSiegeTowerText, StoreKindSiegeTower);
                SetItemKindType(srcTowerShipText, StoreKindTowerShip);
            }

            if (targetTroop != null)
            {
                // 目标部队：基础值 + 已分配（兵装 = 应装备数 + 已转移兵装）
                int dstMorale = (srcTroop.morale * troops + targetTroop.morale * targetTroop.troops) / (targetTroop.troops + troops);
                SetText(targetMoraleText,  $"{targetTroop.morale}→{dstMorale}" );
                SetText(targetSoldiersText, targetTroop.troops + troops);
                SetText(targetGoldText, targetTroop.gold + gold);
                SetText(targetFoodText, targetTroop.food + food);
            }

            int foodCost = srcTroop.PrepeareFoodCost(srcTroop.troops - troops);
            int turnCount = (int)((srcTroop.food - food) / foodCost);
            srcfoodDaysText.text = $"{turnCount * 10}日";

            foodCost = targetTroop.PrepeareFoodCost(targetTroop.troops + troops);
            turnCount = (int)((targetTroop.food + food) / foodCost);
            targetfoodDaysText.text = $"{turnCount * 10}日";

            foodValueText.text = $"{targetTroop.food}→{targetTroop.food + food}";
            goldValueText.text = $"{targetTroop.gold}→{targetTroop.gold + gold}";
            troopsValueText.text = $"{targetTroop.troops}→{targetTroop.troops + troops}";
            srcfoodValueText.text = $"{srcTroop.food}→{srcTroop.food - food}";
            srcgoldValueText.text = $"{srcTroop.gold}→{srcTroop.gold - gold}";
            srctroopsValueText.text = $"{srcTroop.troops}→{srcTroop.troops - troops}";
        }

        /// <summary>安全设置文本（整型）</summary>
        private void SetText(Text text, int value)
        {
            if (text != null) text.text = value.ToString();
        }

        /// <summary>安全设置文本（字符串）</summary>
        private void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        #region 滑块变化回调
        /// <summary>兵粮滑块变化</summary>
        private void OnFoodChanged(float value)
        {
            food = (int)value;
            SetText(foodValueText, food);
            RefreshPanels();
        }

        /// <summary>资金滑块变化</summary>
        private void OnGoldChanged(float value)
        {
            gold = (int)value;
            SetText(goldValueText, gold);
            RefreshPanels();
        }

        /// <summary>兵力滑块变化：同步联动计算需要补给的兵装数量</summary>
        private void OnTroopsChanged(float value)
        {
            troops = (int)value;
            UpdateSupplyItemStore();
            SetText(troopsValueText, troops);
            RefreshPanels();
        }
        #endregion

        /// <summary>“最大补给”：兵粮/资金拉满，兵力拉至“兵装可装备的最大士兵数”</summary>
        private void OnMaxSupply()
        {
            if (foodSlider != null) foodSlider.value = foodSlider.maxValue;
            if (goldSlider != null) goldSlider.value = goldSlider.maxValue;
            if (troopsSlider != null) troopsSlider.value = troopsSlider.maxValue;
            RefreshPanels();
        }

        /// <summary>“初始化”：将所有滑块归零（兵装随之归零）</summary>
        private void OnReset()
        {
            if (foodSlider != null) foodSlider.value = 0;
            if (goldSlider != null) goldSlider.value = 0;
            if (troopsSlider != null) troopsSlider.value = 0;
            RefreshPanels();
        }

        /// <summary>“返回”：执行返回回调并关闭窗口</summary>
        public void OnCancel()
        {
            Close();
            cancelAction?.Invoke();
            //Back();
        }

        /// <summary>“决定”：将当前补给数据交由外部回调处理并关闭窗口</summary>
        public void OnSure()
        {
            Close();
            sureAction?.Invoke(itemStore, gold, food, troops);
        }
    }
}
