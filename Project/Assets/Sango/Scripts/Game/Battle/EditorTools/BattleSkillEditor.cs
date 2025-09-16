using System.Collections.Generic;
namespace Sango.Game.Battle.EditorTools
{
    public class BattleSkillEditor : Singletion<BattleSkillEditor>
    {
        internal List<EditorSkill> skill_list = new List<EditorSkill>();

        public ulong MakeSkillId()
        {
            return (ulong)System.DateTime.Now.Ticks;
        }

        internal EditorSkill CreateEditorSkill()
        {
            ulong skillId = MakeSkillId();
            return null;
        }

        public void SaveSkill()
        {

        }

        internal void ShowSkill(EditorSkill skill)
        {

        }
    }
}
