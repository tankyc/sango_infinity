using Newtonsoft.Json;
using Sango.Game.Render;
using System;
using System.Collections.Generic;
using System.Text;
using TreeEditor;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class City : BuildingBase
    {
        public virtual bool AIFinished { get; set; }
        public virtual bool AIPrepared { get; set; }
        public override SangoObjectType ObjectType { get { return SangoObjectType.City; } }

        /// <summary>
        /// 粮食
        /// </summary>
        [JsonProperty] public int food;

        /// <summary>
        /// 金钱
        /// </summary>
        [JsonProperty] public int gold;

        /// <summary>
        /// 人口
        /// </summary>
        [JsonProperty] public int population;

        /// <summary>
        /// 兵役人口
        /// </summary>
        [JsonProperty] public int troopPopulation;

        /// <summary>
        /// 库存
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(ItemStoreConverter))]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 商业值
        /// </summary>
        [JsonProperty] public int commerce;

        /// <summary>
        /// 农业值
        /// </summary>
        [JsonProperty] public int agriculture;

        /// <summary>
        /// 民心
        /// </summary>
        [JsonProperty] public byte popularSupport;

        /// <summary>
        /// 治安
        /// </summary>
        [JsonProperty] public int security;

        /// <summary>
        /// 战意
        /// </summary>
        [JsonProperty] public int energy;

        /// <summary>
        /// 士气
        /// </summary>
        [JsonProperty] public int morale;

        /// <summary>
        /// 金收入
        /// </summary>
        [JsonProperty] public ushort goldIncome;

        /// <summary>
        /// 粮食收入
        /// </summary>
        [JsonProperty] public ushort foodIncome;

        /// <summary>
        /// 是否有商人
        /// </summary>
        [JsonProperty] public bool hasBusiness;

        /// <summary>
        /// 模型
        /// </summary>
        [JsonProperty] public int model;

        /// <summary>
        /// 当前兵力
        /// </summary>
        [JsonProperty] public int troops;

        /// <summary>
        /// 当前伤兵
        /// </summary>
        [JsonProperty] public int woundedTroops;

        /// <summary>
        /// 是否,满兵
        /// </summary>
        public bool TroopsIsFull => troops + woundedTroops >= CityLevelType.maxTroops;

        /// <summary>
        /// 太守
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Leader;

        /// <summary>
        /// 所属州
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<State>))]
        [JsonProperty]
        public State State;

        /// <summary>
        /// 相邻城市
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<City>))]
        [JsonProperty]
        public SangoObjectList<City> NeighborList = new SangoObjectList<City>();

        /// <summary>
        /// 城市等级数据
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<CityLevelType>))]
        [JsonProperty]
        public CityLevelType CityLevelType;

        /// <summary>
        /// 俘虏
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> CaptiveList = new SangoObjectList<Person>();


        public List<Building> villageList = new List<Building>();

        List<Cell> cell_list = new List<Cell>();

        public int totalGainFood = 0;
        public int totalGainGold = 0;

        /// <summary>
        /// 额外的影响倍率(事件等)
        /// </summary>
        public float extraGainFoodFactor = 1;
        public float extraGainGoldFactor = 1;
        public float extraPopulationFactor = 1;


        float population_increase_factor = 0;
        public override Cell CenterCell => cell_list[0];
        internal int borderLine;
        public bool IsBorderCity => borderLine == 0;
        public int BorderLine => borderLine;

        public List<Person> freePersons = new List<Person>();
        public List<Person> wildPersons = new List<Person>();

        public int FreePersonCount => freePersons.Count;
        public int PersonHole { get; set; }

        //public int eventId;
        //public int specialtyId;
        //public int model_wall;
        //public int model_city;
        internal int virtualFightPower;
        bool isUpdatedFightPower;
        bool boderLineChecked = false;

        /// <summary>
        /// 所有武将
        /// </summary>
        public SangoObjectList<Person> allPersons = new SangoObjectList<Person>();
        /// <summary>
        /// 所有设施
        /// </summary>
        public SangoObjectList<Building> allBuildings = new SangoObjectList<Building>();

        /// <summary>
        /// 所有内城设施
        /// </summary>
        public SangoObjectList<Building> allInnerBuildings = new SangoObjectList<Building>();
        public int[] innerSlot;

        public List<TroopType> activedTroopType = new List<TroopType>();
        int fightPower = 0;
        /// <summary>
        /// 正在转移到此城市的武将
        /// </summary>
        public List<Person> trsformingPesonList = new List<Person>();

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<City, List<Cell>> raodToNeighborCache = new Dictionary<City, List<Cell>>();

        /// <summary>
        /// 所有内政设施
        /// </summary>
        public SangoObjectList<Building> allIntriorBuildings = new SangoObjectList<Building>();

        /// <summary>
        /// AI指令集
        /// </summary> 
        public List<System.Func<City, Scenario, bool>> AICommandList = new List<System.Func<City, Scenario, bool>>();

        public List<Cell> GetRoadToNeighbor(City city)
        {
            List<Cell> list;
            if (!raodToNeighborCache.TryGetValue(city, out list))
            {
                list = new List<Cell>();
                Scenario.Cur.Map.GetDirectPath(CenterCell, city.CenterCell, list);
                raodToNeighborCache.Add(city, list);
                city.raodToNeighborCache.Add(this, list);
            }
            return list;
        }

        public int FightPower
        {
            get
            {
                return fightPower;
            }
        }

        public Person Add(Person person) { allPersons.Add(person); return person; }
        public Person Remove(Person person) { allPersons.Remove(person); return person; }

        public void UpdateActiveTroopTypes()
        {
            activedTroopType.Clear();
            Scenario.Cur.CommonData.TroopTypes.ForEach(x =>
            {
                if (x.activeCondition == null || (x.activeCondition != null && x.activeCondition.Check(null)))
                    activedTroopType.Add(x);
            });
        }

        //public bool CreateTroop(int troopTypeId)
        //{
        //    TroopType troopType = Scenario.Cur.CommonData.TroopTypes.Get(troopTypeId);
        //    return CreateTroop(troopType);
        //}

        //public bool CreateTroop(TroopType troopType)
        //{
        //    if (troopType == null) return false;
        //    //if (!activedTroopType.Contains(troopType))
        //    //    return false;

        //    if (troopType.costFood > 0 || troopType.costGold > 0 || troopType.costPopulation > 0)
        //    {
        //        if (food < troopType.costFood)
        //            return false;
        //        if (gold < troopType.costGold)
        //            return false;
        //        if (troopPopulation < troopType.costPopulation)
        //            return false;
        //    }

        //    //if (troopType.costItems != null && troopType.costItems.Length > 0)
        //    //{
        //    //    for (int i = 0; i < troopType.costItems.Length; ++i)
        //    //    {
        //    //        Item it = troopType.costItems[i];
        //    //        if (store.GetItemNumber(it.Id) < it.Num)
        //    //            return false;
        //    //    }
        //    //}

        //    Troop troop = new Troop()
        //    {
        //        troops = troopType.costPopulation,
        //        energy = 100,
        //        morale = 100,
        //    };
        //    troop.TroopType = troopType;

        //    Scenario.Cur.troopSet.Add(troop);
        //    TroopList.Add(troop);
        //    food -= troopType.costFood;
        //    gold -= troopType.costGold;
        //    troopPopulation -= troopType.costPopulation;

        //    //for (int i = 0; i < troopType.costItems.Length; ++i)
        //    //{
        //    //    Item it = troopType.costItems[i];
        //    //    store.Remove(it);
        //    //}
        //    Sango.Log.Warning($"{BelongForce.Name}势力在{Name}组建部队:{troopType.Name}");
        //    return true;
        //}

        public override void OnScenarioPrepare(Scenario scenario)
        {
            //x *= 2;
            //y *= 2;

            base.OnScenarioPrepare(scenario);
            //TroopList?.InitCache();// = new SangoObjectList<Troop>().FromString(_troopListStr, scenario.troopSet);
            //NeighborList?.InitCache();// = new SangoObjectList<City>().FromString(_neighborListStr, scenario.citySet);
            //CityLevelType = scenario.CommonData.CityLevelTypes.Get(_cityLevelTypeId);
            innerSlot = new int[CityLevelType.insideSlot];
            if (durability <= 0)
                durability = CityLevelType.maxDurability;

            // 地格占用
            effectCells = new System.Collections.Generic.List<Cell>();
            scenario.Map.GetSpiral(x, y, BuildingType.radius, cell_list);
            foreach (Cell cell in cell_list)
                cell.building = this;

            effectCells.Clear();
            scenario.Map.GetDirectSpiral(CenterCell, BuildingType.radius + 1, BuildingType.radius + 10, effectCells);

            foreach (Person person in CaptiveList)
            {
                if (person.BelongForce != null)
                    person.BelongForce.CaptiveList.Add(person);
            }

            Render = new CityRender(this);
        }

        public override void Init(Scenario scenario)
        {
            if (BuildingType.Id == 1)
            {
                UpdateActiveTroopTypes();
                UpdateFightPower();
            }
            CalculateHarvest();
        }


        public void UpdateFightPower()
        {
            fightPower = 1000;
            fightPower += UnityEngine.Mathf.Min(troops, allPersons.Count * 5000);
            isUpdatedFightPower = true;
            virtualFightPower = FightPower;
            ForeachNeighborCities(x =>
            {
                if (x.IsSameForce(this))
                {
                    if (!x.isUpdatedFightPower)
                    {
                        x.UpdateFightPower();
                        x.isUpdatedFightPower = true;
                    }
                    virtualFightPower += x.FightPower;
                }
            });
        }

        public void CalculateHarvest()
        {
            ScenarioVariables variables = Scenario.Cur.Variables;

            // 人口增长率
            population_increase_factor = variables.populationIncreaseBaseFactor;

            // 计算基础收入
            totalGainFood = CityLevelType.baseGainFood * 30 / 100 + CityLevelType.baseGainFood * agriculture * 70 / CityLevelType.maxAgriculture / 100;
            totalGainGold = CityLevelType.baseGainGold * 30 / 100 + CityLevelType.baseGainGold * commerce * 70 / CityLevelType.maxCommerce / 100;

            // 计算建筑收入
            allBuildings.ForEach(x =>
            {
                if (x.isComplte)
                {
                    population_increase_factor += x.BuildingType.populationGain;
                    totalGainFood += x.BuildingType.foodGain;
                    totalGainGold += x.BuildingType.goldGain;
                }
            });

            float securityInfluence = (((float)security / variables.securityInfluenceMax) - 1) * variables.securityInfluence;
            float popularSupportInfluence = (((float)popularSupport / variables.popularSupportInfluenceMax) - 1) * variables.popularSupportInfluence;
            float leftInfluence = 1.0f + securityInfluence + popularSupportInfluence;

            totalGainFood = Mathf.CeilToInt(leftInfluence * totalGainFood * variables.foodFactor * extraGainFoodFactor);
            totalGainGold = Mathf.CeilToInt(leftInfluence * totalGainGold * variables.goldFactor * extraGainGoldFactor);

            population_increase_factor *= extraPopulationFactor;
        }

        public void ForeachNeighborCities(Action<City> action)
        {
            for (int i = 0; i < NeighborList.Count; i++)
            {
                City c = NeighborList[i];
                if (c == null) continue;
                action(c);
            }
        }


        static bool _IsBorderCity(City city)
        {
            if (city.NeighborList == null)
                return false;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City c = city.NeighborList[i];
                if (c == null) continue;
                if (!city.IsSameForce(c)) return true;
            }
            return false;
        }

        static int _CheckBorder(City city, int len)
        {
            if (!_IsBorderCity(city))
            {
                city.boderLineChecked = true;
                for (int i = 0; i < city.NeighborList.Count; i++)
                {
                    City c = city.NeighborList[i];
                    if (c == null) continue;
                    if (!c.boderLineChecked && city.IsSameForce(c)) return _CheckBorder(c, len + 1);
                }
            }
            else
                return len;

            return 0;
        }

        public void AutoMakeTroop(List<Troop> troopList, int count, bool isAttack)
        {
            if (FreePersonCount <= 0)
                return;

            List<Troop> temp = new List<Troop>();
            if (troops < 6000)
                return;

            // 武将按官职-战力-排序
            List<Person> sorted_by_Official = new List<Person>(freePersons);
            sorted_by_Official.Sort((a, b) =>
            {
                Official a_o = a.Official;
                Official b_o = b.Official;
                int a_m = a.MilitaryAbility;
                int b_m = b.MilitaryAbility;
                if (a_o.level == b_o.level)
                {
                    if (a_m == b_m)
                    {
                        return -a.Strength.CompareTo(b.Strength);
                    }
                    else
                    {
                        return -a_m.CompareTo(b_m);
                    }
                }
                return a_o.level.CompareTo(b_o.level);
            });

            // 临时兵力
            int tempTroops = troops - 3000;

            // 临时库存
            ItemStore tempStore = itemStore.Copy();

            // 确定主将和兵种
            int totalCount = UnityEngine.Mathf.Min(FreePersonCount, count);
            for (int i = 0; i < totalCount; i++)
            {
                Person leader = sorted_by_Official[i];
                int personLimitTroopsNum = 5000;
                int finalTroopsNum = UnityEngine.Mathf.Min(tempTroops, personLimitTroopsNum);
                tempTroops -= finalTroopsNum;
                Troop troop = new Troop()
                {
                    energy = energy,
                    morale = morale,
                    Leader = leader,
                    TroopType = Scenario.Cur.CommonData.TroopTypes.Get(GameRandom.Range(2, 6)),
                    troops = finalTroopsNum,
                };
                temp.Add(troop);
                if (tempTroops <= 0)
                    break;
            }

            temp.RemoveAll(x => x.troops <= 0);

            bool notFull = true;
            while (notFull)
            {
                notFull = false;
                for (int i = 0; i < temp.Count; i++)
                {
                    Troop troop = temp[i];
                    if (!troop.IsFull)
                    {
                        int need = troop.MaxTroops - troop.troops;
                        int finalTroopsNum = UnityEngine.Mathf.Min(tempTroops, UnityEngine.Mathf.Min(need, 1000));
                        tempTroops -= finalTroopsNum;
                        troop.troops += finalTroopsNum;
                        notFull = true;
                    }

                    if (tempTroops <= 0)
                    {
                        notFull = false;
                        break;
                    }
                }
            }

            //// 确定兵种
            //for (int i = 0; i < totalCount; i++)
            //{
            //    Troop troop = temp[i];
            //}

            //// 确定副将
            //for (int i = 0; i < totalCount; i++)
            //{
            //    Troop troop = new Troop()
            //    {
            //        Leader = sorted_by_MilitaryAbility[i]
            //    };
            //    temp.Add(troop);
            //}

            if (temp.Count > 0)
                troopList.AddRange(temp);
        }
        public Troop AutoMakeTroop(bool isAttack)
        {
            if (FreePersonCount <= 0)
                return null;

            if (troops < 10000)
                return null;

            // 武将按官职-战力-排序
            int maxOfficialLevel = 9999;
            Person dstPerson = null;
            int minMilitaryAbility = 0;
            int minCommand = 0;

            freePersons.ForEach(person =>
            {
                if (dstPerson == null)
                {
                    dstPerson = person;
                    maxOfficialLevel = person.Official.level;
                    minMilitaryAbility = person.MilitaryAbility;
                    minCommand = person.Command;
                }
                else
                {

                    if (person.Official.level < maxOfficialLevel)
                    {
                        dstPerson = person;
                        maxOfficialLevel = person.Official.level;
                        minMilitaryAbility = person.MilitaryAbility;
                        minCommand = person.Command;
                    }
                    else if (person.Official.level == maxOfficialLevel)
                    {
                        int personMilitaryAbility = person.MilitaryAbility;
                        if (personMilitaryAbility > minMilitaryAbility)
                        {
                            dstPerson = person;
                            maxOfficialLevel = person.Official.level;
                            minMilitaryAbility = personMilitaryAbility;
                            minCommand = person.Command;
                        }
                        else if (personMilitaryAbility == minMilitaryAbility)
                        {
                            if (person.Command > minCommand)
                            {
                                dstPerson = person;
                                maxOfficialLevel = person.Official.level;
                                minMilitaryAbility = personMilitaryAbility;
                                minCommand = person.Command;
                            }
                        }
                    }
                }
            });

            // 没写完
            return null;
        }

        /// <summary>
        /// 季度粮食收入
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public override bool OnSeasonStart(Scenario scenario)
        {
            if (BelongCorps == null)
                return true;

            int harvest = GameRandom.Random(totalGainFood, 0.05f);
            food += harvest;
            Sango.Log.Print($"城市：{Name}, 收获粮食：{harvest}, 现有粮食: {food}");

            if (Render != null)
                Render.UpdateRender();

            return base.OnSeasonStart(scenario);
        }


        /// <summary>
        /// 月度金钱收入
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public override bool OnMonthStart(Scenario scenario)
        {
            int pop = 0;
            int troopPop = 0;
            if (scenario.Variables.populationEnable)
            {
                pop = GameRandom.Random((int)(population * population_increase_factor), 0.1f);
                population += pop;
                troopPop = (int)(pop * 0.3f);
                troopPopulation += troopPop;
            }

            if (BelongCorps == null)
                return true;

            int inComingGold = GameRandom.Random(totalGainGold, 0.05f);
            inComingGold -= GoldCost(scenario);
            gold += inComingGold;
            if (gold < 0) gold = 0;
#if SANGO_DEBUG
            Sango.Log.Print($"城市：{Name}, 武将人数:{allPersons.Count}, 收入<-- 金钱:{inComingGold}, 人口:{pop}, 兵役:{troopPop}, 现有金钱: {gold}, 人口:{population}, 兵役:{troopPopulation}");
#endif
            if (Render != null)
                Render.UpdateRender();

            return base.OnMonthStart(scenario);
        }
        public override bool OnDayStart(Scenario scenario)
        {
            return base.OnDayStart(scenario);
        }

        public void OnBuildingComplete(Building building, Person builder)
        {
            if (builder != null)
                builder.merit += building.durability / 10;
            this.CalculateHarvest();
        }

        public void OnBuildingDestroy(Building building)
        {
            this.CalculateHarvest();
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            AIPrepared = false;
            AIFinished = false;
            ActionOver = false;
            boderLineChecked = false;
            CalculateHarvest();
            UpdateFightPower();
            JobHealingTroop();
            freePersons.Clear();
            allPersons.ForEach(person =>
            {
                if (!person.ActionOver && person.IsFree)
                    freePersons.Add(person);
            });

            FoodCost(scenario);

            if (durability < CityLevelType.maxDurability)
            {
                durability += Leader?.BaseBuildAbility / 3 ?? 50;
                if (durability > CityLevelType.maxDurability)
                    durability = CityLevelType.maxDurability;
            }

            if (Render != null)
                Render.UpdateRender();

            return base.OnDayStart(scenario);
        }
        public override bool OnTurnEnd(Scenario scenario)
        {
            CurActiveTroop = null;
            isUpdatedFightPower = false;
            return base.OnDayStart(scenario);
        }

        public void FoodCost(Scenario scenario)
        {
            if (food > 0)
            {
                int foodCost = 0;
                foodCost += (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInCity * (troops + woundedTroops));
                int needFood = foodCost - food;
                if (needFood > 0)
                {
                    float runawayTroops = ((float)needFood / (float)foodCost) * scenario.Variables.runawayWhenCityFoodNotEnough;
                    troops = (int)System.Math.Ceiling(troops * (1.0f - runawayTroops));
                    if (woundedTroops > 100)
                    {
                        woundedTroops = (int)System.Math.Ceiling(woundedTroops * (1.0f - runawayTroops));
                    }
                    else
                    {
                        woundedTroops = 0;
                    }
                    food = 0;
                }
                else
                    food -= foodCost;

            }
            else
            {
                food = 0;
                float runawayTroops = scenario.Variables.runawayWhenCityFoodNotEnough;
                troops = (int)System.Math.Ceiling(troops * (1.0f - runawayTroops));
                if (woundedTroops > 100)
                {
                    woundedTroops = (int)System.Math.Ceiling(woundedTroops * (1.0f - runawayTroops));
                }
                else
                {
                    woundedTroops = 0;
                }
            }
        }
        public int GoldCost(Scenario scenario)
        {
            int goldCost = 0;
            allPersons.ForEach(person =>
            {
                if (person.Official != null)
                {
                    goldCost += person.Official.cost;
                }
            });
            return goldCost;
        }

        //public void CreateTroop(Troop troop)
        //{
        //    AddTroop(troop);

        //}

        //public void AddTroop(Troop troop)
        //{
        //    // 先加入剧本才能分配ID
        //    Add(troop);
        //    troop.Leader.BelongTroop = troop;
        //    for (int i = 0; i < troop.MemberList.Count; i++)
        //        troop.MemberList[i].BelongTroop = troop;

        //    troop.BelongCity = this;
        //    troop.cell = CenterCell;
        //    troop.cell.troop = troop;
        //    troop.x = troop.cell.x;
        //    troop.y = troop.cell.y;
        //}

        //public Person Add(Person person)
        //{
        //    allPersons.Add(person);
        //    if (BelongCorps == null)
        //    {
        //        Sango.Log.Error($"why {Name}->BelongCorps is null");
        //    }
        //    BelongCorps.Add(person);
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(BelongCorps.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(BelongCorps.Add(building));
        //    return building;
        //}
        //public Troop Add(Troop troop)
        //{
        //    this.TroopList.Add(troop);
        //    return troop;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(person);
        //    BelongCorps.Remove(person);
        //    return person;
        //}
        //public Troop Remove(Troop troop)
        //{
        //    this.TroopList.Remove(troop);
        //    return troop;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(BelongCorps.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(BelongCorps.Remove(building));
        //    return building;
        //}

        public override bool ChangeDurability(int num, Troop atk)
        {
            bool rs = base.ChangeDurability(num, atk);
            if (rs)
            {
                durability = 1500;
            }

            if (Render != null)
                Render.UpdateRender();

            return rs;
        }

        public City GetNearnestForceCity()
        {
            for (int i = 0; i < NeighborList.Count; i++)
            {
                City city = NeighborList[i];
                if (city.IsSameForce(this))
                    return city;
            }

            City nearnest = null;
            int distance = 100000;
            if (BelongForce != null)
            {
                BelongForce.ForEachCity(city =>
                {
                    if (city != this)
                    {
                        int dis = Scenario.Cur.Map.Distance(city.CenterCell, this.CenterCell);
                        if (dis < distance)
                        {
                            distance = dis;
                            nearnest = city;
                        }
                    }
                });
            }
            return nearnest;
        }

        public Corps ChangeCorps(Corps other)
        {
            Corps last = null;
            if (BelongCorps != other)
            {
                last = BelongCorps;
                BelongCorps = other;
                if (BelongForce != other.BelongForce)
                {
                    BelongForce = other.BelongForce;
                }
                Render?.UpdateRender();
            }
            return last;
        }

        public override void OnFall(Troop atk)
        {
            // 白城
            if (BelongCorps == null)
            {
                ChangeCorps(atk.BelongCorps);
                Leader = atk.Leader;
                atk.EnterCity(this);
                Render?.UpdateRender();
                CalculateHarvest();
                Scenario.Cur.Event.OnCityFall?.Invoke(this, atk, Scenario.Cur);
                return;
            }

            // 确认一个撤退城市
            City escapeCity = null;
            if (BelongForce.CityCount > 1)
            {
                if (this == BelongForce.Governor.BelongCity)
                    escapeCity = GetNearnestForceCity();
                else
                    escapeCity = BelongForce.Governor.BelongCity;
            }

            // 处理正在转移的人
            foreach (Person person in trsformingPesonList)
            {
                person.SetMission(MissionType.PersonReturn, person.BelongCity, 1);
            }
            trsformingPesonList.Clear();

            // 基础抓捕率
            int cacaptureChangce = escapeCity != null ? Scenario.Cur.Variables.captureChangceWhenCityFall : Scenario.Cur.Variables.captureChangceWhenLastCityFall;

            // 处理俘虏
            List<Person> captiveList = new List<Person>();

            // 移除部队
            for (int i = allPersons.Count - 1; i >= 0; --i)
            {
                Person person = allPersons[i];

                // 没有执行任务的才能被捕获
                if (person.IsFree && GameRandom.Changce(cacaptureChangce))
                {
                    captiveList.Add(person);
                }
                else
                {
                    if (escapeCity != null)
                    {
                        person.ChangeCity(escapeCity);
                        if (person.BelongTroop == null)
                            person.SetMission(MissionType.PersonReturn, person.BelongCity, 1);
                    }
                    else
                    {
                        person.LeaveToWild();
                    }
                }
            }

            if (escapeCity == null)
            {
                Scenario.Cur.troopsSet.ForEach((troop) =>
                {
                    if (troop.IsAlive && troop.BelongCity == this)
                        troop.Clear();
                });
            }

            //处理建筑
            for (int i = allBuildings.Count - 1; i >= 0; i--)
            {
                Building building = allBuildings[i];
                if (building.isComplte && GameRandom.Changce(30))
                {
                    building.ChangeCorps(atk.BelongCorps);
                }
                else
                {
                    building.Destroy();
                }
            }

            if (escapeCity == null)
            {
                Scenario.Cur.corpsSet.Remove(BelongCorps);
#if SANGO_DEBUG
                Sango.Log.Print($"{BelongForce.Name} 灭亡!!!");
#endif
                Scenario.Cur.forceSet.Remove(BelongForce);

                WindowEvent windowEvent = new WindowEvent()
                {
                    windowName = "window_dialog",
                    arg1 = $"{BelongForce.Name} 灭亡!!!"
                };
                RenderEvent.Instance.Add(windowEvent);


                if (Scenario.Cur.forceSet.DataCount == 1)
                {
                    Sango.Log.Print($"{atk.BelongForce.Name} 统一!!!!!!!!!!!!!!");
                }
            }

            ChangeCorps(atk.BelongCorps);

            if (Render != null)
                Render.UpdateRender();

            CalculateHarvest();

            //TODO: 玩家处理俘虏
            for (int i = 0; i < captiveList.Count; ++i)
            {
                Person person = captiveList[i];
                if (atk.BelongForce.Governor.Persuade(person))
                {
                    person.ChangeCorps(atk.BelongCorps);
                }
                else
                {
                    allPersons.Remove(person.BeCaptive(this));
                }
            }

            Scenario.Cur.Event.OnCityFall?.Invoke(this, atk, Scenario.Cur);

        }

        /// <summary>
        /// 获取城市之间的距离
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int Distance(City other)
        {
            Dictionary<City, int> _distanceMap = new Dictionary<City, int>();
            List<City> _openList = new List<City>
            {
                this
            };
            _distanceMap.Add(this, 0);
            while (_openList.Count > 0)
            {
                City current = _openList[0];
                _openList.RemoveAt(0);
                int currentLen = _distanceMap[current];
                if (current.NeighborList != null)
                {
                    for (int i = 0, count = current.NeighborList.Count; i < count; i++)
                    {
                        City c = current.NeighborList[i];
                        if (c == other)
                        {
                            return _distanceMap[current] + 1;
                        }
                        if (!_distanceMap.ContainsKey(c))
                        {
                            _distanceMap.Add(c, currentLen + 1);
                            _openList.Add(c);
                        }
                    }
                }
            }
            return 0;
        }

        public void OnPersonReturnCity(Person person)
        {
            Sango.Log.Print($"[{person.BelongForce.Name}]{person.Name}回到[{BelongForce.Name}]<{Name}>");
        }
        public void OnPersonTransformEnd(Person person)
        {
            trsformingPesonList.Remove(person);
        }

        public Building BuildBuilding(Cell buildCenter, Person builder, BuildingType buildingType)
        {

            Building building = new Building();
            building.BelongForce = BelongForce;
            building.BelongCorps = BelongCorps;
            building.BelongCity = this;
            building.BuildingType = buildingType;
            building.x = buildCenter.x;
            building.y = buildCenter.y;
            building.durability = 0;

            Scenario scenario = Scenario.Cur;

            // TODO: 获取高度
            // ------
            scenario.buildingSet.Add(building);
            building.Init(scenario);
            building.Builder = builder;
            building.isComplte = false;
            building.durability = 1;

            builder.SetMission(MissionType.PersonBuild, building, 0);

            Sango.Log.Print($"[{BelongForce.Name}]在<{Name}>由{builder.Name}开始修建: {building.Name}");

            return building;
        }

        public Building BuildBuilding(int slotId, Person builder, BuildingType buildingType)
        {
            Building building = new Building();
            building.BelongForce = BelongForce;
            building.BelongCorps = BelongCorps;
            building.BelongCity = this;
            building.BuildingType = buildingType;
            building.SlotId = slotId;
            building.durability = 0;

            Scenario scenario = Scenario.Cur;

            // TODO: 获取高度
            // ------
            scenario.buildingSet.Add(building);
            building.Init(scenario);
            building.Builder = builder;
            building.isComplte = false;
            building.durability = 1;

            builder.SetMission(MissionType.PersonBuild, building, 0);

            Sango.Log.Print($"@内政@[{BelongForce.Name}]在<{Name}>由{builder.Name}开始修建: {building.Name}");

            return building;
        }

        /// <summary>
        /// 农业
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public bool JobFarming(List<Person> personList)
        {
            if (agriculture >= CityLevelType.maxAgriculture) return false;
            if (personList == null || personList.Count == 0) return false;
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Farming;

            // TODO: 特性对开发的影响
            int goldNeed = variables.jobCost[jobId];
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
                (x) => { goldNeed = x; });

            if (gold < goldNeed)
                return false;

            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];


#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int totalValue = 0;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                totalValue += person.BaseAgricultureAbility;
                person.merit += meritGain;
                person.GainExp(meritGain);
                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            scenario.Event.OnCityJobResult?.Invoke(this, jobId, personList, count,
                (x) => { totalValue = x; });

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            agriculture += totalValue;
            if (agriculture > CityLevelType.maxAgriculture)
                agriculture = CityLevelType.maxAgriculture;

#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了开垦!农业值达到了:{agriculture}");
#endif
            return true;
        }

        /// <summary>
        /// 开发
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public bool JobDevelop(List<Person> personList)
        {
            if (commerce >= CityLevelType.maxCommerce) return false;
            if (personList == null || personList.Count == 0) return false;
            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Develop;

            // TODO: 特性对开发的影响
            int goldNeed = variables.jobCost[jobId];
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
               (x) => { goldNeed = x; });

            if (gold < goldNeed)
                return false;

            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int totalValue = 0;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                totalValue += person.BaseCommerceAbility;
                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            scenario.Event.OnCityJobResult?.Invoke(this, jobId, personList, count,
                (x) => { totalValue = x; });

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            commerce += totalValue;
            if (commerce > CityLevelType.maxCommerce)
                commerce = CityLevelType.maxCommerce;

#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了开发!商业值达到了:{commerce}");
#endif
            return true;
        }

        /// <summary>
        /// 治安巡视
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public bool JobInspection(List<Person> personList)
        {
            if (security >= 100) return false;
            if (personList == null || personList.Count == 0) return false;
            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Inspection;

            int goldNeed = variables.jobCost[jobId];
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
                (x) => { goldNeed = x; });


            if (gold < goldNeed)
                return false;

            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int totalValue = 0;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                totalValue += person.BaseSecurityAbility;
                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }
            // 
            scenario.Event.OnCityJobResult?.Invoke(this, jobId, personList, count,
                  (x) => { totalValue = x; });

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            security += totalValue;
            if (security > 100)
                security = 100;

#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了巡视!治安提升到了:{security}");
#endif
            return true;
        }

        /// <summary>
        /// 训练
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public bool JobTrainTroop(List<Person> personList)
        {
            if (morale >= 100) return false;
            if (personList == null || personList.Count == 0) return false;
            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.TrainTroop;

            int goldNeed = variables.jobCost[jobId];
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
                (x) => { goldNeed = x; });

            if (gold < goldNeed)
                return false;

            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int totalValue = 0;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                totalValue += person.BaseTrainTroopAbility;
                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }
            scenario.Event.OnCityJobResult?.Invoke(this, jobId, personList, count,
                 (x) => { totalValue = x; });

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            morale += totalValue;
            if (morale > 100)
                morale = 100;

#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了训练!士气提升到了:{morale}");
#endif
            return true;
        }

        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public bool JobSearching(List<Person> personList)
        {
            if (personList == null || personList.Count == 0) return false;

            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Searching;

            int goldNeed = variables.jobCost[jobId];
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
             (x) => { goldNeed = x; });

            if (gold < goldNeed)
                return false;


            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];

            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                float ability_improve = (float)person.BaseSearchingAbility / 50f;
                freePersons.Remove(person);
                // 发现人才
                if (wildPersons.Count > 0)
                {
                    Person wild_dest = null;
                    for (int j = 0; j < wildPersons.Count; j++)
                    {
                        Person wild = wildPersons[i];
                        if (wild != null && wild.IsAlive && wild.IsValid && !wild.beFinded)
                        {
                            wild_dest = wild;
                            break;
                        }
                    }

                    if (wild_dest != null)
                    {
                        scenario.Event.OnCityJobSearchingWild?.Invoke(this, jobId, personList, count,
                            (x) => { ability_improve = x; });

                        if (GameRandom.Changce((int)(10f * ability_improve)))
                        {
#if SANGO_DEBUG
                            Sango.Log.Print($"@内政@[{BelongForce.Name}]<{Name}>的{person.Name}发现了人才->{wild_dest.Name}");
#endif
                            wild_dest.beFinded = true;
                            person.merit += meritGain;
                            person.GainExp(meritGain);

                            person.ActionOver = true;
                        }
                    }
                }

                //TODO: 搜索道具
                //if (!person.ActionOver && GameRandom.Changce((int)(3 * ability_improve)))
                //{
                //    person.ActionOver = true;
                //    continue;
                //}


                // 搜索钱财
                if (!person.ActionOver && GameRandom.Changce((int)(10 * ability_improve)))
                {
                    person.merit += meritGain;
                    person.GainExp(meritGain);

                    person.ActionOver = true;
                }

                //TODO: 触发事件

                // 什么也没找到
                person.merit += meritGain;
                person.GainExp(meritGain);

                person.ActionOver = true;
            }

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            BelongForce.GainTechniquePoint(techniquePointGain);
            return true;
        }

        /// <summary>
        /// 治疗伤兵
        /// </summary>
        /// <returns></returns>
        public bool JobHealingTroop()
        {
            // 城池满了不再招募

            if (woundedTroops <= 0) return false;
            int recruitNum = agriculture + commerce;
            int rs = Math.Min(woundedTroops, recruitNum);
            troops += rs;
            woundedTroops -= rs;
#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]<{Name}>进行了士兵治愈!共治愈到{rs}人, 当前士兵提升到了:{troops}");
#endif
            return true;
        }

        /// <summary>
        /// 招募
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public bool JobRecuritTroop(List<Person> personList)
        {

            if (personList == null || personList.Count == 0) return false;
            // 城池满了不再招募
            if (TroopsIsFull) return false;

            Scenario scenario = Scenario.Cur;

            if (scenario.Variables.populationEnable && troopPopulation <= 500) return false;
            if (security < 60) return false;
            if (troops > food) return false;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.TrainTroop;

            int goldNeed = variables.jobCost[jobId];
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
                 (x) => { goldNeed = x; });

            if (gold < goldNeed)
                return false;

            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
            int lastTroops = troops;
#endif
            int totalValue = 0;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                totalValue += person.BaseRecruitmentAbility;
                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            scenario.Event.OnCityJobResult?.Invoke(this, jobId, personList, count,
                 (x) => { totalValue = x; });

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            if (Scenario.Cur.Variables.populationEnable)
            {
                totalValue = Math.Min(totalValue, troopPopulation);
                troopPopulation -= totalValue;
                population -= totalValue;
            }

            if (totalValue + troops > CityLevelType.maxTroops)
                totalValue = CityLevelType.maxTroops - troops;

            //士气减少
            morale -= (troops * morale + totalValue * 30) / (troops + totalValue);
            troops += totalValue;
            //治安减少
            security -= Math.Min(6, 4 * totalValue / 1000);

            BelongForce.GainTechniquePoint(techniquePointGain);

#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了招募!共招募到{troops - lastTroops}人, 当前士兵人数提升到了:{troops}");
#endif
            return true;
        }

        /// <summary>
        /// 生产兵装
        /// </summary>
        /// <param name="personList"></param>
        /// <param name="itemType"></param>
        /// <returns></returns>
        public bool JobCreateItems(List<Person> personList, ItemType itemType)
        {
            if (personList == null || personList.Count == 0 || itemType == null) return false;
            if (itemStore.TotalNumber >= CityLevelType.storeMax) return false;

            Scenario scenario = Scenario.Cur;
            int empty = CityLevelType.storeMax - itemStore.TotalNumber;
            if (empty < 1000) return false;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateItems;

            int goldNeed = variables.jobCost[jobId] + itemType.cost;
            int maxCount = variables.jobMaxPersonCount[jobId];
            int count = Math.Min(personList.Count, maxCount);

            scenario.Event.OnCityCheckJobCost?.Invoke(this, jobId, personList, count,
                 (x) => { goldNeed = x; });

            if (gold < goldNeed)
                return false;

            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
            int lastTroops = troops;
#endif
            int totalValue = 0;
            for (int i = 0; i < count; i++)
            {
                Person person = personList[i];
                totalValue += person.BaseRecruitmentAbility;
                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            scenario.Event.OnCityJobResult?.Invoke(this, jobId, personList, count,
                 (x) => { totalValue = x; });

            scenario.Event.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, count,
                (x) => { techniquePointGain = x; });

            totalValue = Math.Min(empty, totalValue);
            int exsistNumber = itemStore.Add(itemType.Id, totalValue);

            BelongForce.GainTechniquePoint(techniquePointGain);

#if SANGO_DEBUG
            Sango.Log.Print($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了生产兵装!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}");
#endif
            return true;
        }

        public bool JobRecuritPerson(Person person, Person dest)
        {
            if (dest.BelongCity == person.BelongCity)
            {
                // 直接招募
                person.JobRecuritPerson(dest);
                freePersons.Remove(person);
                wildPersons.Remove(dest);
            }
            else
            {
                person.SetMission(MissionType.PersonRecruitPerson, dest, Scenario.Cur.GetCityDistance(person.BelongCity, dest.BelongCity));
            }
            return true;
        }

        /// <summary>
        /// 获取城池的攻击力
        /// </summary>
        /// <returns></returns>
        public override int GetAttack()
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            // 根据太守数值来计算基础伤害
            int atk = Math.Max(BuildingType.atk, (Leader?.Strength ?? 50 * 5000 + Leader?.Command ?? 50 * 5000) / 10000);

            return atk;
        }

        /// <summary>
        /// 获取城池的防御力
        /// </summary>
        /// <returns></returns>
        public override int GetDefence()
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;

            // 根据太守数值来计算基础防御
            int def = Math.Max(50, (Leader?.Intelligence ?? 30 * 3000 + Leader?.Command ?? 30 * 7000) / 10000);

            return def;
        }

        /// <summary>
        /// 获取最大耐久
        /// </summary>
        /// <returns></returns>
        public override int GetMaxDurability()
        {
            return CityLevelType.maxDurability;
        }

        /// <summary>
        /// 获取
        /// </summary>
        /// <returns></returns>
        public override int GetSkillMethodAvaliabledTroops()
        {
            return Math.Max(10000, durability * troops / GetMaxDurability());
        }

        public struct EnemyInfo
        {
            public Troop troop;
            public int distance;
        }

        const int SAVE_ROUND = 15;
        List<EnemyInfo> enemies = new List<EnemyInfo>();
        bool[] enemiesRound = new bool[SAVE_ROUND];

        public Troop GetNearestEnemy(Cell checkCell)
        {
            Troop target = null;
            int dis = 999999;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInfo enemyInfo = enemies[i];
                int distance = Scenario.Cur.Map.Distance(enemyInfo.troop.cell, checkCell);
                if (distance < dis)
                {
                    target = enemyInfo.troop;
                }
            }
            return target;
        }



        public static List<Person> sort_by_TroopAbility = new List<Person>();
        public static List<Person> sort_by_BaseCommerceAbility = new List<Person>();
        public static List<Person> sort_by_BaseSecurityAbility = new List<Person>();
        public static List<Person> sort_by_BaseAgricultureAbility = new List<Person>();
        public static List<Person> sort_by_BaseBuildAbility = new List<Person>();
        public static List<Person> sort_by_BaseRecruitmentAbility = new List<Person>();
        public static List<Person> sort_by_BaseTrainTroopAbility = new List<Person>();

        struct SortPerson
        {
            public Person person;
            public int attr;
        }
        public void SortFreePersons()
        {
            sort_by_TroopAbility.Clear();
            sort_by_BaseCommerceAbility.Clear();
            sort_by_BaseSecurityAbility.Clear();
            sort_by_BaseAgricultureAbility.Clear();
            sort_by_BaseBuildAbility.Clear();
            sort_by_BaseRecruitmentAbility.Clear();
            sort_by_BaseTrainTroopAbility.Clear();

            sort_by_TroopAbility.AddRange(freePersons);
            sort_by_BaseCommerceAbility.AddRange(freePersons);
            sort_by_BaseSecurityAbility.AddRange(freePersons);
            sort_by_BaseAgricultureAbility.AddRange(freePersons);
            sort_by_BaseBuildAbility.AddRange(freePersons);
            sort_by_BaseRecruitmentAbility.AddRange(freePersons);
            sort_by_BaseTrainTroopAbility.AddRange(freePersons);

            sort_by_TroopAbility.Sort((a, b) => { return -a.MilitaryAbility.CompareTo(b.MilitaryAbility); });
            sort_by_BaseCommerceAbility.Sort((a, b) => { return -a.BaseCommerceAbility.CompareTo(b.BaseCommerceAbility); });
            sort_by_BaseSecurityAbility.Sort((a, b) => { return -a.BaseSecurityAbility.CompareTo(b.BaseSecurityAbility); });
            sort_by_BaseAgricultureAbility.Sort((a, b) => { return -a.BaseAgricultureAbility.CompareTo(b.BaseAgricultureAbility); });
            sort_by_BaseBuildAbility.Sort((a, b) => { return -a.BaseBuildAbility.CompareTo(b.BaseBuildAbility); });
            sort_by_BaseRecruitmentAbility.Sort((a, b) => { return -a.BaseRecruitmentAbility.CompareTo(b.BaseRecruitmentAbility); });
            sort_by_BaseTrainTroopAbility.Sort((a, b) => { return -a.BaseTrainTroopAbility.CompareTo(b.BaseTrainTroopAbility); });
        }


        public bool IsEnemiesRound(int round)
        {
            if (round < enemiesRound.Length)
                return enemiesRound[round];
            return false;
        }
        public bool IsEnemiesRound()
        {
            for (int i = 0; i < enemiesRound.Length; i++)
            {
                if (enemiesRound[i]) return true;
            }
            return false;
        }


        public bool CheckEnemiesIfAlive(out EnemyInfo enemyInfo)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInfo check = enemies[i];
                if (check.troop.IsAlive)
                {
                    enemyInfo = check;
                    return true;
                }
            }
            enemyInfo = default;
            return false;
        }

        internal List<Troop> troopTempList = new List<Troop>();
        internal Troop CurActiveTroop = null;
        internal List<Troop> CurActiveTroopList = new List<Troop>();

        public Troop EnsureTroop(Troop troop, Scenario scenario, int trunCount)
        {
            // 先加入剧本才能分配ID
            troop.cell = this.CenterCell;
            this.CenterCell.troop = troop;
            scenario.Add(troop);
            troop.Init(scenario);
            int costFood = (int)System.Math.Floor(troop.troops * trunCount * scenario.Variables.baseFoodCostInTroop * troop.TroopType.foodCostFactor);
            costFood = Math.Max(costFood, 1);
            troop.food = costFood;
            food -= costFood;
            troops -= troop.troops;
            troop.ForEachPerson(person =>
            {
                person.BelongTroop = troop;
                freePersons.Remove(person);
            });
            return troop;
        }

        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                scenario.Event.OnCityAIStart?.Invoke(this, scenario);
                AIPrepared = true;
            }

            while (AICommandList.Count > 0)
            {
                System.Func<City, Scenario, bool> CurrentCommand = AICommandList[0];
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                AICommandList.RemoveAt(0);
            }

            scenario.Event.OnCityAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            ActionOver = true;
            return true;
        }

        public void AIPrepare(Scenario scenario)
        {
            // 准备敌人信息
            enemies.Clear();
            for (int i = 0; i < enemiesRound.Length; i++)
                enemiesRound[i] = false;

            scenario.troopsSet.ForEach(x =>
            {
                if (x.IsEnemy(this))
                {
                    int round = scenario.Map.Distance(CenterCell, x.cell);
                    if (round < SAVE_ROUND)
                    {
                        enemies.Add(new EnemyInfo { troop = x, distance = round });
                        for (int j = round; j < enemiesRound.Length; j++)
                            enemiesRound[j] = true;
                    }
                }
            });

            if (enemies.Count > 1)
            {
                enemies.Sort((a, b) =>
                {
                    return a.distance.CompareTo(b.distance);
                });
            }

            UpdateActiveTroopTypes();
            UpdateFightPower();
            SortFreePersons();

            if (IsBorderCity)
            {
                AICommandList.Add(CityAI.AIAttack);
                AICommandList.Add(CityAI.AIIntrior);
                AICommandList.Add(CityAI.AIRecuritTroop);
            }
            else
            {
                // 物资输送
                AICommandList.Add(CityAI.AITransfrom);
                AICommandList.Add(CityAI.AIIntrior);
                AICommandList.Add(CityAI.AIRecuritTroop);
            }

            scenario.Event.OnCityAIPrepare?.Invoke(this, scenario);
        }

        /// <summary>
        /// 检查是否太守需要重新设置
        /// </summary>
        /// <param name="person"></param>
        public void CheckIfLoseLeader(Person person)
        {
            if (Leader != person) return;

            Person dest = null;
            Official higher = null;
            int commandHigher = 0;
            for (int i = 0; i < allPersons.Count; i++)
            {
                Person checker = allPersons[i];
                if (checker != null && checker != Leader && checker.IsAlive)
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
            }
            Leader = dest;
        }

        /// <summary>
        /// 更新太守
        /// </summary>
        /// <param name="person"></param>
        public void UpdateLeader(Person person)
        {
            if (person.BelongCity == this)
            {
                return;
            }

            if (person == BelongForce.Governor)
            {
                person.BelongCity.CheckIfLoseLeader(person);
                Leader = person;
                return;
            }

            if (person.Official.level > Leader.Official.level)
            {
                person.BelongCity.CheckIfLoseLeader(person);
                Leader = person;
                return;
            }
            else if (person.Official.level == Leader.Official.level)
            {
                if (person.Command > Leader.Command)
                {
                    person.BelongCity.CheckIfLoseLeader(person);
                    Leader = person;
                    return;
                }
            }

        }

    }
}
