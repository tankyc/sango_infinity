using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityDiplomacyAlliance : CityDiplomacyBase
    {
        public CityDiplomacyAlliance()
        {
            customTitleName = "结盟";

            customMenuName = "外交/结盟";
            customMenuOrder = 3100;
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
                return TargetCity.freePersons.Count > 0
                    && TargetCity.BelongCorps.ActionPoint >= TargetCity.GetJobCostAP(CityJobType.Alliance)
                    && TargetCity.gold >= 1000;
            }
        }

        public override void OnEnter()
        {
            Activate();     // 复用 window_city_diplomacy_alliance 时，UI 靠它拿到当前命令
            gold = 0;
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
            if (personList.Count <= 0)
                return;

            if (targetForces.Count <= 0)
                return;

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.Alliance, TargetCity.BelongForce, targetForces[0], personList[0], gold);
            diplomacyManager.DispatchDiplomat(action);

            // 【先退出命令，再播台词】
            // Done() 原先挂在 GameDialog 的点击回调里，而该回调会被静默丢弃
            // （GameDialog 窗口创建失败 continue / Reset / seq 不匹配），
            // 一旦丢失 CurrentCommand 永远不为 null，整个回合流程就卡死。
            // 派遣本身在上一步已经完成，关窗不必等这句台词。
            Done();
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧, 保证完成任务!!", null, null, personList[0], 5);
        }
    }
}