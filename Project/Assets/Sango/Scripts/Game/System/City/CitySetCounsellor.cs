using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CitySetCounsellor : CityBaseSystem
    {
        public Force TargetForce;
        public List<Person> targetList = new List<Person>();
        public Person counsellor;

        public CitySetCounsellor()
        {
            customTitleName = "军师";
            customMenuName = "君主/军师";
            customMenuOrder = 901;
            windowName = "window_city_set_counsellor";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByIsCounsellor,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByOfficial,
                PersonSortFunction.SortByMerit,
            };

        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.mBelongForce.PersonCount > 1;
            }
        }

        public override void OnEnter()
        {
            personList.Clear();
            targetList.Clear();
            TargetForce = TargetCity.mBelongForce;
            counsellor = TargetForce.mCounsellor;
            Scenario.Cur.personSet.ForEach(x =>
            {
                if (x.mBelongForce == TargetForce && x != TargetForce.mGovernor && x != TargetForce.mCounsellor && !x.IsPrisoner)
                {
                    targetList.Add(x);
                }
            });
            targetList.Sort((a, b) => -PersonSortFunction.SortByIntelligence.valueSortFunc.Invoke(a, b));
            Window.Instance.Open(windowName);
        }
        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            Window.Instance.Close(windowName);
        }

        public void ClearCounsellor()
        {
            counsellor = null;
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
            {
                TargetForce.ChangeCounsellor(null);
                Done();
                return;
            }

            GameMedia.Instance.PlayDoAcitonSfx();
            TargetForce.ChangeCounsellor(personList[0]);
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧", () =>
            {
                // TODO:展示武将
                // 暂时直接招募
                Done();

            }, personList[0]);
        }
    }
}
