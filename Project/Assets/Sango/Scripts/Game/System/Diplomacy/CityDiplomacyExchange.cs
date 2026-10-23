using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 俘虏交换（城池菜单命令）—— 对应原版 `PersonOrderId_Exchange`（0x14）。
    ///
    /// 【与"赎回俘虏"的区别】原版没有单独的赎买命令，赎回只是交换的一种退化形态
    /// （`release == nullptr` 时只索要、不带回）。本命令同时覆盖两种用法：
    /// UI 里选中"我方交出的俘虏"即为交换，不选则只索要。
    /// </summary>
    [GameSystem]
    public class CityDiplomacyExchange : CityDiplomacyBase
    {
        public CityDiplomacyExchange()
        {
            customTitleName = "俘虏交换";

            customMenuName = "外交/俘虏交换";
            customMenuOrder = 3600;

            // 复用结盟窗口（目标势力 / 使者 / 金额 / 确认），标题由 customTitleName 区分
            windowName = "window_city_diplomacy_alliance";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByPolitics,
            };
        }

        public override bool IsValid
        {
            get
            {
                if (TargetCity == null || TargetCity.BelongCorps == null)
                    return false;

                if (TargetCity.freePersons.Count <= 0)
                    return false;

                // 我方必须有俘虏可作筹码（原版 can_exchange_command 的核心条件）
                Force force = TargetCity.BelongForce;
                if (force == null || force.BeCaptiveList == null || force.BeCaptiveList.Count <= 0)
                    return false;

                if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.Alliance))
                    return false;

                if (force.diplomacyBanTimer > 0)
                    return false;

                return TargetCity.gold >= 1000;
            }
        }

        public override void OnEnter()
        {
            Activate();
            gold = 1000;
            targetForces.Clear();
            personList.Clear();
            customBelongForceTitleList = new List<ObjectSortTitle>()
            {
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
                ForceSortFunction.GetSortByDistanceDay(TargetCity)
            };

            Window.Instance.Open(windowName);
        }

        public override void OnDestroy()
        {
            Deactivate();
            Window.Instance.Close(windowName);
        }

        public override void DoJob()
        {
            if (personList.Count <= 0 || targetForces.Count <= 0)
                return;

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(
                DiplomacyActionType.Exchange, TargetCity.BelongForce, targetForces[0], personList[0], gold);

            if (action == null)
                return;

            diplomacyManager.DispatchDiplomat(action);

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, "交给我吧, 保证完成任务!!", () =>
            {
                Done();
            }, null, personList[0], 5);
        }
    }
}
