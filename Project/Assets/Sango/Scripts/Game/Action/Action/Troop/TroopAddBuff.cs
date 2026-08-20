using Sango.Core.Tools;
using System.Collections.Generic;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变某兵种类型战法的气力消耗
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
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

            JArray array = p.Value<JArray>("values");
            List<int> list = new List<int>();
            for (int i = 0; i < array.Count; i++)
            {
                list.Add(array[i].Value<int>());
            }
            values = list.ToArray();

            array = p.Value<JArray>("weight");
            list.Clear();
            for (int i = 0; i < array.Count; i++)
            {
                list.Add(array[i].Value<int>());
            }

            weight = list.ToArray();
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

            int index = GameRandom.RandomWeightIndex(weight);
            int finalCount = values[index];

            target.AddBuff(buffId, finalCount, trigger.ActionTroop);
        }
    }
}
