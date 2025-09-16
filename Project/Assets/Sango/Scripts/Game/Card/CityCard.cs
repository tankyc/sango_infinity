using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    public class CityCard : ValueCardBase
    {
        /// <summary>
        /// 州
        /// </summary>
        public byte state;
        /// <summary>
        /// 邻居
        /// </summary>
        public int[] neighbor = new int[8];

        public ushort food;
        public ushort gold;
        public byte max_level;
        public byte specialty;

        

        public override void OnSelect(Scenario scenario)
        {
            base.OnSelect(scenario);
        }
    }
}
