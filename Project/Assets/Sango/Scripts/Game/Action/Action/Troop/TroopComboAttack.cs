using Sango.Core.Tools;
using Sango.Render;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 战法额外释放连击（容器触发）。
    ///
    /// 必须放进「触发器 + 效果」容器的 actionList 内；自身不订阅事件。
    /// 参数：count 额外释放次数；value 连击时使用的战法 Id（留空则用原战法）；
    /// condition 触发条件；comboCondition 连击目标条件。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
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
