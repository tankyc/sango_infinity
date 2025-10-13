using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Person : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Person; } }

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Force>))]
        public Force BelongForce { get; set; }

        /// <summary>
        /// 所属势力
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
        public int descriptionID;
        [JsonProperty] public string description;

        /// <summary>
        /// 头像id
        /// </summary>
        [JsonProperty] public int headIconID;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public int imageID;

        /// <summary>
        /// 性别
        /// </summary>
        [JsonProperty] public int sex;

        /// <summary>
        /// 登场年份
        /// </summary>
        [JsonProperty] public int yearAvailable;

        /// <summary>
        /// 是否被发现
        /// </summary>
        [JsonProperty] public bool beFinded;

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
        [JsonProperty] public int state;

        /// <summary>
        /// 官职
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Official>))]
        [JsonProperty]
        public Official Official { get; set; }

        /// <summary>
        /// 忠诚
        /// </summary>
        [JsonProperty] public int loyalty;

        /// <summary>
        /// 功绩
        /// </summary>
        [JsonProperty] public int merit;

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
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> BrotherList { get; private set; }

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
        public PersonAbilityValue horseLv = new PersonAbilityValue();

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
        [JsonConverter(typeof(SangoObjectListIDConverter<Feature>))]
        [JsonProperty]
        public SangoObjectList<Feature> FeatureList;

        public int SpearLv => spearLv.value;
        public int HalberdLv => halberdLv.value;
        public int CrossbowLv => crossbowLv.value;
        public int HorseLv => horseLv.value;
        public int WaterLv => waterLv.value;
        public int MachineLv => machineLv.value;

        public int Command => command.value;
        public int Strength => strength.value;
        public int Intelligence => intelligence.value;
        public int Politics => politics.value;
        public int Glamour => glamour.value;

        /// <summary>
        /// 是否可登场
        /// </summary>
        public virtual bool IsValid => Age >= 16;


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
            get { return Official.troopsLimit + Level.troops + troopsLimitExtra; }
        }

        /// <summary>
        /// 军事能力
        /// </summary>
        public int MilitaryAbility
        {
            get { return Command + Strength + Intelligence; }
        }

        /// <summary>
        /// 商业能力
        /// </summary>
        public int BaseCommerceAbility
        {
            get
            {
                return ((Politics * 70 * 42 / 10000 + Intelligence * 20 * 42 / 10000 + Glamour * 10 * 42 / 10000));
            }
        }

        /// <summary>
        /// 巡视能力
        /// </summary>
        public int BaseSecurityAbility
        {
            get
            {
                return ((Intelligence + (2 * Politics)) + Glamour);
            }
        }

        /// <summary>
        /// 训练能力
        /// </summary>
        public int BaseTrainTroopAbility
        {
            get
            {
                return ((Command * 3 + (2 * Strength)) + Glamour) / 30;
            }
        }

        /// <summary>
        /// 农业能力
        /// </summary>
        public int BaseAgricultureAbility
        {
            get
            {
                return ((Politics * 80 * 42 / 10000 + Intelligence * 10 * 42 / 10000 + Glamour * 10 * 42 / 10000));
            }
        }

        /// <summary>
        /// 建设能力
        /// </summary>
        public int BaseBuildAbility
        {
            get
            {
                // 建设能力 = 政治 * 67% + 50;
                return Politics * 6700 / 10000 + 50;
            }
        }

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
        public int BaseRecruitmentAbility
        {
            get
            {
                return ((5 * this.Command) + (10 * this.Glamour));
            }
        }

        public void OnPersonAgeUpdate(Scenario scenario)
        {
            if (scenario.Variables.AgeEnabled)
            {
                Age = scenario.Info.year - yearBorn;
            }
            else
            {
                Age = 25;
            }

            if (scenario.Variables.EnableAgeAbilityFactor)
            {
                command.Update(); strength.Update(); intelligence.Update(); politics.Update(); glamour.Update();
                //spearLv.Update(); halberdLv.Update(); crossbowLv.Update(); horseLv.Update(); waterLv.Update(); machineLv.Update();
            }
        }

        public ushort skill;

        [JsonProperty] public int missionType;
        [JsonProperty] public int missionTarget;
        [JsonProperty] public int missionCounter;

        public bool rewardOver;

        public int Age { get; private set; }
        public bool IsFree { get { return BelongTroop == null && missionType == (int)MissionType.None; } }
        public bool IsWild { get { return BelongCorps == null; } }

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

        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (BelongCity != null)
            {
                if (!IsWild)
                {
                    beFinded = true;
                    BelongCity.allPersons.Add(this);
                }
                else
                    BelongCity.wildPersons.Add(this);
            }

            if (Father != null)
                Father.sonList.Add(this);

            // 关联武将转移信息
            if (this.missionType == (int)MissionType.PersonTransform)
            {
                City city = scenario.citySet.Get(missionTarget);
                city.trsformingPesonList.Add(this);
            }
            else if (this.missionType == (int)MissionType.PersonBuild)
            {
                Building building = scenario.buildingSet.Get(missionTarget);
                building.Builder = this;
            }

            OnPersonAgeUpdate(scenario);
        }

        public override bool OnYearStart(Scenario scenario)
        {
            OnPersonAgeUpdate(scenario);
            return base.OnYearStart(scenario);
        }

        public void UpdateMission(Scenario scenario)
        {
            switch (missionType)
            {
                case (int)MissionType.PersonReturn:
                    {
                        City dest = scenario.citySet.Get(missionTarget);
                        if (!this.IsSameForce(dest))
                        {
                            SetMission(MissionType.PersonReturn, BelongForce.Governor.BelongCity, 1);
                            return;
                        }
                        else
                        {
                            missionCounter--;
                            if (missionCounter <= 0)
                            {
                                ClearMission();
                                dest.OnPersonReturnCity(this);
                            }
                        }
                    }
                    break;
                case (int)MissionType.PersonTransform:
                    {
                        City dest = scenario.citySet.Get(missionTarget);
                        if (BelongCorps != null && !this.IsSameForce(dest))
                        {
                            SetMission(MissionType.PersonReturn, BelongCity, 1);
                            dest.OnPersonTransformEnd(this);
                        }
                        else
                        {
                            missionCounter--;
                            if (missionCounter <= 0)
                            {
                                ClearMission();
                                ChangeCity(dest);
                                dest.OnPersonTransformEnd(this);
                            }
                        }
                    }
                    break;
                case (int)MissionType.PersonRecruitPerson:
                    {
                        Person dest = scenario.personSet.Get(missionTarget);
                        if (BelongCorps != null && !this.IsSameForce(dest))
                        {
                            SetMission(MissionType.PersonReturn, BelongCity, 1);
                        }
                        else
                        {
                            missionCounter--;
                            if (missionCounter <= 0)
                            {
                                JobRecuritPerson(dest);
                                SetMission(MissionType.PersonReturn, BelongCity, 1);
                            }
                        }
                    }
                    break;
            }
        }

        public void SetMission(MissionType missionType, SangoObject missionTarget, int missionCounter)
        {
            this.missionType = (int)missionType;
            this.missionTarget = missionTarget.Id;
            this.missionCounter = missionCounter;
        }

        public void ClearMission()
        {
            this.missionType = 0;
            this.missionTarget = 0;
            this.missionCounter = 0;
        }

        public override bool OnNewTurn(Scenario scenario)
        {
            return base.OnNewTurn(scenario);
        }
        public override bool OnTurnStart(Scenario scenario)
        {
            //TODO:在野角色随机移动
            UpdateMission(scenario);
            ActionOver = !IsFree;
            return base.OnTurnStart(scenario);
        }

        public void TransformToCity(City dest)
        {
            dest.trsformingPesonList.Add(this);
            SetMission(MissionType.PersonTransform, dest, Scenario.Cur.GetCityDistance(BelongCity, dest));
            BelongCity?.freePersons.Remove(this);
            Sango.Log.Print($"*{BelongForce?.Name}的{Name}从{BelongCity.Name}向{dest.Name}转移*");
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

        public City ChangeCity(City city)
        {
            City last = null;
            if (BelongCity != city)
            {
                last = BelongCity;
                Sango.Log.Print($"*{BelongForce?.Name}的{Name}从{BelongCity.Name}向{city.Name}转移* 移动完成!!");

                if (!IsWild)
                {
                    BelongCity.allPersons.Remove(this);
                    BelongCity.CheckIfLoseLeader(this);
                    city.allPersons.Add(this);
                    BelongCity = city;
                    if (BelongCorps != city.BelongCorps)
                    {
                        BelongCorps = city.BelongCorps;
                        if (BelongForce != city.BelongForce)
                        {
                            BelongForce = city.BelongForce;
                        }
                    }
                    city.UpdateLeader(this);
                }
                else
                {
                    BelongCity.wildPersons.Remove(this);
                    city.wildPersons.Add(this);
                    BelongCity = city;
                    city.UpdateLeader(this);
                }

                BelongTroop?.OnPersonChangeCity(this, last, city);
            }
            return last;
        }

        public void JobRecuritPerson(Person person)
        {
            //TODO: 招募成功概率计算
            bool success = true;
            if (success)
            {
#if SANGO_DEBUG
                Sango.Log.Print($"[{BelongForce.Name}]<{Name}>招募成功, {person.Name}加入了势力{BelongForce.Name}");
#endif
                // 如果在部队里,如果是主将则带部队加入,如果为副将则退出部队
                Troop personTroop = person.BelongTroop;
                if (personTroop != null)
                {
                    if (person == personTroop.Leader)
                    {
                        personTroop.JoinToForce(BelongCity);
                        personTroop.ActionOver = true;
                    }
                    else
                    {
                        personTroop.RemovePerson(person);
                        if (!person.JoinToForce(BelongCity))
                        {
                            person.SetMission(MissionType.PersonReturn, BelongCity, 1);
                        }
                    }
                    personTroop.Render?.UpdateRender();
                }
                else
                {
                    // 有归属
                    if (!person.JoinToForce(BelongCity))
                    {
                        person.SetMission(MissionType.PersonReturn, BelongCity, 1);
                    }
                }
                person.ActionOver = true;
            }
            ScenarioVariables variables = Scenario.Cur.Variables;
            int jobId = (int)CityJobType.RecuritPerson;
            int meritGain = variables.jobMaxPersonCount[jobId];
            int techniquePointGain = variables.jobTechniquePoint[jobId];
            merit += meritGain;
            BelongForce?.GainTechniquePoint(techniquePointGain);
            ActionOver = true;
        }

        /// <summary>
        /// 加入某个势力,需要指定一个城市
        /// </summary>
        /// <param name="city"></param>
        public bool JoinToForce(City city)
        {
            // 先从原有势力移除
            if (BelongCorps != null)
            {
                BelongCity.allPersons.Remove(this);
            }
            else
            {
                if (BelongCity != null)
                    BelongCity.wildPersons.Remove(this);
            }

            bool isSameCity = BelongCity == city;
            BelongCity = city; ;
            BelongCorps = city.BelongCorps;
            BelongForce = city.BelongForce;

            BelongCity.allPersons.Add(this);

            return isSameCity;
        }

        /// <summary>
        /// 下野
        /// </summary>
        public void LeaveToWild()
        {
            BelongCity.allPersons.Remove(this);
            Official = Scenario.Cur.CommonData.Officials.Get(0);
            BelongCity.wildPersons.Add(this);
            BelongCorps = null;
            BelongForce = null;
            BelongTroop = null;
        }

        //TODO: 完善招降概率
        public bool Persuade(Person person)
        {
            //return GameRandom.Changce(30);
            return true;
        }

        public Person BeCaptive(City city)
        {
            city.CaptiveList.Add(this);
            this.BelongForce.CaptiveList.Add(this);
            return this;
        }

        public Person BeCaptive(Troop troop)
        {
            troop.captiveList.Add(this);
            this.BelongForce.CaptiveList.Add(this);
            return this;
        }

        /// <summary>
        /// 获取经验
        /// </summary>
        /// <param name="add"></param>
        public void GainExp(int add)
        {
            Exp += add;
            while (Level.exp > 0)
            {
                if (Exp > Level.exp)
                {
                    Exp = Level.exp - Exp;
                    Level = Level.Next;
                    Scenario.Cur.Event.OnPersonLevelUp?.Invoke(this);
                }
                else
                    break;
            }
        }
    }
}
