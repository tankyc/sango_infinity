/*
 * 文件名：EventActorSlot.cs
 * 描述：剧本事件的角色槽——"这个事件里谁出场"，以及从哪里把他找出来
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 角色槽的筛选条件。只在来源为"随机 / 按条件挑一个"时生效
    /// （如 ForceRandomPerson / CityRandomPerson / ForceBestPerson）。
    /// 所有字段留空或填 -1 表示"不限"。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventSlotFilter
    {
        /// <summary>是否要求存活（null = 不限）</summary>
        [JsonProperty] public bool? Alive;

        /// <summary>所在状态：InForce / InCity / Free（null = 不限）</summary>
        [JsonProperty] public string Belong;

        /// <summary>性别（-1 = 不限）</summary>
        [JsonProperty] public int Gender = -1;

        /// <summary>
        /// 属性筛选的属性名：Command / Strength / Intelligence / Politics / Glamour。
        /// 注意必须走 Person 的公共属性（规则要求），不能直接读五维字段。
        /// </summary>
        [JsonProperty] public string Attribute;

        /// <summary>属性下限（-1 = 不限）</summary>
        [JsonProperty] public int AttributeMin = -1;

        /// <summary>属性上限（-1 = 不限）</summary>
        [JsonProperty] public int AttributeMax = -1;

        /// <summary>忠诚下限（-1 = 不限）</summary>
        [JsonProperty] public int LoyaltyMin = -1;

        /// <summary>忠诚上限（-1 = 不限）</summary>
        [JsonProperty] public int LoyaltyMax = -1;

        /// <summary>必须拥有其中任一特技（空 = 不限）</summary>
        [JsonProperty] public List<int> SkillIdList;

        /// <summary>必须与这些槽位属于同一势力（空 = 不限）</summary>
        [JsonProperty] public List<string> SameForceAsSlot;

        /// <summary>排除这些槽位已经占用的对象，避免同一人占两个槽</summary>
        [JsonProperty] public List<string> ExcludeSlotKeys;

        /// <summary>直接排除的对象 Id</summary>
        [JsonProperty] public List<int> ExcludeIds;

        /// <summary>是否要求"空闲"（无任务、不在部队中）</summary>
        [JsonProperty] public bool? Free;
    }

    /// <summary>
    /// 事件角色槽。
    ///
    /// 它是"三国志14 事件编辑器里登场人物"的对应物，也是旧 ScenarioEvent 的
    /// Action* / Target* 双槽的**超集**：Key 沿用 ActionForce / TargetPerson 这类命名，
    /// 因此既有的条件（走 IConditionDatabase 的 Action/Target 属性）可以不加修改地使用。
    ///
    /// 【重要】槽位解析是**确定性的**：同一次触发内，随机来源的结果会被 EventContext 缓存，
    /// 保证同一事件里多次引用同一个槽得到同一个对象。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventActorSlot
    {
        /// <summary>
        /// 槽位 Key。既是占位符（{:Key}）与效果参数里引用的名字，
        /// 也是条件数据库里的键。建议沿用 ActionXxx / TargetXxx 的既有约定。
        /// </summary>
        [JsonProperty] public string Key;

        /// <summary>对象类型，决定绑定结果如何解析与校验</summary>
        [JsonProperty] public EventSlotType Type = EventSlotType.Person;

        /// <summary>取值来源</summary>
        [JsonProperty] public EventSlotSource Source = EventSlotSource.Fixed;

        /// <summary>
        /// 来源参数。含义随 Source 变化：
        ///   · Fixed            → 对象 Id
        ///   · EventEntity      → 类型名（如 "Person" / "City"）
        ///   · ForceBestPerson  → 属性名（如 "Intelligence"）
        ///   · TroopOfPerson    → 武将槽的 Key
        ///   · NeighborTroop    → 部队槽的 Key
        ///   · DerivedOfSlot    → 形如 "SlotKey.BelongForce"
        ///   · ScenarioVariable → 剧本变量名
        /// </summary>
        [JsonProperty] public string SourceArg;

        /// <summary>取值来源指向的势力/城市槽（用于 ForceBestPerson 等需要作用域的来源）</summary>
        [JsonProperty] public string ScopeSlot;

        /// <summary>筛选条件（随机来源时生效）</summary>
        [JsonProperty] public EventSlotFilter Filters;

        /// <summary>
        /// 找不到时是否允许事件继续。
        /// false（默认）= 绑定失败即放弃整个事件；true = 槽位置空，引用它的指令跳过。
        /// </summary>
        [JsonProperty] public bool Optional;

        /// <summary>编辑器显示名（如"主角势力""军师"），不参与逻辑</summary>
        [JsonProperty] public string Label;

        /// <summary>
        /// 备选来源：按顺序尝试，第一个绑定成功的即采用。
        /// 用于"周瑜或鲁肃""关羽或张飞"这类历史弹性。
        /// 非空时优先于 Source / SourceArg 使用。
        /// </summary>
        [JsonProperty] public List<EventSlotAlternative> Alternatives;

        /// <summary>生成便于日志排查的中文描述</summary>
        public override string ToString()
        {
            string label = string.IsNullOrEmpty(Label) ? Key : Label;
            return $"{label}({Key}, {Type}, {Source})";
        }
    }

    /// <summary>角色槽的备选来源</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventSlotAlternative
    {
        /// <summary>备选来源</summary>
        [JsonProperty] public EventSlotSource Source = EventSlotSource.Fixed;

        /// <summary>备选来源参数（含义同 EventActorSlot.SourceArg）</summary>
        [JsonProperty] public string SourceArg;

        /// <summary>备选来源指向的槽位</summary>
        [JsonProperty] public string ScopeSlot;

        /// <summary>备选来源的筛选条件</summary>
        [JsonProperty] public EventSlotFilter Filters;
    }
}
