using Sango.Core.Tools;
using Sango.Render;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 遭受攻击时对攻击者施展指定战法（容器触发）。
    ///
    /// 必须放进「触发器 + 效果」容器的 actionList 内；自身不订阅事件。
    /// 参数：value 为反击使用的战法 Id。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class TroopAttackBack : TroopTroopActionBase
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

            // 【最优先】眩晕 / 混乱（Stun）状态下不能反击，容器触发的追加反击同样适用。
            Troop targetTroop = trigger.TargetTroop;
            if (targetTroop == null || targetTroop.isStun)
                return;

            if (trigger.ActionSkill.isAdd) return;

            if (!CheckTroop(targetTroop, trigger.ActionSkill)) return;

            SkillInstance skillInstance = SkillInstance.Create(targetTroop, Scenario.Cur.GetObject<Skill>(value));
            if (skillInstance != null)
            {
                skillInstance.isAdd = true;
                TroopSpellSkillEvent @event = RenderEvent.Instance.Create<TroopSpellSkillEvent>();
                @event.Init(skillInstance, trigger.ActionTroop.cell);
                skillInstance.master.skillRenderEvent = @event;
                RenderEvent.Instance.Add(@event);
            }
        }
    }
}
