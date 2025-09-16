namespace Sango.Game.Card
{
    public class CardEffectSet : CardEffect
    {
        public CardEffect[] cardEffects;

        public override void OnGet(Scenario scenario)
        {
            if (cardEffects != null)
            {
                for (int i = 0; i < cardEffects.Length; i++)
                {
                    cardEffects[i].OnGet(scenario);
                }
            }
        }
        public override void OnLost(Scenario scenario)
        {
            if (cardEffects != null)
            {
                for (int i = 0; i < cardEffects.Length; i++)
                {
                    cardEffects[i].OnLost(scenario);
                }
            }
        }
    }
}
