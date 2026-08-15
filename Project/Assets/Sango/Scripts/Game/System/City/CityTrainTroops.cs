using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem(autoInit = false)]
    public class CityTrainTroops : CityBaseSystem
    {
        public CityTrainTroops()
        {
            customTitleName = "训练";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByStrength,
            };
            customMenuName = "军事/训练";
            customMenuOrder = 103;
            windowName = "window_city_train_troops";
        }

        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 &&
                    TargetCity.CheckJobCost(CityJobType.TrainTroops) &&
                    TargetCity.morale < TargetCity.MaxMorale &&
                    TargetCity.GetJobCounter((int)CityJobType.TrainTroops) == 0 &&
                    TargetCity.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.TrainTroops);
            }
        }

        public override int CalculateWonderNumber()
        {
            return TargetCity.JobTrainTroops(personList.ToArray(), true);
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendTrainTroops(TargetCity.freePersons);
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
                TargetCity.JobTrainTroops(personList.ToArray());
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            }
        }
    }
}
