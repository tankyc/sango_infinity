/*
 * 文件名：EventConditionsExt.cs
 * 描述：剧本事件第二批条件——势力宏观量、城市状态、武将关系与部队状态
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【与 EventConditions.cs 的分工】
 *   EventConditions.cs 收录"事件流程类"条件（触发次数、旗标、随机、难度等）；
 *   本文件收录"世界状态类"条件，全部只读游戏对象、不改世界。
 *
 * 【实现约定】
 *   1. 槽位统一走 EventConditionHelper.GetSlot，因此既支持任意命名槽位，
 *      也兼容 Action/Target 双槽的退化路径；
 *   2. 参数一律 PascalCase（与 EventConditions.cs 一致），未填时给保守缺省值；
 *   3. 数值比较统一走 EventConditionHelper.Compare，运算符缺省为 "=="；
 *   4. 取不到槽位一律返回 false（不抛异常）——事件条件失败只代表"不触发"，
 *      不该让整个调度器停摆；
 *   5. 每个条件只做一次对象读取或一次浅遍历，不在这里做全图扫描（ConditionCost 分级的初衷）。
 */

using Newtonsoft.Json.Linq;

namespace Sango.Core.Event
{
    // ========================================================================
    // 一、势力类（9 个）
    // ========================================================================

    /// <summary>势力武将数区间检查（读 Force.PersonCount，引擎在人事变动时维护）</summary>
    public class ForcePersonCountCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>下限（-1 = 不限）</summary>
        int min = -1;

        /// <summary>上限（-1 = 不限）</summary>
        int max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;

            if (min >= 0 && force.PersonCount < min) return false;
            if (max >= 0 && force.PersonCount > max) return false;
            return true;
        }
    }

    /// <summary>势力是否已掌握指定技巧（读 Force.Techniques）</summary>
    public class ForceHasTechniqueCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>技巧 Id</summary>
        int techniqueId;

        /// <summary>期望值（true = 已掌握，false = 未掌握）</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            techniqueId = p.Value<int>("TechniqueId");
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null || force.Techniques == null) return false;

            bool has = techniqueId > 0 && force.Techniques.Contains(techniqueId);
            return has == expect;
        }
    }

    /// <summary>势力技巧点比较（读 Force.TechniquePoint）</summary>
    public class ForceTechniquePointCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>比较运算符</summary>
        string op = ">=";

        /// <summary>比较值</summary>
        int value;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            op = p.Value<string>("Op") ?? ">=";
            value = p.Value<int>("Value");
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;
            return EventConditionHelper.Compare(force.TechniquePoint, op, value);
        }
    }

    /// <summary>势力霸业点比较（读 Force.HegemonyPoint）</summary>
    public class ForceHegemonyPointCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>比较运算符</summary>
        string op = ">=";

        /// <summary>比较值</summary>
        int value;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            op = p.Value<string>("Op") ?? ">=";
            value = p.Value<int>("Value");
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;
            return EventConditionHelper.Compare(force.HegemonyPoint, op, value);
        }
    }

    /// <summary>势力战力比较（读 Force.FightPower）</summary>
    public class ForceFightPowerCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>比较运算符</summary>
        string op = ">=";

        /// <summary>比较值</summary>
        int value;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            op = p.Value<string>("Op") ?? ">=";
            value = p.Value<int>("Value");
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;
            return EventConditionHelper.Compare(force.FightPower, op, value);
        }
    }

    /// <summary>势力爵位检查（读 Force.TitleId）</summary>
    public class ForceTitleCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>爵位 Id</summary>
        int titleId;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            titleId = p.Value<int>("TitleId");
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            if (force == null) return false;
            return (force.TitleId == titleId) == expect;
        }
    }

    /// <summary>势力君主是否为指定武将（读 Force.Governor 的 Id）</summary>
    public class ForceGovernorIsCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>武将槽位 Key</summary>
        string personSlot = "ActionPerson";

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            personSlot = p.Value<string>("PersonSlot") ?? "ActionPerson";
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            Person person = EventConditionHelper.GetSlot<Person>(database, personSlot);
            if (force == null || person == null) return false;
            return force.Governor == person.Id;
        }
    }

    /// <summary>势力军师是否为指定武将（读 Force.Counsellor 的 Id）</summary>
    public class ForceCounsellorIsCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>武将槽位 Key</summary>
        string personSlot = "ActionPerson";

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            personSlot = p.Value<string>("PersonSlot") ?? "ActionPerson";
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force force = EventConditionHelper.GetSlot<Force>(database, slot);
            Person person = EventConditionHelper.GetSlot<Person>(database, personSlot);
            if (force == null || person == null) return false;
            return force.Counsellor == person.Id;
        }
    }

    /// <summary>
    /// 两个势力的外交关系检查。
    /// Relation 取值：ally（同盟）/ enemy（敌对）/ same（同一势力）。
    /// 判定统一走 Force.IsAlliance / Force.IsEnemy，与既有 FactionCheck 口径一致。
    /// </summary>
    public class ForceRelationCheck : Condition
    {
        /// <summary>势力槽位 Key</summary>
        string slot;

        /// <summary>另一势力槽位 Key</summary>
        string otherSlot = "TargetForce";

        /// <summary>关系类型</summary>
        string relation = "enemy";

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionForce";
            otherSlot = p.Value<string>("OtherSlot") ?? "TargetForce";
            relation = p.Value<string>("Relation") ?? "enemy";
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Force a = EventConditionHelper.GetSlot<Force>(database, slot);
            Force b = EventConditionHelper.GetSlot<Force>(database, otherSlot);
            if (a == null || b == null) return false;

            switch (relation)
            {
                case "ally": return a.IsAlliance(b);
                case "enemy": return a.IsEnemy(b);
                case "same": return a == b;
                default: return false;
            }
        }
    }

    // ========================================================================
    // 二、城市类（7 个）
    // ========================================================================

    /// <summary>城市所属州检查（读 City.ProvinceId）</summary>
    public class CityProvinceCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>州 Id</summary>
        int provinceId;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            provinceId = p.Value<int>("ProvinceId");
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            if (city == null) return false;
            return city.ProvinceId == provinceId;
        }
    }

    /// <summary>城市俘虏数区间检查（读 City.captiveList）</summary>
    public class CityPrisonerCountCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>下限（-1 = 不限）</summary>
        int min = -1;

        /// <summary>上限（-1 = 不限）</summary>
        int max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            if (city == null) return false;

            int count = city.captiveList != null ? city.captiveList.Count : 0;
            if (min >= 0 && count < min) return false;
            if (max >= 0 && count > max) return false;
            return true;
        }
    }

    /// <summary>城市太守是否为指定武将（读 City.Leader）</summary>
    public class CityGovernorIsCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>武将槽位 Key</summary>
        string personSlot = "ActionPerson";

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            personSlot = p.Value<string>("PersonSlot") ?? "ActionPerson";
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            Person person = EventConditionHelper.GetSlot<Person>(database, personSlot);
            if (city == null || person == null) return false;
            return city.Leader == person;
        }
    }

    /// <summary>城市的邻接城市中是否有指定势力（读 City.NeighborList）</summary>
    public class CityNeighborBelongForceCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>势力槽位 Key</summary>
        string forceSlot = "TargetForce";

        /// <summary>期望值（true = 存在，false = 不存在）</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            forceSlot = p.Value<string>("ForceSlot") ?? "TargetForce";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            Force force = EventConditionHelper.GetSlot<Force>(database, forceSlot);
            if (city == null || force == null || city.NeighborList == null) return false;

            bool found = false;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City neighbor = city.NeighborList.Get(i);
                if (neighbor != null && neighbor.BelongForce == force)
                {
                    found = true;
                    break;
                }
            }
            return found == expect;
        }
    }

    /// <summary>
    /// 城市是否处于前线（邻接城市里存在"不属于本城势力"的城市）。
    /// 这是"边境城池"的近似判定：只看一阶邻接，不做连通性分析。
    /// </summary>
    public class CityIsFrontlineCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>期望值（true = 是前线）</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            if (city == null || city.NeighborList == null) return false;

            Force self = city.BelongForce;
            bool frontline = false;
            for (int i = 0; i < city.NeighborList.Count; i++)
            {
                City neighbor = city.NeighborList.Get(i);
                if (neighbor == null) continue;
                if (self == null || neighbor.BelongForce != self)
                {
                    frontline = true;
                    break;
                }
            }
            return frontline == expect;
        }
    }

    /// <summary>城市是否已有建筑（读 City.allBuildings）</summary>
    public class CityHasBuildingCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>期望值（true = 至少有一座）</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            if (city == null) return false;

            bool has = city.allBuildings != null && city.allBuildings.Count > 0;
            return has == expect;
        }
    }

    /// <summary>
    /// 城市兵力占上限的百分比区间检查。
    /// 用"百分比"而不是绝对值，是因为各城的兵营上限差异很大（关港 vs 大城），
    /// 写死绝对值的历史事件换个剧本就会失效。
    /// </summary>
    public class CityTroopsRatioCheck : Condition
    {
        /// <summary>城市槽位 Key</summary>
        string slot;

        /// <summary>百分比下限（-1 = 不限）</summary>
        int minPercent = -1;

        /// <summary>百分比上限（-1 = 不限）</summary>
        int maxPercent = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionCity";
            minPercent = p.Value<int?>("MinPercent") ?? -1;
            maxPercent = p.Value<int?>("MaxPercent") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            City city = EventConditionHelper.GetSlot<City>(database, slot);
            if (city == null) return false;

            int limit = city.TroopsLimit;
            if (limit <= 0) return false;

            int percent = city.troops * 100 / limit;
            if (minPercent >= 0 && percent < minPercent) return false;
            if (maxPercent >= 0 && percent > maxPercent) return false;
            return true;
        }
    }

    // ========================================================================
    // 三、武将类（11 个）
    // ========================================================================

    /// <summary>武将是否拥有指定特技（读 Person.FeatureList）</summary>
    public class PersonHasFeatureCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>特技 Id</summary>
        int featureId;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            featureId = p.Value<int>("FeatureId");
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null || person.FeatureList == null) return false;

            bool has = featureId > 0 && person.FeatureList.Contains(featureId);
            return has == expect;
        }
    }

    /// <summary>
    /// 武将兵种适性区间检查。
    /// Kind 取值：spear / halberd / crossbow / ride / water / machine。
    /// 读的是 baseValue（适性等级原值），与 UI 上的等级显示一致。
    /// </summary>
    public class PersonAbilityCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>适性种类</summary>
        string kind = "spear";

        /// <summary>下限（-1 = 不限）</summary>
        int min = -1;

        /// <summary>上限（-1 = 不限）</summary>
        int max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            kind = (p.Value<string>("Kind") ?? "spear").ToLowerInvariant();
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;

            int level;
            switch (kind)
            {
                case "spear": level = person.spearLv != null ? person.spearLv.baseValue : 0; break;
                case "halberd": level = person.halberdLv != null ? person.halberdLv.baseValue : 0; break;
                case "crossbow": level = person.crossbowLv != null ? person.crossbowLv.baseValue : 0; break;
                case "ride": level = person.rideLv != null ? person.rideLv.baseValue : 0; break;
                case "water": level = person.waterLv != null ? person.waterLv.baseValue : 0; break;
                case "machine": level = person.machineLv != null ? person.machineLv.baseValue : 0; break;
                default: return false;
            }

            if (min >= 0 && level < min) return false;
            if (max >= 0 && level > max) return false;
            return true;
        }
    }

    /// <summary>武将官职检查（读 Person.OfficialId）</summary>
    public class PersonOfficialCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>官职 Id</summary>
        int officialId;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            officialId = p.Value<int>("OfficialId");
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return (person.OfficialId == officialId) == expect;
        }
    }

    /// <summary>武将是否为俘虏</summary>
    public class PersonIsPrisonerCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return person.IsPrisoner == expect;
        }
    }

    /// <summary>武将是否在某支部队中（读 Person.mBelongTroop）</summary>
    public class PersonIsInTroopCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return (person.mBelongTroop != null) == expect;
        }
    }

    /// <summary>
    /// 武将是否"空闲可接工作"（读 Person.IsFree：不在部队、无任务、非俘虏、未死亡）。
    /// 用于"只有闲着的武将才能参与某剧情"这类前置判断。
    /// </summary>
    public class PersonIsFreeCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return person.IsFree == expect;
        }
    }

    /// <summary>
    /// 两名武将的人际关系检查。
    /// Relation 取值：father / mother / brother / spouse / like / hate。
    /// 一律用"槽位里存的对象引用"判断，而不是比 Id——
    /// 因为 SpouseList / LikePersonList 在存档载入后是对象列表，比 Id 需要在集合里线性找。
    /// </summary>
    public class PersonRelationCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>另一武将槽位 Key</summary>
        string otherSlot = "TargetPerson";

        /// <summary>关系类型</summary>
        string relation = "brother";

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            otherSlot = p.Value<string>("OtherSlot") ?? "TargetPerson";
            relation = (p.Value<string>("Relation") ?? "brother").ToLowerInvariant();
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person a = EventConditionHelper.GetSlot<Person>(database, slot);
            Person b = EventConditionHelper.GetSlot<Person>(database, otherSlot);
            if (a == null || b == null || a == b) return false;

            switch (relation)
            {
                case "father": return a.Father == b || b.Father == a;
                case "mother": return a.Mother == b || b.Mother == a;
                case "brother": return a.Brother == b || b.Brother == a;
                case "spouse": return Contains(a.SpouseList, b);
                case "like": return Contains(a.LikePersonList, b);
                case "hate": return Contains(a.HatePersonList, b);
                default: return false;
            }
        }

        /// <summary>在对象列表里找指定武将</summary>
        /// <param name="list">武将列表</param>
        /// <param name="person">目标武将</param>
        /// <returns>是否包含</returns>
        static bool Contains(SangoObjectList<Person> list, Person person)
        {
            if (list == null || person == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list.Get(i) == person) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 是否已经属于某个「兄弟组」。
    ///
    /// 判据统一走 <see cref="Person.HasSwornBrother"/>：
    ///   · 剧本原设的兄弟（BrotherId / Brother 字段）；
    ///   · 结义产物（Person.SwornBrothers 写入的 BrotherId + 共享 BrotherList）。
    /// 为什么不自己拼 Brother/BrotherList 判断：BrotherList 不保证对称
    /// （见 Person.IsBrother 的注释），HasSwornBrother 才是项目里唯一可靠的"是否已有兄弟"口径，
    /// PersonJieyi（仲介结义）的候选人过滤也用的是它，两边判定保持一致。
    ///
    /// 典型用法：桃园结义这类事件要求三人「都还没有兄弟」才触发，
    /// 写三条 Expect=false 即可（比逐个两两比对兄弟关系更准确，也覆盖了三人各有别人的情况）。
    ///
    /// 参数：Slot（武将槽位 Key）、Expect（默认 true = 期望已有兄弟组）。
    /// </summary>
    public class PersonHasBrotherCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>期望值：true = 期望已有兄弟组</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查是否已有兄弟组</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否与期望一致</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            // 槽位没绑定（人物未登场 / Id 写错）时一律不成立，避免"没数据就当成没兄弟"误触发结义
            if (person == null) return false;
            return person.HasSwornBrother == expect;
        }
    }

    /// <summary>武将年龄区间检查（读 Person.Age）</summary>
    public class PersonAgeCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>下限（-1 = 不限）</summary>
        int min = -1;

        /// <summary>上限（-1 = 不限）</summary>
        int max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;

            if (min >= 0 && person.Age < min) return false;
            if (max >= 0 && person.Age > max) return false;
            return true;
        }
    }

    /// <summary>武将性别检查（Person.sex：0 = 男，1 = 女，见字段注释）</summary>
    public class PersonGenderCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>是否要求为女性</summary>
        bool female;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            female = p.Value<bool?>("Female") ?? false;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return (person.sex == 1) == female;
        }
    }

    /// <summary>武将理想检查（读 Person.ideal）</summary>
    public class PersonIdealCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>理想 Id</summary>
        int ideal;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            ideal = p.Value<int>("Ideal");
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;
            return person.ideal == ideal;
        }
    }

    /// <summary>武将伤病值区间检查（读 Person.injury）</summary>
    public class PersonInjuryCheck : Condition
    {
        /// <summary>武将槽位 Key</summary>
        string slot;

        /// <summary>下限（-1 = 不限）</summary>
        int min = -1;

        /// <summary>上限（-1 = 不限）</summary>
        int max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionPerson";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = EventConditionHelper.GetSlot<Person>(database, slot);
            if (person == null) return false;

            if (min >= 0 && person.injury < min) return false;
            if (max >= 0 && person.injury > max) return false;
            return true;
        }
    }

    // ========================================================================
    // 四、部队类（5 个）
    // ========================================================================

    /// <summary>
    /// 部队兵种检查。
    /// Water = true 时比对水上兵种（WaterTroopTypeId），否则比对陆上兵种（LandTroopTypeId）。
    /// </summary>
    public class TroopTypeCheck : Condition
    {
        /// <summary>部队槽位 Key</summary>
        string slot;

        /// <summary>兵种 Id</summary>
        int typeId;

        /// <summary>是否比对水上兵种</summary>
        bool water;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionTroop";
            typeId = p.Value<int>("TypeId");
            water = p.Value<bool?>("Water") ?? false;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = EventConditionHelper.GetSlot<Troop>(database, slot);
            if (troop == null) return false;
            return water ? troop.WaterTroopTypeId == typeId : troop.LandTroopTypeId == typeId;
        }
    }

    /// <summary>部队是否持有指定战法（遍历 troop.skills，陆/水自动按当前地形取）</summary>
    public class TroopSkillCheck : Condition
    {
        /// <summary>部队槽位 Key</summary>
        string slot;

        /// <summary>战法 Id</summary>
        int skillId;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionTroop";
            skillId = p.Value<int>("SkillId");
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = EventConditionHelper.GetSlot<Troop>(database, slot);
            if (troop == null) return false;

            bool has = false;
            var list = troop.skills;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].Id == skillId)
                    {
                        has = true;
                        break;
                    }
                }
            }
            return has == expect;
        }
    }

    /// <summary>部队行动力区间检查（读 Troop.energy）</summary>
    public class TroopEnergyCheck : Condition
    {
        /// <summary>部队槽位 Key</summary>
        string slot;

        /// <summary>下限（-1 = 不限）</summary>
        int min = -1;

        /// <summary>上限（-1 = 不限）</summary>
        int max = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionTroop";
            min = p.Value<int?>("Min") ?? -1;
            max = p.Value<int?>("Max") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = EventConditionHelper.GetSlot<Troop>(database, slot);
            if (troop == null) return false;

            if (min >= 0 && troop.energy < min) return false;
            if (max >= 0 && troop.energy > max) return false;
            return true;
        }
    }

    /// <summary>部队士气占上限的百分比区间检查（分母取 Troop.MaxMorale）</summary>
    public class TroopMoraleRatioCheck : Condition
    {
        /// <summary>部队槽位 Key</summary>
        string slot;

        /// <summary>百分比下限（-1 = 不限）</summary>
        int minPercent = -1;

        /// <summary>百分比上限（-1 = 不限）</summary>
        int maxPercent = -1;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionTroop";
            minPercent = p.Value<int?>("MinPercent") ?? -1;
            maxPercent = p.Value<int?>("MaxPercent") ?? -1;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否落在区间内</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = EventConditionHelper.GetSlot<Troop>(database, slot);
            if (troop == null) return false;

            int max = troop.MaxMorale;
            if (max <= 0) return false;

            int percent = troop.morale * 100 / max;
            if (minPercent >= 0 && percent < minPercent) return false;
            if (maxPercent >= 0 && percent > maxPercent) return false;
            return true;
        }
    }

    /// <summary>部队是否位于水上（读 Troop.IsInWater，按所在格地形判断）</summary>
    public class TroopIsInWaterCheck : Condition
    {
        /// <summary>部队槽位 Key</summary>
        string slot;

        /// <summary>期望值</summary>
        bool expect = true;

        /// <summary>初始化</summary>
        /// <param name="p">条件参数</param>
        /// <param name="sangoObjects">相关对象（未用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            slot = p.Value<string>("Slot") ?? "ActionTroop";
            expect = p.Value<bool?>("Expect") ?? true;
        }

        /// <summary>检查</summary>
        /// <param name="database">条件数据库</param>
        /// <returns>是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop troop = EventConditionHelper.GetSlot<Troop>(database, slot);
            if (troop == null) return false;
            return troop.IsInWater == expect;
        }
    }
}
