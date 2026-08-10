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

            if (!atk.BelongCorps.IsPlayer)
            {
                for (int i = captiveList.Count - 1; i >= 0; i--)
                {
                    Person person = captiveList[i];
                    if (atk.Leader.JobRecruitPerson(person, atk.BelongCity, 1))
                    {
#if SANGO_DEBUG
                        Sango.Log.Info($"{person.Name} 加入了 {atk.BelongForce} 势力!!!");
#endif
                    }
                    else
                    {
                        atk.AddCaptive(person);
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
            return atk.BelongCorps.IsPlayer;
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!atk.ActionOver)
                return IsDone;

            return IsDone;
        }
    }
}
