using Newtonsoft.Json;
using Sango.Hexagon;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class ShortForce : SangoObject
    {
        [JsonProperty] public int Governor;
        [JsonProperty] public int Counsellor;
        [JsonProperty] public int Flag;
        /// <summary>国号 Id（对齐 Open311 `nation_id`；0 表示未定国号）</summary>
        [JsonProperty] public int Nation;
        /// <summary>爵位 Id（对齐 Open311 `title_id`；0 表示未设置，运行时解析为「无」）。JSON 键沿用运行时 Force 的 "Title"。</summary>
        [JsonProperty("Title")] public int TitleId;
        [JsonProperty] public string desc;
        public bool IsPlayer;
        public bool IsAppend;
        public int CapitalCity;
        public int CapitalCorps;
        public int cityCount;
        public int personCount;
        public int totalGold;
        public int totalFood;
        public int totalTroops;

        public ShortForce Copy()
        {
            return new ShortForce()
            {
                Id = Id,
                Name = Name,
                Governor = Governor,
                Counsellor = Counsellor,
                Flag = Flag,
                Nation = Nation,
                TitleId = TitleId,
                desc = desc,
                IsPlayer = IsPlayer,
                IsAppend = IsAppend,
                CapitalCity = CapitalCity,
            };
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ShortPerson : SangoObject
    {
        [JsonProperty("BelongForce")] public int BelongForceId;
        [JsonProperty("BelongCity")] public int BelongCityId;
        [JsonProperty] public int headIconID;
        [JsonProperty] public string imageID;
        public PersonLib PersonLib;
        public int state;
        public static ShortPerson FormLib(PersonLib personLib, ShortScenario scenario)
        {
            ShortPerson shortPerson = new ShortPerson();
            shortPerson.Name = personLib.Name;
            shortPerson.PersonLib = personLib;
            shortPerson.BelongForceId = personLib.GetBelongForceId(scenario);
            shortPerson.BelongCityId = personLib.GetBelongCityId(scenario);
            shortPerson.headIconID = personLib.headIconID;
            shortPerson.imageID = personLib.imageID;
            return shortPerson;
        }

        public ShortPerson Copy()
        {
            return new ShortPerson()
            {
                Id = Id,
                Name = Name,
                BelongForceId = BelongForceId,
                BelongCityId = BelongCityId,
                headIconID = headIconID,
                imageID = imageID,
                PersonLib = PersonLib,
                state = state,
            };
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class ShortCity : SangoObject
    {
        // OptOut：字段默认都会参与序列化，所以改名后必须显式钉住原键名，否则旧剧本文件读不出归属
        [JsonProperty("BelongForce")] public int BelongForceId;
        [JsonProperty("BelongCorps")] public int BelongCorpsId;
        public int BuildingType;
        public int x;
        public int y;
        public int troops;
        public int gold;
        public int food;

        public bool IsCity()
        {
            return BuildingType == 1 || BuildingType == 65;
        }

        public Vector3 CityScale
        {
            get
            {
                if (BuildingType == 1)
                    return Vector3.one;
                else
                    return Vector3.one * 0.75f;
            }
        }


        public ShortCity Copy()
        {
            return new ShortCity()
            {
                Id = Id,
                Name = Name,
                BelongForceId = BelongForceId,
                BelongCorpsId = BelongCorpsId,
                BuildingType = BuildingType,
                x = x,
                y = y,
                troops = troops,
                gold = gold,
                food = food,
            };
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class ShortMap
    {
        public int Width;
        public int Height;
        public string Name;
        public float GridSize;
        HexWorld HexWorld;
        public string WorkContent;

        public static List<ShortMap> all_map_info_list = new List<ShortMap>();

        public static ShortMap GetMap(string mpName)
        {
            return all_map_info_list.Find(x => x.Name == mpName);
        }

        public void Init(ShortScenario scenario)
        {
            string mapName = scenario.Info.mapType;
            string FileName = Path.FindFile($"Map/{mapName}.bin");
            if (File.Exists(FileName))
            {
                Name = mapName;
                FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                BinaryReader reader = new BinaryReader(fs);
                int versionCode = reader.ReadInt32();
                if (versionCode < 6)
                {
                    return;
                }
                WorkContent = reader.ReadString();
                int mapWidth = reader.ReadInt32();
                int mapHeight = reader.ReadInt32();
                int grid_size = reader.ReadInt32();
                Width = mapWidth / 4;
                Height = mapHeight / 4;
                GridSize = grid_size;

                reader.Close();
                fs.Close();
                reader.Dispose();
                fs.Dispose();
            }

            HexWorld = new Hexagon.HexWorld(new Hexagon.Point(GridSize, GridSize), new Hexagon.Point(0, 0));

        }
        public Vector3 Coords2Position(int x, int y)
        {
            return HexWorld.CoordsToPosition(x, y);
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ShortScenario
    {
        [JsonProperty(Order = -99)]
        private int _Id = -1;
        public int Id { get { return _Id; } set { _Id = value; } }

        [JsonProperty(Order = -98)]
        public virtual string Name { get; set; }

        #region Data
        [JsonProperty] public ScenarioInfo Info { get; internal set; }
        [JsonProperty] public ScenarioCommonData CommonData { internal set; get; }
        [JsonProperty] public ScenarioVariables Variables { internal set; get; }
        [JsonProperty] public ShortMap Map { internal set; get; }
        [JsonProperty] public SangoObjectSet<ShortForce> forceSet = new SangoObjectSet<ShortForce>();
        [JsonProperty] public SangoObjectSet<ShortPerson> personSet = new SangoObjectSet<ShortPerson>();
        [JsonProperty] public SangoObjectSet<ShortCity> citySet = new SangoObjectSet<ShortCity>();
        #endregion Data
        public static ShortScenario Cur { get; private set; }
        public static List<ShortScenario> all_scenario_info_list = new List<ShortScenario>();
        public static ShortScenario CurSelected { get; set; }
        public string ModName { internal set; get; }

        /// <summary>模组一致性是否已经算过(延迟计算,见 EnsureModsState)</summary>
        bool modsStateReady;
        /// <summary>内部缓存:模组是否不一致</summary>
        bool modsDiffer;
        /// <summary>内部缓存:差异说明</summary>
        string modsDiffMessage;

        /// <summary>
        /// 存档记录的模组列表与"当前启用的模组"是否不一致。
        /// 仅在存档(<see cref="ScenarioInfo.isSave"/>)上有意义;
        /// 不一致时读档界面只展示基础信息(不解析地图/势力),并且必须玩家确认后才尝试加载。
        ///
        /// 注意:这里必须是"用时才算"的延迟计算 —— 存档列表在启动时(<see cref="Sango.Core.Player.Player.Init"/>)
        /// 就被扫描了,而那时 <see cref="Sango.Mod.ModManager.InitMods"/> 还没执行、启用列表还是空的,
        /// 提前比对会把所有存档都误判成"模组不一致"。
        /// </summary>
        public bool ModsDiffer
        {
            get
            {
                EnsureModsState();
                return modsDiffer;
            }
        }

        /// <summary>
        /// 模组差异说明(中文,面向玩家),一致时为 null
        /// </summary>
        public string ModsDiffMessage
        {
            get
            {
                EnsureModsState();
                return modsDiffMessage;
            }
        }

        /// <summary>
        /// 让模组一致性状态失效,下次访问 <see cref="ModsDiffer"/> 时重新计算。
        /// 存档刚保存完、或模组列表变化后调用。
        /// </summary>
        public void RefreshModsState()
        {
            modsStateReady = false;
            modsDiffer = false;
            modsDiffMessage = null;
        }

        /// <summary>
        /// 延迟计算"存档记录的模组"与"当前启用模组"的差异。
        /// 只使用文件头部的 Info,不触发地图/势力解析。
        /// </summary>
        void EnsureModsState()
        {
            if (modsStateReady)
                return;
            modsStateReady = true;
            modsDiffer = false;
            modsDiffMessage = null;

            // 只有存档才有"当时的模组环境"这一概念;剧本定义文件不参与对比
            if (Info == null || !Info.isSave)
                return;

            string message;
            if (!Sango.Mod.ModManager.Instance.MatchSavedMods(Info.mods, out message))
            {
                modsDiffer = true;
                modsDiffMessage = message;
            }
        }

        public string FilePath { internal set; get; }
        Task task;
        public bool loadOK = false;
        public bool loadFullPersons = false;

        ShortScenario()
        {

        }

        /// <summary>
        /// 直接从内存中的 <see cref="ScenarioInfo"/> 构造一个"存档列表用"的短剧本。
        ///
        /// 避免"存完盘再打开文件把 Info 读一遍"的冗余 IO：刚写出的存档，其 Info 就是内存里这一份。
        /// 只填 Info（以及 Name / FilePath）：正文（forceSet / personSet / citySet）仍由
        /// <see cref="LoadContent"/> 在真正需要时惰性读取，行为与走文件构造完全一致。
        /// </summary>
        /// <param name="info">存档当时的信息快照（调用方应传入副本，避免与进行中的剧本共享引用）</param>
        /// <param name="filePath">存档文件路径</param>
        public static ShortScenario FromInfo(ScenarioInfo info, string filePath)
        {
            ShortScenario scenario = new ShortScenario();
            scenario.FilePath = filePath;
            scenario.Info = info;
            scenario.Name = info != null ? info.name : null;
            scenario.loadOK = false;
            // 刚保存出来的存档,其 Info 已记录当前模组;这里同样刷新一次,保证列表颜色立即正确
            scenario.RefreshModsState();
            return scenario;
        }

        public ShortScenario Copy()
        {
            ShortScenario scenario = new ShortScenario()
            {
                Info = Info,
                FilePath = FilePath,
                CommonData = CommonData,
                Map = Map,
            };
            citySet.ForEach(x => scenario.citySet.Add(x.Copy()));
            forceSet.ForEach(x => scenario.forceSet.Add(x.Copy()));
            personSet.ForEach(x => scenario.personSet.Add(x.Copy()));
            return scenario;
        }

        public ShortScenario(string filePath)
        {
            this.FilePath = filePath;
            LoadInfo();
            // LoadContent();
            loadOK = false;
        }

        /// <summary>
        /// 不读任何文件的构造：**仅用于原版（.s11）剧本**。
        ///
        /// 原版剧本没有 Sango 的 JSON，若走 ShortScenario(string) 会触发 LoadInfo 的
        /// JSON 解析并刷一条"存档信息读取失败"错误日志。Info 由 AddFromOpen311 填充。
        /// </summary>
        ShortScenario(bool skipLoad)
        {
            loadOK = false;
        }

        public ShortScenario(string filePath, bool notask)
        {
            this.FilePath = filePath;
            LoadInfo();
            //if (!notask)
            //{
            //    LoadContent();
            //    loadOK = true;
            //}
            //else
            //{
            //    task = Task.Run(() =>
            //    {
            //        LoadContent();
            //        loadOK = true;
            //    });
            //}
        }

        public static ShortScenario Add(string path)
        {
            if (!File.Exists(path))
                return null;

            ShortScenario scenario = new ShortScenario(path);

            all_scenario_info_list.Add(scenario);
            return scenario;
        }

        // ==================== 原版（三国志11 PK）剧本 ====================
        //
        // 原版剧本没有 Sango 的 JSON，只有 .s11 二进制。这里为其生成一个"摘要"
        // 挂进 all_scenario_info_list，剧本选择界面即可与普通剧本并列显示。
        // 真正开局时由 Scenario.StartOpen311Scenario 加载骨架 JSON 再覆盖数值。

        /// <summary>原版剧本文本（ScenXXX.S11）；null = 不是原版剧本</summary>
        public string Open311ScenPath { get; private set; }

        /// <summary>原版静态表（Scenario.s11），提供城市名/设施/地形等</summary>
        public string Open311ScenarioPath { get; private set; }

        /// <summary>是否是原版剧本</summary>
        public bool IsOpen311 { get { return !string.IsNullOrEmpty(Open311ScenPath); } }

        /// <summary>
        /// 用原版数据填 <see cref="forceSet"/> / <see cref="citySet"/> 摘要。
        ///
        /// 【为什么需要】选剧本界面（UIScenarioSelect.ShowScenario → UpdateMap）会遍历
        /// citySet 画城市点位，并按 city.BelongForceId 查 forceSet 取势力配色。
        /// 原版剧本若不填，这两个集合为空 → 地图上不显示任何城市。
        ///
        /// 【对应原版】ScenSummary 提供 剧本名/说明/城市颜色与坐标/势力难度与说明；
        /// 这里的归属（district → force）与坐标来自 Scenario 段与 Building 段。
        /// </summary>
        void BuildOpen311Summary()
        {
            Open311ScenarioData data = new Open311ScenarioData();
            string error;
            if (!Open311ScenarioParser.ParseScen(Open311ScenPath, data, out error))
            {
                Log.Warning($"原版剧本摘要解析失败：{error}");
                return;
            }
            if (!string.IsNullOrEmpty(Open311ScenarioPath))
                Open311ScenarioParser.ParseScenario(Open311ScenarioPath, data, out error);

            // ---------------- 势力 ----------------
            for (int i = 0; i < data.Forces.Count; i++)
            {
                Open311ForceData f = data.Forces[i];
                if (f.Kunshu < 0) continue;                  // 空势力

                ShortForce sf = new ShortForce();
                sf.Id = i + 1;
                sf.Governor = f.Kunshu + 1;
                sf.Counsellor = f.Gunshi >= 0 ? f.Gunshi + 1 : 0;
                sf.Flag = f.Color + 1;                       // CommonData.Flags 从 1 开始
                sf.Nation = f.Kokugou >= 0 ? f.Kokugou + 1 : 0;
                sf.TitleId = f.Title >= 0 ? f.Title + 1 : 0;

                // 君主名（供 ShortForce.Name 显示；personSet 在第 3 趟统一填，
                // 因为君主的 BelongCityId 需要先有 citySet 才能定出"首都"）
                if (f.Kunshu < data.Persons.Count)
                {
                    Open311PersonData k = data.Persons[f.Kunshu];
                    sf.Name = k.FamilyName + k.GivenName;
                }

                forceSet.Add(sf);
            }

            // ---------------- 城市（只填 42 座城，关/港不在预览里画）----------------
            for (int i = 0; i < data.Cities.Count && i < data.Buildings.Count; i++)
            {
                Open311CityData c = data.Cities[i];
                Open311BuildingData b = data.Buildings[i];

                ShortCity sc = new ShortCity();
                sc.Id = i + 1;
                sc.BuildingType = 1;                         // 1 = 城
                sc.x = b.PosX + Open311ScenarioImporter.MapOffset;
                sc.y = b.PosY + Open311ScenarioImporter.MapOffset;
                sc.troops = (int)c.Troops;
                sc.gold = (int)c.Gold;
                sc.food = (int)c.Food;
                if (i < data.CityInfos.Count)
                    sc.Name = data.CityInfos[i].Name;

                // 归属：city.district → district.force → Sango 势力 Id（+1）
                if (c.District >= 0 && c.District < data.Districts.Count)
                {
                    int forceId = data.Districts[c.District].Force;
                    sc.BelongForceId = forceId >= 0 ? forceId + 1 : 0;
                }

                citySet.Add(sc);
            }

            // ---------------- 君主 / 军师（第 3 趟）----------------
            // 势力详情界面会读 personSet[Governor].BelongCityId 再查 citySet 取"都城名"，
            // 所以这里必须把君主挂到该势力真正拥有的某座城上（取 Id 最小的城当都城）。
            for (int i = 0; i < data.Forces.Count; i++)
            {
                Open311ForceData f = data.Forces[i];
                if (f.Kunshu < 0) continue;

                int capitalId = 0;
                foreach (ShortCity c in citySet)
                {
                    if (c == null || c.BelongForceId != i + 1 || c.BuildingType != 1) continue;
                    capitalId = c.Id;
                    break;
                }

                if (f.Kunshu < data.Persons.Count)
                {
                    Open311PersonData k = data.Persons[f.Kunshu];
                    AddSummaryPerson(f.Kunshu + 1, k.FamilyName + k.GivenName, capitalId);
                }
                if (f.Gunshi >= 0 && f.Gunshi < data.Persons.Count)
                {
                    Open311PersonData g = data.Persons[f.Gunshi];
                    AddSummaryPerson(f.Gunshi + 1, g.FamilyName + g.GivenName, capitalId);
                }
            }
        }

        /// <summary>
        /// 往 personSet 里放一条"只带名字"的摘要（君主 / 军师）。
        ///
        /// 【为什么要填】UIScenarioForceSelect.SetSelectedForceName 会读
        /// `scenario.personSet[force.Governor]` 取君主名显示；personSet 为空时下标越界。
        /// 这里只放势力选择界面真正会读到的少数几个人，不是完整武将表。
        /// </summary>
        void AddSummaryPerson(int id, string name, int belongCityId)
        {
            if (id <= 0 || string.IsNullOrEmpty(name)) return;

            ShortPerson p = new ShortPerson();
            p.Id = id;
            p.Name = name;
            p.BelongCityId = belongCityId;
            personSet.Add(p);
        }

        /// <summary>
        /// 从原版 .s11 生成剧本摘要（不读 JSON、不解析全部对象，只取 ScenSummary 段）。
        /// </summary>
        /// <param name="scenS11Path">ScenXXX.S11 完整路径</param>
        /// <param name="scenarioS11Path">同一版本目录下的 Scenario.s11 完整路径（可为 null）</param>
        public static ShortScenario AddFromOpen311(string scenS11Path, string scenarioS11Path)
        {
            if (!File.Exists(scenS11Path)) return null;

            Open311ScenarioData data = new Open311ScenarioData();
            string error;
            if (!Open311ScenarioParser.ParseScen(scenS11Path, data, out error))
            {
                Log.Warning($"原版剧本解析失败：{scenS11Path} -> {error}");
                return null;
            }

            // 不读 JSON（原版文件不是 JSON），Info 直接用 ScenSummary 重建
            ShortScenario sc = new ShortScenario(true);
            sc.FilePath = scenS11Path;
            sc.Open311ScenPath = scenS11Path;
            sc.Open311ScenarioPath = scenarioS11Path;
            sc.Info = new ScenarioInfo();

            // id 与普通剧本的 id 段错开，且保证唯一。
            // 不能用 data.Number：PK 版部分剧本重编号，会出现两个剧本 Number 相同（如 Scen014/015）。
            sc.Info.id = 1000 + all_scenario_info_list.Count;
            sc.Info.name = data.ScenName;
            sc.Info.description = data.ScenDesc;
            sc.Info.year = data.StartYear;
            sc.Info.startYear = data.StartYear;
            sc.Info.month = data.StartMonth;
            sc.Info.day = data.StartDay;
            sc.Info.mapType = "DefaultMap";
            sc.Info.type = 1;
            sc.Info.priority = 0;
            sc.Name = data.ScenName;
            sc.ModName = "<color=#66ccff>原版</color>";

            all_scenario_info_list.Add(sc);
            return sc;
        }

        public void LoadInfo()
        {
            LoadInfo(FilePath);
        }
        public void LoadInfo(string path)
        {
            FilePath = path;

            // 只读文件头部的 Info 段,不解析地图/势力/武将。
            // 这里必须做异常保护:存档损坏(JSON 截断/被改坏)时不能让整个存档列表初始化直接抛出去,
            // 否则玩家一进读档界面就整个界面打不开。
            try
            {
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
                                Name = Info != null ? Info.name : null;
                                RefreshModsState();
                                return;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Info = null;
                Name = null;
                RefreshModsState();
                Sango.Log.Error($"存档信息读取失败(文件可能已损坏): {path} {e.Message}");
            }
        }

        public void LoadContent()
        {
            LoadContent(FilePath);
        }

        public void LoadContent(string path)
        {
            if (loadOK) return;

            // ---------------- 原版（.s11）剧本 ----------------
            // 原版剧本不是 Sango 的 JSON，不能走下面的流式反序列化。
            // 这里只准备"剧本列表预览"需要的最小内容（通用数据 + 地图信息），
            // 真正的势力/城池/武将数值在开局时由 Scenario.CreateFromOpen311 解析 .s11 得到。
            if (IsOpen311)
            {
                Cur = this;
                if (CommonData == null)
                    CommonData = GameData.Instance.LoadCommonData();

                // 地图固定 DefaultMap。必须走 Init（而不是手工填字段）：
                // Init 会从 Map/DefaultMap.bin 头读出 Width/Height/GridSize/WorkContent，
                // 并创建 HexWorld —— 后者是 Coords2Position 的前提，缺了会空引用。
                Map = new ShortMap();
                Map.Init(this);

                // 摘要：对应原版 ScenSummary（剧本名/说明/城市颜色与坐标/势力难度与说明）。
                // 这里直接从 .s11 解析出来，供"选剧本"界面预览地图上的城市点位与势力配色。
                BuildOpen311Summary();

                loadOK = true;
                return;
            }

            Cur = this;
            if (CommonData == null)
                CommonData = GameData.Instance.LoadCommonData();

            //if (Variables == null)
            //    Variables = new ScenarioVariables();

            // =====================================================================
            //  使用自定义 JsonConverter<T> 实现的零反射、最高速反序列化路径。
            //  - ShortForce / ShortPerson / ShortCity 均通过手写 token 解析完成
            //    （ShortScenarioConverters.cs），不依赖反射 / JObject / Populate。
            //  - 字典本身的反序列化由 JsonSerializer 自动使用已注册的值转换器，
            //    整个分支没有反射调用。
            //  - 只需手动流式扫描顶级属性名（forceSet / personSet / citySet），
            //    其余字段由 reader.Skip() 一次性跳过。
            //  - forceSet 加载完毕后，收集所有势力的 Governor/Counsellor ID，
            //    personSet 使用 ShortPersonSetConverter 仅解析这些主公/军师，
            //    其余 entry 通过 reader.Skip() 零开销跳过。
            // =====================================================================
            JsonSerializer serializer = JsonSerializer.CreateDefault();
            serializer.Converters.Add(new ShortForceConverter());
            serializer.Converters.Add(new ShortPersonConverter());
            serializer.Converters.Add(new ShortCityConverter());

            // 主公/军师武将 ID 集合，forceSet 加载完后填充
            HashSet<int> mainPersonIds = null;

            using (StreamReader file = System.IO.File.OpenText(FilePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                bool personSetLoaded = false;
                while (!personSetLoaded && reader.Read()) // 流式推进，零反射定位目标属性
                {
                    // 只对顶级属性名感兴趣，其余 token（StartObject / EndObject / 标量等）一律跳过
                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    string propName = (string)reader.Value;
                    switch (propName)
                    {
                        case "forceSet":
                            // 推进到值（StartObject），反序列化字典
                            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                            {
                                ShortForceSetConverter filter = new ShortForceSetConverter();
                                forceSet = (SangoObjectSet<ShortForce>)filter.ReadJson(
                                        reader, typeof(SangoObjectSet<ShortForce>), null, serializer);
                            }

                            // 收集所有势力的主公（Governor）与军师（Counsellor）ID
                            if (forceSet != null)
                            {
                                mainPersonIds = new HashSet<int>();
                                forceSet.ForEach(force =>
                                {
                                    if (force.Governor != 0) mainPersonIds.Add(force.Governor);
                                    if (force.Counsellor != 0) mainPersonIds.Add(force.Counsellor);
                                });
                            }
                            break;
                        case "personSet":
                            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                            {
                                ShortPersonSetConverter filter = new ShortPersonSetConverter(mainPersonIds);
                                personSet = (SangoObjectSet<ShortPerson>)filter.ReadJson(
                                    reader, typeof(SangoObjectSet<ShortPerson>), null, serializer);
                            }
                            // personSet 是最后一个真正需要的目标，加载完成即可跳出
                            personSetLoaded = true;
                            break;
                        case "citySet":
                            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                            {
                                ShortCitySetConverter filter = new ShortCitySetConverter();
                                citySet = (SangoObjectSet<ShortCity>)filter.ReadJson(
                                        reader, typeof(SangoObjectSet<ShortCity>), null, serializer);
                            }
                            break;
                        default:
                            // 跳过其他字段（Info / Map / Variables / CommonData 等）
                            reader.Skip();
                            break;
                    }
                }
            }


            //JsonConvert.PopulateObject(File.ReadAllText(FilePath), this);

            // 玩家确定
            if (Info.playerForceList != null && Info.playerForceList.Length > 0)
            {
                for (int m = 0; m < forceSet.Count; m++)
                {
                    ShortForce force = forceSet[m];
                    if (force != null)
                    {
                        for (int k = 0; k < Info.playerForceList.Length; k++)
                        {
                            if (Info.playerForceList[k] == force.Id)
                            {
                                force.IsPlayer = true;
                                break;
                            }
                        }
                    }
                }
            }

            ShortMap targetMap = ShortMap.GetMap(Info.mapType);
            if (targetMap == null)
            {
                Map = new ShortMap();
                Map.Init(this);
                ShortMap.all_map_info_list.Add(Map);
            }
            else
                Map = targetMap;

            loadOK = true;
        }

        public void LoadFullPersonContent()
        {
            if (loadFullPersons) return;
            Cur = this;

            // =====================================================================
            //  使用自定义 JsonConverter<T> 实现的零反射、最高速反序列化路径。
            //  - ShortForce / ShortPerson / ShortCity 均通过手写 token 解析完成
            //    （ShortScenarioConverters.cs），不依赖反射 / JObject / Populate。
            //  - 字典本身的反序列化由 JsonSerializer 自动使用已注册的值转换器，
            //    整个分支没有反射调用。
            //  - 只需手动流式扫描顶级属性名（forceSet / personSet / citySet），
            //    其余字段由 reader.Skip() 一次性跳过。
            //  - forceSet 加载完毕后，收集所有势力的 Governor/Counsellor ID，
            //    personSet 使用 ShortPersonSetConverter 仅解析这些主公/军师，
            //    其余 entry 通过 reader.Skip() 零开销跳过。
            // =====================================================================
            JsonSerializer serializer = JsonSerializer.CreateDefault();
            serializer.Converters.Add(new ShortPersonConverter());

            // 主公/军师武将 ID 集合，forceSet 加载完后填充
            HashSet<int> mainPersonIds = null;

            using (StreamReader file = System.IO.File.OpenText(FilePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                bool personSetLoaded = false;
                while (!personSetLoaded && reader.Read()) // 流式推进，零反射定位目标属性
                {
                    // 只对顶级属性名感兴趣，其余 token（StartObject / EndObject / 标量等）一律跳过
                    if (reader.TokenType != JsonToken.PropertyName)
                        continue;

                    string propName = (string)reader.Value;
                    switch (propName)
                    {
                        case "personSet":
                            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                            {
                                // 没有 forceSet 时退回全量加载
                                ShortPersonSetConverter filter = new ShortPersonSetConverter(null);
                                personSet = (SangoObjectSet<ShortPerson>)filter.ReadJson(
                                    reader, typeof(SangoObjectSet<ShortPerson>), null, serializer);
                            }
                            // personSet 是最后一个真正需要的目标，加载完成即可跳出
                            personSetLoaded = true;
                            break;
                        default:
                            // 跳过其他字段（Info / Map / Variables / CommonData 等）
                            reader.Skip();
                            break;
                    }
                }
            }
            loadFullPersons = true;
        }

        public string GetIDName()
        {
            ScenarioInfo scenarioInfo = Info;
            return $" {scenarioInfo.id}. {scenarioInfo.year}年 {scenarioInfo.month}月 {scenarioInfo.name}";
        }

        public string GetDateName()
        {
            ScenarioInfo scenarioInfo = Info;
            return $"{scenarioInfo.year}年 {scenarioInfo.month}月 {scenarioInfo.name}";
        }

        public string GetModIDName(string mod)
        {
            ScenarioInfo scenarioInfo = Info;
            return $" {scenarioInfo.id}. {scenarioInfo.year}年 {scenarioInfo.month}月 {scenarioInfo.name}<{mod}>";
        }

        bool needUpdateAppendInfo = true;
        public void NeedUpdateAppendInfo()
        {
            needUpdateAppendInfo = true;
        }
        int _AppendPersonCount;
        int _AssignedPersonCount;
        int _AppendForceCount;
        void UpdateAppendInfo()
        {
            if (!needUpdateAppendInfo)
                return;
            needUpdateAppendInfo = false;
            _AppendPersonCount = 0;
            _AssignedPersonCount = 0;
            _AppendForceCount = 0;
            for (int i = 1; i < personSet.Count; i++)
            {
                ShortPerson shortPerson = personSet[i];
                if (shortPerson != null)
                {
                    if (shortPerson.PersonLib != null)
                    {
                        _AppendPersonCount++;
                        if (shortPerson.BelongCityId > 0)
                            _AssignedPersonCount++;
                    }
                }
            }
            for (int i = 1; i < forceSet.Count; i++)
            {
                ShortForce shortPerson = forceSet[i];
                if (shortPerson != null)
                {
                    if (shortPerson.IsAppend)
                    {
                        _AppendForceCount++;
                    }
                }
            }
        }

        public int AppendPersonCount
        {
            get
            {
                UpdateAppendInfo();
                return _AppendPersonCount;
            }
        }

        public int AssignedPersonCount
        {
            get
            {
                UpdateAppendInfo();
                return _AssignedPersonCount;
            }
        }
        public int AppendForceCount
        {
            get
            {
                UpdateAppendInfo();
                return _AppendForceCount;
            }
        }

        /// <summary>
        /// 查找一个未被占用的旗帜。
        /// </summary>
        public int FindEmptyFlag()
        {
            List<int> used_list = new List<int>();
            forceSet.ForEach(x => used_list.Add(x.Flag));

            for (int i = CommonData.Flags.Count; i > 0; i--)
            {
                Flag flag = CommonData.Flags[i];
                if (flag != null && !used_list.Contains(flag.Id))
                {
                    return flag.Id;
                }
            }
            return 0;
        }
    }


    public class ShortForceSortFunction : Singleton<ShortForceSortFunction>
    {
        public delegate string PersonValueStrGet(ShortForce person);
        public delegate int PersonValueGet(ShortForce person);
        public delegate int PersonSortFunc(ShortForce person1, ShortForce person2);

        /// <summary>
        /// 获取Person对象属性值的object类型代理
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonValueObjGet(ShortForce person);

        /// <summary>
        /// 设置Person对象属性值的代理
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonValueObjSet(ShortForce person, object value);

        public class SortTitle : ObjectSortTitle
        {
            public PersonValueStrGet valueGetCall;
            public PersonSortFunc personSortFunc;
            public PersonValueObjGet valueObjGet;
            public PersonValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((ShortForce)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((ShortForce)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueGetCall.Invoke((ShortForce)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return personSortFunc.Invoke((ShortForce)a, (ShortForce)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    isNameColumn = isNameColumn,
                    nameExtraChars = nameExtraChars,
                    valueGetCall = valueGetCall,
                    personSortFunc = personSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        public static SortTitle SortByName(ShortScenario scenario)
        {

            return new SortTitle
            {
                isNameColumn = true,        // 名字列：宽度按剧本参数"名字最大支持字数"计算(1.05×字数)
                name = "势力",
                width = 3.00f,
                valueGetCall = x =>
                {

                    ShortPerson person = scenario.personSet.Get(x.Governor);
                    return person.Name;
                },
                personSortFunc = (a, b) => a.Governor.CompareTo(b.Governor),
                valueObjGet = x => x.Governor,
                valueObjSet = null,
            };
        }

        public static SortTitle SortByPersonCount(ShortScenario scenario)
        {

            return new SortTitle()
            {
                name = "武将",
                width = 1.40f,
                valueGetCall = x =>
                {
                    x.personCount = 0;
                    scenario.personSet.ForEach(p =>
                    {
                        if (p.BelongForceId == x.Id)
                        {
                            x.personCount++;
                        }
                    });
                    return x.personCount.ToString();
                },
                personSortFunc = (a, b) => a.personCount.CompareTo(b.personCount),
                valueObjGet = x => x.personCount,
                valueObjSet = null,
            };
        }

        public static SortTitle SortByCityCount(ShortScenario scenario)
        {

            return new SortTitle()
            {
                name = "都市",
                width = 1.40f,
                valueGetCall = x =>
                {
                    x.cityCount = 0;
                    x.totalTroops = 0;
                    x.totalGold = 0;
                    x.totalFood = 0;
                    scenario.citySet.ForEach(p =>
                    {
                        if (p.BelongForceId == x.Id)
                        {
                            x.cityCount++;
                            x.totalTroops += p.troops;
                            x.totalGold += p.gold;
                            x.totalFood += p.food;
                        }
                    });

                    if (x.IsAppend)
                    {
                        x.totalTroops = x.cityCount * 10000 + 10000;
                        x.totalGold = x.cityCount * 4000;
                        x.totalFood = x.cityCount * 40000;
                    }

                    return x.cityCount.ToString();
                },
                personSortFunc = (a, b) => a.cityCount.CompareTo(b.cityCount),
                valueObjGet = x => x.cityCount,
                valueObjSet = null,
            };
        }

        public static SortTitle SortByTotalGold(ShortScenario scenario)
        {

            return new SortTitle()
            {
                name = "资金",
                width = 5.00f,
                valueGetCall = x =>
                {
                    return x.totalGold.ToString();
                },
                personSortFunc = (a, b) => a.totalGold.CompareTo(b.totalGold),
                valueObjGet = x => x.totalGold,
                valueObjSet = null,
            };
        }

        public static SortTitle SortByTotalFood(ShortScenario scenario)
        {

            return new SortTitle()
            {
                name = "粮食",
                width = 5.00f,
                valueGetCall = x =>
                {
                    return x.totalFood.ToString();
                },
                personSortFunc = (a, b) => a.totalFood.CompareTo(b.totalFood),
                valueObjGet = x => x.totalFood,
                valueObjSet = null,
            };
        }

        public static SortTitle SortByTotalTroops(ShortScenario scenario)
        {

            return new SortTitle()
            {
                name = "士兵",
                width = 5.00f,
                valueGetCall = x =>
                {
                    return x.totalTroops.ToString();
                },
                personSortFunc = (a, b) => a.totalTroops.CompareTo(b.totalTroops),
                valueObjGet = x => x.totalTroops,
                valueObjSet = null,
            };
        }
    }
}
