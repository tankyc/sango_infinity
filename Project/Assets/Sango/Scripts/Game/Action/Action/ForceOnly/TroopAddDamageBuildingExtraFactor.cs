using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种战法耐久破坏威力(百分比)增加
    /// value: 增加值(百分比) kinds: 兵种类型
    /// </summary>
    public class TroopAddDamageBuildingExtraFactor : ForceTroopActionBase
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
            float factor = value / 100f;
            if (kinds == null)
            {
                troop.landDamageBuildingExtraFactor += factor;
                troop.waterDamageBuildingExtraFactor += factor;
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
                    troop.landDamageBuildingExtraFactor += factor;
                if (kinds.Contains(troop.WaterTroopType.kind))
                    troop.waterDamageBuildingExtraFactor += factor;

            }
        }
    }
}
