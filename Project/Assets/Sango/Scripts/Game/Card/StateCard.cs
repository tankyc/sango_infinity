using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    public class StateCard : CardBase
    {

        public int addPeople;
        public int addTroops;

        public byte state;
        public int population;
        public ushort food;
        public ushort gold;
        public byte max_level;
        public byte specialty;

        public CityCard[] neighbor = new CityCard[8];

        public override void OnGet(Scenario scenario)
        {
            base.OnGet(scenario);
            //scenario.Player.add_food += food * cardLevel;
            //scenario.Player.add_gold += gold * cardLevel;
            //scenario.Player.population += population * cardLevel;
        }
        public override void OnLost(Scenario scenario)
        {
            base.OnLost(scenario);
            //scenario.Player.add_food -= food * cardLevel;
            //scenario.Player.add_gold -= gold * cardLevel;
            //scenario.Player.population -= population * cardLevel;
        }

        public override void OnLevelUP(Scenario scenario, byte destLevel)
        {
            OnLost(scenario);
            base.OnLevelUP(scenario, destLevel);
            OnGet(scenario);

        }


    }
}
