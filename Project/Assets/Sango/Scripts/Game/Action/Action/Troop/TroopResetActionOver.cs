using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 让某兵种类型的二动
    /// value： 改变值
    /// kinds： 兵种类型 
    /// condition： 额外条件
    /// </summary>
    public class TroopResetActionOver : TroopTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            value = 1;
            base.Init(p, sangoObjects);
        }
        public override void Clear()
        {
        }
        public override void Execute(Trigger trigger)
        {
            if (Force != null && trigger.ActionForce != Force) return;
            if (Troop != null && Troop != trigger.ActionTroop) return;
            // 额外行动不允许再增加二动次数
            if (Troop.isExtraAction) return;
            if (kinds != null && !kinds.Contains(Troop.LandTroopType.kind) && !kinds.Contains(Troop.WaterTroopType.kind))
                return;

            if (condition != null)
            {
                TroopConditionDatabase troopConditionDatabase = new TroopConditionDatabase(Troop);
                if (!condition.Check(troopConditionDatabase))
                    return;
            }

            Troop.ActionOverCount += value;
        }
    }
}
