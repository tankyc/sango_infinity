using Sango.Core.Tools;
using Sango.Render;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种类型战法的增减伤害  
    /// value： 增加值(百分比) 
    /// kinds： 兵种类型 
    /// checkLand： 0:只检查kinds 1:只对landType检查kinds 2只对waterType检查kinds 
    /// isDefender 0攻击方 1受击方 
    /// isNormal  0都可以 1是 2不是
    /// isRange 0都可以 1是 2不是
    /// condition： 额外条件 支持参数(troop,troop,skill)
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
            if (trigger.ActionSkill.isAdd) return;

            if (!CheckTroop(trigger.TargetTroop, trigger.ActionSkill)) return;

            SkillInstance skillInstance = SkillInstance.Create(trigger.TargetTroop, Scenario.Cur.GetObject<Skill>(value));
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
