/*
 * 文件名：EventConditionEvaluator.cs
 * 描述：事件条件求值器——解析 JSON 条件树并求值，同时负责跨回合槽位的解析
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件条件求值器。
    ///
    /// 条件树的 JSON 形式：
    ///   1) 逻辑组合（由本类直接处理，不走 Condition 工厂）：
    ///      { "Type": "and", "Children": [ { ... }, { ... } ] }
    ///      { "Type": "or",  "Children": [ { ... }, { ... } ] }
    ///   2) 叶子条件（交给既有的 <see cref="Condition"/> 工厂）：
    ///      { "Type": "ForceAlive", "Params": { "Slot": "ActionForce" } }
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

        /// <summary>
        /// 求值一个条件树节点。
        /// </summary>
        /// <param name="node">条件树节点（JSON 对象）</param>
        /// <param name="database">条件数据库（承载角色槽绑定）</param>
        /// <param name="failReason">输出：未满足的中文原因</param>
        /// <returns>条件是否成立</returns>
        public static bool Evaluate(JObject node, EventConditionDatabase database, out string failReason)
        {
            failReason = null;

            if (node == null)
            {
                failReason = "条件为空";
                return true;
            }

            string type = node.Value<string>("Type");
            if (string.IsNullOrEmpty(type))
            {
                failReason = "条件缺少 Type 字段";
                return false;
            }

            // —— 逻辑组合：本类自己处理，以便控制 Children 这种更易读的写法 ——
            if (type == TypeAnd || type == TypeAndList)
                return EvaluateAll(node, database, true, out failReason);

            if (type == TypeOr || type == TypeOrList)
                return EvaluateAll(node, database, false, out failReason);

            // —— 叶子条件：交给既有的 Condition 工厂 ——
            Condition condition = Condition.Create(type);
            if (condition == null)
            {
                failReason = $"条件类型未注册：{type}";
                Log.Warning($"事件条件求值失败：{failReason}");
                return false;
            }

            try
            {
                JObject flattened = FlattenParams(node);
                condition.Init(flattened, CollectSlotObjects(database));
                bool ok = condition.Check(database);
                if (!ok)
                    failReason = DescribeLeaf(type, flattened);
                return ok;
            }
            catch (Exception e)
            {
                failReason = $"条件求值异常：{type} → {e.Message}";
                Log.Error($"事件条件求值异常：{failReason}");
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
        /// <returns>逻辑组合的结果</returns>
        static bool EvaluateAll(JObject node, EventConditionDatabase database, bool isAnd, out string failReason)
        {
            failReason = null;

            JArray children = node["Children"] as JArray;
            if (children == null || children.Count == 0)
            {
                failReason = isAnd ? "and 条件没有子条件" : "or 条件没有子条件";
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                JObject child = children[i] as JObject;
                bool ok = Evaluate(child, database, out string childReason);

                if (isAnd && !ok)
                {
                    // 与条件：任一不成立即整体不成立，直接短路
                    failReason = childReason;
                    return false;
                }

                if (!isAnd && ok)
                {
                    // 或条件：任一成立即整体成立
                    failReason = null;
                    return true;
                }
            }

            if (isAnd)
            {
                failReason = null;
                return true;
            }

            failReason = "or 条件的所有分支都不成立";
            return false;
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
            string slot = flattened != null ? flattened.Value<string>("Slot") : null;
            if (!string.IsNullOrEmpty(slot))
                return $"条件未满足：{type}（槽位 {slot}）";
            return $"条件未满足：{type}";
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
