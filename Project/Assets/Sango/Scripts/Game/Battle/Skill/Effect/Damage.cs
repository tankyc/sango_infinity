using Sango.Game.Battle.Core;
using Sango.Game.Battle.Core.AttributeObj;
using System.Collections;

namespace Sango.Game.Battle.Skill.Effect
{
    public class Damage : BattleSkillEffect
    {
        public byte hurtType;
        public FormulaAttributeBounds damage;

        public override IEnumerator Run(BattleObject owner, BattlePerson[] targets)
        {
            BattlePerson srcPerson = owner.owner as BattlePerson;
            if(srcPerson != null )
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    BattlePerson target = targets[i];
                    BattleLogic.PersonAttack(srcPerson, target, hurtType, damage.Calculate(srcPerson), owner);
                }
            }
            
            yield return null;
        }
    }
}
