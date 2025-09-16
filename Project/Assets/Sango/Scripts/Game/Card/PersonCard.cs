using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    public class PersonCard : CardBase
    {
        public ushort heroId;
        public virtual void OnGet() {; }
        public virtual void OnLost() {; }
    
        
    }
}
