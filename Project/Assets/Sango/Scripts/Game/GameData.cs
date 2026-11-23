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
            LoadCommonDataInto(scenarioCommonData);
        }

        /// <summary>
        /// 把 Data/Common 下全部 JSON（Content + CustomEdit + 启用模组）解析进指定对象。
        ///
        /// 【三层顺序与覆盖语义】后加载的同 Id 条目覆盖先加载的：
        ///   ① Content    —— 基础内容，永远全部加载
        ///   ② CustomEdit —— 玩家自建覆盖
        ///   ③ 启用模组    —— 按 Mods/modList.txt 的顺序逐个覆盖；未启用的模组一条都不读
        ///
        /// 【为什么必须抽成一个方法】原先三个入口各写一份，口径已经漂移过一次：
        /// <see cref="LoadCommonData()"/>（GameData 单例，剧本编辑器 / 地图编辑器用的那份）
        /// 少了第 ② 层，导致自建目录的 Common 覆盖表在编辑器里读不到、
        /// 同一份数据在游戏内却生效 —— 典型"改了不生效"。共用一个实现后不会再漂移。
        ///
        /// 【模组列表为空时的回落】由 <c>ModManager.GetMergeMods()</c> 负责：
        /// 编辑器路径绕过 InitMods 时 mEnabledModList 是空表，它会回落到 modList.txt，
        /// 避免编辑器里丢失模组覆盖的特技 / 兵种 / 等级表。
        /// </summary>
        /// <param name="scenarioCommonData">目标容器；为 null 时直接返回</param>
        void LoadCommonDataInto(ScenarioCommonData scenarioCommonData)
        {
            if (scenarioCommonData == null)
                return;

            // ① 基础内容
            Sango.Directory.EnumFiles(Path.ContentRootPath + "/Data/Common", file =>
            {
                scenarioCommonData.Load(file);
            });

            // ② 玩家自建（覆盖基础内容）
            Sango.Directory.EnumFiles(Path.CustomEditRootPath + "/Data/Common", file =>
            {
                scenarioCommonData.Load(file);
            });

            // ③ 启用模组（按 modList.txt 顺序覆盖前两层）
            ModManager.Instance.EnumFiles("Data/Common", "*.json", System.IO.SearchOption.AllDirectories, file =>
            {
                scenarioCommonData.Load(file);
            });
        }

        public ScenarioCommonData LoadNewCommonData()
        {
            ScenarioCommonData scenarioCommonData = new ScenarioCommonData();
            LoadCommonDataInto(scenarioCommonData);
            return scenarioCommonData;
        }

        internal ScenarioCommonData LoadCommonData()
        {
            if (ScenarioCommonData != null)
                return ScenarioCommonData;

            ScenarioCommonData = new ScenarioCommonData();
            // 【历史缺陷已修】这里原先少了 CustomEdit 那一层，与另外两个入口口径不一致：
            // 自建目录的 Common 覆盖表在编辑器（剧本 / 地图编辑器）里读不到，游戏内却生效。
            // 现在统一走 LoadCommonDataInto，三层顺序与覆盖语义完全一致。
            LoadCommonDataInto(ScenarioCommonData);
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
