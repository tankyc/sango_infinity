/*
 * 文件名：Scenario.cs
 * 描述：剧本剧情事件类（**旧版，已被取代**）
 * 创建日期：2026-03-27
 * 最后修改：2026-09-26
 *
 * ⚠ 本文件里的旧事件体系已被 Sango.Core.Event 命名空间下的新框架取代：
 *     · 数据结构   ScenarioEvent          → EventDefinition / EventTriggerDef / EventStage
 *     · 调度器     ScenerioEventManager   → ScenarioEventManager（注意旧类名还把 Scenario 拼错了）
 *     · 数据源     Data/ScenarioEvent/*.json（旧格式）→ 同目录，但改为新 Schema
 *     · 占位符     FormatContent          → EventValue.EvalText（同样支持 {:槽位Key}）
 *
 *   保留原因：IScenarioEventData 仍是"角色槽命名约定"的规范说明
 *   （ActionForce / TargetPerson / ActionCity …），新框架的 EventConditionDatabase
 *   刻意沿用这套命名，使既有 Condition 能不加修改地被事件复用。
 *   其余类型已无使用者，标记 Obsolete 以防新代码误用。
 *
 *   ⚠ 注意：旧类名 ScenarioEvent 与新命名空间里的 ScenarioEventManager 只差一个词，
 *   在同一文件里同时 using Sango.Core 与 Sango.Core.Event 时要小心歧义。
 *   旧体系计划在 Phase 6 整体删除。
 */

using Sango.Render;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 剧情数据获取接口类。
    ///
    /// 本接口没有标记 Obsolete：它是"角色槽命名约定"的规范说明
    /// （ActionForce / TargetPerson / ActionCity ……），新框架的 EventConditionDatabase
    /// 沿用同一套 Key，因此这里仍作为命名依据保留。
    /// </summary>
    public interface IScenarioEventData
    {
        Person ActionGovernor { get; }
        Person ActionCounsellor { get; }
        Person TargetGovernor { get; }
        Person TargetCounsellor { get; }
        SkillInstance ActionSkill { get; }
        SkillInstance TargetSkill { get; }
        Person ActionPerson { get; }
        Person TargetPerson { get; }
        Troop ActionTroop { get; }
        Troop TargetTroop { get; }
        Cell ActionCell { get; }
        Cell TargetCell { get; }
        City ActionCity { get; }
        City TargetCity { get; }
        Corps ActionCorps { get; }
        Corps TargetCorps { get; }
        Force ActionForce { get; }
        Force TargetForce { get; }
        object ActionObject { get; }
        object TargetObject { get; }
    }

    /// <summary>旧事件类型分类。已被 <see cref="Sango.Core.Event.EventOpType"/>（演出指令集）取代。</summary>
    [System.Obsolete("旧事件体系已被 Sango.Core.Event 取代，请使用 EventOpType")]
    public enum ScenarioEventType
    {
        Text,
        PersonTalk,
        PersonTalkChoice,
    }

    /// <summary>
    /// 剧情逻辑基类。
    /// 已被 <see cref="Sango.Core.Event.EventRunner"/>（状态机）+ <see cref="Sango.Core.Event.EventStage"/>（指令流）取代。
    /// </summary>
    [System.Obsolete("旧事件体系已被 Sango.Core.Event 取代，请使用 EventRunner")]
    public class ScenarioEventBase : RenderEventBase
    {
        public IScenarioEventData scenarioEventData;

    }

    /// <summary>
    /// 旧版剧本事件数据（一条剧情 = 一个类型 + 一段格式化文本 + 后续事件 Id）。
    ///
    /// ⚠ 已被 <see cref="Sango.Core.Event.EventDefinition"/> 取代：
    ///   旧的"一个事件只有一句文本"无法表达多句台词、选择肢、效果结算与分支，
    ///   新框架用 EventStage 的线性指令流 + Label/Jump 表达，
    ///   并支持触发条件、角色槽、跨回合定时队列。
    ///
    /// 本类已无外部使用者（只被 ScenerioEventManager 的字典引用），Phase 6 整体删除。
    /// </summary>
    [System.Obsolete("旧事件体系已被 Sango.Core.Event 取代，请使用 EventDefinition")]
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioEvent : SangoObject
    {
        public bool IsDone { get; set; }
        public ScenarioEventType eventType;
        public Condition condition;
        public string formatContent;
        public List<string> variables;
        public List<int> nextEvent;

        /// <summary>
        /// 格式化剧情内容，将formatContent中的{:变量名}占位符替换为IScenarioEventData对应属性的Name值
        /// 占位符格式：{:ActionGovernor}，其中ActionGovernor为IScenarioEventData中定义的属性名
        /// </summary>
        /// <param name="data">剧情事件数据，包含武将、部队、城市等对象引用</param>
        /// <returns>格式化后的剧情文本</returns>
        public string FormatContent(IScenarioEventData data)
        {
            if (string.IsNullOrEmpty(formatContent))
            {
                return formatContent;
            }

            if (data == null)
            {
                return formatContent;
            }

            // 使用StringBuilder拼接最终字符串，手动解析占位符
            StringBuilder sb = new StringBuilder();
            int index = 0;
            int length = formatContent.Length;

            while (index < length)
            {
                // 查找占位符起始标记 "{:"
                int placeholderStart = formatContent.IndexOf("{:", index);
                if (placeholderStart < 0)
                {
                    // 没有更多占位符，直接追加剩余部分
                    sb.Append(formatContent, index, length - index);
                    break;
                }

                // 追加占位符之前的普通文本
                if (placeholderStart > index)
                {
                    sb.Append(formatContent, index, placeholderStart - index);
                }

                // 查找占位符结束标记 "}"
                int placeholderEnd = formatContent.IndexOf('}', placeholderStart + 2);
                if (placeholderEnd < 0)
                {
                    // 没有找到闭合的}，将"{:"及之后内容当作普通文本处理
                    sb.Append(formatContent, index, length - index);
                    break;
                }

                // 提取占位符中的变量名
                int varNameStart = placeholderStart + 2;
                string varName = formatContent.Substring(varNameStart, placeholderEnd - varNameStart);

                // 根据变量名获取对应的属性值并解析其Name
                string nameValue = GetNameByVariableName(data, varName);

                // 追加替换后的值
                sb.Append(nameValue);

                // 移动到占位符之后
                index = placeholderEnd + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 根据变量名从IScenarioEventData中获取对应属性的Name值
        /// 不使用反射，直接通过接口属性访问
        /// </summary>
        /// <param name="data">剧情事件数据</param>
        /// <param name="variableName">变量名</param>
        /// <returns>对应对象的Name值，找不到返回空字符串</returns>
        public static string GetNameByVariableName(IScenarioEventData data, string variableName)
        {
            // 根据变量名直接获取接口属性，不使用反射
            object obj = GetVariableObject(data, variableName);
            if (obj == null)
            {
                return string.Empty;
            }

            // 通过类型检查直接获取Name，不使用反射
            return GetObjectName(obj);
        }

        public static object GetVariableObject(IScenarioEventData data, string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
            {
                return null;
            }

            // 根据变量名直接获取接口属性，不使用反射
            object obj = null;
            switch (variableName)
            {
                case "ActionGovernor":
                    obj = data.ActionGovernor;
                    break;
                case "ActionCounsellor":
                    obj = data.ActionCounsellor;
                    break;
                case "TargetGovernor":
                    obj = data.TargetGovernor;
                    break;
                case "TargetCounsellor":
                    obj = data.TargetCounsellor;
                    break;
                case "ActionSkill":
                    obj = data.ActionSkill;
                    break;
                case "TargetSkill":
                    obj = data.TargetSkill;
                    break;
                case "ActionPerson":
                    obj = data.ActionPerson;
                    break;
                case "TargetPerson":
                    obj = data.TargetPerson;
                    break;
                case "ActionTroop":
                    obj = data.ActionTroop;
                    break;
                case "TargetTroop":
                    obj = data.TargetTroop;
                    break;
                case "ActionCell":
                    obj = data.ActionCell;
                    break;
                case "TargetCell":
                    obj = data.TargetCell;
                    break;
                case "ActionCity":
                    obj = data.ActionCity;
                    break;
                case "TargetCity":
                    obj = data.TargetCity;
                    break;
                case "ActionCorps":
                    obj = data.ActionCorps;
                    break;
                case "TargetCorps":
                    obj = data.TargetCorps;
                    break;
                case "ActionForce":
                    obj = data.ActionForce;
                    break;
                case "TargetForce":
                    obj = data.TargetForce;
                    break;
                case "ActionObject":
                    obj = data.ActionObject;
                    break;
                case "TargetObject":
                    obj = data.TargetObject;
                    break;
                default:
                    // 未知变量名，返回空字符串
                    return null;
            }

            return obj;
        }

        /// <summary>
        /// 获取游戏对象的Name属性值，通过类型判断直接获取，不使用反射
        /// </summary>
        /// <param name="obj">游戏对象</param>
        /// <returns>对象的Name值，获取失败返回空字符串</returns>
        private static string GetObjectName(object obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            // 通过类型检查直接获取Name属性
            if (obj is Person person)
            {
                return person.Name ?? string.Empty;
            }
            if (obj is SkillInstance skill)
            {
                return skill.Name ?? string.Empty;
            }
            if (obj is Troop troop)
            {
                return troop.Name ?? string.Empty;
            }
            if (obj is City city)
            {
                return city.Name ?? string.Empty;
            }
            if (obj is Corps corps)
            {
                return corps.Name ?? string.Empty;
            }
            if (obj is Force force)
            {
                return force.Name ?? string.Empty;
            }

            // 未知类型，返回空字符串
            return string.Empty;
        }

    }




    /// <summary>
    /// 旧事件管理器——**空壳，从未实现过任何功能**，且类名把 Scenario 拼成了 Scenerio。
    ///
    /// 完全由 <see cref="Sango.Core.Event.ScenarioEventManager"/> 取代，
    /// 后者是 [GameSystem]（由 GameSystemManager 反射创建并自动 Init）、
    /// 负责三源加载、触发判定、定时队列与播放调度。
    ///
    /// 保留只是为了避免其它 Mod 的代码引用时直接编译失败；Phase 6 删除。
    /// </summary>
    [System.Obsolete("拼写错误且从未实现的空壳，请使用 Sango.Core.Event.ScenarioEventManager")]
    public class ScenerioEventManager : Singleton<ScenerioEventManager>
    {
        public ScenerioEventManager() { }

        public Dictionary<int, ScenarioEvent> eventMap = new Dictionary<int, ScenarioEvent>();


        public void Init()
        {

        }

        public void Clear()
        {

        }
    }
}
