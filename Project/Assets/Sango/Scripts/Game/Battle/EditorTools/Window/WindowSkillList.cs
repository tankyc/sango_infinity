using Sango.Tools;
using UnityEngine;
namespace Sango.Game.Battle.EditorTools
{
    public class WindowSkillList
    {
        Vector2 scrollPos = Vector2.zero;
        EditorWindow window;
        UnityEngine.Rect window_rect;
        UnityEngine.Rect listBoxRect = new UnityEngine.Rect();
        EditorSkill currentSkillData;
        public WindowSkillList()
        {
            UnityEngine.Rect window_rect = new UnityEngine.Rect(0, 0, 200, Screen.height - 20);
            window = EditorWindow.AddWindow(9001, window_rect, OnGUI, "");
            window.dragable = false;
            window.canClose = false;
            window.minmaxable = false;
        }

        void OnGUI(int winId, EditorWindow window)
        {
            int count = BattleSkillEditor.Instance.skill_list.Count;
            var screenHeight = Screen.height;

            GUILayout.Box("", GUILayout.Width(180), GUILayout.Height(screenHeight - 70));
            if (UnityEngine.Event.current != null && UnityEngine.Event.current.type == UnityEngine.EventType.Repaint)
            {
                if (listBoxRect != GUILayoutUtility.GetLastRect())
                {
                    listBoxRect = GUILayoutUtility.GetLastRect();
                }
            }
            GUILayout.BeginArea(listBoxRect);

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(180), GUILayout.Height(screenHeight - 70));
            GUILayout.BeginVertical();
            for (int i = 1; i < count; i++)
            {


                var skill = BattleSkillEditor.Instance.skill_list[i];
                if (skill == currentSkillData)
                    GUI.color = Color.cyan;
                else
                    GUI.color = Color.white;

                if (GUILayout.Button($"[{skill.id}]"))
                {
                    currentSkillData = skill;
                    BattleSkillEditor.Instance.ShowSkill(currentSkillData);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (GUILayout.Button("创建新技能"))
            {
                var skill = BattleSkillEditor.Instance.CreateEditorSkill();
                if (skill != null)
                {
                    currentSkillData = skill;
                    BattleSkillEditor.Instance.ShowSkill(currentSkillData);
                }
            }
            if (GUILayout.Button("保存技能"))
                BattleSkillEditor.Instance.SaveSkill();

        }
    }


}