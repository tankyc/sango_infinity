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

            if (!atk.BelongCorps.IsPlayer)
            {
                for (int i = captiveList.Count - 1; i >= 0; i--)
                {
                    Person person = captiveList[i];
                    if (atk.Leader.JobRecruitPerson(person, targetCity, recruitType))
                    {
#if SANGO_DEBUG
                        Sango.Log.Info($"{person.Name} 加入了 {atk.BelongForce} 势力!!!");
#endif
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
            if (captiveList.Count == 0)
            {
                IsDone = true;
                return;
            }
            else
            {
                //展示武将
                GameSystem.GetSystem<PersonRecruit>().Start(targetCity, atk, captiveList[0], recruitType, 3, x =>
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
            return IsDone;
        }
    }
}
