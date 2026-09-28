/*
 * 文件名：EventEffectsWorld.cs
 * 描述：势力类事件效果——技巧点、霸业点
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 *
 * 说明：城市类效果（金 / 粮 / 治安 / 兵力 / 耐久 / 太守 / 易主）已独立到 EventEffectsCity.cs，
 * 本文件只保留势力级效果。
 *
 * 尚未实现（需先核实引擎接口）：
 *   · TroopCreate / TroopCreateBatch：Scenario.CreateTroop() 只是 new 出一个空部队，
 *     但"指定主将 / 副将 / 位置 / 兵力 / 兵种"的组装与入场路径尚未核实（T2.4）。
 *   · PersonChangeForce：跨势力归属变更目前只通过"登用（BeRecruit）"表达，
 *     直接改归属需要处理军团 / 城池 / 名单的一致性，待与 Deployment 系统一起设计（T2.8）。
 *   · 外交类（关系 / 同盟 / 宣战 / 停战）：涉及 Alliance 对象的创建与清理，待核实（T2.16）。
 */

namespace Sango.Core.Event
{
    /// <summary>
    /// 给势力增加技巧点。
    /// 数值建议走 GainPlace 键（EventTechniquePointCommon / Major），不要写死。
    /// </summary>
    [EventEffectMeta("势力加技巧点", "给指定势力增加技巧点（数值建议走 GainPlace 键）", "Slot", "Value")]
    public class ForceAddTechniquePointEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("Slot", context);
            if (force == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            force.GainTechniquePoint(value);
            Log.Info($"事件：{force.Name} 技巧点 +{value}");
            return true;
        }
    }

    /// <summary>
    /// 给势力增加霸业点。
    /// 注意：本作每回合每武将会给所在势力自然 +1 霸业点（20 人势力即 20/回合），
    /// 因此事件奖励的数值必须明显更大才有感知（默认配置：通用 50 / 重大 300）。
    /// </summary>
    [EventEffectMeta("势力加霸业点", "给指定势力增加霸业点（需明显大于自然增长才有感知）", "Slot", "Value")]
    public class ForceAddHegemonyPointEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("Slot", context);
            if (force == null)
            {
                WarnSlotMissing("Slot", GetString("Slot"));
                return false;
            }

            int value = EvalInt("Value", context);
            if (value == 0) return true;

            force.GainHegemonyPoint(value);
            Log.Info($"事件：{force.Name} 霸业点 +{value}");
            return true;
        }
    }

    /// <summary>势力类效果的批量注册入口</summary>
    public static class EventEffectsWorld
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("ForceAddTechniquePoint", EventEffectBase.CreateHandle<ForceAddTechniquePointEffect>);
            EventEffectBase.Register("ForceAddHegemonyPoint", EventEffectBase.CreateHandle<ForceAddHegemonyPointEffect>);
        }
    }
}
