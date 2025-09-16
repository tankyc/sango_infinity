namespace Sango.Game.Card
{
    public class Condition
    {
        public ushort conditionType;

        public virtual bool Check(Scenario scenario, Player player, params object[] data)
        {
            return false;
        }
    }
}
