using System.Collections;

namespace Sango.Game.Card
{
    public class AddTroopsByMonth : CardEffect
    {
        public int value;
        public int month_flag;

        public override void OnGet(Scenario scenario)
        {
            //Event.OnMonthUpdate += OnMonthUpdate;
        }

        private void OnMonthUpdate(Scenario scenario)
        {
            int m = 1 >> scenario.Info.month;
            if ((month_flag & m) == m)
            {
               // scenario.Player.troops += value;
            }
        }

        public override void OnLost(Scenario scenario)
        {
            //Event.OnMonthUpdate -= OnMonthUpdate;

        }
    }
}
