using Sango.Game.Battle.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Trigger
{
    public class TriggerOnNormalAttack : Trigger
    {
        public bool isMaster;
        public bool isAttacker;

        internal BattlePerson attacker;
        internal BattlePerson target;
        internal BattleAttackResult battleAttackResult;
        internal byte damageType;
        internal int percent;

        public override Trigger Clone()
        {
            return new TriggerOnDamage()
            {
                isAttacker = isAttacker,
                isMaster = isAttacker,
            };
        }

        public override void Active(TriggerCall call)
        {
            base.Active(call);
            owner.battle.@event.OnDodge += OnDodge;
        }

        IEnumerator OnDodge(BattlePerson attacker, BattlePerson target, BattleAttackResult battleAttackResult, byte damageType, int percent)
        {
            if (!CheckRound()) yield break;

            this.attacker = attacker;
            this.target = target;
            this.battleAttackResult = battleAttackResult;
            this.damageType = damageType;
            this.percent = percent;

            BattlePerson who = isMaster ? owner.master : owner.target;
            BattlePerson whoIs = isAttacker ? attacker : target;

            if (who == whoIs)
            {
                triggerCall?.Invoke(this);
            }
            yield break;
        }

        public override void Clear()
        {
            base.Clear();
            owner.battle.@event.OnDodge -= OnDodge;
        }
    }
}
