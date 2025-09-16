//namespace Sango.Game.Card
//{
//    public class ValueCardBase
//    {
//        public int weight { protected set; get; }
//        public int id { protected set; get; }
//        public string name { protected set; get; }
//        public int icon { protected set; get; }
//        public bool IsValid { protected set; get; }
//        public byte cardType { protected set; get; }
//        public bool unlocked { protected set; get; }
//        public byte cardLevel { protected set; get; }
//        public bool only { protected set; get; }

//        public Condition codition;
//        public CardEffect effect;

//        public virtual void OnSelect(Scenario scenario)
//        {

//        }

//        public virtual void OnGet(Scenario scenario)
//        {
//            effect.OnGet(scenario);
//        }
//        public virtual void OnLost(Scenario scenario)
//        {
//            effect.OnLost(scenario);
//        }

//        public virtual void OnLevelUP(Scenario scenario, byte destLevel)
//        {
//            cardLevel = destLevel;
//        }
//    }
//}
