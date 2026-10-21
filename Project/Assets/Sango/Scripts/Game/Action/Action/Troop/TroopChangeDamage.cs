using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按百分比增减本次兵力变化量（value 可为负）。
    ///
    /// 接入事件：OnTroopChangeTroops（兵力增减结算时），这是"改伤害"的正确挂点。
    /// </summary>
    public class TroopChangeDamage : TroopTroopActionBase
    {

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        void OnTroopChangeTroops(Troop defencer, SangoObject atker,int atkBack, OverrideData<int> overrideData)
        {
            if (!CheckTroop(defencer, atker)) return;
            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }

        public override void Execute(Trigger trigger)
        {

        }

    }
}
