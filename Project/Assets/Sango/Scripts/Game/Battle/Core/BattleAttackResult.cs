using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
namespace Sango.Game.Battle.Core
{
    public class BattleAttackResult
    {
        public BattlePerson master { get; private set; }
        public BattlePerson target { get; private set; }
        public int damage { get; private set; }
        public int heal { get; private set; }
        public bool isDoge { get; private set; }
        public bool isCritical { get; private set; }
        public byte damageType { get; private set; }

        public BattleAttackResult(BattlePerson master, BattlePerson target)
        {
            this.master = master;
            this.target = target;
        }

        public void CalculateDamage(byte damageType, int percent_int = 10000)
        {
            this.damageType = damageType;
            BattleInstance battle = master.battle;

            var attack_attr = master.attribute;
            var target_attr = target.attribute;

            //---规避
            if (target_attr.dodge > 0)
            {
                isDoge = battle.Random(1, 10000) < target_attr.dodge;
            }

            if (isDoge)
            {
                damage = 0;
                return;
            }

            //---正负5 % 的浮动数值
            var float_value = 0.95f + (battle.Random(1, 10000)) / 100000;

            var troops_per = Mathf.Max(1, attack_attr.troops / 3000);

            var critical_value = damageType == 1 ? attack_attr.force_critical : attack_attr.int_critical;
            //---暴击
            if (critical_value > 0)
            {
                isCritical = battle.Random(1, 10000) < critical_value;
            }
            var critical_damage = 1 + (isCritical ? (1 + target_attr.critical_damage_increase / 10000) : 0);
            float percent = percent_int / 10000;
            float f_damage = 0;
            if (damageType == 1)
            {
                f_damage = float_value * 5f * (100f + attack_attr.st_force - target_attr.st_rule) * troops_per * percent * (10000 + attack_attr.force_damage_increase - target_attr.force_damage_defection) / 10000 * (10000 + attack_attr.damage_increase + target_attr.be_hurt_increase - target_attr.damage_decrease) / 10000 * critical_damage;
            }
            else if (damageType == 2)
            {
                f_damage = float_value * 5f * (100f + attack_attr.st_int - target_attr.st_int) * troops_per * percent * (10000 + attack_attr.int_damage_increase - target_attr.int_damage_defection) / 10000 * (10000 + attack_attr.damage_increase + target_attr.be_hurt_increase - target_attr.damage_decrease) / 10000 * critical_damage;

            }
            else if (damageType == 3)
            {
                f_damage = float_value * 5f * (100f + attack_attr.st_force) * troops_per * percent * (10000 + attack_attr.damage_increase + target_attr.be_hurt_increase - target_attr.damage_decrease) / 10000 * critical_damage;

            }
            else if (damageType == 4)
            {
                f_damage = float_value * 5f * (100f + attack_attr.st_int) * troops_per * percent * (10000 + attack_attr.damage_increase + target_attr.be_hurt_increase - target_attr.damage_decrease) / 10000 * critical_damage;
            }

            var defection = damageType == 1 ? attack_attr.force_damage_defection : attack_attr.int_damage_defection;
            if (defection > 0)
            {
                heal = (int)Mathf.Floor(f_damage * (defection / 10000));
            }

            damage = (int)Mathf.Floor(f_damage);
        }

        public void CalculateHeal(int percent_int = 10000)
        {
            var battle = master.battle;
            var attack_attr = master.attribute;
            var target_attr = target.attribute;

            var troops_per = Mathf.Max(1, attack_attr.troops / 3000);
            var critical_value = attack_attr.int_critical;
            //---暴击
            if (critical_value > 0)
            {
                isCritical = battle.Random(1, 10000) < critical_value;
            }
            var critical_damage = 1f + (isCritical ? 1f : 0f);
            float percent = percent_int / 10000;
            heal = (int)Mathf.Floor((50 + attack_attr.st_int) * troops_per * percent * (10000 + target_attr.be_heal_increase) / 10000 * critical_damage);
        }
    }
}
