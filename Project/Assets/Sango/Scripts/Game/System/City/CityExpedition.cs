using Sango.UI;
using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityExpedition : CityBaseSystem
    {
        public List<TroopType> ActivedLandTroopTypes = new List<TroopType>();
        public List<TroopType> ActivedWaterTroopTypes = new List<TroopType>();

        public int CurSelectLandTrropTypeIndex { get; set; }
        public int CurSelectWaterTrropTypeIndex { get; set; }

        public Troop TargetTroop { get; set; }

        public CityExpedition()
        {
            customTitleName = "出征";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByWaterLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "军事/出征";
            customMenuOrder = 100;
            windowName = "window_city_create_troop";
        }
        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.troops > 0 && TargetCity.food > 0 && TargetCity.freePersons.Count > 0 &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.MakeTroop);
            }
        }
        public override void UpdateJobValue()
        {
            if (personList.Count == 0) return;

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1)
                TargetTroop.Member1 = personList[1];
            else
                TargetTroop.Member1 = null;
            if (personList.Count > 2)
                TargetTroop.Member2 = personList[2];
            else
                TargetTroop.Member2 = null;

            TargetTroop.LandTroopType = ActivedLandTroopTypes[CurSelectLandTrropTypeIndex];
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];

            TargetTroop.CalculateMaxTroops();

            if (TargetTroop.troops > TargetTroop.MaxTroops)
                TargetTroop.troops = TargetTroop.MaxTroops;

            int troop = TargetTroop.troops;
            troop = Math.Min(troop, TargetCity.troops);
            troop = TargetCity.itemStore.CheckCostMin(TargetTroop.LandTroopType.costItems, troop);
            troop = TargetCity.itemStore.CheckCostMin(TargetTroop.WaterTroopType.costItems, troop);

            TargetTroop.troops = troop;

            TargetTroop.CalculateAttribute(Scenario.Cur);
        }

        public override void OnEnter()
        {
            personList.Clear();
            TargetTroop = new Troop();
            List<TroopType> activeTroopTypes = new List<TroopType>();
            TroopType.CheckActivTroopTypeList(TargetCity.freePersons, activeTroopTypes);

            ActivedLandTroopTypes.Clear();
            ActivedLandTroopTypes.AddRange(activeTroopTypes.FindAll(x => x.isLand));

            ActivedWaterTroopTypes.Clear();
            ActivedWaterTroopTypes.AddRange(activeTroopTypes.FindAll(x => !x.isLand));

            CurSelectLandTrropTypeIndex = 0;
            CurSelectWaterTrropTypeIndex = 0;

            TargetTroop.LandTroopType = ActivedLandTroopTypes[CurSelectLandTrropTypeIndex];
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];

            TargetTroop.morale = TargetCity.morale;
            //TargetTroop.MaxMorale = TargetCity.MaxMorale;
            TargetTroop.energy = TargetCity.energy;
            if (TargetTroop.troops == 0)
            {
                TargetTroop.troops = 1;
                TargetTroop.MaxTroops = 5000;
            }

            UpdateJobValue();

            Window.Instance.Open(windowName);
        }

        public override void DoJob()
        {
            if (TargetTroop.troops <= 0) return;
            if (TargetTroop.food <= 0) return;
            if (personList.Count == 0) return;

            ContextMenu.CloseAll();
            TargetTroop.ActionOver = false;
            TargetTroop.IsAlive = true;

            TargetTroop.LandTroopType.Cost(TargetCity, TargetTroop.troops);
            TargetTroop.WaterTroopType.Cost(TargetCity, TargetTroop.troops);
            TargetCity.troops -= TargetTroop.troops;
            TargetCity.food -= TargetTroop.food;
            TargetCity.gold -= TargetTroop.gold;

            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Remove(person);
            });
            TargetCity.Render?.UpdateRender();
            TargetTroop.mBelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.MakeTroop));
            TargetCity.EnsureTroop(TargetTroop, Scenario.Cur);
            Window.Instance.SetVisible(windowName, false);
            GameSystem.GetSystem<TroopSystem>().Start(TargetTroop);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            base.OnBack(whoGone);
            if (whoGone is ObjectSelectSystem) return;
            Window.Instance.SetVisible(windowName, true);
            TargetTroop.mBelongCorps.ReduceActionPoint(-JobType.GetJobCostAP((int)CityJobType.MakeTroop));
            TargetTroop.EnterCity(TargetCity);
            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Add(person);
                person.ActionOver = false;
            });
        }

        public void AutoMakeTroop(int troopTypeKind)
        {
            List<TroopType> troopTypes = ActivedLandTroopTypes.FindAll(x => x.kind == troopTypeKind);
            TroopType targetTroopType = troopTypes[troopTypes.Count - 1];
            CurSelectLandTrropTypeIndex = ActivedLandTroopTypes.FindIndex(x => x == targetTroopType);
            AutoMakeTroop(targetTroopType);
        }

        public void AutoMakeTroop(TroopType troopType)
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendMakeTroop(TargetCity.freePersons, troopType);
            if (people == null || people.Length == 0)
                return;
            for (int i = 0; i < people.Length; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                personList.Add(person);
            }

            TargetTroop.LandTroopType = troopType;
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1) TargetTroop.Member1 = personList[1];
            if (personList.Count > 2) TargetTroop.Member2 = personList[2];

            TargetTroop.CalculateMaxTroops();
            TargetTroop.CalculateAttribute(Scenario.Cur);

            SetTroops(TargetTroop.MaxTroops);
        }

        public void AutoMakeBuildTroop()
        {
            personList.Clear();
            Scenario scenario = Scenario.Cur;
            BuildingType buildingType = scenario.GetObject<BuildingType>(31);
            Person[] people = ForceAI.CounsellorRecommendBuild(TargetCity.freePersons, buildingType);
            if (people == null || people.Length == 0)
                return;
            for (int i = 0; i < people.Length; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                personList.Add(person);
            }

            TargetTroop.LandTroopType = scenario.GetObject<TroopType>(1); ;
            TargetTroop.WaterTroopType = ActivedWaterTroopTypes[CurSelectWaterTrropTypeIndex];
            CurSelectLandTrropTypeIndex = 0;

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1) TargetTroop.Member1 = personList[1];
            if (personList.Count > 2) TargetTroop.Member2 = personList[2];

            TargetTroop.CalculateMaxTroops();
            TargetTroop.CalculateAttribute(scenario);

            SetTroops(3000);
            TargetTroop.gold = 1000;
        }


        public void SetTroops(int num)
        {
            int maxTroopNum = Math.Min(num, TargetCity.troops);
            maxTroopNum = TargetCity.itemStore.CheckCostMin(TargetTroop.LandTroopType.costItems, maxTroopNum);
            maxTroopNum = TargetCity.itemStore.CheckCostMin(TargetTroop.WaterTroopType.costItems, maxTroopNum);
            int wonderFood = (int)(maxTroopNum * Scenario.Cur.Variables.baseFoodCostInTroop * 20);
            int food = Math.Min(wonderFood, TargetCity.food);

            TargetTroop.troops = maxTroopNum;
            TargetTroop.food = food;
        }

        public void SetWaterType(TroopType troopType)
        {
            TargetTroop.WaterTroopType = troopType;
            if (personList.Count <= 0)
                return;

            TargetTroop.CalculateAttribute(Scenario.Cur);
            SetTroops(TargetTroop.MaxTroops);
        }

        public void SetLandType(TroopType troopType)
        {
            TargetTroop.LandTroopType = troopType;
            if (personList.Count <= 0)
                return;

            TargetTroop.CalculateAttribute(Scenario.Cur);
            SetTroops(TargetTroop.MaxTroops);
        }
    }
}
