/*
 * 文件名：Scenario.cs
 * 描述：剧本剧情事件类
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Render;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 剧情数据获取接口类
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

    public enum ScenarioEventType
    {
        Text,
        PersonTalk,
        PersonTalkChoice,
    }

    /// <summary>
    /// 剧情逻辑基类
    /// </summary>
    public class ScenarioEventBase : RenderEventBase
    {
        public IScenarioEventData scenarioEventData;

    }

    /// <summary>
    /// 剧本类，管理游戏剧本的所有数据
    /// 包含势力、武将、城市、部队、建筑等游戏对象
    /// 负责剧本的加载、保存、运行等核心功能
    /// </summary>
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
