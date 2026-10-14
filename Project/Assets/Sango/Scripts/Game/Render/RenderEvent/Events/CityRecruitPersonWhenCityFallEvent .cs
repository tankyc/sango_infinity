using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    public class CityRecruitPersonWhenCityFallEvent : RenderEventBase
    {
        public List<Person> captiveList;
        public City targetCity;
        public City escapeCity;
        public Troop atk;
        public int recruitType;

        public void Init(List<Person> captiveList, City targetCity, City escapeCity, Troop atk, int recruitType)
        {
            this.captiveList = captiveList;
            this.targetCity = targetCity;
            this.escapeCity = escapeCity;
            this.atk = atk;
            this.recruitType = recruitType;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            if (captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }

            // 判据用 IsPlayerControl（玩家势力 && 第一军团）而不是 IsPlayer：
            // 玩家势力的委任军团 IsPlayer 也为真，但它由 AI 代管，不能走玩家交互分支，
            // 否则会在玩家当前界面之上叠出招募窗口，并让回合卡在等待玩家操作上。
            if (!atk.BelongCorps.IsPlayerControl)
            {
                for (int i = captiveList.Count - 1; i >= 0; i--)
                {
                    Person person = captiveList[i];
                    if (atk.Leader.JobRecruitPerson(person, targetCity, recruitType))
                    {
                        Sango.Log.Info($"{person.Name} 加入了 {atk.BelongForce} 势力!!!");
                    }
                    else
                    {
                        targetCity.AddCaptive(person);
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
            //展示武将
            GameSystem.GetSystem<PersonRecruit>().Start(targetCity, atk, captiveList[0], captiveList, recruitType, 3, x =>
            {
                IsDone = true;
            });
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
            return IsDone;
        }
    }
}
