/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using System.Diagnostics;
using UnityEngine;
namespace Sango
{
    /// <summary>
    /// 游戏日志管理器.
    /// 所有游戏日志需要从该处打印
    ///
    /// 【编译期开关】现有策略（已按"Error 必须进包"确定）：
    ///   · Info / Warning：标了 [Conditional("SANGO_DEBUG")]。未定义该宏的构建（正式包）里，
    ///     编译器把**调用连同实参求值一起删除** —— `Log.Info($"...{a}{b}")` 既不构造字符串也不发生调用，零开销。
    ///     开发期需要看日志时，Player Settings → Scripting Define Symbols 加上 SANGO_DEBUG 即可，不必改代码。
    ///   · Error：**不加 Conditional，正式包始终输出**（线上排障依赖它）。
    ///     代价是它的实参会被真实求值，所以不要在每帧路径里塞 Error，也不要传昂贵表达式。
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 日志输出类型,用来格式化日志的标题,标题颜色
        /// 日志有无由编译期开关 SANGO_DEBUG 决定（见类注释），类型只影响标题与颜色。
        /// todo: 日志在真机上写入文件,文件需要定期清理,防止塞满用户储存空间
        /// </summary>
        public enum LogType : int
        {
            None,
            Assets,
            Network,
            Object,
            Script,
            UI,
            World,
            Sound,
            Game,
            Download,
            Other
        }

        /// <summary>
        /// 日志输出标题颜色,与LogType对应
        /// </summary>
        static string[] colorArray = { "",
                                         "yellow",      //Assets
                                         "#99ff00",     //Network
                                         "#33ddff",     //Object
                                         "#dddddd",     //Script
                                         "#00ff00",     //UI
                                         "#ff8800",     //World
                                         "#00ffff",     //Sound
                                         "#ff8888",     //Game
                                         "pink",        //Download
                                         "white",        //Other
                                     };


        private static string format(object message, LogType t)
        {
#if UNITY_EDITOR
            return string.Format("<color={0}><b>{1} : </b></color><color=#eeeeee>{2}</color>", colorArray[(int)t], t.ToString(), message.ToString());
#else
            return message.ToString();
#endif
        }

        [Conditional("SANGO_DEBUG")]
        public static void Info(object message, LogType t)
        {
            if (t == LogType.None)
                UnityEngine.Debug.Log(message.ToString());
            else
                UnityEngine.Debug.Log(format(message, t));
        }

        [Conditional("SANGO_DEBUG")]
        public static void Info(object message)
        {
            Info(message, LogType.None);
        }

        public static void Error(object message, LogType t)
        {
            if (t == LogType.None)
                UnityEngine.Debug.LogError(message.ToString());
            else
                UnityEngine.Debug.LogError(format(message, t));
        }

        public static void Error(object message)
        {
            Error(message, LogType.None);
        }

        [Conditional("SANGO_DEBUG")]
        public static void Warning(object message, LogType t)
        {
            if (t == LogType.None)
                UnityEngine.Debug.LogWarning(message.ToString());
            else
                UnityEngine.Debug.LogWarning(format(message, t));
        }

        [Conditional("SANGO_DEBUG")]
        public static void Warning(object message)
        {
            Warning(message, LogType.None);
        }

    }
}
