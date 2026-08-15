using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem(autoInit = false)]
    public class CityInspection : CityBaseSystem
    {
        public CityInspection()
        {
            customTitleName = "巡视";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByCommand,
            };
            customMenuName = "都市/巡视";
            customMenuOrder = 20;
            windowName = "window_city_Inspection";
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 &&
                    TargetCity.security < 100 &&
                    TargetCity.CheckJobCost(CityJobType.Inspection) &&
                    TargetCity.GetJobCounter((int)CityJobType.Inspection) == 0
                    && TargetCity.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Inspection);
            }
        }

        public override int CalculateWonderNumber()
        {
            return TargetCity.JobInspection(personList.ToArray(), true);
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendInspection(TargetCity.freePersons);
            if (people != null)
            {
                for (int i = 0; i < people.Length; ++i)
                {
                    Person p = people[i];
                    if (p != null)
                        personList.Add(p);
                }
            }
        }

        public override void DoJob()
        {
            if (personList.Count > 0)
            {
                TargetCity.JobInspection(personList.ToArray());
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            }
        }
    }
}
