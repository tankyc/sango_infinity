using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 冰冻：**无法行动**（与 <see cref="Stun"/> 同效，区别只在状态位与表现）。
    ///
    /// 【为什么还要单独一个类】<see cref="TroopStateFlag.Freeze"/> 与
    /// <see cref="TroopStateFlag.Stun"/> 是两位（<see cref="Buff.GetStateFlag"/> 会把
    /// Freeze 同时映射成 Freeze|Stun），这样业务侧只判 Stun 能拦住行动，
    /// 而驱散 / 免疫 / 表现又可以通过 Freeze 位区分"是冰冻还是扰乱"。
    /// </summary>
    public class Freeze : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
        }

        /// <summary>
        /// 回合开始：把该部队标记为行动结束。
        ///
        /// 【为什么在回合开始而不是每帧】<c>ActionOver</c> 是本项目"本回合不能再行动"的开关，
        /// 部队每回合开始会被重置为 false（见 <see cref="Troop.OnForceTurnStart"/> 的上层流程），
        /// 所以必须每回合重新压一次，直到 Buff 到期。
        /// </summary>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (troop != null && troop == master.Target)
                troop.ActionOver = true;
        }

        public override void Action(BuffInstance buffInstance, Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
        }

        public override void Clear()
        {
            base.Clear();
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
        }
    }
}
