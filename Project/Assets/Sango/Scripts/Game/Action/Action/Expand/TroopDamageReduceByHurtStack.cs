using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 受击累计物理减伤（汉土特技 0.4.22 —— 55555 久御）。
    ///
    /// <para>
    /// 【设计原文】每受到一次敌军物理攻击，获得 2% 物理减伤，最高 20%。
    /// </para>
    /// <para>
    /// 【为什么不新开事件】沿用官方 <c>OnTroopChangeTroops</c>：
    /// 累计层数只是本队的一个持久状态（<see cref="Troop.physicalHurtCount"/>），
    /// 在既有的"兵力变化前改数值"挂点上换算成减伤百分比即可。
    /// </para>
    /// <para>
    /// 【口径】
    /// · 只处理"我方是受击方"的事件（<c>defencer == Troop</c>），且只算敌方物理攻击；
    ///   计略 / 火焰 / 建筑反击不算（<paramref name="atker"/> 不是 <see cref="SkillInstance"/> 或为计略）；
    /// · 反击被打也算"受到物理攻击"，因此**不**按 <c>atkBack</c> 过滤；
    /// · 先按已有层数减伤、再累计本次 —— 首击不享受（设计"受到后获得"的字面口径）；
    /// · 层数封顶 <c>maxStack</c>（默认 10 → 20%）。设计未要求重置，故持久累计；
    ///   若以后要"每回合清零"，在 <c>OnTroopTurnStart</c> 里把字段归 0 即可，本类不用改。
    /// </para>
    /// <para>
    /// 【状态持久化】<c>physicalHurtCount</c> 带 <c>[JsonProperty]</c>，存读档后层数不丢。
    /// </para>
    /// JSON 参数：<c>perHit</c>（默认 2）、<c>maxStack</c>（默认 10）。
    /// </summary>
    public class TroopDamageReduceByHurtStack : TroopTroopActionBase
    {
        /// <summary>每层减伤百分点（默认 2）</summary>
        int perHit = 2;
        /// <summary>最大层数（默认 10，即 20% 减伤）</summary>
        int maxStack = 10;

        /// <summary>初始化并订阅兵力变化事件</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">宿主对象（[0] 必须是部队）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int ph = p.Value<int>("perHit");
            perHit = ph > 0 ? ph : 2;

            // 缺键不能用 Value<int>（返回 0 会被误读成"无层数"）
            JToken msToken = p["maxStack"];
            if (msToken == null)
                maxStack = 10;
            else
            {
                int ms = msToken.ToObject<int>();
                maxStack = ms > 0 ? ms : 10;
            }

            if (Troop == null)
            {
                Sango.Log.Error("TroopDamageReduceByHurtStack 必须装配在部队上（层数存在部队身上），该配置不会生效");
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
        /// 兵力变化前按累计层数减伤，并累计本次受击。
        /// </summary>
        /// <param name="defencer">兵力变化的一方（这里是本队）</param>
        /// <param name="atker">施加方</param>
        /// <param name="atkBack">反击强度因子（本类不据此过滤：反击被打同样算受击）</param>
        /// <param name="overrideData">待修正的兵力变化量（负数为掉兵）</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (overrideData == null || overrideData.Value >= 0) return;

            Troop self = this.Troop;
            if (self == null || !ReferenceEquals(defencer, self)) return;

            // 只算敌方物理攻击；计略 / 火焰 / 建筑反击（非 SkillInstance）不算
            SkillInstance skill = atker as SkillInstance;
            if (skill == null || skill.IsStrategy()) return;

            if (!CheckTroop(defencer, atker)) return;

            int count = self.physicalHurtCount;
            if (count > maxStack) count = maxStack;

            // 先应用已有层数（首击 0 层 → 不减伤），再累计本次
            if (count > 0)
            {
                int bonus = count * perHit;
                // 兜底：配置写得过大时不允许把掉兵量反向（100 - bonus 必须为正）
                if (bonus > 90) bonus = 90;
                overrideData.Value = overrideData.Value * (100 - bonus) / 100;
            }

            if (self.physicalHurtCount < maxStack)
                self.physicalHurtCount++;
        }

        /// <summary>执行（本类只做伤害修正，不参与触发器链）</summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
