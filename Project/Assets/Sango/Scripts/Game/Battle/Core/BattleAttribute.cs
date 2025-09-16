using Unity.VisualScripting;
using UnityEngine.SocialPlatforms;

namespace Sango.Game.Battle.Core
{
    public class BattleAttribute
    {
        /// <summary>
        /// 兵力
        /// </summary>
        public float troops;
        /// <summary>
        /// 伤兵
        /// </summary>
        public float wounded_troops;
        /// <summary>
        /// 最大兵力
        /// </summary>
        public float max_troops;  // number 最大兵力
        /// <summary>
        /// 兵种类型
        /// </summary>
        public int troops_type;  // number 兵种类型
        /// <summary>
        /// 兵种适应力
        /// </summary>
        public int troops_type_level;  // number 兵种适应力
        /// <summary>
        /// 补给
        /// </summary>
        public float food;  // number 补给
        /// <summary>
        /// 基础武力
        /// </summary>
        public float base_force;  // number 基础武力
        /// <summary>
        /// 基础统率
        /// </summary>
        public float base_rule;  // number 基础统率
        /// <summary>
        /// 基础智力
        /// </summary>
        public float base_int;  // number 基础智力
        /// <summary>
        /// 基础政治
        /// </summary>
        public float base_politics;  // number 基础政治
        /// <summary>
        /// 基础魅力
        /// </summary>
        public float base_charm;  // number 基础魅力
        /// <summary>
        /// 基础速度
        /// </summary>
        public float base_speed;  // number 基础速度
        /// <summary>
        /// 规避
        /// </summary>
        public float dodge;  // number 规避\
        /// <summary>
        /// 反击概率
        /// </summary>
        public float atk_back_prob;  // number 反击概率
        /// <summary>
        /// 反击伤害
        /// </summary>
        public float atk_back;  // number 反击伤害
        /// <summary>
        /// 会心
        /// </summary>
        public float force_critical;  // number 会心
        /// <summary>
        /// 奇谋
        /// </summary>
        public float int_critical;  // number 奇谋
        /// <summary>
        /// 造成会心伤害增加
        /// </summary>
        public float critical_damage_increase;  // number 造成会心伤害增加
        /// <summary>
        /// 受到会心伤害减少
        /// </summary>
        public float critical_damage_decrease;  // number 受到会心伤害减少
        /// <summary>
        /// 造成兵刃伤害增加
        /// </summary>
        public float force_damage_increase;  // number 造成兵刃伤害增加
        /// <summary>
        /// 受到兵刃伤害减少
        /// </summary>
        public float force_damage_decrease;  // number 受到兵刃伤害减少
        /// <summary>
        /// 造成谋略伤害增加
        /// </summary>
        public float int_damage_increase;  // number 造成谋略伤害增加
        /// <summary>
        /// 受到谋略伤害减少
        /// </summary>
        public float int_damage_decrease;  // number 受到谋略伤害减少
        /// <summary>
        /// 造成伤害增加
        /// </summary>
        public float damage_increase;  // number 造成伤害增加
        /// <summary>
        /// 受到伤害减少
        /// </summary>
        public float damage_decrease;  // number 受到伤害减少
        /// <summary>
        /// 倒戈
        /// </summary>
        public float force_damage_defection;  // number 倒戈
        /// <summary>
        /// 攻心
        /// </summary>
        public float int_damage_defection;
        /// <summary>
        /// 受到伤害增加
        /// </summary>
        public float be_hurt_increase;  // number 受到伤害增加
        /// <summary>
        /// 受到治疗增加
        /// </summary>
        public float be_heal_increase;  // number 受到治疗增加
        /// <summary>
        /// 自带主动技能发动率增加
        /// </summary>
        public float self_skill_prob_increase;  // number 自带主动技能发动率增加
        /// <summary>
        /// 所有主动技能发动率增加
        /// </summary>
        public float all_skill_prob_increase;  // number 所有主动技能发动率增加
        /// <summary>
        /// 自带突击技能发动率增加
        /// </summary>
        public float self_assault_skill_prob_increase;  // number 自带突击技能发动率增加
        /// <summary>
        /// 所有突击技能发动率增加
        /// </summary>
        public float all_assault_skill_prob_increase;  // number 所有突击技能发动率增加
        /// <summary>
        /// 武力
        /// </summary>
        public float st_force;  // number 武力
        /// <summary>
        /// 统率
        /// </summary>
        public float st_rule;  // number 统率
        /// <summary>
        /// 智力
        /// </summary>
        public float st_int;  // number 智力
        /// <summary>
        /// 政治
        /// </summary>
        public float st_politics;  // number 政治
        /// <summary>
        /// 魅力
        /// </summary>
        public float st_charm;  // number 魅力
        /// <summary>
        /// 速度
        /// </summary>
        public float st_speed;  // number 速度

        public BattleInstance battle { get { return person?.battle; } }
        public BattlePerson person { internal set; get; }
        public BattleTroops battleTroops { get { return person?.battleTroops; } }
        public BattleAttribute(BattlePerson person)
        {
            this.person = person;
        }

        public void Prepare()
        {
            battle.log.Add($"---{person.name} 属性计算---");
            float type_addon = 1 + BattleDefine.TROOPS_TYPE_LEVEL_INCREASE_ATTRIBUTE[this.troops_type_level];
            battle.log.Add($"{person.name} 兵种适应为{troops_type_level}, 属性提升{type_addon * 100}%");
            this.st_force = this.base_force * type_addon;
            battle.log.Add($"{person.name} 武力{base_force} -> {st_force}");
            this.st_rule = this.base_rule * type_addon;
            battle.log.Add($"{person.name} 统率{base_rule} -> {st_rule}");
            this.st_int = this.base_int * type_addon;
            battle.log.Add($"{person.name} 智力{base_int} -> {st_int}");
            this.st_politics = this.base_politics * type_addon;
            battle.log.Add($"{person.name} 政治{base_politics} -> {st_politics}");
            this.st_charm = this.base_charm * type_addon;
            battle.log.Add($"{person.name} 魅力{base_charm} -> {st_charm}");
            this.st_speed = this.base_speed * type_addon;
            battle.log.Add($"{person.name} 速度{base_speed} -> {st_speed}");
        }
    }
}
