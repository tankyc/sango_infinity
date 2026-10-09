/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using UnityEngine;
namespace Sango
{
    public static class PlatformUtility
    {
//#if UNITY_IPHONE && !UNITY_EDITOR
//        [System.Runtime.InteropServices.DllImport("__Internal")]
//        extern static public string GetDeviceId();
//#endif
        static public void Init()
        {

        }

        /// <summary>
        /// 获取游戏版本号
        /// </summary>
        /// <returns></returns>
        static public string GetApplicationVersion()
        {
            return UnityEngine.Application.version;
        }
        /// <summary>
        /// 获取游戏版本号
        /// </summary>
        /// <returns></returns>
        static public string GetResourceVersion()
        {
            return Platform.ResourceVersion;
        }

        /// <summary>
        /// 获取平台名字
        /// </summary>
        /// <returns></returns>
        static public string GetPlatformName()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return "mac";
#elif UNITY_STANDALONE_WIN
            return "win";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_IPHONE
            return "ios";
#elif UNITY_WEBGL
            return "webgl";
#endif
        }

        /// <summary>
        /// 获取游戏包名
        /// </summary>
        /// <returns></returns>
        static public string GetBundleIdentifier()
        {
            return UnityEngine.Application.identifier;
        }

        /// <summary>
        /// 获取游戏包名
        /// </summary>
        /// <returns></returns>
        static public string GetCompanyName()
        {
            return UnityEngine.Application.companyName;
        }

        /// <summary>
        /// 获取游戏包名
        /// </summary>
        /// <returns></returns>
        static public string GetProductName()
        {
            return UnityEngine.Application.productName;
        }

        /// <summary>
        /// 获取手机型号
        /// </summary>
        /// <returns></returns>
        static public string GetPhoneModel()
        {
            return UnityEngine.SystemInfo.deviceUniqueIdentifier;
        }

        /// <summary>
        /// 安装APP
        /// </summary>
        /// <param name="fileName"></param>
        static public void InstallApp(string fileName)
        {
            Application.OpenURL(fileName);
        }

        /// <summary>
        /// 获取设备ID
        /// </summary>
        /// <returns></returns>
        static public string GetDeviceId()
        {
#if UNITY_IPHONE && !UNITY_EDITOR
            return GetDeviceId();
#elif UNITY_ANDROID && !UNITY_EDITOR
            string idStr = "";
            AndroidJavaClass ac = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject ao = ac.GetStatic<AndroidJavaObject>("currentActivity");
            idStr = ao.Call<string>("GetDeviceId");
            ao.Dispose();
            ac.Dispose();
            return idStr;
#else
            return UnityEngine.SystemInfo.deviceUniqueIdentifier;
#endif
        }


        /// <summary>
        /// 宿主 Activity 提供的重启方法名(原生实现)。
        /// 原生侧接口改名时只需要修改这里,不需要改调用方。
        /// </summary>
        static public string RestartMethodName = "restartApp";

        /// <summary>
        /// 重启游戏(仅在Android真机上生效)
        ///
        /// 旧实现只调用宿主 Activity 的 restartApp:原生侧没有实现该方法时会抛 AndroidJavaException,
        /// 而且没有任何异常处理与回退,玩家点了"重启"游戏既没重启、也没退出,表现成"按钮没反应"。
        /// 现在的实现按顺序尝试,任一成功即返回 true:
        ///   1. 宿主 Activity 的原生重启接口(<see cref="RestartMethodName"/>),与旧实现保持兼容;
        ///   2. 标准 Android 接口:用 PackageManager 取出本应用的启动 Intent 重新拉起,再结束当前进程。
        /// 全部失败时返回 false,由调用方(Platform.RestartGame)决定是否退化为直接退出游戏。
        /// </summary>
        /// <returns>是否已成功发起重启</returns>
        static public bool Restart()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (RestartByActivity())
                return true;

            if (RestartByLaunchIntent())
                return true;

            Sango.Log.Error("重启游戏失败:原生重启接口与本应用启动Intent均不可用");
            return false;
#else
            // 编辑器 / iOS / 桌面 / WebGL 都没有"重新拉起自身进程"的能力,
            // 需要重启的场合请调用 Platform.RestartGame(它已按平台分别处理)
            Sango.Log.Warning("PlatformUtility.Restart 仅在Android真机上生效,当前平台请使用 Platform.RestartGame");
            return false;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR

        /// <summary>
        /// 调用宿主 Activity 的原生重启接口(方法名见 <see cref="RestartMethodName"/>)
        /// </summary>
        /// <returns>原生接口是否调用成功</returns>
        static bool RestartByActivity()
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    // using 保证 JNI 对象在任何分支下都会被释放,不会因为异常泄漏 LocalRef
                    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        if (activity == null)
                        {
                            Sango.Log.Warning("获取 currentActivity 失败,无法调用原生重启接口");
                            return false;
                        }

                        activity.Call(RestartMethodName);
                        Sango.Log.Info("已通过原生接口重启游戏: " + RestartMethodName);
                        return true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning($"原生重启接口 {RestartMethodName} 调用失败,尝试标准重启方式: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 标准 Android 重启:取出本应用的启动 Intent 重新拉起应用,再结束当前进程。
        /// 不依赖宿主 Activity 提供任何自定义接口,适用于原生侧没有 restartApp 的包。
        /// </summary>
        /// <returns>是否已成功发起重启</returns>
        static bool RestartByLaunchIntent()
        {
            try
            {
                // FLAG_ACTIVITY_NEW_TASK | FLAG_ACTIVITY_CLEAR_TOP | FLAG_ACTIVITY_CLEAR_TASK
                const int launchFlags = 0x10000000 | 0x04000000 | 0x00008000;

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        if (activity == null)
                        {
                            Sango.Log.Warning("获取 currentActivity 失败,无法通过启动Intent重启");
                            return false;
                        }

                        using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
                        using (AndroidJavaObject launchIntent = packageManager.Call<AndroidJavaObject>(
                                   "getLaunchIntentForPackage", activity.Call<string>("getPackageName")))
                        {
                            if (launchIntent == null)
                            {
                                Sango.Log.Warning("未取到本应用的启动Intent,无法通过启动Intent重启");
                                return false;
                            }

                            launchIntent.Call<AndroidJavaObject>("addFlags", launchFlags);
                            activity.Call("startActivity", launchIntent);
                        }
                    }
                }

                Sango.Log.Info("已通过启动Intent重新拉起游戏,即将结束当前进程");
                KillProcess();
                return true;
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning("通过启动Intent重启失败: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 结束当前进程(Android 标准接口)。
        /// 新实例已经拉起,旧进程必须退出,否则会出现两个游戏实例并存。
        /// </summary>
        static void KillProcess()
        {
            try
            {
                using (AndroidJavaClass processClass = new AndroidJavaClass("android.os.Process"))
                {
                    int pid = processClass.CallStatic<int>("myPid");
                    processClass.CallStatic("killProcess", pid);
                }
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning("结束当前进程失败,退化为直接退出应用: " + e.Message);
                Application.Quit();
            }
        }

#endif

    }
}