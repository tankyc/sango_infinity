using Sango.Game.Battle.Buff;
using Sango.Game.Battle.Core;
using System.Collections;

namespace Sango.Game.Battle.Skill.Effect
{
    public class AddBuff : BattleSkillEffect
    {
        public int buffId;
        public int level;
        public int life;
        public int layer;

        public override IEnumerator Run(BattleObject owner, BattlePerson[] targets)
        {
            for(int i = 0; i < targets.Length; i++)
            {
                BattleBuff buff = Battle.Instance.CreateBuff(buffId, level, owner, targets[i], life, layer);
                if(buff != null )
                {
                    targets[i].AddBuff(buff);
                }
            }
            yield return null;
        }
    }
}
