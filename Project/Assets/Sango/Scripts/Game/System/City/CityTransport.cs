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

        /// <summary>初始化运输部队，并在场景数据就绪后创建搬运特性排序列。</summary>
        public override void OnEnter()
        {
            // 特性标题依赖 Scenario.Cur，必须延迟到进入运输命令后再生成。
            customTitleList[1] = CreateTransportPersonSortTitle();
            personList.Clear();
            TargetTroop = new Troop();
            Scenario scenario = Scenario.Cur;
            // 默认主将与手动选择器均按搬运优先规则确定。
            Person[] persons = GetRecommendedTransportPersons();
            Person leader = persons.Length > 0 ? persons[0] : null;

            if (leader != null)
                personList.Add(leader);

            TargetTroop.morale = TargetCity.morale;
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

        /// <summary>创建运输专用排序列：搬运优先，同组内按运输能力倒序。</summary>
        private static PersonSortFunction.SortTitle CreateTransportPersonSortTitle()
        {
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.GetSortByFeatrueId(8);
            sortTitle.personSortFunc = (a, b) =>
            {
                // 搬运特性提高输送部队移动力，应优先于基础运输能力。
                int featureCompare = b.HasFeatrue(8).CompareTo(a.HasFeatrue(8));
                return featureCompare != 0 ? featureCompare : b.MilitaryAbility.CompareTo(a.MilitaryAbility);
            };
            return sortTitle;
        }

        /// <summary>按运输选择器的排序规则推荐最多三名武将。</summary>
        private Person[] GetRecommendedTransportPersons()
        {
            List<Person> persons = new List<Person>(TargetCity.freePersons);
            // 复用展示排序，保证自动推荐与玩家手动选择的优先级一致。
            persons.Sort((a, b) => CreateTransportPersonSortTitle().Sort(a, b));
            if (persons.Count > 3) persons.RemoveRange(3, persons.Count - 3);
            return persons.ToArray();
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
