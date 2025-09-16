namespace Sango.Game.Card
{
    public class AddPeople : CardEffect
    {
        public int value;

        public override void OnGet(Scenario scenario)
        {
            //scenario.Player.population += value;
        }
        public override void OnLost(Scenario scenario)
        {
            
        }
    }
}
