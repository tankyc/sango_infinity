using Sango.Game.Battle.Core;
using Sango.Game.Battle.Skill;
namespace Sango.Game.Battle.Buff
{
    public class BattleBuff : BattleObject
    {
        public BattleBuffData data { get; set; }
        public int life { get; set; }
        public int layer { get; set; }
        public byte buffType { get { return data.type; } }
        public int id { get { return data.id; } }
        public bool IsAlive { get { return life > 0; } }

        public BattleBuffEntityList enity;
        public BattleBuff(BattleObject owner, BattleBuffData data, BattlePerson target, int life, int layer) : base(owner.battle, owner)
        {
            type = BattleDefine.ObjectType.Buff;
            this.life = life;
            this.layer = layer;
            this.target = target;
            this.data = data;
        }

        public BattleBuff(BattleInstance battle, BattleBuffData data, BattlePerson target, int life, int layer) : base(battle, null)
        {
            type = BattleDefine.ObjectType.Buff;
            root = this;
            this.life = life;
            this.layer = layer;
            this.target = target;
            this.data = data;
        }

        public void Active()
        {
            if (enity == null)
                enity = data.entity.Clone();
            enity.SetOwner(this);
            enity.Active();
        }

        public void Clear()
        {
            if (enity == null) return;
            enity.Clear();
        }
    }
}