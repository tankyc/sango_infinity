using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变部队逃跑率（部队溃灭 / 单挑落败时的抓捕判定都会减去这个逃跑系数）
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
    ///
    /// 数据用途说明：本项目用"逃跑系数拉满（value:200）"表达免俘 —— 特技 #34 血路
    /// （部队溃灭时同部队武将不会被俘）、特技 #33 强运 都是这么配的，
    /// 判定点见 Troop.ChangeTroops 与 Duel.CalcCaptureChance。
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
            if (Force != null && troop.BelongForce != Force) return;
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
