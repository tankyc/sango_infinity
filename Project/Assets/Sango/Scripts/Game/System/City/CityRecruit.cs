using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityRecruit : CityBaseSystem
    {
        public string customTargetTitleName;
        public string customActionTitleName;
        public List<ObjectSortTitle> customTargetTitleList;
        public List<ObjectSortTitle> customActionTitleList;
        public List<Person> targetList = new List<Person>();
        public List<Person> target = new List<Person>();
        public List<Person> counsellorRecommendList = new List<Person>();

        public CityRecruit()
        {

            customTargetTitleName = "登庸";
            customTitleName = customTargetTitleName;
            customActionTitleName = "执行武将";

            customMenuName = "人事/登庸武将";
            customMenuOrder = 231;
            windowName = "window_city_recruit";

            customActionTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.GetSortByRecruitRecommend(counsellorRecommendList),
                PersonSortFunction.SortByGlamour,
            };

        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 &&
                    TargetCity.mCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.RecruitPerson);
            }
        }

        public override void OnEnter()
        {
            personList.Clear();
            target.Clear();
            counsellorRecommendList.Clear();

            customTargetTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByBelongForce,
                PersonSortFunction.SortByState,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.GetSortByDistanceDay(TargetCity),
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByCurrentCity,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
            };
            targetList.Clear();
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (!x.IsSameForce(TargetCity))
                {
                    //int dis = x.Distance(TargetCity);
                    //if (dis < 10)
                    {
                        x.allPersons.ForEach(y => { 
                            if(y.state != (int)PersonStateType.Governor && y.state != (int)PersonStateType.Prisoner)
                                targetList.Add(y); 
                        
                        });
                        x.wildPersons.ForEach(y => { targetList.Add(y); });
                    }
                }
                else
                {
                    //int dis = x.Distance(TargetCity);
                    //if (dis < 10)
                    {
                        x.wildPersons.ForEach(y => { targetList.Add(y); });
                        x.captiveList.ForEach(y => { targetList.Add(y); });
                    }
                }
            });
            targetList.Sort((a, b) =>
            {
                if (a.state == b.state)
                {
                    if (a.loyalty == b.loyalty)
                    {
                        return a.DistanceDays(TargetCity).CompareTo(b.DistanceDays(TargetCity));
                    }
                    else
                    {
                        return a.loyalty.CompareTo(b.loyalty);
                    }
                }
                else
                {
                    return -a.state.CompareTo(b.state);
                }
            });

            if (customActionTitleList == null)
            {
                customActionTitleList = new List<ObjectSortTitle>()
                {
                    PersonSortFunction.SortByName,
                    PersonSortFunction.GetSortByRecruitRecommend(counsellorRecommendList),
                    PersonSortFunction.SortByGlamour,
                };
            }

            Window.Instance.Open(windowName);
        }
        public override void OnDestroy()
        {
            GameDialog.Close();
            Window.Instance.Close(windowName);
        }

        public void SetTarget(List<Person> target)
        {
            this.target = target;
            personList.Clear();

            if (target.Count > 0)
            {
                counsellorRecommendList.Clear();

                for (int i = 0; i < TargetCity.freePersons.Count; i++)
                {
                    Person person = TargetCity.freePersons[i];
                    int probability = GameFormula.Instance.RecruitPersonProbability(person, target[0], 0);
                    if (probability >= 30)
                    {
                        counsellorRecommendList.Add(person);
                    }
                }

                counsellorRecommendList.Sort((a, b) =>
                {
                    int probA = GameFormula.Instance.RecruitPersonProbability(a, target[0], 0);
                    int probB = GameFormula.Instance.RecruitPersonProbability(b, target[0], 0);
                    return probB.CompareTo(probA);
                });

                if (counsellorRecommendList.Count > 0)
                {
                    personList.Add(counsellorRecommendList[0]);
                }
            }
        }

        public override void DoJob()
        {
            if (personList.Count <= 0 || target.Count <= 0)
                return;

            if (!TargetCity.JobRecruitPerson(personList[0], target[0]))
            {
                GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧", () =>
                {
                    // 暂时直接招募
                    GameDialog.Close();
                    Done();

                });
                dialog1.SetPerson(personList[0]);
            }
            else
            {
                Done();
            }
        }

    }
}
