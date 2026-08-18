using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变部队逃跑率
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
    /// </summary>
    public class TroopChangeEscapeFactor : TroopTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != null && troop.mBelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            if (condition != null)
            {
                TroopConditionDatabase troopActionConditionDatabase = new TroopConditionDatabase(troop);
                if (condition.Check(troopActionConditionDatabase))
                {
                    troop.ForEachPerson(x =>
                    {
                        x.escapeFactorWhenTroopDestroy += value;
                    });
                }
            }
            else
            {
                troop.ForEachPerson(x =>
                {
                    x.escapeFactorWhenTroopDestroy += value;
                });
            }
        }
    }
}
