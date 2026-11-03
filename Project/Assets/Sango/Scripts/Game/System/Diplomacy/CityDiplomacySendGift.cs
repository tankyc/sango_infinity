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
            customMenuOrder = 3000;
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
                // 资金门槛必须与 DoJob 的实际扣费同源（外交府可让亲善资金减半），
                // 原先写死 `gold >= 1000`，与扣费值不同源。
                return TargetCity.freePersons.Count > 0
                    && TargetCity.BelongCorps.ActionPoint >= TargetCity.GetJobCostAP(CityJobType.SendGift)
                    && TargetCity.gold >= TargetCity.GetJobCost(CityJobType.SendGift);
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
            // 走本城口径：外交府可让亲善资金减半
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(
                DiplomacyActionType.SendGift, TargetCity.BelongForce, targetForces[0], personList[0],
                TargetCity.GetJobCost(CityJobType.SendGift));
            diplomacyManager.DispatchDiplomat(action);

            // 【先退出命令，再播台词】Done() 不能挂在 GameDialog 回调上：
            // 该回调会被静默丢弃（窗口创建失败 continue / Reset / seq 不匹配），
            // 一旦丢失 CurrentCommand 永远不为 null，整个回合流程卡死。
            Done();
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧, 保证完成任务!!", null, null, personList[0], 5);
        }
    }
}