using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Sango.Game.Battle.Core
{
    public class BattleDefine
    {
        public static readonly int ACTION_MAX_COUNT = 20;
        public static float[] TROOPS_TYPE_LEVEL_INCREASE_ATTRIBUTE = { -0.3f, -0.15f, 0f, 0.2f };
        public enum SkillType : byte
        {
            //---@����
            Passive = 0,
            //---@����
            Arm,
            //---@��
            Formation,
            //---@ָ��
            Command,
            //---@����
            Active,
            //---@ͻ��
            Assault,
        }

        public enum ObjectType : byte
        {
            Person = 0,

            Skill,

            Buff,

            Formation,

            Condition
        }
        public static string[] skill_type_name = { "����", "����", "��", "ָ��", "����", "ͻ��" };
        public static string[] skill_command_name = { "����", "ı��", "����", "����", "����", "����" };
        public enum TargetType : ushort
        {
            Self = 0,
            RandomEnemy,
            RandomTeammate,
            RandomAll,
            Teammate_MaxTroops,
            Teammate_MinTroops,
            Enemy_MaxTroops,
            Enemy_MinTroops,
            Teammate,
            SeatEnemy,
        }

        public enum PersonState : ushort
        {
            None = 0,
            //---@����
            Stun = 1,
            //---@����
            Slice = 2,
            // ---@��е
            Disarm = 3,
            //---@����
            Chaos = 4,
            //---@����
            Taunt = 5,
            // ---@����
            NoHeal = 6,
            //---@��Ϯ
            OrderBack = 7,
            // ---@����
            Weak = 8,
        }

        public enum BuffType : ushort
        {
            None = 0,
            //---@����
            Stun = 1,
            //---@����
            Slice = 2,
            // ---@��е
            Disarm = 3,
            //---@����
            Chaos = 4,
            //---@����
            Taunt = 5,
            // ---@����
            NoHeal = 6,
            //---@��Ϯ
            OrderBack = 7,
            // ---@����
            Weak = 8,
            //---@����
            Resist = 20,
            // ---@�ȹ�
            OrderFront = 21,
            // ---@����
            Insight = 22,
        }

    }
}