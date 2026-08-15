using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    //[GameSystem]
    public class CityDiplomacyRansom : CityBaseSystem
    {
        public List<Person> counsellorRecommendList = new List<Person>();



        public CityDiplomacyRansom()
        {
            customTitleName = "赎回俘虏";

            customMenuName = "外交/赎回俘虏";
            customMenuOrder = 308;
            windowName = "window_city_diplomacy_ransom";

        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 && TargetCity.mCorps.ActionPoint >= 1;
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

            // 执行赎回俘虏外交行动
            Person diplomat = personList[0];
            Force sender = diplomat.mForce;
            Force receiver = TargetCity.mForce;
            
            // 确保发送方和接收方不是同一个势力
            if (sender != null && receiver != null && sender != receiver)
            {
                // 假设赎金为1000金
                int ransomValue = 1000;
                // 这里需要获取具体的俘虏ID，暂时使用0，实际应该从UI或其他地方获取
                int captiveId = 0;
                GameSystem.GetSystem<DiplomacyManager>().PerformDiplomacyAction(DiplomacyActionType.Ransom, sender, receiver, diplomat, ransomValue, captiveId);
            }

            Done();
        }
    }
}