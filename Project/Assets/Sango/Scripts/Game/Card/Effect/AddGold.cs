namespace Sango.Game.Card
{
    public class AddGold : CardEffect
    {
        public int value;

        public override void OnGet(Scenario scenario)
        {
            //scenario.Player.gold += value;
        }
        public override void OnLost(Scenario scenario)
        {

        }
    }
}
