using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    public class CityRecruitPersonWhenTroopFallEvent : RenderEventBase
    {
        public List<Person> captiveList;
        public Troop atk;

        public void Init(List<Person> captiveList, Troop atk)
        {
            this.captiveList = captiveList;
            this.atk = atk;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            if (captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }

            // 判据必须用 IsPlayerControl（玩家势力 && 第一军团），不能用 IsPlayer：
            // 委任军团（玩家势力里非第一军团）的 IsPlayer 同样为真，但它由 AI 代管，
            // 一旦误走玩家分支就会在玩家当前界面（例如"创建队伍"）之上叠出招募窗口，
            // 而本事件只有等玩家操作完成后才会置 IsDone，于是整条回合推进
            // （Scenario.Run → RenderEvent.Update）被永久卡住。委任军团必须和 AI 势力一样无界面结算。
            if (!atk.BelongCorps.IsPlayerControl)
            {
                for (int i = captiveList.Count - 1; i >= 0; i--)
                {
                    Person person = captiveList[i];
                    if (atk.Leader.JobRecruitPerson(person, atk.BelongCity, 1))
                    {
                        if (person.IsPlayer)
                        {
                            PersonJoinToOtherForceEvent personBeCaptiveEvent = new PersonJoinToOtherForceEvent()
                            {
                                person = person,
                                troop = atk
                            };
                            RenderEvent.Instance.Add(personBeCaptiveEvent);
                        }
                        Sango.Log.Info($"{person.Name} 加入了 {atk.BelongForce} 势力!!!");
                    }
                    else
                    {
                        atk.AddCaptive(person);
                        if (person.IsPlayer)
                        {
                            PersonBeCaptiveEvent personBeCaptiveEvent = new PersonBeCaptiveEvent()
                            {
                                person = person,
                                troop = atk
                            };
                            RenderEvent.Instance.Add(personBeCaptiveEvent);
                        }
                    }
                }
                captiveList.Clear();
                IsDone = true;
                return;
            }

            Next();
        }

        void Next()
        {
            if (captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }
            else
            {
                //展示武将
                GameSystem.GetSystem<PersonRecruit>().Start(atk, captiveList[0], 1, 3, x =>
                {
                    captiveList.RemoveAt(0);
                    Next();
                });
            }
        }

        public override void Exit(Scenario scenario)
        {

        }

        public override bool IsVisible()
        {
            // 与 Enter 的判据保持一致：委任军团不参与玩家侧演出
            return atk.BelongCorps.IsPlayerControl;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!atk.ActionOver)
                return IsDone;

            return IsDone;
        }
    }
}
