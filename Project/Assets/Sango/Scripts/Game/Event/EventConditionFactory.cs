/*
 * 文件名：EventConditionFactory.cs
 * 描述：事件条件注册中心 + 条件读取槽位的统一入口
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件条件注册中心。
    ///
    /// 【为什么不另建一张表】事件条件直接注册进既有的 <see cref="Condition.CreateMap"/>：
    ///   · 已有的 22 个条件（FactionCheck / DistanceCheck / TerrainCheck …）无需改写即可被事件复用；
    ///   · <see cref="EventConditionEvaluator"/> 只需一套解析逻辑；
    ///   · 编辑器侧的下拉列表也只需反射一张表。
    ///
    /// 注册时机：<see cref="ScenarioEventManager.Init"/>（在 GameSystemManager.Init 阶段调用），
    /// 而 Game.Init 里 Condition.Init() 更早（第 71 行 vs 第 83 行），
    /// 因此核心条件先注册、事件条件后追加，二者不会互相覆盖。
    /// </summary>
    public static class EventConditionFactory
    {
        /// <summary>是否已完成注册（防止 Game.Init 多次调用造成重复注册）</summary>
        static bool inited;

        /// <summary>
        /// 注册全部事件专用条件。
        /// </summary>
        public static void InitAll()
        {
            if (inited) return;
            inited = true;

            // —— 事件流程类（依赖事件自身状态，不依赖世界）——
            Condition.Register("FireCountCompare", Condition.CraeteHandle<FireCountCompare>);
            Condition.Register("EventFired", Condition.CraeteHandle<EventFiredCheck>);
            Condition.Register("EventFiredCountCompare", Condition.CraeteHandle<EventFiredCountCompare>);
            Condition.Register("EventOutcomeCheck", Condition.CraeteHandle<EventOutcomeCheck>);
            Condition.Register("FlagCheck", Condition.CraeteHandle<FlagCheck>);
            Condition.Register("VarCompare", Condition.CraeteHandle<VarCompare>);
            Condition.Register("ScenarioVarCompare", Condition.CraeteHandle<ScenarioVarCompare>);
            Condition.Register("RandomChance", Condition.CraeteHandle<EventRandomChanceCheck>);

            // —— 时间类 ——
            Condition.Register("TimeRange", Condition.CraeteHandle<TimeRangeCheck>);
            Condition.Register("DateCheck", Condition.CraeteHandle<EventDateCheck>);
            Condition.Register("ScenarioYearCheck", Condition.CraeteHandle<ScenarioYearCheck>);

            // —— 势力类 ——
            Condition.Register("ForceAlive", Condition.CraeteHandle<ForceAliveCheck>);
            Condition.Register("ForceCityCount", Condition.CraeteHandle<ForceCityCountCheck>);
            Condition.Register("ForceHasCity", Condition.CraeteHandle<ForceHasCityCheck>);
            Condition.Register("ForceHasPerson", Condition.CraeteHandle<ForceHasPersonCheck>);
            Condition.Register("PlayerForceCheck", Condition.CraeteHandle<PlayerForceCheck>);
            Condition.Register("DifficultyCheck", Condition.CraeteHandle<EventDifficultyCheck>);

            // —— 武将类 ——
            Condition.Register("PersonStateCheck", Condition.CraeteHandle<PersonStateCheck>);
            Condition.Register("PersonBelongCheck", Condition.CraeteHandle<PersonBelongCheck>);
            Condition.Register("PersonLoyaltyCheck", Condition.CraeteHandle<EventPersonLoyaltyCheck>);
            Condition.Register("PersonVisible", Condition.CraeteHandle<PersonVisibleCheck>);
            Condition.Register("PersonNotUsedBySlot", Condition.CraeteHandle<PersonNotUsedBySlotCheck>);
            Condition.Register("PersonInForce", Condition.CraeteHandle<PersonInForceCheck>);
            Condition.Register("PersonIsGovernorOf", Condition.CraeteHandle<PersonIsGovernorOfCheck>);

            // —— 城市 / 部队类 ——
            Condition.Register("CityHasWildPerson", Condition.CraeteHandle<CityHasWildPersonCheck>);
            Condition.Register("CityBelongForce", Condition.CraeteHandle<CityBelongForceCheck>);
            Condition.Register("TroopExists", Condition.CraeteHandle<TroopExistsCheck>);
            Condition.Register("TroopDistanceCheck", Condition.CraeteHandle<TroopDistanceCheck>);

            Log.Info($"剧本事件条件注册完成，当前条件总数 {Condition.CreateMap.Count}");
        }
    }

    /// <summary>
    /// 事件条件的公共工具。
    /// </summary>
    internal static class EventConditionHelper
    {
        /// <summary>
        /// 按槽位 Key 取对象。
        ///
        /// 优先走 <see cref="EventConditionDatabase"/>（支持任意命名的槽位）；
        /// 若传进来的是其它 IConditionDatabase 实现，则退化为 Action / Target 双槽映射——
        /// 这样事件里写的 ActionForce / TargetPerson 与既有的 IScenarioEventData 约定完全一致。
        /// </summary>
        /// <param name="database">条件数据库</param>
        /// <param name="key">槽位 Key</param>
        /// <returns>对象；取不到返回 null</returns>
        public static SangoObject GetSlot(IConditionDatabase database, string key)
        {
            if (database == null || string.IsNullOrEmpty(key)) return null;

            if (database is EventConditionDatabase eventDb)
                return eventDb.GetSlot(key);

            // 退化路径：只支持 Action / Target 双槽
            switch (key)
            {
                case "ActionPerson": return database.ActionPerson;
                case "TargetPerson": return database.TargetPerson;
                case "ActionTroop": return database.ActionTroop;
                case "TargetTroop": return database.TargetTroop;
                case "ActionCity": return database.ActionCity;
                case "TargetCity": return database.TargetCity;
                case "ActionCorps": return database.ActionCorps;
                case "TargetCorps": return database.TargetCorps;
                case "ActionForce": return database.ActionForce;
                case "TargetForce": return database.TargetForce;
                default: return null;
            }
        }

        /// <summary>按槽位 Key 取指定类型的对象</summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="database">条件数据库</param>
        /// <param name="key">槽位 Key</param>
        /// <returns>对象；取不到或类型不符返回 null</returns>
        public static T GetSlot<T>(IConditionDatabase database, string key) where T : SangoObject
        {
            return GetSlot(database, key) as T;
        }

        /// <summary>取事件条件数据库（用于读 fireCount / eventId 这类事件上下文）</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>事件数据库；不是则返回 null</returns>
        public static EventConditionDatabase AsEventDatabase(IConditionDatabase database)
        {
            return database as EventConditionDatabase;
        }

        /// <summary>
        /// 通用数值比较。Op 支持 "==" / "!=" / "&gt;=" / "&lt;=" / "&gt;" / "&lt;"，缺省为 "=="。
        /// </summary>
        /// <param name="left">左值</param>
        /// <param name="op">运算符</param>
        /// <param name="right">右值</param>
        /// <returns>比较结果</returns>
        public static bool Compare(int left, string op, int right)
        {
            switch (op)
            {
                case ">=": return left >= right;
                case "<=": return left <= right;
                case ">": return left > right;
                case "<": return left < right;
                case "!=": return left != right;
                default: return left == right;
            }
        }
    }
}
