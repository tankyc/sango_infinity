using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Core
{
    public abstract class BattleObject
    {
        public BattleObject root { internal set; get; }
        public BattleObject owner { internal set; get; }
        public BattlePerson target { internal set; get; }
        public BattlePerson master { internal set; get; }
        public BattleInstance battle { internal set; get; }
        public virtual string name { internal set; get; }
        public BattleDefine.ObjectType type { internal set; get; }

        public BattleObject(BattleInstance battle, BattleObject owner)
        {
            this.owner = owner;
            this.battle = battle;
            if (owner != null && owner.root != null)
                root = owner.root;
        }
    }
}