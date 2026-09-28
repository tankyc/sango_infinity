using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// GameEvent 静态事件的"开局前基线"：开局前拍一次订阅全景，
    /// 每次剧本收尾时把每个事件委托**整体替换回基线**。
    ///
    /// 为什么这样做：<see cref="GameEvent"/> 是静态委托字段、全局共享，剧本级订阅者一旦漏退订，
    /// 就会带着旧剧本的对象活到下一次开局（幽灵部队、事件播两遍、数值翻倍…）。
    /// 逐个类去抓漏成本极高且会反复出现，所以这里改成"统一还原"：
    ///   · 基线在**第一次开局前**拍下 —— 那正是"开局前的状态"，
    ///     包含 <c>Game.Init</c> 里订的应用级订阅（GameMedia / AudioManager / ModManager 等），
    ///     所以还原不会打掉它们；
    ///   · 收尾时按基线还原：基线里有的保留，基线里没有的一律移除；
    ///   · 还原是"整体替换调用列表"，因此**同一对象重复订阅也会自动回到 1 份**（天然去重）。
    ///
    /// 与 <see cref="ScenarioLifecycle"/> 的分工：
    ///   · ScenarioLifecycle 负责"按顺序叫各方退订"（正常路径，各系统自己在 OnDestroy 里对称退订）；
    ///   · 本类是**兜底**：兜住所有没退干净的订阅。两者叠加后，不必再逐个类加退订代码。
    ///
    /// 注意：基线只在第一次开局前拍一次，运行期**不再重拍** ——
    /// 否则会把当前剧本的对象一起钉在基线里，反而制造新的泄漏。
    /// </summary>
    public static class GameEventBaseline
    {
        /// <summary>纳入基线的静态事件总线（默认只有 GameEvent；将来新增总线在这里加一项即可）。</summary>
        static readonly Type[] hubTypes = new Type[] { typeof(GameEvent) };

        static readonly Delegate[] emptyDelegates = new Delegate[0];

        /// <summary>基线里的一项：事件字段 + 当时的调用列表（null 表示开局前该事件为空）。</summary>
        struct Entry
        {
            public FieldInfo field;
            public Delegate[] handlers;
        }

        static List<Entry> baseline;

        /// <summary>是否已拍过基线。</summary>
        public static bool HasBaseline { get { return baseline != null; } }

        /// <summary>开局前调用：只在第一次真正拍下基线，之后不再覆盖（运行期重拍会把当前剧本对象钉进基线）。</summary>
        public static void CaptureIfNeeded()
        {
            if (baseline != null)
                return;

            Capture();
        }

        /// <summary>强制重拍基线。调试用：确认"当前状态是干净的"之后再调用才有意义。</summary>
        public static void Capture()
        {
            List<Entry> list = new List<Entry>();
            int handlerCount = 0;

            for (int i = 0; i < hubTypes.Length; i++)
            {
                FieldInfo[] fields = hubTypes[i].GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                for (int j = 0; j < fields.Length; j++)
                {
                    FieldInfo field = fields[j];
                    if (!IsEventField(field))
                        continue;

                    Delegate handler = null;
                    try { handler = field.GetValue(null) as Delegate; }
                    catch { continue; }

                    Entry entry;
                    entry.field = field;
                    entry.handlers = handler == null ? null : handler.GetInvocationList();
                    list.Add(entry);

                    if (entry.handlers != null)
                        handlerCount += entry.handlers.Length;
                }
            }

            baseline = list;

            if (GameEventDiagnostics.LogEnabled)
                UnityEngine.Debug.Log(
                    $"[GameEvent基线] 已拍下开局前基线：{list.Count} 个事件字段 / {handlerCount} 个订阅者");
        }

        /// <summary>
        /// 按基线还原，返回被移除的"残留或重复"订阅个数（-1 表示还没拍过基线，未做任何事）。
        /// 正常情况下这个数字应该很小（说明各系统自己退干净了，兜底几乎没活干）。
        /// </summary>
        public static int Restore()
        {
            if (baseline == null)
                return -1;

            List<Delegate> removedHandlers = new List<Delegate>();
            List<Delegate> restoredHandlers = new List<Delegate>();
            StringBuilder detail = new StringBuilder();

            for (int i = 0; i < baseline.Count; i++)
            {
                Entry entry = baseline[i];

                Delegate current = null;
                try { current = entry.field.GetValue(null) as Delegate; }
                catch { continue; }

                Delegate[] currentList = current == null ? null : current.GetInvocationList();
                Delegate[] baseList = entry.handlers;

                if (SameHandlers(currentList, baseList))
                    continue;                       // 已经和开局前一致，不动它

                // 差集：当前有、基线没有 → 残留/重复订阅（会被移除）
                List<Delegate> lost = CollectDiff(currentList, baseList);
                // 差集：基线有、当前没有 → 被人用 = 覆盖或误退订（会被补回）
                List<Delegate> extra = CollectDiff(baseList, currentList);

                Delegate target = baseList == null ? null : Delegate.Combine(baseList);

                try { entry.field.SetValue(null, target); }
                catch { continue; }

                removedHandlers.AddRange(lost);
                restoredHandlers.AddRange(extra);

                if (lost.Count > 0 || extra.Count > 0)
                {
                    int before = currentList == null ? 0 : currentList.Length;
                    int after = baseList == null ? 0 : baseList.Length;
                    detail.Append("  ").Append(entry.field.Name)
                          .Append(": ").Append(before).Append(" → ").Append(after);
                    AppendHandlers(detail, "移除", lost);
                    AppendHandlers(detail, "补回", extra);
                    detail.AppendLine();
                }
            }

            if (GameEventDiagnostics.LogEnabled && (removedHandlers.Count > 0 || restoredHandlers.Count > 0))
                UnityEngine.Debug.LogWarning(
                    $"[GameEvent基线] 收尾兜底还原：移除 {removedHandlers.Count} 个残留/重复订阅，" +
                    $"补回 {restoredHandlers.Count} 个被覆盖的订阅\n{detail}");

            return removedHandlers.Count;
        }

        /// <summary>清掉基线（调试用：下次开局前会重新拍）。</summary>
        public static void Clear()
        {
            baseline = null;
        }

        static bool IsEventField(FieldInfo field)
        {
            // 只看事件委托字段：排除常量 / 只读字段（反射改不了）与其它静态字段
            if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) return false;
            if (field.IsInitOnly || field.IsLiteral) return false;
            return true;
        }

        /// <summary>两份调用列表是否等价（按"同一实例 + 同一方法"做多重集比较，与顺序无关）。</summary>
        static bool SameHandlers(Delegate[] a, Delegate[] b)
        {
            if (a == null) a = emptyDelegates;
            if (b == null) b = emptyDelegates;
            if (a.Length != b.Length) return false;

            bool[] used = new bool[b.Length];
            for (int i = 0; i < a.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Length; j++)
                {
                    if (used[j]) continue;
                    if (!IsSameHandler(a[i], b[j])) continue;
                    used[j] = true;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        /// <summary>收集 from 中"reference 里没有"的订阅者（多重集差集）。</summary>
        static List<Delegate> CollectDiff(Delegate[] from, Delegate[] reference)
        {
            List<Delegate> result = new List<Delegate>();
            if (from == null) return result;

            if (reference == null) reference = emptyDelegates;

            bool[] used = new bool[reference.Length];
            for (int i = 0; i < from.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < reference.Length; j++)
                {
                    if (used[j]) continue;
                    if (!IsSameHandler(from[i], reference[j])) continue;
                    used[j] = true;
                    found = true;
                    break;
                }
                if (!found) result.Add(from[i]);
            }
            return result;
        }

        static bool IsSameHandler(Delegate x, Delegate y)
        {
            if (x == null || y == null) return x == null && y == null;
            return ReferenceEquals(x.Target, y.Target) && x.Method == y.Method;
        }

        static void AppendHandlers(StringBuilder sb, string prefix, List<Delegate> handlers)
        {
            if (handlers == null || handlers.Count == 0) return;

            sb.Append(" [").Append(prefix).Append(": ");
            for (int i = 0; i < handlers.Count; i++)
            {
                object target = handlers[i].Target;
                sb.Append(target == null ? "<static>" : target.GetType().Name)
                  .Append('.').Append(handlers[i].Method.Name);
                if (i < handlers.Count - 1) sb.Append("; ");
            }
            sb.Append(']');
        }
    }
}
