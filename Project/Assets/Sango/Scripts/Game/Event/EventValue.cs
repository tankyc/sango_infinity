/*
 * 文件名：EventValue.cs
 * 描述：事件表达式求值器——把事件 JSON 里的数值 / 文本参数解析成实际值
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件表达式求值器。
    ///
    /// 支持三种参数写法（数值字段）：
    ///   · 字面量数字       100
    ///   · 剧本 / 事件变量  "#MyVar"（事件私有）、"@AttributeExpLevelNeed"（剧本变量）
    ///   · 槽位属性引用     "$ActionCity.gold"（取对象属性，按项目规则先大写后小写）
    ///   · 成长配置键       { "GainPlace": "EventMeritCommon" }  ← **成长数值必须用这种**
    ///
    /// 文本字段支持占位符：
    ///   · {:ActionForce}        槽位对象的显示名
    ///   · {:ActionCity.gold}    槽位对象的属性值
    ///   · {:var:MyVar}          事件私有变量
    ///   · {:svar:VarName}       剧本变量
    ///
    /// 【为什么成长数值必须走 GainPlace】
    /// 项目硬性约束：功绩 / 技巧点 / 武将经验 / 兵种适性经验 / 能力经验 / 忠诚等成长数值
    /// 一律通过 GainValueConfig 的键从 ScenarioVariables 读取，代码与数据里都不允许硬编码。
    /// 因此事件 JSON 里写的是"键名"，实际数值由剧本参数表决定，可在剧本设置界面统一调整。
    /// </summary>
    public static class EventValue
    {
        /// <summary>成员查找缓存：类型全名 | 字段名 → MemberInfo（null 表示已确认不存在，避免反复反射）</summary>
        static readonly Dictionary<string, MemberInfo> memberCache = new Dictionary<string, MemberInfo>();

        #region 数值求值

        /// <summary>
        /// 求一个数值参数。
        /// </summary>
        /// <param name="raw">原始 JSON 值（数字 / 字符串 / {GainPlace} 对象）</param>
        /// <param name="context">事件上下文（可为 null）</param>
        /// <param name="fallback">无法解析时的回落值</param>
        /// <returns>求值结果</returns>
        public static int EvalInt(JToken raw, EventContext context, int fallback = 0)
        {
            if (raw == null || raw.Type == JTokenType.Null)
                return fallback;

            // 1) 直接是数字
            if (raw.Type == JTokenType.Integer)
                return raw.Value<int>();
            if (raw.Type == JTokenType.Float)
                return (int)raw.Value<double>();
            if (raw.Type == JTokenType.Boolean)
                return raw.Value<bool>() ? 1 : 0;

            // 2) 对象形式：{ "GainPlace": "键名" } —— 成长数值的唯一合法写法
            if (raw.Type == JTokenType.Object)
            {
                JObject obj = raw as JObject;
                string gainKey = obj != null ? obj.Value<string>("GainPlace") : null;
                if (!string.IsNullOrEmpty(gainKey))
                    return EvalGainPlace(gainKey, fallback);

                // 也允许 { "Value": 12 } 这类显式包裹
                JToken inner = obj != null ? obj["Value"] : null;
                if (inner != null) return EvalInt(inner, context, fallback);

                Log.Warning($"事件数值参数无法识别（缺少 GainPlace / Value）：{raw}");
                return fallback;
            }

            // 3) 字符串形式
            if (raw.Type == JTokenType.String)
            {
                string text = raw.Value<string>();
                if (string.IsNullOrEmpty(text)) return fallback;

                // 剧本 / 事件变量
                if (text[0] == '#')
                    return context != null ? context.GetVar(text.Substring(1), fallback) : fallback;

                if (text[0] == '@')
                    return ToInt(GetObjectField(Scenario.Cur != null ? Scenario.Cur.Variables : null, text.Substring(1)), fallback);

                // 槽位属性引用：$SlotKey.Field
                if (text[0] == '$')
                {
                    int dot = text.IndexOf('.');
                    if (dot > 1)
                    {
                        string slotKey = text.Substring(1, dot - 1);
                        string field = text.Substring(dot + 1);
                        object target = context != null ? context.GetSlot(slotKey) : null;
                        return ToInt(GetObjectField(target, field), fallback);
                    }
                    // 只写了 $SlotKey：退化为取对象 Id
                    SangoObject only = context != null ? context.GetSlot(text.Substring(1)) : null;
                    return only != null ? only.Id : fallback;
                }

                // 带占位符的文本：先替换再解析
                if (text.Contains("{:"))
                    return ToInt(EvalText(text, context), fallback);

                // 纯数字字符串
                return int.TryParse(text, out int parsed) ? parsed : fallback;
            }

            return fallback;
        }

        /// <summary>
        /// 按成长配置键取值（GainValueConfig）。
        /// 区间语义的键会随机取一个值；其余按固定值 / 百分比语义直接读。
        /// </summary>
        /// <param name="gainKey">GainPlace 枚举名，如 "EventMeritCommon"</param>
        /// <param name="fallback">键名非法时的回落值</param>
        /// <returns>配置值</returns>
        public static int EvalGainPlace(string gainKey, int fallback = 0)
        {
            if (string.IsNullOrEmpty(gainKey)) return fallback;

            if (!Enum.TryParse(gainKey, out GainPlace place))
            {
                Log.Warning($"事件引用了不存在的成长配置键：{gainKey}（请检查 GainPlace 枚举）");
                return fallback;
            }

            if (GainValueConfig.KindOf(place) == GainValueKind.Range)
                return GainValueConfig.Roll(place);

            return GainValueConfig.Event(place);
        }

        /// <summary>
        /// 把任意对象转成 int（bool → 1/0；字符串尝试解析；失败回落到缺省值）。
        /// </summary>
        /// <param name="value">待转换的值</param>
        /// <param name="fallback">回落值</param>
        /// <returns>转换结果</returns>
        static int ToInt(object value, int fallback)
        {
            if (value == null) return fallback;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is float f) return (int)f;
            if (value is double d) return (int)d;
            if (value is bool b) return b ? 1 : 0;
            if (value is string s) return int.TryParse(s, out int parsed) ? parsed : fallback;
            return fallback;
        }

        #endregion

        #region 文本求值

        /// <summary>
        /// 替换文本里的占位符。
        /// 支持 {:SlotKey}、{:SlotKey.Attr}、{:var:Name}、{:svar:Name}。
        /// </summary>
        /// <param name="raw">原始文本</param>
        /// <param name="context">事件上下文（可为 null）</param>
        /// <returns>替换后的文本</returns>
        public static string EvalText(string raw, EventContext context)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            if (raw.IndexOf("{:", StringComparison.Ordinal) < 0) return raw;

            StringBuilder sb = new StringBuilder();
            int index = 0;
            int length = raw.Length;

            while (index < length)
            {
                int start = raw.IndexOf("{:", index, StringComparison.Ordinal);
                if (start < 0)
                {
                    sb.Append(raw, index, length - index);
                    break;
                }

                if (start > index)
                    sb.Append(raw, index, start - index);

                int end = raw.IndexOf('}', start + 2);
                if (end < 0)
                {
                    // 没有闭合的 }：把剩下的当普通文本，避免把整句吃掉
                    sb.Append(raw, start, length - start);
                    break;
                }

                string expression = raw.Substring(start + 2, end - start - 2);
                sb.Append(ResolvePlaceholder(expression, context));
                index = end + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 解析单个占位符表达式。
        /// </summary>
        /// <param name="expression">大括号内的内容，如 "ActionForce" / "ActionCity.gold" / "var:X"</param>
        /// <param name="context">事件上下文</param>
        /// <returns>替换文本；无法解析时返回空串</returns>
        static string ResolvePlaceholder(string expression, EventContext context)
        {
            if (string.IsNullOrEmpty(expression)) return string.Empty;

            // {:var:名字}——事件私有变量
            if (expression.StartsWith("var:", StringComparison.Ordinal))
            {
                string varName = expression.Substring(4);
                return context != null ? context.GetVar(varName).ToString() : "0";
            }

            // {:svar:名字}——剧本变量
            if (expression.StartsWith("svar:", StringComparison.Ordinal))
            {
                string varName = expression.Substring(5);
                object v = GetObjectField(Scenario.Cur != null ? Scenario.Cur.Variables : null, varName);
                return v != null ? v.ToString() : "0";
            }

            if (context == null) return string.Empty;

            // {:SlotKey.Attr}——槽位对象的属性
            int dot = expression.IndexOf('.');
            if (dot > 0)
            {
                string slotKey = expression.Substring(0, dot);
                string field = expression.Substring(dot + 1);
                object value = GetObjectField(context.GetSlot(slotKey), field);
                return value != null ? value.ToString() : string.Empty;
            }

            // {:SlotKey}——槽位对象的显示名
            return context.GetSlotName(expression);
        }

        #endregion

        #region 对象成员读取

        /// <summary>
        /// 取对象的显示名。找不到 Name 成员时回落到 Id。
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns>显示名；对象为空返回空串</returns>
        public static string GetObjectDisplayName(object obj)
        {
            if (obj == null) return string.Empty;

            object name = GetObjectField(obj, "Name");
            if (name != null)
            {
                string text = name.ToString();
                if (!string.IsNullOrEmpty(text)) return text;
            }

            if (obj is SangoObject sango) return $"#{sango.Id}";
            return string.Empty;
        }

        /// <summary>
        /// 读取对象的成员值。
        ///
        /// 遵循项目规则：**优先取大写字母开头的属性，不存在再取小写字母开头的属性**。
        /// 例如 "gold" 会先找 Gold、再找 gold；"Command" 会先找 Command、再找 command。
        /// 结果会被缓存（含"确认不存在"的负缓存），避免每帧反复反射。
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="name">成员名</param>
        /// <returns>成员值；找不到返回 null</returns>
        public static object GetObjectField(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return null;

            Type type = obj.GetType();
            string cacheKey = type.FullName + "|" + name;

            if (memberCache.TryGetValue(cacheKey, out MemberInfo cached))
                return ReadMember(obj, cached);

            MemberInfo member = FindMember(type, name);
            memberCache[cacheKey] = member;
            return ReadMember(obj, member);
        }

        /// <summary>按"先大写后小写"的顺序查找成员</summary>
        /// <param name="type">对象类型</param>
        /// <param name="name">成员名</param>
        /// <returns>成员信息；找不到返回 null</returns>
        static MemberInfo FindMember(Type type, string name)
        {
            if (type == null) return null;

            MemberInfo member = FindMemberExact(type, name);
            if (member != null) return member;

            // 回落：把首字母改成小写再找一次（对应规则里的"小写字母开头的属性"）
            string lowered = char.ToLowerInvariant(name[0]) + name.Substring(1);
            if (lowered != name)
            {
                member = FindMemberExact(type, lowered);
                if (member != null) return member;
            }

            return null;
        }

        /// <summary>按精确名字查找属性或字段</summary>
        /// <param name="type">对象类型</param>
        /// <param name="name">成员名</param>
        /// <returns>成员信息；找不到返回 null</returns>
        static MemberInfo FindMemberExact(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null) return property;

            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field;

            return null;
        }

        /// <summary>读取成员值（读取异常一律吞掉，返回 null）</summary>
        /// <param name="obj">对象</param>
        /// <param name="member">成员</param>
        /// <returns>成员值</returns>
        static object ReadMember(object obj, MemberInfo member)
        {
            if (obj == null || member == null) return null;

            try
            {
                if (member is PropertyInfo property)
                    return property.CanRead ? property.GetValue(obj) : null;
                if (member is FieldInfo field)
                    return field.GetValue(obj);
            }
            catch (Exception e)
            {
                Log.Warning($"事件读取成员失败：{member.Name} → {e.Message}");
            }

            return null;
        }

        #endregion
    }
}
