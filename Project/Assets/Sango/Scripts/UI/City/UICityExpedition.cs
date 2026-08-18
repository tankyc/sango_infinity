using TKNewtonsoft.Json.Utilities.LinqBridge;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
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

            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.MakeTroop)}/{cityExpeditionSys.TargetCity.mBelongCorps.ActionPoint}";


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

        public void OpenNumberPanel_troops()
        {

        }

        public void OpenNumberPanel_gold()
        {

        }

        public void OpenNumberPanel_food()
        {

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

            int troop = (int)Math.Ceiling(targetTroop.MaxTroops * p);
            troop = Math.Max(1, troop);
            troop = Math.Min(troop, targetCity.troops);
            troop = targetCity.itemStore.CheckCostMin(targetTroop.LandTroopType.costItems, troop);
            troop = targetCity.itemStore.CheckCostMin(targetTroop.WaterTroopType.costItems, troop);

            int wonderFood = (int)(troop * Scenario.Cur.Variables.baseFoodCostInTroop * 20);
            int food = Math.Min(wonderFood, targetCity.food);
            targetTroop.food = food;
            targetTroop.troops = troop;

            UpdateTroopsInfo();
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
            UpdateTroopsInfo();
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
            troopsSlider.SetValueWithoutNotify((float)targetTroop.troops / targetTroop.MaxTroops);
            goldSlider.SetValueWithoutNotify((float)targetTroop.gold / targetCity.gold);
            foodSlider.SetValueWithoutNotify((float)targetTroop.food / targetCity.food);
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
                itemLabels[showIndex].SetTitle(itemType.Name);
                SetItemLabel(itemLabels[showIndex], has, use);
                showIndex++;
            }

            for (int i = showIndex; i < itemLabels.Length; i++)
            {
                itemLabels[i].gameObject.SetActive(false);
            }
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

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(cityExpeditionSys.TargetCity.freePersons,
                cityExpeditionSys.personList, 3, OnPersonChange, cityExpeditionSys.customTitleList, cityExpeditionSys.customTitleName);
        }
    }
}
