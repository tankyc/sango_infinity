/*
 * 文件名：Scenario.cs
 * 描述：剧本类，管理游戏剧本的所有数据，包括势力、武将、城市、部队等
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using TKNewtonsoft.Json;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace Sango.Core
{
    /// <summary>
    /// 剧本类，管理游戏剧本的所有数据
    /// 包含势力、武将、城市、部队、建筑等游戏对象
    /// 负责剧本的加载、保存、运行等核心功能
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Scenario : SangoObjectExtensionData
    {
        public static ScenarioVariables DefaultVariables = new ScenarioVariables();

        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Scenario; } }

        #region Data
        /// <summary>
        /// 剧本信息
        /// </summary>
        [JsonProperty(Order = -97)] public ScenarioInfo Info { get; internal set; }

        /// <summary>
        /// 剧本视图配置
        /// </summary>
        [JsonProperty(Order = -98)] public ScenarioView View { get; internal set; }

        /// <summary>
        /// 剧本公共数据
        /// </summary>
        [JsonProperty(Order = -96)] public ScenarioCommonData CommonData { internal set; get; }

        /// <summary>
        /// 剧本变量配置
        /// </summary>
        [JsonProperty(Order = -95)] public ScenarioVariables Variables { internal set; get; }

        /// <summary>
        /// 地图数据
        /// </summary>
        [JsonProperty(Order = -94)] public Map Map { internal set; get; }

        /// <summary>
        /// 势力集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Force>))]
        [JsonProperty] public SangoObjectSet<Force> forceSet = new SangoObjectSet<Force>();

        /// <summary>
        /// 军团集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Corps>))]
        [JsonProperty] public SangoObjectSet<Corps> corpsSet = new SangoObjectSet<Corps>();

        /// <summary>
        /// 城市集合（包含关卡和港口）
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetCityConverter))]
        [JsonProperty] public SangoObjectSet<City> citySet = new SangoObjectSet<City>();

        /// <summary>
        /// 武将集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Person>))]
        [JsonProperty] public SangoObjectSet<Person> personSet = new SangoObjectSet<Person>();

        /// <summary>
        /// 部队集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Troop>))]
        [JsonProperty] public SangoObjectSet<Troop> troopsSet = new SangoObjectSet<Troop>();

        /// <summary>
        /// 建筑集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Building>))]
        [JsonProperty] public SangoObjectSet<Building> buildingSet = new SangoObjectSet<Building>();

        /// <summary>
        /// 火焰集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Fire>))]
        [JsonProperty] public SangoObjectSet<Fire> fireSet = new SangoObjectSet<Fire>();

        /// <summary>
        /// 结盟信息集合
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Alliance>))]
        [JsonProperty] public SangoObjectSet<Alliance> allianceSet = new SangoObjectSet<Alliance>();

        /// <summary>
        /// 势力关系矩阵
        /// </summary>
        [JsonProperty] public int[][] RelationMap { get; set; }

        /// <summary>
        /// 添加势力到剧本
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <returns>添加的势力</returns>
        public Force Add(Force force) { forceSet.Add(force); return force; }

        /// <summary>
        /// 添加军团到剧本
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <returns>添加的军团</returns>
        public Corps Add(Corps corps) { corpsSet.Add(corps); return corps; }

        /// <summary>
        /// 添加城市到剧本
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>添加的城市</returns>
        public City Add(City city) { citySet.Add(city); return city; }

        /// <summary>
        /// 添加武将到剧本
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>添加的武将</returns>
        public Person Add(Person person) { personSet.Add(person); return person; }

        /// <summary>
        /// 添加部队到剧本
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>添加的部队</returns>
        public Troop Add(Troop troop)
        {
            troopsSet.Add(troop);
            GameEvent.OnTroopCreated?.Invoke(troop, this);
            return troop;
        }

        /// <summary>
        /// 添加建筑到剧本
        /// </summary>
        /// <param name="building">建筑对象</param>
        /// <returns>添加的建筑</returns>
        public Building Add(Building building) { buildingSet.Add(building); return building; }

        /// <summary>
        /// 添加火焰到剧本
        /// </summary>
        /// <param name="fire">火焰对象</param>
        /// <returns>添加的火焰</returns>
        public Fire Add(Fire fire) { fireSet.Add(fire); return fire; }

        /// <summary>
        /// 添加同盟到剧本
        /// </summary>
        /// <param name="alliance">同盟对象</param>
        /// <returns>添加的同盟</returns>
        public Alliance Add(Alliance alliance) { allianceSet.Add(alliance); return alliance; }

        /// <summary>
        /// 从剧本移除势力
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <returns>移除的势力</returns>
        public Force Remove(Force force)
        {
            forceSet.Remove(force); return force;
        }

        /// <summary>
        /// 从剧本移除军团
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <returns>移除的军团</returns>
        public Corps Remove(Corps corps) { corpsSet.Remove(corps); return corps; }

        /// <summary>
        /// 从剧本移除城市
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>移除的城市</returns>
        public City Remove(City city) { citySet.Remove(city); return city; }

        /// <summary>
        /// 从剧本移除武将
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>移除的武将</returns>
        public Person Remove(Person person) { personSet.Remove(person); return person; }

        /// <summary>
        /// 从剧本移除部队
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>移除的部队</returns>
        public Troop Remove(Troop troop)
        {
            troopsSet.Remove(troop);
            GameEvent.OnTroopDestroyed?.Invoke(troop, this);
            return troop;
        }

        /// <summary>
        /// 从剧本移除建筑
        /// </summary>
        /// <param name="building">建筑对象</param>
        /// <returns>移除的建筑</returns>
        public Building Remove(Building building) { buildingSet.Remove(building); return building; }

        /// <summary>
        /// 从剧本移除火焰
        /// </summary>
        /// <param name="fire">火焰对象</param>
        /// <returns>移除的火焰</returns>
        public Fire Remove(Fire fire) { fireSet.Remove(fire); return fire; }

        /// <summary>
        /// 从剧本移除同盟
        /// </summary>
        /// <param name="alliance">同盟对象</param>
        /// <returns>移除的同盟</returns>
        public Alliance Remove(Alliance alliance) { allianceSet.Remove(alliance); return alliance; }

        /// <summary>
        /// 城市路径缓存映射
        /// </summary>
        public Dictionary<string, List<City>> cityPathMap;

        #endregion Data

        /// <summary>
        /// 当前运行的剧本
        /// </summary>
        public static Scenario Cur { get; private set; }

        /// <summary>
        /// 所有剧本列表
        /// </summary>
        public static List<Scenario> all_scenario_list = new List<Scenario>();

        /// <summary>
        /// 当前选中的剧本
        /// </summary>
        public static Scenario CurSelected { get; set; }

        /// <summary>
        /// 剧本文件路径
        /// </summary>
        public string FilePath { internal set; get; }

        /// <summary>
        /// 待运行的势力队列
        /// </summary>
        private Queue<Force> runForces = new Queue<Force>();

        /// <summary>
        /// 当前正在运行的势力
        /// </summary>
        public Force CurRunForce { get; private set; }

        /// <summary>
        /// 当前季节
        /// </summary>
        public SeasonType CurSeason { get { return GameDefine.SeasonInMonth[Info.month - 1]; } }

        /// <summary>
        /// 开始玩家列表
        /// </summary>
        List<int> startPlayerList;

        /// <summary>
        /// 调试用暂停回合数
        /// </summary>
        internal int PauseTrunCount = -1;

        /// <summary>
        /// 当前回合数
        /// </summary>
        public int TurnCount => Info.turnCount;

        /// <summary>
        /// 是否使用线程运行
        /// </summary>
        public bool useThreadRun = false;

        /// <summary>
        /// 异步任务
        /// </summary>
        Task task;

        /// <summary>
        /// 构造函数，根据文件路径创建剧本
        /// </summary>
        /// <param name="filePath">剧本文件路径</param>
        public Scenario(string filePath)
        {
            this.FilePath = filePath;
            LoadInfo();
        }

        /// <summary>
        /// 准备列表
        /// </summary>
        List<IDatabase> prepareList = new List<IDatabase>();

        /// <summary>
        /// 事件接收列表
        /// </summary>
        List<IDatabase> eventReciveList = new List<IDatabase>();

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public Scenario()
        {
        }

        /// <summary>
        /// 获取指定类型的数据集合
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns>数据集合</returns>
        public Database<T> GetDatabase<T>() where T : SangoObject, new()
        {
            Type tType = typeof(T);
            if (tType == typeof(Person))
            {
                return personSet as Database<T>;
            }
            else if (tType == typeof(Force))
            {
                return forceSet as Database<T>;
            }
            else if (tType == typeof(Troop))
            {
                return troopsSet as Database<T>;
            }
            else if (tType == typeof(City))
            {
                return citySet as Database<T>;
            }
            else if (tType == typeof(Port))
            {
                return citySet as Database<T>;
            }
            else if (tType == typeof(Gate))
            {
                return citySet as Database<T>;
            }
            else if (tType == typeof(Building))
            {
                return buildingSet as Database<T>;
            }
            else if (tType == typeof(Corps))
            {
                return corpsSet as Database<T>;
            }
            else if (tType == typeof(Fire))
            {
                return fireSet as Database<T>;
            }
            else if (tType == typeof(Alliance))
            {
                return allianceSet as Database<T>;
            }
            else if (tType == typeof(TerrainType))
            {
                return CommonData.TerrainTypes as Database<T>;
            }
            else if (tType == typeof(BuildingType))
            {
                return CommonData.BuildingTypes as Database<T>;
            }
            else if (tType == typeof(Feature))
            {
                return CommonData.Features as Database<T>;
            }
            else if (tType == typeof(TroopType))
            {
                return CommonData.TroopTypes as Database<T>;
            }
            else if (tType == typeof(TroopAnimation))
            {
                return CommonData.TroopAnimations as Database<T>;
            }
            else if (tType == typeof(AttributeChangeType))
            {
                return CommonData.AttributeChangeTypes as Database<T>;
            }
            else if (tType == typeof(PersonAttributeType))
            {
                return CommonData.PersonAttributeTypes as Database<T>;
            }
            else if (tType == typeof(CityLevelType))
            {
                return CommonData.CityLevelTypes as Database<T>;
            }
            else if (tType == typeof(Flag))
            {
                return CommonData.Flags as Database<T>;
            }
            else if (tType == typeof(Province))
            {
                return CommonData.Provinces as Database<T>;
            }
            else if (tType == typeof(Region))
            {
                return CommonData.Regions as Database<T>;
            }
            else if (tType == typeof(Title))
            {
                return CommonData.Titles as Database<T>;
            }
            else if (tType == typeof(CityLevelType))
            {
                return CommonData.CityLevelTypes as Database<T>;
            }
            else if (tType == typeof(Official))
            {
                return CommonData.Officials as Database<T>;
            }
            else if (tType == typeof(Skill))
            {
                return CommonData.Skills as Database<T>;
            }
            else if (tType == typeof(PersonLevel))
            {
                return CommonData.PersonLevels as Database<T>;
            }
            else if (tType == typeof(ItemType))
            {
                return CommonData.ItemTypes as Database<T>;
            }
            else if (tType == typeof(JobType))
            {
                return CommonData.JobTypes as Database<T>;
            }
            else if (tType == typeof(Buff))
            {
                return CommonData.Buffs as Database<T>;
            }
            else if (tType == typeof(Technique))
            {
                return CommonData.Techniques as Database<T>;
            }
            else if (tType == typeof(Personality))
            {
                return CommonData.Personalities as Database<T>;
            }
            else if (tType == typeof(Argumentation))
            {
                return CommonData.Argumentations as Database<T>;
            }
            return null;
        }

        public T GetObject<T>(int id) where T : SangoObject, new()
        {
            return GetObject(id, typeof(T)) as T;
        }

        public object GetObject(int id, Type tType)
        {
            if (tType == typeof(Person))
            {
                return personSet.Get(id);
            }
            else if (tType == typeof(Force))
            {
                return forceSet.Get(id);
            }
            else if (tType == typeof(Troop))
            {
                return troopsSet.Get(id);
            }
            else if (tType == typeof(City))
            {
                return citySet.Get(id);
            }
            else if (tType == typeof(Building))
            {
                return buildingSet.Get(id);
            }
            else if (tType == typeof(Corps))
            {
                return corpsSet.Get(id);
            }
            else if (tType == typeof(Fire))
            {
                return fireSet.Get(id);
            }
            else if (tType == typeof(Alliance))
            {
                return allianceSet.Get(id);
            }
            else if (tType == typeof(TerrainType))
            {
                return CommonData.TerrainTypes.Get(id);
            }
            else if (tType == typeof(BuildingType))
            {
                return CommonData.BuildingTypes.Get(id);
            }
            else if (tType == typeof(Feature))
            {
                return CommonData.Features.Get(id);
            }
            else if (tType == typeof(TroopType))
            {
                return CommonData.TroopTypes.Get(id);
            }
            else if (tType == typeof(TroopAnimation))
            {
                return CommonData.TroopAnimations.Get(id);
            }
            else if (tType == typeof(AttributeChangeType))
            {
                return CommonData.AttributeChangeTypes.Get(id);
            }
            else if (tType == typeof(PersonAttributeType))
            {
                return CommonData.PersonAttributeTypes.Get(id);
            }
            else if (tType == typeof(CityLevelType))
            {
                return CommonData.CityLevelTypes.Get(id);
            }
            else if (tType == typeof(Flag))
            {
                return CommonData.Flags.Get(id);
            }
            else if (tType == typeof(Province))
            {
                return CommonData.Provinces.Get(id);
            }
            else if (tType == typeof(Region))
            {
                return CommonData.Regions.Get(id);
            }
            else if (tType == typeof(Title))
            {
                return CommonData.Titles.Get(id);
            }
            else if (tType == typeof(CityLevelType))
            {
                return CommonData.CityLevelTypes.Get(id);
            }
            else if (tType == typeof(Official))
            {
                return CommonData.Officials.Get(id);
            }
            else if (tType == typeof(Skill))
            {
                return CommonData.Skills.Get(id);
            }
            else if (tType == typeof(PersonLevel))
            {
                return CommonData.PersonLevels.Get(id);
            }
            else if (tType == typeof(ItemType))
            {
                return CommonData.ItemTypes.Get(id);
            }
            else if (tType == typeof(JobType))
            {
                return CommonData.JobTypes.Get(id);
            }
            else if (tType == typeof(Buff))
            {
                return CommonData.Buffs.Get(id);
            }
            else if (tType == typeof(Technique))
            {
                return CommonData.Techniques.Get(id);
            }
            else if (tType == typeof(Personality))
            {
                return CommonData.Personalities.Get(id);
            }
            else if (tType == typeof(Argumentation))
            {
                return CommonData.Argumentations.Get(id);
            }
            //else if (tType == typeof(CityLevelType))
            //{
            //    return CommonData.CityLevelTypes.Get(id);
            //}
            //else if (tType == typeof(CityLevelType))
            //{
            //    return CommonData.CityLevelTypes.Get(id);
            //}
            return null;
        }
        public static Scenario Add(string path)
        {
            if (!File.Exists(path))
                return null;

            Scenario scenario = new Scenario(path);
            all_scenario_list.Add(scenario);
            return scenario;
        }
        public void LoadInfo()
        {
            LoadInfo(FilePath);
        }
        public void LoadInfo(string path)
        {
            FilePath = path;

            using (StreamReader file = System.IO.File.OpenText(FilePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                while (reader.Read()) // Advances to the next token in the JSON stream.
                {
                    if (reader.TokenType == JsonToken.StartObject) // Check for start of an object in the JSON stream.
                    {
                        if (!string.IsNullOrEmpty(reader.Path) && reader.Path == "Info")
                        {
                            Info = JsonSerializer.CreateDefault().Deserialize<ScenarioInfo>(reader); // Deserialize the object.
                            Name = Info.name;
                            return;
                        }
                    }
                }
            }
        }

        public void LoadVariables()
        {
            using (StreamReader file = System.IO.File.OpenText(FilePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                while (reader.Read()) // Advances to the next token in the JSON stream.
                {
                    if (reader.TokenType == JsonToken.StartObject) // Check for start of an object in the JSON stream.
                    {
                        if (!string.IsNullOrEmpty(reader.Path) && reader.Path == "Variables")
                        {
                            Variables = JsonSerializer.CreateDefault().Deserialize<ScenarioVariables>(reader); // Deserialize the object.
                            Name = Info.name;
                            return;
                        }
                    }
                }
            }

            if (Variables == null)
                Variables = new ScenarioVariables();
        }

        public void LoadContent()
        {
            LoadContent(FilePath);
        }

        public void CheckPlayer()
        {

            if (startPlayerList != null)
            {
                Info.playerForceList = startPlayerList.ToArray();
                startPlayerList = null;
            }

            // 玩家确定
            if (Info.playerForceList != null && Info.playerForceList.Length > 0)
            {
                forceSet.ForEach(x =>
                {
                    for (int k = 0; k < Info.playerForceList.Length; k++)
                    {
                        if (Info.playerForceList[k] == x.Id)
                        {
                            x.IsPlayer = true;
                            return;
                        }
                    }
                });
            }

        }


        public void LoadBaseContent()
        {
            Cur = this;
            IsAlive = false;

            // 由此来判断是否加载过base了
            if (Map != null && Map.CellSet != null)
                return;

            CommonData = GameData.Instance.LoadNewCommonData();

            if (!Info.isSave)
            {
                //if (CommonData == null)
                //    CommonData = GameData.Instance.LoadNewCommonData();

                if (Variables == null)
                    Variables = new ScenarioVariables();

                if (Map == null)
                    Map = new Map();
            }

            if (CommonData.PersonLibrary != null)
            {
                CommonData.PersonLibrary.ForEach(personLib =>
                {
                    personSet.Add(Person.FormLib(personLib));
                });
            }

            JsonConvert.PopulateObject(File.ReadAllText(FilePath), this);

            GameData.Instance.LoadCommonData(CommonData);

            Map.Load(this);

            GameEvent.OnScenarioPrepare?.Invoke(this);
        }

        public void LoadContent(string path)
        {
            LoadBaseContent();

            prepareList.Add(forceSet);
            prepareList.Add(corpsSet);
            prepareList.Add(citySet);
            prepareList.Add(personSet);
            prepareList.Add(buildingSet);
            prepareList.Add(troopsSet);
            prepareList.Add(fireSet);

            eventReciveList.Add(personSet);
            eventReciveList.Add(buildingSet);
            eventReciveList.Add(citySet);
            eventReciveList.Add(troopsSet);
            eventReciveList.Add(forceSet);
            eventReciveList.Add(fireSet);
        }

        public void LoadWorld()
        {
            MapRender.Instance.Init();
            MapRender.Instance.OnMapLoaded += OnWorldLoaded;
            MapRender.Instance.LoadMap(Map.FileName);
        }

        public void OnWorldLoaded()
        {
            GameEvent.OnWorldLoadEnd?.Invoke(this);
            this.Map.Init(this);
            GameEvent.OnScenarioPrepare?.Invoke(this);
            this.LoadModModify();
            this.Prepare();
            this.Init(this);
            GameController.Instance.Reset();
            this.Start();
            MapRender.Instance.OnMapLoaded -= OnWorldLoaded;
        }

        //public override void Load(BinaryReader reader)
        //{
        //    Info.Load(reader);

        //}

        //public bool Save(string path)
        //{
        //    XmlDocument xmlDocument = new XmlDocument();
        //    xmlDocument.AppendChild(xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null));//xml文件头
        //    XmlLoader.Save(this, xmlDocument, "Scenario");
        //    using (Stream stream = System.IO.File.Open(path, FileMode.Create, FileAccess.Write))
        //    {
        //        using (XmlTextWriter writer = new XmlTextWriter(stream, new UTF8Encoding(false)))
        //        {
        //            writer.Formatting = Formatting.Indented;
        //            xmlDocument.Save(writer);
        //        }
        //    }
        //    return true;
        //}

        public static void OnModInitStart()
        {
            all_scenario_list.Clear();
            string path = $"{Path.ContentRootPath}/Scenario";
            Directory.EnumFiles(path, "*.json", SearchOption.AllDirectories, (file) =>
            {
                Sango.Log.Info($"Find Scenario: {file}");
                Add(file);
                ShortScenario.Add(file);
            });
        }

        public static void OnModInitEnd()
        {
            all_scenario_list.Sort((a, b) =>
            {
                if (a.Info.priority == b.Info.priority)
                {
                    return a.Info.id.CompareTo(b.Info.id);
                }
                else
                {
                    return a.Info.priority.CompareTo(b.Info.priority);
                }
            });

            ShortScenario.all_scenario_info_list.Sort((a, b) =>
            {
                if (a.Info.priority == b.Info.priority)
                {
                    return a.Info.id.CompareTo(b.Info.id);
                }
                else
                {
                    return a.Info.priority.CompareTo(b.Info.priority);
                }
            });
        }

        public static void StartScenario(Scenario scenario, List<int> playerList)
        {
            scenario.startPlayerList = playerList;
            StartScenario(scenario);
        }

        public static void StartScenario(Scenario scenario)
        {
            GameRandom.Init();
            Cur = scenario;
            scenario.IsAlive = false;
            GameEvent.OnScenarioLoadStart?.Invoke(scenario);
            Cur.LoadContent();
            Cur.CheckPlayer();
            GameEvent.OnScenarioLoadEnd?.Invoke(Cur);
            GameEvent.OnWorldLoadStart?.Invoke(Cur);
            Cur.LoadWorld();
            //Cur.OnWorldLoaded();
            //Event.OnScenarioEnd?.Invoke(Cur);
            //Cur = null;
        }

        public static void StartScenario(Scenario scenario, ShortScenario addData)
        {
            GameRandom.Init();
            Cur = scenario;
            scenario.IsAlive = false;
            GameEvent.OnScenarioLoadStart?.Invoke(scenario);
            Cur.LoadContent();

            //准备军团
            addData.forceSet.ForEach(force =>
            {
                if (force.IsAppend)
                {
                    Force targetForce = new Force();
                    targetForce.Id = force.Id;
                    targetForce.Flag = force.Flag;
                    targetForce.Governor = force.Governor;

                    scenario.forceSet.Add(targetForce);

                    // 生成第一军团
                    Corps corps = new Corps();
                    corps.number = 1;
                    corps.BelongForce = force.Id;
                    corps.Comander = force.Governor;
                    scenario.corpsSet.Add(corps);

                    force.CapitalCorps = corps.Id;
                }
            });

            addData.citySet.ForEach(city =>
            {
                if (city.BelongForce > 0)
                {
                    ShortForce force = addData.forceSet[city.BelongForce];
                    if (force.IsAppend)
                    {
                        city.BelongCorps = force.CapitalCorps;
                    }
                }
            });

            addData.personSet.ForEach(x =>
            {
                if (x.PersonLib != null)
                {
                    x.PersonLib.Id = x.Id;
                    Person person = Person.FormLib2(x.PersonLib);
                    scenario.personSet.Add(person);
                    person.BelongCity = x.BelongCity;
                    person.CurrentCity = x.BelongCity;
                    person.BelongForce = x.BelongForce;
                    person.state = x.state;
                    if (!person.IsWild)
                    {
                        City city = scenario.citySet[x.BelongCity];
                        ShortCity shortCity = addData.citySet[x.BelongCity];
                        person.BelongCorps = System.Math.Max(city.BelongCorps, shortCity.BelongCorps);
                    }
                }
            });

            Cur.CheckPlayer();
            GameEvent.OnScenarioLoadEnd?.Invoke(Cur);
            GameEvent.OnWorldLoadStart?.Invoke(Cur);
            Cur.LoadWorld();
            //Cur.OnWorldLoaded();
            //Event.OnScenarioEnd?.Invoke(Cur);
            //Cur = null;
        }



        public override void Clear()
        {
            for (int i = 0; i < prepareList.Count; ++i)
            {
                prepareList[i].ForEach(o => { o.Clear(); });
            }

            GameEvent.OnGameShutdown -= OnGameShutdown;
            GameEvent.OnGamePause -= OnGamePause;
            GameEvent.OnGameResume -= OnGameResume;
            MapRender.Instance.OnMapLoaded -= OnWorldLoaded;
            IsAlive = false;
            base.Clear();
            CommonData = null;
            Variables = null;
            if (Map != null)
            {
                Map.Clear();
                Map = null;
            }

            forceSet.Clear();
            corpsSet.Clear();
            citySet.Clear();
            personSet.Clear();
            troopsSet.Clear();
            buildingSet.Clear();
            fireSet.Clear();
            allianceSet.Clear();
            RelationMap = null;

            prepareList.Clear();
            eventReciveList.Clear();
        }

        public void OnGameShutdown()
        {
            End();
            Clear();
            Cur = null;
        }
        public void OnGamePause()
        {
            isThreadPause = true;
        }
        public void OnGameResume()
        {
            isThreadPause = false;
        }



        // 在Prepare之后
        public override void Init(Scenario scenario)
        {
            CommonData.Init();

            GameEvent.OnGameShutdown += OnGameShutdown;
            GameEvent.OnGamePause += OnGamePause;
            GameEvent.OnGameResume += OnGameResume;

            SeasonType cur_season = GameDefine.SeasonInMonth[Info.month - 1];
            MapRender.Instance.ChangeSeason((int)cur_season);

            // 初始化路径缓存
            cityPathMap = new Dictionary<string, List<City>>();

            for (int i = 0; i < prepareList.Count; ++i)
            {
                prepareList[i].ForEach(o => { o.Init(this); });
            }

            if (RelationMap == null)
            {
                int forceCount = forceSet.Count;
                RelationMap = new int[forceCount][];
                for (int i = 0; i < forceCount; ++i)
                {
                    RelationMap[i] = new int[forceCount];
                }

                for (int i = 0; i < forceCount; ++i)
                {
                    for (int j = i + 1; j < forceCount; ++j)
                    {
                        RelationMap[i][j] = 0;
                        RelationMap[j][i] = 0;
                    }
                }
            }

            GameEvent.OnScenarioInit?.Invoke(this);
        }

        /// <summary>
        /// 在Init之前
        /// </summary>
        public void Prepare()
        {

            for (int i = 0; i < prepareList.Count; ++i)
            {
                prepareList[i].ForEach(o => { o.OnScenarioPrepare(this); });
            }
            //MapRender.Instance.Update();
        }


        bool isThreadPause = false;
        public void Start()
        {
            MapRender.Instance.SetCamera(View.cameraPosition, View.cameraRotation, View.cameraDistance);

            MakeForceQuene();
            // 恢复游戏
            if (Info.curForceId > 0)
            {
                Force force = runForces.Dequeue();
                while (force != null)
                {
                    if (force.Id == Info.curForceId)
                    {
                        CurRunForce = force;
                        HasTurnStarted = true;
                        break;
                    }
                    force = runForces.Dequeue();
                }
            }

            GameEvent.OnScenarioStart?.Invoke(this);

            Window.Instance.Close("window_start");
            Window.Instance.Close("window_loading");
            Window.Instance.Open("window_game");
#if SANGO_DEBUG_AI
            GameAIDebug.Instance.Init();
#endif



            GameController.Instance.Enabled = true;

            Run();

            IsAlive = true;
            if (useThreadRun)
            {
                task = Task.Run(() =>
                {
                    while (IsAlive)
                    {
                        if (!isThreadPause)
                        {
                            Run();
                            Thread.Sleep(1);
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                });
            }
        }

        public void End()
        {
            GameEvent.OnScenarioEnd?.Invoke(this);
        }

        public void MakeForceQuene()
        {
            // 玩家排到最前面
            if (Info.playerForceList != null)
            {
                for (int k = 0; k < Info.playerForceList.Length; k++)
                {
                    Force force = forceSet.Get(Info.playerForceList[k]);
                    if (force != null && force.IsAlive)
                    {
                        force.ActionOver = false;
                        runForces.Enqueue(force);
                    }
                }

            }

            forceSet.ForEach(force =>
            {
                if (force.IsAlive)
                {
                    if (Info.playerForceList == null || !Info.playerForceList.Contains(force.Id))
                    {
                        force.ActionOver = false;
                        runForces.Enqueue(force);
                    }
                }
            });
        }

        public bool TurnStart()
        {
            if (HasTurnStarted) return true;
            for (int i = 1; i < personSet.Count; i++)
            {
                Person person = personSet[i];
                if (person != null && person.IsAlive)
                    person.OnTurnStart(this);
            }

            for (int i = 1; i < allianceSet.Count; i++)
            {
                Alliance a = allianceSet[i];
                if (a != null && a.IsAlive)
                    a.OnTurnStart(this);
            }

            for (int i = 1; i < fireSet.Count; i++)
            {
                Fire a = fireSet[i];
                if (a != null && a.IsAlive)
                    a.OnTurnStart(this);
            }

            GameEvent.OnTurnStart?.Invoke(this);

            allianceSet.RemoveAll(a => !a.IsAlive);

            HasTurnStarted = true;
            return true;
        }

        public bool RunForces()
        {
            // 处理当前势力的逻辑
            if (CurRunForce != null && CurRunForce.IsAlive)
            {
                if (!CurRunForce.Run(this))
                    return false;
                else
                {
                    CurRunForce.OnForceTurnEnd(this);
                    GameEvent.OnForceTurnEnd?.Invoke(CurRunForce, this);
                    CurRunForce = null;
                }
            }
            // 完成一轮
            if (runForces.Count <= 0)
            {
                return true;
            }
            CurRunForce = runForces.Dequeue();
            while (!CurRunForce.IsAlive && runForces.Count > 0)
                CurRunForce = runForces.Dequeue();

            if (CurRunForce != null && CurRunForce.IsAlive)
            {
                Info.curForceId = CurRunForce.Id;
                Info.curForceName = CurRunForce.Name;
                CurRunForce.OnForceTurnStart(this);
                GameEvent.OnForceTurnStart?.Invoke(CurRunForce, this);
            }
            else
            {
                CurRunForce = null;
            }
            return false;
        }

        public bool TurnEnd()
        {
            if (HasTurnEnded) return true;
            for (int i = 1; i < personSet.Count; i++)
            {
                Person person = personSet[i];
                if (person != null && person.IsAlive)
                    person.OnTurnEnd(this);
            }

            for (int i = 1; i < allianceSet.Count; i++)
            {
                Alliance a = allianceSet[i];
                if (a != null && a.IsAlive)
                    a.OnTurnEnd(this);
            }

            for (int i = 1; i < fireSet.Count; i++)
            {
                Fire a = fireSet[i];
                if (a != null && a.IsAlive)
                    a.OnTurnEnd(this);
            }

            HasTurnEnded = true;
            GameEvent.OnTurnEnd?.Invoke(this);
            Info.turnCount++;
            return true;
        }

        public bool IncreaseDate()
        {
            SeasonType last_season = GameDefine.SeasonInMonth[Info.month - 1];
            Info.day += 10;

            bool hasYear = false;
            bool hasMonth = false;
            if (Info.day > 30)
            {
                Info.day -= 30;
                Info.month += 1;
                hasMonth = true;
                if (Info.month > 12)
                {
                    Info.month -= 12;
                    Info.year += 1;
                    hasYear = true;

                }
            }
            if (hasYear)
            {
                OnYearStart(this);
                GameEvent.OnYearUpdate?.Invoke(this);
            }
            if (hasMonth)
            {
                OnMonthStart(this);
                GameEvent.OnMonthStart?.Invoke(this);
                GameEvent.OnMonthUpdate?.Invoke(this);
            }
            OnDayStart(this);
            GameEvent.OnDayUpdate?.Invoke(this);
            OnDayEnd(this);
            if (hasMonth)
            {
                OnMonthEnd(this);
                GameEvent.OnMonthEnd?.Invoke(this);
            }
            if (hasYear)
            {
                OnYearEnd(this);
            }
            SeasonType cur_season = GameDefine.SeasonInMonth[Info.month - 1];
            if (cur_season != last_season)
            {
                OnSeasonStart(this);
                MapRender.Instance.ChangeSeason((int)cur_season);
                GameEvent.OnSeasonUpdate?.Invoke(this);
                OnSeasonEnd(this);
            }

            //if (Info.year == 500)
            //    IsAlive = false;

            return true;
        }
        float waitTime = 5;

        internal bool HasTurnStarted = false;
        internal bool HasTurnEnded = false;

        public void Run()
        {
            // 事件处理
            if (!RenderEvent.Instance.Update(this, Time.deltaTime))
                return;

            if (!IsAlive)
                return;
            //#if SANGO_DEBUG
            if (PauseTrunCount == Info.turnCount)
                return;
            //#endif
            if (!TurnStart())
                return;

            if (!RunForces())
                return;

            if (!TurnEnd())
                return;

            if (!IncreaseDate())
                return;

#if SANGO_DEBUG
            Sango.Log.Warning($"{GetDateStr()}  第{Info.turnCount}回");
#endif
            MakeForceQuene();

            HasTurnEnded = false;
            HasTurnStarted = false;

            waitTime = 1;
        }


        public override bool OnDayStart(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnDayStart(this); });
            }
            return base.OnDayStart(scenario);
        }

        public override bool OnDayEnd(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnDayEnd(this); });
            }
            return base.OnDayEnd(scenario);
        }

        public override bool OnMonthStart(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnMonthStart(this); });
            }
            return base.OnMonthStart(scenario);
        }
        public override bool OnMonthEnd(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnMonthEnd(this); });
            }
            return base.OnMonthEnd(scenario);
        }
        public override bool OnYearStart(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnYearStart(this); });
            }
            return base.OnYearStart(scenario);
        }
        public override bool OnYearEnd(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnYearEnd(this); });
            }
            return base.OnYearEnd(scenario);
        }
        public override bool OnSeasonStart(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnSeasonStart(this); });
            }
            return base.OnSeasonStart(scenario);
        }
        public override bool OnSeasonEnd(Scenario scenario)
        {
            for (int i = 0; i < eventReciveList.Count; ++i)
            {
                eventReciveList[i].ForEach(o => { if (o.IsAlive) o.OnSeasonEnd(this); });
            }
            return base.OnSeasonEnd(scenario);
        }

        /// <summary>
        /// 获取城市之间的相隔距离
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public int GetCityDistance(City a, City b)
        {
            // 检查参数
            if (a == null || b == null)
                return -1;

            // 检查是否是同一个城市
            if (a == b)
                return 0;

            // 使用寻路方法获取距离
            List<City> path = FindShortestPath(a, b);
            return path != null ? path.Count - 1 : -1;
        }

        /// <summary>
        /// 寻找两个城市之间的最短路径,要求自家势力内
        /// </summary>
        /// <param name="startCity">起始城市</param>
        /// <param name="endCity">目标城市</param>
        /// <returns>最短路径的城市列表</returns>
        public List<City> FindShortestPath(City startCity, City endCity)
        {
            // 检查参数
            if (startCity == null || endCity == null)
                return null;

            // 检查是否是同一个城市
            if (startCity == endCity)
            {
                return new List<City> { startCity };
            }

            // 检查缓存中是否已有路径
            string key = $"{startCity.Id}_{endCity.Id}";
            if (cityPathMap.ContainsKey(key))
            {
                return cityPathMap[key];
            }

            // 如果缓存中没有，使用BFS算法重新计算路径
            Dictionary<City, City> parentMap = new Dictionary<City, City>();
            Queue<City> queue = new Queue<City>();
            HashSet<City> visited = new HashSet<City>();

            queue.Enqueue(startCity);
            visited.Add(startCity);
            parentMap[startCity] = null;

            bool found = false;
            while (queue.Count > 0)
            {
                City current = queue.Dequeue();

                foreach (City neighbor in current.NeighborList)
                {
                    if (!visited.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                        visited.Add(neighbor);
                        parentMap[neighbor] = current;

                        if (neighbor == endCity)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                    break;
            }

            // 如果找到路径，构建路径并缓存
            if (found)
            {
                List<City> path = new List<City>();
                City current = endCity;
                while (current != null)
                {
                    path.Add(current);
                    current = parentMap[current];
                }
                path.Reverse();
                cityPathMap[key] = path;
                return path;
            }

            // 如果没有找到路径，返回null
            return null;
        }

        /// <summary>
        /// 寻找两个城市之间的最短路径
        /// </summary>
        /// <param name="startCity">起始城市</param>
        /// <param name="endCity">目标城市</param>
        /// <returns>最短路径的城市列表</returns>
        public List<City> FindShortestPathInForce(City startCity, City endCity)
        {
            // 检查参数
            if (startCity == null || endCity == null)
                return null;

            // 检查是否是同一个城市
            if (startCity == endCity)
            {
                return new List<City> { startCity };
            }

            // 如果缓存中没有，使用BFS算法重新计算路径
            Dictionary<City, City> parentMap = new Dictionary<City, City>();
            Queue<City> queue = new Queue<City>();
            HashSet<City> visited = new HashSet<City>();

            queue.Enqueue(startCity);
            visited.Add(startCity);
            parentMap[startCity] = null;

            bool found = false;
            while (queue.Count > 0)
            {
                City current = queue.Dequeue();

                foreach (City neighbor in current.NeighborList)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        if (neighbor.IsSameForce(startCity))
                        {
                            queue.Enqueue(neighbor);
                            parentMap[neighbor] = current;
                            if (neighbor == endCity)
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                }

                if (found)
                    break;
            }

            // 如果找到路径，构建路径并缓存
            if (found)
            {
                List<City> path = new List<City>();
                City current = endCity;
                while (current != null)
                {
                    path.Add(current);
                    current = parentMap[current];
                }
                path.Reverse();
                return path;
            }

            // 如果没有找到路径，返回null
            return null;
        }

        public static void Pause()
        {
            Scenario.Cur.IsAlive = false;
        }
        public static void Resume()
        {
            Scenario.Cur.IsAlive = true;
            //Scenario.Cur.CurRunForce.IsPlayerControlled = false;
            Scenario.Cur.PauseTrunCount = -1;
        }

        public static void NextForce()
        {
            Scenario.Cur.IsAlive = true;
            //Scenario.Cur.CurRunForce.IsPlayerControlled = false;
            Force nextForce = Scenario.Cur.runForces.Peek();
            if (nextForce != null)
            {
                //nextForce.IsPlayerControlled = true;
            }
            else
            {
                Scenario.Cur.IsAlive = false;
            }
        }

        public static void NextTurn()
        {
            //Scenario.Cur.CurRunForce.IsPlayerControlled = false;
            Scenario.Cur.PauseTrunCount = Scenario.Cur.Info.turnCount + 1;
            Scenario.Cur.IsAlive = true;
        }

        public int GetRelation(Force forceA, Force forceB)
        {
            if (forceA == null || forceB == null || forceA == forceB)
                return 0;

            // 确保索引有效
            if (forceA.Id < 0 || forceA.Id >= RelationMap.Length || forceB.Id < 0 || forceB.Id >= RelationMap[0].Length)
                return 0;

            return RelationMap[forceA.Id][forceB.Id];
        }

        public void AddRelation(Force forceA, Force forceB, int v)
        {
            if (forceA == null || forceB == null || forceA == forceB)
                return;

            // 确保索引有效
            if (forceA.Id < 0 || forceA.Id >= RelationMap.Length || forceB.Id < 0 || forceB.Id >= RelationMap[0].Length)
                return;

            int r = RelationMap[forceA.Id][forceB.Id] + v;

            if (r < -5000) r = -5000;
            else if (r > 5000) r = 5000;

            RelationMap[forceA.Id][forceB.Id] = r;
            RelationMap[forceB.Id][forceA.Id] = r;
        }

        public string GetDateStr()
        {
            return $"{Info.year}年 {Info.month}月 {Info.day}日";
        }

        public Troop CreateTroop()
        {
            return new Troop();
        }

        public void Save(string path)
        {
            for (int i = 0; i < prepareList.Count; ++i)
            {
                prepareList[i].ForEach(o => { o.OnScenarioSave(this); });
            }

            Info.isSave = true;
            ScenarioCommonData saveData = CommonData;
            CommonData = null;
            View.cameraPosition = MapRender.Instance.mapCamera.position;
            View.cameraRotation = MapRender.Instance.mapCamera.lookRotate;
            View.cameraDistance = MapRender.Instance.mapCamera.distance;
            Info.dateTime = DateTime.Now.ToFileTime();
            if (string.IsNullOrEmpty(Info.curForceName))
                Info.curForceName = CurRunForce?.Name;
            Sango.Directory.Create(path, false);
            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Formatting = TKNewtonsoft.Json.Formatting.Indented;
            jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            jsonSerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; // 忽略循环引用
            JsonSerializer serializer = JsonSerializer.CreateDefault(jsonSerializerSettings);
            using (StreamWriter writer = System.IO.File.CreateText(path))
            {
                serializer.Serialize(writer, this);
            }
            CommonData = saveData;
        }

        public void Export(string path)
        {
            ScenarioCommonData saveData = CommonData;
            ScenarioVariables saveVariables = Variables;
            ScenarioView tempView = View;
            Map saveMap = Map;
            CommonData = null;
            Variables = null;
            Map = null;
            View = null;
            Sango.Directory.Create(path, false);
            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Formatting = TKNewtonsoft.Json.Formatting.Indented;
            jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            jsonSerializerSettings.DefaultValueHandling = DefaultValueHandling.Ignore;
            jsonSerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; // 忽略循环引用
            JsonSerializer serializer = JsonSerializer.CreateDefault(jsonSerializerSettings);
            using (StreamWriter writer = System.IO.File.CreateText(path))
            {
                serializer.Serialize(writer, this);
            }
            CommonData = saveData;
            Variables = saveVariables;
            Map = saveMap;
            View = tempView;
        }

        void LoadModModify()
        {
            string modifyDir = FilePath.Remove(FilePath.Length - 5);
            //Sango.Log.Error(modifyDir);
        }
    }
}
