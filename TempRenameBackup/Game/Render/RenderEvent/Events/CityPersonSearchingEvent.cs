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
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, "很遗憾, 什么都没有发现...", () =>
                    {
                        IsDone = true;
                    }, person);
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
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, content, () =>
                {
                    //展示武将
                    GameSystem.GetSystem<PersonRecruit>().Start(person, target, 0, 1, x =>
                    {
                        if (x.result == 1)
                        {
                            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"成功招募了{target.ColorName}", () =>
                            {
                                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                                {
                                    IsDone = true;
                                }, target);
                            }, person);
                        }
                        else if (x.result == 0)
                        {
                            if (searchingType == 0)
                            {
                                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到{target.ColorName}", () =>
                                {
                                    IsDone = true;
                                }, person);
                            }
                            else
                            {
                                IsDone = true;
                            }
                        }
                        else
                            IsDone = true;
                    });
                }, person);
            }
            else
            {
                if (searchingType == 0)
                {
                    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"发现了资金{rs}", () =>
                    {
                        IsDone = true;
                    }, person);
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
