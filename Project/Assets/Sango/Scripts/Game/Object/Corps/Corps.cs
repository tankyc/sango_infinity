using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Corps : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Corps; } }
        public virtual bool IsPlayer { get; set; }
        public virtual bool AIFinished { get; set; }
        public virtual bool AIPrepared { get; set; }
        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Force>))]
        [JsonProperty]
        public Force BelongForce;

        /// <summary>
        /// 军团长
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Comander;

        /// <summary>
        /// 军团任务
        /// </summary>
        [JsonProperty] public int CropsMissionType { get; set; }
        /// <summary>
        /// 军团任务目标
        /// </summary>
        [JsonProperty] public int CropsMissionTarget { get; set; }
        public City TargetCity { get; set; }
        public Force TargetForce { get; set; }

        public int BorderCityCount { get; set; }

        /// <summary>
        /// 所有城市
        /// </summary>
        public SangoObjectList<City> allCities = new SangoObjectList<City>();
        /// <summary>
        /// 所有武将
        /// </summary>
        public SangoObjectList<Person> allPersons = new SangoObjectList<Person>();
        /// <summary>
        /// 所有设施
        /// </summary>
        public SangoObjectList<Building> allBuildings = new SangoObjectList<Building>();
        /// <summary>
        /// 所有部队
        /// </summary>
        public SangoObjectList<Troop> allTroops = new SangoObjectList<Troop>();

        Queue<System.Func<Corps, Scenario, bool>> AICommandQueue = new Queue<Func<Corps, Scenario, bool>>();


        public override void OnScenarioPrepare(Scenario scenario)
        {
            BelongForce.allCorps.Add(this);
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            AIFinished = false;
            AIPrepared = false;
            allTroops.RemoveAll(x => !x.IsAlive);
            PrepareCityPersonHole();
            ActionOver = false;
            return true;
        }

        /// <summary>
        /// 准备人才缺口
        /// </summary>
        public void PrepareCityPersonHole()
        {
            int cityCount = allCities.Count;
            if (cityCount <= 1) return;
            int personCount = allPersons.Count;
            BorderCityCount = 0;
            allCities.ForEach((c) =>
            {
                if (c.IsBorderCity)
                    BorderCityCount++;
                c.PersonHole = 0;
            });

            if (BorderCityCount == 0)
                return;

            int noDoderSeat = 3;
            int avarageTotalSeat = personCount - cityCount * noDoderSeat;
            if (avarageTotalSeat <= 0)
            {
                noDoderSeat = 1;
                avarageTotalSeat = personCount - cityCount * noDoderSeat;
                if (avarageTotalSeat <= 0)
                {
                    avarageTotalSeat = personCount;
                }
            }
            int boderSeat = avarageTotalSeat / BorderCityCount + noDoderSeat;
            allCities.ForEach((c) =>
            {
                if (c.IsBorderCity)
                {
                    c.PersonHole = boderSeat - c.allPersons.Count + c.trsformingPesonList.Count;
                }
                else
                {
                    c.PersonHole = noDoderSeat - c.allPersons.Count + c.trsformingPesonList.Count;
                }
            });

        }

        //public City Add(City city)
        //{
        //    allCities.Add(BelongForce.Add(city));
        //    return city;
        //}
        //public Person Add(Person person)
        //{
        //    allPersons.Add(BelongForce.Add(person));
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(BelongForce.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(BelongForce.Add(building));
        //    return building;
        //}
        //public City Remove(City city)
        //{
        //    allCities.Remove(BelongForce.Remove(city));
        //    return city;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(BelongForce.Remove(person));
        //    return person;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(BelongForce.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(BelongForce.Remove(building));
        //    return building;
        //}

        public override bool Run(Scenario scenario)
        {
            if (ActionOver)
                return true;

            if (!DoAI(scenario))
                return false;

            if (IsPlayer)
            {
                scenario.Event.OnPlayerControl(this, scenario);
                return false;
            }

            ActionOver = true;
            return true;
        }

        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                AIPrepared = true;
            }

            while (AICommandQueue.Count > 0)
            {
                System.Func<Corps, Scenario, bool> CurrentCommand = AICommandQueue.Peek();
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                AICommandQueue.Dequeue();
            }

            AIFinished = true;
            return true;
        }

        /// <summary>
        /// AI准备
        /// </summary>
        private void AIPrepare(Scenario scenario)
        {
            AICommandQueue.Enqueue(CorpsAI.AITransfromPerson);
            AICommandQueue.Enqueue(CorpsAI.AICities);
            AICommandQueue.Enqueue(CorpsAI.AITroops);

            //AISections();
            //AICapital();
            //AIMakeMarriage();
            //AISelectPrince();
            //AIZhaoXian();
            //AIAppointMayor();
            //AIHouGong();

            //AILegions();
            //AITrainChildren();
        }
    }
}
