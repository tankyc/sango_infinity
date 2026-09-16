/*
 * 文件名：DebatePhase.cs
 * 描述：舌战(Debate)阶段状态机，由 s11_sys_debate_phase.cpp 翻译而来
 *
 * 翻译说明：
 *   1. C++ 用函数指针数组（8b32f8 / 8b3320 / 8b3348）分发阶段处理函数；
 *      C# 改为 switch 分发，语义等价且可读性更好。
 *   2. C++ 的 step_++ / next_phase_ = xxx 直接改为对字段 step / nextPhase 的操作。
 *   3. 原 C++ 中被 #if s11_removed 移除的空步骤，这里保留步号以避免改变流程节奏，
 *      并用注释标注。
 */

using System;

namespace Sango.Core.Debate
{
    public partial class Debate
    {
        /// <summary>475810。空阶段处理</summary>
        public void None()
        {
        }

        /// <summary>51ebe0。阶段驱动。返回 false 表示舌战结束</summary>
        public bool Update(int delta)
        {
            if (nextPhase != phase)
            {
                OnPhaseEnd();
                step = 0;
                phase = nextPhase;
                OnPhaseBegin();
            }
            return OnPhase(delta);
        }

        /// <summary>51ec20。设置下一个阶段</summary>
        public void SetNextPhase(int phase)
        {
            nextPhase = phase;
        }

        /// <summary>51ec30。推进步骤</summary>
        public void IncStep()
        {
            step++;
        }

        /// <summary>51fa70, v+c。阶段是否合法</summary>
        public virtual bool IsValidPhase(int phase)
        {
            return phase >= 0;
        }

        /// <summary>51fa90, v+10。阶段开始（8b32f8）</summary>
        public virtual void OnPhaseBegin()
        {
            if (phase < 0)
                return;
            switch (phase)
            {
                case (int)DebatePhase.DebatePhase_Damage:
                    DamagePhaseBegin();
                    break;
                default:
                    None();
                    break;
            }
        }

        /// <summary>51fab0, v+14。阶段处理（8b3320）</summary>
        public virtual bool OnPhase(int delta)
        {
            if (phase < 0)
                return false;
            switch (phase)
            {
                case (int)DebatePhase.DebatePhase_Opening: return OpeningPhase(delta);
                case (int)DebatePhase.DebatePhase_FTK: return FtkPhase(delta);
                case (int)DebatePhase.DebatePhase_Unknown2: return Unknown2Phase(delta);
                case (int)DebatePhase.DebatePhase_TurnStart: return TurnStartPhase(delta);
                case (int)DebatePhase.DebatePhase_Play: return PlayPhase(delta);
                case (int)DebatePhase.DebatePhase_Damage: return DamagePhase(delta);
                case (int)DebatePhase.DebatePhase_Anger: return AngerPhase(delta);
                case (int)DebatePhase.DebatePhase_TurnEnd: return TurnEndPhase(delta);
                case (int)DebatePhase.DebatePhase_Critical: return CriticalPhase(delta);
                case (int)DebatePhase.DebatePhase_Closing: return ClosingPhase(delta);
            }
            return false;
        }

        /// <summary>51fae0, v+18。阶段结束（8b3348）</summary>
        public virtual void OnPhaseEnd()
        {
            if (phase < 0)
                return;
            None();
        }

        /// <summary>51fb00。开场阶段</summary>
        public bool OpeningPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    if (view && engine != null)
                        engine.DebateOpening(this);
                    step++;
                    break;
                case 1:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 2:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 3:
                    nextPhase = (int)DebatePhase.DebatePhase_FTK;
                    break;
            }
            return true;
        }

        /// <summary>51fb90。一击必杀阶段</summary>
        public bool FtkPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    if (!Ftk())
                        nextPhase = (int)DebatePhase.DebatePhase_Unknown2;
                    step++;
                    break;
                case 1:
                    nextPhase = (int)DebatePhase.DebatePhase_Closing;
                    break;
            }
            return true;
        }

        /// <summary>51fbd0。未知阶段</summary>
        public bool Unknown2Phase(int delta)
        {
            switch (step)
            {
                case 0:
                    step++;
                    break;
                case 1:
                    nextPhase = (int)DebatePhase.DebatePhase_TurnStart;
                    break;
            }
            return true;
        }

        /// <summary>51fc00。回合开始阶段</summary>
        public bool TurnStartPhase(int delta)
        {
            TurnStart();
            nextPhase = (int)DebatePhase.DebatePhase_Play;
            return true;
        }

        /// <summary>51fc40。出牌阶段</summary>
        public bool PlayPhase(int delta)
        {
            int team = first;
            int opponentTeam = GetOpponentTeam(team);
            int cardIndex;
            int card;
            switch (step)
            {
                case 0:
                    // 己方出牌
                    if (characters[team].control && view && engine != null)
                        cardIndex = engine.DebateSelectCard(this, team);
                    else
                        cardIndex = AiCalcCard(aiContext[team]);
                    if (!Utils.InRange(cardIndex, 0, characters[team].maxCardCount - 1))
                        break;
                    card = CharacterGetCard(characters[team], cardIndex);
                    if (card < 0)
                        break;
                    PlayCard(team, cardIndex);
                    step++;
                    break;
                case 1:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 2:
                    card = GetPlayedCard(team);
                    if (card == (int)DebateCard.DebateCard_Rethink)
                    {
                        Rethink(team);
                        step++;
                        break;
                    }
                    step = 4;
                    break;
                case 3:
                    ResetPlayedCard(team);
                    step = 0;
                    break;
                case 4:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 5:
                    // 对方出牌
                    if (characters[opponentTeam].control && view && engine != null)
                        cardIndex = engine.DebateSelectCard(this, opponentTeam);
                    else
                        cardIndex = AiCalcCard(aiContext[opponentTeam]);
                    if (!Utils.InRange(cardIndex, 0, characters[opponentTeam].maxCardCount - 1))
                        break;
                    card = CharacterGetCard(characters[opponentTeam], cardIndex);
                    if (card < 0)
                        break;
                    PlayCard(opponentTeam, cardIndex);
                    step++;
                    break;
                case 6:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 7:
                    card = GetPlayedCard(opponentTeam);
                    if (card == (int)DebateCard.DebateCard_Rethink)
                    {
                        Rethink(opponentTeam);
                        step++;
                        break;
                    }
                    step = 9;
                    break;
                case 8:
                    ResetPlayedCard(opponentTeam);
                    step = 4;
                    break;
                case 9:
                    SetNextPhase((int)DebatePhase.DebatePhase_Damage);
                    break;
            }
            return true;
        }

        /// <summary>51ff40。伤害阶段开始</summary>
        public void DamagePhaseBegin()
        {
            CalcAttacker();
        }

        /// <summary>51ff50。回合结束阶段</summary>
        public bool TurnEndPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    TurnEnd();
                    step++;
                    break;
                case 1:
                    nextPhase = (int)DebatePhase.DebatePhase_TurnStart;
                    break;
            }
            return true;
        }

        /// <summary>51ff90。会心阶段</summary>
        public bool CriticalPhase(int delta)
        {
            int criticalValue;
            switch (step)
            {
                case 0:
                    if (characters[winner].control && view && engine != null)
                        criticalValue = engine.DebateSelectCritical(this, winner);
                    else
                        criticalValue = AiCalcCritical(aiContext[winner]);
                    if (!Utils.InRange(criticalValue, 0, (int)DebateCritical.DebateCritical_Max - 1))
                        break;
                    critical = criticalValue;
                    LogDebate($"【会心】{GetTeamName(winner)} 做出抉择：{(criticalValue == (int)DebateCritical.DebateCritical_PushOn ? "追击" : "留情")}");
                    step++;
                    break;
                case 1:
                    CalcWinType();
                    step++;
                    break;
                case 2:
                    nextPhase = (int)DebatePhase.DebatePhase_Closing;
                    break;
            }
            return true;
        }

        /// <summary>520060。结束阶段</summary>
        public bool ClosingPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    if (view && engine != null)
                        engine.DebateClosing(this);
                    step++;
                    break;
                case 1:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 2:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 3:
                    ParamSetWinner(param, winner, winType);
                    return false;
            }
            return true;
        }

        /// <summary>520620。愤怒阶段</summary>
        public bool AngerPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    CalcAngering(true);
                    step++;
                    break;
                case 1:
                    Anger();
                    step++;
                    break;
                case 2:
                    if (combo)
                    {
                        ComboAttack();
                        break;
                    }
                    step++;
                    break;
                case 3:
                    if (CalcWinner())
                    {
                        nextPhase = CanCritical() ? (int)DebatePhase.DebatePhase_Critical : (int)DebatePhase.DebatePhase_Closing;
                        break;
                    }
                    step++;
                    break;
                case 4:
                    CalcAngering(false);
                    step++;
                    break;
                case 5:
                    Anger();
                    step++;
                    break;
                case 6:
                    if (combo)
                    {
                        ComboAttack();
                        break;
                    }
                    step++;
                    break;
                case 7:
                    CalcCrumbled();
                    step++;
                    break;
                case 8:
                    if (CalcWinner())
                    {
                        nextPhase = CanCritical() ? (int)DebatePhase.DebatePhase_Critical : (int)DebatePhase.DebatePhase_Closing;
                        break;
                    }
                    nextPhase = (int)DebatePhase.DebatePhase_TurnEnd;
                    break;
            }
            return true;
        }

        /// <summary>520820。伤害阶段</summary>
        public bool DamagePhase(int delta)
        {
            switch (step)
            {
                case 0:
                    // 原 C++ s11_removed
                    step++;
                    break;
                case 1:
                    Attack();
                    step++;
                    break;
                case 2:
                    Effect();
                    step++;
                    break;
                case 3:
                    LogDebate($"【状态】话题「{system.GetTopicName(topic)}」 → {TeamState(0)} / {TeamState(1)}");
                    if (CalcWinner())
                    {
                        nextPhase = CanCritical() ? (int)DebatePhase.DebatePhase_Critical : (int)DebatePhase.DebatePhase_Closing;
                        break;
                    }
                    nextPhase = (int)DebatePhase.DebatePhase_Anger;
                    break;
            }
            return true;
        }
    }
}
