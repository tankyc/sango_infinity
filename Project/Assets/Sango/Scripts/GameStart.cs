using Sango;
using Sango.Core;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 该文件由X框架自动生成
/// 请将此类挂到Gameobject上开始游戏
/// </summary>
public class GameStart : MonoBehaviour
{
    public bool Debug = false;
    public Camera uiCamera;
    public RectTransform uiRoot;
    public CanvasScaler canvasScaler;
    public GameObject initObject;
    public GameObject progressObject;
    public Text zipInfo;
    public Image zipProgress;
    ScreenOrientation last_orientation;
    void Awake()
    {
#if UNITY_ANDROID
        UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI = false;
#endif
        //    StartCoroutine(GitDownloader.DownloadAndExtract(
        //    "https://gitcode.com/gametank/sango_infinity_mod_test/releases/download/version/mod.info",
        //    "https://gitcode.com/gametank/sango_infinity_mod_test/releases/download/1.0/official_rp.zip",
        //    "D:/target/folder",
        //    progress => UnityEngine.Debug.Log($"下载进度: {progress * 100f:F1}%"),
        //    result =>
        //    {
        //        if (result.IsSuccess) UnityEngine.Debug.Log("完成: " + result.ExtractTargetPath);
        //        else UnityEngine.Debug.LogError("失败: " + result.ErrorMessage);
        //    }
        //)); 

        initObject.SetActive(true);
        GameEvent.OnGameInit += OnGameInit;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        Application.targetFrameRate = 0;
#else
        Application.targetFrameRate = 30;
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string arg in args)
        {
            if(arg.Equals("-console"))
            {
                ServerConsole.ShowConsole();
            }
        }
#endif

#if UNITY_ANDROID || UNITY_IPHONE || UNITY_EDITOR
        last_orientation = Screen.orientation;
        canvasScaler.referenceResolution = new Vector2(1366, 768);
        float ratio = ((float)Screen.width / (float)Screen.height);
        if (ratio < 16f / 9.0f)
        {
            canvasScaler.matchWidthOrHeight = 0;
        }
        else
        {
            canvasScaler.matchWidthOrHeight = 1;
        }
#endif
        Screen.sleepTimeout = UnityEngine.SleepTimeout.NeverSleep;
        Sango.Path.Init();
        StartCoroutine(GameInit());
    }

    void OnGameInit()
    {
        StartCoroutine(AlphaOut());
        GameEvent.OnGameInit -= OnGameInit;
    }

    IEnumerator AlphaOut()
    {
        float alpha = 1;
        while (alpha > 0)
        {
            CanvasGroup canvasGroup = initObject.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
            alpha -= Time.deltaTime * 1.2f;
            yield return null;
        }
        initObject.SetActive(false);
    }

    IEnumerator GameInit()
    {

#if (UNITY_ANDROID || UNITY_IPHONE) && !UNITY_EDITOR
        float pogress = 1f;
        bool appVersionNew = Sango.Platform.CheckAppVersion();
        if (!appVersionNew)
        {
            progressObject.SetActive(true);

            if (Directory.Exists(Path.ContentRootPath))
                Directory.Delete(Path.ContentRootPath);

            // 从streamingAssets中拷贝zip到存储盘
            Platform.CopyContentAndModZipFile((f) =>
            {
                pogress = f * 0.4f;
                zipInfo.text = $"第一次启动需要解压资源,请耐心等待解压完成: {(int)(pogress * 100)}%";
                zipProgress.fillAmount = pogress;
            });

            // 开始解压zip
            Task task = Task.Run(() =>
            {
                try
                {
                    Platform.ExtractContentAndModZipFile((f) => { pogress = 0.4f + 0.6f * f; });
                }
                catch (System.Exception e)
                {
                    Sango.Log.Error(e);
                }
            });
        }

        while (pogress < 1f)
        {
            zipInfo.text = $"第一次启动需要解压资源,请耐心等待解压完成: {(int)(pogress * 100)}%";
            zipProgress.fillAmount = pogress;
            yield return null;
        }

        zipInfo.text = $"第一次启动需要解压资源,请耐心等待解压完成: 100%";
        zipProgress.fillAmount = 1;

        Sango.Platform.SaveAppVersion();
#endif

        progressObject.SetActive(false);

        DontDestroyOnLoad(gameObject);
        //Sango.Tools.MapEditor.IsEditOn = true;
        Config.isDebug = Debug;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
#endif
        Game.Instance.UICamera = uiCamera;
        Game.Instance.UIRoot = uiRoot;
        Game.Instance.RootCanvas = uiRoot.GetComponent<Canvas>();
        Game.Instance.CanvasScaler = canvasScaler;
        /// <summary>
        /// 目标平台
        /// </summary>
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        Game.Instance.Init(this, Platform.PlatformName.Mac);
#elif UNITY_STANDALONE_WIN
        Game.Instance.Init(this, Platform.PlatformName.Window);
#elif UNITY_ANDROID
        Game.Instance.Init(this, Platform.PlatformName.Android);
#elif UNITY_IPHONE
        Game.Instance.Init(this, Platform.PlatformName.Ios);
#elif UNITY_WEBGL
        Game.Instance.Init(this, Platform.PlatformName.Webgl);
#endif

        yield return null;
    }

#if UNITY_EDITOR
    void OnEditorPause(UnityEditor.PauseState state)
    {
        if (state == UnityEditor.PauseState.Paused)
            Game.Instance.Pause();
        else
            Game.Instance.Resume();
    }
#endif

    void Update()
    {
        Game.Instance.Update();

        //if (last_orientation != Screen.orientation)
        //{
        //    last_orientation = Screen.orientation;
        //    StartCoroutine(OnChangeOrientation());
        //}
    }

    IEnumerator OnChangeOrientation()
    {
        float ratio = ((float)Screen.width / (float)Screen.height);
        yield return new WaitForSeconds(0.1f);
        ratio = ((float)Screen.width / (float)Screen.height);
        if (ratio < 1.3333f)
        {
            canvasScaler.matchWidthOrHeight = ratio * 768f / 1366f;
        }
        else
        {
            canvasScaler.matchWidthOrHeight = 1;
        }
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    void OnDestroy()
    {
        // 释放资源
        Game.Instance.Shutdown();
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    void OnApplicationQuit()
    {
        Game.Instance.Shutdown();
    }

    /// <summary>
    /// 游戏暂停和恢复
    /// </summary>
    /// <param name="></param>
    void OnApplicationPause(bool ispause)
    {
        if (ispause)
            Game.Instance.Pause();
        else
            Game.Instance.Resume();
    }
}