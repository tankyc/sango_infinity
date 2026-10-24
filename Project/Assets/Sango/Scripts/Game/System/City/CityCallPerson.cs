using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityCallPerson : CityBaseSystem
    {
        public CityCallPerson()
        {
            customTitleName = "召唤";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByBelongCorps,
                PersonSortFunction.SortByWork,
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
            customMenuName = "人事/召唤";
            customMenuOrder = 2100;
            windowName = "window_city_person_call";

        }
        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                // 需要至少两座城
                if (TargetCity.BelongForce.CityBaseCount <= 1)
                    return false;
                if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.CallPerson))
                    return false;
                // 别处必须有可召唤的武将。
                // 对齐原版 can_call_command（s11_sys_person_order_personnel.cpp:189-205）：
                // 它把各据点的空闲武将合计起来，为 0 时直接不可用。
                // 原实现只查"有两座城"，当别处全是空城时按钮仍亮，点进去是空列表。
                return HasCallablePersonElsewhere();
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.BelongForce.CityBaseCount <= 1)
                return "仅有一座据点";
            if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.CallPerson))
                return "行动力不足";
            if (!HasCallablePersonElsewhere())
                return "别处无可召唤武将";
            return null;
        }

        /// <summary>
        /// 本势力其它据点是否存在可召唤的武将。
        /// </summary>
        /// <returns>存在返回 true</returns>
        bool HasCallablePersonElsewhere()
        {
            Force force = TargetCity.BelongForce;
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null)
                return false;

            bool found = false;
            scenario.citySet.ForEach(city =>
            {
                if (found || city == null || !city.IsAlive)
                    return;
                if (city == TargetCity || city.BelongForce != force)
                    return;
                // 目标城被敌军兵临则不能作为召唤来源 ——
                // 对齐 can_call_command 的目标筛选（s11_sys_person_order_personnel.cpp:220：
                // `system_->building_has_enemy(building)` 命中即跳过该据点）。
                if (city.HasEnemyNearby())
                    return;
                if (city.FreePersonCount > 0)
                    found = true;
            });
            return found;
        }

        public override void DoJob()
        {
            if (personList.Count == 0) return;

            for (int i = 0; i < personList.Count; i++)
            {
                personList[i].TransformToCity(TargetCity);
            }

            // 走 ReduceActionPoint 而非裸 `-=`：前者内部带据点 AI 记账钩子
            //（OutpostRuntime 需要知道这条命令确实执行了），裸减会漏掉记账。
            TargetCity.BelongCorps.ReduceActionPoint(TargetCity.GetJobCostAP(CityJobType.CallPerson));
            GameMedia.Instance.PlayDoAcitonSfx();
            base.DoJob();
        }
    }
}
