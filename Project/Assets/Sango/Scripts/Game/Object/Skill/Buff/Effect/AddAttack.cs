using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 不可行动(扰乱)
    /// </summary>
    public class AddAttack : BuffEffect
    {
        int value;

        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            value = p.Value<int>("value");
            master.Target.extraAttack += value;
        }

        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if(troop == master.Target)
            {
                troop.ActionOver = true;
            }
        }

        public override void Action(BuffInstance skillInstance, Troop troop, Cell spellCell, List<Cell> atkCellList)
        {
        }

        public override void Clear()
        {
            master.Target.extraAttack -= value;
            base.Clear();
        }
    }
}
