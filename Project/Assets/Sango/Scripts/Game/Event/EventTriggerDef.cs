/*
 * 文件名：EventTriggerDef.cs
 * 描述：事件触发设定——什么时候、对谁、以什么频率检查这个事件
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件触发设定。
    ///
    /// 【性能要点】EarliestYear / LatestYear 是**索引键**，不是普通条件：
    /// 项目最终会有数百个历史事件，若每次 hook 都全量求值条件树，开销不可接受。
    /// ScenarioEventManager 用 (触发时机 → 年份区间) 做二级索引裁剪，
    /// 只对"本年份可能触发"的事件求值。因此这两个字段必须填准，
    /// 编辑器会从条件树里的 TimeRange / DateCheck 自动提取并校验一致性。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventTriggerDef
    {
        /// <summary>触发时机。决定订阅哪个 GameEvent 委托，见 EventTriggerKind 的说明</summary>
        [JsonProperty] public EventTriggerKind Kind = EventTriggerKind.TurnStart;

        /// <summary>触发作用域：对谁检查</summary>
        [JsonProperty] public EventTriggerScope Scope = EventTriggerScope.AnyForce;

        /// <summary>
        /// 作用域对应的 Id（Scope 为 SpecificForce / SpecificCity 时使用）。
        /// </summary>
        [JsonProperty] public int ScopeId;

        /// <summary>
        /// 仅 Kind = JobFinished 时生效：只关心这些工作类型（对应 MissionType 的名字）。
        /// 因为 GameEvent.OnPersonActionOver 只带 Person 一个参数、无法区分工作类型，
        /// 必须按 person.missionType 做二次判断。为空表示不筛选（任意工作完成都算）。
        /// </summary>
        [JsonProperty] public List<string> JobTypes;

        /// <summary>
        /// 仅 Kind = JobSettled 时生效：只关心这些工作**结果码**。
        ///
        /// 结果码沿用 City.DoJobSearching 的返回值：0 = 发现人才 / &gt;0 = 发现资金数额 / -1 = 一无所获。
        /// 为空表示不筛选（任何结果都触发）。
        ///
        /// 例：想让一个事件只在"探索一无所获"时播，填 [ -1 ]；
        ///     想只在"发现人才"时播，填 [ 0 ]。
        /// </summary>
        [JsonProperty] public List<int> JobResults;

        /// <summary>
        /// 索引键：最早可能触发的年份。0 表示"任何年份都要检查"。
        /// 无日期条件的历史事件应为 0；有年份限定的事件必须填，否则会白白参与全量求值。
        /// </summary>
        [JsonProperty] public int EarliestYear;

        /// <summary>索引键：最晚可能触发的年份。0 表示不设上限</summary>
        [JsonProperty] public int LatestYear;

        /// <summary>条件满足后延迟 N 回合再播放（用于"事件的余波"）</summary>
        [JsonProperty] public int DelayTurns;

        /// <summary>是否只触发一次</summary>
        [JsonProperty] public bool Once = true;

        /// <summary>触发次数上限（0 = 不限）。与 Once 同时存在时取更严格的一方</summary>
        [JsonProperty] public int MaxFireCount;

        /// <summary>两次触发之间至少间隔的回合数（0 = 不限）</summary>
        [JsonProperty] public int CooldownTurns;

        /// <summary>条件满足后的触发概率（百分比，100 = 必定触发）</summary>
        [JsonProperty] public int Chance = 100;

        /// <summary>
        /// 是否已经用完了触发次数（供调度器快速淘汰）。
        /// 只读判断，不修改状态。
        /// </summary>
        /// <param name="fireCount">该事件历史触发次数</param>
        /// <returns>true = 不可能再触发</returns>
        public bool IsExhausted(int fireCount)
        {
            if (Once && fireCount > 0) return true;
            if (MaxFireCount > 0 && fireCount >= MaxFireCount) return true;
            return false;
        }

        /// <summary>
        /// 年份区间是否与给定年份相交（索引裁剪用）。
        /// </summary>
        /// <param name="year">当前剧本年份</param>
        /// <returns>true = 本年份需要考虑该事件</returns>
        public bool MatchYear(int year)
        {
            if (EarliestYear > 0 && year < EarliestYear) return false;
            if (LatestYear > 0 && year > LatestYear) return false;
            return true;
        }
    }
}
