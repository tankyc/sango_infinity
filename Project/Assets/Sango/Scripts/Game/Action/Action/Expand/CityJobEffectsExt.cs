/*
 * 文件名：CityJobEffectsExt.cs
 * 描述：科技的"内政工作收益"类扩展效果——工作消耗、功绩收益、技巧点收益
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【为什么这三个接口此前空着】
 *   OnCityCheckJobCost / OnCityJobGainMerit / OnCityJobGainTechniquePoint
 *   三个 hook 一直在 BuildingWorking 的产出结算里被抛出，但 ActionBase.Init 里
 *   没有任何 Action 订阅，所以"减少工作开销""提高工作功绩"这类经典科技一直做不出来。
 *
 * 【为什么只用于科技，不用于特技】
 *   这三个 hook 的上下文是"某座城在结算一次内政工作"，属于势力级收益。
 *   特技装配时 sangoObjects[0] 是部队，Force 取不到，效果天然无从谈起，
 *   因此基类用 ForceActionBase，并在判定里统一以 city.BelongForce 对齐。
 *
 * 【参数约定】
 *   value 为百分比修正：100 = 不变，-20 = 降低 20%，120 = 提高 20%。
 *   一律用"乘算"而不是"加算"，避免多个来源叠加时把数值推到荒谬量级。
 */

using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 内政工作的金钱消耗修正（value 为百分比，负数为减免）。
    ///
    /// 接入事件：OnCityCheckJobCost。
    /// 注意该 hook 同时被两处调用：
    ///   · City.CheckJobCost —— 判断"钱够不够做这个工作"
    ///   · BuildingWorking —— 计算建筑产出的实际扣费
    /// 两处共用同一份判定，因此本效果对"能否开工"和"实际扣多少"同时生效，不会出现"判定按折后、扣费按原价"。
    ///
    /// 典型用法：科技「屯田」value = -20 → 内政开销降低 20%。
    /// </summary>
    public class CityJobCostModify : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅工作消耗计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            // 先判空再调 base：ForceActionBase.Init 直接取 sangoObjects[0]，空数组会抛异常
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCityCheckJobCost += OnCheckJobCost;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnCityCheckJobCost -= OnCheckJobCost;
        }

        /// <summary>工作消耗计算回调</summary>
        /// <param name="city">结算中的城市</param>
        /// <param name="jobId">工作类型 Id</param>
        /// <param name="persons">参与工作的武将（可能为 null）</param>
        /// <param name="overrideData">消耗覆盖数据（初始为原价）</param>
        void OnCheckJobCost(City city, int jobId, Person[] persons, OverrideData<int> overrideData)
        {
            if (city == null || Force == null) return;
            if (city.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }

    /// <summary>
    /// 内政工作的功绩收益修正（value 为百分比）。
    ///
    /// 接入事件：OnCityJobGainMerit。
    /// 功绩同时决定武将经验（BuildingWorking 里同一份 meritGain 会喂给 merit 与 GainExp），
    /// 因此提高功绩 = 同时加快武将成长，属于高价值科技，建议 value 控制在 10~30。
    ///
    /// 典型用法：科技「名士」value = 20 → 工作功绩 +20%。
    /// </summary>
    public class CityJobMeritGain : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅功绩收益计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCityJobGainMerit += OnJobGainMerit;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnCityJobGainMerit -= OnJobGainMerit;
        }

        /// <summary>功绩收益计算回调</summary>
        /// <param name="city">结算中的城市</param>
        /// <param name="jobId">工作类型 Id</param>
        /// <param name="persons">参与工作的武将</param>
        /// <param name="overrideData">收益覆盖数据</param>
        void OnJobGainMerit(City city, int jobId, Person[] persons, OverrideData<int> overrideData)
        {
            if (city == null || Force == null) return;
            if (city.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }

    /// <summary>
    /// 内政工作的技巧点收益修正（value 为百分比）。
    ///
    /// 接入事件：OnCityJobGainTechniquePoint。
    /// 技巧点最终由 BuildingWorking 交给 city.BelongForce.GainTechniquePoint，
    /// 是科技树推进的主来源之一，value 建议控制在 10~25。
    ///
    /// 典型用法：科技「学问所」value = 15 → 工作技巧点 +15%。
    /// </summary>
    public class CityJobTechniquePointGain : ForceActionBase
    {
        /// <summary>百分比修正值</summary>
        int value;

        /// <summary>初始化：订阅技巧点收益计算</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">装配对象（势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null || sangoObjects.Length == 0) return;

            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnCityJobGainTechniquePoint += OnJobGainTechniquePoint;
        }

        /// <summary>清理：退订</summary>
        public override void Clear()
        {
            GameEvent.OnCityJobGainTechniquePoint -= OnJobGainTechniquePoint;
        }

        /// <summary>技巧点收益计算回调</summary>
        /// <param name="city">结算中的城市</param>
        /// <param name="jobId">工作类型 Id</param>
        /// <param name="persons">参与工作的武将</param>
        /// <param name="overrideData">收益覆盖数据</param>
        void OnJobGainTechniquePoint(City city, int jobId, Person[] persons, OverrideData<int> overrideData)
        {
            if (city == null || Force == null) return;
            if (city.BelongForce != Force) return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }
}
