/*
 * 文件名：DuelPhase.cs
 * 描述：单挑流程阶段状态机，由 s11_sys_duel_phase.cpp 翻译而来
 *
 * 说明：
 *   1. 每个阶段都是一个 step 驱动的有限状态机：每调用一次推进一帧（或一步），返回 true 表示单挑结束
 *   2. C++ 中阶段函数通过成员函数指针表分派，C# 中改用 Func<int, bool> 委托数组
 *   3. view 为 false（纯逻辑推演、无表现层）时 IsIdle 恒为 true，数值在动画函数中立即结算
 */

using System;

namespace Sango.Core.Duel
{
    public partial class Duel
    {
        /// <summary>阶段处理函数表，下标与 DuelPhase 一致（8b16d0）</summary>
        private Func<int, bool>[] m_phaseFuncs;

        private Func<int, bool>[] GetPhaseFuncs()
        {
            if (m_phaseFuncs == null)
            {
                m_phaseFuncs = new Func<int, bool>[(int)DuelPhase.DuelPhase_Max]
                {
                    InitPhase,
                    FtkPhase,
                    OpeningPhase,
                    TurnStartPhase,
                    JoinPhase,
                    CommandPhase,
                    ActionStartPhase,
                    SpecialCommandPhase,
                    SpecialPhase,
                    ActionEndPhase,
                    RetreatPhase,
                    TurnEndPhase,
                    ClosingPhase,
                };
            }
            return m_phaseFuncs;
        }

        /// <summary>475810</summary>
        public void None()
        {
        }

        /// <summary>506760, v+4。推进一帧</summary>
        /// <returns>true 表示单挑已结束</returns>
        public virtual bool OnPhase(int delta)
        {
            if (nextPhase >= 0)
            {
                Logger logger = system != null ? system.GetLogger() : null;
                if (logger != null)
                {
                    logger.Debug($"on_phase_change {nextPhase} 0x{system.GetSeed():x}");
                }
                OnPhaseChange();
            }
            if (phase >= 0)
            {
                Func<int, bool>[] func = GetPhaseFuncs();
                return func[phase](delta);
            }
            return false;
        }

        /// <summary>506ff0</summary>
        public void SetNextPhase(int phase)
        {
            if (phase < 0)
                phase = (int)DuelPhase.DuelPhase_Closing;
            nextPhase = phase;
        }

        /// <summary>507020, v+18</summary>
        public virtual void OnPhaseEnd()
        {
            if (phase >= 0)
            {
                // 8b1704：原代码中所有阶段均为空实现
                None();
            }
        }

        /// <summary>507050, v+1c</summary>
        public virtual void OnPhaseBegin()
        {
            if (phase >= 0)
            {
                // 8b169c：原代码中所有阶段均为空实现
                None();
            }
        }

        /// <summary>507080, v+20</summary>
        public virtual void OnPhaseChange()
        {
            OnPhaseEnd();
            phase = nextPhase;
            nextPhase = -1;
            step = 0;
            OnPhaseBegin();
        }

        /// <summary>5070b0, v+28。是否处于空闲（动画播放完毕）状态</summary>
        public virtual bool IsIdle()
        {
            if (view)
            {
                if (engine == null)
                    return true;
                if (engine.DuelIsAnimating(this))
                    return false;
                if (messageboxBlocking && engine.DuelIsMessageBoxVisible(this))
                    return false;
            }
            return true;
        }

        /// <summary>5077f0。初始化阶段</summary>
        public bool InitPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    blowCounter = 0;
                    step = 2;
                    break;
                case 2:
                    SetNextPhase((int)DuelPhase.DuelPhase_FTK);
                    break;
            }
            return false;
        }

        /// <summary>507850。寒暄阶段</summary>
        public bool OpeningPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    if (view && engine != null)
                        engine.DuelOpening(this);
                    step = 2;
                    // 原代码此处疑似缺少 break，但有意为之地落入 case 2
                    goto case 2;
                case 2:
                    if (IsManual())
                        state = DuelState.DuelState_Command;
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnStart);
                    break;
            }
            return false;
        }

        /// <summary>50a6b0。一击必杀阶段</summary>
        public bool FtkPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    step = 6;
                    if (!Utils.InRange(ftkTeam, 0, MaxTeamCount - 1))
                        ftkTeam = CalcFtkTeam();
                    if (Utils.InRange(ftkTeam, 0, MaxTeamCount - 1) && !Utils.InRange(ftkType, 0, (int)DuelFtkType.DuelFtkType_Max - 1))
                        ftkType = CalcFtkType();
                    if (Utils.InRange(ftkType, 0, (int)DuelFtkType.DuelFtkType_Max - 1))
                        step = 2;
                    break;
                case 2:
                    step = 6;
                    if (FtkAnim())
                        step = 3;
                    break;
                case 3:
                    if (!IsIdle())
                        break;
                    if (view && engine != null)
                    {
                        int opponentTeam = GetOpponentTeam(ftkTeam);
                        engine.DuelFtk(this, ftkTeam, GetCurrentChara(ftkTeam), ftkType, opponentTeam, GetCurrentChara(opponentTeam));
                    }
                    step = 4;
                    break;
                case 4:
                    if (!IsIdle())
                        break;
                    result = CalcResult(true, hpAnimQueue);
                    switch (result)
                    {
                        case (int)DuelResult.DuelResult_ChallengerWin:
                        case (int)DuelResult.DuelResult_ChallengedWin:
                            step = 5;
                            break;
                        default:
                            step = 7;
                            break;
                    }
                    break;
                case 5:
                    SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                    break;
                case 6:
                    SetNextPhase((int)DuelPhase.DuelPhase_Opening);
                    break;
                case 7:
                    // 此处正常不会执行
                    System.Diagnostics.Debug.Assert(false);
                    if (IsManual())
                        state = DuelState.DuelState_Command;
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnStart);
                    break;
            }
            return false;
        }

        /// <summary>50a8a0。指令输入阶段</summary>
        public bool CommandPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    if (!IsIdle())
                        break;
                    if ((view && engine != null && engine.DuelIsStopButtonPushed(this)) || state == DuelState.DuelState_Command)
                    {
                        if (view && engine != null)
                            engine.DuelStop(this);
                        step = 1;
                        break;
                    }
                    step = 3;
                    break;
                case 1:
                    if (!IsIdle())
                        break;
                    if (engine == null)
                    {
                        step = 3;
                        break;
                    }
                    state = DuelState.DuelState_Command;
                    for (int i = 0; i < MaxTeamCount; i++)
                    {
                        if (team[i].control != (int)DuelControl.DuelControl_Manual)
                            continue;
                        for (int j = 0; j < MaxTeamCharaCount; j++)
                        {
                            if (!TeamIsActive(team[i], j))
                                continue;
                            if (TeamGetState(team[i], j) == (int)DuelCharaState.DuelCharaState_Waiting)
                                break;
                        }
                    }
                    // 原代码此处为 update_ui
                    step = 2;
                    break;
                case 2:
                    switch (state)
                    {
                        case DuelState.DuelState_Play:
                            step = 3;
                            break;
                        case DuelState.DuelState_Command:
                            if (view && engine != null && !engine.DuelIsPlayButtonPushed(this))
                                break;
                            state = DuelState.DuelState_Play;
                            if (view && engine != null)
                                engine.DuelPlay(this);
                            break;
                        case DuelState.DuelState_SpecialCommand:
                            break;
                    }
                    break;
                case 3:
                    if (!IsIdle())
                        break;
                    UpdateStance(true);
                    UpdateStance();
                    step = 4;
                    break;
                case 4:
                    UpdateSwitching();
                    SetNextPhase((int)DuelPhase.DuelPhase_ActionStart);
                    break;
            }
            return false;
        }

        /// <summary>50aa80。必杀指令输入阶段</summary>
        public bool SpecialCommandPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    if (IsManual(specialTryTeam))
                        state = DuelState.DuelState_SpecialCommand;
                    step = 2;
                    break;
                case 2:
                    if (!IsIdle())
                        break;
                    switch (state)
                    {
                        case DuelState.DuelState_Play:
                            step = 3;
                            break;
                        case DuelState.DuelState_Command:
                            step = 5;
                            break;
                        case DuelState.DuelState_SpecialCommand:
                            if (view && engine != null && engine.DuelIsSpecialCancelButtonPushed(this))
                                state = DuelState.DuelState_Play;
                            break;
                    }
                    break;
                case 3:
                    if (UpdateSpecialAction())
                    {
                        UpdateSpecialActionResult();
                        step = 4;
                    }
                    else
                    {
                        step = 5;
                        break;
                    }
                    break;
                case 4:
                    SetNextPhase((int)DuelPhase.DuelPhase_Special);
                    break;
                case 5:
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnEnd);
                    break;
            }
            return false;
        }

        /// <summary>50abd0。退却阶段</summary>
        public bool RetreatPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    if (UpdateRetreatResult())
                        step = 2;
                    else
                        step = 3;
                    break;
                case 2:
                    if (!IsIdle())
                        break;
                    if (view && engine != null)
                        engine.DuelRetreat(this);
                    SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                    break;
                case 3:
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnEnd);
                    break;
            }
            return false;
        }

        /// <summary>50aca0。回合结束阶段</summary>
        public bool TurnEndPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    if (!IsIdle())
                        break;
                    if (blowCounter < maxBlowCounter)
                    {
                        step = 2;
                    }
                    else
                    {
                        if (view && engine != null)
                            engine.DuelDraw(this);
                        step = 4;
                    }
                    break;
                case 2:
                    UpdateTimer();
                    step = 3;
                    break;
                case 3:
                    ResetAction();
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnStart);
                    break;
                case 4:
                    SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                    break;
            }
            return false;
        }

        /// <summary>50ad70。结束阶段</summary>
        public bool ClosingPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    if (!IsIdle())
                        break;
                    step = 1;
                    break;
                case 1:
                    // 平局
                    if (!Utils.InRange(winnerTeam, 0, MaxTeamCount - 1))
                    {
                        step = 4;
                        break;
                    }
                    step = 2;
                    break;
                case 2:
                    if (!IsIdle())
                        break;
                    if (CalcKillChance(loserTeam))
                    {
                        int t = reverse ? GetOpponentTeam(loserTeam) : loserTeam;
                        param.result[t][GetCurrentChara(t)] = (int)DuelCharaResult.DuelCharaResult_Dead;
                        step = 4;
                        break;
                    }
                    step = 3;
                    break;
                case 3:
                    if (!IsIdle())
                        break;
                    if (CalcCaptureChance(loserTeam))
                    {
                        int t = reverse ? GetOpponentTeam(loserTeam) : loserTeam;
                        param.result[t][GetCurrentChara(t)] = (int)DuelCharaResult.DuelCharaResult_Captured;
                    }
                    step = 4;
                    break;
                case 4:
                    UpdateParamResult();
                    step = 5;
                    break;
                case 5:
                    if (!IsIdle())
                        break;
                    if (view && engine != null)
                        engine.DuelClosing(this);
                    step = 6;
                    break;
                case 6:
                    return true;
            }
            return false;
        }

        /// <summary>50b5a0, v+24</summary>
        public virtual bool IsValidPhase(int phase)
        {
            return Utils.InRange(phase, 0, (int)DuelPhase.DuelPhase_Max - 1);
        }

        /// <summary>50bf30。登场阶段</summary>
        public bool JoinPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    step = 2;
                    break;
                case 2:
                    if (!IsIdle())
                        break;
                    if (!JoinAnim())
                        break;
                    step = 3;
                    break;
                case 3:
                    if (!IsIdle())
                        break;
                    if (state != DuelState.DuelState_Command)
                    {
                        // 原代码此处为 update ui
                    }
                    SetNextPhase((int)DuelPhase.DuelPhase_Command);
                    break;
            }
            return false;
        }

        /// <summary>50bfc0。必杀阶段</summary>
        public bool SpecialPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    goto case 1;
                case 1:
                    if (!IsValid(specialAction))
                    {
                        step = 5;
                        break;
                    }
                    if (specialAction.type == (int)DuelSpecial.DuelSpecial_Taikyaku)
                    {
                        SetNextPhase((int)DuelPhase.DuelPhase_Retreat);
                        break;
                    }
                    if (SpecialActionAnim())
                        step = 2;
                    else
                        step = 5;
                    break;
                case 2:
                    if (!Utils.InRange(result, 0, (int)DuelResult.DuelResult_Max - 1))
                        result = CalcResult(true, hpAnimQueue);
                    switch (result)
                    {
                        case (int)DuelResult.DuelResult_ChallengerWin:
                        case (int)DuelResult.DuelResult_ChallengedWin:
                            step = 4;
                            break;
                        case (int)DuelResult.DuelResult_2:
                        case (int)DuelResult.DuelResult_Draw:
                            SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                            break;
                        default:
                            step = 3;
                            break;
                    }
                    break;
                case 3:
                    if (!IsIdle())
                        break;
                    step = 5;
                    break;
                case 4:
                    if (!IsIdle())
                        break;
                    SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                    break;
                case 5:
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnEnd);
                    break;
            }
            return false;
        }

        /// <summary>50c6b0。回合开始阶段</summary>
        public bool TurnStartPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    specialTryTeam = CalcSpecialTry();
                    if (Utils.InRange(specialTryTeam, 0, (int)DuelTeam.DuelTeam_Max - 1))
                        step = 2;
                    else
                        step = 3;
                    break;
                case 2:
                    if (!IsIdle())
                        break;
                    SetNextPhase((int)DuelPhase.DuelPhase_SpecialCommand);
                    break;
                case 3:
                    CalcAppearing();
                    SetNextPhase((int)DuelPhase.DuelPhase_Join);
                    break;
            }
            return false;
        }

        /// <summary>50c760。行动开始阶段</summary>
        public bool ActionStartPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    step = 2;
                    break;
                case 2:
                    if (!IsIdle())
                        break;
                    if (!SwitchAnim())
                        break;
                    // 原代码此处为 update ui
                    step = 3;
                    break;
                case 3:
                    UpdateAction();
                    SetNextPhase((int)DuelPhase.DuelPhase_ActionEnd);
                    break;
            }
            return false;
        }

        /// <summary>50c7f0。行动结束阶段</summary>
        public bool ActionEndPhase(int delta)
        {
            switch (step)
            {
                case 0:
                    step = 1;
                    break;
                case 1:
                    if (ActionAnim())
                        step = 2;
                    else
                        step = 4;
                    break;
                case 2:
                    if (!Utils.InRange(result, 0, (int)DuelResult.DuelResult_Max - 1))
                        result = CalcResult(true, hpAnimQueue);
                    switch (result)
                    {
                        case (int)DuelResult.DuelResult_ChallengerWin:
                        case (int)DuelResult.DuelResult_ChallengedWin:
                            step = 4;
                            break;
                        case (int)DuelResult.DuelResult_2:
                        case (int)DuelResult.DuelResult_Draw:
                            SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                            break;
                        default:
                            step = 3;
                            break;
                    }
                    break;
                case 3:
                    if (!IsIdle())
                        break;
                    step = 5;
                    break;
                case 4:
                    if (!IsIdle())
                        break;
                    step = 6;
                    break;
                case 5:
                    SetNextPhase((int)DuelPhase.DuelPhase_TurnEnd);
                    break;
                case 6:
                    SetNextPhase((int)DuelPhase.DuelPhase_Closing);
                    break;
            }
            return false;
        }
    }
}
