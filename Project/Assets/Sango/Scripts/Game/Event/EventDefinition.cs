/*
 * 文件名：EventDefinition.cs
 * 描述：事件定义——一个剧本事件的完整数据（JSON 表的一行）
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>事件链元信息：把"官渡之战"这样的战役拆成多个阶段事件，便于编辑器折叠成一组</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventChainInfo
    {
        /// <summary>链 Id（4000 段位预留给战役链）</summary>
        [JsonProperty] public int ChainId;

        /// <summary>链名（如"官渡之战"）</summary>
        [JsonProperty] public string Name;

        /// <summary>阶段号（从 1 开始）</summary>
        [JsonProperty] public int Stage;

        /// <summary>阶段名（如"斩颜良"）</summary>
        [JsonProperty] public string StageName;
    }

    /// <summary>结果情报的一条消息</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventResultMessage
    {
        /// <summary>消息文本，支持占位符（{:SlotKey}）</summary>
        [JsonProperty] public string Text;

        /// <summary>消息落到哪个对象上（槽位 Key），决定玩家点消息时镜头跳到哪里</summary>
        [JsonProperty] public string Slot;

        /// <summary>
        /// 消息归属的势力槽位：填了就只有该势力是玩家时才会显示。
        /// 留空表示"只要玩家势力在场就显示"。
        /// </summary>
        [JsonProperty] public string ForceSlot;
    }

    /// <summary>事件的结果情报（对应三国志14 事件结束后的提示窗）</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventResultInfo
    {
        /// <summary>标题</summary>
        [JsonProperty] public string Title;

        /// <summary>消息列表</summary>
        [JsonProperty] public List<EventResultMessage> Messages;
    }

    /// <summary>
    /// 事件与剧本的绑定过滤。
    /// 与条件树里的 TimeRange 不同：那是"游戏进行到的年份"，这里是"剧本的起始年份"。
    /// 官渡之战只在"公元 200 年开局"的剧本里才有意义，不应出现在 184 年开局的剧本里。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventScenarioFilter
    {
        /// <summary>过滤模式：Any / YearRange / ExplicitIds</summary>
        [JsonProperty] public string Mode = "Any";

        /// <summary>剧本起始年份下限</summary>
        [JsonProperty] public int YearMin;

        /// <summary>剧本起始年份上限</summary>
        [JsonProperty] public int YearMax;

        /// <summary>显式指定适用的剧本 Id</summary>
        [JsonProperty] public List<int> ScenarioIds;
    }

    /// <summary>
    /// 事件定义：JSON 表的一行，也是整个事件系统的核心数据。
    ///
    /// 加载来源（后加载者按 Id 覆盖前者）：
    ///   内置  Build/Content/Data/ScenarioEvent/**/*.json
    ///   自定义 <persistentDataPath>/CustomEdit/Data/ScenarioEvent/*.json
    ///   Mod   &lt;mod目录&gt;/Data/ScenarioEvent/*.json
    ///
    /// 关于 Conditions 与各指令的 Params 使用原始 JObject：
    /// 条件与指令的种类会持续新增（设计文档里就有 60 种条件、27 条指令），
    /// 若为每种都建 C# 数据类，每加一种都要同时改数据模型、序列化、编辑器三处。
    /// 用 JObject 则新增条件只需注册一个 Condition 实现即可。
    /// 类型安全由 EventValidator 在编辑器侧兜底（保存前校验）。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventDefinition
    {
        #region 基本信息

        /// <summary>全局唯一 Id。建议按分类分段：1000 史实 / 2000 通用 / 3000 人物 / 4000 战役链</summary>
        [JsonProperty] public int Id;

        /// <summary>事件名（编辑器显示）</summary>
        [JsonProperty] public string Name;

        /// <summary>分类，用于编辑器左侧列表分组</summary>
        [JsonProperty] public EventCategory Category = EventCategory.Common;

        /// <summary>说明（只给编辑器看，不显示给玩家）</summary>
        [JsonProperty] public string Description;

        /// <summary>是否启用</summary>
        [JsonProperty] public bool Enabled = true;

        /// <summary>
        /// 是否为「固定游戏事件」（不受剧本事件总开关影响）。
        ///
        /// 剧本事件按"是否吃玩家开关"分成两类：
        ///   · **常规剧本事件**（本字段 false，缺省）：跟随剧本参数里的
        ///     「启用剧本事件系统」(<see cref="ScenarioVariables.eventSystemEnabled"/>)，
        ///     玩家关掉后不再触发。历史事件、探索/登用这类内容向的事件都属于这一类。
        ///   · **固定游戏事件**（本字段 true）：无论总开关如何都照常触发。
        ///     它们承担的是"游戏流程完整性"——结局演出、必须给玩家的退出出口等。
        ///     例如「统一天下」：玩家关掉开关后若连结局都不播，剧本就断在半路，
        ///     既没有结局演出、也拿不到返回开始页面的入口。
        ///
        /// 【边界】只绕过**总开关**，不绕过触发次数 / 冷却 / 年份 / 作用域 / 条件树 / 概率。
        /// "固定"指的是"不受玩家开关影响"，不是"无条件发生"。
        /// </summary>
        [JsonProperty] public bool AlwaysEnabled;

        /// <summary>优先级：越大越先播放；同优先级按 Id 升序</summary>
        [JsonProperty] public int Priority;

        /// <summary>
        /// 事件等级，决定打断强度与是否受每回合上限约束。
        /// Critical 必播；Normal 正常弹窗；Ambient 只入"事件一览"；Silent 只结算不提示。
        /// </summary>
        [JsonProperty] public EventLevel Level = EventLevel.Normal;

        /// <summary>历史性强度：局势不符历史时，这个事件还触不触发</summary>
        [JsonProperty] public EventMode Mode = EventMode.Semihistorical;

        /// <summary>是否允许打断玩家当前操作（false = 只在回合开始等空闲点触发）</summary>
        [JsonProperty] public bool Interrupt = true;

        /// <summary>
        /// 调试日志开关（缺省关闭）。
        ///
        /// 打开后，这个事件的**判定过程**会逐条打到日志里（Sango.Log，Info 级）：
        ///   · 触发检查阶段：卡在哪一道闸（触发次数 / 冷却 / 年份 / 作用域 / 剧本过滤 / 密度 /
        ///     槽位绑定 / 条件树 / 概率），以及每个槽位绑到了谁；
        ///   · 条件树：每个子条件的成立与否（且组不满足时会列出**全部**不满足项，不是只报第一条）；
        ///   · 演出阶段：执行到第几条指令、是哪个 Op，以及跳转后落到了第几条。
        ///
        /// 【只影响日志】不改变任何判定与演出行为，可以随存档发布；
        /// 打开后每回合检查都会打一行，排查完记得关掉（尤其 AnyForce 作用域的事件）。
        /// </summary>
        [JsonProperty] public bool DebugLog;

        /// <summary>事件链元信息（可为空）</summary>
        [JsonProperty] public EventChainInfo Chain;

        /// <summary>剧本绑定过滤（可为空 = 适用所有剧本）</summary>
        [JsonProperty] public EventScenarioFilter ScenarioFilter;

        #endregion

        #region 触发与条件

        /// <summary>触发设定</summary>
        [JsonProperty] public EventTriggerDef Trigger = new EventTriggerDef();

        /// <summary>
        /// 角色槽。Key 沿用 ActionXxx / TargetXxx 的既有约定，
        /// 这样既有的条件（读 IConditionDatabase 的 Action/Target 属性）可以不加修改地复用。
        /// </summary>
        [JsonProperty] public List<EventActorSlot> Slots;

        /// <summary>
        /// 发生条件（条件树 JSON）。
        /// 结构见 EventConditionEvaluator：逻辑组合用 { "Type": "and", "Children": [...] }，
        /// 叶子条件用 { "Type": "条件名", "Params": { ... } }。
        /// 为空表示无条件。
        /// </summary>
        [JsonProperty] public JObject Conditions;

        #endregion

        #region 演出与结果

        /// <summary>演出指令流（线性 + Label / Jump 分支）</summary>
        [JsonProperty] public List<EventStage> Stages;

        /// <summary>结果情报（事件结束后弹出的提示）</summary>
        [JsonProperty] public EventResultInfo ResultInfo;

        /// <summary>事件私有变量初值（每个存档独立）</summary>
        [JsonProperty] public Dictionary<string, int> Variables;

        #endregion

        #region 运行时（不序列化）

        /// <summary>来源标记：内置 / 自定义 / Mod，用于编辑器保存时决定写到哪个目录</summary>
        [JsonIgnore] public string SourceTag;

        /// <summary>来源文件绝对路径，编辑器另存为时用</summary>
        [JsonIgnore] public string SourceFile;

        #endregion

        /// <summary>
        /// 取某个角色槽的定义，找不到返回 null。
        /// </summary>
        /// <param name="key">槽位 Key</param>
        /// <returns>槽位定义</returns>
        public EventActorSlot GetSlot(string key)
        {
            if (Slots == null || string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < Slots.Count; i++)
            {
                EventActorSlot slot = Slots[i];
                if (slot != null && slot.Key == key) return slot;
            }
            return null;
        }

        /// <summary>
        /// 生成便于日志排查的中文描述。
        /// </summary>
        /// <returns>形如 "官渡大捷(Id=3306, 链4001 阶段6)"</returns>
        public override string ToString()
        {
            if (Chain != null && Chain.ChainId > 0)
                return $"{Name}(Id={Id}, 链{Chain.ChainId} 阶段{Chain.Stage})";
            return $"{Name}(Id={Id})";
        }
    }
}
