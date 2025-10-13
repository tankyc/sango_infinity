using LuaInterface;
using Sango;
using Sango.Game;
using Sango.Tools;
using UnityEngine;
using System;

/// <summary>
/// 该文件由X框架自动生成
/// 请将此类挂到Gameobject上开始游戏
/// </summary>
public class GameStart : MonoBehaviour
{
    [NoToLua]
    public bool Debug = false;
    void Awake()
    {
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

        DontDestroyOnLoad(gameObject);
        //Sango.Tools.MapEditor.IsEditOn = true;
        //// 设置回调创建代理
        DelegateProxy.CreateProxy = DelegateFactory.CreateDelegate;
        DelegateProxy.CreateProxyEx = DelegateFactory.CreateDelegate;
        DelegateProxy.RemoveProxy = DelegateFactory.RemoveDelegate;
        DelegateProxy.RemoveProxyEx = DelegateFactory.RemoveDelegate;
        DelegateProxy.InitProxy = DelegateFactory.Init;

        //// 设置绑定代理
        BinderProxy.proxy = LuaBinder.Bind;

        Config.isDebug = Debug;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
#endif

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