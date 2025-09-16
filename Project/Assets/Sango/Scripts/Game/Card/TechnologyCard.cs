using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    public class TechnologyCard : CardBase
    {
        public int cardType { private set; get; }
        public virtual void OnGet() {; }
        public virtual void OnLost() {; }
    
        
    }
}
