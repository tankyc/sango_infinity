namespace Sango.Game.Card
{
    public class AddTroops : CardEffect
    {
        public int value;

        public override void OnGet(Scenario scenario)
        {
            //scenario.Player.troops += value;
        }
        public override void OnLost(Scenario scenario)
        {
            
        }
    }
}
