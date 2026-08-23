using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityDiplomacySendGift : CityBaseSystem
    {
        public List<Force> targetForces = new List<Force>();
        public List<ObjectSortTitle> customBelongForceTitleList;



        public CityDiplomacySendGift()
        {
            customTitleName = "送礼";

            customMenuName = "外交/送礼";
            customMenuOrder = 300;
            windowName = "window_city_diplomacy_send_gift";
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
                return TargetCity.freePersons.Count > 0 && TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.SendGift) && TargetCity.gold >= 1000;
            }
        }

        public override void OnEnter()
        {
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
            Window.Instance.Close(windowName);
        }

        public override void DoJob()
        {
            if (personList.Count <= 0 || targetForces.Count <= 0)
                return;

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(DiplomacyActionType.SendGift, TargetCity.mBelongForce, targetForces[0], personList[0], JobType.GetJobCost((int)CityJobType.SendGift));
            diplomacyManager.DispatchDiplomat(action);
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧, 保证完成任务!!", () =>
            {
                // 暂时直接招募
                Done();

            }, null, personList[0], 5);
        }
    }
}