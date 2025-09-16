using Sango.Game.Battle.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Battle.Formula
{
    public class BattleFormula<T> where T : struct
    {
        public virtual T Calculate(BattlePerson person)
        {
            return default(T);
        }
    }
}
