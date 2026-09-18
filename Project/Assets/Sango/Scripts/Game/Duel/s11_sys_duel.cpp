#include "pch.h"

s11_begin_namespace

namespace {

struct Stance
{
    int speed; // 0 속도
    int hit; // 4 적중
    int attack; // 8 공격력
    int block; // c 방어
    int _10;
    int _14;
    int attack_sub; // 18 공격력
    int spirit_gain; // 1c 피격 시 투지 회복
};

// 837398
constexpr std::array<int, DuelSpecial_Max> SpecialSpiritCost = {
    100, 100, 100, 100, 200, 300, 0, 0,
};

// 8b1750
constexpr std::array<Stance, DuelStance_Max> StanceCoef = { {
    { 42,  71, 16, 12,  5, 25, 3,  4},
    { 10, 200, 14, 90, 32,  5, 2,  8},
    { 15, 125, 14, 55, 12, 12, 3, 10},
    {  5,  71, 16, 22, 12, 12, 3,  7},
} };

} // namespace

Duel::Duel(System* system_, Param* param) : system_(system_), engine_(system_->get_engine()), param_(param)
{
}

/// <summary>4d3940</summary>
void Duel::result_injury_report(Unit* unit, Person* person, bool last)
{
    if (not utils::is_alive(unit))
        return;
    if (not utils::is_alive(person))
        return;
    if (last)
        system_->ping(unit, PingType_Repeat, 0x80808080);
    Message msg;
    msg.set_obj0_str0(LD_WAR_IKKI_INJURY, person, system_->get_shoubyou_name(person->get_shoubyou()));
    system_->history_log(&msg, unit, true, person->get_color());
    if (person->is_player())
    {
        msg.set_obj0(N_WAR_IKKI_INJURY, person);
        system_->message(system_->get_msg(&msg), nullptr, {}, true);
    }
}

/// <summary>4d3a80</summary>
void Duel::result_injury()
{
    std::array<int, MaxTeamCount> injured = {};
    for (int i = 0; i < MaxTeamCount; i++)
    {
        for (int j = 0; j < MaxTeamCharaCount; j++)
        {
            Person* person = param_get_person(param_, i, j);
            if (not utils::is_alive(person))
                continue;
            shoubyou_t shoubyou = param_get_shoubyou(param_, i, j);
            if (person->get_shoubyou() < shoubyou)
                injured[i]++;
            system_->person_set_shoubyou(person, shoubyou);
            int hp = param_get_hp(param_, i, j);
            hp = std::max(hp, 1);
            system_->person_add_hp(person, hp - person->get_hp());
        }
    }
    for (int i = 0; i < MaxTeamCount; i++)
    {
        for (int j = 0; j < MaxTeamCharaCount; j++)
        {
            if (not injured[i])
                continue;
            injured[i]--;
            Unit* unit = param_get_unit(param_, i, j);
            Person* person = param_get_person(param_, i, j);
            if (not utils::is_alive(unit))
                continue;
            if (not utils::is_alive(person))
                continue;
            result_injury_report(unit, person, injured[i] == 0);
        }
    }
}

/// <summary>4d3bd0</summary>
void Duel::result_draw()
{
    Person* challenger = param_get_challenger(param_);
    Person* challenged = param_get_challenged(param_);
    if (not utils::is_alive(challenger))
        return;
    if (not utils::is_alive(challenged))
        return;
    if (param_is_manual(param_))
    {
        Message msg;
        msg.set_obj0_obj1(F_WAR_IKKI_HIKIWAKE_A, challenger, challenged);
        system_->message(system_->get_msg(&msg), challenger, {}, true);
    }
    result_injury();
    system_->person_add_exp(challenger, PersonStatType_Strength, -1, 3);
    system_->person_add_exp(challenged, PersonStatType_Strength, -1, 3);
    system_->person_add_kouseki(challenger, 50);
    system_->person_add_kouseki(challenged, 50);
}

/// <summary>4d3cb0</summary>
void Duel::result_normal()
{
    Person* challenger = param_get_challenger(param_);
    Person* challenged = param_get_challenged(param_);
    if (not utils::is_alive(challenger))
        return;
    if (not utils::is_alive(challenged))
        return;
    if (not utils::in_range(param_->winner_team, 0, MaxTeamCount - 1))
        return;
    if (not utils::in_range(param_->winner_chara, 0, MaxTeamCharaCount - 1))
        return;
    if (not utils::in_range(param_->loser_team, 0, MaxTeamCount - 1))
        return;
    if (not utils::in_range(param_->loser_chara, 0, MaxTeamCharaCount - 1))
        return;
    Person* winner_person = param_get_winner_person(param_);
    Person* loser_person = param_get_loser_person(param_);
    Unit* winner_unit = param_get_winner_unit(param_);
    Unit* loser_unit = param_get_loser_unit(param_);
    Unit* challenger_unit = param_->unit[DuelTeam_Challenger];
    Unit* challenged_unit = param_->unit[DuelTeam_Challenged];
    if (not utils::is_alive(winner_person))
        return;
    if (not utils::is_alive(loser_person))
        return;
    if (not utils::is_alive(winner_unit))
        return;
    if (not utils::is_alive(loser_unit))
        return;
    if (not utils::is_alive(challenger_unit))
        return;
    if (not utils::is_alive(challenged_unit))
        return;
    bool player_controlled = winner_unit->is_player_controlled() or loser_unit->is_player_controlled();
    Force* winner_force = system_->get_force(winner_person->get_force_id());
    Force* loser_force = system_->get_force(loser_person->get_force_id());
    Force* challenger_force = system_->get_force(challenger->get_force_id());
    if (not utils::is_alive(winner_force))
        return;
    if (not utils::is_alive(loser_force))
        return;
    if (not utils::is_alive(challenger_force))
        return;
    system_->get_events()->on_duel_finished();
    duel_chara_result_t loser_result = param_get_chara_result(param_, param_->loser_team, param_->loser_chara);
    // 한쪽이 일반 세력이 아닐경우 포박이나 사망 없음
    if (not winner_force->is_normal() or not loser_force->is_normal())
        loser_result = DuelCharaResult_Escaped;
    Message msg;
    bool captured = false;
    bool dead = false;
    switch (loser_result)
    {
    case DuelCharaResult_Escaped:
        if (player_controlled)
        {
            msg.set_obj0_obj1(F_WAR_IKKI_ATO_A, winner_person, loser_person);
            system_->message(system_->get_msg(&msg), winner_person, {}, true);
            msg.set_obj0_obj1(F_WAR_IKKI_ATO_B, loser_person, winner_person);
            system_->message(system_->get_msg(&msg), loser_person, {}, true);
        }
        break;
    case DuelCharaResult_Captured:
        if (player_controlled)
        {
            msg.set_obj0_obj1(F_WAR_IKKI_HORYO_A, winner_person, loser_person);
            system_->message(system_->get_msg(&msg), winner_person, {}, true);
            msg.set_obj0_obj1(F_WAR_IKKI_HORYO_B, loser_person, winner_person);
            system_->message(system_->get_msg(&msg), loser_person, {}, true);
        }
        captured = true;
        break;
    case DuelCharaResult_Dead:
        if (player_controlled)
        {
            msg.set_obj0_obj1(F_WAR_IKKI_SHIBOU, winner_person, loser_person);
            system_->message(system_->get_msg(&msg), winner_person, {}, true);
        }
        dead = true;
        break;
    }
    if (winner_force->is_player())
    {
        msg.set_obj0_obj1(LB_WAR_IKKI_WIN, winner_person, loser_person);
        system_->history_log(&msg, winner_unit, true, winner_unit->get_color());
    }
    else if (loser_force->is_player())
    {
        msg.set_obj0_obj1(LD_WAR_IKKI_LOST, loser_person, winner_person);
        system_->history_log(&msg, loser_unit, true, loser_unit->get_color());
    }
    system_->ping(challenger_unit->get_pos(), 0, 0x80808080);
    result_injury();
    if (captured)
    {
        District* district = system_->get_district(loser_person->get_district_id());
        std::vector<Person*> all, captured;
        all.push_back(loser_person);
        captured.push_back(loser_person);
        system_->horyo_shoguu(all, captured, loser_unit, winner_unit);
        if (loser_unit->has_member(loser_person->get_id()))
            system_->person_detach(loser_person, winner_person, winner_unit, loser_unit);
        if (utils::is_alive(district))
            system_->district_appoint_totoku(district, winner_force);
        if (not utils::is_alive(loser_unit) and utils::is_alive(loser_force) and winner_force->is_normal())
        {
            if (loser_force->get_like(winner_force->get_id()) > 15)
                system_->force_set_like(loser_force->get_id(), winner_force->get_id(), 15);
            else
                system_->force_add_like(loser_force->get_id(), winner_force->get_id(), -1);
        }
    }
    else if (dead)
    {
        system_->person_die(loser_person, winner_person, nullptr, nullptr, DeathType_Natural, false);
    }
    int troops_damage = 0;
    if (utils::is_alive(loser_unit))
        troops_damage = std::min(loser_unit->get_troops() * 30 / 100, 2000 + system_->rand_int(50));
    int energy_change = winner_unit->add_energy(15);
    system_->floating_damage(energy_change, FloatingCounterType_Energy, winner_unit);
    if (utils::is_alive(loser_unit))
    {
        energy_change = loser_unit->add_energy(-15);
        system_->floating_damage(energy_change, FloatingCounterType_Energy, loser_unit);
        int troops_chnage = system_->unit_add_troops(loser_unit, -troops_damage);
        loser_unit->sync_equipment_quantity();
        system_->floating_damage(troops_chnage, FloatingCounterType_Troops, loser_unit);
    }
    system_->person_add_exp(winner_person, PersonStatType_Strength, -1, 10);
    if (utils::is_alive(loser_person))
        system_->person_add_exp(loser_person, PersonStatType_Strength, -1, 1);
    system_->person_add_kouseki(winner_person, loser_result == DuelCharaResult_Captured ? 200 : 100);
    if (utils::is_alive(loser_person))
        system_->person_add_kouseki(loser_person, 10);
    if (utils::is_alive(winner_force))
        system_->force_add_tech_point(winner_force, 50, nullptr);
}

/// <summary>4d43c0</summary>
void Duel::result_handler()
{
#if s11_removed
    engine_->update(1);
#endif
    if (utils::in_range(param_->winner_team, 0, MaxTeamCount - 1) and utils::in_range(param_->winner_chara, 0, MaxTeamCharaCount - 1))
        result_normal();
    else
        result_draw();
}

/// <summary>4fc940</summary>
bool Duel::is_valid(const Action* action) const
{
    if (not utils::in_range(action->team, 0, MaxTeamCount - 1))
        return false;
    if (not utils::in_range(action->chara, 0, MaxTeamCharaCount - 1))
        return false;
    if (not utils::in_range(action->type, 0, DuelAction_Max - 1))
        return false;
    if (not utils::in_range(action->result, 0, DuelActionResult_Max - 1))
        return false;
    return true;
}

/// <summary>4fc980</summary>
bool Duel::is_valid(const SpecialAction* special) const
{
    if (not utils::in_range(special->team, 0, MaxTeamCount - 1))
        return false;
    if (not utils::in_range(special->chara, 0, MaxTeamCharaCount - 1))
        return false;
    if (not utils::in_range(special->type, 0, DuelSpecial_Max - 1))
        return false;
    if (not utils::in_range(special->result, 0, DuelSpecialResult_Max - 1))
        return false;
    return true;
}

/// <summary>4560a0, v+14</summary>
bool Duel::is_button_enabled(duel_button_t button) const
{
    return true;
}

/// <summary>5067b0, v+8</summary>
void Duel::exit()
{
}

/// <summary>5067c0</summary>
bool Duel::is_player(duel_team_t team) const
{
    return utils::in_range(team_[team].player_id, 0, Player_Max - 1);
}

/// <summary>5067f0</summary>
bool Duel::is_manual(duel_team_t team) const
{
    return team_[team].control == DuelControl_Manual;
}

/// <summary>506820</summary>
int Duel::get_current_chara(duel_team_t team) const
{
    return team_[team].current_chara;
}

/// <summary>506850</summary>
duel_team_t Duel::get_opponent_team(duel_team_t team) const
{
    if (utils::in_range(team, 0, MaxTeamCount - 1))
        return team == DuelTeam_Challenger ? DuelTeam_Challenged : DuelTeam_Challenger;
    return -1;
}

/// <summary>506870</summary>
Person* Duel::get_person(duel_team_t team, int chara) const
{
    return team_get_person(&team_[team], chara);
}

/// <summary>5068b0</summary>
Person* Duel::get_current_person(duel_team_t team) const
{
    if (not utils::in_range(team_[team].current_chara, 0, MaxTeamCharaCount - 1))
        return nullptr;
    return team_get_person(&team_[team], team_[team].current_chara);
}

/// <summary>5068f0</summary>
duel_stance_t Duel::get_stance(duel_team_t team, int chara) const
{
    return team_get_stance(&team_[team], chara);
}

/// <summary>506930</summary>
int Duel::get_hp(duel_team_t team, int chara) const
{
    return team_get_hp(&team_[team], chara);
}

/// <summary>506970</summary>
void Duel::set_hp(duel_team_t team, int chara, int value)
{
    team_set_hp(&team_[team], chara, value);
}

/// <summary>5069b0</summary>
int Duel::add_hp(duel_team_t team, int chara, int value)
{
    if (value < 0)
    {
        if (team == (reverse_ ? DuelTeam_Challenged : DuelTeam_Challenger))
            utils::set_bits(flags_, DuelStatus_ChallengerHPDamaged);
        else
            utils::set_bits(flags_, DuelStatus_ChallengedHPDamaged);
    }
    return team_add_hp(&team_[team], chara, value);
}

/// <summary>506a20</summary>
int Duel::get_current_hp(duel_team_t team) const
{
    if (not utils::in_range(team_[team].current_chara, 0, MaxTeamCharaCount - 1))
        return 0;
    return team_get_hp(&team_[team], team_[team].current_chara);
}

/// <summary>506a60</summary>
int Duel::get_strength(duel_team_t team, int chara, bool revised) const
{
    return team_get_strength(&team_[team], chara, revised);
}

/// <summary>506aa0</summary>
int Duel::get_current_strength(duel_team_t team, bool revised) const
{
    if (not utils::in_range(team_[team].current_chara, 0, MaxTeamCharaCount - 1))
        return Person::MinStat;
    return team_get_strength(&team_[team], team_[team].current_chara, revised);
}

/// <summary>506ae0</summary>
int Duel::get_spirit(duel_team_t team, int chara) const
{
    return team_get_spirit(&team_[team], chara);
}

/// <summary>506b20</summary>
void Duel::set_spirit(duel_team_t team, int chara, int value)
{
    team_set_spirit(&team_[team], chara, value);
}

/// <summary>506b60</summary>
int Duel::add_spirit(duel_team_t team, int chara, int value)
{
    return team_add_spirit(&team_[team], chara, value);
}

/// <summary>506ba0</summary>
int Duel::get_current_spirit(duel_team_t team, int chara) const
{
    if (not utils::in_range(team_[team].current_chara, 0, MaxTeamCharaCount - 1))
        return 0;
    return team_get_spirit(&team_[team], team_[team].current_chara);
}

/// <summary>506be0</summary>
int Duel::get_switching_timer(duel_team_t team) const
{
    return team_[team].switching_timer;
}

/// <summary>506c10</summary>
bool Duel::is_injured(duel_team_t team, int chara) const
{
    return team_get_shoubyou(&team_[team], chara) != Shoubyou_Kenkou;
}

/// <summary>506c50</summary>
bool Duel::has_buff(duel_team_t team, duel_buff_type_t type) const
{
    return team_has_buff(&team_[team], type);
}

/// <summary>506c90</summary>
bool Duel::has_item(duel_team_t team, int chara, bitset4<DuelItemType_Max> flags) const
{
    return team_has_item(&team_[team], chara, flags);
}

/// <summary>506cd0</summary>
bool Duel::is_invulnerable(duel_team_t team, int chara) const
{
    return team_[team].invulnerable_timer > 0;
}

/// <summary>506d10</summary>
int Duel::get_stance_timer(duel_team_t team) const
{
    return team_[team].stance_timer;
}

/// <summary>506d40</summary>
void Duel::set_ui(void*)
{
#if s11_removed
#endif
}

/// <summary>506d90</summary>
bool Duel::check_state(duel_team_t team, int chara, duel_chara_state_t state) const
{
    bool active = team_is_active(&team_[team], chara);
    if (active and utils::in_range(state, 0, DuelCharaState_Max - 1))
        return team_get_state(&team_[team], chara) == state;
    return active;
}

/// <summary>506e00</summary>
bool Duel::is_joined(duel_team_t team, int chara) const
{
    for (int i = 0; i < DuelCharaState_Max; i++)
    {
        if (i == DuelCharaState_NotJoined)
            continue;
        if (check_state(team, chara, i))
            return true;
    }
    return false;
}

/// <summary>506e70</summary>
bool Duel::is_manual() const
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (team_[i].control == DuelControl_Manual)
            return true;
    }
    return false;
}

/// <summary>506ec0</summary>
bool Duel::can_special(duel_team_t team, int chara) const
{
    for (int i = 0; i < DuelSpecial_Max; i++)
    {
        if (is_special_enabled(team, chara, i))
            return true;
    }
    return false;
}

/// <summary>506f20</summary>
bool Duel::is_special_available(duel_team_t team, int chara, duel_sp_t special) const
{
    return team_get_special_remaining_count(&team_[team], chara, special);
}

/// <summary>506f70</summary>
int Duel::get_special_spirit_cost(duel_sp_t special) const
{
    return SpecialSpiritCost[special];
}

/// <summary>5070f0</summary>
void Duel::set_stance(duel_team_t team, int chara, duel_stance_t stance)
{
    if (team_is_active(&team_[team], chara) and get_stance(team, chara) != stance)
        team_[team].stance_timer = 0;
    team_set_stance(&team_[team], chara, stance);
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("Duel::set_stance {}-{} {} 0x{:x}",
            team,
            chara,
            stance,
            system_->get_seed()
        ));
    }
#endif
}

/// <summary>507170</summary>
int Duel::create_cooldown_timer(duel_team_t team, int chara) const
{
    return 4 + system_->rand_int(3);
}

/// <summary>5071b0</summary>
int Duel::get_special_remaining_count(duel_team_t team, int chara, duel_sp_t special) const
{
    return team_get_special_remaining_count(&team_[team], chara, special);
}

/// <summary>507200</summary>
void Duel::set_special_remaining_count(duel_team_t team, int chara, duel_sp_t special, int value)
{
    team_set_special_remaining_count(&team_[team], chara, special, value);
}

/// <summary>507250</summary>
int Duel::get_action_ratio(duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const
{
    if (a_team == b_team)
        return 0;
    if (a_team == DuelTeam_Challenger)
        return action_ratio_[a_chara][b_chara];
    else
        return 100 - action_ratio_[b_chara][a_chara];
}

/// <summary>5072d0</summary>
int Duel::get_special_hit_count(const SpecialAction* special) const
{
    switch (special->type)
    {
    case DuelSpecial_Hissatsuwaza:
    // case DuelSpecial_Kiai:
    // case DuelSpecial_Kenshu:
    // case DuelSpecial_Taikyaku:
    case DuelSpecial_Kyuusho:
    case DuelSpecial_Musou:
    case DuelSpecial_Anki:
    case DuelSpecial_Nisetaikyaku:
        return 1;
    }
    return 0;
}

/// <summary>507320</summary>
bool Duel::calc_retreat_chance(duel_team_t team, int chara) const
{
    Person* person = get_person(team, chara);
    if (utils::is_active(person) and person->has_skill(SkillId_Kyouun))
        return true;
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = team_[opponent_team].current_chara;
    if (utils::in_range(opponent_chara, 0, MaxTeamCharaCount - 1))
    {
        bool horse = team_has_item(&team_[team], chara, { DuelItemType_EliteHorse });
        bool opponent_horse = team_has_item(&team_[opponent_team], opponent_chara, { DuelItemType_EliteHorse });
        if (horse)
        {
            if (not opponent_horse)
                return true;
        }
        else
        {
            if (opponent_horse)
                return false;
        }
    }
    if (blow_counter_ > 10)
    {
        int n = blow_counter_; // 10 ..
        n += get_hp(team, chara) / 2; // 0 .. 50
        n += get_strength(team, chara, true); // 1 .. 110
        return n > system_->rand_int(100);
    }
    else
    {
        int n = 0;
        n += get_hp(team, chara) / 2; // 0 .. 50
        n += get_strength(team, chara, true); // 1 .. 110
        return n > system_->rand_int(250);
    }
}

/// <summary>5074b0</summary>
bool Duel::calc_critical_chance(duel_team_t team, int chara) const
{
    int chance = 0;
    duel_stance_t stance = get_stance(team, chara);
    int t = get_stance_timer(team);
    switch (stance)
    {
    case DuelStance_Attack:
        chance = 2 * t + system_->rand_bool(5);
        break;
    case DuelStance_Defense:
        chance = 5 + 4 * t + system_->rand_bool(5);
        break;
    case DuelStance_Spirit:
        chance = 5 + 3 * t + system_->rand_bool(5);
        break;
    case DuelStance_Fury:
        chance = 100;
        break;
    default:
        return 0;
    }
    Person* person = get_person(team, chara);
    if (utils::is_active(person))
    {
        int age;
        switch (person->get_id())
        {
        case PersonId_Chouhi:
            chance += 12;
            break;
        case PersonId_Ryofu:
        case PersonId_Kanu:
            chance += 3;
            break;
        case PersonId_Kyocho:
        case PersonId_Chouun:
        case PersonId_Bachou:
            chance += 2;
            break;
        case PersonId_Kouchuu_Kanshou:
            if (system_->get_life_mode() == LifeMode_Virtual)
            {
                chance += 3;
                break;
            }
            age = person->get_age();
            if (age < 60)
                chance += 2;
            else if (age < 65)
                chance += 3;
            else if (age < 70)
                chance += 4;
            else
                chance += 5;
            break;
        }
    }
    if (has_item(team, chara, { DuelItemType_SerpentBlade }))
        chance += 3;
    if (has_buff(team, DuelBuffType_CriticalChance))
        chance += 5;
    if (t <= 5 and stance != DuelStance_Fury)
        chance = 0;
    return system_->rand_bool(chance);
}

/// <summary>507660</summary>
int Duel::create_invulnerable_timer(duel_team_t team, int chara) const
{
    return 5 + system_->rand_int(5);
}

/// <summary>5076a0</summary>
void Duel::reset_buff_timer(duel_team_t team, int chara, duel_buff_type_t buff)
{
    if (view_)
        engine_->duel_reset_buff(this, team, buff);
    team_set_buff_timer(&team_[team], buff, 0);
}

/// <summary>507700</summary>
bool Duel::is_family(duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const
{
    if (not utils::in_range(a_team, 0, MaxTeamCount - 1))
        return false;
    if (not utils::in_range(b_team, 0, MaxTeamCount - 1))
        return false;
    if (not utils::in_range(a_chara, 0, MaxTeamCharaCount - 1))
        return false;
    if (not utils::in_range(b_chara, 0, MaxTeamCharaCount - 1))
        return false;
    Person* src = get_person(a_team, a_chara);
    Person* target = get_person(b_team, b_chara);
    if (not utils::is_active(src))
        return false;
    if (not utils::is_active(target))
        return false;
    return src->is_family(target->get_id()) or src->spouse_is(target->get_id()) or src->is_gikyoudai(target->get_id());
}

/// <summary>5078e0</summary>
bool Duel::blow_anim(int count)
{
    if (not utils::in_range(count, 0, MaxAnimQueueSize - 1))
        return false;
    if (view_)
    {
        engine_->duel_blow_anim(this, blow_anim_queue_.data(), count);
    }
    else
    {
        for (int i = 0; i < count; i++)
        {
            BlowAnim* anim = &blow_anim_queue_[i];
            if (anim->value < 0)
                continue;
            add_blow_counter(anim->value);
            // calc_result 함수에서 사용될 수 있기 때문에 초기화
            *anim = BlowAnim();
        }
    }
    return true;
}

/// <summary>5079f0</summary>
bool Duel::hp_anim(int count)
{
    if (not utils::in_range(count, 0, MaxAnimQueueSize - 1))
        return false;
    if (view_)
    {
        engine_->duel_hp_anim(this, hp_anim_queue_.data(), count);
    }
    else
    {
        for (int i = 0; i < count; i++)
        {
            HPAnim* anim = &hp_anim_queue_[i];
            if (not utils::in_range(anim->def_team, 0, MaxTeamCount - 1))
                continue;
            add_hp(anim->def_team, anim->def_chara, -anim->damage);
            add_shoubyou(anim->def_team, anim->def_chara, anim->shoubyou_damage);
            if (anim->shoubyou_damage > 0)
                calc_action_ratio();
            // calc_result 함수에서 사용될 수 있기 때문에 초기화
            *anim = HPAnim();
        }
    }
    return true;
}

/// <summary>507b10</summary>
bool Duel::spirit_anim(int count)
{
    if (not utils::in_range(count, 0, MaxAnimQueueSize - 1))
        return false;
    if (view_)
    {
        engine_->duel_spirit_anim(this, spirit_anim_queue_.data(), count);
    }
    else
    {
        for (int i = 0; i < count; i++)
        {
            SpiritAnim* anim = &spirit_anim_queue_[i];
            if (not utils::in_range(anim->atk_team, 0, MaxTeamCount - 1))
                continue;
            add_spirit(anim->atk_team, anim->atk_chara, anim->atk_value);
            add_spirit(anim->def_team, anim->def_chara, anim->def_value);
            // calc_result 함수에서 사용될 수 있기 때문에 초기화
            *anim = SpiritAnim();
        }
    }
    return true;
}

/// <summary>507c30</summary>
void Duel::update_param_result()
{
    if (utils::in_range(winner_team_, 0, MaxTeamCount - 1) and utils::in_range(loser_team_, 0, MaxTeamCount - 1))
    {
        param_->winner_team = reverse_ ? get_opponent_team(winner_team_) : winner_team_;
        param_->loser_team = reverse_ ? get_opponent_team(loser_team_) : loser_team_;
        param_->winner_chara = get_current_chara(param_->winner_team);
        param_->loser_chara = get_current_chara(param_->loser_team);
    }
    param_->flags = flags_;
    for (int i = 0; i < MaxTeamCount; i++)
    {
        duel_team_t team = reverse_ ? get_opponent_team(i) : i;
        for (int j = 0; j < MaxTeamCharaCount; j++)
        {
            param_->hp[team][j] = team_get_hp(&team_[team], j);
            param_->shoubyou[team][j] = team_get_shoubyou(&team_[team], j);
        }
    }
    param_->end_blow_counter = blow_counter_;
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("Duel::result {} {} {}-{} {} 0x{:x} 0x{:x}",
            ftk_type_,
            ftk_team_,
            param_->winner_team,
            param_->winner_chara,
            param_->end_blow_counter,
            param_->flags,
            system_->get_seed()
        ));
        for (int i = 0; i < DuelTeam_Max; i++)
        {
            for (int j = 0; j < team_[i].chara_count; j++)
            {
                logger->debug(std::format("{}-{} {} {} {} {}",
                    i,
                    j,
                    param_->result[i][j],
                    param_->hp[i][j],
                    param_->spirit[i][j],
                    param_->shoubyou[i][j]
                ));
            }
        }
    }
#endif
}

/// <summary>507e10</summary>
void Duel::update_stance(bool player)
{
    if (player)
    {
        if (not view_)
            return;
        for (int i = 0; i < MaxTeamCount; i++)
        {
            if (team_[i].control != DuelControl_Manual)
                continue;
            duel_stance_t stance = engine_->duel_get_stance(this, i);
            if (not utils::in_range(stance, 0, DuelStance_Max - 1))
                continue;
            set_stance(i, team_[i].current_chara, stance);
        }
    }
    else
    {
        for (int i = 0; i < MaxTeamCount; i++)
        {
            if (team_[i].control == DuelControl_Manual and utils::in_range(team_[i].current_chara, 0, MaxTeamCharaCount - 1) and utils::in_range(team_get_stance(&team_[i], team_[i].current_chara), 0, DuelStance_Max - 1))
                continue;
            duel_stance_t stance = ai_calc_stance(&ai_[i]);
            if (not utils::in_range(stance, 0, DuelStance_Max - 1))
                continue;
            set_stance(i, team_[i].current_chara, stance);
        }
    }
}

/// <summary>507f10</summary>
bool Duel::update_special_action()
{
    if (not utils::in_range(special_try_team_, 0, MaxTeamCount - 1))
        return false;
    special_action_.team = special_try_team_;
    special_action_.chara = get_current_chara(special_try_team_);
    if (is_manual(special_try_team_))
    {
        if (not view_)
            return false;
        duel_sp_t sp = engine_->duel_get_special(this, special_try_team_);
        if (not utils::in_range(sp, 0, DuelSpecial_Max - 1))
            return false;
        special_action_.type = sp;
    }
    else
    {
        special_action_.type = ai_calc_special(&ai_[special_try_team_]);
    }
    return true;
}

/// <summary>507fd0</summary>
void Duel::update_switching()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (team_[i].control == DuelControl_Manual)
        {
            if (view_)
                switching_chara_[i] = engine_->duel_get_switching_chara(this, i);
        }
        else
        {
            switching_chara_[i] = ai_calc_switch(&ai_[i]);
        }
    }
}

/// <summary>508070</summary>
bool Duel::update_retreat_result()
{
    duel_team_t opponent_team = get_opponent_team(special_action_.team);
    if (not is_valid(&special_action_))
        return false;
    winner_team_ = opponent_team;
    loser_team_ = special_action_.team;
    return true;
}

/// <summary>5080f0</summary>
void Duel::update_timer()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        team_[i].stance_timer++;
        if (team_[i].appearing_timer > 0)
            team_[i].appearing_timer--;
        if (team_[i].switching_timer > 0)
            team_[i].switching_timer--;
        if (team_[i].invulnerable_timer > 0)
        {
            team_[i].invulnerable_timer--;
            if (not team_[i].invulnerable_timer and view_)
                engine_->duel_reset_invulnerable(this, i);
            team_[i].stance_timer = 0;
        }
        for (int j = 0; j < DuelBuffType_Max; j++)
        {
            int timer = team_get_buff_timer(&team_[i], j);
            if (timer > 0)
            {
                timer--;
                team_set_buff_timer(&team_[i], j, timer);
            }
            if (not timer and view_)
                engine_->duel_reset_buff(this, i, j);
        }
    }
}

/// <summary>508200</summary>
void Duel::reset_action()
{
    for (int i = 0; i < MaxAnimQueueSize; i++)
    {
        blow_anim_queue_[i] = BlowAnim();
        hp_anim_queue_[i] = HPAnim();
        spirit_anim_queue_[i] = SpiritAnim();
        action_queue_[i] = Action();
    }
    special_action_ = SpecialAction();
    special_try_team_ = -1;
    if (view_)
        engine_->duel_reset_anim(this);
}

/// <summary>5082e0</summary>
s11_static int Duel::get_duel_min_hp(Person* self)
{
    if (not utils::is_active(self))
        return 50;
    switch (self->get_seikaku())
    {
    case Seikaku_Shoushin:
        return 80;
    case Seikaku_Reisei:
        return 70;
    case Seikaku_Goutan:
        return 60;
    }
    return 50;
}

/// <summary>508330</summary>
int Duel::get_chara_count(duel_team_t team, bool joined) const
{
    int n = 0;
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (joined)
        {
            if (is_joined(team, i))
                n++;
        }
        else
        {
            if (team_is_active(&team_[team], i))
                n++;
        }
    }
    return n;
}

/// <summary>5083a0</summary>
int Duel::add_blow_counter(int value)
{
    int n = blow_counter_;
    blow_counter_ += value;
    if (blow_counter_ < 0)
        blow_counter_ = 0;
    if (blow_counter_ != n)
    {
        if (view_)
            engine_->duel_update_blow_counter(this);
    }
    return blow_counter_ - n;
}

/// <summary>5083e0</summary>
person_id_t Duel::get_person_id(duel_team_t team, int chara) const
{
    Person* person = team_get_person(&team_[team], chara);
    if (utils::is_active(person))
        return person->get_id();
    return -1;
}

/// <summary>508440</summary>
int Duel::get_chara(duel_team_t team, int number) const
{
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (team_get_number(&team_[team], i) == number)
            return i;
    }
    return -1;
}

/// <summary>5084b0</summary>
void Duel::change_shoubyou(duel_team_t team, int chara, shoubyou_t shoubyou)
{
    shoubyou_t old_shoubyou = team_get_shoubyou(&team_[team], chara);
    if (not utils::in_range(old_shoubyou, 0, Shoubyou_Max - 2))
        return;
    shoubyou = std::clamp<shoubyou_t>(shoubyou, 0, Shoubyou_Max - 2);
    team_set_shoubyou(&team_[team], chara, shoubyou);
    if (old_shoubyou < shoubyou)
    {
        if (team == (reverse_ ? DuelTeam_Challenged : DuelTeam_Challenger))
            utils::set_bits(flags_, DuelStatus_ChallengerShoubyouDamaged);
        else
            utils::set_bits(flags_, DuelStatus_ChallengedShoubyouDamaged);
    }
}

/// <summary>508560</summary>
shoubyou_t Duel::add_shoubyou(duel_team_t team, int chara, int value)
{
    shoubyou_t shoubyou = team_get_shoubyou(&team_[team], chara);
    if (not utils::in_range(shoubyou, 0, Shoubyou_Max - 2))
        return -1;
    shoubyou = std::clamp<shoubyou_t>(shoubyou + value, 0, Shoubyou_Max - 2);
    change_shoubyou(team, chara, shoubyou);
    return shoubyou;
}

/// <summary>5085f0, v+c</summary>
bool Duel::is_special_enabled(duel_team_t team, int chara, duel_sp_t special) const
{
    if (system_->is_feat_disabled(Feature_DuelAIRetreat) and special == DuelSpecial_Taikyaku and not is_player(team))
        return false;
    if (not is_special_available(team, chara, special))
        return false;
    if (get_spirit(team, chara) < get_special_spirit_cost(special))
        return false;
    if (special == DuelSpecial_Nisetaikyaku)
        return blow_counter_ >= 15;
    return true;
}

/// <summary>5086c0</summary>
duel_sp_result_t Duel::calc_special_result(const SpecialAction* special) const
{
    if (not utils::in_range(special->type, 0, DuelSpecial_Max - 1))
        return -1;
    switch (special->type)
    {
    case DuelSpecial_Taikyaku:
        if (calc_retreat_chance(special->team, special->chara))
            return DuelSpecialResult_Hit;
        return DuelSpecialResult_Miss;
    }
    return DuelSpecialResult_Hit;
}

/// <summary>508720</summary>
s11_static duel_team_t Duel::calc_duel_ftk_team(Person* a, Person* b, bool a_bow, bool b_bow, shoubyou_t a_shoubyou, shoubyou_t b_shoubyou, int* chance)
{
    Scenario* scenario = a->get_scenario();
    if (scenario->get_game()->is_feat_disabled(Feature_DuelFirstTurnKill))
        return -1;
    if (not utils::is_alive(a))
        return -1;
    if (not utils::is_alive(b))
        return -1;
    int a_str = get_duel_strength(a, a_shoubyou, true);
    int b_str = get_duel_strength(b, b_shoubyou, true);
    Person* atk;
    Person* def;
    int atk_str;
    int def_str;
    int atk_team;
    if (a_str > b_str or (a_str == b_str and scenario->rand_bool(70)))
    {
        atk = a;
        def = b;
        atk_str = a_str;
        def_str = b_str;
        atk_team = DuelTeam_Challenger;
    }
    else
    {
        atk = b;
        def = a;
        atk_str = b_str;
        def_str = a_str;
        atk_team = DuelTeam_Challenged;
    }
    int def_str_rev = std::max(def_str, atk_str / 2);
    int n = (atk_str * atk_str) / (def_str_rev * def_str_rev); // 1 .. 4
    n *= 37 - (74 * def_str_rev * def_str_rev) / (def_str_rev * def_str_rev + atk_str * atk_str); // 0 .. 23
    n = std::clamp(n, 0, 100); // 0 .. 92
#if s11_suspicious(3)
    // atk 기준으로 확인해야 하는것이 아닌지?
#endif
    if (a_bow)
        n += 5;
    if (not atk->is_player() and def->is_player())
    {
        if (utils::is_alive(scenario))
        {
            switch (scenario->get_difficulty())
            {
            case Difficulty_Normal:
                n = n * 4 / 3; // 1.333...
                break;
            case Difficulty_Hard:
                n = n * 3 / 2; // 1.5
                break;
            }
        }
    }
    else if (atk->is_player() and not def->is_player())
    {
        if (utils::is_alive(scenario))
        {
            switch (scenario->get_difficulty())
            {
            case Difficulty_Normal:
                n = n * 4 / 5; // 0.8
                break;
            case Difficulty_Hard:
                n = n / 2; // 0.5
                break;
            }
        }
    }
    int age;
    switch (atk->get_id())
    {
    case PersonId_Ryofu:
        n += 10;
        break;
    case PersonId_Chouhi:
    case PersonId_Kanu:
        n += 5;
        break;
    case PersonId_Kyocho:
    case PersonId_Chouun:
    case PersonId_Bachou:
        n += 3;
        break;
    case PersonId_Kouchuu_Kanshou:
        if (utils::is_alive(scenario) and scenario->get_life_mode() == LifeMode_Virtual)
        {
            n += 5;
            break;
        }
        age = atk->get_age();
        if (age < 60)
            n += 1;
        else if (age < 65)
            n += 2;
        else if (age < 70)
            n += 3;
        else if (age < 80)
            n += 4;
        else if (age < 85)
            n += 5;
        else if (age < 90)
            n += 10;
        else
            n += 15;
        break;
    }
    // 무력이 70 이상일 경우에만 발생
    if (atk->get_stat(PersonStatType_Strength) < 70)
        n = 0;
    // 무력 차이가 5 미만일 경우 활이 있어야만 발생
    if (not a_bow and atk_str - def_str_rev < 5)
        n = 0;
    // 여포, 관우, 장비, 허저, 조운, 마초 상대로 발생하지 않음
    switch (def->get_id())
    {
    case PersonId_Ryofu:
    case PersonId_Kanu:
    case PersonId_Chouhi:
    case PersonId_Kyocho:
    case PersonId_Chouun:
    case PersonId_Bachou:
        n = 0;
        break;
    }
    if (chance)
        *chance = n;
    if (scenario->rand_bool(n))
        return atk_team;
    return -1;
}

/// <summary>508b90</summary>
duel_result_t Duel::calc_result(bool predict, const HPAnim* hp_anim_array)
{
    std::array<int, MaxTeamCount> hp = {};
    // 비동기로 진행될 경우 scene에서 체력이 업데이트됨
    if (predict and hp_anim_array)
    {
        for (int i = 0; i < action_count_; i++)
        {
            if (not utils::in_range(hp_anim_array[i].def_team, 0, MaxTeamCount - 1))
                continue;
            hp[hp_anim_array[i].def_team] += hp_anim_array[i].damage;
        }
    }
    for (int i = 0; i < MaxTeamCount; i++)
        hp[i] = get_current_hp(i) - hp[i];
    if (hp[DuelTeam_Challenger] > 0)
    {
        if (hp[DuelTeam_Challenged] > 0)
        {
            return -1;
        }
        else
        {
            winner_team_ = DuelTeam_Challenger;
            loser_team_ = DuelTeam_Challenged;
            return reverse_ ? DuelResult_ChallengedWin : DuelResult_ChallengerWin;
        }
    }
    else
    {
        if (hp[DuelTeam_Challenged] > 0)
        {
            winner_team_ = DuelTeam_Challenged;
            loser_team_ = DuelTeam_Challenger;
            return reverse_ ? DuelResult_ChallengerWin : DuelResult_ChallengedWin;
        }
        else
        {
            return DuelResult_Draw;
        }
    }
}

/// <summary>508cc0</summary>
int Duel::calc_attack_damage(duel_team_t team, int chara) const
{
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);
    int n = team_get_stance_attack(&team_[team], chara); // 14 .. 16
    n = n * get_action_ratio(team, chara, opponent_team, opponent_chara) / 50; // 0 .. 2
    n = n * team_get_stance_attack_sub(&team_[team], chara) * 7 / 27; // 0.26 * attack_sub
    // = 0 .. 24
    if (n < 3)
        n = 3;
    return n;
}

/// <summary>508da0</summary>
int Duel::calc_special_damage(const SpecialAction* special) const
{
    duel_team_t team = special->team;
    int chara = special->chara;
    duel_team_t opponent_team = get_opponent_team(special->team);
    int opponent_chara = get_current_chara(opponent_team);
    if (not is_valid(special))
        return 0;
    if (not utils::in_range(opponent_team, 0, MaxTeamCount - 1))
        return 0;
    if (not utils::in_range(opponent_chara, 0, MaxTeamCharaCount - 1))
        return 0;

    int n = get_action_ratio(team, chara, opponent_team, opponent_chara); // 1 .. 99
    n = n * 20 / 55; // 0.36

    if (has_buff(team, DuelBuffType_Attack))
        n = n * 5 / 4; // 1.25
    if (has_buff(opponent_team, DuelBuffType_Defense))
        n = n * 3 / 4; // 0.75

    if (has_item(team, chara, { DuelItemType_CrescentHalberd }))
        n = n * 9 / 8; // 1.125
    else if (has_item(team, chara, { DuelItemType_LongSpear }))
        n = n * 10 / 9; // 1.1...

    switch (get_person_id(team, chara))
    {
    case PersonId_Kouchuu_Kanshou:
    case PersonId_Kakouen:
        if (special->type == DuelSpecial_Nisetaikyaku)
            n = n * 11 / 10; // 1.1
        break;
    case PersonId_Ousou:
    case PersonId_Shukuyuu:
        if (special->type == DuelSpecial_Anki)
            n = n * 11 / 10; // 1.1
        break;
    case PersonId_Ryofu:
        n = n * 13 / 11; // 1.18...
        break;
    }

    switch (special->type)
    {
    case DuelSpecial_Hissatsuwaza:
        n = n * 6 / 5; // 1.2
        break;
    case DuelSpecial_Musou:
        n = n * 3;
        break;
    case DuelSpecial_Anki:
        n = n * 6 / 5; // 1.2
        break;
    case DuelSpecial_Nisetaikyaku:
        n = n * 3 / 2; // 1.5
        break;
    case DuelSpecial_Kiai:
    case DuelSpecial_Kenshu:
        n = 0;
        break;
    }

    switch (system_->get_difficulty())
    {
    case Difficulty_Easy:
        if (is_player(team))
        {
            if (not is_player(opponent_team))
                n = n * 11 / 10; // 1.1
        }
        else
        {
            if (is_player(opponent_team))
                n = n * 4 / 5; // 0.8
        }
        break;
    }

    if (get_stance(opponent_team, opponent_chara) == DuelStance_Defense)
        n = n * 3 / 4; // 0.75

    n = std::min(n, 80);
    return n;
}

/// <summary>509120</summary>
bool Duel::can_join(duel_team_t team, int chara) const
{
    int hp = get_hp(team, chara);
    Person* person = get_person(team, chara);
    if (not utils::is_active(person))
        return false;
    if (hp < get_duel_min_hp(person))
        return false;
    return true;
}

/// <summary>509190</summary>
bool Duel::calc_join(duel_team_t team, int chara) const
{
    Person* person = get_person(team, chara);
    Person* cur_person = get_current_person(team);
    Person* opponent_cur_person = get_current_person(get_opponent_team(team));
    if (not utils::is_active(person))
        return false;
    if (not utils::is_active(cur_person))
        return false;
    if (not utils::is_active(opponent_cur_person))
        return false;
    if (person->get_id() < 0)
        return false;
    if (cur_person->get_id() < 0)
        return false;
    if (opponent_cur_person->get_id() < 0)
        return false;

    if (person->is_gikyoudai(cur_person->get_id()) or person->is_fuufu(cur_person->get_id()))
    {
        if (blow_counter_ < 3)
            return false;
        return system_->rand_bool(80);
    }

    if (person->is_hate(opponent_cur_person->get_id()))
    {
        if (blow_counter_ < 4)
            return false;
        return system_->rand_bool(60);
    }

    if (person->is_hate(cur_person->get_id()))
    {
        if (not cur_person->is_kunshu())
            return false;
        force_id_t cur_force_id = cur_person->get_force_id();
        if (cur_force_id >= 0 and person->get_force_id() != cur_force_id)
            return false;
        if (blow_counter_ < 4)
            return false;
        return system_->rand_bool(person->get_loyalty() / 4);
    }

    if (person->is_like(cur_person->get_id()) or person->is_ketsuen(cur_person->get_id()))
    {
        if (blow_counter_ < 6)
            return false;
        return system_->rand_bool(50);
    }

    if (person->get_birthplace_id() == cur_person->get_birthplace_id())
    {
        if (blow_counter_ < 6)
            return false;
        int n = 10 + (75 - person->get_aishou_distance(cur_person->get_id())) / 2; // 10 .. 47
        return system_->rand_bool(n);
    }

    if (blow_counter_ < 6)
        return false;
    int n = (75 - person->get_aishou_distance(cur_person->get_id())) / 2; // 0 .. 37
    if (n < 1)
        n = 1;
    return system_->rand_bool(n);
}

/// <summary>509450</summary>
duel_team_t Duel::calc_actor_team() const
{
    duel_team_t a_team = reverse_ ? DuelTeam_Challenged : DuelTeam_Challenger;
    duel_team_t b_team = reverse_ ? DuelTeam_Challenger : DuelTeam_Challenged;
    int a_speed = team_get_stance_speed(&team_[a_team], team_[a_team].current_chara);
    int b_speed = team_get_stance_speed(&team_[b_team], team_[b_team].current_chara);
    int sum = std::max(b_speed + a_speed, 1);
    if (a_speed < b_speed)
    {
        std::swap(a_team, b_team);
        std::swap(a_speed, b_speed);
    }
    int n = a_speed * 100 / sum;
    n += (get_action_ratio(a_team, team_[a_team].current_chara, b_team, team_[b_team].current_chara) - 50) / 2;
    if (has_item(a_team, team_[a_team].current_chara, { DuelItemType_BlueDragon }))
        n += 2;
    if (has_item(b_team, team_[b_team].current_chara, { DuelItemType_BlueDragon }))
        n -= 2;
    if (get_person_id(a_team, team_[a_team].current_chara) == PersonId_Kanu)
        n += 2;
    if (get_person_id(b_team, team_[b_team].current_chara) == PersonId_Kanu)
        n -= 2;
    n = std::clamp(n, 1, 99);
    if (system_->rand_bool(n))
        return a_team;
    return b_team;
}

/// <summary>509690</summary>
duel_action_t Duel::calc_action(duel_team_t team, int chara)
{
    duel_action_t action = -1;
    if (calc_critical_chance(team, chara))
    {
        switch (get_stance(team, chara))
        {
        case DuelStance_Attack:
            action = DuelAction_AttackCritical;
            break;
        case DuelStance_Defense:
            if (not is_invulnerable(team, chara))
                action = DuelAction_DefenseCritical;
            break;
        case DuelStance_Spirit:
            action = DuelAction_SpiritCritical;
            break;
        case DuelStance_Fury:
            if (not is_invulnerable(team, chara) and system_->rand_bool(10))
            {
                action = DuelAction_DefenseCritical;
                break;
            }
            action = system_->rand_bool(20) ? DuelAction_SpiritCritical : DuelAction_AttackCritical;
            break;
        }
        // 여기서 초기화할 필요가?
        team_[team].stance_timer = 0;
        if (action == DuelAction_DefenseCritical)
            team_[team].invulnerable_timer = create_invulnerable_timer(team, chara);
    }
    if (action < 0)
        return system_->rand_int(DuelAction_AttackMax);
    return action;
}

/// <summary>509800</summary>
duel_team_t Duel::calc_ftk_team() const
{
    Person* a = get_current_person(DuelTeam_Challenger);
    Person* b = get_current_person(DuelTeam_Challenged);
    if (not utils::is_active(a))
        return -1;
    if (not utils::is_active(b))
        return -1;
    bool a_bow = has_item(DuelTeam_Challenger, team_[DuelTeam_Challenger].current_chara, { DuelItemType_Bow });
    bool b_bow = has_item(DuelTeam_Challenged, team_[DuelTeam_Challenged].current_chara, { DuelItemType_Bow });
    shoubyou_t a_shoubyou = team_get_shoubyou(&team_[DuelTeam_Challenger], team_[DuelTeam_Challenger].current_chara);
    shoubyou_t b_shoubyou = team_get_shoubyou(&team_[DuelTeam_Challenged], team_[DuelTeam_Challenged].current_chara);
    return calc_duel_ftk_team(a, b, a_bow, b_bow, a_shoubyou, b_shoubyou, nullptr);
}

/// <summary>509930</summary>
duel_ftk_type_t Duel::calc_ftk_type() const
{
    duel_team_t a_team = ftk_team_;
    if (not utils::in_range(ftk_team_, 0, MaxTeamCount - 1))
        return -1;
    duel_team_t b_team = get_opponent_team(a_team);
    int a_chara = team_[a_team].current_chara;
    int b_chara = team_[b_team].current_chara;
    if (is_family(a_team, a_chara, b_team, b_chara))
        return DuelFtkType_Normal;
    if (type_ == DuelType_Event)
        return DuelFtkType_Normal;
    if (not has_item(a_team, a_chara, { DuelItemType_Bow }))
        return DuelFtkType_Normal;
    if (system_->rand_bool(80))
    {
        if (system_->rand_bool(50))
            return DuelFtkType_BowA;
        return DuelFtkType_BowB;
    }
    return DuelFtkType_Normal;
}

/// <summary>509a30</summary>
bool Duel::calc_kill_chance(duel_team_t team)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return false;
    int chara = get_current_chara(team);
    assert(utils::in_range(chara, 0, MaxTeamCharaCount - 1));
    Person* person = get_person(team, chara);
    if (utils::is_active(person) and person->has_skill(SkillId_Kyouun))
        return false;
    if (special_action_.type == DuelSpecial_Taikyaku)
        return false;
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);
    if (is_family(opponent_team, opponent_chara, team, chara))
        return false;
    int chance = 0;
    switch (system_->get_battle_death_mode())
    {
    case BattleDeathMode_Normal:
        chance = 2;
        break;
    case BattleDeathMode_High:
        chance = 5;
        break;
    }
    return system_->rand_bool(chance);
}

/// <summary>509b40</summary>
bool Duel::calc_capture_chance(duel_team_t team)
{
    if (system_->is_feat_disabled(Feature_Hobaku))
        return false;
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return false;
    int chara = get_current_chara(team);
    assert(utils::in_range(chara, 0, MaxTeamCharaCount - 1));
    Person* person = get_person(team, chara);
    if (utils::is_active(person) and person->has_skill(SkillId_Kyouun))
        return false;
    if (special_action_.type == DuelSpecial_Taikyaku)
        return false;
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);
    if (is_family(opponent_team, opponent_chara, team, chara))
        return false;
    int chance = 80;
    return system_->rand_bool(chance);
}

/// <summary>509c10</summary>
bool Duel::ftk_anim()
{
    duel_team_t opponent_team = get_opponent_team(ftk_team_);
    if (opponent_team >= 0)
    {
        static constexpr std::array<int, DuelFtkType_Max> blow_count = { 1, 1, 1 }; // 8373b8
        blow_anim_queue_[0].value = utils::in_range(ftk_type_, 0, DuelFtkType_Max - 1) ? blow_count[ftk_type_] : 0;
        if (blow_anim(1))
        {
            HPAnim anim;
            anim.damage = MaxHP;
            anim.atk_team = ftk_team_;
            anim.atk_chara = team_[ftk_team_].current_chara;
            anim.def_team = opponent_team;
            anim.def_chara = team_[opponent_team].current_chara;
            hp_anim_queue_[0] = anim;
            if (hp_anim(1))
            {
                utils::set_bits(flags_, DuelStatus_Ftk);
                return true;
            }
        }
    }
    winner_team_ = -1;
    loser_team_ = -1;
    ftk_team_ = -1;
    ftk_type_ = -1;
    return false;
}

/// <summary>509d20</summary>
bool Duel::change_current_chara(duel_team_t team, int chara)
{
    if (not team_change_current_chara(&team_[team], chara))
        return false;
#if s11_removed
    // update ui
#endif
    team_[team].stance_timer = 0;
    team_[team].appearing_timer = create_cooldown_timer(team, chara);
    team_[team].switching_timer = create_cooldown_timer(team, chara);
    team_[team].invulnerable_timer = 0;
    if (view_)
        engine_->duel_change_current_chara(this, team);
    for (int i = 0; i < DuelBuffType_Max; i++)
    {
        team_set_buff_timer(&team_[team], i, 0);
        reset_buff_timer(team, chara, i);
    }
    if (ai_[team].initialized)
        ai_update_chara(&ai_[team]);
    return true;
}

/// <summary>509e30</summary>
int Duel::calc_action_ratio(duel_team_t a_team, int a_chara, duel_team_t b_team, int b_chara) const
{
    if (not check_state(a_team, a_chara, -1) or not check_state(b_team, b_chara, -1))
        return 0;

    int a_str = get_strength(a_team, a_chara, true); // 1 .. 110
    int b_str = get_strength(b_team, b_chara, true);

    int max_str = std::max(a_str, b_str);
    int min_str = std::min(a_str, b_str);
    int a, b, c, d;
    int x, y, z;

    a = std::max(max_str - 5, 0); // 0 .. 105
    a = a * a / 1500; // 0 .. 7

    x = max_str / 10; // 0 .. 11
    y = min_str / 10;
    b = std::max(x - y, 1); // 1 .. 11

    x = a_str - min_str; // 0 .. 109
    y = x + b - a - 1; // 0 .. 112
    c = std::max(y, 0) * b; // 0 .. 1232

    y = std::min(x, a); // 0 .. 7
    z = y + b - a; // 1 .. 11
    d = 0;
    d += y * (a - y); // 0
    d += y * (y + 1) / 2; // 0 .. 28
    d += std::max(z, 0) * std::max(z - 1, 0) / 2; // 0 .. 55
    int a_score = 180 + c + d; // 180 .. 1495

    x = b_str - min_str;
    y = x + b - a - 1;
    c = std::max(y, 0) * b;

    y = std::min(x, a);
    z = y + b - a;
    d = 0;
    d += y * (a - y);
    d += y * (y + 1) / 2;
    d += std::max(z, 0) * std::max(z - 1, 0) / 2;
    int b_score = 180 + c + d;

    a_score *= a_score; // 32400 .. 2235025
    b_score *= b_score;

    int sum = a_score + b_score;

    if (a_score >= b_score)
        return std::min(a_score * 100 / sum, 99); // 1 .. 99
    else
        return 100 - std::min(b_score * 100 / sum, 99);

#if 0
    constexpr int k180 = 180;
    constexpr int k181 = 181;

    int eax, ecx, ebx, esi, edi;
    std::array<int, MaxTeamCount> value;
    int _74;
    int _78;
    int _7c;
    int _80;

    eax = k180 - std::min(a_str, b_str);
    value[a_team] = eax + a_str;
    value[b_team] = eax + b_str;
    _74 = value[a_team];
    _78 = value[b_team];
    eax = std::max(a_str, b_str) - 5;
    ecx = std::max(eax, 0) * std::max(eax, 0) / 1500;
    value[a_team] = std::min(value[a_team] - k180, ecx);
    value[b_team] = std::min(value[b_team] - k180, ecx);

    esi = std::max(a_str, b_str) / 10;
    edi = std::min(a_str, b_str) / 10;
    esi = std::max(esi - edi, 1);

    edi = value[a_team] - ecx + esi;
    _7c = std::max(edi - 1, 0);
    _80 = std::max(edi, 0);
    edi = esi + _74 - ecx - k181;
    eax = std::max(edi, 0) * esi;
    ebx = ecx;
    ebx -= value[a_team];
    ebx *= value[a_team];
    ebx += value[a_team] * (value[a_team] + 1) / 2;
    ebx += _80 * _7c / 2;
    value[a_team] = k180 + eax + ebx;

    edi = value[b_team] - ecx + esi;
    _7c = std::max(edi - 1, 0);
    _80 = std::max(edi, 0);
    edi = esi + _78 - ecx - k181;
    eax = std::max(edi, 0) * esi;
    ebx = ecx;
    ebx -= value[b_team];
    ebx *= value[b_team];
    ebx += value[b_team] * (value[b_team] + 1) / 2;
    ebx += _80 * _7c / 2;
    value[b_team] = k180 + eax + ebx;

    value[a_team] *= value[a_team];
    value[b_team] *= value[b_team];

    int sum = value[a_team] + value[b_team];

    if (value[a_team] >= value[b_team])
        return std::min(value[a_team] * 100 / sum, 99);
    else
        return 100 - std::min(value[b_team] * 100 / sum, 99);
#endif
}

/// <summary>50a120</summary>
int Duel::calc_spirit_gain(duel_team_t team, int chara, bool attack, int value) const
{
    if (value <= 0)
        return 0;

    int n = value;
    int min = 3;
    // 피격 시 방어중시, 투지중시일 경우 최소 5
    if (not attack)
    {
        switch (get_stance(team, chara))
        {
        case DuelStance_Defense:
        case DuelStance_Spirit:
            min = 5;
            break;
        }
    }
    if (n < min)
        n = min;

    n = n * team_get_stance_spirit_gain(&team_[team], chara) / 7;

    if (not attack)
        n = n * 5 / 4;

    int hp = get_hp(team, chara);
    if (hp < 30)
        n = n * 2; // 2
    else if (hp < 50)
        n = n * 3 / 2; // 1.5

    switch (system_->get_difficulty())
    {
    case Difficulty_Easy:
        if (is_player(team))
            n = n * 6 / 5; // 1.2
        break;
    case Difficulty_Hard:
        if (is_player(team))
            n = n * 4 / 5; // 0.8
        else
            n = n * 6 / 5; // 1.2
        break;
    }

    if (has_item(team, chara, { DuelItemType_Sword }))
        n = n * 3 / 2; // 1.5

    if (n < 1)
        n = 1;
    return n;
}

/// <summary>50a2a0</summary>
duel_team_t Duel::calc_special_try() const
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (team_[i].control == DuelControl_Manual)
        {
            if (view_ and engine_->duel_is_special_button_pushed(this, i))
                return i;
        }
    }
    std::array<bool, MaxTeamCount> special = {};
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (team_[i].control != DuelControl_Manual)
            special[i] = ai_calc_special_try(&ai_[i]);
    }
    if (special[0] and special[1])
        special[system_->rand_bool(50) ? 0 : 1] = false;
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (special[i])
            return i;
    }
    return -1;
}

/// <summary>50a390</summary>
void Duel::update_special_action_result()
{
    special_action_.result = calc_special_result(&special_action_);
}

/// <summary>50a3b0</summary>
bool Duel::calc_dodge_chance(duel_team_t team, int chara) const
{
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);
    int strength = get_strength(team, chara, true); // 1 .. 110
    int opponent_strength = get_strength(opponent_team, opponent_chara, true);
    int n = 10 + (strength - opponent_strength) / 3;
    n = std::clamp(n, 5, 30);
    if (get_stance(team, chara) == DuelStance_Defense)
        n += 25;
    return system_->rand_bool(n);
}

/// <summary>50a4b0</summary>
bool Duel::calc_wound_chance(const SpecialAction* special) const
{
    duel_team_t team = special->team;
    int chara = special->chara;
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);

    int n = get_action_ratio(team, chara, opponent_team, opponent_chara); // 1 .. 99
    n = n * n / 50; // 0 .. 196
    n = std::clamp(n, 1, 90);

    if (is_manual(opponent_team))
    {
        switch (system_->get_difficulty())
        {
        case Difficulty_Normal:
            n = n * 6 / 5; // 1.2
            break;
        case Difficulty_Hard:
            n = n * 3 / 2; // 1.5
            break;
        }
    }

    Person* person = get_person(team, chara);
    Person* opponent_person = get_person(opponent_team, opponent_chara);

    switch (person->get_id())
    {
    case PersonId_Kakouen:
    case PersonId_Kouchuu_Kanshou:
        if (special_action_.type == DuelSpecial_Nisetaikyaku)
        {
            switch (opponent_person->get_id())
            {
            case PersonId_Kanu:
            case PersonId_Kyocho:
            case PersonId_Chouhi:
            case PersonId_Chouun:
            case PersonId_Bachou:
            case PersonId_Ryofu:
                n = n * 11 / 10;
                break;
            default:
                n = 100;
                break;
            }
        }
        break;
    }

    if (utils::is_active(opponent_person) and opponent_person->has_skill(SkillId_Kyouun))
        n = 0;

    return system_->rand_bool(n);
}

/// <summary>50afd0</summary>
bool Duel::join_anim()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (not utils::in_range(appearing_chara_[i], 0, MaxTeamCharaCount - 1))
            continue;
        int old_chara = get_current_chara(i);
        if (appearing_chara_[i] == old_chara)
            continue;
        if (not change_current_chara(i, appearing_chara_[i]))
            continue;
        int number = -1;
        for (int j = 0; j < MaxTeamCharaCount; j++)
            number = std::max(number, team_get_number(&team_[i], j));
        team_set_number(&team_[i], appearing_chara_[i], number + 1);
        if (view_)
            engine_->duel_join(this, i, old_chara);
#if s11_removed
        // update ui
#endif
        return false;
    }
    return true;
}

/// <summary>50b0f0</summary>
bool Duel::switch_anim()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (not utils::in_range(switching_chara_[i], 0, MaxTeamCharaCount - 1))
            continue;
        int old_chara = get_current_chara(i);
        if (switching_chara_[i] == old_chara)
            continue;
        if (not change_current_chara(i, switching_chara_[i]))
            continue;
        if (view_)
            engine_->duel_switch(this, i, old_chara);
#if s11_removed
        // update ui
#endif
        return false;
    }
    return true;
}

/// <summary>50b1a0</summary>
bool Duel::special_action_anim()
{
    duel_team_t team = special_action_.team;
    int chara = special_action_.chara;
    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);

    action_count_ = get_special_hit_count(&special_action_);

    for (int i = 0; i < action_count_; i++)
    {
        int hp_damage = calc_special_damage(&special_action_);
#if s11_suspicious(3)
        // special_action_.result 값으로 판단해야 하는게 아닌지?
#endif
        blow_anim_queue_[i].value = action_queue_[i].result != DuelActionResult_Dodged ? 1 : 0;

        hp_anim_queue_[i].damage = hp_damage;
        hp_anim_queue_[i].atk_team = team;
        hp_anim_queue_[i].atk_chara = chara;
        hp_anim_queue_[i].def_team = opponent_team;
        hp_anim_queue_[i].def_chara = opponent_chara;

        spirit_anim_queue_[i].atk_value = 0;
        spirit_anim_queue_[i].atk_team = team;
        spirit_anim_queue_[i].atk_chara = chara;
        spirit_anim_queue_[i].def_value = calc_spirit_gain(opponent_team, opponent_chara, false, hp_damage);
        spirit_anim_queue_[i].def_team = opponent_team;
        spirit_anim_queue_[i].def_chara = opponent_chara;

#if s11_log
        if (Logger* logger = system_->get_logger())
        {
            logger->debug(std::format("Duel::special_action_anim {} {}-{} {} {} {}",
                special_action_.type,
                team,
                chara,
                hp_damage,
                spirit_anim_queue_[i].atk_value,
                spirit_anim_queue_[i].def_value
            ));
        }
#endif
    }

    add_spirit(team, chara, -get_special_spirit_cost(special_action_.type));

    int remaining_count = get_special_remaining_count(team, chara, special_action_.type);
    if (remaining_count > 0)
        set_special_remaining_count(team, chara, special_action_.type, remaining_count - 1);

    bool wound = false;
    switch (special_action_.type)
    {
    case DuelSpecial_Kiai:
        team_set_buff_timer(&team_[team], DuelBuffType_Attack, -1);
        break;
    case DuelSpecial_Kenshu:
        team_set_buff_timer(&team_[team], DuelBuffType_Defense, -1);
        break;
    case DuelSpecial_Kyuusho:
    case DuelSpecial_Nisetaikyaku:
        wound = calc_wound_chance(&special_action_);
        break;
    case DuelSpecial_Musou:
        wound = calc_wound_chance(&special_action_);
        [[fallthrough]];
    case DuelSpecial_Anki:
        for (int i = 0; i < DuelBuffType_Max; i++)
            team_set_buff_timer(&team_[opponent_team], i, 0);
        break;
    }
    if (wound)
        hp_anim_queue_[action_count_ - 1].shoubyou_damage = 1;

    blow_anim(action_count_);
    hp_anim(action_count_);
    spirit_anim(action_count_);
    return true;
}

/// <summary>50b5c0</summary>
void Duel::calc_action_ratio()
{
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        for (int j = 0; j < MaxTeamCharaCount; j++)
            action_ratio_[i][j] = calc_action_ratio(DuelTeam_Challenger, i, DuelTeam_Challenged, j);
    }
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        for (int i = 0; i < MaxTeamCharaCount; i++)
        {
            for (int j = 0; j < MaxTeamCharaCount; j++)
            {
                logger->debug(std::format("calc_action_ratio {}-{} {}",
                    i, j, action_ratio_[i][j]
                ));
            }
        }
    }
#endif
}

/// <summary>50b600</summary>
bool Duel::init_team(duel_team_t team)
{
    if (reverse_)
        team = get_opponent_team(team);
    int count = 0;
    std::array<Person*, MaxTeamCharaCount> person_array = {};
    std::array<int, MaxTeamCharaCount> hp_array = {};
    std::array<int, MaxTeamCharaCount> spirit_array = {};
    std::array<shoubyou_t, MaxTeamCharaCount> shoubyou_array = {};
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        Person* person = param_->person[team][i];
        if (not utils::is_active(person))
            continue;
        person_array[i] = person;
        hp_array[i] = param_->hp[team][i];
        spirit_array[i] = param_->spirit[team][i];
        shoubyou_array[i] = param_->shoubyou[team][i];
        count++;
    }
    if (not count)
        return false;
    int start_chara = param_->start_chara[team];
    if (not utils::in_range(start_chara, 0, MaxTeamCharaCount - 1))
        return false;
    team_init(&team_[team], person_array.data(), hp_array.data(), spirit_array.data(), shoubyou_array.data(), count, start_chara, param_->control[team], param_->player_id[team]);
    change_current_chara(team, start_chara);
    team_set_number(&team_[team], start_chara, 0);
    for (int i = 0; i < MaxTeamCharaCount; i++)
        set_stance(team, i, DuelStance_Spirit);
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        for (int i = 0; i < team_[team].chara_count; i++)
        {
            logger->debug(std::format("{}-{} {}{} {} {} {} 0x{:x}",
                team,
                i,
                team_[team].chara[i].person->get_id(),
                team_[team].chara[i].person->get_name(),
                team_[team].chara[i].hp,
                team_[team].chara[i].spirit,
                team_[team].chara[i].state,
                team_[team].chara[i].item.array()[0]
            ));
        }
    }
#endif
    return true;
}

/// <summary>50b7e0</summary>
int Duel::calc_damage(int* atk_spirit, int* def_spirit, duel_team_t team, int chara, duel_action_t action, duel_action_result_t result)
{
    *atk_spirit = 0;
    *def_spirit = 0;

    assert(utils::in_range(team, 0, MaxTeamCount - 1));
    assert(utils::in_range(chara, 0, MaxTeamCharaCount - 1));
    assert(utils::in_range(action, 0, DuelAction_Max - 1));
    assert(utils::in_range(result, 0, DuelActionResult_Max - 1));

    duel_team_t opponent_team = get_opponent_team(team);
    int opponent_chara = get_current_chara(opponent_team);

    int n = calc_attack_damage(team, chara);

    switch (system_->get_difficulty())
    {
    case Difficulty_Easy:
        if (is_player(team))
        {
            if (not is_player(opponent_team))
                n = n * 11 / 10; // 1.1
        }
        else
        {
            if (is_player(opponent_team))
                n = n * 4 / 5; // 0.8
        }
        break;
    }

    if (has_item(team, chara, { DuelItemType_CrescentHalberd }))
        n = n * 9 / 8; // 1.125
    else if (has_item(team, chara, { DuelItemType_LongSpear }))
        n = n * 10 / 9; // 1.1...

    if (get_person_id(team, chara) == PersonId_Ryofu)
        n = n * 13 / 11; // 1.18...

    if (has_buff(team, DuelBuffType_Attack))
        n = n * 5 / 4; // 1.25
    if (has_buff(opponent_team, DuelBuffType_Defense))
        n = n * 3 / 4; // 0.75

    switch (action)
    {
    case DuelAction_AttackCritical:
        n = n * 3 / 4; // 0.75
        break;
    case DuelAction_DefenseCritical:
    case DuelAction_SpiritCritical:
        *atk_spirit = 0;
        *def_spirit = 0;
        return 0;
    }

    *atk_spirit = n;
    *def_spirit = n;

    bool inc_def_spirit = false;
    duel_stance_t opponent_stance = get_stance(opponent_team, opponent_chara);

    // 피격 시 방어중시, 투지중시일 경우 투지 상승 증가
    switch (opponent_stance)
    {
    case DuelStance_Defense:
    case DuelStance_Spirit:
        inc_def_spirit = true;
        break;
    }

    if (is_invulnerable(opponent_team, opponent_chara))
    {
        *atk_spirit = 0;
        if (inc_def_spirit)
            *def_spirit = std::max(n / 2, 1);
        else
            *def_spirit = 0;
        return 0;
    }

    switch (result)
    {
    case DuelActionResult_Blocked:
        if (opponent_stance == DuelStance_Defense)
            n = std::max(n * 3 / 10, 1); // 0.3
        else
            n = n / 2; // 0.5
        *atk_spirit = *atk_spirit * 3 / 4; // 0.75
        *def_spirit = std::max(*def_spirit / 2, 1); // 0.5
        return n;
    case DuelActionResult_Dodged:
        *atk_spirit = 0;
        if (opponent_stance == DuelStance_Defense)
            return 0;
        if (inc_def_spirit)
            *def_spirit = std::max(*def_spirit / 2, 1); // 0.5
        else
            *def_spirit = 0;
        return 0;
    }

    return n;
}

/// <summary>50bb70</summary>
int Duel::calc_appearing_chara(duel_team_t team) const
{
    if (not team_has_sub_chara(&team_[team], DuelCharaState_NotJoined))
        return -1;
    if (team_[team].appearing_timer > 0)
        return -1;
    if (team_get_hp(&team_[team], team_[team].current_chara) >= 33)
    {
        for (int i = 0; i < DuelBuffType_Max; i++)
        {
            if (team_has_buff(&team_[team], i))
                return -1;
        }
    }
    if (team_[team].invulnerable_timer > 0)
        return -1;
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (not team_is_active(&team_[team], i))
            continue;
        if (team_get_state(&team_[team], i) != DuelCharaState_NotJoined)
            continue;
        if (not can_join(team, i))
            continue;
        if (not calc_join(team, i))
            continue;
        return i;
    }
    return -1;
}

/// <summary>50bcb0</summary>
duel_action_result_t Duel::calc_action_result(duel_team_t team, int chara, duel_action_t action) const
{
    duel_team_t opponent_team = get_opponent_team(team);
    assert(utils::in_range(opponent_team, 0, MaxTeamCount - 1));
    int opponent_chara = get_current_chara(opponent_team);
    assert(utils::in_range(opponent_chara, 0, MaxTeamCharaCount - 1));

    if (is_invulnerable(opponent_team, opponent_chara))
        return DuelActionResult_Blocked;

    Person* person = get_person(team, chara);
    assert(utils::is_active(person));
    Person* opponent_person = get_person(opponent_team, opponent_chara);
    assert(utils::is_active(opponent_person));
    // 8373f0
    static constexpr std::array<person_id_t, 3> table = {
        PersonId_Ryofu, PersonId_Chouhi, PersonId_Kanu,
    };
    if (std::ranges::contains(table, person->get_id()) and std::ranges::contains(table, opponent_person->get_id()) and system_->rand_bool(50))
        return DuelActionResult_Blocked;

    int ratio = get_action_ratio(team, chara, opponent_team, opponent_chara);
    int hit = team_get_stance_hit(&team_[team], chara);
    int block = team_get_stance_block(&team_[opponent_team], opponent_chara);
    int n = hit;
    n = n * (100 - block) / 80; // 1 .. 1.25
    n = n * ratio / 50; // 0 .. 1
    n = n + system_->rand_int(10); // 실제 범위 1 .. 435
    n = std::clamp(n, 10, 99);
    if (system_->rand_bool(n))
        return DuelActionResult_Hit;

    if (calc_dodge_chance(opponent_team, opponent_chara))
        return DuelActionResult_Dodged;
    return DuelActionResult_Blocked;
}

/// <summary>50c170</summary>
void Duel::calc_appearing()
{
    for (int i = 0; i < MaxTeamCount; i++)
        appearing_chara_[i] = calc_appearing_chara(i);
}

/// <summary>50c1a0</summary>
void Duel::update_action()
{
    action_queue_.fill(Action());
    duel_team_t team = calc_actor_team();
    assert(utils::in_range(team, 0, MaxTeamCount - 1));
    int chara = get_current_chara(team);
    assert(utils::in_range(chara, 0, MaxTeamCharaCount - 1));
    duel_action_t action = calc_action(team, chara);
    assert(utils::in_range(action, 0, DuelAction_Max - 1));
    action_count_ = 1;
    if (action == DuelAction_AttackCritical)
        action_count_ = 3 + system_->rand_int(2);
    for (int i = 0; i < action_count_; i++)
    {
        action_queue_[i].team = team;
        action_queue_[i].chara = chara;
        action_queue_[i].type = action;
        action_queue_[i].result = calc_action_result(team, chara, action);
    }
}

/// <summary>50c280</summary>
bool Duel::action_anim()
{
    if (action_count_ <= 0)
        return false;
    for (int i = 0; i < action_count_; i++)
    {
        duel_team_t team = action_queue_[i].team;
        int chara = action_queue_[i].chara;
        duel_team_t opponent_team = get_opponent_team(team);
        int opponent_chara = get_current_chara(opponent_team);
        int atk_spirit = 0;
        int def_spirit = 0;
        int hp_damage = calc_damage(&atk_spirit, &def_spirit, team, chara, action_queue_[i].type, action_queue_[i].result);

        hp_anim_queue_[i].damage = hp_damage;
        hp_anim_queue_[i].atk_team = team;
        hp_anim_queue_[i].atk_chara = chara;
        hp_anim_queue_[i].def_team = opponent_team;
        hp_anim_queue_[i].def_chara = opponent_chara;

        spirit_anim_queue_[i].atk_value = calc_spirit_gain(team, chara, true, atk_spirit);
        spirit_anim_queue_[i].atk_team = team;
        spirit_anim_queue_[i].atk_chara = chara;
        spirit_anim_queue_[i].def_value = calc_spirit_gain(opponent_team, opponent_chara, false, def_spirit);
        spirit_anim_queue_[i].def_team = opponent_team;
        spirit_anim_queue_[i].def_chara = opponent_chara;

        switch (action_queue_[i].type)
        {
        case DuelAction_DefenseCritical:
            blow_anim_queue_[i].value = 0;
            break;
        case DuelAction_SpiritCritical:
            spirit_anim_queue_[i].atk_value = 100;
            blow_anim_queue_[i].value = 0;
            break;
        default:
            blow_anim_queue_[i].value = action_queue_[i].result != DuelActionResult_Dodged ? 1 : 0;;
            break;
        }

#if s11_log
        if (Logger* logger = system_->get_logger())
        {
            logger->debug(std::format("Duel::action_anim {}-{} {} {} {} {} {}",
                team,
                chara,
                action_queue_[i].type,
                action_queue_[i].result,
                hp_damage,
                spirit_anim_queue_[i].atk_value,
                spirit_anim_queue_[i].def_value
            ));
        }
#endif
    }

    blow_anim(action_count_);
    hp_anim(action_count_);
    spirit_anim(action_count_);
    return true;
}

/// <summary>50c490</summary>
s11_static duel_team_t Duel::calc_duel_ftk_team(Person* a, Person* b, int* chance)
{
    Scenario* scenario = a->get_scenario();
    if (scenario->get_game()->is_feat_disabled(Feature_DuelFirstTurnKill))
        return -1;
    if (not utils::is_alive(a))
        return -1;
    if (not utils::is_alive(b))
        return -1;
    shoubyou_t a_shoubyou = a->get_shoubyou();
    shoubyou_t b_shoubyou = b->get_shoubyou();
    bool a_bow = false;
    bool b_bow = false;
    for (Item* item : scenario->get_game()->get_person_item_list(a))
    {
        if (utils::is_alive(item) and item->get_type() == ItemType_Bow)
        {
            a_bow = true;
            break;
        }
    }
    for (Item* item : scenario->get_game()->get_person_item_list(b))
    {
        if (utils::is_alive(item) and item->get_type() == ItemType_Bow)
        {
            b_bow = true;
            break;
        }
    }
    return calc_duel_ftk_team(a, b, a_bow, b_bow, a_shoubyou, b_shoubyou, chance);
}

/// <summary>50c930, v+0</summary>
void Duel::init()
{
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("Duel::init 0x{:x}",
            system_->get_seed()
        ));
    }
#endif
    // 508a20
    *this = Duel(system_, param_);

    max_blow_counter_ = param_->max_blow_counter;
    ftk_type_ = param_->ftk_type;
    ftk_team_ = param_->ftk_team;

    // 50c620
    type_ = param_->type;
    if (not utils::in_range(type_, 0, DuelType_Max - 1))
        type_ = DuelType_2;
    for (int i = 0; i < MaxTeamCount; i++)
        init_team(i);
    for (int i = 0; i < MaxTeamCount; i++)
        ai_init(&ai_[i], i);
    calc_action_ratio();
    
    set_next_phase(DuelPhase_Init);
}

/// <summary>50c980</summary>
void Duel::chara_init_special(Character* self, Person* person)
{
    if (not utils::is_active(person))
        return;
    self->special_remaining_count.fill(-1);
    self->special_remaining_count[DuelSpecial_Anki] = self->item[DuelItemType_ThrowingKnife] ? 1 : 0;
    self->special_remaining_count[DuelSpecial_Nisetaikyaku] = self->item[DuelItemType_Bow] ? 1 : 0;
}

/// <summary>50c9d0</summary>
Person* Duel::team_get_person(const Team* self, int chara) const
{
    Person* person = self->chara[chara].person;
    if (utils::is_active(person))
        return person;
    return nullptr;
}

/// <summary>50ca20</summary>
bool Duel::team_is_active(const Team* self, int chara) const
{
    Person* person = self->chara[chara].person;
    return utils::is_active(person);
}

/// <summary>50ca70</summary>
void Duel::team_set_buff_timer(Team* self, duel_buff_type_t type, int timer)
{
    self->buff_timer[type] = timer;
}

/// <summary>50ca90</summary>
int Duel::team_get_buff_timer(const Team* self, duel_buff_type_t type) const
{
    return self->buff_timer[type];
}

/// <summary>50cab0</summary>
duel_stance_t Duel::team_get_stance(const Team* self, int chara) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].stance;
    return -1;
}

/// <summary>50cb00</summary>
void Duel::team_set_stance(Team* self, int chara, duel_stance_t stance)
{
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (team_is_active(self, i))
            self->chara[i].stance = stance;
    }
}

/// <summary>50cb80</summary>
duel_chara_state_t Duel::team_get_state(const Team* self, int chara) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].state;
    return -1;
}

/// <summary>50cbd0</summary>
int Duel::team_get_hp(const Team* self, int chara) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].hp;
    return MaxHP;
}

/// <summary>50cc20</summary>
void Duel::team_set_hp(Team* self, int chara, int value)
{
    if (team_is_active(self, chara))
        self->chara[chara].hp = value;
}

/// <summary>50cc70</summary>
int Duel::team_get_spirit(const Team* self, int chara) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].spirit;
    return MaxSpirit;
}

/// <summary>50ccc0</summary>
void Duel::team_set_spirit(Team* self, int chara, int value)
{
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (team_is_active(self, i))
            self->chara[i].spirit = value;
    }
}

/// <summary>50cd40</summary>
shoubyou_t Duel::team_get_shoubyou(const Team* self, int chara) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].shoubyou;
    return -1;
}

/// <summary>50cd90</summary>
void Duel::team_set_shoubyou(Team* self, int chara, shoubyou_t shoubyou)
{
    if (team_is_active(self, chara))
        self->chara[chara].shoubyou = shoubyou;
}

/// <summary>50cde0</summary>
int Duel::team_get_number(const Team* self, int chara) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].number;
    return -1;
}

/// <summary>50ce30</summary>
void Duel::team_set_number(Team* self, int chara, int value)
{
    if (team_is_active(self, chara))
        self->chara[chara].number = value;
}

/// <summary>50ce80</summary>
int Duel::team_get_special_remaining_count(const Team* self, int chara, duel_sp_t special) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].special_remaining_count[special];
    return 0;
}

/// <summary>50cee0</summary>
void Duel::team_set_special_remaining_count(Team* self, int chara, duel_sp_t special, int value)
{
    if (team_is_active(self, chara))
        self->chara[chara].special_remaining_count[special] = value;
}

/// <summary>50cf70</summary>
bool Duel::team_has_item(const Team* self, int chara, bitset4<DuelItemType_Max> flags) const
{
    if (team_is_active(self, chara))
        return self->chara[chara].item & flags;
    return false;
}

/// <summary>50cf90</summary>
s11_static int Duel::get_duel_strength(Person* self, shoubyou_t shoubyou, bool revised)
{
    Scenario* scenario = self->get_scenario();
    if (not utils::is_active(self))
        return 0;
    if (not utils::in_range(shoubyou, 0, Shoubyou_Hinshi))
        return 0;
    int n = self->calc_stat(PersonStatType_Strength, shoubyou);
    if (not revised)
        return n;
    int age;
    switch (self->get_id())
    {
    case PersonId_Ryofu:
        return n + 10;
    case PersonId_Chouhi:
    case PersonId_Kanu:
        return n + 5;
    case PersonId_Kyocho:
    case PersonId_Chouun:
    case PersonId_Bachou:
        return n + 3;
    case PersonId_Kouchuu_Kanshou:
        if (utils::is_alive(scenario) and scenario->get_life_mode() == LifeMode_Virtual)
            return n + 5;
        age = self->get_age();
        if (age < 60)
            return n + 1;
        else if (age < 65)
            return n + 2;
        else if (age < 70)
            return n + 3;
        else if (age < 80)
            return n + 4;
        else if (age < 90)
            return n + 5;
        return n + 10;
    }
    return n;
}

/// <summary>50d0c0</summary>
int Duel::team_get_stance_speed(const Team* self, int chara) const
{
    duel_stance_t stance = team_get_stance(self, chara);
    assert(utils::in_range(stance, 0, DuelStance_Max - 1));
    return StanceCoef[stance].speed;
}

/// <summary>50d0f0</summary>
int Duel::team_get_stance_hit(const Team* self, int chara) const
{
    duel_stance_t stance = team_get_stance(self, chara);
    assert(utils::in_range(stance, 0, DuelStance_Max - 1));
    return StanceCoef[stance].hit;
}

/// <summary>50d120</summary>
int Duel::team_get_stance_attack(const Team* self, int chara) const
{
    duel_stance_t stance = team_get_stance(self, chara);
    assert(utils::in_range(stance, 0, DuelStance_Max - 1));
    return StanceCoef[stance].attack;
}

/// <summary>50d150</summary>
int Duel::team_get_stance_block(const Team* self, int chara) const
{
    duel_stance_t stance = team_get_stance(self, chara);
    assert(utils::in_range(stance, 0, DuelStance_Max - 1));
    return StanceCoef[stance].block;
}

/// <summary>50d180</summary>
int Duel::team_get_stance_attack_sub(const Team* self, int chara) const
{
    duel_stance_t stance = team_get_stance(self, chara);
    assert(utils::in_range(stance, 0, DuelStance_Max - 1));
    return StanceCoef[stance].attack_sub;
}

/// <summary>50d1b0</summary>
int Duel::team_get_stance_spirit_gain(const Team* self, int chara) const
{
    duel_stance_t stance = team_get_stance(self, chara);
    assert(utils::in_range(stance, 0, DuelStance_Max - 1));
    return StanceCoef[stance].spirit_gain;
}

/// <summary>50d270</summary>
bool Duel::team_has_sub_chara(const Team* self, duel_chara_state_t state) const
{
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (self->current_chara == i)
            continue;
        if (not team_is_active(self, i))
            continue;
        if (team_get_state(self, i) == state)
            return true;
    }
    return false;
}

/// <summary>50d2f0</summary>
bool Duel::team_has_buff(const Team* self, duel_buff_type_t type) const
{
    int timer = self->buff_timer[type];
    return timer == -1 or timer > 0;
}

/// <summary>50d320</summary>
bool Duel::team_change_current_chara(Team* self, int chara)
{
    int old_chara = self->current_chara;
    self->current_chara = chara;
    if (team_is_active(self, old_chara))
        self->chara[old_chara].state = DuelCharaState_Waiting;
    if (team_is_active(self, chara))
        self->chara[chara].state = DuelCharaState_Active;
    for (int i = 0; i < DuelBuffType_Max; i++)
        self->buff_timer[i] = 0;
    return true;
}

/// <summary>50d3e0</summary>
int Duel::team_get_strength(const Team* self, int chara, bool revised) const
{
    if (team_is_active(self, chara))
        return get_duel_strength(team_get_person(self, chara), team_get_shoubyou(self, chara), revised);
    return Person::MinStat;
}

/// <summary>50d420</summary>
int Duel::team_add_hp(Team* self, int chara, int value)
{
    int diff = MaxHP;
    if (team_is_active(self, chara))
    {
        int n = self->chara[chara].hp;
        int max = MaxHP;
        diff = std::clamp(n + value, 0, max) - n;
        self->chara[chara].hp += diff;
    }
    return diff;
}

/// <summary>50d4a0</summary>
int Duel::team_add_spirit(Team* self, int chara, int value)
{
    int diff = MaxSpirit;
    for (int i = 0; i < MaxTeamCharaCount; i++)
    {
        if (team_is_active(self, i))
        {
            int n = self->chara[i].spirit;
            int max = MaxSpirit;
            diff = std::clamp(n + value, 0, max) - n;
            self->chara[i].spirit += diff;
        }
    }
    return diff;
}

/// <summary>50d5b0</summary>
void Duel::chara_init_item(Character* self, Person* person)
{
    self->item = {};
    if (not utils::is_active(person))
        return;
    for (Item* item : system_->get_person_item_list(person))
    {
        if (not utils::is_alive(item))
            continue;
        switch (item->get_type())
        {
        case ItemType_EliteHorse:
            self->item[DuelItemType_EliteHorse] = true;
            break;
        case ItemType_Sword:
            self->item[DuelItemType_Sword] = true;
            break;
        case ItemType_LongSpear:
            self->item[DuelItemType_LongSpear] = true;
            switch (item->get_id())
            {
            case ItemId_SerpentBlade:
                self->item[DuelItemType_SerpentBlade] = true;
                break;
            case ItemId_BlueDragon:
                self->item[DuelItemType_BlueDragon] = true;
                break;
            case ItemId_CrescentHalberd:
                self->item[DuelItemType_CrescentHalberd] = true;
                break;
            }
            break;
        case ItemType_ThrowingKnife:
            self->item[DuelItemType_ThrowingKnife] = true;
            break;
        case ItemType_Bow:
            self->item[DuelItemType_Bow] = true;
            break;
        }
    }
}

/// <summary>50d700</summary>
void Duel::chara_init(Character* self, Person* person, int hp, int spirit, shoubyou_t shoubyou)
{
    *self = Character();
    if (not utils::is_active(person))
        return;
    self->person = person;
    self->hp = hp;
    self->spirit = spirit;
    self->shoubyou = shoubyou;
    chara_init_item(self, person);
    chara_init_special(self, person);
}

/// <summary>50d780</summary>
void Duel::team_init(Team* self, Person* person_array[], int hp_array[], int spirit_array[], shoubyou_t shoubyou_array[], int count, int current_chara, duel_control_t control, player_t player_id)
{
    assert(person_array);
    assert(hp_array);
    assert(spirit_array);
    assert(shoubyou_array);
    assert(utils::in_range(count, 1, MaxTeamCharaCount));
    *self = Team();
    self->current_chara = current_chara;
    if (not utils::in_range(self->current_chara, 0, MaxTeamCharaCount - 1))
        self->current_chara = 0;
    self->chara_count = count;
    self->control = control;
    self->player_id = player_id;
    for (int i = 0; i < count; i++)
    {
        Person* person = person_array[i];
        if (not utils::is_active(person))
            continue;
        chara_init(&self->chara[i], person, hp_array[i], spirit_array[i], shoubyou_array[i]);
        self->chara[i].state = self->current_chara == i ? DuelCharaState_Active : DuelCharaState_NotJoined;            
    }
}

/// <summary>50dac0</summary>
Unit* Duel::param_get_winner_unit(Param* self)
{
    if (not utils::in_range(self->winner_team, 0, MaxTeamCount - 1))
        return nullptr;
    return self->unit[self->winner_team];
}

/// <summary>50dae0</summary>
Unit* Duel::param_get_loser_unit(Param* self)
{
    if (not utils::in_range(self->loser_team, 0, MaxTeamCount - 1))
        return nullptr;
    return self->unit[self->loser_team];
}

/// <summary>50db00</summary>
duel_chara_result_t Duel::param_get_chara_result(Param* self, duel_team_t team, int chara)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return -1;
    if (not utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return -1;
    return self->result[team][chara];
}

/// <summary>50db40</summary>
Unit* Duel::param_get_unit(Param* self, duel_team_t team, int chara)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return nullptr;
    if (not utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return nullptr;
    return self->unit[team];
}

/// <summary>50db80</summary>
int Duel::param_get_hp(Param* self, duel_team_t team, int chara)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return 0;
    if (not utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return 0;
    return self->hp[team][chara];
}

/// <summary>50dbc0</summary>
int Duel::param_get_spirit(Param* self, duel_team_t team, int chara)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return 0;
    if (not utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return 0;
    return self->spirit[team][chara];
}

/// <summary>50dc10</summary>
shoubyou_t Duel::param_get_shoubyou(Param* self, duel_team_t team, int chara)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return -1;
    if (not utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return -1;
    return self->shoubyou[team][chara];
}

/// <summary>50dc60</summary>
Person* Duel::param_get_person(Param* self, duel_team_t team, int chara)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return nullptr;
    if (not utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return nullptr;
    return self->person[team][chara];
}

/// <summary>50dca0</summary>
Person* Duel::param_get_winner_person(Param* self)
{
    return param_get_person(self, self->winner_team, self->winner_chara);
}

/// <summary>50dcd0</summary>
Person* Duel::param_get_loser_person(Param* self)
{
    return param_get_person(self, self->loser_team, self->loser_chara);
}

/// <summary>50dd00</summary>
int Duel::param_get_start_chara(Param* self, duel_team_t team)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return -1;
    return self->start_chara[team];
}

/// <summary>50dd30</summary>
player_t Duel::param_get_player_id(Param* self, duel_team_t team)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return -1;
    return self->player_id[team];
}

/// <summary>50dd60</summary>
duel_control_t Duel::param_get_control(Param* self, duel_team_t team)
{
    if (not utils::in_range(team, 0, MaxTeamCount - 1))
        return -1;
    return self->control[team];
}

/// <summary>50dd90</summary>
bool Duel::param_is_manual(Param* self)
{
    return self->control[0] == DuelControl_Manual or self->control[1] == DuelControl_Manual;
}

/// <summary>50e290</summary>
Person* Duel::param_get_challenger(Param* self)
{
    int chara = self->start_chara[DuelTeam_Challenger];
    if (utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return self->person[DuelTeam_Challenger][chara];
    return nullptr;
}

/// <summary>50e2b0</summary>
Person* Duel::param_get_challenged(Param* self)
{
    int chara = self->start_chara[DuelTeam_Challenged];
    if (utils::in_range(chara, 0, MaxTeamCharaCount - 1))
        return self->person[DuelTeam_Challenged][chara];
    return nullptr;
}

/// <summary>50ed00</summary>
bool Duel::run()
{
    bool view = false;
    if (not param_->tutorial)
    {
        s11_wip;
        if (param_->control[0] == DuelControl_Manual or param_->control[1] == DuelControl_Manual)
        {
            int challenger_count = 0;
            int challenged_count = 0;
            for (int i = 0; i < MaxTeamCharaCount; i++)
            {
                if (utils::is_alive(param_->person[0][i]))
                    challenger_count++;
                if (utils::is_alive(param_->person[1][i]))
                    challenged_count++;
            }
            Message msg;
            msg.set_obj0_obj1_obj2_obj3_obj4_obj5_num0_num1(N_WAR_IKKI_KAKUNIN, param_->person[0][0], param_->person[0][1], param_->person[0][2], param_->person[1][0], param_->person[1][1], param_->person[1][2], challenger_count, challenged_count);
            view = engine_->yes_no(system_->get_msg(&msg));
            if (not view)
                engine_ = nullptr;
        }
    }
#if s11_removed
    // 50e740
#endif
    init();
    while (not on_phase(0))
    {
    }
    exit();
    return view;
}

/// <summary>682780, v+10</summary>
bool Duel::is_tutorial() const
{
    return false;
}

s11_end_namespace