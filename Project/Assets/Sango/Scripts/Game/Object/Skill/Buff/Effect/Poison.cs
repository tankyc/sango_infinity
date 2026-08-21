using TKNewtonsoft.Json; using TKNewtonsoft.Json.Linq;

namespace Sango.Core
{
    public class Poison : BuffEffect
    {
        private int damagePerTurn;

        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            damagePerTurn = p.Value<int>("damagePerTurn");
        }

        public override void Action(BuffInstance buffInstance, Troop troop, Cell spellCell, System.Collections.Generic.List<Cell> atkCellList)
        {
            // 中毒效果：每回合造成持续伤害
            if (troop.IsAlive)
            {
                troop.ChangeTroops(-damagePerTurn, null, 0);
            }
        }
    }
}
