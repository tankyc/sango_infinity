using TKNewtonsoft.Json;
using Sango.Hexagon;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Task = System.Threading.Tasks.Task;
using System.Threading;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class ShortForce
    {
        [JsonProperty(Order = -99)] public int Id;
        [JsonProperty(Order = -98)] public string Name;
        [JsonProperty] public int Governor;
        [JsonProperty] public int Counsellor;
        [JsonProperty] public int Flag;
        [JsonProperty] public string desc;
        public bool IsPlayer;
    }

    [JsonObject(MemberSerialization.OptOut)]
    public class ShortPerson
    {
        [JsonProperty(Order = -99)] public int Id;
        [JsonProperty(Order = -98)] public string Name;
        public int BelongForce;
        public int BelongCity;
        public int headIconID;
        public string imageID;
    }


    [JsonObject(MemberSerialization.OptOut)]
    public class ShortCity
    {
        [JsonProperty(Order = -99)] public int Id;
        [JsonProperty(Order = -98)] public string Name;
        public int BelongForce;
        public int BuildingType;
        public int x;
        public int y;
        public int troops;
        public int gold;
        public int food;
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
        [JsonProperty] public Dictionary<int, ShortForce> forceSet = new Dictionary<int, ShortForce>();
        [JsonProperty] public Dictionary<int, ShortPerson> personSet = new Dictionary<int, ShortPerson>();
        [JsonProperty] public Dictionary<int, ShortCity> citySet = new Dictionary<int, ShortCity>();
        #endregion Data
        public static ShortScenario Cur { get; private set; }
        public static List<ShortScenario> all_scenario_info_list = new List<ShortScenario>();
        public static ShortScenario CurSelected { get; set; }
        public string ModName { internal set; get; }

        public string FilePath { internal set; get; }
        Task task;
        public bool loadOK = false;
        public bool loadFullPersons = false;

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
                                forceSet = serializer.Deserialize<Dictionary<int, ShortForce>>(reader);

                            // 收集所有势力的主公（Governor）与军师（Counsellor）ID
                            if (forceSet != null)
                            {
                                mainPersonIds = new HashSet<int>();
                                foreach (var force in forceSet.Values)
                                {
                                    if (force.Governor != 0) mainPersonIds.Add(force.Governor);
                                    if (force.Counsellor != 0) mainPersonIds.Add(force.Counsellor);
                                }
                            }
                            break;
                        case "personSet":
                            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                            {
                                if (mainPersonIds != null)
                                {
                                    // 仅加载主公/军师，其余 entry 走 reader.Skip() 零解析
                                    ShortPersonSetConverter filter = new ShortPersonSetConverter(mainPersonIds);
                                    personSet = (Dictionary<int, ShortPerson>)filter.ReadJson(
                                        reader, typeof(Dictionary<int, ShortPerson>), null, serializer);
                                }
                                else
                                {
                                    // 没有 forceSet 时退回全量加载
                                    personSet = serializer.Deserialize<Dictionary<int, ShortPerson>>(reader);
                                }
                            }
                            // personSet 是最后一个真正需要的目标，加载完成即可跳出
                            personSetLoaded = true;
                            break;
                        case "citySet":
                            if (reader.Read() && reader.TokenType == JsonToken.StartObject)
                                citySet = serializer.Deserialize<Dictionary<int, ShortCity>>(reader);
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
                foreach (var x in forceSet.Values)
                {
                    for (int k = 0; k < Info.playerForceList.Length; k++)
                    {
                        if (Info.playerForceList[k] == x.Id)
                        {
                            x.IsPlayer = true;
                            break;
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
                                personSet = serializer.Deserialize<Dictionary<int, ShortPerson>>(reader);
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
    }
}
