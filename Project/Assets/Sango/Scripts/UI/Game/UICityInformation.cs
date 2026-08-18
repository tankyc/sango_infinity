using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityInformation : UGUIWindow
    {
        public Text windiwTitle;

        public Toggle[] tabs;

        public UIObjectList uIObjectList;

        public UIPersonItem cityLeaderPersonItems;
        public UIStatusItem cityLeaderStatusItem;

        public UITextField cityNameLabel;

        public UITextField forceNameLabel;
        public UITextField cityPersonCountLabel;
        public UITextField corpsNameLabel;
        public UITextField captiveCountLabel;
        public UITextField wildCountLabel;
        public UITextField emperorLabel;

        public UIScenarioCityMap scenarioCityMap;
        public UITextField goldLabel;
        public UITextField foodLabel;
        public UITextField troopsLabel;
        public UITextField gold_costLabel;
        public UITextField food_costLabel;
        public UITextField moraleLabel;
        public UITextField gold_gainLabel;
        public UITextField food_gainLabel;
        public UITextField gold_wonderLabel;
        public UITextField food_wonderLabel;
        public UITextField gold_enoughLabel;
        public UITextField food_enoughLabel;
        public UITextField warLabel;
        public UITextField durabilityLabel;
        public UITextField empty_fieldLabel;
        public UITextField securityLabel;
        public UITextField bussinessLabel;
        public UITextField featureLabel;
        public Button[] disaster_icon_list;

        public UIBuildingTypeItem boatItem;
        public UIBuildingTypeItem itemObject;
        CreatePool<UIBuildingTypeItem> itemPool;

        public UIBuildingTypeItem buildingItemObject;
        public UITextField buildingNumLabel;
        CreatePool<UIBuildingTypeItem> buildingPool;

        City Target;
        CityInformation currentSystem;

        bool state_inited = false;
        bool items_inited = false;
        bool buildings_inited = false;
        public int showTab = 0;

        public Button port_gate_btn;
        public Button troop_btn;
        public Button person_btn;
        public Button captive_btn;
        List<Person> personList = new List<Person>();
        List<Troop> troopList = new List<Troop>();

        protected override void Awake()
        {
            buildingPool = new CreatePool<UIBuildingTypeItem>(buildingItemObject);
            itemPool = new CreatePool<UIBuildingTypeItem>(itemObject);
        }

        public override void OnOpen(params object[] objects)
        {
            showTab = 0;
            currentSystem = objects[0] as CityInformation;
            Target = currentSystem.Target;
            uIObjectList.Init(currentSystem.all_objects, CitySortFunction.SortByName, OnObjectSelected);
            uIObjectList.SelectDefaultObject(Target);
            windiwTitle.text = currentSystem.Name;
            tabs[0].isOn = true;
            Show(Target);
        }

        void OnObjectSelected(int index)
        {
            Show(currentSystem.all_objects[index] as City);
        }

        public void Show(City city)
        {
            Target = city;
            state_inited = false;
            items_inited = false;
            buildings_inited = false;

            cityNameLabel.text = city.Name;
            cityLeaderPersonItems.SetPerson(city.Leader);
            cityLeaderStatusItem.SetPerson(city.Leader);

            forceNameLabel.text = CitySortFunction.SortByBelongForce.GetValueStr(city);
            cityPersonCountLabel.text = CitySortFunction.SortByAllPersonCountInfo.GetValueStr(city);
            corpsNameLabel.text = CitySortFunction.SortByBelongCorps.GetValueStr(city);
            captiveCountLabel.text = CitySortFunction.SortByCaptiveCount.GetValueStr(city);
            if (city.IsCity())
                wildCountLabel.text = CitySortFunction.SortByWildCount.GetValueStr(city);

            port_gate_btn.interactable = (city.portList.Count + city.gateList.Count) > 0;
            troop_btn.interactable = city.allTroops.Count > 0;
            person_btn.interactable = city.allPersons.Count > 0;
            captive_btn.interactable = city.captiveList.Count > 0;

            if (city.IsCity())
            {
                // TODO: 皇帝
                emperorLabel.text = "";
            }
            else
            {
                // TODO: 皇帝
                emperorLabel.text = city.mBelongCity.Name;
            }
            switch (showTab)
            {
                case 0:
                    UpdateStateContent();
                    break;
                case 1:
                    UpdateItemsContent();
                    break;
                case 2:
                    UpdateBuildingsContent();
                    break;
            }
        }

        void UpdateStateContent()
        {
            if (state_inited) return;
            state_inited = true;
            City city = Target;
            Scenario scenario = Scenario.Cur;
            scenarioCityMap.Show(scenario, city);
            goldLabel.text = $"{city.gold}/{city.GoldLimit}";
            foodLabel.text = $"{city.food}/{city.FoodLimit}";
            troopsLabel.text = $"{city.troops}/{city.TroopsLimit}";
            moraleLabel.text = $"{city.morale}/{city.MaxMorale}";

            gold_costLabel.text = city.GoldCost(scenario).ToString();
            food_costLabel.text = city.FoodCost(scenario).ToString();
            gold_gainLabel.text = city.totalGainGold.ToString();
            food_gainLabel.text = city.totalGainFood.ToString();

            gold_wonderLabel.text = "";
            food_wonderLabel.text = "";
            gold_enoughLabel.text = "";
            food_enoughLabel.text = "";

            warLabel.text = city.EnemyCount > 0 ? GameDefine.o : GameDefine.x;
            durabilityLabel.text = $"{city.durability}/{city.DurabilityLimit}";
            empty_fieldLabel.text = $"{city.InteriorCellCount - city.GetInteriorCellUsedCount()}/{city.InteriorCellCount}";
            securityLabel.text = city.security.ToString();
            bussinessLabel.text = CitySortFunction.SortByHasBusiness.GetValueStr(city);
            //featureLabel.text = city.security.ToString();
            //disaster_icon_list.text = city.security.ToString();
        }

        void UpdateItemsContent()
        {
            if (items_inited || itemPool == null) return;
            items_inited = true;
            itemPool.Reset();
            List<ItemType> ItemTypes = Target.mBelongForce.createdItemTypes;
            int len = ItemTypes.Count;
            for (int i = 0; i < len; i++)
            {
                ItemType itemType = ItemTypes[i];
                int totalNum = Target.itemStore.GetNumber(itemType);
                if (itemType.storeKind == (int)ItemStoreKindType.Boat)
                {
                    boatItem.SetItemType(itemType).SetIndex(i).SetNum(totalNum);
                }
                else
                {
                    UIBuildingTypeItem cityBuildingSlot = itemPool.Create();
                    cityBuildingSlot.SetItemType(itemType).SetIndex(i).SetNum(totalNum);
                }
            }
        }

        void UpdateBuildingsContent()
        {
            if (buildings_inited) return;
            buildings_inited = true;
            City city = Target;
            buildingPool.Reset();
            buildingNumLabel.text = $"{city.InteriorCellCount - city.GetInteriorCellUsedCount()}/{city.InteriorCellCount}";
            List<BuildingType> canBuildBuildingType = new List<BuildingType>();
            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (x.IsIntrior && x.level == 1 && x.IsValid(Target.mBelongForce) && x.canBuild)
                {
                    canBuildBuildingType.Add(x);
                }
            });

            foreach (var buildingType in canBuildBuildingType)
            {
                UIBuildingTypeItem item = buildingPool.Create();
                int buildingNum = city.GetBuildingNumber(buildingType.kind);
                if (buildingNum > 0)
                {
                    item.SetBuildingType(buildingType).nameLabel.text = $"{buildingType.Name}（{buildingNum}）";
                    item.SetValid(true);
                }
                else
                {
                    item.SetBuildingType(buildingType).nameLabel.text = $"{buildingType.Name}";
                    item.SetValid(false);
                }
            }

        }

        public void OnCancel()
        {
            currentSystem.Back();
        }

        public void OnCityStateTab(bool b)
        {
            if (b)
            {
                showTab = 0; 
                UpdateStateContent();
            }
        }

        public void OnCityItemsTab(bool b)
        {
            if (b)
            {
                showTab = 1;
                UpdateItemsContent();
            }
        }

        public void OnCityBuildingsTab(bool b)
        {
            if (b)
            {
                showTab = 2;
                UpdateBuildingsContent();
            }
        }

        public void OnPortGateButton()
        {
            List<City> port_gate_list = new List<City>();
            port_gate_list.AddRange(Target.portList);
            port_gate_list.AddRange(Target.gateList);

            if (port_gate_list.Count == 0) return;

            List<City> result_list = new List<City>() { port_gate_list[0] };
            PortGateSelectSystem portGateSelectSystem = GameSystem.GetSystem<PortGateSelectSystem>();
            portGateSelectSystem.Start(
                port_gate_list,
                result_list, 1, OnPortGateSelected, null, null);
            portGateSelectSystem.donotFinishThisSystem = true;
        }

        void OnPortGateSelected(List<City> port_gate)
        {
            if (port_gate.Count == 0) return;
            List<SangoObject> port_gate_list = new List<SangoObject>();
            port_gate_list.AddRange(Target.portList);
            port_gate_list.AddRange(Target.gateList);
            GameSystem.GetSystem<PortGateInformation>().Start(port_gate[0], port_gate_list);
        }

        public void OnTroopButton()
        {
            troopList.Clear();
            troopList.AddRange(Target.allTroops);
            List<Troop> result_list = new List<Troop>();
            TroopSelectSystem troopSelectSystem = GameSystem.GetSystem<TroopSelectSystem>();
            troopSelectSystem.Start(
                troopList,
                result_list, 1, OnTroopSelected, null, null);
            troopSelectSystem.donotFinishThisSystem = true;
        }

        void OnTroopSelected(List<Troop> person_list)
        {
            if (person_list == null || person_list.Count == 0)
                return;

            List<SangoObject> object_list = new List<SangoObject>();
            object_list.AddRange(troopList);
            GameSystem.GetSystem<TroopInformation>().Start(person_list[0], object_list);
        }

        public void OnPersonButton()
        {
            personList.Clear();
            Target.allPersons.ForEach(x =>
            {
                personList.Add(x);
            });
            personList.AddRange(Target.wildPersons);
            List<Person> result_list = new List<Person>();
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                personList,
                result_list, 1, OnPersonSelected, null, null);
            personSelectSystem.donotFinishThisSystem = true;
        }

        public void OnCaptiveButton()
        {
            personList.Clear();
            Target.captiveList.ForEach(x =>
            {
                personList.Add(x);
            });
            List<Person> result_list = new List<Person>();
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                personList,
                result_list, 1, OnPersonSelected, null, null);
            personSelectSystem.donotFinishThisSystem = true;
        }

        void OnPersonSelected(List<Person> person_list)
        {
            if (person_list.Count <= 0)
                return;

            List<SangoObject> object_list = new List<SangoObject>();
            object_list.AddRange(personList);
            GameSystem.GetSystem<PersonInformation>().Start(person_list[0], object_list);
        }

    }
}
