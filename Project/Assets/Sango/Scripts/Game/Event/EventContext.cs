/*
 * 文件名：EventContext.cs
 * 描述：事件运行时上下文——承载角色槽绑定、触发序号、私有变量与结局标记
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 一次事件播放的运行时上下文。
    ///
    /// 生命周期：由 ScenarioEventManager 在"条件通过、槽位绑定成功"后创建，
    /// 交给 EventRunner 驱动到结束，结束后回写 EventRunState。
    ///
    /// 【跨回合】定时待办项不会保存本对象（SangoObject 引用会失效），
    /// 只保存 <see cref="boundSlots"/>（类型 + Id），下一阶段重新解析后新建上下文。
    /// </summary>
    public class EventContext
    {
        /// <summary>事件定义</summary>
        public EventDefinition definition;

        /// <summary>条件数据库（同时是角色槽绑定的存放处，供条件与效果读取）</summary>
        public EventConditionDatabase database = new EventConditionDatabase();

        /// <summary>事件运行时状态（私有变量 / 旗标 / 触发次数），可为 null（编辑器试跑时）</summary>
        public EventRunState state;

        /// <summary>
        /// 本次是第几次触发（1-based）。
        /// 三顾茅庐就是靠它分流"一顾 / 二顾 / 三顾"三套不同演出。
        /// </summary>
        public int fireCount = 1;

        /// <summary>触发时机（日志与调试用）</summary>
        public EventTriggerKind fireKind = EventTriggerKind.ManualCall;

        /// <summary>本次 hook 注入的事件实体（如陷落的城池、被俘的武将），可为 null</summary>
        public SangoObject eventEntity;

        /// <summary>
        /// 跨回合继承用的槽位绑定（类型 + Id）。
        /// 由槽位解析阶段填充，Op: Schedule 直接取它登记定时待办。
        /// </summary>
        public List<BoundSlotRef> boundSlots = new List<BoundSlotRef>();

        /// <summary>玩家在最近一次选择肢里选的序号（-1 = 没选过）</summary>
        public int chosenOptionIndex = -1;

        /// <summary>本事件的结局标记（多结局事件用，写入 EventRunState.Outcome）</summary>
        public string outcome;

        /// <summary>是否已中止（槽位失效、异常兜底等），中止后不再执行后续指令</summary>
        public bool aborted;

        /// <summary>Slot 绑定失败时的中文原因（调试面板显示）</summary>
        public string slotFailReason;

        #region 槽位访问

        /// <summary>
        /// 按 Key 取槽位绑定的对象，不存在返回 null。
        /// </summary>
        /// <param name="key">槽位 Key</param>
        /// <returns>绑定的对象</returns>
        public SangoObject GetSlot(string key)
        {
            return database != null ? database.GetSlot(key) : null;
        }

        /// <summary>
        /// 按 Key 取指定类型的槽位对象，类型不符返回 null。
        /// </summary>
        /// <typeparam name="T">期望类型</typeparam>
        /// <param name="key">槽位 Key</param>
        /// <returns>绑定的对象</returns>
        public T GetSlot<T>(string key) where T : SangoObject
        {
            return database != null ? database.GetSlot<T>(key) : null;
        }

        /// <summary>
        /// 绑定一个对象到槽位（同时记录"类型 + Id"以便跨回合继承）。
        /// </summary>
        /// <param name="key">槽位 Key</param>
        /// <param name="type">对象类型</param>
        /// <param name="obj">对象</param>
        public void BindSlot(string key, EventSlotType type, SangoObject obj)
        {
            if (string.IsNullOrEmpty(key) || obj == null) return;

            if (database == null) database = new EventConditionDatabase();
            database.slots[key] = obj;

            // 记录跨回合继承信息；同 Key 重复绑定时覆盖
            if (boundSlots == null) boundSlots = new List<BoundSlotRef>();
            for (int i = 0; i < boundSlots.Count; i++)
            {
                if (boundSlots[i].Key == key)
                {
                    BoundSlotRef updated = boundSlots[i];
                    updated.Type = type;
                    updated.Id = obj.Id;
                    boundSlots[i] = updated;
                    return;
                }
            }

            boundSlots.Add(new BoundSlotRef { Key = key, Type = type, Id = obj.Id });
        }

        /// <summary>
        /// 取槽位对象的名字；对象为空或未绑定槽位时返回空串。
        /// 用于文本占位符替换。
        /// </summary>
        /// <param name="key">槽位 Key</param>
        /// <returns>对象名</returns>
        public string GetSlotName(string key)
        {
            return EventValue.GetObjectDisplayName(GetSlot(key));
        }

        #endregion

        #region 变量与结局

        /// <summary>读取事件私有变量（不存在时返回缺省值）</summary>
        /// <param name="key">变量名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>变量值</returns>
        public int GetVar(string key, int defaultValue = 0)
        {
            return state != null ? state.GetVar(key, defaultValue) : defaultValue;
        }

        /// <summary>写入事件私有变量</summary>
        /// <param name="key">变量名</param>
        /// <param name="value">变量值</param>
        public void SetVar(string key, int value)
        {
            if (state != null) state.SetVar(key, value);
        }

        /// <summary>读取全局旗标（跨事件共享）</summary>
        /// <param name="key">旗标名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>旗标值</returns>
        public bool GetGlobalFlag(string key, bool defaultValue = false)
        {
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            return manager != null ? manager.GetGlobalFlag(key, defaultValue) : defaultValue;
        }

        /// <summary>写入全局旗标（跨事件共享）</summary>
        /// <param name="key">旗标名</param>
        /// <param name="value">旗标值</param>
        public void SetGlobalFlag(string key, bool value)
        {
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (manager != null) manager.SetGlobalFlag(key, value);
        }

        /// <summary>标记本事件的结局（多结局事件用）</summary>
        /// <param name="outcomeName">结局名，如 "CaoWin"</param>
        public void SetOutcome(string outcomeName)
        {
            outcome = outcomeName;
            if (state != null) state.Outcome = outcomeName;
        }

        #endregion
    }
}
