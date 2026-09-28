/*
 * 文件名：EventRunState.cs
 * 描述：剧本事件的运行时状态（触发次数、冷却、私有变量、旗标），随存档持久化
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 单个事件的运行时状态。
    ///
    /// 持久化位置：<see cref="Scenario.EventStates"/>，随剧本一起存读档。
    /// 之所以必须进存档：不加的话，读档后 Once 事件会重复触发
    /// （项目在 d0a5a7e9 刚整治完"反复读档造成的事件重复"，事件系统不能成为新的泄漏源）。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventRunState
    {
        /// <summary>事件 Id（EventDefinition.Id），作为查表键</summary>
        [JsonProperty] public int EventId;

        /// <summary>已触发次数。Once 事件此值大于 0 即不再触发</summary>
        [JsonProperty] public int FireCount;

        /// <summary>上次触发的回合数（Scenario.Info.turnCount），用于 CooldownTurns 判定</summary>
        [JsonProperty] public int LastFireTurn = -1;

        /// <summary>事件私有变量（同一事件内的指令与效果共享），不同事件互不干扰</summary>
        [JsonProperty] public Dictionary<string, int> Vars;

        /// <summary>事件旗标（可跨事件读取，用于表达"某情节已完成"）</summary>
        [JsonProperty] public Dictionary<string, bool> Flags;

        /// <summary>上一次前置事件的结局标记（多结局事件用），如 "CaoWin"</summary>
        [JsonProperty] public string Outcome;

        /// <summary>
        /// 最近一次求值失败的原因（调试用）。
        /// 刻意不序列化：读档后是过期信息，反而会误导排查，重新求值即可刷新。
        /// </summary>
        [JsonIgnore] public string LastFailReason;

        /// <summary>
        /// 最近一次求值耗时（毫秒，调试用）。同样不序列化，理由见上。
        /// </summary>
        [JsonIgnore] public float LastEvalCostMs;

        /// <summary>
        /// 最近一次触发的回合（数据用，与 LastFireTurn 同义但允许为 -1 表示从未触发）。
        /// 这里只作为可读性补充，不额外占用存储。
        /// </summary>
        public bool HasFired { get { return FireCount > 0; } }

        /// <summary>
        /// 读取一个事件私有变量，不存在时返回默认值。
        /// </summary>
        /// <param name="key">变量名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>变量值</returns>
        public int GetVar(string key, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(key) || Vars == null) return defaultValue;
            return Vars.TryGetValue(key, out int v) ? v : defaultValue;
        }

        /// <summary>
        /// 写入一个事件私有变量（字典懒创建）。
        /// </summary>
        /// <param name="key">变量名</param>
        /// <param name="value">变量值</param>
        public void SetVar(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (Vars == null) Vars = new Dictionary<string, int>();
            Vars[key] = value;
        }

        /// <summary>
        /// 读取一个事件旗标，不存在时返回缺省值。
        /// </summary>
        /// <param name="key">旗标名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>旗标值</returns>
        public bool GetFlag(string key, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(key) || Flags == null) return defaultValue;
            return Flags.TryGetValue(key, out bool v) ? v : defaultValue;
        }

        /// <summary>
        /// 写入一个事件旗标（字典懒创建）。
        /// </summary>
        /// <param name="key">旗标名</param>
        /// <param name="value">旗标值</param>
        public void SetFlag(string key, bool value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (Flags == null) Flags = new Dictionary<string, bool>();
            Flags[key] = value;
        }
    }
}
