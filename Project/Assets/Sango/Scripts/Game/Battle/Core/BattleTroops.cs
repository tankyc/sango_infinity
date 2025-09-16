namespace Sango.Game.Battle.Core
{
    public class BattleTroops
    {
        public Person[] persons { internal set; get; }
        public int formationId { internal set; get; }
        
        public Person[] GetPersons()
        {
            return persons;
        }
    }
}
