namespace Sango.Game.Condition
{
    public class ConditionOr : Condition
    {
        public Condition l;
        public Condition r;
        public override ConditionType ConditionType { get { return ConditionType.Or; } }

    }
}