using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 不可行动(扰乱)
    /// </summary>
    public class Stun : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
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
            base.Clear();
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;

        }
    }
}
