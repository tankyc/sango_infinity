using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 暴走（Bousou）—— 失去控制，回合开始时自动扑向最近的敌方部队。
    ///
    /// 【为什么不需要改 AI】暴走只是"替部队改派一次任务"：
    /// 在 <c>OnTroopTurnStart</c> 时把任务强制改成 TroopDestroyTroop(最近敌军)，
    /// 之后完全走现有的任务执行链路（含严格模式下的评分）。
    /// 这样既不改 <see cref="Troop.UpdateAIOrder"/>，也不与玩家/AI 分流冲突。
    ///
    /// 【原版说明】原版 UnitState_Bousou 在 .cpp 里没有引用（死枚举），
    /// 效果按字面语义定义：无视指令、主动攻击最近敌人；没有敌人时保持原任务。
    /// </summary>
    public class Berserk : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
        }

        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (troop == null || troop != master.Target) return;
            if (!troop.IsAlive) return;

            Troop target = FindNearestEnemy(troop, scenario);
            if (target == null) return;

            troop.SetMission(MissionType.TroopDestroyTroop, target.Id);
        }

        /// <summary>扫描全场，取距离最近的敌方部队（无则 null）</summary>
        static Troop FindNearestEnemy(Troop self, Scenario scenario)
        {
            if (scenario == null || scenario.troopsSet == null || self.cell == null)
                return null;

            Troop best = null;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop other = scenario.troopsSet[i];
                if (other == null || other == self || !other.IsAlive || other.cell == null)
                    continue;
                if (!self.IsEnemy(other))
                    continue;

                int distance = scenario.Map.Distance(self.cell, other.cell);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = other;
                }
            }
            return best;
        }

        public override void Action(BuffInstance skillInstance, Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
        }

        public override void Clear()
        {
            base.Clear();
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
        }
    }
}
