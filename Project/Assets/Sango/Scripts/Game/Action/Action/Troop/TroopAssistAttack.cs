namespace Sango.Core.Action
{
    /// <summary>
    /// 辅佐能力标记: 由"辅佐"特性(Features.json)通过 actionEntities 挂载。
    /// 只要部队的 actionList 中存在本 Action, 即代表该部队(Leader/Member1/Member2 任一武将)持有辅佐,
    /// 于是即便未建立人际关系也可获得支援攻击。代码只按本类型判定, 不再依赖任何特性 Id。
    /// value: 辅佐档的援助概率(百分比), 供外部按需读取; 当前 Troop 沿用常量 AssistChanceAssistFeature。
    /// </summary>
    public class TroopAssistAttack : TroopActionBase
    {
        /// <summary>
        /// 数据里配置的辅佐援助概率(百分比), 未配置时为 0。
        /// </summary>
        public int AssistChancePercent => value;

        public override void Init(TKNewtonsoft.Json.Linq.JObject p, params SangoObject[] sangoObjects)
        {
            // 仅作为能力标记装配到部队 actionList, 无需订阅任何事件。
            base.Init(p, sangoObjects);
        }

        public override void Clear()
        {
            // 无事件订阅, 无需清理。
        }
    }
}
