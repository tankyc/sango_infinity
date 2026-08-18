using System.Collections.Generic;
namespace Sango.Core.Player
{
    [GameSystem]
    public class CityUpgradeOfficial : CityBaseSystem
    {
        public List<Person> targetList = new List<Person>();
        public struct UpgradeOfficial
        {
            public Person person;
            public Official official;
        }
        public List<UpgradeOfficial> upgradeList = new List<UpgradeOfficial>();
        public List<ObjectsDisplaySystem.ButtonData> buttonDatas;
        public CityUpgradeOfficial()
        {
            customTitleName = "官职";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByMerit,
                PersonSortFunction.SortByOfficial,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByBelongCorps,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByWaterLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "君主/官职";
            customMenuOrder = 903;
            windowName = "window_official_set";

            buttonDatas = new List<ObjectsDisplaySystem.ButtonData>()
            {
                new ObjectsDisplaySystem.ButtonData()
                {
                    title = "自动晋升",
                    style = 0,
                    action = AutoUpgrade
                },

                new ObjectsDisplaySystem.ButtonData()
                {
                    title = "清理晋升",
                    style = 0,
                    action = AutoClearUpgrade
                }
            };
        }

        public override bool IsValid
        {
            get
            {
                targetList.Clear();
                if (TargetCity.gold > 500)
                {
                    TargetCity.mBelongForce.ForEachPerson(x =>
                  {
                      if (x != TargetCity.mBelongForce.mGovernor && x.CanUpgradeOfficial)
                      {
                          targetList.Add(x);
                      }
                  });
                }
                return targetList.Count > 0;
            }
        }

        public override void OnEnter()
        {
            SortList();
            base.OnEnter();
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            if (targetList.Count > 0)
            {
                personList.Add(targetList[0]);
            }
        }

        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            base.OnDestroy();
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            string []says = new string[]{

                "主公知遇之恩，某虽肝脑涂地，不能报也！",
                "某愿为主公前驱，赴汤蹈火，在所不辞！",
                "主公在上，末将愿以此身，效犬马之劳，万死不辞！",
                "某誓死追随主公，若有二心，天诛地灭！"
                };

            upgradeList.ForEach(x =>
            {
                x.person.UpgradeOfficial(x.official);
                // 加上感谢话语
                x.person.loyalty += 10;
                Render.RenderEvent.Instance.Add(new Render.CityPersonSayEvent()
                {
                    person = x.person,
                    words = says[GameRandom.Range(says.Length)]
                });
            });
            upgradeList.Clear();

            //TargetCity.JobRewardPersons(personList.ToArray());
            Done();
            GameMedia.Instance.PlayDoAcitonSfx();
        }

        public void AutoUpgrade()
        {
            List<Person> operator_list = new List<Person>();
            List<Official> match_officials = new List<Official>();
            targetList.ForEach(person =>
            {
                if (person.CanUpgradeOfficial)
                {
                    Official[] officials = person.Official.NextOfficials;
                    match_officials.Clear();
                    foreach (Official official in officials)
                    {
                        if (official.CheckPerson(person))
                        {
                            match_officials.Add(official);
                        }
                    }

                    // AI随机给一个官职
                    if (match_officials.Count > 0)
                    {
                        Official dst = match_officials[GameRandom.Range(match_officials.Count)];
                        upgradeList.Add(new UpgradeOfficial()
                        {
                            person = person,
                            official = dst
                        });
                        operator_list.Add(person);
                    }
                }
            });
            
            foreach(Person person in operator_list)
                targetList.Remove(person);
        }

        public void SortList()
        {
            targetList.Sort((a, b) => -PersonSortFunction.SortByOfficial.personSortFunc.Invoke(a, b));
        }

        public void AutoClearUpgrade()
        {
            upgradeList.ForEach(x =>
            {
                targetList.Add(x.person);
            });
            upgradeList.Clear();
            SortList();
        }

        public void RemoveUpgrade(int index)
        {
            CityUpgradeOfficial.UpgradeOfficial data = upgradeList[index];
            targetList.Add(data.person);
            upgradeList.RemoveAt(index);
            SortList();
        }

        public void AddSetPerson(Person person, Official official)
        {
            targetList.Remove(person);
            upgradeList.Add(new UpgradeOfficial()
            {
                person = person,
                official = official
            });
        }
    }
}