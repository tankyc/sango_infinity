/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
namespace Sango
{
    static public class Platform
    {
        public enum PlatformName
        {
            Android,
            Ios,
            Window,
            Mac,
            Webgl,
            Webgl_wechat,
            Webgl_tiktok,
        }

        /// <summary>
        /// 当前平台
        /// </summary>
        public static PlatformName targetPlatform;

        /// <summary>
        /// Java类名
        /// </summary>
        static public string JaveClassName = "cn.com.XFramework.XAndroidSDK";

        /// <summary>
        /// Java工具类名
        /// </summary>
        static public string JaveUtilityClassName = "cn.com.XFramework.XAndroidUtility";

        /// <summary>
        /// 资源版本号
        /// </summary>
        static public string ResourceVersion = "0.0.1";

        /// <summary>
        /// 是否使用JIT
        /// </summary>
        public static bool useJit = false;

        /// <summary>
        /// 谁否为编辑器模式
        /// </summary>
        public static bool isEditorMode
        {
            get { return UnityEngine.Application.isEditor; }
        }

        /// <summary>
        /// 平台相关初始化
        /// </summary>
        public static void Init()
        {
            PlatformListener.Init();
        }

        /// <summary>
        /// 获取平台名字
        /// </summary>
        /// <returns></returns>
        static public string GetPlatformName()
        {
           return PlatformUtility.GetPlatformName();
        }
    }
}
