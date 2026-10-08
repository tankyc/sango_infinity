using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UICityExpedition : UGUIWindow
    {
        public UIBuildingTypeItem objUIBuildingTypeItemLand;
        public UIBuildingTypeItem objUIBuildingTypeItemWater;
        List<UIBuildingTypeItem> landTroopTypePool = new List<UIBuildingTypeItem>();
        List<UIBuildingTypeItem> waterTroopTypePool = new List<UIBuildingTypeItem>();
        public UIStatusItem statusItem;

        public UIPersonItem[] personItems;

        public UITextField landTroopTypeDescLabel;
        public UITextField waterTroopTypeDescLabel;

        public Button [] autoMakeButtons;

        public UITextField troopsLabel;
        public UITextField goldLabel;
        public UITextField foodLabel;
        public UITextField dayTurnLabel;


        //public UITextField atkLaebl;
        //public UITextField defLaebl;
        //public UITextField intLaebl;
        //public UITextField buildLaebl;
        //public UITextField moveLaebl;
        public UITextField typeLaebl;
        public UITextField abilityLaebl;
        public UITextField energyLaebl;
        public UITextField[] skillLabel;

        public UITextField[] itemLabels;

        public UITextField itemTroopsLabel;
        public UITextField itemGoldLabel;
        public UITextField itemFoodLabel;


        public UITextField action_value;

        public Slider troopsSlider;
        public Slider goldSlider;
        public Slider foodSlider;

        /// <summary>推荐编队面板（默认关闭，由 bg/select 按钮打开）</summary>
        public GameObject recomandObj;

        /// <summary>推荐队伍列表项模板（池化复制的源节点，自身不显示）</summary>
        public UITroopTeamItem teamTemplate;

        /// <summary>页码 toggle（每个 toggle 对应一页）</summary>
        public Toggle[] pageToggles;

        /// <summary>推荐队伍每页最多显示的数量</summary>
        public const int TeamCountInPage = 30;

        /// <summary>推荐队伍列表项池</summary>
        List<UITroopTeamItem> teamItemPool = new List<UITroopTeamItem>();

        /// <summary>推荐队伍当前页码</summary>
        int curTeamPage;

        /// <summary>推荐队伍当前页码（从 0 开始）</summary>
        public int CurTeamPage { get { return curTeamPage; } }

        CityExpedition cityExpeditionSys;

        bool showLand = true;
        City targetCity;
        Troop targetTroop;
        public override void OnOpen()
        {

            cityExpeditionSys = GameSystem.GetSystem<CityExpedition>();
            targetCity = cityExpeditionSys.TargetCity;
            targetTroop = cityExpeditionSys.TargetTroop;
            showLand = true;

            int slotLength = cityExpeditionSys.ActivedLandTroopTypes.Count;
            while (landTroopTypePool.Count < slotLength)
            {
                GameObject go = GameObject.Instantiate(objUIBuildingTypeItemLand.gameObject, objUIBuildingTypeItemLand.transform.parent);
                UIBuildingTypeItem cityBuildingSlot = go.GetComponent<UIBuildingTypeItem>();
                landTroopTypePool.Add(cityBuildingSlot);
                cityBuildingSlot.onSelected = OnSelectLandType;

                go.SetActive(true);
            }

            for (int i = slotLength; i < landTroopTypePool.Count; i++)
                landTroopTypePool[i].gameObject.SetActive(false);

            for(int i = 0; i < autoMakeButtons.Length; i++)
                autoMakeButtons[i].interactable = false;

            for (int i = 0; i < slotLength; i++)
            {
                TroopType troopType = cityExpeditionSys.ActivedLandTroopTypes[i];
                UIBuildingTypeItem cityBuildingSlot = landTroopTypePool[i];
                cityBuildingSlot.SetTroopType(troopType).SetIndex(i).SetSelected(cityExpeditionSys.CurSelectLandTrropTypeIndex == i);
                bool enoughItems = cityExpeditionSys.TargetCity.itemStore.CheckItemEnough(troopType.costItems, 1);
                cityBuildingSlot.SetValid(enoughItems);
                int destKind = troopType.kind - 2;
                if (enoughItems && destKind >= 0 && destKind < autoMakeButtons.Length)
                    autoMakeButtons[destKind].interactable = true;
            }

            slotLength = cityExpeditionSys.ActivedWaterTroopTypes.Count;
            while (waterTroopTypePool.Count < slotLength)
            {
                GameObject go = GameObject.Instantiate(objUIBuildingTypeItemWater.gameObject, objUIBuildingTypeItemWater.transform.parent);
                UIBuildingTypeItem cityBuildingSlot = go.GetComponent<UIBuildingTypeItem>();
                waterTroopTypePool.Add(cityBuildingSlot);
                cityBuildingSlot.onSelected = OnSelectWaterType;
                go.SetActive(true);
            }

            for (int i = slotLength; i < waterTroopTypePool.Count; i++)
                waterTroopTypePool[i].gameObject.SetActive(false);

            for (int i = 0; i < slotLength; i++)
            {
                TroopType troopType = cityExpeditionSys.ActivedWaterTroopTypes[i];
                UIBuildingTypeItem cityBuildingSlot = waterTroopTypePool[i];
                cityBuildingSlot.SetTroopType(troopType).SetIndex(i).SetSelected(cityExpeditionSys.CurSelectWaterTrropTypeIndex == i);
                cityBuildingSlot.SetValid(cityExpeditionSys.TargetCity.itemStore.CheckItemEnough(troopType.costItems, 1));
            }

            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.MakeTroop)}/{cityExpeditionSys.TargetCity.BelongCorps.ActionPoint}";

            // 推荐编队面板默认关闭，由 select 按钮打开
            if (recomandObj != null) recomandObj.SetActive(false);
            // 推荐队伍模板只作为池化复制的源，自身不参与显示
            if (teamTemplate != null) teamTemplate.gameObject.SetActive(false);
            InitPageToggles();

            UpdateContent();
        }

        /// <summary>
        /// 退出
        /// </summary>
        public void OnCancel()
        {
            cityExpeditionSys.Done();
        }

        public void OnOK()
        {
            cityExpeditionSys.DoJob();
        }

        public void OnBuildTroopType1()
        {

            UIBuildingTypeItem cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(false);
            cityExpeditionSys.AutoMakeTroop(2);
            cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(true);


            UpdateContent();
        }

        public void OnBuildTroopType2()
        {
            UIBuildingTypeItem cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(false);
            cityExpeditionSys.AutoMakeTroop(3);
            cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(true);

            UpdateContent();
        }

        public void OnBuildTroopType3()
        {
            UIBuildingTypeItem cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(false);
            cityExpeditionSys.AutoMakeTroop(4);
            cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(true);

            UpdateContent();
        }

        public void OnBuildTroopType4()
        {
            UIBuildingTypeItem cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(false);
            cityExpeditionSys.AutoMakeTroop(5);
            cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(true);

            UpdateContent();
        }

        public void OnBuildTroopType5()
        {
            UIBuildingTypeItem cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(false);
            cityExpeditionSys.AutoMakeBuildTroop();
            cityBuildingSlot = landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex];
            cityBuildingSlot.SetSelected(true);

            UpdateContent();
        }

        /// <summary>
        /// 自动编队（绑定在 bg/auto 按钮上）
        /// 与 AI 出征同一套思路：优先套用本城能立刻凑齐的推荐队伍，否则按适性选兵种再按顾问推荐挑人
        /// </summary>
        public void OnAutoMakeFormation()
        {
            if (cityExpeditionSys == null) return;

            if (!cityExpeditionSys.AutoMakeFormation())
            {
                Log.Warning("出征: 自动编队失败,本城没有可用武将");
                return;
            }

            SyncTroopTypeSelect();
            UpdateContent();

            Log.Info("出征: 自动编队完成");
        }

        /// <summary>
        /// 同步陆地 / 水上兵种槽位的选中态
        /// </summary>
        void SyncTroopTypeSelect()
        {
            for (int i = 0; i < landTroopTypePool.Count; i++)
                landTroopTypePool[i].SetSelected(cityExpeditionSys.CurSelectLandTrropTypeIndex == i);
            for (int i = 0; i < waterTroopTypePool.Count; i++)
                waterTroopTypePool[i].SetSelected(cityExpeditionSys.CurSelectWaterTrropTypeIndex == i);
        }

        /// <summary>
        /// 是否允许打开数值输入器(与滑块一致: 未选择武将时不能调整)
        /// </summary>
        /// <returns>可以打开返回true</returns>
        bool CanOpenNumberPanel()
        {
            if (cityExpeditionSys == null || targetCity == null || targetTroop == null)
            {
                return false;
            }
            if (cityExpeditionSys.personList.Count == 0)
            {
                Log.Warning("出征: 请先选择武将,再设置士兵/资金/兵粮数量");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 计算可携带士兵的上限 - 规则与滑块拖动时保持一致
        /// 依次受限于: 部队最大兵力 → 城内兵力 → 兵装可支撑的兵力
        /// </summary>
        /// <returns>可携带士兵上限</returns>
        int GetMaxTroops()
        {
            int max = targetTroop.MaxTroops;
            max = System.Math.Min(max, targetCity.troops);
            max = targetCity.itemStore.CheckCostMin(targetTroop.LandTroopType.costItems, max);
            max = targetCity.itemStore.CheckCostMin(targetTroop.WaterTroopType.costItems, max);
            return System.Math.Max(0, max);
        }

        /// <summary>
        /// 应用携带士兵数 - 同时按士兵数推算随军兵粮(与滑块同一套规则)
        /// </summary>
        /// <param name="troops">期望携带的士兵数</param>
        void ApplyTroops(int troops)
        {
            troops = System.Math.Max(1, troops);
            troops = System.Math.Min(troops, targetCity.troops);
            troops = targetCity.itemStore.CheckCostMin(targetTroop.LandTroopType.costItems, troops);
            troops = targetCity.itemStore.CheckCostMin(targetTroop.WaterTroopType.costItems, troops);

            int wonderFood = (int)(troops * Scenario.Cur.Variables.baseFoodCostInTroop * 20);
            int food = System.Math.Min(wonderFood, targetCity.food);
            targetTroop.food = food;
            targetTroop.troops = troops;

            UpdateTroopsInfo();
        }

        /// <summary>
        /// 士兵数值按钮 - 打开数值输入器精确设置携带的士兵数
        /// 取值范围: 1 ~ min(部队最大兵力, 城内兵力, 兵装可支撑的兵力)
        /// </summary>
        public void OpenNumberPanel_troops()
        {
            if (!CanOpenNumberPanel())
            {
                return;
            }

            int current = targetTroop.troops;
            int max = GetMaxTroops();
            // 当前值可能超过上限(更换兵种/武将后的残留数据),保证不会被输入器压回去
            if (max < current) max = current;

            Window.Instance.Open("window_calculator", "士兵", current, 1, max,
                (Action<int>)((val) => ApplyTroops(val)),
                null);
        }

        /// <summary>
        /// 资金数值按钮 - 打开数值输入器精确设置携带的资金
        /// 取值范围: 0 ~ 城内资金
        /// </summary>
        public void OpenNumberPanel_gold()
        {
            if (!CanOpenNumberPanel())
            {
                return;
            }

            int current = targetTroop.gold;
            int max = targetCity.gold;
            if (max < current) max = current;

            Window.Instance.Open("window_calculator", "资金", current, 0, max,
                (Action<int>)((val) =>
                {
                    targetTroop.gold = val;
                    UpdateTroopsInfo();
                }),
                null);
        }

        /// <summary>
        /// 兵粮数值按钮 - 打开数值输入器精确设置携带的兵粮
        /// 取值范围: 0 ~ 城内兵粮
        /// </summary>
        public void OpenNumberPanel_food()
        {
            if (!CanOpenNumberPanel())
            {
                return;
            }

            int current = targetTroop.food;
            int max = targetCity.food;
            if (max < current) max = current;

            Window.Instance.Open("window_calculator", "兵粮", current, 0, max,
                (Action<int>)((val) =>
                {
                    targetTroop.food = val;
                    UpdateTroopsInfo();
                }),
                null);
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

        public void OnTroopsSliderValueChanged(float p)
        {
            if (cityExpeditionSys.personList.Count == 0)
                return;

            int troop = (int)System.Math.Ceiling(targetTroop.MaxTroops * p);
            // 后续的上下限与兵装限制统一在ApplyTroops里处理
            ApplyTroops(troop);
        }

        public void OnGoldSliderValueChanged(float p)
        {
            if (cityExpeditionSys.personList.Count == 0)
                return;

            int gold = (int)Math.Ceiling(targetCity.gold * p);
            targetTroop.gold = gold;
            UpdateTroopsInfo();
        }

        public void OnFoodSliderValueChanged(float p)
        {
            if (cityExpeditionSys.personList.Count == 0)
                return;

            int food = (int)Math.Ceiling(targetCity.food * p);
            targetTroop.food = food;
            UpdateTroopsInfo();
        }

        public void UpdateContent()
        {
            for (int i = 0; i < personItems.Length; ++i)
            {
                if (i < cityExpeditionSys.personList.Count)
                    personItems[i].SetPerson(cityExpeditionSys.personList[i]);
                else
                    personItems[i].SetPerson(null);
            }

            UpdateTroopStatus();
            UpdateTroopTypeDesc();
            UpdateTroopsInfo();
        }

        /// <summary>
        /// 刷新陆地/水上兵种的说明文本
        /// 选择陆地或水上兵种后会同步显示其说明
        /// </summary>
        void UpdateTroopTypeDesc()
        {
            if (landTroopTypeDescLabel != null)
            {
                TroopType landTroopType = targetTroop.LandTroopType;
                landTroopTypeDescLabel.text = landTroopType != null ? landTroopType.desc : "";
            }

            if (waterTroopTypeDescLabel != null)
            {
                TroopType waterTroopType = targetTroop.WaterTroopType;
                waterTroopTypeDescLabel.text = waterTroopType != null ? waterTroopType.desc : "";
            }
        }

        void UpdateTroopStatus()
        {
            int atk, def, intel, build, move;
            bool hasPeson = cityExpeditionSys.personList.Count > 0;
            Troop targetTroop = cityExpeditionSys.TargetTroop;
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
                abilityLaebl.text = Scenario.Cur.Variables.GetAbilityName(targetTroop.LandTroopTypeLv);
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
                abilityLaebl.text = Scenario.Cur.Variables.GetAbilityName(targetTroop.WaterTroopTypeLv);
            }


            statusItem.SetTroopStatus(atk, def, intel, build, move);

            //atkLaebl.text = atk.ToString();
            //defLaebl.text = def.ToString();
            //intLaebl.text = intel.ToString();
            //buildLaebl.text = build.ToString();
            //moveLaebl.text = move.ToString();
            energyLaebl.text = targetTroop.morale.ToString();
        }

        void SetItemLabel(UITextField label, int all, int ues)
        {
            int left = all - ues;
            if (left > 0)
            {
                if (ues == 0)
                    label.text = all.ToString();
                else
                    label.text = $"{all}→{left}";
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

            troopsSlider.SetValueWithoutNotify((float)targetTroop.troops / targetTroop.MaxTroops);
            troopsLabel.text = $"{targetTroop.troops}/{targetTroop.MaxTroops}";
            goldLabel.text = $"{targetTroop.gold}/{targetCity.gold}";
            foodLabel.text = $"{targetTroop.food}/{targetCity.food}";
            int foodCost = targetTroop.PrepeareFoodCost();
            int turnCount = (int)(targetTroop.food / foodCost);
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
                        if (itemId == itemType.storeKind)
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
                        if (itemId == itemType.storeKind)
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

            UpdateTroopTypeItemNums();
        }

        /// <summary>
        /// 刷新兵种槽位上的兵装库存数量
        /// 陆地/水源两列兵种分别按当前选中的兵种计算本次出征的兵装消耗，
        /// 槽位显示扣除消耗后的库存数量，数量发生变化时用黄色提示，未变化则恢复白色
        /// </summary>
        void UpdateTroopTypeItemNums()
        {
            for (int i = 0; i < landTroopTypePool.Count && i < cityExpeditionSys.ActivedLandTroopTypes.Count; i++)
            {
                UpdateTroopTypeItemNum(landTroopTypePool[i],
                    cityExpeditionSys.ActivedLandTroopTypes[i],
                    cityExpeditionSys.CurSelectLandTrropTypeIndex == i);
            }

            for (int i = 0; i < waterTroopTypePool.Count && i < cityExpeditionSys.ActivedWaterTroopTypes.Count; i++)
            {
                UpdateTroopTypeItemNum(waterTroopTypePool[i],
                    cityExpeditionSys.ActivedWaterTroopTypes[i],
                    cityExpeditionSys.CurSelectWaterTrropTypeIndex == i);
            }
        }

        /// <summary>
        /// 刷新单个兵种槽位上的兵装库存数量
        /// </summary>
        /// <param name="slot">兵种槽位</param>
        /// <param name="troopType">兵种类型</param>
        /// <param name="isSelected">该兵种是否为当前选中的兵种</param>
        void UpdateTroopTypeItemNum(UIBuildingTypeItem slot, TroopType troopType, bool isSelected)
        {
            if (slot == null || troopType == null) return;

            // 该兵种没有兵装需求时不显示数量
            if (troopType.costItems == null || troopType.costItems.Length < 2)
            {
                slot.SetNum(-1, false);
                return;
            }

            // 库存数量
            int storeKindId = troopType.costItems[0];
            int has = targetCity.itemStore.GetNumber(storeKindId);

            // 只有当前选中的兵种才会消耗兵装，未选中的兵种不产生消耗
            int use = 0;
            if (isSelected)
                use = troopType.costItems[1] * targetTroop.troops / 1000;

            // 显示扣除消耗后的数量，消耗大于0说明数量发生了变化(黄色)
            slot.SetNum(has - use, use > 0);
        }

        public void OnSelectWaterType(UIBuildingTypeItem buildingTypeItem)
        {
            if (cityExpeditionSys.CurSelectWaterTrropTypeIndex >= 0)
                waterTroopTypePool[cityExpeditionSys.CurSelectWaterTrropTypeIndex].SetSelected(false);
            cityExpeditionSys.CurSelectWaterTrropTypeIndex = buildingTypeItem.index;
            buildingTypeItem.SetSelected(true);
            
            TroopType troopType = cityExpeditionSys.ActivedWaterTroopTypes[buildingTypeItem.index];
            cityExpeditionSys.SetWaterType(troopType);
            
            UpdateContent();
        }

        public void OnSelectLandType(UIBuildingTypeItem buildingTypeItem)
        {
            if (cityExpeditionSys.CurSelectLandTrropTypeIndex >= 0)
                landTroopTypePool[cityExpeditionSys.CurSelectLandTrropTypeIndex].SetSelected(false);
            cityExpeditionSys.CurSelectLandTrropTypeIndex = buildingTypeItem.index;
            buildingTypeItem.SetSelected(true);
            
            TroopType troopType = cityExpeditionSys.ActivedLandTroopTypes[buildingTypeItem.index];
            cityExpeditionSys.SetLandType(troopType);
            
            UpdateContent();
        }

        public void OnPersonChange(List<Person> personList)
        {
            cityExpeditionSys.personList = personList;
            cityExpeditionSys.UpdateJobValue();

            UpdateContent();
        }

        /// <summary>打开出征选将界面，默认按统率倒序展示，统率最高的武将排在首屏。</summary>
        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(cityExpeditionSys.TargetCity.freePersons,
                cityExpeditionSys.personList, 3, OnPersonChange, cityExpeditionSys.customTitleList, cityExpeditionSys.customTitleName,
                CityExpedition.CommandSortTitleIndex);
        }

        // ============================ 推荐编队 ============================

        /// <summary>
        /// 打开 / 关闭推荐编队面板（绑定在 bg/select 按钮上）
        /// </summary>
        public void OnToggleRecomand()
        {
            if (recomandObj == null) return;

            if (recomandObj.activeSelf)
                OnCloseRecomand();
            else
                OnOpenRecomand();
        }

        /// <summary>
        /// 打开推荐编队面板
        /// </summary>
        public void OnOpenRecomand()
        {
            if (recomandObj == null)
            {
                Log.Warning("出征: 推荐编队节点未绑定,无法打开");
                return;
            }

            recomandObj.SetActive(true);
            RefreshTeamList();
        }

        /// <summary>
        /// 关闭推荐编队面板（套用队伍后自动调用，也可绑到面板的返回 / 确定按钮上）
        /// </summary>
        public void OnCloseRecomand()
        {
            if (recomandObj != null)
                recomandObj.SetActive(false);
        }

        /// <summary>
        /// 快捷保存当前队伍（绑定在 bg/save 按钮上）
        /// 队伍按武将 ID 保存；已存在完全相同的武将组合时不重复保存（去重在逻辑层）
        /// </summary>
        public void OnSaveTeam()
        {
            if (cityExpeditionSys == null || targetCity == null) return;

            if (cityExpeditionSys.personList.Count == 0)
            {
                Log.Warning("出征: 请先选择武将,再保存队伍");
                return;
            }

            bool created;
            TroopTeam team = cityExpeditionSys.QuickSaveCurrentTeam(out created);
            if (team == null)
            {
                Log.Warning("出征: 保存队伍失败,队伍数量可能已达上限");
                return;
            }

            if (created)
                Log.Info("出征: 已保存队伍[" + team.name + "]");
            else
                Log.Info("出征: 已存在相同武将组合的队伍[" + team.name + "],未重复保存");

            // 推荐编队面板打开时同步刷新列表
            if (recomandObj != null && recomandObj.activeSelf)
                RefreshTeamList();
        }

        /// <summary>
        /// 绑定页码 toggle 的选中事件（每个 toggle 对应一页）
        /// </summary>
        void InitPageToggles()
        {
            if (pageToggles == null) return;

            for (int i = 0; i < pageToggles.Length; i++)
            {
                if (pageToggles[i] == null) continue;

                int pageIndex = i;
                pageToggles[i].onValueChanged.RemoveAllListeners();
                pageToggles[i].onValueChanged.AddListener(isOn =>
                {
                    if (isOn) ShowTeamPage(pageIndex);
                });
            }
        }

        /// <summary>
        /// 重新评估并刷新推荐队伍列表，回到第一页
        /// 候选数据由逻辑层统一维护（界面只负责显示与点击）
        /// </summary>
        void RefreshTeamList()
        {
            cityExpeditionSys.RefreshTeamCandidates();

            UpdatePageToggles();
            ShowTeamPage(0);
        }

        /// <summary>
        /// 刷新页码 toggle：按总页数显示 / 隐藏，并把页码写到 toggle 的标题上
        /// </summary>
        void UpdatePageToggles()
        {
            if (pageToggles == null) return;

            int maxPage = GetMaxTeamPage();
            for (int i = 0; i < pageToggles.Length; i++)
            {
                if (pageToggles[i] == null) continue;

                bool need = i < maxPage;
                pageToggles[i].gameObject.SetActive(need);
                if (!need) continue;

                UIToggleItem toggleItem = pageToggles[i].GetComponent<UIToggleItem>();
                if (toggleItem != null)
                    toggleItem.SetTitle((i + 1).ToString());
            }
        }

        /// <summary>
        /// 推荐队伍的总页数（每页最多 <see cref="TeamCountInPage"/> 个）
        /// </summary>
        int GetMaxTeamPage()
        {
            int count = cityExpeditionSys.teamCandidates.Count;
            if (count == 0) return 1;

            int maxPage = count / TeamCountInPage;
            if (count % TeamCountInPage != 0)
                maxPage++;
            return maxPage;
        }

        /// <summary>
        /// 显示指定页的推荐队伍
        /// </summary>
        /// <param name="page">页码（从 0 开始）</param>
        void ShowTeamPage(int page)
        {
            if (page < 0) page = 0;

            int maxPage = GetMaxTeamPage();
            if (page >= maxPage) page = maxPage - 1;
            curTeamPage = page;

            // 同步页码 toggle 的选中状态（值没变化时不会再次触发事件）
            if (pageToggles != null)
            {
                for (int i = 0; i < pageToggles.Length; i++)
                {
                    if (pageToggles[i] == null) continue;
                    pageToggles[i].isOn = (i == page);
                }
            }

            int start = page * TeamCountInPage;
            int count = System.Math.Min(TeamCountInPage, cityExpeditionSys.teamCandidates.Count - start);
            if (count < 0) count = 0;

            EnsureTeamItemPool(count);

            for (int i = 0; i < teamItemPool.Count; i++)
            {
                if (i < count)
                {
                    teamItemPool[i].gameObject.SetActive(true);
                    teamItemPool[i].SetIndex(start + i).SetData(cityExpeditionSys.teamCandidates[start + i]);
                }
                else
                {
                    teamItemPool[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 保证列表项池的数量足够（不够时复制模板节点）
        /// </summary>
        /// <param name="count">本次需要显示的数量</param>
        void EnsureTeamItemPool(int count)
        {
            if (teamTemplate == null) return;

            while (teamItemPool.Count < count)
            {
                GameObject go = GameObject.Instantiate(teamTemplate.gameObject, teamTemplate.transform.parent);
                UITroopTeamItem item = go.GetComponent<UITroopTeamItem>();
                if (item == null) return;

                item.onApply = OnApplyTeam;
                item.onDelete = OnDeleteTeam;
                teamItemPool.Add(item);
                go.SetActive(true);
            }
        }

        /// <summary>
        /// 套用推荐队伍：把它的成员与推荐兵种设置到当前编队（实际套用由逻辑层完成）
        /// </summary>
        /// <param name="item">被点击的列表项</param>
        void OnApplyTeam(UITroopTeamItem item)
        {
            if (item == null || item.candidate == null) return;

            // 不可用的队伍列表项已置灰，这里再兜一层
            if (!item.candidate.IsReady) return;

            if (!cityExpeditionSys.ApplyTeam(item.candidate.team))
            {
                Log.Warning("出征: 队伍[" + item.candidate.name + "]当前用不了");
                return;
            }

            SyncTroopTypeSelect();

            UpdateContent();
            OnCloseRecomand();

            Log.Info("出征: 已套用推荐队伍[" + item.candidate.name + "]");
        }

        /// <summary>
        /// 删除玩家自建的队伍（推荐模板只读，不能删除；实际删除由逻辑层完成）
        /// </summary>
        /// <param name="item">被点击的列表项</param>
        void OnDeleteTeam(UITroopTeamItem item)
        {
            if (item == null || item.candidate == null || !item.candidate.custom) return;

            // 逻辑层的删除接口按"当前选中"操作，这里先用名字定位到该队伍
            cityExpeditionSys.selectedTeamIndex = cityExpeditionSys.FindTeamIndex(item.candidate.name);
            if (!cityExpeditionSys.DeleteSelectedTeam())
            {
                Log.Warning("出征: 删除队伍[" + item.candidate.name + "]失败");
                return;
            }

            Log.Info("出征: 已删除队伍[" + item.candidate.name + "]");

            // 逻辑层删完已重建过候选，这里只刷新分页显示（停在当前页）
            UpdatePageToggles();
            ShowTeamPage(curTeamPage);
        }
    }
}
