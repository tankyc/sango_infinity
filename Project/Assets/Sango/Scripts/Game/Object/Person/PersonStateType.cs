using System.IO;
using Newtonsoft.Json;

namespace Sango.Core
{
    public enum PersonStateType : int
    {
        /// <summary>
        /// 错误 / 身份未初始化（**非法武将**）。
        ///
        /// 【为什么是 0】对应 Open311 的 <c>mibun_ = -1</c>（s11_enum_scen.h:466-478 的值域是 0~8，
        /// -1 留给"未初始化"）。本项目把 0 拿来做这个哨兵值，原版的 0~8 顺势偏移成 1~9，
        /// 因此**原版的 <c>mibun_ >= 0</c> 等价于本项目的 <c>state > 0</c>**。
        ///
        /// 【处理约定】这类对象不是"某个身份"，而是"根本没有身份"：
        ///   · <see cref="Person.HasValidState"/> 为 false、<see cref="Person.IsInvalidPerson"/> 为 true；
        ///   · 不登场、不参与回合结算 / 寿终 / 患病 / 编成 / 成长 / 归属巡检；
        ///   · 但仍留在 <c>scenario.personSet</c> 里 —— 关系表与事件按 Id 引用，
        ///     直接从表里删会让这些引用悬空，正确做法是"留而不理"。
        ///
        /// 剧本里的"史实０１"这一类占位武将就是 state = 0（850 条里有 167 条）。
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