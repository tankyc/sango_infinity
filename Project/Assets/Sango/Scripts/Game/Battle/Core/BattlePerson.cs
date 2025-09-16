using Sango.Game.Battle.Buff;
using System;
using System.Collections;
using System.Collections.Generic;
using static Sango.Game.Battle.Core.BattleFormation;

namespace Sango.Game.Battle.Core
{
    public class BattlePerson : BattleObject
    {
        public FormationSeat seat { internal set; get; }
        public Person person { internal set; get; }
        public BattleTroops battleTroops { internal set; get; }
        public BattleFormation formation { internal set; get; }
        public BattleAttribute attribute { internal set; get; }
        public BattlePerson(BattleFormation owner, Person person, BattleTroops troops, int index) : base(owner.battle, null)
        {
            type = BattleDefine.ObjectType.Person;
            this.seat = owner.GetSeat(index);
            this.person = person;
            this.battleTroops = troops;
            this.formation = owner;
            this.attribute = new BattleAttribute(this);
        }

        public byte Order { internal set; get; }
        public byte Speed { internal set; get; }
        public bool IsAlive { get { return troops > 0; } }
        public int troops { internal set; get; }

        private List<BattleBuff> buffList = new List<BattleBuff>();
        public IEnumerator PrepareAttribute()
        {
            yield return null;
        }

        public IEnumerator PrepareSkill(BattleDefine.SkillType skillType)
        {
            yield return null;
        }

        public IEnumerator Run(BattleInstance battle, int count)
        {
            yield return null;
        }

        public bool HasState(BattleDefine.PersonState state)
        {
            return HasState((ushort)state);
        }
        public bool HasState(ushort state)
        {
            return false;
        }

        public bool IsEnemy(BattlePerson other)
        {
            return false;
        }

        public BattlePerson GetTauntTarget()
        {
            return null;
        }
        public void AddBuff(BattleBuff buff)
        {
            buffList.Add(buff);
        }

        public void RemoveBuff(BattleBuff buff)
        {
            buffList.Remove(buff);
        }

        public BattleBuff FindBuff(Predicate<BattleBuff> comparer)
        {
            return buffList.Find(comparer);
        }

        public void UpdateBuff()
        {
        }

        public BattleAttackResult CreateAttackResult(BattlePerson target)
        {
            return new BattleAttackResult(this, target);
        }

        public bool OnDamage(int value, BattleAttackResult attackResult, BattleObject srcObject)
        {
            return false;
        }

        public bool OnHeal(int value, BattleAttackResult attackResult, BattleObject srcObject)
        {
            return false;
        }
    }
}
