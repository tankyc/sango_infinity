using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队忽略ZOC
    /// land: 0 忽略水上 1 忽略陆地 2都忽略
    /// </summary>
    public class TroopIgnoreZOC : TroopActionBase
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

            if (value == 0)
            {
                troop.ignoreWaterZOC = true;
                troop.ignoreLandZOC = false;
            }
            else if (value == 1)
            {
                troop.ignoreWaterZOC = false;
                troop.ignoreLandZOC = true;
            }
            else 
            {
                troop.ignoreWaterZOC = true;
                troop.ignoreLandZOC = true;
            }
        }
    }
}
