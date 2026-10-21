/*
 * 文件名：EventEffectsPersonExt.cs
 * 描述：武将类事件效果（第二批）——五维、适性、特技、官职、关系、伤病
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【与 EventEffectsPerson.cs 的关系】
 *   EventEffectsPerson.cs 已有忠诚 / 登用 / 结义 / 登场 / 死亡 / 归属变更；
 *   本文件补的是"成长与关系"，是史实事件里出现频率最高的一类
 *   （"关羽武力+1""孔明之智"之类）。
 *
 * 【为什么改 baseValue 而不是最终值】
 *   五维的最终值是 <c>ApplyInjuryDecay(RawCommand, injury)</c> 算出来的派生量，
 *   直接写最终值没有落点。项目内所有编辑入口（PersonSortFunction / UIPersonEdit）
 *   都是"改 baseValue + 调一次 Update"，本文件沿用同一口径。
 *
 * 【尚未实现】
 *   · 兵种适性的经验值：PersonAbilityValue 有独立经验体系，直接加经验要走 SetExp，
 *     本文件只改等级原值（baseValue），避免两套成长口径打架。
 *   · 跨势力直接改归属：目前只通过 BeRecruit 表达（见 EventEffectsPerson.cs 的说明），
 *     直接改 BelongForce 需要同步军团 / 城池 / 三份名单，风险过高。
 *   · 装备替换：EquippedWeaponId 等字段可写，但换装后需要重算攻防与特技加成，
 *     引擎侧的入口尚未核实。
 */

namespace Sango.Core.Event
{
    /// <summary>增减武将五维基础值（统御 / 武力 / 智力 / 政治 / 魅力）</summary>
    [EventEffectMeta("武将属性增减", "增减武将五维基础值；Kind 取 command/strength/intelligence/politics/glamour", "PersonSlot", "Kind", "Value")]
    public class PersonAddAttributeEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            string kind = (GetString("Kind", "command") ?? "command").ToLowerInvariant();
            PersonAttributeValue attr;
            switch (kind)
            {
                case "command": attr = person.command; break;
                case "strength": attr = person.strength; break;
                case "intelligence": attr = person.intelligence; break;
                case "politics": attr = person.politics; break;
                case "glamour": attr = person.glamour; break;
                default:
                    Log.Warning($"事件：武将属性增减失败 —— 未知的 Kind={kind}");
                    return false;
            }

            if (attr == null)
            {
                Log.Warning($"事件：{person.Name} 的 {kind} 属性对象为空，无法增减");
                return false;
            }

            int old = attr.baseValue;
            attr.baseValue = old + value;
            if (attr.baseValue < 0) attr.baseValue = 0;
            // 与 PersonSortFunction / UIPersonEdit 的编辑口径一致：改完原值重算一次
            attr.UpdateNoAge();

            Log.Info($"事件：{person.Name} 的 {kind} {value:+#;-#;0} → {attr.baseValue}");
            return true;
        }
    }

    /// <summary>增减武将兵种适性等级（原始等级，不是经验）</summary>
    [EventEffectMeta("武将适性增减", "增减兵种适性等级；Kind 取 spear/halberd/crossbow/ride/water/machine", "PersonSlot", "Kind", "Value")]
    public class PersonAddAbilityEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            string kind = (GetString("Kind", "spear") ?? "spear").ToLowerInvariant();
            PersonAbilityValue ability;
            switch (kind)
            {
                case "spear": ability = person.spearLv; break;
                case "halberd": ability = person.halberdLv; break;
                case "crossbow": ability = person.crossbowLv; break;
                case "ride": ability = person.rideLv; break;
                case "water": ability = person.waterLv; break;
                case "machine": ability = person.machineLv; break;
                default:
                    Log.Warning($"事件：武将适性增减失败 —— 未知的 Kind={kind}");
                    return false;
            }

            if (ability == null)
            {
                Log.Warning($"事件：{person.Name} 的 {kind} 适性对象为空，无法增减");
                return false;
            }

            int old = ability.baseValue;
            ability.baseValue = old + value;
            if (ability.baseValue < 0) ability.baseValue = 0;
            // 适性对象的刷新入口是无参 Update（见 UIPersonEdit 的用法）
            ability.Update();

            Log.Info($"事件：{person.Name} 的 {kind} 适性 {value:+#;-#;0} → {ability.baseValue}");
            return true;
        }
    }

    /// <summary>设置武将伤病值（0 = 痊愈）</summary>
    [EventEffectMeta("设置武将伤病", "直接设置武将的伤病值，0 表示痊愈", "PersonSlot", "Value")]
    public class PersonSetInjuryEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value < 0) value = 0;

            int old = person.injury;
            person.injury = value;
            Log.Info($"事件：{person.Name} 伤病 {old} → {value}");
            return true;
        }
    }

    /// <summary>给武将授予特技（从 CommonData.Features 里取）</summary>
    [EventEffectMeta("授予特技", "给武将添加一个特技（已拥有则跳过）", "PersonSlot", "FeatureId")]
    public class PersonAddFeatureEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int featureId = EvalInt("FeatureId", context);
            if (featureId <= 0)
            {
                Log.Warning($"事件：授予特技失败 —— FeatureId 非法（{featureId}）");
                return false;
            }

            Feature feature = GetFeature(featureId);
            if (feature == null)
            {
                Log.Warning($"事件：授予特技失败 —— CommonData.Features 里找不到 Id={featureId}");
                return false;
            }

            if (person.FeatureList == null)
            {
                Log.Warning($"事件：{person.Name} 的特技列表为空，无法授予特技（可能是尚未完成初始化的对象）");
                return false;
            }

            if (person.FeatureList.Contains(featureId))
            {
                Log.Info($"事件：{person.Name} 已拥有特技 {feature.Name}，本次授予跳过");
                return true;
            }

            person.FeatureList.Add(feature);
            // 存档字段是 Id 数组（Person.FeatureListId），这里立即同步，避免"运行期有、读档后没了"
            person.FeatureListId = person.FeatureList.ToArray();

            Log.Info($"事件：{person.Name} 获得特技 {feature.Name}");
            return true;
        }

        /// <summary>按 Id 取特技定义</summary>
        /// <param name="featureId">特技 Id</param>
        /// <returns>特技对象；取不到返回 null</returns>
        internal static Feature GetFeature(int featureId)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Features == null)
                return null;
            return scenario.CommonData.Features.Get(featureId);
        }
    }

    /// <summary>移除武将特技</summary>
    [EventEffectMeta("移除特技", "移除武将的一个特技（没有则跳过）", "PersonSlot", "FeatureId")]
    public class PersonRemoveFeatureEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int featureId = EvalInt("FeatureId", context);
            if (featureId <= 0 || person.FeatureList == null)
            {
                Log.Warning($"事件：移除特技失败 —— FeatureId 非法或特技列表为空（{featureId}）");
                return false;
            }

            Feature feature = person.FeatureList.Find(featureId);
            if (feature == null)
            {
                Log.Info($"事件：{person.Name} 没有特技 Id={featureId}，本次移除跳过");
                return true;
            }

            person.FeatureList.Remove(feature);
            person.FeatureListId = person.FeatureList.ToArray();

            Log.Info($"事件：{person.Name} 失去特技 {feature.Name}");
            return true;
        }
    }

    /// <summary>设置武将官职（同时写 OfficialId 与对象引用）</summary>
    [EventEffectMeta("设置官职", "把武将的官职设为指定 Id（0 表示免官）", "PersonSlot", "OfficialId")]
    public class PersonSetOfficialEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int officialId = EvalInt("OfficialId", context);
            if (officialId < 0) officialId = 0;

            Official official = null;
            Scenario scenario = Scenario.Cur;
            if (officialId > 0 && scenario != null && scenario.CommonData != null && scenario.CommonData.Officials != null)
                official = scenario.CommonData.Officials.Get(officialId);

            person.OfficialId = officialId;
            person.Official = official;

            Log.Info(official != null
                ? $"事件：{person.Name} 的官职设为 {official.Name}"
                : $"事件：{person.Name} 的官职被清空（Id={officialId}）");
            return true;
        }
    }

    /// <summary>让两名武将互相成为"亲爱"关系</summary>
    [EventEffectMeta("添加亲爱关系", "让两名武将互相成为亲爱武将（影响援助与登用）", "PersonSlot", "OtherSlot")]
    public class PersonAddLikeEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            return AddRelation(context, true);
        }

        /// <summary>双向添加关系并同步 Id 数组</summary>
        /// <param name="context">上下文</param>
        /// <param name="like">true = 亲爱，false = 厌恶</param>
        /// <returns>是否成功</returns>
        internal static bool AddRelation(EventContext context, bool like)
        {
            Person a = context != null ? context.GetSlot<Person>("PersonSlot") : null;
            Person b = context != null ? context.GetSlot<Person>("OtherSlot") : null;

            if (a == null || b == null || a == b)
            {
                Log.Warning("事件：添加武将关系失败 —— PersonSlot / OtherSlot 未绑定或指向同一人");
                return false;
            }

            if (like)
            {
                if (a.LikePersonList == null || b.LikePersonList == null) return false;
                if (!Contains(a.LikePersonList, b)) a.LikePersonList.Add(b);
                if (!Contains(b.LikePersonList, a)) b.LikePersonList.Add(a);
                a.LikePersonListId = a.LikePersonList.ToArray();
                b.LikePersonListId = b.LikePersonList.ToArray();
            }
            else
            {
                if (a.HatePersonList == null || b.HatePersonList == null) return false;
                if (!Contains(a.HatePersonList, b)) a.HatePersonList.Add(b);
                if (!Contains(b.HatePersonList, a)) b.HatePersonList.Add(a);
                a.HatePersonListId = a.HatePersonList.ToArray();
                b.HatePersonListId = b.HatePersonList.ToArray();
            }

            Log.Info(like
                ? $"事件：{a.Name} 与 {b.Name} 结为亲爱关系"
                : $"事件：{a.Name} 与 {b.Name} 结下仇怨");
            return true;
        }

        /// <summary>判断列表里是否已有该武将（内部工具，供夫妻关系复用）</summary>
        /// <param name="list">武将列表</param>
        /// <param name="person">目标武将</param>
        /// <returns>是否已存在</returns>
        internal static bool Contains(SangoObjectList<Person> list, Person person)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list.Get(i) == person) return true;
            }
            return false;
        }
    }

    /// <summary>让两名武将互相成为"厌恶"关系</summary>
    [EventEffectMeta("添加厌恶关系", "让两名武将互相成为厌恶武将（影响登用与舌战）", "PersonSlot", "OtherSlot")]
    public class PersonAddHateEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            return PersonAddLikeEffect.AddRelation(context, false);
        }
    }

    /// <summary>让两名武将结为夫妻（双向写入，并同步 Id 数组）</summary>
    [EventEffectMeta("结为夫妻", "让两名武将结为配偶（双向关系）", "PersonSlot", "OtherSlot")]
    public class PersonSetSpouseEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person a = GetSlot<Person>("PersonSlot", context);
            Person b = GetSlot<Person>("OtherSlot", context);

            if (a == null || b == null || a == b)
            {
                Log.Warning("事件：结为夫妻失败 —— PersonSlot / OtherSlot 未绑定或指向同一人");
                return false;
            }
            if (a.SpouseList == null || b.SpouseList == null)
            {
                Log.Warning($"事件：结为夫妻失败 —— {a.Name} 或 {b.Name} 的配偶列表为空");
                return false;
            }

            if (!PersonAddLikeEffect.Contains(a.SpouseList, b)) a.SpouseList.Add(b);
            if (!PersonAddLikeEffect.Contains(b.SpouseList, a)) b.SpouseList.Add(a);

            a.SpouseListId = a.SpouseList.ToArray();
            b.SpouseListId = b.SpouseList.ToArray();

            Log.Info($"事件：{a.Name} 与 {b.Name} 结为夫妻");
            return true;
        }
    }

    /// <summary>治疗武将（把伤病清零，等价于 PersonSetInjury 的常用特例）</summary>
    [EventEffectMeta("治疗武将", "把武将的伤病值清零（华佗医治、伤愈复出）", "PersonSlot")]
    public class PersonHealEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int old = person.injury;
            person.injury = 0;
            Log.Info($"事件：{person.Name} 伤病 {old} → 0（痊愈）");
            return true;
        }
    }

    /// <summary>
    /// 设置武将的父亲。
    ///
    /// 只写单向（child.Father）。Person 没有"子女列表"字段，
    /// 反向的父子关系由引擎在需要时通过遍历全量武将查找 FatherId 得到，
    /// 因此这里不存在"两边都要写"的一致性问题。
    /// </summary>
    [EventEffectMeta("设置父亲", "把指定武将设为目标武将的父亲（用于血缘剧情）", "PersonSlot", "OtherSlot")]
    public class PersonSetFatherEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            Person father = GetSlot<Person>("OtherSlot", context);

            if (person == null || father == null || person == father)
            {
                Log.Warning("事件：设置父亲失败 —— PersonSlot / OtherSlot 未绑定或指向同一人");
                return false;
            }

            person.FatherId = father.Id;
            person.Father = father;

            Log.Info($"事件：{person.Name} 的父亲设为 {father.Name}");
            return true;
        }
    }

    /// <summary>
    /// 设置武将的兄弟。
    ///
    /// 与父亲不同，兄弟是**双向**的：只写一边会出现"甲认乙为兄、乙不认甲"，
    /// 而援助攻击 / 结义判定两边都会读，单向一定会漏一半效果，因此两边都写。
    /// 注意 Brother 是单值字段（多兄弟请用 BrotherList / 结义 SwornBrothers）。
    /// </summary>
    [EventEffectMeta("设置兄弟", "让两名武将互为兄弟（双向写入）", "PersonSlot", "OtherSlot")]
    public class PersonSetBrotherEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person a = GetSlot<Person>("PersonSlot", context);
            Person b = GetSlot<Person>("OtherSlot", context);

            if (a == null || b == null || a == b)
            {
                Log.Warning("事件：设置兄弟失败 —— PersonSlot / OtherSlot 未绑定或指向同一人");
                return false;
            }

            a.BrotherId = b.Id;
            a.Brother = b;
            b.BrotherId = a.Id;
            b.Brother = a;

            Log.Info($"事件：{a.Name} 与 {b.Name} 互为兄弟");
            return true;
        }
    }

    /// <summary>设置武将等级（同时写 LevelId 与对象引用）</summary>
    [EventEffectMeta("设置等级", "把武将等级设为指定 Id（从 CommonData.PersonLevels 取）", "PersonSlot", "LevelId")]
    public class PersonSetLevelEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("PersonSlot", context);
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int levelId = EvalInt("LevelId", context);
            if (levelId <= 0)
            {
                Log.Warning($"事件：设置等级失败 —— LevelId 非法（{levelId}）");
                return false;
            }

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.PersonLevels == null)
            {
                Log.Warning("事件：设置等级失败 —— 剧本未加载，取不到等级表");
                return false;
            }

            PersonLevel level = scenario.CommonData.PersonLevels.Get(levelId);
            if (level == null)
            {
                Log.Warning($"事件：设置等级失败 —— CommonData.PersonLevels 里找不到 Id={levelId}");
                return false;
            }

            person.LevelId = levelId;
            person.Level = level;

            Log.Info($"事件：{person.Name} 的等级设为 {level.Name}");
            return true;
        }
    }

    /// <summary>武将类效果（第二批）的批量注册入口</summary>
    public static class EventEffectsPersonExt
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("PersonAddAttribute", EventEffectBase.CreateHandle<PersonAddAttributeEffect>);
            EventEffectBase.Register("PersonAddAbility", EventEffectBase.CreateHandle<PersonAddAbilityEffect>);
            EventEffectBase.Register("PersonSetInjury", EventEffectBase.CreateHandle<PersonSetInjuryEffect>);
            EventEffectBase.Register("PersonAddFeature", EventEffectBase.CreateHandle<PersonAddFeatureEffect>);
            EventEffectBase.Register("PersonRemoveFeature", EventEffectBase.CreateHandle<PersonRemoveFeatureEffect>);
            EventEffectBase.Register("PersonSetOfficial", EventEffectBase.CreateHandle<PersonSetOfficialEffect>);
            EventEffectBase.Register("PersonAddLike", EventEffectBase.CreateHandle<PersonAddLikeEffect>);
            EventEffectBase.Register("PersonAddHate", EventEffectBase.CreateHandle<PersonAddHateEffect>);
            EventEffectBase.Register("PersonSetSpouse", EventEffectBase.CreateHandle<PersonSetSpouseEffect>);
            EventEffectBase.Register("PersonHeal", EventEffectBase.CreateHandle<PersonHealEffect>);
            EventEffectBase.Register("PersonSetFather", EventEffectBase.CreateHandle<PersonSetFatherEffect>);
            EventEffectBase.Register("PersonSetBrother", EventEffectBase.CreateHandle<PersonSetBrotherEffect>);
            EventEffectBase.Register("PersonSetLevel", EventEffectBase.CreateHandle<PersonSetLevelEffect>);
        }
    }
}
