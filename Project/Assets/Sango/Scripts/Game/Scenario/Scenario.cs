using Sango.Game.Render;
using Sango.Render;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;
using System.Xml;
using UnityEngine;
using Task = System.Threading.Tasks.Task;
using System.IO;
using UnityEngine.Rendering;

namespace Sango.Game
{

    [JsonObject(MemberSerialization.OptIn)]
    public class Scenario : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Scenario; } }

        #region Data
        [JsonProperty] public ScenarioInfo Info { get; internal set; }
        [JsonProperty] public ScenarioCommonData CommonData { internal set; get; }
        [JsonProperty] public ScenarioVariables Variables { internal set; get; }
        [JsonProperty] public Map Map { internal set; get; }

        [JsonConverter(typeof(SangoObjectSetConverter<Force>))]
        [JsonProperty] public SangoObjectSet<Force> forceSet = new SangoObjectSet<Force>();
        [JsonConverter(typeof(SangoObjectSetConverter<Corps>))]
        [JsonProperty] public SangoObjectSet<Corps> corpsSet = new SangoObjectSet<Corps>();
        [JsonConverter(typeof(SangoObjectSetConverter<City>))]
        [JsonProperty] public SangoObjectSet<City> citySet = new SangoObjectSet<City>();
        [JsonConverter(typeof(SangoObjectSetConverter<Person>))]
        [JsonProperty] public SangoObjectSet<Person> personSet = new SangoObjectSet<Person>();
        [JsonConverter(typeof(SangoObjectSetConverter<Troop>))]
        [JsonProperty] public SangoObjectSet<Troop> troopsSet = new SangoObjectSet<Troop>();
        [JsonConverter(typeof(SangoObjectSetConverter<Building>))]
        [JsonProperty] public SangoObjectSet<Building> buildingSet = new SangoObjectSet<Building>();
        [JsonConverter(typeof(SangoObjectSetConverter<Fire>))]
        [JsonProperty] public SangoObjectSet<Fire> fireSet = new SangoObjectSet<Fire>();

        /// 结盟信息
        /// </summary>
        [JsonConverter(typeof(SangoObjectSetConverter<Alliance>))]
        [JsonProperty] public SangoObjectSet<Alliance> allianceSet = new SangoObjectSet<Alliance>();

        /// <summary>
        /// 关系信息
        /// </summary>
        [JsonProperty] public int[][] RelationMap { get; set; }

        private ScenarioEvent scenarioEvent = new ScenarioEvent();
        public ScenarioEvent Event { get { return scenarioEvent; } }
        public Force Add(Force force) { forceSet.Add(force); return force; }
        public Corps Add(Corps corps) { corpsSet.Add(corps); return corps; }
        public City Add(City city) { citySet.Add(city); return city; }
        public Person Add(Person person) { personSet.Add(person); return person; }
        public Troop Add(Troop troop)
        {
            troopsSet.Add(troop);
            Event.OnTroopCreated?.Invoke(troop, this);
            return troop;
        }
        public Building Add(Building building) { buildingSet.Add(building); return building; }
        public Fire Add(Fire fire) { fireSet.Add(fire); return fire; }
        public Alliance Add(Alliance alliance) { allianceSet.Add(alliance); return alliance; }
        public Force Remove(Force force) { forceSet.Remove(force); return force; }
        public Corps Remove(Corps corps) { corpsSet.Remove(corps); return corps; }
        public City Remove(City city) { citySet.Remove(city); return city; }
        public Person Remove(Person person) { personSet.Remove(person); return person; }
        public Troop Remove(Troop troop)
        {
            troopsSet.Remove(troop);
            Event.OnTroopDestroyed?.Invoke(troop, this);
            return troop;
        }
        public Building Remove(Building building) { buildingSet.Remove(building); return building; }
        public Fire Remove(Fire fire) { fireSet.Remove(fire); return fire; }
        public Alliance Remove(Alliance alliance) { allianceSet.Remove(alliance); return alliance; }

        public int[][] cityDistanceMap;

        #endregion Data
        public static Scenario Cur { get; private set; }
        public static List<Scenario> all_scenario_list = new List<Scenario>();
        public string FilePath { internal set; get; }
        private Queue<Force> runForces = new Queue<Force>();
        public Force CurRunForce { get; private set; }
        public SeasonType CurSeason { get { return GameDefine.SeasonInMonth[Info.month - 1]; } }

        /// <summary>
        /// 调试用
        /// </summary>
        internal int PauseTrunCount = -1;


        public bool useThreadRun = false;
        Task task;

        public Scenario(string filePath)
        {
            this.FilePath = filePath;
            LoadInfo();
        }

        public Scenario()
        {

        }

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
            else if (tType == typeof(State))
            {
                return CommonData.States.Get(id);
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
        public static void Add(string path)
        {
            if (!File.Exists(path))
                return;

            Scenario scenario = new Scenario(path);
            all_scenario_list.Add(scenario);
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
                            return;
                        }
                    }
                }
            }
        }
        public void LoadContent()
        {
            LoadContent(FilePath);
        }

        public void LoadContent(string path)
        {
            //FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            //BinaryReader reader = new BinaryReader(fs);
            //LoadFromStream(reader);
            //reader.Close();
            //fs.Close();
            FilePath = path;

            if (CommonData == null)
                CommonData = GameData.Instance.LoadCommonData();
            if (Variables == null)
                Variables = new ScenarioVariables();
            if (Map == null)
                Map = new Map();

            JsonConvert.PopulateObject(File.ReadAllText(FilePath), this);

            Map.Load(Info.mapType);

        }

        public void LoadWorld()
        {
            MapRender.Instance.Init();
            MapRender.Instance.OnMapLoaded += OnWorldLoaded;
            MapRender.Instance.LoadMap(Map.FileName);
        }

        public void OnWorldLoaded()
        {
            Event.OnWorldLoadEnd?.Invoke(this);
            this.Map.Init(this);
            this.Prepare();
            this.Init(null);
            Event.OnStart?.Invoke(this);
            this.Start();
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
                Sango.Log.Print($"Find Scenario: {file}");
                Add(file);
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
        }

        public static void StartScenario(Scenario scenario)
        {
            GameRandom.Init();
            Cur = scenario;
            scenario.IsAlive = false;
            Cur.Event.OnLoadStart?.Invoke(scenario);
            Cur.LoadContent();
            Cur.Event.OnLoadEnd?.Invoke(Cur);
            Cur.Event.OnWorldLoadStart?.Invoke(Cur);
            Cur.LoadWorld();
            //Cur.OnWorldLoaded();
            //Event.OnScenarioEnd?.Invoke(Cur);
            //Cur = null;
        }

        public override void Clear()
        {
            IsAlive = false;
            base.Clear();
        }

        public void OnGameShutdown()
        {
            Clear();
        }
        public void OnGamePause()
        {
            isThreadPause = true;
        }
        public void OnGameResume()
        {
            isThreadPause = false;
        }

        public override void Init(Scenario scenario)
        {
            Sango.Game.Event.OnGameShutdown += OnGameShutdown;
            Sango.Game.Event.OnGamePause += OnGamePause;
            Sango.Game.Event.OnGameResume += OnGameResume;

            // 初始化城市距离信息
            int cityCount = citySet.Count;
            cityDistanceMap = new int[cityCount][];
            for (int i = 0; i < cityCount; i++)
            {
                cityDistanceMap[i] = new int[cityCount];
            }

            for (int i = 0; i < cityCount; ++i)
            {
                City city = citySet[i];
                if (city != null)
                {
                    cityDistanceMap[city.Id][city.Id] = 0;
                    for (int j = i + 1; j < cityCount; ++j)
                    {
                        City dest = citySet[j];
                        if (dest != null)
                        {
                            int len = city.Distance(dest);
                            cityDistanceMap[city.Id][dest.Id] = len;
                            cityDistanceMap[dest.Id][city.Id] = len;
                        }
                    }
                }
            }

            forceSet.ForEach(o =>
            {
                o.Init(this);
            });

            corpsSet.ForEach(o =>
            {
                o.Init(this);
            });

            citySet.ForEach(o =>
            {
                o.Init(this);
            });

            personSet.ForEach(o =>
            {
                o.Init(this);
            });

            buildingSet.ForEach(o =>
            {
                o.Init(this);
            });

            troopsSet.ForEach(o =>
            {
                o.Init(this);
            });

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
                        RelationMap[i][j] = 5000;
                        RelationMap[j][i] = 5000;
                    }
                }
            }
        }

        /// <summary>
        /// 在Init之前
        /// </summary>
        public void Prepare()
        {
            Event.OnPrepare?.Invoke(this);

            forceSet.ForEach(o =>
            {
                o.OnScenarioPrepare(this);
            });

            corpsSet.ForEach(o =>
            {
                o.OnScenarioPrepare(this);
            });

            citySet.ForEach(o =>
            {
                o.OnScenarioPrepare(this);
            });

            personSet.ForEach(o =>
            {
                o.OnScenarioPrepare(this);
            });

            buildingSet.ForEach(o =>
            {
                o.OnScenarioPrepare(this);
            });

            troopsSet.ForEach(o =>
            {
                o.OnScenarioPrepare(this);
            });

            //MapRender.Instance.Update();
        }


        bool isThreadPause = false;
        public void Start()
        {
            Window.Instance.HideWindow("window_start");
            Window.Instance.HideWindow("window_loading");
            Window.Instance.ShowWindow("window_game");
#if SANGO_DEBUG_AI
            GameAIDebug.Instance.Init();
#endif

            // 恢复游戏
            if (Info.curForceId > 0)
            {
                MakeForceQuene();
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

        public void MakeForceQuene()
        {
            forceSet.ForEach(force =>
            {
                if (force.IsAlive)
                {
                    force.ActionOver = false;
                    runForces.Enqueue(force);
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
                    person.OnNewTurn(this);
            }
            for (int i = 1; i < allianceSet.Count; i++)
            {
                Alliance a = allianceSet[i];
                if (a != null && a.IsAlive)
                    a.OnNewTurn(this);
            }

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
                    CurRunForce.OnTurnEnd(this);
                    scenarioEvent.OnForceEnd?.Invoke(CurRunForce, this);
                    CurRunForce = null;
                }
            }
            // 完成一轮
            if (runForces.Count <= 0)
            {
                return true;
            }
            CurRunForce = runForces.Dequeue();
            if (CurRunForce != null && CurRunForce.IsAlive)
            {
                Info.curForceId = CurRunForce.Id;
                CurRunForce.OnTurnStart(this);
                scenarioEvent.OnForceStart?.Invoke(CurRunForce, this);
            }
            return false;
        }

        public bool TurnEnd()
        {
            if (HasTurnEnded) return true;
            forceSet.ForEach(force =>
            {
                if (force.IsAlive)
                {
                    force.OnTurnEnd(this);
                }
            });
            HasTurnEnded = true;
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
                scenarioEvent.OnYearUpdate?.Invoke(this);
            }
            if (hasMonth)
            {
                OnMonthStart(this);
                scenarioEvent.OnMonthUpdate?.Invoke(this);
            }
            OnDayStart(this);
            scenarioEvent.OnDayUpdate?.Invoke(this);
            OnDayEnd(this);
            if (hasMonth)
            {
                OnMonthEnd(this);
            }
            if (hasYear)
            {
                OnYearEnd(this);
            }
            SeasonType cur_season = GameDefine.SeasonInMonth[Info.month - 1];
            if (cur_season != last_season)
            {
                OnSeasonStart(this);
                scenarioEvent.OnSeasonUpdate?.Invoke(this);
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
#if SANGO_DEBUG
            if (PauseTrunCount == Info.turnCount)
                return;
#endif
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
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnDayStart(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnDayStart(this);
                }
            });
            return base.OnDayStart(scenario);
        }

        public override bool OnDayEnd(Scenario scenario)
        {
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnDayEnd(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnDayEnd(this);
                }
            });
            return base.OnDayEnd(scenario);
        }

        public override bool OnMonthStart(Scenario scenario)
        {
            int forceCount = forceSet.Count;
            for (int i = 0; i < forceCount; ++i)
            {
                for (int j = i + 1; j < forceCount; ++j)
                {
                    if (GameRandom.Changce(scenario.Variables.relationChangeChangce))
                    {
                        RelationMap[i][j] += scenario.Variables.relationChangePerMonth;
                        RelationMap[j][i] += scenario.Variables.relationChangePerMonth;
                    }
                }
            }

            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnMonthStart(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnMonthStart(this);
                }
            });
            scenario.forceSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnMonthStart(this);
                }
            });
            return base.OnMonthStart(scenario);
        }
        public override bool OnMonthEnd(Scenario scenario)
        {
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnMonthEnd(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnMonthEnd(this);
                }
            });
            scenario.forceSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnMonthEnd(this);
                }
            });
            return base.OnMonthEnd(scenario);
        }
        public override bool OnYearStart(Scenario scenario)
        {
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnYearStart(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnYearStart(this);
                }
            });
            scenario.forceSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnYearStart(this);
                }
            });
            return base.OnYearStart(scenario);
        }
        public override bool OnYearEnd(Scenario scenario)
        {
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnYearEnd(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnYearEnd(this);
                }
            });
            scenario.forceSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnYearEnd(this);
                }
            });
            return base.OnYearEnd(scenario);
        }
        public override bool OnSeasonStart(Scenario scenario)
        {
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnSeasonStart(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnSeasonStart(this);
                }
            });
            scenario.forceSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnSeasonStart(this);
                }
            });
            return base.OnSeasonStart(scenario);
        }
        public override bool OnSeasonEnd(Scenario scenario)
        {
            scenario.personSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnSeasonEnd(this);
                }
            });
            scenario.citySet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnSeasonEnd(this);
                }
            });
            scenario.forceSet.ForEach(p =>
            {
                if (p.IsAlive)
                {
                    p.OnSeasonEnd(this);
                }
            });
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
            return cityDistanceMap[a.Id][b.Id];
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
            return RelationMap[forceA.Id][forceB.Id];
        }

        public void AddRelation(Force forceA, Force forceB, int v)
        {
            int r = RelationMap[forceA.Id][forceB.Id] + v;

            if (r < -10000) r = -10000;
            else if (r > 10000) r = 10000;

            RelationMap[forceA.Id][forceB.Id] = r;
            RelationMap[forceB.Id][forceA.Id] = r;
        }

        public string GetDateStr()
        {
            return $"{Info.year}年{Info.month}月{Info.day}日";
        }
    }
}
