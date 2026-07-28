using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using Sango.Render;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 对目标添加BUFF
    /// probability : 概率,万分比
    /// condition: 条件
    /// values : 回合数集合[]
    /// weight : 回合数命中的权重[]
    /// buffId : 添加的状态ID
    /// </summary>
    public class AddBuff : SkillEffect
    {
        Condition condition;
        int probability;
        int[] values;
        int[] weight;
        int buffId;

        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

            buffId = p.Value<int>("buffId");
            probability = p.Value<int>("probability");

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

            int index = GameRandom.RandomWeightIndex(weight);
            int finalCount = values[index];

            target.AddBuff(buffId, finalCount, master.master);
        }

    }
}
