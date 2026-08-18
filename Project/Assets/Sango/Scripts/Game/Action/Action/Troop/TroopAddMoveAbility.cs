using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种攻击力增加
    /// value: 增加值  kinds:兵种kind   checkLand 0:只检查kinds 1:只对landType检查kinds 2只对waterType检查kinds
    /// </summary>
    public class TroopAddMoveAbility : TroopActionBase
    {
        int checkLand;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            checkLand = p.Value<int>("checkLand");
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

            if (checkLand == 0)
            {
                if (kinds == null)
                {
                    troop.landMoveAbility += value;
                    troop.waterMoveAbility += value;
                }
                else
                {
                    if (kinds.Contains(troop.LandTroopType.kind))
                        troop.landMoveAbility += value;
                    if (kinds.Contains(troop.WaterTroopType.kind))
                        troop.waterMoveAbility += value;
                }
            }
            else if (checkLand == 1)
            {
                if (kinds == null)
                {
                    troop.landMoveAbility += value;
                }
                else
                {
                    if (kinds.Contains(troop.LandTroopType.kind))
                        troop.landMoveAbility += value;
                }
            }
            else if (checkLand == 2)
            {
                if (kinds == null)
                {
                    troop.waterMoveAbility += value;
                }
                else
                {
                    if (kinds.Contains(troop.WaterTroopType.kind))
                        troop.waterMoveAbility += value;
                }
            }
        }
    }
}
