/*
 * 文件名：ScheduledEventItem.cs
 * 描述：定时待办项，用于实现跨回合的战役级事件链（如官渡之战分 6 个阶段）
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 跨回合继承的槽位绑定。
    ///
    /// 为什么不用单纯的 "Key → Id" 字典：不同对象集合的 Id 会重叠
    /// （势力的 1 号与城市的 1 号都存在），只存 Id 无法判断该去哪个集合里取。
    /// 因此把对象类型一起存下来。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public struct BoundSlotRef
    {
        /// <summary>槽位 Key（与 EventActorSlot.Key 一致，如 "ActionForce"）</summary>
        [JsonProperty] public string Key;

        /// <summary>对象类型，决定去哪个集合里解析</summary>
        [JsonProperty] public EventSlotType Type;

        /// <summary>对象 Id</summary>
        [JsonProperty] public int Id;
    }

    /// <summary>
    /// 定时待办项：在指定回合触发某个事件，或直接执行一组效果。
    ///
    /// 为什么需要它：剧本事件的基础约定是"立即结算、不跨回合挂起"，
    /// 但官渡、赤壁、夷陵这类战役天然分阶段推进（数月到数年）。
    /// 本项就是"在某回合把某段剧情接上"的调度单位。
    ///
    /// 持久化位置：<see cref="Scenario.ScheduledEvents"/>，随剧本一起存读档。
    ///
    /// 【关键设计】跨回合后不继承对象引用，只继承**对象类型 + Id**。
    /// 因为等待期间武将可能死亡、部队可能解散、城市可能易主，
    /// 直接持有 SangoObject 引用会拿到失效对象。触发时按类型与 Id 重新解析并做有效性校验。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ScheduledEventItem
    {
        /// <summary>要触发的事件 Id</summary>
        [JsonProperty] public int TargetEventId;

        /// <summary>绝对触发回合数（= 登记时的 Scenario.Info.turnCount + DelayTurns）</summary>
        [JsonProperty] public int FireTurn;

        /// <summary>所属事件链 Id，用于编辑器分组展示与整链取消</summary>
        [JsonProperty] public int ChainId;

        /// <summary>所属事件链的阶段号</summary>
        [JsonProperty] public int Stage;

        /// <summary>登记时继承下来的槽位绑定（只存类型 + Id，理由见类注释）</summary>
        [JsonProperty] public List<BoundSlotRef> BoundSlots;

        /// <summary>
        /// 取消条件（序列化后的条件树 JSON）。
        /// 每回合检查一次，成立则丢弃本项——用于"袁绍提前灭亡就不再有官渡余波"这类场景。
        /// 求值时使用 <see cref="BoundSlots"/> 恢复的槽位。
        /// </summary>
        [JsonProperty] public string CancelConditionJson;

        /// <summary>
        /// 安排本项的事件 Id（日志追溯用，说明"这一项是谁安排的"）。
        /// 与 TargetEventId 不同：一个是"由谁安排"，一个是"要去触发谁"。
        /// </summary>
        [JsonProperty] public int SourceEventId;

        /// <summary>
        /// 生成便于调试面板显示的中文描述。
        /// </summary>
        /// <returns>中文描述文本</returns>
        public string Describe()
        {
            return $"定时事件 目标={TargetEventId} 触发回合={FireTurn} 链={ChainId} 阶段={Stage} 由事件={SourceEventId} 安排";
        }
    }
}
