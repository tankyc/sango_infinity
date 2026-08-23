using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityExilePerson : CityBaseSystem
    {
        public Force TargetForce;
        public List<Person> targetList = new List<Person>();

        public CityExilePerson()
        {
            customTitleName = "流放";
            customMenuName = "君主/流放";
            customMenuOrder = 901;
            windowName = "";
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

                TargetForce = TargetCity.mBelongForce;
                targetList.Clear();
                // 属下
                Scenario.Cur.personSet.ForEach(x =>
                {
                    if (x.mBelongForce == TargetForce && x != TargetForce.mGovernor && x != TargetForce.mCounsellor && !x.IsPrisoner)
                    {
                        targetList.Add(x);
                    }
                });

                // 俘虏
                Scenario.Cur.citySet.ForEach((System.Action<City>)(x =>
                {
                    if (x.mBelongForce == TargetForce)
                    {
                        x.captiveList.ForEach(y =>
                        {
                            targetList.Add(y);
                        });
                    }
                }));

                return targetList.Count > 0;
            }
        }

        public override void OnEnter()
        {
            personList.Clear();
            targetList.Sort((a, b) => -PersonSortFunction.SortByIntelligence.personSortFunc.Invoke(a, b));
            PersonSelectSystem personSelectSystem = GameSystem.GetSystem<PersonSelectSystem>();
            personSelectSystem.Start(
                targetList,
                personList, targetList.Count, OnPersonSelected, null, null);
        }

        void OnPersonSelected(List<Person> person_list)
        {
            if (person_list.Count <= 0)
            {
                Done();
                return;
            }

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"再见了!!", () =>
            {
                // 暂时直接招募
                Done();

            }, null, person_list[0]);

            string content = "";
            if (person_list.Count > 1)
            {
                content = $"流放了{person_list[0].ColorName}等{person_list.Count}人!";
            }
            else
            {
                content = $"流放了{person_list[0].ColorName}!";
            }
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, content, () =>
            {
                for (int i = 0; i < person_list.Count; i++)
                {
                    Person person = person_list[i];
                    if (person.IsPrisoner)
                    {
                        person.Escape(EscapeType.Released, TargetForce);
                    }
                    else
                    {
                        person.LeaveToWild();
                        // 流放到临近都市
                        person.ChangeBelongCity(TargetCity.RandomNerghbor());
                        person.mCurrentCity = person.mBelongCity;
                        TargetForce.PersonCount--;
                    }
                }

                TargetForce.ForEachCorps(x =>
                {
                    x.CheckValid();
                });

                Done();

            }, 56);
        }

        public override void OnDestroy()
        {

        }
    }
}
