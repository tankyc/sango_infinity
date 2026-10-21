using Sango.Core.Tools;
using Sango.Render;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 被战法命中时用同样的战法反击（容器触发）。
    ///
    /// 必须放进「触发器 + 效果」容器的 actionList 内；自身不订阅事件。
    /// 参数：value 为反击时使用的战法 Id，留空则使用命中的原战法。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class TroopSkillBack : TroopActionBase
    {
        Condition condition;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                condition.Init(conObj, sangoObjects);
            }
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if (trigger == null) return;
            if (trigger.TargetTroop != Troop) return;
            if (trigger.ActionSkill == null) return;
            TroopActionConditionDatabase troopActionConditionDatabase = new TroopActionConditionDatabase(trigger.ActionSkill, trigger.ActionCell);
            if (condition != null && !condition.Check(troopActionConditionDatabase))
            {
                return;
            }
            SkillInstance skillInstance = SkillInstance.Create(Troop, trigger.ActionSkill.skill);
            int critical = skillInstance.CheckCritical(trigger.ActionCell);
            if (critical > 100 && !skillInstance.IsNormal())
            {
                TroopSpellSkillCriticalEvent @event = RenderEvent.Instance.Create<TroopSpellSkillCriticalEvent>();
                @event.Init(skillInstance, trigger.ActionCell, skillInstance.tempCriticalFactor);
                skillInstance.master.skillRenderEvent = @event;
                RenderEvent.Instance.Add(@event);
            }
            else
            {
                TroopSpellSkillEvent @event = RenderEvent.Instance.Create<TroopSpellSkillEvent>();
                @event.Init(skillInstance, trigger.ActionCell);
                skillInstance.master.skillRenderEvent = @event;
                RenderEvent.Instance.Add(@event);
            }
        }
    }
}
