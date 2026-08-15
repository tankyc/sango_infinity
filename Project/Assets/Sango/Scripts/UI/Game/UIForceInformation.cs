using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIForceInformation : UGUIWindow
    {
        public Text windiwTitle;

        public Toggle[] tabs;

        public UIObjectList uIObjectList;

        // base info
        public UIPersonItem leaderPersonItems;
        public UIPersonItem counsellorPersonItems;
        public UIStatusItem leaderStatusItem;

        public Image colorImg;
        public UITextField nameLabel;
        public UITextField techniquePointLabel;
        public UITextField intelligenceLabel;

        public UITextField countryLabel;
        public UITextField dukeLabel;
        public UITextField emperorLabel;
        public UITextField corpsCountLabel;
        public UITextField cityCountLabel;
        public UITextField personCountLabel;


        // state
        public UIScenarioCityMap scenarioCityMap;
        public UITextField goldLabel;
        public UITextField foodLabel;
        public UITextField troopsLabel;
        public UITextField gold_costLabel;
        public UITextField food_costLabel;
        public UITextField gold_gainLabel;
        public UITextField food_gainLabel;
        public UITextField gold_wonderLabel;
        public UITextField food_wonderLabel;
        public UITextField gold_enoughLabel;
        public UITextField food_enoughLabel;

        public UITextField stopwarLabel;
        public UITextField relationshipLabel;
        public UITextField friendlyLabel;

        // item
        public UIBuildingTypeItem boatItem;
        public UIBuildingTypeItem itemObject;
        CreatePool<UIBuildingTypeItem> itemPool;

        // building
        public UIBuildingTypeItem buildingItemObject;
        public UITextField buildingNumLabel;
        CreatePool<UIBuildingTypeItem> buildingPool;

        Force Target;
        ForceInformation currentSystem;

        bool state_inited = false;
        bool items_inited = false;
        bool buildings_inited = false;
        public int showTab = 0;

        public Button troop_btn;
        public Button port_gate_btn;

        List<Corps> corpsList = new List<Corps>();
        List<Person> personList = new List<Person>();
        List<City> cityList = new List<City>();
        List<City> portgateList = new List<City>();
        List<Troop> troopList = new List<Troop>();

        protected override void Awake()
        {
            buildingPool = new CreatePool<UIBuildingTypeItem>(buildingItemObject);
            itemPool = new CreatePool<UIBuildingTypeItem>(itemObject);
        }

        public override void OnOpen(params object[] objects)
        {
            showTab = 0;
            currentSystem = objects[0] as ForceInformation;
            Target = currentSystem.Target;
            uIObjectList.Init(currentSystem.all_objects, ForceSortFunction.SortByName, OnObjectSelected);
            uIObjectList.SelectDefaultObject(Target);
            windiwTitle.text = currentSystem.Name;
            tabs[0].isOn = true;
            Show(Target);
        }

        void OnObjectSelected(int index)
        {
            Show(currentSystem.all_objects[index] as Force);
        }

        int gold = 0, goldGain = 0, goldWonder = 0, goldCost = 0;
        int food = 0, foodGain = 0, foodWonder = 0, foodCost = 0;
        int troops = 0;
        ItemStore itemStore = new ItemStore();
        Dictionary<int, int> buildingCountMap = new Dictionary<int, int>();
        int buildingCount;
        int interiorCellCount;

        int GetBuildingNumber(int buildingKindId)
        {
            int count;
            if (buildingCountMap.TryGetValue(buildingKindId, out count))
                return count;
            return 0;
        }

        public void Show(Force force)
        {
            Target = force;
            state_inited = false;
            items_inited = false;
            buildings_inited = false;

            corpsList.Clear();
            personList.Clear();
            cityList.Clear();
            portgateList.Clear();
            troopList.Clear();

            nameLabel.text = force.Name;
            leaderPersonItems.SetPerson(force.mGovernor);
            leaderStatusItem.SetPerson(force.mGovernor);
            counsellorPersonItems.SetPerson(force.mCounsellor);
            intelligenceLabel.text = force.mCounsellor?.Intelligence.ToString() ?? "-";

            colorImg.color = force.mFlag.color;
            techniquePointLabel.text = force.TechniquePoint.ToString();

            interiorCellCount = 0;
            buildingCount = 0;
            gold = 0;
            goldGain = 0;
            goldWonder = 0;
            goldCost = 0;
            food = 0;
            foodGain = 0;
            foodWonder = 0;
            foodCost = 0;
            troops = 0;
            itemStore.Clear();
            Scenario scenario = Scenario.Cur;
            scenario.citySet.ForEach(city =>
            {
                if (city.mForce == force)
                {
                    gold += city.gold;
                    goldGain += city.totalGainGold;
                    goldCost += city.GoldCost(scenario);
                    troops += city.troops;
                    food += city.food;
                    foodGain += city.totalGainFood;
                    foodCost += city.FoodCost(scenario);
                    interiorCellCount += city.InteriorCellCount;
                    buildingCount += city.GetInteriorCellUsedCount();
                    itemStore.Add(city.itemStore);

                    foreach (var k in city.buildingCountMap.Keys)
                    {
                        if(!buildingCountMap.ContainsKey(k))
                            buildingCountMap.Add(k, 0);
                        buildingCountMap[k] += city.buildingCountMap[k];
                    }

                    goldWonder = goldCost * 10;
                    foodWonder = foodCost * 20;

                    if (city.IsCity())
                    {
                        cityList.Add(city);
                    }
                    else
                    {
                        portgateList.Add(city);
                    }
                }
            });

            scenario.personSet.ForEach(person =>
            {
                if (person.mForce == force)
                {
                    personList.Add(person);
                }
            });

            scenario.troopsSet.ForEach(person =>
            {
                if (person.BelongForce == force)
                {
                    troopList.Add(person);
                }
            });

            scenario.corpsSet.ForEach(person =>
            {
                if (person.mForce == force)
                {
                    corpsList.Add(person);
                }
            });


            // TODO: 皇帝
            emperorLabel.text = "--";
            // TODO: 国号
            countryLabel.text = "--";
            // TODO: 爵位
            dukeLabel.text = "--";

            corpsCountLabel.text = corpsList.Count.ToString();
            cityCountLabel.text = cityList.Count.ToString();
            personCountLabel.text = personList.Count.ToString();

            port_gate_btn.interactable = portgateList.Count > 0;
            troop_btn.interactable = troopList.Count > 0;

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
            Force force = Target;
            Scenario scenario = Scenario.Cur;
            scenarioCityMap.Show(scenario, force);


            goldLabel.text = gold.ToString();
            foodLabel.text = food.ToString();
            troopsLabel.text = troops.ToString();

            gold_costLabel.text = goldCost.ToString();
            food_costLabel.text = foodCost.ToString();
            gold_gainLabel.text = goldGain.ToString();
            food_gainLabel.text = foodGain.ToString();

            gold_wonderLabel.text = goldWonder.ToString();
            food_wonderLabel.text = foodWonder.ToString();
            gold_enoughLabel.text = "";
            food_enoughLabel.text = "";

            stopwarLabel.text = "--";
            relationshipLabel.text = scenario.GetRelation(force, scenario.CurRunForce).ToString();
            friendlyLabel.text = "--";
        }

        void UpdateItemsContent()
        {
            if (items_inited) return;
            items_inited = true;
            itemPool.Reset();
            List<ItemType> ItemTypes = Target.createdItemTypes;
            int len = ItemTypes.Count;
            for (int i = 0; i < len; i++)
            {
                ItemType itemType = ItemTypes[i];
                int totalNum = itemStore.GetNumber(itemType);
                if (itemType.storeKind == (int)ItemStoreKindType.Boat)
                {
                    boatItem.SetItemType(itemType).SetIndex(i).SetNum(totalNum);
                }
                else
                {
                    UIBuildingTypeItem forceBuildingSlot = itemPool.Create();
                    forceBuildingSlot.SetItemType(itemType).SetIndex(i).SetNum(totalNum);
                }
            }
        }

        void UpdateBuildingsContent()
        {
            if (buildings_inited) return;
            buildings_inited = true;
            buildingPool.Reset();
            buildingNumLabel.text = $"{buildingCount}/{interiorCellCount}";
            List<BuildingType> canBuildBuildingType = new List<BuildingType>();
            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (x.IsIntrior && x.level == 1 && x.IsValid(Target) && x.canBuild)
                {
                    canBuildBuildingType.Add(x);
                }
            });

            foreach (var buildingType in canBuildBuildingType)
            {
                UIBuildingTypeItem item = buildingPool.Create();
                int buildingNum = GetBuildingNumber(buildingType.kind);
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

        public void OnCorpsButton()
        {

        }

        public void OnCityButton()
        {
            List<City> result_list = new List<City>();
            CitySelectSystem portGateSelectSystem = GameSystem.GetSystem<CitySelectSystem>();
            portGateSelectSystem.Start(
                cityList,
                result_list, 1, OnCitySelected, null, null);
            portGateSelectSystem.donotFinishThisSystem = true;
        }

        void OnCitySelected(List<City> objList)
        {
            if (objList.Count == 0) return;
            List<SangoObject> result_list = new List<SangoObject>();
            result_list.AddRange(cityList);
            GameSystem.GetSystem<PortGateInformation>().Start(objList[0], result_list);
        }

        public void OnPortGateButton()
        {
            List<City> result_list = new List<City>();
            PortGateSelectSystem portGateSelectSystem = GameSystem.GetSystem<PortGateSelectSystem>();
            portGateSelectSystem.Start(
                portgateList,
                result_list, 1, OnPortGateSelected, null, null);
            portGateSelectSystem.donotFinishThisSystem = true;
        }

        void OnPortGateSelected(List<City> port_gate)
        {
            if (port_gate.Count == 0) return;
            List<SangoObject> result_list = new List<SangoObject>();
            result_list.AddRange(portgateList);
            GameSystem.GetSystem<PortGateInformation>().Start(port_gate[0], result_list);
        }

        public void OnTroopButton()
        {
            List<Troop> result_list = new List<Troop>();
            TroopSelectSystem troopSelectSystem = GameSystem.GetSystem<TroopSelectSystem>();
            troopSelectSystem.Start(
                troopList,
                result_list, 1, OnTroopSelected, null, null);
            troopSelectSystem.donotFinishThisSystem = true;
        }

        void OnTroopSelected(List<Troop> person_list)
        {
            if (person_list.Count == 0) return;
            List<SangoObject> result_list = new List<SangoObject>();
            result_list.AddRange(troopList);
            GameSystem.GetSystem<TroopInformation>().Start(person_list[0], result_list);
        }

        public void OnPersonButton()
        {
            List<Person> result_list = new List<Person>();
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                personList,
                result_list, 1, OnPersonSelected, null, null);
            personSelectSystem.donotFinishThisSystem = true;
        }

        void OnPersonSelected(List<Person> person_list)
        {
            if (person_list.Count == 0) return;
            List<SangoObject> result_list = new List<SangoObject>();
            result_list.AddRange(personList);
            GameSystem.GetSystem<PersonInformation>().Start(person_list[0], result_list);
        }

        public void OnDiplomacyButton()
        {
            //List<Force> allForces = new List<Force>();
            //Scenario.Cur.forceSet.ForEach(force =>
            //{
            //    if (force.IsAlive)
            //    {
            //        allForces.Add(force);
            //    }
            //});
            //GameSystem.GetSystem<DiplomacySystem>().Start(Target, allForces);
        }

        public void OnTechniqueButton()
        {
        }

        public void OnAabilityButton()
        {
        }
    }
}
