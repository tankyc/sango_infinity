using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Force : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Force; } }
        public virtual bool AIFinished { get; set; }
        public virtual bool AIPrepared { get; set; }
        public virtual bool IsPlayer { get; set; }

        public override string Name { get { return Governor?.Name; } }

        /// <summary>
        /// 主公
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Governor;

        /// <summary>
        /// 军师
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Counsellor;

        /// <summary>
        /// 旗帜
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Flag>))]
        [JsonProperty]
        public Flag Flag { get; set; }

        /// <summary>
        /// 联盟信息
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Alliance>))]
        [JsonProperty]
        public SangoObjectList<Alliance> AllianceList = new SangoObjectList<Alliance>();

        /// <summary>
        /// 技巧点数
        /// </summary>
        [JsonProperty] public int TechniquePoint { get; set; }

        /// <summary>
        /// 势力方针
        /// </summary>
        [JsonProperty] public int PolicyType { get; set; }

        /// <summary>
        /// 国库
        /// </summary>
        public ItemStore Stroe = new ItemStore();

        /// <summary>
        /// 当前研究的技术
        /// </summary>
        public int ResearchTechnique { get; set; }

        /// <summary>
        /// 所有军团
        /// </summary>
        public SangoObjectList<Corps> allCorps = new SangoObjectList<Corps>();

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

        /// <summary>
        /// AI指令集
        /// </summary>
        public List<System.Func<Force, Scenario, bool>> AICommandList = new List<System.Func<Force, Scenario, bool>>();

        public override void OnScenarioPrepare(Scenario scenario)
        {
            //AllianceList?.InitCache();// = new SangoObjectList<Alliance>().FromString(_allianceListStr, scenario.allianceSet);
        }

        public bool IsAlliance(Force other)
        {
            return false;
        }
        //public Corps Add(Corps corps)
        //{
        //    allCorps.Add(Scenario.Cur.Add(corps));
        //    return corps;
        //}
        //public City Add(City city)
        //{
        //    allCities.Add(Scenario.Cur.Add(city));
        //    return city;
        //}
        //public Person Add(Person person)
        //{
        //    allPersons.Add(Scenario.Cur.Add(person));
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(Scenario.Cur.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(Scenario.Cur.Add(building));
        //    return building;
        //}
        //public Corps Remove(Corps corps)
        //{
        //    allCorps.Remove(Scenario.Cur.Remove(corps));
        //    return corps;
        //}
        //public City Remove(City city)
        //{
        //    allCities.Remove(Scenario.Cur.Remove(city));
        //    return city;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(Scenario.Cur.Remove(person));
        //    return person;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(Scenario.Cur.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(Scenario.Cur.Remove(building));
        //    return building;
        //}

        public override bool Run(Scenario scenario)
        {
            if (ActionOver)
                return true;

            if (!DoAI(scenario))
                return false;

            for (int i = 0; i < allCorps.Count; ++i)
            {
                Corps corps = allCorps[i];
                if (corps.IsAlive && !corps.ActionOver)
                {
                    if (!corps.Run(scenario))
                        return false;
                }
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
                scenario.Event.OnForceAIStart?.Invoke(this, scenario);
                AIPrepared = true;
            }

            while (AICommandList.Count > 0)
            {
                System.Func<Force, Scenario, bool> CurrentCommand = AICommandList[0];
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                AICommandList.RemoveAt(0);
            }

            scenario.Event.OnForceAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            return true;
        }

        /// <summary>
        /// AI准备
        /// </summary>
        private void AIPrepare(Scenario scenario)
        {
            // 计算相邻临势力
            List<Force> NeighborForceList = new List<Force>();
            foreach (City city in this.allCities)
            {
                foreach (City neighbor in city.NeighborList)
                {
                    if (!neighbor.IsSameForce(city) && neighbor.BelongForce != null)
                    {
                        if (!NeighborForceList.Contains(neighbor.BelongForce))
                        {
                            NeighborForceList.Add(neighbor.BelongForce);
                        }
                    }
                }
            }

            AICommandList.Add(ForceAI.AIDiplomacy);
            AICommandList.Add(ForceAI.AICaptives);
            AICommandList.Add(ForceAI.AITechniques);

            scenario.Event.OnForceAIPrepare?.Invoke(this, scenario);
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            AIFinished = false;
            AIPrepared = false;
            allTroops.RemoveAll(x => !x.IsAlive);

            Sango.Log.Print($"{Name} 回合");
            allCorps.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnStart(scenario);
            });
            allBuildings.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnStart(scenario);
            });
            allCities.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnStart(scenario);
            });
            allTroops.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnStart(scenario);
            });



            return base.OnTurnStart(scenario);
        }

        public override bool OnTurnEnd(Scenario scenario)
        {
            allCorps.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnEnd(scenario);
            });
            allCities.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnEnd(scenario);
            });
            allTroops.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnEnd(scenario);
            });
            allBuildings.ForEach(c =>
            {
                if (c.IsAlive)
                    c.OnTurnEnd(scenario);
            });
            return base.OnTurnEnd(scenario);
        }
    }
}
