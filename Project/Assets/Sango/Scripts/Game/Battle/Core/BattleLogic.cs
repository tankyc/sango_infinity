using Sango.Game.Battle.Buff;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Sango.Game.Battle.Core
{
    public class BattleLogic
    {
        public static void PersonAttack(BattlePerson attacker, BattlePerson target, byte damageType, int percent, BattleObject srcObject)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive) return;
            BattleInstance battle = attacker.battle;

            // �������
            BattleBuff buff = target.FindBuff(x =>
            {
                return x.buffType == (byte)BattleDefine.BuffType.Resist;
            });

            if (buff != null)
            {
                battle.log.Add($"{target.name}����һ�ε���!!�˺���Ч!");
                target.RemoveBuff(buff);
                return;
            }

            BattleAttackResult attackResult = attacker.CreateAttackResult(target);
            attackResult.CalculateDamage(damageType, percent);
            battle.@event.OnWillBeDamage?.Invoke(attacker, target, attackResult, damageType, percent);

            if (attackResult.isDoge)
            {
                battle.log.Add($"{target.name}����, �˺���Ч!!");
                battle.@event.OnDodge(attacker, target, attackResult, damageType, percent);
                return;
            }

            if (attackResult.isCritical)
            {
                battle.log.Add($"{attacker.name}��������!!");
                battle.@event.OnCritical(attacker, target, attackResult, damageType, percent);
                return;
            }

            battle.@event.OnBeforDamage(attacker, target, attackResult, damageType, percent);
            if (target.OnDamage(attackResult.damage, attackResult, srcObject))
            {
                int heal = attackResult.heal;
                if (heal > 0)
                {
                    battle.@event.OnBeforHeal(attacker, target, attackResult, percent);
                    if (attacker.OnHeal(heal, attackResult, srcObject))
                    {
                        battle.@event.OnHeal(attacker, target, attackResult, percent);
                        battle.@event.OnAfterHeal(attacker, target, attackResult, percent);
                    }
                }

                battle.@event.OnDamage(attacker, target, attackResult, damageType, percent);
                battle.@event.OnAfterDamage(attacker, target, attackResult, damageType, percent);
            }
        }

        public static void PersonHeal(BattlePerson attacker, BattlePerson target, int percent, BattleObject srcObject)
        {
            if (attacker == null || target == null || !attacker.IsAlive || !target.IsAlive) return;
            BattleInstance battle = attacker.battle;

            // ���Ƽ��
            BattleBuff buff = target.FindBuff(x =>
            {
                return x.buffType == (byte)BattleDefine.BuffType.NoHeal;
            });

            if (buff != null)
            {
                battle.log.Add($"{target.name}����, ������Ч!");
                return;
            }

            BattleAttackResult attackResult = attacker.CreateAttackResult(target);
            attackResult.CalculateHeal(percent);
            battle.@event.OnWillBeHeal?.Invoke(attacker, target, attackResult, percent);

            if (attackResult.isCritical)
            {
                battle.log.Add($"{attacker.name}��������!!");
                battle.@event.OnHealCritical(attacker, target, attackResult, percent);
                return;
            }

            battle.@event.OnBeforHeal(attacker, target, attackResult, percent);
            if (target.OnHeal(attackResult.heal, attackResult, srcObject))
            {
                battle.@event.OnHeal(attacker, target, attackResult, percent);
                battle.@event.OnAfterHeal(attacker, target, attackResult, percent);
            }
        }
    }
}