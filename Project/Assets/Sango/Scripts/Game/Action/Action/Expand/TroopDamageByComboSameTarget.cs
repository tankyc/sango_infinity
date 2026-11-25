using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 连续攻击同一目标递增伤害（汉土特技 0.4.22 —— 55559 叠锋）。
    ///
    /// <para>
    /// 【设计原文】连续物理攻击同一支敌军，每次伤害比基础提高 15%，最大 45%；
    /// 改打其他部队重新累计。
    /// </para>
    /// <para>
    /// 【为什么不新开事件】沿用官方 <c>OnTroopChangeTroops</c>：
    /// 连续次数只是本队的一个持久状态（<see cref="Troop.comboAttackCount"/>），
    /// 在既有的"兵力变化前改数值"挂点上换算成百分比即可。
    /// </para>
    /// <para>
    /// 【口径】
    /// · 首击 +0%，第 2 次 +<c>perHit</c>，第 3 次 +2×<c>perHit</c>……封顶 <c>maxBonus</c>；
    /// · 换目标立刻重置为 1（连击不跨目标继承），同目标跨回合继续累计（设计未要求回合重置）；
    /// · 只在"我方是攻击方"且非反击时计数（反击时我方是被打的一方）；
    /// · 同一个战法打同一目标多段时会多次触发，即"多段也按段数累计"，
    ///   若以后设计要求"一次战法只算一次"，把计数挪到战法结算入口即可，本类不用改。
    /// </para>
    /// <para>
    /// 【状态持久化】<c>comboAttackTargetId</c> / <c>comboAttackCount</c> 带 <c>[JsonProperty]</c>，
    /// 存读档后连击不丢；部队解散重建时字段归 0，天然重置。
    /// </para>
    /// JSON 参数：<c>perHit</c>（默认 15）、<c>maxBonus</c>（默认 45，配 0/负数表示不封顶）。
    /// </summary>
    public class TroopDamageByComboSameTarget : TroopTroopActionBase
    {
        /// <summary>每多连续攻击一次提高的百分点（默认 15）</summary>
        int perHit = 15;
        /// <summary>加成上限（百分点，默认 45）；hasMaxBonus = false 表示不封顶</summary>
        int maxBonus = 45;
        /// <summary>是否启用上限</summary>
        bool hasMaxBonus = true;

        /// <summary>初始化并订阅兵力变化事件</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">宿主对象（[0] 必须是部队）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int ph = p.Value<int>("perHit");
            perHit = ph != 0 ? ph : 15;

            // 缺键不能用 Value<int>（返回 0 会被误读成"不封顶"）
            JToken mbToken = p["maxBonus"];
            if (mbToken == null)
            {
                maxBonus = 45;
                hasMaxBonus = true;
            }
            else
            {
                int mb = mbToken.ToObject<int>();
                hasMaxBonus = mb > 0;
                maxBonus = hasMaxBonus ? mb : 0;
            }

            if (Troop == null)
            {
                Sango.Log.Error("TroopDamageByComboSameTarget 必须装配在部队上（连击状态存在部队身上），该配置不会生效");
                return;
            }

            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        /// <summary>
        /// 兵力变化前按连击次数提高伤害。
        /// </summary>
        /// <param name="defencer">兵力变化的一方（被攻击目标）</param>
        /// <param name="atker">施加方</param>
        /// <param name="atkBack">反击强度因子，0 = 非反击</param>
        /// <param name="overrideData">待修正的兵力变化量（负数为掉兵）</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (overrideData == null || overrideData.Value >= 0) return;
            if (IsAttackBack(atkBack)) return;
            if (!CheckTroop(defencer, atker)) return;

            Troop self = this.Troop;
            if (self == null || defencer == null) return;

            // 同一目标延续连击，换目标从 1 重新累计
            if (self.comboAttackTargetId == defencer.Id)
            {
                self.comboAttackCount++;
            }
            else
            {
                self.comboAttackTargetId = defencer.Id;
                self.comboAttackCount = 1;
            }

            // 首击 +0%，第 n 次 +perHit × (n - 1)；用 long 计算，避免次数异常时溢出
            long bonus = (long)perHit * (self.comboAttackCount - 1);
            if (hasMaxBonus && bonus > maxBonus)
                bonus = maxBonus;
            if (bonus <= 0)
                return;

            overrideData.Value = (int)(overrideData.Value * (100 + bonus) / 100);
        }

        /// <summary>执行（本类只做伤害修正，不参与触发器链）</summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
