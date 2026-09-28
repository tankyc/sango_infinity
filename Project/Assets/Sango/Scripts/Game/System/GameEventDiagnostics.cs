using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// GameEvent 订阅数诊断（**只报告，不改行为**）。
    ///
    /// 用途：验证"读档复位"是否真的生效。做法是给 GameEvent 里每个静态事件委托拍一张
    /// "调用链长度"快照，并与**上一次同标签**的快照对比——同相位（例如两次都是 shutdown）
    /// 本应完全一致，出现差异就说明有订阅残留或重复订阅，日志里会直接列出事件名与前后数量。
    ///
    /// 为什么用反射而不是写死清单：事件有 140+ 个而且还在增加，写死的清单一定会漏。
    ///
    /// 注意：本文件必须留在 Assembly-CSharp 里（`Game/` 下）。放到 `Framework/` 下会
    /// 落进 Sango.Framework 独立程序集，那里看不到 GameEvent。
    /// </summary>
    public static class GameEventDiagnostics
    {
        static readonly Dictionary<string, Dictionary<string, int>> snapshots = new Dictionary<string, Dictionary<string, int>>();

        /// <summary>
        /// 是否输出诊断日志。
        ///
        /// 刻意**不走** Sango.Log：那套出口带 [Conditional("SANGO_DEBUG")]，而本工程当前
        /// 任何平台都没定义 SANGO_DEBUG（见 ProjectSettings → scriptingDefineSymbols），
        /// 走它等于什么都不打。这里直接用 UnityEngine.Debug，且只对"编辑器"生效，
        /// 正式玩家包保持安静。
        /// </summary>
        internal static bool LogEnabled
        {
            get { return UnityEngine.Application.isEditor; }
        }

        /// <summary>拍一张快照并与上一次同标签快照比较，有差异就写 Warning 日志。</summary>
        public static void Snapshot(string tag)
        {
            if (!LogEnabled)
                return;                 // 正式包不拍快照，省一次反射遍历

            Dictionary<string, int> current = Capture();

            Dictionary<string, int> previous;
            if (snapshots.TryGetValue(tag, out previous))
            {
                string diff = Diff(previous, current);
                if (!string.IsNullOrEmpty(diff))
                    UnityEngine.Debug.LogWarning(
                        $"[GameEvent诊断] 两次 {tag} 快照不一致（订阅残留/重复订阅）:\n{diff}{DescribeHandlers(current, previous)}");
                else
                    UnityEngine.Debug.Log($"[GameEvent诊断] {tag} 快照一致：{current.Count} 个事件有订阅，无残留");
            }
            else
            {
                UnityEngine.Debug.Log($"[GameEvent诊断] {tag} 首次快照：{current.Count} 个事件有订阅");
            }

            snapshots[tag] = current;
        }

        /// <summary>
        /// 对"订阅数变多"的事件，列出当前处理链的宿主类型与方法名（即：谁在订阅）。
        ///
        /// 只报数量往往定位不到具体泄漏对象（例如"OnCityHeadbarShowInfoChange: 6 → 13"只能猜是血条），
        /// 这里直接把订阅者的类型打出来：是 UICityHeadbar 还是别的类、有几个，一目了然。
        /// 只在有差异时调用，属于诊断专用路径。
        /// </summary>
        static string DescribeHandlers(Dictionary<string, int> current, Dictionary<string, int> previous)
        {
            if (previous == null) return string.Empty;

            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, int> pair in current)
            {
                int before;
                if (!previous.TryGetValue(pair.Key, out before))
                    before = 0;

                if (pair.Value <= before)
                    continue;               // 只看变多的

                FieldInfo field = typeof(GameEvent).GetField(pair.Key,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                    continue;

                Delegate handler = field.GetValue(null) as Delegate;
                if (handler == null)
                    continue;

                Delegate[] list = handler.GetInvocationList();
                sb.Append("  ").Append(pair.Key).Append(" 当前订阅者(").Append(list.Length).Append("): ");
                for (int i = 0; i < list.Length; i++)
                {
                    object target = list[i].Target;
                    sb.Append(target == null ? "<static>" : target.GetType().Name)
                      .Append('.').Append(list[i].Method.Name).Append("; ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>当前所有非空事件的订阅数（事件名 → 调用链长度）。</summary>
        public static Dictionary<string, int> Capture()
        {
            Dictionary<string, int> result = new Dictionary<string, int>();

            FieldInfo[] fields = typeof(GameEvent).GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                // 只统计事件委托字段，其它静态字段（缓存/常量等）跳过
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType))
                    continue;

                Delegate handler = field.GetValue(null) as Delegate;
                if (handler == null)
                    continue;

                Delegate[] list = handler.GetInvocationList();
                result[field.Name] = list == null ? 0 : list.Length;
            }

            return result;
        }

        /// <summary>对比两份快照，返回可读差异；无差异返回空串。</summary>
        public static string Diff(Dictionary<string, int> previous, Dictionary<string, int> current)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, int> pair in current)
            {
                int before;
                if (!previous.TryGetValue(pair.Key, out before))
                    before = 0;

                if (before != pair.Value)
                    sb.AppendLine($"  {pair.Key}: {before} → {pair.Value}");
            }

            foreach (KeyValuePair<string, int> pair in previous)
            {
                if (!current.ContainsKey(pair.Key))
                    sb.AppendLine($"  {pair.Key}: {pair.Value} → 0");
            }

            return sb.ToString();
        }

        /// <summary>清空历史快照（调试用）</summary>
        public static void Clear()
        {
            snapshots.Clear();
        }
    }
}
