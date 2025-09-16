using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Core
{
    public class BattleEvent : EventBase
    {
        public CoEventDelegate<BattleInstance, BattlePerson> OnPrepareStart;
        public CoEventDelegate<BattleInstance, BattlePerson> OnPrepareEnd;
        public CoEventDelegate<BattleInstance, BattlePerson, int> OnRoundStart;
        public CoEventDelegate<BattleInstance, BattlePerson, int> OnRoundEnd;


        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, byte, int> OnWillBeDamage;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, byte, int> OnDodge;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, byte, int> OnCritical;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, byte, int> OnBeforDamage;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, byte, int> OnDamage;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, byte, int> OnAfterDamage;


        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnWillBeHeal;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnHealCritical;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnBeforHeal;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnHeal;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnAfterHeal;


        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnNormalAttackBefore;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnNormalAttack;
        public CoEventDelegate<BattlePerson, BattlePerson, BattleAttackResult, int> OnNormalAttackAfter;


    }
}

