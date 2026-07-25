using Sango.UI;

namespace Sango.Core.Player
{
    [GameSystem]
    public class GameScenarioVariables : GameSettingMenuBase
    {
        public GameScenarioVariables()
        {
            customMenuName = "剧本参数";
            customMenuOrder = 3;
        }

        public override void OnEnter()
        {
            Window.Instance.Open("window_scenario_variables_ingame", Scenario.Cur, (System.Action)OnSure, (System.Action)OnCancel );
        }

        void OnSure()
        {
            Done();
        }

        void OnCancel()
        {
            Done();
        }

        public override void OnDestroy()
        {
            Window.Instance.Close("window_scenario_variables_ingame");
        }
    }
}
