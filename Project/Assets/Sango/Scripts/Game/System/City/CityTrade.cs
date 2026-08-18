using Sango.UI;
using System.Collections.Generic;
using static Sango.Core.PersonSortFunction;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityTrade : CityBaseSystem
    {
        public int targetValue;

        public CityTrade()
        {
            customTitleName = "交易";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByPolitics,
            };
            customMenuName = "都市/商人";
            customMenuOrder = 25;
            windowName = "window_city_trade";
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.FreePersonCount > 0 &&
                    TargetCity.hasBusiness > 0 &&
                    TargetCity.CheckJobCost(CityJobType.TradeFood) &&
                    TargetCity.GetJobCounter((int)CityJobType.TradeFood) == 0 &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.TradeFood);
            }
        }

        public override int CalculateWonderNumber()
        {
            if (personList.Count == 0)
                return 0;
            wonderNumber = GameUtility.Method_Trade(personList[0].Politics);
            return wonderNumber;
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendTrade(TargetCity.freePersons);
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
                if (targetValue != 0)
                {
                    TargetCity.JobTradeFood(personList.ToArray(), targetValue);

                    GameMedia.Instance.PlayDoAcitonSfx();
                }
                Done();
            }
        }
    }
}
