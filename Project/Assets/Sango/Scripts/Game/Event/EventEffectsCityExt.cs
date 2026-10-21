/*
 * 文件名：EventEffectsCityExt.cs
 * 描述：城市类事件效果（第二批）——商业、农业、人口、兵役人口、民心、俘虏、改名
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【与 EventEffectsCity.cs 的关系】
 *   EventEffectsCity.cs 已有金 / 粮 / 治安 / 兵力 / 耐久 / 太守 / 易主；
 *   本文件补齐其余"可写城市数值"与人事（俘虏）操作，命名风格保持一致。
 *
 * 【关于上限】
 *   凡是引擎提供了上限派生属性的（CommerceLimit / AgricultureLimit /
 *   PopulationLimit / MaxMorale），一律按上限夹取；
 *   没有上限属性的（兵役人口 troopPopulation）只保证不为负，并在注释里写明——
 *   宁可少夹一层，也不臆造一个不存在的上限字段。
 *
 * 【尚未实现】
 *   · 城市建筑的新建 / 拆除：City.allBuildings 只是登记表，真正的新建要走
 *     Building 的创建与格子挂接（cell.building + 渲染），目前没有公共封装。
 *   · 城市改名之外的"改所属州"：ProvinceId 是数据表映射，单改数值不会重建辖区关系。
 */

namespace Sango.Core.Event
{
    /// <summary>增减城市商业（按 CommerceLimit 夹取）</summary>
    [EventEffectMeta("城市商业增减", "增减指定城市的商业值，超出上限的部分丢弃", "CitySlot", "Value")]
    public class CityAddCommerceEffect : EventEffectBase
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

            int old = city.commerce;
            city.commerce = CityAddGoldEffect.Clamp(old + value, 0, city.CommerceLimit);
            Log.Info($"事件：{city.Name} 商业 {value:+#;-#;0} → {city.commerce}");
            return true;
        }
    }

    /// <summary>增减城市农业（按 AgricultureLimit 夹取）</summary>
    [EventEffectMeta("城市农业增减", "增减指定城市的农业值，超出上限的部分丢弃", "CitySlot", "Value")]
    public class CityAddAgricultureEffect : EventEffectBase
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

            city.agriculture = CityAddGoldEffect.Clamp(city.agriculture + value, 0, city.AgricultureLimit);
            Log.Info($"事件：{city.Name} 农业 {value:+#;-#;0} → {city.agriculture}");
            return true;
        }
    }

    /// <summary>增减城市人口（按 PopulationLimit 夹取）</summary>
    [EventEffectMeta("城市人口增减", "增减指定城市的人口，超出上限的部分丢弃", "CitySlot", "Value")]
    public class CityAddPopulationEffect : EventEffectBase
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

            city.population = CityAddGoldEffect.Clamp(city.population + value, 0, city.PopulationLimit);
            Log.Info($"事件：{city.Name} 人口 {value:+#;-#;0} → {city.population}");
            return true;
        }
    }

    /// <summary>
    /// 增减城市兵役人口。
    /// 该项没有独立的"上限派生属性"，因此只保证不为负（详见文件头注释）。
    /// </summary>
    [EventEffectMeta("城市兵役人口增减", "增减指定城市的兵役人口（不封顶，只保证非负）", "CitySlot", "Value")]
    public class CityAddTroopPopulationEffect : EventEffectBase
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

            city.troopPopulation = city.troopPopulation + value;
            if (city.troopPopulation < 0) city.troopPopulation = 0;

            Log.Info($"事件：{city.Name} 兵役人口 {value:+#;-#;0} → {city.troopPopulation}");
            return true;
        }
    }

    /// <summary>增减城市民心（按 MaxMorale 夹取）</summary>
    [EventEffectMeta("城市民心增减", "增减指定城市的民心，夹在 0~该城民心上限之间", "CitySlot", "Value")]
    public class CityAddMoraleEffect : EventEffectBase
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

            city.morale = CityAddGoldEffect.Clamp(city.morale + value, 0, city.MaxMorale);
            Log.Info($"事件：{city.Name} 民心 {value:+#;-#;0} → {city.morale}");
            return true;
        }
    }

    /// <summary>
    /// 把指定武将关押进城市（成为俘虏）。
    /// 走 City.AddCaptive：它会一并维护城的 captiveList 与势力的 BeCaptiveList、
    /// 并把 state 置为 Prisoner，因此不要手写这几个字段。
    /// </summary>
    [EventEffectMeta("收押俘虏", "把指定武将关押进该城（自动加入城的俘虏名单）", "CitySlot", "PersonSlot")]
    public class CityAddPrisonerEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            Person person = GetSlot<Person>("PersonSlot", context);

            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            city.AddCaptive(person);
            Log.Info($"事件：{person.Name} 被收押于 {city.Name}");
            return true;
        }
    }

    /// <summary>
    /// 释放城中的俘虏。
    /// 走 City.RemoveCaptive：它负责把该武将移出城的俘虏名单与势力的关押名单。
    /// </summary>
    [EventEffectMeta("释放俘虏", "把指定武将从此城的俘虏名单中释放", "CitySlot", "PersonSlot")]
    public class CityReleasePrisonerEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            City city = GetSlot<City>("CitySlot", context);
            Person person = GetSlot<Person>("PersonSlot", context);

            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            if (city.captiveList == null || !city.captiveList.Contains(person))
            {
                Log.Warning($"事件：{person.Name} 并不在 {city.Name} 的俘虏名单里，释放被跳过");
                return false;
            }

            city.RemoveCaptive(person);
            Log.Info($"事件：{person.Name} 从 {city.Name} 获释");
            return true;
        }
    }

    /// <summary>城市改名（文本支持 $槽位.属性 与 #变量 占位符）</summary>
    [EventEffectMeta("城市改名", "修改城市名（如把许昌改为许都）", "CitySlot", "Text")]
    public class CityRenameEffect : EventEffectBase
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

            string text = EvalText("Text", context);
            if (string.IsNullOrEmpty(text))
            {
                Log.Warning($"事件：{city.Name} 改名失败 —— Text 参数为空");
                return false;
            }

            string old = city.Name;
            city.Name = text;
            Log.Info($"事件：{old} 改名为 {text}");
            return true;
        }
    }

    /// <summary>
    /// 把城市金钱直接设为指定值（不累加）。
    /// 与 CityAddGold 的区别：这个用于"剧情校准"（如"城中只剩 100 金"），
    /// 不依赖当前值，重复执行结果一致。
    /// </summary>
    [EventEffectMeta("设置城市金钱", "把城市金钱直接设为指定值（夹在金库上限内），用于剧情校准", "CitySlot", "Value")]
    public class CitySetGoldEffect : EventEffectBase
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
            int old = city.gold;
            city.gold = CityAddGoldEffect.Clamp(value, 0, city.GoldLimit);

            Log.Info($"事件：{city.Name} 金钱被设为 {city.gold}（原 {old}）");
            return true;
        }
    }

    /// <summary>城市类效果（第二批）的批量注册入口</summary>
    public static class EventEffectsCityExt
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("CityAddCommerce", EventEffectBase.CreateHandle<CityAddCommerceEffect>);
            EventEffectBase.Register("CityAddAgriculture", EventEffectBase.CreateHandle<CityAddAgricultureEffect>);
            EventEffectBase.Register("CityAddPopulation", EventEffectBase.CreateHandle<CityAddPopulationEffect>);
            EventEffectBase.Register("CityAddTroopPopulation", EventEffectBase.CreateHandle<CityAddTroopPopulationEffect>);
            EventEffectBase.Register("CityAddMorale", EventEffectBase.CreateHandle<CityAddMoraleEffect>);
            EventEffectBase.Register("CityAddPrisoner", EventEffectBase.CreateHandle<CityAddPrisonerEffect>);
            EventEffectBase.Register("CityReleasePrisoner", EventEffectBase.CreateHandle<CityReleasePrisonerEffect>);
            EventEffectBase.Register("CityRename", EventEffectBase.CreateHandle<CityRenameEffect>);
            EventEffectBase.Register("CitySetGold", EventEffectBase.CreateHandle<CitySetGoldEffect>);
        }
    }
}
