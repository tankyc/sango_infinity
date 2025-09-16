using Sango.Game.Battle.Core;
using System.Collections;

namespace Sango.Game.Battle.Skill
{
    public class BattleSkillEntity
    {
        public ushort targetType;
        public byte targetNum;
        public BattleSkillEffect[] skillEffects;

        public IEnumerator Run(BattleSkill skill)
        {
            BattlePerson[] targets = skill.battle.GetTargets(targetType, skill.owner as BattlePerson, targetNum);
            if (targets == null)
                yield break;

            for (int j = 0; j < skillEffects.Length; j++)
                yield return skillEffects[j].Run(skill, targets);
        }
    }
}
