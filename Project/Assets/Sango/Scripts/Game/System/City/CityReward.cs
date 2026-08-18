using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityReward : CityBaseSystem
    {
        public List<Person> targetList = new List<Person>();

        public CityReward()
        {
            customTitleName = "褒赏";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByBelongCorps,
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
            customMenuName = "人事/褒赏";
            customMenuOrder = 241;
            windowName = "window_city_reward";
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.gold > 100 &&
                       TargetCity.CheckJobCost(CityJobType.Reward) &&
                       TargetCity.mBelongCorps.GetJobCounter((int)CityJobType.Reward) == 0 &&
                       TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Reward);
            }
        }

        public override void OnEnter()
        {
            targetList.Clear();
            TargetCity.mBelongForce.ForEachPerson(x =>
            {
                if (x != TargetCity.mBelongForce.mGovernor && x.mTroop == null && x.loyalty < 100)
                {
                    targetList.Add(x);
                }
            });
            targetList.Sort((a, b) => PersonSortFunction.SortByLoyalty.personSortFunc.Invoke(a, b));
            base.OnEnter();
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            if (targetList.Count > 0)
            {
                personList.Add(targetList[0]);
            }
        }

        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            base.OnDestroy();
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            TargetCity.JobRewardPersons(personList.ToArray());
            Done();
            GameMedia.Instance.PlayDoAcitonSfx();
        }
    }
}