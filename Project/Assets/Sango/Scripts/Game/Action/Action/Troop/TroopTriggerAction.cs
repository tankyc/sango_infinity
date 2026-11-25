using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队触发器容器：triggerList 决定触发时机，actionList 内的效果在该时机依次执行。
    ///
    /// 子效果自身不订阅任何事件，单独挂在特技 / 技法上不会生效。
    /// 装配逻辑已抽到 ActionContainer，建筑 / 城市分别用 BuildingTriggerAction / CityTriggerAction。
    /// </summary>
    public class TroopTriggerAction : TroopActionBase
    {
        /// <summary>容器内容</summary>
        readonly ActionContainer container = new ActionContainer();

        /// <summary>
        /// 容器级条件（可选）。
        ///
        /// 【为什么放在容器上】容器内的子效果（TroopChangeMorale / TroopRecure 等）不订阅事件、
        /// 也不解析 condition，过去只能靠"给每个子效果加一份过滤"。把条件提到容器后，
        /// 扫荡 / 威风 / 掎角 / 攻心 / 怒发 这类"触发器 + 效果"的特技都能用一条
        /// condition 统一限定"只在一般攻击 / 只在战法"（对应 SkillIsNormalSkill / SkillIsStrategySkill）。
        /// </summary>
        protected Condition condition;

        /// <summary>
        /// 配置了 condition 但条件类未注册：整条效果作废。
        ///
        /// 原实现只打日志后把 condition 留在 null，OnTrigger 里 `condition != null` 视为通过，
        /// 等于“条件写错反而全量生效”，在数百条特技配置下极难发现。
        /// </summary>
        protected bool conditionInvalid;

        /// <summary>
        /// 初始化容器
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            container.Init(p, sangoObjects);

            // 复用对象先清旧条件，避免上一次配置残留
            condition = null;
            conditionInvalid = false;

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                string conditionClass = conObj.Value<string>("class");
                condition = Condition.Create(conditionClass);
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                {
                    conditionInvalid = true;
                    Sango.Log.Error($"部队触发器容器的条件类未注册：{conditionClass}，整条效果已作废（装配宿主：{Troop?.Name ?? Force?.Name ?? "未知"}）");
                }
            }
        }

        /// <summary>
        /// 清理容器（退订所有触发器 + 子效果）
        /// </summary>
        public override void Clear()
        {
            container.Clear();
        }

        /// <summary>
        /// 触发器回调：依次执行容器内的效果
        /// </summary>
        /// <param name="trigger">触发事件</param>
        public virtual void OnTrigger(Trigger trigger)
        {
            // 条件类未注册：整条效果作废，不让它退化成“无条件生效”
            if (conditionInvalid)
                return;

            // 容器级条件先过：Trigger 本身实现了 IConditionDatabase，
            // 因此 ActionSkill / ActionTroop / TargetTroop 等字段都能被条件类直接读取。
            if (condition != null && !condition.Check(trigger))
                return;

            container.Dispatch(trigger);
        }
    }
}
