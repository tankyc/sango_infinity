using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using Sango.Render;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 治疗伤兵
    /// probability : 概率,万分比
    /// condition: 条件
    /// values : 值集合[] (百分比)
    /// weight : 值命中的权重[]
    /// </summary>
    public class RecureWoundedTroops : SkillEffect
    {
        Condition condition;
        int probability;
        int[] values;
        int[] weight;
        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);

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
            int v = target.woundedTroops * finalCount / 100;
            target.woundedTroops = Mathf.Max(0, target.woundedTroops - v);
            target.ChangeTroops(v, master, 0);

        }
    }
}
