/*
 * 文件名：PersonMarkType.cs
 * 描述：武将标记位枚举（容器为 Person.markFlag）
 * 创建日期：2026-11-12
 */

namespace Sango.Core
{
    /// <summary>
    /// 武将标记位（容器见 <see cref="Person.markFlag"/>）。
    ///
    /// 【取值语义】枚举值是**位序号**（从 0 起），不是掩码 —— 与 <see cref="BitCheck32"/> 的
    /// <c>Has / Set / Remove(bitPos)</c> 直接对应。不要照 PathFlag（掩码风格，取值形如 1 &lt;&lt; 5）
    /// 的用法往这里套：传掩码进来会落到错误的位上。
    ///
    /// 【谁负责置位】唯一初始化入口是 <see cref="Person.InitPersonActions"/>，分两步：
    ///   ① <see cref="Person.RefreshMarks"/> 从当前状态推导系统来源（名马 / 特技强运）；
    ///   ② 特技 / 装备的 <c>PersonSetMark</c> Action 在装配阶段按配置 SetMark / ClearMark。
    /// 标记位不存档，每次初始化都整张重建，因此不存在"卸了装备位还在"的残留。
    /// </summary>
    public enum PersonMarkType : int
    {
        /// <summary>
        /// 免疫俘虏 —— 不会被收押，城陷 / 部队溃灭 / 单挑三条路径共用
        /// （判定入口 <see cref="Person.IsCaptureImmune"/>）。
        /// 当前来源：装备名马（工程内骑乘任意马匹即视为名马）、特技「强运」（#33）。
        /// </summary>
        IgnoreCapture = 0,
    }
}
