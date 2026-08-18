using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 增加势力全队伍的兵力上限 
    /// value: 增加值 kinds: 兵种类型
    /// </summary>
    public class ForceTroopMaxTroop : ForceTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateMaxTroops += OnTroopCalculateMaxTroops;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateMaxTroops -= OnTroopCalculateMaxTroops;
        }

        void OnTroopCalculateMaxTroops(City city, Troop troop, OverrideData<int> overrideData)
        {
            if (!CheckForceTroop(troop))
                return;
            overrideData.Value += value;
        }

    }
}
