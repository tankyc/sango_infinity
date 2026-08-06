using Sango.Core.Tools;
using Sango.Render;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 战法额外释放连击
    /// count： 额外次数
    /// </summary>
    public class TroopComboAttack : TroopActionBase
    {
        int count;
        int currentCount;
        Condition condition;
        Condition comboCondition;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                condition.Init(conObj, sangoObjects);
            }
            conObj = p.Value<JObject>("comboCondition");
            if (conObj != null)
            {
                comboCondition = Condition.Create(conObj.Value<string>("class"));
                comboCondition.Init(conObj, sangoObjects);
            }
            count = p.Value<int>("count");
            currentCount = 0;
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if (trigger == null) return;
            if (trigger.ActionTroop != Troop) return;
            if (trigger.ActionSkill == null) return;
            TroopActionConditionDatabase troopActionConditionDatabase = new TroopActionConditionDatabase(trigger.ActionSkill, trigger.TargetCell);
            if (condition != null && !condition.Check(troopActionConditionDatabase))
            {
                return;
            }

            if (currentCount < count)
            {
                currentCount++;
                if (comboCondition != null && comboCondition.Check(troopActionConditionDatabase))
                {
                    SkillInstance skillInstance = trigger.ActionSkill;
                    if (skillInstance.tempCriticalFactor >= 100 && !skillInstance.IsNormal())
                    {
                        TroopSpellSkillCriticalEvent @event = RenderEvent.Instance.Create<TroopSpellSkillCriticalEvent>();
                        @event.Init(skillInstance, trigger.TargetCell, skillInstance.tempCriticalFactor);
                        skillInstance.master.skillRenderEvent = @event;
                        RenderEvent.Instance.Add(@event);
                    }
                    else
                    {
                        TroopSpellSkillEvent @event = RenderEvent.Instance.Create<TroopSpellSkillEvent>();
                        @event.Init(skillInstance, trigger.TargetCell);
                        skillInstance.master.skillRenderEvent = @event;
                        RenderEvent.Instance.Add(@event);
                    }
                }
            }
            else
            {
                currentCount = 0;
            }
        }
    }
}
