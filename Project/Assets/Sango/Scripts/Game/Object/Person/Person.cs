using TKNewtonsoft.Json;
using Sango.Render;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 逃出方式枚举
    /// </summary>
    public enum EscapeType
    {
        /// <summary>
        /// 无
        /// </summary>
        None,
        /// <summary>
        /// 逃跑
        /// </summary>
        Escape,
        /// <summary>
        /// 被释放
        /// </summary>
        Released,
        /// <summary>
        /// 部队灭亡
        /// </summary>
        TroopDestroyed
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Person : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Person; } }

        public string ColorName => $"<color=#7CCADB>{Name}</color>";

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Force>))]
        public Force BelongForce { get; set; }

        public bool IsPlayer => BelongForce?.IsPlayer ?? false;
        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => BelongCorps?.IsPlayerControl ?? false;
        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => BelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// 所属军团
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Force>))]
        public Corps BelongCorps { get; set; }

        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<City>))]
        public City BelongCity { get; set; }

        /// <summary>
        /// 所在城池
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<City>))]
        public City CurrentCity { get; set; }

        /// <summary>
        /// 所属部队
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Troop>))]
        public Troop BelongTroop { get; set; }

        /// <summary>
        /// 姓
        /// </summary>
        public int familyNameID;
        [JsonProperty] public string familyName;

        /// <summary>
        /// 名
        /// </summary>
        public int giveNameID;
        [JsonProperty] public string giveName;

        /// <summary>
        /// 字
        /// </summary>
        public int nickNameID;
        [JsonProperty] public string nickName;

        /// <summary>
        /// 身平
        /// </summary>
        [JsonProperty] public int description;

        /// <summary>
        /// 头像id
        /// </summary>
        [JsonProperty] public string headIconID;

        /// <summary>
        /// 立绘id(弃用)
        /// </summary>
        [JsonProperty] public string imageID;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public string image;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public string image_old;

        /// <summary>
        /// 性别
        /// </summary>
        [JsonProperty] public int sex;

        /// <summary>
        /// 登场年份
        /// </summary>
        [JsonProperty] public int appearance;

        /// <summary>
        /// 出生地
        /// </summary>
        [JsonProperty] public int birthplace;

        /// <summary>
        /// 语气
        /// </summary>
        [JsonProperty] public int tone;

        /// <summary>
        /// 声音
        /// </summary>
        [JsonProperty] public int voice;

        /// <summary>
        /// 是否被发现
        /// </summary>
        public bool beFinded => !Invisible;

        /// <summary>
        /// 出生年
        /// </summary>
        [JsonProperty] public int yearBorn;

        /// <summary>
        /// 死亡年
        /// </summary>
        [JsonProperty] public int yearDead;

        /// <summary>
        /// 相性
        /// </summary>
        [JsonProperty] public int compatibility;

        /// <summary>
        /// 身分
        /// </summary>
        [JsonProperty]
#if SANGO_DEBUG

        public int state
        {
            get { return _state; }
            set
            {
                _state = value;
                Sango.Log.Info($"{Name}改变状态=> {PersonSortFunction.SortByState.GetValueStr(this)}");
            }
        }
        private int _state;
#else
        public int state;
#endif

        /// <summary>
        /// 性格
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Personality>))]
        public Personality personality;

        /// <summary>
        /// 义理
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Argumentation>))]
        public Argumentation argumentation;

        /// <summary>
        /// 官职
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Official>))]
        [JsonProperty]
        public Official Official { get; set; }

        public bool CanUpgradeOfficial
        {
            get
            {
                if (Official == null)
                    return false;
                return Official.meritNeeds > 0 && merit >= Official.meritNeeds;
            }
        }

        /// <summary>
        /// 忠诚
        /// </summary>
        [JsonProperty] public int loyalty;

        /// <summary>
        /// 功绩
        /// </summary>
        [JsonProperty] public int merit;

        /// <summary>
        /// 体力
        /// </summary>
        [JsonProperty] public int stamina;

        /// <summary>
        /// 经验
        /// </summary>
        [JsonProperty] public int Exp { get; private set; }

        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<PersonLevel>))]
        public PersonLevel Level { get; set; }

        /// <summary>
        /// 统御
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue command = new PersonAttributeValue();

        /// <summary>
        /// 武力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue strength = new PersonAttributeValue();

        /// <summary>
        /// 智力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue intelligence = new PersonAttributeValue();

        /// <summary>
        /// 政治
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue politics = new PersonAttributeValue();

        /// <summary>
        /// 魅力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAttributeValueConverter))]
        public PersonAttributeValue glamour = new PersonAttributeValue();

        /// <summary>
        /// 血缘
        /// </summary>
        [JsonProperty] public int consanguinity;

        /// <summary>
        /// 伤病
        /// </summary>
        [JsonProperty] public int injury;

        /// <summary>
        /// 父亲
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Father { get; set; }

        /// <summary>
        /// 母亲
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Mother { get; set; }

        /// <summary>
        /// 配偶
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> SpouseList { get; private set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Brother { get; set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        public List<Person> BrotherList;

        /// <summary>
        /// 喜欢武将
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> LikePersonList { get; private set; }

        /// <summary>
        /// 厌恶武将
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> HatePersonList { get; private set; }

        /// <summary>
        /// 儿子们, 由father属性添加至父亲的属性里
        /// </summary>
        public SangoObjectList<Person> sonList = new SangoObjectList<Person>();

        /// <summary>
        /// 矛
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue spearLv = new PersonAbilityValue();

        /// <summary>
        /// 戟
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue halberdLv = new PersonAbilityValue();

        /// <summary>
        /// 弓弩
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue crossbowLv = new PersonAbilityValue();

        /// <summary>
        /// 骑
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue rideLv = new PersonAbilityValue();

        /// <summary>
        /// 水军
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue waterLv = new PersonAbilityValue();

        /// <summary>
        /// 器械
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(PersonAbilityValueConverter))]
        public PersonAbilityValue machineLv = new PersonAbilityValue();

        /// <summary>
        /// 行动标记
        /// </summary>
        [JsonProperty] public BitCheck32 actionFlag = new BitCheck32();

        /// <summary>
        /// 武将特性
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(SangoObjectListIDConverter<Feature>))]
        public SangoObjectList<Feature> FeatureList { get; private set; }

        /// <summary>
        /// 库存
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(ItemStoreConverter))]
        public ItemStore itemStore = new ItemStore();

        /// <summary>
        /// 装备的武器
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Equipment>))]
        [JsonProperty]
        public Equipment EquippedWeapon { get; set; }

        /// <summary>
        /// 装备的马
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Equipment>))]
        [JsonProperty]
        public Equipment EquippedHorse { get; set; }

        /// <summary>
        /// 装备的铠甲
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Equipment>))]
        [JsonProperty]
        public Equipment EquippedArmor { get; set; }

        [JsonProperty]
        public int bannedForceId;

        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Building>))]
        public Building workingBuilding;

        public int escapeFactorWhenTroopDestroy = 0;

        public bool HasItem(int itemTypeId)
        {
            return itemStore.GetNumber(itemTypeId) > 0;
        }

        public bool IsLeader => state == (int)PersonStateType.Leader || state == (int)PersonStateType.Governor;
        public bool IsCommander => state == (int)PersonStateType.Commander;
        public bool IsGovernor => state == (int)PersonStateType.Governor;

        public void SetStateNormal() { state = (int)PersonStateType.Normal; }
        public void SetStateLeader()
        {
            if (IsGovernor) return;
            if (IsCommander) return;
            state = (int)PersonStateType.Leader;
        }

        /// <summary>
        /// 枪兵适应
        /// </summary>
        public int SpearLv => spearLv.value;

        /// <summary>
        /// 盾兵适应
        /// </summary>
        public int HalberdLv => halberdLv.value;

        /// <summary>
        /// 弓兵适应
        /// </summary>
        public int CrossbowLv => crossbowLv.value;

        /// <summary>
        /// 骑兵适应
        /// </summary>
        public int RideLv => rideLv.value;

        /// <summary>
        /// 水军适应
        /// </summary>
        public int WaterLv => waterLv.value;

        /// <summary>
        /// 兵器适应
        /// </summary>
        public int MachineLv => machineLv.value;

        /// <summary>
        /// 统率
        /// </summary>
        public int Command => command.Value + GetEquipmentBonus(x => x.commandBonus);

        /// <summary>
        /// 武力
        /// </summary>
        public int Strength => strength.Value + GetEquipmentBonus(x => x.strengthBonus);

        /// <summary>
        /// 智力
        /// </summary>
        public int Intelligence => intelligence.Value + GetEquipmentBonus(x => x.intelligenceBonus);

        /// <summary>
        /// 政治
        /// </summary>
        public int Politics => politics.Value + GetEquipmentBonus(x => x.politicsBonus);

        /// <summary>
        /// 魅力
        /// </summary>
        public int Glamour => glamour.Value + GetEquipmentBonus(x => x.glamourBonus);

        /// <summary>
        /// 是否可登场
        /// </summary>
        public virtual bool IsValid => state > 0 && state != (int)PersonStateType.Invalid && state != (int)PersonStateType.Dead;

        /// <summary>
        /// 兵力上限其他更改值(道具等加持)
        /// </summary>
        public int troopsLimitExtra = 0;

        /// <summary>
        /// 带兵上限,根据官职和国家科技决定
        /// </summary>
        public int TroopsLimit
        {
            //TODO: 增加国家科技加持
            get { return Math.Max(IsGovernor ? 20000 : 0, Official.troopsLimit) + Level.troops + troopsLimitExtra; }
        }

        /// <summary>
        /// 军事能力
        /// </summary>
        public int MilitaryAbility
        {
            get { return Command * 2 + Strength * 3; }
        }

        /// <summary>
        /// 商业能力
        /// </summary>
        public int BaseCommerceAbility => Intelligence;

        /// <summary>
        /// 巡视能力
        /// </summary>
        public int BaseSecurityAbility => Command;

        /// <summary>
        /// 训练能力
        /// </summary>
        public int BaseTrainTroopAbility => Strength;

        /// <summary>
        /// 农业能力
        /// </summary>
        public int BaseAgricultureAbility => Politics;

        /// <summary>
        /// 建设能力
        /// </summary>
        public int BaseBuildAbility => Politics;

        /// <summary>
        /// 生产能力
        /// </summary>
        public int BaseCreativeAbility => Intelligence;

        /// <summary>
        /// 搜寻能力
        /// </summary>
        public int BaseSearchingAbility
        {
            get
            {
                return (Politics + Glamour) / 2;
            }
        }

        /// <summary>
        /// 招募能力
        /// </summary>
        public int BaseRecruitmentAbility => Glamour;


        public void OnPersonAgeUpdate(Scenario scenario)
        {
            Age = scenario.Info.year - yearBorn;
            if (scenario.Variables.AgeEnabled && scenario.Variables.EnableAgeAbilityFactor)
            {
                command.Update(); strength.Update(); intelligence.Update(); politics.Update(); glamour.Update();
                //spearLv.Update(); halberdLv.Update(); crossbowLv.Update(); horseLv.Update(); waterLv.Update(); machineLv.Update();
            }
        }

        public ushort skill;

        [JsonProperty] public int missionType;
        [JsonProperty] public int missionTarget;
        [JsonProperty] public int missionCounter;
        [JsonProperty] public int missionParams1;
        [JsonProperty] public int missionParams2;
        [JsonProperty] public int missionParams3;
        [JsonProperty] public int missionParams4;

        /// <summary>
        /// 在当前城市的停留回合数
        /// </summary>
        [JsonProperty] public int stayTurnCount;

        public bool rewardOver;

        public int Age { get; private set; }

        /// <summary>
        /// 是否空闲
        /// </summary>
        public bool IsFree { get { return BelongTroop == null && missionType == (int)MissionType.None && !IsPrisoner && !IsDead; } }

        /// <summary>
        /// 是否在野
        /// </summary>
        public bool IsWild { get { return state == (int)PersonStateType.Unemployed; } }

        /// <summary>
        /// 是否为俘虏
        /// </summary>
        public bool IsPrisoner { get { return state == (int)PersonStateType.Prisoner; } }

        /// <summary>
        /// 是否未发现
        /// </summary>
        public bool Invisible { get { return state == (int)PersonStateType.Invisible; } }

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead { get { return state == (int)PersonStateType.Dead; } }

        public bool IsAlliance(BuildingBase other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Person other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        /// <summary>
        /// 所有的武将情况归属,全由武将决定,城池不再记录任何武将归属情况
        /// </summary>
        /// <param name="scenario"></param>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (Brother != null)
            {
                if (Brother.BrotherList == null)
                    Brother.BrotherList = new List<Person>();

                Brother.BrotherList.Add(this);
            }

            if (IsAlive)
            {
                switch ((PersonStateType)state)
                {
                    case PersonStateType.Governor:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            BelongCity.Leader = this;
                        }
                        break;
                    case PersonStateType.Commander:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            BelongCity.Leader = this;
                        }
                        break;
                    case PersonStateType.Leader:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            BelongCity.Leader = this;
                        }
                        break;
                    case PersonStateType.Normal:
                        if (BelongCity != null)
                        {
                            BelongCity.allPersons.Add(this);
                            if (BelongForce != BelongCity.BelongForce || BelongCorps != BelongCity.BelongCorps)
                            {
                                Sango.Log.Error($"[{Id}]{Name}归属force:{BelongForce?.Name} corps:{BelongCorps?.Name}, 但在city[{BelongCity?.Name}] force:{BelongCity.BelongForce?.Name} corps:{BelongCity.BelongCorps?.Name}");
                                BelongForce = BelongCity.BelongForce;
                                BelongCorps = BelongCity.BelongCorps;
                            }
                        }
                        break;
                    case PersonStateType.Unemployed:
                        CurrentCity.wildPersons.Add(this);
                        break;
                    case PersonStateType.Prisoner:

                        if (CurrentCity.IsSameForce(this))
                        {
                            // 修复一下
                            BelongCity = CurrentCity;
                            BelongCity.allPersons.Add(this);
                        }
                        else
                        {
                            // 准备俘虏
                            if (BelongForce != null)
                                BelongForce.BeCaptiveList.Add(this);

                            if (BelongTroop != null)
                            {
                                BelongTroop.captiveList.Add(this);
                            }
                            else
                            {
                                CurrentCity.captiveList.Add(this);
                            }
                        }
                        break;
                    case PersonStateType.Invalid:
                        break;
                    case PersonStateType.Invisible:
                        if (CurrentCity != null)
                            CurrentCity.invisiblePersons.Add(this);
                        else if (BelongCity != null)
                            BelongCity.invisiblePersons.Add(this);
                        break;
                    case PersonStateType.Dead:
                        break;
                }

                //if (IsPrisoner)
                //{
                //    // 准备俘虏
                //    if (BelongForce != null)
                //        BelongForce.BeCaptiveList.Add(this);

                //    if (BelongTroop != null)
                //        BelongTroop.captiveList.Add(this);
                //    else
                //        CurrentCity.captiveList.Add(this);
                //}
                //else
                //{

                //    if (IsValid && BelongCity != null)
                //    {

                //        if (Invisible)
                //        {
                //            BelongCity.invisiblePersons.Add(this);
                //        }
                //        else if (IsWild)
                //        {
                //            BelongCity.wildPersons.Add(this);
                //        }
                //        else
                //        {
                //            BelongCity.allPersons.Add(this);
                //            if (state == (int)PersonStateType.Leader)
                //            {
                //                BelongCity.Leader = this;
                //            }
                //            else if (state == (int)PersonStateType.Governor)
                //            {
                //                BelongCity.Leader = this;
                //            }

                //            if (BelongForce != BelongCity.BelongForce || BelongCorps != BelongCity.BelongCorps)
                //            {
                //                Sango.Log.Error($"[{Id}]{Name}归属force:{BelongForce?.Name} corps:{BelongCorps?.Name}, 但在city[{BelongCity?.Name}] force:{BelongCity.BelongForce?.Name} corps:{BelongCity.BelongCorps?.Name}");

                //                BelongForce = BelongCity.BelongForce;
                //                BelongCorps = BelongCity.BelongCorps;
                //            }
                //        }
                //    }

                //}
            }


            if (Father != null)
                Father.sonList.Add(this);

            OnPersonAgeUpdate(scenario);

            if (Official == null)
                Official = scenario.CommonData.Officials[0];

            Official.OnPersonAdd(this);

            if (Level == null)
                Level = scenario.CommonData.PersonLevels[0];
        }

        public override void Init(Scenario scenario)
        {
            base.Init(scenario);

            if (Brother != null)
            {
                if (Brother == this)
                {
                    BrotherList.Sort(SangoObject.Compare);

                }
                else
                {
                    BrotherList = Brother.BrotherList;
                }
            }
        }

        public override bool OnYearStart(Scenario scenario)
        {
            if (IsDead) return true;

            OnPersonAgeUpdate(scenario);

            if (state == (int)PersonStateType.Invalid)
            {
                if (appearance > 0 && appearance <= scenario.Info.year)
                {
                    state = (int)PersonStateType.Invisible;

                    City city = null;
                    if (birthplace > 0)
                        city = scenario.citySet.Get(birthplace);
                    if (city == null)
                        city = scenario.citySet.RandomGet();

                    // 这里要处理登场城池
                    city.invisiblePersons.Add(this);
                    CurrentCity = city;
                }
            }
            else
            {
                if (IsWild && sonList != null)
                {
                    sonList.ForEach(x =>
                    {
                        if (x.state == (int)PersonStateType.Invalid)
                        {
                            if (x.Age >= 16)
                            {
                                x.CurrentCity = CurrentCity;
                                x.state = (int)PersonStateType.Invisible;
                                CurrentCity.invisiblePersons.Add(this);
                            }
                        }
                    });
                }
            }

            return base.OnYearStart(scenario);
        }

        public bool DoMove(City dest, Scenario scenario)
        {
            City target = dest.BelongCity == null ? dest : dest.BelongCity;
            City currentCity = CurrentCity.BelongCity == null ? CurrentCity : CurrentCity.BelongCity;

            if (target == currentCity)
            {
                return true;
            }

            // 找到最短移动路径
            List<City> path = scenario.FindShortestPath(currentCity, target);
            if (path == null || path.Count <= 1)
            {
                return true;
            }

            City next = path[1];
            ChangeCurrentCity(next);
            if (next == dest)
            {
                return true;
            }
            return false;
        }

        public void UpdateMission(Scenario scenario)
        {
            if (missionType == 0) return;

            switch (missionType)
            {
                case (int)MissionType.PersonReturn:
                    {
                        City dest = scenario.citySet.Get(missionTarget);
                        if (!this.IsSameForce(dest))
                        {
                            if (BelongForce != null)
                            {
                                ChangeCity(BelongForce.CapitalCity);
                                SetMission(MissionType.PersonReturn, BelongForce.CapitalCity);
                            }
                            else
                            {
                                ClearMission();
                            }
                            return;
                        }

                        if (DoMove(dest, scenario))
                        {
                            ClearMission();
                            dest.OnPersonReturnCity(this);
                        }
                    }
                    break;
                case (int)MissionType.PersonRecruitPerson:
                    {
                        Person dest_person = scenario.personSet.Get(missionTarget);
                        City dest = scenario.citySet.Get(missionParams1);
                        if (BelongCorps != null && this.IsSameForce(dest_person))
                        {
                            // 已经有人招募成功
                            SetMission(MissionType.PersonReturn, BelongCity);
                            return;
                        }

                        if (DoMove(dest, scenario))
                        {
                            ClearMission();
                            CityRecruitPersonEvent te = RenderEvent.Instance.Create<CityRecruitPersonEvent>();
                            te.Init(this, dest_person);
                            RenderEvent.Instance.AddFront(te);
                            SetMission(MissionType.PersonReturn, BelongCity);
                        }
                    }
                    break;
                case (int)MissionType.PersonCreateBoat:
                    {
                        missionCounter--;
                        if (missionCounter <= 0)
                        {
                            int buildingId = missionParams1;
                            int totalValue = missionParams2;
                            ItemType itemType = scenario.GetObject<ItemType>(missionTarget);
                            BelongCity.DoJobCreateBoat(itemType, buildingId, totalValue);
                        }
                    }
                    break;
                case (int)MissionType.PersonCreateMachine:
                    {
                        missionCounter--;
                        if (missionCounter <= 0)
                        {
                            int buildingId = missionParams1;
                            int totalValue = missionParams2;
                            ItemType itemType = scenario.GetObject<ItemType>(missionTarget);
                            BelongCity.DoJobCreateMachine(itemType, buildingId, totalValue);
                        }
                    }
                    break;
                case (int)MissionType.PersonResearch:
                    {
                        missionCounter--;
                        if (missionCounter <= 0)
                        {
                            ClearMission();
                        }
                    }
                    break;
                case (int)MissionType.PersonDiplomacy:
                    {
                        City targetCity = scenario.citySet.Get(missionTarget);
                        if (DoMove(targetCity, scenario))
                        {
                            // 执行外交行动
                            Force receiverForce = scenario.forceSet.Get(missionParams1);
                            if (receiverForce == null || !receiverForce.IsAlive || receiverForce.CapitalCity != targetCity)
                            {
                                // 完成任务，返回原城市
                                SetMission(MissionType.PersonReturn, BelongCity);
                                return;
                            }
                            DiplomacyActionType actionType = (DiplomacyActionType)missionParams2;
                            if (this.IsPlayer || receiverForce.IsPlayer)
                            {
                                Sango.Render.DiplomacyEvent diplomacyEvent = RenderEvent.Instance.Create<Sango.Render.DiplomacyEvent>();
                                diplomacyEvent.Init(this, actionType, receiverForce, targetCity, missionParams3, missionParams4);
                                RenderEvent.Instance.Add(diplomacyEvent);
                            }
                            else
                            {
                                GameSystem.GetSystem<DiplomacyManager>().ExecuteDiplomacyMission(this, actionType, receiverForce, missionParams3);
                            }

                            // 完成任务，返回原城市
                            SetMission(MissionType.PersonReturn, BelongCity);
                        }
                    }
                    break;
            }
        }
        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3, int p4)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = p3;
            this.missionParams4 = p4;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2, int p3)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = p3;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1, int p2)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = p2;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter, int p1)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = p1;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = 0;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public void ClearMission()
        {
            this.missionType = 0;
            this.missionTarget = 0;
            this.missionCounter = 0;
            this.missionParams1 = 0;
            this.missionParams2 = 0;
            this.missionParams3 = 0;
            this.missionParams4 = 0;
        }

        public override bool OnTurnStart(Scenario scenario)
        {

            return base.OnTurnStart(scenario);
        }
        public override bool OnForceTurnStart(Scenario scenario)
        {
            if (BelongForce != null && IsAlive)
            {
                BelongForce.GainHegemonyPoint(1);
            }

            // 这里肯定有势力
            if (sonList != null)
            {
                sonList.ForEach(x =>
                {
                    if (x.state == (int)PersonStateType.Invalid)
                    {
                        if (x.Age >= 16)
                        {
                            x.BelongForce = BelongForce;
                            x.BelongCorps = BelongCorps;

                            City becameCity = BelongCity;
                            if (IsPrisoner)
                            {
                                becameCity = BelongForce.CapitalCity;
                            }
                            x.BelongCity = becameCity;
                            x.CurrentCity = becameCity;
                            becameCity.allPersons.Add(x);
                            becameCity.freePersons.Add(x);
                            x.state = (int)PersonStateType.Normal;
                        }
                    }
                });
            }


            ActionOver = !IsFree;
            return base.OnForceTurnStart(scenario);
        }

        public override bool OnForceTurnEnd(Scenario scenario)
        {
            return base.OnForceTurnEnd(scenario);
        }

        public override bool OnTurnEnd(Scenario scenario)
        {
            // 在野武将移动逻辑
            if (IsWild)
            {
                stayTurnCount++;
                if (stayTurnCount > 5 && GameRandom.Chance(10)) // 10%概率
                {
                    // 随机选择一个邻接城市
                    SangoObjectList<City> neighborCities = BelongCity.NeighborList;
                    if (neighborCities.Count > 0)
                    {
                        int randomIndex = GameRandom.Range(neighborCities.Count);
                        City targetCity = neighborCities[randomIndex];
                        if (targetCity != null)
                        {
                            CurrentCity.RemoveWildPerson(this);
                            // 移动到新城市
                            ChangeCurrentCity(targetCity);
                            targetCity.AddWildPerson(this);
                            BelongCity = targetCity;

                            // 重置停留时间
                            stayTurnCount = 0;
#if SANGO_DEBUG
                            Sango.Log.Info($"@人才@在野武将{Name}从{BelongCity.Name}移动到{targetCity.Name}");
#endif
                        }
                    }
                }
            }

            UpdateMission(scenario);
            return base.OnTurnEnd(scenario);
        }

        public void TransformToCity(City dest)
        {
            // 如果转移主公到其他军团城市,需要解散目标军团
            if (IsGovernor && dest.BelongCorps != BelongCorps)
            {
                BelongForce.DeleteCorps(dest.BelongCorps);
            }
            BelongCity.RemovePerson(this);
            ChangeBelongCity(dest);
            dest.AddPerson(this);
            SetMission(MissionType.PersonReturn, dest);
            ActionOver = true;
#if SANGO_DEBUG
            Sango.Log.Info($"*{BelongForce?.Name}的{Name}从{BelongCity.Name}向{dest.Name}转移*");
#endif
        }

        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;
            if (BelongCorps != corps)
            {
                last = BelongCorps;
                BelongCorps = corps;
                if (BelongForce != corps.BelongForce)
                {
                    BelongForce = corps.BelongForce;
                }
            }
            return last;
        }

        /// <summary>
        /// 改变所属城市
        /// </summary>
        /// <param name="city"></param>
        /// <returns></returns>
        public City ChangeCity(City city)
        {
            City last = null;
            if (BelongCity != city)
            {
                last = BelongCity;
#if SANGO_DEBUG
                Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所属城市 {BelongCity?.Name} => {city.Name}");
#endif
                if (!IsWild)
                {
                    BelongCity?.RemovePerson(this);
                    city.AddPerson(this);
                    BelongCity = city;
                    if (BelongCorps != city.BelongCorps)
                        BelongCorps = city.BelongCorps;
                    if (BelongForce != city.BelongForce)
                        BelongForce = city.BelongForce;
                    if (!IsGovernor)
                    {
                        SetStateNormal();
                    }
                }
                else
                {
                    BelongCity?.RemoveWildPerson(this);
                    city.AddWildPerson(this);
                    BelongCity = city;
                }

                BelongTroop?.OnPersonChangeCity(this, last, city);
            }
            return last;
        }

        /// <summary>
        /// 改变所在城市
        /// </summary>
        /// <param name="city"></param>
        /// <returns></returns>
        public City ChangeCurrentCity(City city)
        {
            City last = CurrentCity;
            CurrentCity = city;
#if SANGO_DEBUG
            Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所在城市 {last.Name} -> {city.Name}");
#endif
            GameEvent.OnPersonChangCurrentCity?.Invoke(this, city, last);
            return last;
        }

        /// <summary>
        /// 改变所属城市
        /// </summary>
        /// <param name="city"></param>
        /// <returns></returns>
        public City ChangeBelongCity(City city)
        {
            City last = BelongCity;
            BelongCity = city;
#if SANGO_DEBUG
            Sango.Log.Info($"*{BelongForce?.Name}的{Name} 改变所属城市 {last?.Name} -> {city.Name}");
#endif
            if (BelongCorps != city.BelongCorps)
                BelongCorps = city.BelongCorps;
            if (BelongForce != city.BelongForce)
                BelongForce = city.BelongForce;
            GameEvent.OnPersonChangeBelongCity?.Invoke(this, last, city);
            return last;
        }


        public bool JobRecruitPerson(Person person, City targetCity, int type)
        {
            int probability = GameFormula.Instance.RecruitPersonProbability(this, person, type);
#if SANGO_DEBUG
            Sango.Log.Info($"[{BelongForce.Name}]<{Name}>登庸 -> {person.Name} 成功率:{probability}");
#endif
            //TODO: 招募成功概率计算
            bool success = GameRandom.Chance(probability);
            if (success)
            {
                person.BeRecruit(this, targetCity);
            }
            ScenarioVariables variables = Scenario.Cur.Variables;
            int jobId = (int)CityJobType.RecruitPerson;
            int meritGain = JobType.GetJobLimit(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);
            merit += meritGain;
            BelongForce?.GainTechniquePoint(techniquePointGain);
            ActionOver = true;
            return success;
        }

        public bool JobRecruitPerson(Person person, int type)
        {
            return JobRecruitPerson(person, BelongCity, type);
        }

        public void BeRecruit(Person person, City targetCity)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"[{person.BelongForce.Name}]<{person.Name}>登庸成功, {Name}加入了势力{person.BelongForce.Name}");
#endif
            loyalty = 80;
            if (IsPrisoner)
            {
                // 囚犯从监牢中移除
                if (BelongTroop != null)
                    BelongTroop.RemoveCaptive(this);
                else
                    CurrentCity.RemoveCaptive(this);
                state = (int)PersonStateType.Normal;
                if (!JoinToForce(targetCity))
                {
                    SetMission(MissionType.PersonReturn, targetCity);
                }
            }
            else
            {
                if (IsWild)
                    CurrentCity.RemoveWildPerson(this);
                else if (Invisible)
                    CurrentCity.RemoveInvisiblePerson(this);
                else
                    BelongCity?.RemovePerson(this);

                // 部队中
                if (BelongTroop != null)
                {
                    Troop troop = BelongTroop;
                    // 部队主将
                    if (this == BelongTroop.Leader)
                    {
                        BelongTroop.JoinToForce(targetCity);
                        BelongTroop.ActionOver = true;
                    }
                    else
                    {
                        BelongTroop.RemovePerson(this);
                        ChangeCurrentCity(troop.CurrentCity);
                        if (!JoinToForce(targetCity))
                        {
                            SetMission(MissionType.PersonReturn, targetCity);
                        }
                    }
                    troop.Render?.UpdateRender();
                }
                else
                {
                    // 有归属
                    if (!JoinToForce(targetCity))
                    {
                        SetMission(MissionType.PersonReturn, targetCity);
                    }
                }
            }
            ActionOver = true;
        }

        /// <summary>
        /// 加入某个势力,需要指定一个城市
        /// </summary>
        /// <param name="city"></param>
        public bool JoinToForce(City city)
        {
            bool isSameCity = CurrentCity == city;
            BelongCity = city;
            BelongCorps = city.BelongCorps;
            BelongForce = city.BelongForce;
            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;
            state = (int)PersonStateType.Normal;
            BelongCity.AddPerson(this);
            return isSameCity;
        }

        /// <summary>
        /// 下野
        /// </summary>
        public void LeaveToWild()
        {
            workingBuilding = null;
            loyalty = 0;
            BelongCity.RemovePerson(this);
            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;
            if (IsPrisoner)
            {
                BelongCity = CurrentCity;
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@<{Name}>失去势力,进入囚犯下野状态");
#endif
            }
            else
            {
                state = (int)PersonStateType.Unemployed;
                // 关卡和港口的武将下野到对应的城池里
                BelongCity = CurrentCity.BelongCity == null ? CurrentCity : CurrentCity.BelongCity;
                CurrentCity = BelongCity;
                BelongCity.wildPersons.Add(this);

#if SANGO_DEBUG
                Sango.Log.Info($"@人才@[{BelongForce.Name}]的<{Name}>下野至{BelongCity.Name}");
#endif
            }

            BelongCorps = null;
            BelongForce = null;
            BelongTroop = null;
        }

        public Person BeCaptive(City city, bool breakCircal = false)
        {
            if (!breakCircal)
                city.AddCaptive(this, true);
#if SANGO_DEBUG
            Sango.Log.Info($"@人才@[{Name}]被<{city.BelongForce.Name}>俘虏至{city.Name}");
#endif
            return this;
        }

        public Person BeCaptive(Troop troop)
        {
            troop.AddCaptive(this);
#if SANGO_DEBUG
            Sango.Log.Info($"@人才@[{Name}]被<{troop.BelongForce.Name}>俘虏至{troop.Name}");
#endif
            return this;
        }

        public Person Escape(EscapeType escapeType = EscapeType.None, SangoObject sangoObject = null)
        {
            if (!IsPrisoner)
            {
#if SANGO_DEBUG
                Sango.Log.Error($"不是囚犯,无法逃跑!");
#endif
                CurrentCity.RemoveCaptive(this);

                if (BelongTroop != null)
                {
                    BelongTroop.RemoveCaptive(this);
                }
                return this;
            }

            if (BelongTroop != null)
            {
                City currentCity = BelongTroop.CurrentCity;
                BelongTroop.RemoveCaptive(this);
                ChangeCurrentCity(currentCity);
                BelongTroop = null;
            }
            else
            {
                CurrentCity.RemoveCaptive(this);
            }

            if (BelongForce != null && BelongForce.IsAlive)
            {
                state = (int)PersonStateType.Normal;
                ChangeCity(BelongForce.CapitalCity);
                SetMission(MissionType.PersonReturn, BelongCity);
            }
            else
            {
                state = (int)PersonStateType.Unemployed;
                ChangeCity(CurrentCity);
            }

            // 根据逃出方式触发对应的事件
            if (escapeType == EscapeType.Escape)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@[{Name}]逃亡!");
#endif
                GameEvent.OnPersonEscape?.Invoke(this, BelongCity);
            }
            else if (escapeType == EscapeType.Released)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@[{Name}]被释放!");
#endif
                // 被释放的逻辑已经在PersonRecruit.ReleaseTarget中处理
                GameEvent.OnPersonRelease?.Invoke(this, sangoObject as Force);
            }
            else if (escapeType == EscapeType.TroopDestroyed)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@[{Name}]逃亡!");
#endif
                // 部队灭亡的情况可以在这里处理
                GameEvent.OnPersonEscape?.Invoke(this, BelongCity);
            }

            return this;
        }


        /// <summary>
        /// 获取经验
        /// </summary>
        /// <param name="add"></param>
        public void GainExp(int add)
        {
            Exp += add;
            if (Level.Next == null)
                return;
            while (Level.exp > 0)
            {
                if (Exp > Level.exp)
                {
                    if (Level.Next != null)
                    {
                        Exp = Level.exp - Exp;
                        Level = Level.Next;
                    }
                    else
                        break;
#if SANGO_DEBUG
                    Sango.Log.Info($"@个人@{Name}升级到{Level.Id}级");
#endif
                    GameEvent.OnPersonLevelUp?.Invoke(this);
                }
                else
                    break;
            }
        }

        public bool HasFeatrue(int id)
        {
            if (FeatureList == null || FeatureList.Count == 0) return false;
            return FeatureList.Contains(id);
        }

        public bool HasFeatrue(int[] ids)
        {
            if (FeatureList == null || FeatureList.Count == 0) return false;
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (FeatureList.Contains(ids[i])) return true;
            }
            return false;
        }

        public int Distance(Person other)
        {
            if (other == null) return 0;
            Cell cell = BelongTroop != null ? BelongTroop.cell : BelongCity.CenterCell;
            Cell otherCell = other.BelongTroop != null ? other.BelongTroop.cell : other.BelongCity.CenterCell;
            return cell.Distance(otherCell);
        }

        public int DistanceDays(Person other)
        {
            if (other == null) return 0;
            City otherCity = other.BelongTroop != null ? other.BelongTroop.cell.BelongCity : other.BelongCity;
            City thisCity = BelongTroop != null ? BelongTroop.cell.BelongCity : BelongCity;
            return otherCity.Distance(thisCity);
        }

        public int DistanceDays(City otherCity)
        {
            if (otherCity == null) return 0;
            City thisCity = BelongTroop != null ? BelongTroop.cell.BelongCity : (BelongCity == null ? CurrentCity : BelongCity);
            return otherCity.Distance(thisCity);
        }

        public int CompatibilityDistance(Person other)
        {
            if (other == null) return 0;
            return System.Math.Abs(compatibility - (other.compatibility));
        }

        public bool IsLike(Person other)
        {
            if (other == null || LikePersonList == null) return false;
            return LikePersonList.Contains(other);
        }

        public bool IsHate(Person other)
        {
            if (other == null || HatePersonList == null) return false;
            return HatePersonList.Contains(other);
        }

        public bool IsBrother(Person other)
        {
            if (other == null || BrotherList == null) return false;
            return BrotherList.Contains(other);
        }

        public bool IsParentchild(Person other)
        {
            if (other == null) return false;
            if (other.Father == this) return true;
            if (other.Mother == this) return true;
            if (Father == other) return true;
            if (Mother == other) return true;
            return false;
        }

        public void Dead()
        {
            state = (int)PersonStateType.Dead;
            if (BelongCity != null)
            {
                BelongCity.allPersons.Remove(this);
                BelongCity.wildPersons.Remove(this);
            }

            if (IsPrisoner)
            {
                if (BelongTroop != null)
                {
                    BelongTroop.captiveList.Remove(this);
                }
                else
                    BelongCity.captiveList.Remove(this);
            }
            else if (BelongTroop != null)
            {
                BelongTroop.RemovePerson(this);
            }
        }

        public int GetAttribute(int attrType)
        {
            switch (attrType)
            {
                case 0:// (int)AttributeType.Command:
                    return Command;
                case 1:// (int)AttributeType.Strength:
                    return Strength;
                case 2:// (int)AttributeType.Intelligence:
                    return Intelligence;
                case 3:// (int)AttributeType.Politics:
                    return Politics;
                case 4:// (int)AttributeType.Glamour:
                    return Glamour;
            }
            return 0;
        }

        /// <summary>
        /// 获取装备的属性加成
        /// </summary>
        /// <param name="getBonus">获取单个装备加成的委托</param>
        /// <returns>总加成值</returns>
        private int GetEquipmentBonus(System.Func<Equipment, int> getBonus)
        {
            int bonus = 0;

            if (EquippedWeapon != null)
            {
                bonus += getBonus(EquippedWeapon);
            }

            if (EquippedHorse != null)
            {
                bonus += getBonus(EquippedHorse);
            }

            if (EquippedArmor != null)
            {
                bonus += getBonus(EquippedArmor);
            }

            return bonus;
        }

        /// <summary>
        /// 装备武器
        /// </summary>
        /// <param name="weapon">武器</param>
        public void EquipWeapon(Equipment weapon)
        {
            if (weapon != null && weapon.kind == (int)ItemKindType.Equipment_Weapon)
            {
                EquippedWeapon = weapon;
            }
        }

        /// <summary>
        /// 装备马
        /// </summary>
        /// <param name="horse">马</param>
        public void EquipHorse(Equipment horse)
        {
            if (horse != null && horse.kind == (int)ItemKindType.Equipment_Horse)
            {
                EquippedHorse = horse;
            }
        }

        /// <summary>
        /// 装备铠甲
        /// </summary>
        /// <param name="armor">铠甲</param>
        public void EquipArmor(Equipment armor)
        {
            if (armor != null && armor.kind == (int)ItemKindType.Equipment_Armor)
            {
                EquippedArmor = armor;
            }
        }

        /// <summary>
        /// 卸下武器
        /// </summary>
        public void UnequipWeapon()
        {
            EquippedWeapon = null;
        }

        /// <summary>
        /// 卸下马
        /// </summary>
        public void UnequipHorse()
        {
            EquippedHorse = null;
        }

        /// <summary>
        /// 卸下铠甲
        /// </summary>
        public void UnequipArmor()
        {
            EquippedArmor = null;
        }

        public void UpgradeOfficial(Official official)
        {
            if (official == null) return;

            Official last = Official;
            last.OnPersonRemove(this);
            int need = Official.meritNeeds;
            Official = official;
            Official.OnPersonAdd(this);
            merit -= need;
#if SANGO_DEBUG
            Sango.Log.Info($"@个人@{Name}官职升到[{Official.Name}]!!");
#endif
            GameEvent.OnPersonUpgradeOfficial?.Invoke(this, last);
        }

        public static Person FormLib(PersonLib personLib)
        {
            Person person = new Person();
            person.Id = personLib.Id;
            person.image = personLib.image;
            person.image_old = personLib.image_old;
            return person;
        }
    }
}
