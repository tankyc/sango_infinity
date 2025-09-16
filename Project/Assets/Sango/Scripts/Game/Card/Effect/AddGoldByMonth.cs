using System.Collections;

namespace Sango.Game.Card
{
    public class AddGoldByMonth : CardEffect
    {
        public int value;
        public int month_flag;

        public override void OnGet(Scenario scenario)
        {
            ScenarioEvent.Event.OnMonthUpdate += OnMonthUpdate;
        }

        private void OnMonthUpdate(Scenario scenario)
        {
            int m = 1 >> scenario.Info.month;
            if ((month_flag & m) == m)
            {
                //scenario.Player.gold += value;
            }
        }

        public override void OnLost(Scenario scenario)
        {
            ScenarioEvent.Event.OnMonthUpdate -= OnMonthUpdate;

        }
    }
}
