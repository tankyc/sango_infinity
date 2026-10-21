/*
 * 文件名：EventEffectsTroop.cs
 * 描述：部队类事件效果——兵力、士气、行动力、攻城力
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【只管数值，不管编成】
 *   本文件刻意不涉及"创建部队 / 解散部队 / 换主将 / 换兵种 / 移动位置"：
 *   这些操作要同时处理编成、渲染、格子占用、行动力归属，
 *   而引擎侧目前没有可直接复用的公共入口（见 EventEffectsWorld.cs 头注释的同类说明）。
 *   直接改字段会留下"部队存在但格子/渲染对不上"的脏状态，故不做。
 *
 * 【为什么士气要用 MaxMorale 夹取】
 *   Troop.MaxMorale 是从所在城市的 MaxMorale 派生的（部队在野外时取 100），
 *   写死 100 会让"守城部队士气上限 120"这类配置失效。
 */

namespace Sango.Core.Event
{
    /// <summary>增减部队兵力（按 MaxTroops 夹取）</summary>
    [EventEffectMeta("部队兵力增减", "增减部队兵力，超出编制上限的部分丢弃", "TroopSlot", "Value")]
    public class TroopAddTroopsEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            int old = troop.troops;
            int next = old + value;
            if (next < 0) next = 0;

            // MaxTroops 为 0 表示尚未算出编制（例如刚 new 出来），此时不封顶，只保非负
            int max = troop.MaxTroops;
            if (max > 0 && next > max) next = max;

            troop.troops = next;
            Log.Info($"事件：{troop.Name} 兵力 {old} → {troop.troops}");
            return true;
        }
    }

    /// <summary>增减部队士气（按 MaxMorale 夹取）</summary>
    [EventEffectMeta("部队士气增减", "增减部队士气，夹在 0~当前士气上限之间", "TroopSlot", "Value")]
    public class TroopAddMoraleEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            int max = troop.MaxMorale;
            if (max <= 0) max = 100;

            int old = troop.morale;
            int next = old + value;
            if (next < 0) next = 0;
            if (next > max) next = max;

            troop.morale = next;
            Log.Info($"事件：{troop.Name} 士气 {old} → {troop.morale}（上限 {max}）");
            return true;
        }
    }

    /// <summary>增减部队行动力（无上限字段，只保证非负）</summary>
    [EventEffectMeta("部队行动力增减", "增减部队行动力（无上限，只保证非负）", "TroopSlot", "Value")]
    public class TroopAddEnergyEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            int old = troop.energy;
            troop.energy = old + value;
            if (troop.energy < 0) troop.energy = 0;

            Log.Info($"事件：{troop.Name} 行动力 {old} → {troop.energy}");
            return true;
        }
    }

    /// <summary>设置部队攻城力（BuildPower）</summary>
    [EventEffectMeta("设置攻城力", "直接设置部队的攻城力（冲车、投石等装备加成的临时修正）", "TroopSlot", "Value")]
    public class TroopSetBuildPowerEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value < 0) value = 0;

            int old = troop.BuildPower;
            troop.BuildPower = value;
            Log.Info($"事件：{troop.Name} 攻城力 {old} → {value}");
            return true;
        }
    }

    /// <summary>部队类效果的批量注册入口</summary>
    public static class EventEffectsTroop
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("TroopAddTroops", EventEffectBase.CreateHandle<TroopAddTroopsEffect>);
            EventEffectBase.Register("TroopAddMorale", EventEffectBase.CreateHandle<TroopAddMoraleEffect>);
            EventEffectBase.Register("TroopAddEnergy", EventEffectBase.CreateHandle<TroopAddEnergyEffect>);
            EventEffectBase.Register("TroopSetBuildPower", EventEffectBase.CreateHandle<TroopSetBuildPowerEffect>);
        }
    }
}
