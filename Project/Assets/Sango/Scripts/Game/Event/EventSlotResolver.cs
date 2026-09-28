/*
 * 文件名：EventSlotResolver.cs
 * 描述：角色槽解析器——把 EventActorSlot 的声明解析成实际的游戏对象
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 角色槽解析器。
    ///
    /// 职责：按 <see cref="EventActorSlot"/> 声明的 Source / SourceArg / Filters，
    /// 从当前剧本里把一个具体对象找出来，绑进 <see cref="EventContext"/>。
    ///
    /// 【确定性】同一次触发的解析结果必须可复现：
    ///   · 随机来源用 GameRandom，但结果一旦绑定就不再重抽；
    ///   · 解析顺序为"声明顺序"，前一个槽的结果可以被后一个槽引用（如 TroopOfPerson 引用武将槽）。
    ///
    /// 【失败策略】任何 Optional=false 的槽解析失败 → 整个事件放弃触发
    /// （不改世界状态、不计触发次数），这是安全的退化方向。
    /// </summary>
    public static class EventSlotResolver
    {
        /// <summary>
        /// 校验一个槽位声明是否配置完整（不涉及世界状态，编辑器与运行时都用）。
        /// </summary>
        /// <param name="slot">槽位声明</param>
        /// <param name="reason">不完整时的中文原因</param>
        /// <returns>是否配置完整</returns>
        public static bool Validate(EventActorSlot slot, out string reason)
        {
            reason = null;

            if (slot == null)
            {
                reason = "槽位声明为空";
                return false;
            }

            if (string.IsNullOrEmpty(slot.Key))
            {
                reason = "槽位缺少 Key";
                return false;
            }

            // 部队不能用 Fixed + Id 绑定：部队是运行时创建 / 销毁的对象，其 Id 每局都可能不同。
            // 这是项目里最容易踩的坑（设计文档中单列了一条硬规矩）。
            if (slot.Type == EventSlotType.Troop && slot.Source == EventSlotSource.Fixed)
            {
                reason = $"部队槽 {slot.Key} 不能用 Fixed 绑定：部队是运行时对象，Id 每局都会变，请改用 TroopOfPerson / ForceBestTroop / NeighborTroop / EventEntity";
                return false;
            }

            if (slot.Source == EventSlotSource.Fixed && string.IsNullOrEmpty(slot.SourceArg))
            {
                reason = $"槽位 {slot.Key} 的 Source 为 Fixed 但没有 SourceArg（对象 Id）";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 解析全部槽位并绑定到上下文。
        /// </summary>
        /// <param name="definition">事件定义</param>
        /// <param name="context">运行时上下文（结果写入其 database 与 boundSlots）</param>
        /// <returns>true = 所有必需槽位都绑定成功；false = 有必需槽位失败（事件应放弃）</returns>
        public static bool ResolveAll(EventDefinition definition, EventContext context)
        {
            if (definition == null || context == null) return false;
            if (definition.Slots == null || definition.Slots.Count == 0) return true;

            for (int i = 0; i < definition.Slots.Count; i++)
            {
                EventActorSlot slot = definition.Slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Key)) continue;

                if (!Validate(slot, out string invalidReason))
                {
                    Log.Error($"事件 {definition} 的槽位配置非法：{invalidReason}");
                    if (!slot.Optional)
                    {
                        context.slotFailReason = invalidReason;
                        return false;
                    }
                    continue;
                }

                SangoObject resolved = Resolve(slot, context);
                if (resolved == null)
                {
                    string reason = $"槽位 {slot} 解析失败";
                    if (slot.Optional)
                    {
                        // 可选槽位允许为空：后续引用它的指令 / 效果会跳过
                        Log.Warning($"事件 {definition} 的{reason}（可选槽位，置空继续）");
                        continue;
                    }

                    context.slotFailReason = reason;
                    Log.Info($"事件 {definition} 的{reason}，放弃本次触发");
                    return false;
                }

                context.BindSlot(slot.Key, slot.Type, resolved);
            }

            return true;
        }

        /// <summary>
        /// 解析单个槽位（不做 Optional 处理，由 ResolveAll 统一决定失败策略）。
        /// </summary>
        /// <param name="slot">槽位声明</param>
        /// <param name="context">运行时上下文</param>
        /// <returns>解析到的对象；失败返回 null</returns>
        public static SangoObject Resolve(EventActorSlot slot, EventContext context)
        {
            if (slot == null) return null;

            // 备选来源：按顺序尝试，第一个成功的即采用
            if (slot.Alternatives != null && slot.Alternatives.Count > 0)
            {
                for (int i = 0; i < slot.Alternatives.Count; i++)
                {
                    EventSlotAlternative alt = slot.Alternatives[i];
                    if (alt == null) continue;
                    SangoObject result = ResolveBySource(slot, alt.Source, alt.SourceArg, alt.ScopeSlot, alt.Filters, context);
                    if (result != null) return result;
                }
                return null;
            }

            return ResolveBySource(slot, slot.Source, slot.SourceArg, slot.ScopeSlot, slot.Filters, context);
        }

        /// <summary>
        /// 按具体来源解析。
        /// </summary>
        /// <param name="slot">槽位声明（取 Type）</param>
        /// <param name="source">来源</param>
        /// <param name="sourceArg">来源参数</param>
        /// <param name="scopeSlot">作用域槽位</param>
        /// <param name="filters">筛选条件</param>
        /// <param name="context">运行时上下文</param>
        /// <returns>解析到的对象；失败返回 null</returns>
        static SangoObject ResolveBySource(EventActorSlot slot, EventSlotSource source, string sourceArg, string scopeSlot,
            EventSlotFilter filters, EventContext context)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return null;

            switch (source)
            {
                case EventSlotSource.Fixed:
                    {
                        if (!int.TryParse(sourceArg, out int id))
                        {
                            Log.Warning($"槽位 {slot.Key} 的 SourceArg \"{sourceArg}\" 不是合法 Id");
                            return null;
                        }
                        return EventConditionEvaluator.ResolveObject(scenario, slot.Type, id);
                    }

                case EventSlotSource.EventEntity:
                    // hook 注入的实体：类型必须匹配，否则视为解析失败
                    if (context == null || context.eventEntity == null) return null;
                    return MatchType(context.eventEntity, slot.Type) ? context.eventEntity : null;

                case EventSlotSource.PlayerForce:
                    return FindPlayerForce(scenario);

                case EventSlotSource.ForceGovernor:
                    {
                        Force force = ResolveScopeForce(sourceArg, scopeSlot, context, slot);
                        return force != null ? force.mGovernor : null;
                    }

                case EventSlotSource.ForceCounsellor:
                    {
                        Force force = ResolveScopeForce(sourceArg, scopeSlot, context, slot);
                        return force != null ? force.mCounsellor : null;
                    }

                case EventSlotSource.ForceBestPerson:
                    {
                        Force force = ResolveScopeForce(sourceArg, scopeSlot, context, slot);
                        return FindBestPerson(force, slot, filters);
                    }

                case EventSlotSource.ForceRandomPerson:
                    {
                        Force force = ResolveScopeForce(sourceArg, scopeSlot, context, slot);
                        return FindRandomPerson(force, slot, filters);
                    }

                case EventSlotSource.CityGovernor:
                    {
                        City city = ResolveScopeCity(sourceArg, scopeSlot, context, slot);
                        return city != null ? city.Leader : null;
                    }

                case EventSlotSource.CityRandomPerson:
                    {
                        City city = ResolveScopeCity(sourceArg, scopeSlot, context, slot);
                        return FindRandomPersonInCity(city, slot, filters);
                    }

                case EventSlotSource.PlayerMainCity:
                    {
                        Force force = FindPlayerForce(scenario);
                        return force != null ? force.CapitalCity : null;
                    }

                case EventSlotSource.TroopOfPerson:
                    {
                        Person person = context != null ? context.GetSlot<Person>(sourceArg) : null;
                        return person != null ? FindTroopOfPerson(scenario, person) : null;
                    }

                case EventSlotSource.ForceBestTroop:
                    {
                        Force force = ResolveScopeForce(sourceArg, scopeSlot, context, slot);
                        return FindBestTroop(scenario, force);
                    }

                case EventSlotSource.DerivedOfSlot:
                    return ResolveDerived(sourceArg, context);

                default:
                    // NeighborForce / NeighborCity / NeighborTroop / ScenarioVariable 属于 Phase 2，
                    // 这里明确报错而不是静默返回 null，避免"事件没触发却查不出原因"
                    Log.Warning($"槽位 {slot.Key} 使用了尚未实现的来源：{source}");
                    return null;
            }
        }

        #region 各来源的实现

        /// <summary>找玩家操作的势力（可能有多家，取第一个）</summary>
        /// <param name="scenario">剧本</param>
        /// <returns>玩家势力；没有返回 null</returns>
        static Force FindPlayerForce(Scenario scenario)
        {
            if (scenario.forceSet == null) return null;
            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force force = scenario.forceSet[i];
                if (force != null && force.IsPlayer) return force;
            }
            return null;
        }

        /// <summary>
        /// 解析来源指向的势力：scopeSlot 优先（引用别的槽），否则用 sourceArg 当势力 Id。
        /// </summary>
        /// <param name="sourceArg">势力 Id 字符串</param>
        /// <param name="scopeSlot">作用域槽位 Key</param>
        /// <param name="context">上下文</param>
        /// <param name="slot">当前槽位（仅用于日志）</param>
        /// <returns>势力；失败返回 null</returns>
        static Force ResolveScopeForce(string sourceArg, string scopeSlot, EventContext context, EventActorSlot slot)
        {
            if (!string.IsNullOrEmpty(scopeSlot) && context != null)
            {
                Force fromSlot = context.GetSlot<Force>(scopeSlot);
                if (fromSlot != null) return fromSlot;
            }

            if (!string.IsNullOrEmpty(sourceArg) && int.TryParse(sourceArg, out int forceId))
                return Scenario.Cur.forceSet != null ? Scenario.Cur.forceSet.Get(forceId) : null;

            Log.Warning($"槽位 {slot.Key} 无法确定所属势力（ScopeSlot={scopeSlot}, SourceArg={sourceArg}）");
            return null;
        }

        /// <summary>解析来源指向的城市</summary>
        /// <param name="sourceArg">城市 Id 字符串</param>
        /// <param name="scopeSlot">作用域槽位 Key</param>
        /// <param name="context">上下文</param>
        /// <param name="slot">当前槽位（仅用于日志）</param>
        /// <returns>城市；失败返回 null</returns>
        static City ResolveScopeCity(string sourceArg, string scopeSlot, EventContext context, EventActorSlot slot)
        {
            if (!string.IsNullOrEmpty(scopeSlot) && context != null)
            {
                City fromSlot = context.GetSlot<City>(scopeSlot);
                if (fromSlot != null) return fromSlot;
            }

            if (!string.IsNullOrEmpty(sourceArg) && int.TryParse(sourceArg, out int cityId))
                return Scenario.Cur.citySet != null ? Scenario.Cur.citySet.Get(cityId) : null;

            Log.Warning($"槽位 {slot.Key} 无法确定所属城市（ScopeSlot={scopeSlot}, SourceArg={sourceArg}）");
            return null;
        }

        /// <summary>在势力内按属性挑最高的武将（如"智力最高者当军师"）</summary>
        /// <param name="force">势力</param>
        /// <param name="slot">槽位（SourceArg 为属性名，空则默认智力）</param>
        /// <param name="filters">筛选</param>
        /// <returns>武将；无候选人返回 null</returns>
        static Person FindBestPerson(Force force, EventActorSlot slot, EventSlotFilter filters)
        {
            if (force == null) return null;

            string attributeName = string.IsNullOrEmpty(slot.SourceArg) ? "Intelligence" : slot.SourceArg;
            List<Person> candidates = CollectForcePersons(force, filters, null);
            if (candidates.Count == 0) return null;

            Person best = null;
            int bestValue = int.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Person person = candidates[i];
                int value = ToAttributeValue(person, attributeName);
                if (best == null || value > bestValue)
                {
                    best = person;
                    bestValue = value;
                }
            }
            return best;
        }

        /// <summary>读武将属性值（走公共属性，符合项目规则）</summary>
        /// <param name="person">武将</param>
        /// <param name="attributeName">属性名：Command / Strength / Intelligence / Politics / Glamour</param>
        /// <returns>属性值；读不到返回 0</returns>
        public static int ToAttributeValue(Person person, string attributeName)
        {
            if (person == null || string.IsNullOrEmpty(attributeName)) return 0;
            object value = EventValue.GetObjectField(person, attributeName);
            return value is int i ? i : 0;
        }

        /// <summary>收集势力内符合条件的武将</summary>
        /// <param name="force">势力</param>
        /// <param name="filters">筛选</param>
        /// <param name="exclude">需要排除的武将</param>
        /// <returns>候选列表</returns>
        static List<Person> CollectForcePersons(Force force, EventSlotFilter filters, List<Person> exclude)
        {
            List<Person> result = new List<Person>();
            if (force == null) return result;

            force.ForEachPerson(person =>
            {
                if (person == null) return;
                if (exclude != null && exclude.Contains(person)) return;
                if (!PassFilters(person, filters)) return;
                result.Add(person);
            });

            return result;
        }

        /// <summary>在势力内随机挑一个符合条件的武将</summary>
        /// <param name="force">势力</param>
        /// <param name="slot">槽位</param>
        /// <param name="filters">筛选</param>
        /// <returns>武将；无候选人返回 null</returns>
        static Person FindRandomPerson(Force force, EventActorSlot slot, EventSlotFilter filters)
        {
            List<Person> candidates = CollectForcePersons(force, filters, null);
            if (candidates.Count == 0) return null;
            return candidates[GameRandom.Range(0, candidates.Count)];
        }

        /// <summary>在城内随机挑一个符合条件的武将</summary>
        /// <param name="city">城市</param>
        /// <param name="slot">槽位</param>
        /// <param name="filters">筛选</param>
        /// <returns>武将；无候选人返回 null</returns>
        static Person FindRandomPersonInCity(City city, EventActorSlot slot, EventSlotFilter filters)
        {
            if (city == null) return null;

            List<Person> candidates = new List<Person>();
            CollectFromList(city.wildPersons, candidates, filters);
            CollectFromList(city.freePersons, candidates, filters);
            CollectFromList(city.allPersons, candidates, filters);

            if (candidates.Count == 0) return null;
            return candidates[GameRandom.Range(0, candidates.Count)];
        }

        /// <summary>
        /// 把列表中符合条件的武将并入候选集（自动去重）。
        /// 注意：城市的人员名单容器类型并不统一 —— wildPersons 是 SangoObjectList，
        /// freePersons / allPersons 是 List，因此这里需要两个重载。
        /// </summary>
        /// <param name="source">来源列表</param>
        /// <param name="into">目标候选集</param>
        /// <param name="filters">筛选</param>
        static void CollectFromList(SangoObjectList<Person> source, List<Person> into, EventSlotFilter filters)
        {
            if (source == null) return;

            source.ForEach(person =>
            {
                if (person == null || into.Contains(person)) return;
                if (!PassFilters(person, filters)) return;
                into.Add(person);
            });
        }

        /// <summary>把 List 中的武将并入候选集（自动去重）</summary>
        /// <param name="source">来源列表</param>
        /// <param name="into">目标候选集</param>
        /// <param name="filters">筛选</param>
        static void CollectFromList(List<Person> source, List<Person> into, EventSlotFilter filters)
        {
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                Person person = source[i];
                if (person == null || into.Contains(person)) continue;
                if (!PassFilters(person, filters)) continue;
                into.Add(person);
            }
        }

        /// <summary>找某武将所在的部队（主将或副将）</summary>
        /// <param name="scenario">剧本</param>
        /// <param name="person">武将</param>
        /// <returns>部队；不在任何部队里返回 null</returns>
        public static Troop FindTroopOfPerson(Scenario scenario, Person person)
        {
            if (scenario == null || person == null || scenario.troopsSet == null) return null;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null) continue;
                if (troop.Leader == person || troop.Member1 == person || troop.Member2 == person)
                    return troop;
            }
            return null;
        }

        /// <summary>找势力内兵力最多的部队</summary>
        /// <param name="scenario">剧本</param>
        /// <param name="force">势力</param>
        /// <returns>部队；没有返回 null</returns>
        static Troop FindBestTroop(Scenario scenario, Force force)
        {
            if (scenario == null || force == null || scenario.troopsSet == null) return null;

            Troop best = null;
            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop troop = scenario.troopsSet[i];
                if (troop == null || troop.BelongForce != force) continue;
                if (best == null || troop.troops > best.troops) best = troop;
            }
            return best;
        }

        /// <summary>
        /// 由另一槽位派生。SourceArg 形如 "SlotKey.BelongForce"。
        /// </summary>
        /// <param name="sourceArg">派生表达式</param>
        /// <param name="context">上下文</param>
        /// <returns>派生对象；失败返回 null</returns>
        static SangoObject ResolveDerived(string sourceArg, EventContext context)
        {
            if (string.IsNullOrEmpty(sourceArg) || context == null) return null;

            int dot = sourceArg.IndexOf('.');
            if (dot <= 0)
            {
                Log.Warning($"DerivedOfSlot 的 SourceArg \"{sourceArg}\" 格式应为 \"槽位Key.属性名\"");
                return null;
            }

            string slotKey = sourceArg.Substring(0, dot);
            string field = sourceArg.Substring(dot + 1);

            object value = EventValue.GetObjectField(context.GetSlot(slotKey), field);
            return value as SangoObject;
        }

        /// <summary>判断对象是否符合槽位声明的类型</summary>
        /// <param name="obj">对象</param>
        /// <param name="type">期望类型</param>
        /// <returns>是否匹配</returns>
        static bool MatchType(SangoObject obj, EventSlotType type)
        {
            if (obj == null) return false;
            switch (type)
            {
                case EventSlotType.Force: return obj is Force;
                case EventSlotType.Corps: return obj is Corps;
                case EventSlotType.City: return obj is City;
                case EventSlotType.Person: return obj is Person;
                case EventSlotType.Troop: return obj is Troop;
                case EventSlotType.Skill: return obj is SkillInstance;
                case EventSlotType.Building: return obj is Building;
                default: return false;
            }
        }

        /// <summary>
        /// 武将是否通过全部筛选条件。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="filters">筛选条件（可为 null = 全通过）</param>
        /// <returns>是否通过</returns>
        public static bool PassFilters(Person person, EventSlotFilter filters)
        {
            if (person == null) return false;
            if (filters == null) return true;

            if (filters.Alive.HasValue && (person.state != (int)PersonStateType.Dead) != filters.Alive.Value)
                return false;

            if (filters.Belong == "InForce" && person.BelongForce == null) return false;
            if (filters.Belong == "InCity" && person.BelongCity == null) return false;
            if (filters.Belong == "Free" && !person.IsWild) return false;

            if (filters.Gender >= 0 && person.sex != filters.Gender) return false;

            if (!string.IsNullOrEmpty(filters.Attribute))
            {
                int value = ToAttributeValue(person, filters.Attribute);
                if (filters.AttributeMin >= 0 && value < filters.AttributeMin) return false;
                if (filters.AttributeMax >= 0 && value > filters.AttributeMax) return false;
            }

            if (filters.LoyaltyMin >= 0 && person.loyalty < filters.LoyaltyMin) return false;
            if (filters.LoyaltyMax >= 0 && person.loyalty > filters.LoyaltyMax) return false;

            if (filters.Free.HasValue && person.IsFree != filters.Free.Value) return false;

            if (filters.ExcludeIds != null && filters.ExcludeIds.Contains(person.Id)) return false;

            return true;
        }

        #endregion
    }
}
