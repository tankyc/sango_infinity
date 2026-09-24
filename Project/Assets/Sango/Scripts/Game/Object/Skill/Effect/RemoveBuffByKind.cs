using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 移除某些BUFF
    /// probability : 概率,万分比
    /// condition: 条件
    /// values : 状态kind集合
    /// </summary>
    public class RemoveBuffByKind : SkillEffect
    {
        Condition condition;
        int probability;
        int[] values;

        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);
            JArray array = p.Value<JArray>("values");
            List<int> list = new List<int>();
            for (int i = 0; i < array.Count; i++)
            {
                list.Add(array[i].Value<int>());
            }
            values = list.ToArray();

            probability = p.Value<int>("probability");
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                condition.Init(conObj, master);
            }
        }

        public override void Action(Cell targetCell)
        {
            Troop target = targetCell.troop;
            if (target == null) return;

            if (!GameRandom.Chance(probability, 10000))
                return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            for(int i = 0; i < values.Length; i++) 
                target.RemoveBuffByKind(values[i]);
        }
    }
}
