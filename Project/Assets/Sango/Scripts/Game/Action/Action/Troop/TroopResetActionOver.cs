using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 在触发瞬间给部队增加额外行动次数（容器触发）。
    ///
    /// 必须放进「触发器 + 效果」容器的 actionList 内；自身不订阅事件。
    /// 参数：value 增加的行动次数（默认 1）；kinds 兵种类型过滤；condition 额外条件。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
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
            if (trigger == null) return;

            // 【空引用修复】本 Action 既可能装配在部队上（Troop），也可能装配在势力技法上（Force）。
            // 后者 Troop 字段为 null，原先直接读 Troop.isExtraAction / Troop.LandTroopType 会抛 NullReferenceException。
            // 统一改为从触发事件里取当前部队（ActionTroop），两者都能工作。
            Troop target = trigger.ActionTroop;
            if (target == null) return;

            if (Force != null && target.BelongForce != Force) return;
            if (Troop != null && Troop != target) return;

            // 额外行动不允许再增加二动次数
            if (target.isExtraAction) return;

            if (kinds != null && !kinds.Contains(target.LandTroopType.kind) && !kinds.Contains(target.WaterTroopType.kind))
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(target)))
                return;

            target.ActionOverCount += value;
        }
    }
}
