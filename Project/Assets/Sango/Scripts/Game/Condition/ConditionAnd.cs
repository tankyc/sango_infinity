namespace Sango.Game.Condition
{
    public class ConditionAnd : Condition
    {
        public Condition l;
        public Condition r;

        public override ConditionType ConditionType { get { return ConditionType.And; } }

        public override bool Check(ConditionParams sanObj)
        {
            if (l != null && !l.Check(sanObj))
            {
                return false;
            }
            if (r != null && !r.Check(sanObj))
            {
                return false;
            }
            return true;
        }
        public override Condition Clone()
        {
            return new ConditionAnd()
            {
                l = l?.Clone(),
                r = r?.Clone(),
            };
        }

    }
}