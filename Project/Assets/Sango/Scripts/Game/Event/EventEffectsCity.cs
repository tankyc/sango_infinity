/*
 * 文件名：EventEffectsCity.cs
 * 描述：城市类事件效果——金钱、粮草、治安、兵力、耐久、太守、归属
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 *
 * 【关于金 / 粮】本作的金 / 粮按城市存储（City.gold / City.food），
 * 势力层只通过军团（Corps.gold）聚合统计，没有"势力金库"。
 * 因此效果是 CityAddGold / CityAddFood，而不是 ForceAddGold。
 *
 * 【关于耐久】刻意直接改 durability 字段 + 按 DurabilityLimit 夹取，
 * 而不走 BuildingBase.ChangeDurability：后者是"受击扣耐久"的入口，
 * 会连带触发渲染表现与受损事件；剧本事件要的是"设定值"而非"挨了一下"。
 */

namespace Sango.Core.Event
{
    /// <summary>给城市增加金钱（夹在金库上限内）</summary>
    [EventEffectMeta("城市加金", "给指定城市增加金钱，超出金库上限的部分丢弃", "CitySlot", "Value")]
    public class CityAddGoldEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            city.gold = Clamp(city.gold + value, 0, city.GoldLimit);
            Log.Info($"事件：{city.Name} 金钱 {value:+#;-#;0} → {city.gold}");
            return true;
        }

        /// <summary>把值夹到区间内</summary>
        /// <param name="value">原值</param>
        /// <param name="min">下限</param>
        /// <param name="max">上限</param>
        /// <returns>夹取结果</returns>
        internal static int Clamp(int value, int min, int max)
        {
            if (max < min) max = min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    /// <summary>给城市增加粮草（夹在粮仓上限内）</summary>
    [EventEffectMeta("城市加粮", "给指定城市增加粮草，超出粮仓上限的部分丢弃", "CitySlot", "Value")]
    public class CityAddFoodEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            city.food = CityAddGoldEffect.Clamp(city.food + value, 0, city.FoodLimit);
            Log.Info($"事件：{city.Name} 粮草 {value:+#;-#;0} → {city.food}");
            return true;
        }
    }

    /// <summary>增减城市治安（走 City.AddSecurity，自带 0~100 夹取）</summary>
    [EventEffectMeta("城市治安增减", "增减指定城市的治安（0~100）", "CitySlot", "Value")]
    public class CityAddSecurityEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            int result = city.AddSecurity(value);
            Log.Info($"事件：{city.Name} 治安 {value:+#;-#;0} → {result}");
            return true;
        }
    }

    /// <summary>增减城市兵力（夹在兵营上限内）</summary>
    [EventEffectMeta("城市兵力增减", "增减指定城市的驻扎兵力，超出上限的部分丢弃", "CitySlot", "Value")]
    public class CityAddTroopEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            city.troops = CityAddGoldEffect.Clamp(city.troops + value, 0, city.TroopsLimit);
            Log.Info($"事件：{city.Name} 兵力 {value:+#;-#;0} → {city.troops}");
            return true;
        }
    }

    /// <summary>增减城市耐久（直接设定，不走受击扣除入口）</summary>
    [EventEffectMeta("城市耐久增减", "增减指定城市的耐久，夹在耐久上限内", "CitySlot", "Value")]
    public class CityAddDurabilityEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            city.durability = CityAddGoldEffect.Clamp(city.durability + value, 0, city.DurabilityLimit);
            city.Render?.UpdateRender();
            Log.Info($"事件：{city.Name} 耐久 {value:+#;-#;0} → {city.durability}");
            return true;
        }
    }

    /// <summary>更换城市太守</summary>
    [EventEffectMeta("更换太守", "把指定武将设为该城的太守（典韦、张辽这类任命桥段）", "CitySlot", "Slot")]
    public class CityChangeGovernorEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            Person person = GetSlot<Person>("Slot", context);

            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }
            if (person == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            city.Leader = person;
            city.NeedUpdateLeader();
            Log.Info($"事件：{city.Name} 的太守变更为 {person.Name}");
            return true;
        }
    }

    /// <summary>
    /// 城池易主（取荆南四郡、曹操平河北这类批量归属变更的单城版）。
    ///
    /// 走 <see cref="City.ChangeCorps"/>：它会一并处理军团归属与势力归属，
    /// 并做适当的名单清理。传入目标势力的第一军团（CapitalCorps）即可。
    /// </summary>
    [EventEffectMeta("城池易主", "把城市移交给指定势力（自动落到该势力的第一军团）", "CitySlot", "ForceSlot", "CorpsSlot")]
    public class CityChangeBelongEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }

            Force force = GetSlot<Force>("ForceSlot", context);
            Corps corps = GetSlot<Corps>("CorpsSlot", context);

            // 没显式指定军团时落到目标势力的第一军团（君主所在军团）
            if (corps == null && force != null) corps = force.CapitalCorps;
            if (corps == null)
            {
                Log.Warning($"事件：{city.Name} 易主失败 —— 既没有 CorpsSlot 也没有可用的 ForceSlot 第一军团");
                return false;
            }

            Force oldForce = city.BelongForce;
            city.ChangeCorps(corps);

            string from = oldForce != null ? oldForce.Name : "无势力";
            Log.Info($"事件：{city.Name} 由 {from} 转归 {city.BelongForce?.Name}");
            return true;
        }
    }

    /// <summary>城市类效果的批量注册入口</summary>
    public static class EventEffectsCity
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("CityAddGold", EventEffectBase.CreateHandle<CityAddGoldEffect>);
            EventEffectBase.Register("CityAddFood", EventEffectBase.CreateHandle<CityAddFoodEffect>);
            EventEffectBase.Register("CityAddSecurity", EventEffectBase.CreateHandle<CityAddSecurityEffect>);
            EventEffectBase.Register("CityAddTroop", EventEffectBase.CreateHandle<CityAddTroopEffect>);
            EventEffectBase.Register("CityAddDurability", EventEffectBase.CreateHandle<CityAddDurabilityEffect>);
            EventEffectBase.Register("CityChangeGovernor", EventEffectBase.CreateHandle<CityChangeGovernorEffect>);
            EventEffectBase.Register("CityChangeBelong", EventEffectBase.CreateHandle<CityChangeBelongEffect>);
        }
    }
}
