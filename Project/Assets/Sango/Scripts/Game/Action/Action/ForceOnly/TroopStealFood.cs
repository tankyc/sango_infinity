using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种攻击力增加
    /// value: 增加值 kinds: 兵种类型
    /// </summary>
    public class TroopStealFood : ForceTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
        }

        public override void Clear()
        {
        }

        public override void Execute(Trigger trigger, params object[] sangoObjects)
        {
            if (trigger == null) return;
            if (Force != trigger.ActionForce) return;
            if (trigger.ActionTroop == null) return;

            Troop troop = trigger.ActionTroop;
            int stealNum = System.Math.Min(troop.troops / 2, troop.Attack * GameRandom.Range(10, 21) / 10);
            if (kinds == null)
            {
                troop.ChangeFood(stealNum);
                if (trigger.TargetTroop != null)
                    trigger.TargetTroop.ChangeFood(-stealNum);
            }
            else
            {
                if (kinds.Contains(troop.TroopType.kind))
                {
                    troop.ChangeFood(stealNum);
                    if (trigger.TargetTroop != null)
                        trigger.TargetTroop.ChangeFood(-stealNum);
                }
            }
        }

    }
}
