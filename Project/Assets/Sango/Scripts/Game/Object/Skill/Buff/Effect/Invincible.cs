using Newtonsoft.Json; using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    public class Invincible : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
        }

        public override void Action(BuffInstance buffInstance, Troop troop, Cell spellCell, System.Collections.Generic.List<Cell> atkCellList)
        {
            // 无敌效果：免疫所有伤害
        }
    }
}
