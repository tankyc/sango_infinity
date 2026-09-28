/*
 * 文件名：EventEffectsPerson.cs
 * 描述：武将类事件效果——忠诚、登用、结义、登场、死亡、归属变更
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>把武将忠诚设为指定值（0~100 之间夹取）</summary>
    [EventEffectMeta("设置武将忠诚", "把忠诚直接设为指定值（建议用 GrowthPlace 键而非写死数字）", "Slot", "Value")]
    public class PersonSetLoyaltyEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            int value = EvalInt("Value", context);
            person.loyalty = Clamp(value, 0, 100);
            Log.Info($"事件：{person.Name} 忠诚设为 {person.loyalty}");
            return true;
        }

        /// <summary>把值夹到区间内</summary>
        /// <param name="value">原值</param>
        /// <param name="min">下限</param>
        /// <param name="max">上限</param>
        /// <returns>夹取结果</returns>
        internal static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>增减武将忠诚（结果夹在 0~100）</summary>
    [EventEffectMeta("增减武将忠诚", "在当前忠诚基础上增减，结果夹在 0~100", "Slot", "Value")]
    public class PersonAddLoyaltyEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            int delta = EvalInt("Value", context);
            person.loyalty = PersonSetLoyaltyEffect.Clamp(person.loyalty + delta, 0, 100);
            Log.Info($"事件：{person.Name} 忠诚 {delta:+#;-#;0} → {person.loyalty}");
            return true;
        }
    }

    /// <summary>
    /// 登用武将：让目标加入登用者的势力。
    ///
    /// 走项目既有的 <see cref="Person.BeRecruit"/>，它会一并置忠诚、清在野 / 俘虏名单、
    /// 设置归属城池与势力，因此不要手写这些字段（容易漏项）。
    /// </summary>
    [EventEffectMeta("登用武将", "让目标武将加入指定势力的指定城池（三顾茅庐、招降等）",
        "Slot", "RecruitorSlot", "CitySlot", "ForceSlot")]
    public class PersonRecruitEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person target = GetSlot<Person>("Slot", context);
            if (target == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            Person recruitor = GetSlot<Person>("RecruitorSlot", context);
            Force force = GetSlot<Force>("ForceSlot", context);
            City city = GetSlot<City>("CitySlot", context);

            // 登用者缺失时用势力君主兜底
            if (recruitor == null && force != null) recruitor = force.mGovernor;
            if (force == null && recruitor != null) force = recruitor.BelongForce;
            if (force == null)
            {
                Log.Warning($"登用 {target.Name} 失败：既没有登用者也没有目标势力");
                return false;
            }

            // 加入城池：优先显式指定，其次登用者所在城，最后势力主城
            if (city == null) city = recruitor != null ? recruitor.BelongCity : null;
            if (city == null) city = force.CapitalCity;
            if (city == null)
            {
                Log.Warning($"登用 {target.Name} 失败：找不到可加入的城池");
                return false;
            }

            target.BeRecruit(recruitor, city);
            Log.Info($"事件：{target.Name} 加入了 {force.Name}（{city.Name}）");
            return true;
        }
    }

    /// <summary>
    /// 结义：把若干武将结为异姓兄弟（桃园结义）。
    ///
    /// 走项目既有的 <see cref="Person.SwornBrothers"/>，收益由引擎自动生效：
    /// 义兄弟不会掉忠诚、援助攻击概率 50%、兄弟同心加气。
    /// 注意判断"是否同组"一律用 IsBrotherGroupmate（IsBrother 不保证对称）。
    /// </summary>
    [EventEffectMeta("结义", "把多个武将结为异姓兄弟，带来不掉忠/援助/同心加气等收益", "Slots")]
    public class PersonSwornBrothersEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            if (context == null || parameters == null) return false;

            JArray slotKeys = parameters["Slots"] as JArray;
            if (slotKeys == null || slotKeys.Count < 2)
            {
                Log.Warning("结义效果需要至少两个槽位（Slots 数组）");
                return false;
            }

            List<Person> members = new List<Person>();
            for (int i = 0; i < slotKeys.Count; i++)
            {
                string key = slotKeys[i].Value<string>();
                Person person = context.GetSlot<Person>(key);
                if (person == null)
                {
                    Log.Warning($"结义失败：槽位 {key} 未绑定武将");
                    return false;
                }
                if (!members.Contains(person)) members.Add(person);
            }

            if (members.Count < 2)
            {
                Log.Warning("结义失败：有效武将不足两人");
                return false;
            }

            Person.SwornBrothers(members);

            // 用 IsBrotherGroupmate 回读校验（该判定对称，是项目里唯一可靠的"同组"判断）
            bool ok = members[0].IsBrotherGroupmate(members[1]);
            string names = string.Join("、", members.ConvertAll(p => p.Name).ToArray());
            Log.Info($"事件：{names} 结为异姓兄弟（校验{(ok ? "通过" : "失败")}）");
            return ok;
        }
    }

    /// <summary>
    /// 让未登场的武将登场（出现在指定城市，处于在野状态）。
    /// 走 <see cref="Person.Appear"/>，会一并登记城市的在野名单。
    /// </summary>
    [EventEffectMeta("武将登场", "让未登场的武将出现在指定城市（在野状态）", "Slot", "CitySlot")]
    public class PersonAppearEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            City city = GetSlot<City>("CitySlot", context);

            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            return person.Appear(city);
        }
    }

    /// <summary>
    /// 让武将变为"已发现"。未发现（Invisible）的武将玩家看不到也找不到，
    /// 指定 CitySlot 时按登场处理（落到该城的在野名单）。
    /// </summary>
    [EventEffectMeta("发现武将", "把未发现的武将变为可见；给了 CitySlot 就落到该城在野名单", "Slot", "CitySlot")]
    public class PersonSetVisibleEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            City city = GetSlot<City>("CitySlot", context);
            if (city != null && !person.IsValid)
                return person.Appear(city);

            if (person.Invisible)
            {
                person.state = (int)PersonStateType.Unemployed;
                Log.Info($"事件：{person.Name} 已被发现");
            }
            return true;
        }
    }

    /// <summary>处决武将（斩首 / 赐死），并广播处决事件</summary>
    [EventEffectMeta("处决武将", "斩杀或赐死目标武将，会广播 OnPersonExecute", "Slot", "KillerSlot")]
    public class PersonExecuteEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            Person killer = GetSlot<Person>("KillerSlot", context);
            Force killerForce = killer != null ? killer.BelongForce : null;

            person.Dead();
            GameEvent.OnPersonExecute?.Invoke(person, killerForce);
            Log.Info($"事件：{person.Name} 被处决");
            return true;
        }
    }

    /// <summary>
    /// 武将死亡（非处决）。
    /// Cause 区分死法（Battle / Illness / Suicide / Assassinate），
    /// 注意引擎目前尚未按 Cause 区分后果，事件可用台词自行表现。
    /// </summary>
    [EventEffectMeta("武将死亡", "让武将死亡；Cause 表示死法（战死/病逝/自刎/遇刺）", "Slot", "Cause")]
    public class PersonDieEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            string cause = GetString("Cause", "Natural");
            person.Dead();
            Log.Info($"事件：{person.Name} 死亡（{cause}）");
            return true;
        }
    }

    /// <summary>释放武将（解除俘虏身份，回到原势力或成为在野）</summary>
    [EventEffectMeta("释放武将", "解除目标武将的俘虏身份", "Slot")]
    public class PersonReleaseEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            Force releaseForce = GetSlot<Force>("ForceSlot", context);
            if (releaseForce == null) releaseForce = person.BelongForce;

            // 与 PersonRecruit.ReleaseTarget 同一动作：解除俘虏身份并回城待命
            person.SetMission(MissionType.PersonReturn, person.BelongCity);
            GameEvent.OnPersonRelease?.Invoke(person, releaseForce);
            Log.Info($"事件：{person.Name} 被释放");
            return true;
        }
    }

    /// <summary>让武将下野（脱离势力，落到所在城的在野名单）</summary>
    [EventEffectMeta("武将下野", "让武将脱离势力成为在野", "Slot")]
    public class PersonLeaveToWildEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            person.LeaveToWild();
            return true;
        }
    }

    /// <summary>把武将迁移到指定城市（在城内待命）</summary>
    [EventEffectMeta("武将迁城", "把武将调到指定城市", "Slot", "CitySlot")]
    public class PersonChangeCurrentCityEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            City city = GetSlot<City>("CitySlot", context);

            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            person.ChangeCurrentCity(city);
            Log.Info($"事件：{person.Name} 迁往 {city.Name}");
            return true;
        }
    }

    /// <summary>给武将加功绩</summary>
    [EventEffectMeta("武将加功绩", "给武将增加功绩（数值建议走 GainPlace 键）", "Slot", "Value")]
    public class PersonAddMeritEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            person.GainMerit(value);
            Log.Info($"事件：{person.Name} 功绩 +{value}");
            return true;
        }
    }

    /// <summary>给武将加武将经验</summary>
    [EventEffectMeta("武将加经验", "给武将增加武将经验（数值建议走 GainPlace 键）", "Slot", "Value")]
    public class PersonAddExpEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Person person = GetSlot<Person>("Slot", context);
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            person.GainExp(value);
            Log.Info($"事件：{person.Name} 武将经验 +{value}");
            return true;
        }
    }

    /// <summary>武将类效果的批量注册入口</summary>
    public static class EventEffectsPerson
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("PersonSetLoyalty", EventEffectBase.CreateHandle<PersonSetLoyaltyEffect>);
            EventEffectBase.Register("PersonAddLoyalty", EventEffectBase.CreateHandle<PersonAddLoyaltyEffect>);
            EventEffectBase.Register("PersonRecruit", EventEffectBase.CreateHandle<PersonRecruitEffect>);
            EventEffectBase.Register("PersonSwornBrothers", EventEffectBase.CreateHandle<PersonSwornBrothersEffect>);
            EventEffectBase.Register("PersonAppear", EventEffectBase.CreateHandle<PersonAppearEffect>);
            EventEffectBase.Register("PersonSetVisible", EventEffectBase.CreateHandle<PersonSetVisibleEffect>);
            EventEffectBase.Register("PersonExecute", EventEffectBase.CreateHandle<PersonExecuteEffect>);
            EventEffectBase.Register("PersonDie", EventEffectBase.CreateHandle<PersonDieEffect>);
            EventEffectBase.Register("PersonRelease", EventEffectBase.CreateHandle<PersonReleaseEffect>);
            EventEffectBase.Register("PersonLeaveToWild", EventEffectBase.CreateHandle<PersonLeaveToWildEffect>);
            EventEffectBase.Register("PersonChangeCurrentCity", EventEffectBase.CreateHandle<PersonChangeCurrentCityEffect>);
            EventEffectBase.Register("PersonAddMerit", EventEffectBase.CreateHandle<PersonAddMeritEffect>);
            EventEffectBase.Register("PersonAddExp", EventEffectBase.CreateHandle<PersonAddExpEffect>);
        }
    }
}
