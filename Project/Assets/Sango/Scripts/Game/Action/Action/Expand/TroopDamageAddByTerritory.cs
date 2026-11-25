using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 领地内物理攻击附加固定伤害（汉土特技 0.4.22 —— 55459 乘险）。
    ///
    /// <para>
    /// 【设计原文】在所属势力领地作战时，每次物理攻击增加本队攻击力三倍的伤害。
    /// </para>
    /// <para>
    /// 【为什么不新开事件】沿用官方 <c>OnTroopChangeTroops</c>（兵力变化前改数值）：
    /// 本类只是在掉兵量上再减一个固定值，事件、时序、归属过滤全部复用，
    /// 与 <see cref="TroopChangeDamage"/>（百分比）互补——那个改倍率，这个加绝对值。
    /// </para>
    /// <para>
    /// 【口径】
    /// · 只在"我方是攻击方"的掉兵事件里生效：<c>isDefender = 0</c>，
    ///   <c>CheckTroop</c> 已保证 <c>Troop == skill.master</c>；
    /// · 只在非反击时生效（<c>atkBack == 0</c>，反击时我方是被打的一方）；
    /// · 计略不是物理攻击，直接跳过（物理战法与普攻都算）；
    /// · 领地 = 我方所在格的所属都市（<c>Cell.BelongCity</c>）归属本势力；
    /// · 附加量 = 本队面板攻击力 × <c>attackFactor</c>，按固定值扣兵，不受百分比上限约束。
    /// </para>
    /// <para>
    /// 【只支持部队宿主】需要读取"本队攻击力"与"所在格"，势力级（技巧）宿主无法表达，
    /// Init 时直接报错停用，避免出现"配了却不生效"的静默失败。
    /// </para>
    /// JSON 参数：<c>attackFactor</c>（默认 3）。
    /// </summary>
    public class TroopDamageAddByTerritory : TroopTroopActionBase
    {
        /// <summary>附加伤害 = 本队攻击力 × 该倍数（默认 3，对齐设计原文"攻击力三倍"）</summary>
        int attackFactor = 3;

        /// <summary>初始化并订阅兵力变化事件</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">宿主对象（[0] 必须是部队）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int af = p.Value<int>("attackFactor");
            attackFactor = af > 0 ? af : 3;

            if (Troop == null)
            {
                Sango.Log.Error("TroopDamageAddByTerritory 必须装配在部队上（需要读取本队攻击力与所在格），该配置不会生效");
                return;
            }

            // 重复 Init 先退订再订阅，避免同一实例挂两次导致附加伤害翻倍
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        /// <summary>
        /// 兵力变化前追加领地物理攻击伤害。
        /// </summary>
        /// <param name="defencer">兵力变化的一方（被攻击者）</param>
        /// <param name="atker">施加方（这里是 SkillInstance）</param>
        /// <param name="atkBack">反击强度因子，0 = 非反击</param>
        /// <param name="overrideData">待修正的兵力变化量（负数为掉兵）</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (overrideData == null || overrideData.Value >= 0) return;
            if (IsAttackBack(atkBack)) return;
            if (!CheckTroop(defencer, atker)) return;

            Troop self = this.Troop;
            if (self == null || self.cell == null) return;

            // 计略不是物理攻击；CheckTroop 在 isDefender = 0 时已要求 atker 是 SkillInstance，
            // 这里仍显式判空，避免以后基类口径调整后空引用。
            SkillInstance skill = atker as SkillInstance;
            if (skill == null || skill.IsStrategy()) return;

            // 领地判定：本队所在格的所属都市必须归属本势力
            City belongCity = self.cell.BelongCity;
            if (belongCity == null || belongCity.BelongForce != self.BelongForce) return;

            int extra = self.Attack * attackFactor;
            if (extra <= 0) return;

            overrideData.Value -= extra;
        }

        /// <summary>执行（本类只做伤害修正，不参与触发器链）</summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
