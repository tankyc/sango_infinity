using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
   // [GameSystem]
    public class CityDiplomacyTruce : CityBaseSystem
    {
        public List<Person> counsellorRecommendList = new List<Person>();

        /// <summary>
        /// 停战的目标势力（对齐 CityDiplomacyAlliance / SendGift 的同名列表）。
        /// 仅取 <c>[0]</c>：一次命令只与一个势力谈停战。
        /// </summary>
        public List<Force> targetForces = new List<Force>();

        /// <summary>目标势力列表的排序项（UI 侧势力列表用）</summary>
        public List<ObjectSortTitle> customBelongForceTitleList;

        public CityDiplomacyTruce()
        {
            customTitleName = "停战";

            customMenuName = "外交/停战";
            customMenuOrder = 3300;
            windowName = "window_city_diplomacy_truce";

        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 && TargetCity.BelongCorps.ActionPoint >= 1;
            }
        }

        public override void OnEnter()
        {
            // 每次进入都重置目标势力，避免沿用上一次的选择
            targetForces.Clear();
            customBelongForceTitleList = new List<ObjectSortTitle>()
            {
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
                ForceSortFunction.GetSortByDistanceDay(TargetCity)
            };

            counsellorRecommendList.Clear();
            Person[] recommandList = ForceAI.CounsellorRecommendDiplomacy(TargetCity.freePersons);
            if (recommandList != null)
            {
                for (int i = 0; i < recommandList.Length; i++)
                {
                    counsellorRecommendList.Add(recommandList[i]);
                }
            }

            personList.Clear();
            if (counsellorRecommendList.Count > 0)
                personList.Add(counsellorRecommendList[0]);

            if (customTitleList == null)
            {
                customTitleList = new List<ObjectSortTitle>()
                {
                    PersonSortFunction.SortByName,
                    PersonSortFunction.GetSortByContainsInList("军师推荐", counsellorRecommendList),
                    PersonSortFunction.SortByPolitics,
                    PersonSortFunction.SortByGlamour,
                };
            }
            Window.Instance.Open(windowName);
        }

        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            // 【原实现是空壳】旧代码在这里只有一句 Done()：窗口一关、什么都没发生，
            // 玩家点了"停战"却既不派使者也不生成 Alliance，界面外也看不到任何停战标记。
            // 这里接上真实链路：CreateDiplomacyAction(Truce) → DispatchDiplomat。
            // 结算逻辑（创建 Alliance(Truce)、成功率、舌战接管）在
            // DiplomacyActionTruce.Perform / PerformWithoutCheck 里，已对齐原版
            // s11_sys_person_order_diplomacy.cpp:1132-1151。
            if (targetForces.Count <= 0)
            {
                Sango.Log.Warning($"@外交@{TargetCity.Name} 停战：尚未选择目标势力，本次取消");
                Done();
                return;
            }

            DiplomacyManager diplomacyManager = GameSystem.GetSystem<DiplomacyManager>();
            DiplomacyActionBase action = diplomacyManager.CreateDiplomacyAction(
                DiplomacyActionType.Truce, TargetCity.BelongForce, targetForces[0], personList[0], 0);

            if (action != null)
                diplomacyManager.DispatchDiplomat(action);

            // 【先退出命令，再播台词】Done() 挂回调会被 GameDialog 静默丢弃，导致命令不退出、回合卡死
            Done();
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, "交给我吧, 保证完成任务!!", null, null, personList[0], 5);
        }
    }
}