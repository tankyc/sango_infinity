using System.IO;
using Newtonsoft.Json;

namespace Sango.Core
{
    public enum PersonStateType : int
    {
        /// <summary>
        /// 错误
        /// </summary>
        Error = 0,

        /// <summary>
        /// 君主
        /// </summary>
        Governor,

        /// <summary>
        /// 都督
        /// </summary>
        Commander,

        /// <summary>
        /// 太守
        /// </summary>
        Leader,

        /// <summary>
        /// 普通
        /// </summary>
        Normal,

        /// <summary>
        /// 在野
        /// </summary>
        Unemployed,

        /// <summary>
        /// 俘虏
        /// </summary>
        Prisoner,

        /// <summary>
        /// 未登场
        /// </summary>
        Invalid,

        /// <summary>
        /// 未发现
        /// </summary>
        Invisible,

        /// <summary>
        /// 死亡
        /// </summary>
        Dead,


        Max
    }
}