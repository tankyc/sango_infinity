/*
'*******************************************************************
Tank Framework
'*******************************************************************
*/
using UnityEditor;
using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Sango;

/// <summary>
/// 简单打包工具类
/// </summary>
public class SimpleBuilder
{
    private const string PublishRootPathKey = "SangoBuilder_PublishRootPath";

    public static string PublishRootPath
    {
        get { return EditorPrefs.GetString(PublishRootPathKey, "D:/"); }
        set { EditorPrefs.SetString(PublishRootPathKey, value); }
    }

    [MenuItem("Sango/发布游戏", false, 1)]
    public static void BuildGame()
    {
        BuildVersionWindow.ShowWindow();
    }

    public static void ExecuteBuild(bool isDevelopment)
    {
        Sango.Path.Init();

        BuildTarget targetPlatform = EditorUserBuildSettings.activeBuildTarget;

        ModPackageBuilder.BuildAssetBundls();

        if (targetPlatform == BuildTarget.StandaloneWindows || targetPlatform == BuildTarget.StandaloneWindows64)
        {
            BuildWindows(isDevelopment);
        }
        else if (targetPlatform == BuildTarget.Android)
        {
            BuildAndroid(isDevelopment);
        }
        else if (targetPlatform == BuildTarget.iOS)
        {
            BuildIos(isDevelopment);
        }
        else
        {
            Log.Info("当前平台不支持自动打包", Log.LogType.Game);
        }
    }

    private static void BuildWindows(bool isDevelopment)
    {

        string winPackagePath = System.IO.Path.Combine(Sango.Path.ContentRootPath, "Package", "android");
        if (System.IO.Directory.Exists(winPackagePath))
        {
            System.IO.Directory.Delete(winPackagePath, true);
        }

        winPackagePath = System.IO.Path.Combine(Sango.Path.ContentRootPath, "Package", "ios");
        if (System.IO.Directory.Exists(winPackagePath))
        {
            System.IO.Directory.Delete(winPackagePath, true);
        }

        string buildPath = Sango.Path.SaveRootPath;
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        string version = Application.version;
        string buildType = isDevelopment ? "_dev电脑版" : "_release电脑版";
        string productName = Application.productName;
        string exeName = $"{productName}.exe";

        BuildOptions buildOptions = isDevelopment
            ? BuildOptions.Development | BuildOptions.AllowDebugging
            : BuildOptions.None;

        BuildPlayerOptions playerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath + "/" + exeName,
            target = BuildTarget.StandaloneWindows64,
            options = buildOptions
        };

        BuildPipeline.BuildPlayer(playerOptions);

        Log.Info("Windows平台构建完成", Log.LogType.Game);

        string tempPublishPath = System.IO.Path.Combine(PublishRootPath, productName);
        string finalPublishPath = System.IO.Path.Combine(PublishRootPath, $"{productName}_v{version}{buildType}");

        if (System.IO.Directory.Exists(tempPublishPath))
        {
            System.IO.Directory.Delete(tempPublishPath, true);
        }

        if (System.IO.Directory.Exists(finalPublishPath))
        {
            System.IO.Directory.Delete(finalPublishPath, true);
        }

        System.IO.Directory.CreateDirectory(tempPublishPath);
        CopyDirectory(buildPath, tempPublishPath);

        Log.Info("拷贝到发布目录完成", Log.LogType.Game);

        DeleteUnnecessaryFiles(tempPublishPath, productName);

        Log.Info("删除不必要文件完成", Log.LogType.Game);

        System.IO.Directory.Move(tempPublishPath, finalPublishPath);

        Log.Info("重命名发布文件夹完成", Log.LogType.Game);

        string zipPath = finalPublishPath + ".zip";
        if (System.IO.File.Exists(zipPath))
        {
            System.IO.File.Delete(zipPath);
        }

        System.IO.Compression.ZipFile.CreateFromDirectory(finalPublishPath, zipPath, System.IO.Compression.CompressionLevel.Optimal, true);

        Log.Info("压缩发布文件夹完成", Log.LogType.Game);

        EditorUtility.DisplayDialog("发布完成", $"Windows版本已发布成功！\n路径: {finalPublishPath}\n压缩包: {zipPath}", "确定");
    }

    private static void BuildAndroid(bool isDevelopment)
    {

        string winPackagePath = System.IO.Path.Combine(Sango.Path.ContentRootPath, "Package", "win");
        if (System.IO.Directory.Exists(winPackagePath))
        {
            System.IO.Directory.Delete(winPackagePath, true);
        }

        winPackagePath = System.IO.Path.Combine(Sango.Path.ContentRootPath, "Package", "ios");
        if (System.IO.Directory.Exists(winPackagePath))
        {
            System.IO.Directory.Delete(winPackagePath, true);
        }

        Platform.ZipContentToStreamingAssets();

        string version = Application.version;
        string buildType = isDevelopment ? "_dev安卓" : "_release安卓";
        string productName = Application.productName;
        string buildPath = System.IO.Path.Combine(PublishRootPath, $"{productName}_v{version}{buildType}.apk");
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if(isDevelopment)
            PlayerSettings.SetAdditionalIl2CppArgs("--linker-flags=\"-Wl,--stub-group-size=115343360\"");
        else
            PlayerSettings.SetAdditionalIl2CppArgs("");

        BuildOptions buildOptions = isDevelopment
            ? BuildOptions.Development | BuildOptions.AllowDebugging
            : BuildOptions.None;

        BuildPlayerOptions playerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.Android,
            options = buildOptions
        };

        UnityEditor.PlayerSettings.Android.bundleVersionCode = UnityEditor.PlayerSettings.Android.bundleVersionCode + 1;
        BuildPipeline.BuildPlayer(playerOptions);

        Log.Info("Android平台构建完成", Log.LogType.Game);

        EditorUtility.DisplayDialog("发布完成", $"Android版本已发布成功！\n路径: {buildPath}", "确定");
    }

    private static void BuildIos(bool isDevelopment)
    {
        string winPackagePath = System.IO.Path.Combine(Sango.Path.ContentRootPath, "Package", "win");
        if (System.IO.Directory.Exists(winPackagePath))
        {
            System.IO.Directory.Delete(winPackagePath, true);
        }

        winPackagePath = System.IO.Path.Combine(Sango.Path.ContentRootPath, "Package", "android");
        if (System.IO.Directory.Exists(winPackagePath))
        {
            System.IO.Directory.Delete(winPackagePath, true);
        }

        Platform.ZipContentToStreamingAssets();

        string version = Application.version;
        string buildType = isDevelopment ? "_dev_ios" : "_release_ios";
        string productName = Application.productName;
        string buildPath = System.IO.Path.Combine(PublishRootPath, $"{productName}_v{version}{buildType}");
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (isDevelopment)
            PlayerSettings.SetAdditionalIl2CppArgs("--linker-flags=\"-Wl,--stub-group-size=115343360\"");
        else
            PlayerSettings.SetAdditionalIl2CppArgs("");

        BuildOptions buildOptions = isDevelopment
            ? BuildOptions.Development | BuildOptions.AllowDebugging
            : BuildOptions.None;

        BuildPlayerOptions playerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.iOS,
            options = buildOptions
        };

        UnityEditor.PlayerSettings.iOS.buildNumber = UnityEditor.PlayerSettings.iOS.buildNumber + 1;
        BuildPipeline.BuildPlayer(playerOptions);

        Log.Info("IOS", Log.LogType.Game);

        EditorUtility.DisplayDialog("发布完成", $"IOS版本已发布成功！\n路径: {buildPath}", "确定");
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        System.IO.DirectoryInfo sourceInfo = new System.IO.DirectoryInfo(sourceDir);
        System.IO.DirectoryInfo destInfo = new System.IO.DirectoryInfo(destDir);

        if (!destInfo.Exists)
        {
            destInfo.Create();
        }

        foreach (System.IO.FileInfo file in sourceInfo.GetFiles())
        {
            string destFilePath = System.IO.Path.Combine(destDir, file.Name);
            file.CopyTo(destFilePath, true);
        }

        foreach (System.IO.DirectoryInfo subDir in sourceInfo.GetDirectories())
        {
            string destSubDir = System.IO.Path.Combine(destDir, subDir.Name);
            CopyDirectory(subDir.FullName, destSubDir);
        }
    }

    private static void DeleteUnnecessaryFiles(string publishPath, string productName)
    {
        string[] directoriesToDelete = {
            System.IO.Path.Combine(publishPath, $"{productName}_BackUpThisFolder_ButDontShipItWithYourGame"),
            System.IO.Path.Combine(publishPath, $"{productName}_BurstDebugInformation_DoNotShip"),
            System.IO.Path.Combine(publishPath, "Mods"),
            System.IO.Path.Combine(publishPath, "Save")
        };

        foreach (string dirPath in directoriesToDelete)
        {
            if (System.IO.Directory.Exists(dirPath))
            {
                System.IO.Directory.Delete(dirPath, true);
            }
        }

        string[] filesToDelete = {
            System.IO.Path.Combine(publishPath, $"{productName}_Data", "StreamingAssets", "Content.zip"),
            System.IO.Path.Combine(publishPath, $"{productName}_Data", "StreamingAssets", "Mods.zip")
        };

        foreach (string filePath in filesToDelete)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}

public class BuildVersionWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private string publishPath;

    public static void ShowWindow()
    {
        BuildVersionWindow window = GetWindow<BuildVersionWindow>(true, "发布游戏", true);
        window.minSize = new Vector2(400, 350);
        window.maxSize = new Vector2(400, 350);
    }

    private void OnEnable()
    {
        publishPath = SimpleBuilder.PublishRootPath;
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space();

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 16;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("游戏发布确认", titleStyle);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        GUIStyle versionLabelStyle = new GUIStyle(GUI.skin.label);
        versionLabelStyle.fontSize = 14;
        versionLabelStyle.fontStyle = FontStyle.Bold;

        EditorGUILayout.LabelField("发布路径设置", versionLabelStyle);

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("Box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("发布根目录:", GUILayout.Width(80));
        EditorGUILayout.LabelField(publishPath, new GUIStyle(GUI.skin.textField) { normal = { textColor = Color.blue } });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("选择文件夹", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择发布根目录", publishPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                publishPath = selectedPath + "/";
                SimpleBuilder.PublishRootPath = publishPath;
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("当前版本信息", versionLabelStyle);

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("Box");

        EditorGUILayout.LabelField($"应用版本号: {Application.version}", new GUIStyle(GUI.skin.label) { fontSize = 13 });
        EditorGUILayout.LabelField($"资源版本号: {Platform.ResourceVersion}", new GUIStyle(GUI.skin.label) { fontSize = 13 });

        BuildTarget targetPlatform = EditorUserBuildSettings.activeBuildTarget;
        string platformName = targetPlatform.ToString();
        EditorGUILayout.LabelField($"目标平台: {platformName}", new GUIStyle(GUI.skin.label) { fontSize = 13 });

        string buildDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        EditorGUILayout.LabelField($"发布时间: {buildDate}", new GUIStyle(GUI.skin.label) { fontSize = 13 });

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("发布选项", versionLabelStyle);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        bool executeDev = false;
        if (GUILayout.Button("发布开发版本", GUILayout.Height(40), GUILayout.MinWidth(150)))
        {
            if (EditorUtility.DisplayDialog("确认发布", $"即将发布开发版本 v{Application.version}\n\n开发版本包含调试信息，仅供内部测试使用。\n\n确定继续？", "确定", "取消"))
            {
                executeDev = true;
            }
        }

        GUILayout.Space(20);
        bool executeRelease = false;
        if (GUILayout.Button("发布正式版本", GUILayout.Height(40), GUILayout.MinWidth(150)))
        {
            if (EditorUtility.DisplayDialog("确认发布", $"即将发布正式版本 v{Application.version}\n\n正式版本不包含调试信息，适用于对外发布。\n\n确定继续？", "确定", "取消"))
            {
                executeRelease = true;

                Close();
                SimpleBuilder.ExecuteBuild(false);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("Box");
        EditorGUILayout.LabelField("版本说明:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        EditorGUILayout.LabelField("• 开发版本(Development): 包含调试信息和Profiler支持");
        EditorGUILayout.LabelField("• 正式版本(Release): 优化后的发布版本，不含调试信息");
        EditorGUILayout.LabelField($"• 包体命名格式: {Application.productName}_v{{版本号}}_{{类型}}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();

        if(executeDev)
        {
            Close();
            SimpleBuilder.ExecuteBuild(true);
        }

        if (executeRelease)
        {
            Close();
            SimpleBuilder.ExecuteBuild(false);
        }
    }
}