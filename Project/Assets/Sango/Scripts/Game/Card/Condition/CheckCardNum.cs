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
    public class CheckCardNum : Condition
    {
        public byte cardType;
        public ushort cardNum;
        public sbyte result;
        public override bool Check(Scenario scenario, Player player, params object[] data)
        {
            List<CardBase> cards = player.cards.FindAll(x => x.cardType == cardType);
            if(result == 1)
                return cards != null && cards.Count > cardNum;
            else if (result == 2)
                return cards != null && cards.Count >= cardNum;
            else if (result == -1)
                return cards != null && cards.Count < cardNum;
            else if (result == -2)
                return cards != null && cards.Count <= cardNum;
            else
                return cards != null && cards.Count == cardNum;
        }
    }
}
