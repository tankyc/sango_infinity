/*
 * 文件名：Corps.cs
 * 描述：军团类，管理游戏中的军团对象
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using TKNewtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 军团类，管理游戏中的军团对象
    /// 军团是势力下的组织单位，包含多个城市和武将
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Corps : SangoObject
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Corps; } }

        /// <summary>
        /// 是否为玩家军团
        /// </summary>
        public virtual bool IsPlayer => BelongForce?.IsPlayer ?? false;

        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => IsPlayer && number == 1;

        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => BelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// AI是否完成行动
        /// </summary>
        public virtual bool AIFinished { get; set; }

        /// <summary>
        /// AI是否准备完成
        /// </summary>
        public virtual bool AIPrepared { get; set; }

        /// <summary>
        /// 军团编号文本
        /// </summary>
        public readonly string[] numberTxt = { "零", "一", "二", "三", "四", "五", "六", "七", "八" };

        /// <summary>
        /// 军团名称
        /// </summary>
        public override string Name
        {
            get { return $"第{numberTxt[number]}军团"; }
        }

        /// <summary>
        /// 带颜色的军团名称
        /// </summary>
        public string ColorName { get { return $"<color=#7CCADB>第{numberTxt[number]}军团</color>"; } }

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
        /// 军团番号
        /// </summary>
        [JsonProperty]
        public int number;

        /// <summary>
        /// 中期目标类型
        /// </summary>
        [JsonProperty] public int mid_objective;

        /// <summary>
        /// 中期目标
        /// </summary>
        [JsonProperty] public int mid_objective_target;

        /// <summary>
        /// 政策类型
        /// </summary>
        [JsonProperty] public int policy;

        /// <summary>
        /// 政策目标
        /// </summary>
        [JsonProperty] public int policy_target;

        /// <summary>
        /// 委任
        /// </summary>
        [JsonProperty] public int appoint;

        /// <summary>
        /// 委任目标,攻略势力为势力ID, 攻占城池为城池ID
        /// </summary>
        [JsonProperty] public int appoint_target;

        public enum AppointType : int
        {
            None = 0,
            DestroyForce,
            OccupyCity,
            Auto,
        }

        public enum AppointContentType : int
        {
            MakeItem_Spear = 0,
            MakeItem_Halberd,
            MakeItem_Crossbow,
            MakeItem_Horse,
            MakeItem_Machine,
            MakeItem_Boat,
            Donot_Store_Gold,
            Store_Foood,
            Store_Troops,
            Person,
            Attack,
            Transport,
            Build,
            Max = 13
        }

        /// <summary>
        /// 政策内容
        /// </summary>
        [JsonProperty] public int[] appointSetting;

        /// <summary>
        /// 行动力点数
        /// </summary>
        [JsonProperty] public int ActionPoint { get; set; }

        /// <summary>
        /// 工作计数
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> jobCounter = new Dictionary<int, int>();


        /// <summary>
        /// 获取工作计数
        /// </summary>
        /// <param name="jobId">工作ID</param>
        /// <returns>工作计数</returns>
        public int GetJobCounter(int jobId)
        {
            if (jobCounter.TryGetValue(jobId, out var job))
            {
                return job;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 增加工作计数
        /// </summary>
        /// <param name="jobId">工作ID</param>
        /// <returns>增加后的工作计数</returns>
        public int AddJobCounter(int jobId)
        {
            if (jobCounter.TryGetValue(jobId, out var job))
            {
                job++;
                jobCounter[jobId] = job;
                return job;
            }
            else
            {
                jobCounter.Add(jobId, 1);
                return 1;
            }
        }

        /// <summary>
        /// 创建时候的缓存城市列表
        /// </summary>
        public List<City> inti_cities;

        static Color[] colors = new Color[]
        {
            Color.cyan,
            Color.red * 0.8f,
            Color.yellow * 0.8f,
            Color.green * 0.8f,
            Color.blue * 0.8f,
            Color.gray,
            Color.magenta * 0.8f,
        };

        public Color Color => colors[number - 1];

        /// <summary>
        /// 番号
        /// </summary>
        public int Index => number;

        public City TargetCity { get; set; }
        public Force TargetForce { get; set; }


        public Queue<System.Func<Corps, Scenario, bool>> AICommandQueue = new Queue<Func<Corps, Scenario, bool>>();


        public Corps()
        {
            appointSetting = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        }

        public override void Init(Scenario scenario)
        {
            if (appointSetting == null || appointSetting.Length < (int)AppointContentType.Max)
                appointSetting = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            PrepareCityInfo();
        }

        public int GetAppointValue(AppointContentType policyContentType)
        {
            if (appointSetting == null) return 0;
            return appointSetting[(int)policyContentType];
        }

        public bool CheckTargetIsAppointTarget(Force force)
        {
            if (appoint == (int)AppointType.DestroyForce)
                return appoint_target == force.Id;
            return true;
        }

        public bool CheckTargetIsAppointTarget(City city)
        {
            if (city.BelongForce == null) return true;
            if (appoint == (int)AppointType.OccupyCity)
                return appoint_target == city.Id;
            else if (appoint == (int)AppointType.DestroyForce)
                return appoint_target == city.BelongForce.Id;
            return true;
        }

        public override bool OnForceTurnStart(Scenario scenario)
        {
            jobCounter.Clear();
            AIFinished = false;
            AIPrepared = false;
            AddActionPoint(scenario);
            ActionOver = false;
            PrepareCityInfo();
            if (needUpdateCommander)
                UpdateCommander();

            return true;
        }

        public int cityCount;
        public int personCount;
        public int gold;
        public int troops;
        public int food;
        bool needUpdateCommander = false;
        public void UpdateWhenCityChange()
        {
            PrepareCityInfo();
            // 不能解散第一军团
            if (cityCount == 0 && number > 1)
            {
                BelongForce.DeleteCorps(this);
            }
        }

        public void PrepareCityInfo()
        {
            cityCount = 0;
            personCount = 0;
            gold = 0;
            troops = 0;
            food = 0;
            ForEachCity(x =>
            {
                cityCount++;
                personCount += x.allPersons.Count;
                gold += x.gold;
                troops += x.troops;
                food += x.food;
            });
        }

        public void AddActionPoint(Scenario scenario)
        {
            /*
                每回合新增行动力=（君主参数+城市参数+武将参数）* 军师参数

                君主参数=40*【0.65+0.025*（能力参数-6）】
                上式中的“能力参数”为君主“统率”和“魅力”两个属性中数值较高者，然后再除以5所得的数值。这个数值取整，非四舍五入。
                另外，（能力参数-6）最小取0，不能取负数。
                比如某君主统率78，魅力99，则这个值首选选取统率和魅力两项中较高的魅力值99，然后除以5，得19.8。然后取整，得19。
                
                城市参数=10*（拥有城数的数量-1）
                也就是说，每多拥有一个城池，这个参数就多10点。
                不过需要注意的是，只有一个城池的时候，这个数值是0。而拥有6个城池的时候，达到最大值的50上限。
             
                武将参数=拥有最多所属武将数的前6个城池、港口、关卡的武将数之和
                不过，每个据点最多只计算10个，超出不计。
                比如，你只有一个城，城里有15个武将，那也只按10个计算。
                还有一点需要注意的是，必须占领所在地的主城才可以计算该参数。仅仅占领了该城附属的港口和关卡是没用的。
                比如，你没有打下来洛阳，但是打下了虎牢关，那虎牢关中的武将是不计人数的。
                另外就是武将只计算人头，和他本身的身份、属性等等都没有关系。也就是说，刘备和刘禅，在凑人头方面，没有区别。
                武将参数上限为60，即60个武将理论上有可能达到最大行动力。

                军师参数
                根据计算公式也可以看出来，军师参数是一项非常重要的参数，因为前面全是相加，到这里变成了相乘。
                军师参数=1.2-0.01*（50-智力参数）
                其中，智力参数=军师智力/2，取整，同样的，不四舍五入。
                如果没有军师的时候，军师参数取1。
                可以看到，上式中，军师的智力越高，军师参数的数值就越大。当军师智力达到100时，军师参数有最大值1.2。
                而军师的智力≤60的时候，军师参数反而小于了没有军师的1，这时候，不要军师就对了，反正一个60智力的军师，说话能靠谱才怪了。
             */

            int governorAdd = (int)(40 * (0.65f + 0.025f * System.Math.Max(0, (int)(System.Math.Max((float)BelongForce.Governor.Command, (float)BelongForce.Governor.Glamour) / 5.0f) - 6)));
            int personAdd = 0;
            List<City> cities = new List<City>();
            int cityCount = 0;
            BelongForce.ForEachCityBase((c) =>
            {
                if (c.BelongCity != null && c.BelongCity.BelongForce != BelongForce)
                    return;

                if (c.IsCity()) cityCount++;

                if (c.BelongCorps == this)
                    cities.Add(c);
            });
            int cityAdd = System.Math.Min(50, 10 * (cityCount - 1));

            cities.Sort((a, b) => -a.allPersons.Count.CompareTo(b.allPersons.Count));
            for (int i = 0; i < 6; i++)
            {
                if (i < cities.Count)
                    personAdd = personAdd + System.Math.Min(10, cities[i].allPersons.Count);
            }

            float counsellorFactor = 1.0f;
            if (BelongForce.Counsellor != null)
                counsellorFactor = 1.2f - 0.01f * (50 - BelongForce.Counsellor.Intelligence / 2);

            //TODO: 建筑影响， 特技影响

            ActionPoint = System.Math.Min(Scenario.Cur.Variables.ActionPointLimit, ActionPoint + (int)((governorAdd + personAdd + cityAdd) * counsellorFactor * scenario.Variables.ActionPointFactor));
            ActionPoint = System.Math.Max(0, ActionPoint);


            if (IsPlayer && BelongForce == Scenario.Cur.CurRunForce)
            {
                GameEvent.OnCorpsActionPointChange?.Invoke(this);
            }

        }

        public void ReduceActionPoint(int v)
        {
            ActionPoint -= v;
            if (IsPlayer && BelongForce == Scenario.Cur.CurRunForce)
            {
                GameEvent.OnCorpsActionPointChange?.Invoke(this);
            }
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

            // 主军团永远不是委任军团,除此之外全是委任军团
            if (IsPlayer && Comander == BelongForce.Governor)
            {
                GameEvent.OnPlayerControl?.Invoke(this, scenario);
                return false;
            }

            if (!DoAI(scenario))
                return false;

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

        public void ForEachCity(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && c.IsCity())
                {
                    action(c);
                }
            }
        }

        public void ForEachPort(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && c.IsPort())
                {
                    action(c);
                }
            }
        }

        public void ForEachGate(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && c.IsGate())
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
                if (c != null && c.IsAlive && c.BelongCorps == this)
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
                if (c != null && c.IsAlive && c.BelongCorps == this)
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
                if (c != null && c.IsAlive && c.BelongCorps == this)
                {
                    action(c);
                }
            }
        }

        public void RemoveCity(City target)
        {
            if (cityCount == 1)
            {
                BelongForce.DeleteCorps(this);
            }
            else
            {
                if (Comander.BelongCity == target)
                {
                    Comander?.SetStateNormal();
                    Comander = null;
                    NeedUpdateCommander();
                }
            }
        }

        public void NeedUpdateCommander()
        {
            needUpdateCommander = true;
        }

        public void UpdateCommander()
        {
            needUpdateCommander = false;
            Person dest = null;
            Official higher = null;
            int commandHigher = 0;
            Comander?.SetStateNormal();
            ForEachPerson((checker) =>
            {
                if (checker != null && checker.IsAlive)
                {
                    if (dest == null)
                    {
                        dest = checker;
                        higher = dest.Official;
                        commandHigher = dest.Command;
                    }
                    else
                    {
                        if (checker.Official.level > higher.level)
                        {
                            dest = checker;
                            higher = dest.Official;
                            commandHigher = dest.Command;
                        }
                        else if (checker.Official.level == higher.level)
                        {
                            if (checker.Command > commandHigher)
                            {
                                dest = checker;
                                higher = dest.Official;
                                commandHigher = dest.Command;
                            }
                        }
                    }
                }
            });

            Comander = dest;
            if (Comander != null)
            {
                Comander.SetStateCommander();
                Comander.BelongCity.UpdateNewLeader();
            }
        }
    }
}
