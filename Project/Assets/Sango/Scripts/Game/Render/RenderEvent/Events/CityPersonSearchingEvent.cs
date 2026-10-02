using Sango.Core;

namespace Sango.Render
{
    public class CityPersonSearchingEvent : RenderEventBase
    {
        public City city;
        public Person person;
        public Person target;
        public int searchingType = 0; // 1是工作制

        /// <summary>
        /// 本次探索的结果码（与 <see cref="City.DoJobSearching"/> 返回值一致）：
        /// 0 = 发现人才（<see cref="target"/> 有效）／ &gt;0 = 发现资金数额 ／ -1 = 一无所获。
        ///
        /// 记录下来是为了在 <see cref="Exit"/> 里抛 <see cref="GameEvent.OnCityJobSearchingSettled"/> ——
        /// 演出结束时 Enter 的局部变量已经没了，结果必须随事件实例带着。
        /// 本类是**对象池复用**的，因此 Init 里必须复位，否则会带上一次的旧值。
        /// </summary>
        public int searchResult = -1;

        public void Init(City city, Person person)
        {
            this.city = city;
            this.person = person;
            this.target = null;
            searchResult = -1;
            searchingType = 0;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            int rs = city.DoJobSearching(person, out target);
            searchResult = rs;
            if (rs < 0)
            {
                if (city.BelongCorps.IsPlayerControl && searchingType == 0)
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

            if (!city.BelongCorps.IsPlayerControl)
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
                        //if (x.result == 1)
                        //{
                        //    GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"成功招募了{target.ColorName}", () =>
                        //    {
                        //        GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"{target.ColorName}愿为主公献犬马之劳", () =>
                        //        {
                        //            IsDone = true;
                        //        }, target);
                        //    }, person);
                        //}
                        //else if (x.result == 0)
                        //{
                        //    if (searchingType == 0)
                        //    {
                        //        GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"很遗憾，\n未能招募到{target.ColorName}", () =>
                        //        {
                        //            IsDone = true;
                        //        }, person);
                        //    }
                        //    else
                        //    {
                        //        IsDone = true;
                        //    }
                        //}
                        //else
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

        /// <summary>
        /// 演出结束——**此刻**才抛"探索结算完成"。
        ///
        /// 为什么不在 Enter 里抛：Enter 在执行 DoJobSearching 之后紧接着就要开对话框，
        /// 那一刻本事件还没结束（IsDone 仍为 false），若有人在此时启动另一套演出，
        /// 两边会同时往 GameDialog 投递，窗口互相覆盖。
        /// Exit 由 RenderEvent 播放器在本项 Update 返回 true 之后调用，此时所有对话都已关闭。
        /// </summary>
        public override void Exit(Scenario scenario)
        {
            GameEvent.OnCityJobSearchingSettled?.Invoke(person, city, searchResult, target);
        }

        public override bool IsVisible()
        {
            return city.BelongCorps.IsPlayer;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            return IsDone;
        }
    }
}
