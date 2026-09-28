/*
 * 文件名：EventConditionDatabase.cs
 * 描述：事件条件数据库——在 IConditionDatabase 的 Action/Target 双槽之外，额外支持任意命名的角色槽
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件条件数据库。
    ///
    /// 与既有的 <see cref="DefaultConditionDatabase"/> 的区别：
    ///   · Action / Target 双槽仍按 <see cref="IConditionDatabase"/> 的约定暴露，
    ///     因此**既有的全部 Condition（FactionCheck / DistanceCheck / PersonAttributeCheck …）
    ///     可以不加修改地在事件里直接使用**。
    ///   · 额外提供一个"任意命名槽位"的字典，承载 Action/Target 之外的槽
    ///     （如 Slot3、AllyForce、ActionCounsellor），供事件专用条件读取。
    ///
    /// 槽位 Key 的命名沿用项目既有的 IScenarioEventData 约定：
    /// ActionForce / TargetForce / ActionPerson / TargetPerson / ActionCity / TargetCity ……
    /// </summary>
    public class EventConditionDatabase : IConditionDatabase
    {
        /// <summary>槽位绑定表：槽位 Key → 游戏对象。Key 与 EventActorSlot.Key 一致</summary>
        public readonly Dictionary<string, SangoObject> slots = new Dictionary<string, SangoObject>();

        /// <summary>
        /// 本次是第几次触发（1-based）。
        /// 供 FireCountCompare 使用——三顾茅庐靠它分流"一顾 / 二顾 / 三顾"。
        /// 之所以放在数据库而不是让条件去拿 EventContext：
        /// Condition 的接口只收 IConditionDatabase，条件不应反向依赖事件运行时。
        /// </summary>
        public int fireCount = 1;

        /// <summary>当前事件 Id（供需要查触发史的条件使用）</summary>
        public int eventId;

        /// <summary>行动方所在格子（Cell 不是 SangoObject，单独存放）</summary>
        public Cell ActionCell { get; set; }

        /// <summary>目标方所在格子</summary>
        public Cell TargetCell { get; set; }

        /// <summary>行动方战法</summary>
        public SkillInstance ActionSkill { get; set; }

        /// <summary>目标方战法</summary>
        public SkillInstance TargetSkill { get; set; }

        /// <summary>
        /// 按槽位 Key 取对象。不存在时返回 null，调用方需自行判空。
        /// </summary>
        /// <param name="key">槽位 Key</param>
        /// <returns>槽位绑定的对象；不存在返回 null</returns>
        public SangoObject GetSlot(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return slots.TryGetValue(key, out SangoObject obj) ? obj : null;
        }

        /// <summary>
        /// 按槽位 Key 取指定类型的对象。类型不匹配时返回 null。
        /// </summary>
        /// <typeparam name="T">期望的对象类型</typeparam>
        /// <param name="key">槽位 Key</param>
        /// <returns>槽位绑定的对象；不存在或类型不符返回 null</returns>
        public T GetSlot<T>(string key) where T : SangoObject
        {
            return GetSlot(key) as T;
        }

        /// <summary>清空所有槽位绑定（复用实例时调用，避免上一事件的槽位泄漏）</summary>
        public void Clear()
        {
            slots.Clear();
            ActionCell = null;
            TargetCell = null;
            ActionSkill = null;
            TargetSkill = null;
            fireCount = 1;
            eventId = 0;
        }

        #region IConditionDatabase 实现

        /// <summary>行动方武将</summary>
        public Person ActionPerson { get { return GetSlot<Person>("ActionPerson"); } }

        /// <summary>目标方武将</summary>
        public Person TargetPerson { get { return GetSlot<Person>("TargetPerson"); } }

        /// <summary>行动方部队</summary>
        public Troop ActionTroop { get { return GetSlot<Troop>("ActionTroop"); } }

        /// <summary>目标方部队</summary>
        public Troop TargetTroop { get { return GetSlot<Troop>("TargetTroop"); } }

        /// <summary>行动方城市</summary>
        public City ActionCity { get { return GetSlot<City>("ActionCity"); } }

        /// <summary>目标方城市</summary>
        public City TargetCity { get { return GetSlot<City>("TargetCity"); } }

        /// <summary>行动方军团</summary>
        public Corps ActionCorps { get { return GetSlot<Corps>("ActionCorps"); } }

        /// <summary>目标方军团</summary>
        public Corps TargetCorps { get { return GetSlot<Corps>("TargetCorps"); } }

        /// <summary>行动方势力</summary>
        public Force ActionForce { get { return GetSlot<Force>("ActionForce"); } }

        /// <summary>目标方势力</summary>
        public Force TargetForce { get { return GetSlot<Force>("TargetForce"); } }

        /// <summary>行动方通用对象</summary>
        public object ActionObject { get { return GetSlot("ActionObject"); } }

        /// <summary>目标方通用对象</summary>
        public object TargetObject { get { return GetSlot("TargetObject"); } }

        #endregion
    }
}
