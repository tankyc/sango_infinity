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
                // 【已移除"整城每回合一次"的限制】
                // 原版褒赏没有城市级计数，只有**逐人**的 `PersonStatus_BonusDone`：
                // 一条命令可以褒赏多人（exec_bonus_command 里 `for (person : actors)`，
                // 每人各自 `set_bonus_done(true)`），同一城同一回合可以连续褒赏不同的人。
                // 原实现用 `GetJobCounter(Reward) == 0` 挡住整城第二次操作，比原版严得多。
                // 现在"每人每回合只能一次"由 City.CanBeRewarded 的 IsBonusDone 保证。
                return TargetCity.gold > 100 &&
                       TargetCity.CheckJobCost(CityJobType.Reward) &&
                       TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Reward) &&
                       HasRewardTarget();
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.gold <= 100)
                return "资金不足";
            if (!TargetCity.CheckJobCost(CityJobType.Reward))
                return "资金不足";
            if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.Reward))
                return "行动力不足";
            if (!HasRewardTarget())
                return "无可褒赏武将";
            return null;
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
            TargetCity.BelongForce.ForEachPerson(x =>
            {
                // ForEachPerson 不支持中途退出，用 found 短路掉后续命中判断。
                if (!found && IsRewardTarget(x))
                    found = true;
            });
            return found;
        }

        /// <summary>
        /// 褒赏候选口径，直接转给 <see cref="City.CanBeRewarded"/>。
        ///
        /// 口径与原版 `can_bonus`（s11_sys_person_order_personnel.cpp:1187-1210）逐项对齐：
        /// 非事件/NPC/异族、身份为都督/太守/一般（不含君主）、不在部队、不在出征途中、
        /// 本回合未褒赏过、忠诚 &lt; 100。
        /// OnEnter 填充候选列表与 IsValid 的置灰判定共用本方法，且与执行侧
        /// <see cref="City.JobRewardPerson"/> / <see cref="City.JobRewardPersons"/> 完全一致，
        /// 不会再出现"界面能点、执行被拒"的分叉。
        /// </summary>
        /// <param name="person">待判定的武将，由 ForEachPerson 保证非空、存活且属于本势力。</param>
        /// <returns>该武将是否可被褒赏。</returns>
        private bool IsRewardTarget(Person person)
        {
            return TargetCity.CanBeRewarded(person);
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