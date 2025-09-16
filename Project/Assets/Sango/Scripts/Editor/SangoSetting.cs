/*
'*******************************************************************
Tank 
'*******************************************************************
*/
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SangoSetting
{
    /// <summary>
    /// 工程资源目录路径
    /// </summary>
    public static string projectDataDir = "";

    /// <summary>
    /// 配置保存文件地址
    /// </summary>
    private static string settingSavePath = Sango.Path.settingSavePath;


    static SangoSetting()
    {
        // 处理资源文件夹
        // 恢复配置

        if (File.Exists(settingSavePath))
        {
            projectDataDir = File.ReadAllText(settingSavePath);
        }
        else
        {
            string savedir = EditorUtility.OpenFolderPanel("选择资源文件夹", Application.dataPath, "");

            // 检测资源文件夹的合法性
            if (!Directory.Exists(savedir + "/Assets") && Directory.Exists(savedir + "/Scripts"))
            {
                Debug.LogError("目录并未包含Assets和Scripts文件夹,请核实正确性!!" + projectDataDir);
            }

            projectDataDir = savedir;
            File.WriteAllText(settingSavePath, projectDataDir);
        }

    }

    public static string GetBuildTargetName()
    {
        string platformName = "";
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
        {
            platformName = "android";
        }
        else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows)
        {
            platformName = "win";
        }
        else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSXIntel || EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSXIntel64 )
        {
            platformName = "mac";
        }
        else if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
        {
            platformName = "ios";
        }
        return platformName;
    }


    [MenuItem("Sango/工具/重设项目文件夹", false, 55)]
    public static void ResetDataDir()
    {
        string savedir = EditorUtility.OpenFolderPanel("选择项目外部资源文件夹", Application.dataPath, "");
        projectDataDir = savedir;
        File.WriteAllText(settingSavePath, projectDataDir);
        Debug.LogWarning("重设项目目录: " + projectDataDir);
    }
   
}
