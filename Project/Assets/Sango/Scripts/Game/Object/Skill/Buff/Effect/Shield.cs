using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 护盾：按 <c>shieldValue</c> 点数**吸收兵力损失**，盾值耗尽后失效。
    ///
    /// 【挂载点】与 <see cref="Invincible"/> 相同 —— <c>GameEvent.OnTroopChangeTroops</c>。
    /// 区别：无敌是把伤害清零，护盾是按剩余盾值扣减伤害。
    ///
    /// 【盾值为什么存在实例里而不是 Troop 上】它属于"这一个 Buff 实例"的私有资源：
    ///   · 同一部队被反复施加护盾时，每个实例各算各的，先加的先耗尽；
    ///   · Buff 到期 / 被驱散时随实例一起消失，不会在部队上留下"幽灵盾值"。
    /// 数值来自数据 <c>"shieldValue": 100</c>，代码不写死。
    /// </summary>
    public class Shield : BuffEffect
    {
        /// <summary>剩余护盾值（吸收伤害后递减）</summary>
        int shieldValue;

        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            shieldValue = p.Value<int>("shieldValue");
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (defencer == null || defencer != master.Target || overrideData == null)
                return;

            int num = overrideData.Value;

            // 只吸收伤害（负增量）；治疗 / 补给放行
            if (num >= 0 || shieldValue <= 0)
                return;

            int absorb = -num;
            if (absorb > shieldValue)
                absorb = shieldValue;

            shieldValue -= absorb;
            overrideData.Value = num + absorb;

            if (absorb > 0)
            {
                Sango.Log.Info($"[护盾] {defencer.Name} 吸收 {absorb} 点伤害，剩余护盾 {shieldValue}");
            }
        }

        public override void Action(BuffInstance buffInstance, Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
        }

        public override void Clear()
        {
            base.Clear();
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }
    }
}
