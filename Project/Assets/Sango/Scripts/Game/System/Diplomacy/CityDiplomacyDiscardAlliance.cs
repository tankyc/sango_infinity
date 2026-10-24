using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityDiplomacyDiscardAlliance : CityBaseSystem
    {
        public List<Force> targetForces = new List<Force>();
        public List<ObjectSortTitle> customBelongForceTitleList;

        public CityDiplomacyDiscardAlliance()
        {
            customTitleName = "摒弃";
            customMenuName = "外交/摒弃";
            customMenuOrder = 3200;
            windowName = "window_city_diplomacy_discard_alliance";
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
                // 【去掉 gold >= 1000】弃盟只是解除同盟关系，DoJob 里没有任何资金扣减；
                // 原先的门禁凭空要求 1000 金，资金不足时按钮白灰。
                return TargetCity.freePersons.Count > 0
                    && TargetCity.BelongCorps.ActionPoint >= TargetCity.GetJobCostAP(CityJobType.DiscardAlliance);
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
            if (targetForces.Count <= 0)
                return;

            // 【扣行动力】弃盟不派使者（不经 DiplomacyManager.DispatchDiplomat），
            // 因此必须在这里自己扣一次，口径与 IsValid 同源（含外交府减半）。
            if (TargetCity.BelongCorps != null)
                TargetCity.BelongCorps.ReduceActionPoint(TargetCity.GetJobCostAP(CityJobType.DiscardAlliance));

            // 【关系值与技巧点惩罚】对齐原版破弃同盟（s11_sys_person_order_diplomacy.cpp:861-898）：
            // 友好度 -50、技巧点 -50，并中断停战。
            // 此前**只有 AI 侧**会扣（ForceAI.cs），玩家可以零成本反复背盟。
            Force myForce = TargetCity.BelongForce;
            if (myForce != null && targetForces.Count > 0 && targetForces[0] != null)
            {
                myForce.AddLike(targetForces[0], DiplomacyRules.DiscardLikePenalty);
                myForce.GainTechniquePoint(-DiplomacyRules.DiscardTechCost);
            }

            TargetCity.BelongForce.AllianceList.ForEach(x =>
            {
                if(x.Contains(targetForces[0]))
                {
                    x.ForceList.ForEach(y =>
                    {
                        y.AllianceList.Remove(x);
                    });
                }
            });

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, $"与{targetForces[0].ColorName}不再是同盟!!", () =>
            {
                // 暂时直接招募
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            }, 5);
        }
    }
}