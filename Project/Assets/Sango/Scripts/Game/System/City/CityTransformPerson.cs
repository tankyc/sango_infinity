using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityTransformPerson : CityBaseSystem
    {
        public List<City> transformTo = new List<City>();
        public List<ObjectSortTitle> citySortTitleList;
        public CityTransformPerson()
        {
            customTitleName = "移动";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByWork,
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
            customMenuName = "人事/移动";
            customMenuOrder = 201;
            windowName = "window_city_person_transform";

            citySortTitleList = new List<ObjectSortTitle>()
            {
                CitySortFunction.SortByName,
                CitySortFunction.SortByPersonCount,
                CitySortFunction.SortByBelongCity,
                CitySortFunction.SortByTroops,
                CitySortFunction.SortByGold,
                CitySortFunction.SortByFood,
                CitySortFunction.SortByLevel,
            };
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
                return TargetCity.mBelongForce.CityBaseCount > 1 &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.TransformPerson) && TargetCity.FreePersonCount > 0;
            }
        }

        public override void OnEnter()
        {
            transformTo.Clear();
            base.OnEnter();
        }

        public override void DoJob()
        {
            if (personList.Count == 0) return;
            if (transformTo.Count == 0) return;

            for (int i = 0; i < personList.Count; i++)
            {
                personList[i].TransformToCity(transformTo[0]);
            }
            TargetCity.mBelongCorps.ActionPoint -= JobType.GetJobCostAP((int)CityJobType.CallPerson);
            GameMedia.Instance.PlayDoAcitonSfx();
            base.DoJob();
        }
    }
}
