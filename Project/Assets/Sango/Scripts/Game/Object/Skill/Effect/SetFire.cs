using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 燃烧某个地方
    /// probability : 概率,万分比
    /// condition: 条件
    /// values : 回合数集合[]
    /// weight : 回合数命中的权重[]
    /// </summary>
    public class SetFire : SkillEffect
    {
        Condition condition;
        int probability;
        int [] values;
        int [] weight;

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
            if (!GameRandom.Chance(probability, 10000))
                return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            int index = GameRandom.RandomWeightIndex(weight);
            int finalCount = values[index];

            Fire fire = targetCell.fire;
            if (fire == null)
            {
                fire = new Fire()
                {
                    damage = Scenario.Cur.Variables.baseFireDamage,
                    intelligence = master.master.Intelligence,
                    cell = targetCell,
                    counter = finalCount
                };
                Scenario.Cur.Add(fire);
                fire.Init(Scenario.Cur);
            }
            else
            {
                fire.intelligence = master.master.Intelligence;
                fire.counter = finalCount;
            }

            if(fire.Render != null && fire.Render.IsVisible())
            {
                GameMedia.Instance.PlaySfx(86);
            }

            GameEvent.OnFireAdd?.Invoke(master, fire);

            targetCell.fire = fire;
            fire.Action();
        }
    }
}
