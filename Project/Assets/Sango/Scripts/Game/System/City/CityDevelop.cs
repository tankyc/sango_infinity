using System.Collections.Generic;

namespace Sango.Core.Player
{
    public class CityDevelop : CityBaseSystem
    {
        public CityDevelop()
        {
            customTitleName = "商业";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByIntelligence,
            };
            customMenuName = "都市/商业";
            customMenuOrder = 5;
            windowName = "window_city_command_base";
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 
                    && TargetCity.commerce < TargetCity.commerceLimit 
                    && TargetCity.CheckJobCost(CityJobType.Develop)
                    && TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Develop);
            }
        }

        //protected override void OnUIInit()
        //{
        //    base.OnUIInit();
        //    targetUI.title_value.gameObject.SetActive(true);
        //    targetUI.value_value.gameObject.SetActive(true);
        //    targetUI.title_gold.gameObject.SetActive(true);
        //    targetUI.value_gold.gameObject.SetActive(true);

        //    targetUI.title_value.text = "商业";
        //    targetUI.title_gold.text = "资金";

        //    int destValue = TargetCity.commerce + wonderNumber;
        //    if (destValue > TargetCity.CommerceLimit)
        //        destValue = TargetCity.CommerceLimit;

        //    targetUI.value_value.text = $"{TargetCity.commerce}→{destValue}";
        //    targetUI.value_gold.text = $"{TargetCity.GetJobCost(CityJobType.Develop)}/{TargetCity.gold}";

        //    targetUI.action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.Develop)}/{TargetCity.BelongCorps.ActionPoint}";


        //}

        public override int CalculateWonderNumber()
        {
            return TargetCity.JobDevelop(personList.ToArray(), true);
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendDevelop(TargetCity.freePersons);
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
                TargetCity.JobDevelop(personList.ToArray());
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            }
        }
    }
}
