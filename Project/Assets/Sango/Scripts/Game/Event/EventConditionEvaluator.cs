/*
 * 文件名：EventConditionEvaluator.cs
 * 描述：事件条件求值器——解析 JSON 条件树并求值，同时负责跨回合槽位的解析
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件条件求值器。
    ///
    /// 条件树的 JSON 形式：
    ///   1) 逻辑组合（由本类直接处理，不走 Condition 工厂）：
    ///      { "Type": "and", "Children": [ { ... }, { ... } ] }
    ///      { "Type": "or",  "Children": [ { ... }, { ... } ] }
    ///      { "Type": "not", "Children": [ { ... }, { ... } ] }
    ///   2) 叶子条件（交给既有的 <see cref="Condition"/> 工厂）：
    ///      { "Type": "ForceAlive", "Params": { "Slot": "ActionForce" } }
    ///
    /// 语义（与编辑器 EventEditor 的提示保持一致）：
    ///   · null / 空条件            → 恒成立；
    ///   · and / andList            → 全部子条件成立；
    ///   · or  / orList             → 任一子条件成立；
    ///   · not                      → 所有子条件都**不**成立才成立（即"一个都不能满足"，
    ///                                单子条件时就是常规的取反）；
    ///   · 空 and / 空 or / 空 not  → 不成立（编辑器会对空组报错提醒）。
    ///
    /// 关于 Params：为了让 JSON 可读（参数与元信息分开），写的时候用 Params 包一层；
    /// 求值前本类会把 Params 的内容**拍平到根节点**，这样条件实现里直接 p.Value&lt;int&gt;("Slot")
    /// 即可，不必层层剥。
    ///
    /// 【失败即安全】任何解析异常 / 条件类型未注册，一律返回 false 并给出中文原因，
    /// 绝不让异常冒泡打断回合流程。条件不成立 = 事件不发生，是安全的退化方向。
    /// </summary>
    public static class EventConditionEvaluator
    {
        /// <summary>逻辑与的 Type 名</summary>
        const string TypeAnd = "and";
        /// <summary>逻辑与列表的 Type 名</summary>
        const string TypeAndList = "andList";
        /// <summary>逻辑或的 Type 名</summary>
        const string TypeOr = "or";
        /// <summary>逻辑或列表的 Type 名</summary>
        const string TypeOrList = "orList";
        /// <summary>逻辑取反（全部不成立）的 Type 名</summary>
        const string TypeNot = "not";

        /// <summary>
        /// 求值一个条件树节点。
        /// </summary>
        /// <param name="node">条件树节点（JSON 对象）</param>
        /// <param name="database">条件数据库（承载角色槽绑定）</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <returns>条件是否成立</returns>
        public static bool Evaluate(JObject node, EventConditionDatabase database, out string failReason)
        {
            // 【热路径】走预编译缓存：条件树在整局里不变，只解析一次。
            // 原实现每次求值都要 FlattenParams(new JObject) + Condition.Create(反射)
            // + Init(重新解析参数) + JToken.Value(装箱)，200 个候选事件 × 多节点就是几十 MB GC。
            return EvaluateCompiled(node, database, out failReason);
        }

        /// <summary>
        /// 求值一个条件树节点，并把每个节点的判定结果写进 trace（供事件 DebugLog 打印）。
        ///
        /// 与无 trace 版本的区别：**关闭短路**。
        /// 正常求值遇到"且"的第一条不满足就返回，日志里只会看到一条原因；
        /// 带 trace 时会把所有子条件都跑一遍，于是能一次列全"到底还差几条、分别差在哪"。
        /// 最终结果与短路版本完全一致（且 = 全部成立，或 = 任一成立）。
        /// </summary>
        /// <param name="node">条件树节点</param>
        /// <param name="database">条件数据库</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <param name="trace">输出：逐条判定过程（null 表示不收集）</param>
        /// <returns>条件是否成立</returns>
        public static bool Evaluate(JObject node, EventConditionDatabase database, out string failReason, List<string> trace)
        {
            // 只有开了 DebugLog 的事件才走 trace 分支（慢路径：逐节点解析 + 收集判定过程）。
            // trace 为空是正常游戏流程，必须走预编译缓存。
            if (trace == null)
                return EvaluateCompiled(node, database, out failReason);

            return EvaluateTraced(node, database, out failReason, trace, 0);
        }

        #region 预编译条件树（热路径）

        /// <summary>预编译节点类型：叶子条件</summary>
        const int NodeLeaf = 0;
        /// <summary>预编译节点类型：且</summary>
        const int NodeAnd = 1;
        /// <summary>预编译节点类型：或</summary>
        const int NodeOr = 2;
        /// <summary>预编译节点类型：取反（全部不成立才成立）</summary>
        const int NodeNot = 3;

        /// <summary>
        /// 预编译后的条件树节点。
        ///
        /// 【为什么需要】原实现在每次求值时都要：<c>FlattenParams(new JObject)</c> 复制参数、
        /// <c>Condition.Create</c> 反射新建条件、<c>Init</c> 重新解析参数、<c>JToken.Value</c> 装箱。
        /// 而条件树在整局里是不变的 —— 200 个候选事件 × 多节点重复解析，就是 Profiler 里
        /// <c>EventConditionEvaluator.Evaluate</c> 的 50MB 量级 GC 与 3.6 秒耗时。
        /// </summary>
        sealed class CompiledNode
        {
            /// <summary>节点类型（NodeLeaf / NodeAnd / NodeOr / NodeNot）</summary>
            public int kind;
            /// <summary>逻辑节点的子节点</summary>
            public CompiledNode[] children;
            /// <summary>叶子：已完成 Init 的条件实例（可跨求值复用）</summary>
            public Condition condition;
            /// <summary>叶子：条件类型名（异常日志用）</summary>
            public string type;
            /// <summary>预生成的中文原因：叶子为"未满足"描述，逻辑节点为结构错误描述</summary>
            public string failText;
            /// <summary>编译失败 / 类型未注册 / 空组 → 恒不成立</summary>
            public bool invalid;
        }

        /// <summary>
        /// 条件树编译缓存（JObject → CompiledNode）。
        /// 【为什么用弱表】事件库重载（切剧本 / 重读 mod）会换一批 JObject，
        /// 旧编译结果随源对象一起被回收，不会长期驻留。
        /// </summary>
        static readonly ConditionalWeakTable<JObject, CompiledNode> compiledCache =
            new ConditionalWeakTable<JObject, CompiledNode>();

        /// <summary>
        /// 条件 <c>Init</c> 用的空对象数组。
        ///
        /// 【为什么可以传空】Game/Condition 下所有 Condition 实现的 <c>Init</c> 都只读 JSON 参数 p，
        /// 没有任何一处引用 <c>sangoObjects</c>（现场检索 0 命中）。
        /// **若将来有条件依赖槽位对象，必须把该类排除出缓存**，或改回每次重新 Init，
        /// 否则会一直用第一次编译时绑定的对象。
        /// </summary>
        static readonly SangoObject[] noSlotObjects = new SangoObject[0];

        /// <summary>取条件树的编译结果（未编译过则当场编译并写入缓存）</summary>
        /// <param name="node">条件树 JSON 节点</param>
        /// <returns>编译结果；node 为 null 时返回 null（按"条件为空"恒成立处理）</returns>
        static CompiledNode GetCompiled(JObject node)
        {
            if (node == null)
                return null;

            CompiledNode compiled;
            if (compiledCache.TryGetValue(node, out compiled))
                return compiled;

            compiled = Compile(node);
            compiledCache.Add(node, compiled);
            return compiled;
        }

        /// <summary>把条件树 JSON 编译成 <see cref="CompiledNode"/>（只在首次求值时执行一次）</summary>
        /// <param name="node">条件树节点</param>
        /// <returns>编译结果</returns>
        static CompiledNode Compile(JObject node)
        {
            CompiledNode result = new CompiledNode();
            result.kind = NodeLeaf;

            if (node == null)
            {
                // condition 为 null 且非 invalid → 按"条件为空恒成立"处理，与原实现一致
                result.failText = "条件为空";
                return result;
            }

            string type = node.Value<string>("Type");
            if (string.IsNullOrEmpty(type))
            {
                result.invalid = true;
                result.failText = "条件缺少 Type 字段";
                return result;
            }

            // 逻辑组合由本类自己编译，叶子交给 Condition 工厂
            if (type == TypeAnd || type == TypeAndList)
                return CompileGroup(node, NodeAnd, type);

            if (type == TypeOr || type == TypeOrList)
                return CompileGroup(node, NodeOr, type);

            if (type == TypeNot)
                return CompileGroup(node, NodeNot, type);

            Condition condition = Condition.Create(type);
            if (condition == null)
            {
                result.invalid = true;
                result.type = type;
                result.failText = $"条件类型未注册：{type}";
                Log.Warning($"事件条件编译失败：{result.failText}");
                return result;
            }

            JObject flattened = FlattenParams(node);
            try
            {
                condition.Init(flattened, noSlotObjects);
            }
            catch (Exception e)
            {
                result.invalid = true;
                result.type = type;
                result.failText = $"条件初始化异常：{type} → {e.Message}";
                Log.Error($"事件条件编译异常：{result.failText}");
                return result;
            }

            result.type = type;
            result.condition = condition;
            // 预生成"未满足"的中文原因，避免每次求值都走 DescribeLeaf 的字符串拼接
            result.failText = DescribeLeaf(type, flattened);
            return result;
        }

        /// <summary>编译逻辑组合节点（and / or / not）</summary>
        /// <param name="node">节点 JSON</param>
        /// <param name="kind">节点类型</param>
        /// <param name="type">原始 Type 文本（日志用）</param>
        /// <returns>编译结果</returns>
        static CompiledNode CompileGroup(JObject node, int kind, string type)
        {
            CompiledNode result = new CompiledNode();
            result.kind = kind;
            result.type = type;

            JArray childrenJson = node["Children"] as JArray;
            if (childrenJson == null || childrenJson.Count == 0)
            {
                // 空逻辑组：与语义不明，按"不成立"处理（与原实现一致，编辑器会对空组告警）
                result.invalid = true;
                result.failText = kind == NodeAnd ? "and 条件没有子条件"
                    : kind == NodeOr ? "or 条件没有子条件"
                    : "not 条件没有子条件";
                return result;
            }

            CompiledNode[] children = new CompiledNode[childrenJson.Count];
            for (int i = 0; i < childrenJson.Count; i++)
                children[i] = Compile(childrenJson[i] as JObject);

            result.children = children;
            return result;
        }

        /// <summary>
        /// 求值预编译后的条件树（零 JSON 解析、零条件对象分配）。
        /// 短路语义与原实现严格一致：and 遇 false 即停，or 遇 true 即停。
        /// </summary>
        /// <param name="compiled">编译后的节点</param>
        /// <param name="database">条件数据库</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <returns>条件是否成立</returns>
        static bool EvaluateCompiled(CompiledNode compiled, EventConditionDatabase database, out string failReason)
        {
            failReason = null;
            if (compiled == null)
                return true;   // 条件为空 = 恒成立

            if (compiled.invalid)
            {
                failReason = compiled.failText;
                return false;
            }

            switch (compiled.kind)
            {
                case NodeAnd:
                    for (int i = 0; i < compiled.children.Length; i++)
                    {
                        if (!EvaluateCompiled(compiled.children[i], database, out failReason))
                            return false;
                    }
                    return true;

                case NodeOr:
                    for (int i = 0; i < compiled.children.Length; i++)
                    {
                        if (EvaluateCompiled(compiled.children[i], database, out string _))
                        {
                            failReason = null;
                            return true;
                        }
                    }
                    failReason = "or 条件的所有分支都不成立";
                    return false;

                case NodeNot:
                    for (int i = 0; i < compiled.children.Length; i++)
                    {
                        if (EvaluateCompiled(compiled.children[i], database, out string _))
                        {
                            failReason = $"not 条件被满足：第 {i + 1} 个子条件成立了";
                            return false;
                        }
                    }
                    failReason = null;
                    return true;

                default:
                    // 叶子：condition == null 且非 invalid → 条件为空，恒成立
                    if (compiled.condition == null)
                        return true;

                    try
                    {
                        bool ok = compiled.condition.Check(database);
                        if (!ok)
                            failReason = compiled.failText;
                        return ok;
                    }
                    catch (Exception e)
                    {
                        failReason = $"条件求值异常：{compiled.type} → {e.Message}";
                        Log.Error($"事件条件求值异常：{failReason}");
                        return false;
                    }
            }
        }

        /// <summary>
        /// 求值入口（编译缓存版）。
        /// </summary>
        /// <param name="node">条件树 JSON</param>
        /// <param name="database">条件数据库</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <returns>条件是否成立</returns>
        static bool EvaluateCompiled(JObject node, EventConditionDatabase database, out string failReason)
        {
            return EvaluateCompiled(GetCompiled(node), database, out failReason);
        }

        #endregion

        /// <summary>
        /// 求值实现（带追踪与缩进深度）。**仅用于 DebugLog 的慢路径**，
        /// 正常流程请走 <see cref="EvaluateCompiled(JObject, EventConditionDatabase, out string)"/>。
        /// </summary>
        /// <param name="node">条件树节点</param>
        /// <param name="database">条件数据库</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <param name="trace">输出：逐条判定过程；null 表示不收集（此时保持原有的短路行为）</param>
        /// <param name="depth">缩进层级（0 = 根）</param>
        /// <returns>条件是否成立</returns>
        static bool EvaluateTraced(JObject node, EventConditionDatabase database, out string failReason,
            List<string> trace, int depth)
        {
            failReason = null;
            string indent = Indent(depth);

            if (node == null)
            {
                failReason = "条件为空";
                if (trace != null) trace.Add($"{indent}√ 条件为空，按恒成立处理");
                return true;
            }

            string type = node.Value<string>("Type");
            if (string.IsNullOrEmpty(type))
            {
                failReason = "条件缺少 Type 字段";
                if (trace != null) trace.Add($"{indent}× 条件缺少 Type 字段");
                return false;
            }

            // —— 逻辑组合：本类自己处理，以便控制 Children 这种更易读的写法 ——
            if (type == TypeAnd || type == TypeAndList)
                return EvaluateAll(node, database, true, out failReason, trace, depth);

            if (type == TypeOr || type == TypeOrList)
                return EvaluateAll(node, database, false, out failReason, trace, depth);

            if (type == TypeNot)
                return EvaluateNot(node, database, out failReason, trace, depth);

            // —— 叶子条件：交给既有的 Condition 工厂 ——
            Condition condition = Condition.Create(type);
            if (condition == null)
            {
                failReason = $"条件类型未注册：{type}";
                Log.Warning($"事件条件求值失败：{failReason}");
                if (trace != null) trace.Add($"{indent}× {failReason}");
                return false;
            }

            try
            {
                JObject flattened = FlattenParams(node);
                condition.Init(flattened, CollectSlotObjects(database));
                bool ok = condition.Check(database);
                if (!ok)
                    failReason = DescribeLeaf(type, flattened);
                if (trace != null)
                    trace.Add($"{indent}{(ok ? "√" : "×")} {DescribeLeaf(type, flattened, ok)}");
                return ok;
            }
            catch (Exception e)
            {
                failReason = $"条件求值异常：{type} → {e.Message}";
                Log.Error($"事件条件求值异常：{failReason}");
                if (trace != null) trace.Add($"{indent}× {failReason}");
                return false;
            }
        }

        /// <summary>
        /// 求值一段序列化的条件树 JSON。
        /// </summary>
        /// <param name="conditionJson">条件树 JSON 文本</param>
        /// <param name="database">条件数据库</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <returns>条件是否成立</returns>
        public static bool Evaluate(string conditionJson, EventConditionDatabase database, out string failReason)
        {
            failReason = null;

            if (string.IsNullOrEmpty(conditionJson))
                return true;

            JObject node;
            try
            {
                node = JObject.Parse(conditionJson);
            }
            catch (Exception e)
            {
                failReason = $"条件 JSON 解析失败：{e.Message}";
                Log.Error($"事件条件解析失败：{failReason}");
                return false;
            }

            return Evaluate(node, database, out failReason);
        }

        /// <summary>
        /// 求值逻辑组合节点。短路求值：与条件遇 false 即停，或条件遇 true 即停。
        /// </summary>
        /// <param name="node">逻辑节点</param>
        /// <param name="database">条件数据库</param>
        /// <param name="isAnd">true = 与；false = 或</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <param name="trace">输出：逐条判定过程；null 表示不收集（保持原有短路行为）</param>
        /// <param name="depth">缩进层级</param>
        /// <returns>逻辑组合的结果</returns>
        static bool EvaluateAll(JObject node, EventConditionDatabase database, bool isAnd, out string failReason,
            List<string> trace, int depth)
        {
            failReason = null;
            string indent = Indent(depth);
            string kindText = isAnd ? "且" : "或";

            JArray children = node["Children"] as JArray;
            if (children == null || children.Count == 0)
            {
                failReason = isAnd ? "and 条件没有子条件" : "or 条件没有子条件";
                if (trace != null) trace.Add($"{indent}× {kindText} 组没有子条件，判为不成立");
                return false;
            }

            int total = children.Count;
            int okCount = 0;
            // 只有收集 trace 时才记录所有失败原因（不短路）；否则保持"第一条失败即返回"
            List<string> fails = trace != null ? new List<string>() : null;

            for (int i = 0; i < children.Count; i++)
            {
                JObject child = children[i] as JObject;
                bool ok = EvaluateTraced(child, database, out string childReason, trace, depth + 1);

                if (ok) okCount++;

                if (isAnd && !ok)
                {
                    if (fails == null)
                    {
                        // 与条件：任一不成立即整体不成立，直接短路
                        failReason = childReason;
                        return false;
                    }
                    fails.Add(childReason);
                }

                if (!isAnd && ok && fails == null)
                {
                    // 或条件：任一成立即整体成立（仅在非 trace 模式下短路）
                    failReason = null;
                    return true;
                }
            }

            if (isAnd)
            {
                if (okCount == total)
                {
                    failReason = null;
                    if (trace != null) trace.Add($"{indent}√ {kindText}：{total} 条全部成立");
                    return true;
                }

                // 把全部不满足项拼成一句，方便一眼看出"还差哪几条"
                failReason = fails.Count == 1
                    ? fails[0]
                    : $"{fails.Count} 条条件未满足：" + string.Join("；", fails);
                if (trace != null) trace.Add($"{indent}× {kindText}：{total} 条中有 {total - okCount} 条不成立");
                return false;
            }

            if (okCount > 0)
            {
                failReason = null;
                if (trace != null) trace.Add($"{indent}√ {kindText}：{total} 条中 {okCount} 条成立");
                return true;
            }

            failReason = "or 条件的所有分支都不成立";
            if (trace != null) trace.Add($"{indent}× {kindText}：{total} 条全部不成立");
            return false;
        }

        /// <summary>
        /// 求值逻辑取反（not）节点：所有子条件都**不**成立时才成立。
        ///
        /// 为什么是"全部不成立"而不是"整体取反"：
        /// 编辑器里 not 组的文案是「以下条件全部不成立」，这里与之一致；
        /// 只有一个子条件时，两者完全等价（即常见的取反用法）。
        /// </summary>
        /// <param name="node">not 节点</param>
        /// <param name="database">条件数据库</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <param name="trace">输出：逐条判定过程；null 表示不收集（保持原有短路行为）</param>
        /// <param name="depth">缩进层级</param>
        /// <returns>取反结果</returns>
        static bool EvaluateNot(JObject node, EventConditionDatabase database, out string failReason,
            List<string> trace, int depth)
        {
            failReason = null;
            string indent = Indent(depth);

            JArray children = node["Children"] as JArray;
            if (children == null || children.Count == 0)
            {
                // 空取反组没有可判定的对象，按"失败即安全"返回不成立，并在编辑器里报错提醒
                failReason = "not 条件没有子条件";
                if (trace != null) trace.Add($"{indent}× 取反组没有子条件，判为不成立");
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                JObject child = children[i] as JObject;
                bool ok = EvaluateTraced(child, database, out string _, trace, depth + 1);

                // 只要有一个子条件成立，取反就不成立（短路）
                if (ok)
                {
                    failReason = $"not 条件被满足：第 {i + 1} 个子条件成立了";
                    if (trace != null) trace.Add($"{indent}× 取反失败：第 {i + 1} 个子条件成立");
                    return false;
                }
            }

            // 全部子条件都不成立 → 取反成立
            failReason = null;
            if (trace != null) trace.Add($"{indent}√ 取反成立：{children.Count} 条子条件全部不成立");
            return true;
        }

        /// <summary>
        /// 把 Params 子对象的内容拍平到根节点，便于条件实现直接读取。
        /// 根节点已有的同名键以根节点为准（不覆盖），避免 Params 意外改写 Type 等元信息。
        /// </summary>
        /// <param name="node">原始条件节点</param>
        /// <returns>拍平后的新节点</returns>
        static JObject FlattenParams(JObject node)
        {
            JObject result = new JObject();
            foreach (KeyValuePair<string, JToken> kv in node)
            {
                if (kv.Key == "Params") continue;
                result[kv.Key] = kv.Value;
            }

            if (node["Params"] is JObject prms)
            {
                foreach (KeyValuePair<string, JToken> kv in prms)
                {
                    if (result[kv.Key] == null)
                        result[kv.Key] = kv.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// 把槽位表收集成 SangoObject 数组，供 <see cref="Condition.Init"/> 使用。
        /// 顺序固定，保证同一条 JSON 每次求值的对象顺序一致。
        /// </summary>
        /// <param name="database">条件数据库</param>
        /// <returns>槽位对象数组</returns>
        static SangoObject[] CollectSlotObjects(EventConditionDatabase database)
        {
            if (database == null) return new SangoObject[0];

            const int count = 20;
            SangoObject[] objects = new SangoObject[count];
            objects[0] = database.GetSlot("ActionGovernor");
            objects[1] = database.GetSlot("ActionCounsellor");
            objects[2] = database.GetSlot("TargetGovernor");
            objects[3] = database.GetSlot("TargetCounsellor");
            objects[4] = database.GetSlot("ActionPerson");
            objects[5] = database.GetSlot("TargetPerson");
            objects[6] = database.GetSlot("ActionTroop");
            objects[7] = database.GetSlot("TargetTroop");
            objects[8] = database.GetSlot("ActionCity");
            objects[9] = database.GetSlot("TargetCity");
            objects[10] = database.GetSlot("ActionCorps");
            objects[11] = database.GetSlot("TargetCorps");
            objects[12] = database.GetSlot("ActionForce");
            objects[13] = database.GetSlot("TargetForce");
            objects[14] = database.GetSlot("ActionObject");
            objects[15] = database.GetSlot("TargetObject");
            return objects;
        }

        /// <summary>
        /// 生成叶子条件未满足时的中文描述，用于调试面板与日志。
        /// </summary>
        /// <param name="type">条件类型名</param>
        /// <param name="flattened">拍平后的条件节点</param>
        /// <returns>中文描述</returns>
        static string DescribeLeaf(string type, JObject flattened)
        {
            return DescribeLeaf(type, flattened, false);
        }

        /// <summary>
        /// 生成叶子条件的中文描述（可指定成立与否，供 DebugLog 的逐条追踪使用）。
        /// </summary>
        /// <param name="type">条件类型名</param>
        /// <param name="flattened">拍平后的条件节点</param>
        /// <param name="ok">条件是否成立</param>
        /// <returns>中文描述</returns>
        static string DescribeLeaf(string type, JObject flattened, bool ok)
        {
            string slot = flattened != null ? flattened.Value<string>("Slot") : null;
            string head = ok ? "条件成立" : "条件未满足";
            return !string.IsNullOrEmpty(slot) ? $"{head}：{type}（槽位 {slot}）" : $"{head}：{type}";
        }

        /// <summary>日志缩进：每层两个空格</summary>
        /// <param name="depth">层级</param>
        /// <returns>缩进字符串</returns>
        static string Indent(int depth)
        {
            return depth <= 0 ? string.Empty : new string(' ', depth * 2);
        }

        #region 槽位解析（跨回合用）

        /// <summary>
        /// 按"类型 + Id"把跨回合保存的槽位绑定还原成对象。
        ///
        /// 【失效处理】等待期间对象可能已消失（武将死亡、部队解散、城易主被删），
        /// 此时该槽位空缺，由调用方按槽位的 Optional 策略决定是放弃事件还是置空。
        /// </summary>
        /// <param name="boundSlots">跨回合保存的槽位绑定</param>
        /// <returns>还原后的条件数据库</returns>
        public static EventConditionDatabase ResolveSlots(List<BoundSlotRef> boundSlots)
        {
            EventConditionDatabase database = new EventConditionDatabase();
            Scenario scenario = Scenario.Cur;
            if (scenario == null || boundSlots == null) return database;

            for (int i = 0; i < boundSlots.Count; i++)
            {
                BoundSlotRef slotRef = boundSlots[i];
                if (string.IsNullOrEmpty(slotRef.Key)) continue;

                SangoObject obj = ResolveObject(scenario, slotRef.Type, slotRef.Id);
                if (obj == null)
                {
                    Log.Warning($"定时事件的槽位 {slotRef.Key} 已失效（{slotRef.Type} Id={slotRef.Id}），本槽位置空");
                    continue;
                }

                database.slots[slotRef.Key] = obj;
            }

            return database;
        }

        /// <summary>
        /// 从剧本的对应集合里按 Id 取对象。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="type">对象类型</param>
        /// <param name="id">对象 Id</param>
        /// <returns>对象；找不到返回 null</returns>
        public static SangoObject ResolveObject(Scenario scenario, EventSlotType type, int id)
        {
            if (scenario == null) return null;

            switch (type)
            {
                case EventSlotType.Force:
                    return scenario.forceSet != null ? scenario.forceSet.Get(id) : null;
                case EventSlotType.City:
                    return scenario.citySet != null ? scenario.citySet.Get(id) : null;
                case EventSlotType.Person:
                    return scenario.personSet != null ? scenario.personSet.Get(id) : null;
                case EventSlotType.Corps:
                    return scenario.corpsSet != null ? scenario.corpsSet.Get(id) : null;
                case EventSlotType.Troop:
                    return scenario.troopsSet != null ? scenario.troopsSet.Get(id) : null;
                case EventSlotType.Building:
                    return scenario.buildingSet != null ? scenario.buildingSet.Get(id) : null;
                default:
                    // Skill / Cell 不是 SangoObject 集合成员，暂不支持跨回合继承
                    return null;
            }
        }

        /// <summary>
        /// 求值定时待办项的取消条件。
        /// 槽位从待办项保存的绑定还原；取消条件为空视为"不取消"。
        /// </summary>
        /// <param name="item">定时待办项</param>
        /// <param name="reason">输出：取消原因（未取消时为 null）</param>
        /// <returns>是否应取消</returns>
        public static bool IsCancelConditionMet(ScheduledEventItem item, out string reason)
        {
            reason = null;
            if (item == null || string.IsNullOrEmpty(item.CancelConditionJson))
                return false;

            EventConditionDatabase database = ResolveSlots(item.BoundSlots);
            bool met = Evaluate(item.CancelConditionJson, database, out string failReason);
            if (met)
                reason = string.IsNullOrEmpty(failReason) ? "取消条件成立" : failReason;
            return met;
        }

        #endregion
    }
}
