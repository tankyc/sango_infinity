using Sango.Core.Player;
using Sango.Render;
using System;
using System.Collections.Generic;
#if SANGO_DEBUG
using System.Text;
#endif
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 城市类，继承自BuildingBase，用于管理城市的各种属性和行为
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class City : BuildingBase
    {

        /// <summary>
        /// AI是否完成行动
        /// </summary>
        public virtual bool AIFinished { get; set; }
        /// <summary>
        /// AI是否准备完成
        /// </summary>
        public virtual bool AIPrepared { get; set; }
        /// <summary>
        /// 对象类型，返回城市类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.City; } }
        /// <summary>
        /// 带颜色的城市名称
        /// </summary>
        public override string ColorName => $"<color=#93C86D>{Name}</color>";

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
        /// 工作委任类型
        /// </summary>
        [JsonProperty] public int workingAppointType;

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
        /// 最大士气
        /// </summary>
        public int MaxMorale { get; set; }

        /// <summary>
        /// 是否有商人, 数字为兑换比例 0为没有商人
        /// </summary>
        [JsonProperty] public byte hasBusiness;

        /// <summary>
        /// 当前兵力
        /// </summary>
        [JsonProperty] public int troops;

        /// <summary>
        /// 当前伤兵
        /// </summary>
        [JsonProperty] public int woundedTroops;

        /// <summary>
        /// 可容纳兵力基础值
        /// </summary>
        [JsonProperty] public int troopsLimit;
        /// <summary>
        /// 可容纳兵力总值
        /// </summary>
        public int TroopsLimit => troopsLimit + CityLevelType.troopsLimitAdd + troopsLimitAdd;
        /// <summary>
        /// 可容纳兵力额外增加值
        /// </summary>
        int troopsLimitAdd;
        /// <summary>
        /// 仓库大小基础值
        /// </summary>
        [JsonProperty] public int storeLimit;
        /// <summary>
        /// 仓库大小总值
        /// </summary>
        public int StoreLimit => storeLimit + CityLevelType.storeLimitAdd + storeLimitAdd;
        /// <summary>
        /// 仓库大小额外增加值
        /// </summary>
        int storeLimitAdd;

        /// <summary>
        /// 金库大小基础值
        /// </summary>
        [JsonProperty] public int goldLimit;
        /// <summary>
        /// 金库大小总值
        /// </summary>
        public int GoldLimit => goldLimit + CityLevelType.goldLimitAdd + goldLimitAdd;
        /// <summary>
        /// 金库大小额外增加值
        /// </summary>
        int goldLimitAdd;

        /// <summary>
        /// 粮仓大小基础值
        /// </summary>
        [JsonProperty] public int foodLimit;
        /// <summary>
        /// 粮仓大小总值
        /// </summary>
        public int FoodLimit => foodLimit + CityLevelType.foodLimitAdd + foodLimitAdd;
        /// <summary>
        /// 粮仓大小额外增加值
        /// </summary>
        int foodLimitAdd;

        /// <summary>
        /// 城内建筑槽位
        /// </summary>
        //[JsonProperty] public int insideSlot;
        //public int InsideSlot => insideSlot + CityLevelType.insideSlotAdd;

        /// <summary>
        /// 城外建筑槽位
        /// </summary>
        //[JsonProperty] public int outsideSlot;
        //public int OutsideSlot => outsideSlot + CityLevelType.outsideSlotAdd;

        /// <summary>
        /// 村庄槽位
        /// </summary>
        //[JsonProperty] public int villageSlot;
        //public int VillageSlot => villageSlot + CityLevelType.villageSlotAdd;

        /// <summary>
        /// 基础金钱收入 基础收入 = 基础收入 * 当前商业值 / 最大商业值
        /// </summary>
        [JsonProperty] public int baseGainGold;
        public virtual int BaseGainGold => baseGainGold + CityLevelType.baseGainGoldAdd;

        /// <summary>
        /// 基础粮食收入 基础收入 = 基础粮食收入 * 当前农业值 / 最大农业值
        /// </summary>
        [JsonProperty] public int baseGainFood;
        public virtual int BaseGainFood => baseGainFood + CityLevelType.baseGainFoodAdd;

        /// <summary>
        /// 最大商业值
        /// </summary>
        [JsonProperty] public int commerceLimit;
        public int CommerceLimit => commerceLimit + CityLevelType.commerceLimitAdd;

        /// <summary>
        /// 最大农业值
        /// </summary>
        [JsonProperty] public int agricultureLimit;

        public int AgricultureLimit => agricultureLimit + CityLevelType.agricultureLimitAdd;
        /// <summary>
        /// 最大耐久基础值
        /// </summary>
        [JsonProperty] public int durabilityLimit;
        /// <summary>
        /// 最大耐久额外增加值
        /// </summary>
        int durabilityLimitAdd;

        /// <summary>
        /// 最大耐久总值
        /// </summary>
        public override int DurabilityLimit => durabilityLimit + CityLevelType.durabilityLimitAdd + durabilityLimitAdd;

        /// <summary>
        /// 是否满兵
        /// </summary>
        public bool TroopsIsFull => troops /*+ woundedTroops*/ >= TroopsLimit;

        /// <summary>
        /// 太守
        /// </summary>
        //[JsonConverter(typeof(Id2ObjConverter<Person>))]
        //[JsonProperty]
        public Person Leader;

        /// <summary>
        /// 所属州
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Province>))]
        [JsonProperty]
        public Province province;

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
        /// 工作计数
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> jobCounter = new Dictionary<int, int>();

        //public List<Building> villageList = new List<Building>();
        /// <summary>
        /// 港口列表
        /// </summary>
        public List<Port> portList = new List<Port>();
        /// <summary>
        /// 关卡列表
        /// </summary>
        public List<Gate> gateList = new List<Gate>();

        /// <summary>
        /// 总粮食收入
        /// </summary>
        public int totalGainFood = 0;
        /// <summary>
        /// 总金钱收入
        /// </summary>
        public int totalGainGold = 0;

        /// <summary>
        /// 额外的影响倍率(事件等)
        /// </summary>
        public float extraGainFoodFactor = 0;
        /// <summary>
        /// 额外的金钱收入倍率
        /// </summary>
        public float extraGainGoldFactor = 0;
        /// <summary>
        /// 额外的人口增长倍率
        /// </summary>
        public float extraPopulationFactor = 1;

        /// <summary>
        /// 人口增长因子
        /// </summary>
        public float population_increase_factor = 0;

        /// <summary>
        /// 人口上限
        /// </summary>
        public int PopulationLimit => Scenario.Cur.Variables.populationLimitBase + CityLevelType.Id * Scenario.Cur.Variables.populationLimitPerLevel;

        /// <summary>
        /// 最大兵役人口
        /// </summary>
        public int MaxTroopPopulation => (int)(population * Scenario.Cur.Variables.maxTroopPopulationRatio);

        /// <summary>
        /// 基础兵役人口
        /// </summary>
        public int BaseTroopPopulation => (int)(population * Scenario.Cur.Variables.baseTroopPopulationRatio);
        /// <summary>
        /// 边界线
        /// </summary>
        internal int borderLine;
        /// <summary>
        /// 是否为边界城市
        /// </summary>
        public bool IsBorderCity => borderLine == 0;
        /// <summary>
        /// 边界线值
        /// </summary>
        public int BorderLine => borderLine;

        /// <summary>
        /// 空闲人员列表
        /// </summary>
        public List<Person> freePersons = new List<Person>();

        /// <summary>
        /// 在野人员列表
        /// </summary>
        public List<Person> wildPersons = new List<Person>();

        /// <summary>
        /// 不可见人员列表
        /// </summary>
        public List<Person> invisiblePersons = new List<Person>();

        /// <summary>
        /// 其他经过人员
        /// </summary>
        public List<Person> otherPersons = new List<Person>();

        /// <summary>
        /// 所有武将
        /// </summary>
        public SangoObjectList<Person> allPersons = new SangoObjectList<Person>();

        /// <summary>
        /// 俘虏
        /// </summary>
        //[JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        //[JsonProperty]
        public SangoObjectList<Person> captiveList = new SangoObjectList<Person>();

        /// <summary>
        /// 空闲人员数量
        /// </summary>
        public int FreePersonCount => freePersons.Count;
        /// <summary>
        /// 人员容纳上限
        /// </summary>
        public int PersonHole { get; set; }

        //public int eventId;
        //public int specialtyId;
        //public int model_wall;
        //public int model_city;
        /// <summary>
        /// 虚拟战斗力
        /// </summary>
        internal int virtualFightPower;
        /// <summary>
        /// 战斗力是否已更新
        /// </summary>
        internal bool isUpdatedFightPower;
        /// <summary>
        /// 边界线是否已检查
        /// </summary>
        internal bool boderLineChecked = false;

        /// <summary>
        /// 所有攻击部队
        /// </summary>
        public List<Troop> allAttackTroops = new List<Troop>();
        /// <summary>
        /// 所有部队
        /// </summary>
        public List<Troop> allTroops = new List<Troop>();

        /// <summary>
        /// 城池直辖范围cell
        /// </summary>
        public List<Cell> areaCellList = new List<Cell>();

        /// <summary>
        /// 添加城池直辖范围的cell
        /// </summary>
        /// <param name="cell">要添加的cell</param>
        public void AddAreaCell(Cell cell)
        {
            areaCellList.Add(cell);
            if (BelongCity != null)
            {
                BelongCity.AddAreaCell(cell);
            }
        }

        /// <summary>
        /// 所有内城设施
        /// </summary>
        //public int[] innerSlot;

        /// <summary>
        /// 已激活的部队类型
        /// </summary>
        public List<TroopType> activedTroopType = new List<TroopType>();
        /// <summary>
        /// 战斗力
        /// </summary>
        int fightPower = 0;

        /// <summary>
        /// 到相邻城市的道路缓存
        /// </summary>
        public Dictionary<City, List<Cell>> raodToNeighborCache = new Dictionary<City, List<Cell>>();

        /// <summary>
        /// 所有设施
        /// </summary>
        public SangoObjectList<Building> allBuildings = new SangoObjectList<Building>();
        /// <summary>
        /// 建筑数量映射
        /// </summary>
        public Dictionary<int, int> buildingCountMap = new Dictionary<int, int>();


        //public SangoObjectList<Building> allIntriorBuildings = new SangoObjectList<Building>();
        //public Dictionary<int, int> buildingCountMap = new Dictionary<int, int>();

        /// <summary>
        /// AI指令集
        /// </summary> 
        public List<System.Func<City, Scenario, bool>> AICommandList = new List<System.Func<City, Scenario, bool>>();

        /// <summary>
        /// 防御cell列表
        /// </summary>
        public List<Cell> defenceCellList = new List<Cell>();
        /// <summary>
        /// 内城cell列表
        /// </summary>
        public List<Cell> interiorCellList = new List<Cell>();

        /// <summary>
        /// 内城cell数量
        /// </summary>
        public int InteriorCellCount => interiorCellList.Count;

        /// <summary>
        /// 攻击部队数量
        /// </summary>
        public int AttackTroopsCount
        {
            get
            {
                //int troopsCount = 0;
                //SangoObjectSet<Troop> troopSet = Scenario.Cur.troopsSet;
                //for (int i = 0; i < troopSet.Count; i++)
                //{
                //    Troop troop = troopSet[i];
                //    if (troop != null && troop.IsAlive && troop.BelongCity == this)
                //        troopsCount++;
                //}
                //return troopsCount;
                return allAttackTroops.Count;
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
        /// 增加金钱
        /// </summary>
        /// <param name="v">增加的金钱数量</param>
        /// <returns>增加后的金钱数量</returns>
        public int AddGold(int v)
        {
            if (v == 0) return gold;
            gold += v;
            if (gold > GoldLimit)
                gold = GoldLimit;
            else if (gold < 0)
                gold = 0;
            return gold;
        }

        /// <summary>
        /// 增加粮食
        /// </summary>
        /// <param name="v">增加的粮食数量</param>
        /// <returns>增加后的粮食数量</returns>
        public int AddFood(int v)
        {
            if (v == 0) return food;
            food += v;
            if (food > FoodLimit)
                food = FoodLimit;
            else if (food < 0)
                food = 0;
            return food;
        }

        /// <summary>
        /// 增加兵力
        /// </summary>
        /// <param name="v">增加的兵力数量</param>
        /// <returns>增加后的兵力数量</returns>
        public int AddTroops(int v)
        {
            if (v == 0) return troops;
            troops += v;
            if (troops > TroopsLimit)
                troops = TroopsLimit;
            else if (troops < 0)
                troops = 0;
            return troops;
        }

        /// <summary>
        /// 增加物品
        /// </summary>
        /// <param name="itemTypeId">物品类型ID</param>
        /// <param name="v">增加的数量</param>
        /// <returns>增加后的物品数量</returns>
        public int AddItem(int itemTypeId, int v)
        {
            return itemStore.Add(itemTypeId, v);
        }

        /// <summary>
        /// 增加治安
        /// </summary>
        /// <param name="v">增加的治安值</param>
        /// <returns>增加后的治安值</returns>
        public int AddSecurity(int v)
        {
            security += v;
            if (security > 100)
                security = 100;
            else if (security < 0)
                security = 0;
            return security;
        }

        /// <summary>
        /// 增加士气
        /// </summary>
        /// <param name="v">增加的士气值</param>
        /// <returns>增加后的士气值</returns>
        public int AddMorale(int v)
        {
            morale += v;
            if (morale > MaxMorale)
                morale = MaxMorale;
            else if (morale < 0)
                morale = 0;
            return morale;
        }

        /// <summary>
        /// 获取到相邻城市的道路
        /// </summary>
        /// <param name="city">目标城市</param>
        /// <returns>道路的cell列表</returns>
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

        /// <summary>
        /// 战斗力
        /// </summary>
        public int FightPower => fightPower;

        /// <summary>
        /// 添加一般武将
        /// </summary>
        /// <param name="person"></param>
        public void AddPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> allPersons 添加 {person.Name} ");
#endif
            allPersons.Add(person);
        }

        /// <summary>
        /// 移除一般武将
        /// </summary>
        /// <param name="person"></param>
        public void RemovePerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> allPersons 删除 {person.Name} ");
#endif
            allPersons.Remove(person);
            freePersons.Remove(person);
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person AddCaptive(Person person, bool breakCircal = false)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> captiveList 添加 {person.Name} ");
#endif
            person.ClearMission();
            person.state = (int)PersonStateType.Prisoner;
            captiveList.Add(person);
            person.BelongForce?.BeCaptiveList.Remove(person);
            person.BelongForce?.BeCaptiveList.Add(person);
            person.ChangeCurrentCity(this);
            if (person.BelongCity != null)
            {
                person.BelongCity.allPersons.Remove(person);
                person.BelongCity.wildPersons.Remove(person);
                person.BelongCity.freePersons.Remove(person);
                person.BelongCity = this;
            }
            if (!breakCircal)
                person.BeCaptive(this, true);
            return person;
        }

        /// <summary>
        /// 添加囚犯
        /// </summary>
        /// <param name="person">要添加的武将</param>
        /// <returns>添加的武将</returns>
        public Person RemoveCaptive(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> captiveList 删除 {person.Name} ");
#endif
            captiveList.Remove(person);
            person.BelongForce?.BeCaptiveList.Remove(person);
            return person;
        }

        /// <summary>
        /// 添加在野武将
        /// </summary>
        /// <param name="person"></param>
        public void AddWildPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> wildPersons 添加 {person.Name} ");
#endif
            wildPersons.Add(person);
        }

        /// <summary>
        /// 移除在野武将
        /// </summary>
        /// <param name="person"></param>
        public void RemoveWildPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> wildPersons 删除 {person.Name} ");
#endif
            wildPersons.Remove(person);
        }

        /// <summary>
        /// 添加未发现武将
        /// </summary>
        /// <param name="person"></param>
        public void AddInvisiblePerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> invisiblePersons 添加 {person.Name} ");
#endif
            invisiblePersons.Add(person);
        }

        /// <summary>
        /// 移除未发现武将
        /// </summary>
        /// <param name="person"></param>
        public void RemoveInvisiblePerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> invisiblePersons 删除 {person.Name} ");
#endif
            invisiblePersons.Remove(person);
        }

        /// <summary>
        /// 添加其他过路武将
        /// </summary>
        /// <param name="person"></param>
        public void AddOtherPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> otherPersons 添加 {person.Name} ");
#endif
            otherPersons.Add(person);
        }

        /// <summary>
        /// 移除其他过路武将
        /// </summary>
        /// <param name="person"></param>
        public void RemoveOtherPerson(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"*{Name} -> otherPersons 删除 {person.Name} ");
#endif
            otherPersons.Remove(person);
        }

        /// <summary>
        /// 更新已激活的部队类型
        /// </summary>
        public void UpdateActiveTroopTypes()
        {
            activedTroopType.Clear();
            Scenario.Cur.CommonData.TroopTypes.ForEach(x =>
            {
                //if (x.activeCondition == null || (x.activeCondition != null && x.activeCondition.Check(null)))
                //    activedTroopType.Add(x);
            });
        }

        /// <summary>
        /// 场景准备时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            //x *= 2;
            //y *= 2;

            base.OnScenarioPrepare(scenario);
            isComplate = true;

            //innerSlot = new int[InsideSlot];
            if (durability <= 0)
                durability = DurabilityLimit;

            // 地格占用
            OccupyCellList = new List<Cell>();
            scenario.Map.GetSpiral(x, y, BuildingType.radius, OccupyCellList);
            foreach (Cell cell in OccupyCellList)
                cell.building = this;
            CenterCell = OccupyCellList[0];

            // 效果范围
            effectCells = new System.Collections.Generic.List<Cell>();
            scenario.Map.GetDirectSpiral(CenterCell, BuildingType.radius + 1, BuildingType.radius + BuildingType.atkRange, effectCells);

            for (int i = 0; i < areaCellList.Count; i++)
            {
                Cell cell = areaCellList[i];
                if (cell.HasGridState(Sango.Render.MapGrid.GridState.Defence))
                    defenceCellList.Add(cell);

                if (cell.IsInterior)
                {
                    if (BelongCity != null)
                        BelongCity.interiorCellList.Add(cell);
                    else
                        interiorCellList.Add(cell);
                }
            }

            captiveList.RemoveAll(x => x.CurrentCity != this);
            foreach (Person person in captiveList)
            {
                if (person.BelongForce != null)
                    person.BelongForce.BeCaptiveList.Add(person);
            }
        }

        /// <summary>
        /// 准备渲染时的处理
        /// </summary>
        public override void OnPrepareRender()
        {
            Render = new CityRender(this);
        }

        /// <summary>
        /// 初始化城市
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public override void Init(Scenario scenario)
        {
            base.Init(scenario);

            // 空闲人员判断
            freePersons.Clear();
            allPersons.ForEach(person =>
            {
                if (!person.ActionOver && person.IsFree)
                    freePersons.Add(person);
            });

            if (BelongForce != null)
            {
                BelongForce.CityBaseCount++;
                BelongForce.CityList.Add(this);
                if (IsCity())
                {
                    BelongForce.CityCount++;
                }
            }

            if (IsPort())
                BelongCity.portList.Add((Port)this);
            else if (IsGate())
                BelongCity.gateList.Add((Gate)this);
            if (BuildingType.Id == 1)
            {
                UpdateActiveTroopTypes();
                UpdateFightPower();
            }

            for (int i = 0; i < interiorCellList.Count; i++)
            {
                Cell c = interiorCellList[i];
                c.CreateInteriorModel();
            }

            //计算最大士气
            CalculateMaxMorale();
            CalculateHarvest();
        }

        /// <summary>
        /// 变为野生状态
        /// </summary>
        public void LeaveToWild()
        {
            Leader = null;
            BelongCorps = null;
            BelongForce = null;
            Render?.UpdateRender();
        }

        /// <summary>
        /// 更新战斗力
        /// </summary>
        public void UpdateFightPower()
        {
            fightPower = 1000 + troops;
            fightPower += UnityEngine.Mathf.Min(fightPower, allPersons.Count * 5000);
            isUpdatedFightPower = true;
            virtualFightPower = FightPower;
            //ForeachNeighborCities(x =>
            //{
            //    if (x.IsSameForce(this))
            //    {
            //        if (!x.isUpdatedFightPower)
            //        {
            //            x.UpdateFightPower();
            //            x.isUpdatedFightPower = true;
            //        }
            //        virtualFightPower += x.FightPower / 3;
            //    }
            //});
        }

        /// <summary>
        /// 计算收获
        /// </summary>
        public void CalculateHarvest()
        {
            GameEvent.OnCityCalculateHarvest?.Invoke(this);
        }

        /// <summary>
        /// 遍历相邻城市
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForeachNeighborCities(Action<City> action)
        {
            for (int i = 0; i < NeighborList.Count; i++)
            {
                City c = NeighborList[i];
                if (c == null) continue;
                action(c);
            }
        }

        /// <summary>
        /// 检查是否为边界城市
        /// </summary>
        /// <param name="city">要检查的城市</param>
        /// <returns>是否为边界城市</returns>
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

        /// <summary>
        /// 检查边界线
        /// </summary>
        /// <param name="city">要检查的城市</param>
        /// <param name="len">当前长度</param>
        /// <returns>边界线长度</returns>
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

        /// <summary>
        /// 季度粮食收入
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnSeasonStart(Scenario scenario)
        {
            //if (BelongCorps == null)
            //    return true;

            //            int harvest = GameRandom.Random(totalGainFood, 0.05f);
            //            Render?.ShowInfo(harvest, (int)InfoType.Food);
            //            food += harvest;
            //#if SANGO_DEBUG
            //            Sango.Log.Info($"城市：{Name}, 收获粮食：{harvest}, 现有粮食: {food}");
            //#endif

            GameEvent.OnCitySeasonStart?.Invoke(this, scenario);


            return base.OnSeasonStart(scenario);
        }


        /// <summary>
        /// 月度金钱收入
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnMonthStart(Scenario scenario)
        {
            int pop = 0;
            int troopPop = 0;
            if (scenario.Variables.populationEnable)
            {
                // 计算人口增长
                pop = GameRandom.Random((int)(population * population_increase_factor * extraPopulationFactor), 0.1f);
                // 确保人口不超过上限
                int newPopulation = population + pop;
                if (newPopulation > PopulationLimit)
                {
                    pop = PopulationLimit - population;
                    newPopulation = PopulationLimit;
                }
                population = newPopulation;

                // 计算兵役人口增长
                troopPop = (int)(pop * scenario.Variables.baseTroopPopulationRatio);
                int newTroopPopulation = troopPopulation + troopPop;
                // 确保兵役人口不超过最大限制
                if (newTroopPopulation > MaxTroopPopulation)
                {
                    newTroopPopulation = MaxTroopPopulation;
                }
                troopPopulation = newTroopPopulation;
            }

            if (BelongCorps == null)
                return true;

            if (Render != null)
                Render.UpdateRender();

            GameEvent.OnCityMonthStart?.Invoke(this, scenario);

            int cost = -GoldCost(scenario);
            if (cost < 0)
            {
                AddGold(cost);
                Render?.ShowInfo(cost, (int)InfoType.Gold);

#if SANGO_DEBUG
                Sango.Log.Info($"城市：{Name},  支出：{cost}, 现有资金: {gold}");
#endif
            }

            return base.OnMonthStart(scenario);
        }

        /// <summary>
        /// 月度金钱收入
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnYearStart(Scenario scenario)
        {
            // 每年1月清理黑市
            allBuildings.ForEach(building =>
            {
                if (building.BuildingType.kind == (int)BuildingKindType.BlackMarket)
                    building.OnFall(null);
            });

            return base.OnYearStart(scenario);
        }

        /// <summary>
        /// 每日开始时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnDayStart(Scenario scenario)
        {
            return base.OnDayStart(scenario);
        }

        /// <summary>
        /// 建筑完成时的处理
        /// </summary>
        /// <param name="building">完成的建筑</param>
        /// <param name="builder">建造者</param>
        public virtual void OnBuildingComplete(Building building, SangoObjectList<Person> builder)
        {
            this.CalculateHarvest();
        }

        /// <summary>
        /// 建筑升级完成时的处理
        /// </summary>
        /// <param name="building">升级的建筑</param>
        /// <param name="builder">升级者</param>
        public virtual void OnBuildingUpgradeComplete(Building building, SangoObjectList<Person> builder)
        {
            this.CalculateHarvest();
        }

        /// <summary>
        /// 建筑创建时的处理
        /// </summary>
        /// <param name="building">创建的建筑</param>
        public void OnBuildingCreate(Building building)
        {
            allBuildings.Add(building);
            int buildingKind = building.BuildingType.kind;
            int count;
            if (buildingCountMap.TryGetValue(buildingKind, out count))
                buildingCountMap[buildingKind] = count + 1;
            else
                buildingCountMap.Add(buildingKind, 1);
        }

        /// <summary>
        /// 建筑销毁时的处理
        /// </summary>
        /// <param name="building">销毁的建筑</param>
        public void OnBuildingDestroy(Building building)
        {
            allBuildings.Remove(building);
            this.CalculateHarvest();
            int buildingKind = building.BuildingType.kind;
            int count;
            if (buildingCountMap.TryGetValue(buildingKind, out count))
                buildingCountMap[buildingKind] = count - 1;
        }

        /// <summary>
        /// 获取一个未行动的建筑
        /// </summary>
        /// <param name="buildingKindType">建筑类型</param>
        /// <returns>未行动的建筑</returns>
        public Building GetCommandBuilding(BuildingKindType buildingKindType)
        {
            Building rs = null;
            int maxLv = 0;
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building building = allBuildings[i];
                if (!building.ActionOver && building.BuildingType.kind == (int)buildingKindType)
                {
                    if (building.BuildingType.level > maxLv)
                    {
                        rs = building;
                        maxLv = building.BuildingType.level;
                    }
                }
            }
            return rs;
        }

        /// <summary>
        /// 势力回合开始时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnForceTurnStart(Scenario scenario)
        {
            jobCounter.Clear();
            AIPrepared = false;
            AIFinished = false;
            ActionOver = false;
            boderLineChecked = false;
            CalculateHarvest();
            UpdateFightPower();
            JobHealingTroop();

            // 空闲人员判断
            freePersons.Clear();
            allPersons.ForEach(person =>
            {
                if (!person.ActionOver && person.IsFree)
                    freePersons.Add(person);
            });

            // 粮食消耗
            CostFood(scenario);

            // 耐久自修复
            if (durability < DurabilityLimit)
            {
                durability += Leader?.BaseBuildAbility * 2 + 50 ?? 50;
                if (durability > DurabilityLimit)
                    durability = DurabilityLimit;
            }

            if (Render != null)
                Render.UpdateRender();

            // 俘虏羁押天数累积
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                if (person != null)
                    person.missionCounter++;
            }

            GameEvent.OnCityTurnStart?.Invoke(this, scenario);

            return base.OnForceTurnStart(scenario);
        }

        /// <summary>
        /// 势力回合结束时的处理
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否成功</returns>
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            CurActiveTroop = null;
            isUpdatedFightPower = false;

            // 计算俘虏越狱
            for (int i = captiveList.Count - 1; i >= 0; i--)
            {
                Person person = captiveList[i];
                if (person == null) continue;
                if (GameRandom.Chance(GameFormula.Instance.PersonEscapeProbablility_InCity(person, this, scenario), 10000))
                {
                    person.Escape(EscapeType.Escape);
#if SANGO_DEBUG
                    Sango.Log.Info($"{person.Name}逃跑!");
#endif
                    //GameEvent.OnPersonEscape?.Invoke(person, this);
                }
            }

            GameEvent.OnCityTurnEnd?.Invoke(this, scenario);

            if (Leader == null || Leader.BelongCity != this)
                UpdateNewLeader();

            return base.OnForceTurnEnd(scenario);
        }

        /// <summary>
        /// 计算粮食消耗
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>粮食消耗数量</returns>
        public int FoodCost(Scenario scenario)
        {
            int foodCost = 0;
            // 军队粮食消耗
            foodCost += (int)System.Math.Ceiling(scenario.Variables.baseFoodCostInCity * (troops + woundedTroops));
            // 人口粮食消耗
            if (scenario.Variables.populationEnable)
            {
                foodCost += (int)System.Math.Ceiling(population * scenario.Variables.populationFoodCostFactor);
            }
            return foodCost;
        }

        /// <summary>
        /// 消耗粮食
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public void CostFood(Scenario scenario)
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

        /// <summary>
        /// 计算金钱消耗
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>金钱消耗数量</returns>
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

            // 计算俘虏的消耗
            for (int i = 0; i < captiveList.Count; i++)
            {
                if (captiveList[i] != null)
                    goldCost += 100;
            }

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

        /// <summary>
        /// 改变耐久度
        /// </summary>
        /// <param name="num">改变的耐久度值</param>
        /// <param name="atk">攻击者</param>
        /// <param name="showDamage">是否显示伤害</param>
        /// <returns>是否成功</returns>
        public override bool ChangeDurability(int num, SangoObject atk, bool showDamage = true)
        {
            bool rs = base.ChangeDurability(num, atk, showDamage);
            if (rs)
            {
                durability = 1500;
            }

            if (Render != null)
                Render.UpdateRender();

            return rs;
        }

        /// <summary>
        /// 获取最近的己方城市
        /// </summary>
        /// <returns>最近的己方城市</returns>
        public City GetNearnestForceCity()
        {
            City checkCity = BelongCity;
            if (checkCity != null)
            {
                if (checkCity.BelongForce == BelongForce)
                    return checkCity;

                for (int i = 0; i < checkCity.NeighborList.Count; i++)
                {
                    City city = checkCity.NeighborList[i];
                    if (city.BelongForce == BelongForce)
                        return city;
                }
            }
            else
            {
                for (int i = 0; i < NeighborList.Count; i++)
                {
                    City city = NeighborList[i];
                    if (city.IsSameForce(this))
                        return city;
                }
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

        /// <summary>
        /// 改变所属军团
        /// </summary>
        /// <param name="other">新的军团</param>
        /// <returns>原来的军团</returns>
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

        /// <summary>
        /// 改变军团后,需要用此方法更新所属信息
        /// </summary>
        public void UpdateCorps()
        {
            allPersons.ForEach(person =>
            {
                person.ChangeCorps(BelongCorps);
            });
            allBuildings.ForEach(person =>
            {
                person.ChangeCorps(BelongCorps);
            });
        }

        /// <summary>
        /// 改变兵力
        /// </summary>
        /// <param name="num">改变的兵力值</param>
        /// <param name="atk">攻击者</param>
        /// <param name="showDamage">是否显示伤害</param>
        /// <returns>是否还有兵力</returns>
        public bool ChangeTroops(int num, SangoObject atk, bool showDamage = true)
        {
            // 白城直接占领
            if (this.BelongForce == null)
                return false;

            if (showDamage)
                Render?.ShowInfo(num, (int)InfoType.Troop);

            troops = troops + num;
            if (troops < 0)
                troops = 0;

            if (Render != null)
            {
                Render.UpdateRender();
            }
            return troops > 0;
        }

        /// <summary>
        /// 城市被攻占时的处理
        /// </summary>
        /// <param name="atker">攻击者</param>
        public override void OnFall(SangoObject atker)
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables scenarioVariables = Scenario.Cur.Variables;

            Troop atk = atker as Troop;
            SkillInstance skillInstance = null;
            if (atker.ObjectType == SangoObjectType.SkillInstance)
            {
                skillInstance = (atker as SkillInstance);
                atk = skillInstance.master;
            }
            else
            {
                atk = atker as Troop;
            }

            if (atk == null) return;

            Force lastBelongForce = BelongForce;
            Corps lastBelongCorps = BelongCorps;
            freePersons.Clear();

            // 清理火
            for (int i = 0; i < OccupyCellList.Count; ++i)
            {
                Cell cell = OccupyCellList[i];
                if (cell.fire != null)
                {
                    cell.fire.Clear();
                    cell.fire = null;
                }
            }

            //this.captiveList.Clear();

            // 白城
            if (BelongCorps == null)
            {
                ChangeCorps(atk.BelongCorps);
                Leader = atk.Leader;
                atk.EnterCity(this);
                Render?.UpdateRender();
                CalculateHarvest();
                GameEvent.OnCityFall?.Invoke(this, lastBelongForce, atk);
                return;
            }

            BelongForce.CityBaseCount--;
            if (IsCity()) BelongForce.CityCount--;
            BelongForce.CityList.Remove(this);

            // 确认一个撤退城市
            City escapeCity = null;
            if (BelongForce.CityCount != 0 || !IsCity())
            {
                if (this == BelongForce.CapitalCity)
                    escapeCity = GetNearnestForceCity();
                else
                    escapeCity = BelongForce.CapitalCity;
            }

#if SANGO_DEBUG
            if (IsPort() || IsGate())
            {
                if (escapeCity == null)
                {
                    Debug.LogError("为啥 escapeCity == null");
                }
            }
#endif

            // 基础抓捕率
            int cacaptureChangce = escapeCity != null ? scenarioVariables.captureChangceWhenCityFall : scenarioVariables.captureChangceWhenLastCityFall;

            // 处理俘虏
            List<Person> temp_captive_list = new List<Person>();

            // 必须优先处理队伍
            if (escapeCity == null)
            {
                // 灭亡后,队伍要清除
                scenario.troopsSet.ForEach((troop) =>
                {
                    if (troop.IsAlive && troop.BelongForce == this.BelongForce)
                        troop.Clear();
                });

                // 清理港口,关卡
                for (int i = 0; i < scenario.citySet.Count; ++i)
                {
                    var c = scenario.citySet[i];
                    if (c != null && c.IsAlive && c.BelongForce == BelongForce)
                    {
                        if (c.IsGate() || c.IsPort())
                        {
                            c.allPersons.ForEach(p =>
                             {
                                 p.ClearMission();
                                 p.LeaveToWild();
                             });
                            c.allPersons.Clear();
                            c.captiveList.ForEach(p =>
                            {
                                p.Escape(EscapeType.Escape);
                            });
                            c.LeaveToWild();
                        }
                    }
                }
            }

            // 处理缓存队伍信息
            allAttackTroops.Clear();
            allTroops.Clear();

            for (int i = allPersons.Count - 1; i >= 0; --i)
            {
                Person person = allPersons[i];
                person.ClearMission();
                if (escapeCity != null)
                {
                    RemovePerson(person);
                    person.ChangeBelongCity(escapeCity);
                    escapeCity.AddPerson(person);
                    if (person.BelongTroop == null && person.CurrentCity == this && person != person.BelongForce.Governor && GameRandom.Chance(cacaptureChangce))
                    {
                        temp_captive_list.Add(person);
                    }
                    else
                    {
                        if (person.BelongTroop == null)
                            person.SetMission(MissionType.PersonReturn, person.BelongCity);
                    }
                }
                else
                {
                    // 最后一城
                    person.LeaveToWild();
                    temp_captive_list.Add(person);
                }
            }

            //处理建筑
            for (int i = allBuildings.Count - 1; i >= 0; i--)
            {
                Building building = allBuildings[i];
                if (building.isComplate && GameRandom.Chance(30))
                {
                    building.ChangeCorps(atk.BelongCorps);
                    if (building.Workers != null)
                        building.Workers.ForEach(x => x.workingBuilding = null);
                    building.Builder?.Clear();
                    building.Workers?.Clear();
                }
                else
                {
                    building.OnFall(atk);
                }
            }
            Force destroyedForce = null;
            if (escapeCity == null)
            {
                destroyedForce = BelongForce;
                BelongCorps.IsAlive = false;
#if SANGO_DEBUG
                Sango.Log.Info($"{BelongForce.Name} 灭亡!!!");
#endif
                BelongForce.IsAlive = false;
                BelongForce.BeCaptiveList.ForEach(x =>
                {
                    x.BelongForce = null;
                    x.BelongCorps = null;
                });
                BelongForce.BeCaptiveList.Clear();

                // 势力灭亡事件
                GameEvent.OnForceFall?.Invoke(BelongForce, this, atk);

                bool allInOne = true;
                scenario.citySet.ForEach(x =>
                {
                    if (x != this && x.IsCity() && x.BelongForce != atk.BelongForce)
                        allInOne = false;
                });

                if (allInOne)
                {
                    Sango.Log.Info($"{Scenario.Cur.GetDateStr()} --> {atk.BelongForce.Name} 统一!!!!!!!!!!!!!!");
                    scenario.OnGamePause();
                }
            }

            ChangeCorps(atk.BelongCorps);
            atk.BelongForce.CityBaseCount++;
            if (IsCity())
            {
                atk.BelongForce.CityCount++;
            }
            atk.BelongForce.CityList.Add(this);
            lastBelongCorps.UpdateWhenCityChange();
            atk.BelongCorps.UpdateWhenCityChange();

            // 处理库存和钱粮,兵力
            food = food * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepFoodFactor) * 10 + 10) / 100;
            gold = gold * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepGoldFactor) * 10 + 10) / 100;
            troops = troops * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepTroopsFactor) * 10 + 10) / 100;
            itemStore.Split((100 - (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepItemFactor) * 10 + 10)) / 100);
            agriculture = agriculture * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepAgriculture) * 10 + 10) / 100;
            commerce = agriculture * (GameRandom.RandomWeightIndex(scenarioVariables.cityFallCanKeepCommerce) * 10 + 10) / 100;

            // 解救俘虏, 一定是在势力更改后解救
            for (int i = this.captiveList.Count - 1; i >= 0; i--)
            {
                Person person = this.captiveList[i];
                if (person != null && person.IsSameForce(atk))
                {
                    RemoveCaptive(person);
                    person.state = (int)PersonStateType.Normal;
                    person.ChangeBelongCity(this);
                    AddPerson(person);
                }
            }

            if (skillInstance != null && !skillInstance.IsRange())
            {
                Leader = atk.Leader;
                atk.EnterCity(this);
            }

            Render?.UpdateRender();

            CalculateHarvest();

            GameEvent.OnCityFall?.Invoke(this, lastBelongForce, atk);

            if (atk.BelongCorps.IsPlayer)
            {
                RenderEvent.Instance.Add(new Render.CityFallCompleteEvent()
                {
                    city = this
                });
            }
           
            CityRecruitPersonWhenCityFallEvent te = RenderEvent.Instance.Create<CityRecruitPersonWhenCityFallEvent>();
            te.Init(temp_captive_list, this, escapeCity, atk, escapeCity == null ? (int)PersonRecruitType.OnForceFall : (int)PersonRecruitType.OnCityFall);
            RenderEvent.Instance.Add(te);


            if (escapeCity == null && destroyedForce != null)
            {
                RenderEvent.Instance.Add(new Render.ForceFallCompleteEvent()
                {
                    force = destroyedForce
                });
            }


        }

        /// <summary>
        /// 获取城市之间的距离
        /// </summary>
        /// <param name="other">目标城市</param>
        /// <returns>城市之间的距离</returns>
        public int Distance(City other)
        {
            if (this == other)
                return 0;

            if (BelongCity != null)
            {
                // 隶属范围内,需要1回合
                if (BelongCity == other) return 1;
                return BelongCity.Distance(other);
            }

            if (other.BelongCity != null)
            {
                // 隶属范围内,需要1回合
                if (other.BelongCity == this) return 1;
                other = other.BelongCity;
            }

            return Scenario.Cur.GetCityDistance(this, other);
        }

        /// <summary>
        /// 武将返回城市时的处理
        /// </summary>
        /// <param name="person">返回的武将</param>
        public void OnPersonReturnCity(Person person)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"[{person.BelongForce.Name}]{person.Name}回到[{BelongForce.Name}]<{Name}>");
#endif
            GameEvent.OnPersonChangeBelongCity?.Invoke(person, person.BelongCity, this);
        }

        /// <summary>
        /// 武将转换结束时的处理
        /// </summary>
        /// <param name="person">转换的武将</param>
        public void OnPersonTransformEnd(Person person, City from)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"[{person.BelongForce.Name}]{person.Name}到达[{BelongForce.Name}]<{Name}>");
#endif
            GameEvent.OnPersonChangeBelongCity?.Invoke(person, from, this);
        }

        /// <summary>
        /// 检查工作成本
        /// </summary>
        /// <param name="cityJobType">城市工作类型</param>
        /// <returns>是否有足够的资金</returns>
        public bool CheckJobCost(CityJobType cityJobType)
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)cityJobType;
            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, null, overrideData);
            goldNeed = overrideData.ValueAndRecycle;
            return gold >= goldNeed;
        }

        /// <summary>
        /// 获取工作成本
        /// </summary>
        /// <param name="cityJobType">城市工作类型</param>
        /// <returns>工作成本</returns>
        public int GetJobCost(CityJobType cityJobType)
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)cityJobType;
            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, null, overrideData);
            goldNeed = overrideData.ValueAndRecycle;
            return goldNeed;
        }


        /// <summary>
        /// 建造建筑
        /// </summary>
        /// <param name="buildCenter">建造中心</param>
        /// <param name="builder">建造者部队</param>
        /// <param name="buildingType">建筑类型</param>
        /// <returns>建造的建筑</returns>
        public Building BuildBuilding(Cell buildCenter, Troop builder, BuildingType buildingType)
        {
            Building building = new Building();
            building.BelongForce = BelongForce;
            building.BelongCorps = BelongCorps;
            building.BelongCity = this;
            building.BuildingType = buildingType;
            building.x = buildCenter.x;
            building.y = buildCenter.y;
            building.rot = GameRandom.Range(0, 10) * 90 * Mathf.Deg2Rad;

            building.durability = 0;

            Scenario scenario = Scenario.Cur;

            // TODO: 获取高度
            // ------
            scenario.Add(building);
            building.Init(scenario);
            building.Workers = null;
            building.isComplate = false;
            building.durability = 1;
            building.ChangeDurability(GameUtility.Method_TroopBuildAbility(builder), null);

            builder.gold -= buildingType.cost;

#if SANGO_DEBUG
            Sango.Log.Info($"[{BelongForce.Name}]在<{Name}>由{builder.Name}开始修建: {building.Name}");
#endif
            building.Render.UpdateRender();
            return building;
        }

        /// <summary>
        /// 获取空的内城cell
        /// </summary>
        /// <returns>空的内城cell</returns>
        public Cell GetEmptyInteriorCell()
        {
            for (int i = 0; i < interiorCellList.Count; ++i)
            {
                Cell c = interiorCellList[i];
                if (c.building == null)
                    return c;
            }
            return null;
        }

        /// <summary>
        /// 初始化工作特性
        /// </summary>
        /// <param name="people">人员数组</param>
        public void InitJobFeature(Person[] people)
        {
            GameUtility.InitJobFeature(people, this);
        }

        /// <summary>
        /// 初始化工作特性
        /// </summary>
        /// <param name="person">人员</param>
        public void InitJobFeature(Person person)
        {
            GameUtility.InitJobFeature(person, this);
        }

        /// <summary>
        /// 清除工作特性
        /// </summary>
        public void ClearJobFeature()
        {
            GameUtility.ClearJobFeature();
        }

        /// <summary>
        /// 建造内城建筑
        /// </summary>
        /// <param name="buildCenter">建造中心</param>
        /// <param name="builders">建造者</param>
        /// <param name="buildingType">建筑类型</param>
        /// <param name="buildCount">建造所需回合数</param>
        /// <returns>建造的建筑</returns>
        public Building JobBuildBuilding(Cell buildCenter, Person[] builders, BuildingType buildingType, int buildCount)
        {
            Building building = new Building();
            building.BelongForce = BelongForce;
            building.BelongCorps = BelongCorps;
            building.BelongCity = this;
            building.BuildingType = buildingType;
            building.x = buildCenter.x;
            building.y = buildCenter.y;
            building.durability = 0;
            building.rot = GameRandom.Range(0, 10) * 90 * Mathf.Deg2Rad;

            Scenario scenario = Scenario.Cur;
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            // TODO: 获取高度
            // ------
            scenario.Add(building);
            building.Init(scenario);
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();
            foreach (Person person in builders)
            {
                if (person == null) continue;
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
                person.SetMission(MissionType.PersonBuild, building, 0);
                person.ActionOver = true;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
            }

            building.Builder = sangoObjectList;
            building.isComplate = false;
            building.durability = 1;
            building.LeftCounter = buildCount;
            gold -= buildingType.cost;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.Build));

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>由{stringBuilder}开始修建: {building.Name} 需耗时:{buildCount} 回合");
#endif
            building.Render.UpdateRender();
            return building;
        }

        /// <summary>
        /// 升级内城建筑
        /// </summary>
        /// <param name="building">要升级的建筑</param>
        /// <param name="builders">升级者</param>
        /// <param name="upgradeBuildingType">升级后的建筑类型</param>
        /// <param name="buildCount">升级所需回合数</param>
        /// <returns>升级后的建筑</returns>
        public Building JobUpgradeBuilding(Building building, Person[] builders, BuildingType upgradeBuildingType, int buildCount)
        {
            building.isUpgrading = true;
            building.durability = 1;

            Scenario scenario = Scenario.Cur;
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            // TODO: 获取高度
            // ------
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();
            foreach (Person person in builders)
            {
                if (person == null) continue;
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
                person.SetMission(MissionType.PersonBuild, building, 0);
                person.ActionOver = true;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
            }

            building.Builder = sangoObjectList;
            building.LeftCounter = buildCount;
            gold -= upgradeBuildingType.cost;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.UpgradeBuilding));

            building.Render?.UpdateRender();

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>由{stringBuilder}开始升级建筑: {building.Name} 需耗时: {buildCount}回合");
#endif
            return building;
        }

        /// <summary>
        /// 生产船
        /// </summary>
        /// <param name="personList">生产人员</param>
        /// <param name="itemType">船的物品类型</param>
        /// <param name="building">建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>生产信息数组</returns>
        public int[] JobCreateBoat(Person[] personList, ItemType itemType, Building building, bool isTest = false)
        {
            if (personList == null || personList.Length == 0 || itemType == null) return null;

            if (itemType.kind != (int)ItemKindType.Boat) return null;

            if (itemStore.TotalNumber >= StoreLimit) return null;

            Scenario scenario = Scenario.Cur;

            InitJobFeature(personList);
            int empty = StoreLimit - itemStore.TotalNumber;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateBoat;

            int goldNeed = JobType.GetJobCost(jobId) + itemType.cost;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return null;
            }

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseCreativeAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseCreativeAbility;
                }
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            int subValue = 0;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    subValue += maxPerson.BaseCreativeAbility;
                }
            }

            int turnCount = GameUtility.Method_CreateBoatCounter(maxValue, subValue, building.BuildingType.level);
            overrideData = Tools.OverrideData<int>.Create(turnCount);
            GameEvent.OnCityJobCounterResult?.Invoke(this, jobId, personList, overrideData);
            turnCount = overrideData.ValueAndRecycle;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            maxValue *= 5;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    maxValue += maxPerson.BaseCreativeAbility * 10;
                }
            }


            if (isTest)
            {
                int totalValue = GameUtility.Method_CreateItems(maxValue, building.BuildingType.level) / itemType.p1 / 3;
                overrideData = Tools.OverrideData<int>.Create(totalValue);
                GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
                totalValue = overrideData.ValueAndRecycle;
                ClearJobFeature();
                return new int[] { turnCount, totalValue };
            }

            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            building.ActionOver = true;
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
                person.ActionOver = true;
                person.SetMission(MissionType.PersonCreateBoat, itemType, turnCount, building.Id, maxValue);
            }
            //building.Workers = sangoObjectList;
            building.LeftCounter = turnCount;
            building.isWorking = true;
#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了舰船生产!开始生产{itemType.Name}, 所需回合:{turnCount}, 建筑:{building.Name}");
#endif
            ClearJobFeature();
            return null;
        }

        /// <summary>
        /// 生产兵装
        /// </summary>
        /// <param name="itemType">物品类型</param>
        /// <param name="buildingId">建筑ID</param>
        /// <param name="totalValue">总价值</param>
        /// <returns>是否成功</returns>
        public bool DoJobCreateBoat(ItemType itemType, int buildingId, int totalValue)
        {
            List<Person> people = new List<Person>();
            allPersons.ForEach(person =>
            {
                if (person.missionType == (int)MissionType.PersonCreateBoat)
                {
                    people.Add(person);
                }
            });
            if (people.Count == 0) return false;

            InitJobFeature(people.ToArray());
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateBoat;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

            Building building = scenario.GetObject<Building>(buildingId);
            building.isWorking = false;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < people.Count; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.ClearMission();
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            totalValue = GameUtility.Method_CreateItems(totalValue, building.BuildingType.level) / itemType.p1 / 3;

            Person[] personList = people.ToArray();

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.Value;


            overrideData.Value = techniquePointGain;
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.Value;

            overrideData.Recycle();

            int empty = StoreLimit - itemStore.TotalNumber;
            totalValue = Math.Min(empty, totalValue);
            int exsistNumber = itemStore.Add(itemType.storeKind, totalValue);

            BelongForce.GainTechniquePoint(techniquePointGain);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了船只生产!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}, 建筑:{building.Name}");
#endif

            Render?.ShowInfo(totalValue, itemType.Id + 1);
            ClearJobFeature();
            return true;
        }



        /// <summary>
        /// 生产器械
        /// </summary>
        /// <param name="personList">生产人员</param>
        /// <param name="itemType">器械的物品类型</param>
        /// <param name="building">建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>生产信息数组</returns>
        public int[] JobCreateMachine(Person[] personList, ItemType itemType, Building building, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return null;
            if (itemType == null) return null;

            if (itemType.kind != (int)ItemKindType.Machine) return null;

            if (itemStore.TotalNumber >= StoreLimit) return null;

            Scenario scenario = Scenario.Cur;
            int empty = StoreLimit - itemStore.TotalNumber;

            InitJobFeature(personList);

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateMachine;

            int goldNeed = JobType.GetJobCost(jobId) + itemType.cost;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();

                return null;
            }

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
            int lastTroops = troops;
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseCreativeAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseCreativeAbility;
                }

#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            int subValue = 0;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    subValue += maxPerson.BaseCreativeAbility;
                }
            }


            int turnCount = GameUtility.Method_CreateMachineCounter(maxValue, subValue, building.BuildingType.level);
            overrideData = Tools.OverrideData<int>.Create(turnCount);
            GameEvent.OnCityJobCounterResult?.Invoke(this, jobId, personList, overrideData);
            turnCount = overrideData.ValueAndRecycle;

            // 最高属性武将获得100%加成,其余两个获取50%加成
            maxValue *= 5;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    maxValue += maxPerson.BaseCreativeAbility * 10;
                }
            }

            if (isTest)
            {
                int totalValue = GameUtility.Method_CreateItems(maxValue, building.BuildingType.level) / itemType.p1 / 3;
                overrideData = Tools.OverrideData<int>.Create(totalValue);
                GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
                totalValue = overrideData.ValueAndRecycle;
                ClearJobFeature();
                return new int[] { turnCount, totalValue };
            }

            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            building.ActionOver = true;
            SangoObjectList<Person> sangoObjectList = new SangoObjectList<Person>();

            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                freePersons.Remove(person);
                sangoObjectList.Add(person);
                person.ActionOver = true;
                person.SetMission(MissionType.PersonCreateMachine, itemType, turnCount, building.Id, maxValue);
            }
            //building.Workers = sangoObjectList;
            building.LeftCounter = turnCount;
            building.isWorking = true;

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了器械生产!开始生产{itemType.Name}, 所需回合:{turnCount}, 建筑:{building.Name}");
#endif
            Render?.UpdateRender();
            ClearJobFeature();
            return null;
        }

        /// <summary>
        /// 执行器械生产任务
        /// </summary>
        /// <param name="itemType">物品类型</param>
        /// <param name="buildingId">建筑ID</param>
        /// <param name="totalValue">总价值</param>
        /// <returns>是否成功</returns>
        public bool DoJobCreateMachine(ItemType itemType, int buildingId, int totalValue)
        {
            List<Person> people = new List<Person>();
            allPersons.ForEach(person =>
            {
                if (person.missionType == (int)MissionType.PersonCreateMachine)
                {
                    people.Add(person);
                }
            });
            if (people.Count == 0) return false;

            InitJobFeature(people.ToArray());
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.CreateMachine;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);
            Building building = scenario.GetObject<Building>(buildingId);
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < people.Count; i++)
            {
                Person person = people[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.ClearMission();
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
            }

            totalValue = GameUtility.Method_CreateItems(totalValue, building.BuildingType.level) / itemType.p1 / 3;
            building.isWorking = false;
            Person[] personList = people.ToArray();

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.Value;

            overrideData.Value = techniquePointGain;
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.Value;

            overrideData.Recycle();

            int empty = StoreLimit - itemStore.TotalNumber;
            totalValue = Math.Min(empty, totalValue);
            int exsistNumber = itemStore.Add(itemType.storeKind, totalValue);

            BelongForce.GainTechniquePoint(techniquePointGain);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了器械生产!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}, 建筑:{building.Name}");
#endif

            Render?.ShowInfo(totalValue, itemType.Id + 1);
            ClearJobFeature();
            return true;
        }

        /// <summary>
        /// 农业
        /// </summary>
        /// <param name="personList">农业人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>农业值增加量</returns>
        public int JobFarming(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;
            if (agriculture >= AgricultureLimit) return 0;
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Farming;

            InitJobFeature(personList);

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                totalValue += person.BaseAgricultureAbility;
            }

            totalValue = GameUtility.Method_FarmingAbility(totalValue);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            agriculture += totalValue;
            if (agriculture > AgricultureLimit)
                agriculture = AgricultureLimit;

            Render?.ShowInfo(totalValue, (int)InfoType.Food);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了开垦!农业值达到了:{agriculture}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 开发
        /// </summary>
        /// <param name="personList">开发人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>商业值增加量</returns>
        public int JobDevelop(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            if (commerce >= CommerceLimit) return 0;
            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Develop;

            InitJobFeature(personList);
            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                totalValue += person.BaseCommerceAbility;
            }

            totalValue = GameUtility.Method_DevelopAbility(totalValue);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            commerce += totalValue;
            if (commerce > CommerceLimit)
                commerce = CommerceLimit;

            Render?.ShowInfo(totalValue, (int)InfoType.Gold);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了开发!商业值达到了:{commerce}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 治安巡视
        /// </summary>
        /// <param name="personList">巡视人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>治安值增加量</returns>
        public int JobInspection(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            if (security >= 100) return 0;

            Scenario scenario = Scenario.Cur;

            InitJobFeature(personList);
            //int barracksLv = GetIntriorBuildingComplateMaxLevel((int)BuildingKindType.PatrolBureau);
            //if (barracksLv == 0) return 0;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Inspection;

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;


            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                totalValue += person.BaseSecurityAbility;
            }

            // 最终数值
            totalValue = GameUtility.Method_SecurityAbility(totalValue, 3);

            // 
            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            security += totalValue;
            if (security > 100)
                security = 100;
            AddJobCounter(jobId);
            Render?.ShowInfo(totalValue, (int)InfoType.Security);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了巡视!治安提升到了:{security}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 训练
        /// </summary>
        /// <param name="personList">训练人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>士气增加量</returns>
        public int JobTrainTroops(Person[] personList, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            if (morale >= MaxMorale) return 0;
            Scenario scenario = Scenario.Cur;

            InitJobFeature(personList);

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.TrainTroops;

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            int subValue = 0;
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseTrainTroopAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseTrainTroopAbility;
                }
            }

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    subValue += maxPerson.BaseTrainTroopAbility;
                }
                else
                {
                    totalValue += maxPerson.BaseTrainTroopAbility;
                }
            }

            // 最终数值
            totalValue = GameUtility.Method_TrainTroops(totalValue, subValue);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;
            BelongForce.GainTechniquePoint(techniquePointGain);

            gold -= goldNeed;
            morale += totalValue;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));
            if (morale > MaxMorale)
                morale = MaxMorale;

            AddJobCounter(jobId);
            Render?.ShowInfo(totalValue, (int)InfoType.Morale);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了训练!士气提升到了:{morale}");
#endif
            ClearJobFeature();
            return totalValue;
        }


        /// <summary>
        /// 登庸武将
        /// </summary>
        /// <param name="person">执行登庸的武将</param>
        /// <param name="dest">目标武将</param>
        /// <returns>是否成功</returns>
        public bool JobRecruitPerson(Person person, Person dest)
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.RecruitPerson;
            int apCost = JobType.GetJobCostAP(jobId);

            freePersons.Remove(person);
            if (dest.CurrentCity == person.CurrentCity)
            {
                CityRecruitPersonEvent te = RenderEvent.Instance.Create<CityRecruitPersonEvent>();
                te.Init(person, dest);
                RenderEvent.Instance.AddFront(te);
                BelongCorps.ReduceActionPoint(apCost);
                return true;
            }
            else
            {
                person.SetMission(MissionType.PersonRecruitPerson, dest, 100, dest.CurrentCity.Id);
                person.ActionOver = true;
                BelongCorps.ReduceActionPoint(apCost);
                return false;
            }
        }

        /// <summary>
        /// 褒奖武将
        /// </summary>
        /// <param name="persons">要褒奖的武将数组</param>
        /// <returns>是否成功</returns>
        public bool JobRewardPersons(Person[] persons)
        {
            if (!GameUtility.IsValidPersonArray(persons)) return true;


            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Reward;
            int apCost = JobType.GetJobCostAP(jobId);
            int goldCost = JobType.GetJobCost(jobId);
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            int totalApCost = 0;
            int totalGoldCost = 0;
            for (int i = 0; i < persons.Length; ++i)
            {
                Person person = persons[i];
                if (person == null) continue;

                totalApCost += apCost;
                totalGoldCost += goldCost;
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.loyalty += 10;
            }
            gold -= totalGoldCost;
            BelongCorps.ReduceActionPoint(totalApCost);
            BelongCorps.AddJobCounter(jobId);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>使用资金对{stringBuilder}进行了褒赏!!");
#endif
            return true;
        }


        /// <summary>
        /// 褒奖单个武将
        /// </summary>
        /// <param name="person">要褒奖的武将</param>
        /// <returns>是否成功</returns>
        public bool JobRewardPerson(Person person)
        {
            if (person == null)
                return true;

            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Reward;
            int apCost = JobType.GetJobCostAP(jobId);
            int goldCost = JobType.GetJobCost(jobId);
#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif

#if SANGO_DEBUG
            stringBuilder.Append(person.Name);
            stringBuilder.Append(",");
            int lastLoyalty = person.loyalty;
#endif
            person.loyalty += GameRandom.Range(7, 18);
            gold -= goldCost;
            BelongCorps.ReduceActionPoint(apCost);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]在<{Name}>使用资金对{stringBuilder}进行了褒赏!! 忠诚从{lastLoyalty}提升到->{person.loyalty}");
#endif
            return true;
        }


        /// <summary>
        /// 搜索
        /// </summary>
        /// <param name="personList">搜索人员</param>
        /// <returns>是否成功</returns>
        public bool JobSearching(Person[] personList)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return false;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                CityPersonSearchingEvent te = RenderEvent.Instance.Create<CityPersonSearchingEvent>();
                te.Init(this, person);
                RenderEvent.Instance.Add(te);
            }
            return true;
        }

        /// <summary>
        /// 执行搜索任务
        /// </summary>
        /// <param name="person">执行搜索的武将</param>
        /// <param name="target">找到的目标武将</param>
        /// <returns>搜索结果：0表示找到人才，正数表示找到的金钱，-1表示什么也没找到</returns>
        public int DoJobSearching(Person person, out Person target)
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Searching;
            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);
            int apCost = JobType.GetJobCostAP(jobId);
            target = null;
            freePersons.Remove(person);
            InitJobFeature(person);

            BelongCorps.ReduceActionPoint(apCost);

            // 发现人才
            int probality = 20 + person.Politics * 3 / 5;
            if (invisiblePersons.Count > 0)
            {
                Tools.OverrideData<int> overrideData1 = Tools.OverrideData<int>.Create(probality);
                GameEvent.OnCityJobSearchingWild?.Invoke(this, jobId, person, overrideData1);
                probality = overrideData1.ValueAndRecycle;
                if (GameRandom.Chance(probality))
                {
                    target = invisiblePersons[GameRandom.Range(0, invisiblePersons.Count)];
                    target.state = (int)PersonStateType.Unemployed;

                    if (IsPlayer)
                    {
                        PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}发现人才{target.ColorName}。",
                            BelongForce, x, y);
                    }

#if SANGO_DEBUG
                    Sango.Log.Info($"@内政@[{BelongForce.Name}]<{Name}>的{person.Name}发现了人才->{target.Name}");
#endif
                    RemoveInvisiblePerson(target);
                    AddWildPerson(target);
                    person.merit += meritGain;
                    person.GainExp(meritGain);
                    person.ActionOver = true;
                    ClearJobFeature();
                    return 0;
                }
            }

            //TODO: 搜索道具
            //if (!person.ActionOver && GameRandom.Changce((int)(3 * ability_improve)))
            //{
            //    person.ActionOver = true;
            //    continue;
            //}

            // 搜索钱财
            probality = person.Politics * 3 / 5;
            if (!person.ActionOver && GameRandom.Chance(probality))
            {
                int findGold = GameRandom.Range(person.Politics * 2, person.Politics * 3);
                if (IsPlayer)
                {
                    PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}发现资金{findGold}。",
                        BelongForce, x, y);
                }
                AddGold(findGold);
                Render?.ShowInfo(findGold, (int)InfoType.Gold);

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.ActionOver = true;
                ClearJobFeature();
                return findGold;
            }

            //TODO: 触发事件

            // 什么也没找到
            if (!person.ActionOver)
            {
                if (IsPlayer)
                {
                    PlayerMessage.AddTextMessage($"{person.ColorName}在{ColorName}什么也没发现。",
                        BelongForce, x, y);
                }

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.ActionOver = true;
            }

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, new Person[] { person }, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
            ClearJobFeature();
            return -1;
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
            Sango.Log.Info($"@内政@[{BelongForce.Name}]<{Name}>进行了士兵治愈!共治愈到{rs}人, 当前士兵提升到了:{troops}");
#endif
            return true;
        }

        /// <summary>
        /// 招募士兵
        /// </summary>
        /// <param name="personList">招募人员</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>招募的士兵数量</returns>
        public int JobRecruitTroop(Person[] personList, bool isTest = false)
        {
            Building barracks = GetFreeBuilding((int)BuildingKindType.Barracks);
            if (barracks == null) return 0;
            return JobRecruitTroop(personList, barracks, isTest);
        }

        /// <summary>
        /// 招募士兵
        /// </summary>
        /// <param name="personList">招募人员</param>
        /// <param name="barracks">兵营建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>招募的士兵数量</returns>
        public int JobRecruitTroop(Person[] personList, Building barracks, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;

            // 城池满了不再招募
            if (TroopsIsFull) return 0;

            Scenario scenario = Scenario.Cur;
            InitJobFeature(personList);
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.RecruitTroops;

            int goldNeed = JobType.GetJobCost(jobId);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int meritGain = JobType.GetJobMeritGain(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

            int totalValue = 0;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                totalValue += person.BaseRecruitmentAbility;

            }

            totalValue = GameUtility.Method_RecruitTroops(totalValue, barracks.BuildingType.level);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;

            // 治安对征兵的影响
            totalValue = (int)(totalValue * (1f - Math.Max(0, (100 - security)) * variables.securityInfluenceRecruitTroops));

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            if (Scenario.Cur.Variables.populationEnable)
            {
                totalValue = Math.Min(totalValue, troopPopulation);
            }

            if (totalValue + troops > TroopsLimit)
                totalValue = TroopsLimit - troops;

            if (isTest)
            {

                ClearJobFeature();
                return totalValue;
            }

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
            int lastTroops = troops;
#endif

            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                person.merit += meritGain;
                person.GainExp(meritGain);
                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            if (Scenario.Cur.Variables.populationEnable)
            {
                troopPopulation -= totalValue;
                population -= totalValue;
            }

            //士气减少
            morale = (troops * morale + totalValue * 30) / (troops + totalValue);
            troops += totalValue;
            gold -= goldNeed;

            //治安减少
            security -= Math.Min(6, 4 * totalValue / 1000);
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            BelongForce.GainTechniquePoint(techniquePointGain);

            barracks.ActionOver = true;

            Render?.UpdateRender();
            Render?.ShowInfo(totalValue, (int)InfoType.Troop);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了招募!共招募到{troops - lastTroops}人, 当前士兵人数提升到了:{troops}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 生产兵装
        /// </summary>
        /// <param name="personList">生产人员</param>
        /// <param name="itemType">物品类型</param>
        /// <param name="building">生产建筑</param>
        /// <param name="isTest">是否为测试</param>
        /// <returns>生产的兵装数量</returns>
        public int JobCreateItems(Person[] personList, ItemType itemType, Building building, bool isTest = false)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return 0;
            if (itemStore.TotalNumber >= StoreLimit) return 0;

            Scenario scenario = Scenario.Cur;

            if (isTest && itemType == null)
            {
                building = GetFreeBuilding((int)BuildingKindType.BlacksmithShop);
                itemType = scenario.GetObject<ItemType>((int)ItemKindType.Weapon);
            }

            if (itemType == null) return 0;
            if (building == null) return 0;

            InitJobFeature(personList);

            int empty = StoreLimit - itemStore.TotalNumber;

            ScenarioVariables variables = scenario.Variables;
            int jobId = itemType.Id == 5 ? (int)CityJobType.CreateHorse : (int)CityJobType.CreateItems;

            int goldNeed = JobType.GetJobCost(jobId) + itemType.cost;

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(goldNeed);
            GameEvent.OnCityCheckJobCost?.Invoke(this, jobId, personList, overrideData);
            goldNeed = overrideData.ValueAndRecycle;

            if (gold < goldNeed)
            {
                ClearJobFeature();
                return 0;
            }

            int totalValue = 0;
            int maxValue = 0;
            Person maxPerson = null;
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person.BaseCreativeAbility > maxValue)
                {
                    maxPerson = person;
                    maxValue = person.BaseCreativeAbility;
                }
            }

            // 最高属性武将获得100%加成,其余两个获取50%加成
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;
                if (person != maxPerson)
                {
                    totalValue += maxPerson.BaseCreativeAbility * 5;
                }
                else
                {
                    totalValue += maxPerson.BaseCreativeAbility * 10;
                }
            }

            totalValue = GameUtility.Method_CreateItems(totalValue, building.BuildingType.level);

            overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.ValueAndRecycle;
            totalValue = Math.Min(empty, totalValue);

            if (isTest)
            {
                ClearJobFeature();
                return totalValue;
            }

            int meritGain = JobType.GetJobLimit(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);

                freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            int exsistNumber = itemStore.Add(itemType.Id, totalValue);

            overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;
            building.ActionOver = true;
            gold -= goldNeed;
            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            BelongForce.GainTechniquePoint(techniquePointGain);

            if (itemType.Id == 2)
                Render?.ShowInfo(totalValue, 1);
            else if (itemType.Id == 5)
                Render?.ShowInfo(totalValue, itemType.Id + 1);
            else
                Render?.ShowInfo(totalValue, itemType.Id);

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{BelongForce.Name}]{stringBuilder}对<{Name}>进行了生产兵装!共生产了{totalValue}{itemType.Name}, 当前数量:{exsistNumber}, 建筑:{building.Name}");
#endif
            ClearJobFeature();
            return totalValue;
        }

        /// <summary>
        /// 交易粮食
        /// </summary>
        /// <param name="personList">交易人员</param>
        /// <param name="goldNum">交易的金钱数量（正数为购买粮食，负数为出售粮食）</param>
        /// <returns>是否成功</returns>
        public bool JobTradeFood(Person[] personList, int goldNum)
        {
            if (!GameUtility.IsValidPersonArray(personList)) return false;
            if (goldNum == 0) return false;
            if (hasBusiness == 0) return false;

            Scenario scenario = Scenario.Cur;

            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.TradeFood;

            int meritGain = JobType.GetJobLimit(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

            Person person = personList[0];
            if (person == null) return false;

            InitJobFeature(personList);

            person.merit += meritGain;
            person.GainExp(meritGain);
            freePersons.Remove(person);
            person.ActionOver = true;

            int totalValue = GameUtility.Method_Trade(person.Politics);

            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(totalValue);
            GameEvent.OnCityJobResult?.Invoke(this, jobId, personList, overrideData);
            totalValue = overrideData.Value;


            overrideData.Value = techniquePointGain;
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(this, jobId, personList, overrideData);
            techniquePointGain = overrideData.Value;

            overrideData.Recycle();

            // TODO : 城市粮价
            int p = hasBusiness;

            if (goldNum > 0)
            {
                totalValue = totalValue * goldNum * p / 100;
                if (totalValue + food > foodLimit)
                    totalValue = foodLimit - food;

                AddGold(-goldNum);
                AddFood(totalValue);

                Render?.ShowInfo(-goldNum, (int)InfoType.Gold);
                Render?.ShowInfo(totalValue, (int)InfoType.Food);
#if SANGO_DEBUG
                Sango.Log.Info($"@内政@[{BelongForce.Name}]{person.Name}在<{Name}>花费{goldNum}交易到了{totalValue}粮食, 现有粮食:{food}");
#endif
            }
            else
            {
                totalValue = totalValue * (-goldNum / p) / 100;
                if (totalValue + gold > goldLimit)
                    totalValue = goldLimit - gold;
                AddGold(totalValue);
                AddFood(goldNum);

                Render?.ShowInfo(goldNum, (int)InfoType.Gold);
                Render?.ShowInfo(totalValue, (int)InfoType.Food);

#if SANGO_DEBUG
                Sango.Log.Info($"@内政@[{BelongForce.Name}]{person.Name}在<{Name}>花费{-goldNum}交易到了{totalValue}资金, 现有资金:{gold}");
#endif
            }

            BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

            AddJobCounter(jobId);

            BelongForce.GainTechniquePoint(techniquePointGain);

            ClearJobFeature();
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
        /// 获取城池的反击力
        /// </summary>
        /// <returns>城池的反击力</returns>
        public override int GetAttackBack()
        {
            ScenarioVariables Variables = Scenario.Cur.Variables;
            // 根据太守数值来计算基础伤害
            int atk = Math.Max(BuildingType.atkBack, (Leader?.Strength ?? 50 * 5000 + Leader?.Command ?? 50 * 5000) / 10000);

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
            int def = Math.Max(70, (Leader?.Intelligence ?? 70 * 3000 + Leader?.Command ?? 70 * 7000) / 10000);

            return def;
        }

        /// <summary>
        /// 获取可用于技能的兵力
        /// </summary>
        /// <returns>可用于技能的兵力</returns>
        public override int GetSkillMethodAvaliabledTroops()
        {
            return Math.Max(5000, durability * troops / DurabilityLimit);
        }

        public struct EnemyInfo
        {
            public Troop troop;
            public int distance;
        }

        protected const int SAVE_ROUND = 40;
        protected List<EnemyInfo> enemies = new List<EnemyInfo>();
        protected bool[] enemiesRound = new bool[SAVE_ROUND];

        /// <summary>
        /// 获取最近的敌人部队
        /// </summary>
        /// <param name="checkCell">检查的单元格</param>
        /// <returns>最近的敌人部队</returns>
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

        /// <summary>
        /// 检查指定回合是否有敌人
        /// </summary>
        /// <param name="round">回合数</param>
        /// <returns>是否有敌人</returns>
        public bool IsEnemiesRound(int round)
        {
            if (round < enemiesRound.Length)
                return enemiesRound[round];
            return false;
        }
        /// <summary>
        /// 检查是否有敌人
        /// </summary>
        /// <returns>是否有敌人</returns>
        public bool IsEnemiesRound()
        {
            for (int i = 0; i < enemiesRound.Length; i++)
            {
                if (enemiesRound[i]) return true;
            }
            return false;
        }


        /// <summary>
        /// 检查敌人是否存活
        /// </summary>
        /// <param name="enemyInfo">存活的敌人信息</param>
        /// <returns>是否有存活的敌人</returns>
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

        /// <summary>
        /// 检查是否有存活的敌人
        /// </summary>
        /// <returns>是否有存活的敌人</returns>
        public bool CheckEnemiesIfAlive()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyInfo check = enemies[i];
                if (check.troop.IsAlive)
                {
                    return true;
                }
            }
            return false;
        }

        /// 这几个属性提供给AI使用
        internal Troop CurActiveTroop = null;
        internal MissionType TroopMissionType = MissionType.None;
        internal int TroopMissionTargetId;

        /// <summary>
        /// 确保部队初始化并加入场景
        /// </summary>
        /// <param name="troop">要确保的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>确保后的部队</returns>
        public Troop EnsureTroop(Troop troop, Scenario scenario)
        {
            // 先加入剧本才能分配ID
            troop.cell = this.CenterCell;
            this.CenterCell.troop = troop;
            scenario.Add(troop);
            troop.Init(scenario);
            return troop;
        }

        /// <summary>
        /// 执行AI逻辑
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>是否完成</returns>
        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                GameEvent.OnCityAIStart?.Invoke(this, scenario);
                AIPrepared = true;
            }

            if (CurActiveTroop != null)
            {
                if (!CurActiveTroop.DoAI(scenario))
                    return false;
                CurActiveTroop = null;
            }

            while (AICommandList.Count > 0)
            {
                System.Func<City, Scenario, bool> CurrentCommand = AICommandList[0];
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                AICommandList.RemoveAt(0);
            }

            GameEvent.OnCityAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            ActionOver = true;
            return true;
        }

        /// <summary>
        /// 准备敌人信息
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public virtual void PrepareEnemiesInfo(Scenario scenario)
        {
            // 准备敌人信息
            enemies.Clear();
            for (int i = 0; i < enemiesRound.Length; i++)
                enemiesRound[i] = false;

            for (int i = 0; i < areaCellList.Count; i++)
            {
                Cell cell = areaCellList[i];
                if (cell.troop != null && cell.troop.IsEnemy(this))
                {
                    int round = scenario.Map.Distance(CenterCell, cell);
                    if (round < SAVE_ROUND)
                    {
                        enemies.Add(new EnemyInfo { troop = cell.troop, distance = round });
                        for (int j = round; j < enemiesRound.Length; j++)
                            enemiesRound[j] = true;
                    }
                }
            }

            if (enemies.Count > 1)
            {
                enemies.Sort((a, b) =>
                {
                    return a.distance.CompareTo(b.distance);
                });
            }
        }

        /// <summary>
        /// 准备AI逻辑
        /// </summary>
        /// <param name="scenario">场景对象</param>
        public virtual void AIPrepare(Scenario scenario)
        {
            // 准备敌人信息
            PrepareEnemiesInfo(scenario);

            UpdateActiveTroopTypes();
            UpdateFightPower();

            //if (IsBorderCity)
            //{

            //    AICommandList.Add(CityAI.AIAttack);
            //    AICommandList.Add(CityAI.AITradeFood);
            //    //AICommandList.Add(CityAI.AISecurity);

            //    //if (troops < 20000)
            //    //{
            //    //    AICommandList.Add(CityAI.AIRecruitTroop);
            //    //    AICommandList.Add(CityAI.AIIntrior);
            //    //}
            //    //else
            //    //{
            //    //    if (scenario.Info.day == 10)
            //    //    {
            //    //        AICommandList.Add(CityAI.AIRecruitTroop);
            //    //        AICommandList.Add(CityAI.AICreateItems);
            //    //        AICommandList.Add(CityAI.AIIntrior);
            //    //    }
            //    //    else if (scenario.Info.day == 20)
            //    //    {
            //    //        AICommandList.Add(CityAI.AIIntrior);
            //    //        AICommandList.Add(CityAI.AIRecruitTroop);
            //    //        AICommandList.Add(CityAI.AICreateItems);
            //    //    }
            //    //    else
            //    //    {
            //    //        AICommandList.Add(CityAI.AICreateItems);
            //    //        AICommandList.Add(CityAI.AIRecruitTroop);
            //    //        AICommandList.Add(CityAI.AIIntrior);
            //    //    }
            //    //}
            //    AICommandList.Add(CityAI.AIIntrior);
            //}
            //else
            //{
            //    //AICommandList.Add(CityAI.AISecurity);
            //    AICommandList.Add(CityAI.AITradeFood);
            //    // 物资输送
            //    AICommandList.Add(CityAI.AITransfrom);
            //    //if (troops < itemStore.TotalNumber)
            //    //    AICommandList.Add(CityAI.AIRecruitTroop);
            //    //else
            //    //    AICommandList.Add(CityAI.AICreateItems);
            //    AICommandList.Add(CityAI.AIIntrior);
            //}

            GameEvent.OnCityAIPrepare?.Invoke(this, scenario);
        }

        /// <summary>
        /// 检查是否太守需要重新设置
        /// </summary>
        /// <param name="person">原太守</param>
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
        public void UpdateNewLeader()
        {
            Person dest = null;
            Official higher = null;
            int commandHigher = 0;
            for (int i = 0; i < allPersons.Count; i++)
            {
                Person checker = allPersons[i];
                if (checker != null && checker.IsAlive)
                {
                    if (checker.IsGovernor)
                    {
                        dest = checker;
                        break;
                    }
                }
            }

            if (dest == null)
            {
                for (int i = 0; i < allPersons.Count; i++)
                {
                    Person checker = allPersons[i];
                    if (checker != null && checker.IsAlive)
                    {
                        if (checker.IsCommander)
                        {
                            dest = checker;
                            break;
                        }
                    }
                }
            }

            if (dest == null)
            {
                for (int i = 0; i < allPersons.Count; i++)
                {
                    Person checker = allPersons[i];
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
                }
            }

            Leader = dest;
            Leader?.SetStateLeader();
        }

        /// <summary>
        /// 获取已建造完成的建筑类型的数量
        /// </summary>
        /// <param name="buildingKindId">建筑类型ID</param>
        /// <returns>已建造完成的建筑数量</returns>
        public int GetBuildingComplateNumber(int buildingKindId)
        {
            int complateNum = 0;
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building building = allBuildings[i];
                if (building.BuildingType.kind == buildingKindId && building.isComplate)
                {
                    complateNum++;
                }
            }
            return complateNum;
        }


        /// <summary>
        /// 获取空闲的建筑
        /// </summary>
        /// <param name="buildingKindId">建筑类型ID</param>
        /// <returns>空闲的建筑</returns>
        public Building GetFreeBuilding(int buildingKindId)
        {
            for (int i = 0; i < allBuildings.Count; i++)
            {
                Building building = allBuildings[i];
                if (building.BuildingType.kind == buildingKindId && building.isComplate && !building.ActionOver && !building.isWorking)
                {
                    return building;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取已建造的建筑类型的数量(包括未完成的)
        /// </summary>
        /// <param name="buildingKindId">建筑类型ID</param>
        /// <returns>建筑数量</returns>
        public int GetBuildingNumber(int buildingKindId)
        {
            int count;
            if (buildingCountMap.TryGetValue(buildingKindId, out count))
                return count;
            return 0;
        }

        /// <summary>
        /// 检查内城建筑是否已满
        /// </summary>
        /// <returns>内城建筑是否已满</returns>
        public bool IsInteriorBuildFull()
        {
            for (int i = 0; i < interiorCellList.Count; i++)
            {
                if (interiorCellList[i].building == null)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 获取内政地使用数量
        /// </summary>
        /// <returns>内政地使用数量</returns>
        public int GetInteriorCellUsedCount()
        {
            int complateNum = 0;
            for (int i = 0; i < interiorCellList.Count; i++)
            {
                if (interiorCellList[i].building != null)
                    complateNum++;
            }
            return complateNum;
        }

        ///// <summary>
        ///// 获取已建造完成的建筑类型的最大等级(不叠加)
        ///// </summary>
        ///// <param name="buildingTypeId"></param>
        ///// <returns></returns>
        //public int GetIntriorBuildingComplateMaxLevel(int buildingKindId)
        //{
        //    int complateNum = 0;
        //    for (int i = 0; i < allBuildings.Count; i++)
        //    {
        //        Building building = allBuildings[i];
        //        if (building.BuildingType.kind == buildingKindId && building.isComplte)
        //        {
        //            complateNum = Math.Max(complateNum, building.BuildingType.level);
        //        }
        //    }
        //    return complateNum;
        //}

        public int EnemyCount
        {
            get
            {
                return enemies.Count;
            }
        }

        /// <summary>
        /// 计算最大士气
        /// </summary>
        public void CalculateMaxMorale()
        {
            int max = 100;
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(max);
            GameEvent.OnCityCalculateMaxMorale?.Invoke(this, overrideData);
            MaxMorale = overrideData.ValueAndRecycle;
        }

        public void CalculateLimit()
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(0);
            GameEvent.OnCityCalculateMaxGold?.Invoke(this, overrideData);
            goldLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxFood?.Invoke(this, overrideData);
            foodLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxItemStoreSize?.Invoke(this, overrideData);
            storeLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxTroops?.Invoke(this, overrideData);
            troopsLimitAdd = overrideData.Value;

            overrideData.Value = 0;
            GameEvent.OnCityCalculateMaxDurability?.Invoke(this, overrideData);
            durabilityLimitAdd = overrideData.Value;

            overrideData.Recycle();
        }
    }
}
