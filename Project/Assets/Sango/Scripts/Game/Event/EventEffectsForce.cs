/*
 * 文件名：EventEffectsForce.cs
 * 描述：势力 / 军团类事件效果——君主、军师、霸业点、战力、军团金粮
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【与 EventEffectsWorld.cs 的分工】
 *   EventEffectsWorld.cs 只管"技巧点 / 霸业点"这两个成长资源；
 *   本文件补的是人事与军团经济，两者都在势力层级，但语义不同，故分文件。
 *
 * 【尚未实现（需先核实引擎接口，不写假实现）】
 *   · 势力技巧的授予：Force.Techniques 是 SangoObjectList<Technique>，直接 Add 不会同步
 *     techniqueTree / techniqueMaxLevel（这两个由 Force.InitTechniquesTree 在载入时构建），
 *     需要先找到"授予技巧后重建树"的入口，否则会出现"已掌握但科技树里看不见"。
 *   · 势力爵位变更：Title 属性 setter 接受 Title 对象，但 CommonData 里爵位集合的取用方式未核实。
 *   · 外交关系的建立 / 解除：Alliance 对象只在 DiplomacyActionAlliance / DiplomacyActionTruce
 *     内部 new 出来，没有公共封装；直接 new 会漏掉 AllianceList 与 leftCount 的维护。
 *   · 势力灭亡：ForceFall 涉及城池移交、武将下野、军团回收，必须走引擎既有的灭亡流程。
 */

namespace Sango.Core.Event
{
    /// <summary>更换势力君主（同时改 Governor 的 Id 与对象引用）</summary>
    [EventEffectMeta("更换君主", "把指定武将设为该势力的君主（托孤、禅让等）", "ForceSlot", "PersonSlot")]
    public class ForceSetGovernorEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            Person person = GetSlot<Person>("PersonSlot", context);

            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            // Governor 是存档用的 Id 数值，mGovernor 是运行时对象引用；
            // 只改其中一个会导致"界面显示新君主、引擎里还是旧君主"，必须成对修改。
            string oldName = force.mGovernor != null ? force.mGovernor.Name : "无";
            force.Governor = person.Id;
            force.mGovernor = person;

            Log.Info($"事件：{force.Name} 的君主由 {oldName} 变更为 {person.Name}");
            return true;
        }
    }

    /// <summary>更换势力军师</summary>
    [EventEffectMeta("更换军师", "把指定武将设为该势力的军师（三顾茅庐后诸葛亮任军师）", "ForceSlot", "PersonSlot")]
    public class ForceSetCounsellorEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            Person person = GetSlot<Person>("PersonSlot", context);

            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }
            if (person == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            string oldName = force.mCounsellor != null ? force.mCounsellor.Name : "无";
            force.Counsellor = person.Id;
            force.mCounsellor = person;

            Log.Info($"事件：{force.Name} 的军师由 {oldName} 变更为 {person.Name}");
            return true;
        }
    }

    /// <summary>把势力霸业点设为指定值（下限 0）</summary>
    [EventEffectMeta("设置霸业点", "把势力霸业点直接设为指定值（不累加，用于剧情校准）", "ForceSlot", "Value")]
    public class ForceSetHegemonyPointEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value < 0) value = 0;

            int old = force.HegemonyPoint;
            force.HegemonyPoint = value;
            Log.Info($"事件：{force.Name} 霸业点 {old} → {value}");
            return true;
        }
    }

    /// <summary>
    /// 设置势力战力显示值（Force.FightPower）。
    ///
    /// 注意：这个是**引擎会重算的派生值**，事件里改它主要用于"剧情演出期间临时抬高观感"，
    /// 下一次势力统计时会被覆盖。要真正变强请用城池兵力 / 武将属性类效果。
    /// </summary>
    [EventEffectMeta("设置势力战力", "临时设置势力战力显示值（会被引擎重算覆盖，仅用于演出）", "ForceSlot", "Value")]
    public class ForceSetFightPowerEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value < 0) value = 0;

            force.FightPower = value;
            Log.Info($"事件：{force.Name} 战力显示值设为 {value}");
            return true;
        }
    }

    /// <summary>给军团增加金钱（下限 0，军团没有金库上限概念，故不封顶）</summary>
    [EventEffectMeta("军团加金", "给指定军团增加金钱（军团无金库上限，只保证不为负）", "CorpsSlot", "Value")]
    public class CorpsAddGoldEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Corps corps = GetSlot<Corps>("CorpsSlot", context);
            if (corps == null)
            {
                WarnSlotMissing("CorpsSlot", GetString("CorpsSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            int old = corps.gold;
            corps.gold = old + value;
            if (corps.gold < 0) corps.gold = 0;

            Log.Info($"事件：军团 {corps.number} 金钱 {old} → {corps.gold}");
            return true;
        }
    }

    /// <summary>给军团增加粮草（下限 0，不封顶）</summary>
    [EventEffectMeta("军团加粮", "给指定军团增加粮草（军团无粮仓上限，只保证不为负）", "CorpsSlot", "Value")]
    public class CorpsAddFoodEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Corps corps = GetSlot<Corps>("CorpsSlot", context);
            if (corps == null)
            {
                WarnSlotMissing("CorpsSlot", GetString("CorpsSlot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            int old = corps.food;
            corps.food = old + value;
            if (corps.food < 0) corps.food = 0;

            Log.Info($"事件：军团 {corps.number} 粮草 {old} → {corps.food}");
            return true;
        }
    }

    /// <summary>
    /// 让势力灭亡。
    ///
    /// 【必须先移城】执行前该势力名下不能有任何城池，否则会被拒绝并告警。
    /// 事件里的正确顺序：CityChangeBelong × N（逐城移交给接收方）→ 本效果。
    /// 详见 Force.Extinguish 的前置说明。
    /// </summary>
    [EventEffectMeta("势力灭亡", "让指定势力灭亡（执行前必须先移交其所有城池）", "ForceSlot")]
    public class ForceExtinguishEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }

            return force.Extinguish();
        }
    }

    /// <summary>势力 / 军团类效果的批量注册入口</summary>
    public static class EventEffectsForce
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("ForceSetGovernor", EventEffectBase.CreateHandle<ForceSetGovernorEffect>);
            EventEffectBase.Register("ForceSetCounsellor", EventEffectBase.CreateHandle<ForceSetCounsellorEffect>);
            EventEffectBase.Register("ForceSetHegemonyPoint", EventEffectBase.CreateHandle<ForceSetHegemonyPointEffect>);
            EventEffectBase.Register("ForceSetFightPower", EventEffectBase.CreateHandle<ForceSetFightPowerEffect>);
            EventEffectBase.Register("CorpsAddGold", EventEffectBase.CreateHandle<CorpsAddGoldEffect>);
            EventEffectBase.Register("CorpsAddFood", EventEffectBase.CreateHandle<CorpsAddFoodEffect>);
            EventEffectBase.Register("ForceExtinguish", EventEffectBase.CreateHandle<ForceExtinguishEffect>);
        }
    }
}
