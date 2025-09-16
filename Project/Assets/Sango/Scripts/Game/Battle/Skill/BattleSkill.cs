using Sango.Game.Battle.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Skill
{
    public class BattleSkill : BattleObject
    {
        public byte skillType { get { return skillData.type; } }
        public int skillId { get { return skillData.id; } }
        public int level { internal set; get; }
        public override string name { get { return BattleTranslation.Translation(skillData.nameId, this); } }
        public string desc { get { return BattleTranslation.Translation(skillData.descId, this); } }
        public BattleSkillData skillData { internal set; get; }


        public BattleSkill(BattlePerson person, BattleSkillData skillData, int level) : base(person.battle, person)
        {
            type = BattleDefine.ObjectType.Skill;
            root = this;
            this.level = level;
            this.skillData = skillData;
        }

        public bool CheckProb(int addProb = 0)
        {
            return battle.RandomCheck(addProb + skillData.prob);
        }

        public bool CheckProb()
        {
            return battle.RandomCheck(skillData.prob);
        }

        public IEnumerator Active()
        {
            for (int i = 0; i < skillData.entities.Length; i++)
                yield return skillData.entities[i].Run(this);
        }

    }
}
