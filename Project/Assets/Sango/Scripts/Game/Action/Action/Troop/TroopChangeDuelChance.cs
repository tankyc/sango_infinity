using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 改变部队的单挑概率（战法命中后触发单挑用的那个属性）。
    ///
    /// value： 改变值（百分比，可为负）
    /// condition： 额外条件（如兵种、攻守、是否一般攻击等，见 TroopConditionDatabase）
    ///
    /// 与 TroopChangeCaptiveFactor 同一套路：部队属性重算时，在基础值（剧本参数）之上增减。
    /// </summary>
    public class TroopChangeDuelChance : TroopTroopActionBase
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
                    troop.duelChance += value;
            }
            else
            {
                troop.duelChance += value;
            }
        }
    }
}
