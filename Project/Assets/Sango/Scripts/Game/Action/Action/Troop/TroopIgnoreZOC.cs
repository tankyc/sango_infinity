using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队忽略 ZOC（区域控制），无参数。
    ///
    /// 接入事件：OnTroopCalculateAttribute（属性重算时写入标记，供移动判定读取）。
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
            if (Force != null && troop.BelongForce != Force) return;
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
