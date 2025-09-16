using System.Collections.Generic;

namespace Sango.Game.Battle.Core
{
    public class BattleFormation : BattleObject
    {
        public struct FormationData
        {
            public byte id;
            public byte maxSeat;
            public FormationSeat[] seatSet;
        }

        public struct FormationSeat
        {
            public byte index;
            public ushort weight;
            public byte type;
        }
        public FormationData formationData { internal set; get; }
        public BattleTroops troops { internal set; get; }
        public BattlePerson[] persons { internal set; get; } 
        public BattleFormation(BattleInstance battle, BattleTroops troops) : base(battle, null)
        {
            type = BattleDefine.ObjectType.Formation;
            formationData = Battle.Instance.GetFormationData(troops.formationId);
            persons = new BattlePerson[formationData.maxSeat];
            this.troops = troops;
            Person[] gamePersons = troops.GetPersons();
            for (int i = 0; i < formationData.maxSeat; i++)
            {
                Person person = gamePersons[i];
                if(person == null)
                    continue;
                persons[i] = new BattlePerson(this, person, troops, i);
            }
        }

        public FormationSeat GetSeat(int index)
        {
            return formationData.seatSet[index];
        }

        public BattlePerson[] GetPersons()
        {
            return persons;
        }

        public void GetPersons(List<BattlePerson> list)
        {
            for(int i = 0; i < persons.Length; ++i)
            {
                BattlePerson p = persons[i];
                if(p != null)
                    list.Add(p);
            }
        }

    }
}
