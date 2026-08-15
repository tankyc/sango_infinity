using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem(autoInit = false)]
    public class CitySeraching : CityBaseSystem
    {
        public List<Person> counsellorRecommendList = new List<Person>();

        public CitySeraching()
        {
            customTitleName = "探索人才";

            customMenuName = "人事/探索人才";
            customMenuOrder = 221;
            windowName = "window_city_searching";

        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 && TargetCity.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Searching);
            }
        }

        public override void OnEnter()
        {
            counsellorRecommendList.Clear();
            int[] recommandFeatrues = new int[] { 86 };
            Person[] recommandList = ForceAI.CounsellorRecommendSearching(TargetCity.freePersons, TargetCity, recommandFeatrues);
            if (recommandList != null)
            {
                for (int i = 0; i < recommandList.Length; i++)
                {
                    counsellorRecommendList.Add(recommandList[i]);
                }
            }

            counsellorRecommendList.Sort(
                (a, b) =>
                        {
                            bool ahas = a.HasFeatrue(recommandFeatrues);
                            bool bhas = b.HasFeatrue(recommandFeatrues);
                            if (ahas == bhas)
                            {
                                return -a.Politics.CompareTo(b.Politics);
                            }
                            else
                            {
                                return -ahas.CompareTo(bhas);
                            }
                        }
                );

            personList.Clear();
            if (counsellorRecommendList.Count > 0)
                personList.Add(counsellorRecommendList[0]);

            if (customTitleList == null)
            {
                customTitleList = new List<ObjectSortTitle>()
                {
                    PersonSortFunction.SortByName,
                    PersonSortFunction.GetSortBySearchingRecommend(counsellorRecommendList, 86),
                    PersonSortFunction.SortByPolitics,
                    PersonSortFunction.GetSortByFeatrueId(86),
                };
            }

            Window.Instance.Open(windowName);
        }
        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            Window.Instance.Close(windowName);
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            TargetCity.JobSearching(personList.ToArray());

            Done();
            GameMedia.Instance.PlayDoAcitonSfx();
        }
    }
}
