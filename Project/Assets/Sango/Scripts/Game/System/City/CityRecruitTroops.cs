using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 都市内政「征兵」Job 的玩家侧入口系统：注册右键菜单、定义选择器列（武将|名声|魅力）、
    /// 推荐默认人选（名声优先、魅力倒序取三人）并派发执行。兵力数值结算不在本类，统一委托 City.JobRecruitTroop。
    /// </summary>
    [GameSystem(autoInit = false)]
    public class CityRecruitTroops : CityBaseSystem
    {
        public CityRecruitTroops()
        {
            customTitleName = "征兵";
            // 构造期取不到场景数据，先声明 武将|魅力 两列，OnEnter 再重建为 武将|名声|魅力。
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByGlamour,
            };
            customMenuName = "都市/征兵";
            customMenuOrder = 400;
            windowName = "window_city_recruit_troops";
        }

        /// <summary>场景数据就绪后重建选择器列：武将|名声|魅力，避免构造系统时访问未初始化的场景。</summary>
        public override void OnEnter()
        {
            // 整体重建而非按下标替换，保证反复进入征兵流程时列数不会累加。
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                CreateRecruitPersonSortTitle(),
                CreateRecruitGlamourSortTitle(),
            };
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
            // SortTitle 的标准比较器字段为 valueSortFunc，按名声特性与魅力排序。
            sortTitle.valueSortFunc = (a, b) =>
            {
                // 先比较名声特性，确保具有征兵加成的武将始终位于列表前部。
                int featureCompare = b.HasFeatrue(80).CompareTo(a.HasFeatrue(80));
                return featureCompare != 0 ? featureCompare : b.Glamour.CompareTo(a.Glamour);
            };
            return sortTitle;
        }

        /// <summary>创建征兵专用魅力列：展示魅力数值，点击表头时按魅力倒序，与推荐口径「名声优先、魅力倒序」保持一致。</summary>
        private static PersonSortFunction.SortTitle CreateRecruitGlamourSortTitle()
        {
            // 取共享静态列的副本再覆写比较器，避免把其它界面的魅力列默认排序方向一起改成倒序。
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.SortByGlamour.Copy();
            sortTitle.valueSortFunc = (a, b) => b.Glamour.CompareTo(a.Glamour);
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
