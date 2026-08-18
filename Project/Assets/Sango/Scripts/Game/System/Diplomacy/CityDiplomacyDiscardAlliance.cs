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
            customMenuOrder = 300;
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
                return TargetCity.freePersons.Count > 0 && TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.DiscardAlliance) && TargetCity.gold >= 1000;
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
            GameDialog.Close();
            Window.Instance.Close(windowName);
        }

        public override void DoJob()
        {
            if (targetForces.Count <= 0)
                return;

            TargetCity.mBelongForce.AllianceList.ForEach(x =>
            {
                if(x.Contains(targetForces[0]))
                {
                    x.ForceList.ForEach(y =>
                    {
                        y.AllianceList.Remove(x);
                    });
                }
            });

            GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickSay, $"与{targetForces[0].ColorName}不再是同盟!!", () =>
            {
                // 暂时直接招募
                GameDialog.Close();
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            });
        }
    }
}