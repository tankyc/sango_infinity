using TKNewtonsoft.Json;
using Sango.Mod;
using System.Xml;

namespace Sango.Core
{
    public class GameCustomEdit : Singleton<GameCustomEdit>
    {
        /// <summary>
        /// 剧本附加数据
        /// </summary>
        public ScenarioAddon ScenarioAddon { get; private set; }

        public PersonLib TargetEditPerson;

        public void Init()
        {
            LoadScenarioAddon();
            ScenarioAddon.Init();
        }

        public void LoadScenarioAddon(ScenarioAddon scenarioAddon)
        {
            scenarioAddon.Load(Path.SaveRootPath + "/CustomEdit/CustomPerson.json");
            ModManager.Instance.EnumFiles("Data/CustomEdit/CustomPerson.json", file =>
            {
                scenarioAddon.Load(file);
            });

            scenarioAddon.Load(Path.ContentRootPath + "/Data/FaceConfig.json");
            ModManager.Instance.EnumFiles("Data/FaceConfig.json", file =>
            {
                scenarioAddon.Load(file);
            });
        }

        public ScenarioAddon LoadNewScenarioAddon()
        {
            ScenarioAddon scenarioAddon = new ScenarioAddon();
            LoadScenarioAddon(scenarioAddon);
            return scenarioAddon;
        }

        public ScenarioAddon LoadScenarioAddon()
        {
            if (ScenarioAddon != null)
                return ScenarioAddon;

            ScenarioAddon = LoadNewScenarioAddon();
            return ScenarioAddon;
        }
    }
}
