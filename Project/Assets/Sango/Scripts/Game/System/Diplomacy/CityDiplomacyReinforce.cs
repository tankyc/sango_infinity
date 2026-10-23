using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 援军请求（城池菜单命令）—— 对应原版 `PersonOrderId_Reinforce`（0x15）。
    ///
    /// 【与原版一致】
    ///   · 只能向**同盟**提出（原版 `can_reinforce_command` 要求 `get_allied_force_list` 非空）；
    ///   · 同一时间只允许一个进行中的请求（`requestForceId >= 0` 时不可再提）；
    ///   · 失败会让对方进入禁谈期（由 Action 结算，菜单只管前置）。
    /// </summary>
    [GameSystem]
    public class CityDiplomacyReinforce : CityDiplomacyBase
    {
        public CityDiplomacyReinforce()
        {
            customTitleName = "请求援军";

            customMenuName = "外交/请求援军";
            customMenuOrder = 3700;

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

                Force force = TargetCity.BelongForce;
                if (force == null)
                    return false;

                // 已有进行中的援军请求
                if (force.requestForceId >= 0 || force.requestTimer > 0)
                    return false;

                if (force.diplomacyBanTimer > 0)
                    return false;

                if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.Alliance))
                    return false;

                // 至少存在一个同盟对象
                bool hasAlly = false;
                if (force.AllianceList != null)
                {
                    force.AllianceList.ForEach(a =>
                    {
                        if (!hasAlly && a != null && a.IsAlive && a.allianceType == AllianceType.Alliance)
                            hasAlly = true;
                    });
                }

                return hasAlly;
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
                DiplomacyActionType.Reinforce, TargetCity.BelongForce, targetForces[0], personList[0], gold);

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
