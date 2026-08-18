using Sango.Core;

namespace Sango.Render
{
    public class CityPersonSearchingEvent : RenderEventBase
    {
        public City city;
        public Person person;
        public Person target;
        public int searchingType = 0; // 1是工作制

        public void Init(City city, Person person)
        {
            this.city = city;
            this.person = person;
            this.target = null;
            searchingType = 0;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            int rs = city.DoJobSearching(person, out target);
            if (rs < 0)
            {
                if (city.mBelongCorps.IsPlayerControl && searchingType == 0)
                {
                    GameDialog.IDialog dialog3 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, "很遗憾, 什么都没有发现...", () =>
                    {
                        GameDialog.Close();
                        IsDone = true;
                    });
                    dialog3.SetPerson(person);
                }
                else
                {
                    IsDone = true;
                }
                return;
            }

            if (!city.mBelongCorps.IsPlayerControl)
            {
                if (rs == 0)
                {
                    person.JobRecruitPerson(target, (int)PersonRecruitType.OnSearching);
                }
                IsDone = true;
                return;
            }

            if (rs == 0)
            {

                string content = $"搜索结果，\n发现了名为{target.ColorName}的武将。";
                GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, content, () =>
                {
                    GameDialog.Close();

                    //展示武将
                    GameSystem.GetSystem<PersonRecruit>().Start(person, target, 0, 1, x =>
                    {
                        if (x.result == 1)
                        {
                            GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"成功招募了{target.ColorName}", () =>
                            {
                                GameDialog.Close();
                                GameDialog.IDialog dialog2 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                                {
                                    GameDialog.Close();
                                    IsDone = true;
                                });
                                dialog2.SetPerson(target);
                            });
                            dialog1.SetPerson(person);
                        }
                        else if (x.result == 0)
                        {
                            if (searchingType == 0)
                            {
                                GameDialog.IDialog dialog1 = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到{target.ColorName}", () =>
                                {
                                    GameDialog.Close();
                                    IsDone = true;
                                });
                                dialog1.SetPerson(person);
                            }
                            else
                            {
                                IsDone = true;
                            }
                        }
                        else
                            IsDone = true;
                    });
                });
                dialog.SetPerson(person);
            }
            else
            {
                if (searchingType == 0)
                {
                    GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"发现了资金{rs}", () =>
                    {
                        GameDialog.Close();
                        IsDone = true;
                    });
                    dialog.SetPerson(person);
                }
                else
                {
                    IsDone = true;
                }
            }
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            return city.mBelongCorps.IsPlayer;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
