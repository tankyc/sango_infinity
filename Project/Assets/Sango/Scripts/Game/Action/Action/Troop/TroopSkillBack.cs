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
        /// <summary>
        /// 配置非法（条件类名写错 / 未注册）时直接停用。
        /// 条件是"满足才反射"的闸门，缺失若被当成"通过"就等于无条件反射，风险更大。
        /// </summary>
        bool invalidConfig;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            // 实例可能被复用：先清掉上一次的停用标记，否则改好配置重新 Init 也不生效
            invalidConfig = false;
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                {
                    Sango.Log.Error($"TroopSkillBack 的条件类未注册：{conObj.Value<string>("class")}，该配置已停用（否则会变成无条件反射）");
                    invalidConfig = true;
                }
            }
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            // 【最优先】眩晕 / 混乱（Stun）状态下不能反击，战法反制同样适用。
            if (Troop == null || Troop.isStun)
                return;

            if (invalidConfig) return;
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
