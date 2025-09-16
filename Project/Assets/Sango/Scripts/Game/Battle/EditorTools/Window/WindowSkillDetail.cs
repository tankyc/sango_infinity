using Sango.Game.Battle.Core;
using Sango.Game.Battle.Skill;
using Sango.Tools;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.EditorTools
{
    public class WindowSkillDetail
    {
        Vector2 scrollPos = Vector2.zero;
        EditorWindow window;
        UnityEngine.Rect window_rect;
        UnityEngine.Rect listBoxRect = new UnityEngine.Rect();
        EditorSkill currentSkillData;
        public WindowSkillDetail()
        {
            UnityEngine.Rect window_rect = new UnityEngine.Rect(201, 0, 340, Screen.height);
            window = EditorWindow.AddWindow(9002, window_rect, OnGUI, "");
            window.dragable = false;
            window.canClose = false;
            window.minmaxable = false;
        }

        void OnGUI(int winId, EditorWindow window)
        {
            if (currentSkillData == null)
                return;

            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(380), GUILayout.Height(680));
            GUILayout.BeginVertical();
            currentSkillData.battleSkillData.id = EditorUtility.IntField(currentSkillData.battleSkillData.id, "技能ID", 100);
            currentSkillData.name = EditorUtility.TextField(currentSkillData.name, "技能名字", 100);
            currentSkillData.battleSkillData.type = (byte)EditorUtility.Popup("技能类型", currentSkillData.battleSkillData.type, BattleDefine.skill_type_name, 100);
            if (currentSkillData.battleSkillData.type == (byte)BattleDefine.SkillType.Active || currentSkillData.battleSkillData.type == (byte)BattleDefine.SkillType.Assault)
            {
                GUILayout.Space(20);
                currentSkillData.battleSkillData.prob = (ushort)EditorUtility.IntField(currentSkillData.battleSkillData.prob, "发动率", 100);
            }
            else
            {
                currentSkillData.battleSkillData.prob = 10000;
            }
            currentSkillData.battleSkillData.command = (byte)EditorUtility.Popup("推荐类型", currentSkillData.battleSkillData.command, BattleDefine.skill_command_name, 100);
            GUILayout.Label("技能描述");
            currentSkillData.desc = EditorUtility.TextField(currentSkillData.desc, "", 340, 100);
            GUILayout.Label("●技能效果组:", GUILayout.MinWidth(100));

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.BeginVertical();
            var count = currentSkillData.battleSkillData.entities.Length;
            for (int i = 1; i < count; ++i)
            {
                var skill_effect = currentSkillData.battleSkillData.entities[i];
                // ShowSkillEffect(skill_effect);
                GUILayout.Space(10);
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("添加-技能效果组", GUILayout.MinWidth(30)))
            {
                //List<SkillEffectEntityData> entity = new List<SkillEffectEntityData>(currentSkillData.battleSkillData.entities);
                //entity.Add(new SkillEffectEntityData()
                //{
                //    targetType = 1,
                //    targetNum = 1,
                //    skillEffects = { }
                //});
                //currentSkillData.battleSkillData.entities = entity.ToArray();
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

        }
    }
}