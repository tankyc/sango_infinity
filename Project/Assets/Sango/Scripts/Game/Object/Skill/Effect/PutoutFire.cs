using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 灭火
    /// probability : 概率,万分比
    /// condition: 条件
    /// </summary>
    public class PutoutFire : SkillEffect
    {
        Condition condition;
        int probability;

        public override void Init(JObject p, SkillInstance master)
        {
            base.Init(p, master);
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
            if (!GameRandom.Chance(probability, 10000))
                return;

            if (condition != null && !condition.Check(new SkillEffectConditionDatabase(this, targetCell)))
                return;

            if (targetCell.fire != null)
            {
                targetCell.fire.Clear();
            }
        }
    }
}
