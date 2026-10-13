using Newtonsoft.Json;
using Sango.Mod;
using System.Xml;

namespace Sango.Core
{
    public class GameData : Singleton<GameData>
    {
        /// <summary>
        /// 通用配置
        /// </summary>
        public ScenarioCommonData ScenarioCommonData { get; private set; }

        /// <summary>
        /// 模型配置
        /// </summary>
        public SangoObjectMap<ModelConfig> ModelConfigs { get; private set; }

        public void Init()
        {
            Sango.Log.Info("GameData.Init()");
            LoadCommonData();
            LoadModelConfig();
            GameMedia.Instance.Load();
            SkillConfigManager.Instance.Init();
            GameCustomEdit.Instance.Init();
            // 玩家自建出征队伍（跨存档保留，与自建武将同目录）
            CustomTroopTeams.Init();
        }


        /// <summary>
        /// 把 Data/Common 下全部 JSON（Content + CustomEdit + Mod）解析进指定的通用数据对象。
        /// 【注意】本方法不是 LoadNewCommonData 的重复实现，不能删除：
        /// Scenario.CommonData 带 [JsonProperty]，剧本 JSON 里内嵌了一份（往往残缺/陈旧的）CommonData 段，
        /// 加载剧本时 Populate 会用这份内嵌数据覆盖掉从文件解析出的完整数据，
        /// 必须靠再调用一次本方法把文件数据恢复回来，否则会出现通用数据丢失
        /// （技能/特性/兵种/州郡等集合被清空）。见 Scenario.LoadScenarioDataStage。
        /// </summary>
        public void LoadCommonData(ScenarioCommonData scenarioCommonData)
        {
            Sango.Directory.EnumFiles(Path.ContentRootPath + "/Data/Common", file =>
            {
                scenarioCommonData.Load(file);
            });

            Sango.Directory.EnumFiles(Path.CustomEditRootPath + "/Data/Common", file =>
            {
                scenarioCommonData.Load(file);
            });

            ModManager.Instance.EnumFiles("Data/Common", "*.json", System.IO.SearchOption.AllDirectories, file =>
            {
                scenarioCommonData.Load(file);
            });
        }

        public ScenarioCommonData LoadNewCommonData()
        {
            ScenarioCommonData scenarioCommonData = new ScenarioCommonData();
            Sango.Directory.EnumFiles(Path.ContentRootPath + "/Data/Common", file =>
            {
                scenarioCommonData.Load(file);
            });
            Sango.Directory.EnumFiles(Path.CustomEditRootPath + "/Data/Common", file =>
            {
                scenarioCommonData.Load(file);
            });
            ModManager.Instance.EnumFiles("Data/Common", "*.json", System.IO.SearchOption.AllDirectories, file =>
            {
                scenarioCommonData.Load(file);
            });
            return scenarioCommonData;
        }

        internal ScenarioCommonData LoadCommonData()
        {
            if (ScenarioCommonData != null)
                return ScenarioCommonData;

            ScenarioCommonData = new ScenarioCommonData();
            Sango.Directory.EnumFiles(Path.ContentRootPath + "/Data/Common", file =>
            {
                ScenarioCommonData.Load(file);
            });
            ModManager.Instance.EnumFiles("Data/Common", "*.json", System.IO.SearchOption.AllDirectories, file =>
            {
                ScenarioCommonData.Load(file);
            });
            return ScenarioCommonData;
        }

        public SangoObjectMap<ModelConfig> LoadModelConfig()
        {
            if (ModelConfigs != null)
                return ModelConfigs;

            ModelConfigs = new SangoObjectMap<ModelConfig>();
            string commonDataFileName = "Data/Model/ModelConfig.json";
            ModManager.Instance.LoadFile(commonDataFileName, (System.Action<string>)(file =>
            {
                //XmlDocument xmlDocument = new XmlDocument();
                //xmlDocument.Load(file);
                //ModelConfigs.Load(xmlDocument.LastChild);
                Newtonsoft.Json.JsonSerializerSettings jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add((Newtonsoft.Json.JsonConverter)new SangoObjectMapConverter<ModelConfig>());
                ModelConfigs = Newtonsoft.Json.JsonConvert.DeserializeObject<SangoObjectMap<ModelConfig>>(File.ReadAllText(file), (Newtonsoft.Json.JsonSerializerSettings)jsonSerializerSettings);

            }));

            //SimpleJSON.JSONArray node = new SimpleJSON.JSONArray();
            //ModelConfigs.Save(node);
            //node.SaveToFile("D:/modelConfig.json");
            //File.WriteAllText("D:/modelConfig1.json", node.ToJson());

            //SimpleJSON.JSONNode loaded = SimpleJSON.JSON.Parse(File.ReadAllText("D:/modelConfig1.json"));
            //File.WriteAllText("D:/modelConfig2.json", loaded.ToJson());

            return ModelConfigs;
        }
    }
}
