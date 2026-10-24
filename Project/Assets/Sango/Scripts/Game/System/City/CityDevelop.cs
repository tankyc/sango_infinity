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
            customMenuOrder = 200;
            windowName = "window_city_command_base";
        }

        public override bool IsValid
        {
            get
            {
                // 兵临城下禁止开发 —— 对齐 can_develop_command（s11_sys_person_order_city.cpp:25-33）
                if (TargetCity.HasEnemyNearby())
                    return false;
                return TargetCity.freePersons.Count > 0 
                    && TargetCity.commerce < TargetCity.commerceLimit 
                    && TargetCity.CheckJobCost(CityJobType.Develop)
                    && TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Develop);
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.HasEnemyNearby())
                return "敌军兵临";
            if (TargetCity.freePersons.Count <= 0)
                return "无空闲武将";
            if (TargetCity.commerce >= TargetCity.commerceLimit)
                return "商业已达上限";
            if (!TargetCity.CheckJobCost(CityJobType.Develop))
                return "资金不足";
            if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.Develop))
                return "行动力不足";
            return null;
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

        //    targetUI.action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.Develop)}/{TargetCity.BelongCorpsId.ActionPoint}";


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
