/*
 * 文件名：DuelAI.cs
 * 描述：单挑 AI 决策模块，由 s11_sys_duel_ai.cpp 翻译而来
 *
 * 说明：
 *   1. 决策表以 AI::Row 数组表示，末尾追加一个默认 Row（id = DuelAIRow_Max）作为终止标记，
 *      对应 C++ 中数组末尾的 AI::Row() 哨兵
 *   2. C++ 中通过指针递增遍历表，C# 中改为索引遍历
 */

using System;

namespace Sango.Core.Duel
{
    public partial class Duel
    {
        #region AI 决策表

        private static readonly AI.Row[] LuBuSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_Steadfast, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_FightingSpirit, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE, 50, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 25, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] TimidSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_Steadfast, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 50, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] CalmSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_SpecialTry_Steadfast, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_FightingSpirit, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_SpecialTry_WeakPoint, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 50, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] BoldSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_Steadfast, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_FightingSpirit, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_WeakPoint, 0, 0),
            new AI.Row( 35, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 50, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE, 25, 0),
            new AI.Row( 35, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] RecklessSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_SpecialTry_FightingSpirit, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_SpecialTry_WeakPoint, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 33, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE, 33, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] LuBuSpecial =
        {
            new AI.Row( 95, (int)DuelAIRow.DuelAIRow_Special_Retreat, 12, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] TimidSpecial =
        {
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_Retreat, 16, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Special_Steadfast, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Special_WeakPoint, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] CalmSpecial =
        {
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_Retreat, 14, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_FightingSpirit, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_Steadfast, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Special_WeakPoint, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] BoldSpecial =
        {
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_Retreat, 12, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Special_FightingSpirit, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Special_Steadfast, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_WeakPoint, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] RecklessSpecial =
        {
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_Retreat, 12, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 15, (int)DuelAIRow.DuelAIRow_Special_FightingSpirit, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Special_WeakPoint, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_Special_Steadfast, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_FeintRetreat, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Special_HiddenWeaponOrPeerless, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] LuBuStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 95, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 1, 0), // 1回合 95% 概率维持
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Stance_S_HP_GTE, 80, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] TimidStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Stance_D_BlowCounter_GTE, 40, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 3, 0), // 3回合 90% 概率维持
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Stance_A_OpponentHP_LTE, 25, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] CalmStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 3, 0), // 3回合 90% 概率维持
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Stance_D_BlowCounter_GTE, 40, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Stance_A_OpponentHP_LTE, 33, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] BoldStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 2, 0), // 2回合 90% 概率维持
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Stance_S_HP_GTE, 75, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Stance_A_OpponentHP_LTE, 40, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] RecklessStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 1, 0), // 1回合 90% 概率维持
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Stance_A_OpponentHP_LTE, 50, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Stance_S_HP_GTE, 80, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_Stance_F_Always, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] LuBuSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Switch_Governor, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 33, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] TimidSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Governor, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Switch_NotBestChara, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 75, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 7, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] CalmSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Switch_Governor, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Switch_NotBestChara, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 7, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 50, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] BoldSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_Governor, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 33, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 8, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] RecklessSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_Governor, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 25, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 12, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        /// <summary>AI 决策总表（8b0a40）[AI 性格][决策表类型]</summary>
        private static readonly AI.Row[][][] AiTable = new AI.Row[][][]
        {
            new AI.Row[][] { LuBuSpecialTry, LuBuSpecial, LuBuStance, LuBuSwitch },
            new AI.Row[][] { TimidSpecialTry, TimidSpecial, TimidStance, TimidSwitch },
            new AI.Row[][] { CalmSpecialTry, CalmSpecial, CalmStance, CalmSwitch },
            new AI.Row[][] { BoldSpecialTry, BoldSpecial, BoldStance, BoldSwitch },
            new AI.Row[][] { RecklessSpecialTry, RecklessSpecial, RecklessStance, RecklessSwitch },
        };

        #endregion

        #region AI 实现

        /// <summary>刷新 AI 上下文中的当前出战武将（本方与对手）</summary>
        public void AiUpdateChara(AI self)
        {
            if (self.parent == null)
                return;
            if (!Utils.InRange(self.team, 0, (int)DuelTeam.DuelTeam_Max - 1))
                return;
            if (!Utils.InRange(self.opponentTeam, 0, (int)DuelTeam.DuelTeam_Max - 1))
                return;
            self.chara = self.parent.GetCurrentChara(self.team);
            self.opponentChara = self.parent.GetCurrentChara(self.opponentTeam);
        }

        /// <summary>按类型与编号取 AI 决策表</summary>
        public AI.Row[] AiGetTable(AI self, int type, int tableId)
        {
            return AiTable[type][tableId];
        }

        /// <summary>取指定队伍、指定武将的体力</summary>
        public int AiGetHp(AI self, int team, int chara)
        {
            return self.parent.GetHp(team, chara);
        }

        /// <summary>取当前出战武将的体力（opponent = true 时取对手）</summary>
        public int AiGetHp(AI self, bool opponent = false)
        {
            if (!opponent)
                return AiGetHp(self, self.team, self.chara);
            else
                return AiGetHp(self, self.opponentTeam, self.opponentChara);
        }

        /// <summary>取当前出战武将的斗志（opponent = true 时取对手）</summary>
        public int AiGetSpirit(AI self, bool opponent = false)
        {
            if (!opponent)
                return self.parent.GetSpirit(self.team, self.chara);
            else
                return self.parent.GetSpirit(self.opponentTeam, self.opponentChara);
        }

        /// <summary>挑决策表：先用武将专属表（行为数据驱动），没有则按性格选</summary>
        public AI.Row[] AiGetTable(AI self, int tableId)
        {
            Person person = self.parent.GetPerson(self.team, self.chara);
            if (!Utils.IsActive(person))
                return null;
            // 是否使用专属 AI 表由"武将单挑行为"数据驱动（未配置则走下面的性格分支）
            int behaviourTable = DuelPersonBehaviours.Get(person).GetAITableId(person);
            if (behaviourTable >= 0)
                return AiGetTable(self, behaviourTable, tableId);
            switch (person.GetPersonality())
            {
                case DuelPersonality.Timid:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Timid, tableId);
                case DuelPersonality.Calm:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Calm, tableId);
                case DuelPersonality.Bold:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Bold, tableId);
                case DuelPersonality.Reckless:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Reckless, tableId);
            }
            return null;
        }

        /// <summary>按 AI 比较算子比较两个数值</summary>
        public bool AiComp(AI self, int op, int a, int b)
        {
            switch (op)
            {
                case (int)DuelAICompOp.DuelAICompOp_GreaterThanOrEqual:
                    return a >= b;
                case (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual:
                    return a <= b;
                case (int)DuelAICompOp.DuelAICompOp_LessThan:
                    return a < b;
                case (int)DuelAICompOp.DuelAICompOp_GreaterThan:
                    return a > b;
            }
            return false;
        }

        /// <summary>把体力与决策表里的参数做比较</summary>
        public bool AiCompHp(AI self, AI.Row row, int op, bool opponent = false)
        {
            return AiComp(self, op, AiGetHp(self, opponent), row.param1);
        }

        /// <summary>把斗志与决策表里的参数做比较</summary>
        public bool AiCompSpirit(AI self, AI.Row row, int op, bool opponent = false)
        {
            return AiComp(self, op, AiGetSpirit(self, opponent), row.param1);
        }

        /// <summary>指定必杀当前是否可用（opponent = true 时查对手）</summary>
        public bool AiIsSpecialEnabled(AI self, int special, bool opponent = false)
        {
            if (!opponent)
                return self.parent.IsSpecialEnabled(self.team, self.chara, special);
            else
                return self.parent.IsSpecialEnabled(self.opponentTeam, self.opponentChara, special);
        }

        /// <summary>本方（或对手）是否带有指定增益</summary>
        public bool AiHasBuff(AI self, int buff, bool opponent = false)
        {
            if (!opponent)
                return self.parent.HasBuff(self.team, buff);
            else
                return self.parent.HasBuff(self.opponentTeam, buff);
        }

        /// <summary>走决策表判定本回合要不要尝试发动必杀</summary>
        public bool AiCalcSpecialTry(AI self)
        {
            // 没有可用的必杀
            if (!self.parent.CanSpecial(self.team, self.chara))
                return false;
            AI.Row[] table = AiGetTable(self, (int)DuelAITable.DuelAITable_SpecialTry);
            if (table == null)
                return false;
            for (int i = 0; ; i++)
            {
                AI.Row row = table[i];
                if (!Utils.InRange(row.id, 0, (int)DuelAIRow.DuelAIRow_Max - 1))
                    break;
                if (!system.RandBool(row.chance))
                    continue;
                switch (row.id)
                {
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE:
                        if (!AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE:
                        if (!AiCompSpirit(self, row, (int)DuelAICompOp.DuelAICompOp_GreaterThanOrEqual))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE:
                        if (!AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual, true))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_HiddenWeaponOrPeerless:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_HiddenWeapon) && !AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Peerless))
                            continue;
                        for (int j = 0; j < (int)DuelBuffType.DuelBuffType_Max; j++)
                        {
                            if (AiHasBuff(self, j, true))
                                return true;
                        }
                        continue;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_WeakPoint:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_WeakPoint))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_FightingSpirit:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_FightingSpirit))
                            continue;
                        // 已处于攻击增益状态
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Steadfast:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Steadfast))
                            continue;
                        // 已处于防御增益状态
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Defense))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Always:
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Stop:
                        return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>初始化队伍 AI：绑定单挑本体、本方与对手队伍，并刷新出战武将</summary>
        public bool AiInit(AI self, int team)
        {
            self.parent = this;
            self.team = team;
            self.opponentTeam = GetOpponentTeam(team);
            AiUpdateChara(self);
            self.initialized = true;
            return true;
        }

        /// <summary>估算武将战力（体力、武力与宝物加成折算）</summary>
        public int AiGetPower(AI self, int team, int chara)
        {
            int hpCoef = (AiGetHp(self, team, chara) + 9) / 10;             // 0 .. 10
            int strengthCoef = (self.parent.GetStrength(team, chara, true) + 4) / 5; // 1 .. 22
            Person person = self.parent.GetPerson(team, chara);
            if (!Utils.IsAlive(person))
                return 0;
            int n = (hpCoef + 20) * strengthCoef * strengthCoef * 96 / 10;  // 192 .. 139392
            n += system.GetDuelItemPower(person);                           // 0 .. 30
            if (n < 1)
                return 1;
            return n;
        }

        /// <summary>选出该队战力最高的武将</summary>
        public int AiGetBestChara(AI self, int team)
        {
            int bestPower = 0;
            int best = -1;
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (!self.parent.CheckState(team, i, -1))
                    continue;
                int power = AiGetPower(self, team, i);
                if (bestPower < power)
                {
                    bestPower = power;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>按决策表权重随机挑一个必杀</summary>
        public int AiRandomSpecial(AI self)
        {
            int[] weight = new int[(int)DuelSpecial.DuelSpecial_Max];
            int weightSum = 0;
            for (int i = 0; i < (int)DuelSpecial.DuelSpecial_Max; i++)
            {
                if (!AiIsSpecialEnabled(self, i))
                    continue;
                switch (i)
                {
                    case (int)DuelSpecial.DuelSpecial_DeadlyMove:
                        weightSum += 20;
                        break;
                    case (int)DuelSpecial.DuelSpecial_FightingSpirit:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        weightSum += 3;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Steadfast:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Defense))
                            continue;
                        weightSum += 3;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Retreat:
                        continue;
                    case (int)DuelSpecial.DuelSpecial_WeakPoint:
                        weightSum += 20;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Peerless:
                        weightSum += 60;
                        break;
                    case (int)DuelSpecial.DuelSpecial_HiddenWeapon:
                        weightSum += 20;
                        break;
                    case (int)DuelSpecial.DuelSpecial_FeintRetreat:
                        weightSum += 20;
                        break;
                }
                weight[i] = weightSum;
            }
            int n = system.RandInt(weightSum);
            for (int i = 0; i < (int)DuelSpecial.DuelSpecial_Max; i++)
            {
                if (weight[i] > n)
                    return i;
            }
            return -1;
        }

        /// <summary>走决策表决定是否交替武将、换谁上场</summary>
        public int AiCalcSwitch(AI self)
        {
            bool canSwitch = false;
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (IsJoined(self.team, i) && i != self.chara)
                {
                    canSwitch = true;
                    break;
                }
            }
            if (!canSwitch)
                return -1;

            int hp = AiGetHp(self);
            int timer = GetSwitchingTimer(self.team);
            // 体力 20 以下时放宽计时器限制
            if (timer == 0 || (hp <= 20 && timer < 3))
            {
                // 允许交替
            }
            else
            {
                return -1;
            }

            int bestChara = AiGetBestChara(self, self.team);
            if (!Utils.InRange(bestChara, 0, MaxTeamCharaCount - 1))
                return -1;

            AI.Row[] table = AiGetTable(self, (int)DuelAITable.DuelAITable_Switch);
            if (table == null)
                return -1;
            for (int i = 0; ; i++)
            {
                AI.Row row = table[i];
                if (!Utils.InRange(row.id, 0, (int)DuelAIRow.DuelAIRow_Max - 1))
                    break;

                // 体力越低概率越高，最高 2 倍
                int chance = row.chance;
                if (hp <= 30)
                    chance = chance * Math.Min(45 - hp, 30) / 15;
                if (!system.RandBool(chance))
                    continue;

                // 体力在 1/3 以上且处于增益状态时不变换
                if (hp >= 33)
                {
                    for (int j = 0; j < (int)DuelBuffType.DuelBuffType_Max; j++)
                    {
                        if (HasBuff(self.team, j))
                            return -1;
                    }
                }

                switch (row.id)
                {
                    case (int)DuelAIRow.DuelAIRow_Switch_HP_LTE:
                        if (!AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual))
                            continue;
                        return bestChara;
                    case (int)DuelAIRow.DuelAIRow_Switch_Governor:
                        {
                            Person person = GetPerson(self.team, self.chara);
                            if (person == null || !person.IsGovernor)
                                continue;
                            return bestChara;
                        }
                    case (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE:
                        if (GetStrength(self.opponentTeam, self.opponentChara, true) - GetStrength(self.team, self.chara, true) <= row.param1)
                            continue;
                        return bestChara;
                    case (int)DuelAIRow.DuelAIRow_Switch_NotBestChara:
                        if (self.chara == bestChara)
                            continue;
                        return bestChara;
                    case (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable:
                        if (!IsInvulnerable(self.team, self.chara))
                            continue;
                        return -1;
                }
            }
            return -1;
        }

        /// <summary>比较两方武将的战力</summary>
        public bool AiCompPower(AI self, int op, int aTeam, int aChara, int bTeam, int bChara)
        {
            int a = AiGetPower(self, aTeam, aChara);
            int b = AiGetPower(self, bTeam, bChara);
            return AiComp(self, op, a, b);
        }

        /// <summary>走决策表挑选一个要发动的必杀</summary>
        public int AiCalcSpecial(AI self)
        {
            // 没有可用的必杀
            if (!self.parent.CanSpecial(self.team, self.chara))
                return -1;
            AI.Row[] table = AiGetTable(self, (int)DuelAITable.DuelAITable_Special);
            if (table == null)
                return -1;
            for (int i = 0; ; i++)
            {
                AI.Row row = table[i];
                if (!Utils.InRange(row.id, 0, (int)DuelAIRow.DuelAIRow_Max - 1))
                    break;
                if (!system.RandBool(row.chance))
                    continue;
                int sp = -1;
                bool buff = false;
                switch (row.id)
                {
                    case (int)DuelAIRow.DuelAIRow_Special_FeintRetreat:
                        sp = (int)DuelSpecial.DuelSpecial_FeintRetreat;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_HiddenWeapon:
                        sp = (int)DuelSpecial.DuelSpecial_HiddenWeapon;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_WeakPoint:
                        sp = (int)DuelSpecial.DuelSpecial_WeakPoint;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_HiddenWeaponOrPeerless:
                        if (AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_HiddenWeapon))
                            sp = (int)DuelSpecial.DuelSpecial_HiddenWeapon;
                        else if (AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Peerless))
                            sp = (int)DuelSpecial.DuelSpecial_Peerless;
                        for (int j = 0; j < (int)DuelBuffType.DuelBuffType_Max; j++)
                        {
                            if (AiHasBuff(self, j))
                            {
                                buff = true;
                                break;
                            }
                        }
                        if (buff)
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_FightingSpirit:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        sp = (int)DuelSpecial.DuelSpecial_FightingSpirit;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_Steadfast:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Defense))
                            continue;
                        sp = (int)DuelSpecial.DuelSpecial_Steadfast;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_Retreat:
                        if (AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual))
                        {
                            AI.Row row2 = new AI.Row(row.chance, row.id, row.param1 + 10, row.param2);
                            if (!AiCompHp(self, row2, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual, true))
                            {
                                int chara = AiCalcSwitch(self);
                                // 有可替换的武将
                                if (Utils.InRange(chara, 0, MaxTeamCharaCount - 1) && chara != self.chara)
                                    continue;
                                sp = (int)DuelSpecial.DuelSpecial_Retreat;
                                break;
                            }
                        }
                        continue;
                    case (int)DuelAIRow.DuelAIRow_Special_Random:
                        sp = AiRandomSpecial(self);
                        break;
                }
                // sp 为 -1 表示本行未选出必杀（原代码此处会越界读取，等价于读取到未知内存）
                if (sp < 0 || !AiIsSpecialEnabled(self, sp))
                    continue;
                return sp;
            }
            return -1;
        }

        /// <summary>走决策表挑选行动方针</summary>
        public int AiCalcStance(AI self)
        {
            int bestChara = AiGetBestChara(self, self.team);
            int opponentBestChara = AiGetBestChara(self, self.opponentTeam);
            AI.Row[] table = AiGetTable(self, (int)DuelAITable.DuelAITable_Stance);
            if (table == null)
                return -1;
            for (int i = 0; ; i++)
            {
                AI.Row row = table[i];
                if (!Utils.InRange(row.id, 0, (int)DuelAIRow.DuelAIRow_Max - 1))
                    break;
                if (!system.RandBool(row.chance))
                    continue;
                switch (row.id)
                {
                    case (int)DuelAIRow.DuelAIRow_Stance_A_Always:
                        return (int)DuelStance.DuelStance_Attack;
                    case (int)DuelAIRow.DuelAIRow_Stance_A_HP_GTE:
                        if (!AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_GreaterThanOrEqual))
                            continue;
                        return (int)DuelStance.DuelStance_Attack;
                    case (int)DuelAIRow.DuelAIRow_Stance_A_LowHP:
                        if (AiGetHp(self) * 2 > AiGetHp(self, true))
                            continue;
                        return (int)DuelStance.DuelStance_Attack;
                    case (int)DuelAIRow.DuelAIRow_Stance_A_OpponentHP_LTE:
                        if (!AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual, true))
                            continue;
                        return (int)DuelStance.DuelStance_Attack;
                    case (int)DuelAIRow.DuelAIRow_Stance_A_OpponentBestChara:
                        if (self.opponentChara != opponentBestChara)
                            continue;
                        return (int)DuelStance.DuelStance_Attack;
                    case (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable:
                        if (!IsInvulnerable(self.team, self.chara))
                            continue;
                        return (int)DuelStance.DuelStance_Attack;
                    case (int)DuelAIRow.DuelAIRow_Stance_D_BestChara:
                        if (self.chara != bestChara)
                            continue;
                        return (int)DuelStance.DuelStance_Defense;
                    case (int)DuelAIRow.DuelAIRow_Stance_D_BlowCounter_GTE:
                        if (blowCounter < row.param1)
                            continue;
                        return (int)DuelStance.DuelStance_Defense;
                    case (int)DuelAIRow.DuelAIRow_Stance_S_HP_GTE:
                        if (!AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_GreaterThanOrEqual))
                            continue;
                        return (int)DuelStance.DuelStance_Spirit;
                    case (int)DuelAIRow.DuelAIRow_Stance_S_Weak:
                        if (!AiCompPower(self, (int)DuelAICompOp.DuelAICompOp_GreaterThanOrEqual, self.opponentTeam, self.opponentChara, self.team, self.chara))
                            continue;
                        return (int)DuelStance.DuelStance_Spirit;
                    case (int)DuelAIRow.DuelAIRow_Stance_S_NotBestChara:
                        if (self.chara == bestChara)
                            continue;
                        return (int)DuelStance.DuelStance_Spirit;
                    case (int)DuelAIRow.DuelAIRow_Stance_S_OpponentNotBestChara:
                        if (self.opponentChara == opponentBestChara)
                            continue;
                        return (int)DuelStance.DuelStance_Spirit;
                    case (int)DuelAIRow.DuelAIRow_Stance_S_HasNotAttackBuff:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        return (int)DuelStance.DuelStance_Spirit;
                    case (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Defense))
                            continue;
                        return (int)DuelStance.DuelStance_Spirit;
                    case (int)DuelAIRow.DuelAIRow_Stance_F_Always:
                        return (int)DuelStance.DuelStance_Fury;
                    case (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE:
                        if (GetStanceTimer(self.team) < row.param1)
                            continue;
                        return -1;
                }
            }
            return -1;
        }

        #endregion
    }
}
