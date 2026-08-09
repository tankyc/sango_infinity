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
            Sango.Log.Error(ScenarioAddon.PersonAddonMap.Count);
        }

        public void LoadScenarioAddon(ScenarioAddon scenarioCommonData)
        {
            scenarioCommonData.Load(Path.SaveRootPath + "/CustomEdit/CustomPerson.json");
            ModManager.Instance.EnumFiles("Data/CustomEdit/CustomPerson.json", file =>
            {
                scenarioCommonData.Load(file);
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
