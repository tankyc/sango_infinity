using Unity.VisualScripting;
using UnityEngine.SocialPlatforms;

namespace Sango.Game.Battle.Core
{
    public class BattleAttribute
    {
        /// <summary>
        /// ����
        /// </summary>
        public float troops;
        /// <summary>
        /// �˱�
        /// </summary>
        public float wounded_troops;
        /// <summary>
        /// ������
        /// </summary>
        public float max_troops;  // number ������
        /// <summary>
        /// ��������
        /// </summary>
        public int troops_type;  // number ��������
        /// <summary>
        /// ������Ӧ��
        /// </summary>
        public int troops_type_level;  // number ������Ӧ��
        /// <summary>
        /// ����
        /// </summary>
        public float food;  // number ����
        /// <summary>
        /// ��������
        /// </summary>
        public float base_force;  // number ��������
        /// <summary>
        /// ����ͳ��
        /// </summary>
        public float base_rule;  // number ����ͳ��
        /// <summary>
        /// ��������
        /// </summary>
        public float base_int;  // number ��������
        /// <summary>
        /// ��������
        /// </summary>
        public float base_politics;  // number ��������
        /// <summary>
        /// ��������
        /// </summary>
        public float base_charm;  // number ��������
        /// <summary>
        /// �����ٶ�
        /// </summary>
        public float base_speed;  // number �����ٶ�
        /// <summary>
        /// ���
        /// </summary>
        public float dodge;  // number ���\
        /// <summary>
        /// ��������
        /// </summary>
        public float atk_back_prob;  // number ��������
        /// <summary>
        /// �����˺�
        /// </summary>
        public float atk_back;  // number �����˺�
        /// <summary>
        /// ����
        /// </summary>
        public float force_critical;  // number ����
        /// <summary>
        /// ��ı
        /// </summary>
        public float int_critical;  // number ��ı
        /// <summary>
        /// ��ɻ����˺�����
        /// </summary>
        public float critical_damage_increase;  // number ��ɻ����˺�����
        /// <summary>
        /// �ܵ������˺�����
        /// </summary>
        public float critical_damage_decrease;  // number �ܵ������˺�����
        /// <summary>
        /// ��ɱ����˺�����
        /// </summary>
        public float force_damage_increase;  // number ��ɱ����˺�����
        /// <summary>
        /// �ܵ������˺�����
        /// </summary>
        public float force_damage_decrease;  // number �ܵ������˺�����
        /// <summary>
        /// ���ı���˺�����
        /// </summary>
        public float int_damage_increase;  // number ���ı���˺�����
        /// <summary>
        /// �ܵ�ı���˺�����
        /// </summary>
        public float int_damage_decrease;  // number �ܵ�ı���˺�����
        /// <summary>
        /// ����˺�����
        /// </summary>
        public float damage_increase;  // number ����˺�����
        /// <summary>
        /// �ܵ��˺�����
        /// </summary>
        public float damage_decrease;  // number �ܵ��˺�����
        /// <summary>
        /// ����
        /// </summary>
        public float force_damage_defection;  // number ����
        /// <summary>
        /// ����
        /// </summary>
        public float int_damage_defection;
        /// <summary>
        /// �ܵ��˺�����
        /// </summary>
        public float be_hurt_increase;  // number �ܵ��˺�����
        /// <summary>
        /// �ܵ���������
        /// </summary>
        public float be_heal_increase;  // number �ܵ���������
        /// <summary>
        /// �Դ��������ܷ���������
        /// </summary>
        public float self_skill_prob_increase;  // number �Դ��������ܷ���������
        /// <summary>
        /// �����������ܷ���������
        /// </summary>
        public float all_skill_prob_increase;  // number �����������ܷ���������
        /// <summary>
        /// �Դ�ͻ�����ܷ���������
        /// </summary>
        public float self_assault_skill_prob_increase;  // number �Դ�ͻ�����ܷ���������
        /// <summary>
        /// ����ͻ�����ܷ���������
        /// </summary>
        public float all_assault_skill_prob_increase;  // number ����ͻ�����ܷ���������
        /// <summary>
        /// ����
        /// </summary>
        public float st_force;  // number ����
        /// <summary>
        /// ͳ��
        /// </summary>
        public float st_rule;  // number ͳ��
        /// <summary>
        /// ����
        /// </summary>
        public float st_int;  // number ����
        /// <summary>
        /// ����
        /// </summary>
        public float st_politics;  // number ����
        /// <summary>
        /// ����
        /// </summary>
        public float st_charm;  // number ����
        /// <summary>
        /// �ٶ�
        /// </summary>
        public float st_speed;  // number �ٶ�

        public BattleInstance battle { get { return person?.battle; } }
        public BattlePerson person { internal set; get; }
        public BattleTroops battleTroops { get { return person?.battleTroops; } }
        public BattleAttribute(BattlePerson person)
        {
            this.person = person;
        }

        public void Prepare()
        {
            battle.log.Add($"---{person.name} ���Լ���---");
            float type_addon = 1 + BattleDefine.TROOPS_TYPE_LEVEL_INCREASE_ATTRIBUTE[this.troops_type_level];
            battle.log.Add($"{person.name} ������ӦΪ{troops_type_level}, ��������{type_addon * 100}%");
            this.st_force = this.base_force * type_addon;
            battle.log.Add($"{person.name} ����{base_force} -> {st_force}");
            this.st_rule = this.base_rule * type_addon;
            battle.log.Add($"{person.name} ͳ��{base_rule} -> {st_rule}");
            this.st_int = this.base_int * type_addon;
            battle.log.Add($"{person.name} ����{base_int} -> {st_int}");
            this.st_politics = this.base_politics * type_addon;
            battle.log.Add($"{person.name} ����{base_politics} -> {st_politics}");
            this.st_charm = this.base_charm * type_addon;
            battle.log.Add($"{person.name} ����{base_charm} -> {st_charm}");
            this.st_speed = this.base_speed * type_addon;
            battle.log.Add($"{person.name} �ٶ�{base_speed} -> {st_speed}");
        }
    }
}
