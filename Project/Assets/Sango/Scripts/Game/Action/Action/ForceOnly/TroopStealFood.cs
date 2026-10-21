using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 夺取目标部队兵粮：把目标部队的兵粮转到自己身上。
    ///
    /// 触发时机：容器触发。本类只实现 Execute(Trigger)，自身不订阅任何 GameEvent，
    /// 必须放进「触发器 + 效果」容器的 actionList 里，由外层触发器决定何时执行；
    /// 单独挂在特技 / 技法上不会生效。
    ///
    /// 参数：
    /// kinds 限定生效的兵种类型，null 表示不限制
    /// 夺取量 = min(自身兵力 / 2, 自身攻击力 × 随机(1.0 ~ 2.0) 倍)
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
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

        public override void Execute(Trigger trigger)
        {
            if (trigger == null) return;

            // 【静默失效修复】原先写死 if (Force != trigger.ActionForce) return;
            // Force 为 null（效果挂在部队等非势力宿主上）时该判断恒成立，效果永远不会生效。
            // 改为"仅当装配了势力时才校验归属"。
            if (Force != null && Force != trigger.ActionForce) return;
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
