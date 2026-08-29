using Sango.Core.Tools;
using Sango.Render;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 用同样的战法反击
    /// count： 额外次数
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
