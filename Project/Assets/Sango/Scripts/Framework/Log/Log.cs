/*
'*******************************************************************
'Tank Framework
'*******************************************************************
*/
using UnityEngine;
namespace Sango
{
    /// <summary>
    /// 游戏日志管理器.
    /// 所有游戏日志需要从该处打印
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 日志输出类型,用来格式化日志的标题,标题颜色
        /// 为了优化诸如: XLog.Log( ffff(dddd()) );中对于链式结构造成的链式函数性能消耗,XLog并没有开关去控制显示,请自行判断后再调用XLog.如: if(xxx) XLog.Log(ffff);
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

        public static void Print(object message, LogType t)
        {
            if (t == LogType.None)
                Debug.Log(message.ToString());
            else
                Debug.Log(format(message, t));
        }
        public static void Print(object message)
        {
            Print(message, LogType.None);
        }

        public static void Error(object message, LogType t)
        {
            if (t == LogType.None)
                Debug.LogError(message.ToString());
            else
                Debug.LogError(format(message, t));
        }
        public static void Error(object message)
        {
            Error(message, LogType.None);
        }

        public static void Warning(object message, LogType t)
        {
            if (t == LogType.None)
                Debug.LogWarning(message.ToString());
            else
                Debug.LogWarning(format(message, t));
        }
        public static void Warning(object message)
        {
            Warning(message, LogType.None);
        }

    }
}