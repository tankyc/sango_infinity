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
            //---@±»¶¯
            Passive = 0,
            //---@±øÖÖ
            Arm,
            //---@Õó·¨
            Formation,
            //---@Ö¸»Ó
            Command,
            //---@Ö÷¶¯
            Active,
            //---@Í»»÷
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
        public static string[] skill_type_name = { "±»¶¯", "±øÖÖ", "Õó·¨", "Ö¸»Ó", "Ö÷¶¯", "Í»»÷" };
        public static string[] skill_command_name = { "±øÈÐ", "Ä±ÂÔ", "¸¨Öú", "ÖÎÁÆ", "·ÀÓù", "ÎÄÎä" };
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
            //---@ÕðÉå
            Stun = 1,
            //---@¼¼Çî
            Slice = 2,
            // ---@½ÉÐµ
            Disarm = 3,
            //---@»ìÂÒ
            Chaos = 4,
            //---@³°·í
            Taunt = 5,
            // ---@½ûÁÆ
            NoHeal = 6,
            //---@ÓöÏ®
            OrderBack = 7,
            // ---@ÐéÈõ
            Weak = 8,
        }

        public enum BuffType : ushort
        {
            None = 0,
            //---@ÕðÉå
            Stun = 1,
            //---@¼¼Çî
            Slice = 2,
            // ---@½ÉÐµ
            Disarm = 3,
            //---@»ìÂÒ
            Chaos = 4,
            //---@³°·í
            Taunt = 5,
            // ---@½ûÁÆ
            NoHeal = 6,
            //---@ÓöÏ®
            OrderBack = 7,
            // ---@ÐéÈõ
            Weak = 8,
            //---@µÖÓù
            Resist = 20,
            // ---@ÏÈ¹¥
            OrderFront = 21,
            // ---@¶´²ì
            Insight = 22,
        }

    }
}