/*
 * 文件名：EventConditions.cs
 * 描述：事件专用条件——流程、时间、势力、武将、城市、部队
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 *
 * 约定：
 *   · Init 收到的是"已拍平 Params"的 JObject，因此直接 p.Value<T>("Slot") 即可；
 *   · 槽位读取统一走 EventConditionHelper.GetSlot（优先 EventConditionDatabase，
 *     退化为 Action / Target 双槽），这样既有的 IScenarioEventData 命名约定可以原样沿用；
 *   · 任何参数缺失 / 引用不到对象都返回 false，绝不抛异常——
 *     条件不成立 = 事件不发生，是安全的退化方向。
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    #region 事件流程类

    /// <summary>
    /// 按本次是第几次触发来判定（1-based）。
    /// 三顾茅庐靠它分流"一顾 / 二顾 / 三顾"三套不同演出，是重复触发型事件的核心条件。
    /// </summary>
    public class FireCountCompare : Condition
    {
        string op = "==";
        int value = 1;

        /// <summary>初始化：读 Op 与 Value</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            op = p.Value<string>("Op") ?? "==";
            value = p.Value<int?>("Value") ?? 1;
        }

        /// <summary>检查本次触发序号</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            EventConditionDatabase db = EventConditionHelper.AsEventDatabase(database);
            int fireCount = db != null ? db.fireCount : 1;
            return EventConditionHelper.Compare(fireCount, op, value);
        }
    }

    /// <summary>检查某个事件是否已触发过（Expect 取反即"尚未触发"）</summary>
    public class EventFiredCheck : Condition
    {
        int eventId;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            eventId = p.Value<int?>("EventId") ?? 0;
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查触发史</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (manager == null || eventId <= 0) return false;
            bool fired = manager.GetFireCount(eventId) > 0;
            return fired == expect;
        }
    }

    /// <summary>按某事件的触发次数比较</summary>
    public class EventFiredCountCompare : Condition
    {
        int eventId;
        string op = ">=";
        int value;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            eventId = p.Value<int?>("EventId") ?? 0;
            op = p.Value<string>("Op") ?? ">=";
            value = p.Value<int?>("Value") ?? 0;
        }

        /// <summary>检查触发次数</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (manager == null || eventId <= 0) return false;
            return EventConditionHelper.Compare(manager.GetFireCount(eventId), op, value);
        }
    }

    /// <summary>检查某个事件上一次达成的结局（多结局事件用）</summary>
    public class EventOutcomeCheck : Condition
    {
        int eventId;
        string outcome;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            eventId = p.Value<int?>("EventId") ?? 0;
            outcome = p.Value<string>("Outcome");
        }

        /// <summary>检查结局标记</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (manager == null || eventId <= 0 || string.IsNullOrEmpty(outcome)) return false;
            EventRunState state = manager.GetState(eventId, false);
            return state != null && state.Outcome == outcome;
        }
    }

    /// <summary>检查全局旗标（跨事件共享，随存档持久化）</summary>
    public class FlagCheck : Condition
    {
        string flag;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            flag = p.Value<string>("Flag");
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查旗标</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (manager == null || string.IsNullOrEmpty(flag)) return false;
            return manager.GetGlobalFlag(flag, false) == expect;
        }
    }

    /// <summary>比较事件私有变量（&lt;c&gt;#变量名&lt;/c&gt;）</summary>
    public class VarCompare : Condition
    {
        string varName;
        string op = "==";
        int value;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            varName = p.Value<string>("Var");
            op = p.Value<string>("Op") ?? "==";
            value = p.Value<int?>("Value") ?? 0;
        }

        /// <summary>检查变量</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            EventConditionDatabase db = EventConditionHelper.AsEventDatabase(database);
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (db == null || manager == null || string.IsNullOrEmpty(varName)) return false;
            return EventConditionHelper.Compare(manager.GetEventVar(db.eventId, varName), op, value);
        }
    }

    /// <summary>比较剧本变量（Scenario.Cur.Variables 上的配置项）</summary>
    public class ScenarioVarCompare : Condition
    {
        string varName;
        string op = "==";
        int value;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            varName = p.Value<string>("Var");
            op = p.Value<string>("Op") ?? "==";
            value = p.Value<int?>("Value") ?? 0;
        }

        /// <summary>检查剧本变量</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (Scenario.Cur == null || Scenario.Cur.Variables == null || string.IsNullOrEmpty(varName))
                return false;

            object raw = EventValue.GetObjectField(Scenario.Cur.Variables, varName);
            int current = raw is int i ? i : 0;
            return EventConditionHelper.Compare(current, op, value);
        }
    }

    /// <summary>
    /// 概率判定。
    /// 注意：由 EventConditionEvaluator 保证它排在条件树的最后求值——
    /// 否则会先消耗随机数再因其它条件失败，读档重放会不一致。
    /// </summary>
    public class EventRandomChanceCheck : Condition
    {
        int percent = 100;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            percent = p.Value<int?>("Percent") ?? 100;
        }

        /// <summary>掷骰</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否命中</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (percent >= 100) return true;
            if (percent <= 0) return false;
            return GameRandom.Chance(percent);
        }
    }

    #endregion

    #region 时间类

    /// <summary>限定游戏进行到的年份 / 月份区间（历史事件的时期锚点）</summary>
    public class TimeRangeCheck : Condition
    {
        int yearMin = -1, yearMax = -1, monthMin = -1, monthMax = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            yearMin = p.Value<int?>("YearMin") ?? -1;
            yearMax = p.Value<int?>("YearMax") ?? -1;
            monthMin = p.Value<int?>("MonthMin") ?? -1;
            monthMax = p.Value<int?>("MonthMax") ?? -1;
        }

        /// <summary>检查当前日期</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (Scenario.Cur == null || Scenario.Cur.Info == null) return false;

            int year = Scenario.Cur.Info.year;
            int month = Scenario.Cur.Info.month;

            if (yearMin > 0 && year < yearMin) return false;
            if (yearMax > 0 && year > yearMax) return false;
            if (monthMin > 0 && month < monthMin) return false;
            if (monthMax > 0 && month > monthMax) return false;
            return true;
        }
    }

    /// <summary>精确日期判定（年 / 月 / 日，0 表示不限定）</summary>
    public class EventDateCheck : Condition
    {
        int year, month, day;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            year = p.Value<int?>("Year") ?? 0;
            month = p.Value<int?>("Month") ?? 0;
            day = p.Value<int?>("Day") ?? 0;
        }

        /// <summary>检查日期</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否匹配</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (Scenario.Cur == null || Scenario.Cur.Info == null) return false;

            if (year != 0 && Scenario.Cur.Info.year != year) return false;
            if (month != 0 && Scenario.Cur.Info.month != month) return false;
            if (day != 0 && Scenario.Cur.Info.day != day) return false;
            return true;
        }
    }

    /// <summary>
    /// 剧本起始年份过滤。
    /// 与 TimeRangeCheck 的区别：那是"游戏进行到哪一年"，这里是"剧本从哪一年开局"。
    /// 官渡之战只在"公元 200 年开局"的剧本里才有意义。
    /// </summary>
    public class ScenarioYearCheck : Condition
    {
        int min = -1, max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查剧本起始年份</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否匹配</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (Scenario.Cur == null || Scenario.Cur.Info == null) return false;

            // 【必须用"开局年份"，不能用 Scenario.Cur.Info.year】
            // Info.year 是**当前**年份，IncreaseDate() 里会 Info.year += 1 ——
            // 用它就退化成和 TimeRangeCheck 的年判定等价，表现为
            // "事件在某个年份突然开始 / 停止生效"，而配置者以为比的是开局年份。
            // 开局年份由 ScenarioEventManager 在 OnScenarioStart 时捕获并缓存。
            ScenarioEventManager manager = ScenarioEventManager.Instance;
            int startYear = manager != null ? manager.ScenarioStartYear : 0;

            // 取不到开局年份时**不做过滤**：宁可多触发，也不要因为数据缺失而静默失效
            if (startYear <= 0) return true;

            if (min > 0 && startYear < min) return false;
            if (max > 0 && startYear > max) return false;
            return true;
        }
    }

    #endregion

    #region 势力类

    /// <summary>势力是否存在且未灭亡</summary>
    public class ForceAliveCheck : Condition
    {
        string slot;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
        }

        /// <summary>检查势力存活</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            return force != null && force.IsAlive;
        }
    }

    /// <summary>势力城池数量区间（以弱胜强类事件的前提）</summary>
    public class ForceCityCountCheck : Condition
    {
        string slot;
        int min = -1, max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>实时遍历统计城池数（不用 Force.CityCount，避免回合内的陈旧值）</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null || Scenario.Cur == null || Scenario.Cur.citySet == null) return false;

            int count = 0;
            Scenario.Cur.citySet.ForEach(city =>
            {
                if (city == null || !city.IsAlive) return;
                if (city.BelongForce != force) return;
                if (!city.IsCity()) return;
                count++;
            });

            if (min >= 0 && count < min) return false;
            if (max >= 0 && count > max) return false;
            return true;
        }
    }

    /// <summary>势力是否拥有指定城市</summary>
    public class ForceHasCityCheck : Condition
    {
        string slot;
        string citySlot;
        int cityId;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            citySlot = p.Value<string>("CitySlot");
            cityId = p.Value<int?>("CityId") ?? 0;
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查归属</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;

            City city = !string.IsNullOrEmpty(citySlot)
                ? EventConditionHelper.GetSlot<City>(database, citySlot)
                : (Scenario.Cur != null && Scenario.Cur.citySet != null && cityId > 0 ? Scenario.Cur.citySet.Get(cityId) : null);

            if (city == null) return false;
            return (city.BelongForce == force) == expect;
        }
    }

    /// <summary>势力麾下是否有指定武将</summary>
    public class ForceHasPersonCheck : Condition
    {
        string slot;
        string personSlot;
        int personId;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            personSlot = p.Value<string>("PersonSlot");
            personId = p.Value<int?>("PersonId") ?? 0;
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查归属</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;

            Person person = !string.IsNullOrEmpty(personSlot)
                ? EventConditionHelper.GetSlot<Person>(database, personSlot)
                : (Scenario.Cur != null && Scenario.Cur.personSet != null && personId > 0 ? Scenario.Cur.personSet.Get(personId) : null);

            if (person == null) return false;
            return (person.BelongForce == force) == expect;
        }
    }

    /// <summary>指定槽位是否为玩家操作的势力</summary>
    public class PlayerForceCheck : Condition
    {
        string slot;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
        }

        /// <summary>检查是否玩家势力</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            return force != null && force.IsPlayer;
        }
    }

    /// <summary>难度区间</summary>
    public class EventDifficultyCheck : Condition
    {
        int min = -1, max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查难度</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (Scenario.Cur == null || Scenario.Cur.Variables == null) return false;

            int difficulty = Scenario.Cur.Variables.difficulty;
            if (min >= 0 && difficulty < min) return false;
            if (max >= 0 && difficulty > max) return false;
            return true;
        }
    }

    #endregion

    #region 武将类

    /// <summary>
    /// 武将状态判定。State 取值：
    /// Alive（未死亡）/ Dead / Prisoner（俘虏）/ Wild（在野）/ Normal（仕官）/ Invisible（未发现）
    /// </summary>
    public class PersonStateCheck : Condition
    {
        string slot;
        string state = "Alive";

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "TargetPerson";
            state = p.Value<string>("State") ?? "Alive";
        }

        /// <summary>检查状态</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;

            switch (state)
            {
                case "Alive": return !person.IsDead;
                case "Dead": return person.IsDead;
                case "Prisoner": return person.IsPrisoner;
                case "Wild": return person.IsWild;
                case "Normal": return person.state == (int)PersonStateType.Normal;
                case "Invisible": return person.Invisible;
                default: return false;
            }
        }
    }

    /// <summary>
    /// 武将归属判定。Belong 取值：
    /// InForce（有势力）/ InCity（有归属城）/ Free（在野）/ Any（只要有对象）
    /// </summary>
    public class PersonBelongCheck : Condition
    {
        string slot;
        string belong = "InForce";
        string forceSlot;
        string citySlot;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "TargetPerson";
            belong = p.Value<string>("Belong") ?? "InForce";
            forceSlot = p.Value<string>("ForceSlot");
            citySlot = p.Value<string>("CitySlot");
        }

        /// <summary>检查归属</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;

            switch (belong)
            {
                case "InForce":
                    if (person.BelongForce == null) return false;
                    break;
                case "InCity":
                    if (person.BelongCity == null) return false;
                    break;
                case "Free":
                    if (!person.IsWild) return false;
                    break;
                case "Any":
                    break;
                default:
                    return false;
            }

            // 追加校验：指定了势力 / 城池槽位时还必须归属一致
            if (!string.IsNullOrEmpty(forceSlot))
            {
                Force force = EventConditionHelper.GetSlot<Force>(database, forceSlot);
                if (force != null && person.BelongForce != force) return false;
            }

            if (!string.IsNullOrEmpty(citySlot))
            {
                City city = EventConditionHelper.GetSlot<City>(database, citySlot);
                if (city != null && person.BelongCity != city && person.CurrentCity != city) return false;
            }

            return true;
        }
    }

    /// <summary>武将忠诚区间</summary>
    public class EventPersonLoyaltyCheck : Condition
    {
        string slot;
        int min = -1, max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "TargetPerson";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查忠诚</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            if (min >= 0 && person.loyalty < min) return false;
            if (max >= 0 && person.loyalty > max) return false;
            return true;
        }
    }

    /// <summary>武将是否已被发现（未发现 = PersonStateType.Invisible）</summary>
    public class PersonVisibleCheck : Condition
    {
        string slot;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "TargetPerson";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查可见性</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return (!person.Invisible) == expect;
        }
    }

    /// <summary>
    /// 槽位互斥：确保指定槽位与 Exclude 里的槽位不是同一个人。
    /// 用于"军师不能是被俘的那个"这类约束，避免同一人占两个槽造成演出错乱。
    /// </summary>
    public class PersonNotUsedBySlotCheck : Condition
    {
        string slot;
        List<string> exclude = new List<string>();

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot");
            exclude.Clear();

            JArray array = p["Exclude"] as JArray;
            if (array != null)
            {
                for (int i = 0; i < array.Count; i++)
                    exclude.Add(array[i].Value<string>());
            }
        }

        /// <summary>检查是否与排除槽位指向同一对象</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>互斥成立返回 true</returns>
        public override bool Check(IConditionDatabase database)
        {
            SangoObject self = EventConditionHelper.GetSlot(database, slot);
            if (self == null) return false;

            for (int i = 0; i < exclude.Count; i++)
            {
                SangoObject other = EventConditionHelper.GetSlot(database, exclude[i]);
                if (other != null && other == self) return false;
            }
            return true;
        }
    }

    /// <summary>武将是否在某势力（Expect 取反即"不在"）</summary>
    public class PersonInForceCheck : Condition
    {
        string slot;
        string forceSlot;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "TargetPerson";
            forceSlot = p.Value<string>("ForceSlot") ?? "ActionForce";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查归属</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            Force force = EventConditionHelper.GetSlot<Force>(database, forceSlot);
            if (person == null || force == null) return false;
            return (person.BelongForce == force) == expect;
        }
    }

    /// <summary>武将是否为指定势力的君主</summary>
    public class PersonIsGovernorOfCheck : Condition
    {
        string slot;
        string forceSlot;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "TargetPerson";
            forceSlot = p.Value<string>("ForceSlot") ?? "ActionForce";
        }

        /// <summary>检查是否为君主</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            Force force = EventConditionHelper.GetSlot<Force>(database, forceSlot);
            if (person == null || force == null) return false;
            return force.mGovernor == person;
        }
    }

    #endregion

    #region 城市 / 部队类

    /// <summary>
    /// 某在野武将是否真的挂在指定城市的在野名单上。
    /// 三顾茅庐的关键条件：诸葛亮必须"人在隆中"，否则刘备白跑一趟。
    /// </summary>
    public class CityHasWildPersonCheck : Condition
    {
        string citySlot;
        string personSlot;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            citySlot = p.Value<string>("CitySlot") ?? "ActionCity";
            personSlot = p.Value<string>("PersonSlot") ?? "TargetPerson";
        }

        /// <summary>检查在野名单</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, citySlot);
            Person person = EventConditionHelper.GetSlot<Person>(database, personSlot);
            if (city == null || person == null || city.wildPersons == null) return false;

            return city.wildPersons.Contains(person);
        }
    }

    /// <summary>城市归属判定</summary>
    public class CityBelongForceCheck : Condition
    {
        string citySlot;
        string forceSlot;
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            citySlot = p.Value<string>("CitySlot") ?? "ActionCity";
            forceSlot = p.Value<string>("ForceSlot") ?? "ActionForce";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查归属</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, citySlot);
            Force force = EventConditionHelper.GetSlot<Force>(database, forceSlot);
            if (city == null || force == null) return false;
            return (city.BelongForce == force) == expect;
        }
    }

    /// <summary>部队是否存在且未灭亡</summary>
    public class TroopExistsCheck : Condition
    {
        string slot;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionTroop";
        }

        /// <summary>检查存在性</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否成立</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = EventConditionHelper.GetSlot<Troop>(database, slot);
            return troop != null && troop.IsAlive;
        }
    }

    /// <summary>
    /// 两支部队的距离判定（三英战吕布的"接敌"前提）。
    /// 用地图格距，因此部队没有 cell 时直接判定不成立。
    /// </summary>
    public class TroopDistanceCheck : Condition
    {
        string slotA;
        string slotB;
        int minDistance = -1;
        int maxDistance = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slotA = p.Value<string>("SlotA") ?? "ActionTroop";
            slotB = p.Value<string>("SlotB") ?? "TargetTroop";
            minDistance = p.Value<int?>("MinDistance") ?? -1;
            maxDistance = p.Value<int?>("MaxDistance") ?? -1;
        }

        /// <summary>检查距离</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在距离区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop a = EventConditionHelper.GetSlot<Troop>(database, slotA);
            Troop b = EventConditionHelper.GetSlot<Troop>(database, slotB);
            if (a == null || b == null) return false;
            if (a.cell == null || b.cell == null) return false;
            if (Scenario.Cur == null || Scenario.Cur.Map == null) return false;

            int distance = Scenario.Cur.Map.Distance(a.cell, b.cell);
            if (minDistance >= 0 && distance < minDistance) return false;
            if (maxDistance >= 0 && distance > maxDistance) return false;
            return true;
        }
    }

    #endregion
}
