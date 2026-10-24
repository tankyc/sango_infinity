using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityTransformPerson : CityBaseSystem
    {
        public List<City> transformTo = new List<City>();
        public List<ObjectSortTitle> citySortTitleList;
        public CityTransformPerson()
        {
            customTitleName = "移动";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByWork,
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
            customMenuName = "人事/移动";
            customMenuOrder = 2000;
            windowName = "window_city_person_transform";

            citySortTitleList = new List<ObjectSortTitle>()
            {
                CitySortFunction.SortByName,
                CitySortFunction.SortByPersonCount,
                CitySortFunction.SortByBelongCity,
                CitySortFunction.SortByTroops,
                CitySortFunction.SortByGold,
                CitySortFunction.SortByFood,
                CitySortFunction.SortByLevel,
            };
        }

        protected override bool MenuCanShow()
        {
            return true;
        }


        public override bool IsValid
        {
            get
            {
                // 敌军兵临则禁止人员调动 —— 对齐原版 validate_move_command 的 building_has_enemy
                if (TargetCity.HasEnemyNearby())
                    return false;
                // 需要至少两座城
                return TargetCity.BelongForce.CityBaseCount > 1 &&
                    TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.TransformPerson) && TargetCity.FreePersonCount > 0;
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.HasEnemyNearby())
                return "敌军兵临";
            if (TargetCity.BelongForce.CityBaseCount <= 1)
                return "仅有一座据点";
            if (TargetCity.FreePersonCount <= 0)
                return "无空闲武将";
            if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.TransformPerson))
                return "行动力不足";
            return null;
        }

        public override void OnEnter()
        {
            transformTo.Clear();
            base.OnEnter();
        }

        public override void DoJob()
        {
            // 【失败必须可见】原版 exec 先走 can_/validate_ 并把错误文案回传给玩家；
            // 这里原先两条 `return` 都是静默的，玩家点确认后界面直接关掉、什么也没发生。
            if (personList.Count == 0)
            {
                PlayerMessage.AddTextMessage("请先选择要移动的武将。",
                    TargetCity.BelongForce, TargetCity.x, TargetCity.y);
                return;
            }
            if (transformTo.Count == 0)
            {
                PlayerMessage.AddTextMessage("请先选择目标城池。",
                    TargetCity.BelongForce, TargetCity.x, TargetCity.y);
                return;
            }

            for (int i = 0; i < personList.Count; i++)
            {
                personList[i].TransformToCity(transformTo[0]);
            }
            // 【修正两处】
            // ① 键用错了：这里扣的是"派遣"，却取 `CallPerson`（召唤）的成本；
            //    两个工作当前恰好同价（JobTypes 14/15 都是 20），改配置就会错扣。
            // ② 裸 `-=` 绕过 ReduceActionPoint，漏掉据点 AI 的记账钩子。
            TargetCity.BelongCorps.ReduceActionPoint(TargetCity.GetJobCostAP(CityJobType.TransformPerson));
            GameMedia.Instance.PlayDoAcitonSfx();
            base.DoJob();
        }
    }
}
