using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 无敌：**免疫一切兵力损失**。
    ///
    /// 【挂载点】<c>GameEvent.OnTroopChangeTroops</c> —— 本项目所有兵力增减
    /// （普攻 / 战法 / 毒 / 火 / 落石）最终都汇聚到 <see cref="Troop.ChangeTroops"/>，
    /// 它在真正改 <c>troops</c> 之前会派发该事件并允许改写增量。
    /// 因此在这里把"负增量"改成 0，即可覆盖全部伤害来源，
    /// 不需要在每种伤害公式里各加一个"是否无敌"的分支。
    ///
    /// 【写入 / 移除成对】订阅在 <see cref="Init"/>，退订在 <see cref="Clear"/>，
    /// 由 <see cref="BuffInstance"/> 的生命周期保证成对调用。
    /// </summary>
    public class Invincible : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (defencer == null || defencer != master.Target || overrideData == null)
                return;

            // 只拦"掉兵"；治疗 / 补给这类正增量照常放行
            if (overrideData.Value < 0)
                overrideData.Value = 0;
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
