using Sango.UI;
using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityTransport : CityBaseSystem
    {
        public CityTransport()
        {
            customTitleName = "运输";
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
            customMenuName = "军事/运输";
            customMenuOrder = 110;
            windowName = "window_city_create_transport";
        }

        public Troop TargetTroop { get; set; }

        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.troops > 0 && TargetCity.food > 0 && TargetCity.freePersons.Count > 0 &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.MakeTansport);
            }
        }
        public override void UpdateJobValue()
        {
            if (personList.Count == 0) return;

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1) TargetTroop.Member1 = personList[1];
            if (personList.Count > 2) TargetTroop.Member2 = personList[2];

            TargetTroop.CalculateAttribute(Scenario.Cur);
        }

        public override void OnEnter()
        {
            personList.Clear();
            TargetTroop = new Troop();
            Scenario scenario = Scenario.Cur;
            Person[] persons = ForceAI.CounsellorRecommendTransportTroop(TargetCity.freePersons);
            Person leader = persons[0];

            if (leader != null)
                personList.Add(leader);

            TargetTroop.morale = TargetCity.morale;
            //TargetTroop.MaxMorale = TargetCity.MaxMorale;
            TargetTroop.energy = TargetCity.energy;
            TargetTroop.Leader = leader;
            TargetTroop.Member1 = null;
            TargetTroop.Member2 = null;
            TargetTroop.LandTroopType = TroopType.GetTransportType(scenario, TargetCity.mBelongForce);
            TargetTroop.WaterTroopType = scenario.GetObject<TroopType>(8);
            if (TargetTroop.troops == 0)
            {
                TargetTroop.troops = 1;
                TargetTroop.food = Math.Min(20, TargetCity.food);
            }
            TargetTroop.missionType = (int)MissionType.TroopTransformGoodsToCity;
            TargetTroop.CalculateAttribute(Scenario.Cur);
            Window.Instance.Open(windowName);
        }

        public void MakeTroop()
        {
            if (TargetTroop.troops <= 0) return;
            if (TargetTroop.food <= 0) return;
            if (personList.Count == 0) return;

            ContextMenu.CloseAll();
            TargetTroop.ActionOver = false;
            TargetTroop.IsAlive = true;

            TargetCity.troops -= TargetTroop.troops;
            TargetCity.food -= TargetTroop.food;
            TargetCity.gold -= TargetTroop.gold;
            TargetCity.itemStore.Remove(TargetTroop.itemStore);
            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Remove(person);
            });
            TargetCity.Render?.UpdateRender();
            TargetCity.EnsureTroop(TargetTroop, Scenario.Cur);
            TargetTroop.mBelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.MakeTansport));
            Window.Instance.SetVisible(windowName, false);
            GameSystem.GetSystem<TroopSystem>().Start(TargetTroop);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            base.OnBack(whoGone);
            if (whoGone is ObjectSelectSystem) return;
            Window.Instance.SetVisible(windowName, true);
            TargetTroop.mBelongCorps.ReduceActionPoint(-JobType.GetJobCostAP((int)CityJobType.MakeTansport));
            TargetTroop.EnterCity(TargetCity);
            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Add(person);
                person.ActionOver = false;
            });
        }
    }
}
