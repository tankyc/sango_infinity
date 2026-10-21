using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 在触发瞬间增减部队气力（容器触发）。
    ///
    /// 必须放进「触发器 + 效果」容器的 actionList 内；自身不订阅事件。
    /// 参数：value 气力变化值；targetType 0 行动方 / 1 目标方；
    /// isDefender 为 true 时要求归属部队是本次战斗的受击方。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class TroopChangeMorale : TroopActionBase
    {
        int targetType = 0;
        bool isDefender = false;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            targetType = p.Value<int>("targetType");
            isDefender = p.Value<bool>("isDefender");
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if (trigger == null) return;

            // 归属判定：装配在部队上时要求归属部队就是本次的（行动方 / 受击方）；
            // 装配在势力技法上（Troop == null）时只校验势力归属。
            // 原先写 if (trigger.ActionTroop != Troop) return;，Force 绑定时 Troop 为 null，
            // 判断恒成立，效果会静默失效。
            Troop owner = isDefender ? trigger.TargetTroop : trigger.ActionTroop;
            if (owner == null) return;
            if (Troop != null && owner != Troop) return;
            if (Force != null && owner.BelongForce != Force) return;

            if (targetType == 0)
            {
                if (trigger.ActionTroop != null)
                    trigger.ActionTroop.ChangeMorale(value);
            }
            else if (targetType == 1)
            {
                if (trigger.TargetTroop != null)
                    trigger.TargetTroop.ChangeMorale(value);
            }
        }
    }
}
