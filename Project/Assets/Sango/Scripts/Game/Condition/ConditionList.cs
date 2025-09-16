using Sango.Game.Battle.Core;

namespace Sango.Game.Condition
{

    public class ConditionList : Condition
    {
        public Condition[] conditions;
        public override ConditionType ConditionType { get { return ConditionType.List; } }

    }
}
