/*
'*******************************************************************
Tank 
'*******************************************************************
*/
using UnityEditor;
using Sango;

/// <summary>
/// 原始资源打包,推荐使用扩展ABBuilder进行自定义手动打包
/// </summary>
public class AssetsBuilder
{
    [MenuItem("Sango/美术工具/资源导出", false, 0)]
    public static void BuildAssetBundls()
    {
        string outDir = SangoSetting.projectDataDir + "/Assets/" + SangoSetting.GetBuildTargetName();
        if (!Directory.Exists(outDir))
            Directory.Create(outDir, true);
        if (EditorUtility.DisplayDialog("自动生成", "确认路径:" + outDir + "， 点击确定开始导出!", "确定", "取消"))
        {
            BuildPipeline.BuildAssetBundles(outDir, BuildAssetBundleOptions.DeterministicAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DisableWriteTypeTree, EditorUserBuildSettings.activeBuildTarget);
        }
    }
}