using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 从多个 buffId 中随机选 1 个施加(恰好中一个,互斥)
    /// buffIds : 候选 buffId 数组,如 [1,2]
    /// probability : 总体触发概率,万分比(10000=必中)
    /// values / weight : 被选中的 buff 对应的回合数(同 AddBuff)
    /// </summary>
    public class AddBuffRandom : SkillEffect
    {
        int probability;
        int[] buffIds;
        int[] values;
        int[] weight;

        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

            probability = p.Value<int>("probability");

            JArray arr = p.Value<JArray>("buffIds");
            if (arr == null) return;
            List<int> list = new List<int>();
            for (int i = 0; i < arr.Count; i++)
                list.Add(arr[i].Value<int>());
            buffIds = list.ToArray();

            arr = p.Value<JArray>("values");
            if (arr != null)
            {
                list.Clear();
                for (int i = 0; i < arr.Count; i++)
                    list.Add(arr[i].Value<int>());
                values = list.ToArray();
            }

            arr = p.Value<JArray>("weight");
            if (arr != null)
            {
                list.Clear();
                for (int i = 0; i < arr.Count; i++)
                    list.Add(arr[i].Value<int>());
                weight = list.ToArray();
            }
        }

        public override void Action(Cell targetCell)
        {
            Troop target = targetCell.troop;
            if (target == null) return;
            if (buffIds == null || buffIds.Length == 0) return;

            if (!GameRandom.Chance(probability, 10000))
                return;

            // 恰好随机选一个 buffId(均匀分布)
            int chosenIdx = GameRandom.Range(0, buffIds.Length);
            int buffId = buffIds[chosenIdx];

            int finalCount = 2; // 默认 2 回合
            if (values != null && values.Length > 0)
            {
                if (weight != null && weight.Length == values.Length)
                    finalCount = values[GameRandom.RandomWeightIndex(weight)];
                else if (values.Length == 1)
                    finalCount = values[0];
                else
                    finalCount = values[GameRandom.Range(0, values.Length)];
            }

            target.AddBuff(buffId, finalCount, master.master);
        }
    }
}
