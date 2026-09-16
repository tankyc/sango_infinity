using TKNewtonsoft.Json;
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
        }


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
            ModManager.Instance.LoadFile(commonDataFileName, file =>
            {
                //XmlDocument xmlDocument = new XmlDocument();
                //xmlDocument.Load(file);
                //ModelConfigs.Load(xmlDocument.LastChild);
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new SangoObjectMapConverter<ModelConfig>());
                ModelConfigs = TKNewtonsoft.Json.JsonConvert.DeserializeObject<SangoObjectMap<ModelConfig>>(File.ReadAllText(file), jsonSerializerSettings);

            });

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
