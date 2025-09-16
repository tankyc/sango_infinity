using Newtonsoft.Json;
using Sango.Mod;
using System.Xml;

namespace Sango.Game
{
    public class GameData : Singletion<GameData>
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
            LoadCommonData();
            LoadModelConfig();
        }

        public ScenarioCommonData LoadCommonData()
        {
            ScenarioCommonData = new ScenarioCommonData();
            string commonDataFileName = "Data/Common/Common.json";
            ModManager.Instance.LoadFile(commonDataFileName, file =>
            {
                ScenarioCommonData = Newtonsoft.Json.JsonConvert.DeserializeObject<ScenarioCommonData>(File.ReadAllText(file));
            });
            //SimpleJSON.JSONClass node = new SimpleJSON.JSONClass();
            //ScenarioCommonData.Save(node);
            //node.SaveToFile("D:/commonData.json");
            return ScenarioCommonData;
        }

        public SangoObjectMap<ModelConfig> LoadModelConfig()
        {
            ModelConfigs = new SangoObjectMap<ModelConfig>();
            string commonDataFileName = "Data/Model/ModelConfig.json";
            ModManager.Instance.LoadFile(commonDataFileName, file =>
            {
                //XmlDocument xmlDocument = new XmlDocument();
                //xmlDocument.Load(file);
                //ModelConfigs.Load(xmlDocument.LastChild);
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new SangoObjectMaptConverter<ModelConfig>());
                ModelConfigs = Newtonsoft.Json.JsonConvert.DeserializeObject<SangoObjectMap<ModelConfig>>(File.ReadAllText(file), jsonSerializerSettings);

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
