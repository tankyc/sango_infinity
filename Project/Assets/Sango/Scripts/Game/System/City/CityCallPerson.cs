using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityCallPerson : CityBaseSystem
    {
        public CityCallPerson()
        {
            customTitleName = "召唤";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByBelongCorps,
                PersonSortFunction.SortByWork,
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
            customMenuName = "人事/召唤";
            customMenuOrder = 211;
            windowName = "window_city_person_call";

        }
        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                // 需要至少两座城
                return TargetCity.mForce.CityBaseCount > 1 &&
                    TargetCity.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.CallPerson);
            }
        }

        public override void DoJob()
        {
            if (personList.Count == 0) return;

            for (int i = 0; i < personList.Count; i++)
            {
                personList[i].TransformToCity(TargetCity);
            }

            TargetCity.mCorps.ActionPoint -= JobType.GetJobCostAP((int)CityJobType.CallPerson);
            GameMedia.Instance.PlayDoAcitonSfx();
            base.DoJob();
        }
    }
}
