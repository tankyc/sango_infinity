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

            // 兵种限定：原版是"枪兵 + 兵粮术科技"才生效（s11_sys_damage.cpp:1516-1529），
            // 本项目由 kinds 配置承担兵种限定。
            if (kinds != null)
            {
                if (troop.TroopType == null || !kinds.Contains(troop.TroopType.kind))
                    return;
            }

            Troop target = trigger.TargetTroop;

            // 夺取量 = min(自身兵力 / 2, 自身攻击力 × [1.0, 2.0])
            // 对应原版 calc_food_damage 的 b 项 max(troops/2, 1) 与 a 项 atk + rand_int(atk)。
            int stealNum = System.Math.Min(troop.troops / 2, troop.Attack * GameRandom.Range(10, 21) / 10);

            // 原版 c 项：不得超过目标现有兵粮。
            // 不加这一条时，扣粮会被 ChangeFood 钳到 0，而自身仍按 stealNum 增加 —— 等于凭空造粮。
            if (target != null)
                stealNum = System.Math.Min(stealNum, target.food);

            // 【未对齐】原版 d 项 max_food - food（自身装粮上限）在本项目没有对应字段，暂不限制。
            if (stealNum <= 0) return;

            troop.ChangeFood(stealNum);
            target?.ChangeFood(-stealNum);
        }

    }
}
