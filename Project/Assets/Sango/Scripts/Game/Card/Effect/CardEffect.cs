using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    public abstract class CardEffect
    {
        public virtual void OnGet(Scenario scenario) {; }
        public virtual void OnLost(Scenario scenario) {; }
    }
}
