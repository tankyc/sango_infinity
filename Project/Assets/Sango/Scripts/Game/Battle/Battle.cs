using Sango.Game.Battle.Buff;
using Sango.Game.Battle.Core;
using Sango.Game.Battle.Skill;

namespace Sango.Game.Battle
{

    public class Battle : Singletion<Battle>
    {
        public BattleInstance CreateBattle(BattleTroops a, BattleTroops b)
        {
            return new BattleInstance(a, b);
        }

        public BattleInstance CreateBattle(BattleTroops a, BattleTroops b, int seed)
        {
            return new BattleInstance(a, b, seed);
        }

        public bool GetSkillData(int id, int level, out BattleSkillData data)
        {
            data = new BattleSkillData();
            return true;
        }

        public BattleSkill CreateSkill(int id, int level, BattlePerson person)
        {
            BattleSkillData battleSkillData;
            if (GetSkillData(id, level, out battleSkillData))
                return new BattleSkill(person, battleSkillData, level);
            return null;
        }


        public BattleFormation.FormationData GetFormationData(int id)
        {
            return new BattleFormation.FormationData();
        }

        public bool GetBuffData(int id, int level, out BattleBuffData data)
        {
            data = new BattleBuffData();
            return true;
        }

        public BattleBuff CreateBuff(int id, int level, BattleObject owner, BattlePerson target, int life, int layer)
        {
            BattleBuffData battleBuffData;
            if (GetBuffData(id, level, out battleBuffData))
                return new BattleBuff(owner, battleBuffData, target, life, layer);
            return null;
        }

        public BattleBuff CreateBuff(int id, int level, BattleInstance battle, BattlePerson target, int life, int layer)
        {
            BattleBuffData battleBuffData;
            if (GetBuffData(id, level, out battleBuffData))
                return new BattleBuff(battle, battleBuffData, target, life, layer);
            return null;
        }
    }
}
