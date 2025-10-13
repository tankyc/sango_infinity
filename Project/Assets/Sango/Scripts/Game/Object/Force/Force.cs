using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Force : SangoObject
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
        /// 本国被俘虏
        /// </summary>
        public List<Person> CaptiveList = new List<Person>();

        /// <summary>
        /// 技巧点数
        /// </summary>
        [JsonProperty] public int TechniquePoint { get; private set; }

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

        ///// <summary>
        ///// 所有军团
        ///// </summary>
        //public SangoObjectList<Corps> allCorps = new SangoObjectList<Corps>();

        ///// <summary>
        ///// 所有城市
        ///// </summary>
        //public SangoObjectList<City> allCities = new SangoObjectList<City>();

        ///// <summary>
        ///// 所有武将
        ///// </summary>
        //public SangoObjectList<Person> allPersons = new SangoObjectList<Person>();

        ///// <summary>
        ///// 所有设施
        ///// </summary>
        //public SangoObjectList<Building> allBuildings = new SangoObjectList<Building>();

        ///// <summary>
        ///// 所有部队
        ///// </summary>
        //public SangoObjectList<Troop> allTroops = new SangoObjectList<Troop>();

        /// <summary>
        /// AI指令集
        /// </summary>
        public List<System.Func<Force, Scenario, bool>> AICommandList = new List<System.Func<Force, Scenario, bool>>();

        /// <summary>
        /// 相邻势力
        /// </summary>
        public List<Force> NeighborForceList = new List<Force>();

        /// <summary>
        /// 国力值
        /// </summary>
        public int FightPower;

        /// <summary>
        /// 执行建筑行为的建筑列表(建筑攻击等)
        /// </summary>
        Queue<BuildingBase> buildingBaseList = new Queue<BuildingBase>();

        public int PersonCount { get; set; }
        public int CityCount { get; set; }


        public override void OnScenarioPrepare(Scenario scenario)
        {

        }

        public bool IsAlliance(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.Contains(other))
                    return true;
            }
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

            if (!DoBuildingBehaviour(scenario))
                return false;

            if (!DoAI(scenario))
                return false;

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps != null && corps.IsAlive && corps.BelongForce == this && !corps.ActionOver)
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


        public bool DoBuildingBehaviour(Scenario scenario)
        {
            if (buildingBaseList.Count <= 0)
                return true;

            while (buildingBaseList.Count > 0)
            {
                BuildingBase currentBuilding = buildingBaseList.Peek();
                if (!currentBuilding.DoBuildingBehaviour(scenario))
                    return false;

                buildingBaseList.Dequeue();
            }

            return true;
        }

        /// <summary>
        /// AI准备
        /// </summary>
        private void AIPrepare(Scenario scenario)
        {
            AICommandList.Add(ForceAI.AIDiplomacy);
            AICommandList.Add(ForceAI.AICaptives);
            AICommandList.Add(ForceAI.AITechniques);

            scenario.Event.OnForceAIPrepare?.Invoke(this, scenario);
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            buildingBaseList.Clear();
            AIFinished = false;
            AIPrepared = false;
            FightPower = 0;
            PersonCount = 0;
            CityCount = 0;
#if SANGO_DEBUG
            Sango.Log.Print($"==={Name} 回合===");
#endif

            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    PersonCount++;
                    c.OnTurnStart(scenario);
                }
            }

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnStart(scenario);
                }
            }

            bool hasNoCheckBorder = false;
            NeighborForceList.Clear();
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    CityCount++;
                    c.OnTurnStart(scenario);
                    FightPower += c.FightPower;
                    buildingBaseList.Enqueue(c);
                    c.borderLine = -1;
                    // 计算相邻势力
                    foreach (City neighbor in c.NeighborList)
                    {
                        if (!neighbor.IsSameForce(c))
                        {
                            c.borderLine = 0;
                            if (neighbor.BelongForce != null)
                            {
                                if (!NeighborForceList.Contains(neighbor.BelongForce))
                                {
                                    NeighborForceList.Add(neighbor.BelongForce);
                                }
                            }
                        }
                    }
                    hasNoCheckBorder = c.borderLine == -1;
                }
            }

            while (hasNoCheckBorder)
            {
                for (int i = 0; i < scenario.citySet.Count; ++i)
                {
                    var c = scenario.citySet[i];
                    if (c != null && c.IsAlive && c.BelongForce == this && c.borderLine < 0)
                    {
                        int minBorder = 99;
                        // 计算相邻势力
                        foreach (City neighbor in c.NeighborList)
                        {
                            if (neighbor.borderLine >= 0)
                                minBorder = Mathf.Min(minBorder, neighbor.borderLine);
                        }
                        if (minBorder >= 0)
                        {
                            c.borderLine = minBorder + 1;
                        }
                        hasNoCheckBorder = c.borderLine == -1;
                    }
                }
            }

            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnStart(scenario);
                    buildingBaseList.Enqueue(c);
                }
            }

            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnStart(scenario);
                }
            }

            return base.OnTurnStart(scenario);
        }

        public override bool OnTurnEnd(Scenario scenario)
        {
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnTurnEnd(scenario);
                }
            }
            return base.OnTurnEnd(scenario);
        }

        public override bool OnMonthStart(Scenario scenario)
        {
            return base.OnMonthStart(scenario);
        }

        public void ForEachCity(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        public void ForEachPerson(System.Action<Person> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        public void ForEachCorps(System.Action<Corps> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        public void ForEachBuilding(System.Action<Building> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        public void ForEachTroop(System.Action<Troop> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        public void GainTechniquePoint(int value)
        {
            TechniquePoint += value;
        }
    }
}
