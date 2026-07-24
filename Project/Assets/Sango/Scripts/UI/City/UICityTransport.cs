using TKNewtonsoft.Json.Utilities.LinqBridge;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sango.Core;
namespace Sango.UI
{
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

        CityTransport cityTransportSys;
        Dictionary<int, UIItemType> id2UIItemType = new Dictionary<int, UIItemType>();
        City targetCity;
        Troop targetTroop;
        public override void OnOpen()
        {
            showLand = true; 
            cityTransportSys = GameSystem.GetSystem<CityTransport>();
            targetCity = cityTransportSys.TargetCity;
            targetTroop = cityTransportSys.TargetTroop;

            itemTypeRect.onItemTypeShow = OnItemTypeShow;
            itemTypeSliderRect.onItemTypeShow = OnItemTypeSliderShow;
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.MakeTansport)}/{cityTransportSys.TargetCity.BelongCorps.ActionPoint}";

            UpdateContent();
        }

        void OnItemTypeShow(ItemType itemType, UIItemType uIItemType)
        {
            id2UIItemType.Add(itemType.Id, uIItemType);
            uIItemType.SetItemType(itemType);
            uIItemType.SetNumber(targetCity.itemStore.GetNumber(itemType));
            int troopHas = targetTroop.itemStore.GetNumber(itemType);
            uIItemType.SetUsed(troopHas);
        }

        void OnItemTypeSliderShow(ItemType itemType, UIItemTypeSlider uIItemTypeSlider)
        {
            int itemNumber = targetCity.itemStore.GetNumber(itemType);
            if (itemNumber <= 0)
            {
                uIItemTypeSlider.SetValid(false);
                uIItemTypeSlider.numberSlider.SetValueWithoutNotify(0);
                uIItemTypeSlider.numberLabel.text = "0/0";
                return;
            }
            uIItemTypeSlider.SetValid(true);

            int troopHas = targetTroop.itemStore.GetNumber(itemType);
            uIItemTypeSlider.numberSlider.SetValueWithoutNotify(troopHas / itemNumber);
            uIItemTypeSlider.numberLabel.text = $"{troopHas}/{itemNumber}";
            uIItemTypeSlider.numberSlider.onValueChanged.RemoveAllListeners();
            uIItemTypeSlider.numberSlider.onValueChanged.AddListener((p) =>
            {
                int num = (int)Math.Ceiling(itemNumber * p);
                uIItemTypeSlider.numberLabel.text = $"{num}/{itemNumber}";
                targetTroop.itemStore.Set(itemType, num);

                UIItemType uIItemType = id2UIItemType[itemType.Id];
                uIItemType.SetUsed(num);
            });
        }

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

        public void OpenNumberPanel_troops()
        {

        }

        public void OpenNumberPanel_gold()
        {

        }

        public void OpenNumberPanel_food()
        {

        }

        public void OnTroopsSliderValueChanged(float p)
        {
            if (cityTransportSys.personList.Count == 0)
                return;

            int troop = (int)Math.Ceiling(targetCity.troops * p);
            targetTroop.troops = troop;
            UpdateTroopsInfo();
        }

        public void OnGoldSliderValueChanged(float p)
        {
            if (cityTransportSys.personList.Count == 0)
                return;

            int gold = (int)Math.Ceiling(targetCity.gold * p);
            targetTroop.gold = gold;
            UpdateTroopsInfo();
        }

        public void OnFoodSliderValueChanged(float p)
        {
            if (cityTransportSys.personList.Count == 0)
                return;

            int food = (int)Math.Ceiling(targetCity.food * p);
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
            troopsSlider.SetValueWithoutNotify((float)targetTroop.troops / targetCity.troops);
            goldSlider.SetValueWithoutNotify((float)targetTroop.gold / targetCity.gold);
            foodSlider.SetValueWithoutNotify((float)targetTroop.food / targetCity.food);
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
                itemLabels[showIndex].SetTitle(itemType.Name);
                SetItemLabel(itemLabels[showIndex], has, use);
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

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(cityTransportSys.TargetCity.freePersons,
                cityTransportSys.personList, 3, OnPersonChange, cityTransportSys.customTitleList, cityTransportSys.customTitleName);
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
