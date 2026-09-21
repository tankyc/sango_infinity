#include "pch.h"

s11_begin_namespace

/// <summary>475810</summary>
void Debate::none()
{
}

/// <summary>51ebe0</summary>
bool Debate::update(int delta)
{
    if (next_phase_ != phase_)
    {
        on_phase_end();
        step_ = 0;
        phase_ = next_phase_;
        on_phase_begin();
    }
    return on_phase(delta);
}

/// <summary>51ec20</summary>
void Debate::set_next_phase(debate_phase_t phase)
{
    next_phase_ = phase;
}

/// <summary>51ec30</summary>
void Debate::inc_step()
{
    step_++;
}

/// <summary>51fa70, v+c</summary>
bool Debate::is_valid_phase(debate_phase_t phase) s11_pure
{
    return phase >= 0;
}

/// <summary>51fa90, v+10</summary>
void Debate::on_phase_begin() s11_pure
{
    if (phase_ >= 0)
    {
        // 8b32f8
        static constexpr std::array<decltype(&Debate::none), DebatePhase_Max> func = {
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::damage_phase_begin,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
        };
        (this->*func[phase_])();
    }
}

/// <summary>51fab0, v+14</summary>
bool Debate::on_phase(int delta) s11_pure
{
    if (phase_ >= 0)
    {
        // 8b3320
        static constexpr std::array<decltype(&Debate::on_phase), DebatePhase_Max> func = {
            &Debate::opening_phase,
            &Debate::ftk_phase,
            &Debate::unknown2_phase,
            &Debate::turn_start_phase,
            &Debate::play_phase,
            &Debate::damage_phase,
            &Debate::anger_phase,
            &Debate::turn_end_phase,
            &Debate::critical_phase,
            &Debate::closing_phase,
        };
        return (this->*func[phase_])(delta);
    }
    return false;
}

/// <summary>51fae0, v+18</summary>
void Debate::on_phase_end() s11_pure
{
    if (phase_ >= 0)
    {
        // 8b3348
        static constexpr std::array<decltype(&Debate::none), DebatePhase_Max> func = {
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
            &Debate::none,
        };
        (this->*func[phase_])();
    }
}

/// <summary>51fb00</summary>
bool Debate::opening_phase(int delta)
{
    switch (step_)
    {
    case 0:
        if (view_)
            engine_->debate_opening(this);
        step_++;
        break;
    case 1:
#if s11_removed
        // ?
#endif
        step_++;
        break;
    case 2:
#if s11_removed
        // ?
#endif
        step_++;
        break;
    case 3:
        next_phase_ = DebatePhase_FTK;
        break;
    }
    return true;
}

/// <summary>51fb90</summary>
bool Debate::ftk_phase(int delta)
{
    switch (step_)
    {
    case 0:
        if (not ftk())
            next_phase_ = DebatePhase_Unknown2;
        step_++;
        break;
    case 1:
        next_phase_ = DebatePhase_Closing;
        break;
    }
    return true;
}

/// <summary>51fbd0</summary>
bool Debate::unknown2_phase(int delta)
{
    switch (step_)
    {
    case 0:
        step_++;
        break;
    case 1:
        next_phase_ = DebatePhase_TurnStart;
        break;
    }
    return true;
}

/// <summary>51fc00</summary>
bool Debate::turn_start_phase(int delta)
{
    turn_start();
#if s11_removed
#endif
    next_phase_ = DebatePhase_Play;
    return true;
}

/// <summary>51fc40</summary>
bool Debate::play_phase(int delta)
{
    debate_team_t team = first_;
    debate_team_t opponent_team = get_opponent_team(team);
    int card_index;
    debate_card_t card;
    switch (step_)
    {
    case 0:
        if (chara_[team].control and view_)
            card_index = engine_->debate_select_card(this, team);
        else
            card_index = ai_calc_card(&ai_[team]);
        if (not utils::in_range(card_index, 0, chara_[team].max_card_count - 1))
            break;
        card = chara_get_card(&chara_[team], card_index);
        if (card < 0)
            break;
        play_card(team, card_index);
        step_++;
        break;
    case 1:
#if s11_removed
#endif
        step_++;
        break;
    case 2:
        card = get_played_card(team);
        if (card == DebateCard_Rethink)
        {
            rethink(team);
            step_++;
            break;
        }
        step_ = 4;
        break;
    case 3:
        reset_played_card(team);
#if s11_removed
#endif
        step_ = 0;
        break;
    case 4:
#if s11_removed
#endif
        step_++;
        break;
    case 5:
        if (chara_[opponent_team].control and view_)
            card_index = engine_->debate_select_card(this, opponent_team);
        else
            card_index = ai_calc_card(&ai_[opponent_team]);
        if (not utils::in_range(card_index, 0, chara_[opponent_team].max_card_count - 1))
            break;
        card = chara_get_card(&chara_[opponent_team], card_index);
        if (card < 0)
            break;
        play_card(opponent_team, card_index);
        step_++;
        break;
    case 6:
#if s11_removed
#endif
        step_++;
        break;
    case 7:
        card = get_played_card(opponent_team);
        if (card == DebateCard_Rethink)
        {
            rethink(opponent_team);
            step_++;
            break;
        }
        step_ = 9;
        break;
    case 8:
        reset_played_card(opponent_team);
#if s11_removed
#endif
        step_ = 4;
        break;
    case 9:
#if s11_removed
#endif
        set_next_phase(DebatePhase_Damage);
        break;
    }
    return true;
}

/// <summary>51ff40</summary>
void Debate::damage_phase_begin()
{
    calc_attacker();
}

/// <summary>51ff50</summary>
bool Debate::turn_end_phase(int delta)
{
    switch (step_)
    {
    case 0:
        turn_end();
        step_++;
        break;
    case 1:
        next_phase_ = DebatePhase_TurnStart;
        break;
    }
    return true;
}

/// <summary>51ff90</summary>
bool Debate::critical_phase(int delta)
{
    debate_critical_t critical;
    switch (step_)
    {
    case 0:
        if (chara_[winner_].control and view_)
            critical = engine_->debate_select_critical(this, winner_);
        else
            critical = ai_calc_critical(&ai_[winner_]);
        if (not utils::in_range(critical, 0, DebateCritical_Max - 1))
            break;
        critical_ = critical;
#if s11_removed
        if (chara_[winner_].control and view_)
            engine_->debate_critical();
#endif
        step_++;
        break;
    case 1:
        calc_win_type();
        step_++;
        break;
    case 2:
        next_phase_ = DebatePhase_Closing;
        break;
    }
    return true;
}

/// <summary>520060</summary>
bool Debate::closing_phase(int delta)
{
    switch (step_)
    {
    case 0:
        if (view_)
            engine_->debate_closing(this);
#if s11_removed
#endif
        step_++;
        break;
    case 1:
#if s11_removed
#endif
        step_++;
        break;
    case 2:
#if s11_removed
#endif
        step_++;
        break;
    case 3:
        param_set_winner(param_, winner_, win_type_);
        return false;
    }
    return true;
}

/// <summary>520620</summary>
bool Debate::anger_phase(int delta)
{
    switch (step_)
    {
    case 0:
        calc_angering(true);
        step_++;
        break;
    case 1:
        anger();
        step_++;
        break;
    case 2:
        if (combo_)
        {
            combo_attack();
            break;
        }
        step_++;
        break;
    case 3:
#if s11_removed
#endif
        if (calc_winner())
        {
            next_phase_ = can_critical() ? DebatePhase_Critical : DebatePhase_Closing;
            break;
        }
        step_++;
        break;
    case 4:
        calc_angering(false);
        step_++;
        break;
    case 5:
        anger();
        step_++;
        break;
    case 6:
        if (combo_)
        {
            combo_attack();
            break;
        }
        step_++;
        break;
    case 7:
        if (calc_crumbled())
        {
#if s11_removed
#endif
        }
        step_++;
        break;
    case 8:
#if s11_removed
#endif
        if (calc_winner())
        {
            next_phase_ = can_critical() ? DebatePhase_Critical : DebatePhase_Closing;
            break;
        }
        next_phase_ = DebatePhase_TurnEnd;
        break;
    }
    return true;
}

/// <summary>520820</summary>
bool Debate::damage_phase(int delta)
{
    switch (step_)
    {
    case 0:
#if s11_removed
#endif
        step_++;
        break;
    case 1:
        attack();
        step_++;
        break;
    case 2:
        effect();
        step_++;
        break;
    case 3:
#if s11_log
        if (Logger* logger = system_->get_logger())
        {
            logger->debug(std::format("damage_phase {} {} {} : {} {}",
                system_->get_wadai_name(wadai_),
                chara_[0].hp,
                chara_[0].stress,
                chara_[1].hp,
                chara_[1].stress
            ));
        }
#endif
#if s11_removed
#endif
        if (calc_winner())
        {
            next_phase_ = can_critical() ? DebatePhase_Critical : DebatePhase_Closing;
            break;
        }
        next_phase_ = DebatePhase_Anger;
        break;
    }
    return true;
}

s11_end_namespace