using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 都市内政「巡视」Job 的玩家侧入口系统：注册右键菜单、定义选择器列（武将|统率倒序）、
    /// 推荐默认人选（统率最高的三人）并派发执行。治安数值结算不在本类，统一委托 City.JobInspection。
    /// </summary>
    [GameSystem(autoInit = false)]
    public class CityInspection : CityBaseSystem
    {
        public CityInspection()
        {
            customTitleName = "巡视";
            // 统率列使用巡视专用副本（倒序），使选择器打开时统率最高的武将排在首屏。
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                CreateInspectionCommandSortTitle(),
            };
            customMenuName = "都市/巡视";
            customMenuOrder = 600;
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
                    && TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Inspection);
            }
        }

        public override int CalculateWonderNumber()
        {
            return TargetCity.JobInspection(personList.ToArray(), true);
        }

        /// <summary>按统率倒序自动推荐最多三名巡视武将。</summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
            List<Person> people = new List<Person>(TargetCity.freePersons);
            // 候选中可能残留空引用，先剔除再排序，避免空引用占用推荐名额。
            people.RemoveAll(x => x == null);
            // 巡视的治安能力等于统率（Person.BaseSecurityAbility），故直接取统率最高的三人。
            PersonSortFunction.SortTitle commandSortTitle = CreateInspectionCommandSortTitle();
            people.Sort((a, b) => commandSortTitle.Sort(a, b));
            if (people.Count > 3) people.RemoveRange(3, people.Count - 3);
            for (int i = 0; i < people.Count; ++i)
            {
                personList.Add(people[i]);
            }
        }

        /// <summary>创建巡视专用统率列：按统率倒序，使统率最高的武将排在列表前部。</summary>
        private static PersonSortFunction.SortTitle CreateInspectionCommandSortTitle()
        {
            // 取共享静态列的副本再覆写比较器，避免把其它界面的统率列默认排序方向一起改成倒序。
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.SortByCommand.Copy();
            sortTitle.valueSortFunc = (a, b) => b.Command.CompareTo(a.Command);
            return sortTitle;
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
