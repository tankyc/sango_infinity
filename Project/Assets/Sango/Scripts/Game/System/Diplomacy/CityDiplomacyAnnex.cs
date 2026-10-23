using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 劝降（城池菜单命令）—— 对应原版 `PersonOrderId_Annex`（0x13）。
    ///
    /// 【与原版一致】劝降**不花金钱**，只要求行动力与"存在满足硬条件的目标势力"；
    /// 硬条件链（势力数 / 年数 / 城数比 / 兵力比 / 相邻 / 宁死不降）由
    /// <see cref="DiplomacyActionAnnex.CanPerform"/> 判定，菜单只做粗筛。
    /// </summary>
    [GameSystem]
    public class CityDiplomacyAnnex : CityDiplomacyBase
    {
        public CityDiplomacyAnnex()
        {
            customTitleName = "劝降";

            customMenuName = "外交/劝降";
            customMenuOrder = 3500;

            // 复用结盟窗口：目标势力 / 使者 / 关系 / 距离 / 确认按钮完全通用，
            // 只有标题与金额条不同（劝降不花钱，NeedGold 覆盖为 false）。
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

                // 劝降不放宽行动力要求：沿用结盟的 AP 成本
                if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.Alliance))
                    return false;

                // 禁谈期内不可发起
                if (TargetCity.BelongForce != null && TargetCity.BelongForce.diplomacyBanTimer > 0)
                    return false;

                return true;
            }
        }

        /// <summary>劝降不花钱，隐藏金额条</summary>
        public override bool NeedGold => false;

        public override void OnEnter()
        {
            Activate();
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
                DiplomacyActionType.Annex, TargetCity.BelongForce, targetForces[0], personList[0], 0);

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
