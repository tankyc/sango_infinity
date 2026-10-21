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
            RegisterUnique("FireCountCompare", Condition.CraeteHandle<FireCountCompare>);
            RegisterUnique("EventFired", Condition.CraeteHandle<EventFiredCheck>);
            RegisterUnique("EventFiredCountCompare", Condition.CraeteHandle<EventFiredCountCompare>);
            RegisterUnique("EventOutcomeCheck", Condition.CraeteHandle<EventOutcomeCheck>);
            RegisterUnique("FlagCheck", Condition.CraeteHandle<FlagCheck>);
            RegisterUnique("VarCompare", Condition.CraeteHandle<VarCompare>);
            RegisterUnique("ScenarioVarCompare", Condition.CraeteHandle<ScenarioVarCompare>);
            RegisterUnique("RandomChance", Condition.CraeteHandle<EventRandomChanceCheck>);

            // —— 时间类 ——
            RegisterUnique("TimeRange", Condition.CraeteHandle<TimeRangeCheck>);
            RegisterUnique("DateCheck", Condition.CraeteHandle<EventDateCheck>);
            RegisterUnique("ScenarioYearCheck", Condition.CraeteHandle<ScenarioYearCheck>);

            // —— 势力类 ——
            RegisterUnique("ForceAlive", Condition.CraeteHandle<ForceAliveCheck>);
            RegisterUnique("ForceCityCount", Condition.CraeteHandle<ForceCityCountCheck>);
            RegisterUnique("ForceHasCity", Condition.CraeteHandle<ForceHasCityCheck>);
            RegisterUnique("ForceHasPerson", Condition.CraeteHandle<ForceHasPersonCheck>);
            RegisterUnique("PlayerForceCheck", Condition.CraeteHandle<PlayerForceCheck>);
            RegisterUnique("DifficultyCheck", Condition.CraeteHandle<EventDifficultyCheck>);

            // —— 武将类 ——
            RegisterUnique("PersonStateCheck", Condition.CraeteHandle<PersonStateCheck>);
            RegisterUnique("PersonBelongCheck", Condition.CraeteHandle<PersonBelongCheck>);
            // 注意：不能叫 "PersonLoyaltyCheck"。核心条件（Condition.Init）已经用这个名字注册了
            // Sango.Core.PersonLoyaltyCheck，参数是 checkTarget/value/operator；
            // 而事件版参数是 Slot/Min/Max。两者同名会让事件版**静默覆盖**核心版
            // （Condition.Register 是字典索引赋值，不报警），
            // 于是任何按旧格式写的技能/战法条件都会落到事件版解析：槽位读不到、Min/Max 均为 -1，
            // 结果恒为 true。故事件版改名为 EventPersonLoyalty，与核心版共存。
            RegisterUnique("EventPersonLoyalty", Condition.CraeteHandle<EventPersonLoyaltyCheck>);
            RegisterUnique("PersonVisible", Condition.CraeteHandle<PersonVisibleCheck>);
            RegisterUnique("PersonNotUsedBySlot", Condition.CraeteHandle<PersonNotUsedBySlotCheck>);
            RegisterUnique("PersonInForce", Condition.CraeteHandle<PersonInForceCheck>);
            RegisterUnique("PersonIsGovernorOf", Condition.CraeteHandle<PersonIsGovernorOfCheck>);

            // —— 城市 / 部队类 ——
            RegisterUnique("CityHasWildPerson", Condition.CraeteHandle<CityHasWildPersonCheck>);
            RegisterUnique("CityBelongForce", Condition.CraeteHandle<CityBelongForceCheck>);
            RegisterUnique("TroopExists", Condition.CraeteHandle<TroopExistsCheck>);
            RegisterUnique("TroopDistanceCheck", Condition.CraeteHandle<TroopDistanceCheck>);

            // —— 第二批：势力宏观量与外交 ——
            RegisterUnique("ForcePersonCount", Condition.CraeteHandle<ForcePersonCountCheck>);
            RegisterUnique("ForceHasTechnique", Condition.CraeteHandle<ForceHasTechniqueCheck>);
            RegisterUnique("ForceTechniquePoint", Condition.CraeteHandle<ForceTechniquePointCheck>);
            RegisterUnique("ForceHegemonyPoint", Condition.CraeteHandle<ForceHegemonyPointCheck>);
            RegisterUnique("ForceFightPower", Condition.CraeteHandle<ForceFightPowerCheck>);
            RegisterUnique("ForceTitle", Condition.CraeteHandle<ForceTitleCheck>);
            RegisterUnique("ForceGovernorIs", Condition.CraeteHandle<ForceGovernorIsCheck>);
            RegisterUnique("ForceCounsellorIs", Condition.CraeteHandle<ForceCounsellorIsCheck>);
            RegisterUnique("ForceRelation", Condition.CraeteHandle<ForceRelationCheck>);

            // —— 第二批：城市状态 ——
            RegisterUnique("CityProvince", Condition.CraeteHandle<CityProvinceCheck>);
            RegisterUnique("CityPrisonerCount", Condition.CraeteHandle<CityPrisonerCountCheck>);
            RegisterUnique("CityGovernorIs", Condition.CraeteHandle<CityGovernorIsCheck>);
            RegisterUnique("CityNeighborBelongForce", Condition.CraeteHandle<CityNeighborBelongForceCheck>);
            RegisterUnique("CityIsFrontline", Condition.CraeteHandle<CityIsFrontlineCheck>);
            RegisterUnique("CityHasBuilding", Condition.CraeteHandle<CityHasBuildingCheck>);
            RegisterUnique("CityTroopsRatio", Condition.CraeteHandle<CityTroopsRatioCheck>);

            // —— 第二批：武将关系与状态 ——
            RegisterUnique("PersonHasFeature", Condition.CraeteHandle<PersonHasFeatureCheck>);
            RegisterUnique("PersonAbility", Condition.CraeteHandle<PersonAbilityCheck>);
            RegisterUnique("PersonOfficial", Condition.CraeteHandle<PersonOfficialCheck>);
            RegisterUnique("PersonIsPrisoner", Condition.CraeteHandle<PersonIsPrisonerCheck>);
            RegisterUnique("PersonIsInTroop", Condition.CraeteHandle<PersonIsInTroopCheck>);
            RegisterUnique("PersonIsFree", Condition.CraeteHandle<PersonIsFreeCheck>);
            RegisterUnique("PersonRelation", Condition.CraeteHandle<PersonRelationCheck>);
            // 是否已属于兄弟组（剧本原设兄弟 / 结义产物）：桃园结义这类"三人都还没结义"的前置判断用它
            RegisterUnique("PersonHasBrother", Condition.CraeteHandle<PersonHasBrotherCheck>);
            RegisterUnique("PersonAge", Condition.CraeteHandle<PersonAgeCheck>);
            RegisterUnique("PersonGender", Condition.CraeteHandle<PersonGenderCheck>);
            RegisterUnique("PersonIdeal", Condition.CraeteHandle<PersonIdealCheck>);
            RegisterUnique("PersonInjury", Condition.CraeteHandle<PersonInjuryCheck>);

            // —— 第二批：部队状态 ——
            RegisterUnique("TroopType", Condition.CraeteHandle<TroopTypeCheck>);
            RegisterUnique("TroopSkill", Condition.CraeteHandle<TroopSkillCheck>);
            RegisterUnique("TroopEnergy", Condition.CraeteHandle<TroopEnergyCheck>);
            RegisterUnique("TroopMoraleRatio", Condition.CraeteHandle<TroopMoraleRatioCheck>);
            RegisterUnique("TroopIsInWater", Condition.CraeteHandle<TroopIsInWaterCheck>);

            Log.Info($"剧本事件条件注册完成，当前条件总数 {Condition.CreateMap.Count}");
        }

        /// <summary>
        /// 注册一个事件条件；若名字已被占用则**跳过并告警**，绝不做静默覆盖。
        ///
        /// 【为什么不用 Condition.Register】它是 <c>CreateMap[name] = creator</c>，
        /// 同名直接覆盖、不报警。核心条件（Condition.Init）先注册、事件条件后注册，
        /// 一旦重名就会把核心条件在生产环境里悄悄换掉，
        /// 表现为"某个战法条件突然恒为 true"，极难排查（本文件历史上有过一例）。
        /// </summary>
        /// <param name="name">条件名（事件 JSON 里的 Type）</param>
        /// <param name="creator">创建器</param>
        static void RegisterUnique(string name, Condition.ConditionCreator creator)
        {
            if (Condition.CreateMap.ContainsKey(name))
            {
                Log.Warning($"剧本事件条件注册冲突，已跳过：{name}（同名条件已存在，请改用事件专用名）");
                return;
            }
            Condition.Register(name, creator);
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
