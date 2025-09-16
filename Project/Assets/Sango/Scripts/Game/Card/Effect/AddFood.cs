namespace Sango.Game.Card
{
    public class AddFood : CardEffect
    {
        public int value;

        public override void OnGet(Scenario scenario)
        {
            //scenario.Player.food += value;
        }
        public override void OnLost(Scenario scenario)
        {
            
        }
    }
}
