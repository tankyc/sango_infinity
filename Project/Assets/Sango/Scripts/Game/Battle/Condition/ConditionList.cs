using Sango.Game.Battle.Core;

namespace Sango.Game.Battle.Condition
{

    public class ConditionList : Condition
    {
        public Condition[] conditions;

        public override void SetOwner(BattleObject o)
        {
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Length; ++i)
                    conditions[i].SetOwner(o);
            }
        }
    }
}
