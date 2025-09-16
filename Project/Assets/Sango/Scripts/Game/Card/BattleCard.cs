using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    public struct PersonData
    {
        public ushort personId;
        public ushort troops;
        public int skill1;
        public int skill2;
        public int troopsType;
    }

    public struct TroopsData
    {
        public byte formationId;
        public PersonData[] personIds;
    }

    public class BattleCard : CardBase
    {
        TroopsData[] troopsDatas;

        public override void OnSelect(Scenario scenario)
        {
            base.OnSelect(scenario);
            Event.OnSelectBattle?.Invoke(scenario, troopsDatas);
        }
    }
}
