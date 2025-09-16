/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using LuaInterface;

namespace Sango
{
    /// <summary>
    /// 游戏配置类, 可开放至第三方脚本修改
    /// </summary>
    public class Config
    {
        #region 配置 config
        /// <summary>
        /// 调试信息开关
        /// </summary>
        public static bool isDebug = false;

        #endregion //配置

        #region 游戏引擎配置 config_engine
        /// <summary>
        /// 脚本行为物件名字,用于UnityBehaviour链接Lua
        /// </summary>
        [NoToLua]
        public const string SCRIPT_BEHAVIOUR_NAME = "Behaviour";

        /// <summary>
        /// 模块名字,用于C#功能链接LUA
        /// </summary>
        [NoToLua]
        public const string SCRIPT_CONTAINER_NAME = "Container";

        #endregion //游戏配置

        #region Behaviour
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string AWAKE_REF = "Awake";
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string START_REF = "Start";
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string DESTROY_REF = "OnDestroy";
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string ENABLE_REF = "OnEnable";
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string DISABLE_REF = "OnDisable";
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string TRIGGERENTER_REF = "OnTriggerEnter";
        /// <summary>
        /// 调用脚本名字
        /// </summary>
        [NoToLua]
        public const string TRIGGEREXIT_REF = "OnTriggerExit";
        #endregion Behaviour

    }
}