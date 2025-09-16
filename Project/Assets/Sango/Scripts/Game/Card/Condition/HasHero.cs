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
    public class HasHero : Condition
    {
        public ushort heroId;

        public override bool Check(Scenario scenario, Player player, params object[] data)
        {
            return player.heroes.Find(x => x.heroId == heroId) != null;
        }
    }
}
