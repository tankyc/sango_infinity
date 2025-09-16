namespace Sango.Game.Card
{
    public class LoseCard : CardEffect
    {
        public int cardType;
        public int cardNum;

        public override void OnGet(Scenario scenario)
        {
            //scenario.Player.RandomLoseCard(cardType, cardNum);
        }
        public override void OnLost(Scenario scenario)
        {
            
        }
    }
}
