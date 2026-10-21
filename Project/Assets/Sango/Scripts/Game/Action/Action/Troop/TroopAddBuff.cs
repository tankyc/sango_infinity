using Sango.Core.Tools;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 在触发瞬间给部队附加 BUFF（容器触发）。
    ///
    /// 必须放进「触发器 + 效果」容器的 actionList 内；自身不订阅事件。
    /// 参数：buffId BUFF Id；counter 持续回合；probability 触发概率（万分比）；
    /// values / weight 一一对应的层数池与权重（按权重随机取一个层数，两者长度必须相等）；
    /// targetType 0 行动方 / 1 目标方；condition 额外条件。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class TroopAddBuff : TroopTroopActionBase
    {
        int targetType = 0;
        int probability;
        int[] values;
        int[] weight;
        int buffId;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            buffId = p.Value<int>("buffId");
            probability = p.Value<int>("probability");
            targetType = p.Value<int>("targetType");

            // 【空数组防护】values / weight 在 JSON 中缺失时 p.Value<JArray> 返回 null，
            // 原先直接取 Count 会 NullReferenceException（手改配置时很容易漏项）。
            values = ParseIntArray(p.Value<JArray>("values"));
            weight = ParseIntArray(p.Value<JArray>("weight"));
        }

        /// <summary>
        /// 把 JSON 数组解析为 int 数组；缺失或为空时返回空数组（不返回 null，调用方无需再判空）。
        /// </summary>
        /// <param name="array">JSON 数组，可为 null</param>
        /// <returns>解析后的 int 数组</returns>
        static int[] ParseIntArray(JArray array)
        {
            if (array == null || array.Count == 0)
                return System.Array.Empty<int>();

            int[] result = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
                result[i] = array[i].Value<int>();
            return result;
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if (trigger == null) return;

            if (trigger.ActionTroop != Troop) return;

            if (targetType == 0)
            {
                if (trigger.ActionTroop == null) return;
                Action(trigger.ActionTroop, trigger);
            }
            else if (targetType == 1)
            {
                if (trigger.TargetTroop == null) return;
                Action(trigger.TargetTroop, trigger);
            }

        }
        
        public void Action(Troop target, Trigger trigger)
        {
            if (target == null) return;

            if (!GameRandom.Chance(probability, 10000))
                return;

            if (condition != null && !condition.Check(trigger))
                return;

            // 【越界防护】values 与 weight 必须一一对应；
            // 配置缺失或数量不一致时直接放弃本次效果并告警，避免 RandomWeightIndex / 数组索引抛异常。
            if (values.Length == 0 || weight.Length == 0)
            {
                Sango.Log.Warning($"[TroopAddBuff] buffId={buffId} 缺少 values 或 weight，本次效果不生效");
                return;
            }
            if (values.Length != weight.Length)
            {
                Sango.Log.Warning($"[TroopAddBuff] buffId={buffId} 的 values({values.Length}) 与 weight({weight.Length}) 数量不一致，本次效果不生效");
                return;
            }

            int index = GameRandom.RandomWeightIndex(weight);
            int finalCount = values[index];

            target.AddBuff(buffId, finalCount, trigger.ActionTroop);
        }
    }
}
