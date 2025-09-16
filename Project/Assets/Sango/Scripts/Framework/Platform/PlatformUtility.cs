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
#if UNITY_IPHONE && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        extern static public string GetDeviceId();
#endif
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
            switch (Platform.targetPlatform)
            {
                case Platform.PlatformName.Android:
                    return "android";
                case Platform.PlatformName.Ios:
                    return "ios";
                case Platform.PlatformName.Window:
                    return "win";
                case Platform.PlatformName.Mac:
                    return "mac";
                default:
                    return "editor";
            }
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


        static public void Restart(int t = 0)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaClass ac = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject ao = ac.GetStatic<AndroidJavaObject>("currentActivity");
            if (t == 0)
                ao.Call("restartApp");
            else
                ao.Call("restartApp2");
            ao.Dispose();
            ac.Dispose();
#endif
        }

    }
}