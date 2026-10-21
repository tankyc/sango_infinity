using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队恢复：在配置的事件发生时，按百分比把事件数值(如战法造成的伤害)转换成兵力补回部队。
    /// percent：恢复百分比, 恢复兵力 = 事件数值 × percent / 100
    /// targetType：恢复对象(判断敌我), 0 己方(触发事件的部队, 默认) 1 敌方(事件的目标部队)
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class TroopRecure : TroopActionBase
    {
        /// <summary>
        /// 恢复百分比,恢复兵力 = 事件数值 × percent / 100
        /// </summary>
        int percent = 0;

        /// <summary>
        /// 恢复对象(判断敌我), 0 己方(触发事件的部队) 1 敌方(事件的目标部队)
        /// </summary>
        int targetType = 0;

        /// <summary>
        /// 初始化部队恢复效果
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            percent = p.Value<int>("percent");
            targetType = p.Value<int>("targetType");
        }

        public override void Clear()
        {

        }

        /// <summary>
        /// 执行部队恢复
        /// </summary>
        /// <param name="trigger">触发事件</param>
        public override void Execute(Trigger trigger)
        {
            if (trigger == null) return;

            // 本效果只归属于触发事件的发起部队
            if (trigger.ActionTroop != Troop) return;

            // 没有事件数值(如伤害值)时无从换算,直接返回
            if (trigger.ValueOverride == null) return;

            // 按 targetType 判断恢复的是敌是我: 0 己方(发起部队) 1 敌方(目标部队)
            Troop target = targetType == 1 ? trigger.TargetTroop : trigger.ActionTroop;
            if (target == null || !target.IsAlive) return;

            int hp = trigger.ValueOverride.Value * percent / 100;
            if (hp > 0)
            {
                target.ChangeTroops(hp, trigger.ActionSkill, 0);
            }
        }
    }
}
