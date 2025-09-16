using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Card
{ 
    public class Card
    {
        public byte type;
        public int nameId;
        public int descId;
        public int id;


        public virtual void OnEnable()
        {

        }

        public virtual void OnDisable()
        {

        }

    }
}
