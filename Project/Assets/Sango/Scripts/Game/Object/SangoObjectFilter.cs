using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sango.Core
{
    /// <summary>
    /// 过滤比较运算符
    /// </summary>
    public enum FilterCompareOp
    {
        /// <summary>包含(字符串)或等于(数值), 由:号触发</summary>
        Contains = 0,

        /// <summary>等于, 由=号触发</summary>
        Equal,

        /// <summary>不等于, 由!=触发</summary>
        NotEqual,

        /// <summary>大于, 由&gt;触发</summary>
        Greater,

        /// <summary>小于, 由&lt;触发</summary>
        Less,

        /// <summary>大于等于, 由&gt;=触发</summary>
        GreaterEqual,

        /// <summary>小于等于, 由&lt;=触发</summary>
        LessEqual,
    }

    /// <summary>
    /// 单条过滤条件 - 由"条件类型+分隔符/比较符+条件值"组成
    /// 示例: 类型:势力 / 统率&gt;70 / 姓名:曹 / ID=123
    /// </summary>
    public class SangoObjectFilterCondition
    {
        /// <summary>
        /// 条件类型(字段中文名或内置键)
        /// </summary>
        public string key;

        /// <summary>
        /// 比较运算符
        /// </summary>
        public FilterCompareOp op;

        /// <summary>
        /// 条件值(原始字符串)
        /// </summary>
        public string value;

        /// <summary>
        /// 对象类型中文别名 -> 类型名映射(用于"类型"条件)
        /// </summary>
        private static readonly Dictionary<string, string> TypeAliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "武将", "Person" },
            { "人物", "Person" },
            { "势力", "Force" },
            { "军团", "Corps" },
            { "城池", "City" },
            { "城市", "City" },
            { "都市", "City" },
            { "建筑", "Building" },
            { "部队", "Troop" },
        };

        /// <summary>
        /// 判断目标对象是否满足本条件
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="sortTitles">当前列表的排序标题(中文字段名的数据来源)</param>
        /// <returns>是否满足条件</returns>
        public bool Match(SangoObject obj, List<ObjectSortTitle> sortTitles)
        {
            if (obj == null)
            {
                return false;
            }

            // 1.内置键: 类型(对象类型过滤, 如 类型:势力)
            if (IsTypeKey(key))
            {
                return CompareString(GetTypeName(obj));
            }

            // 2.内置键: ID/编号
            if (IsIdKey(key))
            {
                return CompareValue(obj.Id);
            }

            // 3.内置键: 名称/姓名/名字
            if (IsNameKey(key))
            {
                return CompareString(obj.Name);
            }

            // 4.按中文字段名匹配排序标题列(如 统率/武力/所属势力 等)
            ObjectSortTitle title = FindSortTitle(sortTitles, key);
            if (title != null)
            {
                object titleValue = title.GetValue(obj);
                // 值不存在时退化为显示字符串比较
                if (titleValue == null)
                {
                    return CompareString(title.GetValueStr(obj));
                }
                return CompareValue(titleValue);
            }

            // 5.反射兜底: 按成员名(字段/属性, 忽略大小写)获取值
            if (TryGetMemberValue(obj, key, out object memberValue))
            {
                return CompareValue(memberValue);
            }

            // 条件类型无法识别时不命中
            return false;
        }

        /// <summary>
        /// 是否为类型条件键
        /// </summary>
        private static bool IsTypeKey(string key)
        {
            return string.Equals(key, "类型", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "t", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "type", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否为ID条件键
        /// </summary>
        private static bool IsIdKey(string key)
        {
            return string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "编号", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 是否为名称条件键
        /// </summary>
        private static bool IsNameKey(string key)
        {
            return string.Equals(key, "名称", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "姓名", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "名字", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "name", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取对象类型名(Person/Force/Corps/City等)
        /// </summary>
        private static string GetTypeName(SangoObject obj)
        {
            return obj.GetType().Name;
        }

        /// <summary>
        /// 按中文字段名在排序标题列表中查找对应列(忽略大小写)
        /// </summary>
        private static ObjectSortTitle FindSortTitle(List<ObjectSortTitle> sortTitles, string titleName)
        {
            if (sortTitles == null || string.IsNullOrEmpty(titleName))
            {
                return null;
            }
            for (int i = 0; i < sortTitles.Count; i++)
            {
                ObjectSortTitle title = sortTitles[i];
                if (title != null && string.Equals(title.name, titleName, StringComparison.OrdinalIgnoreCase))
                {
                    return title;
                }
            }
            return null;
        }

        /// <summary>
        /// 通过反射按成员名获取对象属性值(字段优先, 其次属性, 忽略大小写)
        /// </summary>
        private static bool TryGetMemberValue(SangoObject obj, string memberName, out object result)
        {
            result = null;
            if (string.IsNullOrEmpty(memberName))
            {
                return false;
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            Type type = obj.GetType();
            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                result = field.GetValue(obj);
                return true;
            }
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanRead)
            {
                result = property.GetValue(obj, null);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 比较目标值与条件值(数值优先, 其次字符串)
        /// </summary>
        private bool CompareValue(object target)
        {
            if (target == null)
            {
                return false;
            }

            // 数值比较: 两边均可转为数值时按数值语义比较
            if (TryToDouble(target, out double targetNumber) && TryToDouble(value, out double conditionNumber))
            {
                switch (op)
                {
                    case FilterCompareOp.Contains:
                    case FilterCompareOp.Equal:
                        return targetNumber.Equals(conditionNumber);
                    case FilterCompareOp.NotEqual:
                        return !targetNumber.Equals(conditionNumber);
                    case FilterCompareOp.Greater:
                        return targetNumber > conditionNumber;
                    case FilterCompareOp.Less:
                        return targetNumber < conditionNumber;
                    case FilterCompareOp.GreaterEqual:
                        return targetNumber >= conditionNumber;
                    case FilterCompareOp.LessEqual:
                        return targetNumber <= conditionNumber;
                }
                return false;
            }

            // 字符串比较
            string targetStr = target as string;
            if (targetStr == null)
            {
                // SangoObject引用类型用名称参与字符串比较
                if (target is SangoObject sangoObj)
                {
                    targetStr = sangoObj.Name;
                }
                else
                {
                    targetStr = target.ToString();
                }
            }
            return CompareString(targetStr);
        }

        /// <summary>
        /// 字符串比较(:为包含, =为相等, !=为不包含, 其余按字典序)
        /// </summary>
        private bool CompareString(string targetStr)
        {
            if (targetStr == null)
            {
                return false;
            }
            switch (op)
            {
                case FilterCompareOp.Contains:
                    return targetStr.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                        || MatchTypeAlias(targetStr);
                case FilterCompareOp.Equal:
                    return string.Equals(targetStr, value, StringComparison.OrdinalIgnoreCase)
                        || MatchTypeAlias(targetStr);
                case FilterCompareOp.NotEqual:
                    return targetStr.IndexOf(value, StringComparison.OrdinalIgnoreCase) < 0
                        && !MatchTypeAlias(targetStr);
                case FilterCompareOp.Greater:
                    return string.CompareOrdinal(targetStr, value) > 0;
                case FilterCompareOp.Less:
                    return string.CompareOrdinal(targetStr, value) < 0;
                case FilterCompareOp.GreaterEqual:
                    return string.CompareOrdinal(targetStr, value) >= 0;
                case FilterCompareOp.LessEqual:
                    return string.CompareOrdinal(targetStr, value) <= 0;
            }
            return false;
        }

        /// <summary>
        /// 类型中文别名匹配(仅用于类型条件, 如 类型:势力)
        /// </summary>
        private bool MatchTypeAlias(string typeName)
        {
            if (!IsTypeKey(key))
            {
                return false;
            }
            if (TypeAliasMap.TryGetValue(value, out string aliasName))
            {
                return string.Equals(typeName, aliasName, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// 尝试将对象转换为数值(支持数字/布尔/数字字符串/SangoObject的Id)
        /// </summary>
        private static bool TryToDouble(object target, out double number)
        {
            number = 0;
            if (target == null)
            {
                return false;
            }
            if (target is bool boolValue)
            {
                number = boolValue ? 1 : 0;
                return true;
            }
            if (target is SangoObject sangoObj)
            {
                number = sangoObj.Id;
                return true;
            }
            if (target is IConvertible convertible && !(target is string))
            {
                try
                {
                    number = convertible.ToDouble(null);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            if (target is string str)
            {
                return double.TryParse(str, out number);
            }
            return false;
        }
    }

    /// <summary>
    /// 对象过滤器 - 解析过滤文本并判断对象是否满足全部条件(条件之间为与关系)
    /// 语法: 过滤条目以空格分隔, 条件类型与条件值以:分隔;
    /// 支持比较符 &gt; &lt; &gt;= &lt;= != =, 出现比较符时按比较语义判断;
    /// 不含量任何分隔符的条目按名称模糊匹配。
    /// 示例: 类型:势力 统率&gt;70 姓名:曹 性别:1 ID=123
    /// </summary>
    public class SangoObjectFilter
    {
        /// <summary>
        /// 过滤条件列表
        /// </summary>
        public List<SangoObjectFilterCondition> conditions = new List<SangoObjectFilterCondition>();

        /// <summary>
        /// 过滤器是否为空(空过滤器命中全部对象)
        /// </summary>
        public bool IsEmpty => conditions.Count == 0;

        /// <summary>
        /// 解析过滤文本为过滤器
        /// </summary>
        /// <param name="text">过滤文本</param>
        /// <returns>过滤器实例</returns>
        public static SangoObjectFilter Parse(string text)
        {
            SangoObjectFilter filter = new SangoObjectFilter();
            if (string.IsNullOrEmpty(text))
            {
                return filter;
            }

            // 条目以空格分隔(兼容全角空格与制表符)
            string[] entries = text.Split(new char[] { ' ', '\t', '　' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                SangoObjectFilterCondition condition = ParseEntry(entries[i]);
                if (condition != null)
                {
                    filter.conditions.Add(condition);
                }
            }
            return filter;
        }

        /// <summary>
        /// 解析单个过滤条目(条件类型+分隔符/比较符+条件值)
        /// </summary>
        /// <param name="entry">过滤条目</param>
        /// <returns>过滤条件(无法解析时返回null)</returns>
        private static SangoObjectFilterCondition ParseEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry))
            {
                return null;
            }

            // 按优先级查找分隔符(双字符比较符优先于单字符)
            string[] operators = new string[] { ">=", "<=", "!=", ":", "=", ">", "<" };
            FilterCompareOp[] ops = new FilterCompareOp[]
            {
                FilterCompareOp.GreaterEqual,
                FilterCompareOp.LessEqual,
                FilterCompareOp.NotEqual,
                FilterCompareOp.Contains,
                FilterCompareOp.Equal,
                FilterCompareOp.Greater,
                FilterCompareOp.Less,
            };

            for (int i = 0; i < operators.Length; i++)
            {
                int pos = entry.IndexOf(operators[i], StringComparison.Ordinal);
                if (pos > 0)
                {
                    string key = entry.Substring(0, pos).Trim();
                    string value = entry.Substring(pos + operators[i].Length).Trim();
                    if (key.Length == 0 || value.Length == 0)
                    {
                        return null;
                    }
                    return new SangoObjectFilterCondition
                    {
                        key = key,
                        op = ops[i],
                        value = value,
                    };
                }
            }

            // 不含分隔符的条目按名称模糊匹配
            return new SangoObjectFilterCondition
            {
                key = "名称",
                op = FilterCompareOp.Contains,
                value = entry,
            };
        }

        /// <summary>
        /// 判断对象是否满足全部过滤条件
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="sortTitles">当前列表的排序标题(中文字段名的数据来源)</param>
        /// <returns>是否命中</returns>
        public bool Match(SangoObject obj, List<ObjectSortTitle> sortTitles)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!conditions[i].Match(obj, sortTitles))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 过滤源列表, 将命中对象追加到目标列表(目标列表会先清空)
        /// </summary>
        /// <param name="source">源列表</param>
        /// <param name="dest">目标列表</param>
        /// <param name="sortTitles">当前列表的排序标题(中文字段名的数据来源)</param>
        public void Filter(List<SangoObject> source, List<SangoObject> dest, List<ObjectSortTitle> sortTitles)
        {
            dest.Clear();
            if (source == null)
            {
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                SangoObject obj = source[i];
                if (obj != null && Match(obj, sortTitles))
                {
                    dest.Add(obj);
                }
            }
        }
    }
}
