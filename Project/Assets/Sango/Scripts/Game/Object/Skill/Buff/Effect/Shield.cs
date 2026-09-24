using Newtonsoft.Json; using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    public class Shield : BuffEffect
    {
        private int shieldValue;

        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            shieldValue = p.Value<int>("shieldValue");
        }

        public override void Action(BuffInstance buffInstance, Troop troop, Cell spellCell, System.Collections.Generic.List<Cell> atkCellList)
        {
            // 护盾效果：吸收伤害
        }
    }
}
