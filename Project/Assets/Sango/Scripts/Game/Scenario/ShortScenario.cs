using TKNewtonsoft.Json;
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
        [JsonProperty] public string desc;
        public bool IsPlayer;
        public bool IsAppend;
        public int CapitalCity;
        public int CapitalCorps;

        public ShortForce Copy()
        {
            return new ShortForce()
            {
                Id = Id,
                Name = Name,
                Governor = Governor,
                Counsellor = Counsellor,
                Flag = Flag,
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
        [JsonProperty] public int BelongForce;
        [JsonProperty] public int BelongCity;
        [JsonProperty] public int headIconID;
        [JsonProperty] public string imageID;
        public PersonLib PersonLib;
        public int state;
        public static ShortPerson FormLib(PersonLib personLib, ShortScenario scenario)
        {
            ShortPerson shortPerson = new ShortPerson();
            shortPerson.Name = personLib.Name;
            shortPerson.PersonLib = personLib;
            shortPerson.BelongForce = personLib.BelongForce(scenario);
            shortPerson.BelongCity = personLib.BelongCity(scenario);
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
                BelongForce = BelongForce,
                BelongCity = BelongCity,
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
        public int BelongForce;
        public int BelongCorps;
        public int BuildingType;
        public int x;
        public int y;
        public int troops;
        public int gold;
        public int food;
        public ShortCity Copy()
        {
            return new ShortCity()
            {
                Id = Id,
                Name = Name,
                BelongForce = BelongForce,
                BelongCorps = BelongCorps,
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
                reader.ReadString();
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

        public string FilePath { internal set; get; }
        Task task;
        public bool loadOK = false;
        public bool loadFullPersons = false;

        ShortScenario()
        {

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

        public void LoadContent()
        {
            LoadContent(FilePath);
        }

        public void LoadContent(string path)
        {
            if (loadOK) return;
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
                        if (shortPerson.BelongCity > 0)
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
}
