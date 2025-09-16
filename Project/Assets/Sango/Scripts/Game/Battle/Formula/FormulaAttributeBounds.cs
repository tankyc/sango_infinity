using Sango.Game.Battle.Formula;

namespace Sango.Game.Battle.Core.AttributeObj
{
    public class FormulaAttributeBounds : BattleFormula<int>
    {
        public int value;
        public override int Calculate(BattlePerson person)
        {
            return value;
        }
    }
}
