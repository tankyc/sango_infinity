#include "pch.h"

s11_begin_namespace

Debate::Debate(System* system, Param* param) : system_(system), engine_(system->get_engine()), param_(param)
{
}

/// <summary>51db90</summary>
void Debate::deck_generate(Deck* self)
{
    // 4, 1, 7, 5, 2, 8, 6, 3, 9
    std::array<int, 9> Table = {
        DebateCard_Logic1, DebateCard_Fact1, DebateCard_Time1,
        DebateCard_Logic2, DebateCard_Fact2, DebateCard_Time2,
        DebateCard_Logic3, DebateCard_Fact3, DebateCard_Time3,
    };
    // 소 카드
    for (int i = 0; i < 8; i++)
        self->table[i] = Table[system_->rand_int(3)];
    // 중 카드
    for (int i = 0; i < 4; i++)
        self->table[8 + i] = Table[3 + system_->rand_int(3)];
    // 대 카드
    for (int i = 0; i < 2; i++)
        self->table[12 + i] = Table[6 + system_->rand_int(3)];
    // 화술 카드 섞기
    if (self->wajutsu_card_count)
    {
        for (int i = 0; i < 2000; i++)
            std::swap(self->wajutsu_card[i % self->wajutsu_card_count], self->wajutsu_card[system_->rand_int(self->wajutsu_card_count)]);
    }
    // 화술 카드(없으면 무작위 공격 카드)
    for (int i = 0; i < 4; i++)
    {
        if (i < self->wajutsu_card_count)
            self->table[14 + i] = self->wajutsu_card[i];
        else
            self->table[14 + i] = Table[system_->rand_int(9)];
    }
    // 전체 섞기
    for (size_t i = 0; i < 2000; i++)
        std::swap(self->table[i % 18], self->table[system_->rand_int(18)]);
}

/// <summary>51dd50</summary>
int Debate::get_max_card_count(Person* person)
{
    if (not utils::is_alive(person))
        return 0;
    int intelligence = person->get_stat(PersonStatType_Intelligence);
    if (intelligence < 70)
        return 4;
    else if (intelligence < 80)
        return 5;
    else if (intelligence < 90)
        return 6;
    return 7;
}

/// <summary>51dd90</summary>
int Debate::chara_get_intelligence(const Character* self) const
{
    assert(utils::is_active(self->person));
    return self->person->get_stat(PersonStatType_Intelligence);
}

/// <summary>51ddc0</summary>
int Debate::chara_get_strength(const Character* self) const
{
    assert(utils::is_active(self->person));
    return self->person->get_stat(PersonStatType_Strength);
}

/// <summary>51ddf0</summary>
seikaku_t Debate::chara_get_seikaku(const Character* self) const
{
    if (utils::in_range(self->seikaku, 0, Seikaku_Max - 1))
        return self->seikaku;
    assert(utils::is_active(self->person));
    return self->person->get_seikaku();
}

/// <summary>51de20</summary>
void Debate::chara_set_anger_timer(Character* self)
{
    switch (chara_get_seikaku(self))
    {
    case Seikaku_Shoushin:
    case Seikaku_Chototsu:
        self->anger_timer = 1;
        break;
    case Seikaku_Reisei:
    case Seikaku_Goutan:
        self->anger_timer = 4;
        break;
    }
}

/// <summary>51de80</summary>
void Debate::chara_dec_anger_timer(Character* self)
{
    if (self->anger_timer > 0)
        self->anger_timer--;
}

/// <summary>51de90</summary>
void Debate::chara_reset_anger_timer(Character* self)
{
    self->anger_timer = 0;
}

/// <summary>51dea0</summary>
void Debate::chara_sort_cards(Character* self)
{
    std::sort(self->card.begin(), self->card.begin() + self->max_card_count);
}

/// <summary>51def0</summary>
void Debate::chara_fill_cards(Character* self)
{
    self->card[0] = DebateCard_Rethink;
    int last = -1;
    for (int i = 1; i < self->max_card_count; i++)
    {
        if (self->card[i] != -1)
            continue;
        if (self->deck.index >= DeckTableSize)
        {
            deck_generate(&self->deck);
            self->deck.index = 0;
        }
        self->card[i] = self->deck.table[self->deck.index++];
        last = i;
    }
    // 공격 카드 최소 한장
    int wadai_card_count = 0;
    for (int i = 1; i < self->max_card_count; i++)
    {
        if (utils::in_range(self->card[i], DebateCard_WadaiFirst, DebateCard_WadaiLast))
            wadai_card_count++;
    }
    if (not wadai_card_count)
        self->card[last] = DebateCard_WadaiFirst + system_->rand_int(DebateCard_WadaiLast - DebateCard_WadaiFirst + 1);
    chara_sort_cards(self);
}

/// <summary>51dfb0</summary>
void Debate::chara_set_card(Character* self, int index, debate_card_t card)
{
    self->card[index] = card;
}

/// <summary>51dfc0</summary>
debate_card_t Debate::chara_get_card(const Character* self, int index) const
{
    return self->card[index];
}

/// <summary>51dfe0</summary>
void Debate::chara_remove_card(Character* self, int index)
{
    chara_set_card(self, index, -1);
    std::array<debate_card_t, MaxCardCount> temp = self->card;
    self->card.fill(-1);
    int p = 0;
    for (int i = 0; i < self->max_card_count; i++)
    {
        if (temp[i] != -1)
            self->card[p++] = temp[i];
    }
}

/// <summary>51e070</summary>
int Debate::chara_get_card_index(const Character* self, debate_card_t card) const
{
    assert(utils::in_range(card, 0, DebateCard_Max - 1));
    for (int i = 0; i < self->max_card_count; i++)
    {
        if (self->card[i] == card)
            return i;
    }
    return -1;
}

/// <summary>51e0b0</summary>
int Debate::chara_get_empty_card_index(const Character* self) const
{
    for (int i = 0; i < self->max_card_count; i++)
    {
        if (self->card[i] == -1)
            return i;
    }
    return -1;
}

/// <summary>51e0e0</summary>
void Debate::chara_rethink(Character* self, wadai_t wadai)
{
    for (int i = 0; i < self->max_card_count; i++)
        self->card[i] = -1;
    chara_fill_cards(self);
    if (self->anger_timer > 0)
    {
        seikaku_t seikaku = chara_get_seikaku(self);
        if (seikaku == Seikaku_Reisei and system_->rand_bool(40))
        {
            // 8b3294
            static constexpr std::array<debate_card_t, Wadai_Max> card = {
                DebateCard_Fact3, DebateCard_Logic3, DebateCard_Time3
            };
            self->card[1] = card[wadai];
        }
    }
    chara_sort_cards(self);
}

/// <summary>51e170</summary>
void Debate::chara_set_hp(Character* self, int value)
{
    self->hp = std::clamp(value, MinHP, MaxHP);
}

/// <summary>51e1a0</summary>
void Debate::chara_add_hp(Character* self, int value)
{
    chara_set_hp(self, self->hp + value);
}

/// <summary>51e1d0</summary>
void Debate::chara_set_stress(Character* self, int value)
{
    self->stress = std::clamp(value, 0, MaxStress);
}

/// <summary>51e1f0</summary>
void Debate::chara_add_stress(Character* self, int value)
{
    chara_set_stress(self, self->stress + value);
}

/// <summary>51e220</summary>
bool Debate::deck_init(Deck* self, Person* person)
{
    assert(utils::is_active(person));
    std::vector<Item*> item_list = system_->get_person_item_list(person);
    bool book = false;
    for (Item* item : item_list)
    {
        if (utils::is_alive(item) and item->get_type() == ItemType_Book)
        {
            book = true;
            break;
        }
    }
    self->wajutsu_card_count = 0;
    for (int i = 0; i < Wajutsu_Max; i++)
    {
        if (book or person->has_wajutsu(i))
            self->wajutsu_card[self->wajutsu_card_count++] = DebateCard_WajutsuFirst + i;
    }
    deck_generate(self);
    return true;
}

/// <summary>51e350</summary>
bool Debate::chara_init(Character* self, Person* person, Person* opponent_person, int hp, bool control)
{
    assert(utils::is_active(person));
    assert(utils::is_active(opponent_person));
    if (hp <= 0)
        hp = MaxHP;
    self->person = person;
    self->hp = hp;
    int my_int = person->get_stat(PersonStatType_Intelligence); // 1 .. 100
    int opponent_int = opponent_person->get_stat(PersonStatType_Intelligence); // 1 .. 100
    int n = 40 * (my_int - opponent_int) / (131 - my_int); // -30 .. 127
    self->attack = 100 + n; // 70 .. 227
    chara_init_cards(self, get_max_card_count(person));
    self->control = control;
    self->stress = 0;
    self->anger_timer = 0;
    return true;
}

/// <summary>51e420</summary>
void Debate::chara_init_cards(Character* self, int max_card_count)
{
    self->max_card_count = max_card_count;
    self->card.fill(-1);
    deck_init(&self->deck, self->person);
    chara_fill_cards(self);
}

/// <summary>51e460</summary>
void Debate::param_set_control(Param* self)
{
    assert(utils::is_active(self->chara[0].person));
    assert(utils::is_active(self->chara[1].person));
    District* district;
    district = system_->get_district(self->chara[0].person->get_district_id());
    self->chara[0].control = utils::is_active(district) and district->is_player() and district->get_number() == 1;
    district = system_->get_district(self->chara[1].person->get_district_id());
    self->chara[1].control = utils::is_active(district) and district->is_player() and district->get_number() == 1;
}

/// <summary>51e510</summary>
bool Debate::param_is_challenger_win(const Param* self) const
{
    debate_team_t team = self->reverse ? DebateTeam_Challenged : DebateTeam_Challenger;
    return self->winner == team;
}

/// <summary>51e520</summary>
Person* Debate::param_get_winner_person(const Param* self) const
{
    debate_team_t team = self->reverse ? DebateTeam_Challenged : DebateTeam_Challenger;
    if (self->winner == team)
        return self->chara[DebateTeam_Challenger].person;
    if (self->winner == get_opponent_team(team))
        return self->chara[DebateTeam_Challenged].person;
    return nullptr;
}

/// <summary>51e550</summary>
Person* Debate::param_get_loser_person(const Param* self) const
{
    debate_team_t team = self->reverse ? DebateTeam_Challenged : DebateTeam_Challenger;
    if (self->winner == team)
        return self->chara[DebateTeam_Challenged].person;
    if (self->winner == get_opponent_team(team))
        return self->chara[DebateTeam_Challenger].person;
    return nullptr;
}

/// <summary>51e580</summary>
Person* Debate::param_get_challenger_person(const Param* self) const
{
    return self->chara[DebateTeam_Challenger].person;
}

/// <summary>51e590</summary>
Person* Debate::param_get_person(const Param* self, debate_team_t team) const
{
    assert(utils::in_range(team, 0, MaxTeamCount - 1));
    if (self->reverse)
        team = get_opponent_team(team);
    return self->chara[team].person;
}

/// <summary>51e5b0</summary>
int Debate::param_get_hp(const Param* self, debate_team_t team) const
{
    assert(utils::in_range(team, 0, MaxTeamCount - 1));
    if (self->reverse)
        team = get_opponent_team(team);
    return self->chara[team].hp;
}

/// <summary>51e5d0</summary>
bool Debate::param_get_control(const Param* self, debate_team_t team) const
{
    assert(utils::in_range(team, 0, MaxTeamCount - 1));
    if (self->reverse)
        team = get_opponent_team(team);
    return self->chara[team].control;
}

/// <summary>51e5f0</summary>
void Debate::param_set_winner(Param* self, debate_team_t team, debate_win_type_t type)
{
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("param_set_winner {} {} 0x{:x}",
            team,
            type,
            system_->get_seed()
        ));
    }
#endif
    self->winner = team;
    self->win_type = type;
    Person* person = param_get_person(self, team);
    Person* opponent_person = param_get_person(self, get_opponent_team(team));
    int exp = 10;
    int kouseki = 100;
    bool injured = false;
    switch (type)
    {
    case DebateWinType_HaveMercy:
        // 기교 증가
        if (utils::is_alive(person))
        {
            Force* force = system_->get_force(person->get_force_id());
            if (utils::is_alive(force))
                system_->force_add_tech_point(force, 50, nullptr);
        }
        // 부상
        if (utils::is_alive(opponent_person))
        {
            shoubyou_t shoubyou = opponent_person->get_shoubyou();
            if (utils::in_range(shoubyou, 0, Shoubyou_Juushou))
            {
                system_->person_set_shoubyou(opponent_person, shoubyou + 1);
                injured = true;
            }
        }
        kouseki = 200;
        break;
    case DebateWinType_PushOn:
        // 경험치 증가
        exp = 30;
        // 부상
        if (utils::is_alive(opponent_person))
        {
            shoubyou_t shoubyou = opponent_person->get_shoubyou();
            if (utils::in_range(shoubyou, 0, Shoubyou_Juushou))
            {
                system_->person_set_shoubyou(opponent_person, shoubyou + 1);
                injured = true;
            }
        }
        kouseki = 200;
        break;
    }
    if (utils::is_alive(person))
    {
        system_->person_add_stat_exp(person, PersonStatType_Intelligence, exp, true);
        system_->person_add_kouseki(person, kouseki);
    }
    if (utils::is_alive(opponent_person))
    {
        system_->person_add_stat_exp(opponent_person, PersonStatType_Intelligence, 1, true);
        system_->person_add_kouseki(opponent_person, 10);
    }
    Message msg;
    if (utils::is_alive(opponent_person) and injured)
    {
        if (view_)
        {
            if (person->is_player_controlled() and not opponent_person->is_player_controlled())
                system_->play_se(10);
            else
                system_->play_se(11);
            msg.set_obj0(O_DEB_FATALBLOW_INJURED, opponent_person);
            system_->message(&msg, nullptr, {}, false);
        }
        if (opponent_person->is_player_controlled())
        {
            point16 pos = NullPos;
            MilitaryUnitObject* hex_obj = system_->get_location_object(opponent_person->get_location_id());
            if (utils::is_alive(hex_obj))
                pos = hex_obj->get_pos();
            msg.set_obj0(LD_DEB_FATALBLOW_INJURED, opponent_person);
            system_->history_log(pos, opponent_person->get_color(), system_->get_msg(&msg), false);
        }
    }
    self->finished = true;
}

/// <summary>51e8c0</summary>
void Debate::param_set_finished(Param* self)
{
    self->finished = true;
}

/// <summary>51e8d0</summary>
bool Debate::param_init(Param* self, Person* a, int a_hp, bool a_control, Person* b, int b_hp, bool b_control, bool tutorial)
{
    assert(utils::is_active(a));
    assert(utils::is_active(b));
#if s11_removed
    if (not self->finished)
        return false;
#endif
    a_hp = std::clamp(a_hp, 1, MaxHP);
    b_hp = std::clamp(b_hp, 1, MaxHP);
    self->chara[0].person = a;
    self->chara[0].hp = a_hp;
    self->chara[0].control = a_control;
    self->chara[1].person = b;
    self->chara[1].hp = b_hp;
    self->chara[1].control = b_control;
    self->reverse = not a_control and b_control;
    self->winner = DebateTeam_Max;
    self->win_type = DebateWinType_Normal;
    self->tutorial = tutorial;
    self->finished = false;
    return true;
}

/// <summary>51e9f0</summary>
bool Debate::param_set_is_full(const Param* self)
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (not utils::is_active(param_get_person(self, i)))
            return false;
    }
    return true;
}

/// <summary>51ea30</summary>
bool Debate::param_init(Param* self, Person* a, int a_hp, Person* b, int b_hp, bool tutorial)
{
    assert(utils::is_active(a));
    assert(utils::is_active(b));
    Force* a_force = system_->get_force(a->get_force_id());
    Force* b_force = system_->get_force(b->get_force_id());
    bool a_control = utils::is_active(a_force) and a_force->is_player();
    bool b_control = utils::is_active(b_force) and b_force->is_player();
    return param_init(self, a, a_hp, a_control, b, b_hp, b_control, tutorial);
}

/// <summary>51eb00</summary>
bool Debate::param_init(Param* self, Person* a, Person* b, bool tutorial)
{
    return param_init(self, a, MaxHP, b, MaxHP, tutorial);
}

/// <summary>65cc50</summary>
debate_team_t Debate::param_get_challenger(const Param* self) const
{
    return self->reverse ? DebateTeam_Challenged : DebateTeam_Challenger;
}

/// <summary>51eb30</summary>
debate_team_t Debate::get_opponent_team(debate_team_t team)
{
    return team == DebateTeam_Challenger ? DebateTeam_Challenged : DebateTeam_Challenger;
}

/// <summary>51eb40</summary>
wadai_t Debate::get_card_wadai(debate_card_t card)
{
    switch (card)
    {
    case DebateCard_Fact1:
    case DebateCard_Fact2:
    case DebateCard_Fact3:
        return Wadai_Fact;
    case DebateCard_Logic1:
    case DebateCard_Logic2:
    case DebateCard_Logic3:
        return Wadai_Logic;
    case DebateCard_Time1:
    case DebateCard_Time2:
    case DebateCard_Time3:
        return Wadai_Time;
    }
    return -1;
}

/// <summary>51eb90</summary>
int Debate::get_card_level(debate_card_t card)
{
    switch (card)
    {
    case DebateCard_Fact1:
    case DebateCard_Logic1:
    case DebateCard_Time1:
        return 0;
    case DebateCard_Fact2:
    case DebateCard_Logic2:
    case DebateCard_Time2:
        return 1;
    case DebateCard_Fact3:
    case DebateCard_Logic3:
    case DebateCard_Time3:
        return 2;
    }
    return -1;
}

/// <summary>51ec40</summary>
Debate::Character* Debate::get_chara(debate_team_t team)
{
    return &chara_[team];
}

/// <summary>51ec60</summary>
void Debate::set_wadai(wadai_t wadai)
{
    wadai_ = wadai;
}

/// <summary>51ec70</summary>
void Debate::add_hp(debate_team_t team, int value)
{
    chara_add_hp(get_chara(team), value);
}

/// <summary>51ec90</summary>
void Debate::add_stress(debate_team_t team, int value)
{
    chara_add_stress(get_chara(team), value);
}

/// <summary>51ecb0</summary>
bool Debate::is_card_available(debate_team_t team, debate_card_t card)
{
    debate_team_t opponent_team = get_opponent_team(team);
    Character* chara = get_chara(team);
    Character* opponent_chara = get_chara(opponent_team);
    seikaku_t seikaku = chara_get_seikaku(chara);
    seikaku_t opponent_seikaku = chara_get_seikaku(opponent_chara);
    bool angered = false;
    bool opponent_angered = false;
    if (chara->anger_timer > 0)
    {
        switch (seikaku)
        {
        case Seikaku_Reisei:
        case Seikaku_Goutan:
            angered = true;
            break;
        }
    }
    if (opponent_chara->anger_timer > 0)
    {
        switch (opponent_seikaku)
        {
        case Seikaku_Reisei:
        case Seikaku_Goutan:
            opponent_angered = true;
            break;
        }
    }
    if (not utils::in_range(card, 0, DebateCard_Max - 1))
        return false;
    switch (card)
    {
    case DebateCard_Rethink:
        if (angered and seikaku != Seikaku_Reisei)
            return false;
        return can_rethink_[team];
    case DebateCard_Daikatsu:
    case DebateCard_Kiben:
    case DebateCard_Mushi:
        if (angered and seikaku != Seikaku_Reisei)
            return false;
        if (opponent_angered and opponent_seikaku == Seikaku_Reisei)
            return false;
        return true;
    case DebateCard_Chinsei:
    case DebateCard_Gyakujou:
        if (angered)
            return false;
        if (opponent_angered and opponent_seikaku == Seikaku_Reisei)
            return false;
        return true;
    }
    return true;
}

/// <summary>51ee00</summary>
bool Debate::play_card(debate_team_t team, int index)
{
    Character* chara = get_chara(team);
    assert(utils::in_range(index, 0, chara->max_card_count - 1));
    debate_card_t card = chara_get_card(chara, index);
    assert(utils::in_range(card, 0, DebateCard_Max - 1));
    played_card_[team] = card;
    if (view_)
        engine_->debate_play_card(this, team, index);
    chara_remove_card(chara, index);
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("play_card {} {} {}{} 0x{:x}",
            team,
            index,
            card,
            system_->get_debate_card_name(card),
            system_->get_seed()
        ));
    }
#endif
    return true;
}

/// <summary>51ee90</summary>
debate_card_t Debate::get_played_card(debate_team_t team) const
{
    return played_card_[team];
}

/// <summary>51eeb0</summary>
void Debate::reset_played_card(debate_team_t team)
{
    played_card_[team] = -1;
}

/// <summary>51eed0</summary>
bool Debate::ftk()
{
    if (winner_ == -1)
    {
        for (int i = 0; i < MaxTeamCount; i++)
        {
            debate_team_t opponent_team = get_opponent_team(i);
            int my_int = chara_get_intelligence(&chara_[i]);
            int opponent_int = chara_get_intelligence(&chara_[opponent_team]);
            if (my_int > 80 and my_int > opponent_int + 10 and chara_[i].attack + system_->rand_int(200) >= 270)
            {
                winner_ = i;
                break;
            }
        }
    }
    if (winner_ != -1)
    {
        debate_team_t opponent_team = get_opponent_team(winner_);
        chara_set_hp(&chara_[opponent_team], MinHP);
        win_type_ = DebateWinType_Max;
        if (view_)
            engine_->debate_ftk(this);
        return true;
    }
    return false;
}

/// <summary>51efd0</summary>
void Debate::turn_start()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        played_card_[i] = -1;

        // 발판이 무너짐
        if (crumbled_[i])
            can_rethink_[i] = true;

        // 분노(냉정)
        if (chara_[i].anger_timer > 0 and chara_get_seikaku(&chara_[i]) == Seikaku_Reisei)
            can_rethink_[i] = true;

        if (not is_tutorial())
            chara_fill_cards(&chara_[i]);

        // 분노(대담)
        if (chara_[i].anger_timer > 0 and chara_get_seikaku(&chara_[i]) == Seikaku_Goutan)
        {
            int wadai_card_count = 0;
            for (int j = 0; j < chara_[i].max_card_count; j++)
            {
                debate_card_t card = chara_get_card(&chara_[i], j);
                if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
                    wadai_card_count++;
            }
            // 더이상 화제 카드가 없을 경우 분노 상태 종료
            if (not wadai_card_count)
                chara_reset_anger_timer(&chara_[i]);
        }

        crumbled_[i] = false;
    }
}

/// <summary>51f0c0</summary>
void Debate::turn_end()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        if (chara_[i].anger_timer > 0)
        {
            chara_dec_anger_timer(&chara_[i]);
            if (not chara_[i].anger_timer)
            {
#if s11_log
                if (Logger* logger = system_->get_logger())
                {
                    logger->debug(std::format("anger_end {}",
                        i
                    ));
                }
#endif
                if (view_)
                    engine_->debate_anger_end(this, i);
            }
        }
    }

    if (utils::in_range(attacker_, 0, MaxTeamCount - 1))
    {
        first_ = attacker_;
        debate_card_t card = played_card_[attacker_];
        if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
        {
            wadai_t wadai = get_card_wadai(card);
            set_wadai(wadai);
        }

        if (not is_tutorial())
        {
            bool change_wadai = false;
            for (int i = 0; i < MaxTeamCount; i++)
            {
                if (played_card_[i] == DebateCard_Kiben and played_card_[get_opponent_team(i)] != DebateCard_Mushi)
                    change_wadai = true;
            }
            if (change_wadai)
            {
                wadai_t wadai = system_->rand_int(Wadai_Max);
                set_wadai(wadai);
            }
        }
    }
}

/// <summary>51f1f0</summary>
int Debate::get_card_power(wadai_t wadai, debate_card_t card)
{
    switch (card)
    {
    case DebateCard_Mushi:
        return 120;
    case DebateCard_Daikatsu:
        return 110;
    case DebateCard_Kiben:
        return 20;
    }
    if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
    {
        int n = 1 + get_card_level(card);
        if (utils::in_range(wadai, 0, Wadai_Max - 1) and get_card_wadai(card) == wadai)
            n += 10;
        return n;
    }
    return 0;
}

/// <summary>51f250</summary>
int Debate::calc_attacker()
{
    int a_power = get_card_power(wadai_, played_card_[0]);
    int b_power = get_card_power(wadai_, played_card_[1]);
    if (chara_[0].anger_timer > 0 and chara_get_seikaku(&chara_[0]) == Seikaku_Goutan)
        a_power = 100;
    if (chara_[1].anger_timer > 0 and chara_get_seikaku(&chara_[1]) == Seikaku_Goutan)
        b_power = 100;
    attacker_ = -1;
    if (a_power > b_power)
        attacker_ = 0;
    else if (b_power > a_power)
        attacker_ = 1;
    return attacker_;
}

/// <summary>51f300</summary>
int Debate::calc_hp_damage(duel_team_t team, debate_card_t card)
{
    int wadai_coef = 6;
    if (chara_[team].anger_timer > 0)
    {
        switch (chara_get_seikaku(&chara_[team]))
        {
        case Seikaku_Shoushin:
        case Seikaku_Goutan:
            wadai_coef = 10;
            break;
        }
    }
    else
    {
        if (card == DebateCard_Daikatsu)
            wadai_coef = 12;
        else if (get_card_wadai(card) == wadai_)
            wadai_coef = 10;
    }

    int level_coef;
    if (card == DebateCard_Daikatsu)
    {
        level_coef = 15;
    }
    else
    {
        int level = get_card_level(card);
        static constexpr std::array<int, 3> LevelCoef = { 10, 15, 20 }; // 8b3380
        if (utils::in_range(level, 0, 2))
            level_coef = LevelCoef[level];
        else
            level_coef = 0;
    }

    int anger_coef = 10;
    if (chara_[team].anger_timer > 0 and chara_get_seikaku(&chara_[team]) == Seikaku_Reisei)
        anger_coef = 15;

    int n = chara_[team].attack + system_->rand_int(5); // 70 .. 231
    n *= anger_coef; // 10 .. 15
    n *= level_coef; // 10 .. 20
    n *= wadai_coef; // 6 .. 12
    n /= 1000; // 42 .. 693(화제3, 대담 분노)
    return n;
}

/// <summary>51f3e0</summary>
int Debate::calc_stress_damage(debate_card_t card)
{
    if (card == DebateCard_Daikatsu)
        return 15;
    if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
    {
        static constexpr std::array<int, 3> StressDamage = { 10, 15, 20 }; // 8b338c
        return StressDamage[get_card_level(card)];
    }
    return 0;
}

/// <summary>51f420</summary>
void Debate::attack_draw()
{
    int stress_damage = 15;
    for (int i = 0; i < MaxTeamCount; i++)
        chara_add_stress(&chara_[i], stress_damage);
    if (view_)
        engine_->debate_attack_draw(this, stress_damage);
}

/// <summary>51f460</summary>
void Debate::daikatsu(debate_team_t team, int hp_damage, int stress_damage)
{
    Character* chara = get_chara(get_opponent_team(team));
    chara_add_hp(chara, -hp_damage);
    chara_add_stress(chara, stress_damage);
    if (view_)
        engine_->debate_daikatsu(this, team, hp_damage, stress_damage);
}

/// <summary>1f4c0</summary>
void Debate::wadai(debate_team_t team, debate_card_t card, int hp_damage, int stress_damage, bool kiben)
{
    Character* chara = get_chara(kiben ? team : get_opponent_team(team));
    chara_add_hp(chara, -hp_damage);
    chara_add_stress(chara, stress_damage);
    if (view_)
        engine_->debate_wadai(this, team, card, hp_damage, stress_damage, kiben);
}

/// <summary>51f540</summary>
void Debate::rethink(debate_team_t team)
{
    Character* chara = get_chara(team);
    chara_rethink(chara, wadai_);
    if (view_)
        engine_->debate_rethink(this, team);
    can_rethink_[team] = false;
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("rethink {} {} {} {} {} {} {} 0x{:x}",
            chara->card[0],
            chara->card[1],
            chara->card[2],
            chara->card[3],
            chara->card[4],
            chara->card[5],
            chara->card[6],
            system_->get_seed()
        ));
    }
#endif
}

/// <summary>51f580</summary>
void Debate::mushi(debate_team_t team, int stress_damage)
{
    Character* chara = get_chara(get_opponent_team(team));
    chara_add_stress(chara, stress_damage);
    if (view_)
        engine_->debate_mushi(this, team, stress_damage);
}

/// <summary>51f5c0</summary>
void Debate::chinsei(debate_team_t team, int stress_damage, bool kiben)
{
    Character* chara = get_chara(kiben ? team : get_opponent_team(team));
    chara_add_stress(chara, stress_damage);
    if (view_)
        engine_->debate_chinsei(this, team, stress_damage, kiben);
}

/// <summary>51f620</summary>
void Debate::gyakujou(debate_team_t team, int stress_damage, bool kiben)
{
    Character* chara = get_chara(kiben ? get_opponent_team(team) : team);
    chara_add_stress(chara, stress_damage);
    if (view_)
        engine_->debate_gyakujou(this, team, stress_damage, kiben);
}

/// <summary>51f680</summary>
void Debate::anger_trigger(debate_team_t team, debate_card_t card)
{
    Character* chara = get_chara(team);
    Character* opponent_chara = get_chara(get_opponent_team(team));
    switch (card)
    {
    case DebateCard_Gyakujou:
        chara_add_stress(opponent_chara, -MaxStress);
        chara_add_stress(chara, -MaxStress / 2);
        break;
    case DebateCard_Chinsei:
        chara_add_stress(chara, -MaxStress / 2);
        break;
    default:
        chara_add_stress(chara, -MaxStress);
        break;
    }
    if (view_)
        engine_->debate_anger_trigger(this, angering_, card);
    if (card >= 0)
    {
#if s11_suspicious(1)
        // 포인터가 누락된 것으로 보임
        // 의도 : Character* opponent_chara = get_chara(get_opponent_team(team));
        // 오타 : Character opponent_chara = get_chara(get_opponent_team(team));
        Character temp = *opponent_chara;
        opponent_chara = &temp;
#endif
        int index = chara_get_card_index(opponent_chara, card);
        chara_remove_card(opponent_chara, index);
    }
}

/// <summary>51f740</summary>
void Debate::anger()
{
    if (not utils::in_range(angering_, 0, MaxTeamCount - 1))
        return;
#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("anger {}",
            angering_
        ));
    }
#endif
    Character* chara = get_chara(angering_);
    Character* opponent_chara = get_chara(get_opponent_team(angering_));
    int hp_damage;
    int stress_damage;
    chara_set_anger_timer(chara);
    switch (chara_get_seikaku(chara))
    {
    case Seikaku_Shoushin:
        combo_attacker_ = angering_;
        combo_counter_ = 0;
        combo_ = true;
        break;
    case Seikaku_Chototsu:
        hp_damage = 200 + chara_get_strength(chara);
        stress_damage = hp_damage / 15;
        chara_add_hp(opponent_chara, -hp_damage);
        chara_add_stress(opponent_chara, stress_damage);
        if (view_)
            engine_->debate_anger_chotosu(this, angering_, hp_damage, stress_damage);
        break;
    }
}

/// <summary>51f820</summary>
void Debate::combo_attack()
{
    debate_team_t team = combo_attacker_;
    debate_team_t opponent_team = get_opponent_team(team);
    for (int i = 0; i < chara_[team].max_card_count; i++)
    {
        debate_card_t card = chara_get_card(&chara_[team], i);
        if (not utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
            continue;
        int hp_damage = calc_hp_damage(team, card);
        int stress_damage = hp_damage / 15;
        chara_add_hp(&chara_[opponent_team], -hp_damage);
        chara_add_stress(&chara_[opponent_team], stress_damage);
        if (view_)
            engine_->debate_anger_shoushin(this, team, hp_damage, stress_damage, combo_counter_);
        chara_remove_card(&chara_[team], i);
        combo_counter_++;
    }
#if s11_removed
#endif
    combo_ = false;
}

/// <summary>51f950</summary>
bool Debate::calc_winner()
{
    if (winner_ == -1)
    {
        debate_team_t team = first_;
        debate_team_t opponent_team = get_opponent_team(team);
        if (chara_[opponent_team].hp <= 0)
            winner_ = team;
        else if (chara_[team].hp <= 0)
            winner_ = opponent_team;
    }
    if (winner_ != -1)
    {
#if s11_removed
#endif
        return true;
    }
    return false;
}

/// <summary>51f9d0</summary>
bool Debate::can_critical() const
{
    if (winner_ == -1)
        return false;
    if (system_->is_feat_disabled(Feature_DebateCritical))
        return false;
    return chara_[get_opponent_team(winner_)].hp <= -100;
}

/// <summary>51fa30</summary>
void Debate::calc_win_type()
{
    switch (critical_)
    {
    case DebateCritical_HaveMercy:
        win_type_ = DebateWinType_HaveMercy;
        break;
    case DebateCritical_PushOn:
        win_type_ = DebateWinType_PushOn;
        break;
    }
#if s11_removed
#endif
}

/// <summary>5202d0</summary>
void Debate::attack()
{
    if (not utils::in_range(attacker_, 0, MaxTeamCount - 1))
    {
        attack_draw();
        return;
    }
    debate_team_t team = attacker_;
    debate_team_t opponent_team = get_opponent_team(team);
    debate_card_t card;
    int hp_damage;
    int stress_damage;
    card = played_card_[team];
    switch (card)
    {
    case DebateCard_Mushi:
        stress_damage = 30;
        mushi(team, stress_damage);
        break;
    case DebateCard_Kiben:
        card = played_card_[opponent_team];
        if (utils::in_range(card, DebateCard_WadaiFirst, DebateCard_WadaiLast))
        {
            hp_damage = calc_hp_damage(team, card);
            stress_damage = calc_stress_damage(card);
            if (hp_damage > 0)
                wadai(opponent_team, card, hp_damage, stress_damage, true);
        }
        break;
    case DebateCard_Daikatsu:
        hp_damage = calc_hp_damage(team, card);
        stress_damage = calc_stress_damage(card);
        daikatsu(team, hp_damage, stress_damage);
        break;
    default:
        hp_damage = calc_hp_damage(team, card);
        stress_damage = calc_stress_damage(card);
        wadai(team, card, hp_damage, stress_damage, false);
        break;
    }
}

/// <summary>5203a0</summary>
void Debate::effect(debate_team_t team, debate_card_t card)
{
    debate_team_t opponent_team = get_opponent_team(team);
    int stress_damage;
    bool kiben;
    switch (card)
    {
    case DebateCard_Rethink:
        rethink(team);
        break;
    case DebateCard_Chinsei:
        stress_damage = -std::max(get_chara(opponent_team)->stress / 2, 30);
        kiben = played_card_[opponent_team] == DebateCard_Kiben;
        chinsei(team, stress_damage, kiben);
        break;
    case DebateCard_Gyakujou:
        stress_damage = 40;
        kiben = played_card_[opponent_team] == DebateCard_Kiben;
        gyakujou(team, stress_damage, kiben);
        break;
    }
}

/// <summary>520450</summary>
bool Debate::calc_angering(bool can_reflect)
{
    debate_team_t team = first_;
    debate_team_t opponent_team = get_opponent_team(team);
    debate_card_t card = -1;

    angering_ = -1;
    if (chara_[team].anger_timer <= 0 and chara_[team].stress >= MaxStress)
        angering_ = team;
    if (angering_ == -1)
    {
        if (chara_[opponent_team].anger_timer <= 0 and chara_[opponent_team].stress >= MaxStress)
            angering_ = opponent_team;
    }
    // 흥분한 캐릭터 없음
    if (not utils::in_range(angering_, 0, MaxTeamCount - 1))
        return angering_;

    team = angering_;
    opponent_team = get_opponent_team(team);

    if (chara_[opponent_team].anger_timer <= 0)
    {
        if (can_reflect and chara_get_card_index(&chara_[opponent_team], DebateCard_Gyakujou) > 0)
            card = DebateCard_Gyakujou;
        else if (chara_get_card_index(&chara_[opponent_team], DebateCard_Chinsei) > 0)
            card = DebateCard_Chinsei;
    }
    anger_trigger(angering_, card);
    if (card == DebateCard_Gyakujou)
        angering_ = opponent_team;
    else if (card == DebateCard_Chinsei)
        angering_ = -1;
    return angering_;
}

/// <summary>520560</summary>
bool Debate::calc_crumbled()
{
    for (int i = 0; i < MaxTeamCount; i++)
    {
        int level = (MaxHP - chara_[i].hp) / 250;
        level = std::clamp(level, 0, 3);
        if (level == crumbled_level_[i])
            continue;
        crumbled_level_[i] = level;
        crumbled_[i] = true;
    }
    return crumbled_[0] or crumbled_[1];
}

/// <summary>520600, v+4</summary>
bool Debate::init()
{
    // 5201c0
    wadai_ = system_->rand_int(Wadai_Max);
    first_ = -1;
    critical_ = -1;
    winner_ = -1;
    win_type_ = DebateWinType_Normal;
    Person* a_person = param_get_person(param_, 0);
    bool a_control = param_get_control(param_, 0);
    int a_hp = param_get_hp(param_, 0);
    Person* b_person = param_get_person(param_, 1);
    bool b_control = param_get_control(param_, 1);
    int b_hp = param_get_hp(param_, 1);
    if (not chara_init(&chara_[0], a_person, b_person, a_hp, a_control))
        return false;
    if (not chara_init(&chara_[1], b_person, a_person, b_hp, b_control))
        return false;
    for (int i = 0; i < MaxTeamCount; i++)
    {
        ai_init(&ai_[i], this, i);
        crumbled_level_[i] = 0;
        crumbled_[i] = false;
        can_rethink_[i] = true;
    }
    first_ = param_get_challenger(param_);

#if s11_log
    if (Logger* logger = system_->get_logger())
    {
        logger->debug(std::format("Debate::init {}{} {} : {}{} {} 0x{:x}",
            a_person->get_id(),
            a_person->get_name(),
            a_hp,
            b_person->get_id(),
            b_person->get_name(),
            b_hp,
            system_->get_seed()
        ));
    }
#endif
    next_phase_ = DebatePhase_Opening;
    return true;
}

/// <summary>5207c0</summary>
void Debate::effect()
{
    if (not utils::in_range(attacker_, 0, MaxTeamCount - 1))
        return;
    debate_team_t team = attacker_;
    debate_card_t card = played_card_[team];
    debate_team_t opponent_team = get_opponent_team(team);
    debate_card_t opponent_card = played_card_[opponent_team];
    effect(team, card);
    if (card == DebateCard_Mushi)
        return;
    effect(opponent_team, opponent_card);
}

/// <summary>520f70</summary>
bool Debate::run()
{
    bool view = false;
    s11_wip;
    if (param_->chara[0].control or param_->chara[1].control)
    {
        Message msg;
        msg.set_obj0_obj1(N_DEB_DEBATE_KAKUNIN, param_->chara[0].person, param_->chara[1].person);
        view = engine_->yes_no(system_->get_msg(&msg));
        if (not view)
            engine_ = nullptr;
    }
#if s11_suspicious(3)
    std::array<seikaku_t, 2> seikaku;
    if (not view)
    {
        seikaku[0] = system_->person_swap_seikaku(param_->chara[0].person, Seikaku_Reisei);
        seikaku[1] = system_->person_swap_seikaku(param_->chara[1].person, Seikaku_Reisei);
    }
#endif
    init();
    while (update(0))
    {
    }
#if s11_suspicious(3)
    if (not view)
    {
        system_->person_swap_seikaku(param_->chara[0].person, seikaku[0]);
        system_->person_swap_seikaku(param_->chara[1].person, seikaku[1]);
    }
#endif
    return view;
}

/// <summary>682780, v+8</summary>
bool Debate::is_tutorial() const
{
    return false;
}

s11_end_namespace