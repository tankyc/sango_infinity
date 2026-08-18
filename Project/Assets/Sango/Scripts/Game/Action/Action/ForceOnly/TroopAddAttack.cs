using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种攻击力增加
    /// value: 增加值 kinds: 兵种类型
    /// </summary>
    public class TroopAddAttack : ForceTroopActionBase
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
            if (Force != troop.mBelongForce) return;

            if (kinds == null)
            {
                troop.landAttack += value;
                troop.waterAttack += value;
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
                    troop.landAttack += value;
                if (kinds.Contains(troop.WaterTroopType.kind))
                    troop.waterAttack += value;
            }
        }
    }
}
