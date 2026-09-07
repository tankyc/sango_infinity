using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem(autoInit = false)]
    public class CityRecruitTroops : CityBaseSystem
    {
        public CityRecruitTroops()
        {
            customTitleName = "征兵";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByGlamour,
            };
            customMenuName = "都市/征兵";
            customMenuOrder = 15;
            windowName = "window_city_recruit_troops";
        }

        /// <summary>场景数据就绪后创建名声特性列，避免构造系统时访问未初始化的场景。</summary>
        public override void OnEnter()
        {
            customTitleList[1] = CreateRecruitPersonSortTitle();
            base.OnEnter();
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.FreePersonCount > 0 && 
                    TargetCity.GetFreeBuilding((int)BuildingKindType.Barracks) != null &&
                    TargetCity.CheckJobCost(CityJobType.RecruitTroops) &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.RecruitTroops);
            }
        }
        
        public override int CalculateWonderNumber()
        {
            return TargetCity.JobRecruitTroop(personList.ToArray(), true);
        }

        /// <summary>按名声优先、魅力倒序自动推荐最多三名征兵武将。</summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
            List<Person> people = new List<Person>(TargetCity.freePersons);
            // 名声特性直接提高征兵数量，因此优先于魅力参与排序。
            people.Sort((a, b) => CreateRecruitPersonSortTitle().Sort(a, b));
            if (people.Count > 3) people.RemoveRange(3, people.Count - 3);
            if (people != null)
            {
                for (int i = 0; i < people.Count; ++i)
                {
                    Person p = people[i];
                    if (p != null)
                        personList.Add(p);
                }
            }
        }

        /// <summary>创建征兵专用排序列：名声优先，同组内按魅力倒序。</summary>
        private static PersonSortFunction.SortTitle CreateRecruitPersonSortTitle()
        {
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.GetSortByFeatrueId(80);
            sortTitle.personSortFunc = (a, b) =>
            {
                // 先比较名声特性，确保具有征兵加成的武将始终位于列表前部。
                int featureCompare = b.HasFeatrue(80).CompareTo(a.HasFeatrue(80));
                return featureCompare != 0 ? featureCompare : b.Glamour.CompareTo(a.Glamour);
            };
            return sortTitle;
        }

        public override void DoJob()
        {
            if (personList.Count > 0)
            {
                TargetCity.JobRecruitTroop(personList.ToArray());
                Done();
                GameMedia.Instance.PlayDoAcitonSfx();
            }
        }
    }
}
