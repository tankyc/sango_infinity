using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    /// <summary>
    /// 日期范围
    /// </summary>
    public class NotHasCard : Condition
    {
        public int cardId;

        public override bool Check(Scenario scenario, Player player, params object[] data)
        {
            return player.cards.Find(x => x.id == cardId) == null;
        }
    }
}
