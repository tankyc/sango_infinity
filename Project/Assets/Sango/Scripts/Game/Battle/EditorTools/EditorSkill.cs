using Sango.Game.Battle.Skill;

namespace Sango.Game.Battle.EditorTools
{
    internal class EditorSkill
    {
        public int id
        {
            internal set
            {
                battleSkillData.id = value;
            }
            get
            {
                return battleSkillData.id;
            }
        }
        public string name;
        public string desc;
        public BattleSkillData battleSkillData;
    }
}
