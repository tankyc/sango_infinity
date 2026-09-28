/*
 * 文件名：EventEffectNode.cs
 * 描述：事件效果节点——"结算时对世界做什么改动"
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Event
{
    /// <summary>
    /// 一个事件效果节点。
    ///
    /// 形如：
    ///   { "Type": "PersonAddLoyalty", "Params": { "Slot": "TargetPerson", "Value": { "GainPlace": "EventLoyaltyGain" } } }
    ///
    /// Type 由 <see cref="EventEffectFactory"/> 解析为具体的 EventEffectBase 实现；
    /// Params 里所有数值字段都支持三种写法：
    ///   · 字面量数字        100
    ///   · 剧本变量引用      "#VarName"
    ///   · 成长配置键        { "GainPlace": "EventMeritCommon" }   ← 成长数值必须用这种（项目硬约束）
    /// 求值由 <see cref="EventValue.EvalInt"/> 统一负责。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventEffectNode
    {
        /// <summary>效果类型名，必须已在 EventEffectFactory 注册</summary>
        [JsonProperty] public string Type;

        /// <summary>效果参数</summary>
        [JsonProperty] public JObject Params;

        /// <summary>
        /// 生效条件（条件树的 JSON）。
        /// 为空表示无条件生效；不满足时本效果被跳过、其余效果继续执行。
        /// 用于"若某武将还活着才给奖励"这类局部判断。
        /// </summary>
        [JsonProperty] public JObject Condition;

        /// <summary>编辑器的备注，不参与逻辑</summary>
        [JsonProperty] public string Comment;

        /// <summary>生成便于日志排查的中文描述</summary>
        public override string ToString()
        {
            return string.IsNullOrEmpty(Type) ? "(未指定效果)" : Type;
        }
    }
}
