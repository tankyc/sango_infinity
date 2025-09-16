using Sango.Game.Battle.Condition;
using Sango.Game.Battle.Core;
using Sango.Game.Battle.Trigger;

namespace Sango.Game.Battle.Buff
{
    public class BattleBuffData
    {
        public int id;
        public byte type;
        public int nameId;
        public int descId;
        public byte maxLayer;
        public byte state;
        public byte replaceType;
        public BattleBuffEntityList entity;

        public BattleBuffEntityList InstanceEntity
        {
            get
            {
                return entity.Clone();
            }
        }
    }
}
