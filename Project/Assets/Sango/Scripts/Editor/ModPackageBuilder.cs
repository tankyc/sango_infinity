/*
'*******************************************************************
Tank 
'*******************************************************************
*/
using UnityEditor;
using Sango;
using UnityEngine;
using Sango.Mod;
using System.Collections.Generic;

/// <summary>
/// 原始资源打包,推荐使用扩展ABBuilder进行自定义手动打包
/// </summary>
public class ModPackageBuilder
{
    [MenuItem("Sango/模组/资源包导出", false, 0)]
    public static void BuildAssetBundls()
    {
        Path.Init();
        string packageFolder = "Assets/Packages";
        string modRootFolder = Path.ModRootPath;
        string[] findFolders = AssetDatabase.FindAssets("*.pkg", new string[] { packageFolder });
        for (int i = 0; i < findFolders.Length; i++)
        {
            List<AssetBundleBuild> assetBundle = new List<AssetBundleBuild>();

            string folder = AssetDatabase.GUIDToAssetPath(findFolders[i]);
            string substr_folder = folder.Substring(packageFolder.Length + 1);
            string[] mod_and_pkg = substr_folder.Split('+');
            string pkgName = mod_and_pkg[0];
            string modName = mod_and_pkg.Length > 1 ? mod_and_pkg[1] : "Content";

            string[] findObject = AssetDatabase.FindAssets("t:Object", new string[] { folder });
            List<string> assetList = new List<string>();
            List<string> assetRenameList = new List<string>();
            foreach (string asset in findObject)
            {
                string tmpPath = AssetDatabase.GUIDToAssetPath(asset);
                if (!AssetDatabase.IsValidFolder(tmpPath))
                {
                    Debug.Log(tmpPath);
                    assetList.Add(tmpPath);
                    assetRenameList.Add(tmpPath.Substring(folder.Length + 1));
                }
            }

            if (assetList.Count == 0) continue;

            AssetBundleBuild assetBundleBuild = new AssetBundleBuild();
            assetBundleBuild.assetNames = assetList.ToArray();
            assetBundleBuild.addressableNames = assetRenameList.ToArray();
            assetBundleBuild.assetBundleName = $"{modName}_{pkgName}";
            assetBundle.Add(assetBundleBuild);

            string saveDir = $"{Application.dataPath.Substring(0, Application.dataPath.Length - 7)}/PackageTemp/{modName}";
            if (!Directory.Exists(saveDir))
                Directory.Create(saveDir);

            BuildPipeline.BuildAssetBundles(saveDir, assetBundle.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression |
            BuildAssetBundleOptions.DisableWriteTypeTree,
            EditorUserBuildSettings.activeBuildTarget);

            string dstFile = $"{saveDir}/{modName}_{pkgName}";
            string moveDstDir = $"{modRootFolder}/{modName}/Package/{pkgName}";

            if(modName.Equals("Content"))
                moveDstDir = $"{Path.ContentRootPath}/Package/{pkgName}";
            Sango.Directory.Create(moveDstDir, false);
            if (File.Exists(dstFile))
                File.Copy(dstFile, moveDstDir);
        }
    }
}