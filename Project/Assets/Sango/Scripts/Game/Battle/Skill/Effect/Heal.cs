using Sango.Game.Battle.Core.AttributeObj;
using Sango.Game.Battle.Core;
using System.Collections;

namespace Sango.Game.Battle.Skill.Effect
{
    public class Heal : BattleSkillEffect
    {
        public FormulaAttributeBounds heal;

        public override IEnumerator Run(BattleObject owner, BattlePerson[] targets)
        {
            BattlePerson srcPerson = owner.owner as BattlePerson;
            if (srcPerson != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    BattlePerson target = targets[i];
                    BattleLogic.PersonHeal(srcPerson, target, heal.Calculate(srcPerson), owner);
                }
            }

            yield return null;
        }
    }
}
