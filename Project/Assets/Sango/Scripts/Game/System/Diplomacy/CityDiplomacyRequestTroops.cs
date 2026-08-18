using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    //[GameSystem]
    public class CityDiplomacyRequestTroops : CityBaseSystem
    {
        public List<Person> counsellorRecommendList = new List<Person>();

        public CityDiplomacyRequestTroops()
        {
            customTitleName = "请求兵力";

            customMenuName = "外交/请求兵力";
            customMenuOrder = 306;
            windowName = "window_city_diplomacy_request_troops";

        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 && TargetCity.mBelongCorps.ActionPoint >= 1;
            }
        }

        public override void OnEnter()
        {
            counsellorRecommendList.Clear();
            Person[] recommandList = ForceAI.CounsellorRecommendDiplomacy(TargetCity.freePersons);
            if (recommandList != null)
            {
                for (int i = 0; i < recommandList.Length; i++)
                {
                    counsellorRecommendList.Add(recommandList[i]);
                }
            }

            personList.Clear();
            if (counsellorRecommendList.Count > 0)
                personList.Add(counsellorRecommendList[0]);

            if (customTitleList == null)
            {
                customTitleList = new List<ObjectSortTitle>()
                {
                    PersonSortFunction.SortByName,
                    PersonSortFunction.GetSortByContainsInList("军师推荐", counsellorRecommendList),
                    PersonSortFunction.SortByPolitics,
                    PersonSortFunction.SortByGlamour,
                };
            }

            Window.Instance.Open(windowName);
        }

        public override void OnDestroy()
        {
            GameDialog.Close();
            Window.Instance.Close(windowName);
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            // 这里应该调用外交系统的请求兵力方法
            // 暂时留空，等待具体实现

            Done();
        }
    }
}