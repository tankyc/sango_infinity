using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Battle.Core
{
    public class BattleEffect
    {
        public byte type;
        public virtual IEnumerator Run(BattleObject owner, BattlePerson[] targets)
        {
            yield return null;
        }
    }
}
