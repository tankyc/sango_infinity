#include "pch.h"

s11_begin_namespace

int System::get_duel_item_power(Person* self)
{
    if (not utils::is_active(self))
        return 0;
    person_id_t person_id = self->get_id();
    int horse = 0;
    int sword = 0;
    int spear = 0;
    int throwing_knife = 0;
    int bow = 0;
    for (Item* item : utils::ptr_view(get_item_pool()))
    {
        if (not item->is_alive())
            continue;
        if (item->get_owner_person_id() != person_id)
            continue;
        switch (item->get_id())
        {
        case ItemId_CrescentHalberd:
            spear = std::max(spear, 10);
            break;
        case ItemId_BlueDragon:
            spear = std::max(spear, 10);
            break;
        case ItemId_SerpentBlade:
            spear = std::max(spear, 7);
            break;
        default:
            switch (item->get_type())
            {
            case ItemType_EliteHorse:
                horse = 1;
                break;
            case ItemType_Sword:
                sword = 2;
                break;
            case ItemType_LongSpear:
                spear = std::max(spear, 5);
                break;
            case ItemType_ThrowingKnife:
                throwing_knife = 7;
                break;
            case ItemType_Bow:
                bow = 10;
                break;
            }
        }
    }
    return horse + sword + spear + throwing_knife + bow; // 0 .. 30
}

int System::get_duel_troops(Unit* self)
{
    if (utils::is_alive(self))
        return std::max(self->get_troops(), 1000);
    return 0;
}

int System::get_duel_person_power(Person* self, bool ignore_hp)
{
    if (not utils::is_active(self))
        return 0;
    int hp = self->get_hp();
    if (hp < Duel::get_duel_min_hp(self) and not ignore_hp)
        return 0;
    int strength = self->get_stat(PersonStatType_Strength); // 1 .. 100
    int item_power = get_duel_item_power(self); // 0 .. 30
    int power = item_power + static_cast<int>((static_cast<float>(hp) + 200) * (1.f / 60.f) * (strength * strength) * (1.f / 400.f)); // 0 .. 125 + 30
    power = std::max(power, 1); // 1 .. 155
    if (self->is_kunshu() and strength < 95)
        return std::max(power - 20, 1);
    return power;
}

int System::get_duel_person_team_power(Person* self, Person* starter)
{
    if (not utils::is_active(self))
        return 0;
    if (self->is_hate(starter->get_id()))
        return 0;
    return get_duel_person_power(self, false);
}

Person* System::get_best_duel_starter(Unit* self, bool ignore_hp)
{
    if (not utils::is_alive(self))
        return 0;
    Person* best = nullptr;
    int best_power = 0;
    for (int i = 0; i < Unit::MaxMemberCount; i++)
    {
        Person* person = get_person(self->get_member_id(i));
        if (not utils::is_active(person))
            continue;
        int power = get_duel_person_power(person, ignore_hp);
        if (power > 0)
        {
            seikaku_t seikaku = person->get_seikaku();
            static constexpr std::array<int, Seikaku_Max> SeikakuCoef = { 0, 5, 10, 15 }; // 849468
            if (utils::in_range(seikaku, 0, Seikaku_Max - 1))
                power += SeikakuCoef[seikaku];
            switch (person->get_id())
            {
            case PersonId_Kanu:
            case PersonId_Shukuyuu:
            case PersonId_Sonsaku:
                power += 5;
                break;
            case PersonId_Ousou:
            case PersonId_Kyocho:
            case PersonId_Moukaku:
                power += 15;
                break;
            case PersonId_Chouhi:
            case PersonId_Ryofu:
                power += 25;
                break;
            }
        }
        if (best_power < power)
        {
            best_power = power;
            best = person;
        }
    }
    return best;
}

Person* System::select_attack_duel_challenger(Unit* self)
{
    if (not utils::is_alive(self))
        return nullptr;
    Person* person = get_best_duel_starter(self, true);
    if (not utils::is_active(person))
        return nullptr;
    return person;
}

Person* System::get_random_duel_starter(Unit* self)
{
    if (not utils::is_alive(self))
        return nullptr;
    std::array<int, Unit::MaxMemberCount> arr = {};
    int acc = 0, sum = 0;
    for (int i = 0; i < Unit::MaxMemberCount; i++)
    {
        Person* person = get_person(self->get_member_id(i));
        if (not utils::is_active(person))
            continue;
        int power = get_duel_person_power(person, true) + 100;
        sum += power;
        arr[i] = power;
    }
    int n = rand_int(sum);
    for (int i = 0; i < Unit::MaxMemberCount; i++)
    {
        acc += arr[i];
        if (acc > n)
        {
            person_id_t person_id = self->get_member_id(i);
            if (person_id >= 0)
                return get_person(person_id);
        }
    }
    return nullptr;
}

bool System::init_duel_param(Duel::Param* self, Unit* src, Unit* target, Person* challenger, Person* challenged)
{
    if (not utils::is_alive(src))
        return false;
    if (not utils::is_alive(target))
        return false;
    if (not utils::is_alive(challenger))
        return false;
    if (not utils::is_alive(challenged))
        return false;
    Force* src_force = get_force(src->get_force_id());
    Force* target_force = get_force(target->get_force_id());
    self->player_id[0] = utils::is_alive(src_force) ? src_force->get_player_id() : -1;
    self->player_id[1] = utils::is_alive(target_force) ? target_force->get_player_id() : -1;
    self->type = DuelType_2;
    self->unit[0] = src;
    self->unit[1] = target;
    self->control[0] = src->is_player_controlled() ? DuelControl_Manual : DuelControl_Auto;
    self->control[1] = target->is_player_controlled() ? DuelControl_Manual : DuelControl_Auto;
    self->start_chara[0] = 0;
    self->start_chara[1] = 0;
    self->person[0][0] = challenger;
    for (int i = 0, n = 1; i < Unit::MaxMemberCount; i++)
    {
        Person* person = get_person(src->get_member_id(i));
        if (utils::is_active(person) and person != challenger and get_duel_person_team_power(person, challenger) > 0)
            self->person[0][n++] = person;
    }
    self->person[1][0] = challenged;
    for (int i = 0, n = 1; i < Unit::MaxMemberCount; i++)
    {
        Person* person = get_person(target->get_member_id(i));
        if (utils::is_active(person) and person != challenged and get_duel_person_team_power(person, challenged) > 0)
            self->person[1][n++] = person;
    }
    if (self->start_chara[0] > Unit::MaxMemberCount or self->start_chara[1] > Unit::MaxMemberCount)
        return false;
#if s11_added
    for (int i = 0; i < Duel::MaxTeamCount; i++)
    {
        for (int j = 0; j < Duel::MaxTeamCharaCount; j++)
        {
            if (utils::is_active(self->person[i][j]))
            {
                self->hp[i][j] = self->person[i][j]->get_hp();
                self->shoubyou[i][j] = self->person[i][j]->get_shoubyou();
            }
        }
    }
#endif
    // 58a8c0
    for (MapRangeIterator i(&map_, target->get_pos(), 1, 2); i; i++)
    {
        Hex hex = map_.get_hex(i->pos);
        Building* building = get_building(hex.get_building_id());
        if (not utils::is_alive(building))
            continue;
        switch (building->get_facility_id())
        {
        case FacilityId_City:
        case FacilityId_Gate:
        case FacilityId_Port:
        case FacilityId_Outpost:
        case FacilityId_Fort:
            self->stage = DuelStage_Rampart;
            return true;
        }
    }
    // 58a900
    for (MapRangeIterator i(&map_, target->get_pos(), 0, 1); i; i++)
    {
        Hex hex = map_.get_hex(i->pos);
        if (hex.get_terrain_id() == TerrainId_Forest)
        {
            self->stage = DuelStage_Forest;
            return true;
        }
    }
    self->stage = DuelStage_Grassland;
    return true;
}

int System::get_duel_unit_power(Unit* self, s11_in_opt Person* starter)
{
    if (not utils::is_alive(self))
        return 0;
    if (not utils::is_active(starter))
        starter = get_best_duel_starter(self, false);
    if (not utils::is_active(starter))
        return 0;
    int power = 0;
    for (int i = 0; i < Unit::MaxMemberCount; i++)
    {
        Person* person = get_person(self->get_member_id(i));
        power += get_duel_person_team_power(person, starter);
    }
    return power; // 0 .. 465
}

Person* System::select_tactic_duel_challenger(Unit* src, Unit* target)
{
    if (not utils::is_alive(src))
        return nullptr;
    if (not utils::is_alive(target))
        return nullptr;
    Person* person = get_best_duel_starter(src, false);
    if (not utils::is_active(person))
        return nullptr;
    int power = get_duel_person_power(person, false);
    if (power < 70)
        return nullptr;
    seikaku_t seikaku = person->get_seikaku();
    if (seikaku == Seikaku_Shoushin)
        return nullptr;
    if (get_duel_unit_power(src, nullptr) < get_duel_unit_power(target, nullptr) - 30)
        return nullptr;
    int src_troops = get_duel_troops(src);
    int target_troops = get_duel_troops(target);
    if (src_troops >= target_troops * 2 and src_troops - target_troops >= 2500)
        return nullptr;
    int chance = power / 20;
    static constexpr std::array<int, Seikaku_Max> SeikakuCoef = { 0, 0, 1, 3 }; // 849478
    if (utils::in_range(seikaku, 0, Seikaku_Max - 1))
        chance += SeikakuCoef[seikaku];
    if (not rand_bool(chance))
        return nullptr;
    return person;
}

int System::get_duel_provocation_chance(Person* challenger, Person* challenged, Unit* src, Unit* target)
{
    int src_troops = get_duel_troops(src); // 1000 .. 18000
    int target_troops = get_duel_troops(target); // 1000 .. 18000

    // 병력이 훨씬 많을 경우 도발에 걸리지 않음
    if (target_troops >= src_troops * 3 and target_troops - src_troops >= 6000)
        return 0;

    int src_unit_power = get_duel_unit_power(src, challenger); // 1(3) .. 155(465)
    int target_unit_power = get_duel_unit_power(target, challenged); // 1(3) .. 155(465)

    if (src_unit_power > target_unit_power * 2)
        return 0;
    if (challenged->get_seikaku() != Seikaku_Chototsu)
        return 0;
    if (challenged->get_stat(PersonStatType_Strength) < 70)
        return 0;
    if (challenged->get_hp() < 70)
        return 0;

    int challenged_power = get_duel_person_power(challenged, false); // 1 .. 155
    int n = (challenged_power - challenged->get_stat(PersonStatType_Intelligence)) / 5; // -20 .. 30
    n = std::max(n, 1); // 1 .. 30

    if (challenged->get_id() == PersonId_Chouhi)
    {
        n += 10;
        if (challenger->get_id() == PersonId_Ryofu)
            return 100;
    }
    else if (challenged->get_id() == PersonId_Ryofu)
    {
        n += 10;
        if (challenger->get_id() == PersonId_Chouhi)
            return 100;
    }
    return n;
}

Person* System::select_duel_provocation_challenged(Person* challenger, Unit* src, Unit* target)
{
    if (not utils::is_active(challenger))
        return nullptr;
    if (not utils::is_alive(src))
        return nullptr;
    if (not utils::is_alive(target))
        return nullptr;
    Person* best = nullptr;
    int best_chance = 0;
    for (int i = 0; i < Unit::MaxMemberCount; i++)
    {
        Person* challenged = get_person(target->get_member_id(i));
        if (not utils::is_active(challenged))
            continue;
        int chance = get_duel_provocation_chance(challenger, challenged, src, target);
        if (best_chance < chance)
        {
            best_chance = chance;
            best = challenged;
        }
    }
    if (rand_bool(best_chance))
        return best;
    return nullptr;
}

int System::duel_chance(Person* challenger, Unit* src, Unit* target, s11_in_opt point16* pos)
{
    if (is_feat_disabled(Feature_DuelChance))
        return 100;

    int src_troops = get_duel_troops(src); // 1000 .. 18000
    int target_troops = get_duel_troops(target); // 1000 .. 18000
    int src_def = src->get_stat(UnitStatType_Defense); // 1 .. 132
    int target_def = target->get_stat(UnitStatType_Defense); // 1 .. 132
    int src_atk = src->get_stat(UnitStatType_Attack); // 1 .. 132
    int target_atk = target->get_stat(UnitStatType_Attack); // 1 .. 132
    int troops_sum = src_troops + target_troops; // 2000 .. 36000
    int troops_diff = std::max(target_troops - src_troops, 0); // 0 .. 17000

    // src_atk, target_atk, src_def, target_def, src_troops, target_troops
    // 132, 1, 132, 1, 1000, 18000 = 74
    // 132, 1, 132, 1, 18000, 1000 = 61
    // 1, 132, 1, 132, 1000, 18000 = -54
    // 1, 132, 1, 132, 18000, 1000 = -67
    int a = 0;
    a += (src_atk - target_def) / 4; // -33 .. 33
    a += (200 * src_troops) / troops_sum; // 10 .. 189 상대보다 병력 수가 많을수록
    a -= (target_atk - src_def) / 4; // -33 .. 33
    a -= (troops_diff / 100) * (troops_diff / 100) / 150; // 0 .. 192 상대보다 병력 수가 많을수록

    Person* challenged = get_best_duel_starter(target, false);
    if (not utils::is_active(challenged))
        return 0;

    int challenger_power = get_duel_person_power(challenger, true); // 1 .. 155
    int challenged_power = get_duel_person_power(challenged, true); // 1 .. 155
    int src_unit_power = get_duel_unit_power(src, challenger); // 1(3) .. 155(465)
    int target_unit_power = get_duel_unit_power(target, challenged); // 1(3) .. 155(465)
    if (src_unit_power <= target_unit_power * 3 / 2)
    {
        if (challenged->get_id() == PersonId_Chouhi)
        {
            if (challenger->get_id() == PersonId_Ryofu)
                return a;
            if (challenged_power >= 90)
                return a;
        }
        else if (challenged->get_id() == PersonId_Ryofu)
        {
            if (challenger->get_id() == PersonId_Chouhi)
                return a;
            if (challenged_power >= 90)
                return a;
        }
        else if (challenged->get_seikaku() == Seikaku_Chototsu and challenged_power > 116)
        {
            return a;
        }
    }

    // challenger_power, challenged_power, src_unit_power, target_unit_power, src_troops, target_troops
    // 155, 1, 465, 1, 1000, 18000 = -201
    // 155, 1, 465, 1, 18000, 1000 = -183
    // 1, 155, 1, 465, 1000, 18000 = 261
    // 1, 155, 1, 465, 18000, 1000 = 279
    int b = 0;
    b += (challenged_power - challenger_power) / 2; // -77 .. 77
    b += (target_unit_power - src_unit_power) / 3; // -155 .. 154
    b += src_troops / target_troops; // 0 .. 18
    b += 30;

    int hate_coef = challenged->is_hate(challenger->get_id()) ? 1 : 0;
    int state_coef = target->get_state() == UnitState_Konran ? 2 : 0;
    int mibun_coef = challenged->is_kunshu() ? 2 : 1;
    int ftk_chance;
    Duel::calc_duel_ftk_team(challenger, challenged, &ftk_chance);
    if (challenged->get_stat(PersonStatType_Strength) > challenger->get_stat(PersonStatType_Strength))
        ftk_chance = -ftk_chance;
    static constexpr std::array<int, Seikaku_Max> SeikakuCoef = { 0, 5, 10, 15 }; // 849468

    int c = 0;
    c += 100 - ftk_chance;
    c *= (10 + state_coef + hate_coef); // 10 .. 13
    c *= (b + SeikakuCoef[challenged->get_seikaku()]) / mibun_coef; // -201 .. 294
    c /= 1000;
    c = std::clamp(c, 0, 99);

    int n = a * c * 100 / 10000;
    n = std::clamp(n, 0, 100);

    point16 src_pos = pos and map_.is_valid_pos(*pos) ? *pos : src->get_pos();
    if (map_.range_contains(src_pos, 3, [this, force_id = challenger->get_force_id()](Hex hex, int distance) { return find_drum_tower_pred(hex, distance, force_id); }))
        n = std::min(n + 20, 100);
    return n;
}

Person* System::select_duel_challenged(Person* challenger, Unit* src, Unit* target, point16* pos)
{
    if (not utils::is_active(challenger))
        return nullptr;
    if (not utils::is_alive(src))
        return nullptr;
    if (not utils::is_alive(target))
        return nullptr;
    int chance = duel_chance(challenger, src, target, pos);
    if (rand_bool(chance))
        return get_best_duel_starter(target, false);
    return nullptr;
}

Person* System::select_duel_challenged(Person* challenger, Unit* src, Unit* target, bool tactic, point16* pos)
{
    if (not utils::is_active(challenger))
        return nullptr;
    if (not utils::is_alive(src))
        return nullptr;
    if (not utils::is_alive(target))
        return nullptr;
    Person* challenged;
    if (tactic)
    {
        challenged = get_random_duel_starter(target);
        if (utils::is_active(challenged))
            return challenged;
    }
    challenged = get_best_duel_starter(target, true);
    if (not utils::is_active(challenged))
        return nullptr;
    if (tactic or is_feat_disabled(Feature_DuelChance))
        return challenged;
    return select_duel_challenged(challenger, src, target, pos);
}

Person* System::select_duel_starter_dialog(Unit* src, bool challenge, Person* opponent, Unit* target, point16* pos)
{
    if (not utils::is_alive(src))
        return nullptr;
    if (not utils::is_alive(target))
        return nullptr;
    s11_wip;
    return nullptr;
}

Person* System::select_duel_starter(Unit* src, Unit* target, bool player_controlled, tactic_id_t tactic_id, point16* pos, s11_out_opt bool* provoked, bool challenge, s11_in_opt Person* opponent)
{
    if (not utils::is_alive(src))
        return nullptr;
    if (not utils::is_alive(target))
        return nullptr;
    // 받는쪽을 계산할 때 거는쪽이 누군지 알아야함
    if (not challenge and not utils::is_active(opponent))
        return nullptr;
    bool tactic = tactic_id >= 0;
    Force* src_force = get_force(src->get_force_id());
    Force* target_force = get_force(target->get_force_id());
    if (not utils::is_alive(src_force))
        return nullptr;
    if (not utils::is_alive(target_force))
        return nullptr;
    bool manual = not tactic and challenge and player_controlled; // 일기 메뉴를 통해 시도한 경우
    if (not manual)
    {
        if (not src_force->is_normal())
            return nullptr;
        if (not target_force->is_normal())
            return nullptr;
    }
    if (provoked)
        *provoked = false;
    Person* person = nullptr;
    // 플레이어
    if (player_controlled)
    {
        // 거는쪽
        if (challenge)
        {
            if (tactic)
                person = select_tactic_duel_challenger(src, target);
        }
        // 받는쪽
        else
        {
            if (tactic)
            {
                person = select_duel_challenged(opponent, target, src, tactic, pos);
            }
            else
            {
                person = select_duel_provocation_challenged(opponent, target, src);
                if (utils::is_active(person))
                {
                    if (provoked)
                        *provoked = true;
                    return person;
                }
            }
        }
        if (not utils::is_active(person) and not tactic)
            return select_duel_starter_dialog(src, challenge, opponent, target, pos);
        return person;
    }
    // AI
    else
    {
        // 거는쪽
        if (challenge)
        {
            if (tactic)
                return select_tactic_duel_challenger(src, target);
            return select_attack_duel_challenger(src);
        }
        // 받는쪽
        else
        {
            if (not utils::is_active(opponent))
                return nullptr;
            person = select_duel_provocation_challenged(opponent, target, src);
            if (utils::is_active(person))
            {
                if (provoked)
                    *provoked = true;
                return person;
            }
            return select_duel_challenged(opponent, target, src, tactic, pos);
        }
    }
}

s11_end_namespace