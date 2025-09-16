/*
'*******************************************************************
Tank 
'*******************************************************************
*/
using UnityEditor;
using Sango;

[CanEditMultipleObjects]
[CustomEditor(typeof(AssetReleaser))]
public class AssetReleaserEditor : Editor
{
    public override void OnInspectorGUI()
    {
        AssetReleaser id = (AssetReleaser)target;
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField("ABIndex:", id.abIndex.ToString());
        EditorGUI.EndDisabledGroup();
    }
}