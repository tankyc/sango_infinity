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

        private static readonly AI.Row[] RyofuSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_Kenshu, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_Kiai, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_AnkiOrMusou, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE, 50, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 25, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ShoushinSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_Kenshu, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 50, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ReiseiSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_AnkiOrMusou, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_SpecialTry_Kenshu, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Kiai, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_SpecialTry_Kyuusho, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 50, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] GoutanSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_Kenshu, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_SpecialTry_Kiai, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_AnkiOrMusou, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_Kyuusho, 0, 0),
            new AI.Row( 35, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 50, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE, 25, 0),
            new AI.Row( 35, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ChototsuSpecialTry =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Spirit_GTE, 300, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_SpecialTry_Kiai, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_SpecialTry_Kyuusho, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_HP_LTE, 33, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_SpecialTry_OpponentHP_LTE, 33, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_SpecialTry_Always, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_SpecialTry_Stop, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] RyofuSpecial =
        {
            new AI.Row( 95, (int)DuelAIRow.DuelAIRow_Special_Taikyaku, 12, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_AnkiOrMusou, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ShoushinSpecial =
        {
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_Taikyaku, 16, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_AnkiOrMusou, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Special_Kenshu, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Special_Kyuusho, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ReiseiSpecial =
        {
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_Taikyaku, 14, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_AnkiOrMusou, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_Kiai, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_Kenshu, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Special_Kyuusho, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] GoutanSpecial =
        {
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_Taikyaku, 12, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Special_Kiai, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_AnkiOrMusou, 0, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Special_Kenshu, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_Kyuusho, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ChototsuSpecial =
        {
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Special_Taikyaku, 12, 0),
            new AI.Row( 20, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 15, (int)DuelAIRow.DuelAIRow_Special_Kiai, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Special_Kyuusho, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_Special_Kenshu, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Special_AnkiOrMusou, 0, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Special_Anki, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Special_Random, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] RyofuStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 95, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 1, 0), // 1回合 95% 概率维持
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Stance_S_HP_GTE, 80, 0),
            new AI.Row( 10, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotAttackBuff, 0, 0),
            new AI.Row(  5, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ShoushinStance =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Invulnerable, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Stance_D_BlowCounter_GTE, 40, 0),
            new AI.Row( 90, (int)DuelAIRow.DuelAIRow_Stance_Stop_StanceTimer_GTE, 3, 0), // 3回合 90% 概率维持
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Stance_S_HasNotDefenseBuff, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Stance_A_OpponentHP_LTE, 25, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Stance_A_Always, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ReiseiStance =
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

        private static readonly AI.Row[] GoutanStance =
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

        private static readonly AI.Row[] ChototsuStance =
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

        private static readonly AI.Row[] RyofuSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 50, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Switch_Kunshu, 0, 0),
            new AI.Row( 40, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 33, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ShoushinSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Kunshu, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Switch_NotBestChara, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 75, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 7, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ReiseiSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Switch_Kunshu, 0, 0),
            new AI.Row( 30, (int)DuelAIRow.DuelAIRow_Switch_NotBestChara, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 7, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 50, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] GoutanSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_Kunshu, 0, 0),
            new AI.Row( 80, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 33, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 8, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        private static readonly AI.Row[] ChototsuSwitch =
        {
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_Stop_Invulnerable, 0, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_Kunshu, 0, 0),
            new AI.Row( 70, (int)DuelAIRow.DuelAIRow_Switch_HP_LTE, 25, 0),
            new AI.Row( 60, (int)DuelAIRow.DuelAIRow_Switch_StrengthDiff_LTE, 12, 0),
            new AI.Row(100, (int)DuelAIRow.DuelAIRow_Switch_38, 0, 0),
            new AI.Row(),
        };

        /// <summary>AI 决策总表（8b0a40）[AI 性格][决策表类型]</summary>
        private static readonly AI.Row[][][] AiTable = new AI.Row[][][]
        {
            new AI.Row[][] { RyofuSpecialTry, RyofuSpecial, RyofuStance, RyofuSwitch },
            new AI.Row[][] { ShoushinSpecialTry, ShoushinSpecial, ShoushinStance, ShoushinSwitch },
            new AI.Row[][] { ReiseiSpecialTry, ReiseiSpecial, ReiseiStance, ReiseiSwitch },
            new AI.Row[][] { GoutanSpecialTry, GoutanSpecial, GoutanStance, GoutanSwitch },
            new AI.Row[][] { ChototsuSpecialTry, ChototsuSpecial, ChototsuStance, ChototsuSwitch },
        };

        #endregion

        #region AI 实现

        /// <summary>4fb490</summary>
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

        /// <summary>4fb4d0</summary>
        public AI.Row[] AiGetTable(AI self, int type, int tableId)
        {
            return AiTable[type][tableId];
        }

        /// <summary>4fb500</summary>
        public int AiGetHp(AI self, int team, int chara)
        {
            return self.parent.GetHp(team, chara);
        }

        /// <summary>4fb540</summary>
        public int AiGetHp(AI self, bool opponent = false)
        {
            if (!opponent)
                return AiGetHp(self, self.team, self.chara);
            else
                return AiGetHp(self, self.opponentTeam, self.opponentChara);
        }

        /// <summary>4fb590</summary>
        public int AiGetSpirit(AI self, bool opponent = false)
        {
            if (!opponent)
                return self.parent.GetSpirit(self.team, self.chara);
            else
                return self.parent.GetSpirit(self.opponentTeam, self.opponentChara);
        }

        /// <summary>4fb5e0</summary>
        public AI.Row[] AiGetTable(AI self, int tableId)
        {
            Person person = self.parent.GetPerson(self.team, self.chara);
            if (!Utils.IsActive(person))
                return null;
            if (person.GetId() == PersonId.Ryofu)
                return AiGetTable(self, (int)DuelAIType.DuelAIType_Ryofu, tableId);
            switch (person.GetSeikaku())
            {
                case Seikaku.Shoushin:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Shoushin, tableId);
                case Seikaku.Reisei:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Reisei, tableId);
                case Seikaku.Goutan:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Goutan, tableId);
                case Seikaku.Chototsu:
                    return AiGetTable(self, (int)DuelAIType.DuelAIType_Chototsu, tableId);
            }
            return null;
        }

        /// <summary>4fb6f0</summary>
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

        /// <summary>4fb750</summary>
        public bool AiCompHp(AI self, AI.Row row, int op, bool opponent = false)
        {
            return AiComp(self, op, AiGetHp(self, opponent), row.param1);
        }

        /// <summary>4fb780</summary>
        public bool AiCompSpirit(AI self, AI.Row row, int op, bool opponent = false)
        {
            return AiComp(self, op, AiGetSpirit(self, opponent), row.param1);
        }

        /// <summary>4fb7b0</summary>
        public bool AiIsSpecialEnabled(AI self, int special, bool opponent = false)
        {
            if (!opponent)
                return self.parent.IsSpecialEnabled(self.team, self.chara, special);
            else
                return self.parent.IsSpecialEnabled(self.opponentTeam, self.opponentChara, special);
        }

        /// <summary>4fb830</summary>
        public bool AiHasBuff(AI self, int buff, bool opponent = false)
        {
            if (!opponent)
                return self.parent.HasBuff(self.team, buff);
            else
                return self.parent.HasBuff(self.opponentTeam, buff);
        }

        /// <summary>4fb890</summary>
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
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_AnkiOrMusou:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Anki) && !AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Musou))
                            continue;
                        for (int j = 0; j < (int)DuelBuffType.DuelBuffType_Max; j++)
                        {
                            if (AiHasBuff(self, j, true))
                                return true;
                        }
                        continue;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Kyuusho:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Kyuusho))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Kiai:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Kiai))
                            continue;
                        // 已处于攻击增益状态
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        break;
                    case (int)DuelAIRow.DuelAIRow_SpecialTry_Kenshu:
                        if (!AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Kenshu))
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

        /// <summary>4fbb00</summary>
        public bool AiInit(AI self, int team)
        {
            self.parent = this;
            self.team = team;
            self.opponentTeam = GetOpponentTeam(team);
            AiUpdateChara(self);
            self.initialized = true;
            return true;
        }

        /// <summary>4fbb70</summary>
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

        /// <summary>4fbc60</summary>
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

        /// <summary>4fbcd0</summary>
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
                    case (int)DuelSpecial.DuelSpecial_Hissatsuwaza:
                        weightSum += 20;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Kiai:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        weightSum += 3;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Kenshu:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Defense))
                            continue;
                        weightSum += 3;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Taikyaku:
                        continue;
                    case (int)DuelSpecial.DuelSpecial_Kyuusho:
                        weightSum += 20;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Musou:
                        weightSum += 60;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Anki:
                        weightSum += 20;
                        break;
                    case (int)DuelSpecial.DuelSpecial_Nisetaikyaku:
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

        /// <summary>4fbe00</summary>
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
                    case (int)DuelAIRow.DuelAIRow_Switch_Kunshu:
                        {
                            Person person = GetPerson(self.team, self.chara);
                            if (person == null || !person.IsKunshu())
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

        /// <summary>4fc1a0</summary>
        public bool AiCompPower(AI self, int op, int aTeam, int aChara, int bTeam, int bChara)
        {
            int a = AiGetPower(self, aTeam, aChara);
            int b = AiGetPower(self, bTeam, bChara);
            return AiComp(self, op, a, b);
        }

        /// <summary>4fc1e0</summary>
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
                    case (int)DuelAIRow.DuelAIRow_Special_Nisetaikyaku:
                        sp = (int)DuelSpecial.DuelSpecial_Nisetaikyaku;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_Anki:
                        sp = (int)DuelSpecial.DuelSpecial_Anki;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_Kyuusho:
                        sp = (int)DuelSpecial.DuelSpecial_Kyuusho;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_AnkiOrMusou:
                        if (AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Anki))
                            sp = (int)DuelSpecial.DuelSpecial_Anki;
                        else if (AiIsSpecialEnabled(self, (int)DuelSpecial.DuelSpecial_Musou))
                            sp = (int)DuelSpecial.DuelSpecial_Musou;
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
                    case (int)DuelAIRow.DuelAIRow_Special_Kiai:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Attack))
                            continue;
                        sp = (int)DuelSpecial.DuelSpecial_Kiai;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_Kenshu:
                        if (AiHasBuff(self, (int)DuelBuffType.DuelBuffType_Defense))
                            continue;
                        sp = (int)DuelSpecial.DuelSpecial_Kenshu;
                        break;
                    case (int)DuelAIRow.DuelAIRow_Special_Taikyaku:
                        if (AiCompHp(self, row, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual))
                        {
                            AI.Row row2 = new AI.Row(row.chance, row.id, row.param1 + 10, row.param2);
                            if (!AiCompHp(self, row2, (int)DuelAICompOp.DuelAICompOp_LessThanOrEqual, true))
                            {
                                int chara = AiCalcSwitch(self);
                                // 有可替换的武将
                                if (Utils.InRange(chara, 0, MaxTeamCharaCount - 1) && chara != self.chara)
                                    continue;
                                sp = (int)DuelSpecial.DuelSpecial_Taikyaku;
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

        /// <summary>4fc5a0</summary>
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
