namespace Sango.Core.Action
{
    /// <summary>
    /// 辅佐能力: 由"辅佐"特性(Features.json)通过 actionEntities 挂载。
    /// 只要部队的 actionList 中存在本 Action, 即代表该部队(Leader/Member1/Member2 任一武将)持有辅佐,
    /// 于是即便未建立人际关系也可获得支援攻击。代码只按本类型判定, 不再依赖任何特性 Id。
    /// 本 Action 不再直接参与援助判定: 它订阅 OnTroopCalculateAttribute,
    /// 在部队属性计算阶段把数据里配置的辅佐概率(百分比)以取Max(不叠加)的方式写入 Troop.assistChance,
    /// 由 Troop.CalcAssistChance 在关系档位均未命中时读取该值兜底判定。
    /// 接入事件：OnTroopCalculateAttribute
    /// </summary>
    public class TroopAssistAttack : TroopActionBase
    {
        public override void Init(Newtonsoft.Json.Linq.JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            // 订阅部队属性计算事件: InitActionList(装配本Action)总是先于 CalculateAttribute 触发,
            // 因此不会漏掉首次计算; Clear 时退订, 避免跨剧本残留订阅
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            // 与其他修改型 Action 同一套归属过滤: 只作用于装配本Action的部队(或同势力部队)
            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            // 取Max不叠加: 多名武将同时持有"辅佐"时只保留最高档,
            // 每次属性重算前 Troop.assistChance 已复位为 0, 此处写回不会越滚越大
            troop.assistChance = System.Math.Max(troop.assistChance, value);
        }
    }
}
