using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 都市人事「褒赏」Job 的玩家侧入口系统：注册"人事/褒赏"菜单、按候选口径筛出可褒赏武将、
    /// 在菜单构建阶段提前判定无候选则置灰按钮，并派发执行。忠诚提升逻辑不在本类，统一委托 City.JobRewardPersons。
    /// </summary>
    [GameSystem]
    public class CityReward : CityBaseSystem
    {
        public List<Person> targetList = new List<Person>();

        public CityReward()
        {
            customTitleName = "褒赏";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByBelongCorps,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByWaterLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "人事/褒赏";
            customMenuOrder = 2400;
            windowName = "window_city_reward";
        }

        protected override bool MenuCanShow()
        {
            return TargetCity.IsCityBase();
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.gold > 100 &&
                       TargetCity.CheckJobCost(CityJobType.Reward) &&
                       TargetCity.BelongCorps.GetJobCounter((int)CityJobType.Reward) == 0 &&
                       TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Reward)
                       HasRewardTarget();;
            }
        }

        /// <summary>
        /// 提前执行褒赏候选口径，判断本城所属势力当前是否存在可褒赏武将。
        /// 菜单构建（点开「人事」）阶段由 IsValid 调用，无候选时菜单项直接置灰，避免进入空候选窗口。
        /// 返回结果只影响按钮可用性，不改变任何数值状态。
        /// </summary>
        /// <returns>存在至少一名满足 IsRewardTarget 的武将时返回 true。</returns>
        private bool HasRewardTarget()
        {
            bool found = false;
            TargetCity.mBelongForce.ForEachPerson(x =>
            {
                // ForEachPerson 不支持中途退出，用 found 短路掉后续命中判断。
                if (!found && IsRewardTarget(x))
                    found = true;
            });
            return found;
        }

        /// <summary>
        /// 褒赏候选口径：不是本势力太守、未随军（无所属部队）且忠诚低于 100。
        /// OnEnter 填充候选列表与 IsValid 的置灰判定共用本方法，避免两处口径分叉。
        /// </summary>
        /// <param name="person">待判定的武将，由 ForEachPerson 保证非空、存活且属于本势力。</param>
        /// <returns>该武将是否可被褒赏。</returns>
        private bool IsRewardTarget(Person person)
        {
            return person != TargetCity.mBelongForce.mGovernor && person.mTroop == null && person.loyalty < 100;
        }

        public override void OnEnter()
        {
            targetList.Clear();
            TargetCity.BelongForce.ForEachPerson(x =>
            {
                if (IsRewardTarget(x))
                {
                    targetList.Add(x);
                }
            });
            targetList.Sort((a, b) => PersonSortFunction.SortByLoyalty.valueSortFunc.Invoke(a, b));
            base.OnEnter();
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            if (targetList.Count > 0)
            {
                personList.Add(targetList[0]);
            }
        }

        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            base.OnDestroy();
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            TargetCity.JobRewardPersons(personList.ToArray());
            Done();
            GameMedia.Instance.PlayDoAcitonSfx();
        }
    }
}