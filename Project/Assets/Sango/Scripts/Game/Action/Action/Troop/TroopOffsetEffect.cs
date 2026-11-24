using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 造成位移触发：持技部队**以战法把敌方部队推动格子之后**，对被打退的部队追加效果。
    ///
    /// 【时机】订阅 <c>GameEvent.OnTroopSkillOffset</c>。
    /// 该事件只在"目标真的换了格子"时派发（被挡住 / 无处可去不算），
    /// 施法者自己位移（Master 系列战法）不会走到这里，与"对敌军造成位移"的语义一致。
    ///
    /// 【参数】
    /// buffId             追加的状态 Id（"乘胜逐北"用 Buffs.json Id 1 混乱）
    /// buffProbability    附加概率（万分比）
    /// buffTurns          状态持续回合数
    /// moraleStealPercent 掠夺目标当前气力的百分比（"狼奔豕突"= 10）
    /// moraleStealToSelf  掠夺来的气力是否转给持技部队（false 则只是削减目标）
    /// </summary>
    public class TroopOffsetEffect : TroopActionBase
    {
        /// <summary>追加状态 Id</summary>
        int buffId;
        /// <summary>状态附加概率（万分比）</summary>
        int buffProbability;
        /// <summary>状态持续回合数</summary>
        int buffTurns;
        /// <summary>掠夺气力百分比</summary>
        int moraleStealPercent;
        /// <summary>掠夺来的气力是否转给持技部队</summary>
        bool moraleStealToSelf;

        /// <summary>
        /// 初始化参数并订阅位移事件。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">装配宿主（[0] 部队，[1] 触发装配的武将）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            buffId = p.Value<int>("buffId");
            buffProbability = p.Value<int>("buffProbability");
            buffTurns = p.Value<int>("buffTurns");
            moraleStealPercent = p.Value<int>("moraleStealPercent");
            moraleStealToSelf = p.Value<bool>("moraleStealToSelf");

            // 装配宿主既不是部队也不是势力时不订阅事件（避免"任何位移都由本效果结算"）
            if (Troop == null && Force == null)
            {
                Sango.Log.Error($"位移触发效果的装配宿主既不是部队也不是势力，特技不会生效：{p}");
                return;
            }

            GameEvent.OnTroopSkillOffset += OnTroopSkillOffset;
        }

        /// <summary>
        /// 退订事件。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopSkillOffset -= OnTroopSkillOffset;
        }

        /// <summary>
        /// 事件驱动效果，不需要外部主动触发。
        /// </summary>
        /// <param name="trigger">触发参数（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 位移回调：确认是持技部队推动的，再对目标追加效果。
        /// </summary>
        /// <param name="atker">施法部队</param>
        /// <param name="target">被推动的部队</param>
        /// <param name="offset">位移格数（未使用）</param>
        void OnTroopSkillOffset(Troop atker, Troop target, int offset)
        {
            if (atker == null || target == null || !target.IsAlive)
                return;

            if (Troop != null)
            {
                if (atker != Troop)
                    return;
            }
            else if (Force != null && atker.BelongForce != Force)
            {
                return;
            }

            // ① 概率附加状态（乘胜逐北 → 混乱）
            if (buffId > 0 && GameRandom.Chance(buffProbability <= 0 ? 10000 : buffProbability, 10000))
                target.AddBuff(buffId, buffTurns <= 0 ? 1 : buffTurns, atker);

            // ② 掠夺气力（狼奔豕突）
            if (moraleStealPercent > 0)
            {
                int steal = target.morale * moraleStealPercent / 100;
                if (steal > 0)
                {
                    target.ChangeMorale(-steal);
                    if (moraleStealToSelf)
                        atker.ChangeMorale(steal);
                }
            }
        }
    }
}
