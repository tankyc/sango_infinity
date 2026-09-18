#include "pch.h"

s11_begin_namespace

/// <summary>475810</summary>
void Duel::none()
{
}

/// <summary>506760, v+4</summary>
bool Duel::on_phase(int delta)
{
    if (next_phase_ >= 0)
    {
#if s11_log
        if (Logger* logger = system_->get_logger())
        {
            logger->debug(std::format("on_phase_change {} 0x{:x}",
                next_phase_,
                system_->get_seed()
            ));
        }
#endif
        on_phase_change();
    }
    if (phase_ >= 0)
    {
        // 8b16d0
        static constexpr std::array<decltype(&Duel::init_phase), DuelPhase_Max> func = {
            &Duel::init_phase,
            &Duel::ftk_phase,
            &Duel::opening_phase,
            &Duel::turn_start_phase,
            &Duel::join_phase,
            &Duel::command_phase,
            &Duel::action_start_phase,
            &Duel::special_command_phase,
            &Duel::special_phase,
            &Duel::action_end_phase,
            &Duel::retreat_phase,
            &Duel::turn_end_phase,
            &Duel::closing_phase,
        };
        return (this->*func[phase_])(delta);
    }
    return false;
}

/// <summary>506ff0</summary>
void Duel::set_next_phase(duel_phase_t phase)
{
    if (phase < 0)
        phase = DuelPhase_Closing;
    next_phase_ = phase;
}

/// <summary>507020, v+18</summary>
void Duel::on_phase_end()
{
    if (phase_ >= 0)
    {
        // 8b1704
        static constexpr std::array<decltype(&Duel::none), DuelPhase_Max> func = {
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
        };
        (this->*func[phase_])();
    }
}

/// <summary>507050, v+1c</summary>
void Duel::on_phase_begin()
{
    if (phase_ >= 0)
    {
        // 8b169c
        static constexpr std::array<decltype(&Duel::none), DuelPhase_Max> func = {
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
            &Duel::none,
        };
        (this->*func[phase_])();
    }
}

/// <summary>507080, v+20</summary>
void Duel::on_phase_change()
{
    on_phase_end();
    phase_ = next_phase_;
    next_phase_ = -1;
    step_ = 0;
    on_phase_begin();
}

/// <summary>5070b0, v+28</summary>
bool Duel::is_idle() const
{
    if (view_)
    {
        if (engine_->duel_is_animating(this))
            return false;
        if (messagebox_blocking_ and engine_->duel_is_messagebox_visible(this))
            return false;
    }
    return true;
}

/// <summary>5077f0</summary>
bool Duel::init_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        blow_counter_ = 0;
#if s11_removed
#endif
        step_ = 2;
        break;
    case 2:
        set_next_phase(DuelPhase_FTK);
        break;
    }
    return false;
}

/// <summary>507850</summary>
bool Duel::opening_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        if (view_)
        {
            duel_team_t team = reverse_ ? DuelTeam_Challenged : DuelTeam_Challenger;
            engine_->duel_opening(this);
        }
        step_ = 2;
#if s11_suspicious(3)
        // break 누락된게 아닌지? 중요하진 않음.
#endif
        [[fallthrough]];
    case 2:
        if (is_manual())
            state_ = DuelState_Command;
        set_next_phase(DuelPhase_TurnStart);
        break;
    }
    return false;
}

/// <summary>50a6b0</summary>
bool Duel::ftk_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        step_ = 6;
        if (not utils::in_range(ftk_team_, 0, MaxTeamCount - 1))
            ftk_team_ = calc_ftk_team();
        if (utils::in_range(ftk_team_, 0, MaxTeamCount - 1) and not utils::in_range(ftk_type_, 0, DuelFtkType_Max - 1))
            ftk_type_ = calc_ftk_type();
        if (utils::in_range(ftk_type_, 0, DuelFtkType_Max - 1))
            step_ = 2;
        break;
    case 2:
        step_ = 6;
        if (ftk_anim())
            step_ = 3;
        break;
    case 3:
        if (not is_idle())
            break;
        if (view_)
        {
            duel_team_t opponent_team = get_opponent_team(ftk_team_);
            engine_->duel_ftk(this, ftk_team_, get_current_chara(ftk_team_), ftk_type_, opponent_team, get_current_chara(opponent_team));
        }
        step_ = 4;
        break;
    case 4:
        if (not is_idle())
            break;
        result_ = calc_result(true, &hp_anim_queue_[0]);
        switch (result_)
        {
        case DuelResult_ChallengerWin:
        case DuelResult_ChallengedWin:
            step_ = 5;
            break;
        default:
            step_ = 7;
            break;
        }
        break;
    case 5:
        set_next_phase(DuelPhase_Closing);
        break;
    case 6:
        set_next_phase(DuelPhase_Opening);
        break;
    case 7:
        // 여기가 실행될 경우는 없음
        assert(false);
        if (is_manual())
            state_ = DuelState_Command;
        set_next_phase(DuelPhase_TurnStart);
        break;
    }
    return false;
}

/// <summary>50a8a0</summary>
bool Duel::command_phase(int delta)
{
    bool sub = false;
    switch (step_)
    {
    case 0:
        if (not is_idle())
            break;
        if ((view_ and engine_->duel_is_stop_button_pushed(this)) or state_ == DuelState_Command)
        {
            if (view_)
                engine_->duel_stop(this);
            step_ = 1;
            break;
        }
        step_ = 3;
        break;
    case 1:
        if (not is_idle())
            break;
        if (not engine_)
        {
            step_ = 3;
            break;
        }
        state_ = DuelState_Command;
        for (int i = 0; i < MaxTeamCount; i++)
        {
            if (team_[i].control != DuelControl_Manual)
                continue;
            for (int j = 0; j < MaxTeamCharaCount; j++)
            {
                if (not team_is_active(&team_[i], j))
                    continue;
                if (team_get_state(&team_[i], j) == DuelCharaState_Waiting)
                {
                    sub = true;
                    break;
                }
            }
        }
#if s11_removed
        // update_ui
#endif
        step_ = 2;
        break;
    case 2:
        switch (state_)
        {
        case DuelState_Play:
            step_ = 3;
            break;
        case DuelState_Command:
            if (view_ and not engine_->duel_is_play_button_pushed(this))
                break;
            state_ = DuelState_Play;
            if (view_)
                engine_->duel_play(this);
            break;
        case DuelState_SpecialCommand:
            break;
        }
        break;
    case 3:
        if (not is_idle())
            break;
        update_stance(true);
        update_stance();
        step_ = 4;
        break;
    case 4:
        update_switching();
        set_next_phase(DuelPhase_ActionStart);
        break;
    }
    return false;
}

/// <summary>50aa80</summary>
bool Duel::special_command_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        if (is_manual(special_try_team_))
        {
#if s11_removed
            if (view_)
                engine_->duel_special_enter(this);
#endif
            state_ = DuelState_SpecialCommand;
        }
        step_ = 2;
        break;
    case 2:
        if (not is_idle())
            break;
        switch (state_)
        {
        case DuelState_Play:
            step_ = 3;
            break;
        case DuelState_Command:
            step_ = 5;
            break;
        case DuelState_SpecialCommand:
            if (view_ and engine_->duel_is_special_cancel_button_pushed(this))
                state_ = DuelState_Play;
            break;
        }
        break;
    case 3:
        if (update_special_action())
        {
            update_special_action_result();
            step_ = 4;
        }
        else
        {
            step_ = 5;
            break;
        }
        if (is_manual(special_try_team_))
        {
#if s11_removed
            if (view_)
                engine_->duel_special_try_anim(this);
#endif
        }
        break;
    case 4:
        set_next_phase(DuelPhase_Special);
        break;
    case 5:
        set_next_phase(DuelPhase_TurnEnd);
        break;
    }
    return false;
}

/// <summary>50abd0</summary>
bool Duel::retreat_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
#if s11_removed
        // ?
#endif
        break;
    case 1:
        if (update_retreat_result())
            step_ = 2;
        else
            step_ = 3;
        break;
    case 2:
        if (not is_idle())
            break;
        if (view_)
            engine_->duel_retreat(this);
        set_next_phase(DuelPhase_Closing);
        break;
    case 3:
        set_next_phase(DuelPhase_TurnEnd);
        break;
    }
    return false;
}

/// <summary>50aca0</summary>
bool Duel::turn_end_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        if (not is_idle())
            break;
#if s11_removed
        if (view_)
            engine_->duel_close_message(this);
#endif
        if (blow_counter_ < max_blow_counter_)
        {
            step_ = 2;
        }
        else
        {
            if (view_)
                engine_->duel_draw(this);
            step_ = 4;
        }
        break;
    case 2:
        update_timer();
        step_ = 3;
        break;
    case 3:
        reset_action();
        set_next_phase(DuelPhase_TurnStart);
        break;
    case 4:
        set_next_phase(DuelPhase_Closing);
        break;
    }
    return false;
}

/// <summary>50ad70</summary>
bool Duel::closing_phase(int delta)
{
    switch (step_)
    {
    case 0:
        if (not is_idle())
            break;
#if s11_removed
#endif
        step_ = 1;
        break;
    case 1:
        // 무승부
        if (not utils::in_range(winner_team_, 0, MaxTeamCount - 1))
        {
            step_ = 4;
            break;
        }
#if s11_removed
#endif
        step_ = 2;
        break;
    case 2:
        if (not is_idle())
            break;
        if (calc_kill_chance(loser_team_))
        {
            duel_team_t team = reverse_ ? get_opponent_team(loser_team_) : loser_team_;
            param_->result[team][get_current_chara(team)] = DuelCharaResult_Dead;
            step_ = 4;
            break;
        }
        step_ = 3;
        break;
    case 3:
        if (not is_idle())
            break;
        if (calc_capture_chance(loser_team_))
        {
            duel_team_t team = reverse_ ? get_opponent_team(loser_team_) : loser_team_;
            param_->result[team][get_current_chara(team)] = DuelCharaResult_Captured;
        }
        step_ = 4;
        break;
    case 4:
        update_param_result();
        step_ = 5;
        break;
    case 5:
        if (not is_idle())
            break;
        if (view_)
            engine_->duel_closing(this);
        step_ = 6;
        break;
    case 6:
        return true;
    }
    return false;
}

/// <summary>50b5a0, v+24</summary>
bool Duel::is_valid_phase(int phase) const
{
    return utils::in_range(phase, 0, DuelPhase_Max - 1);
}

/// <summary>50bf30</summary>
bool Duel::join_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        step_ = 2;
        break;
    case 2:
        if (not is_idle())
            break;
        if (not join_anim())
            break;
        step_ = 3;
        break;
    case 3:
        if (not is_idle())
            break;
        if (state_ != DuelState_Command)
        {
#if s11_removed
            // update ui
#endif
        }
        set_next_phase(DuelPhase_Command);
        break;
    }
    return false;
}

/// <summary>50bfc0</summary>
bool Duel::special_phase(int delta)
{
    switch (step_)
    {
    case 0:
#if s11_removed
        if (view_ and is_manual(special_try_team_))
            engine_->duel_special_start(this);
#endif
        step_ = 1;
        [[fallthrough]];
    case 1:
        if (not is_valid(&special_action_))
        {
            step_ = 5;
            break;
        }
        if (special_action_.type == DuelSpecial_Taikyaku)
        {
            set_next_phase(DuelPhase_Retreat);
            break;
        }
        if (special_action_anim())
            step_ = 2;
        else
            step_ = 5;
        break;
    case 2:
        if (not utils::in_range(result_, 0, DuelResult_Max - 1))
            result_ = calc_result(true, hp_anim_queue_.data());
        switch (result_)
        {
        case DuelResult_ChallengerWin:
        case DuelResult_ChallengedWin:
            step_ = 4;
            break;
        case DuelResult_2:
        case DuelResult_Draw:
            set_next_phase(DuelPhase_Closing);
            break;
        default:
            step_ = 3;
            break;
        }
        break;
    case 3:
        if (not is_idle())
            break;
#if s11_removed
        if (view_)
            engine_->duel_special_end(this);
#endif
        step_ = 5;
        break;
    case 4:
        if (not is_idle())
            break;
#if s11_removed
        if (view_)
            engine_->duel_special_end(this);
#endif
        set_next_phase(DuelPhase_Closing);
        break;
    case 5:
        set_next_phase(DuelPhase_TurnEnd);
        break;
    }
    return false;
}

/// <summary>50c6b0</summary>
bool Duel::turn_start_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        special_try_team_ = calc_special_try();
        if (utils::in_range(special_try_team_, 0, DuelTeam_Max - 1))
            step_ = 2;
        else
            step_ = 3;
        break;
    case 2:
        if (not is_idle())
            break;
#if s11_removed
        if (view_)
            engine_->duel_hide_stance_ui();
#endif
        set_next_phase(DuelPhase_SpecialCommand);
        break;
    case 3:
        calc_appearing();
        set_next_phase(DuelPhase_Join);
        break;
    }
    return false;
}

/// <summary>50c760</summary>
bool Duel::action_start_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        step_ = 2;
        break;
    case 2:
        if (not is_idle())
            break;
        if (not switch_anim())
            break;
#if s11_removed
        // update ui
#endif
        step_ = 3;
        break;
    case 3:
        update_action();
        set_next_phase(DuelPhase_ActionEnd);
        break;
    }
    return false;
}

/// <summary>50c7f0</summary>
bool Duel::action_end_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_ = 1;
        break;
    case 1:
        if (action_anim())
            step_ = 2;
        else
            step_ = 4;
        break;
    case 2:
        if (not utils::in_range(result_, 0, DuelResult_Max - 1))
            result_ = calc_result(true, hp_anim_queue_.data());
        switch (result_)
        {
        case DuelResult_ChallengerWin:
        case DuelResult_ChallengedWin:
            step_ = 4;
            break;
        case DuelResult_2:
        case DuelResult_Draw:
            set_next_phase(DuelPhase_Closing);
            break;
        default:
            step_ = 3;
            break;
        }
        break;
    case 3:
        if (not is_idle())
            break;
#if s11_removed
        if (view_)
            engine_->duel_action_anim(this);
#endif
        step_ = 5;
        break;
    case 4:
        if (not is_idle())
            break;
#if s11_removed
        if (view_)
            engine_->duel_action_anim(this);
#endif
        step_ = 6;
        break;
    case 5:
        set_next_phase(DuelPhase_TurnEnd);
        break;
    case 6:
        set_next_phase(DuelPhase_Closing);
        break;
    }
    return false;
}

s11_end_namespace