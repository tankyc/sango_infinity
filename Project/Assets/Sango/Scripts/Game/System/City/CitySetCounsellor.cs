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
                return true;
            }
        }

        public override void OnEnter()
        {
            personList.Clear();
            targetList.Clear();
            TargetForce = TargetCity.BelongForce;
            counsellor = TargetForce.Counsellor;
            Scenario.Cur.personSet.ForEach(x =>
            {
                if (x.BelongForce == TargetForce && x != TargetForce.Governor && x != TargetForce.Counsellor && !x.IsPrisoner)
                {
                    targetList.Add(x);
                }
            });
            targetList.Sort((a, b) => -PersonSortFunction.SortByIntelligence.personSortFunc.Invoke(a, b));
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
            GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧", () =>
            {
                // TODO:展示武将
                // 暂时直接招募
                GameDialog.Close();
                Done();

            });
            dialog1.SetPerson(personList[0]);
        }
    }
}
