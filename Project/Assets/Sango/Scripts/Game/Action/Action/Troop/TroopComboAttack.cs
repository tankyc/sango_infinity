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
        /// <summary>
        /// 配置非法（条件类名写错 / 未注册）时直接停用。
        /// 【为什么不能"忽略条件"】本类的条件决定"要不要连击"，
        /// 条件缺失若被当成"通过"，等于变成无条件连击 / 对任意目标连击，比不触发危险得多。
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
                    Sango.Log.Error($"TroopComboAttack 的条件类未注册：{conObj.Value<string>("class")}，该配置已停用（否则会变成无条件连击）");
                    invalidConfig = true;
                }
            }
            conObj = p.Value<JObject>("comboCondition");
            if (conObj != null)
            {
                comboCondition = Condition.Create(conObj.Value<string>("class"));
                if (comboCondition != null)
                    comboCondition.Init(conObj, sangoObjects);
                else
                {
                    Sango.Log.Error($"TroopComboAttack 的连击条件类未注册：{conObj.Value<string>("class")}，该配置已停用（否则会对任意目标连击）");
                    invalidConfig = true;
                }
            }
            count = p.Value<int>("count");
            currentCount = 0;
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if (invalidConfig) return;
            if (trigger == null) return;
            if (trigger.ActionTroop != Troop) return;
            if (trigger.ActionSkill == null) return;
            TroopActionConditionDatabase troopActionConditionDatabase = new TroopActionConditionDatabase(trigger.ActionSkill, trigger.TargetCell);
            if (condition != null && !condition.Check(troopActionConditionDatabase))
            {
                // 【自愈】不满足触发条件 = 这次出手本来就不该连击。
                // 计数必须一并归零：否则上一次连击链若在"外层条件"这一步中断，
                // currentCount 会卡在中间值，导致下一次正常普攻走进 else 分支被跳过（表现成"隔次不连击"）。
                currentCount = 0;
                return;
            }

            if (currentCount < count)
            {
                // 【记账位置】只有"条件通过且真的排了连击事件"才递增。
                // 原写法先 ++ 再判 comboCondition：条件不满足时没有后续事件回到这里，
                // 计数就卡在 1，下一次普攻直接走 else 被跳过。
                if (comboCondition != null && !comboCondition.Check(troopActionConditionDatabase))
                {
                    // 条件不满足：本次不出连击，计数一并归零。
                    // 连击链中途失败时没有后续事件再回到这里，不清零会把计数卡在中间值，
                    // 下一次普攻直接走 else 被跳过（实际触发率会从 50% 掉到 25%）。
                    currentCount = 0;
                    return;
                }

                currentCount++;
                SkillInstance skillInstance = trigger.ActionSkill;
                // 【一次出手 = 一个序号】连击复用同一个技能实例，但它是独立的一发伤害，
                // 必须换号，否则"一次出手只结算一次"的 Action 会把连击这一发当成同一发跳过。
                skillInstance.BeginNewCast();
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
            else
            {
                currentCount = 0;
            }
        }
    }
}
