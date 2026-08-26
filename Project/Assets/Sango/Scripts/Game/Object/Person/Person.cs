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
    public class Person : SangoObjectExtensionData
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Person; } }

        public string ColorName => $"<color=#7CCADB>{Name}</color>";

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty]
        public int BelongForce;
        public Force mBelongForce { get; set; }

        public bool IsPlayer => mBelongForce?.IsPlayer ?? false;
        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => mBelongCorps?.IsPlayerControl ?? false;
        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => mBelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// 所属军团
        /// </summary>
        [JsonProperty]
        public int BelongCorps;

        public Corps mBelongCorps { get; set; }

        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonProperty]
        public int BelongCity;

        public City mBelongCity { get; set; }

        /// <summary>
        /// 所在城池
        /// </summary>
        [JsonProperty]
        public int CurrentCity;

        public City mCurrentCity { get; set; }

        /// <summary>
        /// 所属部队
        /// </summary>
        [JsonProperty]
        public int BelongTroop;

        public Troop mTroop { get; set; }

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
        [JsonProperty] public string description;

        /// <summary>
        /// 头像id
        /// </summary>
        [JsonProperty] public int headIconID;

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
        /// 性别 0男,1女
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
                //Sango.Log.Info($"{Name}改变状态=> {PersonSortFunction.SortByState.GetValueStr(this)}");
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
        public int personality;

        public Personality mPersonality;

        /// <summary>
        /// 义理
        /// </summary>
        [JsonProperty]
        public int argumentation;

        public Argumentation mArgumentation;

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
        /// 汉室态度
        /// </summary>
        [JsonProperty]
        public int kanshitsu;

        /// <summary>
        /// 理想
        /// </summary>
        [JsonProperty]
        public int ideal;

        /// <summary>
        /// 才幹
        /// </summary>
        [JsonProperty]
        public int talent;

        /// <summary>
        /// 父亲
        /// </summary>
        [JsonProperty]
        public int Father;
        public Person mFather { get; set; }

        /// <summary>
        /// 母亲
        /// </summary>
        [JsonProperty]
        public int Mother;
        public Person mMother { get; set; }

        /// <summary>
        /// 配偶
        /// </summary>
        [JsonProperty]
        public int[] SpouseList;

        public SangoObjectList<Person> mSpouseList { get; private set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        [JsonProperty]
        public int Brother;

        public Person mBrother { get; set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        public List<Person> BrotherList;

        /// <summary>
        /// 喜欢武将
        /// </summary>
        [JsonProperty]
        public int[] LikePersonList;

        public SangoObjectList<Person> mLikePersonList { get; set; }

        /// <summary>
        /// 厌恶武将
        /// </summary>
        [JsonProperty]
        public int[] HatePersonList;
        public SangoObjectList<Person> mHatePersonList { get; set; }

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
        public int[] FeatureList;

        public SangoObjectList<Feature> mFeatureList { get; set; }

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

        public bool IsLeader => state == (int)PersonStateType.Leader;
        public bool IsCommander => state == (int)PersonStateType.Commander;
        public bool IsGovernor => state == (int)PersonStateType.Governor;

        public void SetStateNormal() { state = (int)PersonStateType.Normal; }
        public void SetStateLeader()
        {
            if (IsGovernor) return;
            if (IsCommander) return;
            state = (int)PersonStateType.Leader;
        }
        public void SetStateCommander()
        {
            if (IsGovernor) return;
            state = (int)PersonStateType.Commander;
        }

        public override bool ActionOver
        {
            get => base.ActionOver;

            set
            {
                if (value == true)
                {
                    if (base.ActionOver != value)
                    {
                        GameEvent.OnPersonActionOver?.Invoke(this);
                    }
                }

                base.ActionOver = value;
            }
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
        /// 带兵上限,根据官职和国家科技决定ui 
        /// </summary>
        public int TroopsLimit
        {
            //TODO: 增加国家科技加持
            get { return Math.Max(IsGovernor ? 15000 : 0, Official.troopsLimit) + Level.troops + troopsLimitExtra; }
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
                command.Update(Age, scenario); strength.Update(Age, scenario); intelligence.Update(Age, scenario); politics.Update(Age, scenario); glamour.Update(Age, scenario);
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

        /// <summary>
        /// 在野回合数
        /// </summary>
        [JsonProperty] public int wildTurnCount;

        public bool rewardOver;

        public int Age { get; private set; }

        /// <summary>
        /// 是否空闲
        /// </summary>
        public bool IsFree { get { return mTroop == null && missionType == (int)MissionType.None && !IsPrisoner && !IsDead; } }

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
            return IsAlliance(mBelongForce, other.mBelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(mBelongForce, other.mBelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(mBelongForce, other.mBelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(mBelongForce, other.mBelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(mBelongForce, other.mBelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(mBelongForce, other.mBelongForce);
        }

        public bool IsSameForce(Person other)
        {
            return IsSameForce(mBelongForce, other.mBelongForce);
        }

        /// <summary>
        /// 所有的武将情况归属,全由武将决定,城池不再记录任何武将归属情况
        /// </summary>
        /// <param name="scenario"></param>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            mBelongForce = scenario.Id2Object(scenario.forceSet, BelongForce);
            mBelongCorps = scenario.Id2Object(scenario.corpsSet, BelongCorps);
            mBelongCity = scenario.Id2Object(scenario.citySet, BelongCity);
            mCurrentCity = scenario.Id2Object(scenario.citySet, CurrentCity);
            mTroop = scenario.Id2Object(scenario.troopsSet, BelongTroop);

            if (personality <= 0)
                personality = 1;
            mPersonality = scenario.Id2Object(scenario.CommonData.Personalities, personality);

            if (argumentation <= 0)
                argumentation = 2;
            mArgumentation = scenario.Id2Object(scenario.CommonData.Argumentations, argumentation);

            //if (attributeChangeType <= 0)
            //    attributeChangeType = 5;
            //mAttributeChangeType = scenario.Id2Object(scenario.CommonData.AttributeChangeTypes, attributeChangeType);

            mFather = scenario.Id2Object(scenario.personSet, Father);
            mMother = scenario.Id2Object(scenario.personSet, Mother);
            mBrother = scenario.Id2Object(scenario.personSet, Brother);

            mSpouseList = scenario.Array2ObjectList(scenario.personSet, SpouseList);
            mLikePersonList = scenario.Array2ObjectList(scenario.personSet, LikePersonList);
            mHatePersonList = scenario.Array2ObjectList(scenario.personSet, HatePersonList);
            mFeatureList = scenario.Array2ObjectList(scenario.CommonData.Features, FeatureList);

            if (!scenario.Variables.AgeEnabled || !scenario.Variables.EnableAgeAbilityFactor)
            {
                command.UpdateNoAge();
                strength.UpdateNoAge();
                intelligence.UpdateNoAge();
                politics.UpdateNoAge();
                glamour.UpdateNoAge();
            }

            // 处理义兄弟
            if (mBrother != null)
            {
                if (mBrother.BrotherList == null)
                    mBrother.BrotherList = new List<Person>();

                mBrother.BrotherList.Add(this);
            }

            if (IsAlive)
            {
                switch ((PersonStateType)state)
                {
                    // 处理主公
                    case PersonStateType.Governor:
                        if (mBelongCity != null)
                        {
                            mBelongCity.allPersons.Add(this);
                            mBelongCity.NeedUpdateLeader();
                        }
                        break;
                    // 军团长
                    case PersonStateType.Commander:
                        if (mBelongCity != null)
                        {
                            mBelongCity.allPersons.Add(this);
                            mBelongCity.NeedUpdateLeader();
                        }
                        break;
                    // 太守
                    case PersonStateType.Leader:
                        if (mBelongCity != null)
                        {
                            mBelongCity.allPersons.Add(this);
                            mBelongCity.NeedUpdateLeader();
                        }
                        break;
                    // 一般武将
                    case PersonStateType.Normal:
                        if (mBelongCity != null)
                        {
                            mBelongCity.allPersons.Add(this);
                            if (mBelongForce != mBelongCity.mBelongForce || mBelongCorps != mBelongCity.mBelongCorps)
                            {
                                Sango.Log.Error($"[{Id}]{Name}归属force:{mBelongForce?.Name} corps:{mBelongCorps?.Name}, 但在city[{mBelongCity?.Name}] force:{mBelongCity.mBelongForce?.Name} corps:{mBelongCity.mBelongCorps?.Name}");
                                mBelongForce = mBelongCity.mBelongForce;
                                mBelongCorps = mBelongCity.mBelongCorps;
                            }
                        }
                        break;
                    // 在野
                    case PersonStateType.Unemployed:
                        mCurrentCity.wildPersons.Add(this);
                        break;
                    // 囚犯
                    case PersonStateType.Prisoner:
                        // 囚犯只有currentCity
                        if (mCurrentCity.IsSameForce(this))
                        {
                            // 修复一下
                            mBelongCity = mCurrentCity;
                            mBelongCity.allPersons.Add(this);
                        }
                        else
                        {
                            // 准备俘虏
                            if (mBelongForce != null)
                                mBelongForce.BeCaptiveList.Add(this);

                            if (mTroop != null)
                                mTroop.captiveList.Add(this);
                            else
                                mCurrentCity.captiveList.Add(this);
                        }
                        break;
                    // 未登场
                    case PersonStateType.Invalid:
                        break;
                    // 未发现
                    case PersonStateType.Invisible:
                        if (mCurrentCity != null)
                            mCurrentCity.invisiblePersons.Add(this);
                        else if (mBelongCity != null)
                        {
                            mCurrentCity = mBelongCity;
                            mBelongCity.invisiblePersons.Add(this);
                        }
                        break;
                    // 死亡
                    case PersonStateType.Dead:
                        break;
                }
            }

            // 处理父亲
            if (mFather != null)
                mFather.sonList.Add(this);

            if (mMother != null)
                mMother.sonList.Add(this);

            OnPersonAgeUpdate(scenario);

            spearLv.Update();
            halberdLv.Update();
            crossbowLv.Update();
            rideLv.Update();
            waterLv.Update();
            machineLv.Update();

            if (Official == null)
                Official = scenario.CommonData.Officials[0];

            Official.OnPersonAdd(this);

            if (Level == null)
                Level = scenario.CommonData.PersonLevels[0];
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            BelongForce = mBelongForce?.Id ?? 0;
            BelongCorps = mBelongCorps?.Id ?? 0;
            BelongCity = mBelongCity?.Id ?? 0;
            CurrentCity = mCurrentCity?.Id ?? 0;
            BelongTroop = mTroop?.Id ?? 0;
            personality = mPersonality?.Id ?? 0;
            argumentation = mArgumentation?.Id ?? 0;

            Father = mFather?.Id ?? 0;
            Mother = mMother?.Id ?? 0;
            Brother = mBrother?.Id ?? 0;

            SpouseList = mSpouseList?.ToArray() ?? null;
            LikePersonList = mLikePersonList?.ToArray() ?? null;
            HatePersonList = mHatePersonList?.ToArray() ?? null;
            FeatureList = mFeatureList?.ToArray() ?? null;
        }

        public override void Init(Scenario scenario)
        {
            base.Init(scenario);

            if (mBrother != null)
            {
                if (mBrother == this)
                {
                    BrotherList.Sort(SangoObject.Compare);

                }
                else
                {
                    BrotherList = mBrother.BrotherList;
                }
            }

            if (IsPrisoner && mCurrentCity.IsSameForce(this))
            {
                if (mBelongForce != null)
                {
                    mBelongForce.BeCaptiveList.Remove(this);
                    state = (int)PersonStateType.Normal;
                    mCurrentCity.allPersons.Remove(this);
                    mCurrentCity.allPersons.Add(this);
                    mCurrentCity.freePersons.Remove(this);
                    mCurrentCity.freePersons.Add(this);
                }
                else
                {
                    state = (int)PersonStateType.Unemployed;
                    mCurrentCity.wildPersons.Remove(this);
                    mCurrentCity.wildPersons.Add(this);
                }
                mCurrentCity.captiveList.Remove(this);
            }

        }

        public override bool OnYearStart(Scenario scenario)
        {
            if (IsDead) return true;

            OnPersonAgeUpdate(scenario);

            if (state == (int)PersonStateType.Invalid)
            {
                if (scenario.Variables.allowInvalidPersonValidWhenYearPass)
                {
                    //出场年
                    if (appearance > 0 && appearance <= scenario.Info.year)
                    {
                        state = (int)PersonStateType.Invisible;

                        City city = null;
                        if (birthplace > 0)
                        {
                            Province prov = scenario.CommonData.Provinces[birthplace];
                            city = prov.RandomBelongCity(scenario);
                        }

                        if (city == null)
                            city = scenario.citySet.RandomGet();

                        // 这里要处理登场城池
                        city.invisiblePersons.Add(this);
                        mCurrentCity = city;
                    }
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
                                x.mCurrentCity = mCurrentCity;
                                x.state = (int)PersonStateType.Invisible;
                                mCurrentCity.invisiblePersons.Add(this);
                            }
                        }
                    });
                }
            }

            return base.OnYearStart(scenario);
        }

        public bool DoMove(City dest, Scenario scenario)
        {
            City target = dest.mBelongCity == null ? dest : dest.mBelongCity;
            City currentCity = mCurrentCity.mBelongCity == null ? mCurrentCity : mCurrentCity.mBelongCity;

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
                            if (mBelongForce != null)
                            {
                                SetMission(MissionType.PersonReturn, mBelongCity);
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
                        if (mBelongCorps != null && this.IsSameForce(dest_person))
                        {
                            // 已经有人招募成功
                            SetMission(MissionType.PersonReturn, mBelongCity);
                            return;
                        }

                        if (DoMove(dest, scenario))
                        {
                            ClearMission();
                            CityRecruitPersonEvent te = RenderEvent.Instance.Create<CityRecruitPersonEvent>();
                            te.Init(this, dest_person);
                            RenderEvent.Instance.Add(te);
                            SetMission(MissionType.PersonReturn, mBelongCity);
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
                            mBelongCity.DoJobCreateBoat(itemType, buildingId, totalValue);
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
                            mBelongCity.DoJobCreateMachine(itemType, buildingId, totalValue);
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
                case (int)MissionType.PersonBuild:
                    {
                        Building target = scenario.GetObject<Building>(missionTarget);
                        if (target == null || !target.IsAlive || !target.IsSameForce(this) || (target.isComplate && !target.isUpgrading))
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
                                SetMission(MissionType.PersonReturn, mBelongCity);
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
                            SetMission(MissionType.PersonReturn, mBelongCity);
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
            if (state == (int)PersonStateType.Invalid)
            {
                if (scenario.Variables.allowInvalidPersonValidWhenYearPass)
                {
                    //出场年
                    if (appearance > 0 && appearance <= scenario.Info.year && GameRandom.Chance(10))
                    {
                        state = (int)PersonStateType.Invisible;

                        City city = null;
                        if (birthplace > 0)
                        {
                            Province prov = scenario.CommonData.Provinces[birthplace];
                            city = prov.RandomBelongCity(scenario);
                        }

                        if (city == null)
                            city = scenario.citySet.RandomGet();

                        // 这里要处理登场城池
                        city.invisiblePersons.Add(this);
                        mCurrentCity = city;

                        RenderEvent.Instance.Add(new PersonValidEvent()
                        {
                            province = city.province,
                            person = this
                        });

                    }
                }
            }
            return base.OnTurnStart(scenario);
        }
        public override bool OnForceTurnStart(Scenario scenario)
        {
            if (mBelongForce != null && IsAlive)
            {
                mBelongForce.GainHegemonyPoint(1);
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
                            x.mBelongForce = mBelongForce;
                            x.mBelongCorps = mBelongCorps;

                            City becameCity = mBelongCity;
                            if (IsPrisoner)
                            {
                                becameCity = mBelongForce.CapitalCity;
                            }
                            x.mBelongCity = becameCity;
                            x.mCurrentCity = becameCity;
                            becameCity.allPersons.Add(x);
                            becameCity.freePersons.Add(x);
                            x.state = (int)PersonStateType.Normal;

                            if (IsPlayer)
                            {
                                RenderEvent.Instance.Add(new PersonGrowupEvent()
                                {
                                    father = this,
                                    person = x
                                });
                            }
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
            if (IsWild || state == (int)PersonStateType.Invisible)
            {
                wildTurnCount++;
                stayTurnCount++;
                if (stayTurnCount > 5 && GameRandom.Chance(5)) // 10%概率
                {
                    if (IsWild)
                        mCurrentCity.wildPersons.Remove(this);
                    else
                        mCurrentCity.invisiblePersons.Remove(this);

                    //如果在港关,移动到所属城市
                    if (!mCurrentCity.IsCity())
                    {
                        City targetCity = mCurrentCity.mBelongCity;
                        mCurrentCity.RemoveWildPerson(this);
                        // 移动到新城市
                        ChangeCurrentCity(targetCity);
                        mBelongCity = targetCity;

                        // 重置停留时间
                        stayTurnCount = 0;
#if SANGO_DEBUG
                        Sango.Log.Info($"@人才@在野武将{Name}从{mBelongCity.Name}移动到{targetCity.Name}");
#endif
                    }
                    else
                    {
                        // 随机选择一个邻接城市
                        SangoObjectList<City> neighborCities = mBelongCity.NeighborList;
                        if (neighborCities.Count > 0)
                        {
                            int randomIndex = GameRandom.Range(neighborCities.Count);
                            City targetCity = neighborCities[randomIndex];
                            if (targetCity != null)
                            {
                                // 移动到新城市
                                ChangeCurrentCity(targetCity);
                                mBelongCity = targetCity;

                                // 重置停留时间
                                stayTurnCount = 0;
#if SANGO_DEBUG
                                Sango.Log.Info($"@人才@在野武将{Name}从{mBelongCity.Name}移动到{targetCity.Name}");
#endif
                            }
                        }
                    }

                    if (IsWild)
                        mCurrentCity.wildPersons.Add(this);
                    else
                        mCurrentCity.invisiblePersons.Add(this);
                }
            }
            else
            {
                wildTurnCount = 0;
            }

            UpdateMission(scenario);
            return base.OnTurnEnd(scenario);
        }

        public void OnWillBeCaptive()
        {
            // 军师被捕
            if (mBelongForce != null)
            {
                if (mBelongForce.mCounsellor == this)
                {
                    mBelongForce.mCounsellor = null;
                }
            }

            if (IsGovernor)
            {

            }
            else if (IsCommander)
            {
                mBelongCorps.mComander = null;
                mBelongCorps.NeedUpdateCommander();
                mBelongCity.Leader = null;
                mBelongCity.NeedUpdateLeader();
            }
            else if (IsLeader)
            {
                mBelongCity.Leader = null;
                mBelongCity.NeedUpdateLeader();
            }

        }

        public void OnWillChangeToCity(City dest)
        {
            // 如果转移主公到其他军团城市,需要解散目标军团
            if (IsGovernor && dest.mBelongCorps != mBelongCorps)
            {
                Corps corps = dest.mBelongCorps;
                dest.ChangeCorps(mBelongCorps);
                dest.UpdateCorps();
                dest.Render?.UpdateRender();
                corps.RemoveCity(dest);
                mBelongCity.NeedUpdateLeader();
                dest.NeedUpdateLeader();
            }
            else if (IsCommander)
            {
                if (dest.mBelongCorps != mBelongCorps)
                {
                    SetStateNormal();
                    mBelongCorps.NeedUpdateCommander();
                }
                else
                {
                    dest.NeedUpdateLeader();
                }
                mBelongCity.NeedUpdateLeader();
            }
            else if (IsLeader)
            {
                SetStateNormal();
                mBelongCity.NeedUpdateLeader();
            }
        }

        public void TransformToCity(City dest)
        {
            OnWillChangeToCity(dest);

            City lastCity = mBelongCity;
            ChangeBelongCity(dest);
            //dest.AddPerson(this);
            SetMission(MissionType.PersonReturn, dest);

            ActionOver = true;
#if SANGO_DEBUG
            Sango.Log.Info($"*{mBelongForce?.Name}的{Name}从{mBelongCity.Name}向{dest.Name}转移*");
#endif
        }

        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;
            if (mBelongCorps != corps)
            {
                last = mBelongCorps;
                mBelongCorps = corps;
                if (mBelongForce != corps.mBelongForce)
                {
                    mBelongForce = corps.mBelongForce;
                }
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
            City last = mCurrentCity;
            mCurrentCity = city;
#if SANGO_DEBUG
            Sango.Log.Info($"*{mBelongForce?.Name}的{Name} 改变所在城市 {last.Name} -> {city.Name}");
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
            City last = null;
            if (mBelongCity != city)
            {
                last = mBelongCity;
#if SANGO_DEBUG
                Sango.Log.Info($"*{mBelongForce?.Name}的{Name} 改变所属城市 {mBelongCity?.Name} => {city.Name}");
#endif
                if (!IsWild)
                {
                    mBelongCity?.RemovePerson(this);
                    city.AddPerson(this);
                    mBelongCity = city;
                    if (mBelongCorps != city.mBelongCorps)
                        mBelongCorps = city.mBelongCorps;
                    if (mBelongForce != city.mBelongForce)
                        mBelongForce = city.mBelongForce;
                }
                else
                {
                    mBelongCity?.RemoveWildPerson(this);
                    city.AddWildPerson(this);
                    mBelongCity = city;
                }

                mTroop?.OnPersonChangeCity(this, last, city);
            }
            return last;
        }


        public bool JobRecruitPerson(Person person, City targetCity, int type)
        {
            int probability = GameFormula.Instance.RecruitPersonProbability(this, person, type);
#if SANGO_DEBUG
            Sango.Log.Info($"[{mBelongForce.Name}]<{Name}>登庸 -> {person.Name} 成功率:{probability}");
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
            mBelongForce?.GainTechniquePoint(techniquePointGain);
            ActionOver = true;
            return success;
        }

        public bool JobRecruitPerson(Person person, int type)
        {
            return JobRecruitPerson(person, mBelongCity, type);
        }

        public void BeRecruit(Person person, City targetCity)
        {
#if SANGO_DEBUG
            Sango.Log.Info($"[{person.mBelongForce.Name}]<{person.Name}>登庸成功, {Name}加入了势力{person.mBelongForce.Name}");
#endif
            loyalty = 80;
            if (IsPrisoner)
            {
                mBelongForce?.BeCaptiveList.Remove(this);
                // 囚犯从监牢中移除
                if (mTroop != null)
                    mTroop.RemoveCaptive(this);
                else
                    mCurrentCity.RemoveCaptive(this);
                state = (int)PersonStateType.Normal;
                JoinToForce(targetCity);
                SetMission(MissionType.PersonReturn, targetCity);
            }
            else
            {
                if (IsWild)
                    mCurrentCity.RemoveWildPerson(this);
                else if (Invisible)
                    mCurrentCity.RemoveInvisiblePerson(this);
                else
                    mBelongCity?.RemovePerson(this);

                // 部队中
                if (mTroop != null)
                {
                    Troop troop = mTroop;
                    // 部队主将
                    if (this == mTroop.Leader)
                    {
                        mTroop.JoinToForce(targetCity);
                        mTroop.ActionOver = true;
                    }
                    else
                    {
                        mTroop.RemovePerson(this);
                        ChangeCurrentCity(troop.mCurrentCity);
                        JoinToForce(targetCity);
                        SetMission(MissionType.PersonReturn, targetCity);
                        troop.ResetActionAndStatus();
                    }
                    troop.Render?.UpdateRender();
                }
                else
                {
                    // 有归属
                    JoinToForce(targetCity);
                    SetMission(MissionType.PersonReturn, targetCity);
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
            bool isSameCity = mCurrentCity == city;
            mBelongCity = city;
            mBelongCorps = city.mBelongCorps;
            mBelongForce = city.mBelongForce;
            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;
            state = (int)PersonStateType.Normal;
            mBelongCity.AddPerson(this);
            return isSameCity;
        }

        /// <summary>
        /// 下野
        /// </summary>
        public void LeaveToWild()
        {
            if (IsCommander)
            {
                mBelongCorps.mComander = null;
                mBelongCorps.NeedUpdateCommander();
            }

            if (IsLeader)
            {
                mBelongCity.Leader = null;
                mBelongCity.NeedUpdateLeader();
            }

            workingBuilding = null;
            loyalty = 0;
            mBelongCity?.RemovePerson(this);
            mCurrentCity?.RemovePerson(this);
            UpgradeOfficial(Scenario.Cur.CommonData.Officials.Get(0));
            merit = 0;
            mBelongCity = mCurrentCity.mBelongCity == null ? mCurrentCity : mCurrentCity.mBelongCity;
            if (IsPrisoner)
            {
                mBelongForce?.BeCaptiveList.Remove(this);
                mCurrentCity.captiveList.Remove(this);
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@<{Name}>失去势力,进入囚犯下野状态");
#endif
            }
            else
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@[{mBelongForce.Name}]的<{Name}>下野至{mBelongCity.Name}");
#endif
            }
            state = (int)PersonStateType.Unemployed;
            mCurrentCity = mBelongCity;
            mBelongCity.wildPersons.Add(this);

            mBelongCorps = null;
            mBelongForce = null;
            mTroop = null;
        }

        public Person Escape(EscapeType escapeType = EscapeType.None, SangoObject sangoObject = null)
        {
            if (!IsPrisoner)
            {
                Sango.Log.Error($"不是囚犯,无法逃跑!");
                mCurrentCity.RemoveCaptive(this);
                if (mTroop != null)
                    mTroop.RemoveCaptive(this);
                return this;
            }

            // 在部队中
            if (mTroop != null)
            {
                City currentCity = mTroop.mCurrentCity;
                mTroop.RemoveCaptive(this);
                ChangeCurrentCity(currentCity);
                mTroop = null;
            }
            else
            {
                mCurrentCity.RemoveCaptive(this);
            }

            if (mBelongForce != null && mBelongForce.IsAlive)
            {
                state = (int)PersonStateType.Normal;
                ChangeBelongCity(mBelongForce.CapitalCity);
                SetMission(MissionType.PersonReturn, mBelongCity);
            }
            else
            {
                state = (int)PersonStateType.Unemployed;
                ChangeBelongCity(mCurrentCity);
            }

            // 根据逃出方式触发对应的事件
            if (escapeType == EscapeType.Escape)
            {
#if SANGO_DEBUG
                Sango.Log.Info($"@人才@[{Name}]逃亡!");
#endif
                GameEvent.OnPersonEscape?.Invoke(this, mBelongCity);
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
                GameEvent.OnPersonEscape?.Invoke(this, mBelongCity);
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

        public void GainMerit(int m)
        {
            merit += m;
        }

        public bool HasFeatrue(int id)
        {
            if (mFeatureList == null || mFeatureList.Count == 0) return false;
            return mFeatureList.Contains(id);
        }

        public bool HasFeatrue(int[] ids)
        {
            if (mFeatureList == null || mFeatureList.Count == 0) return false;
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (mFeatureList.Contains(ids[i])) return true;
            }
            return false;
        }

        public int Distance(Person other)
        {
            if (other == null) return 0;
            Cell cell = mTroop != null ? mTroop.cell : mBelongCity.CenterCell;
            Cell otherCell = other.mTroop != null ? other.mTroop.cell : other.mBelongCity.CenterCell;
            return cell.Distance(otherCell);
        }

        public int DistanceDays(Person other)
        {
            if (other == null) return 0;
            City otherCity = other.mTroop != null ? other.mTroop.cell.BelongCity : other.mBelongCity;
            City thisCity = mTroop != null ? mTroop.cell.BelongCity : mBelongCity;
            return otherCity.Distance(thisCity);
        }

        public int DistanceDays(City otherCity)
        {
            if (otherCity == null) return 0;
            City thisCity = mTroop != null ? mTroop.cell.BelongCity : (mBelongCity == null ? mCurrentCity : mBelongCity);
            return otherCity.Distance(thisCity);
        }

        public int CompatibilityDistance(Person other)
        {
            if (other == null) return 0;
            return System.Math.Abs(compatibility - (other.compatibility));
        }

        public bool IsLike(Person other)
        {
            if (other == null || mLikePersonList == null) return false;
            return mLikePersonList.Contains(other);
        }

        public bool IsHate(Person other)
        {
            if (other == null || mHatePersonList == null) return false;
            return mHatePersonList.Contains(other);
        }

        public bool IsBrother(Person other)
        {
            if (other == null || BrotherList == null) return false;
            return BrotherList.Contains(other);
        }

        public bool IsParentchild(Person other)
        {
            if (other == null) return false;
            if (other.mFather == this) return true;
            if (other.mMother == this) return true;
            if (mFather == other) return true;
            if (mMother == other) return true;
            return false;
        }

        public void Dead()
        {
            state = (int)PersonStateType.Dead;
            if (mBelongCity != null)
            {
                mBelongCity.allPersons.Remove(this);
                mBelongCity.freePersons.Remove(this);
                mBelongCity.wildPersons.Remove(this);
            }

            if (IsPrisoner)
            {
                if (mTroop != null)
                {
                    mTroop.captiveList.Remove(this);
                }
                else
                    mBelongCity.captiveList.Remove(this);
            }
            else if (mTroop != null)
            {
                mTroop.RemovePerson(this);
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

        public bool IsHighStength()
        {
            return strength.baseValue > command.baseValue && strength.baseValue > intelligence.baseValue
                && strength.baseValue > politics.baseValue;
        }

        public string GetDescription()
        {
            if (!string.IsNullOrEmpty(description) && description != "0")
                return description;
            return GameLanguage.GetString(Id);
        }

        public static Person FormLib(PersonLib personLib)
        {
            Person person = new Person();
            person.Id = personLib.Id;
            person.image = personLib.image;
            person.image_old = personLib.image_old;
            return person;
        }
        private static int[] CloneArray(int[] source)
        {
            if (source == null) return new int[0];
            return (int[])source.Clone();
        }
        public static Person FormLib2(PersonLib personLib)
        {
            Person person = new Person();
            // 深拷贝:字段名字与类型都一致的直接拷贝,不一致的字段放弃
            //person.Id = personLib.Id;
            person.Name = personLib.familyName + personLib.giveName;
            person.familyNameID = personLib.familyNameID;
            person.familyName = personLib.familyName;
            person.description = personLib.description;
            person.giveNameID = personLib.giveNameID;
            person.giveName = personLib.giveName;
            person.nickNameID = personLib.nickNameID;
            person.nickName = personLib.nickName;
            person.headIconID = personLib.headIconID;
            person.imageID = personLib.imageID;
            person.image = personLib.image;
            person.image_old = personLib.image_old;
            person.sex = personLib.sex;
            person.yearBorn = personLib.yearBorn;
            person.yearDead = personLib.yearDead;
            person.compatibility = personLib.compatibility & 0xFF;
            person.state = personLib.state;
            person.voice = personLib.voice;
            person.tone = personLib.tone;
            person.kanshitsu = personLib.kanshitsu;
            person.ideal = personLib.ideal;
            person.talent = personLib.talent;
            person.merit = personLib.merit;
            person.stamina = personLib.stamina;
            person.Exp = personLib.Exp;
            person.consanguinity = personLib.consanguinity;
            person.command.baseValue = personLib.command;
            person.command.changeId = personLib.attributeChangeType;
            person.strength.baseValue = personLib.strength;
            person.strength.changeId = personLib.attributeChangeType;
            person.intelligence.baseValue = personLib.intelligence;
            person.intelligence.changeId = personLib.attributeChangeType;
            person.politics.baseValue = personLib.politics;
            person.politics.changeId = personLib.attributeChangeType;
            person.glamour.baseValue = personLib.glamour;
            person.glamour.changeId = personLib.attributeChangeType;
            person.spearLv.baseValue = personLib.spearLv;
            person.halberdLv.baseValue = personLib.halberdLv;
            person.crossbowLv.baseValue = personLib.crossbowLv;
            person.rideLv.baseValue = personLib.rideLv;
            person.waterLv.baseValue = personLib.waterLv;
            person.machineLv.baseValue = personLib.machineLv;
            person.personality = personLib.personality;
            person.argumentation = personLib.argumentation;

            person.Father = personLib.Father;
            person.Mother = personLib.Mother;
            person.Brother = personLib.Brother;

            person.SpouseList = CloneArray(personLib.SpouseList);
            person.LikePersonList = CloneArray(personLib.LikePersonList);
            person.HatePersonList = CloneArray(personLib.LikePersonList);
            person.FeatureList = CloneArray(personLib.FeatureList);


            return person;
        }
    }
}
