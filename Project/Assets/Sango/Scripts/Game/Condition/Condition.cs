namespace Sango.Game.Condition
{
    public class Condition : DataFactory
    {
        public virtual ConditionType ConditionType { get; }

        public virtual bool Check(ConditionParams sanObj)
        {
            return false;
        }

        public virtual Condition Clone()
        {
            return null;
        }
        public virtual void Active(Trigger trigger)
        {
        }
        public virtual void Clear()
        {

        }
    }
}
